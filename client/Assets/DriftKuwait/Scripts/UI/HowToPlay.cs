using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// طريقة اللعب: five short cards on the goal and the controls. Shown before the first event, and from Settings.
    public class HowToPlay : MonoBehaviour
    {
        static readonly (string icon, string title, string body)[] Cards =
        {
            ("hud/ic_trophy", "الهدف", "تقحّص بشوارع الكويت وجمّع نقاط. كل تحدي على الخريطة له ثلاث نجوم. النجوم تفتح لك تحديات جديدة، والكاش والسمعة تشتري فيها سيارات وقطع."),
            ("hud/ic_wheel", "التحكم", "السكّان على اليسار: حط صبعك عليه ولفّه. على اليمين: دفع وفرامل والهاند بريك. الناقل اليدوي: + تبديل فوگ و − تبديل تحت. تقدر تغيّر التحكم من الإعدادات."),
            ("hud/ic_flame", "شلون تقحّص", "دوس بانزين، لف السكّان واسحب الهاند بريك لحظة عشان يزلق الصدر. بعدين عاكس السكّان وخلك ماسك البانزين عشان تكمّل الزلقة."),
            ("hud/ic_rep", "النقاط", "النقاط من ثلاث: الزاوية والسرعة والاقتراب من الجدران. كل ما طالت السلسلة زاد المضاعف. لو اعتدلت السيارة أو صدمت تنقطع السلسلة."),
            ("hud/ic_people", "الترتيب", "سجّل دخول Game Center وتنافس على أفضل جولة وأطول سلسلة ونجوم المسيرة ضد لاعبين الكويت والخليج. التقحيص الأونلاين ويا الربع جاي قريب."),
        };

        int page;
        Image icon; Text title, body, pageTxt, nextTxt;
        System.Action onClose;

        public static bool Seen { get { try { return PlayerPrefs.GetInt("dk_howto", 0) == 1; } catch { return true; } } set { try { PlayerPrefs.SetInt("dk_howto", value ? 1 : 0); PlayerPrefs.Save(); } catch { } } }

        public static void Show(System.Action onClose)
        {
            var c = UIKit.MakeCanvas("DK_HowTo", 70);
            var h = c.gameObject.AddComponent<HowToPlay>();
            h.onClose = onClose;
            h.Build(c.transform);
        }

        void Build(Transform root)
        {
            var dim = UIKit.Img(root, "Dim", null, new Color(0, 0, 0, 0.7f), true);
            UIKit.Stretch(dim.rectTransform);
            var p = UIKit.Panel(root, "Panel", new Color(0.06f, 0.055f, 0.05f, 0.97f));
            UIKit.Place(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 760f));
            UIKit.Outline(p.transform, new Color(1f, 0.7f, 0.2f, 0.5f));
            var tr = p.transform;
            var head = UIKit.Label(tr, "Head", "طريقة اللعب", 40, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(800f, 50f));
            icon = UIKit.Img(tr, "Icon", null, UIKit.Gold);
            UIKit.Place(icon.rectTransform, new Vector2(1f, 1f), new Vector2(-170f, -230f), new Vector2(170f, 170f));
            title = UIKit.Label(tr, "Title", "", 76, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(title.rectTransform, new Vector2(1f, 1f), new Vector2(-560f, -170f), new Vector2(700f, 100f));
            body = UIKit.Label(tr, "Body", "", 38, Color.white, TextAnchor.UpperRight, UIKit.Arabic);
            UIKit.Place(body.rectTransform, new Vector2(1f, 1f), new Vector2(-560f, -360f), new Vector2(1000f, 260f));
            body.lineSpacing = 1.15f;
            pageTxt = UIKit.Label(tr, "Page", "", 30, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(pageTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(300f, 50f));

            var next = UIKit.Button(tr, "Next", UIKit.Gold, Next, "hud/panel_sharp");
            UIKit.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(380f, 100f));
            nextTxt = UIKit.Label(next.transform, "T", "التالي", 44, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(nextTxt.rectTransform);
            var prev = UIKit.Button(tr, "Prev", UIKit.Ink, () => { if (page > 0) { page--; Refresh(); } }, "hud/panel_sharp");
            UIKit.Place((RectTransform)prev.transform, new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 100f));
            UIKit.Outline(prev.transform, new Color(1, 1, 1, 0.25f), "hud/panel_sharp_outline");
            var pt = UIKit.Label(prev.transform, "T", "السابق", 40, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(pt.rectTransform);
            Refresh();
        }

        void Refresh()
        {
            var c = Cards[page];
            icon.sprite = UIKit.S(c.icon);
            title.text = UIKit.Ar(c.title);
            body.text = UIKit.ArWrap(c.body, 46);
            pageTxt.text = UIKit.Num(page + 1, false) + " / " + UIKit.Num(Cards.Length, false);
            nextTxt.text = UIKit.Ar(page == Cards.Length - 1 ? "يلّا نبدأ" : "التالي");
            Sfx.Play("ui_whoosh", 0.5f);
        }

        void Next()
        {
            if (page < Cards.Length - 1) { page++; Refresh(); return; }
            Seen = true;
            Destroy(gameObject);
            onClose?.Invoke();
        }
    }
}
