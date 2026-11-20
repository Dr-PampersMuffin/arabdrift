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

        static readonly Color AsphaltSmoke = new Color(0.92f, 0.91f, 0.89f, 0.55f);
        static readonly Color SandDust = new Color(0.80f, 0.66f, 0.46f, 0.6f);

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
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        ParticleSystem MakeSmoke(float scale)
        {
            var go = new GameObject("TyreSmoke");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f * scale, 2.0f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = AsphaltSmoke;
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 450;
            main.playOnAwake = true;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.25f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3.4f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial("M_Smoke", TextureKit.Smoke, Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;

            ps.Play();
            return ps;
        }

        void LateUpdate()
        {
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var w = car.wheels[i];
                var ps = smoke[i];
                ps.transform.position = w.grounded ? w.contactPoint + Vector3.up * 0.25f : transform.position;

                bool sandy = w.surface != null && w.surface.GetComponent<SurfaceType>() is SurfaceType st && st.grip < 0.8f;
                var main = ps.main;
                main.startColor = sandy ? SandDust : AsphaltSmoke;

                var em = ps.emission;
                float k = w.grounded ? w.skid : 0f;
                em.rateOverTime = k * k * (w.front ? 18f : 55f);

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
