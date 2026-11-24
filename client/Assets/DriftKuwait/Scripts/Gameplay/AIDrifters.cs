using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Local guys drifting round the roundabouts in free roam: proof that the moves are doable and a live example of how
    /// a drift looks (the nose tucked into the circle, rear tyres smoking, a steady angle). Scripted on a circle like a
    /// real donut session, solid to the player and to traffic; they stop to let you through if you get in their way.
    public class AIDrifters : MonoBehaviour
    {
        public DriftCarController player;
        public DriveHUD hud;

        class Drifter
        {
            public GameObject go;
            public DisplayWheels wheels;
            public Vector3 c;
            public float r, ang, dir, speed, angle, phase, v;
            public ParticleSystem[] smoke;
            public AudioSource squeal;
        }

        readonly List<Drifter> drifters = new List<Drifter>();
        static readonly string[] Ids = { "faris", "ghazal", "wanait" };
        bool told;
        int groundMask;

        void Start()
        {
            groundMask = ~((1 << 2) | (1 << TrafficSystem.TrafficLayer));
            int i = 0;
            foreach (var (c, r) in SalwaBlockBuilder.Islands)
            {
                if (i >= 2) break;
                var go = CarFactory.BuildDisplay(Ids[i % Ids.Length], transform);
                if (go == null) continue;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = TrafficSystem.TrafficLayer;
                var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
                var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.8f, 1.4f, 4.5f);
                var d = new Drifter
                {
                    go = go, wheels = go.GetComponent<DisplayWheels>(), c = new Vector3(c.x, 0f, c.z), r = r + 8f,
                    ang = Random.value * Mathf.PI * 2f, dir = 1f, speed = 11.5f + i * 1.2f, angle = 36f + i * 6f, phase = Random.value * 10f,
                };
                d.smoke = new ParticleSystem[2];
                for (int k = 0; k < 2; k++) d.smoke[k] = Smoke(go.transform, new Vector3(k == 0 ? -0.8f : 0.8f, 0.25f, -1.4f));
                d.squeal = go.AddComponent<AudioSource>();
                d.squeal.clip = Resources.Load<AudioClip>("DriftKuwait/Audio/tyre_squeal_loop");
                d.squeal.loop = true; d.squeal.spatialBlend = 1f; d.squeal.minDistance = 6f; d.squeal.maxDistance = 70f;
                d.squeal.rolloffMode = AudioRolloffMode.Linear; d.squeal.volume = 0.45f * GameSettings.Effects;
                if (d.squeal.clip != null) d.squeal.Play();
                drifters.Add(d);
                i++;
            }
        }

        static ParticleSystem Smoke(Transform parent, Vector3 local)
        {
            var go = new GameObject("DrifterSmoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startColor = new Color(0.94f, 0.93f, 0.91f, 0.38f);
            main.maxParticles = 220;
            main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = 26f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = TyreFX.SmokeMaterial();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pp = player != null ? player.transform.position : new Vector3(9999f, 0f, 9999f);
            foreach (var d in drifters)
            {
                if (d.go == null) continue;
                // Let the player through: ease off when he is in the way, then carry on
                Vector3 here = d.go.transform.position;
                float toPlayer = Vector3.Distance(new Vector3(here.x, 0f, here.z), new Vector3(pp.x, 0f, pp.z));
                Vector3 ahead = d.go.transform.position + d.go.transform.forward * 4f;
                bool blocked = toPlayer < 7f || (Vector3.Distance(ahead, pp) < 6f);
                float want = blocked ? 0f : d.speed * (0.92f + 0.08f * Mathf.Sin(Time.time * 0.7f + d.phase));
                d.v = Mathf.MoveTowards(d.v, want, (blocked ? 14f : 4f) * dt);
                float v = d.v;

                d.ang += d.dir * v / d.r * dt;
                Vector3 radial = new Vector3(Mathf.Cos(d.ang), 0f, Mathf.Sin(d.ang));
                Vector3 tangent = new Vector3(-Mathf.Sin(d.ang), 0f, Mathf.Cos(d.ang)) * d.dir;
                Vector3 pos = d.c + radial * d.r;
                if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.8f)
                    pos.y = hit.point.y + 0.02f;
                // Oversteer: the nose tucked into the circle by the drift angle (with a little wobble, like a real driver)
                float a = (d.angle + Mathf.Sin(Time.time * 1.9f + d.phase) * 5f) * Mathf.Clamp01(v / 6f);
                Vector3 nose = Quaternion.AngleAxis(-a * d.dir, Vector3.up) * tangent;
                d.go.GetComponent<Rigidbody>().MovePosition(pos);
                d.go.transform.rotation = Quaternion.Slerp(d.go.transform.rotation, Quaternion.LookRotation(nose), 1f - Mathf.Exp(-6f * dt));
                if (d.wheels != null) d.wheels.Roll(v * 1.25f * dt);   // rear tyres spinning faster than the road
                bool sliding = v > 5f;
                foreach (var s in d.smoke) { var em = s.emission; em.rateOverTime = sliding ? 26f * Mathf.Lerp(0.3f, 1.2f, GameSettings.Smoke) : 0f; }
                if (d.squeal != null) d.squeal.volume = (sliding ? 0.45f : 0f) * GameSettings.Effects;

                if (!told && hud != null && toPlayer < 60f)
                {
                    told = true;
                    hud.Callout("شوف الشباب يقحصون حول الدوّار .. سوّ مثلهم!", UIKit.Gold);
                }
            }
        }
    }
}
