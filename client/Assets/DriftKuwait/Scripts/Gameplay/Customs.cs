using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// The customs shop (التعديل): pearls (لؤلؤ, the paid currency) and everything that changes how a car looks.
    /// Fair play: looks are bought with pearls; anything that makes a car faster (engine, turbo, nitro) is bought only
    /// with cash earned by drifting. Looks are owned once and can be fitted to any car; each car remembers its own setup.
    public static class Customs
    {
        public enum Cat { Engine, Turbo, Nitro, Rims, Paint, Stickers, Slogans, Fenders, Wing, Exhaust, Smoke, Neon }

        public static string CatAr(Cat c)
        {
            switch (c)
            {
                case Cat.Engine: return "المكينة";
                case Cat.Turbo: return "التيربو";
                case Cat.Nitro: return "النيترو";
                case Cat.Rims: return "الجنوط";
                case Cat.Paint: return "الصبغ";
                case Cat.Stickers: return "الملصقات";
                case Cat.Slogans: return "العبارات";
                case Cat.Fenders: return "الرفارف العريضة";
                case Cat.Wing: return "الجناح";
                case Cat.Exhaust: return "الأكزوز";
                case Cat.Smoke: return "دخان ملوّن";
                default: return "النيون";
            }
        }

        public static string CatSubAr(Cat c)
        {
            switch (c)
            {
                case Cat.Engine: return "قوة أكثر .. بالكاش";
                case Cat.Turbo: return "بوست وصفير";
                case Cat.Nitro: return "دفعة في الطلعة";
                case Cat.Rims: return "جنوط شبك وكروم";
                case Cat.Paint: return "كاندي · لؤلؤي · مطفي";
                case Cat.Stickers: return "صقر · بوم · أبراج";
                case Cat.Slogans: return "على الزجاج";
                case Cat.Fenders: return "وايد بودي";
                case Cat.Wing: return "كاربون";
                case Cat.Exhaust: return "طلقات ونار";
                case Cat.Smoke: return "أخضر · أبيض · أحمر";
                default: return "تحت السيارة";
            }
        }

        public static string CatTile(Cat c)
        {
            switch (c)
            {
                case Cat.Engine: return "tile_engine";
                case Cat.Turbo: return "tile_turbo";
                case Cat.Nitro: return "tile_nitro";
                case Cat.Rims: return "tile_rims";
                case Cat.Paint: return "tile_paint";
                case Cat.Stickers: return "tile_stickers";
                case Cat.Slogans: return null;   // drawn: two windscreen banners
                case Cat.Fenders: return "tile_fenders";
                case Cat.Wing: return "tile_wing";
                case Cat.Exhaust: return "tile_exhaust";
                case Cat.Smoke: return "tile_smoke";
                default: return "tile_neon";
            }
        }

        public static bool IsSpeed(Cat c) => c == Cat.Engine || c == Cat.Turbo || c == Cat.Nitro;

        /// One shop item. Rims and paint live in Tuning (per car); the rest are here.
        public class Item
        {
            public string ar;
            public int pearls;
            public Color color = Color.white, color2 = Color.white, color3 = Color.white;
            public string tex;      // sticker / slogan texture in Resources/DriftKuwait/Customs
            public int style;       // per category meaning (wing shape, fender finish, exhaust style)
        }

        static readonly Color KwGreen = new Color(0.0f, 0.48f, 0.24f), KwRed = new Color(0.81f, 0.07f, 0.15f), KwWhite = new Color(0.97f, 0.97f, 0.95f);

        public static readonly Dictionary<Cat, Item[]> Items = new Dictionary<Cat, Item[]>
        {
            { Cat.Stickers, new[] {
                new Item { ar = "صقر", pearls = 80, tex = "stk_falcon" },
                new Item { ar = "بوم", pearls = 80, tex = "stk_dhow" },
                new Item { ar = "أبراج الكويت", pearls = 100, tex = "stk_towers" },
                new Item { ar = "خطوط العلم", pearls = 100, tex = "stk_flag" },
                new Item { ar = "دلة ونخلة", pearls = 80, tex = "stk_dallah" },
                new Item { ar = "هجن", pearls = 80, tex = "stk_camel" } } },
            { Cat.Slogans, new[] {
                new Item { ar = "شباب سلوى", pearls = 60, tex = "slg_salwa" },
                new Item { ar = "ملك التقحيص", pearls = 80, tex = "slg_king" },
                new Item { ar = "عيال الديرة", pearls = 60, tex = "slg_deera" },
                new Item { ar = "الكويت ٨٧", pearls = 60, tex = "slg_87" },
                new Item { ar = "لا تلحقني", pearls = 60, tex = "slg_catch" },
                new Item { ar = "على راحتي", pearls = 60, tex = "slg_easy" } } },
            { Cat.Fenders, new[] {
                new Item { ar = "رفارف سودا لامعة", pearls = 400, style = 0 },
                new Item { ar = "رفارف كاربون", pearls = 450, style = 1, color = new Color(0.06f, 0.06f, 0.07f) } } },
            { Cat.Wing, new[] {
                new Item { ar = "ذيل بطة", pearls = 200, style = 0, color = new Color(0.06f, 0.06f, 0.07f) },
                new Item { ar = "جناح كاربون GT", pearls = 300, style = 1, color = new Color(0.05f, 0.05f, 0.06f) },
                new Item { ar = "جناح عالي أحمر", pearls = 300, style = 2, color = new Color(0.75f, 0.08f, 0.06f) } } },
            { Cat.Exhaust, new[] {
                new Item { ar = "نار زرقا", pearls = 150, color = new Color(0.35f, 0.55f, 1f), color2 = new Color(0.55f, 0.8f, 1f) },
                new Item { ar = "نار خضرا", pearls = 150, color = new Color(0.25f, 1f, 0.4f), color2 = new Color(0.7f, 1f, 0.6f) },
                new Item { ar = "طلقات عالية", pearls = 120, style = 1, color = new Color(1f, 0.55f, 0.12f), color2 = new Color(1f, 0.85f, 0.4f) } } },
            { Cat.Smoke, new[] {
                new Item { ar = "علم الكويت", pearls = 250, style = 1, color = KwGreen, color2 = KwWhite, color3 = KwRed },
                new Item { ar = "أحمر", pearls = 150, color = new Color(0.9f, 0.12f, 0.12f) },
                new Item { ar = "أزرق", pearls = 150, color = new Color(0.15f, 0.4f, 0.95f) },
                new Item { ar = "أخضر", pearls = 150, color = new Color(0.1f, 0.7f, 0.3f) },
                new Item { ar = "ذهبي", pearls = 180, color = new Color(0.98f, 0.75f, 0.2f) },
                new Item { ar = "أسود", pearls = 150, color = new Color(0.12f, 0.12f, 0.13f) } } },
            { Cat.Neon, new[] {
                new Item { ar = "سماوي", pearls = 200, color = new Color(0.1f, 0.9f, 1f) },
                new Item { ar = "أحمر", pearls = 200, color = new Color(1f, 0.1f, 0.15f) },
                new Item { ar = "أخضر", pearls = 200, color = new Color(0.15f, 1f, 0.35f) },
                new Item { ar = "بنفسجي", pearls = 200, color = new Color(0.65f, 0.2f, 1f) } } },
        };

        // ---------------------------------------------------------------- pearls
        public static long Pearls { get => GetL("dk_pearls", 0); private set => SetL("dk_pearls", value); }
        public static void AddPearls(long n) { Pearls = System.Math.Max(0, Pearls + n); Changed?.Invoke(); }
        public static bool SpendPearls(long n) { if (Pearls < n) return false; Pearls -= n; Changed?.Invoke(); return true; }

        public static event System.Action Changed;
        public static void Notify() => Changed?.Invoke();

        // ---------------------------------------------------------------- ownership and fitting
        public static bool Owns(Cat c, int i) => PlayerPrefs.GetInt($"dk_own_{(int)c}_{i}", 0) == 1;
        static void Own(Cat c, int i) { PlayerPrefs.SetInt($"dk_own_{(int)c}_{i}", 1); PlayerPrefs.Save(); }

        /// What a car has fitted (-1 = nothing). While trying on in the shop, the preview wins.
        public static int Fitted(string car, Cat c)
        {
            if (PreviewCar == car && Preview.TryGetValue(c, out int p)) return p;
            return PlayerPrefs.GetInt($"dk_eq_{car}_{(int)c}", -1);
        }
        public static int SavedFit(string car, Cat c) => PlayerPrefs.GetInt($"dk_eq_{car}_{(int)c}", -1);
        public static void Fit(string car, Cat c, int i) { PlayerPrefs.SetInt($"dk_eq_{car}_{(int)c}", i); PlayerPrefs.Save(); Changed?.Invoke(); }

        /// Buys (if needed) and fits. False when not enough pearls.
        public static bool BuyAndFit(string car, Cat c, int i)
        {
            if (i >= 0 && !Owns(c, i))
            {
                if (!SpendPearls(Items[c][i].pearls)) return false;
                Own(c, i);
            }
            Fit(car, c, i);
            return true;
        }

        public static Item FittedItem(string car, Cat c)
        {
            int i = Fitted(car, c);
            return Items.TryGetValue(c, out var list) && i >= 0 && i < list.Length ? list[i] : null;
        }

        // ---------------------------------------------------------------- try-on
        public static string PreviewCar;
        public static readonly Dictionary<Cat, int> Preview = new Dictionary<Cat, int>();
        public static void ClearPreview() { Preview.Clear(); PreviewCar = null; Tuning.PreviewRim = -1; Tuning.PreviewPaint = -1; }

        // ---------------------------------------------------------------- nitro (speed: cash only)
        public const int NitroMax = 2;
        public static readonly long[] NitroPrice = { 12000, 24000 };
        public static int Nitro(string car) => PlayerPrefs.GetInt($"dk_nitro_{car}", 0);
        public static bool BuyNitro(string car)
        {
            int l = Nitro(car);
            if (l >= NitroMax) return false;
            long price = NitroPrice[l];
            if (Wallet.Cash < price) return false;
            Wallet.Add(-price, 0);
            PlayerPrefs.SetInt($"dk_nitro_{car}", l + 1); PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- starter pack
        public static bool StarterOwned => PlayerPrefs.GetInt("dk_starter", 0) == 1;
        public const int StarterPearls = 500;
        public static int StarterRim => Tuning.RoyalGoldRim;

        /// Grants the starter pack once (purchase or restore): pearls only the first time, the items always.
        public static void GrantStarter(bool pearls)
        {
            if (pearls && !StarterOwned) AddPearls(StarterPearls);
            PlayerPrefs.SetInt("dk_starter", 1);
            Own(Cat.Smoke, 0);   // Kuwait flag smoke
            foreach (var c in CarCatalog.Garage) Tuning.GiveRim(c.id, StarterRim);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static long GetL(string k, long d) { try { return long.Parse(PlayerPrefs.GetString(k, d.ToString())); } catch { return d; } }
        static void SetL(string k, long v) { try { PlayerPrefs.SetString(k, v.ToString()); PlayerPrefs.Save(); } catch { } }
    }
}
