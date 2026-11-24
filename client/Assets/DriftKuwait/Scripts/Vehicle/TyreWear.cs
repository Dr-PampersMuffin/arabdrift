using UnityEngine;

namespace DriftKuwait
{
    /// Rear tyre heat and wear. Long burnouts and hard slides heat the rubber: the smoke turns thick and grey, bits of
    /// rubber fly off, grip fades. Keep cooking a tyre that is already shredded and it blows: a bang, the bare rim
    /// grinds sparks (about half a minute of non-stop burnout at full heat; a short one never does it)
        /// on the asphalt and that corner loses most of its grip. A reset, the pause-menu recovery or the
    /// garage fits fresh tyres.
    [RequireComponent(typeof(DriftCarController))]
    public class TyreWear : MonoBehaviour
    {
        public static TyreWear Current { get; private set; }
        DriftCarController car;
        public readonly float[] heat = new float[4], health = { 1f, 1f, 1f, 1f };
        readonly bool[] blown = new bool[4];
        ParticleSystem[] marbles = new ParticleSystem[4], rimSparks = new ParticleSystem[4];
        AudioSource bang, grind, flap;
        bool warned;

        public float Heat(int i) => heat[i];
        public bool Blown(int i) => blown[i];

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        void Start()
        {
            car = GetComponent<DriftCarController>();
            for (int i = 0; i < car.wheels.Length && i < 4; i++)
            {
                if (car.wheels[i].front) continue;
                marbles[i] = Marbles();
                rimSparks[i] = RimSparks();
            }
            bang = Src("tyre_blowout", false); grind = Src("rim_grind", true); flap = Src("tyre_shred_flap", true);
        }

        AudioSource Src(string key, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>("DriftKuwait/Audio/" + key);
            s.loop = loop; s.volume = 0f; s.spatialBlend = 0.5f; s.playOnAwake = false;
            if (loop && s.clip != null) s.Play();
            return s;
        }

        ParticleSystem Marbles()
        {
            var go = new GameObject("RubberBits"); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            m.startColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            m.gravityModifier = 1.6f; m.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0.3f; col.dampen = 0.5f; col.quality = ParticleSystemCollisionQuality.Low;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TyreFX.ParticleMaterial("DK_Rubber", Texture2D.whiteTexture, new Color(0.08f, 0.08f, 0.08f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        ParticleSystem RimSparks()
        {
            var go = new GameObject("RimSparks"); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.12f));
            m.gravityModifier = 1.2f; m.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f;
            var inh = ps.inheritVelocity; inh.enabled = true; inh.mode = ParticleSystemInheritVelocityMode.Initial; inh.curve = new ParticleSystem.MinMaxCurve(0.5f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.6f;
            r.sharedMaterial = TyreFX.ParticleMaterial("DK_Sparks", Texture2D.whiteTexture, new Color(1f, 0.85f, 0.4f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public void Refit()
        {
            for (int i = 0; i < 4; i++) { heat[i] = 0f; health[i] = 1f; blown[i] = false; if (i < car.wheels.Length) car.wheels[i].gripMul = 1f; }
            warned = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || car == null) return;
            float spin = Mathf.Clamp01(car.RearSpin / 8f);
            float anyBlown = 0f, anyShred = 0f;
            for (int i = 0; i < car.wheels.Length && i < 4; i++)
            {
                var w = car.wheels[i];
                if (w.front) continue;
                bool sandy = w.surface != null && w.surface.GetComponent<SurfaceType>() is SurfaceType st && st.grip < 0.8f;
                // heating: wheelspin cooks the rubber fastest; a slide heats it too; rolling and air cool it
                float work = w.grounded && !sandy ? spin * 0.22f + w.skid * 0.07f : 0f;
                heat[i] = Mathf.Clamp01(heat[i] + work * dt - (0.05f + car.SpeedKmh * 0.0008f) * dt * (work < 0.02f ? 1f : 0.3f));
                // wear only once the tyre is really hot (a long burnout or a very long slide)
                if (heat[i] > 0.8f && work > 0.05f) health[i] = Mathf.Max(0f, health[i] - (heat[i] - 0.75f) * work * 0.55f * dt);
                if (!blown[i] && health[i] <= 0f) Blow(i);

                float shred = 1f - health[i];
                w.gripMul = blown[i] ? 0.35f : Mathf.Lerp(1f, 0.8f, shred);
                if (marbles[i] != null)
                {
                    marbles[i].transform.position = w.contactPoint + Vector3.up * 0.2f;
                    var em = marbles[i].emission; em.rateOverTime = w.grounded && !blown[i] ? Mathf.Clamp01((heat[i] - 0.55f) * 2.5f) * work * 900f : 0f;
                }
                if (rimSparks[i] != null)
                {
                    rimSparks[i].transform.position = w.grounded ? w.contactPoint + Vector3.up * 0.05f : transform.position;
                    var em = rimSparks[i].emission; em.rateOverTime = blown[i] && w.grounded ? Mathf.Clamp01(car.SpeedKmh / 25f + spin) * 260f : 0f;
                }
                if (blown[i]) anyBlown = Mathf.Max(anyBlown, Mathf.Clamp01(car.SpeedKmh / 20f + spin));
                else anyShred = Mathf.Max(anyShred, Mathf.Clamp01((shred - 0.5f) * 2f) * Mathf.Clamp01(car.SpeedKmh / 20f));

                if (!warned && health[i] < 0.35f && !blown[i])
                {
                    warned = true;
                    var hud = Object.FindAnyObjectByType<DriveHUD>();
                    if (hud != null) hud.Callout("الكفر بينفجر! خفّف", UIKit.Gold);
                }
            }
            float vol = Sfx.SfxVolume;
            if (grind.clip != null) { grind.volume = Mathf.Lerp(grind.volume, anyBlown * 0.85f * vol, 0.2f); grind.pitch = 0.85f + Mathf.Clamp01(car.SpeedKmh / 80f) * 0.4f; }
            if (flap.clip != null) { flap.volume = Mathf.Lerp(flap.volume, Mathf.Max(anyShred * 0.5f, anyBlown * 0.6f) * vol, 0.2f); flap.pitch = 0.8f + Mathf.Clamp01(car.SpeedKmh / 60f) * 0.7f; }
        }

        void Blow(int i)
        {
            blown[i] = true;
            if (bang.clip != null) bang.PlayOneShot(bang.clip, 1f * Sfx.SfxVolume);
            var hud = Object.FindAnyObjectByType<DriveHUD>();
            if (hud != null) hud.Callout("انفجر الكفر!", new Color(1f, 0.45f, 0.3f));
            if (marbles[i] != null) marbles[i].Emit(60);
            Debug.Log($"[TyreWear] blowout wheel {i}");
        }
    }
}
