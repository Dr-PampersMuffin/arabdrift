using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Shared building blocks for every screen: fonts, sprites from Resources/DriftKuwait/UI, Arabic labels
    /// (shaped and wrapped for Unity's left-to-right text), buttons, bars, and the colour palette from the blueprint.
    public static class UIKit
    {
        // Q8 DRIFT 1985 design language: deep navy and petrol-blue plates, warm cream and ivory type, mustard for
        // the primary action, and the racing tri-stripe (red / burnt orange / mustard) from 80s Gulf rally liveries.
        public static readonly Color Gold = new Color(0.97f, 0.72f, 0.16f);          // mustard
        public static readonly Color GoldDeep = new Color(0.90f, 0.42f, 0.10f);      // burnt orange
        public static readonly Color RacingRed = new Color(0.80f, 0.13f, 0.10f);
        public static readonly Color Petrol = new Color(0.05f, 0.30f, 0.36f);
        public static readonly Color Navy = new Color(0.035f, 0.06f, 0.11f);
        public static readonly Color Silver = new Color(0.78f, 0.80f, 0.82f);
        public static readonly Color Ink = new Color(0.035f, 0.06f, 0.11f, 0.88f);
        public static readonly Color InkSoft = new Color(0.035f, 0.06f, 0.11f, 0.62f);
        public static readonly Color Cream = new Color(0.97f, 0.93f, 0.85f);
        public static readonly Color Dust = new Color(0.85f, 0.73f, 0.54f);
        public static readonly Color SaduRed = new Color(0.62f, 0.17f, 0.15f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.16f);

        static Font arabic, arabicBlack, display, digital, retro;
        /// Vintage digital-instrument face for numbers and Latin labels (Orbitron, OFL).
        public static Font Digital => digital != null ? digital : (digital = LoadFont("Orbitron-Black"));
        /// 80s motorsport lettering for Latin titles (Audiowide, OFL).
        public static Font Retro => retro != null ? retro : (retro = LoadFont("Audiowide"));

        /// The racing tri-stripe: three slim bars (red, burnt orange, mustard). Vertical by default.
        public static RectTransform Stripe(Transform parent, Vector2 anchor, Vector2 pos, float length, float bar = 7f, bool horizontal = false)
        {
            var root = Rect(parent, "TriStripe");
            Place(root, anchor, pos, horizontal ? new Vector2(length, bar * 3 + 6) : new Vector2(bar * 3 + 6, length));
            Color[] cs = { RacingRed, GoldDeep, Gold };
            for (int i = 0; i < 3; i++)
            {
                var im = Img(root, "S" + i, null, cs[i]);
                var rt = im.rectTransform;
                if (horizontal) { rt.anchorMin = new Vector2(0, 1 - (i + 1) / 3f); rt.anchorMax = new Vector2(1, 1 - i / 3f); rt.offsetMin = new Vector2(0, 1); rt.offsetMax = new Vector2(0, -1); }
                else { rt.anchorMin = new Vector2(i / 3f, 0); rt.anchorMax = new Vector2((i + 1) / 3f, 1); rt.offsetMin = new Vector2(1, 0); rt.offsetMax = new Vector2(-1, 0); }
            }
            return root;
        }
        public static Font Arabic => arabic != null ? arabic : (arabic = LoadFont("Cairo-Bold"));
        public static Font Black => arabicBlack != null ? arabicBlack : (arabicBlack = LoadFont("Cairo-Black"));
        public static Font Display => display != null ? display : (display = LoadFont("Lalezar"));

        static Font LoadFont(string n)
        {
            var f = Resources.Load<Font>("DriftKuwait/Fonts/" + n);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// Sprite from Resources/DriftKuwait/UI/<path>. Panels get 9-slice borders so they stretch cleanly.
        public static Sprite S(string path)
        {
            if (sprites.TryGetValue(path, out var sp)) return sp;
            sp = Resources.Load<Sprite>("DriftKuwait/UI/" + path);
            if (sp == null)
            {
                var tex = Resources.Load<Texture2D>("DriftKuwait/UI/" + path);
                if (tex != null)
                {
                    Vector4 border = Vector4.zero;
                    if (path.Contains("panel") || path.Contains("pill")) border = new Vector4(40, 40, 40, 40);
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                }
            }
            sprites[path] = sp;
            return sp;
        }

        static Sprite Sliced(string path)
        {
            string key = path + "#9";
            if (sprites.TryGetValue(key, out var sp)) return sp;
            var tex = Resources.Load<Texture2D>("DriftKuwait/UI/" + path);
            if (tex == null) return null;
            float b = path.Contains("pill") ? 60 : path.Contains("sharp") ? 14 : 34;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprites[key] = sp;
            return sp;
        }

        // ---------- Canvas ----------
        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(2340, 1080);   // iPhone landscape
            sc.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        /// Safe-area container (notch and home bar) that fills the canvas.
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect(canvas, "SafeArea");
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        // ---------- Layout helpers ----------
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float b = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        /// Place by anchor (0..1), pivot = anchor, position offset in reference pixels, size in reference pixels.
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sp, Color c, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sp;
            im.color = c;
            im.raycastTarget = raycast;
            if (sp != null && sp.border != Vector4.zero) im.type = Image.Type.Sliced;
            else im.preserveAspect = false;
            return im;
        }

        public static Image Panel(Transform parent, string name, Color c, string kind = "hud/panel")
        {
            var im = Img(parent, name, Sliced(kind), c);
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 2f;
            return im;
        }

        public static Image Outline(Transform parent, Color c, string kind = "hud/panel_outline")
        {
            var im = Img(parent, "Outline", Sliced(kind), c);
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 2f;
            Stretch(im.rectTransform);
            return im;
        }

        /// A photo that fills its rect without stretching (crops the overflow), like CSS object-fit: cover.
        public static RawImage Cover(Transform parent, string name, Texture tex)
        {
            var rt = Rect(parent, name);
            var ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;
            rt.gameObject.AddComponent<CoverFit>();
            return ri;
        }

        // ---------- Text ----------
        public static Text Label(Transform parent, string name, string text, int size, Color c, TextAnchor align, Font font = null, bool arabicText = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : Arabic;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.text = arabicText ? Ar(text) : text;
            return t;
        }

        public static Shadow AddShadow(Graphic g, float d = 3f, float a = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, a);
            s.effectDistance = new Vector2(d, -d);
            return s;
        }

        /// Shape Arabic for display. Mixed strings work word-by-word; Latin-only strings pass through.
        public static string Ar(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            bool hasArabic = false;
            foreach (char c in s) if (c >= '؀' && c <= 'ۿ') { hasArabic = true; break; }
            return hasArabic ? ArabicShaper.Shape(s) : s;
        }

        /// Arabic text wrapped to lines of about maxChars (wrapping must happen before shaping, because shaped text is reversed).
        public static string ArWrap(string s, int maxChars)
        {
            var words = s.Split(' ');
            var lines = new List<string>();
            var cur = new StringBuilder();
            foreach (var w in words)
            {
                if (cur.Length > 0 && cur.Length + 1 + w.Length > maxChars) { lines.Add(cur.ToString()); cur.Clear(); }
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(w);
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++) { if (i > 0) sb.Append('\n'); sb.Append(Ar(lines[i])); }
            return sb.ToString();
        }

        static readonly char[] EastDigits = { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
        public static bool EasternNumerals = true;

        /// Instrument numbers (speed, gear, score, timers) always use the digital face's Western digits, like a 1985 dash.
        public static string Dig(long v, bool grouping = true) =>
            grouping ? v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// 4870 -> "٤٬٨٧٠" (Eastern Arabic digits, the blueprint's option, on by default).
        public static string Num(long v, bool grouping = true)
        {
            string s = grouping ? v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!EasternNumerals) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '0' && c <= '9') sb.Append(EastDigits[c - '0']);
                else if (c == ',') sb.Append('٬');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------- Controls ----------
        public static Button Button(Transform parent, string name, Color bg, System.Action onClick, string kind = "hud/panel")
        {
            var im = Panel(parent, name, bg, kind);
            im.raycastTarget = true;
            var b = im.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            b.colors = colors;
            b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.7f); onClick?.Invoke(); });
            im.gameObject.AddComponent<PressScale>();
            return b;
        }

        /// The menu button style from the reference: dark plate, icon on the right, Arabic title and small English under it,
        /// chevron on the left (RTL), gold fill when primary.
        public static Button MenuButton(Transform parent, string ar, string en, string icon, bool primary, System.Action onClick)
        {
            var b = Button(parent, "Btn_" + en, primary ? Gold : Ink, onClick, "hud/panel_sharp");
            var tr = b.transform;
            if (!primary) Outline(tr, new Color(0.97f, 0.93f, 0.85f, 0.18f), "hud/panel_sharp_outline");
            Stripe(tr, new Vector2(1f, 0.5f), new Vector2(0f, 0f), 112f * 0.78f, 5f);
            Color fg = primary ? Navy : Cream;
            var ic = Img(tr, "Icon", S("hud/ic_" + icon), fg);
            Place(ic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(60f, 60f));
            var t1 = Label(tr, "Ar", ar, 50, fg, TextAnchor.MiddleCenter, Black);
            Place(t1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(10f, 14f), new Vector2(360f, 70f));
            var t2 = Label(tr, "En", en.ToUpperInvariant(), 18, new Color(fg.r, fg.g, fg.b, 0.75f), TextAnchor.MiddleCenter, Digital, false);
            Place(t2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(10f, -30f), new Vector2(360f, 30f));
            var ch = Img(tr, "Chevron", S("hud/ic_chev_l"), fg);
            Place(ch.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(46f, 46f));
            return b;
        }

        public static Image Bar(Transform parent, string name, float value01, Color fill, Vector2 size)
        {
            var bg = Panel(parent, name, new Color(1f, 1f, 1f, 0.14f), "hud/pill");
            bg.rectTransform.sizeDelta = size;
            var f = Panel(bg.transform, "Fill", fill, "hud/pill");
            var rt = f.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return f;
        }

        public static void SetBar(Image fill, float value01)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
        }
    }

    /// Keeps UI inside the iPhone safe area (notch, Dynamic Island, home bar).
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// RawImage that covers its parent without stretching.
    [RequireComponent(typeof(RawImage))]
    public class CoverFit : MonoBehaviour
    {
        public Vector2 focus = new Vector2(0.5f, 0.5f);
        RawImage ri;
        Vector2 lastSize;
        void Awake() { ri = GetComponent<RawImage>(); }
        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null || ri.texture == null) return;
            if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one) UIKit.Stretch(rt);
            Vector2 size = parent.rect.size;
            if (size == lastSize) return;
            lastSize = size;
            float texA = ri.texture.width / (float)ri.texture.height, a = size.x / Mathf.Max(1f, size.y);
            Rect uv = new Rect(0, 0, 1, 1);
            if (a > texA) { float h = texA / a; uv = new Rect(0, (1 - h) * focus.y, 1, h); }
            else { float w = a / texA; uv = new Rect((1 - w) * focus.x, 0, w, 1); }
            ri.uvRect = uv;
        }
    }

    /// Small press feedback on buttons.
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e) => transform.localScale = Vector3.one * 0.96f;
        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
    }

    /// One-shot UI and voice sounds from Resources/DriftKuwait/Audio, plus the music player with crossfades.
    public static class Sfx
    {
        static AudioSource oneShot, voice;
        static AudioSource[] music = new AudioSource[2];
        static int musicIdx;
        static string currentMusic;
        public static float MusicVolume = 0.55f, SfxVolume = 1f;

        static System.Collections.Generic.Dictionary<string, string> lines;
        /// Subtitle for a voice line, from Resources/DriftKuwait/voice_lines.json (the same script the voices were
        /// recorded from), so what is heard and what is read always match.
        public static string Line(string key, string fallback)
        {
            if (lines == null)
            {
                lines = new System.Collections.Generic.Dictionary<string, string>();
                var ta = Resources.Load<TextAsset>("DriftKuwait/voice_lines");
                if (ta != null)
                    foreach (Match m in Regex.Matches(ta.text, "\"([a-z0-9_]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                        lines[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return key != null && lines.TryGetValue(key, out var t) ? t : fallback;
        }

        static GameObject Host()
        {
            var go = GameObject.Find("DK_Audio");
            if (go == null) { go = new GameObject("DK_Audio"); Object.DontDestroyOnLoad(go); }
            return go;
        }

        public static void Play(string key, float vol = 1f)
        {
            var clip = Resources.Load<AudioClip>("DriftKuwait/Audio/" + key);
            if (clip == null) return;
            if (oneShot == null) { oneShot = Host().AddComponent<AudioSource>(); oneShot.spatialBlend = 0f; }
            oneShot.PlayOneShot(clip, vol * SfxVolume);
        }

        /// Plays a voice line (Audio/Voice/<key>), interrupting the previous one. Returns its length in seconds.
        public static float Voice(string key, float vol = 1f)
        {
            var clip = Resources.Load<AudioClip>("DriftKuwait/Audio/Voice/" + key);
            if (clip == null) return 0f;
            if (voice == null) { voice = Host().AddComponent<AudioSource>(); voice.spatialBlend = 0f; voice.priority = 0; }
            voice.Stop();
            voice.clip = clip;
            voice.volume = vol * SfxVolume;
            voice.Play();
            return clip.length;
        }

        public static void StopVoice() { if (voice != null) voice.Stop(); }

        public static void Music(string key, float fade = 1.5f)
        {
            if (key == currentMusic) return;
            currentMusic = key;
            var host = Host();
            if (music[0] == null)
                for (int i = 0; i < 2; i++) { music[i] = host.AddComponent<AudioSource>(); music[i].loop = true; music[i].spatialBlend = 0f; music[i].volume = 0f; }
            var clip = key == null ? null : Resources.Load<AudioClip>("DriftKuwait/Audio/Music/" + key);
            var from = music[musicIdx];
            musicIdx = 1 - musicIdx;
            var to = music[musicIdx];
            to.clip = clip;
            if (clip != null) to.Play();
            var fader = host.GetComponent<MusicFader>() ?? host.AddComponent<MusicFader>();
            fader.Fade(from, to, fade);
        }

        public static float MusicDuck = 1f;   // lowered while voice lines play
    }

    public class MusicFader : MonoBehaviour
    {
        AudioSource from, to;
        float t, dur = 1f;
        public void Fade(AudioSource a, AudioSource b, float d) { from = a; to = b; t = 0f; dur = Mathf.Max(0.05f, d); }
        void Update()
        {
            if (to == null) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float target = Sfx.MusicVolume * Sfx.MusicDuck;
            if (from != null && from != to) { from.volume = (1f - k) * target; if (k >= 1f) from.Stop(); }
            to.volume = k * target;
        }
    }
}
