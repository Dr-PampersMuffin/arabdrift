using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// خريطة: the district from above (rendered live from the real streets), a pin per event with its stars,
    /// district tabs along the top, and a side card with the selected event's goal, stars and a start button.
    public class MapScreen : MonoBehaviour
    {
        GameFlow flow;
        Text cardTitle, cardGoal, cardHow, cardBest, cardLock, cardStartTxt;
        Image[] cardStars = new Image[3];
        Button startBtn;
        Career.EventDef selected;
        RenderTexture rt;
        Camera mapCam;
        readonly System.Collections.Generic.List<(Career.EventDef ev, Image ring)> pins = new System.Collections.Generic.List<(Career.EventDef, Image)>();

        const float MapW = 1440f, MapH = 860f;

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
            UIKit.Place(stars.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -88f), new Vector2(360f, 44f));
            stars.fontSize = 30;

            // District tabs (RTL): Salwa open, the rest locked for now
            (string ar, bool open)[] tabs = { ("سلوى", true), ("الرميثية", false), ("أم نقا", false), ("كبد", false) };
            for (int i = 0; i < tabs.Length; i++)
            {
                var (ar, open) = tabs[i];
                var b = UIKit.Button(safe, "Tab" + i, open ? new Color(1f, 0.7f, 0.18f, 0.95f) : UIKit.InkSoft,
                    open ? (System.Action)(() => { }) : () => flow.Toast(ar + " .. قريباً"), "hud/panel_sharp");
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-440f - i * 250f, -34f), new Vector2(234f, 76f));
                var t = UIKit.Label(b.transform, "T", ar, 32, open ? new Color(0.12f, 0.08f, 0.03f) : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Stretch(t.rectTransform, open ? 0f : 56f, 10f, 0f, 0f);
                if (!open)
                {
                    var lk = UIKit.Img(b.transform, "Lock", UIKit.S("hud/ic_lock"), new Color(1, 1, 1, 0.6f));
                    UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
                }
            }

            // Map (left), rendered once from an orthographic camera over the real district
            var frame = UIKit.Panel(safe, "MapFrame", new Color(0, 0, 0, 0.6f), "hud/panel_sharp");
            UIKit.Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(MapW + 16f, MapH + 16f));
            rt = new RenderTexture(2760, 1480, 24) { antiAliasing = 4 }; rt.name = "DK_MapRT"; // { name = "DK_MapRT" };
            var img = UIKit.Cover(frame.transform, "Map", rt);
            img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = new Vector2(8f, 8f); img.rectTransform.offsetMax = new Vector2(-8f, -8f);
            img.color = new Color(0.92f, 0.9f, 0.88f, 1f);
            RenderMap();
            var vign = UIKit.Img(frame.transform, "Vignette", UIKit.S("hud/grad_v"), new Color(0, 0, 0, 0.35f));
            UIKit.Stretch(vign.rectTransform, 8, 8, 8, 8);
            UIKit.Outline(frame.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");

            foreach (var ev in Career.Events)
                if (ev.district == "salwa") Pin(img.rectTransform, ev);

            // Side card (right)
            var card = UIKit.Panel(safe, "Card", new Color(0.035f, 0.06f, 0.11f, 0.94f), "hud/panel_sharp");
            UIKit.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(600f, MapH + 16f));
            UIKit.Outline(card.transform, new Color(0.97f, 0.93f, 0.85f, 0.22f), "hud/panel_sharp_outline");
            var ct = card.transform;
            UIKit.Stripe(ct, new Vector2(1f, 1f), new Vector2(-48f, -20f), 300f, 6f, true);
            cardTitle = UIKit.Label(ct, "Title", "", 58, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(cardTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(540f, 80f));
            cardGoal = UIKit.Label(ct, "Goal", "", 34, UIKit.Gold, TextAnchor.UpperRight, UIKit.Black);
            UIKit.Place(cardGoal.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -110f), new Vector2(540f, 100f));
            cardHow = UIKit.Label(ct, "How", "", 28, new Color(1, 1, 1, 0.8f), TextAnchor.UpperRight, UIKit.Arabic);
            UIKit.Place(cardHow.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -210f), new Vector2(540f, 200f));
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Img(ct, "Star" + i, UIKit.S("hud/ic_star_big"), Color.white);
                UIKit.Place(s.rectTransform, new Vector2(1f, 1f), new Vector2(-80f - i * 100f, -440f), new Vector2(84f, 84f));
                cardStars[i] = s;
            }
            cardBest = UIKit.Label(ct, "Best", "", 30, Color.white, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(cardBest.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -520f), new Vector2(540f, 50f));
            cardLock = UIKit.Label(ct, "Lock", "", 30, new Color(1f, 0.5f, 0.4f), TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(cardLock.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -570f), new Vector2(540f, 50f));
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
        }

        void RenderMap()
        {
            var go = new GameObject("DK_MapCam");
            mapCam = go.AddComponent<Camera>();
            mapCam.orthographic = true;
            // The district runs north-south (z); on screen it runs right to left, like the reference map.
            mapCam.transform.SetPositionAndRotation(new Vector3(0f, 300f, 0f), Quaternion.Euler(90f, -90f, 0f));
            mapCam.orthographicSize = 218f;   // fill the frame with the district
            mapCam.aspect = MapW / MapH;
            mapCam.cullingMask = ~((1 << 2) | (1 << Showroom.Layer) | (1 << 31));
            mapCam.clearFlags = CameraClearFlags.SolidColor;
            mapCam.backgroundColor = new Color(0.75f, 0.6f, 0.42f);
            mapCam.farClipPlane = 600f;
            mapCam.targetTexture = rt;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.renderPostProcessing = true;
            mapCam.Render();
            mapCam.enabled = false;
        }

        void Pin(RectTransform map, Career.EventDef ev)
        {
            Vector3 vp = mapCam.WorldToViewportPoint(ev.kind == Career.Kind.Zone ? ev.zone : ev.spawn);
            bool open = Career.Unlocked(ev);
            int st = Career.Stars(ev.id);
            var root = UIKit.Rect(map, "Pin_" + ev.id);
            if (ev.kind == Career.Kind.Run) vp = new Vector3(0.07f, 0.16f, 0f);   // the desert is off the map to the south: pin it at the edge
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
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, pins.Count % 2 == 0 ? -86f : 128f), new Vector2(240f, 82f));   // alternate below / above so neighbours never collide
            var nm = UIKit.Label(tag.transform, "N", ev.titleAr, 27, open ? UIKit.Cream : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(240f, 42f));
            if (ev.stars != null)
                for (int i = 0; i < 3; i++)
                {
                    var s = UIKit.Img(tag.transform, "S" + i, UIKit.S("hud/ic_star_big"), i < st ? UIKit.Gold : new Color(1, 1, 1, 0.22f));
                    UIKit.Place(s.rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 32f, 6f), new Vector2(28f, 28f));
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
            cardGoal.text = UIKit.ArWrap(ev.goalAr, 30);
            cardHow.text = UIKit.ArWrap(ev.howAr, 40);
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
            if (mapCam != null) Destroy(mapCam.gameObject);
            if (rt != null) rt.Release();
        }
    }
}
