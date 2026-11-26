using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// The desert south of Salwa: a two-lane highway that leaves the end of the main avenue, sweeps through dunes past
    /// power-line poles, and ends at a desert camp (a goat-hair tent, a campfire, the crew's cars). The Desert Run races
    /// along it; free roam can drive out there too.
    public static class DesertBuilder
    {
        public static readonly List<Vector3> Route = new List<Vector3>();   // centre line, every ~6 m, start to camp
        public static Vector3 Camp { get; private set; }
        const float RoadW = 12f;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Desert").transform;
            root.SetParent(parent, false);
            Random.State saved = Random.state;
            Random.InitState(1987);

            // More sand to the south (the Salwa ground ends at z = -1200)
            var g = new MeshKit();
            g.Box(new Vector3(0f, -0.1f, -2600f), new Vector3(3000f, 0.2f, 2800f));
            var sand = Mat.Textured("M_Sand", TextureKit.Sand, Color.white, 0.12f, new Vector2(0.1f, 0.1f));
            var go = g.ToObject("DesertGround", root, sand);
            go.AddComponent<BoxCollider>();
            var st = go.AddComponent<SurfaceType>(); st.id = "sand"; st.grip = 0.6f;

            // The highway: control points smoothed into a curve
            Vector3[] ctrl =
            {
                new Vector3(5f, 0f, -400f), new Vector3(5f, 0f, -560f), new Vector3(10f, 0f, -720f), new Vector3(70f, 0f, -860f),
                new Vector3(130f, 0f, -1000f), new Vector3(135f, 0f, -1160f), new Vector3(80f, 0f, -1320f), new Vector3(10f, 0f, -1470f),
                new Vector3(0f, 0f, -1620f), new Vector3(0f, 0f, -1700f)
            };
            Route.Clear();
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                Vector3 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(ctrl.Length - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(p1, p2) / 6f));
                for (int k = 0; k < n; k++) Route.Add(CatmullRom(p0, p1, p2, p3, k / (float)n));
            }
            Route.Add(ctrl[ctrl.Length - 1]);
            Camp = ctrl[ctrl.Length - 1] + new Vector3(0f, 0f, -30f);

            var road = new MeshKit(); var paint = new MeshKit(); var shoulder = new MeshKit();
            var colliders = new GameObject("HighwayColliders").transform;
            colliders.SetParent(root, false);
            for (int i = 0; i < Route.Count - 1; i++)
            {
                Vector3 a = Route[i], b = Route[i + 1];
                Vector3 f = (b - a); f.y = 0f; float len = f.magnitude; f /= Mathf.Max(0.01f, len);
                Vector3 r = Vector3.Cross(Vector3.up, f);
                Vector3 y = Vector3.up * 0.03f;
                // wound clockwise seen from above so the surface faces up
                road.QuadUv(a - r * RoadW * 0.5f + y, b - r * RoadW * 0.5f + y, b + r * RoadW * 0.5f + y, a + r * RoadW * 0.5f + y,
                    new Vector2(0, i * len / 8f), new Vector2(0, (i + 1) * len / 8f), new Vector2(RoadW / 8f, (i + 1) * len / 8f), new Vector2(RoadW / 8f, i * len / 8f));
                // sand blown over the edges
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 e0 = a + r * s * RoadW * 0.5f, e1 = b + r * s * RoadW * 0.5f;
                    shoulder.Quad(e0 + Vector3.up * 0.035f, e1 + Vector3.up * 0.035f, e1 - r * s * 1.2f + Vector3.up * 0.035f, e0 - r * s * 1.2f + Vector3.up * 0.035f, Vector3.up);
                }
                if (i % 3 == 0)   // dashed centre line
                    paint.Quad(a - r * 0.08f + Vector3.up * 0.04f, a + r * 0.08f + Vector3.up * 0.04f, a + f * 3f + r * 0.08f + Vector3.up * 0.04f, a + f * 3f - r * 0.08f + Vector3.up * 0.04f, Vector3.up);
                var c = new GameObject("Seg").AddComponent<BoxCollider>();
                c.transform.SetParent(colliders, false);
                c.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.down * 0.12f, Quaternion.LookRotation(f));
                c.size = new Vector3(RoadW, 0.3f, len + 0.4f);
            }
            road.ToObject("Highway", root, Mat.Textured("M_DesertAsphalt", TextureKit.Asphalt, new Color(0.72f, 0.72f, 0.74f), 0.16f, new Vector2(0.125f, 0.125f)));
            paint.ToObject("HighwayPaint", root, Mat.Paint("M_RoadPaint", new Color(0.95f, 0.93f, 0.85f), 0.3f));
            shoulder.ToObject("SandDrift", root, Mat.Paint("M_SandDrift", new Color(0.86f, 0.72f, 0.52f), 0.1f));

            // Power-line poles along the east side (solid: you bump off them, you never drive through)
            var poles = new MeshKit();
            var hard = new GameObject("DesertColliders").transform;
            hard.SetParent(root, false);
            for (int i = 0; i < Route.Count; i += 9)
            {
                Vector3 p = Route[Mathf.Min(i + 1, Route.Count - 1)] - Route[i];
                Vector3 r = Vector3.Cross(Vector3.up, p.normalized);
                Vector3 b = Route[i] + r * 16f;
                poles.Box(b + Vector3.up * 5.5f, new Vector3(0.3f, 11f, 0.3f), Quaternion.LookRotation(p.normalized), true);
                poles.Box(b + Vector3.up * 10.4f, new Vector3(3.2f, 0.22f, 0.22f), Quaternion.LookRotation(p.normalized), true);
                Solid(hard, "Pole", b + Vector3.up * 2f, new Vector3(0.45f, 4f, 0.45f), Quaternion.LookRotation(p.normalized));
            }
            poles.ToObject("PowerPoles", root, Mat.Paint("M_Pole", new Color(0.32f, 0.26f, 0.2f), 0.2f));

            // Dunes: soft mounds away from the road, solid and drivable
            var dunes = new MeshKit();
            for (int i = 0; i < 140; i++)
            {
                Vector3 p = new Vector3(Random.Range(-700f, 800f), 0f, Random.Range(-2300f, -480f));
                float rad = Random.Range(22f, 70f), h = Random.Range(3f, 13f);
                if (NearRoad(p, rad + 18f) || (p - Camp).magnitude < rad + 50f) continue;
                var prof = new List<Vector2>();
                for (int k = 0; k <= 12; k++) { float t = k / 12f; prof.Add(new Vector2(rad * (1f - t), h * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI)))); }   // rounded, wind-blown mound
                prof.Add(new Vector2(0f, h));
                dunes.Lathe(p + Vector3.down * 0.4f, prof.ToArray(), 24);
            }
            var dgo = dunes.ToObject("Dunes", root, sand);
            var mc = dgo.AddComponent<MeshCollider>(); mc.sharedMesh = dgo.GetComponent<MeshFilter>().sharedMesh;
            var dst = dgo.AddComponent<SurfaceType>(); dst.id = "sand"; dst.grip = 0.6f;

            CampSite(root, hard);
            Random.state = saved;
        }

        static bool NearRoad(Vector3 p, float d)
        {
            for (int i = 0; i < Route.Count; i += 4)
            {
                Vector3 q = Route[i] - p; q.y = 0f;
                if (q.sqrMagnitude < d * d) return true;
            }
            return false;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// The crew's camp: a black goat-hair tent open on one side, rugs, a campfire with warm light, cars parked round it.
        /// An invisible solid box (the visible meshes are merged for speed, so they carry no collision themselves).
        static void Solid(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot);
            go.AddComponent<BoxCollider>().size = size;
        }

        static void CampSite(Transform root, Transform hard)
        {
            var camp = new GameObject("DesertCamp").transform;
            camp.SetParent(root, false);   // left at the origin: everything below is placed in world coordinates
            var tent = new MeshKit(); var rug = new MeshKit(); var stones = new MeshKit();
            Vector3 t0 = Camp + new Vector3(-14f, 0f, -12f);
            // low pitched roof on poles
            float w = 12f, d = 7f, h = 2.6f;
            tent.Quad(t0 + new Vector3(-w / 2, h, 0), t0 + new Vector3(w / 2, h, 0), t0 + new Vector3(w / 2, 1.6f, -d / 2), t0 + new Vector3(-w / 2, 1.6f, -d / 2), Vector3.up);
            tent.Quad(t0 + new Vector3(w / 2, h, 0), t0 + new Vector3(-w / 2, h, 0), t0 + new Vector3(-w / 2, 1.6f, d / 2), t0 + new Vector3(w / 2, 1.6f, d / 2), Vector3.up);
            tent.Box(t0 + new Vector3(0, 0.8f, -d / 2), new Vector3(w, 1.6f, 0.08f), Quaternion.identity, true);   // back wall
            for (int i = -1; i <= 1; i++) tent.Box(t0 + new Vector3(i * w * 0.45f, h * 0.5f, 0), new Vector3(0.12f, h, 0.12f), Quaternion.identity, true);
            var tm = Mat.Paint("M_Tent", new Color(0.12f, 0.10f, 0.09f), 0.1f); tm.SetFloat("_Cull", 0f);
            tent.ToObject("Tent", camp, tm);
            Solid(hard, "TentWall", t0 + new Vector3(0, 0.8f, -d / 2), new Vector3(w, 1.6f, 0.3f), Quaternion.identity);
            for (int i = -1; i <= 1; i++) Solid(hard, "TentPole", t0 + new Vector3(i * w * 0.45f, h * 0.5f, 0), new Vector3(0.3f, h, 0.3f), Quaternion.identity);
            rug.Box(t0 + new Vector3(0, 0.02f, 0.6f), new Vector3(9f, 0.04f, 5f), Quaternion.identity, true);
            rug.ToObject("Rugs", camp, Mat.Paint("M_Rug", new Color(0.55f, 0.12f, 0.1f), 0.15f));
            Vector3 fire = Camp + new Vector3(-14f, 0f, -2f);
            stones.Lathe(fire, new[] { new Vector2(0, 0), new Vector2(1.1f, 0), new Vector2(1.0f, 0.35f), new Vector2(0.7f, 0.3f), new Vector2(0, 0.05f) }, 10);
            stones.ToObject("Firepit", camp, Mat.Paint("M_Firepit", new Color(0.25f, 0.22f, 0.2f), 0.1f));
            Solid(hard, "Firepit", fire + Vector3.up * 0.25f, new Vector3(2.1f, 0.5f, 2.1f), Quaternion.identity);
            var l = new GameObject("Campfire").AddComponent<Light>();
            l.transform.SetParent(camp, false); l.transform.position = fire + Vector3.up * 1.2f;
            l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 22f; l.intensity = 14f; l.shadows = LightShadows.Soft;
            l.gameObject.AddComponent<Flicker>();
            // the crew's cars parked round the fire
            string[] ids = { "tr_pickup", "tr_suv", "tr_sedan_big" };
            for (int i = 0; i < ids.Length; i++)
            {
                var car = CarFactory.BuildDisplay(ids[i], camp);
                if (car == null) continue;
                float a = -0.9f + i * 0.9f;
                Vector3 pos = fire + new Vector3(Mathf.Sin(a) * 13f + 6f, 0.02f, Mathf.Cos(a) * 13f);
                car.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((fire - pos).normalized));
                var bc = car.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.8f, 1.4f, 4.6f);
            }
        }

        class Flicker : MonoBehaviour
        {
            Light l; float baseI;
            void Start() { l = GetComponent<Light>(); baseI = l.intensity; }
            void Update() { l.intensity = baseI * (0.8f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0.3f)); }
        }
    }
}
