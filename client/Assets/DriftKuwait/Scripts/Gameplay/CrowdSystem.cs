using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// The drift crowd: young Kuwaiti and Bedouin men on the pavements round the roundabouts and along the avenue,
    /// in dishdashas and shemaghs, filming on VHS camcorders and snapping flash cameras (it is 1987). They murmur and
    /// whistle while waiting, and jump, cheer and flash when you slide past them. Photo cut-outs that always face the
    /// camera, so a big crowd costs almost nothing on the phone.
    public class CrowdSystem : MonoBehaviour
    {
        public DriftCarController player;

        class Fan { public Transform t; public Vector3 home; public float phase, h; public bool camera; }
        class Group { public Vector3 c; public List<Fan> fans = new List<Fan>(); public AudioSource loop, burst; public float excite, cheerCd; }

        readonly List<Group> groups = new List<Group>();
        Material[] mats;
        float[] aspect;
        bool[] isCamera;
        Light flash; float flashT;
        AudioClip cheer, whistle;
        int groundMask;

        static readonly string[] Tex = { "crowd_a", "crowd_b", "crowd_c", "crowd_d", "crowd_e", "crowd_f" };

        void Start()
        {
            groundMask = ~((1 << 2) | (1 << TrafficSystem.TrafficLayer));
            var list = new List<Material>(); var asp = new List<float>(); var cam = new List<bool>();
            foreach (var n in Tex)
            {
                var tx = Resources.Load<Texture2D>("DriftKuwait/Crowd/" + n);
                if (tx == null) continue;
                var m = Mat.Textured("M_Crowd_" + n, tx, Color.white, 0.1f, Vector2.one);
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0f);
                list.Add(m); asp.Add(tx.width / (float)tx.height); cam.Add(n == "crowd_b" || n == "crowd_d");
            }
            if (list.Count == 0) { enabled = false; return; }
            mats = list.ToArray(); aspect = asp.ToArray(); isCamera = cam.ToArray();
            cheer = Resources.Load<AudioClip>("DriftKuwait/Audio/crowd_cheer_big");
            whistle = Resources.Load<AudioClip>("DriftKuwait/Audio/crowd_whistle");
            var loopClip = Resources.Load<AudioClip>("DriftKuwait/Audio/crowd_drift_loop");

            Random.State saved = Random.state; Random.InitState(87);
            // Round every roundabout: four groups on the corners between the roads
            foreach (var (c, r) in SalwaBlockBuilder.Islands)
                for (int q = 0; q < 4; q++)
                {
                    float a = (45f + q * 90f + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                    float rr = r + 22f + Random.Range(0f, 3f);
                    AddGroup(new Vector3(c.x + Mathf.Cos(a) * rr, 0f, c.z + Mathf.Sin(a) * rr), Random.Range(6, 10), loopClip);
                }
            // Along the main avenue pavements, where the long slides happen
            for (float z = -330f; z <= 330f; z += 110f)
                AddGroup(new Vector3((Random.value > 0.5f ? 1f : -1f) * 17f, 0f, z + Random.Range(-20f, 20f)), Random.Range(5, 8), loopClip);
            Random.state = saved;

            flash = new GameObject("CameraFlash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point; flash.color = new Color(0.9f, 0.95f, 1f); flash.range = 7f; flash.intensity = 0f; flash.shadows = LightShadows.None;
        }

        void AddGroup(Vector3 c, int n, AudioClip loopClip)
        {
            // keep off the roads: the spot must not be within a road rectangle
            foreach (var r in SalwaBlockBuilder.RoadRects)
                if (c.x > r.x - 1.5f && c.x < r.y + 1.5f && c.z > r.z - 1.5f && c.z < r.w + 1.5f) return;
            var g = new Group { c = c };
            var root = new GameObject("CrowdGroup").transform; root.SetParent(transform, false); root.position = c;
            for (int i = 0; i < n; i++)
            {
                int k = Random.Range(0, mats.Length);
                float h = Random.Range(1.68f, 1.86f);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Fan";
                quad.transform.SetParent(root, false);
                quad.transform.localScale = new Vector3(h * aspect[k] * 1.05f, h * 1.05f, 1f);
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mats[k];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Vector2 off = Random.insideUnitCircle * 3.2f;
                Vector3 p = c + new Vector3(off.x, 0f, off.y);
                p.y = Ground(p) + h * 1.05f * 0.5f;
                quad.transform.position = p;
                // a solid post where each spectator stands, so cars stop at the crowd instead of passing through it
                var post = new GameObject("FanCollider").AddComponent<CapsuleCollider>();
                post.transform.SetParent(root, false); post.transform.position = new Vector3(p.x, p.y, p.z);
                post.direction = 1; post.radius = 0.3f; post.height = h;
                g.fans.Add(new Fan { t = quad.transform, home = p, phase = Random.value * 10f, h = h, camera = isCamera[k] });
            }
            g.loop = root.gameObject.AddComponent<AudioSource>();
            g.loop.clip = loopClip; g.loop.loop = true; g.loop.spatialBlend = 1f; g.loop.minDistance = 6f; g.loop.maxDistance = 70f;
            g.loop.rolloffMode = AudioRolloffMode.Linear; g.loop.volume = 0f;
            if (loopClip != null) { g.loop.time = Random.Range(0f, loopClip.length * 0.9f); g.loop.Play(); }
            g.burst = root.gameObject.AddComponent<AudioSource>();
            g.burst.spatialBlend = 1f; g.burst.minDistance = 8f; g.burst.maxDistance = 90f; g.burst.rolloffMode = AudioRolloffMode.Linear;
            groups.Add(g);
        }

        float Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return 0f;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || player == null) return;
            float dt = Time.deltaTime;
            Vector3 pp = player.transform.position;
            Vector3 cp = cam.transform.position;
            float drift = Mathf.Abs(player.DriftAngle);
            flashT -= dt; flash.intensity = flashT > 0f ? 6f : 0f;

            foreach (var g in groups)
            {
                float d = Vector3.Distance(new Vector3(pp.x, 0f, pp.z), g.c);
                float show = Mathf.Clamp01((55f - d) / 40f) * Mathf.Clamp01((drift - 12f) / 25f) * Mathf.Clamp01(player.SpeedKmh / 30f);
                g.excite = Mathf.MoveTowards(g.excite, show, dt * (show > g.excite ? 3f : 0.6f));
                g.cheerCd -= dt;
                if (g.loop.clip != null) g.loop.volume = (0.18f + 0.62f * g.excite) * Sfx.SfxVolume;
                if (g.excite > 0.55f && g.cheerCd <= 0f && cheer != null)
                {
                    g.cheerCd = Random.Range(5f, 8f);
                    g.burst.pitch = Random.Range(0.95f, 1.05f);
                    g.burst.PlayOneShot(Random.value > 0.35f ? cheer : (whistle ?? cheer), 0.9f * Sfx.SfxVolume);
                }

                bool near = (g.c - cp).sqrMagnitude < 200f * 200f;
                foreach (var f in g.fans)
                {
                    if (!near) continue;
                    // face the camera (upright billboard)
                    Vector3 look = f.t.position - cp; look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) f.t.rotation = Quaternion.LookRotation(look.normalized);
                    // jump and sway with the excitement; step back from a car coming at them
                    float hop = g.excite * Mathf.Max(0f, Mathf.Sin(Time.time * (7f + f.phase % 3f) + f.phase)) * 0.22f;
                    float sway = Mathf.Sin(Time.time * 1.3f + f.phase) * 0.04f;
                    Vector3 away = f.home - pp; away.y = 0f;
                    float push = away.magnitude < 8f && player.SpeedKmh > 15f ? (8f - away.magnitude) * 0.35f : 0f;
                    Vector3 target = f.home + away.normalized * push + Vector3.up * hop + f.t.right * sway;
                    f.t.position = Vector3.Lerp(f.t.position, target, 1f - Mathf.Exp(-10f * dt));
                    // camera flashes from the ones with cameras
                    if (f.camera && g.excite > 0.4f && flashT <= -0.25f && Random.value < dt * 1.5f)
                    {
                        flash.transform.position = f.t.position + Vector3.up * 0.5f + (cp - f.t.position).normalized * 0.4f;
                        flashT = 0.06f;
                    }
                }
            }
        }
    }
}
