using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Pause / settings overlay: resume, controls layout (أسهم / دركسون / ميلان), drift assist, camera, time of day,
    /// music and effects volume, numerals, and back to the menu. Pauses time while driving.
    public class PauseMenu : MonoBehaviour
    {
        static PauseMenu open;
        GameFlow flow;
        bool inDrive;
        Text layoutVal, timeVal, numVal, camVal;

        public static void Open(GameFlow flow, bool inDrive)
        {
            if (open != null) return;
            var c = UIKit.MakeCanvas("DK_Pause", 60);
            var p = c.gameObject.AddComponent<PauseMenu>();
            open = p;
            p.flow = flow; p.inDrive = inDrive;
            if (inDrive) Time.timeScale = 0f;
            p.Build(c.transform);
        }

        void Build(Transform root)
        {
            var dim = UIKit.Img(root, "Dim", null, new Color(0, 0, 0, 0.6f), true);
            UIKit.Stretch(dim.rectTransform);
            var panel = UIKit.Panel(root, "Panel", new Color(0.06f, 0.055f, 0.05f, 0.96f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 860f));
            UIKit.Outline(panel.transform, new Color(1f, 0.7f, 0.2f, 0.45f));
            var tr = panel.transform;
            var title = UIKit.Label(tr, "Title", inDrive ? "إيقاف مؤقت" : "الإعدادات", 64, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Display);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(800f, 84f));

            float y = -140f;
            layoutVal = Row(tr, ref y, "التحكم", LayoutName(), () => { GameSettings.Layout = (GameSettings.Controls)(((int)GameSettings.Layout + 1) % 3); layoutVal.text = UIKit.Ar(LayoutName()); Object.FindAnyObjectByType<DriveHUD>()?.ApplyLayout(); });
            SliderRow(tr, ref y, "مساعدة التقحيص", GameSettings.Assist, v =>
            {
                GameSettings.Assist = v;
                if (flow.car != null) flow.car.GetComponent<DriftCarController>().counterSteerAssist = v;
            });
            if (inDrive)
            {
                camVal = Row(tr, ref y, "الكاميرا", CamName(), () => { flow.driftCam.NextMode(); camVal.text = UIKit.Ar(CamName()); });
                timeVal = Row(tr, ref y, "الوقت", Atmosphere.IsNight ? "ليل" : "غروب", () =>
                {
                    Atmosphere.Apply(Atmosphere.IsNight ? Atmosphere.Preset.GoldenHour : Atmosphere.Preset.Night);
                    timeVal.text = UIKit.Ar(Atmosphere.IsNight ? "ليل" : "غروب");
                    Sfx.Music(Atmosphere.IsNight ? "mus_drive_night" : "mus_drive_day");
                });
            }
            SliderRow(tr, ref y, "الموسيقى", GameSettings.Music, v => GameSettings.Music = v);
            SliderRow(tr, ref y, "المؤثرات", GameSettings.Effects, v => GameSettings.Effects = v);
            numVal = Row(tr, ref y, "الأرقام", GameSettings.EasternNumerals ? "١٢٣" : "123", () =>
            {
                GameSettings.EasternNumerals = !GameSettings.EasternNumerals;
                numVal.text = GameSettings.EasternNumerals ? UIKit.Ar("١٢٣") : "123";
            });

            var resume = UIKit.Button(tr, "Resume", UIKit.Gold, Close, "hud/panel_sharp");
            UIKit.Place((RectTransform)resume.transform, new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(500f, 100f));
            var rt = UIKit.Label(resume.transform, "T", inDrive ? "متابعة" : "تم", 46, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(rt.rectTransform);
            if (inDrive)
            {
                var quit = UIKit.Button(tr, "Quit", UIKit.Ink, () => { Close(); flow.ShowTitle(); }, "hud/panel_sharp");
                UIKit.Place((RectTransform)quit.transform, new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(500f, 100f));
                UIKit.Outline(quit.transform, new Color(1, 1, 1, 0.25f), "hud/panel_sharp_outline");
                var qt = UIKit.Label(quit.transform, "T", "القائمة الرئيسية", 40, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Stretch(qt.rectTransform);
            }
        }

        static string LayoutName() => GameSettings.Layout == GameSettings.Controls.Buttons ? "أسهم" : GameSettings.Layout == GameSettings.Controls.Wheel ? "دركسون" : "ميلان";
        string CamName()
        {
            var m = flow.driftCam.mode;
            return m == DriftCamera.Mode.ChaseNear ? "قريبة" : m == DriftCamera.Mode.ChaseFar ? "بعيدة" : m == DriftCamera.Mode.Bumper ? "صدام" : "كبوت";
        }

        Text Row(Transform tr, ref float y, string label, string value, System.Action onTap)
        {
            var l = UIKit.Label(tr, "L_" + label, label, 40, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(l.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, y), new Vector2(420f, 70f));
            var b = UIKit.Button(tr, "B_" + label, new Color(1, 1, 1, 0.08f), onTap, "hud/pill");
            UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(70f, y), new Vector2(520f, 70f));
            UIKit.Outline(b.transform, new Color(1, 1, 1, 0.22f), "hud/pill_outline");
            var v = UIKit.Label(b.transform, "V", value, 36, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black, value.Length > 0 && value[0] > 127);
            UIKit.Stretch(v.rectTransform);
            var ic = UIKit.Img(b.transform, "Ch", UIKit.S("hud/ic_arrows"), new Color(1, 1, 1, 0.5f));
            UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(40f, 40f));
            y -= 88f;
            return v;
        }

        void SliderRow(Transform tr, ref float y, string label, float value, System.Action<float> onChange)
        {
            var l = UIKit.Label(tr, "L_" + label, label, 40, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(l.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, y), new Vector2(420f, 70f));
            var root = UIKit.Rect(tr, "S_" + label);
            UIKit.Place(root, new Vector2(0f, 1f), new Vector2(70f, y - 22f), new Vector2(520f, 26f));
            var bg = UIKit.Panel(root, "Bg", new Color(1, 1, 1, 0.14f), "hud/pill");
            UIKit.Stretch(bg.rectTransform);
            var fillArea = UIKit.Rect(root, "FillArea");
            UIKit.Stretch(fillArea);
            var fill = UIKit.Panel(fillArea, "Fill", UIKit.Gold, "hud/pill");
            UIKit.Stretch(fill.rectTransform);
            var handleArea = UIKit.Rect(root, "HandleArea");
            UIKit.Stretch(handleArea, 20, 20, 0, 0);
            var handle = UIKit.Img(handleArea, "Handle", UIKit.S("hud/circle_mask"), Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(54f, 54f);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            y -= 88f;
        }

        void Close()
        {
            if (inDrive) Time.timeScale = 1f;
            open = null;
            Destroy(gameObject);
        }
    }
}
