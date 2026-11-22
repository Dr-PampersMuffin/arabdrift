using UnityEngine;

namespace DriftKuwait
{
    /// Builds الشاهين from code: an early-80s three-box turbo coupe silhouette with our own
    /// details (bar tail lights, four-spoke wheels, black bumpers, two-tone paint).
    /// This is the blockout car. The final photoreal mesh replaces the "Body" child only;
    /// physics, wheels and FX keep working.
    public static class ShaheenBuilder
    {
        public static Color PaintUpper = new Color(0.78f, 0.78f, 0.80f);   // silver metallic
        public static Color PaintLower = new Color(0.16f, 0.16f, 0.17f);   // gunmetal two-tone

        public static GameObject Build(Vector3 pos, Quaternion rot)
        {
            var root = new GameObject("Shaheen");
            root.layer = 2; // Ignore Raycast: wheel rays and near-miss rays skip the car
            root.transform.SetPositionAndRotation(pos, rot);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1150f;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.68f, 0f);
            col.size = new Vector3(1.68f, 0.62f, 4.62f);
            var cab = root.AddComponent<BoxCollider>();
            cab.center = new Vector3(0f, 1.12f, -0.4f);
            cab.size = new Vector3(1.36f, 0.45f, 2.4f);

            var bodyRoot = new GameObject("Body");
            bodyRoot.layer = 2;
            bodyRoot.transform.SetParent(root.transform, false);
            BuildBody(bodyRoot.transform);

            root.AddComponent<CarInput>();
            var car = root.AddComponent<DriftCarController>();
            const float axleF = 1.38f, axleR = -1.24f, track = 0.71f, mountY = 0.55f;
            car.wheels = new[]
            {
                MakeWheel(root.transform, "Wheel_FL", new Vector3(-track, mountY, axleF), true, true),
                MakeWheel(root.transform, "Wheel_FR", new Vector3(track, mountY, axleF), true, false),
                MakeWheel(root.transform, "Wheel_RL", new Vector3(-track, mountY, axleR), false, true),
                MakeWheel(root.transform, "Wheel_RR", new Vector3(track, mountY, axleR), false, false),
            };
            rb.centerOfMass = new Vector3(0f, 0.38f, 0.05f);

            root.AddComponent<TyreFX>();
            root.AddComponent<EngineAudio>();
            root.AddComponent<TyreAudio>();
            return root;
        }

