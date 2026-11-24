using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// A voiced, illustrated story scene: full-screen painted panels with a slow push-in, the speaker's name and the
    /// subtitle, the Kuwaiti voice line. Tap to move on; skip to leave.
    public class StoryScene : MonoBehaviour
    {
        public struct Beat
        {
            public string panel, voice, speaker, caption;   // caption: who is in the picture (name plate, top right)
            public Beat(string panel, string voice, string speaker, string caption = null) { this.panel = panel; this.voice = voice; this.speaker = speaker; this.caption = caption; }
        }

        Beat[] beats;
        System.Action done;
        RawImage img;
        Text who, line, cap;
        GameObject capPlate;
        int index = -1;
        float beatTime, beatLen;
        bool tapped;

        public static void Play(Beat[] beats, System.Action done)
        {
            var c = UIKit.MakeCanvas("DK_Story", 90);
            var s = c.gameObject.AddComponent<StoryScene>();
            s.beats = beats; s.done = done;
            s.Build(c.transform);
            Sfx.Music("mus_garage", 1.5f);
            s.StartCoroutine(s.Run());
        }

        void Build(Transform root)
        {
            var bg = UIKit.Img(root, "Black", null, Color.black, true);
            UIKit.Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => tapped = true);
            img = UIKit.Cover(root, "Panel", null);
            UIKit.Stretch(img.rectTransform);

            // Bottom band: gradient-like dark plate, speaker in mustard, the line in cream
            var band = UIKit.Img(root, "Band", null, new Color(0.02f, 0.03f, 0.06f, 0.78f));
            band.rectTransform.anchorMin = new Vector2(0f, 0f); band.rectTransform.anchorMax = new Vector2(1f, 0f);
            band.rectTransform.pivot = new Vector2(0.5f, 0f); band.rectTransform.sizeDelta = new Vector2(0f, 250f); band.rectTransform.anchoredPosition = Vector2.zero;
            var safe = UIKit.SafeArea(root);
            UIKit.Stripe(safe, new Vector2(1f, 0f), new Vector2(-60f, 206f), 220f, 5f, true);
            who = UIKit.Label(safe, "Who", "", 46, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(who.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 140f), new Vector2(700f, 60f));
            line = UIKit.Label(safe, "Line", "", 36, UIKit.Cream, TextAnchor.UpperRight, UIKit.Arabic);
            line.lineSpacing = 1.15f;
            UIKit.Place(line.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 22f), new Vector2(1900f, 110f));
            UIKit.AddShadow(line, 2f, 0.7f);

            // Name plate: who is in the picture
            var cp = UIKit.Panel(safe, "Caption", new Color(0.02f, 0.03f, 0.06f, 0.8f), "hud/panel_sharp");
            UIKit.Place(cp.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(900f, 84f));
            UIKit.Stripe(cp.transform, new Vector2(1f, 0f), new Vector2(-20f, 8f), 160f, 4f, true);
            cap = UIKit.Label(cp.transform, "T", "", 40, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(cap.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 4f), new Vector2(860f, 64f));
            cap.resizeTextForBestFit = true; cap.resizeTextMinSize = 28; cap.resizeTextMaxSize = 42;
            capPlate = cp.gameObject;

            var skip = UIKit.Button(safe, "Skip", UIKit.Ink, Finish, "hud/pill");
            UIKit.Place((RectTransform)skip.transform, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(170f, 64f));
            var st = UIKit.Label(skip.transform, "T", "تخطّي", 28, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(st.rectTransform);
        }

        IEnumerator Run()
        {
            for (index = 0; index < beats.Length; index++)
            {
                var b = beats[index];
                img.texture = Resources.Load<Texture2D>("DriftKuwait/Story/" + b.panel);
                img.rectTransform.localScale = Vector3.one;
                who.text = UIKit.Ar(b.speaker);
                capPlate.SetActive(!string.IsNullOrEmpty(b.caption));
                cap.text = string.IsNullOrEmpty(b.caption) ? "" : UIKit.Ar(b.caption);
                line.text = UIKit.ArWrap(Sfx.Line(b.voice, ""), 70);
                beatLen = PreviewHooks.Active ? 600f : Mathf.Max(3f, Sfx.Voice(b.voice, 1f) + 0.8f);
                beatTime = 0f; tapped = false;
                while (beatTime < beatLen && !tapped)
                {
                    beatTime += Time.unscaledDeltaTime;
                    img.rectTransform.localScale = Vector3.one * (1f + 0.06f * beatTime / beatLen);   // slow push-in
                    yield return null;
                }
                Sfx.StopVoice();
            }
            Finish();
        }

        void Finish()
        {
            if (this == null || gameObject == null) return;
            StopAllCoroutines();
            Sfx.StopVoice();
            var d = done; done = null;
            Destroy(gameObject);
            d?.Invoke();
        }
    }
}
