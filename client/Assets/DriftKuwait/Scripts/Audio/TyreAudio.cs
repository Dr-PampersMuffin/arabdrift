using UnityEngine;

namespace DriftKuwait
{
    /// Tyre squeal and burnout roar. Uses ElevenLabs loops if present in
    /// Resources/DriftKuwait/Audio (tyre_squeal_loop, tyre_burnout_loop, sand_roost_loop),
    /// otherwise a procedural squeal.
    [RequireComponent(typeof(DriftCarController))]
    public class TyreAudio : MonoBehaviour
    {
        DriftCarController car;
        AudioSource squeal, burnout, sand, synth;
        volatile float aSlip, aSpin;
        int sr;
        double ph1, ph2;
        float bp1, bp2, lowN;
        uint rng = 987654u;

        void Start()
        {
            car = GetComponent<DriftCarController>();
            sr = AudioSettings.outputSampleRate;
            var sq = Resources.Load<AudioClip>("DriftKuwait/Audio/tyre_squeal_loop");
            var bo = Resources.Load<AudioClip>("DriftKuwait/Audio/tyre_burnout_loop");
            var sd = Resources.Load<AudioClip>("DriftKuwait/Audio/sand_roost_loop");
            if (sq != null) squeal = Loop(sq);
            if (bo != null) burnout = Loop(bo);
            if (sd != null) sand = Loop(sd);
            if (sq == null) synth = Loop(AudioClip.Create("TyreSynth", sr, 1, sr, true, OnRead));
        }

        AudioSource Loop(AudioClip c)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.volume = 0f; s.spatialBlend = 0.5f; s.dopplerLevel = 0f;
            s.Play();
            return s;
        }

        void Update()
        {
            bool onSand = false;
            foreach (var w in car.wheels)
                if (w.grounded && w.surface != null && w.surface.GetComponent<SurfaceType>() is SurfaceType st && st.grip < 0.8f) onSand = true;

            float slip = onSand ? 0f : car.RearSlip;
            float spin = Mathf.Clamp01(car.RearSpin / 8f) * (car.SpeedKmh < 30f ? 1f : 0.4f);
            aSlip = slip; aSpin = onSand ? 0f : spin;

            if (squeal != null) { squeal.volume = Mathf.Lerp(squeal.volume, slip * 0.9f, 0.2f); squeal.pitch = 0.9f + slip * 0.2f; }
            if (burnout != null) burnout.volume = Mathf.Lerp(burnout.volume, aSpin * 0.9f, 0.2f);
            if (sand != null) sand.volume = Mathf.Lerp(sand.volume, onSand ? Mathf.Clamp01(car.RearSlip + car.SpeedKmh / 80f) * 0.8f : 0f, 0.1f);
            if (synth != null) synth.volume = 1f;
        }

        float Noise()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }

        void OnRead(float[] data)
        {
            float slip = aSlip, spin = aSpin;
            for (int i = 0; i < data.Length; i++)
            {
                float n = Noise();
                // Squeal: two wobbling resonances over band-passed noise
                ph1 += (950.0 + 60.0 * System.Math.Sin(ph2 * 6.283)) / sr;
                ph2 += 7.0 / sr;
                if (ph1 > 1.0) ph1 -= 1.0;
                if (ph2 > 1.0) ph2 -= 1.0;
                bp1 += (n - bp1) * 0.35f;
                bp2 += (bp1 - bp2) * 0.35f;
                float band = bp1 - bp2;
                float tone = Mathf.Sin((float)ph1 * Mathf.PI * 2f);
                float sq = (tone * 0.5f + band * 0.8f) * slip * 0.22f;
                // Burnout: low roaring noise
                lowN += (n - lowN) * 0.05f;
                float roar = lowN * spin * 0.9f;
                data[i] = Mathf.Clamp(sq + roar, -1f, 1f);
            }
        }
    }
}
