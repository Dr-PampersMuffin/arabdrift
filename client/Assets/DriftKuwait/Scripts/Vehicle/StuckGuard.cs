using UnityEngine;

namespace DriftKuwait
{
    /// Safety net for the player's car: if it cannot move for a couple of seconds while you are pressing the gas or
    /// the brake (wedged between cars, beached on something, pinned against a wall), or if it ends up on its roof or
    /// side, it is lifted back onto the nearest free stretch of road, facing along it. Also callable from the pause menu.
    public class StuckGuard : MonoBehaviour
    {
        public static StuckGuard Current { get; private set; }
        DriftCarController car;
        CarInput input;
        float stuckT, flipT;
        Collider[] own;
        readonly Collider[] hits = new Collider[16];

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        void Start()
        {
            car = GetComponent<DriftCarController>();
            input = GetComponent<CarInput>();
            own = GetComponentsInChildren<Collider>();
        }

        void Update()
        {
            if (car == null || input == null || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            bool pushing = input.Throttle > 0.4f || input.Brake > 0.4f;
            if (pushing && car.SpeedKmh < 2.5f) stuckT += dt;
            else stuckT = Mathf.Max(0f, stuckT - dt * 2f);
            bool flipped = Vector3.Dot(transform.up, Vector3.up) < 0.45f;
            flipT = flipped ? flipT + dt : 0f;
            if (stuckT > 2.2f || flipT > 1.5f) Recover();
        }

        public void Recover()
        {
            stuckT = 0f; flipT = 0f;
            Vector3 p = transform.position;
            Vector3 bestPos = p, bestDir = Flat(transform.forward);
            float bestD = float.MaxValue;

            // Candidate spots: road centre lines in Salwa, and the desert highway
            foreach (var r in SalwaBlockBuilder.RoadRects)
            {
                bool alongX = (r.y - r.x) > (r.w - r.z);
                for (int s = -2; s <= 2; s++)
                {
                    Vector3 q = alongX ? new Vector3(Mathf.Clamp(p.x + s * 6f, r.x + 3f, r.y - 3f), 0f, (r.z + r.w) * 0.5f)
                                       : new Vector3((r.x + r.y) * 0.5f, 0f, Mathf.Clamp(p.z + s * 6f, r.z + 3f, r.w - 3f));
                    Consider(q, alongX ? Vector3.right : Vector3.forward, p, ref bestPos, ref bestDir, ref bestD);
                }
            }
            var route = DesertBuilder.Route;
            for (int i = 0; i + 1 < route.Count; i += 2)
                Consider(route[i], (route[i + 1] - route[i]).normalized, p, ref bestPos, ref bestDir, ref bestD);

            if (bestD == float.MaxValue) bestPos = p;   // nothing free nearby: at least lift it and set it upright
            // keep roughly the way the driver was facing
            if (Vector3.Dot(bestDir, Flat(transform.forward)) < 0f) bestDir = -bestDir;
            float y = 0f;
            if (Physics.Raycast(bestPos + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~((1 << 2) | (1 << TrafficSystem.TrafficLayer)), QueryTriggerInteraction.Ignore)) y = hit.point.y;

            var rb = car.Body;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            var tw = GetComponent<TyreWear>(); if (tw != null) tw.Refit();   // fresh tyres with the recovery
            transform.SetPositionAndRotation(new Vector3(bestPos.x, y + 0.6f, bestPos.z), Quaternion.LookRotation(bestDir));
            Physics.SyncTransforms();
            var hud = Object.FindAnyObjectByType<DriveHUD>();
            if (hud != null) hud.Callout("رجّعناك للشارع", UIKit.Gold);
            if (PreviewHooks.Active) Debug.Log($"[StuckGuard] recovered to {transform.position}");
        }

        void Consider(Vector3 q, Vector3 dir, Vector3 from, ref Vector3 bestPos, ref Vector3 bestDir, ref float bestD)
        {
            float d = (new Vector3(q.x - from.x, 0f, q.z - from.z)).sqrMagnitude;
            if (d >= bestD || d < 4f) return;   // not the very spot it is stuck on
            if (!Free(q, dir)) return;
            bestD = d; bestPos = q; bestDir = Flat(dir);
        }

        bool Free(Vector3 q, Vector3 dir)
        {
            // a car-sized box above the road surface must be empty (walls, parked cars, traffic, people)
            int n = Physics.OverlapBoxNonAlloc(q + Vector3.up * 1.1f, new Vector3(1.3f, 0.8f, 2.8f), hits, Quaternion.LookRotation(Flat(dir)), ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                bool mine = false;
                foreach (var c in own) if (c == hits[i]) { mine = true; break; }
                if (!mine) return false;
            }
            return true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
