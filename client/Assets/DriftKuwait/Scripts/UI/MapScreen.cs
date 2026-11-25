using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// خريطة: a painted semi top-down map of Kuwait (the real coastline, Kuwait Towers, the city and Salwa on the
    /// coast, the highway to the Bedouin camp with tents and camels). Our stages are drawn on it as big numbered
    /// markers joined by a dotted route (1 → 2 → 3 …), side challenges with an icon, locked districts as plaques.
    /// It opens zoomed in on the stages and drags like a phone map; one button shows all of Kuwait. A side card
    /// shows the selected stage with big text and a start button.
    public class MapScreen : MonoBehaviour
    {
        GameFlow flow;
        Text cardTitle, cardGoal, cardHow, cardBest, cardLock, cardStartTxt;
        Image[] cardStars = new Image[3];
        Button startBtn;
        Career.EventDef selected;
        RectTransform viewport, map;
        ScrollRect scroll;
        Text zoomTxt;
        bool zoomedOut;
        Coroutine zoom;
        /// Open on the whole of Kuwait (preview shot).
        public static bool OpenZoomedOut;

        // The painting is 1536 x 1024; the map shows it at Zoom units per painting pixel.
        const float PaintW = 1536f, PaintH = 1024f, Zoom = 2.7f;   // close enough that every stage and its name have room
        static readonly Vector2 Focus = new Vector2(940f, 615f);   // the middle of our stages (painting pixels)

        /// Where each stage sits on the painting (pixels from the top left): Salwa's streets and roundabouts, the
        /// coast road, the highway and the desert camp.
        static readonly System.Collections.Generic.Dictionary<string, Vector2> Spots = new System.Collections.Generic.Dictionary<string, Vector2>
        {
            { "salwa_first", new Vector2(1010f, 655f) },      // Salwa, south block
            { "salwa_dallah", new Vector2(967f, 527f) },      // the big roundabout
            { "salwa_desert_run", new Vector2(875f, 772f) },  // the desert camp
            { "salwa_chain", new Vector2(1078f, 596f) },      // the coast road
            { "salwa_free", new Vector2(935f, 600f) },        // free roam: the lower roundabout
            { "salwa_block5", new Vector2(845f, 520f) },      // block 5, west side
            { "salwa_escape", new Vector2(800f, 700f) },      // the highway
            { "salwa_job_taxi", new Vector2(1040f, 470f) },
            { "salwa_job_pickup", new Vector2(790f, 590f) },
            { "salwa_job_sedan", new Vector2(1085f, 535f) },
        };

        static Vector2 UV(Vector2 px) => new Vector2(px.x / PaintW, 1f - px.y / PaintH);
        static Vector2 Local(Vector2 px) => new Vector2((px.x - PaintW * 0.5f) * Zoom, (PaintH * 0.5f - px.y) * Zoom);
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
                    open ? (System.Action)(() => { if (zoomedOut) ToggleZoom(); }) : () => flow.Toast(ar + " .. قريباً"), "hud/panel_sharp");
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-440f - i * 250f, -34f), new Vector2(234f, 76f));
                var t = UIKit.Label(b.transform, "T", ar, 38, open ? new Color(0.12f, 0.08f, 0.03f) : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Stretch(t.rectTransform, open ? 0f : 56f, 10f, 0f, 0f);
                if (!open)
                {
                    var lk = UIKit.Img(b.transform, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.6f));
                    UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
                }
            }

            // Map (left): the painted Kuwait map, zoomed in on our stages and draggable like a phone map
            var frame = UIKit.Panel(safe, "MapFrame", new Color(0, 0, 0, 0.6f), "hud/panel_sharp");
            UIKit.Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(MapW + 16f, MapH + 16f));
            viewport = UIKit.Rect(frame.transform, "Viewport");
            UIKit.Stretch(viewport, 8, 8, 8, 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0.01f);   // catches drags
            map = UIKit.Rect(viewport, "Kuwait");
            map.anchorMin = map.anchorMax = new Vector2(0.5f, 0.5f); map.pivot = new Vector2(0.5f, 0.5f);
            map.sizeDelta = new Vector2(PaintW * Zoom, PaintH * Zoom);
            var raw = map.gameObject.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("DriftKuwait/Map/map_kuwait");
            raw.color = raw.texture != null ? Color.white : new Color(0.75f, 0.6f, 0.42f);
            scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = map;
            scroll.horizontal = scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = 0.12f; scroll.scrollSensitivity = 0f;
            BuildPlaces();
            BuildRoute();
            // reserve every marker's spot first, so no name tag ever lands on a marker
            foreach (var ev in Career.Events)
                if (ev.district == "salwa" && (ev.chapter <= 1 || Story.Chapter >= ev.chapter) && Spots.TryGetValue(ev.id, out var sp0))
                { var c0 = Local(sp0); tagRects.Add(new Rect(c0.x - 62f, c0.y - 62f, 124f, 124f)); }
            foreach (var ev in Career.Events)
                if (ev.district == "salwa" && (ev.chapter <= 1 || Story.Chapter >= ev.chapter)) Pin(map, ev);
            var vign = UIKit.Img(frame.transform, "Vignette", UIKit.S("hud/grad_v"), new Color(0, 0, 0, 0.3f));
            UIKit.Stretch(vign.rectTransform, 8, 8, 8, 8);
            vign.raycastTarget = false;
            UIKit.Outline(frame.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");

            // zoom button: all of Kuwait <-> our stages
            var zb = UIKit.Button(frame.transform, "ZoomBtn", new Color(0.035f, 0.06f, 0.11f, 0.9f), ToggleZoom, "hud/panel_sharp");
            UIKit.Place((RectTransform)zb.transform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(330f, 86f));
            UIKit.Outline(zb.transform, UIKit.Gold, "hud/panel_sharp_outline");
            zoomTxt = UIKit.Label(zb.transform, "T", "شوف الكويت كاملة", 38, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(zoomTxt.rectTransform, 12f, 12f, 0f, 0f);
            UIKit.Fit(zoomTxt, 28, 38);

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
            startBtn = UIKit.Button(ct, "Start", UIKit.Gold, () => { if (selected != null && Career.Unlocked(selected)) flow.StartEvent(selected); }, "hud/panel_sharp");
            UIKit.Place((RectTransform)startBtn.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 110f));
            cardStartTxt = UIKit.Label(startBtn.transform, "T", "ابدأ", 52, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(cardStartTxt.rectTransform);

            var back = UIKit.Button(safe, "Back", UIKit.Ink, () => flow.ShowTitle(), "hud/panel_sharp");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(40f, -34f), new Vector2(200f, 76f));
            UIKit.Outline(back.transform, new Color(1, 1, 1, 0.25f), "hud/panel_sharp_outline");
            var bt = UIKit.Label(back.transform, "T", "رجوع", 36, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(bt.rectTransform);

            // Select the first unlocked event without three stars
            Career.EventDef pick = Career.Events[0];
            foreach (var ev in Career.Events) if (Career.Unlocked(ev) && ev.stars != null && Career.Stars(ev.id) < 3) { pick = ev; break; }
            Select(pick);
            CenterOn(Focus, true);
            if (OpenZoomedOut) ToggleZoom(true);
        }

        // ------------------------------------------------------------------ places, route and stages
        void BuildPlaces()
        {
            Plaque("الخليج العربي", new Vector2(1330f, 700f), 44, new Color(0.88f, 0.96f, 1f, 0.95f), false, null, true);
            Plaque("أبراج الكويت", new Vector2(960f, 300f), 38, UIKit.Cream, false, null, false);
            Plaque("مدينة الكويت", new Vector2(800f, 420f), 40, UIKit.Cream, false, () => flow.Toast("مدينة الكويت .. قريباً"), false, true);
            Plaque("سلوى", new Vector2(690f, 600f), 64, UIKit.Gold, true, null, false);
            Plaque("مخيم البر", new Vector2(990f, 850f), 42, UIKit.Cream, false, null, false);
            Plaque("كبد", new Vector2(500f, 560f), 48, UIKit.Cream, false, () => flow.Toast("كبد .. قريباً"), false, true);
            Plaque("أم نقا", new Vector2(620f, 175f), 48, UIKit.Cream, false, () => flow.Toast("أم نقا .. قريباً"), false, true);
            Plaque("الرميثية", new Vector2(700f, 330f), 44, UIKit.Cream, false, () => flow.Toast("الرميثية .. قريباً"), false, true);
        }

        /// Dotted route through the story stages in order, following the highway out to the desert camp.
        void BuildRoute()
        {
            var ladder = Story.Chapter >= 2 ? Story.Ch2Ladder : Story.Ch1Ladder;
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < ladder.Length; i++)
            {
                if (!Spots.TryGetValue(ladder[i], out var p)) continue;
                if (ladder[i] == "salwa_desert_run") { pts.Add(new Vector2(905f, 600f)); pts.Add(new Vector2(845f, 690f)); pts.Add(new Vector2(800f, 745f)); }   // along the highway
                pts.Add(p);
                if (ladder[i] == "salwa_desert_run" && i + 1 < ladder.Length) { pts.Add(new Vector2(800f, 745f)); pts.Add(new Vector2(845f, 690f)); pts.Add(new Vector2(905f, 600f)); }   // and back to town
            }
            var dot = UIKit.S("hud/circle_mask");
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = Local(pts[i]), b = Local(pts[i + 1]);
                float len = Vector2.Distance(a, b);
                for (float d = 0f; d < len; d += 30f)
                {
                    var im = UIKit.Img(map, "Dot", dot, new Color(1f, 0.86f, 0.3f, 0.95f));
                    im.raycastTarget = false;
                    var r = im.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.sizeDelta = new Vector2(15f, 15f); r.anchoredPosition = Vector2.Lerp(a, b, d / len);
                    UIKit.AddShadow(im, 2f, 0.7f);
                }
            }
        }

        void Plaque(string ar, Vector2 px, int size, Color col, bool hero, System.Action tap, bool bare, bool locked = false)
        {
            var root = UIKit.Rect(map, "P_" + ar);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f); root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Local(px);
            float w = 60f + ar.Length * size * 0.52f + (locked ? 50f : 0f), h = size * 1.6f;
            root.sizeDelta = new Vector2(w, h);
            Graphic bg;
            if (tap != null) { var b = UIKit.Button(root, "Btn", new Color(0.035f, 0.06f, 0.11f, 0.8f), () => { Sfx.Play("ui_click"); tap(); }, "hud/pill"); bg = b.GetComponent<Image>(); }
            else { bg = UIKit.Img(root, "Bg", UIKit.S("hud/pill"), bare ? new Color(0, 0, 0, 0) : new Color(0.035f, 0.06f, 0.11f, hero ? 0.9f : 0.72f)); bg.raycastTarget = false; }
            UIKit.Stretch(bg.rectTransform);
            if (hero) UIKit.Outline(bg.transform, UIKit.Gold);
            var t = UIKit.Label(bg.transform, "T", ar, size, col, TextAnchor.MiddleCenter, UIKit.Black);
            t.raycastTarget = false;
            UIKit.Stretch(t.rectTransform, locked ? 50f : 0f, 0f, 0f, 0f);
            UIKit.AddShadow(t, 3f, 0.8f);
            if (locked)
            {
                var lk = UIKit.Img(bg.transform, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.75f));
                lk.raycastTarget = false;
                UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(34f, 34f));
            }
        }

        void CenterOn(Vector2 px, bool instant)
        {
            Vector2 target = -Local(px) * map.localScale.x;
            Vector2 half = (map.sizeDelta * map.localScale.x - viewport.rect.size) * 0.5f;
            half = Vector2.Max(half, Vector2.zero);
            target = new Vector2(Mathf.Clamp(target.x, -half.x, half.x), Mathf.Clamp(target.y, -half.y, half.y));
            map.anchoredPosition = target;
            scroll.StopMovement();
        }

        void ToggleZoom() => ToggleZoom(false);

        /// All of Kuwait (the painting fits the frame) or back to our stages.
        void ToggleZoom(bool instant)
        {
            zoomedOut = !zoomedOut;
            zoomTxt.text = UIKit.Ar(zoomedOut ? "قرّب على المراحل" : "شوف الكويت كاملة");
            if (zoom != null) StopCoroutine(zoom);
            zoom = StartCoroutine(ZoomTo(zoomedOut ? Mathf.Min(MapW / (PaintW * Zoom), MapH / (PaintH * Zoom)) : 1f, instant));
        }

        System.Collections.IEnumerator ZoomTo(float scale, bool instant)
        {
            scroll.enabled = false;
            float s0 = map.localScale.x; Vector2 p0 = map.anchoredPosition;
            Vector2 p1 = scale < 1f ? Vector2.zero : -Local(selected != null && Spots.TryGetValue(selected.id, out var sp) ? sp : Focus);
            float dur = instant ? 0f : 0.5f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                float sc = Mathf.Lerp(s0, scale, k);
                map.localScale = Vector3.one * sc;
                map.anchoredPosition = Vector2.Lerp(p0, p1 * scale, k);
                yield return null;
            }
            map.localScale = Vector3.one * scale;
            if (scale < 1f) map.anchoredPosition = Vector2.zero;
            else { map.anchoredPosition = p1; CenterOn(selected != null && Spots.TryGetValue(selected.id, out var sp2) ? sp2 : Focus, true); }
            scroll.enabled = scale >= 1f;   // drag around when zoomed in
            zoom = null;
        }

        readonly System.Collections.Generic.List<Rect> tagRects = new System.Collections.Generic.List<Rect>();

        void Pin(RectTransform parent, Career.EventDef ev)
        {
            if (!Spots.TryGetValue(ev.id, out var px)) return;
            bool open = Career.Unlocked(ev);
            int st = Career.Stars(ev.id);
            var ladder = Story.Chapter >= 2 ? Story.Ch2Ladder : Story.Ch1Ladder;
            int step = System.Array.IndexOf(ladder, ev.id);
            var root = UIKit.Rect(parent, "Pin_" + ev.id);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(104f, 104f);
            root.pivot = new Vector2(0.5f, 0.5f);
            Vector2 at = Local(px);
            root.anchoredPosition = at;
            var ring = UIKit.Img(root, "Ring", UIKit.S("hud/circle_mask"), new Color(1f, 1f, 1f, 0f));
            ring.raycastTarget = false;
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            Color body = !open ? new Color(0.36f, 0.36f, 0.38f) : st == 3 ? UIKit.Gold : step >= 0 ? new Color(0.92f, 0.28f, 0.2f) : new Color(0.16f, 0.45f, 0.75f);
            var rim = UIKit.Img(root, "Rim", UIKit.S("hud/circle_mask"), new Color(1f, 0.97f, 0.88f, 0.95f));   // cream edge
            rim.raycastTarget = false;
            UIKit.Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f));
            var pin = UIKit.Button(root, "Btn", body, () => Select(ev), "hud/circle_mask");
            var pim = pin.GetComponent<Image>(); pim.sprite = UIKit.S("hud/circle_mask"); pim.type = Image.Type.Simple;
            UIKit.Stretch((RectTransform)pin.transform);
            if (step >= 0 && open)
            {
                var num = UIKit.Label(pin.transform, "Num", UIKit.Num(step + 1 + (Story.Chapter >= 2 ? Story.Ch1Ladder.Length : 0)), 54, st == 3 ? new Color(0.12f, 0.08f, 0.03f) : Color.white, TextAnchor.MiddleCenter, UIKit.Display);
                UIKit.Stretch(num.rectTransform);
                num.raycastTarget = false;
            }
            else
            {
                var ic = UIKit.Img(pin.transform, "Icon", UIKit.S(open ? IconFor(ev.kind) : "hud/ic_lock"), Color.white);
                ic.raycastTarget = false;
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            }
            // name tag on whichever side is free: below, above, left, right
            var tag = UIKit.Panel(root, "Tag", new Color(0.035f, 0.06f, 0.11f, 0.88f), "hud/pill");
            tag.raycastTarget = false;
            Vector2[] offs = { new Vector2(0f, -112f), new Vector2(0f, 112f), new Vector2(-218f, 0f), new Vector2(218f, 0f), new Vector2(-190f, -100f), new Vector2(190f, -100f), new Vector2(-190f, 100f), new Vector2(190f, 100f), new Vector2(0f, -200f), new Vector2(0f, 200f) };
            Vector2 chosen = offs[0];
            foreach (var o in offs)
            {
                var r = new Rect(at.x + o.x - 150f, at.y + o.y - 46f, 300f, 92f);
                bool clash = false;
                foreach (var t in tagRects) if (t.Overlaps(r)) { clash = true; break; }
                if (!clash) { chosen = o; break; }
            }
            tagRects.Add(new Rect(at.x + chosen.x - 150f, at.y + chosen.y - 46f, 300f, 92f));
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), chosen, new Vector2(300f, ev.stars != null ? 96f : 70f));
            var nm = UIKit.Label(tag.transform, "N", ev.titleAr, 40, open ? UIKit.Cream : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(284f, 60f));
            UIKit.Fit(nm, 28, 40);
            nm.raycastTarget = false;
            if (ev.stars != null)
                for (int i = 0; i < 3; i++)
                {
                    var s = UIKit.Img(tag.transform, "S" + i, UIKit.S("hud/ic_star_big"), i < st ? UIKit.Gold : new Color(1, 1, 1, 0.25f));
                    s.raycastTarget = false;
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
