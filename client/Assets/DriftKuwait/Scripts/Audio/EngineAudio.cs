using UnityEngine;

namespace DriftKuwait
{
    /// Engine sound. Two modes:
    ///  1) Clip mode: if ElevenLabs loops exist in Resources/DriftKuwait/Audio (shaheen_idle,
    ///     shaheen_2000, shaheen_4000, shaheen_6000), they are crossfaded and pitched by RPM.
    ///  2) Synth mode (default until the loops are generated): a turbo-four built from
    ///     firing pulses, exhaust filtering, intake noise and a turbo whistle.
    [RequireComponent(typeof(DriftCarController))]
    public class EngineAudio : MonoBehaviour
    {
        static readonly string[] ClipKeys = { "shaheen_idle", "shaheen_2000", "shaheen_4000", "shaheen_6000" };
        static readonly float[] ClipRpm = { 900f, 2000f, 4000f, 6000f };

        DriftCarController car;
        AudioSource[] layers, rasp, sub;
        float load;   // smoothed engine load (throttle with a little lag, like a real intake)
        AudioSource synthSource, blowoff, limiterPop, shiftSrc;

        // Values handed to the audio thread
        volatile float aRpm = 900f, aLoad, aBoost;
        volatile bool aCut;
        int sampleRate;
        double phase, whistlePhase;
        float lp1, lp2, noiseLp, lastBoost;
        uint rng = 22222u;

        void Start()
        {
            car = GetComponent<DriftCarController>();
            sampleRate = AudioSettings.outputSampleRate;

            var clips = new AudioClip[ClipKeys.Length];
            bool all = true;
            for (int i = 0; i < ClipKeys.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>("DriftKuwait/Audio/" + ClipKeys[i]);
                all &= clips[i] != null;
            }

            if (all)
            {
                layers = new AudioSource[clips.Length];
                for (int i = 0; i < clips.Length; i++) layers[i] = MakeSource(clips[i], true, 0f);
                // Heavier engine: a header/exhaust rasp layer (high-passed, overdriven) that opens up on load and high
                // rpm, and a sub-bass layer that gives the chest thump at idle and on the overrun.
                var r = new AudioClip[ClipKeys.Length]; var b = new AudioClip[ClipKeys.Length]; bool extra = true;
                for (int i = 0; i < ClipKeys.Length; i++)
                {
                    r[i] = Resources.Load<AudioClip>("DriftKuwait/Audio/" + ClipKeys[i] + "_rasp");
                    b[i] = Resources.Load<AudioClip>("DriftKuwait/Audio/" + ClipKeys[i] + "_sub");
                    extra &= r[i] != null && b[i] != null;
                }
                if (extra)
                {
                    rasp = new AudioSource[r.Length]; sub = new AudioSource[b.Length];
                    for (int i = 0; i < r.Length; i++)
                    {
                        rasp[i] = MakeSource(r[i], true, 0f); sub[i] = MakeSource(b[i], true, 0f);
                        // start all three in step so the firing pulses line up
                        rasp[i].timeSamples = layers[i].timeSamples; sub[i].timeSamples = layers[i].timeSamples;
                    }
                }
            }
            else
            {
                var clip = AudioClip.Create("ShaheenSynth", sampleRate, 1, sampleRate, true, OnRead);
                synthSource = MakeSource(clip, true, 0.9f);
            }

            var bo = Resources.Load<AudioClip>("DriftKuwait/Audio/shaheen_blowoff");
            if (bo != null) blowoff = MakeSource(bo, false, 0.8f);
            // Manual shifts: a gearbox clunk, and a blow-off on the way up
            var sh = Resources.Load<AudioClip>("DriftKuwait/Audio/shaheen_shift");
            if (sh != null)
            {
                shiftSrc = MakeSource(sh, false, 0.7f);
                car.Shifted += up => { shiftSrc.volume = 0.7f * Sfx.SfxVolume; shiftSrc.Play(); if (up && blowoff != null && car.Boost > 0.3f) blowoff.Play(); };
            }
        }

        AudioSource MakeSource(AudioClip clip, bool loop, float vol)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol;
            s.spatialBlend = 0.6f; s.dopplerLevel = 0.2f; s.minDistance = 4f; s.maxDistance = 120f;
            s.playOnAwake = false;
            if (loop) s.Play();
            return s;
        }

