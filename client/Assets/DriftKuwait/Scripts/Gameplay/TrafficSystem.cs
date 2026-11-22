using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Salwa traffic: 1980s cars driving the street grid on the right-hand lane, turning at junctions, going round the
    /// roundabouts, slowing for whatever is ahead and stopping if you hit them. One of them can be a cruising patrol: drive
    /// like a fool next to it and the chase starts. Kinematic, so they are solid to the player without physics cost.
    public class TrafficSystem : MonoBehaviour
    {
        public DriftCarController player;
        public Pursuit pursuit;
        public int count = 14;
        public bool patrol = true;

        class Agent
        {
            public GameObject go;
            public DisplayWheels wheels;
            public Vector4 road;          // x0, x1, z0, z1
            public bool alongX;
            public float dir, speed, cruise, stopTimer, turnCooldown;
            public bool police;
        }

        public const int TrafficLayer = 29;
        static readonly string[] Ids = { "ghazal", "faris", "wanait", "abusitta", "shaheen" };
        readonly List<Vector4> roads = new List<Vector4>();
        readonly List<Agent> agents = new List<Agent>();
        readonly List<(Vector3 c, float r)> islands = new List<(Vector3, float)>();
        float patrolCooldown = 40f;
        int groundMask;

        void Start()
        {
            groundMask = ~((1 << 2) | (1 << TrafficLayer));
            foreach (var r in SalwaBlockBuilder.RoadRects)
                if (Mathf.Max(r.y - r.x, r.w - r.z) > 40f && Mathf.Min(r.y - r.x, r.w - r.z) > 6f) roads.Add(r);
            islands.AddRange(SalwaBlockBuilder.Islands);
            if (roads.Count == 0) { enabled = false; return; }
            for (int i = 0; i < count; i++)
            {
                bool cop = patrol && i == 0 && CarFactory.Load("dawriya") != null;
                var go = CarFactory.BuildDisplay(cop ? "dawriya" : Ids[i % Ids.Length], transform);
                if (go == null) continue;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = TrafficLayer;
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.8f, 1.4f, 4.5f);
                var a = new Agent { go = go, wheels = go.GetComponent<DisplayWheels>(), police = cop };
                Place(a, 60f, 200f);
                agents.Add(a);
            }
        }

        static bool AlongX(Vector4 r) => (r.y - r.x) > (r.w - r.z);
        static float Width(Vector4 r) => AlongX(r) ? r.w - r.z : r.y - r.x;

        Vector3 Fwd(Agent a) => a.alongX ? new Vector3(a.dir, 0f, 0f) : new Vector3(0f, 0f, a.dir);

        /// Point on the agent's right-hand lane line nearest to p.
        Vector3 Lane(Agent a, Vector3 p)
        {
            var r = a.road;
            float lane = Width(r) * 0.25f;
            Vector3 right = Vector3.Cross(Vector3.up, Fwd(a));
            Vector3 centre = a.alongX ? new Vector3(p.x, 0f, (r.z + r.w) * 0.5f) : new Vector3((r.x + r.y) * 0.5f, 0f, p.z);
            return centre + right * lane;
        }

        void Place(Agent a, float minD, float maxD)
        {
            Vector3 pp = player != null ? player.transform.position : Vector3.zero;
            for (int tries = 0; tries < 30; tries++)
            {
                var r = roads[Random.Range(0, roads.Count)];
                a.road = r; a.alongX = AlongX(r); a.dir = Random.value > 0.5f ? 1f : -1f;
                float t = Random.Range(0.15f, 0.85f);
                Vector3 p = new Vector3(Mathf.Lerp(r.x, r.y, t), 0f, Mathf.Lerp(r.z, r.w, t));
                p = Lane(a, p);
                float d = Vector3.Distance(new Vector3(pp.x, 0f, pp.z), p);
                if (d < minD || d > maxD) continue;
                bool clash = false;
                foreach (var o in agents) if (o != a && o.go != null && (o.go.transform.position - p).sqrMagnitude < 400f) clash = true;
                if (clash) continue;
                a.go.transform.SetPositionAndRotation(Ground(p), Quaternion.LookRotation(Fwd(a)));
                a.cruise = Random.Range(28f, 46f) / 3.6f;
                a.speed = a.cruise; a.stopTimer = 0f; a.turnCooldown = 2f;
                return;
            }
        }

        Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.8f)
                return new Vector3(p.x, hit.point.y + 0.02f, p.z);
            return new Vector3(p.x, 0.02f, p.z);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || player == null) return;
            Vector3 pp = player.transform.position;
            patrolCooldown -= dt;

            foreach (var a in agents)
            {
                if (a.go == null) continue;
                var tr = a.go.transform;
                Vector3 pos = tr.position;

                // Too far from the player: come back somewhere ahead in the neighbourhood
                if ((pos - pp).sqrMagnitude > 240f * 240f) { Place(a, 70f, 180f); continue; }

                // Bumped or blocked by the player: stop a moment
                float toPlayer = Vector3.Distance(pos, pp);
                if (toPlayer < 3.6f) a.stopTimer = 2.5f;

                // Look ahead for anything solid (other cars, the player, walls)
                float want = a.cruise;
                Vector3 eye = pos + Vector3.up * 1.0f + tr.forward * 2.6f;
                Vector3 toP = pp - pos; toP.y = 0f;
                if (toPlayer < 16f && Vector3.Dot(toP.normalized, tr.forward) > 0.75f)
                    want = Mathf.Min(want, Mathf.Max(0f, (toPlayer - 4f) / 12f) * a.cruise);
                if (Physics.SphereCast(eye, 0.6f, tr.forward, out var hit, 16f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    want = Mathf.Min(want, Mathf.Max(0f, (hit.distance - 3f) / 13f) * a.cruise);
                if (a.stopTimer > 0f) { a.stopTimer -= dt; want = 0f; }
                a.speed = Mathf.MoveTowards(a.speed, want, (want < a.speed ? 9f : 3f) * dt);

                // Steer toward a point on the lane a few metres ahead; round the roundabout islands
                Vector3 ahead = pos + Fwd(a) * 8f;
                Vector3 target = Lane(a, ahead);
                foreach (var (c, r) in islands)
                {
                    Vector3 off = target - c; off.y = 0f;
                    float keep = r + 5.5f;
                    if (off.magnitude < keep)
                    {
                        // go round anticlockwise (right-hand traffic)
                        Vector3 rad = off.sqrMagnitude > 0.01f ? off.normalized : -Fwd(a);
                        target = c + rad * keep + Vector3.Cross(Vector3.up, rad) * -4f;
                    }
                }
                Vector3 heading = target - pos; heading.y = 0f;
                if (heading.sqrMagnitude > 0.01f)
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(heading.normalized), 1f - Mathf.Exp(-3f * dt));

                float step = a.speed * dt;
                Vector3 next = pos + tr.forward * step;
                next = Ground(next);
                a.go.GetComponent<Rigidbody>().MovePosition(next);
                if (a.wheels != null) a.wheels.Roll(step);

                // Junctions: at the end of the road (or sometimes crossing another), turn onto a perpendicular road
                a.turnCooldown -= dt;
                float along = a.alongX ? next.x : next.z;
                float end = a.dir > 0 ? (a.alongX ? a.road.y : a.road.w) : (a.alongX ? a.road.x : a.road.z);
                bool atEnd = Mathf.Abs(end - along) < 7f;
                if (a.turnCooldown <= 0f)
                {
                    var options = new List<Vector4>();
                    foreach (var r in roads)
                    {
                        if (AlongX(r) == a.alongX) continue;
                        if (next.x > r.x - 1f && next.x < r.y + 1f && next.z > r.z - 1f && next.z < r.w + 1f) options.Add(r);
                    }
                    if (options.Count > 0 && (atEnd || Random.value < 0.35f))
                    {
                        var r = options[Random.Range(0, options.Count)];
                        a.road = r; a.alongX = AlongX(r);
                        float mid = a.alongX ? (r.x + r.y) * 0.5f : (r.z + r.w) * 0.5f;
                        float here = a.alongX ? next.x : next.z;
                        a.dir = Mathf.Abs(here - mid) < 10f ? (Random.value > 0.5f ? 1f : -1f) : (here < mid ? 1f : -1f);
                        a.turnCooldown = 4f;
                    }
                    else if (atEnd) { a.dir = -a.dir; a.turnCooldown = 4f; }   // dead end: turn round
                    else a.turnCooldown = 1.2f;
                }

                // The cruising patrol: hooning right next to it starts a chase
                if (a.police && pursuit != null && pursuit.allowHeat && patrolCooldown <= 0f && toPlayer < 22f &&
                    (player.SpeedKmh > 85f || Mathf.Abs(player.DriftAngle) > 25f))
                {
                    patrolCooldown = 90f;
                    pursuit.ForceStart();
                    Place(a, 150f, 230f);   // it becomes the chasing patrol; this cruiser leaves the scene
                }
            }
        }
    }
}
