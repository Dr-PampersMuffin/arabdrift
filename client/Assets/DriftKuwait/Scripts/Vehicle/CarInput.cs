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
        /// Manual gearbox: set true for one shift (paddles, keys, pad); the car consumes it.
        public static bool ShiftUpRequest, ShiftDownRequest;

        // AI drivers (الدورية) write these instead of reading the player's controls.
        public bool ai;
        public float aiSteer, aiThrottle, aiBrake;
        public bool aiHandbrake;

        float steerSmoothed;

        void Update()
        {
            if (ai)
            {
                Steer = Mathf.MoveTowards(Steer, Mathf.Clamp(aiSteer, -1f, 1f), 5f * Time.deltaTime);
                Throttle = Mathf.Clamp01(aiThrottle); Brake = Mathf.Clamp01(aiBrake); Handbrake = aiHandbrake;
                return;
            }
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
                if (kb.eKey.wasPressedThisFrame) ShiftUpRequest = true;
                if (kb.qKey.wasPressedThisFrame) ShiftDownRequest = true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                float gs = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(gs) > 0.08f) steer = gs;
                thr = Mathf.Max(thr, gp.rightTrigger.ReadValue());
                brk = Mathf.Max(brk, gp.leftTrigger.ReadValue());
                hb |= gp.buttonSouth.isPressed;
                if (gp.rightShoulder.wasPressedThisFrame) ShiftUpRequest = true;
                if (gp.leftShoulder.wasPressedThisFrame) ShiftDownRequest = true;
            }
#else
            steer = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            thr = Mathf.Max(0, v); brk = Mathf.Max(0, -v);
            hb = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.E)) ShiftUpRequest = true;
            if (Input.GetKeyDown(KeyCode.Q)) ShiftDownRequest = true;
#endif
            if (GameSettings.Layout == GameSettings.Controls.Tilt && !PreviewHooks.Active)
            {
#if !ENABLE_INPUT_SYSTEM
                float tilt = Input.acceleration.x * Mathf.Lerp(1.4f, 3.6f, GameSettings.SteerSens);
                if (Mathf.Abs(tilt) > 0.06f) steer = Mathf.Clamp(tilt, -1f, 1f);
#endif
            }
            if (Mathf.Abs(TouchSteer) > Mathf.Abs(steer)) steer = TouchSteer;
            thr = Mathf.Max(thr, TouchThrottle);
            brk = Mathf.Max(brk, TouchBrake);
            hb |= TouchHandbrake;

            // Digital steering feels harsh on a phone: ramp in, snap back faster.
            float rate = (Mathf.Abs(steer) > Mathf.Abs(steerSmoothed) && Mathf.Sign(steer) == Mathf.Sign(steerSmoothed)) ? 4.5f : 7f;
            if (GameSettings.Layout != GameSettings.Controls.Buttons) rate = GameSettings.Layout == GameSettings.Controls.Wheel ? 28f : 14f;   // wheel: near-instant; tilt: close
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, steer, rate * Time.deltaTime);

            Steer = steerSmoothed;
            Throttle = thr;
            Brake = brk;
            Handbrake = hb;
        }
    }
}