        void Update()
        {
            aRpm = car.Rpm;
            aLoad = car.ThrottleInput;
            aBoost = car.Boost;
            aCut = car.LimiterCut;

            // Turbo blow-off when lifting off on boost
            if (blowoff != null && lastBoost > 0.6f && car.ThrottleInput < 0.1f) blowoff.Play();
            lastBoost = car.ThrottleInput > 0.1f ? car.Boost : 0f;

            if (layers == null) return;
            float rpm = car.Rpm;
            load = Mathf.MoveTowards(load, car.ThrottleInput, Time.deltaTime * (car.ThrottleInput > load ? 6f : 3.5f));
            float cut = car.LimiterCut ? 0.4f : 1f;
            float rasping = (0.12f + 0.95f * load * Mathf.Clamp01((rpm - 1500f) / 3500f)) * cut;   // headers scream on load
            float thump = Mathf.Lerp(0.6f, 1f, load);                                             // sub stays on the overrun
            for (int i = 0; i < layers.Length; i++)
            {
                float lo = i > 0 ? ClipRpm[i - 1] : 0f;
                float hi = i < layers.Length - 1 ? ClipRpm[i + 1] : 99999f;
                float c = ClipRpm[i];
                float wgt = rpm < c ? Mathf.InverseLerp(lo, c, rpm) : 1f - Mathf.InverseLerp(c, hi, rpm);
                if (i == 0 && rpm < c) wgt = 1f;
                if (i == layers.Length - 1 && rpm > c) wgt = 1f;
                wgt = Mathf.Clamp01(wgt);
                float pitch = Mathf.Clamp(rpm / c, 0.6f, 1.6f);
                layers[i].volume = wgt * Mathf.Lerp(0.72f, 1f, load) * cut;   // louder off-throttle burble too
                layers[i].pitch = pitch;
                if (rasp != null)
                {
                    rasp[i].volume = wgt * rasping; rasp[i].pitch = pitch;
                    sub[i].volume = wgt * thump * cut; sub[i].pitch = pitch;
                }
            }
        }

        float Noise()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }

        // Audio thread
        void OnRead(float[] data)
        {
            float rpm = aRpm, load = aLoad, boost = aBoost;
            bool cut = aCut;
            float f0 = rpm / 30f;                         // four-cylinder firing frequency
            float cutoff = Mathf.Lerp(0.04f, 0.22f, Mathf.Clamp01(rpm / 7000f)) * (0.6f + 0.4f * load);
            float whistleHz = 1800f + boost * 3200f;
            float amp = 0.28f * (0.45f + 0.55f * load) * (cut ? 0.15f : 1f);

            for (int i = 0; i < data.Length; i++)
            {
                phase += f0 / sampleRate;
                if (phase > 1.0) phase -= 1.0;
                float p = (float)phase;

                // Firing pulse: sharp attack, exponential decay, plus harmonic body
                float pulse = Mathf.Exp(-p * 9f) * 2f - 0.6f;
                float body = Mathf.Sin(p * Mathf.PI * 2f) * 0.5f + Mathf.Sin(p * Mathf.PI * 4f) * 0.35f + Mathf.Sin(p * Mathf.PI * 6f) * 0.2f;
                float crackle = Noise() * Mathf.Exp(-p * 14f) * (0.25f + 0.75f * load);

                float raw = pulse * 0.6f + body * 0.5f + crackle * 0.5f;
                lp1 += (raw - lp1) * cutoff;              // exhaust pipe / muffler
                lp2 += (lp1 - lp2) * (cutoff * 1.4f);

                noiseLp += (Noise() - noiseLp) * 0.3f;    // intake roar
                float intake = noiseLp * 0.12f * load;

                whistlePhase += whistleHz / sampleRate;
                if (whistlePhase > 1.0) whistlePhase -= 1.0;
                float whistle = Mathf.Sin((float)whistlePhase * Mathf.PI * 2f) * 0.035f * boost * load;

                data[i] = Mathf.Clamp((lp2 * 1.8f + intake + whistle) * amp * 2.2f, -1f, 1f);
            }
        }
    }
}
