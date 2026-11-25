using UnityEngine;

namespace DriftKuwait
{
    /// Fits the customs-shop looks onto a built car (the player's car on the street and on the garage turntable):
    /// door stickers, a windscreen banner, a boot wing, wide over-fenders, neon underglow. Smoke and exhaust-flame
    /// colours are read from this component by TyreFX and DriftFX. Positions come from Cars/<id>/anchors.json,
    /// measured off each body by tools/art/car_anchors.py.
    public class CarCustoms : MonoBehaviour
    {
        public Customs.Item smoke, exhaust;
        public Color neon;
        Light neonLight;

        [System.Serializable]
        class Anchors { public float[] side, glass, wing, arches; public float roof; }

        static readonly System.Collections.Generic.Dictionary<string, Anchors> cache = new System.Collections.Generic.Dictionary<string, Anchors>();

        static Anchors Load(string id)
        {
            if (cache.TryGetValue(id, out var a)) return a;
            var ta = Resources.Load<TextAsset>($"DriftKuwait/Cars/{id}/anchors");
            a = ta != null ? JsonUtility.FromJson<Anchors>(ta.text) : null;
            cache[id] = a;
            return a;
        }

        /// How far each wheel moves out under wide fenders (metres).
        public static float TrackAdd(string id) => Customs.Fitted(id, Customs.Cat.Fenders) >= 0 && Load(id) != null ? 0.055f : 0f;

        public static void Attach(string id, Transform root, Transform body, float radius, Vector3[] wheels)
        {
            var cc = root.gameObject.AddComponent<CarCustoms>();
            cc.smoke = Customs.FittedItem(id, Customs.Cat.Smoke);
            cc.exhaust = Customs.FittedItem(id, Customs.Cat.Exhaust);
            var a = Load(id);
            if (a == null) return;
            int layer = root.gameObject.layer;
            var holder = new GameObject("Customs").transform;
            holder.SetParent(body, false);
            holder.gameObject.layer = layer;

            // Door stickers, both sides
            var st = Customs.FittedItem(id, Customs.Cat.Stickers);
            if (st != null && a.side != null && a.side.Length >= 3)
            {
                var tex = Resources.Load<Texture2D>("DriftKuwait/Customs/" + st.tex);
                if (tex != null)
                {
                    var m = Decal("DK_Sticker_" + st.tex, tex);
                    float w = 0.62f, h = w * tex.height / (float)tex.width;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var q = Quad(holder, "Sticker", m, layer);
                        q.localPosition = new Vector3(s * (a.side[0] + 0.012f), a.side[1], a.side[2]);
                        q.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);   // face outwards (same as the door lettering)
                        q.localScale = new Vector3(w, h, 1f);
                    }
                }
            }

            // Windscreen banner along the top of the glass
            var sl = Customs.FittedItem(id, Customs.Cat.Slogans);
            if (sl != null && a.glass != null && a.glass.Length >= 5)
            {
                var tex = Resources.Load<Texture2D>("DriftKuwait/Customs/" + sl.tex);
                if (tex != null)
                {
                    var n = new Vector3(0f, a.glass[2], a.glass[3]).normalized;
                    var up = new Vector3(0f, n.z, -n.y);                // up the glass, toward the roof
                    var q = Quad(holder, "Banner", Decal("DK_Banner_" + sl.tex, tex), layer);
                    float w = a.glass[4] * 1.25f;
                    q.localPosition = new Vector3(0f, a.glass[0], a.glass[1]) + n * 0.012f;
                    q.localRotation = Quaternion.LookRotation(-n, up);
                    q.localScale = new Vector3(w, w * tex.height / (float)tex.width, 1f);
                }
            }

            // Boot wing
            var wg = Customs.FittedItem(id, Customs.Cat.Wing);
            if (wg != null && a.wing != null && a.wing.Length >= 3)
            {
                var mat = Mat.Paint("DK_Wing_" + wg.style, wg.color, wg.style == 2 ? 0.8f : 0.55f, 0.2f);
                float y = a.wing[0], z = a.wing[1], span = a.wing[2];
                if (wg.style == 0)   // duck tail: a lip across the boot
                    Box(holder, "DuckTail", mat, layer, new Vector3(0f, y + 0.03f, z - 0.06f), new Vector3(span * 0.92f, 0.05f, 0.22f), -14f);
                else
                {
                    float hgt = wg.style == 1 ? 0.24f : 0.34f;
                    for (int s = -1; s <= 1; s += 2)
                        Box(holder, "Upright", mat, layer, new Vector3(s * span * 0.32f, y + hgt * 0.5f, z), new Vector3(0.035f, hgt, 0.16f), 0f);
                    Box(holder, "Blade", mat, layer, new Vector3(0f, y + hgt + 0.02f, z - 0.03f), new Vector3(span * 1.02f, 0.035f, 0.3f), -8f);
                    for (int s = -1; s <= 1; s += 2)
                        Box(holder, "EndPlate", mat, layer, new Vector3(s * span * 0.52f, y + hgt + 0.02f, z - 0.03f), new Vector3(0.012f, 0.16f, 0.36f), 0f);
                }
            }

