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

            Ground(root);
            Roads(root);
            Roundabout(root, new Vector3(0f, 0f, 0f), 15f, true);
            Roundabout(root, new Vector3(0f, 0f, -240f), 9f, false);
            Blocks();
            Lamps(root);
            Skyline(root);
            Flush(root);
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
                houseKits[palette].Box(c, new Vector3(T, H, s1 - s0), rot, true);
                houseKits[(palette + 2) % houseKits.Length].Box(c + Vector3.up * (H * 0.5f + 0.06f), new Vector3(T + 0.08f, 0.12f, s1 - s0), rot, true);
                collKit.Box(c, new Vector3(T, H, s1 - s0), rot);
            }
            if (gateAt < 0f) { Seg(0f, len); return; }
            const float G = 4.2f;
            Seg(0f, gateAt - G * 0.5f);
            Seg(gateAt + G * 0.5f, len);
            // Steel gate, slightly lower than the wall, with a pedestrian door beside it.
            Vector3 gc = a + dir * gateAt + Vector3.up * (baseY + 1.15f);
            gateKits[gateColour].Box(gc, new Vector3(0.08f, 2.3f, G), rot, true);
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
            Wall(fa, fb, baseY, pal, Random.Range(4f, frontLen - 4f), gateCol);
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
            Vector3 center = (fa + fb) * 0.5f + fwdIn * (5f + hd);
            bool twoStorey = Random.value > 0.3f;
            float H = twoStorey ? 6.8f : 3.6f;
            var rot = Quaternion.LookRotation(fwdIn, Vector3.up);       // local +z = into the plot, local +x = along
            Vector3 size = new Vector3(hw * 2f, H, hd * 2f);
            houseKits[pal].Box(center + Vector3.up * (baseY + H * 0.5f), size, rot, true);
            collKit.Box(center + Vector3.up * (baseY + H * 0.5f), size, rot);

            // Parapet
            float py = baseY + H + 0.45f;
            var cap = houseKits[(pal + 2) % houseKits.Length];
            cap.Box(center - fwdIn * hd + Vector3.up * py, new Vector3(0.25f, 0.9f, hw * 2f + 0.25f), rot * Quaternion.Euler(0, 90, 0), true);
            cap.Box(center + fwdIn * hd + Vector3.up * py, new Vector3(0.25f, 0.9f, hw * 2f + 0.25f), rot * Quaternion.Euler(0, 90, 0), true);
            cap.Box(center - along * hw + Vector3.up * py, new Vector3(0.25f, 0.9f, hd * 2f), rot, true);
            cap.Box(center + along * hw + Vector3.up * py, new Vector3(0.25f, 0.9f, hd * 2f), rot, true);

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
                    glassKit.Box(wc, new Vector3(1.3f, 1.5f, 0.06f), rot, true);
                    houseKits[(pal + 2) % houseKits.Length].Box(wc + Vector3.up * 0.9f - fwdIn * 0.2f, new Vector3(1.8f, 0.1f, 0.42f), rot, true);
                    if (Random.value > 0.6f)
                        acKit.Box(wc - Vector3.up * 0.95f - fwdIn * 0.25f, new Vector3(0.7f, 0.45f, 0.55f), rot, true);
                }
            // Side windows
            for (int f = 0; f < floors; f++)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 wc = center + along * sgn * (hw + 0.02f) + Vector3.up * (baseY + 1.7f + f * 3.3f);
                    glassKit.Box(wc, new Vector3(0.06f, 1.4f, 1.1f), rot, true);
                }

            if (Random.value > 0.45f)
                Palm((fa + fb) * 0.5f + fwdIn * 2.6f + along * Random.Range(-hw, hw) + Vector3.up * baseY, Random.Range(6f, 9f));
        }

        // ------------------------------------------------------------------ props
        static void Palm(Vector3 b, float height)
        {
            trunkKit.Lathe(b, new[] { new Vector2(0, 0), new Vector2(0.34f, 0), new Vector2(0.27f, 0.6f), new Vector2(0.22f, height * 0.5f),
                                      new Vector2(0.19f, height), new Vector2(0, height + 0.05f) }, 8);
            Vector3 top = b + Vector3.up * height;
            int fronds = 13;
            float twist = Random.Range(0f, 360f);
            for (int i = 0; i < fronds; i++)
            {
                float yaw = twist + i * (360f / fronds) + Random.Range(-8f, 8f);
                float pitch = (i % 3 == 0) ? Random.Range(-10f, 10f) : Random.Range(20f, 48f);
                var rot = Quaternion.Euler(pitch, yaw, 0f);
                frondKit.Box(top + rot * new Vector3(0f, 0f, 1.7f), new Vector3(0.75f, 0.03f, 3.4f), rot);
            }
            // Date clusters
            frondKit.Box(top - Vector3.up * 0.5f, new Vector3(0.5f, 0.45f, 0.5f), Quaternion.Euler(0, twist, 0));
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
                    if (n++ % 3 == 0)
                    {
                        var l = head.AddComponent<Light>();
                        l.type = LightType.Point; l.range = 26f; l.intensity = 9f; l.color = new Color(1f, 0.62f, 0.25f);
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
            var plaster = TextureKit.Plaster;
            for (int i = 0; i < houseKits.Length; i++)
                houseKits[i].ToObject("Houses_" + i, village, Mat.Textured("M_House_" + i, plaster, HousePalette[i], 0.15f, new Vector2(0.18f, 0.18f)));
            for (int i = 0; i < gateKits.Length; i++)
                gateKits[i].ToObject("Gates_" + i, village, Mat.Paint("M_Gate_" + i, GatePalette[i], 0.45f, 0.7f));
            glassKit.ToObject("Windows", village, Mat.Paint("M_Window", new Color(0.08f, 0.10f, 0.11f), 0.92f, 0.2f));
            acKit.ToObject("WindowACs", village, Mat.Paint("M_AC", new Color(0.82f, 0.80f, 0.74f), 0.4f, 0.3f));
            tankKit.ToObject("WaterTanks", village, Mat.Paint("M_Tank", new Color(0.90f, 0.88f, 0.80f), 0.35f));
            pavingKit.ToObject("Pavements", root, Mat.Textured("M_Paving", TextureKit.Paving, Color.white, 0.2f, new Vector2(0.6f, 0.6f)));
            kerbYellow.ToObject("Kerb_Yellow", root, Mat.Paint("M_KerbY", new Color(0.95f, 0.75f, 0.10f), 0.35f));
            kerbBlack.ToObject("Kerb_Black", root, Mat.Paint("M_KerbB", new Color(0.06f, 0.06f, 0.06f), 0.35f));
            paintKit.ToObject("RoadPaint", root, Mat.Paint("M_RoadPaint", new Color(0.92f, 0.92f, 0.88f), 0.45f));
            trunkKit.ToObject("PalmTrunks", root, Mat.Paint("M_PalmTrunk", new Color(0.42f, 0.33f, 0.24f), 0.15f));
            frondKit.ToObject("PalmFronds", root, Mat.Paint("M_PalmFrond", new Color(0.34f, 0.41f, 0.20f), 0.25f));

            var coll = new GameObject("StaticColliders");
            coll.transform.SetParent(root, false);
            coll.AddComponent<MeshCollider>().sharedMesh = collKit.ToMesh("StaticColliders");
            coll.AddComponent<SurfaceType>(); // kerb tops / pavement count as asphalt-grip
        }
    }
}
