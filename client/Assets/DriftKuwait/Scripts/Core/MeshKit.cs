using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Small procedural-mesh toolkit: extrusions of side profiles (cars, walls), boxes, cylinders.
    /// Every triangle is wound by its outward direction, so faces always point the right way.
    public class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int> tris = new List<int>();
        readonly List<Vector2> uvs = new List<Vector2>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(Uv(a, outward)); uvs.Add(Uv(b, outward)); uvs.Add(Uv(c, outward));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Tri(a, b, c, outward);
            Tri(a, c, d, outward);
        }

        /// Quad with explicit UVs (no winding fix-up: use a double-sided material).
        public void QuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // Planar box-projected UVs in metres (good for tiling textures later).
        static Vector2 Uv(Vector3 p, Vector3 n)
        {
            n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (n.y >= n.x && n.y >= n.z) return new Vector2(p.x, p.z);
            if (n.x >= n.z) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        /// Extrude a CCW side profile (x = forward Z, y = up) across width along X.
        public void ExtrudeProfile(IList<Vector2> profile, float width, Vector3 offset)
        {
            float hx = width * 0.5f;
            Vector3 P(Vector2 p, float x) => offset + new Vector3(x, p.y, p.x);

            var idx = Triangulate(profile);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector2 a = profile[idx[t]], b = profile[idx[t + 1]], c = profile[idx[t + 2]];
                Tri(P(a, hx), P(b, hx), P(c, hx), Vector3.right);
                Tri(P(a, -hx), P(b, -hx), P(c, -hx), Vector3.left);
            }
            for (int i = 0; i < profile.Count; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[(i + 1) % profile.Count];
                Vector2 e = p1 - p0;
                Vector3 outward = new Vector3(0f, -e.x, e.y); // CCW right-hand normal mapped to 3D
                Quad(P(p0, -hx), P(p0, hx), P(p1, hx), P(p1, -hx), outward);
            }
        }

        public void Box(Vector3 center, Vector3 size) => Box(center, size, Quaternion.identity);

        public void Box(Vector3 center, Vector3 size, Quaternion rot, bool skipBottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 C(float x, float y, float z) => center + rot * new Vector3(x, y, z);
            Vector3[] p =
            {
                C(-h.x, -h.y, -h.z), C(h.x, -h.y, -h.z), C(h.x, h.y, -h.z), C(-h.x, h.y, -h.z),
                C(-h.x, -h.y, h.z),  C(h.x, -h.y, h.z),  C(h.x, h.y, h.z),  C(-h.x, h.y, h.z)
            };
            Quad(p[0], p[1], p[2], p[3], rot * Vector3.back);
            Quad(p[5], p[4], p[7], p[6], rot * Vector3.forward);
            Quad(p[4], p[0], p[3], p[7], rot * Vector3.left);
            Quad(p[1], p[5], p[6], p[2], rot * Vector3.right);
            Quad(p[3], p[2], p[6], p[7], rot * Vector3.up);
            if (!skipBottom) Quad(p[4], p[5], p[1], p[0], rot * Vector3.down);
        }

        /// Surface of revolution around +Y. Profile = (radius, height) from the bottom axis
        /// outwards, up, and back in to the top axis.
        public void Lathe(Vector3 basePos, IList<Vector2> profile, int segments)
        {
            for (int i = 0; i < profile.Count - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                Vector2 e = b - a;
                Vector2 n2 = new Vector2(e.y, -e.x);
                for (int s = 0; s < segments; s++)
                {
                    float t0 = s * Mathf.PI * 2f / segments, t1 = (s + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0));
                    Vector3 d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                    Vector3 dm = (d0 + d1).normalized;
                    Vector3 outward = dm * n2.x + Vector3.up * n2.y;
                    Vector3 a0 = basePos + d0 * a.x + Vector3.up * a.y, a1 = basePos + d1 * a.x + Vector3.up * a.y;
                    Vector3 b0 = basePos + d0 * b.x + Vector3.up * b.y, b1 = basePos + d1 * b.x + Vector3.up * b.y;
                    if (a.x < 1e-4f) Tri(a0, b1, b0, outward);
                    else if (b.x < 1e-4f) Tri(a0, a1, b0, outward);
                    else Quad(a0, a1, b1, b0, outward);
                }
            }
        }

        /// Flat ring (annulus) in the XZ plane, facing up.
        public void Ring(Vector3 center, float inner, float outer, int segments)
        {
            for (int s = 0; s < segments; s++)
            {
                float t0 = s * Mathf.PI * 2f / segments, t1 = (s + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0));
                Vector3 d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                Quad(center + d0 * inner, center + d1 * inner, center + d1 * outer, center + d0 * outer, Vector3.up);
            }
        }

        public int VertexCount => verts.Count;

        /// Cylinder along an axis (axis = Vector3.up or Vector3.right ...).
        public void Cylinder(Vector3 center, Vector3 axis, float radius, float length, int segments, bool caps = true)
        {
            axis = axis.normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(axis, u);
            Vector3 top = center + axis * length * 0.5f, bot = center - axis * length * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 r0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * radius;
                Vector3 r1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * radius;
                Vector3 mid = (r0 + r1).normalized;
                Quad(bot + r0, bot + r1, top + r1, top + r0, mid);
                if (caps)
                {
                    Tri(top, top + r0, top + r1, axis);
                    Tri(bot, bot + r1, bot + r0, -axis);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.SetUVs(0, uvs);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        public GameObject ToObject(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---- Ear-clipping triangulation for simple CCW polygons ----
        public static List<int> Triangulate(IList<Vector2> poly)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            if (SignedArea(poly) < 0f) idx.Reverse();

            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // reflex
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (PointInTri(poly[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float SignedArea(IList<Vector2> p)
        {
            float s = 0f;
            for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.y - b.x * a.y; }
            return s * 0.5f;
        }

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 > 0f && d2 > 0f && d3 > 0f;
        }
    }

    /// URP material factory with a cache so repeated colours share one material.
    public static class Mat
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit;

        static Shader Lit
        {
            get
            {
                if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard"); // fallback if URP is missing
                return lit;
            }
        }

        public static Material Paint(string key, Color c, float smooth = 0.5f, float metal = 0f)
        {
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Lit) { name = key };
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        public static Material Glow(string key, Color c, float intensity)
        {
            if (cache.TryGetValue(key, out var m)) return m;
            m = Paint(key + "_base", c * 0.3f, 0.8f);
            m = new Material(m) { name = key };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cache[key] = m;
            return m;
        }

        public static Material Textured(string key, Texture2D tex, Color tint, float smooth, Vector2 tiling)
        {
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Paint(key + "_b", tint, smooth)) { name = key };
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureScale("_MainTex", tiling);
            var nrm = tex != null ? Resources.Load<Texture2D>("DriftKuwait/Tex/" + tex.name + "_n") : null;
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm);
                m.SetTextureScale("_BumpMap", tiling);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            cache[key] = m;
            return m;
        }
    }
}