        static void BuildBody(Transform t)
        {
            var paint = Mat.Paint("Shaheen_Paint", PaintUpper, 0.82f, 0.65f);
            var lower = Mat.Paint("Shaheen_Lower", PaintLower, 0.6f, 0.5f);
            var glass = Mat.Paint("Shaheen_Glass", new Color(0.05f, 0.07f, 0.08f), 0.96f, 0.1f);
            var black = Mat.Paint("Trim_Black", new Color(0.03f, 0.03f, 0.03f), 0.35f);
            var chrome = Mat.Paint("Chrome", new Color(0.85f, 0.85f, 0.85f), 0.95f, 1f);
            var head = Mat.Glow("Headlight", new Color(1f, 0.93f, 0.78f), 2.2f);
            var tail = Mat.Glow("Taillight", new Color(0.85f, 0.05f, 0.03f), 1.6f);
            var plate = Mat.Paint("Plate", new Color(0.92f, 0.92f, 0.88f), 0.3f);

            // Main body with wheel-arch notches (z forward, y up), counter-clockwise.
            var body = new MeshKit();
            body.ExtrudeProfile(new[]
            {
                new Vector2(-2.30f, 0.36f), new Vector2(-1.58f, 0.36f), new Vector2(-1.52f, 0.58f),
                new Vector2(-1.38f, 0.69f), new Vector2(-1.10f, 0.69f), new Vector2(-0.96f, 0.58f),
                new Vector2(-0.90f, 0.36f), new Vector2(1.04f, 0.36f),  new Vector2(1.10f, 0.58f),
                new Vector2(1.24f, 0.69f),  new Vector2(1.52f, 0.69f),  new Vector2(1.66f, 0.58f),
                new Vector2(1.72f, 0.36f),  new Vector2(2.24f, 0.36f),  new Vector2(2.33f, 0.60f),
                new Vector2(2.20f, 0.80f),  new Vector2(0.95f, 0.88f),  new Vector2(-1.75f, 0.95f),
                new Vector2(-2.30f, 0.93f), new Vector2(-2.35f, 0.60f),
            }, 1.70f, Vector3.zero);
            body.ToObject("Shell", t, paint);

            // Two-tone lower band (sits 5 mm proud of the shell).
            var band = new MeshKit();
            band.Box(new Vector3(0.855f, 0.47f, 0.07f), new Vector3(0.012f, 0.16f, 1.92f));
            band.Box(new Vector3(-0.855f, 0.47f, 0.07f), new Vector3(0.012f, 0.16f, 1.92f));
            band.ToObject("TwoTone", t, lower);

            // Glasshouse
            var cabin = new MeshKit();
            cabin.ExtrudeProfile(new[]
            {
                new Vector2(-1.72f, 0.95f), new Vector2(0.95f, 0.88f),
                new Vector2(0.30f, 1.34f),  new Vector2(-1.05f, 1.34f),
            }, 1.40f, Vector3.zero);
            cabin.ToObject("Glass", t, glass);

            // Roof skin, B-pillars, sail panels in body colour
            var roof = new MeshKit();
            roof.ExtrudeProfile(new[]
            {
                new Vector2(-1.08f, 1.33f), new Vector2(0.33f, 1.33f),
                new Vector2(0.29f, 1.375f), new Vector2(-1.04f, 1.375f),
            }, 1.38f, Vector3.zero);
            roof.Box(new Vector3(0.705f, 1.12f, -0.30f), new Vector3(0.012f, 0.44f, 0.09f));
            roof.Box(new Vector3(-0.705f, 1.12f, -0.30f), new Vector3(0.012f, 0.44f, 0.09f));
            roof.ToObject("Roof", t, paint);

            // Black 80s trim: bumpers, grille, mirrors, side rubbing strip
            var trim = new MeshKit();
            trim.Box(new Vector3(0f, 0.47f, 2.35f), new Vector3(1.74f, 0.17f, 0.12f));
            trim.Box(new Vector3(0f, 0.47f, -2.37f), new Vector3(1.74f, 0.17f, 0.12f));
            trim.Box(new Vector3(0f, 0.68f, 2.285f), new Vector3(1.06f, 0.15f, 0.04f));
            trim.Box(new Vector3(0.90f, 1.0f, 0.78f), new Vector3(0.10f, 0.08f, 0.14f));
            trim.Box(new Vector3(-0.90f, 1.0f, 0.78f), new Vector3(0.10f, 0.08f, 0.14f));
            trim.Box(new Vector3(0.858f, 0.62f, 0.07f), new Vector3(0.014f, 0.035f, 1.9f));
            trim.Box(new Vector3(-0.858f, 0.62f, 0.07f), new Vector3(0.014f, 0.035f, 1.9f));
            trim.ToObject("Trim", t, black);

            // Grille slats + exhaust tip
            var chr = new MeshKit();
            for (int i = 0; i < 4; i++)
                chr.Box(new Vector3(0f, 0.63f + i * 0.03f, 2.31f), new Vector3(1.0f, 0.008f, 0.01f));
            chr.Cylinder(new Vector3(-0.55f, 0.36f, -2.38f), Vector3.forward, 0.035f, 0.16f, 10);
            chr.ToObject("Chrome", t, chrome);

            // Rectangular twin headlights (our design, not the original's)
            var hl = new MeshKit();
            hl.Box(new Vector3(0.62f, 0.70f, 2.30f), new Vector3(0.34f, 0.12f, 0.03f));
            hl.Box(new Vector3(-0.62f, 0.70f, 2.30f), new Vector3(0.34f, 0.12f, 0.03f));
            hl.ToObject("Headlights", t, head);

            // Full-width bar tail lights (our design)
            var tl = new MeshKit();
            tl.Box(new Vector3(0.52f, 0.80f, -2.335f), new Vector3(0.62f, 0.11f, 0.03f));
            tl.Box(new Vector3(-0.52f, 0.80f, -2.335f), new Vector3(0.62f, 0.11f, 0.03f));
            tl.ToObject("Taillights", t, tail);

            var pl = new MeshKit();
            pl.Box(new Vector3(0f, 0.60f, -2.335f), new Vector3(0.40f, 0.12f, 0.02f));
            pl.Box(new Vector3(0f, 0.47f, 2.42f), new Vector3(0.40f, 0.11f, 0.02f));
            pl.ToObject("Plates", t, plate);

            // Real headlight beams for night runs (off by default; HUD toggles later)
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                var lg = new GameObject("Beam");
                lg.transform.SetParent(t, false);
                lg.transform.localPosition = new Vector3(x, 0.70f, 2.35f);
                lg.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 40f; l.spotAngle = 55f; l.intensity = 12f;
                l.color = new Color(1f, 0.92f, 0.75f);
                l.enabled = false;
            }
        }

        static DriftCarController.Wheel MakeWheel(Transform car, string name, Vector3 mount, bool front, bool left)
        {
            var pivot = new GameObject(name);
            pivot.layer = 2;
            pivot.transform.SetParent(car, false);
            pivot.transform.localPosition = mount - Vector3.up * 0.23f;

            float side = left ? -1f : 1f;
            var tyre = new MeshKit();
            tyre.Cylinder(Vector3.zero, Vector3.right, 0.31f, 0.205f, 28);
            tyre.ToObject("Tyre", pivot.transform, Mat.Paint("Tyre", new Color(0.05f, 0.05f, 0.05f), 0.25f));

            var rim = new MeshKit();
            rim.Cylinder(new Vector3(side * 0.06f, 0f, 0f), Vector3.right, 0.20f, 0.10f, 24);
            // Four-spoke period wheel: a cross of two bars, turned 45 degrees.
            rim.Box(new Vector3(side * 0.115f, 0f, 0f), new Vector3(0.02f, 0.38f, 0.055f));
            rim.Box(new Vector3(side * 0.115f, 0f, 0f), new Vector3(0.02f, 0.055f, 0.38f));
            rim.Cylinder(new Vector3(side * 0.12f, 0f, 0f), Vector3.right, 0.05f, 0.03f, 12);
            var rimGo = rim.ToObject("Rim", pivot.transform, Mat.Paint("Rim", new Color(0.70f, 0.70f, 0.72f), 0.85f, 1f));
            rimGo.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

            return new DriftCarController.Wheel { visual = pivot.transform, localMount = mount, front = front, left = left };
        }
    }
}
