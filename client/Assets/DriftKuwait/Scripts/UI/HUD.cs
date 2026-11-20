using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DriftKuwait
{
    /// Arabic-first, right-to-left HUD built from code: score top-right, drift chain top-centre,
    /// callouts, speedo + gear bottom-right, camera/weather/reset buttons, touch controls.
    public class HUD : MonoBehaviour
    {
        public DriftCarController car;
        public DriftScorer scorer;
        public DriftCamera cam;

        Font latin, arabic;
        Text totalTxt, currentTxt, multTxt, speedTxt, gearTxt, calloutTxt;
        float calloutT;
        int preset;
        static readonly Color Ink = new Color(1f, 0.97f, 0.9f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

        void Start()
        {
            latin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arabic = Font.CreateDynamicFontFromOSFont(new[] { "Geeza Pro", "Damascus", "Arial", "Tahoma", "Segoe UI", "Noto Sans Arabic", "Noto Naskh Arabic" }, 64);

            EnsureEventSystem();
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2340f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Safe area for the notch
            var safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(root, false);
            var sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            // Top-right: total score
            Label(safe, "النقاط", arabic, 34, new Vector2(1, 1), new Vector2(-60, -40), TextAnchor.UpperRight, Gold, true);
            totalTxt = Label(safe, "0", latin, 64, new Vector2(1, 1), new Vector2(-60, -88), TextAnchor.UpperRight, Ink, false);

            // Top-centre: current chain
            currentTxt = Label(safe, "", latin, 76, new Vector2(0.5f, 1), new Vector2(0, -40), TextAnchor.UpperCenter, Ink, false);
            multTxt = Label(safe, "", latin, 44, new Vector2(0.5f, 1), new Vector2(0, -130), TextAnchor.UpperCenter, Gold, false);

            // Callout
            calloutTxt = Label(safe, "", arabic, 110, new Vector2(0.5f, 0.68f), Vector2.zero, TextAnchor.MiddleCenter, Gold, false);

            // Bottom-right: speedo
            speedTxt = Label(safe, "0", latin, 120, new Vector2(1, 0), new Vector2(-70, 210), TextAnchor.LowerRight, Ink, false);
            Label(safe, "كم/س", arabic, 34, new Vector2(1, 0), new Vector2(-70, 170), TextAnchor.LowerRight, Gold, true);
            gearTxt = Label(safe, "1", latin, 56, new Vector2(1, 0), new Vector2(-330, 230), TextAnchor.LowerRight, Gold, false);

            // Top-left: utility buttons (RTL layouts keep these on the left, away from the score)
            var camBtn = Button(safe, "كاميرا", new Vector2(0, 1), new Vector2(150, -70), new Vector2(200, 80));
            var hold = camBtn.gameObject.AddComponent<HoldButton>();
            hold.onTap = () => cam.NextMode();
            hold.onLongPress = () => cam.ToggleNearFar();
            var wBtn = Button(safe, "الجو", new Vector2(0, 1), new Vector2(370, -70), new Vector2(200, 80));
            wBtn.gameObject.AddComponent<HoldButton>().onTap = CycleWeather;
            var rBtn = Button(safe, "رجّع", new Vector2(0, 1), new Vector2(590, -70), new Vector2(200, 80));
            rBtn.gameObject.AddComponent<HoldButton>().onTap = ResetCar;

            var touch = gameObject.AddComponent<TouchControls>();
            touch.Build(safe, arabic);
            touch.SetVisible(Application.isMobilePlatform);

            scorer.OnCallout += (word, colour) =>
            {
                calloutTxt.text = ArabicShaper.Shape(word);
                calloutTxt.color = colour;
                calloutT = 1.6f;
            };
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        Text Label(RectTransform parent, string text, Font font, int size, Vector2 anchor, Vector2 pos, TextAnchor align, Color c, bool shape)
        {
            var go = new GameObject("Txt", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? 1f : 0.5f,
                                   align == TextAnchor.LowerRight ? 0f : align == TextAnchor.MiddleCenter ? 0.5f : 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, size * 1.4f);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = align; t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = shape ? ArabicShaper.Shape(text) : text;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        RectTransform Button(RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.05f, 0.45f);
            var t = Label(rt, label, arabic, 34, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, Ink, true);
            t.raycastTarget = false;
            return rt;
        }

        void CycleWeather()
        {
            preset = (preset + 1) % 4;
            Atmosphere.Apply((Atmosphere.Preset)preset);
            foreach (var l in car.GetComponentsInChildren<Light>(true)) l.enabled = preset == 3;
        }

        void ResetCar()
        {
            var rb = car.Body;
            Vector3 p = car.transform.position + Vector3.up * 1.2f;
            Vector3 fwd = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            rb.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        void Update()
        {
            totalTxt.text = scorer.Total.ToString("N0");
            if (scorer.Current > 0f)
            {
                currentTxt.text = Mathf.FloorToInt(scorer.Current).ToString("N0");
                multTxt.text = "x" + scorer.Multiplier;
            }
            else { currentTxt.text = ""; multTxt.text = ""; }

            speedTxt.text = Mathf.RoundToInt(car.SpeedKmh).ToString();
            gearTxt.text = car.Gear == -1 ? "R" : car.Gear.ToString();
            gearTxt.color = car.LimiterCut ? new Color(1f, 0.3f, 0.2f) : Gold;

            if (calloutT > 0f)
            {
                calloutT -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(calloutT / 0.4f);
                float pop = 1f + Mathf.Clamp01((calloutT - 1.3f) / 0.3f) * 0.35f;
                var c = calloutTxt.color; c.a = a; calloutTxt.color = c;
                calloutTxt.rectTransform.localScale = Vector3.one * pop;
            }

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.cKey.wasPressedThisFrame) cam.NextMode();
                if (kb.vKey.wasPressedThisFrame) cam.ToggleNearFar();
                if (kb.rKey.wasPressedThisFrame) ResetCar();
                if (kb.tKey.wasPressedThisFrame) GetComponent<TouchControls>().Toggle();
                if (kb.digit1Key.wasPressedThisFrame) { preset = 3; CycleWeather(); }
            }
#else
            if (Input.GetKeyDown(KeyCode.C)) cam.NextMode();
            if (Input.GetKeyDown(KeyCode.V)) cam.ToggleNearFar();
            if (Input.GetKeyDown(KeyCode.R)) ResetCar();
            if (Input.GetKeyDown(KeyCode.T)) GetComponent<TouchControls>().Toggle();
            if (Input.GetKeyDown(KeyCode.Alpha1)) { preset = 3; CycleWeather(); }
#endif
        }
    }
}
