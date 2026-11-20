using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// A button that reports hold state, tap and long-press. Multi-touch safe.
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Pressed { get; private set; }
        public Action onTap, onLongPress;
        float downTime;
        bool longFired;
        Image img;
        Color baseCol;

        void Awake() { img = GetComponent<Image>(); if (img != null) baseCol = img.color; }

        public void OnPointerDown(PointerEventData e)
        {
            Pressed = true; downTime = Time.unscaledTime; longFired = false;
            if (img != null) img.color = baseCol + new Color(0.25f, 0.2f, 0.1f, 0.2f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (Pressed && !longFired) onTap?.Invoke();
            Release();
        }

        public void OnPointerExit(PointerEventData e) => Release();

        void Release()
        {
            Pressed = false;
            if (img != null) img.color = baseCol;
        }

        void Update()
        {
            if (Pressed && !longFired && onLongPress != null && Time.unscaledTime - downTime > 0.5f)
            {
                longFired = true;
                onLongPress();
            }
        }
    }
}
