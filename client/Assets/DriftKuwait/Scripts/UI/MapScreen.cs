using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// خريطة: two levels. First a painted semi top-down map of Kuwait (the real coastline, Kuwait Towers, the desert
    /// camp with tents and camels, the districts as big plaques: Salwa open, the rest locked). Tap Salwa and the map
    /// zooms into a painted map of our actual Salwa streets and roundabouts with a pin per event. A side card shows
    /// the selected district or event with big text and a start button.
    public class MapScreen : MonoBehaviour
    {
        GameFlow flow;
        Text cardTitle, cardGoal, cardHow, cardBest, cardLock, cardStartTxt;
        Image[] cardStars = new Image[3];
        Button startBtn;
        Career.EventDef selected;
        RectTransform kuwaitLayer, salwaLayer;
        CanvasGroup kuwaitGroup, salwaGroup;
        bool inSalwa;
        Coroutine zoom;
        /// Open straight on the Salwa streets (after the first visit in a session, and for the preview shot).
        public static bool OpenSalwa;
        // Where Salwa sits on the Kuwait painting (0..1, from the bottom left)
        static readonly Vector2 SalwaOnKuwait = new Vector2(0.655f, 0.39f);
        readonly System.Collections.Generic.List<(Career.EventDef ev, Image ring)> pins = new System.Collections.Generic.List<(Career.EventDef, Image)>();

        const float MapW = 1290f, MapH = 860f;   // 3:2, the shape of the paintings

        public static MapScreen Build(GameFlow flow)
        {
            var c = UIKit.MakeCanvas("DK_Map", 10);
            var m = c.gameObject.AddComponent<MapScreen>();
            m.flow = flow;
            m.BuildUI(c.transform);
            return m;
        }

        void BuildUI(Transform canvas)
        {
            var bg = UIKit.Img(canvas, "Bg", null, new Color(0.05f, 0.045f, 0.04f, 1f));
            UIKit.Stretch(bg.rectTransform);
            var safe = UIKit.SafeArea(canvas);

            var title = UIKit.Label(safe, "Title", "الخريطة", 64, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(title.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -8f), new Vector2(360f, 84f));
            var stars = UIKit.Label(safe, "Stars", "نجومك: " + UIKit.Num(Career.TotalStars), 34, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(stars.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -18f), new Vector2(320f, 70f));   // beside the back button, clear of the card
            stars.alignment = TextAnchor.MiddleLeft;
            stars.fontSize = 44;

            // District tabs (RTL): Salwa open, the rest locked for now
            (string ar, bool open)[] tabs = { ("سلوى", true), ("الرميثية", false), ("أم نقا", false), ("كبد", false) };
            for (int i = 0; i < tabs.Length; i++)
            {
                var (ar, open) = tabs[i];
                var b = UIKit.Button(safe, "Tab" + i, open ? new Color(1f, 0.7f, 0.18f, 0.95f) : UIKit.InkSoft,
                    open ? (System.Action)(() => EnterSalwa(false)) : () => flow.Toast(ar + " .. قريباً"), "hud/panel_sharp");
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-440f - i * 250f, -34f), new Vector2(234f, 76f));
                var t = UIKit.Label(b.transform, "T", ar, 38, open ? new Color(0.12f, 0.08f, 0.03f) : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Stretch(t.rectTransform, open ? 0f : 56f, 10f, 0f, 0f);
                if (!open)
                {
                    var lk = UIKit.Img(b.transform, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.6f));
                    UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
                }
            }

            // Map (left): the painted Kuwait map, and the painted Salwa streets on top of it (shown when zoomed in)
            var frame = UIKit.Panel(safe, "MapFrame", new Color(0, 0, 0, 0.6f), "hud/panel_sharp");
            UIKit.Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(MapW + 16f, MapH + 16f));
            var clip = UIKit.Rect(frame.transform, "Clip");
            UIKit.Stretch(clip, 8, 8, 8, 8);
            clip.gameObject.AddComponent<RectMask2D>();
            kuwaitLayer = Layer(clip, "Kuwait", "DriftKuwait/Map/map_kuwait", out kuwaitGroup);
            salwaLayer = Layer(clip, "Salwa", "DriftKuwait/Map/map_salwa", out salwaGroup);
            BuildKuwait();
            var vign = UIKit.Img(frame.transform, "Vignette", UIKit.S("hud/grad_v"), new Color(0, 0, 0, 0.35f));
            UIKit.Stretch(vign.rectTransform, 8, 8, 8, 8);
            UIKit.Outline(frame.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");

            foreach (var ev in Career.Events)
                if (ev.district == "salwa") Pin(salwaLayer, ev);

            // Side card (right)
            var card = UIKit.Panel(safe, "Card", new Color(0.035f, 0.06f, 0.11f, 0.94f), "hud/panel_sharp");
            UIKit.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(600f, MapH + 16f));
            UIKit.Outline(card.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");
            var ct = card.transform;
            UIKit.Stripe(ct, new Vector2(1f, 1f), new Vector2(-48f, -20f), 300f, 6f, true);
            cardTitle = UIKit.Label(ct, "Title", "", 58, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(cardTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(540f, 80f));
            cardGoal = UIKit.Label(ct, "Goal", "", 40, UIKit.Gold, TextAnchor.UpperRight, UIKit.Black);
            UIKit.Place(cardGoal.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -108f), new Vector2(540f, 130f));
            cardHow = UIKit.Label(ct, "How", "", 37, new Color(1, 1, 1, 0.85f), TextAnchor.UpperRight, UIKit.Arabic);
            UIKit.Place(cardHow.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -262f), new Vector2(540f, 200f));   // below a two-line goal (40 px lines need ~60 px each)
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Img(ct, "Star" + i, UIKit.S("hud/ic_star_big"), Color.white);
                UIKit.Place(s.rectTransform, new Vector2(1f, 1f), new Vector2(-80f - i * 100f, -478f), new Vector2(84f, 84f));
                cardStars[i] = s;
            }
            cardBest = UIKit.Label(ct, "Best", "", 42, Color.white, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(cardBest.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -560f), new Vector2(540f, 60f));
            cardLock = UIKit.Label(ct, "Lock", "", 40, new Color(1f, 0.5f, 0.4f), TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(cardLock.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -620f), new Vector2(540f, 60f));
            startBtn = UIKit.Button(ct, "Start", UIKit.Gold, () => { if (!inSalwa) EnterSalwa(false); else if (selected != null && Career.Unlocked(selected)) flow.StartEvent(selected); }, "hud/panel_sharp");
            UIKit.Place((RectTransform)startBtn.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 110f));
            cardStartTxt = UIKit.Label(startBtn.transform, "T", "ابدأ", 52, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(cardStartTxt.rectTransform);

            var back = UIKit.Button(safe, "Back", UIKit.Ink, () => { if (inSalwa) ShowKuwait(false); else flow.ShowTitle(); }, "hud/panel_sharp");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(40f, -34f), new Vector2(200f, 76f));
            UIKit.Outline(back.transform, new Color(1, 1, 1, 0.25f), "hud/panel_sharp_outline");
            var bt = UIKit.Label(back.transform, "T", "رجوع", 36, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(bt.rectTransform);

            // Select the first unlocked event without three stars
            Career.EventDef pick = Career.Events[0];
            foreach (var ev in Career.Events) if (Career.Unlocked(ev) && ev.stars != null && Career.Stars(ev.id) < 3) { pick = ev; break; }
            pickDefault = pick;
            if (OpenSalwa) EnterSalwa(true); else ShowKuwait(true);
        }

        Career.EventDef pickDefault;

        static RectTransform Layer(RectTransform parent, string name, string res, out CanvasGroup group)
        {
            var r = UIKit.Rect(parent, name);
            UIKit.Stretch(r);
            group = r.gameObject.AddComponent<CanvasGroup>();
            var raw = r.gameObject.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>(res);
            raw.color = raw.texture != null ? Color.white : new Color(0.75f, 0.6f, 0.42f);
            return r;
        }

        // ------------------------------------------------------------------ Kuwait overview
        void BuildKuwait()
        {
            // decorative names (no button)
            Plaque("الخليج العربي", new Vector2(0.86f, 0.30f), 40, new Color(0.85f, 0.95f, 1f, 0.9f), false, null, true);
            Plaque("أبراج الكويت", new Vector2(0.535f, 0.66f), 36, UIKit.Cream, false, null, false);
            Plaque("مدينة الكويت", new Vector2(0.52f, 0.53f), 38, UIKit.Cream, false, () => flow.Toast("مدينة الكويت .. قريباً"), false);
            // districts
            Plaque("سلوى", SalwaOnKuwait, 60, UIKit.Gold, true, () => { if (inSalwa) return; SelectDistrict(); EnterSalwa(false); }, false);
            Plaque("الرميثية", new Vector2(0.735f, 0.50f), 40, UIKit.Cream, false, () => flow.Toast("الرميثية .. قريباً"), false, true);
            Plaque("كبد", new Vector2(0.33f, 0.40f), 44, UIKit.Cream, false, () => flow.Toast("كبد .. قريباً"), false, true);
            Plaque("أم نقا", new Vector2(0.40f, 0.86f), 44, UIKit.Cream, false, () => flow.Toast("أم نقا .. قريباً"), false, true);
            // the desert camp: the desert race starts in Salwa and ends here
            Career.EventDef run = null;
            foreach (var ev in Career.Events) if (ev.kind == Career.Kind.Run) run = ev;
            Plaque("مخيم البر", new Vector2(0.60f, 0.20f), 44, UIKit.Cream, false, run == null ? (System.Action)null : () => { EnterSalwa(false); Select(run); }, false);
        }

        void Plaque(string ar, Vector2 at, int size, Color col, bool hero, System.Action tap, bool bare, bool locked = false)
        {
            var root = UIKit.Rect(kuwaitLayer, "P_" + ar);
            root.anchorMin = root.anchorMax = at; root.pivot = new Vector2(0.5f, 0.5f); root.anchoredPosition = Vector2.zero;
            float w = 60f + ar.Length * size * 0.52f + (locked ? 50f : 0f), h = size * 1.6f;
            root.sizeDelta = new Vector2(w, h);
            if (hero)
            {
                var glow = UIKit.Img(root, "Glow", UIKit.S("hud/circle_mask"), new Color(1f, 0.8f, 0.25f, 0.35f));
                UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w + 70f, w + 70f));
                glow.gameObject.AddComponent<Pulse>();
            }
            Graphic bg;
            if (tap != null) { var b = UIKit.Button(root, "Btn", bare ? new Color(0, 0, 0, 0) : new Color(0.035f, 0.06f, 0.11f, hero ? 0.92f : 0.8f), () => { Sfx.Play("ui_click"); tap(); }, "hud/pill"); bg = b.GetComponent<Image>(); }
            else bg = UIKit.Img(root, "Bg", UIKit.S("hud/pill"), bare ? new Color(0, 0, 0, 0) : new Color(0.035f, 0.06f, 0.11f, 0.72f));
            UIKit.Stretch(bg.rectTransform);
            if (hero) UIKit.Outline(bg.transform, UIKit.Gold);
            var t = UIKit.Label(bg.transform, "T", ar, size, col, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(t.rectTransform, locked ? 50f : 0f, 0f, 0f, 0f);
            UIKit.AddShadow(t, 3f, 0.8f);
            if (locked)
            {
                var lk = UIKit.Img(bg.transform, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.75f));
                UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(34f, 34f));
            }
        }

        void SelectDistrict()
        {
            selected = null;
            int have = 0, total = 0, open = 0;
            foreach (var ev in Career.Events)
            {
                if (ev.district != "salwa") continue;
                if (ev.stars != null) { total += 3; have += Career.Stars(ev.id); }
                if (Career.Unlocked(ev)) open++;
            }
            cardTitle.text = UIKit.Ar("سلوى");
            cardGoal.text = UIKit.ArWrap("نجومك هني: " + UIKit.Num(have) + " من " + UIKit.Num(total), 22);
            cardHow.text = UIKit.ArWrap("شوارع سلوى ودوّاراتها .. منها يبدأ كل شي. دوس على سلوى بالخريطة عشان تشوف التحديات.", 26);
            for (int i = 0; i < 3; i++) cardStars[i].gameObject.SetActive(false);
            cardBest.text = UIKit.Ar("تحديات مفتوحة: " + UIKit.Num(open));
            cardLock.text = "";
            startBtn.interactable = true;
            cardStartTxt.text = UIKit.Ar("ادخل سلوى");
        }

        void ShowKuwait(bool instant)
        {
            inSalwa = false;
            SelectDistrict();
            if (zoom != null) StopCoroutine(zoom);
            zoom = StartCoroutine(Zoom(false, instant));
        }

        void EnterSalwa(bool instant)
        {
            if (inSalwa && !instant) return;
            inSalwa = true; OpenSalwa = true;
            Select(selected ?? pickDefault);
            if (zoom != null) StopCoroutine(zoom);
            zoom = StartCoroutine(Zoom(true, instant));
        }

        /// Dive into Salwa: the Kuwait map scales up around Salwa while the street map fades in (and the reverse).
        System.Collections.IEnumerator Zoom(bool into, bool instant)
        {
            kuwaitLayer.pivot = SalwaOnKuwait;
            kuwaitLayer.anchoredPosition = Vector2.zero;
            float from = salwaGroup.alpha, to = into ? 1f : 0f;
            salwaGroup.blocksRaycasts = into; salwaGroup.interactable = into;
            kuwaitGroup.blocksRaycasts = !into; kuwaitGroup.interactable = !into;
            float dur = instant ? 0f : 0.55f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                float a = Mathf.Lerp(from, to, k);
                salwaGroup.alpha = a;
                kuwaitLayer.localScale = Vector3.one * Mathf.Lerp(1f, 4f, a);
                yield return null;
            }
            salwaGroup.alpha = to;
            kuwaitLayer.localScale = Vector3.one * (into ? 4f : 1f);
            kuwaitGroup.alpha = into ? 0f : 1f;
            if (!into) kuwaitGroup.alpha = 1f;
            zoom = null;
        }

        /// World position in Salwa -> 0..1 on the painted street map (same framing the painting was made from).
        static Vector2 SalwaUV(Vector3 p) => new Vector2(0.5f + p.z / 880f, 0.5f - p.x / 586.67f);

        readonly System.Collections.Generic.List<Rect> tagRects = new System.Collections.Generic.List<Rect>();

        void Pin(RectTransform map, Career.EventDef ev)
        {
            Vector2 vp = SalwaUV(ev.kind == Career.Kind.Zone ? ev.zone : ev.spawn);
            bool open = Career.Unlocked(ev);
            int st = Career.Stars(ev.id);
            var root = UIKit.Rect(map, "Pin_" + ev.id);
            if (ev.kind == Career.Kind.Run) vp = new Vector2(0.05f, 0.5f);   // where the avenue leaves for the desert camp
            root.anchorMin = root.anchorMax = new Vector2(Mathf.Clamp(vp.x, 0.1f, 0.9f), Mathf.Clamp(vp.y, 0.06f, 0.8f));   // pin and tag stay inside the frame
            root.sizeDelta = new Vector2(96f, 120f);
            root.pivot = new Vector2(0.5f, 0f);          // the pin tip sits on the spot
            root.anchoredPosition = Vector2.zero;
            var ring = UIKit.Img(root, "Ring", UIKit.S("hud/circle_mask"), new Color(1f, 1f, 1f, 0f));
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(120f, 120f));
            var pin = UIKit.Button(root, "Btn", open ? (st == 3 ? UIKit.Gold : new Color(0.95f, 0.3f, 0.22f)) : new Color(0.35f, 0.35f, 0.35f), () => Select(ev), "hud/map_pin");
            var pim = pin.GetComponent<Image>(); pim.sprite = UIKit.S("hud/map_pin"); pim.type = Image.Type.Simple; pim.preserveAspect = true;
            UIKit.Stretch((RectTransform)pin.transform);
            var ic = UIKit.Img(pin.transform, "Icon", UIKit.S(open ? IconFor(ev.kind) : "hud/ic_lock"), Color.white);
            UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(44f, 44f));
            var tag = UIKit.Panel(root, "Tag", new Color(0.035f, 0.06f, 0.11f, 0.85f), "hud/pill");
            // Put the name tag on whichever side of the pin is free: below, above, then left or right.
            Vector2 tip = new Vector2(root.anchorMin.x * MapW, root.anchorMin.y * MapH);
            Vector2[] offs = { new Vector2(0f, -86f), new Vector2(0f, 128f), new Vector2(-285f, 20f), new Vector2(285f, 20f), new Vector2(0f, -190f), new Vector2(0f, 232f) };
            Vector2 chosen = offs[0];
            foreach (var o in offs)
            {
                var r = new Rect(tip.x + o.x - 145f, tip.y + o.y - 4f, 290f, 102f);
                bool inside = r.xMin > -10f && r.xMax < MapW + 10f && r.yMin > -10f && r.yMax < MapH + 10f;
                bool clash = !inside;
                foreach (var t in tagRects) if (t.Overlaps(r)) { clash = true; break; }
                if (!clash) { chosen = o; break; }
            }
            tagRects.Add(new Rect(tip.x + chosen.x - 145f, tip.y + chosen.y - 4f, 290f, 102f));
            tagRects.Add(new Rect(tip.x - 50f, tip.y, 100f, 120f));   // the pin itself
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0f), chosen, new Vector2(300f, 100f));
            var nm = UIKit.Label(tag.transform, "N", ev.titleAr, 40, open ? UIKit.Cream : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(292f, 60f));
            UIKit.Fit(nm, 28, 42);
            if (ev.stars != null)
                for (int i = 0; i < 3; i++)
                {
                    var s = UIKit.Img(tag.transform, "S" + i, UIKit.S("hud/ic_star_big"), i < st ? UIKit.Gold : new Color(1, 1, 1, 0.22f));
                    UIKit.Place(s.rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 34f, 4f), new Vector2(30f, 30f));
                }
            pins.Add((ev, ring));
        }

        static string IconFor(Career.Kind k) => k == Career.Kind.Delivery ? "hud/ic_wrench" : k == Career.Kind.Escape ? "hud/ic_flame" : k == Career.Kind.Zone ? "hud/ic_pin" : k == Career.Kind.Chain ? "hud/ic_trophy" : k == Career.Kind.FreeRoam ? "hud/ic_car" : "hud/ic_flag";

        void Select(Career.EventDef ev)
        {
            selected = ev;
            bool open = Career.Unlocked(ev);
            int st = Career.Stars(ev.id);
            cardTitle.text = UIKit.Ar(ev.titleAr);
            cardGoal.text = UIKit.ArWrap(ev.goalAr, 22);
            cardHow.text = UIKit.ArWrap(ev.howAr, 26);   // short lines: the text stays inside the card
            for (int i = 0; i < 3; i++)
            {
                cardStars[i].gameObject.SetActive(ev.stars != null);
                cardStars[i].color = i < st ? UIKit.Gold : new Color(1, 1, 1, 0.15f);
            }
            long best = Career.Best(ev.id);
            cardBest.text = ev.stars == null ? "" : UIKit.Ar("أفضل نتيجة: " + (best > 0 ? UIKit.Num(best) + (ev.kind == Career.Kind.Escape ? " ثانية" : ev.kind == Career.Kind.Delivery ? "٪" : "") : "—"));
            cardLock.text = open ? "" : UIKit.Ar(Story.Chapter < ev.chapter ? "تنفتح بالفصل الثاني" : "تحتاج " + UIKit.Num(ev.needStars) + " نجوم عشان تفتحه");
            startBtn.interactable = open;
            cardStartTxt.text = UIKit.Ar(open ? "ابدأ" : "مقفول");
            foreach (var (pev, ring) in pins) ring.color = pev == ev ? new Color(1f, 0.85f, 0.3f, 0.45f) : new Color(1, 1, 1, 0f);
            Sfx.Play("ui_click", 0.6f);
        }

        void OnDestroy()
        {
            if (zoom != null) StopCoroutine(zoom);
        }
    }

    /// Slow breathing glow (the open district on the Kuwait map).
    public class Pulse : MonoBehaviour
    {
        Graphic g; Color c0;
        void Awake() { g = GetComponent<Graphic>(); if (g != null) c0 = g.color; }
        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f);
            transform.localScale = Vector3.one * (0.92f + 0.16f * k);
            if (g != null) { var c = c0; c.a = c0.a * (0.55f + 0.45f * k); g.color = c; }
        }
    }
}
