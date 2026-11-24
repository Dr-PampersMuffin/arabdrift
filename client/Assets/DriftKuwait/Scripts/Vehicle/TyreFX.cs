using UnityEngine;
using UnityEngine.Rendering;

namespace DriftKuwait
{
    /// Tyre smoke (white on asphalt, tan dust on sand) and persistent skid marks.
    [RequireComponent(typeof(DriftCarController))]
    public class TyreFX : MonoBehaviour
    {
        DriftCarController car;
        ParticleSystem[] smoke;
        int[] lastSkid;
        Vector3[] lastSkidPos;

        static readonly Color AsphaltSmoke = new Color(0.94f, 0.93f, 0.91f, 0.42f);
        static readonly Color SandDust = new Color(0.80f, 0.68f, 0.50f, 0.28f);

        void Start()
        {
            car = GetComponent<DriftCarController>();
            int n = car.wheels.Length;
            smoke = new ParticleSystem[n];
            lastSkid = new int[n];
            lastSkidPos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                lastSkid[i] = -1;
                smoke[i] = MakeSmoke(car.wheels[i].front ? 0.35f : 1f);
            }
            SkidMarks.Ensure();
        }

        public static Material ParticleMaterial(string key, Texture tex, Color tint)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = key };
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// Lit, soft smoke: street lamps and the sun light it (blueprint: "lit by streetlights"), and it fades into the road.
        public static Material SmokeMaterial()
        {
            var tex = Resources.Load<Texture2D>("DriftKuwait/FX/smoke_puff");
            // Unlit, tinted by time of day (see LateUpdate): lit particles read as dark soot at night on phones.
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null || tex == null) return ParticleMaterial("M_Smoke", TextureKit.Smoke, Color.white);
            var m = new Material(sh) { name = "M_Smoke" };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.93f, 0.92f, 0.90f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // No soft particles / camera fade: both depend on depth data that some devices and capture paths do not
            // provide, and with it missing the smoke renders fully transparent. Puffs spawn above the road instead.
            m.SetFloat("_SoftParticlesEnabled", 0f);
            m.SetFloat("_CameraFadingEnabled", 0f);
            m.DisableKeyword("_SOFTPARTICLES_ON");
            m.DisableKeyword("_FADING_ON");
            m.SetFloat("_Smoothness", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Material smokeMat;

        ParticleSystem MakeSmoke(float scale)
        {
            var go = new GameObject("TyreSmoke");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);     // a thick billowing cloud that hangs behind the car (the camera stays clear: see maxParticleSize)
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f * scale, 1.8f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = AsphaltSmoke;
            main.gravityModifier = -0.012f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 320;
            main.playOnAwake = true;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.25f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.6f), new Keyframe(1f, 2.8f)));

            var inherit = ps.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new ParticleSystem.MinMaxCurve(0.3f);
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.06f;
            limit.limit = new ParticleSystem.MinMaxCurve(1.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.78f, 0.06f), new GradientAlphaKey(0.32f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (smokeMat == null) smokeMat = SmokeMaterial();
            r.sharedMaterial = smokeMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.minParticleSize = 0f;
            r.maxParticleSize = 0.38f;   // fraction of the screen: a puff next to the camera can never blind the player

            ps.Play();
            return ps;
        }

        static bool smokeNight;
        static bool smokeTinted;

        void LateUpdate()
        {
            if (smokeMat != null && (!smokeTinted || smokeNight != Atmosphere.IsNight))
            {
                smokeTinted = true;
                smokeNight = Atmosphere.IsNight;
                // Day: warm sunlit white. Night: sodium-washed grey, like smoke under the street lamps.
                smokeMat.SetColor("_BaseColor", smokeNight ? new Color(0.62f, 0.52f, 0.42f, 0.9f) : new Color(0.95f, 0.91f, 0.85f, 1f));
            }
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var w = car.wheels[i];
                var ps = smoke[i];
                ps.transform.position = w.grounded ? w.contactPoint + Vector3.up * 0.45f : transform.position;

                bool sandy = w.surface != null && w.surface.GetComponent<SurfaceType>() is SurfaceType st && st.grip < 0.8f;
                var main = ps.main;
                main.startColor = sandy ? SandDust : AsphaltSmoke;

                var em = ps.emission;
                float k = w.grounded ? w.skid : 0f;
                float rate = k * k * (w.front ? 3f : 44f) + (w.front ? 0f : car.RearSpin * 2.4f * (w.grounded ? 1f : 0f));   // both rear tyres burn
                rate *= Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(5f, 45f, car.SpeedKmh));   // standing burnouts stay modest
                if (sandy) rate *= 0.5f;
                em.rateOverTime = rate * Mathf.Lerp(0.25f, 1.4f, GameSettings.Smoke);

                // Skid marks only on hard surfaces (sand gets tracks later)
                if (w.grounded && w.skid > 0.3f && !sandy)
                {
                    if (lastSkid[i] < 0 || (w.contactPoint - lastSkidPos[i]).sqrMagnitude > 0.09f)
                    {
                        lastSkid[i] = SkidMarks.Instance.Add(w.contactPoint, w.contactNormal, Mathf.Clamp01(w.skid), lastSkid[i]);
                        lastSkidPos[i] = w.contactPoint;
                    }
                }
                else lastSkid[i] = -1;
            }
        }
    }

}
