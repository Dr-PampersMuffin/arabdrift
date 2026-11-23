using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Builds a Salwa-inspired district from code: a main avenue, a cross street, a grid of
    /// residential streets, two roundabouts, walled villas with gates, window ACs, sun-shade
    /// eyebrows, rooftop water tanks, palms, sodium street lamps, a brass dallah on the big
    /// roundabout, and neighbourhood water towers on the skyline. Geometry is original;
    /// the real street plan is matched later from OpenStreetMap.
    public static class SalwaBlockBuilder
    {
        struct R
        {
            public float x0, x1, z0, z1;
            public R(float x0, float x1, float z0, float z1) { this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
            public float W => x1 - x0;
            public float D => z1 - z0;
            public Vector3 Center(float y) => new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            public bool Overlaps(R o) => x0 < o.x1 && x1 > o.x0 && z0 < o.z1 && z1 > o.z0;
            public bool Contains(float x, float z, float pad = 0f) => x > x0 - pad && x < x1 + pad && z > z0 - pad && z < z1 + pad;
            public R Shrink(float m) => new R(x0 + m, x1 - m, z0 + m, z1 - m);
        }

        static readonly List<R> roads = new List<R>();
        static readonly List<R> plazas = new List<R>();
        static readonly List<(Vector3 c, float r)> islands = new List<(Vector3, float)>();

        static readonly Color[] HousePalette =
        {
            new Color(0.91f, 0.87f, 0.78f), new Color(0.85f, 0.76f, 0.62f), new Color(0.93f, 0.92f, 0.88f),
            new Color(0.88f, 0.80f, 0.74f), new Color(0.82f, 0.82f, 0.80f)
        };
        static readonly Color[] GatePalette =
        {
            new Color(0.10f, 0.22f, 0.18f), new Color(0.30f, 0.18f, 0.10f), new Color(0.06f, 0.06f, 0.06f), new Color(0.08f, 0.26f, 0.30f)
        };

        // Mesh batches (one draw call each)
        static MeshKit[] houseKits, gateKits;
        static MeshKit glassKit, acKit, tankKit, collKit, kerbYellow, kerbBlack, paintKit, trunkKit, frondKit, pavingKit;
        // Facade detail: limestone cladding, white trim (frames, bands, cornices), timber, lit windows, steel railings
        static MeshKit stoneKit, trimKit, woodKit, litGlassKit, railKit, dateKit;
        static Material litWindowMat;

        // Photo facades (tools/art/make_world_v3.py): each is a straight-on photo of a real-style 1980s Salwa villa front,
        // laid on the street face of the house. Boundary walls and gates use photo textures too.
        struct Facade { public Texture2D tex; public float aspect; public bool two; public MeshKit kit; }
        static readonly List<Facade> facades = new List<Facade>();
        static MeshKit wallKit;
        static MeshKit[] gatePhotoKits;
        static Texture2D wallTex, sideTex;
        static Texture2D[] gateTex;
        static readonly List<(Vector3 pos, Quaternion rot)> parkSpots = new List<(Vector3, Quaternion)>();

        static Texture2D Tex(string n) => Resources.Load<Texture2D>("DriftKuwait/Tex/" + n);

        static void LoadPhotoSet()
        {
            facades.Clear();
            foreach (var n in new[] { "fac_villa2_a", "fac_villa2_b", "fac_villa2_c", "fac_villa2_d", "fac_villa1_a" })
            {
                var t = Tex(n);
                if (t == null) continue;
                facades.Add(new Facade { tex = t, aspect = t.width / (float)t.height, two = n.StartsWith("fac_villa2"), kit = new MeshKit() });
            }
            wallTex = Tex("wall_boundary");
            sideTex = Tex("wall_side");
            gateTex = new[] { Tex("gate_steel_a"), Tex("gate_steel_b") };
            gatePhotoKits = new[] { new MeshKit(), new MeshKit() };
            wallKit = new MeshKit();
        }

        /// Called by Atmosphere when the time of day changes: some windows glow warm at night.
        static Material asphaltMat;

        public static void SetNight(bool night)
        {
            // Night streets look freshly washed: wetter, darker asphalt that mirrors the sodium lamps and headlights.
            if (asphaltMat != null)
            {
                asphaltMat.SetFloat("_Smoothness", night ? 0.78f : 0.32f);
                asphaltMat.SetColor("_BaseColor", night ? new Color(0.62f, 0.62f, 0.64f) : Color.white);
            }
            if (litWindowMat == null) return;
            litWindowMat.SetColor("_EmissionColor", night ? new Color(1f, 0.72f, 0.42f) * 2.2f : Color.black);
            litWindowMat.SetColor("_BaseColor", night ? new Color(0.35f, 0.26f, 0.16f) : new Color(0.08f, 0.10f, 0.11f));
        }

        public static void Build(Transform parent)
        {
            Random.InitState(1985);
            roads.Clear(); plazas.Clear(); islands.Clear();
            var root = new GameObject("Salwa").transform;
            root.SetParent(parent, false);

            houseKits = new MeshKit[HousePalette.Length];
            for (int i = 0; i < houseKits.Length; i++) houseKits[i] = new MeshKit();
            gateKits = new MeshKit[GatePalette.Length];
            for (int i = 0; i < gateKits.Length; i++) gateKits[i] = new MeshKit();
            glassKit = new MeshKit(); acKit = new MeshKit(); tankKit = new MeshKit(); collKit = new MeshKit();
            kerbYellow = new MeshKit(); kerbBlack = new MeshKit(); paintKit = new MeshKit();
            trunkKit = new MeshKit(); frondKit = new MeshKit(); pavingKit = new MeshKit();
            stoneKit = new MeshKit(); trimKit = new MeshKit(); woodKit = new MeshKit(); litGlassKit = new MeshKit();
            railKit = new MeshKit(); dateKit = new MeshKit();
            mosqueKit = new MeshKit(); shopKit = new MeshKit(); domeKit = new MeshKit();
            LoadPhotoSet();
            parkSpots.Clear();
            mosqueBuilt = shopsBuilt = false;

            Ground(root);
            Roads(root);
            Roundabout(root, new Vector3(0f, 0f, 0f), 15f, true);
            Roundabout(root, new Vector3(0f, 0f, -240f), 9f, false);
            Blocks();
            Lamps(root);
            Skyline(root);
            Flush(root);
            ParkedCars(root);
        }

        // ------------------------------------------------------------------ ground & roads
        static void Ground(Transform root)
        {
            var k = new MeshKit();
            k.Box(new Vector3(0f, -0.1f, 0f), new Vector3(2400f, 0.2f, 2400f));
            var go = k.ToObject("Sand", root, Mat.Textured("M_Sand", TextureKit.Sand, Color.white, 0.12f, new Vector2(0.1f, 0.1f)));
            go.AddComponent<BoxCollider>();
            go.AddComponent<SurfaceType>().grip = 0.6f;
            go.GetComponent<SurfaceType>().id = "sand";
        }

        static void AddRoad(R r, float top) { roads.Add(r); roadTops.Add(top); }

        /// The drivable network for traffic AI: each road as (x0, x1, z0, z1); roundabout islands as (centre, radius).
        public static IEnumerable<Vector4> RoadRects { get { foreach (var r in roads) yield return new Vector4(r.x0, r.x1, r.z0, r.z1); } }
        public static IEnumerable<(Vector3 c, float r)> Islands => islands;
        static readonly List<float> roadTops = new List<float>();

        static void Roads(Transform root)
        {
            roadTops.Clear();
            AddRoad(new R(-10f, 10f, -430f, 430f), 0.020f);            // main avenue
            AddRoad(new R(-430f, 430f, -7f, 7f), 0.021f);              // cross street
            foreach (float x in new[] { -180f, -90f, 90f, 180f })
                AddRoad(new R(x - 4.5f, x + 4.5f, -430f, 430f), 0.019f);
            foreach (float z in new[] { -320f, -160f, -80f, 80f, 160f, 240f, 320f })
                AddRoad(new R(-205f, 205f, z - 4.5f, z + 4.5f), 0.0185f);
            AddRoad(new R(-205f, 205f, -246f, -234f), 0.0186f);        // wider street through roundabout 2

            var p1 = new R(-40f, 40f, -40f, 40f);
            var p2 = new R(-26f, 26f, -266f, -214f);
            plazas.Add(p1); plazas.Add(p2);
            AddRoad(p1, 0.022f);
            AddRoad(p2, 0.0225f);

            var asphalt = Mat.Textured("M_Asphalt", TextureKit.Asphalt, Color.white, 0.32f, new Vector2(0.125f, 0.125f));
            asphaltMat = asphalt;
            var parent = new GameObject("Roads").transform;
            parent.SetParent(root, false);
            for (int i = 0; i < roads.Count; i++)
            {
                var r = roads[i];
                var k = new MeshKit();
                k.Box(r.Center(roadTops[i] - 0.05f), new Vector3(r.W, 0.1f, r.D), Quaternion.identity, true);
                var go = k.ToObject("Road_" + i, parent, asphalt);
                go.AddComponent<BoxCollider>();
                go.AddComponent<SurfaceType>();
            }

            // Lane paint: avenue centre double line + lane dashes, cross street centre dashes.
            for (float z = -430f; z < 430f; z += 6f)
            {
                if (InPlaza(0f, z, 4f) || CrossesRoad(0f, z, 0)) continue;
                Paint(new Vector3(-0.18f, 0f, z + 1.5f), new Vector2(0.14f, 3f), 0.0215f);
                Paint(new Vector3(0.18f, 0f, z + 1.5f), new Vector2(0.14f, 3f), 0.0215f);
                Paint(new Vector3(-5f, 0f, z + 1.5f), new Vector2(0.14f, 2.2f), 0.0215f);
                Paint(new Vector3(5f, 0f, z + 1.5f), new Vector2(0.14f, 2.2f), 0.0215f);
            }
            for (float x = -430f; x < 430f; x += 6f)
            {
                if (InPlaza(x, 0f, 4f) || CrossesRoad(x, 0f, 1)) continue;
                Paint(new Vector3(x + 1.5f, 0f, 0f), new Vector2(3f, 0.14f), 0.0225f);
            }
        }

        static bool InPlaza(float x, float z, float pad)
        {
            foreach (var p in plazas) if (p.Contains(x, z, pad)) return true;
            return false;
        }

        static bool CrossesRoad(float x, float z, int self)
        {
            for (int i = 0; i < roads.Count; i++)
                if (i != self && roads[i].Contains(x, z, 1f)) return true;
            return false;
        }

        static void Paint(Vector3 c, Vector2 size, float y)
        {
            c.y = y + 0.004f;
            var h = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
            paintKit.Quad(c + new Vector3(-h.x, 0, -h.z), c + new Vector3(h.x, 0, -h.z), c + new Vector3(h.x, 0, h.z), c + new Vector3(-h.x, 0, h.z), Vector3.up);
        }

        // ------------------------------------------------------------------ roundabouts
        static void Roundabout(Transform root, Vector3 c, float islandR, bool hero)
        {
            islands.Add((c, islandR));
            float top = 0.25f;
            var island = new MeshKit();
            island.Lathe(c, new[] { new Vector2(0f, 0f), new Vector2(islandR, 0f), new Vector2(islandR, top), new Vector2(0f, top) }, 48);
            var go = island.ToObject("Island", root, Mat.Textured("M_IslandSand", TextureKit.Sand, new Color(0.92f, 0.86f, 0.74f), 0.1f, new Vector2(0.2f, 0.2f)));
            go.AddComponent<MeshCollider>();
            go.AddComponent<SurfaceType>().grip = 0.6f;

            // Yellow/black kerb ring
            int segs = Mathf.RoundToInt(2f * Mathf.PI * islandR / 1.2f);
            for (int s = 0; s < segs; s++)
            {
                float t0 = s * Mathf.PI * 2f / segs, t1 = (s + 1) * Mathf.PI * 2f / segs;
                var kit = (s % 2 == 0) ? kerbYellow : kerbBlack;
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                Vector3 o0 = c + d0 * (islandR + 0.01f), o1 = c + d1 * (islandR + 0.01f);
                kit.Quad(o0 + Vector3.up * 0.02f, o1 + Vector3.up * 0.02f, o1 + Vector3.up * (top + 0.003f), o0 + Vector3.up * (top + 0.003f), (d0 + d1).normalized);
                Vector3 i0 = c + d0 * (islandR - 0.35f), i1 = c + d1 * (islandR - 0.35f);
                kit.Quad(i0 + Vector3.up * (top + 0.003f), o0 + Vector3.up * (top + 0.003f), o1 + Vector3.up * (top + 0.003f), i1 + Vector3.up * (top + 0.003f), Vector3.up);
            }

            // Dashed lane ring on the carriageway
            float laneR = islandR + 7f;
            int dashes = Mathf.RoundToInt(2f * Mathf.PI * laneR / 6f);
            for (int s = 0; s < dashes; s++)
            {
                float t0 = s * Mathf.PI * 2f / dashes, t1 = t0 + (3f / laneR);
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                float y = 0.0265f;
                paintKit.Quad(c + d0 * (laneR - 0.07f) + Vector3.up * y, c + d1 * (laneR - 0.07f) + Vector3.up * y,
                              c + d1 * (laneR + 0.07f) + Vector3.up * y, c + d0 * (laneR + 0.07f) + Vector3.up * y, Vector3.up);
            }

            if (hero)
            {
                // Brass dallah sculpture on a stone plinth: the roundabout's landmark.
                var stone = new MeshKit();
                stone.Lathe(c + Vector3.up * top, new[] { new Vector2(0, 0), new Vector2(3.6f, 0), new Vector2(3.6f, 0.5f), new Vector2(2.8f, 0.6f), new Vector2(2.8f, 1.9f), new Vector2(0, 1.9f) }, 32);
                var sgo = stone.ToObject("Plinth", root, Mat.Paint("M_Stone", new Color(0.86f, 0.80f, 0.70f), 0.25f));
                sgo.AddComponent<MeshCollider>();

                const float sc = 1.7f;
                Vector3 b = c + Vector3.up * (top + 1.9f);
                var brass = new MeshKit();
                var prof = new List<Vector2>();
                foreach (var p in new[] { new Vector2(0, 0), new Vector2(1.6f, 0), new Vector2(1.85f, 0.4f), new Vector2(1.95f, 1.4f), new Vector2(1.55f, 2.6f),
                                          new Vector2(0.9f, 3.4f), new Vector2(0.8f, 4.4f), new Vector2(1.15f, 4.9f), new Vector2(0.6f, 5.5f), new Vector2(0.25f, 6.4f), new Vector2(0, 6.6f) })
                    prof.Add(p * sc);
                brass.Lathe(b, prof, 36);
                brass.Cylinder(b + new Vector3(2.3f, 4.2f, 0f) * sc, new Vector3(0.75f, 0.66f, 0f), 0.22f * sc, 3.4f * sc, 14);
                brass.Box(b + new Vector3(-1.75f, 3.6f, 0f) * sc, new Vector3(0.25f, 2.6f, 0.35f) * sc, Quaternion.Euler(0, 0, -12f));
                brass.ToObject("Dallah", root, Mat.Paint("M_Brass", new Color(0.80f, 0.60f, 0.28f), 0.72f, 0.95f));

                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad + 0.4f;
                    Palm(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (islandR - 4f) + Vector3.up * top, Random.Range(7f, 9.5f));
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f * Mathf.Deg2Rad;
                    Palm(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f + Vector3.up * top, Random.Range(6.5f, 8.5f));
                }
            }
        }

        // ------------------------------------------------------------------ blocks & villas
        static List<(float a, float b)> Gaps(List<(float a, float b)> spans, float lo, float hi)
        {
            spans.Sort((p, q) => p.a.CompareTo(q.a));
            var gaps = new List<(float, float)>();
            float cur = lo;
            foreach (var s in spans)
            {
                if (s.a > cur) gaps.Add((cur, s.a));
                cur = Mathf.Max(cur, s.b);
            }
            if (hi > cur) gaps.Add((cur, hi));
            return gaps;
        }

        static List<R> Subtract(R a, R b)
        {
            var o = new List<R>();
            if (!a.Overlaps(b)) { o.Add(a); return o; }
            if (b.x0 > a.x0) o.Add(new R(a.x0, b.x0, a.z0, a.z1));
            if (b.x1 < a.x1) o.Add(new R(b.x1, a.x1, a.z0, a.z1));
            float mx0 = Mathf.Max(a.x0, b.x0), mx1 = Mathf.Min(a.x1, b.x1);
            if (b.z0 > a.z0) o.Add(new R(mx0, mx1, a.z0, b.z0));
            if (b.z1 < a.z1) o.Add(new R(mx0, mx1, b.z1, a.z1));
            return o;
        }

        static void Blocks()
        {
            var xs = new List<(float, float)> { (-10f, 10f), (-184.5f, -175.5f), (-94.5f, -85.5f), (85.5f, 94.5f), (175.5f, 184.5f) };
            var zs = new List<(float, float)> { (-7f, 7f), (-324.5f, -315.5f), (-246f, -234f), (-164.5f, -155.5f), (-84.5f, -75.5f),
                                                 (75.5f, 84.5f), (155.5f, 164.5f), (235.5f, 244.5f), (315.5f, 324.5f) };
            var cols = Gaps(xs, -184.5f, 184.5f);
            var rows = Gaps(zs, -324.5f, 324.5f);
            foreach (var col in cols)
                foreach (var row in rows)
                {
                    var pieces = new List<R> { new R(col.a, col.b, row.a, row.b) };
                    foreach (var p in plazas)
                    {
                        var next = new List<R>();
                        foreach (var r in pieces) next.AddRange(Subtract(r, p));
                        pieces = next;
                    }
                    foreach (var r in pieces)
                        if (r.W > 18f && r.D > 18f) Block(r);
                        else if (r.W > 5f && r.D > 5f) PavedLot(r);
                }
        }

        static void Block(R r)
        {
            // Raised pavement slab + painted kerb
            const float h = 0.14f;
            var slab = new R(r.x0 + 0.25f, r.x1 - 0.25f, r.z0 + 0.25f, r.z1 - 0.25f);
            pavingKit.Box(slab.Center(h * 0.5f), new Vector3(slab.W, h, slab.D), Quaternion.identity, true);
            collKit.Box(slab.Center(h * 0.5f), new Vector3(slab.W, h, slab.D));
            KerbEdge(new Vector3(slab.x0, 0, slab.z0), new Vector3(slab.x1, 0, slab.z0), Vector3.back, h);
            KerbEdge(new Vector3(slab.x1, 0, slab.z0), new Vector3(slab.x1, 0, slab.z1), Vector3.right, h);
            KerbEdge(new Vector3(slab.x1, 0, slab.z1), new Vector3(slab.x0, 0, slab.z1), Vector3.forward, h);
            KerbEdge(new Vector3(slab.x0, 0, slab.z1), new Vector3(slab.x0, 0, slab.z0), Vector3.left, h);

            // Neighbourhood landmarks on two blocks by the avenue: the mosque, and a row of shops with a baqala
            if (!mosqueBuilt && r.Contains(-50f, -120f, 0f)) { mosqueBuilt = true; Mosque(slab, h); return; }
            if (!shopsBuilt && r.Contains(50f, -120f, 0f)) { shopsBuilt = true; Shops(slab, h); return; }

            var inner = slab.Shrink(2.5f);
            bool alongX = inner.W >= inner.D;
            float length = alongX ? inner.W : inner.D;
            float depth = alongX ? inner.D : inner.W;
            bool twoRows = depth > 46f;

            for (int row = 0; row < (twoRows ? 2 : 1); row++)
            {
                float d0 = twoRows ? (row == 0 ? 0f : depth * 0.5f) : 0f;
                float d1 = twoRows ? (row == 0 ? depth * 0.5f : depth) : depth;
                float pos = 0f;
                while (pos < length - 12f)
                {
                    float w = Mathf.Min(Random.Range(20f, 28f), length - pos);
                    if (length - (pos + w) < 12f) w = length - pos;
                    R plot = alongX ? new R(inner.x0 + pos, inner.x0 + pos + w, inner.z0 + d0, inner.z0 + d1)
                                    : new R(inner.x0 + d0, inner.x0 + d1, inner.z0 + pos, inner.z0 + pos + w);
                    // Facing: row 0 faces the min side, row 1 faces the max side.
                    Vector3 face = alongX ? (row == 0 ? Vector3.back : Vector3.forward) : (row == 0 ? Vector3.left : Vector3.right);
                    Villa(plot, face, h, pos + w >= length - 0.01f, !twoRows || row == 0);
                    pos += w;
                }
            }
        }

        static bool mosqueBuilt, shopsBuilt;
        static MeshKit mosqueKit, shopKit, domeKit;
        static Texture2D mosqueTex, shopTex;

        /// Leftover narrow lots beside the plazas: paved with kerbs and a palm or two, never bare sand.
        static void PavedLot(R r)
        {
            const float h = 0.14f;
            var slab = new R(r.x0 + 0.25f, r.x1 - 0.25f, r.z0 + 0.25f, r.z1 - 0.25f);
            pavingKit.Box(slab.Center(h * 0.5f), new Vector3(slab.W, h, slab.D), Quaternion.identity, true);
            collKit.Box(slab.Center(h * 0.5f), new Vector3(slab.W, h, slab.D));
            KerbEdge(new Vector3(slab.x0, 0, slab.z0), new Vector3(slab.x1, 0, slab.z0), Vector3.back, h);
            KerbEdge(new Vector3(slab.x1, 0, slab.z0), new Vector3(slab.x1, 0, slab.z1), Vector3.right, h);
            KerbEdge(new Vector3(slab.x1, 0, slab.z1), new Vector3(slab.x0, 0, slab.z1), Vector3.forward, h);
            KerbEdge(new Vector3(slab.x0, 0, slab.z1), new Vector3(slab.x0, 0, slab.z0), Vector3.left, h);
            float len = Mathf.Max(slab.W, slab.D);
            for (float t = 6f; t < len - 4f; t += 14f)
            {
                Vector3 c = slab.W > slab.D ? new Vector3(slab.x0 + t, h, (slab.z0 + slab.z1) * 0.5f) : new Vector3((slab.x0 + slab.x1) * 0.5f, h, slab.z0 + t);
                Palm(c, Random.Range(6.5f, 9f));
            }
        }

        static void PhotoFront(MeshKit kit, Vector3 baseCentre, Vector3 outward, float width, float height, int copies)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -outward);   // viewer looks along -outward
            float w = width / copies;
            for (int i = 0; i < copies; i++)
            {
                Vector3 l = baseCentre - right * (width * 0.5f) + right * (w * i), rr = l + right * w;
                bool flip = i % 2 == 1;
                kit.QuadUv(l, rr, rr + Vector3.up * height, l + Vector3.up * height,
                    new Vector2(flip ? 1 : 0, 0), new Vector2(flip ? 0 : 1, 0), new Vector2(flip ? 0 : 1, 1), new Vector2(flip ? 1 : 0, 1));
            }
        }

        /// A 1980s neighbourhood mosque: prayer hall facing the avenue (photo front, mirrored to twice the width), a dome on
        /// the roof and an octagonal minaret with a balcony.
        static void Mosque(R slab, float h)
        {
            mosqueTex = mosqueTex != null ? mosqueTex : Tex("fac_mosque");
            float hallH = 7.5f, aspect = mosqueTex != null ? mosqueTex.width / (float)mosqueTex.height : 1.5f;
            float hallW = Mathf.Min(slab.D - 16f, aspect * hallH * 2f), hallD = 20f;
            Vector3 c = new Vector3(slab.x1 - 10f - hallD * 0.5f, h, (slab.z0 + slab.z1) * 0.5f);   // faces +x, the avenue
            houseKits[0].Box(c + Vector3.up * (hallH * 0.5f), new Vector3(hallD, hallH, hallW), Quaternion.identity, true);
            collKit.Box(c + Vector3.up * (hallH * 0.5f), new Vector3(hallD, hallH, hallW));
            if (mosqueTex != null)
            {
                PhotoFront(mosqueKit, c + Vector3.right * (hallD * 0.5f + 0.03f), Vector3.right, hallW, hallH, 2);
                PhotoFront(mosqueKit, c - Vector3.right * (hallD * 0.5f + 0.03f), Vector3.left, hallW, hallH, 2);
            }
            // Dome on a short drum
            Vector3 roof = c + Vector3.up * hallH;
            trimKit.Lathe(roof, new[] { new Vector2(0, 0), new Vector2(5.4f, 0), new Vector2(5.4f, 1.2f), new Vector2(0, 1.2f) }, 24);
            var prof = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float a = i / 10f * Mathf.PI * 0.5f; prof.Add(new Vector2(Mathf.Cos(a) * 5.2f, 1.2f + Mathf.Sin(a) * 5.6f)); }
            prof.Add(new Vector2(0f, 7.4f));
            domeKit.Lathe(roof, prof.ToArray(), 28);
            // Minaret at the front corner
            Vector3 m = new Vector3(slab.x1 - 6f, h, c.z + hallW * 0.5f + 3f);
            houseKits[0].Lathe(m, new[] { new Vector2(0, 0), new Vector2(2.2f, 0), new Vector2(2.2f, 2f), new Vector2(1.6f, 2.4f), new Vector2(1.6f, 22f), new Vector2(0, 22f) }, 8);
            trimKit.Lathe(m + Vector3.up * 21.5f, new[] { new Vector2(0, 0), new Vector2(2.4f, 0), new Vector2(2.4f, 1.1f), new Vector2(0, 1.1f) }, 8);
            houseKits[0].Lathe(m + Vector3.up * 22.6f, new[] { new Vector2(0, 0), new Vector2(1.2f, 0), new Vector2(1.2f, 4f), new Vector2(0, 4f) }, 8);
            domeKit.Lathe(m + Vector3.up * 26.6f, new[] { new Vector2(0, 0), new Vector2(1.4f, 0), new Vector2(0.9f, 1.6f), new Vector2(0.2f, 3.2f), new Vector2(0, 3.6f) }, 8);
            collKit.Box(m + Vector3.up * 11f, new Vector3(4f, 22f, 4f));
            for (int i = 0; i < 4; i++) Palm(new Vector3(slab.x1 - 4f, h, slab.z0 + 6f + i * (slab.D - 12f) / 3f), Random.Range(7f, 9.5f));
        }

        /// A row of small shops (baqala, tailor, barber) facing the avenue, with a paved forecourt.
        static void Shops(R slab, float h)
        {
            shopTex = shopTex != null ? shopTex : Tex("fac_baqala");
            float sh = 6.4f, aspect = shopTex != null ? shopTex.width / (float)shopTex.height : 1.5f;
            float len = Mathf.Min(slab.D - 10f, aspect * sh * 2f), dep = 10f;
            Vector3 c = new Vector3(slab.x0 + 10f + dep * 0.5f, h, (slab.z0 + slab.z1) * 0.5f);     // faces -x, the avenue
            houseKits[1].Box(c + Vector3.up * (sh * 0.5f), new Vector3(dep, sh, len), Quaternion.identity, true);
            collKit.Box(c + Vector3.up * (sh * 0.5f), new Vector3(dep, sh, len));
            if (shopTex != null) PhotoFront(shopKit, c - Vector3.right * (dep * 0.5f + 0.03f), Vector3.left, len, sh, 2);
            // Shade canopy along the shopfronts
            trimKit.Box(c - Vector3.right * (dep * 0.5f + 1.2f) + Vector3.up * 3.9f, new Vector3(2.4f, 0.15f, len), Quaternion.identity, true);
            for (int i = 0; i < 3; i++)
                parkSpots.Add((new Vector3(slab.x0 + 4f, h, c.z - len * 0.3f + i * len * 0.3f), Quaternion.LookRotation(Vector3.right)));
        }

        static void KerbEdge(Vector3 a, Vector3 b, Vector3 outward, float h)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.0f));
            Vector3 dir = (b - a) / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = a + dir * i + outward * 0.003f, p1 = a + dir * (i + 1) + outward * 0.003f;
                var kit = (i % 2 == 0) ? kerbYellow : kerbBlack;
                kit.Quad(p0 + Vector3.up * 0.018f, p1 + Vector3.up * 0.018f, p1 + Vector3.up * (h + 0.002f), p0 + Vector3.up * (h + 0.002f), outward);
                Vector3 q0 = p0 - outward * 0.3f, q1 = p1 - outward * 0.3f;
                kit.Quad(q0 + Vector3.up * (h + 0.002f), p0 + Vector3.up * (h + 0.002f), p1 + Vector3.up * (h + 0.002f), q1 + Vector3.up * (h + 0.002f), Vector3.up);
            }
        }

        static void Wall(Vector3 a, Vector3 b, float baseY, int palette, float gateAt = -1f, int gateColour = 0)
        {
            const float H = 2.6f, T = 0.3f;
            Vector3 dir = (b - a);
            float len = dir.magnitude;
            dir /= len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            void Seg(float s0, float s1)
            {
                if (s1 - s0 < 0.05f) return;
                Vector3 c = a + dir * ((s0 + s1) * 0.5f) + Vector3.up * (baseY + H * 0.5f);
                if (wallTex != null) wallKit.Box(c, new Vector3(T, H, s1 - s0), rot, true);
                else houseKits[palette].Box(c, new Vector3(T, H, s1 - s0), rot, true);
                trimKit.Box(c + Vector3.up * (H * 0.5f + 0.06f), new Vector3(T + 0.12f, 0.12f, s1 - s0), rot, true);
                // Limestone plinth along the foot of the wall (the photo wall already has its stone band)
                if (wallTex == null) stoneKit.Box(a + dir * ((s0 + s1) * 0.5f) + Vector3.up * (baseY + 0.3f), new Vector3(T + 0.06f, 0.6f, s1 - s0), rot, true);
                // Pillars every ~3.6 m, slightly proud and taller, with a white cap
                // Street-facing walls get pillars every ~3.6 m, side and back walls every ~8 m (cheaper)
                int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / (gateAt >= 0f ? 3.6f : 8f)));
                for (int k = 0; k <= n; k++)
                {
                    Vector3 pc = a + dir * Mathf.Lerp(s0 + 0.25f, s1 - 0.25f, k / (float)n) + Vector3.up * baseY;
                    houseKits[palette].Box(pc + Vector3.up * (H * 0.5f + 0.15f), new Vector3(T + 0.22f, H + 0.3f, 0.5f), rot, true);
                    trimKit.Box(pc + Vector3.up * (H + 0.36f), new Vector3(T + 0.34f, 0.12f, 0.62f), rot, true);
                    stoneKit.Box(pc + Vector3.up * 0.35f, new Vector3(T + 0.28f, 0.7f, 0.56f), rot, true);
                }
                collKit.Box(c, new Vector3(T, H, s1 - s0), rot);
            }
            if (gateAt < 0f) { Seg(0f, len); return; }
            const float G = 4.2f;
            Seg(0f, gateAt - G * 0.5f);
            Seg(gateAt + G * 0.5f, len);
            // Steel gate, slightly lower than the wall, with a pedestrian door beside it.
            Vector3 gc = a + dir * gateAt + Vector3.up * (baseY + 1.15f);
            int gi = gateColour % 2;
            if (gateTex[gi] != null)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                foreach (float sg in new[] { -1f, 1f })
                {
                    Vector3 o = gc + side * (sg * 0.045f);
                    Vector3 l = o - dir * (G * 0.5f) - Vector3.up * 1.15f, rr = o + dir * (G * 0.5f) - Vector3.up * 1.15f;
                    Vector3 up = Vector3.up * 2.3f;
                    gatePhotoKits[gi].QuadUv(l, rr, rr + up, l + up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                }
                gateKits[gateColour].Box(gc, new Vector3(0.06f, 2.3f, G), rot, true);
            }
            else gateKits[gateColour].Box(gc, new Vector3(0.08f, 2.3f, G), rot, true);
            for (int i = -3; i <= 3; i++)
                gateKits[gateColour].Box(gc + dir * (i * 0.55f) + Vector3.up * 1.2f, new Vector3(0.05f, 0.25f, 0.05f), rot, true);
            collKit.Box(gc, new Vector3(0.1f, 2.3f, G), rot);
        }

        static void Villa(R p, Vector3 face, float baseY, bool lastInRow, bool buildBack)
        {
            int pal = Random.Range(0, houseKits.Length);
            int gateCol = Random.Range(0, gateKits.Length);

            // Plot corners in a local frame: front edge faces the road.
            Vector3 c00 = new Vector3(p.x0, 0, p.z0), c10 = new Vector3(p.x1, 0, p.z0), c11 = new Vector3(p.x1, 0, p.z1), c01 = new Vector3(p.x0, 0, p.z1);
            Vector3 fa, fb, ba, bb; // front edge a->b, back edge
            if (face == Vector3.back) { fa = c00; fb = c10; ba = c01; bb = c11; }
            else if (face == Vector3.forward) { fa = c11; fb = c01; ba = c10; bb = c00; }
            else if (face == Vector3.left) { fa = c01; fb = c00; ba = c11; bb = c10; }
            else { fa = c10; fb = c11; ba = c00; bb = c01; }

            float frontLen = Vector3.Distance(fa, fb);
            float gateAt = Random.Range(4f, frontLen - 4f);
            Wall(fa, fb, baseY, pal, gateAt, gateCol);
            // Kerbside parking spot in the road, beside the gate (some get a parked car later)
            {
                Vector3 alongF = (fb - fa).normalized;
                Vector3 g = fa + alongF * gateAt;
                parkSpots.Add((new Vector3(g.x, baseY, g.z) + face * 1.35f + alongF * Random.Range(-5f, 5f), Quaternion.LookRotation(Random.value > 0.5f ? alongF : -alongF)));   // on the pavement by the wall, the Kuwaiti way: never in the traffic lane
            }
            // Each plot owns its side-A wall; the last plot in a row also closes side B.
            Wall(fa, ba, baseY, pal);
            if (lastInRow) Wall(fb, bb, baseY, pal);
            if (buildBack) Wall(ba, bb, baseY, pal);

            // House footprint: setbacks front 5, sides 2.5, back 3
            Vector3 fwdIn = -face;
            Vector3 along = (fb - fa).normalized;
            float depth = Vector3.Distance(fa, ba);
            float hw = frontLen * 0.5f - 2.5f;
            float hd = (depth - 5f - 3f) * 0.5f;
            if (hw < 4f || hd < 4f) return;
            bool twoStorey = Random.value > 0.3f;
            float H = twoStorey ? 6.8f : 3.6f;

            // Photo facade: the house takes the photo's proportions (two-storey ~8 m to the parapet top, single-storey
            // 4.6 m, mirrored twice across so it reads as one wide bungalow), sits inside the plot with a side yard.
            int fi = -1;
            float fw = 0f, fh = 0f;
            if (facades.Count > 0)
            {
                var pickList = new List<int>();
                for (int i = 0; i < facades.Count; i++) if (facades[i].two == twoStorey) pickList.Add(i);
                if (pickList.Count == 0) for (int i = 0; i < facades.Count; i++) pickList.Add(i);
                fi = pickList[Random.Range(0, pickList.Count)];
                twoStorey = facades[fi].two;
                fh = twoStorey ? Random.Range(7.8f, 8.4f) : 4.6f;
                fw = facades[fi].aspect * fh * (twoStorey ? 1f : 2f);
                if (fw > hw * 2f) { float k = hw * 2f / fw; fw *= k; fh *= k; }
                hw = fw * 0.5f;
                H = fh - 0.9f;            // the photo includes the parapet
            }
            float slack = (frontLen * 0.5f - 2.5f) - hw;
            Vector3 center = (fa + fb) * 0.5f + fwdIn * (5f + hd) + along * Random.Range(-slack, slack);
            var rot = Quaternion.LookRotation(fwdIn, Vector3.up);       // local +z = into the plot, local +x = along
            Vector3 size = new Vector3(hw * 2f, H, hd * 2f);
            houseKits[pal].Box(center + Vector3.up * (baseY + H * 0.5f), size, rot, true);
            collKit.Box(center + Vector3.up * (baseY + H * 0.5f), size, rot);
            if (fi >= 0)
            {
                PhotoHouse(center, fwdIn, along, hw, hd, H, fw, fh, baseY, pal, fi, twoStorey);
                if (Random.value > 0.45f)
                    Palm((fa + fb) * 0.5f + fwdIn * 2.6f + along * Random.Range(-hw, hw) + Vector3.up * baseY, Random.Range(6f, 9f));
                return;
            }
            // Stone-clad ground floor on the street side (or just a plinth), white floor band and cornice
            bool clad = Random.value > 0.45f;
            float cladH = clad ? 3.3f : 0.8f;
            stoneKit.Box(center - fwdIn * (hd + 0.05f) + Vector3.up * (baseY + cladH * 0.5f), new Vector3(hw * 2f + 0.1f, cladH, 0.1f), rot, true);
            stoneKit.Box(center + Vector3.up * (baseY + 0.4f), new Vector3(hw * 2f + 0.12f, 0.8f, hd * 2f + 0.12f), rot, true);
            if (twoStorey)
                trimKit.Box(center + Vector3.up * (baseY + 3.4f), new Vector3(hw * 2f + 0.3f, 0.22f, hd * 2f + 0.3f), rot, true);
            trimKit.Box(center + Vector3.up * (baseY + H - 0.1f), new Vector3(hw * 2f + 0.4f, 0.3f, hd * 2f + 0.4f), rot, true);

            // Parapet
            float py = baseY + H + 0.45f;
            var cap = houseKits[(pal + 2) % houseKits.Length];
            cap.Box(center - fwdIn * hd + Vector3.up * py, new Vector3(0.25f, 0.9f, hw * 2f + 0.25f), rot * Quaternion.Euler(0, 90, 0), true);
            cap.Box(center + fwdIn * hd + Vector3.up * py, new Vector3(0.25f, 0.9f, hw * 2f + 0.25f), rot * Quaternion.Euler(0, 90, 0), true);
            cap.Box(center - along * hw + Vector3.up * py, new Vector3(0.25f, 0.9f, hd * 2f), rot, true);
            cap.Box(center + along * hw + Vector3.up * py, new Vector3(0.25f, 0.9f, hd * 2f), rot, true);
            // Stepped Gulf parapet merlons on the street face
            int merl = Mathf.FloorToInt(hw * 2f / 1.6f);
            for (int m = 0; m < merl; m++)
            {
                float t = (m + 0.5f) / merl * 2f - 1f;
                trimKit.Box(center - fwdIn * hd + along * (t * hw) + Vector3.up * (py + 0.6f), new Vector3(0.7f, 0.3f, 0.3f), rot, true);
            }

            // Roof stair room + water tanks (no satellite dishes: it's the 80s)
            Vector3 roofC = center + Vector3.up * (baseY + H);
            houseKits[pal].Box(roofC + along * (hw * 0.4f) + Vector3.up * 1.3f, new Vector3(3.2f, 2.6f, 3.2f), rot, true);
            int tanks = Random.Range(1, 3);
            for (int i = 0; i < tanks; i++)
                tankKit.Lathe(roofC - along * (hw * 0.3f + i * 1.6f) + fwdIn * (hd * 0.3f),
                    new[] { new Vector2(0, 0), new Vector2(0.65f, 0), new Vector2(0.65f, 1.3f), new Vector2(0.5f, 1.5f), new Vector2(0, 1.55f) }, 14);

            // Front facade: windows with concrete sun-shade eyebrows, some window ACs
            int floors = twoStorey ? 2 : 1;
            int cols = Mathf.Max(2, Mathf.FloorToInt(hw * 2f / 3.6f));
            for (int f = 0; f < floors; f++)
                for (int i = 0; i < cols; i++)
                {
                    float t = (i + 0.5f) / cols * 2f - 1f;
                    Vector3 wc = center - fwdIn * (hd + 0.02f) + along * (t * (hw - 1.2f)) + Vector3.up * (baseY + 1.7f + f * 3.3f);
                    trimKit.Box(wc - fwdIn * 0.01f, new Vector3(1.56f, 1.76f, 0.1f), rot, true);
                    (Random.value < 0.35f ? litGlassKit : glassKit).Box(wc - fwdIn * 0.03f, new Vector3(1.3f, 1.5f, 0.1f), rot, true);
                    trimKit.Box(wc - Vector3.up * 0.86f - fwdIn * 0.1f, new Vector3(1.7f, 0.08f, 0.24f), rot, true);   // sill
                    houseKits[(pal + 2) % houseKits.Length].Box(wc + Vector3.up * 0.9f - fwdIn * 0.2f, new Vector3(1.8f, 0.1f, 0.42f), rot, true);
                    if (Random.value < 0.3f)
                        foreach (float sg in new[] { -1f, 1f })
                            woodKit.Box(wc + along * sg * 1.08f - fwdIn * 0.06f, new Vector3(0.62f, 1.6f, 0.06f), rot, true);
                    if (Random.value > 0.6f)
                        acKit.Box(wc - Vector3.up * 0.95f - fwdIn * 0.25f, new Vector3(0.7f, 0.45f, 0.55f), rot, true);
                }
            // Side windows
            for (int f = 0; f < floors; f++)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 wc = center + along * sgn * (hw + 0.02f) + Vector3.up * (baseY + 1.7f + f * 3.3f);
                    trimKit.Box(wc, new Vector3(0.1f, 1.6f, 1.3f), rot, true);
                    (Random.value < 0.3f ? litGlassKit : glassKit).Box(wc + along * sgn * 0.02f, new Vector3(0.1f, 1.4f, 1.1f), rot, true);
                }

            // Entrance: timber door, two columns and a slab canopy
            {
                Vector3 door = center - fwdIn * (hd + 0.04f) + along * (hw * 0.35f) + Vector3.up * baseY;
                woodKit.Box(door + Vector3.up * 1.2f, new Vector3(1.5f, 2.4f, 0.1f), rot, true);
                trimKit.Box(door + Vector3.up * 1.25f + fwdIn * 0.02f, new Vector3(1.8f, 2.6f, 0.08f), rot, true);
                trimKit.Box(door - fwdIn * 1.1f + Vector3.up * 3.0f, new Vector3(3.2f, 0.22f, 2.3f), rot, true);
                foreach (float sg in new[] { -1f, 1f })
                    trimKit.Cylinder(door - fwdIn * 2.0f + along * sg * 1.35f + Vector3.up * 1.45f, Vector3.up, 0.16f, 2.9f, 10);
                stoneKit.Box(door - fwdIn * 1.1f + Vector3.up * 0.1f, new Vector3(3.2f, 0.2f, 2.3f), rot, true);
            }
            // Upper balcony with a steel railing on some two-storey houses
            if (twoStorey && Random.value > 0.4f)
            {
                Vector3 bc = center - fwdIn * (hd + 0.6f) - along * (hw * 0.3f) + Vector3.up * (baseY + 3.45f);
                float bw = Mathf.Min(hw, 4.5f);
                trimKit.Box(bc, new Vector3(bw, 0.2f, 1.2f), rot, true);
                railKit.Box(bc + Vector3.up * 0.95f - fwdIn * 0.58f, new Vector3(bw, 0.06f, 0.06f), rot, true);
                for (int r = 0; r <= Mathf.FloorToInt(bw / 0.18f); r++)
                    railKit.Box(bc + Vector3.up * 0.5f - fwdIn * 0.58f + along * (-bw * 0.5f + r * 0.18f), new Vector3(0.025f, 0.9f, 0.025f), rot, true);
            }

            if (Random.value > 0.45f)
                Palm((fa + fb) * 0.5f + fwdIn * 2.6f + along * Random.Range(-hw, hw) + Vector3.up * baseY, Random.Range(6f, 9f));
        }

        /// The street face is the photo; the other three sides are plaster with a few small windows; parapet on the
        /// sides and back (the photo carries the front one), roof stair room and water tanks.
        static void PhotoHouse(Vector3 center, Vector3 fwdIn, Vector3 along, float hw, float hd, float H, float fw, float fh, float baseY, int pal, int fi, bool two)
        {
            var rot = Quaternion.LookRotation(fwdIn, Vector3.up);
            houseKits[pal].Box(center + Vector3.up * (baseY + H * 0.5f), new Vector3(hw * 2f, H, hd * 2f), rot, true);
            collKit.Box(center + Vector3.up * (baseY + H * 0.5f), new Vector3(hw * 2f, H, hd * 2f), rot);

            // Photo quad(s) a hair in front of the street face
            Vector3 f0 = center - fwdIn * (hd + 0.03f) + Vector3.up * baseY;
            var kit = facades[fi].kit;
            Vector3 right = Vector3.Cross(Vector3.up, fwdIn);           // the viewer's right, standing in the street looking in
            if (two)
            {
                Vector3 l = f0 - right * hw, rgt = f0 + right * hw;
                kit.QuadUv(l, rgt, rgt + Vector3.up * fh, l + Vector3.up * fh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            else
            {
                Vector3 l = f0 - right * hw, m = f0, rgt = f0 + right * hw;
                kit.QuadUv(l, m, m + Vector3.up * fh, l + Vector3.up * fh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                kit.QuadUv(m, rgt, rgt + Vector3.up * fh, m + Vector3.up * fh, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            }
            // The back of the house faces the next street or the avenue: give it a real face too (another photo).
            {
                var bk = facades[(fi + 1 + (int)(Mathf.Abs(center.x * 7f + center.z * 3f)) % (facades.Count)) % facades.Count];
                if (bk.two == two)
                {
                    Vector3 b0 = center + fwdIn * (hd + 0.03f) + Vector3.up * baseY;
                    Vector3 l = b0 + right * hw, rgt = b0 - right * hw;      // seen from behind, left and right swap
                    if (two) bk.kit.QuadUv(l, rgt, rgt + Vector3.up * fh, l + Vector3.up * fh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    else
                    {
                        bk.kit.QuadUv(l, b0, b0 + Vector3.up * fh, l + Vector3.up * fh, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                        bk.kit.QuadUv(b0, rgt, rgt + Vector3.up * fh, b0 + Vector3.up * fh, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
                    }
                }
            }
            // Parapet behind the photo's own: sides and back only, same height as the photo top
            float py = baseY + H + 0.45f;
            var cap = houseKits[pal];
            cap.Box(center + fwdIn * hd + Vector3.up * py, new Vector3(0.25f, 0.9f, hw * 2f + 0.25f), rot * Quaternion.Euler(0, 90, 0), true);
            cap.Box(center - along * hw + Vector3.up * py + fwdIn * 0.1f, new Vector3(0.25f, 0.9f, hd * 2f - 0.2f), rot, true);
            cap.Box(center + along * hw + Vector3.up * py + fwdIn * 0.1f, new Vector3(0.25f, 0.9f, hd * 2f - 0.2f), rot, true);
            trimKit.Box(center + Vector3.up * (py + 0.47f) + fwdIn * hd, new Vector3(hw * 2f + 0.35f, 0.06f, 0.33f), rot, true);

            Vector3 roofC = center + Vector3.up * (baseY + H);
            houseKits[pal].Box(roofC + along * (hw * 0.4f) + fwdIn * (hd * 0.35f) + Vector3.up * 1.3f, new Vector3(3.2f, 2.6f, 3.2f), rot, true);
            int tanks = Random.Range(1, 3);
            for (int i = 0; i < tanks; i++)
                tankKit.Lathe(roofC - along * (hw * 0.3f + i * 1.6f) + fwdIn * (hd * 0.45f),
                    new[] { new Vector2(0, 0), new Vector2(0.65f, 0), new Vector2(0.65f, 1.3f), new Vector2(0.5f, 1.5f), new Vector2(0, 1.55f) }, 14);

            // Small side windows with frames (seen when driving past the side yards)
            int floors = two ? 2 : 1;
            for (int f = 0; f < floors; f++)
                foreach (float sgn in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 wc = center + along * sgn * (hw + 0.02f) + fwdIn * ((k == 0 ? -0.45f : 0.35f) * hd) + Vector3.up * (baseY + 1.7f + f * 3.4f);
                        trimKit.Box(wc, new Vector3(0.1f, 1.5f, 1.2f), rot, true);
                        (Random.value < 0.3f ? litGlassKit : glassKit).Box(wc + along * sgn * 0.02f, new Vector3(0.1f, 1.3f, 1.0f), rot, true);
                        if (Random.value > 0.65f) acKit.Box(wc - Vector3.up * 0.95f + along * sgn * 0.3f, new Vector3(0.55f, 0.45f, 0.7f), rot, true);
                    }
        }

        /// Cars parked at the kerb outside some gates: the garage models, no physics, solid to hit.
        static void ParkedCars(Transform root)
        {
            var parent = new GameObject("ParkedCars").transform;
            parent.SetParent(root, false);
            string[] ids = { "tr_sedan_big", "tr_suv", "tr_pickup", "tr_compact", "tr_taxi" };
            int placed = 0;
            var used = new List<Vector3>();
            for (int i = 0; i < parkSpots.Count && placed < 10; i++)
            {
                if (Random.value > 0.22f) continue;
                var (pos, rot) = parkSpots[i];
                if (InPlaza(pos.x, pos.z, 6f)) continue;
                int on = 0;
                foreach (var rd in roads) if (rd.Contains(pos.x, pos.z, 7f)) on++;
                if (on != 1) continue;                    // keep junctions clear
                if (Mathf.Abs(pos.x) < 12f && Mathf.Abs(pos.z + 390f) < 60f) continue;   // drive start
                bool near = false;
                foreach (var u in used) if ((u - pos).sqrMagnitude < 100f) near = true;
                if (near) continue;
                var car = CarFactory.BuildDisplay(ids[placed % ids.Length], parent) ?? CarFactory.BuildDisplay("ghazal", parent);
                if (car == null) continue;
                car.transform.SetPositionAndRotation(pos + Vector3.up * 0.02f, rot);
                var bc = car.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.8f, 1.4f, 4.6f);
                used.Add(pos);
                placed++;
            }
            Debug.Log($"[World] parked cars: {placed} of {parkSpots.Count} spots");
        }

        // ------------------------------------------------------------------ props
        static void Palm(Vector3 b, float height)
        {
            trunkKit.Lathe(b, new[] { new Vector2(0, 0), new Vector2(0.34f, 0), new Vector2(0.27f, 0.6f), new Vector2(0.22f, height * 0.5f),
                                      new Vector2(0.19f, height), new Vector2(0, height + 0.05f) }, 12);
            Vector3 top = b + Vector3.up * height;
            int fronds = 20;
            float twist = Random.Range(0f, 360f);
            for (int i = 0; i < fronds; i++)
            {
                float yaw = twist + i * (360f / fronds) + Random.Range(-8f, 8f);
                float lift = (i % 3 == 0) ? Random.Range(25f, 45f) : Random.Range(-5f, 20f);
                float len = Random.Range(3.4f, 4.4f);
                var yr = Quaternion.Euler(0f, yaw, 0f);
                Vector3 fwd = yr * Vector3.forward, side = yr * Vector3.right;
                // Arc: starts rising, droops under its own weight. 5 segments, leaflet texture along v.
                const int S = 5;
                Vector3 prev = top; float prevW = 0.25f;
                for (int k = 1; k <= S; k++)
                {
                    float t = k / (float)S;
                    float ang = (lift - t * 85f) * Mathf.Deg2Rad;
                    Vector3 pt = prev + (fwd * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)) * (len / S);
                    float w = Mathf.Lerp(1.3f, 0.35f, t) * (k == 1 ? 0.8f : 1f);
                    Vector3 roll = side * 0.5f;
                    frondKit.QuadUv(prev - roll * prevW + Vector3.up * 0.02f, prev + roll * prevW + Vector3.up * 0.02f, pt + roll * w, pt - roll * w,
                        new Vector2(0.5f - prevW * 0.5f, (k - 1) / (float)S), new Vector2(0.5f + prevW * 0.5f, (k - 1) / (float)S),
                        new Vector2(0.5f + w * 0.5f, t), new Vector2(0.5f - w * 0.5f, t));
                    prev = pt; prevW = w;
                }
            }
            // Date clusters
            dateKit.Box(top - Vector3.up * 0.5f, new Vector3(0.5f, 0.45f, 0.5f), Quaternion.Euler(0, twist, 0));
            dateKit.Box(top - Vector3.up * 0.55f + Vector3.right * 0.3f, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.Euler(0, twist + 40f, 0));
        }

        static void Lamps(Transform root)
        {
            var parent = new GameObject("StreetLamps").transform;
            parent.SetParent(root, false);
            var pole = new MeshKit();
            int n = 0;
            for (float z = -420f; z <= 420f; z += 32f)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    float x = sgn * 11.2f;
                    if (InPlaza(x, z, 3f) || CrossesRoad(x, z, 0)) continue;
                    Vector3 b = new Vector3(x, 0.14f, z);
                    pole.Lathe(b, new[] { new Vector2(0, 0), new Vector2(0.16f, 0), new Vector2(0.11f, 1.0f), new Vector2(0.08f, 9f), new Vector2(0, 9.05f) }, 8);
                    pole.Box(b + new Vector3(-sgn * 0.9f, 8.9f, 0f), new Vector3(1.8f, 0.1f, 0.1f));
                    collKit.Box(b + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f));

                    var head = new GameObject("Lamp");
                    head.transform.SetParent(parent, false);
                    head.transform.position = b + new Vector3(-sgn * 1.75f, 8.8f, 0f);
                    var hk = new MeshKit();
                    hk.Box(Vector3.zero, new Vector3(0.7f, 0.18f, 0.32f));
                    var mf = head.AddComponent<MeshFilter>(); mf.sharedMesh = hk.ToMesh("LampHead");
                    var mr = head.AddComponent<MeshRenderer>();
                    var sl = head.AddComponent<StreetLamp>();
                    sl.bulb = mr;
                    if (n++ % 2 == 0)
                    {
                        // Sodium pool on the road under the lamp (Forward+ handles many lights per tile).
                        var lgo = new GameObject("Pool");
                        lgo.transform.SetParent(head.transform, false);
                        lgo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Spot; l.range = 24f; l.spotAngle = 125f; l.innerSpotAngle = 60f;
                        l.intensity = 140f; l.color = new Color(1f, 0.6f, 0.26f);
                        l.shadows = LightShadows.None;
                        sl.lamp = l;
                    }
                    sl.SetOn(false);
                }
            pole.ToObject("LampPoles", root, Mat.Paint("M_LampPole", new Color(0.55f, 0.55f, 0.52f), 0.45f, 0.6f));
        }

        static void Skyline(Transform root)
        {
            // Neighbourhood water towers: the skyline anchors you see from every street.
            var tower = new MeshKit();
            foreach (var p in new[] { new Vector3(320f, 0f, 260f), new Vector3(-340f, 0f, -150f), new Vector3(140f, 0f, 560f) })
                tower.Lathe(p, new[] { new Vector2(0, 0), new Vector2(3.4f, 0), new Vector2(2.6f, 2f), new Vector2(2.2f, 30f), new Vector2(6f, 33f),
                                       new Vector2(12.5f, 37f), new Vector2(12f, 40f), new Vector2(6f, 44f), new Vector2(0.8f, 46f), new Vector2(0, 46.5f) }, 40);
            tower.ToObject("WaterTowers", root, Mat.Paint("M_Tower", new Color(0.90f, 0.90f, 0.87f), 0.5f));
            var band = new MeshKit();
            foreach (var p in new[] { new Vector3(320f, 0f, 260f), new Vector3(-340f, 0f, -150f), new Vector3(140f, 0f, 560f) })
                band.Lathe(p + Vector3.up * 37.4f, new[] { new Vector2(0, 0), new Vector2(12.62f, 0), new Vector2(12.62f, 1.2f), new Vector2(0, 1.2f) }, 40);
            band.ToObject("TowerBands", root, Mat.Paint("M_TowerBand", new Color(0.12f, 0.42f, 0.48f), 0.55f));

            // Low distant houses fading into the haze.
            var far = new MeshKit();
            for (int i = 0; i < 260; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(470f, 900f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 s = new Vector3(Random.Range(12f, 26f), Random.Range(4f, 8f), Random.Range(12f, 26f));
                far.Box(p + Vector3.up * s.y * 0.5f, s, Quaternion.Euler(0f, Random.Range(0f, 90f), 0f), true);
            }
            far.ToObject("FarHouses", root, Mat.Paint("M_Far", new Color(0.84f, 0.76f, 0.63f), 0.1f));
        }

        // ------------------------------------------------------------------ flush batches
        static void Flush(Transform root)
        {
            var village = new GameObject("Villas").transform;
            village.SetParent(root, false);
            var plaster = sideTex != null ? sideTex : TextureKit.Plaster;
            for (int i = 0; i < houseKits.Length; i++)
                houseKits[i].ToObject("Houses_" + i, village, Mat.Textured("M_House_" + i, plaster,
                    sideTex != null ? Color.Lerp(HousePalette[i], Color.white, 0.15f) * 0.92f : HousePalette[i], 0.12f, new Vector2(0.2f, 0.2f)));
            for (int i = 0; i < facades.Count; i++)
            {
                var fm = Mat.Textured("M_Facade_" + i, facades[i].tex, Color.white, 0.18f, Vector2.one);
                fm.SetFloat("_Cull", 0f);
                fm.SetFloat("_AlphaClip", 1f); fm.SetFloat("_Cutoff", 0.5f); fm.EnableKeyword("_ALPHATEST_ON");   // sky cut out round the parapet
                fm.renderQueue = 2450;
                if (fm.HasProperty("_BumpScale")) fm.SetFloat("_BumpScale", 0.6f);
                facades[i].kit.ToObject("Facade_" + facades[i].tex.name, village, fm);
            }
            if (mosqueTex != null) { var mm = Mat.Textured("M_MosquePhoto", mosqueTex, Color.white, 0.15f, Vector2.one); mm.SetFloat("_Cull", 0f); mosqueKit.ToObject("Mosque_Front", village, mm); }
            if (shopTex != null) { var sm = Mat.Textured("M_ShopsPhoto", shopTex, Color.white, 0.15f, Vector2.one); sm.SetFloat("_Cull", 0f); shopKit.ToObject("Shops_Front", village, sm); }
            domeKit.ToObject("Mosque_Dome", village, Mat.Paint("M_Dome", new Color(0.86f, 0.84f, 0.78f), 0.35f));
            if (wallTex != null)
            {
                // Band texture: u repeats every 3.9 m along the wall, v spans the 2.6 m height (walls start 0.14 m up).
                var wm = Mat.Textured("M_BoundaryWall", wallTex, Color.white, 0.12f, new Vector2(1f / 3.9f, 1f / 2.6f));
                wm.SetTextureOffset("_BaseMap", new Vector2(0f, -0.14f / 2.6f));
                wm.SetTextureOffset("_BumpMap", new Vector2(0f, -0.14f / 2.6f));
                wallKit.ToObject("BoundaryWalls", village, wm);
            }
            for (int i = 0; i < gatePhotoKits.Length; i++)
                if (gateTex[i] != null)
                {
                    var gm = Mat.Textured("M_GatePhoto_" + i, gateTex[i], Color.white, 0.35f, Vector2.one);
                    gm.SetFloat("_Cull", 0f); gm.SetFloat("_Metallic", 0f); gm.SetFloat("_Smoothness", 0.25f);
                    gatePhotoKits[i].ToObject("Gates_Photo_" + i, village, gm);
                }
            for (int i = 0; i < gateKits.Length; i++)
                gateKits[i].ToObject("Gates_" + i, village, Mat.Paint("M_Gate_" + i, GatePalette[i], 0.45f, 0.7f));
            glassKit.ToObject("Windows", village, Mat.Paint("M_Window", new Color(0.17f, 0.20f, 0.21f), 0.93f, 0.25f));
            litWindowMat = Mat.Glow("M_WindowLit", new Color(1f, 0.72f, 0.42f), 0f);
            litWindowMat.SetFloat("_Smoothness", 0.9f);
            litGlassKit.ToObject("WindowsLit", village, litWindowMat);
            stoneKit.ToObject("Stone", village, Mat.Textured("M_Stone", TextureKit.Stone, Color.white, 0.25f, new Vector2(0.83f, 0.83f)));
            trimKit.ToObject("Trim", village, Mat.Paint("M_Trim", new Color(0.95f, 0.94f, 0.90f), 0.35f));
            woodKit.ToObject("Timber", village, Mat.Paint("M_Timber", new Color(0.36f, 0.22f, 0.12f), 0.4f));
            railKit.ToObject("Railings", village, Mat.Paint("M_Rail", new Color(0.12f, 0.13f, 0.13f), 0.5f, 0.6f));
            dateKit.ToObject("Dates", root, Mat.Paint("M_Dates", new Color(0.55f, 0.30f, 0.08f), 0.3f));
            acKit.ToObject("WindowACs", village, Mat.Paint("M_AC", new Color(0.82f, 0.80f, 0.74f), 0.4f, 0.3f));
            tankKit.ToObject("WaterTanks", village, Mat.Paint("M_Tank", new Color(0.90f, 0.88f, 0.80f), 0.35f));
            pavingKit.ToObject("Pavements", root, Mat.Textured("M_Paving", TextureKit.Paving, Color.white, 0.2f, new Vector2(0.6f, 0.6f)));
            kerbYellow.ToObject("Kerb_Yellow", root, Mat.Paint("M_KerbY", new Color(0.95f, 0.75f, 0.10f), 0.35f));
            kerbBlack.ToObject("Kerb_Black", root, Mat.Paint("M_KerbB", new Color(0.06f, 0.06f, 0.06f), 0.35f));
            paintKit.ToObject("RoadPaint", root, Mat.Paint("M_RoadPaint", new Color(0.92f, 0.92f, 0.88f), 0.45f));
            trunkKit.ToObject("PalmTrunks", root, TextureKit.Trunk != null
                ? Mat.Textured("M_PalmTrunkTex", TextureKit.Trunk, Color.white, 0.1f, new Vector2(1.4f, 0.9f))
                : Mat.Paint("M_PalmTrunk", new Color(0.42f, 0.33f, 0.24f), 0.15f));
            var frondMat = Mat.Textured("M_PalmFrondTex", TextureKit.Frond, Color.white, 0.2f, Vector2.one);
            frondMat.SetFloat("_AlphaClip", 1f); frondMat.SetFloat("_Cutoff", 0.4f); frondMat.EnableKeyword("_ALPHATEST_ON");
            frondMat.SetFloat("_Cull", 0f);
            frondMat.renderQueue = 2450;
            frondKit.ToObject("PalmFronds", root, frondMat);

            var coll = new GameObject("StaticColliders");
            coll.transform.SetParent(root, false);
            coll.AddComponent<MeshCollider>().sharedMesh = collKit.ToMesh("StaticColliders");
            coll.AddComponent<SurfaceType>(); // kerb tops / pavement count as asphalt-grip
        }
    }
}
