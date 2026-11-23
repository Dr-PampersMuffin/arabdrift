using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// An AI rival on the desert highway: follows the route centre line in its own lane offset, brakes for the bends,
    /// slides a little through them, and is held in a loose pack with the player (rubber band) so the race stays close.
    public class RivalRacer : MonoBehaviour
    {
        public DriftCarController car;
        public CarInput input;
        public List<Vector3> route;
        public float lane;          // metres right of the centre line
        public float skill = 1f;    // 0.9..1.05
        public DriftCarController player;
        public int index;           // route point reached
        public bool finished;

        public float Progress => index + (route != null && index < route.Count - 1
            ? Mathf.Clamp01(1f - Vector3.Distance(transform.position, route[index + 1]) / Mathf.Max(1f, Vector3.Distance(route[index], route[index + 1])))
            : 0f);

        public static float ProgressOf(Vector3 p, List<Vector3> route, ref int idx)
        {
            while (idx < route.Count - 2 && (route[idx + 1] - p).sqrMagnitude < (route[idx] - p).sqrMagnitude) idx++;
            return idx;
        }

        void Update()
        {
            if (route == null || car == null || finished) return;
            Vector3 p = transform.position;
            ProgressOf(p, route, ref index);
            if (index >= route.Count - 2) { finished = true; input.aiThrottle = 0f; input.aiBrake = 1f; return; }

            // Aim at a point ahead on the line, offset into this racer's lane
            int ahead = Mathf.Min(route.Count - 1, index + 3 + Mathf.RoundToInt(car.SpeedKmh / 35f));
            Vector3 a = route[ahead], b = route[Mathf.Min(route.Count - 1, ahead + 1)];
            Vector3 r = Vector3.Cross(Vector3.up, (b - a).normalized);
            Vector3 target = a + r * lane;
            Vector3 to = target - p; to.y = 0f;
            float ang = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            input.aiSteer = Mathf.Clamp(ang / 25f, -1f, 1f);

            // Corner speed from how much the road turns over the next stretch
            int far = Mathf.Min(route.Count - 1, index + 14);
            Vector3 d0 = route[Mathf.Min(route.Count - 1, index + 1)] - route[index], d1 = route[far] - route[Mathf.Max(0, far - 1)];
            float bend = Vector3.Angle(d0, d1);
            float want = Mathf.Lerp(175f, 95f, Mathf.InverseLerp(5f, 45f, bend)) * skill;

            // Rubber band: stay within reach of the player
            if (player != null)
            {
                int pi = 0; ProgressOf(player.transform.position, route, ref pi);
                float gap = (index - pi) * 6f;   // metres ahead of the player
                if (gap > 90f) want *= 0.85f;
                else if (gap < -90f) want *= 1.15f;
            }
            bool tooFast = car.SpeedKmh > want + 6f;
            input.aiThrottle = tooFast ? 0f : 1f;
            input.aiBrake = car.SpeedKmh > want + 20f ? 0.6f : 0f;
            input.aiHandbrake = bend > 35f && car.SpeedKmh > 90f && Mathf.Abs(ang) > 12f;   // flick it into the long bends
        }

        public static RivalRacer Spawn(string id, Vector3 pos, Quaternion rot, List<Vector3> route, float lane, float skill, DriftCarController player)
        {
            var go = CarFactory.Build(id, pos, rot);
            if (go == null) return null;
            go.name = "Rival_" + id;
            foreach (var c in go.GetComponents<MonoBehaviour>()) if (c is EngineAudio || c is TyreAudio) Destroy(c);
            var rr = go.AddComponent<RivalRacer>();
            rr.car = go.GetComponent<DriftCarController>();
            rr.input = go.GetComponent<CarInput>();
            rr.input.ai = true;
            rr.car.gameplayPower = 1.45f;
            rr.car.counterSteerAssist = 1f;
            rr.route = route; rr.lane = lane; rr.skill = skill; rr.player = player;
            return rr;
        }
    }
}