            // Wide over-fenders: a bolt-on arch flare round each wheel
            var fd = Customs.FittedItem(id, Customs.Cat.Fenders);
            if (fd != null && a.arches != null && a.arches.Length >= 4 && wheels != null)
            {
                var mat = Mat.Paint("DK_Flare_" + fd.style, fd.style == 1 ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.03f, 0.03f, 0.035f), fd.style == 1 ? 0.45f : 0.85f, 0.1f);
                for (int i = 0; i < 4; i++) Flare(holder, mat, layer, wheels[i], a.arches[i], radius);
            }

            // Neon underglow: glowing strips under the sills and a soft light on the road
            var ne = Customs.FittedItem(id, Customs.Cat.Neon);
            if (ne != null && a.side != null && wheels != null)
            {
                cc.neon = ne.color;
                var glow = Mat.Glow("DK_Neon_" + ColorUtility.ToHtmlStringRGB(ne.color), ne.color, 4f);
                float len = Mathf.Abs(wheels[0].z - wheels[2].z) * 0.8f, zc = (wheels[0].z + wheels[2].z) * 0.5f;
                for (int s = -1; s <= 1; s += 2)
                    Box(holder, "NeonStrip", glow, layer, new Vector3(s * (a.side[0] - 0.12f), 0.16f, zc), new Vector3(0.03f, 0.02f, len), 0f);
                var lg = new GameObject("NeonLight");
                lg.transform.SetParent(holder, false);
                lg.transform.localPosition = new Vector3(0f, 0.12f, zc);
                cc.neonLight = lg.AddComponent<Light>();
                cc.neonLight.type = LightType.Point; cc.neonLight.color = ne.color; cc.neonLight.range = 3.2f; cc.neonLight.intensity = 3.5f;
                cc.neonLight.shadows = LightShadows.None;
            }
        }

        void Update()
        {
            if (neonLight != null) neonLight.intensity = 3.2f + Mathf.Sin(Time.time * 3f) * 0.3f;   // a little life in the glow
        }

        // ------------------------------------------------------------------ helpers
        static Material Decal(string key, Texture2D tex)
        {
            var m = Mat.Paint(key, Color.white, 0.8f);
            if (m.GetTexture("_BaseMap") == tex) return m;
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static Transform Quad(Transform parent, string name, Material m, int layer)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = name; q.layer = layer;
            q.transform.SetParent(parent, false);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q.transform;
        }

        static void Box(Transform parent, string name, Material m, int layer, Vector3 pos, Vector3 size, float pitch)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(b.GetComponent<Collider>());
            b.name = name; b.layer = layer;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// A flared arch round a wheel: an outer lip and its top face, from the body side outwards.
        static void Flare(Transform parent, Material m, int layer, Vector3 c, float bodyX, float R)
        {
            var mk = new MeshKit();
            float side = Mathf.Sign(c.x);
            float x0 = side * (bodyX - 0.01f), x1 = side * (bodyX + 0.085f);
            float r0 = R * 1.18f, r1 = R * 1.36f;
            const int N = 16;
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.Lerp(-12f, 192f, i / (float)N) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-12f, 192f, (i + 1) / (float)N) * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)), d1 = new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1));
                Vector3 cc0 = new Vector3(0f, c.y, c.z), cc1 = cc0;
                // outer lip face (looks sideways)
                mk.Quad(new Vector3(x1, 0f, 0f) + cc0 + d0 * r0, new Vector3(x1, 0f, 0f) + cc1 + d1 * r0,
                        new Vector3(x1, 0f, 0f) + cc1 + d1 * r1, new Vector3(x1, 0f, 0f) + cc0 + d0 * r1, new Vector3(side, 0f, 0f));
                // top face (from the body out to the lip)
                mk.Quad(new Vector3(x0, 0f, 0f) + cc0 + d0 * r1, new Vector3(x0, 0f, 0f) + cc1 + d1 * r1,
                        new Vector3(x1, 0f, 0f) + cc1 + d1 * r1, new Vector3(x1, 0f, 0f) + cc0 + d0 * r1, (d0 + d1).normalized);
                // underside (seen from below the arch)
                mk.Quad(new Vector3(x0, 0f, 0f) + cc0 + d0 * r0, new Vector3(x1, 0f, 0f) + cc0 + d0 * r0,
                        new Vector3(x1, 0f, 0f) + cc1 + d1 * r0, new Vector3(x0, 0f, 0f) + cc1 + d1 * r0, -(d0 + d1).normalized);
            }
            var go = mk.ToObject("Flare", parent, m);
            go.layer = layer;
        }
    }
}
