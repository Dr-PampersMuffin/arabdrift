using UnityEngine;

namespace DriftKuwait
{
    /// Chase camera built for drifting: swings wide to show the car's angle, pulls back with
    /// speed (FOV 62 -> 78), punches in on near-misses, shakes on hits.
    /// Tap camera button = next mode; long-press = toggle near/far.
    public class DriftCamera : MonoBehaviour
    {
        public enum Mode { ChaseNear, ChaseFar, Bumper, Hood }
        public Rigidbody target;
        public Mode mode = Mode.ChaseNear;

        Camera cam;
        DriftCarController car;
        Vector3 travelDir, pos;
        float shake, punch, fov;
        bool initialised;

        Rigidbody boundTarget;

        void Start()
        {
            cam = GetComponent<Camera>();
            fov = 62f;
        }

        // The target changes when the player picks another car: re-bind and snap the camera behind it.
        void Bind()
        {
            boundTarget = target;
            if (target == null) return;
            car = target.GetComponent<DriftCarController>();
            if (car != null) car.OnImpact += (impulse, point) =>
            {
                if (impulse > 2500f) shake = Mathf.Max(shake, Mathf.Clamp01(impulse / 14000f));
            };
            travelDir = target.transform.forward;
            initialised = false;
        }

        public void NextMode() => mode = (Mode)(((int)mode + 1) % 4);
        public void ToggleNearFar() => mode = mode == Mode.ChaseNear ? Mode.ChaseFar : Mode.ChaseNear;
        public void Punch() => punch = 0.3f;

        void LateUpdate()
        {
            if (target != boundTarget) Bind();
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            Transform t = target.transform;
            Vector3 up = Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, up).normalized;
            Vector3 v = target.linearVelocity;
            float kmh = v.magnitude * 3.6f;
            float speedT = Mathf.InverseLerp(40f, 180f, kmh);

            Vector3 flatV = Vector3.ProjectOnPlane(v, up);
            Vector3 travel = flatV.magnitude > 3f && Vector3.Dot(flatV, fwd) > -0.2f ? flatV.normalized : fwd;
            travelDir = Vector3.Slerp(travelDir, travel, 1f - Mathf.Exp(-4f * dt));

            float driftAbs = Mathf.Abs(car.DriftAngle);
            float baseFov;

            if (mode == Mode.ChaseNear || mode == Mode.ChaseFar)
            {
                bool far = mode == Mode.ChaseFar;
                float swing = Mathf.Clamp01(driftAbs / 40f);
                // Look along the direction of travel more than the nose: you see the car's side in a slide.
                Vector3 lookDir = Vector3.Slerp(fwd, travelDir, 0.35f + swing * 0.4f).normalized;
                float dist = (far ? 8f : 5.5f) + speedT * 0.8f + (driftAbs > 60f ? 0.5f : 0f);
                float height = far ? 2.4f : 1.6f;
                Vector3 desired = t.position - lookDir * dist + up * height;

                if (!initialised) { pos = desired; initialised = true; }
                pos = Vector3.Lerp(pos, desired, 1f - Mathf.Exp(-9f * dt));
                // Never sink below the road.
                if (pos.y < t.position.y + 0.6f) pos.y = t.position.y + 0.6f;

                // Don't go through walls: pull in along the line from the car's roof to the camera.
                Vector3 pivot = t.position + up * 1.2f;
                Vector3 toCam = pos - pivot;
                Vector3 shown = pos;
                if (Physics.SphereCast(pivot, 0.3f, toCam.normalized, out var wallHit, toCam.magnitude, 1, QueryTriggerInteraction.Ignore)
                    && wallHit.rigidbody == null)
                    shown = pivot + toCam.normalized * Mathf.Max(0.8f, wallHit.distance - 0.1f);

                transform.position = shown;
                Vector3 look = t.position + up * 0.9f + fwd * 1.2f;
                transform.rotation = Quaternion.LookRotation(look - shown, up);
                baseFov = far ? Mathf.Lerp(58f, 72f, speedT) : Mathf.Lerp(62f, 78f, speedT);
            }
            else if (mode == Mode.Bumper)
            {
                transform.SetPositionAndRotation(t.TransformPoint(new Vector3(0f, 0.55f, 2.5f)), t.rotation);
                baseFov = 75f;
                initialised = false;
            }
            else
            {
                transform.SetPositionAndRotation(t.TransformPoint(new Vector3(-0.35f, 1.16f, 0.1f)),
                    t.rotation * Quaternion.Euler(0f, car.SteerAngle * 0.15f, 0f));
                baseFov = 68f;
                initialised = false;
            }

            // Near-miss punch-in with a touch of slow motion
            if (punch > 0f)
            {
                punch -= dt;
                baseFov = Mathf.Lerp(baseFov, 50f, 0.75f);
                Time.timeScale = 0.85f;
            }
            else if (Time.timeScale < 1f) Time.timeScale = 1f;

            fov = Mathf.Lerp(fov, baseFov, 1f - Mathf.Exp(-6f * dt));
            cam.fieldOfView = fov;

            if (shake > 0.001f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.18f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 3.5f);
            }
        }
    }
}
