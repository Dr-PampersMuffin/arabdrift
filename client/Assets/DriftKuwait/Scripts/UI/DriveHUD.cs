using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Driving HUD (reference: the night pursuit mock-up). Right-to-left Arabic layout, Eastern Arabic numerals:
    ///  top-left   pause + mission panel          top-right  score, multiplier, callout, chain breakdown
    ///  right edge camera / lights / horn / more  bottom-centre drift angle + radio/dialogue subtitles
    ///  bottom-left minimap, location, chase level, steering (buttons, wheel or tilt)
    ///  bottom-right rev gauge with speed and gear, handbrake, brake and gas pedals
    public class DriveHUD : MonoBehaviour
    {
        GameFlow flow;
        DriftCarController car;
        DriftScorer scorer;
        DriftCamera cam;

        Image scorePlate;
        Text totalTxt, chainTxt, multTxt, calloutTxt, angleTxt, speedTxt, gearTxt, turboTxt, missionTitle, missionSub, missionTime;
        Text[] breakdown = new Text[3];
        GameObject breakdownRoot, multRoot, heatRoot, dialogRoot;
        Image[] angleSegs, flames;
        RectTransform needle;
        Image dialogPortrait;
        Text dialogName, dialogText;
        float calloutTimer, dialogTimer, shownTotal;
        RawImage minimap;
        RectTransform mapArrow;
        Camera mapCam;
        RenderTexture mapRT;
        HoldButton gas, brake, hand, left, right;
        SteeringWheelWidget wheel;
        GameObject steerButtons;
        public int Heat { get; set; }
        public float PursuitTime { get; set; }
        public bool InPursuit { get; set; }

        static readonly Color AngleLow = new Color(1f, 1f, 1f, 0.9f), AngleHigh = new Color(1f, 0.55f, 0.1f, 1f), SegOff = new Color(1f, 1f, 1f, 0.15f);

        public static DriveHUD Build(GameFlow flow, DriftCarController car, DriftScorer scorer, DriftCamera cam)
        {
            var canvas = UIKit.MakeCanvas("DK_DriveHUD", 20);
            var h = canvas.gameObject.AddComponent<DriveHUD>();
            h.flow = flow; h.car = car; h.scorer = scorer; h.cam = cam;
            var safe = UIKit.SafeArea(canvas.transform);
            h.BuildTopLeft(safe);
            h.BuildScore(safe);
            h.BuildRightEdge(safe);
            h.BuildCentre(safe);
            h.BuildMinimap(safe);
            h.BuildControls(safe);
            h.BuildGauge(safe);
            scorer.OnCallout += h.Callout;
            return h;
        }

        // ---------------- layout ----------------
        void BuildTopLeft(RectTransform safe)
        {
            var pause = UIKit.Button(safe, "Pause", new Color(0.06f, 0.06f, 0.07f, 0.7f), () => PauseMenu.Open(flow, true), "hud/panel");
            UIKit.Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(104f, 104f));
            UIKit.Outline(pause.transform, new Color(1, 1, 1, 0.25f));
            var pi = UIKit.Img(pause.transform, "I", UIKit.S("hud/ic_pause"), Color.white);
            UIKit.Place(pi.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));

            var mp = UIKit.Panel(safe, "Mission", new Color(0.06f, 0.06f, 0.07f, 0.7f));
            UIKit.Place(mp.rectTransform, new Vector2(0f, 1f), new Vector2(156f, -30f), new Vector2(400f, 150f));
            UIKit.Outline(mp.transform, new Color(1, 1, 1, 0.2f));
            missionTitle = UIKit.Label(mp.transform, "T", "تقحيص حر", 40, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(missionTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -10f), new Vector2(360f, 50f));
            missionSub = UIKit.Label(mp.transform, "S", "سلوى · قطعة ٥", 28, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(missionSub.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -62f), new Vector2(360f, 40f));
            missionTime = UIKit.Label(mp.transform, "Time", "", 40, Color.white, TextAnchor.MiddleRight, UIKit.Black, false);
            UIKit.Place(missionTime.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -100f), new Vector2(360f, 44f));
        }

        void BuildScore(RectTransform safe)
        {
            var root = UIKit.Rect(safe, "Score");
            UIKit.Place(root, new Vector2(1f, 1f), new Vector2(-190f, -20f), new Vector2(760f, 470f));   // clear of the side buttons
            var plate = UIKit.Img(root, "Plate", UIKit.S("hud/grad_h"), new Color(1, 1, 1, 0.32f));
            scorePlate = plate;
            UIKit.Place(plate.rectTransform, new Vector2(1f, 1f), new Vector2(40f, -6f), new Vector2(700f, 150f));

            totalTxt = UIKit.Label(root, "Total", UIKit.Num(0), 124, Color.white, TextAnchor.UpperRight, UIKit.Black);
            UIKit.Place(totalTxt.rectTransform, new Vector2(1f, 1f), new Vector2(-150f, 0f), new Vector2(560f, 140f));
            UIKit.AddShadow(totalTxt, 4f, 0.7f);

            var mb = UIKit.Panel(root, "Mult", new Color(0.06f, 0.05f, 0.04f, 0.6f), "hud/panel_sharp");
            multRoot = mb.gameObject;
            UIKit.Place(mb.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(126f, 96f));
            UIKit.Outline(mb.transform, UIKit.Gold, "hud/panel_sharp_outline");
            multTxt = UIKit.Label(mb.transform, "T", "x1", 62, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black, false);
            UIKit.Stretch(multTxt.rectTransform);
            mb.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);

            calloutTxt = UIKit.Label(root, "Callout", "", 62, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(calloutTxt.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -128f), new Vector2(700f, 80f));
            UIKit.AddShadow(calloutTxt, 3f, 0.8f);

            chainTxt = UIKit.Label(root, "Chain", "", 52, Color.white, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(chainTxt.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -200f), new Vector2(500f, 64f));
            UIKit.AddShadow(chainTxt, 3f, 0.7f);

            var bd = UIKit.Panel(root, "Breakdown", new Color(0.05f, 0.05f, 0.06f, 0.62f), "hud/panel");
            breakdownRoot = bd.gameObject;
            UIKit.Place(bd.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -266f), new Vector2(290f, 136f));
            for (int i = 0; i < 3; i++)
            {
                breakdown[i] = UIKit.Label(bd.transform, "B" + i, "", 32, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Arabic);
                UIKit.Place(breakdown[i].rectTransform, new Vector2(1f, 1f), new Vector2(-22f, -8f - i * 40f), new Vector2(260f, 40f));
            }
        }

        void BuildRightEdge(RectTransform safe)
        {
            var col = UIKit.Rect(safe, "Side");
            UIKit.Place(col, new Vector2(1f, 0.5f), new Vector2(-30f, 60f), new Vector2(110f, 520f));
            (string icon, string ar, System.Action act)[] items =
            {
                ("video", "كاميرا", () => cam.NextMode()),
                ("lights", "أضواء", () => Atmosphere.Apply(Atmosphere.IsNight ? Atmosphere.Preset.GoldenHour : Atmosphere.Preset.Night)),
                ("horn", "هوز", () => Sfx.Play("horn", 0.9f)),
                ("more", "", () => PauseMenu.Open(flow, true)),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var b = UIKit.Button(col, "S" + i, new Color(0.06f, 0.06f, 0.07f, 0.68f), it.act, "hud/panel");
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -i * 124f), new Vector2(104f, 112f));
                UIKit.Outline(b.transform, new Color(1, 1, 1, 0.2f));
                var ic = UIKit.Img(b.transform, "I", UIKit.S("hud/ic_" + it.icon), Color.white);
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, it.ar == "" ? -28f : -12f), new Vector2(54f, 54f));
                if (it.ar != "")
                {
                    var t = UIKit.Label(b.transform, "T", it.ar, 24, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Arabic);
                    UIKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(104f, 34f));
                }
            }
        }

        void BuildCentre(RectTransform safe)
        {
            var c = UIKit.Rect(safe, "Centre");
            UIKit.Place(c, new Vector2(0.5f, 0f), new Vector2(-40f, 150f), new Vector2(620f, 130f));
            angleTxt = UIKit.Label(c, "Angle", "", 58, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(angleTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(300f, 70f));
            UIKit.AddShadow(angleTxt, 3f, 0.7f);
            var segRoot = UIKit.Rect(c, "Segs");
            UIKit.Place(segRoot, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(560f, 34f));
            var bgp = UIKit.Panel(segRoot, "Bg", new Color(0, 0, 0, 0.45f), "hud/pill");
            UIKit.Stretch(bgp.rectTransform, -10, -10, -6, -6);
            angleSegs = new Image[14];
            for (int i = 0; i < 14; i++)
            {
                var s = UIKit.Panel(segRoot, "S" + i, SegOff, "hud/panel_sharp");
                UIKit.Place(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * 40f, 0f), new Vector2(32f, 22f));
                angleSegs[i] = s;
            }

            // Radio / dialogue subtitle box
            var d = UIKit.Panel(safe, "Dialog", new Color(0.04f, 0.04f, 0.05f, 0.82f), "hud/panel");
            dialogRoot = d.gameObject;
            UIKit.Place(d.rectTransform, new Vector2(0.5f, 0f), new Vector2(-40f, 26f), new Vector2(980f, 112f));
            UIKit.Outline(d.transform, new Color(1, 1, 1, 0.18f));
            var pm = UIKit.Rect(d.transform, "PortraitMask");
            UIKit.Place(pm, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(90f, 90f));
            pm.gameObject.AddComponent<RectMask2D>();
            dialogPortrait = UIKit.Img(pm, "P", null, Color.white);
            UIKit.Stretch(dialogPortrait.rectTransform);
            dialogName = UIKit.Label(d.transform, "N", "", 32, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(dialogName.rectTransform, new Vector2(1f, 1f), new Vector2(-120f, -6f), new Vector2(820f, 44f));
            dialogText = UIKit.Label(d.transform, "T", "", 32, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(dialogText.rectTransform, new Vector2(1f, 0f), new Vector2(-120f, 10f), new Vector2(840f, 54f));
            dialogRoot.SetActive(false);
        }

        void BuildMinimap(RectTransform safe)
        {
            var root = UIKit.Rect(safe, "Minimap");
            UIKit.Place(root, new Vector2(0f, 0f), new Vector2(34f, 300f), new Vector2(300f, 300f));
            var mask = UIKit.Img(root, "Mask", UIKit.S("hud/circle_mask"), Color.white);
            UIKit.Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mapRT = new RenderTexture(320, 320, 16) { name = "MinimapRT" };
            var bgp = UIKit.Img(mask.transform, "Bg", null, new Color(0.08f, 0.09f, 0.1f, 0.9f));
            UIKit.Stretch(bgp.rectTransform);
            minimap = UIKit.Cover(mask.transform, "Map", mapRT);
            minimap.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            var frame = UIKit.Img(root, "Frame", UIKit.S("hud/minimap_frame"), Color.white);
            UIKit.Stretch(frame.rectTransform);
            var arrow = UIKit.Img(root, "Arrow", UIKit.S("hud/player_arrow"), Color.white);
            mapArrow = arrow.rectTransform;
            UIKit.Place(mapArrow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            mapArrow.pivot = new Vector2(0.5f, 0.5f);

            var loc = UIKit.Panel(safe, "Loc", new Color(0.05f, 0.05f, 0.06f, 0.75f), "hud/panel");
            UIKit.Place(loc.rectTransform, new Vector2(0f, 0f), new Vector2(64f, 214f), new Vector2(240f, 92f));
            var l1 = UIKit.Label(loc.transform, "A", "سلوى", 34, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(l1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(240f, 46f));
            var l2 = UIKit.Label(loc.transform, "B", "قطعة ٥", 26, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, UIKit.Arabic);
            UIKit.Place(l2.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(240f, 36f));

            var camGo = new GameObject("DK_MinimapCam");
            mapCam = camGo.AddComponent<Camera>();
            mapCam.orthographic = true;
            mapCam.orthographicSize = 70f;
            mapCam.targetTexture = mapRT;
            mapCam.clearFlags = CameraClearFlags.SolidColor;
            mapCam.backgroundColor = new Color(0.1f, 0.1f, 0.11f);
            mapCam.cullingMask = ~((1 << 2) | (1 << Showroom.Layer));
            mapCam.farClipPlane = 400f;
            mapCam.depth = -5;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            // Chase level (only during a pursuit)
            var heat = UIKit.Rect(safe, "Heat");
            heatRoot = heat.gameObject;
            UIKit.Place(heat, new Vector2(0f, 0f), new Vector2(34f, 616f), new Vector2(420f, 60f));
            var ht = UIKit.Label(heat, "T", "مستوى الملاحقة", 28, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(ht.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(220f, 50f));
            UIKit.AddShadow(ht, 2f, 0.8f);
            flames = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                flames[i] = UIKit.Img(heat, "F" + i, UIKit.S("hud/ic_flame"), Color.white);
                UIKit.Place(flames[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(i * 38f, 0f), new Vector2(40f, 40f));
            }
            heatRoot.SetActive(false);
        }

        void BuildControls(RectTransform safe)
        {
            steerButtons = UIKit.Rect(safe, "SteerButtons").gameObject;
            var sb = (RectTransform)steerButtons.transform;
            UIKit.Stretch(sb);
            left = HoldImage(sb, "Left", "hud/steer_l", new Vector2(0f, 0f), new Vector2(360f, 26f), new Vector2(178f, 178f));
            right = HoldImage(sb, "Right", "hud/steer_r", new Vector2(0f, 0f), new Vector2(560f, 26f), new Vector2(178f, 178f));

            wheel = SteeringWheelWidget.Build(safe);
            ApplyLayout();

            hand = HoldImage(safe, "Handbrake", "hud/handbrake", new Vector2(1f, 0f), new Vector2(-640f, 40f), new Vector2(210f, 210f));
            var hl = UIKit.Label(hand.transform, "L", "هاند بريك", 28, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(hl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(220f, 40f));
            UIKit.AddShadow(hl, 2f, 0.8f);
            brake = HoldImage(safe, "Brake", "hud/pedal_brake", new Vector2(1f, 0f), new Vector2(-420f, 40f), new Vector2(130f, 160f));
            var bl = UIKit.Label(brake.transform, "L", "فرامل", 28, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(bl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(160f, 40f));
            UIKit.AddShadow(bl, 2f, 0.8f);
            gas = HoldImage(safe, "Gas", "hud/pedal_gas", new Vector2(1f, 0f), new Vector2(-220f, 40f), new Vector2(150f, 230f));
            var gl = UIKit.Label(gas.transform, "L", "دفع", 28, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(gl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(160f, 40f));
            UIKit.AddShadow(gl, 2f, 0.8f);
        }

        public void ApplyLayout()
        {
            var l = GameSettings.Layout;
            steerButtons.SetActive(l == GameSettings.Controls.Buttons);
            wheel.gameObject.SetActive(l == GameSettings.Controls.Wheel);
        }

        HoldButton HoldImage(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var im = UIKit.Img(parent, name, UIKit.S(sprite), Color.white, true);
            im.preserveAspect = true;
            UIKit.Place(im.rectTransform, anchor, pos, size);
            return im.gameObject.AddComponent<HoldButton>();
        }

        void BuildGauge(RectTransform safe)
        {
            var g = UIKit.Img(safe, "Gauge", UIKit.S("hud/gauge"), Color.white);
            UIKit.Place(g.rectTransform, new Vector2(1f, 0f), new Vector2(-150f, 300f), new Vector2(360f, 360f));
            var n = UIKit.Img(g.transform, "Needle", UIKit.S("hud/needle"), Color.white);
            needle = n.rectTransform;
            UIKit.Stretch(needle);
            speedTxt = UIKit.Label(g.transform, "Speed", UIKit.Num(0), 92, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Place(speedTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(240f, 110f));
            var unit = UIKit.Label(g.transform, "Unit", "كم/س", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, UIKit.Arabic);
            UIKit.Place(unit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -102f), new Vector2(200f, 36f));
            turboTxt = UIKit.Label(g.transform, "Turbo", "TURBO", 22, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, UIKit.Black, false);
            UIKit.Place(turboTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(160f, 30f));
            var gb = UIKit.Panel(g.transform, "GearBox", new Color(0, 0, 0, 0.7f), "hud/panel_sharp");
            UIKit.Place(gb.rectTransform, new Vector2(1f, 0f), new Vector2(52f, 30f), new Vector2(76f, 96f));
            UIKit.Outline(gb.transform, new Color(1, 1, 1, 0.8f), "hud/panel_sharp_outline");
            gearTxt = UIKit.Label(gb.transform, "T", "١", 62, Color.white, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(gearTxt.rectTransform);
        }

        // ---------------- runtime ----------------
        void Update()
        {
            if (car == null) return;
            // Controls -> input (preview runs script the inputs directly)
            if (!PreviewHooks.Active)
            {
                CarInput.TouchThrottle = gas.Pressed ? 1f : 0f;
                CarInput.TouchBrake = brake.Pressed ? 1f : 0f;
                CarInput.TouchHandbrake = hand.Pressed;
                if (GameSettings.Layout == GameSettings.Controls.Buttons) CarInput.TouchSteer = (right.Pressed ? 1f : 0f) - (left.Pressed ? 1f : 0f);
                else if (GameSettings.Layout == GameSettings.Controls.Wheel) CarInput.TouchSteer = wheel.Value;
                else CarInput.TouchSteer = 0f;
            }

            // Score
            shownTotal = Mathf.Lerp(shownTotal, scorer.Total, 1f - Mathf.Exp(-8f * Time.deltaTime));
            totalTxt.text = shownTotal < 0.5f ? "" : UIKit.Num((long)Mathf.Round(shownTotal));
            bool chain = scorer.Current > 1f;
            multRoot.SetActive(scorer.Multiplier > 1);
            multTxt.text = "x" + scorer.Multiplier;
            chainTxt.text = chain ? "+" + UIKit.Num((long)scorer.Current) : "";
            breakdownRoot.SetActive(chain);
            scorePlate.enabled = shownTotal >= 0.5f || chain;
            if (chain)
            {
                breakdown[0].text = UIKit.Ar("زاوية +" + UIKit.Num((long)scorer.AnglePts));
                breakdown[1].text = UIKit.Ar("سرعة +" + UIKit.Num((long)scorer.SpeedPts));
                breakdown[2].text = UIKit.Ar("اقتراب +" + UIKit.Num((long)scorer.ProxPts));
            }
            if (calloutTimer > 0f)
            {
                calloutTimer -= Time.deltaTime;
                var c = calloutTxt.color; c.a = Mathf.Clamp01(calloutTimer / 0.4f); calloutTxt.color = c;
                calloutTxt.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, calloutTimer - 1.6f) * 0.8f);
            }

            // Drift angle
            float ang = Mathf.Abs(car.DriftAngle);
            bool drifting = ang > 8f && car.SpeedKmh > 15f;
            angleTxt.text = drifting ? UIKit.Num((long)ang) + "°" : "";
            int lit = drifting ? Mathf.Clamp(Mathf.RoundToInt(ang / 90f * 14f), 0, 14) : 0;
            for (int i = 0; i < angleSegs.Length; i++)
                angleSegs[i].color = i < lit ? Color.Lerp(AngleLow, AngleHigh, i / 13f) : SegOff;

            // Gauge
            float rpmT = Mathf.Clamp01(car.Rpm / 8000f);
            needle.localRotation = Quaternion.Euler(0f, 0f, 135f - rpmT * 270f);
            speedTxt.text = UIKit.Num((long)car.SpeedKmh, false);
            gearTxt.text = car.Gear < 0 ? "R" : UIKit.Num(car.Gear, false);
            turboTxt.color = new Color(1f, 0.55f, 0.15f, 0.25f + car.Boost * 0.75f);

            // Mission panel / pursuit
            heatRoot.SetActive(InPursuit);
            if (InPursuit)
            {
                missionTitle.text = UIKit.Ar("ملاحقة");
                missionSub.text = UIKit.Ar("اهرب من الدورية");
                int s = Mathf.FloorToInt(PursuitTime);
                missionTime.text = UIKit.Num(s / 60, false) + ":" + (UIKit.EasternNumerals ? UIKit.Num(s % 60 / 10, false) + UIKit.Num(s % 10, false) : (s % 60).ToString("00"));
                for (int i = 0; i < 5; i++) flames[i].color = i < Heat ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            }
            else
            {
                missionTitle.text = UIKit.Ar("تقحيص حر");
                missionSub.text = UIKit.Ar("سلوى · قطعة ٥");
                missionTime.text = "";
            }

            if (dialogTimer > 0f) { dialogTimer -= Time.deltaTime; if (dialogTimer <= 0f) { dialogRoot.SetActive(false); Sfx.MusicDuck = 1f; } }
        }

        void LateUpdate()
        {
            if (car == null || mapCam == null) return;
            var p = car.transform.position;
            mapCam.transform.position = p + Vector3.up * 150f;
            mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mapArrow.localRotation = Quaternion.Euler(0f, 0f, -car.transform.eulerAngles.y);
            mapCam.enabled = Time.frameCount % 2 == 0;
        }

        public void Callout(string text, Color c)
        {
            calloutTxt.text = UIKit.Ar(text);
            calloutTxt.color = c;
            calloutTimer = 2f;
        }

        /// Subtitle box with portrait, plus the voice line. Returns the line length.
        public float Say(string voiceKey, string portrait, string nameAr, string textAr)
        {
            float len = Sfx.Voice(voiceKey, 1f);
            dialogRoot.SetActive(true);
            dialogPortrait.sprite = UIKit.S(portrait);
            dialogName.text = UIKit.Ar(nameAr);
            dialogText.text = UIKit.Ar(textAr);
            dialogTimer = Mathf.Max(2.5f, len + 0.6f);
            Sfx.MusicDuck = 0.45f;
            return dialogTimer;
        }

        public void PlayIntro() => StartCoroutine(Intro());

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(1.0f);
            yield return new WaitForSeconds(Say("ab_intro_1", "por_abu_rashid", "أبو راشد:", "هلا فيك يا ولدي. هذي سلوى .. وهذا الشاهين."));
            yield return new WaitForSeconds(Say("ab_intro_2", "por_abu_rashid", "أبو راشد:", "الشارع هني له روح .. وروح الشارع ما تنشرى، تنكسب."));
            float t = 0f;
            while (car != null && car.SpeedKmh < 25f && t < 6f) { t += Time.deltaTime; yield return null; }
            if (car == null) yield break;
            if (car.SpeedKmh < 25f) yield return new WaitForSeconds(Say("ab_tut_throttle", "por_abu_rashid", "أبو راشد:", "دوس البانزين .. خل التيربو يصفّر."));
            t = 0f;
            while (car != null && car.SpeedKmh < 55f && t < 10f) { t += Time.deltaTime; yield return null; }
            if (car == null) yield break;
            yield return new WaitForSeconds(Say("ab_tut_handbrake", "por_abu_rashid", "أبو راشد:", "الحين! شد الهاندبريك وخل الصدر يلف .. لا تخاف."));
            t = 0f;
            while (car != null && Mathf.Abs(car.DriftAngle) < 20f && t < 8f) { t += Time.deltaTime; yield return null; }
            if (car == null) yield break;
            yield return new WaitForSeconds(Say("ab_tut_counter", "por_abu_rashid", "أبو راشد:", "عاكس الدركسون .. إي، جذي. لا تفك الدعسة."));
            long before = scorer.Total;
            t = 0f;
            while (car != null && scorer.Total - before < 800 && t < 20f) { t += Time.deltaTime; yield return null; }
            if (car == null) yield break;
            if (scorer.Total - before >= 800) Say("ab_good", "por_abu_rashid", "أبو راشد:", "كفو! هذا التقحيص اللي أبيه.");
        }

        void OnDestroy()
        {
            if (scorer != null) scorer.OnCallout -= Callout;
            if (mapCam != null) Destroy(mapCam.gameObject);
            if (mapRT != null) mapRT.Release();
            CarInput.TouchThrottle = CarInput.TouchBrake = CarInput.TouchSteer = 0f;
            CarInput.TouchHandbrake = false;
            Sfx.MusicDuck = 1f;
        }
    }

    /// Virtual steering wheel: drag around its centre; springs back to centre on release. Value -1..1 (±135°).
    public class SteeringWheelWidget : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Value { get; private set; }
        RectTransform rt, wheelImg;
        float angle, startPointer, startAngle;
        bool held;

        public static SteeringWheelWidget Build(RectTransform parent)
        {
            var bg = UIKit.Img(parent, "Wheel", UIKit.S("hud/round_dark"), new Color(1, 1, 1, 0.8f), true);
            UIKit.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(380f, 20f), new Vector2(300f, 300f));
            var w = bg.gameObject.AddComponent<SteeringWheelWidget>();
            w.rt = bg.rectTransform;
            var im = UIKit.Img(bg.transform, "WheelIcon", UIKit.S("hud/ic_wheel"), Color.white);
            UIKit.Place(im.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 250f));
            w.wheelImg = im.rectTransform;
            return w;
        }

        float PointerAngle(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp);
            return Mathf.Atan2(lp.x, lp.y) * Mathf.Rad2Deg;
        }

        public void OnPointerDown(PointerEventData e) { held = true; startPointer = PointerAngle(e); startAngle = angle; }
        public void OnDrag(PointerEventData e) { angle = Mathf.Clamp(startAngle + Mathf.DeltaAngle(startPointer, PointerAngle(e)), -135f, 135f); }
        public void OnPointerUp(PointerEventData e) { held = false; }

        void Update()
        {
            if (!held) angle = Mathf.MoveTowards(angle, 0f, 540f * Time.deltaTime);
            Value = angle / 135f;
            wheelImg.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
