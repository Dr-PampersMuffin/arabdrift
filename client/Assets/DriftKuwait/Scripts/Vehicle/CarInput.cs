using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DriftKuwait
{
    /// Reads keyboard, gamepad and on-screen touch controls into one set of values.
    public class CarInput : MonoBehaviour
    {
        public float Steer { get; private set; }      // -1 left .. +1 right
        public float Throttle { get; private set; }   // 0..1
        public float Brake { get; private set; }      // 0..1
        public bool Handbrake { get; private set; }

        // Written by TouchControls every frame.
        public static float TouchSteer, TouchThrottle, TouchBrake;
        public static bool TouchHandbrake;

        float steerSmoothed;

        void Update()
        {
            float steer = 0, thr = 0, brk = 0; bool hb = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) thr = 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brk = 1;
                if (kb.spaceKey.isPressed) hb = true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                float gs = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(gs) > 0.08f) steer = gs;
                thr = Mathf.Max(thr, gp.rightTrigger.ReadValue());
                brk = Mathf.Max(brk, gp.leftTrigger.ReadValue());
                hb |= gp.buttonSouth.isPressed || gp.rightShoulder.isPressed;
            }
#else
            steer = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            thr = Mathf.Max(0, v); brk = Mathf.Max(0, -v);
            hb = Input.GetKey(KeyCode.Space);
#endif
            if (Mathf.Abs(TouchSteer) > Mathf.Abs(steer)) steer = TouchSteer;
            thr = Mathf.Max(thr, TouchThrottle);
            brk = Mathf.Max(brk, TouchBrake);
            hb |= TouchHandbrake;

            // Digital steering feels harsh on a phone: ramp in, snap back faster.
            float rate = (Mathf.Abs(steer) > Mathf.Abs(steerSmoothed) && Mathf.Sign(steer) == Mathf.Sign(steerSmoothed)) ? 4.5f : 7f;
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, steer, rate * Time.deltaTime);

            Steer = steerSmoothed;
            Throttle = thr;
            Brake = brk;
            Handbrake = hb;
        }
    }
}
