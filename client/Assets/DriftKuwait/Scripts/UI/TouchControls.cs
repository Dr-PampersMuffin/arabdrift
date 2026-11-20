using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Layout A from the design doc: steering on the left thumb, pedals + a big handbrake on the right.
    public class TouchControls : MonoBehaviour
    {
        HoldButton left, right, gas, brake, hand;
        GameObject group;

        public void Build(RectTransform parent, Font arabic)
        {
            group = new GameObject("TouchControls", typeof(RectTransform));
            var g = group.GetComponent<RectTransform>();
            g.SetParent(parent, false);
            g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one;
            g.offsetMin = g.offsetMax = Vector2.zero;

            left  = Make(g, "\u25C4", null, new Vector2(0, 0), new Vector2(150, 150), new Vector2(230, 230));
            right = Make(g, "\u25BA", null, new Vector2(0, 0), new Vector2(410, 150), new Vector2(230, 230));
            brake = Make(g, "بريك", arabic, new Vector2(1, 0), new Vector2(-640, 140), new Vector2(200, 200));
            hand  = Make(g, "هاند", arabic, new Vector2(1, 0), new Vector2(-420, 300), new Vector2(240, 160));
            gas   = Make(g, "بنزين", arabic, new Vector2(1, 0), new Vector2(-420, 120), new Vector2(240, 220));
        }

        HoldButton Make(RectTransform parent, string label, Font arabic, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Touch_" + label, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.14f);
            var tgo = new GameObject("Label", typeof(RectTransform));
            var trt = tgo.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<Text>();
            t.font = arabic != null ? arabic : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 46; t.alignment = TextAnchor.MiddleCenter; t.color = new Color(1f, 1f, 1f, 0.85f);
            t.text = arabic != null ? ArabicShaper.Shape(label) : label;
            t.raycastTarget = false;
            return go.AddComponent<HoldButton>();
        }

        public void SetVisible(bool v) { if (group != null) group.SetActive(v); }
        public void Toggle() { if (group != null) group.SetActive(!group.activeSelf); }

        void Update()
        {
            bool on = group != null && group.activeInHierarchy;
            CarInput.TouchSteer = on ? (right.Pressed ? 1f : 0f) - (left.Pressed ? 1f : 0f) : 0f;
            CarInput.TouchThrottle = on && gas.Pressed ? 1f : 0f;
            CarInput.TouchBrake = on && brake.Pressed ? 1f : 0f;
            CarInput.TouchHandbrake = on && hand.Pressed;
        }
    }
}
