using UnityEngine;
using UnityEngine.Rendering;

namespace DriftKuwait
{
    /// One mesh, ring buffer of quads. Marks stay for the session (a few thousand metres).
    public class SkidMarks : MonoBehaviour
    {
        public static SkidMarks Instance { get; private set; }
        const int MaxMarks = 3000;
        const float Width = 0.21f;

        struct Section { public Vector3 pos, normal, left, right; public float alpha; public int last; }

        readonly Section[] sections = new Section[MaxMarks];
        int count;
        Mesh mesh;
        Vector3[] verts;
        Color32[] cols;
        Vector2[] uvs;
        int[] tris;
        bool dirty;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("DK_SkidMarks");
            Instance = go.AddComponent<SkidMarks>();
        }

        void Awake()
        {
            Instance = this;
            mesh = new Mesh { name = "SkidMarks" };
            mesh.MarkDynamic();
            verts = new Vector3[MaxMarks * 4];
            cols = new Color32[MaxMarks * 4];
            uvs = new Vector2[MaxMarks * 4];
            tris = new int[MaxMarks * 6];
            for (int i = 0; i < MaxMarks; i++)
            {
                tris[i * 6 + 0] = i * 4 + 0; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1;
                tris[i * 6 + 3] = i * 4 + 2; tris[i * 6 + 4] = i * 4 + 3; tris[i * 6 + 5] = i * 4 + 1;
            }
            mesh.vertices = verts;
            mesh.colors32 = cols;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5000f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = TyreFX.ParticleMaterial("M_Skid", Texture2D.whiteTexture, new Color(0.05f, 0.045f, 0.04f, 0.62f));
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        public int Add(Vector3 pos, Vector3 normal, float intensity, int lastIndex)
        {
            int idx = count % MaxMarks;
            count++;
            var s = new Section { pos = pos + normal * 0.012f, normal = normal, alpha = intensity * 0.85f, last = lastIndex };
            if (lastIndex >= 0)
            {
                var prev = sections[lastIndex % MaxMarks];
                Vector3 dir = s.pos - prev.pos;
                Vector3 side = Vector3.Cross(dir, normal).normalized * (Width * 0.5f);
                s.left = s.pos + side; s.right = s.pos - side;
                if (prev.last < 0) { prev.left = prev.pos + side; prev.right = prev.pos - side; sections[lastIndex % MaxMarks] = prev; }

                int v = idx * 4;
                verts[v + 0] = prev.left; verts[v + 1] = prev.right; verts[v + 2] = s.left; verts[v + 3] = s.right;
                byte a0 = (byte)(prev.alpha * 255f), a1 = (byte)(s.alpha * 255f);
                cols[v + 0] = cols[v + 1] = new Color32(255, 255, 255, a0);
                cols[v + 2] = cols[v + 3] = new Color32(255, 255, 255, a1);
                dirty = true;
            }
            else
            {
                int v = idx * 4;
                verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = Vector3.zero; // clear a recycled slot
                dirty = true;
            }
            sections[idx] = s;
            return count - 1; // monotonic id; "% MaxMarks" maps it back to the slot
        }

        void LateUpdate()
        {
            if (!dirty) return;
            mesh.vertices = verts;
            mesh.colors32 = cols;
            dirty = false;
        }
    }
}
