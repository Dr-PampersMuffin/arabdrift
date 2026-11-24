using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// The workshop side panel over the garage stats: القطع (performance parts in stages), المظهر (paint and rims,
    /// shown live on the turntable car) and the داينو (a run on the rollers with the power and torque curves).
    public class WorkshopPanel : MonoBehaviour
    {
        public enum Mode { Parts, Look, Dyno }
        GarageScreen garage;
        GameFlow flow;
        RectTransform panel, body;
        Mode mode;
        string car;
        Coroutine dynoRun;
        AudioSource rev;

        public static WorkshopPanel Open(GarageScreen g, GameFlow flow, RectTransform safe, Mode m, string carId)
        {
            var old = safe.GetComponentInChildren<WorkshopPanel>();
            if (old != null) Destroy(old.gameObject);
            var p = UIKit.Panel(safe, "Workshop", new Color(0.035f, 0.06f, 0.11f, 0.94f));
            UIKit.Place(p.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, -160f), new Vector2(780f, 640f));   // wide enough for big, readable rows
            UIKit.Outline(p.transform, new Color(1f, 1f, 1f, 0.14f));
            var w = p.gameObject.AddComponent<WorkshopPanel>();
            w.garage = g; w.flow = flow; w.panel = p.rectTransform; w.mode = m; w.car = carId;
            w.Rebuild();
            return w;
        }

        public void SetCar(string id) { car = id; Rebuild(); }

        void Rebuild()
        {
            if (dynoRun != null) { StopCoroutine(dynoRun); dynoRun = null; }
            if (body != null) Destroy(body.gameObject);
            body = UIKit.Rect(panel, "Body");
            UIKit.Stretch(body);
            var tr = body;
            UIKit.Stripe(tr, new Vector2(1f, 1f), new Vector2(-30f, -14f), 220f, 5f, true);
            string title = mode == Mode.Parts ? "القطع والتعديلات" : mode == Mode.Look ? "الصبغ والجنوط" : "الداينو";
            var t = UIKit.Label(tr, "Title", title, 50, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(t.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(460f, 70f));
            var close = UIKit.Button(tr, "Close", new Color(1, 1, 1, 0.08f), () => { Sfx.Play("ui_click"); Destroy(gameObject); }, "hud/pill");
            UIKit.Place((RectTransform)close.transform, new Vector2(0f, 1f), new Vector2(22f, -22f), new Vector2(150f, 66f));
            var ct = UIKit.Label(close.transform, "T", "رجوع", 38, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(ct.rectTransform);

            if (!CarCatalog.Has3D(car))
            {
                var s = UIKit.Label(tr, "Soon", "هذي السيارة قريباً", 34, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 80f));
                return;
            }
            if (mode == Mode.Parts) BuildParts(tr);
            else if (mode == Mode.Look) BuildLook(tr);
            else BuildDyno(tr);
        }

        // ------------------------------------------------------------------ parts
        void BuildParts(RectTransform tr)
        {
            var spec = CarCatalog.Get(car);
            for (int i = 0; i < Tuning.Parts.Length; i++)
            {
                var part = Tuning.Parts[i];
                int lvl = Tuning.Level(car, part);
                float y = -110f - i * 130f;
                var row = UIKit.Panel(tr, "Row" + i, new Color(1, 1, 1, 0.05f), "hud/panel_sharp");
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(736f, 122f));
                var rt = row.transform;
                var ic = UIKit.Img(rt, "Ic", UIKit.S(Tuning.PartIcon(part)), UIKit.Gold);
                UIKit.Place(ic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, 10f), new Vector2(44f, 44f));
                var nm = UIKit.Label(rt, "N", Tuning.PartAr(part), 44, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
                UIKit.Place(nm.rectTransform, new Vector2(1f, 1f), new Vector2(-74f, -4f), new Vector2(230f, 56f));
                var st = UIKit.Label(rt, "S", Tuning.StageAr(lvl), 38, lvl > 0 ? UIKit.Gold : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(st.rectTransform, new Vector2(1f, 1f), new Vector2(-310f, -6f), new Vector2(200f, 52f));
                // stage pips
                for (int k = 0; k < Tuning.MaxLevel; k++)
                {
                    var pip = UIKit.Img(rt, "P" + k, null, k < lvl ? UIKit.Gold : new Color(1, 1, 1, 0.16f));
                    UIKit.Place(pip.rectTransform, new Vector2(1f, 0f), new Vector2(-74f - k * 52f, 8f), new Vector2(44f, 8f));
                }
                string what = lvl < Tuning.MaxLevel ? Tuning.WhatAr(part, lvl + 1, spec) : "أقصى تعديل";
                var wt = UIKit.Label(rt, "W", what, 36, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(wt.rectTransform, new Vector2(1f, 0f), new Vector2(-74f, 18f), new Vector2(460f, 48f));
                wt.resizeTextForBestFit = true; wt.horizontalOverflow = HorizontalWrapMode.Wrap; wt.verticalOverflow = VerticalWrapMode.Overflow; wt.resizeTextMinSize = 28; wt.resizeTextMaxSize = 42;

                bool maxed = lvl >= Tuning.MaxLevel;
                long price = maxed ? 0 : Tuning.Price(part, lvl + 1);
                bool afford = !maxed && Wallet.Cash >= price;
                var p = part;
                var buy = UIKit.Button(rt, "Buy", maxed ? new Color(1, 1, 1, 0.08f) : afford ? UIKit.Gold : new Color(0.3f, 0.3f, 0.32f, 0.9f), () => BuyPart(p), "hud/panel_sharp");
                UIKit.Place((RectTransform)buy.transform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(196f, 98f));
                var bt = UIKit.Label(buy.transform, "T", maxed ? "مكتمل" : "ركّب", 40, maxed ? UIKit.Gold : new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(bt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, maxed ? -22f : -2f), new Vector2(196f, 50f));
                if (!maxed)
                {
                    var pr = UIKit.Label(buy.transform, "Pr", UIKit.Dig(price), 38, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Digital, false);
                    UIKit.Place(pr.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(196f, 44f));
                }
            }
        }

        void BuyPart(Tuning.Part p)
        {
            if (Tuning.Level(car, p) >= Tuning.MaxLevel) return;
            if (!Tuning.Buy(car, p)) { flow.Toast("ما عندك كاش كافي .. اشتغل بالشارع وارجع"); return; }
            Sfx.Play("ui_reward");
            flow.Toast(Tuning.PartAr(p) + " .. " + Tuning.StageAr(Tuning.Level(car, p)) + " ركبت");
            garage.Refresh();
            Rebuild();
        }

        // ------------------------------------------------------------------ paint and rims
        void BuildLook(RectTransform tr)
        {
            var h1 = UIKit.Label(tr, "H1", "الصبغ  ·  " + UIKit.Dig(Tuning.PaintPrice) + " كاش", 40, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(h1.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -100f), new Vector2(720f, 54f));
            int cur = Tuning.Paint(car);
            for (int i = 0; i < Tuning.Paints.Length; i++)
            {
                int idx = i;
                int col = i % 5, row = i / 5;
                var pos = new Vector2(-82f - col * 146f, -190f - row * 122f);
                var sw = UIKit.Button(tr, "Paint" + i, i == 0 ? new Color(0.25f, 0.25f, 0.27f) : Tuning.Paints[i].c, () => PickPaint(idx), "hud/circle_mask");
                UIKit.Place((RectTransform)sw.transform, new Vector2(1f, 1f), pos, new Vector2(74f, 74f));
                var swi = sw.GetComponent<Image>(); swi.sprite = UIKit.S("hud/circle_mask"); swi.type = Image.Type.Simple; swi.preserveAspect = true;
                if (i == cur)
                {
                    var ring = UIKit.Img(tr, "Sel", UIKit.S("hud/circle_mask"), new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.9f));
                    UIKit.Place(ring.rectTransform, new Vector2(1f, 1f), pos, new Vector2(92f, 92f));
                    ring.type = Image.Type.Simple; ring.preserveAspect = true;
                    ring.transform.SetSiblingIndex(sw.transform.GetSiblingIndex());
                }
                var lb = UIKit.Label(tr, "PL" + i, Tuning.Paints[i].ar, 24, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, UIKit.Arabic);
                lb.resizeTextForBestFit = true; lb.horizontalOverflow = HorizontalWrapMode.Wrap; lb.verticalOverflow = VerticalWrapMode.Overflow; lb.resizeTextMinSize = 28; lb.resizeTextMaxSize = 34;
                UIKit.Place(lb.rectTransform, new Vector2(1f, 1f), pos + new Vector2(0f, -58f), new Vector2(144f, 40f));
            }

            var h2 = UIKit.Label(tr, "H2", "الجنوط  ·  " + UIKit.Dig(Tuning.RimPrice) + " كاش", 40, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(h2.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -420f), new Vector2(720f, 54f));
            int rim = Tuning.Rim(car);
            for (int i = 0; i < Tuning.Rims.Length; i++)
            {
                int idx = i;
                int col = i % 3, row = i / 3;
                bool on = i == rim, own = Tuning.OwnsRim(car, i);
                var b = UIKit.Button(tr, "Rim" + i, on ? UIKit.Gold : new Color(1, 1, 1, 0.07f), () => PickRim(idx), "hud/panel_sharp");
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-30f - col * 240f, -480f - row * 82f), new Vector2(228f, 72f));
                var t = UIKit.Label(b.transform, "T", Tuning.Rims[i].ar + (own || on ? "" : " ·"), 38, on ? new Color(0.12f, 0.08f, 0.03f) : UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Stretch(t.rectTransform);
            }
        }

        void PickPaint(int i)
        {
            if (i == Tuning.Paint(car)) return;
            if (!Tuning.SetPaint(car, i)) { flow.Toast("الصبغة بـ " + UIKit.Dig(Tuning.PaintPrice) + " .. ما يكفي الكاش"); return; }
            Sfx.Play("ui_whoosh");
            garage.Refresh();
            Rebuild();
        }

        void PickRim(int i)
        {
            if (i == Tuning.Rim(car)) return;
            if (!Tuning.SetRim(car, i)) { flow.Toast("الجنوط بـ " + UIKit.Dig(Tuning.RimPrice) + " .. ما يكفي الكاش"); return; }
            Sfx.Play("ui_click");
            garage.Refresh();
            Rebuild();
        }

        // ------------------------------------------------------------------ dyno
        RectTransform chart, cursor;
        Text hpTxt, nmTxt, rpmTxt;

        void BuildDyno(RectTransform tr)
        {
            var spec = Tuning.Effective(CarCatalog.Get(car));
            Tuning.Dyno(spec, out float hp, out float nm);
            var stock = CarCatalog.Get(car);
            Tuning.Dyno(stock, out float hp0, out float nm0);

            // readouts
            hpTxt = Readout(tr, new Vector2(-30f, -100f), "قوة (حصان)", "0");
            nmTxt = Readout(tr, new Vector2(-280f, -100f), "عزم (نيوتن متر)", "0");
            rpmTxt = Readout(tr, new Vector2(-530f, -100f), "دورات", "0");

            // chart
            var frame = UIKit.Panel(tr, "Chart", new Color(0f, 0f, 0f, 0.35f), "hud/panel_sharp");
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(700f, 250f));
            chart = frame.rectTransform;
            for (int g = 1; g < 4; g++)
            {
                var gl = UIKit.Img(chart, "G" + g, null, new Color(1, 1, 1, 0.07f));
                UIKit.Place(gl.rectTransform, new Vector2(0f, 0f), new Vector2(0f, g * 62f), new Vector2(700f, 2f));
            }
            float maxNm = Tuning.TorqueAt(spec, 0.6f) * 1.6f + 1f;
            float maxHp = hp * 1.25f + 1f;
            Curve(chart, x => Tuning.TorqueAt(stock, x) / maxNm, new Color(1, 1, 1, 0.18f), 3f);                         // stock, faint
            Curve(chart, x => Tuning.TorqueAt(spec, x) / maxNm, UIKit.Gold, 5f);                                          // torque
            Curve(chart, x => Tuning.TorqueAt(spec, x) * x * spec.maxRpm / 7121f / maxHp, new Color(0.95f, 0.35f, 0.25f), 5f);   // power
            cursor = UIKit.Img(chart, "Cursor", null, new Color(1, 1, 1, 0.5f)).rectTransform;
            UIKit.Place(cursor, new Vector2(0f, 0f), Vector2.zero, new Vector2(2f, 250f));
            var leg = UIKit.Label(tr, "Leg", "الأصفر: العزم   ·   الأحمر: القوة   ·   الباهت: الوكالة", 34, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter, UIKit.Arabic);
            UIKit.Place(leg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(730f, 46f));

            string gain = hp > hp0 + 0.5f ? "+" + UIKit.Dig((long)Mathf.Round(hp - hp0)) + " حصان على الوكالة" : "مكينة وكالة";
            var g2 = UIKit.Label(tr, "Gain", gain, 40, hp > hp0 + 0.5f ? UIKit.Gold : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(g2.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 34f), new Vector2(420f, 70f));
            var run = UIKit.Button(tr, "Run", UIKit.Gold, () => { if (dynoRun == null) dynoRun = StartCoroutine(Run(spec, hp, nm)); }, "hud/panel_sharp");
            UIKit.Place((RectTransform)run.transform, new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(290f, 86f));
            var rt = UIKit.Label(run.transform, "T", "شغّل الداينو", 40, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(rt.rectTransform);
            hpTxt.text = UIKit.Dig((long)Mathf.Round(hp)); nmTxt.text = UIKit.Dig((long)Mathf.Round(nm)); rpmTxt.text = UIKit.Dig((long)spec.maxRpm);
        }

        Text Readout(RectTransform tr, Vector2 pos, string label, string v)
        {
            var l = UIKit.Label(tr, "RL" + label, label, 36, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(l.rectTransform, new Vector2(1f, 1f), pos, new Vector2(240f, 44f));
            var t = UIKit.Label(tr, "RV" + label, v, 50, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Digital, false);
            UIKit.Place(t.rectTransform, new Vector2(1f, 1f), pos + new Vector2(0f, -42f), new Vector2(240f, 62f));
            return t;
        }

        static void Curve(RectTransform chart, System.Func<float, float> f, Color c, float thick)
        {
            const int N = 48; float W = 700f, H = 250f;
            Vector2 prev = new Vector2(0f, Mathf.Clamp01(f(0.05f)) * H);
            for (int i = 1; i <= N; i++)
            {
                float x = 0.05f + 0.95f * i / N;
                var p = new Vector2((x - 0.05f) / 0.95f * W, Mathf.Clamp01(f(x)) * H);
                var seg = UIKit.Img(chart, "Seg", null, c).rectTransform;
                seg.anchorMin = seg.anchorMax = Vector2.zero; seg.pivot = new Vector2(0f, 0.5f);
                seg.anchoredPosition = prev;
                Vector2 d = p - prev;
                seg.sizeDelta = new Vector2(d.magnitude + 1f, thick);
                seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                prev = p;
            }
        }

        IEnumerator Run(CarSpec spec, float hp, float nm)
        {
            Sfx.Play("shaheen_start");
            yield return new WaitForSecondsRealtime(0.9f);
            if (rev == null)
            {
                rev = gameObject.AddComponent<AudioSource>();
                rev.clip = Resources.Load<AudioClip>("DriftKuwait/Audio/shaheen_4000");
                rev.loop = true; rev.spatialBlend = 0f; rev.volume = 0.8f * GameSettings.Effects;
            }
            if (rev.clip != null) rev.Play();
            float T = 3.4f;
            for (float t = 0f; t < T; t += Time.unscaledDeltaTime)
            {
                float x = t / T;
                cursor.anchoredPosition = new Vector2(x * 560f, 0f);
                float xx = 0.05f + 0.95f * x;
                float tq = Tuning.TorqueAt(spec, xx);
                float rpm = xx * spec.maxRpm;
                rpmTxt.text = UIKit.Dig((long)(Mathf.Round(rpm / 50f) * 50f));
                nmTxt.text = UIKit.Dig((long)Mathf.Round(tq));
                hpTxt.text = UIKit.Dig((long)Mathf.Round(tq * rpm / 7121f));
                if (rev != null) rev.pitch = Mathf.Lerp(0.55f, 1.55f, x);
                garage.SpinWheels(Mathf.Lerp(4f, 60f, x) * Time.unscaledDeltaTime);
                yield return null;
            }
            if (rev != null) rev.Stop();
            Sfx.Play(spec.turbo ? "shaheen_blowoff" : "shaheen_backfire");
            hpTxt.text = UIKit.Dig((long)Mathf.Round(hp)); nmTxt.text = UIKit.Dig((long)Mathf.Round(nm)); rpmTxt.text = UIKit.Dig((long)spec.maxRpm);
            cursor.anchoredPosition = new Vector2(560f, 0f);
            dynoRun = null;
        }
    }
}
