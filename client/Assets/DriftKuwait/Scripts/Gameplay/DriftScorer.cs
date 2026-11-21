using System;
using UnityEngine;

namespace DriftKuwait
{
    /// تقحيص scoring: angle x speed x chain multiplier, near-miss bonus, a wall hit wipes the chain.
    /// Callouts are placeholders in Kuwaiti dialect, to be finalised by a Kuwaiti writer.
    [RequireComponent(typeof(DriftCarController))]
    public class DriftScorer : MonoBehaviour
    {
        public DriftCamera cam;

        public long Total { get; private set; }
        public float Current { get; private set; }
        public int Multiplier { get; private set; } = 1;
        public bool Drifting { get; private set; }
        public long BestRun { get; private set; }

        public event Action<string, Color> OnCallout;
        public event Action<long> OnBank;
        // Breakdown of the running chain for the HUD (زاوية / سرعة / اقتراب).
        public float AnglePts { get; private set; }
        public float SpeedPts { get; private set; }
        public float ProxPts { get; private set; }

        const float MinAngle = 12f, MinKmh = 25f, Grace = 1.4f;
        static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
        static readonly Color Red = new Color(1f, 0.32f, 0.25f);
        static readonly Color Teal = new Color(0.35f, 0.9f, 0.85f);

        DriftCarController car;
        float chainTime, graceLeft, nearMissCooldown;
        int mask;

        void Start()
        {
            car = GetComponent<DriftCarController>();
            car.OnImpact += OnImpact;
            mask = ~(1 << 2);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            nearMissCooldown -= dt;
            float angle = Mathf.Abs(car.DriftAngle);
            Drifting = angle > MinAngle && car.SpeedKmh > MinKmh && car.GroundedCount >= 3;

            if (Drifting)
            {
                chainTime += dt;
                graceLeft = Grace;
                Multiplier = Mathf.Clamp(1 + Mathf.FloorToInt(chainTime / 2.5f), 1, 5);
                float angleFactor = Mathf.InverseLerp(MinAngle, 65f, angle);
                float pps = (40f + angleFactor * 160f) * Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(25f, 120f, car.SpeedKmh));
                float gain = pps * Multiplier * dt;
                Current += gain;
                float aShare = (40f + angleFactor * 160f) / Mathf.Max(1f, 40f + angleFactor * 160f + 60f * Mathf.InverseLerp(25f, 120f, car.SpeedKmh));
                AnglePts += gain * aShare;
                SpeedPts += gain * (1f - aShare);
                if (angle > 80f) Current -= pps * 0.5f * dt; // over-rotation is sloppy
                CheckNearMiss();
            }
            else if (Current > 0f)
            {
                graceLeft -= dt;
                if (graceLeft <= 0f) Bank();
            }
        }

        void CheckNearMiss()
        {
            if (nearMissCooldown > 0f) return;
            Transform t = transform;
            Vector3[] origins =
            {
                new Vector3(-0.86f, 0.6f, 1.8f), new Vector3(-0.86f, 0.6f, -1.8f),
                new Vector3(0.86f, 0.6f, 1.8f),  new Vector3(0.86f, 0.6f, -1.8f),
                new Vector3(0f, 0.6f, -2.38f),
            };
            Vector3[] dirs = { -t.right, -t.right, t.right, t.right, -t.forward };
            for (int i = 0; i < origins.Length; i++)
            {
                if (Physics.Raycast(t.TransformPoint(origins[i]), dirs[i], out RaycastHit hit, 0.45f, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y > 0.6f) continue; // the ground is not a wall
                    Current += 250f * Multiplier;
                    ProxPts += 250f * Multiplier;
                    nearMissCooldown = 1.5f;
                    OnCallout?.Invoke("على الشعرة!", Teal);
                    if (cam != null) cam.Punch();
                    return;
                }
            }
        }

        void Bank()
        {
            long pts = (long)Current;
            Total += pts;
            if (pts > BestRun) BestRun = pts;
            string word = pts < 500 ? "حلو" : pts < 2000 ? "عدل!" : pts < 6000 ? "كفو!" : "يا سلام!";
            OnCallout?.Invoke(word, Gold);
            if (pts >= 800)
            {
                string[] lines = pts < 2000 ? new[] { "cl_adel", "cl_kafu" } : pts < 6000 ? new[] { "cl_kafu", "cl_mumtaz", "cl_wahsh" } : new[] { "cl_yasalam", "cl_khateer" };
                Sfx.Voice(lines[UnityEngine.Random.Range(0, lines.Length)], 0.9f);
            }
            if (pts >= 4000) Sfx.Play("crowd_cheer", 0.5f);
            Wallet.Add(pts / 4, pts / 100);
            OnBank?.Invoke(pts);
            Current = 0f; chainTime = 0f; Multiplier = 1;
            AnglePts = SpeedPts = ProxPts = 0f;
        }

        void OnImpact(float impulse, Vector3 point)
        {
            if (impulse < 3000f || Current <= 0f) return;
            Current = 0f; chainTime = 0f; Multiplier = 1;
            AnglePts = SpeedPts = ProxPts = 0f;
            OnCallout?.Invoke("خربتها!", Red);
        }
    }
}
