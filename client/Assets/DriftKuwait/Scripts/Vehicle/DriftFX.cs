using UnityEngine;

namespace DriftKuwait
{
    /// The street-drift show on the player's car:
    ///  - exhaust flames and pops when lifting off high in the revs, on every rev-limiter bounce and on upshifts
    ///  - sparks and a metal scrape when the exhaust drags on the road (bottoming out, or a lowered car in a hard slide)
    ///  - a high tyre scream that rises with the slide, and the roar of both rear tyres burning in a burnout
    public class DriftFX : MonoBehaviour
    {
        DriftCarController car;
        ParticleSystem flame, sparks;
        Light flash;
        AudioSource pops, limiter, scrape, scream, burn;
        Vector3 exhaustLocal;
        float lastThrottle, flashT, popCooldown, scrapeLevel;
        bool lastCut;

        void Start()
        {
            car = GetComponent<DriftCarController>();
            // Exhaust tip: rear right, low, at the back of the body
            var b = LocalBounds();
            exhaustLocal = new Vector3(b.center.x + b.extents.x * 0.45f, Mathf.Max(0.22f, b.min.y + 0.18f), b.min.z + 0.05f);

            flame = MakeFlame();
            sparks = MakeSparks();
            flash = new GameObject("ExhaustFlash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.transform.localPosition = exhaustLocal + Vector3.back * 0.4f;
            flash.type = LightType.Point; flash.color = new Color(1f, 0.55f, 0.2f); flash.range = 5f; flash.intensity = 0f; flash.shadows = LightShadows.None;

            pops = Src("exhaust_pops", false, 0.9f);
            limiter = Src("rev_limiter", true, 0f);
            scrape = Src("exhaust_scrape", true, 0f);
            scream = Src("tyre_squeal_high", true, 0f);
            burn = Src("tyre_burst_smoke", true, 0f);
            car.Shifted += up => { if (up && car.Rpm > 4500f) Backfire(5, 0.6f); };
        }

        Bounds LocalBounds()
        {
            var body = transform.Find("Body");
            var rs = (body != null ? body : transform).GetComponentsInChildren<MeshRenderer>();
            bool any = false; Bounds b = new Bounds(Vector3.zero, new Vector3(1.7f, 1.3f, 4.4f));
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                // corners into the car's space
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        AudioSource Src(string key, bool loop, float vol)
        {
            var clip = Resources.Load<AudioClip>("DriftKuwait/Audio/" + key);
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol * Sfx.SfxVolume; s.spatialBlend = 0.5f; s.dopplerLevel = 0f;
            s.minDistance = 5f; s.maxDistance = 120f; s.playOnAwake = false;
            if (loop && clip != null) s.Play();
            return s;
        }

        ParticleSystem MakeFlame()
        {
            var go = new GameObject("ExhaustFlame");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = exhaustLocal;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.04f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 0.7f, 1f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.25f), 0.25f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TyreFX.ParticleMaterial("DK_ExhaustFlame", Resources.Load<Texture2D>("DriftKuwait/FX/smoke_puff") ?? Texture2D.whiteTexture, new Color(1f, 0.8f, 0.5f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        ParticleSystem MakeSparks()
        {
            var go = new GameObject("ExhaustSparks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(exhaustLocal.x, 0.05f, exhaustLocal.z + 0.4f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.15f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f;
            var inh = ps.inheritVelocity; inh.enabled = true; inh.mode = ParticleSystemInheritVelocityMode.Initial; inh.curve = new ParticleSystem.MinMaxCurve(0.6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f;
            r.sharedMaterial = TyreFX.ParticleMaterial("DK_Sparks", Texture2D.whiteTexture, new Color(1f, 0.85f, 0.4f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Backfire(int count, float vol)
        {
            if (popCooldown > 0f) return;
            popCooldown = 0.12f;
            flame.Emit(count);
            flashT = 0.07f;
            if (pops.clip != null) { pops.pitch = Random.Range(0.9f, 1.15f); pops.volume = vol * Sfx.SfxVolume; pops.Play(); }
        }

        void Update()
        {
            if (car == null) return;
            float dt = Time.deltaTime;
            popCooldown -= dt;

            // Flames: lift-off high in the revs, and every limiter bounce
            float thr = car.ThrottleInput;
            if (lastThrottle > 0.6f && thr < 0.15f && car.Rpm > 4200f) Backfire(Random.Range(5, 10), 0.95f);
            if (car.LimiterCut && !lastCut) Backfire(3, 0.5f);
            lastCut = car.LimiterCut;
            lastThrottle = thr;
            if (limiter.clip != null) limiter.volume = Mathf.MoveTowards(limiter.volume, (car.Rpm >= car.maxRpm - 60f && thr > 0.5f ? 0.75f : 0f) * Sfx.SfxVolume, dt * 6f);
            flashT -= dt;
            flash.intensity = flashT > 0f ? Random.Range(3f, 6f) : 0f;

            // Exhaust dragging: bottoming out over bumps, or a lowered car hard sideways
            float bottom = 0f;
            for (int i = 2; i < 4; i++)
            {
                var w = car.GetWheel(i);
                if (w.grounded) bottom = Mathf.Max(bottom, Mathf.InverseLerp(car.restLength * 0.7f, car.restLength * 0.95f, w.compression));
            }
            float lowered = Mathf.InverseLerp(0.3f, 0.22f, car.restLength);   // stock 0.30 m; coilovers drop it
            float slideDrag = Mathf.Abs(car.DriftAngle) > 25f && car.SpeedKmh > 35f
                ? (0.25f + 0.75f * lowered) * Mathf.Clamp01(Mathf.PerlinNoise(Time.time * 3.1f, 0.7f) * 1.6f - 0.55f) : 0f;
            float target = Mathf.Max(bottom, slideDrag) * (car.GroundedCount >= 3 ? 1f : 0f);
            scrapeLevel = Mathf.MoveTowards(scrapeLevel, target, dt * 8f);
            var sem = sparks.emission; sem.rateOverTime = scrapeLevel * 320f * Mathf.Clamp01(car.SpeedKmh / 30f);
            if (scrape.clip != null) scrape.volume = scrapeLevel * 0.8f * Sfx.SfxVolume;
            if (scrapeLevel > 0.3f && flashT <= 0f) flash.intensity = Random.Range(0.8f, 2f) * scrapeLevel;

            // Tyres: the high scream rises with the slide; both rear tyres roar in a burnout
            float slip = Mathf.Clamp01(car.RearSlip) * Mathf.Clamp01(car.SpeedKmh / 25f);
            if (scream.clip != null)
            {
                scream.volume = Mathf.MoveTowards(scream.volume, slip * 0.85f * Sfx.SfxVolume, dt * 3f);
                scream.pitch = 0.92f + Mathf.Clamp01(Mathf.Abs(car.DriftAngle) / 50f) * 0.25f + Mathf.Clamp01(car.SpeedKmh / 140f) * 0.1f;
            }
            float spin = Mathf.Clamp01(car.RearSpin / 6f);
            if (burn.clip != null) burn.volume = Mathf.MoveTowards(burn.volume, spin * 0.8f * Sfx.SfxVolume, dt * 3f);
        }
    }
}
