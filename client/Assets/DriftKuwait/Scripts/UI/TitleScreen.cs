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
            var shadeL = UIKit.Img(root, "ShadeL", UIKit.S("hud/grad_h"), new Color(1, 1, 1, 0.32f));
            shadeL.rectTransform.anchorMin = new Vector2(0f, 0f); shadeL.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            shadeL.rectTransform.offsetMin = shadeL.rectTransform.offsetMax = Vector2.zero;
            shadeL.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var shadeR = UIKit.Img(root, "ShadeR", UIKit.S("hud/grad_h"), new Color(1, 1, 1, 0.62f));
            shadeR.rectTransform.anchorMin = new Vector2(0.62f, 0f); shadeR.rectTransform.anchorMax = new Vector2(1f, 1f);
            shadeR.rectTransform.offsetMin = shadeR.rectTransform.offsetMax = Vector2.zero;
            var shadeB = UIKit.Img(root, "ShadeB", UIKit.S("hud/grad_v"), new Color(1, 1, 1, 0.5f));
            shadeB.rectTransform.anchorMin = new Vector2(0f, 0f); shadeB.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            shadeB.rectTransform.offsetMin = shadeB.rectTransform.offsetMax = Vector2.zero;
            shadeB.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            var safe = UIKit.SafeArea(root);

            // Logo
            var logoSp = UIKit.S("logo");
            var logo = UIKit.Img(safe, "Logo", logoSp, Color.white);
            logo.preserveAspect = true;
            UIKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(880f, 400f));

            // حكايتنا: replay the garage's history and the story so far
            {
                var hb = UIKit.Button(safe, "History", new Color(0.04f, 0.07f, 0.13f, 0.78f), () => { Sfx.Play("ui_click"); Sfx.StopVoice(); StoryScene.Play(Story.Replay(), null); }, "hud/pill");
                UIKit.Place((RectTransform)hb.transform, new Vector2(0f, 1f), new Vector2(48f, -448f), new Vector2(430f, 112f));
                UIKit.Outline(hb.transform, new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.7f), "hud/pill_outline");
                var hi = UIKit.Img(hb.transform, "Ic", UIKit.S("hud/ic_play"), UIKit.Gold);
                UIKit.Place(hi.rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(54f, 54f));
                var ht = UIKit.Label(hb.transform, "T", "حكايتنا", 56, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(ht.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-20f, 10f), new Vector2(320f, 70f));
                var he = UIKit.Label(hb.transform, "E", "OUR STORY", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, UIKit.Digital, false);
                UIKit.Place(he.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-20f, -34f), new Vector2(320f, 26f));
            }
            var ver = UIKit.Label(safe, "Ver", "VER 0.4", 18, new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft, UIKit.Digital, false);
            UIKit.Place(ver.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -14f), new Vector2(600f, 30f));

            // Top-right: guest profile and wallet
            BuildTopRight(safe, flow);

            // Main menu (right column)
            var col = UIKit.Rect(safe, "Menu");
            UIKit.Place(col, new Vector2(1f, 0.5f), new Vector2(-60f, 80f), new Vector2(520f, 640f));   // sits above the goal card
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
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -i * 118f), new Vector2(520f, 106f));
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
                var card = UIKit.Button(cards, "Card_" + d.id, new Color(0.035f, 0.06f, 0.11f, 0.9f),
                    d.open ? (System.Action)(() => flow.ShowMap()) : () => flow.Toast(d.ar + " .. قريباً"), "hud/panel_sharp");
                var crt = (RectTransform)card.transform;
                UIKit.Place(crt, new Vector2(0f, 0f), new Vector2(i * 318f, 0f), new Vector2(300f, 250f));
                var pic = UIKit.Rect(crt, "PicMask");
                UIKit.Stretch(pic, 6, 6, 74, 6);
                pic.gameObject.AddComponent<RectMask2D>();
                var img = UIKit.Cover(pic, "Pic", Resources.Load<Texture2D>("DriftKuwait/UI/district_" + d.id));
                if (!d.open) img.color = new Color(0.55f, 0.55f, 0.55f);
                var n1 = UIKit.Label(crt, "Ar", d.ar, 46, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(n1.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(290f, 56f));
                var n2 = UIKit.Label(crt, "En", d.en, 15, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, UIKit.Digital, false);
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
                    var soon = UIKit.Label(crt, "Soon", "قريباً", 34, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black);
                    UIKit.Place(soon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(200f, 40f));
                }
            }

            // The next level, always one tap away (bottom-right): which level of the chapter, what to do, and a button
            // that starts it (or pays the instalment when that is the step).
            {
                var g = Story.Next();
                var dc = UIKit.Panel(safe, "Debt", UIKit.Ink, "hud/panel_sharp");
                UIKit.Place(dc.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(600f, 262f));
                UIKit.Outline(dc.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");
                UIKit.Stripe(dc.transform, new Vector2(1f, 1f), new Vector2(-24f, -14f), 160f, 4f, true);
                string chapter = Story.Chapter == 1 ? "الفصل الأول: العودة" : Story.Chapter == 2 ? "الفصل الثاني: الكراج" : "الكراج حلالك";
                var ch = UIKit.Label(dc.transform, "Ch", chapter, 28, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(ch.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(550f, 38f));
                var dt = UIKit.Label(dc.transform, "T", g.title, 46, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Display);
                UIKit.Place(dt.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -58f), new Vector2(550f, 62f));
                var ds = UIKit.Label(dc.transform, "S", g.sub, 36, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
                ds.resizeTextForBestFit = true; ds.horizontalOverflow = HorizontalWrapMode.Wrap; ds.verticalOverflow = VerticalWrapMode.Overflow; ds.resizeTextMinSize = 28; ds.resizeTextMaxSize = 42;
                UIKit.Place(ds.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -122f), new Vector2(550f, 54f));
                // one pip per level
                for (int i = 0; i < g.steps; i++)
                {
                    bool doneStep = g.done || i < g.step - 1, cur = i == g.step - 1;
                    var pip = UIKit.Img(dc.transform, "Pip" + i, null, doneStep ? UIKit.Gold : cur ? new Color(1f, 0.85f, 0.4f, 0.55f) : new Color(1, 1, 1, 0.14f));
                    UIKit.Place(pip.rectTransform, new Vector2(1f, 0f), new Vector2(-24f - i * 62f, 34f), new Vector2(54f, 10f));
                }
                if (!g.done)
                {
                    bool play = g.ev != null;
                    var go = UIKit.Button(dc.transform, "Pay", play || g.ready ? UIKit.Gold : new Color(0.3f, 0.3f, 0.32f, 0.95f),
                        () => { if (play) flow.StartEvent(g.ev); else flow.PayDebt(); }, "hud/panel_sharp");
                    UIKit.Place((RectTransform)go.transform, new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(160f, 74f));
                    var pt = UIKit.Label(go.transform, "T", play ? "يلا" : "سدّد", 42, UIKit.Navy, TextAnchor.MiddleCenter, UIKit.Black);
                    UIKit.Stretch(pt.rectTransform);
                }
            }

            // Strip of words along the bottom edge
            var strip = UIKit.Label(safe, "Strip", "KUWAIT 1987   |   CARS   |   STREETS   |   DESERT   |   FRIENDS   |   NO LIMITS", 20,
                new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, UIKit.Digital, false);
            UIKit.Place(strip.rectTransform, new Vector2(0f, 0f), new Vector2(48f, 22f), new Vector2(1300f, 30f));

            // See-through plates so the car and the street show behind the menu
            UIKit.Translucent(safe, 0.45f);
            if (!PreviewHooks.Active) Sfx.Voice("ab_title", 0.9f);
            return ts;
        }

        static void BuildTopRight(RectTransform safe, GameFlow flow)
        {
            var bar = UIKit.Rect(safe, "TopRight");
            UIKit.Place(bar, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(1150f, 96f));
            float x = 0f;
            var prof = UIKit.Panel(bar, "Profile", UIKit.InkSoft, "hud/pill");
            UIKit.Place(prof.rectTransform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(320f, 84f));
            var pic = UIKit.Img(prof.transform, "Ic", UIKit.S("hud/ic_people"), UIKit.Cream);
            UIKit.Place(pic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(48f, 48f));
            string who = GameCenter.PlayerName ?? "ضيف";
            if (who.Length > 16) who = who.Substring(0, 15) + "…";
            var nm = UIKit.Label(prof.transform, "Name", who, 34, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(214f, 60f));
            nm.resizeTextForBestFit = true; nm.horizontalOverflow = HorizontalWrapMode.Wrap; nm.verticalOverflow = VerticalWrapMode.Overflow; nm.resizeTextMinSize = 28; nm.resizeTextMaxSize = 42; nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            x -= 336f;
            x = Chip(bar, x, "hud/ic_rep", UIKit.Dig(Wallet.Rep), 270f, 84f, 40);
            Chip(bar, x, "hud/ic_coin", UIKit.Dig(Wallet.Cash), 380f, 84f, 40);
        }

        public static float Chip(RectTransform bar, float x, string icon, string value, float w, float h = 64f, int font = 28)
        {
            var p = UIKit.Panel(bar, "Chip", UIKit.InkSoft, "hud/pill");
            UIKit.Place(p.rectTransform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, h));
            var ic = UIKit.Img(p.transform, "Ic", UIKit.S(icon), Color.white);
            float isz = h * 0.7f;
            UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(h * 0.18f, 0f), new Vector2(isz, isz));
            var v = UIKit.Label(p.transform, "V", value, font, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Digital, false);
            UIKit.Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(w - isz - 34f, h - 4f));
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
