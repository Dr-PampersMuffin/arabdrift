using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// After an event: the result, stars won (animated in), best, rewards, and what to do next.
    public class ResultsScreen : MonoBehaviour
    {
        public static void Show(GameFlow flow, Career.EventDef ev, long result, int stars, int cash, int rep, bool best)
        {
            var c = UIKit.MakeCanvas("DK_Results", 55);
            var r = c.gameObject.AddComponent<ResultsScreen>();
            var root = c.transform;
            var dim = UIKit.Img(root, "Dim", null, new Color(0, 0, 0, 0.55f), true);
            UIKit.Stretch(dim.rectTransform);
            var p = UIKit.Panel(root, "Panel", new Color(0.06f, 0.055f, 0.05f, 0.95f));
            UIKit.Place(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));
            UIKit.Outline(p.transform, new Color(1f, 0.7f, 0.2f, 0.5f));
            var tr = p.transform;

            var t = UIKit.Label(tr, "Title", ev.titleAr, 70, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Display);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1200f, 90f));
            string verdict = stars == 3 ? "أسطورة الشارع!" : stars == 2 ? "عفيه عليك!" : stars == 1 ? "مو بطّال .. تقدر أحسن" : "ما وصلت الهدف .. جرّب مرة ثانية";
            var v = UIKit.Label(tr, "Verdict", verdict, 40, stars > 0 ? UIKit.Gold : new Color(1f, 0.45f, 0.35f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 60f));

            // Stars
            var starImgs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Img(tr, "Star" + i, UIKit.S("hud/ic_star_big"), new Color(1f, 1f, 1f, 0.15f));
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 1f), new Vector2((1 - i) * 190f, -290f), new Vector2(170f, 170f));
                starImgs[i] = s;
            }

            string resLabel = ev.kind == Career.Kind.Escape ? (result > 0 ? "وقت الهروب" : "انحجزت السيارة") : ev.kind == Career.Kind.Chain ? "أطول سلسلة" : "النقاط";
            string resValue = ev.kind == Career.Kind.Escape ? (result > 0 ? UIKit.Num(result) + " ث" : "—") : UIKit.Num(result);
            Row(tr, 0, resLabel, resValue, true);
            long bestV = Career.Best(ev.id);
            Row(tr, 1, best ? "رقم قياسي جديد!" : "أفضل نتيجة", ev.kind == Career.Kind.Escape ? (bestV > 0 ? UIKit.Num(bestV) + " ث" : "—") : UIKit.Num(bestV), false);
            Row(tr, 2, "الجايزة", "+" + UIKit.Num(cash) + " كاش   +" + UIKit.Num(rep) + " سمعة", false);
            if (ev.stars != null)
            {
                string targets = ev.kind == Career.Kind.Escape
                    ? $"النجوم: أقل من {UIKit.Num(ev.stars[0])} · {UIKit.Num(ev.stars[1])} · {UIKit.Num(ev.stars[2])} ثانية"
                    : $"النجوم: {UIKit.Num(ev.stars[0])} · {UIKit.Num(ev.stars[1])} · {UIKit.Num(ev.stars[2])}";
                var tg = UIKit.Label(tr, "Targets", targets, 30, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, UIKit.Arabic);
                UIKit.Place(tg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1300f, 44f));
            }

            Btn(tr, "إعادة", new Vector2(1f, 0f), new Vector2(-60f, 40f), true, () => { Destroy(c.gameObject); flow.StartEvent(ev); });
            Btn(tr, "الخريطة", new Vector2(0.5f, 0f), new Vector2(0f, 40f), false, () => { Destroy(c.gameObject); flow.ShowMap(); });
            var next = NextEvent(ev);
            if (next != null && Career.Unlocked(next))
                Btn(tr, "التالي: " + next.titleAr, new Vector2(0f, 0f), new Vector2(60f, 40f), false, () => { Destroy(c.gameObject); flow.StartEvent(next); });
            else
                Btn(tr, "الكراج", new Vector2(0f, 0f), new Vector2(60f, 40f), false, () => { Destroy(c.gameObject); flow.ShowGarage(); });

            r.StartCoroutine(r.StarsIn(starImgs, stars));
            Sfx.Play("ui_reward", 0.9f);
        }

        IEnumerator StarsIn(Image[] imgs, int n)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            for (int i = 0; i < n; i++)
            {
                var im = imgs[i];
                im.color = UIKit.Gold;
                float t = 0f;
                while (t < 0.25f) { t += Time.unscaledDeltaTime; im.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t / 0.25f); yield return null; }
                Sfx.Play("ui_click", 1f);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        static Career.EventDef NextEvent(Career.EventDef ev)
        {
            int i = Career.Events.IndexOf(ev);
            return i >= 0 && i + 1 < Career.Events.Count ? Career.Events[i + 1] : null;
        }

        static void Row(Transform tr, int i, string label, string value, bool big)
        {
            float y = -430f - i * 70f;
            var l = UIKit.Label(tr, "L" + i, label, 38, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(260f, y), new Vector2(520f, 60f));
            var v = UIKit.Label(tr, "V" + i, value, big ? 56 : 38, big ? UIKit.Gold : Color.white, TextAnchor.MiddleLeft, UIKit.Black);
            UIKit.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(-280f, y), new Vector2(560f, 64f));
        }

        static void Btn(Transform tr, string text, Vector2 anchor, Vector2 pos, bool primary, System.Action act)
        {
            var b = UIKit.Button(tr, "B_" + text, primary ? UIKit.Gold : UIKit.Ink, act, "hud/panel_sharp");
            UIKit.Place((RectTransform)b.transform, anchor, pos, new Vector2(440f, 100f));
            if (!primary) UIKit.Outline(b.transform, new Color(1, 1, 1, 0.25f), "hud/panel_sharp_outline");
            var t = UIKit.Label(b.transform, "T", text, 38, primary ? new Color(0.12f, 0.08f, 0.03f) : UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(t.rectTransform);
        }
    }
}
