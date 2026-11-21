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
        float heatPoints, escapeTimer, bustTimer, cooldown = 25f, radioCooldown;
        bool active;

        class Patrol
        {
            public GameObject go;
            public DriftCarController car;
            public CarInput input;
            public AudioSource siren;
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
                if (heatPoints > 6000f && CarFactory.Load("dawriya") != null) StartCoroutine(Begin());
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

            if (nearest > 140f) escapeTimer += dt; else escapeTimer = Mathf.Max(0f, escapeTimer - dt * 0.5f);
            if (nearest < 7f && player.SpeedKmh < 12f) bustTimer += dt; else bustTimer = 0f;

            if (radioCooldown <= 0f && nearest < 40f)
            {
                radioCooldown = 14f;
                if (Random.value < 0.5f) hud.Say("rd_ahead", "por_bader", "دورية ١٢:", "السيارة الفضية قدامنا .. انتبه .. انتبه.");
                else hud.Say("pa_stop", "por_bader", "دورية ١٢:", "السيارة الفضية! وقّف على اليمين!");
            }

            if (escapeTimer > 7f) End(true);
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
            Vector3 back = -player.transform.forward * (55f + patrols.Count * 15f) + player.transform.right * (patrols.Count % 2 == 0 ? 3f : -3f);
            Vector3 pos = player.transform.position + back + Vector3.up * 3f;
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
            Vector3 target = player.transform.position + player.Body.linearVelocity * 0.8f;
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
            if (p.car.SpeedKmh < 3f && Mathf.Abs(ang) > 60f) { p.input.aiThrottle = 0f; p.input.aiBrake = 1f; p.input.aiSteer = -p.input.aiSteer; }
        }

        void End(bool escaped)
        {
            active = false;
            cooldown = 40f;
            hud.InPursuit = false;
            if (escaped)
            {
                hud.Say("rd_lost", "por_bader", "دورية ١٢:", "فقدناه .. ما في أثر للسيارة.");
                hud.Callout("هربت!", new Color(0.35f, 0.9f, 0.85f));
                Wallet.Add(2000, 50);
            }
            else
            {
                hud.Callout("انحجزت السيارة!", new Color(1f, 0.32f, 0.25f));
                StartCoroutine(Impound());
            }
            Sfx.Music(Atmosphere.IsNight ? "mus_drive_night" : "mus_drive_day", 2f);
            Cleanup();
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
