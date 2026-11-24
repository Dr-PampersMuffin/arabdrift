using UnityEngine;

namespace DriftKuwait
{
    /// Tileable procedural textures so v0.1 needs no image files.
    /// These are placeholders; scanned PBR textures replace them in the art pass.
    public static class TextureKit
    {
        static Texture2D sand, asphalt, paving, plaster, smoke;

        // Photographic tileables (tools/art/make_textures.py) replace the procedural fallbacks when present.
        static Texture2D stone, frond;
        public static Texture2D Stone => stone != null ? stone : (stone = Res("stone") ?? Plaster);
        public static Texture2D Frond => frond != null ? frond : (frond = Res("frond_v3") ?? Res("frond"));
        static Texture2D trunk;
        public static Texture2D Trunk => trunk != null ? trunk : (trunk = Res("palm_trunk"));
        /// True when the build fetched the CC0 photo-scanned set (tools/art/fetch_cc0.py).
        public static bool Pbr => Res("pbr_asphalt") != null;
        static Texture2D Res(string n) => Resources.Load<Texture2D>("DriftKuwait/Tex/" + n);

        static float Tile(float x, float y, float period, float scale)
        {
            // 4-corner blend of Perlin makes the noise tile seamlessly over 'period'.
            float fx = x / period, fy = y / period;
            float a = Mathf.PerlinNoise(x * scale, y * scale);
            float b = Mathf.PerlinNoise((x - period) * scale, y * scale);
            float c = Mathf.PerlinNoise(x * scale, (y - period) * scale);
            float d = Mathf.PerlinNoise((x - period) * scale, (y - period) * scale);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static Texture2D Make(string name, int size, System.Func<int, int, Color> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name };
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Sand => sand != null ? sand : (sand = Res("pbr_sand") ?? Res("sand") ?? Make("DK_Sand", 256, (x, y) =>
        {
            float n = Tile(x, y, 256, 0.02f) * 0.6f + Tile(x, y, 256, 0.09f) * 0.3f + Random.value * 0.1f;
            return Color.Lerp(new Color(0.70f, 0.56f, 0.38f), new Color(0.88f, 0.75f, 0.55f), n);
        }));

        public static Texture2D Asphalt => asphalt != null ? asphalt : (asphalt = Res("pbr_asphalt") ?? Res("asphalt_v3") ?? Res("asphalt") ?? Make("DK_Asphalt", 256, (x, y) =>
        {
            float n = Tile(x, y, 256, 0.05f) * 0.5f + Random.value * 0.5f;
            float g = Mathf.Lerp(0.20f, 0.33f, n);
            if (Random.value > 0.985f) g += 0.18f; // pale aggregate stones
            return new Color(g * 1.02f, g, g * 0.96f);
        }));

        public static Texture2D Paving => paving != null ? paving : (paving = Res("pbr_paving") ?? Res("paving") ?? Make("DK_Paving", 256, (x, y) =>
        {
            bool joint = (x % 64) < 2 || (y % 64) < 2;
            float n = Tile(x, y, 256, 0.06f) * 0.3f + Random.value * 0.08f;
            var c = Color.Lerp(new Color(0.72f, 0.67f, 0.58f), new Color(0.84f, 0.79f, 0.70f), n);
            return joint ? c * 0.7f : c;
        }));

        public static Texture2D Plaster => plaster != null ? plaster : (plaster = Res("pbr_plaster") ?? Res("plaster") ?? Make("DK_Plaster", 256, (x, y) =>
        {
            float n = Tile(x, y, 256, 0.03f) * 0.5f + Random.value * 0.12f;
            float v = Mathf.Lerp(0.86f, 1f, n);
            float stain = Mathf.Clamp01(1f - y / 60f) * 0.12f * Tile(x, 0, 256, 0.05f); // dust at the base
            return new Color(v - stain, v - stain * 1.1f, v - stain * 1.3f);
        }));

        /// Soft round puff for tyre smoke and sand.
        public static Texture2D Smoke => smoke != null ? smoke : (smoke = Make("DK_Puff", 128, (x, y) =>
        {
            float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
            float a = Mathf.Clamp01(1f - r) ;
            a = a * a * (0.6f + 0.4f * n);
            return new Color(1f, 1f, 1f, a);
        }));
    }
}
