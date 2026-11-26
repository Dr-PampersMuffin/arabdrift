using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// الدورية (blueprint: fully fictional patrol, Captain Bader, never a villain). Big drift chains raise the heat; at
    /// heat 1 a patrol sedan joins behind you with the siren on and the radio crackles in Kuwaiti dialect. Escape by
    /// breaking away (distance) for a few seconds. A bust is never game over: the car is "impounded" and you restart.
    public class Pursuit : MonoBehaviour
    {
        public DriftCarController player;
        public DriveHUD hud;

        readonly List<Patrol> patrols = new List<Patrol>();
        /// Raised when a chase ends: true = escaped, false = busted.
        public event System.Action<bool> OnEnd;
        /// Off during timed events, so the patrol only appears in free roam and the escape event.
        public bool allowHeat = true;

        /// The escape event starts the chase straight away.
        public void ForceStart() { if (!active) { cooldown = 0f; StartCoroutine(Begin()); } }
        float heatPoints, escapeTimer, bustTimer, cooldown = 25f, radioCooldown;
        bool active;

        class Patrol
        {
            public GameObject go;
            public DriftCarController car;
            public CarInput input;
            public AudioSource siren;
            public float stuck, reverse;
            public Light red, teal;
        }

        void Start()
        {
            var sc = player != null ? player.GetComponent<DriftScorer>() : null;
            if (sc != null) sc.OnBank += pts => { if (!active && cooldown <= 0f) heatPoints += pts; };
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            cooldown -= dt;
            radioCooldown -= dt;
            if (!active)
            {
                if (allowHeat && heatPoints > 6000f && CarFactory.Load("dawriya") != null) StartCoroutine(Begin());
                return;
            }

            hud.InPursuit = true;
            hud.PursuitTime += dt;
            hud.Heat = Mathf.Clamp(1 + Mathf.FloorToInt(hud.PursuitTime / 25f), 1, 5);
            if (patrols.Count < Mathf.Min(hud.Heat, 3) && hud.PursuitTime > patrols.Count * 18f) Spawn(-1);

            float nearest = float.MaxValue;
            foreach (var p in patrols)
            {
                if (p.go == null) continue;
                Drive(p, dt);
                nearest = Mathf.Min(nearest, Vector3.Distance(p.go.transform.position, player.transform.position));
                bool on = (Time.time * 6f) % 2f < 1f;
                p.red.intensity = on ? 9f : 0f;
                p.teal.intensity = on ? 0f : 9f;
            }

            if (nearest > 110f) escapeTimer += dt; else escapeTimer = Mathf.Max(0f, escapeTimer - dt * 0.5f);
            if (nearest < 7f && player.SpeedKmh < 12f) bustTimer += dt; else bustTimer = 0f;

            if (radioCooldown <= 0f && nearest < 40f)
            {
                radioCooldown = 14f;
                if (Random.value < 0.5f) hud.Say("rd_ahead", "por_bader", "دورية ١٢:", "السيارة الفضية قدامنا .. انتبه .. انتبه.");
                else hud.Say("pa_stop", "por_bader", "دورية ١٢:", "السيارة الفضية! وقّف على اليمين!");
            }

            if (escapeTimer > 5f) End(true);
            else if (bustTimer > 3f) End(false);
        }

        IEnumerator Begin()
        {
            active = true;
            heatPoints = 0f;
            hud.PursuitTime = 0f;
            Sfx.Music("mus_pursuit", 1f);
            Spawn(-1);
            yield return new WaitForSeconds(1.2f);
            hud.Say("rd_spotted", "por_bader", "دورية ١٢:", "دورية اثنعش للعمليات .. عندنا سيارة فضية تقحص عند دوار سلوى.");
            radioCooldown = 10f;
        }

        void Spawn(int side)
        {
            // Behind the player, on the ground, facing the player.
            Vector3 pos = RoadPointBehind(55f + patrols.Count * 15f) + Vector3.up * 3f;
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 60f, ~(1 << 2))) pos = hit.point + Vector3.up * 0.6f;
            var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - pos, Vector3.up));
            var go = CarFactory.Build("dawriya", pos, rot);
            go.name = "Dawriya_" + patrols.Count;
            foreach (var c in go.GetComponents<MonoBehaviour>())
                if (c is EngineAudio || c is TyreAudio) Destroy(c);
            var p = new Patrol { go = go, car = go.GetComponent<DriftCarController>(), input = go.GetComponent<CarInput>() };
            p.input.ai = true;
            p.car.gameplayPower = 1.7f;
            p.car.counterSteerAssist = 1f;
            p.siren = go.AddComponent<AudioSource>();
            p.siren.clip = Resources.Load<AudioClip>("DriftKuwait/Audio/siren_dawriya");
            p.siren.loop = true; p.siren.spatialBlend = 1f; p.siren.minDistance = 8f; p.siren.maxDistance = 220f; p.siren.volume = 0.8f * Sfx.SfxVolume;
            p.siren.dopplerLevel = 0.6f;
            if (p.siren.clip != null) p.siren.Play();
            var data = CarFactory.Load("dawriya");
            float roofY = data != null ? data.height + 0.05f : 1.5f;
            p.red = BarLight(go.transform, new Vector3(-0.35f, roofY, 0f), new Color(1f, 0.55f, 0.1f));    // amber
            p.teal = BarLight(go.transform, new Vector3(0.35f, roofY, 0f), new Color(0.1f, 0.85f, 0.8f));  // teal
            patrols.Add(p);
            Sfx.Play("siren_yelp", 0.6f);
        }

        static Light BarLight(Transform car, Vector3 pos, Color c)
        {
            var g = new GameObject("BarLight");
            g.transform.SetParent(car, false);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = 14f; l.color = c; l.intensity = 0f; l.shadows = LightShadows.None;
            return l;
        }

        void Drive(Patrol p, float dt)
        {
            var t = p.go.transform;
            Vector3 target = Route(t.position, player.transform.position + player.Body.linearVelocity * 0.8f);
            Vector3 to = target - t.position;
            float dist = to.magnitude;
            float ang = Vector3.SignedAngle(t.forward, Vector3.ProjectOnPlane(to, Vector3.up), Vector3.up);
            p.input.aiSteer = Mathf.Clamp(ang / 30f, -1f, 1f);
            float wantKmh = Mathf.Lerp(40f, 150f, Mathf.InverseLerp(10f, 80f, dist));
            if (dist < 12f) wantKmh = Mathf.Max(20f, player.SpeedKmh - 5f);
            bool tooFast = p.car.SpeedKmh > wantKmh + 10f || (Mathf.Abs(ang) > 70f && p.car.SpeedKmh > 45f);
            p.input.aiThrottle = tooFast ? 0f : (Mathf.Abs(ang) > 100f ? 0.4f : 1f);
            p.input.aiBrake = tooFast ? 0.6f : 0f;
            p.input.aiHandbrake = Mathf.Abs(ang) > 110f && p.car.SpeedKmh > 25f;
            // Unstick: if nearly stopped and pointing away for a while, reverse a little
            // Stuck on a wall or a car: back off for a moment, steering the other way; give up and come round again if hopeless
            if (p.car.SpeedKmh < 4f && dist > 9f) p.stuck += dt; else p.stuck = Mathf.Max(0f, p.stuck - dt);
            if (p.stuck > 2f && p.reverse <= 0f) p.reverse = 1.3f;
            if (p.reverse > 0f)
            {
                p.reverse -= dt;
                p.input.aiThrottle = 0f; p.input.aiBrake = 1f; p.input.aiHandbrake = false; p.input.aiSteer = -Mathf.Sign(ang);
            }
            if (p.stuck > 8f && dist > 50f)
            {
                p.stuck = 0f;
                Vector3 np = RoadPointBehind(70f);
                p.car.Body.linearVelocity = Vector3.zero;
                p.go.transform.SetPositionAndRotation(np + Vector3.up * 0.6f, Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - np, Vector3.up)));
            }
        }

        // ---- Street routing: the patrol follows the road grid instead of driving into walls ----
        static List<Vector4> roads;
        static List<Vector4> Roads
        {
            get
            {
                if (roads == null) { roads = new List<Vector4>(); foreach (var r in SalwaBlockBuilder.RoadRects) roads.Add(r); }
                return roads;
            }
        }
        static bool In(Vector4 r, Vector3 p, float pad) => p.x > r.x - pad && p.x < r.y + pad && p.z > r.z - pad && p.z < r.w + pad;
        static bool AlongX(Vector4 r) => (r.y - r.x) > (r.w - r.z);
        static Vector3 Centre(Vector4 r) => new Vector3((r.x + r.y) * 0.5f, 0f, (r.z + r.w) * 0.5f);

        /// The road the point is on; inside a junction, the one pointing toward `toward`.
        static int RoadAt(Vector3 p, Vector3 toward)
        {
            int best = -1; float bestScore = -9f;
            Vector3 d = toward - p; d.y = 0f; d.Normalize();
            for (int i = 0; i < Roads.Count; i++)
            {
                if (!In(Roads[i], p, 1.5f)) continue;
                float score = Mathf.Abs(AlongX(Roads[i]) ? d.x : d.z);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// Where to aim: straight at the player when nothing solid is between, else the next junction toward them.
        Vector3 Route(Vector3 from, Vector3 to)
        {
            Vector3 a = from + Vector3.up * 1f, b = to + Vector3.up * 1f;
            bool clear = true;
            foreach (var h in Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~((1 << 2) | (1 << TrafficSystem.TrafficLayer)), QueryTriggerInteraction.Ignore))
                if (h.rigidbody == null) { clear = false; break; }
            if (clear || Roads.Count == 0) return to;
            int r1 = RoadAt(from, to), r2 = RoadAt(to, from);
            if (r1 < 0 || r2 < 0 || r1 == r2) return to;
            Vector4 A = Roads[r1], B = Roads[r2];
            if (AlongX(A) != AlongX(B)) return Cross(A, B);
            // Parallel roads: go via the cross street that links them with the shortest detour
            Vector3 bestP = to; float bestD = float.MaxValue;
            foreach (var C in Roads)
            {
                if (AlongX(C) == AlongX(A)) continue;
                Vector3 c1 = Cross(A, C), c2 = Cross(B, C);
                if (!In(A, c1, 0.5f) || !In(C, c1, 0.5f) || !In(B, c2, 0.5f) || !In(C, c2, 0.5f)) continue;
                float d = Vector3.Distance(from, c1) + Vector3.Distance(c1, c2) + Vector3.Distance(c2, to);
                if (d < bestD) { bestD = d; bestP = c1; }
            }
            return bestP;
        }

        static Vector3 Cross(Vector4 a, Vector4 b)
        {
            // centre of the overlap of two perpendicular road rectangles
            Vector3 ca = Centre(a), cb = Centre(b);
            return AlongX(a) ? new Vector3(cb.x, 0f, ca.z) : new Vector3(ca.x, 0f, cb.z);
        }

        /// A point on the player's road, some way behind them (in the lane centre), for patrols to appear.
        Vector3 RoadPointBehind(float back)
        {
            Vector3 pp = player.transform.position;
            int ri = RoadAt(pp, pp - player.transform.forward * 10f);
            if (ri < 0) return pp - player.transform.forward * back;
            var r = Roads[ri];
            bool ax = AlongX(r);
            float dir = ax ? Mathf.Sign(player.transform.forward.x) : Mathf.Sign(player.transform.forward.z);
            float along = (ax ? pp.x : pp.z) - dir * back;
            along = ax ? Mathf.Clamp(along, r.x + 3f, r.y - 3f) : Mathf.Clamp(along, r.z + 3f, r.w - 3f);
            Vector3 c = Centre(r);
            return ax ? new Vector3(along, pp.y, c.z) : new Vector3(c.x, pp.y, along);
        }

        void End(bool escaped)
        {
            active = false;
            cooldown = 40f;
            hud.InPursuit = false;
            if (escaped)
            {
                hud.Say("rd_lost", "por_bader", "دورية ١٢:", "فقدناه .. ما في أثر للسيارة.");
                hud.Pop("هربت!", new Color(0.35f, 0.9f, 0.85f));
                Wallet.Add(2000, 50);
            }
            else
            {
                hud.Pop("انحجزت السيارة!", new Color(1f, 0.32f, 0.25f));
                StartCoroutine(Impound());
            }
            Sfx.Music("mus_drive_day", 2f);
            Cleanup();
            OnEnd?.Invoke(escaped);
        }

        IEnumerator Impound()
        {
            yield return new WaitForSeconds(2.5f);
            if (player == null) yield break;
            player.Body.linearVelocity = Vector3.zero;
            player.Body.angularVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.6f, -140f), Quaternion.identity);
        }

        public void Cleanup()
        {
            foreach (var p in patrols) if (p.go != null) Destroy(p.go);
            patrols.Clear();
        }
    }
}
