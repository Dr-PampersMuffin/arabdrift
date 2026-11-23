using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// The workshop's tuning: four upgrade parts in three stages each, rims and paint, saved per car.
    /// Upgrades change the real physics (torque, boost, grip, springs, ride height) and the garage bars; rims and paint
    /// change how the car looks on the turntable and on the street.
    public static class Tuning
    {
        public enum Part { Engine, Turbo, Tyres, Suspension }
        public const int MaxLevel = 3;
        public static readonly Part[] Parts = { Part.Engine, Part.Turbo, Part.Tyres, Part.Suspension };
        public static string PartAr(Part p) => p == Part.Engine ? "المكينة" : p == Part.Turbo ? "التيربو" : p == Part.Tyres ? "الكفرات" : "التعليق";
        public static string PartIcon(Part p) => p == Part.Engine ? "hud/ic_flame" : p == Part.Turbo ? "hud/ic_trophy" : p == Part.Tyres ? "hud/ic_wheel" : "hud/ic_car";
        public static string StageAr(int lvl) => lvl == 0 ? "وكالة" : lvl == 1 ? "شارع" : lvl == 2 ? "سباق" : "بطولة";

        /// What each stage does, in the words a mechanic would use.
        public static string WhatAr(Part p, int lvl, CarSpec baseSpec)
        {
            switch (p)
            {
                case Part.Engine: return lvl == 1 ? "فلتر هوا وبواجي .. عزم +١٠٪" : lvl == 2 ? "كامات ورأس مكينة .. عزم +٢٠٪" : "مكينة مفتوحة .. عزم +٣٢٪";
                case Part.Turbo: return baseSpec.turbo ? (lvl == 1 ? "بوست أعلى" : lvl == 2 ? "تيربو أكبر .. يشتغل أسرع" : "تيربو بطولة .. بوست كامل")
                                                       : (lvl == 1 ? "طقم تيربو خفيف" : lvl == 2 ? "تيربو متوسط وانتركولر" : "تيربو كبير .. صفير ونار");
                case Part.Tyres: return lvl == 1 ? "كفرات شارع أعرض" : lvl == 2 ? "كفرات رياضية .. مسكة أقوى" : "كفرات سباق .. تحكم كامل";
                default: return lvl == 1 ? "سوست قصيرة .. تنزل ٢٫٥ سم" : lvl == 2 ? "كويل اوفر .. تنزل ٥ سم" : "تعليق سباق .. تنزل ٧٫٥ سم";
            }
        }

        static readonly long[][] Prices =
        {
            new long[] { 6000, 14000, 28000 },   // engine
            new long[] { 8000, 16000, 32000 },   // turbo
            new long[] { 4000, 9000, 18000 },    // tyres
            new long[] { 5000, 11000, 22000 },   // suspension
        };
        public static long Price(Part p, int nextLevel) => Prices[(int)p][Mathf.Clamp(nextLevel - 1, 0, 2)];

        public static int Level(string car, Part p) => PlayerPrefs.GetInt($"dk_t_{car}_{(int)p}", 0);
        static void SetLevel(string car, Part p, int v) { PlayerPrefs.SetInt($"dk_t_{car}_{(int)p}", v); PlayerPrefs.Save(); }

        /// Buys the next stage. False when maxed or not enough cash.
        public static bool Buy(string car, Part p)
        {
            int l = Level(car, p);
            if (l >= MaxLevel) return false;
            long price = Price(p, l + 1);
            if (Wallet.Cash < price) return false;
            Wallet.Add(-price, 0);
            SetLevel(car, p, l + 1);
            Changed?.Invoke(car);
            return true;
        }

        // ---------------- Rims ----------------
        public struct RimStyle { public string ar; public int spokes; public Color rim, lip; }
        public static readonly RimStyle[] Rims =
        {
            new RimStyle { ar = "وكالة" },
            new RimStyle { ar = "شبك ذهبي", spokes = 0, rim = new Color(0.78f, 0.6f, 0.28f), lip = new Color(0.88f, 0.88f, 0.9f) },
            new RimStyle { ar = "خمس فضي", spokes = 5, rim = new Color(0.82f, 0.82f, 0.85f), lip = new Color(0.9f, 0.9f, 0.92f) },
            new RimStyle { ar = "ثمان أسود", spokes = 8, rim = new Color(0.07f, 0.07f, 0.08f), lip = new Color(0.82f, 0.82f, 0.84f) },
            new RimStyle { ar = "عشر أبيض", spokes = 10, rim = new Color(0.92f, 0.92f, 0.9f), lip = new Color(0.92f, 0.92f, 0.9f) },
            new RimStyle { ar = "شبك برونز", spokes = 0, rim = new Color(0.36f, 0.25f, 0.14f), lip = new Color(0.8f, 0.8f, 0.82f) },
        };
        public const long RimPrice = 4500;
        public static int Rim(string car) => PlayerPrefs.GetInt($"dk_rim_{car}", 0);
        public static bool OwnsRim(string car, int i) => i == 0 || PlayerPrefs.GetInt($"dk_rimown_{car}_{i}", 0) == 1;
        public static bool SetRim(string car, int i)
        {
            if (!OwnsRim(car, i))
            {
                if (Wallet.Cash < RimPrice) return false;
                Wallet.Add(-RimPrice, 0);
                PlayerPrefs.SetInt($"dk_rimown_{car}_{i}", 1);
            }
            PlayerPrefs.SetInt($"dk_rim_{car}", i); PlayerPrefs.Save();
            Changed?.Invoke(car);
            return true;
        }

        // ---------------- Paint ----------------
        public struct PaintColor { public string ar; public Color c; }
        public static readonly PaintColor[] Paints =
        {
            new PaintColor { ar = "وكالة", c = Color.white },
            new PaintColor { ar = "أبيض لؤلؤي", c = new Color(0.98f, 0.97f, 0.93f) },
            new PaintColor { ar = "أحمر", c = new Color(0.78f, 0.08f, 0.06f) },
            new PaintColor { ar = "أسود", c = new Color(0.1f, 0.1f, 0.11f) },
            new PaintColor { ar = "كحلي", c = new Color(0.1f, 0.17f, 0.38f) },
            new PaintColor { ar = "فضي", c = new Color(0.72f, 0.74f, 0.77f) },
            new PaintColor { ar = "رملي", c = new Color(0.86f, 0.72f, 0.48f) },
            new PaintColor { ar = "أخضر", c = new Color(0.1f, 0.36f, 0.2f) },
            new PaintColor { ar = "برتقالي", c = new Color(0.95f, 0.42f, 0.08f) },
            new PaintColor { ar = "ذهبي", c = new Color(0.82f, 0.62f, 0.22f) },
        };
        public const long PaintPrice = 3500;
        public static int Paint(string car) => PlayerPrefs.GetInt($"dk_paint_{car}", 0);
        public static bool SetPaint(string car, int i)
        {
            if (i == Paint(car)) return true;
            if (i != 0)
            {
                if (Wallet.Cash < PaintPrice) return false;
                Wallet.Add(-PaintPrice, 0);
            }
            PlayerPrefs.SetInt($"dk_paint_{car}", i); PlayerPrefs.Save();
            Changed?.Invoke(car);
            return true;
        }

        public static event System.Action<string> Changed;

        // ---------------- The tuned car ----------------
        /// The car as it leaves the workshop: base spec with the upgrades, rims and ride height applied.
        public static CarSpec Effective(CarSpec b)
        {
            var s = b.Clone();
            int e = Level(b.id, Part.Engine), t = Level(b.id, Part.Turbo), y = Level(b.id, Part.Tyres), u = Level(b.id, Part.Suspension);
            float[] eMul = { 1f, 1.10f, 1.20f, 1.32f };
            s.baseTorque *= eMul[e];
            s.maxRpm += 200f * e;
            if (t > 0)
            {
                if (b.turbo) { s.boost = b.boost + 0.12f * t; s.turboSpool = b.turboSpool * (1f - 0.15f * t); }
                else { s.turbo = true; s.boost = new[] { 0f, 0.2f, 0.3f, 0.42f }[t]; s.turboSpool = new[] { 0f, 0.85f, 0.7f, 0.6f }[t]; }
            }
            s.rearGrip *= 1f + 0.04f * y;
            s.frontGrip *= 1f + 0.05f * y;
            s.rideDrop = 0.025f * u;
            s.springMul = 1f + 0.12f * u;
            s.antiRollMul = 1f + 0.18f * u;

            s.topSpeed = Mathf.Min(10f, b.topSpeed + 0.35f * e + 0.25f * t);
            s.accel = Mathf.Min(10f, b.accel + 0.45f * e + 0.4f * t);
            s.handling = Mathf.Min(10f, b.handling + 0.35f * y + 0.35f * u);
            s.braking = Mathf.Min(10f, b.braking + 0.4f * y);
            s.drift = Mathf.Min(10f, b.drift + 0.15f * e + 0.1f * t + 0.2f * u);

            int r = Rim(b.id);
            if (r > 0 && r < Rims.Length) { s.rimSpokes = Rims[r].spokes; s.rimColor = Rims[r].rim; s.rimLip = Rims[r].lip; }
            s.paintIndex = Paint(b.id);
            return s;
        }

        /// Peak power (hp) and torque (Nm) as the dyno shows them, from the physics numbers.
        public static void Dyno(CarSpec s, out float hp, out float nm)
        {
            hp = 0f; nm = 0f;
            for (int i = 0; i <= 60; i++)
            {
                float x = 0.05f + 0.95f * i / 60f, t = TorqueAt(s, x);
                nm = Mathf.Max(nm, t);
                hp = Mathf.Max(hp, t * x * s.maxRpm / 7121f);       // P(hp) = T(Nm) x rpm / 7121
            }
        }

        /// Torque at a given rpm (0..1 of the limiter), for the dyno curve.
        public static float TorqueAt(CarSpec s, float x)
        {
            float na = Mathf.Sin(Mathf.Clamp01(0.15f + x * 0.9f) * Mathf.PI * 0.95f) * 0.85f + 0.15f;
            float boost = s.turbo ? s.boost * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f + s.turboSpool * 0.2f, 0.55f + s.turboSpool * 0.2f, x)) : 0f;
            return s.baseTorque * (na + boost) * (x > 0.96f ? Mathf.Lerp(1f, 0.85f, (x - 0.96f) / 0.04f) : 1f);
        }

        // ---------------- Paint on the body texture ----------------
        static readonly Dictionary<string, Material> paintCache = new Dictionary<string, Material>();

        /// The car's paint material in the chosen colour (null = the factory paint). The body panels found by
        /// tools/art/paint_mask.py are recoloured keeping their shading; glass, trim, lights and badges keep theirs.
        public static Material PaintMaterial(string id, int paintIndex)
        {
            if (paintIndex <= 0 || paintIndex >= Paints.Length) return null;
            string key = id + "_" + paintIndex;
            if (paintCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var src = Resources.Load<Material>($"DriftKuwait/Cars/{id}/paint");
            var mask = Resources.Load<Texture2D>($"DriftKuwait/Cars/{id}/paintmask");
            if (src == null || mask == null || !mask.isReadable) return null;
            Color32[] px;
            try { px = mask.GetPixels32(); } catch { return null; }

            // the body's own brightness, so the new colour lands at the same shading
            double sum = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 7)
                if (px[i].a > 230) { sum += 0.299 * px[i].r + 0.587 * px[i].g + 0.114 * px[i].b; n++; }
            float body = n > 0 ? (float)(sum / n) : 160f;
            Color tint = Paints[paintIndex].c;
            float tr = tint.r * 255f, tg = tint.g * 255f, tb = tint.b * 255f;
            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a == 0) { outPx[i] = new Color32(p.r, p.g, p.b, 255); continue; }
                float a = p.a / 255f;
                float lum = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                float shade = Mathf.Clamp(lum / body, 0f, 1.6f);
                // keep the highlights from washing out dark colours: gentle curve on the shade
                float sh = shade < 1f ? shade : 1f + (shade - 1f) * 0.5f;
                byte r = (byte)Mathf.Clamp(Mathf.Lerp(p.r, tr * sh, a), 0f, 255f);
                byte g = (byte)Mathf.Clamp(Mathf.Lerp(p.g, tg * sh, a), 0f, 255f);
                byte b = (byte)Mathf.Clamp(Mathf.Lerp(p.b, tb * sh, a), 0f, 255f);
                outPx[i] = new Color32(r, g, b, 255);
            }
            var tex = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, true) { name = "DK_Paint_" + key, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(outPx);
            tex.Apply(true, true);
            var m = new Material(src) { name = "DK_Paint_" + key };
            m.SetTexture("_BaseMap", tex);
            paintCache[key] = m;
            return m;
        }
    }
}
