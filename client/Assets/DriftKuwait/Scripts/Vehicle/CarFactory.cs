using System;
using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Builds a drivable (or display) car from CarCatalog + Resources/DriftKuwait/Cars/<id>/:
    ///   body.obj  the Tripo body with the fused wheels cut out (tools/art/car_prep.py)
    ///   paint.mat the body material (made by BuildScript from albedo/normal/metallic, so its shader variants ship)
    ///   car.json  size, wheel centres and radius, tail-light positions
    /// The wheels are built here (smooth tyre with sidewall, deep-dish rim with N spokes or cross-spoke mesh, brake disc
    /// and calliper) so they spin and steer. Falls back to the blockout ShaheenBuilder if a car has no 3D body yet.
    public static class CarFactory
    {
        [Serializable] class Vec { public float x, y, z; }
        [Serializable]
        class CarJson
        {
            public float length, width, height, wheel_radius, wheel_width, wheelbase;
        }

        public class CarData
        {
            public float length, width, height, radius, tyreWidth;
            public Vector3 fl, fr, rl, rr;
            public List<Vector4> tail = new List<Vector4>();   // x, y, z, width (height ignored)
            public List<Vector4> head = new List<Vector4>();
            public List<float[]> door = new List<float[]>();     // x, y, z, width, height, body r, g, b (lettering to cover)
        }

        public static CarData Load(string id)
        {
            var ta = Resources.Load<TextAsset>($"DriftKuwait/Cars/{id}/car");
            if (ta == null) return null;
            var j = JsonUtility.FromJson<CarJson>(ta.text);
            var d = new CarData { length = j.length, width = j.width, height = j.height, radius = j.wheel_radius, tyreWidth = j.wheel_width };
            // JsonUtility can't read dictionaries of arrays: pull the few arrays out by hand.
            d.fl = ReadVec(ta.text, "\"FL\""); d.fr = ReadVec(ta.text, "\"FR\"");
            d.rl = ReadVec(ta.text, "\"RL\""); d.rr = ReadVec(ta.text, "\"RR\"");
            d.tail = ReadList(ta.text, "\"tail\"");
            d.head = ReadList(ta.text, "\"head\"");
            d.door = ReadRows(ta.text, "\"door\"");
            return d;
        }

        static Vector3 ReadVec(string s, string key)
        {
            int i = s.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return Vector3.zero;
            int a = s.IndexOf('[', i), b = s.IndexOf(']', a);
            var p = s.Substring(a + 1, b - a - 1).Split(',');
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        static List<Vector4> ReadList(string s, string key)
        {
            var list = new List<Vector4>();
            int i = s.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return list;
            int a = s.IndexOf('[', i);
            int depth = 0, start = -1;
            for (int k = a; k < s.Length; k++)
            {
                char c = s[k];
                if (c == '[') { depth++; if (depth == 2) start = k + 1; }
                else if (c == ']')
                {
                    if (depth == 2 && start > 0)
                    {
                        var p = s.Substring(start, k - start).Split(',');
                        if (p.Length >= 4) list.Add(new Vector4(F(p[0]), F(p[1]), F(p[2]), F(p[3])));
                    }
                    depth--;
                    if (depth == 0) break;
                }
            }
            return list;
        }

        static List<float[]> ReadRows(string s, string key)
        {
            var rows = new List<float[]>();
            int i = s.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return rows;
            int a = s.IndexOf('[', i), depth = 0, start = -1;
            for (int k = a; k < s.Length; k++)
            {
                char c = s[k];
                if (c == '[') { depth++; if (depth == 2) start = k + 1; }
                else if (c == ']')
                {
                    if (depth == 2 && start > 0)
                    {
                        var p = s.Substring(start, k - start).Split(',');
                        var r = new float[p.Length];
                        for (int j = 0; j < p.Length; j++) r[j] = F(p[j]);
                        rows.Add(r);
                    }
                    depth--;
                    if (depth == 0) break;
                }
            }
            return rows;
        }

        static float F(string t) => float.Parse(t.Trim(), System.Globalization.CultureInfo.InvariantCulture);

        /// A drivable car with physics, input, FX and audio.
        public static GameObject Build(string id, Vector3 pos, Quaternion rot)
        {
            var spec = CarCatalog.Get(id);
            var data = Load(spec.id);
            if (data == null) return ShaheenBuilder.Build(pos, rot);

            var root = new GameObject(spec.nameAr + "_" + spec.id);
            root.layer = 2;   // Ignore Raycast: wheel rays and near-miss rays skip the car
            root.transform.SetPositionAndRotation(pos, rot);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            float clear = data.radius * 0.55f;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, clear + (data.height * 0.55f - clear) * 0.5f + 0.05f, 0f);
            col.size = new Vector3(data.width * 0.96f, data.height * 0.5f - clear * 0.4f, data.length * 0.98f);
            var cab = root.AddComponent<BoxCollider>();
            cab.center = new Vector3(0f, data.height * 0.8f, -data.length * 0.06f);
            cab.size = new Vector3(data.width * 0.78f, data.height * 0.36f, data.length * 0.5f);

            var visual = BuildVisual(spec, data, root.transform, true);

            root.AddComponent<CarInput>();
            var car = root.AddComponent<DriftCarController>();
            car.wheelRadius = data.radius;
            car.restLength = 0.30f;
            car.baseTorque = spec.baseTorque;
            car.maxRpm = spec.maxRpm;
            car.turboSpoolTime = spec.turboSpool;
            car.boostTorque = spec.boost;
            car.frontGrip = spec.frontGrip;
            car.rearGrip = spec.rearGrip;
            car.handbrakeGripFactor = spec.handbrakeGrip;
            car.springRate = 42000f * spec.mass / 1150f;
            car.damperRate = 3800f * spec.mass / 1150f;

            // Raycast suspension mounts sit restLength above the wheel centre at full droop.
            float mountY = data.radius + car.restLength - 0.065f;   // static sag ~6.5 cm (spring scaled by mass)
            car.wheels = new[]
            {
                Wheel(visual.wheels[0], new Vector3(data.fl.x, mountY, data.fl.z), true, true),
                Wheel(visual.wheels[1], new Vector3(data.fr.x, mountY, data.fr.z), true, false),
                Wheel(visual.wheels[2], new Vector3(data.rl.x, mountY, data.rl.z), false, true),
                Wheel(visual.wheels[3], new Vector3(data.rr.x, mountY, data.rr.z), false, false),
            };
            rb.centerOfMass = new Vector3(0f, data.radius + 0.05f, (data.fl.z + data.rl.z) * 0.5f + 0.05f);

            root.AddComponent<TyreFX>();
            root.AddComponent<EngineAudio>();
            root.AddComponent<TyreAudio>();
            var lights = root.AddComponent<CarLights>();
            lights.tail = visual.tail;
            lights.head = visual.head;
            lights.beams = visual.beams;
            return root;
        }

        /// A car for the garage turntable or the menu: looks only, no physics.
        public static GameObject BuildDisplay(string id, Transform parent)
        {
            var spec = CarCatalog.Get(id);
            var data = Load(spec.id);
            if (data == null) return null;
            var root = new GameObject("Display_" + spec.id);
            root.transform.SetParent(parent, false);
            var v = BuildVisual(spec, data, root.transform, false);
            for (int i = 0; i < 4; i++)
                v.wheels[i].localPosition = i == 0 ? data.fl : i == 1 ? data.fr : i == 2 ? data.rl : data.rr;
            var dw = root.AddComponent<DisplayWheels>();
            dw.wheels = v.wheels; dw.radius = data.radius;
            return root;
        }

        static DriftCarController.Wheel Wheel(Transform t, Vector3 mount, bool front, bool left)
            => new DriftCarController.Wheel { visual = t, localMount = mount, front = front, left = left };

        public class Visual
        {
            public Transform[] wheels = new Transform[4];
            public List<Renderer> tail = new List<Renderer>(), head = new List<Renderer>();
            public List<Light> beams = new List<Light>();
        }

        static Visual BuildVisual(CarSpec spec, CarData d, Transform root, bool dynamic)
        {
            var v = new Visual();
            var bodyRoot = new GameObject("Body");
            bodyRoot.layer = root.gameObject.layer;
            bodyRoot.transform.SetParent(root, false);

            var prefab = Resources.Load<GameObject>($"DriftKuwait/Cars/{spec.id}/body");
            var paint = Resources.Load<Material>($"DriftKuwait/Cars/{spec.id}/paint");
            if (prefab != null)
            {
                var body = UnityEngine.Object.Instantiate(prefab, bodyRoot.transform, false);
                body.name = "Shell";
                foreach (var r in body.GetComponentsInChildren<MeshRenderer>())
                {
                    if (paint != null) r.sharedMaterial = paint;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.gameObject.layer = root.gameObject.layer;
                }
                foreach (var c in body.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
            }

            // Correct Arabic door lettering over whatever the model's texture painted there (patrol: شرطة).
            var decalTex = Resources.Load<Texture2D>($"DriftKuwait/Cars/{spec.id}/decal");
            if (decalTex != null && d.door.Count > 0)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var dm = new Material(lit) { name = "DK_Decal_" + spec.id };
                dm.SetTexture("_BaseMap", decalTex);
                dm.SetFloat("_Surface", 1f); dm.SetFloat("_Blend", 0f);
                dm.SetFloat("_Smoothness", 0.75f);
                dm.SetOverrideTag("RenderType", "Transparent");
                dm.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dm.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dm.SetInt("_ZWrite", 0);
                dm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dm.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                foreach (var r in d.door)
                {
                    if (r.Length < 5) continue;
                    float sgn = Mathf.Sign(r[0]);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.Destroy(q.GetComponent<Collider>());
                    q.name = "DoorDecal";
                    q.layer = root.gameObject.layer;
                    q.transform.SetParent(bodyRoot.transform, false);
                    q.transform.localPosition = new Vector3(r[0] + sgn * 0.012f, r[1], r[2]);
                    q.transform.localRotation = Quaternion.Euler(0f, sgn > 0 ? -90f : 90f, 0f);   // face outwards
                    q.transform.localScale = new Vector3(Mathf.Max(r[3] * 1.3f, 0.7f), Mathf.Max(r[4] * 1.7f, 0.26f), 1f);
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = dm;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // Dark wheel-well liners behind each cut-out, so the arches never show the inside of the shell.
            var liner = Mat.Paint("DK_WellLiner", new Color(0.018f, 0.018f, 0.02f), 0.15f);
            foreach (var c in new[] { d.fl, d.fr, d.rl, d.rr })
            {
                var mk = new MeshKit();
                float side = Mathf.Sign(c.x);
                for (int i = 0; i < 14; i++)
                {
                    float a0 = Mathf.Lerp(-8f, 188f, i / 14f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-8f, 188f, (i + 1) / 14f) * Mathf.Deg2Rad;
                    float R = d.radius * 1.12f;
                    Vector3 p0 = new Vector3(0f, Mathf.Sin(a0) * R, Mathf.Cos(a0) * R), p1 = new Vector3(0f, Mathf.Sin(a1) * R, Mathf.Cos(a1) * R);
                    Vector3 inner = new Vector3(-side * d.tyreWidth * 1.4f, 0f, 0f), outer = new Vector3(side * d.tyreWidth * 0.2f, 0f, 0f);
                    mk.Quad(c + p0 + inner, c + p1 + inner, c + p1 + outer, c + p0 + outer, -(p0 + p1).normalized);
                }
                mk.Quad(c + new Vector3(-side * d.tyreWidth * 1.4f, -d.radius * 0.18f, -d.radius * 1.1f), c + new Vector3(-side * d.tyreWidth * 1.4f, -d.radius * 0.18f, d.radius * 1.1f),
                        c + new Vector3(-side * d.tyreWidth * 1.4f, d.radius * 1.1f, d.radius * 1.1f), c + new Vector3(-side * d.tyreWidth * 1.4f, d.radius * 1.1f, -d.radius * 1.1f),
                        new Vector3(side, 0f, 0f));
                var lg = mk.ToObject("WellLiner", bodyRoot.transform, liner);
                lg.layer = root.gameObject.layer;
            }

            // Wheels
            var wheelMesh = WheelKit.Build(spec, d.radius, d.tyreWidth);
            var centres = new[] { d.fl, d.fr, d.rl, d.rr };
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject(names[i]);
                pivot.layer = root.gameObject.layer;
                pivot.transform.SetParent(root, false);
                pivot.transform.localPosition = centres[i];
                bool left = centres[i].x < 0f;
                var mirror = new GameObject("Mirror");
                mirror.transform.SetParent(pivot.transform, false);
                mirror.transform.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);   // the rim face points outwards on both sides
                foreach (var part in wheelMesh)
                {
                    var go = new GameObject(part.name);
                    go.layer = root.gameObject.layer;
                    go.transform.SetParent(mirror.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = part.mat;
                }
                v.wheels[i] = pivot.transform;
            }

            // Light glows. Tail lights from the texture (car.json), headlights from the car's proportions.
            // Soft additive glows over the textured lamps: invisible by day, they bloom at night and on the brakes.
            var tailMat = GlowMat("DK_TailGlowAdd");
            var headMat = GlowMat("DK_HeadGlowAdd");
            var tails = d.tail.Count > 0 ? d.tail : new List<Vector4>
            {
                new Vector4(-d.width * 0.34f, d.height * 0.52f, -d.length * 0.49f, d.width * 0.2f),
                new Vector4(d.width * 0.34f, d.height * 0.52f, -d.length * 0.49f, d.width * 0.2f),
            };
            foreach (var t in tails)
                v.tail.Add(Glow(bodyRoot.transform, "TailGlow", new Vector3(t.x, t.y, t.z - 0.012f), new Vector2(Mathf.Max(t.w, 0.16f) * 1.35f, 0.2f), true, tailMat));
            var heads = d.head.Count > 0 ? d.head : new List<Vector4>
            {
                new Vector4(-d.width * 0.33f, d.height * 0.47f, d.length * 0.495f, 0.3f),
                new Vector4(d.width * 0.33f, d.height * 0.47f, d.length * 0.495f, 0.3f),
            };
            foreach (var h in heads)
            {
                v.head.Add(Glow(bodyRoot.transform, "HeadGlow", new Vector3(h.x, h.y, h.z + 0.012f), new Vector2(Mathf.Max(h.w, 0.22f) * 1.4f, 0.24f), false, headMat));
                if (!dynamic) continue;
                var lg = new GameObject("Beam");
                lg.transform.SetParent(bodyRoot.transform, false);
                lg.transform.localPosition = new Vector3(h.x, h.y, h.z);
                lg.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 45f; l.spotAngle = 62f; l.innerSpotAngle = 30f; l.intensity = 16f;
                l.color = new Color(1f, 0.9f, 0.72f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                v.beams.Add(l);
            }
            return v;
        }

        static Texture2D glowTex;
        static readonly Dictionary<string, Material> glowMats = new Dictionary<string, Material>();

        /// Additive Particles/Unlit material with a soft rounded-rectangle falloff (generated once).
        static Material GlowMat(string key)
        {
            if (glowMats.TryGetValue(key, out var m) && m != null) return m;
            if (glowTex == null)
            {
                const int W = 128, H = 64;
                glowTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DK_GlowSoft" };
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Abs((x + 0.5f) / W * 2f - 1f) - 0.55f) / 0.45f;
                        float dy = Mathf.Max(0f, Mathf.Abs((y + 0.5f) / H * 2f - 1f) - 0.25f) / 0.75f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        byte b = (byte)(a * 255f);
                        px[y * W + x] = new Color32(255, 255, 255, b);
                    }
                glowTex.SetPixels32(px);
                glowTex.Apply();
            }
            m = TyreFX.ParticleMaterial(key, glowTex, new Color(0f, 0f, 0f, 0f));
            m.SetFloat("_Blend", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.renderQueue = 3100;
            glowMats[key] = m;
            return m;
        }

        static Renderer Glow(Transform parent, string name, Vector3 pos, Vector2 size, bool rear, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rear ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);   // quad faces -Z by default
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }
    }

    /// Brake lights brighten on the brake or handbrake; headlights and beams come on at night.
    public class CarLights : MonoBehaviour
    {
        public List<Renderer> tail, head;
        public List<Light> beams;
        public static bool NightMode;
        DriftCarController car;
        CarInput input;
        MaterialPropertyBlock mpb;
        bool lastNight = true;

        void Start() { car = GetComponent<DriftCarController>(); input = GetComponent<CarInput>(); mpb = new MaterialPropertyBlock(); lastNight = !NightMode; }

        void Update()
        {
            if (input == null || tail == null) return;
            bool braking = input.Brake > 0.1f || input.Handbrake;
            float k = braking ? (NightMode ? 1f : 0.75f) : (NightMode ? 0.45f : 0f);
            foreach (var r in tail)
            {
                if (r == null) continue;
                r.enabled = k > 0f;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", new Color(4f, 0.25f, 0.12f, k));
                r.SetPropertyBlock(mpb);
            }
            if (NightMode != lastNight)
            {
                lastNight = NightMode;
                foreach (var b in beams) if (b != null) b.enabled = NightMode;
                foreach (var h in head)
                {
                    if (h == null) continue;
                    h.enabled = NightMode;
                    h.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", new Color(3.2f, 3f, 2.6f, 0.9f));
                    h.SetPropertyBlock(mpb);
                }
            }
        }
    }

    /// Procedural wheel: smooth tyre with sidewall bulge and tread grooves, deep-dish rim, spokes or cross-spoke mesh,
    /// centre cap, brake disc and calliper. One mesh set per car, shared by all four wheels. Axis = X, outer face +X.
    public static class WheelKit
    {
        public struct Part { public string name; public Mesh mesh; public Material mat; }
        static readonly Dictionary<string, Part[]> cache = new Dictionary<string, Part[]>();

        public static Part[] Build(CarSpec spec, float R, float W)
        {
            string key = spec.id + R.ToString("F3");
            if (cache.TryGetValue(key, out var parts)) return parts;
            float rimR = R * 0.64f;
            var tyreMat = Mat.Paint("DK_Tyre", new Color(0.035f, 0.035f, 0.037f), 0.28f);
            var rimMat = Mat.Paint("DK_Rim_" + spec.id, spec.rimColor, 0.86f, 0.9f);
            var lipMat = Mat.Paint("DK_RimLip_" + spec.id, spec.rimLip, 0.94f, 1f);
            var discMat = Mat.Paint("DK_Disc", new Color(0.42f, 0.42f, 0.44f), 0.7f, 1f);
            var calMat = Mat.Paint("DK_Caliper", new Color(0.55f, 0.06f, 0.05f), 0.6f, 0.2f);

            // Tyre: profile (x across the width, r radius), revolved around X with shared vertices = smooth.
            var tyreProfile = new List<Vector2>
            {
                new Vector2(W * 0.48f, rimR * 1.02f), new Vector2(W * 0.52f, rimR + (R - rimR) * 0.35f),
                new Vector2(W * 0.54f, rimR + (R - rimR) * 0.7f), new Vector2(W * 0.5f, R * 0.965f),
                new Vector2(W * 0.42f, R), new Vector2(-W * 0.42f, R), new Vector2(-W * 0.5f, R * 0.965f),
                new Vector2(-W * 0.54f, rimR + (R - rimR) * 0.7f), new Vector2(-W * 0.52f, rimR + (R - rimR) * 0.35f),
                new Vector2(-W * 0.48f, rimR * 1.02f),
            };
            var tyre = Revolve(tyreProfile, 48, true);

            // Rim barrel and polished lip (deep dish: the spoke face sits inboard of the lip).
            var lip = Revolve(new List<Vector2>
            {
                new Vector2(W * 0.5f, rimR * 0.9f), new Vector2(W * 0.52f, rimR * 1.04f), new Vector2(W * 0.46f, rimR * 1.06f),
                new Vector2(W * 0.38f, rimR * 0.98f), new Vector2(W * 0.18f, rimR * 0.95f),
            }, 40, false);
            var barrel = Revolve(new List<Vector2> { new Vector2(W * 0.2f, rimR * 0.95f), new Vector2(-W * 0.48f, rimR * 0.95f) }, 32, false);

            var face = new MeshKit();
            float faceX = W * 0.16f;
            face.Cylinder(new Vector3(faceX, 0f, 0f), Vector3.right, rimR * 0.2f, W * 0.1f, 18);               // hub
            face.Cylinder(new Vector3(faceX + W * 0.07f, 0f, 0f), Vector3.right, rimR * 0.12f, W * 0.05f, 16);  // centre cap
            if (spec.rimSpokes > 0)
            {
                for (int i = 0; i < spec.rimSpokes; i++)
                {
                    float ang = i * 360f / spec.rimSpokes;
                    var q = Quaternion.AngleAxis(ang, Vector3.right);
                    float len = rimR * 0.78f;
                    face.Box(new Vector3(faceX, 0f, 0f) + q * new Vector3(0f, rimR * 0.2f + len * 0.5f, 0f),
                             new Vector3(W * 0.07f, len, rimR * (spec.rimSpokes > 7 ? 0.13f : 0.2f)), q);
                }
            }
            else
            {
                // Cross-spoke mesh wheel: two sets of thin bars crossing at an angle.
                for (int i = 0; i < 18; i++)
                {
                    foreach (float tilt in new[] { 22f, -22f })
                    {
                        var q = Quaternion.AngleAxis(i * 20f, Vector3.right) * Quaternion.AngleAxis(tilt, Vector3.right);
                        face.Box(new Vector3(faceX, 0f, 0f) + q * new Vector3(0f, rimR * 0.58f, 0f),
                                 new Vector3(W * 0.05f, rimR * 0.78f, rimR * 0.035f), q);
                    }
                }
            }
            face.Cylinder(new Vector3(faceX - W * 0.02f, 0f, 0f), Vector3.right, rimR * 0.97f, W * 0.02f, 32, false);

            var disc = new MeshKit();
            disc.Cylinder(new Vector3(-W * 0.15f, 0f, 0f), Vector3.right, rimR * 0.82f, W * 0.08f, 28);
            var cal = new MeshKit();
            cal.Box(new Vector3(-W * 0.1f, rimR * 0.62f, -rimR * 0.25f), new Vector3(W * 0.2f, rimR * 0.3f, rimR * 0.42f), Quaternion.Euler(-25f, 0f, 0f));

            parts = new[]
            {
                new Part { name = "Tyre", mesh = tyre, mat = tyreMat },
                new Part { name = "Lip", mesh = lip, mat = lipMat },
                new Part { name = "Barrel", mesh = barrel, mat = rimMat },
                new Part { name = "Face", mesh = face.ToMesh("RimFace"), mat = rimMat },
                new Part { name = "Disc", mesh = disc.ToMesh("Disc"), mat = discMat },
                new Part { name = "Caliper", mesh = cal.ToMesh("Caliper"), mat = calMat },
            };
            cache[key] = parts;
            return parts;
        }

        /// Revolve (x, r) points around the X axis. Shared vertices per ring give smooth normals.
        static Mesh Revolve(List<Vector2> prof, int seg, bool tread)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int s = 0; s <= seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int i = 0; i < prof.Count; i++)
                {
                    float r = prof[i].y;
                    if (tread && Mathf.Abs(prof[i].x) < prof[0].x * 0.9f && r > prof[0].y * 1.3f)
                        r -= (s % 2 == 0) ? 0.004f : 0f;   // shallow tread blocks
                    verts.Add(new Vector3(prof[i].x, ca * r, sa * r));
                    uvs.Add(new Vector2(i / (float)(prof.Count - 1), s / (float)seg));
                }
            }
            int n = prof.Count;
            for (int s = 0; s < seg; s++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a0 = s * n + i, a1 = a0 + 1, b0 = a0 + n, b1 = b0 + 1;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            var m = new Mesh { name = "Revolve" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            // Make normals point away from the axis (the winding above may face either way for a given profile).
            var nrm = m.normals;
            bool flip = false;
            int probe = n / 2;
            if (Vector3.Dot(nrm[probe], new Vector3(0f, verts[probe].y, verts[probe].z)) < 0f) flip = true;
            if (flip)
            {
                for (int t = 0; t < tris.Count; t += 3) { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
            }
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    /// Wheels of a non-physics car (traffic, parked, showroom), so AI cars can roll them.
    public class DisplayWheels : MonoBehaviour
    {
        public Transform[] wheels;
        public float radius = 0.3f;
        float angle;
        public void Roll(float metres)
        {
            angle += metres / Mathf.Max(0.1f, radius) * Mathf.Rad2Deg;
            foreach (var w in wheels) if (w != null) w.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
