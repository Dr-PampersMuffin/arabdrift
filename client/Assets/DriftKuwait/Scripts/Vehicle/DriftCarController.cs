using System;
using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Raycast-suspension car with a slip-angle tyre model, friction circle (power oversteer),
    /// turbo lag, auto gearbox, rev limiter and counter-steer assist. Tuned for الشاهين.
    [RequireComponent(typeof(Rigidbody))]
    public class DriftCarController : MonoBehaviour
    {
        [Serializable]
        public class Wheel
        {
            public Transform visual;
            public Vector3 localMount;
            public bool front, left;
            [NonSerialized] public bool grounded;
            [NonSerialized] public Vector3 contactPoint, contactNormal;
            [NonSerialized] public Collider surface;
            [NonSerialized] public float compression, lastCompression, load, spinDeg, skid, latSpeed;
            [NonSerialized] public float gripMul = 1f;   // tyre condition (TyreWear): 1 = fresh, lower = shredded / blown
        }

        public Wheel[] wheels;

        [Header("Suspension")]
        public float restLength = 0.30f;
        public float springRate = 42000f;
        public float damperRate = 3800f;
        public float antiRoll = 9000f;
        public float wheelRadius = 0.31f;

        [Header("Tyres")]
        public float frontGrip = 1.15f;
        public float rearGrip = 1.0f;
        public float handbrakeGripFactor = 0.42f;
        public float latB = 8f;     // slip-curve stiffness
        public float latC = 1.6f;   // shape: >1 means grip falls after the peak (slides hold)

        [Header("Engine (turbo four)")]
        public float idleRpm = 900f, maxRpm = 7200f;
        public float baseTorque = 175f;      // Nm off boost
        public float boostTorque = 0.55f;    // +55% on full boost
        public float gameplayPower = 1.5f;   // mobile fun factor
        public float turboSpoolTime = 0.55f; // the lag, then the kick
        public float[] gears = { 3.32f, 1.90f, 1.30f, 1.00f, 0.80f };
        public float finalDrive = 3.9f, reverseRatio = 3.4f, drivetrainEff = 0.85f;
        public float brakeForce = 9000f;

        [Header("Steering")]
        public float maxSteerLow = 38f, maxSteerHigh = 17f, maxDriftSteer = 46f, steerSpeedRef = 38f;
        [Range(0, 1)] public float counterSteerAssist = 0.75f;

        // Read by camera, HUD, audio, FX, scorer.
        public float Rpm { get; private set; }
        public int Gear { get; private set; } = 1;   // -1 = reverse
        public int GearCount => gears.Length;
        /// Raised on a manual shift (true = up), for the shift sound and HUD flash.
        public event System.Action<bool> Shifted;
        public float SpeedKmh { get; private set; }
        public float DriftAngle { get; private set; } // signed, degrees
        public float Boost { get; private set; }
        public float ThrottleInput { get; private set; }
        public bool LimiterCut { get; private set; }
        public bool Handbrake { get; private set; }
        public float RearSlip { get; private set; }   // 0..1, drives smoke + squeal
        public float RearSpin => rearSpin;
        public int GroundedCount { get; private set; }
        public float SteerAngle => steer;
        public Rigidbody Body => rb;

        public event Action<float, Vector3> OnImpact; // impulse, point

        Rigidbody rb;
        CarInput input;
        float steer, rearSpin, shiftTimer, reverseTimer, limiterTimer, flickT;
        int groundMask;
        readonly Dictionary<int, float> gripCache = new Dictionary<int, float>();

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<CarInput>();
            if (input == null) input = gameObject.AddComponent<CarInput>();
            groundMask = ~(1 << 2); // everything but Ignore Raycast (the car lives there)
            Rpm = idleRpm;
        }

        float SurfaceGrip(Collider c)
        {
            if (c == null) return 1f;
            int id = c.GetInstanceID();
            if (gripCache.TryGetValue(id, out float g)) return g;
            var s = c.GetComponent<SurfaceType>();
            g = s != null ? s.grip : 1f;
            gripCache[id] = g;
            return g;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            float fwdSpeed = Vector3.Dot(v, transform.forward);
            SpeedKmh = v.magnitude * 3.6f;

            Vector3 flatV = Vector3.ProjectOnPlane(v, transform.up);
            float da = flatV.magnitude > 2f ? Vector3.SignedAngle(transform.forward, flatV, transform.up) : 0f;
            DriftAngle = Mathf.Abs(da) > 120f ? 0f : da; // rolling backwards is not a drift

            // ---------- Gear selection ----------
            bool manual = GameSettings.Manual && !input.ai && !PreviewHooks.Active;
            if (!input.ai)
            {
                if (CarInput.ShiftUpRequest)
                {
                    CarInput.ShiftUpRequest = false;
                    if (Gear == -1) Gear = 1;
                    else if (manual && Gear < gears.Length) { Gear++; shiftTimer = 0.18f; Shifted?.Invoke(true); }
                }
                if (CarInput.ShiftDownRequest)
                {
                    CarInput.ShiftDownRequest = false;
                    if (Gear == 1 && fwdSpeed < 1.5f) Gear = -1;
                    else if (manual && Gear > 1) { Gear--; shiftTimer = 0.18f; Shifted?.Invoke(false); }
                }
            }
            if (!manual && input.Brake > 0.5f && fwdSpeed < 1f && input.Throttle < 0.1f && Gear != -1)
            {
                reverseTimer += dt;
                if (reverseTimer > 0.35f) { Gear = -1; reverseTimer = 0; }
            }
            else reverseTimer = 0;
            if (!manual && Gear == -1 && input.Throttle > 0.1f && fwdSpeed > -1f) Gear = 1;

            // Auto: reverse is driven by holding the brake. Manual: reverse is a gear, the gas pedal drives it.
            bool autoRev = Gear == -1 && !manual;
            float throttle = autoRev ? input.Brake : input.Throttle;
            float brake = autoRev ? input.Throttle : input.Brake;
            if (Handbrake && !input.Handbrake) flickT = 0.6f;   // just let go of the handbrake: let the slide develop
            flickT -= dt;
            Handbrake = input.Handbrake;
            ThrottleInput = throttle;

            // ---------- Steering with counter-steer assist ----------
            // Gripping: less lock as speed rises (stable on the straight). Sliding: the full lock opens up so the
            // driver can counter-steer as far as the slide needs; the assist only nudges toward the right angle.
            float speedT = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / steerSpeedRef);
            float driftT = Gear != -1 ? Mathf.Clamp01((Mathf.Abs(DriftAngle) - 6f) / 14f) : 0f;
            float maxSteer = Mathf.Lerp(Mathf.Lerp(maxSteerLow, maxSteerHigh, speedT), maxDriftSteer, Mathf.Max(driftT, Handbrake ? 0.55f : 0f));
            float target = input.Steer * maxSteer;
            if (driftT > 0f)
            {
                float cs = Mathf.Clamp(DriftAngle, -maxDriftSteer, maxDriftSteer) + input.Steer * maxSteerHigh;
                target = Mathf.Clamp(Mathf.Lerp(target, cs, counterSteerAssist * 0.7f * driftT), -maxDriftSteer, maxDriftSteer);
            }
            steer = Mathf.MoveTowards(steer, target, (Handbrake ? 420f : 300f) * dt);

            // ---------- Engine ----------
            float ratio = Gear == -1 ? reverseRatio : gears[Gear - 1];
            float rearSurface = Mathf.Abs(fwdSpeed) + rearSpin;
            float wheelRpm = rearSurface / (2f * Mathf.PI * wheelRadius) * 60f;
            float floorRpm = idleRpm + throttle * 2600f * (1f - Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 8f)); // clutch slip off the line
            float targetRpm = Mathf.Max(wheelRpm * ratio * finalDrive, floorRpm);
            Rpm = Mathf.Lerp(Rpm, targetRpm, 1f - Mathf.Exp(-12f * dt));

            LimiterCut = false;
            if (Rpm >= maxRpm)
            {
                Rpm = maxRpm;
                limiterTimer += dt;
                LimiterCut = (limiterTimer % 0.1f) < 0.05f; // bounce
            }
            else limiterTimer = 0;

            shiftTimer -= dt;
            if (Gear > 0 && shiftTimer <= 0f && !manual)
            {
                if (Rpm > maxRpm - 350f && Gear < gears.Length && rearSpin < 3f) { Gear++; shiftTimer = 0.35f; }
                else if (Rpm < 2900f && Gear > 1 && !Handbrake) { Gear--; shiftTimer = 0.35f; }
            }
            bool shifting = shiftTimer > 0.15f;

            bool spooling = throttle > 0.6f && Rpm > 2800f;
            Boost = Mathf.MoveTowards(Boost, spooling ? 1f : 0f, dt / (spooling ? turboSpoolTime : 0.25f));

            float curve = 0.55f + 0.45f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((Rpm - 1000f) / 6000f));
            float engineTorque = baseTorque * curve * (1f + boostTorque * Boost) * gameplayPower;
            if (LimiterCut || shifting) engineTorque = 0f;
            float driveTotal = engineTorque * throttle * ratio * finalDrive * drivetrainEff / wheelRadius;
            float drivePerRear = (Gear == -1 ? -1f : 1f) * driveTotal * 0.5f;

            // ---------- Suspension pass ----------
            GroundedCount = 0;
            foreach (var w in wheels)
            {
                Vector3 mount = transform.TransformPoint(w.localMount);
                if (Physics.Raycast(mount, -transform.up, out RaycastHit hit, restLength + wheelRadius, groundMask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = true;
                    w.contactPoint = hit.point;
                    w.contactNormal = hit.normal;
                    w.surface = hit.collider;
                    w.compression = Mathf.Max(0f, restLength - (hit.distance - wheelRadius));
                    float compVel = (w.compression - w.lastCompression) / dt;
                    w.load = Mathf.Max(0f, w.compression * springRate + compVel * damperRate);
                    GroundedCount++;
                }
                else
                {
                    w.grounded = false; w.compression = 0f; w.load = 0f; w.surface = null;
                }
                w.lastCompression = w.compression;
            }

            // Anti-roll bars (front pair = 0,1 ; rear pair = 2,3)
            ApplyAntiRoll(wheels[0], wheels[1]);
            ApplyAntiRoll(wheels[2], wheels[3]);

            // ---------- Tyre forces ----------
            float spinTarget = 0f;
            float rearSkidSum = 0f;
            foreach (var w in wheels)
            {
                Vector3 mount = transform.TransformPoint(w.localMount);
                if (!w.grounded) { w.skid = 0; continue; }
                rb.AddForceAtPosition(transform.up * w.load, mount);

                Vector3 n = w.contactNormal;
                Vector3 heading = w.front ? Quaternion.AngleAxis(steer, transform.up) * transform.forward : transform.forward;
                Vector3 fwd = Vector3.ProjectOnPlane(heading, n).normalized;
                Vector3 right = Vector3.Cross(n, fwd);

                Vector3 pv = rb.GetPointVelocity(w.contactPoint);
                float vLong = Vector3.Dot(pv, fwd);
                float vLat = Vector3.Dot(pv, right);
                w.latSpeed = vLat;

                float grip = (w.front ? frontGrip : rearGrip) * SurfaceGrip(w.surface);
                grip *= w.gripMul;
                // Handbrake locks the rears: they lose most of their side grip so the tail steps out right away
                if (!w.front && Handbrake) grip *= handbrakeGripFactor * 0.68f;
                float fMax = grip * w.load;

                // Lateral: slip angle -> force, past the peak the grip drops so slides can be held.
                float alpha = Mathf.Atan2(vLat, Mathf.Max(Mathf.Abs(vLong), 0.6f));
                float fy = -Mathf.Sin(latC * Mathf.Atan(latB * alpha)) * fMax;
                float lowT = 1f - Mathf.Clamp01(pv.magnitude / 3f);
                if (lowT > 0f)
                {
                    float damp = Mathf.Clamp(-vLat * rb.mass * 0.25f / dt * 0.2f, -fMax, fMax);
                    fy = Mathf.Lerp(fy, damp, lowT);
                }

                // Longitudinal
                float fx = 0f;
                if (!w.front) fx += drivePerRear;
                float stopT = Mathf.Clamp01(Mathf.Abs(vLong) / 1.5f);
                if (brake > 0f) fx -= Mathf.Sign(vLong) * brakeForce * 0.25f * brake * stopT * (w.front ? 1.2f : 0.8f);
                if (!w.front && Handbrake) fx -= Mathf.Sign(vLong) * fMax * 0.35f * stopT;   // a flick, not an anchor: the slide keeps its speed
                fx -= vLong * 12f;                                    // rolling resistance
                if (!w.front && throttle < 0.05f) fx -= vLong * 35f;   // engine braking

                // Friction circle, longitudinal first: flooring it steals rear lateral grip = power oversteer.
                float demand = fx;
                fx = Mathf.Clamp(fx, -fMax, fMax);
                float latCap = Mathf.Max(Mathf.Sqrt(Mathf.Max(0f, fMax * fMax - fx * fx)), fMax * 0.25f);
                fy = Mathf.Clamp(fy, -latCap, latCap);

                if (!w.front)
                {
                    float excess = Mathf.Abs(demand) - fMax;
                    if (excess > 0f && throttle > 0.1f) spinTarget += excess / Mathf.Max(fMax, 1f) * 9f;
                }

                rb.AddForceAtPosition(fwd * fx + right * fy, w.contactPoint + transform.up * 0.12f);

                float slide = Mathf.Clamp01((Mathf.Abs(vLat) - 2.5f) / 6f);
                float spin = w.front ? 0f : Mathf.Clamp01(rearSpin / 8f);
                float lockSlide = (!w.front && Handbrake) ? Mathf.Clamp01(Mathf.Abs(vLong) / 10f) : 0f;
                w.skid = Mathf.Clamp01(Mathf.Max(slide, Mathf.Max(spin, lockSlide)));
                if (!w.front) rearSkidSum += w.skid;

                float surf = w.front ? vLong : vLong + (Gear == -1 ? -1f : 1f) * rearSpin;
                if (!w.front && Handbrake) surf = 0f;
                w.spinDeg += surf / wheelRadius * Mathf.Rad2Deg * dt;
            }

            rearSpin = Mathf.Lerp(rearSpin, spinTarget * 0.5f, 1f - Mathf.Exp(-6f * dt));
            RearSlip = rearSkidSum * 0.5f;

            // ---------- Drift stabiliser (what makes a slide holdable on a phone) ----------
            if (GroundedCount >= 3 && Gear != -1 && SpeedKmh > 18f && driftT > 0f)
            {
                float a = DriftAngle, absA = Mathf.Abs(a);
                float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
                // 1) Damp the yaw rate so the tail swings smoothly instead of snapping into a spin
                rb.AddTorque(-transform.up * yawRate * Mathf.Lerp(0.6f, 2.4f, counterSteerAssist) * driftT * (Handbrake ? 0.2f : flickT > 0f ? 0.4f : 1f), ForceMode.Acceleration);
                // 2) Past ~55 degrees the nose is pulled back toward the direction of travel (no spin-outs unless you really mean it)
                float limit = Mathf.Lerp(70f, 52f, counterSteerAssist);
                if (absA > limit) rb.AddTorque(transform.up * Mathf.Sign(a) * (absA - limit) * 0.09f, ForceMode.Acceleration);
                // 3) Throttle steers the angle: on the gas the tail stays out, off the gas the car straightens
                if (absA < 42f) rb.AddTorque(-transform.up * Mathf.Sign(a) * (throttle - 0.35f) * 1.1f * driftT, ForceMode.Acceleration);
                // 4) A committed, on-throttle slide keeps its momentum instead of scrubbing to a stop
                if (throttle > 0.3f && flatV.sqrMagnitude > 1f)
                    rb.AddForce(flatV.normalized * throttle * 2.2f * driftT, ForceMode.Acceleration);
            }

            // ---------- Handbrake entry ----------
            // Real e-brake entry: locked rears + steering = the car pivots round the front axle. The tyre model alone
            // made it lazy on a phone (no weight transfer feel), so add the rotation a real flick gives, toward the
            // steering, fading out as the angle builds so it hands over to throttle and counter-steer.
            if (Handbrake && GroundedCount >= 3 && Gear != -1 && SpeedKmh > 15f && Mathf.Abs(input.Steer) > 0.15f)
            {
                float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
                float absA = Mathf.Abs(DriftAngle);
                float speedK = Mathf.Clamp01((SpeedKmh - 15f) / 35f);
                float fade = Mathf.Clamp01(1f - absA / 48f);
                float dir = Mathf.Sign(input.Steer);
                if (yawRate * dir < 3.2f)   // cap the swing: no instant spins
                    rb.AddTorque(transform.up * input.Steer * 12f * speedK * fade, ForceMode.Acceleration);
                // weight goes onto the nose: a touch of extra front bite while the tail swings
                rb.AddForceAtPosition(-transform.up * rb.mass * 1.5f * speedK, transform.TransformPoint(0f, 0f, 1.2f), ForceMode.Force);
            }

            // Light downforce keeps it planted over kerbs.
            rb.AddForce(-transform.up * v.sqrMagnitude * 0.6f);
        }

        void ApplyAntiRoll(Wheel a, Wheel b)
        {
            float diff = (a.compression - b.compression) * antiRoll;
            if (a.grounded) a.load = Mathf.Max(0f, a.load + diff);
            if (b.grounded) b.load = Mathf.Max(0f, b.load - diff);
        }

        void LateUpdate()
        {
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                float drop = w.grounded ? restLength - w.compression : restLength;
                w.visual.localPosition = w.localMount - Vector3.up * drop;
                float yaw = w.front ? steer : 0f;
                w.visual.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(w.spinDeg % 360f, 0f, 0f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.contactCount > 0) OnImpact?.Invoke(c.impulse.magnitude, c.GetContact(0).point);
        }

        public Wheel GetWheel(int i) => wheels[i];
    }
}
