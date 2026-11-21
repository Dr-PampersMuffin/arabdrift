using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Title screen (reference: the "روح الشارع / DRIFT KUWAIT 1985" mock-up, renamed ARAB DRIFT):
    /// golden-hour key art with a slow push-in, the logo top-left, the main menu on the right, district cards bottom-left,
    /// the campaign cassette bottom-right.
    public class TitleScreen : MonoBehaviour
    {
        RawImage bg;
        float t;

        public static TitleScreen Build(GameFlow flow)
        {
            var canvas = UIKit.MakeCanvas("DK_Title", 10);
            var ts = canvas.gameObject.AddComponent<TitleScreen>();
            var root = canvas.transform;

            ts.bg = UIKit.Cover(root, "KeyArt", Resources.Load<Texture2D>("DriftKuwait/UI/title_bg"));
            ts.bg.GetComponent<CoverFit>().focus = new Vector2(0.35f, 0.5f);

            // Shade the left (logo) and right (menu) edges so text reads over the photo.
            var shadeL = UIKit.Img(root, "ShadeL", UIKit.S("hud/grad_h"), new Color(1, 1, 1, 0.75f));
            shadeL.rectTransform.anchorMin = new Vector2(0f, 0f); shadeL.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            shadeL.rectTransform.offsetMin = shadeL.rectTransform.offsetMax = Vector2.zero;
            shadeL.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var shadeR = UIKit.Img(root, "ShadeR", UIKit.S("hud/grad_h"), new Color(1, 1, 1, 0.85f));
            shadeR.rectTransform.anchorMin = new Vector2(0.62f, 0f); shadeR.rectTransform.anchorMax = new Vector2(1f, 1f);
            shadeR.rectTransform.offsetMin = shadeR.rectTransform.offsetMax = Vector2.zero;
            var shadeB = UIKit.Img(root, "ShadeB", UIKit.S("hud/grad_v"), new Color(1, 1, 1, 0.8f));
            shadeB.rectTransform.anchorMin = new Vector2(0f, 0f); shadeB.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            shadeB.rectTransform.offsetMin = shadeB.rectTransform.offsetMax = Vector2.zero;
            shadeB.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            var safe = UIKit.SafeArea(root);

            // Logo
            var logoSp = UIKit.S("logo");
            var logo = UIKit.Img(safe, "Logo", logoSp, Color.white);
            logo.preserveAspect = true;
            UIKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(880f, 400f));
            var ver = UIKit.Label(safe, "Ver", "VER 0.3", 22, new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft, UIKit.Arabic, false);
            UIKit.Place(ver.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -14f), new Vector2(600f, 30f));

            // Top-right: guest profile and wallet
            BuildTopRight(safe, flow);

            // Main menu (right column)
            var col = UIKit.Rect(safe, "Menu");
            UIKit.Place(col, new Vector2(1f, 0.5f), new Vector2(-60f, 40f), new Vector2(500f, 640f));
            (string ar, string en, string icon, bool primary, System.Action act)[] items =
            {
                ("ابدأ اللعبة", "PLAY", "flag", true, () => flow.Play()),
                ("الكراج", "GARAGE", "car", false, () => flow.ShowGarage()),
                ("الخريطة", "MAP", "map", false, () => flow.ShowMap()),
                ("الترتيب", "LEADERBOARDS", "trophy", false, () => GameCenter.ShowLeaderboards()),
                ("الإعدادات", "SETTINGS", "gear", false, () => PauseMenu.Open(flow, false)),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var b = UIKit.MenuButton(col, it.ar, it.en, it.icon, it.primary, it.act);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -i * 128f), new Vector2(500f, 112f));
            }

            // District cards (bottom-left)
            var cards = UIKit.Rect(safe, "Districts");
            UIKit.Place(cards, new Vector2(0f, 0f), new Vector2(40f, 70f), new Vector2(1300f, 260f));
            (string id, string ar, string en, bool open)[] districts =
            {
                ("salwa", "سلوى", "SALWA", true), ("rumaithiya", "الرميثية", "RUMAITHIYA", false),
                ("ummniqa", "أم نقا", "UMM NIQA", false), ("kabd", "كبد", "KABD", false),
            };
            for (int i = 0; i < districts.Length; i++)
            {
                var d = districts[i];
                var card = UIKit.Button(cards, "Card_" + d.id, new Color(0.08f, 0.07f, 0.06f, 0.9f),
                    d.open ? (System.Action)(() => flow.ShowMap()) : () => flow.Toast(d.ar + " .. قريباً"), "hud/panel_sharp");
                var crt = (RectTransform)card.transform;
                UIKit.Place(crt, new Vector2(0f, 0f), new Vector2(i * 318f, 0f), new Vector2(300f, 250f));
                var pic = UIKit.Rect(crt, "PicMask");
                UIKit.Stretch(pic, 6, 6, 74, 6);
                pic.gameObject.AddComponent<RectMask2D>();
                var img = UIKit.Cover(pic, "Pic", Resources.Load<Texture2D>("DriftKuwait/UI/district_" + d.id));
                if (!d.open) img.color = new Color(0.55f, 0.55f, 0.55f);
                var n1 = UIKit.Label(crt, "Ar", d.ar, 38, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(n1.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(290f, 46f));
                var n2 = UIKit.Label(crt, "En", d.en, 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, UIKit.Arabic, false);
                UIKit.Place(n2.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(290f, 24f));
                if (d.open)
                {
                    UIKit.Outline(crt, UIKit.Gold, "hud/panel_sharp_outline");
                    var line = UIKit.Img(crt, "Sel", null, UIKit.Gold);
                    UIKit.Place(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(150f, 6f));
                }
                else
                {
                    var lk = UIKit.Img(crt, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.9f));
                    UIKit.Place(lk.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(54f, 54f));
                    var soon = UIKit.Label(crt, "Soon", "قريباً", 28, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black);
                    UIKit.Place(soon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(200f, 40f));
                }
            }

            // Strip of words along the bottom edge
            var strip = UIKit.Label(safe, "Strip", "KUWAIT 1985   |   CARS   |   STREETS   |   DESERT   |   FRIENDS   |   NO LIMITS", 20,
                new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, UIKit.Arabic, false);
            UIKit.Place(strip.rectTransform, new Vector2(0f, 0f), new Vector2(48f, 22f), new Vector2(1300f, 30f));

            // Campaign cassette (bottom-right)
            var cas = UIKit.Button(safe, "Cassette", new Color(1, 1, 1, 0f), () => flow.Toast("آخر صيف بالثمانينات .. القصة قريباً"));
            var crt2 = (RectTransform)cas.transform;
            UIKit.Place(crt2, new Vector2(1f, 0f), new Vector2(-70f, 40f), new Vector2(430f, 290f));
            crt2.localRotation = Quaternion.Euler(0f, 0f, 4f);
            var tape = UIKit.Img(crt2, "Tape", UIKit.S("cassette"), Color.white);
            tape.preserveAspect = true;
            UIKit.Stretch(tape.rectTransform);
            var l1 = UIKit.Label(crt2, "T1", "آخر صيف بالثمانينات", 34, new Color(0.62f, 0.12f, 0.1f), TextAnchor.MiddleCenter, UIKit.Display);
            UIKit.Place(l1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(330f, 44f));
            var l2 = UIKit.Label(crt2, "T2", "THE LAST SUMMER OF THE EIGHTIES", 15, new Color(0.15f, 0.12f, 0.1f), TextAnchor.MiddleCenter, UIKit.Arabic, false);
            UIKit.Place(l2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(330f, 22f));
            var l3 = UIKit.Label(crt2, "T3", "الذكريات ما تنتهي .. نرجع لها", 22, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Display);
            UIKit.Place(l3.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(400f, 34f));
            UIKit.AddShadow(l3, 2f, 0.8f);

            if (!PreviewHooks.Active) Sfx.Voice("ab_title", 0.9f);
            return ts;
        }

        static void BuildTopRight(RectTransform safe, GameFlow flow)
        {
            var bar = UIKit.Rect(safe, "TopRight");
            UIKit.Place(bar, new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(900f, 84f));
            float x = 0f;
            var prof = UIKit.Panel(bar, "Profile", UIKit.InkSoft, "hud/pill");
            UIKit.Place(prof.rectTransform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(210f, 72f));
            var pic = UIKit.Img(prof.transform, "Ic", UIKit.S("hud/ic_people"), UIKit.Cream);
            UIKit.Place(pic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(48f, 48f));
            var nm = UIKit.Label(prof.transform, "Name", "ضيف", 34, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 2f), new Vector2(130f, 60f));
            x -= 230f;
            x = Chip(bar, x, "hud/ic_rep", UIKit.Num(Wallet.Rep), 230f);
            Chip(bar, x, "hud/ic_coin", UIKit.Num(Wallet.Cash), 300f);
        }

        public static float Chip(RectTransform bar, float x, string icon, string value, float w)
        {
            var p = UIKit.Panel(bar, "Chip", UIKit.InkSoft, "hud/pill");
            UIKit.Place(p.rectTransform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, 64f));
            var ic = UIKit.Img(p.transform, "Ic", UIKit.S(icon), Color.white);
            UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 44f));
            var v = UIKit.Label(p.transform, "V", value, 32, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(w - 70f, 60f));
            return x - w - 16f;
        }

        void Update()
        {
            // Slow push-in on the key art
            t += Time.unscaledDeltaTime;
            if (bg != null) bg.rectTransform.localScale = Vector3.one * (1.02f + 0.04f * Mathf.PingPong(t / 18f, 1f));
        }
    }
}
