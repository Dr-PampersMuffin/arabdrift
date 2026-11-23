using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Garage (reference: the Abu Rashid workshop mock-up): the workshop photo behind, the selected car in real 3D on a
    /// slow turntable (drag to spin), tabs along the top, the stats panel on the right, the five cars along the bottom.
    /// Cars without a 3D body yet show their studio art and "قريباً".
    public class GarageScreen : MonoBehaviour
    {
        GameFlow flow;
        Showroom showroom;
        RawImage carView;
        Image carArt;
        Text nameTxt, tagTxt, cylTxt, driveTxt;
        Image[] bars = new Image[5];
        Text[] vals = new Text[5];
        Image[] tileOutlines;
        RectTransform[] tileRects;
        CanvasGroup[] tileGroups;
        Button driveBtn;
        string selected;
        RectTransform safeRoot;
        Text cashTxt;
        WorkshopPanel workshop;

        public static GarageScreen Build(GameFlow flow)
        {
            var canvas = UIKit.MakeCanvas("DK_Garage", 10);
            var g = canvas.gameObject.AddComponent<GarageScreen>();
            g.flow = flow;
            var root = canvas.transform;
            UIKit.Cover(root, "Workshop", Resources.Load<Texture2D>("DriftKuwait/UI/garage_bg"));
            // The workshop's own sign, painted on the board on the back wall
            var sign = UIKit.Label(root, "Sign", "كراج بو نبيل", 84, new Color(0.46f, 0.11f, 0.08f, 0.92f), TextAnchor.MiddleCenter, UIKit.Display);
            UIKit.Place(sign.rectTransform, new Vector2(0.6f, 1f), new Vector2(0f, -196f), new Vector2(640f, 110f));
            var sign2 = UIKit.Label(root, "Sign2", "لصيانة السيارات", 34, new Color(0.30f, 0.18f, 0.12f, 0.85f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(sign2.rectTransform, new Vector2(0.6f, 1f), new Vector2(0f, -272f), new Vector2(640f, 50f));
            var shadeB = UIKit.Img(root, "ShadeB", UIKit.S("hud/grad_v"), new Color(1, 1, 1, 0.85f));
            shadeB.rectTransform.anchorMin = Vector2.zero; shadeB.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            shadeB.rectTransform.offsetMin = shadeB.rectTransform.offsetMax = Vector2.zero;
            shadeB.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var shadeT = UIKit.Img(root, "ShadeT", UIKit.S("hud/grad_v"), new Color(1, 1, 1, 0.6f));
            shadeT.rectTransform.anchorMin = new Vector2(0f, 0.8f); shadeT.rectTransform.anchorMax = Vector2.one;
            shadeT.rectTransform.offsetMin = shadeT.rectTransform.offsetMax = Vector2.zero;

            // 3D car view (render texture from the showroom), with drag-to-spin
            g.showroom = Showroom.Create();
            g.carView = UIKit.Cover(root, "CarView", g.showroom.rt);
            g.carView.raycastTarget = true;
            g.carView.gameObject.AddComponent<DragSpin>().showroom = g.showroom;
            g.carArt = UIKit.Img(root, "CarArt", null, Color.white);
            g.carArt.preserveAspect = true;
            UIKit.Place(g.carArt.rectTransform, new Vector2(0.42f, 0.5f), new Vector2(0f, -20f), new Vector2(1250f, 600f));

            var safe = UIKit.SafeArea(root);
            g.safeRoot = safe;
            g.BuildTop(safe);
            g.BuildStats(safe);
            g.BuildTiles(safe);
            g.BuildBottom(safe);

            g.Select(CarCatalog.SelectedId, false);
            if (!PreviewHooks.Active) Sfx.Voice("ab_garage", 0.9f);
            return g;
        }

        void BuildTop(RectTransform safe)
        {
            var logo = UIKit.Img(safe, "Logo", UIKit.S("logo"), Color.white);
            logo.preserveAspect = true;
            UIKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(250f, 116f));

            var tabs = UIKit.Rect(safe, "Tabs");
            UIKit.Place(tabs, new Vector2(1f, 1f), new Vector2(-640f, -20f), new Vector2(1100f, 128f));
            (string ar, string icon)[] items = { ("الكراج", "car"), ("السيارات", "car"), ("التعديلات", "wrench"), ("المظهر", "spray"),
                                                 ("الخرائط", "map"), ("الفعاليات", "flag"), ("الطاقم", "people") };
            float w = 150f;
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                bool active = i == 0;
                var b = UIKit.Button(tabs, "Tab" + i, active ? new Color(0.1f, 0.08f, 0.05f, 0.92f) : new Color(0, 0, 0, 0),
                    () => { if (idx == 2) OpenWorkshop(WorkshopPanel.Mode.Parts); else if (idx == 3) OpenWorkshop(WorkshopPanel.Mode.Look);
                            else if (idx == 4) flow.ShowMap(); else if (idx > 4) flow.Toast("قريباً"); else CloseWorkshop(); }, "hud/panel_sharp");
                var rt = (RectTransform)b.transform;
                // RTL: first tab on the right
                UIKit.Place(rt, new Vector2(1f, 0.5f), new Vector2(-i * (w + 6f), 0f), new Vector2(w, 124f));
                if (active) UIKit.Outline(rt, UIKit.Gold, "hud/panel_sharp_outline");
                var ic = UIKit.Img(rt, "Ic", UIKit.S("hud/ic_" + items[i].icon), active ? UIKit.Gold : UIKit.Cream);
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(48f, 48f));
                var t = UIKit.Label(rt, "T", items[i].ar, 30, active ? UIKit.Gold : UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(w, 44f));
            }

            var bar = UIKit.Rect(safe, "TopRight");
            UIKit.Place(bar, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(560f, 70f));
            float x = 0f;
            var set = UIKit.Button(bar, "Settings", UIKit.InkSoft, () => PauseMenu.Open(flow, false), "hud/pill");
            UIKit.Place((RectTransform)set.transform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(70f, 64f));
            var gi = UIKit.Img(set.transform, "G", UIKit.S("hud/ic_gear"), UIKit.Cream);
            UIKit.Place(gi.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
            x -= 86f;
            x = TitleScreen.Chip(bar, x, "hud/ic_rep", UIKit.Dig(Wallet.Rep), 190f);
            TitleScreen.Chip(bar, x, "hud/ic_coin", UIKit.Dig(Wallet.Cash), 250f);
            cashTxt = bar.GetChild(bar.childCount - 1).Find("V").GetComponent<Text>();
        }

        void BuildStats(RectTransform safe)
        {
            var p = UIKit.Panel(safe, "Stats", new Color(0.035f, 0.06f, 0.11f, 0.88f));
            UIKit.Place(p.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, -160f), new Vector2(600f, 500f));   // clear of the car tiles below
            UIKit.Outline(p.transform, new Color(1f, 1f, 1f, 0.12f));
            var tr = p.transform;
            UIKit.Stripe(tr, new Vector2(1f, 1f), new Vector2(-30f, -14f), 220f, 5f, true);
            nameTxt = UIKit.Label(tr, "Name", "", 72, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(nameTxt.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, -18f), new Vector2(540f, 90f));
            tagTxt = UIKit.Label(tr, "Tag", "", 30, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(tagTxt.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, -110f), new Vector2(360f, 44f));
            var chip = UIKit.Panel(tr, "Cyl", new Color(1, 1, 1, 0.08f), "hud/panel_sharp");
            UIKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(34f, -110f), new Vector2(170f, 46f));
            UIKit.Outline(chip.transform, new Color(1, 1, 1, 0.3f), "hud/panel_sharp_outline");
            cylTxt = UIKit.Label(chip.transform, "T", "", 24, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Arabic);
            UIKit.Stretch(cylTxt.rectTransform);
            var sep = UIKit.Img(tr, "Sep", null, new Color(1, 1, 1, 0.12f));
            UIKit.Place(sep.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(540f, 2f));

            string[] labels = { "السرعة القصوى", "التسارع", "التحكم", "الكبح", "التقحيص" };
            for (int i = 0; i < 5; i++)
            {
                float y = -196f - i * 50f;
                var l = UIKit.Label(tr, "L" + i, labels[i], 28, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(l.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, y), new Vector2(220f, 40f));
                vals[i] = UIKit.Label(tr, "V" + i, "", 28, UIKit.Cream, TextAnchor.MiddleLeft, UIKit.Black);
                UIKit.Place(vals[i].rectTransform, new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(70f, 40f));
                var barRoot = UIKit.Rect(tr, "B" + i);
                UIKit.Place(barRoot, new Vector2(0f, 1f), new Vector2(110f, y - 26f), new Vector2(230f, 10f));
                bars[i] = UIKit.Bar(barRoot, "Bar", 0.5f, new Color(0.8f, 0.86f, 0.92f), new Vector2(230f, 10f));
                ((RectTransform)bars[i].transform.parent).anchoredPosition = Vector2.zero;
            }

            driveBtn = UIKit.Button(tr, "Drive", UIKit.Gold, () => { if (CarCatalog.Has3D(selected)) flow.StartDrive(selected); else flow.Toast("هذي السيارة قريباً"); }, "hud/panel_sharp");
            UIKit.Place((RectTransform)driveBtn.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(540f, 96f));
            var ic = UIKit.Img(driveBtn.transform, "Ic", UIKit.S("hud/ic_flag"), new Color(0.12f, 0.08f, 0.03f));
            UIKit.Place(ic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(54f, 54f));
            driveTxt = UIKit.Label(driveBtn.transform, "T", "قيادة", 46, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(driveTxt.rectTransform);
            var ch = UIKit.Img(driveBtn.transform, "Ch", UIKit.S("hud/ic_chev_l"), new Color(0.12f, 0.08f, 0.03f));
            UIKit.Place(ch.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(44f, 44f));
        }

        void BuildTiles(RectTransform safe)
        {
            var row = UIKit.Rect(safe, "Tiles");
            UIKit.Place(row, new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(1900f, 190f));   // a low carousel: the car above stays clear
            var cars = CarCatalog.Garage;
            tileOutlines = new Image[cars.Length];
            tileRects = new RectTransform[cars.Length];
            tileGroups = new CanvasGroup[cars.Length];
            float w = 230f, gap = 34f;
            float total = cars.Length * w + (cars.Length - 1) * gap;
            for (int i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                string id = c.id;
                var b = UIKit.Button(row, "Tile_" + id, new Color(0.035f, 0.06f, 0.11f, 0.92f), () => Select(id, true), "hud/panel_sharp");
                var rt = (RectTransform)b.transform;
                // RTL order: first car on the right
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(total / 2f - w / 2f - i * (w + gap), 0f), new Vector2(w, 150f));
                tileRects[i] = rt;
                tileGroups[i] = b.gameObject.AddComponent<CanvasGroup>();
                var art = UIKit.Img(rt, "Art", UIKit.S("car_" + id), Color.white);
                art.preserveAspect = true;
                UIKit.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(w - 16f, 92f));
                var n = UIKit.Label(rt, "N", c.nameAr, 32, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(n.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(w, 46f));
                if (!CarCatalog.Has3D(id))
                {
                    var soon = UIKit.Panel(rt, "Soon", new Color(0, 0, 0, 0.7f), "hud/pill");
                    UIKit.Place(soon.rectTransform, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(84f, 30f));
                    var st = UIKit.Label(soon.transform, "T", "قريباً", 20, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black);
                    UIKit.Stretch(st.rectTransform);
                }
                tileOutlines[i] = UIKit.Outline(rt, UIKit.Gold, "hud/panel_sharp_outline");
            }
            var la = UIKit.Button(safe, "PrevCar", UIKit.InkSoft, () => Step(1), "hud/pill");
            UIKit.Place((RectTransform)la.transform, new Vector2(0f, 0f), new Vector2(18f, 180f), new Vector2(64f, 96f));
            var li = UIKit.Img(la.transform, "I", UIKit.S("hud/ic_chev_l"), UIKit.Cream);
            UIKit.Place(li.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            var ra = UIKit.Button(safe, "NextCar", UIKit.InkSoft, () => Step(-1), "hud/pill");
            UIKit.Place((RectTransform)ra.transform, new Vector2(1f, 0f), new Vector2(-18f, 180f), new Vector2(64f, 96f));
            var ri = UIKit.Img(ra.transform, "I", UIKit.S("hud/ic_chev_r"), UIKit.Cream);
            UIKit.Place(ri.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        }

        void BuildBottom(RectTransform safe)
        {
            var back = UIKit.Button(safe, "Back", UIKit.Ink, () => flow.ShowTitle(), "hud/pill");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(250f, 80f));
            var bt = UIKit.Label(back.transform, "T", "رجوع", 36, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(bt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(20f, 2f), new Vector2(160f, 60f));
            var bi = UIKit.Img(back.transform, "I", UIKit.S("hud/ic_chev_l"), UIKit.Cream);
            UIKit.Place(bi.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(40f, 40f));

            var row = UIKit.Rect(safe, "Actions");
            UIKit.Place(row, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(1250f, 80f));
            (string ar, string icon)[] acts = { ("الداينو", "gear"), ("القطع", "wrench"), ("الصبغ والجنوط", "spray"), ("وضع التصوير", "camera") };
            float x = 0f;
            for (int ai = 0; ai < acts.Length; ai++)
            {
                var a = acts[ai];
                int which = ai;
                float w = 70f + a.ar.Length * 22f;
                var b = UIKit.Button(row, a.ar, UIKit.Ink, () =>
                {
                    Sfx.Play("ui_click");
                    if (which == 0) OpenWorkshop(WorkshopPanel.Mode.Dyno);
                    else if (which == 1) OpenWorkshop(WorkshopPanel.Mode.Parts);
                    else if (which == 2) OpenWorkshop(WorkshopPanel.Mode.Look);
                    else flow.Toast("قريباً");
                }, "hud/pill");
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, 72f));
                var ic = UIKit.Img(b.transform, "I", UIKit.S("hud/ic_" + a.icon), UIKit.Cream);
                UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(38f, 38f));
                var t = UIKit.Label(b.transform, "T", a.ar, 26, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(t.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 2f), new Vector2(w - 70f, 60f));
                x -= w + 14f;
            }
        }

        void Step(int dir)
        {
            var cars = CarCatalog.Garage;
            int i = System.Array.FindIndex(cars, c => c.id == selected);
            i = (i + dir + cars.Length) % cars.Length;
            Select(cars[i].id, true);
        }

        void Select(string id, bool user)
        {
            selected = id;
            var spec = Tuning.Effective(CarCatalog.Get(id));   // the bars show the car as tuned
            bool has3D = CarCatalog.Has3D(id);
            if (has3D) CarCatalog.SelectedId = id;
            nameTxt.text = UIKit.Ar(spec.nameAr);
            tagTxt.text = UIKit.Ar(spec.taglineAr);
            cylTxt.text = UIKit.Ar(spec.cylinderAr);
            float[] v = { spec.topSpeed, spec.accel, spec.handling, spec.braking, spec.drift };
            for (int i = 0; i < 5; i++)
            {
                UIKit.SetBar(bars[i], v[i] / 10f);
                vals[i].text = UIKit.EasternNumerals ? UIKit.Num((long)v[i]) + "٫" + UIKit.Num((long)Mathf.Round((v[i] % 1f) * 10f) % 10) : v[i].ToString("0.0");
            }
            var cars = CarCatalog.Garage;
            for (int i = 0; i < cars.Length; i++) tileOutlines[i].enabled = cars[i].id == id;
            showroom.Show(has3D ? id : null);
            carView.enabled = has3D;
            carArt.enabled = !has3D;
            carArt.sprite = UIKit.S("car_" + id);
            driveTxt.text = UIKit.Ar(has3D ? "قيادة" : "قريباً");
            if (user && id == "abusitta") Sfx.Voice("ab_abusitta", 0.9f);
            if (user && workshop != null) workshop.SetCar(id);
        }

        /// Carousel: the chosen card grows and lights up, the rest shrink back and dim.
        void LateUpdate()
        {
            if (tileRects == null) return;
            var cars = CarCatalog.Garage;
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            for (int i = 0; i < tileRects.Length; i++)
            {
                bool on = cars[i].id == selected;
                tileRects[i].localScale = Vector3.Lerp(tileRects[i].localScale, Vector3.one * (on ? 1.18f : 0.9f), k);
                tileGroups[i].alpha = Mathf.Lerp(tileGroups[i].alpha, on ? 1f : 0.62f, k);
                if (on) tileRects[i].SetAsLastSibling();
            }
        }

        void OpenWorkshop(WorkshopPanel.Mode m)
        {
            workshop = WorkshopPanel.Open(this, flow, safeRoot, m, selected);
        }

        void CloseWorkshop() { if (workshop != null) Destroy(workshop.gameObject); }

        /// After a purchase: rebuild the turntable car and the bars, update the cash chip.
        public void Refresh()
        {
            string keep = selected;
            Select(keep, false);
            if (cashTxt != null) cashTxt.text = UIKit.Dig(Wallet.Cash);
        }

        public void SpinWheels(float metres) { if (showroom != null) showroom.Spin(metres); }

        void OnDestroy()
        {
            if (showroom != null) Destroy(showroom.gameObject);
        }
    }

    /// The 3D turntable: an isolated layer with warm workshop lights, a soft contact shadow, a camera rendering into a
    /// transparent render texture that the garage screen shows over the workshop photo.
    public class Showroom : MonoBehaviour
    {
        public const int Layer = 30;
        public RenderTexture rt;
        public float yaw = 205f, spin = 9f;
        Transform turntable;
        GameObject current;
        Camera cam;
        float idle;

        public static Showroom Create()
        {
            var go = new GameObject("DK_Showroom");
            go.transform.position = new Vector3(0f, -400f, 0f);
            var s = go.AddComponent<Showroom>();
            s.rt = new RenderTexture(2340, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "ShowroomRT" };
            s.turntable = new GameObject("Turntable").transform;
            s.turntable.SetParent(go.transform, false);

            var camGo = new GameObject("ShowroomCam");
            camGo.transform.SetParent(go.transform, false);
            s.cam = camGo.AddComponent<Camera>();
            s.cam.cullingMask = 1 << Layer;
            s.cam.clearFlags = CameraClearFlags.SolidColor;
            s.cam.backgroundColor = new Color(0, 0, 0, 0);
            s.cam.fieldOfView = 26f;
            s.cam.nearClipPlane = 0.3f; s.cam.farClipPlane = 60f;
            s.cam.allowHDR = false;
            s.cam.targetTexture = s.rt;
            camGo.transform.localPosition = new Vector3(0.9f, 1.55f, -11.2f);   // closer: the car fills the left of the screen, like the hero shot
            camGo.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);   // car sits low, on the workshop floor of the backdrop
            var data = s.cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data == null) data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            // Warm tungsten key lights like the workshop lamps, plus a cool rim.
            AddLight(go.transform, new Vector3(-3.5f, 4.2f, -2.5f), new Color(1f, 0.82f, 0.6f), 16f);
            AddLight(go.transform, new Vector3(3.8f, 3.8f, -1.5f), new Color(1f, 0.78f, 0.55f), 12f);
            AddLight(go.transform, new Vector3(0.5f, 3.0f, 4.5f), new Color(0.7f, 0.8f, 1f), 10f);
            AddLight(go.transform, new Vector3(1.5f, 0.7f, -5.5f), new Color(1f, 0.9f, 0.78f), 9f);   // low fill: rims and lower doors

            // Studio reflections: big warm softboxes the probe sees (layer 31) but the camera does not, so the
            // clear coat picks up long highlights like a real workshop shoot.
            const int ReflLayer = 31;
            var soft = TyreFX.ParticleMaterial("DK_Softbox", Texture2D.whiteTexture, new Color(1.25f, 1.1f, 0.9f, 1f));
            foreach (var (pos, rot, scale) in new[]
            {
                (new Vector3(0f, 5.5f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(3f, 7f, 1f)),
                (new Vector3(-6f, 2.2f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector3(9f, 1.2f, 1f)),
                (new Vector3(6f, 2.2f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(9f, 1.2f, 1f)),
                (new Vector3(0f, 2.4f, 7f), Quaternion.identity, new Vector3(6f, 1.6f, 1f)),
            })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Softbox"; q.layer = ReflLayer;
                q.transform.SetParent(go.transform, false);
                q.transform.localPosition = pos; q.transform.localRotation = rot; q.transform.localScale = scale;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = soft;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var pg = new GameObject("StudioProbe");
            pg.transform.SetParent(go.transform, false);
            pg.transform.localPosition = new Vector3(0f, 1f, 0f);
            var probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(20f, 12f, 20f);
            probe.resolution = 128; probe.hdr = true; probe.importance = 10;
            probe.cullingMask = 1 << ReflLayer;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.10f, 0.075f, 0.055f);
            probe.RenderProbe();

            // Contact shadow
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(sh.GetComponent<Collider>());
            sh.name = "ContactShadow";
            sh.layer = Layer;
            sh.transform.SetParent(go.transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = new Vector3(3.2f, 6.0f, 1f);
            var m = TyreFX.ParticleMaterial("DK_ContactShadow", BlobTexture(), new Color(0, 0, 0, 0.85f));
            sh.GetComponent<MeshRenderer>().sharedMaterial = m;
            return s;
        }

        static void AddLight(Transform p, Vector3 pos, Color c, float intensity)
        {
            var lg = new GameObject("Lamp");
            lg.transform.SetParent(p, false);
            lg.transform.localPosition = pos;
            lg.transform.LookAt(p.position + Vector3.up * 0.6f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 70f; l.innerSpotAngle = 40f; l.range = 20f; l.intensity = intensity; l.color = c;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.Soft;
        }

        static Texture2D BlobTexture()
        {
            var t = new Texture2D(64, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DK_Blob" };
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 32f, dy = (y - 63.5f) / 64f;
                    float d = Mathf.Sqrt(dx * dx * 1.0f + dy * dy * 1.0f);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            return t;
        }

        public void Show(string id)
        {
            if (current != null) Destroy(current);
            current = null;
            if (id == null) return;
            current = CarFactory.BuildDisplay(id, turntable, true);
            if (current == null) return;
            foreach (var tr in current.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
        }

        public void Drag(float dx) { yaw -= dx * 0.25f; idle = 0f; }

        public void Spin(float metres)
        {
            var dw = current != null ? current.GetComponent<DisplayWheels>() : null;
            if (dw != null) dw.Roll(metres);
        }

        void Update()
        {
            idle += Time.unscaledDeltaTime;
            if (idle > 2.5f) yaw += spin * Time.unscaledDeltaTime;
            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void OnDestroy() { if (rt != null) rt.Release(); }
    }

    public class DragSpin : MonoBehaviour, IDragHandler
    {
        public Showroom showroom;
        public void OnDrag(PointerEventData e) { if (showroom != null) showroom.Drag(e.delta.x); }
    }
}
