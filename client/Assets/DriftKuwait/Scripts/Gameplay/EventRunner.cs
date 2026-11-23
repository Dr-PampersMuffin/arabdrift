using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace DriftKuwait
{
    /// Runs one career event inside a drive: places the car, counts down, keeps the clock and the result, shows the
    /// goal and the next star target on the HUD, then hands the result to the results screen.
    public class EventRunner : MonoBehaviour
    {
        public Career.EventDef ev;
        public DriftCarController car;
        public DriftScorer scorer;
        public DriveHUD hud;
        public Pursuit pursuit;
        public GameFlow flow;

        float timeLeft, elapsed, lastValue;
        long zonePts, startBest;
        bool running, finished;
        GameObject zoneRing;
        // What went wrong, for live coaching and for the results screen
        float slowTime, straightTime, outsideTime, coachCooldown = 3f;
        int hits;
        // Desert Run
        readonly List<RivalRacer> rivals = new List<RivalRacer>();
        int playerIdx, place = 3;
        bool copsCalled;
        // Repair jobs (delivery)
        float condition = 100f;
        GameObject dropBeacon;

        void Start()
        {
            pursuit.allowHeat = false;
            if (ev.kind == Career.Kind.Zone) zoneRing = ZoneRing(ev.zone, ev.zoneRadius);
            pursuit.OnEnd += escaped => { if (ev.kind == Career.Kind.Escape && running) Finish(escaped ? (long)Mathf.Max(1f, elapsed) : 0); };
            car.OnImpact += (imp, _) => { if (running && imp > 3000f) hits++; };
            if (ev.kind == Career.Kind.Delivery)
            {
                dropBeacon = FreeRoam.MakeBeacon("DK_DropOff", ev.dest, TyreFX.ParticleMaterial("DK_DropMat", Texture2D.whiteTexture, new Color(0.35f, 1f, 0.55f, 0.4f)));
                car.OnImpact += (imp, _) =>
                {
                    if (!running || imp < 1500f) return;
                    float dmg = Mathf.Clamp((imp - 1500f) / 400f, 2f, 30f);
                    condition = Mathf.Max(0f, condition - dmg);
                    hud.Callout("-" + UIKit.Num((long)Mathf.Round(dmg)) + "٪ حالة السيارة", new Color(1f, 0.45f, 0.35f));
                };
            }
            if (ev.kind == Career.Kind.Run)
            {
                pursuit.OnEnd += escaped => { if (running && !escaped) Finish(0); };   // caught = race lost
                var route = DesertBuilder.Route;
                var rot = car.transform.rotation;
                var right = car.transform.right;
                var r1 = RivalRacer.Spawn("faris", car.transform.position + right * -3.4f + car.transform.forward * 7f, rot, route, -3f, 0.97f, car);
                var r2 = RivalRacer.Spawn("ghazal", car.transform.position + right * 3.4f + car.transform.forward * 3f, rot, route, 3f, 1.0f, car);
                foreach (var r in new[] { r1, r2 }) if (r != null) { r.car.Body.isKinematic = true; rivals.Add(r); }
            }
            StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            hud.MissionTitle = ev.titleAr;
            hud.MissionSub = ev.goalAr;
            hud.MissionTime = "";
            var rb = car.Body;
            rb.isKinematic = true;
            if (!PreviewHooks.Active)
            {
                if (ev.kind == Career.Kind.Run)
                {
                    // Cinematic intro card, Need for Speed style
                    bool shown = false;
                    StoryScene.Play(new[] { new StoryScene.Beat("story_desert_run", "run_intro", "بو نبيل") }, () => shown = true);
                    while (!shown) yield return null;
                }
                else
                {
                    hud.Say("ev_" + ev.id, "por_abu_rashid", "بو نبيل:", ev.howAr);
                    yield return new WaitForSeconds(Mathf.Clamp(ev.howAr.Length / 15f, 2.5f, 6f));
                }
                foreach (var n in new[] { "٣", "٢", "١" })
                {
                    hud.Pop(n, UIKit.Cream);
                    Sfx.Play("ui_click", 1f);
                    yield return new WaitForSeconds(0.8f);
                }
                hud.Pop("يلّا!", UIKit.Gold);
            }
            rb.isKinematic = false;
            foreach (var r in rivals) r.car.Body.isKinematic = false;
            timeLeft = ev.seconds;
            lastValue = scorer.Total + scorer.Current;
            startBest = scorer.BestRun;
            running = true;
            if (ev.kind == Career.Kind.Escape) pursuit.ForceStart();
        }

        void Update()
        {
            if (!running || finished) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            timeLeft -= dt;

            float value = scorer.Total + scorer.Current;
            if (ev.kind == Career.Kind.Zone)
            {
                Vector3 d = car.transform.position - ev.zone; d.y = 0f;
                if (d.magnitude < ev.zoneRadius && value > lastValue) zonePts += (long)(value - lastValue);
            }
            lastValue = value;

            // Live coaching in Kuwaiti: say what to fix, at most every few seconds
            bool inZone = true;
            if (ev.kind == Career.Kind.Zone) { Vector3 dz = car.transform.position - ev.zone; dz.y = 0f; inZone = dz.magnitude < ev.zoneRadius; }
            if (ev.kind == Career.Kind.Delivery)
            {
                Vector3 dd = car.transform.position - ev.dest; dd.y = 0f;
                float dist = dd.magnitude;
                int sl = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
                hud.JudgedValue = -1;
                hud.MissionTime = (sl / 60) + ":" + (sl % 60).ToString("00") + "     " + UIKit.Ar("الحالة ") + UIKit.Num((long)Mathf.Round(condition)) + "٪     " + UIKit.Num((long)dist) + UIKit.Ar(" م");
                if (dist < 9f && car.SpeedKmh < 50f) { Finish((long)Mathf.Max(1f, Mathf.Round(condition))); return; }
                if (timeLeft <= 0f) Finish(0);
                return;
            }
            if (ev.kind != Career.Kind.Escape)
            {
                if (!scorer.Drifting)
                {
                    if (car.SpeedKmh < 25f) slowTime += dt; else straightTime += dt;
                }
                if (!inZone) outsideTime += dt;
                coachCooldown -= dt;
                if (coachCooldown <= 0f && !scorer.Drifting)
                {
                    string tip = !inZone ? "ارجع داخل الدائرة!" : car.SpeedKmh < 25f ? "أسرع شوي! فوق ٢٥" : "اسحب الهاندبريك وعاكس السكّان";
                    hud.Callout(tip, UIKit.Cream);
                    coachCooldown = 4f;
                }
            }

            if (ev.kind == Career.Kind.Run)
            {
                RunUpdate();
                if (finished) return;
            }

            long result = Result();
            hud.JudgedValue = ev.kind == Career.Kind.Escape || ev.kind == Career.Kind.Run ? -1 : result;
            long next = NextTarget(result);
            int s = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            string clock = (s / 60) + ":" + (s % 60).ToString("00");
            if (ev.kind == Career.Kind.Escape) hud.MissionTime = clock;
            else if (ev.kind == Career.Kind.Run) hud.MissionTime = clock + "     " + UIKit.Ar("المركز ") + place + "/3";
            else hud.MissionTime = clock + "     " + (next > 0 ? UIKit.Dig(result) + " / " + UIKit.Dig(next) : UIKit.Dig(result));

            if (ev.seconds > 0f && timeLeft <= 0f)
                Finish(ev.kind == Career.Kind.Escape ? 0 : Result());
        }

        long Result()
        {
            switch (ev.kind)
            {
                case Career.Kind.Zone: return zonePts;
                case Career.Kind.Chain: return (long)Mathf.Max(scorer.BestRun > startBest ? scorer.BestRun : 0, scorer.Current);
                case Career.Kind.Escape: return (long)elapsed;
                case Career.Kind.Run: return 0;
                default: return (long)(scorer.Total + scorer.Current);
            }
        }

        /// The main thing that held the score back, in Kuwaiti.
        string Why()
        {
            if (ev.kind == Career.Kind.Escape) return "ابتعد عن الدورية واستخدم الشوارع الضيقة عشان تفقدك";
            if (ev.kind == Career.Kind.Delivery) return timeLeft <= 0f ? "تأخرت على الزبون .. خذ الشارع العام وخل التقحيص لبعدين" : "كل صدمة تنقص من حالة السيارة .. الزبون يبيها سليمة";
            if (ev.kind == Career.Kind.Run) return hits >= 3 ? $"صدمت {hits} مرات .. كل صدمة تبطّئك" : "خذ المنعطفات بتقحيصة قصيرة وخلك على الإسفلت .. الرمل يبطّئ";
            float t = Mathf.Max(1f, elapsed);
            if (ev.kind == Career.Kind.Zone && outsideTime / t > 0.35f) return "النقاط تنحسب بس داخل الدائرة الذهبية حول الدوّار";
            if (hits >= 3) return $"صدمت {hits} مرات .. كل صدمة تمسح السلسلة اللي تجمعها";
            if (slowTime / t > 0.3f) return "كنت أبطأ من ٢٥ كم/س أغلب الوقت .. التقحيص ما ينحسب تحت هالسرعة";
            if (straightTime / t > 0.35f) return "السيارة كانت ماشية سيدة .. اسحب الهاندبريك وخلها تنزلق";
            if (ev.kind == Career.Kind.Chain) return "تنحسب أطول سلسلة وحدة .. لا تخلي السيارة تعتدل";
            return "طوّل الانزلاق وقرّب من الجدران عشان المضاعف يكبر";
        }

        long NextTarget(long result)
        {
            if (ev.stars == null || ev.kind == Career.Kind.Escape || ev.kind == Career.Kind.Run) return 0;
            foreach (var t in ev.stars) if (result < t) return t;
            return 0;
        }

        void Finish(long result)
        {
            if (finished) return;
            finished = true; running = false;
            hud.MissionTime = "";
            var rec = Career.Record(ev, result);
            if (ev.kind == Career.Kind.Chain) GameCenter.Report(GameCenter.BestChain, result);
            else if (ev.kind != Career.Kind.Escape && ev.kind != Career.Kind.Delivery) GameCenter.Report(GameCenter.BestRun, result);
            GameCenter.Report(GameCenter.TotalStars, Career.TotalStars);
            if (rec.stars == 3) Sfx.Voice("ab_stars3", 1f);
            else if (rec.stars > 0) Sfx.Voice("ab_good", 1f);
            else Sfx.Voice("ab_fail", 1f);
            StartCoroutine(ShowResults(result, rec));
        }

        /// Desert Run: race position, the patrol joining half way, the finish at the camp.
        void RunUpdate()
        {
            var route = DesertBuilder.Route;
            RivalRacer.ProgressOf(car.transform.position, route, ref playerIdx);
            int ahead = 0;
            foreach (var r in rivals) if (r != null && (r.finished || r.index > playerIdx)) ahead++;
            if (ahead + 1 < place) hud.Pop(ahead == 0 ? "الأول!" : "تقدّمت!", UIKit.Gold);
            place = ahead + 1;
            if (!copsCalled && playerIdx > route.Count * 0.35f)
            {
                copsCalled = true;
                pursuit.ForceStart();
                hud.Pop("دورية!", new Color(0.4f, 0.85f, 1f));
            }
            Vector3 dc = car.transform.position - DesertBuilder.Camp; dc.y = 0f;
            if (playerIdx >= route.Count - 3 || dc.magnitude < 26f)
            {
                pursuit.Cleanup();
                Finish(4 - place);   // 1st = 3 stars
            }
        }

        IEnumerator ShowResults(long result, (int stars, int cash, int rep, bool best) rec)
        {
            // Let the car roll to a stop under the brakes before the results come up.
            CarInput.TouchThrottle = 0f;
            yield return new WaitForSeconds(1.2f);
            Time.timeScale = 0.25f;
            yield return new WaitForSecondsRealtime(0.6f);
            Time.timeScale = 1f;
            car.Body.isKinematic = true;
            if (ev.kind == Career.Kind.Run && result > 0 && !PreviewHooks.Active)
            {
                // Chill at the camp: the crew round the fire, then the results
                bool done = false;
                StoryScene.Play(new[]
                {
                    new StoryScene.Beat("story_desert_camp", "camp_01", "بو نبيل"),
                    new StoryScene.Beat("story_desert_camp", "camp_02", "الربع"),
                }, () => done = true);
                while (!done) yield return null;
            }
            ResultsScreen.Show(flow, ev, result, rec.stars, rec.cash, rec.rep, rec.best, rec.stars < 3 ? Why() : null);
        }

        void OnDestroy()
        {
            if (zoneRing != null) Destroy(zoneRing);
            if (dropBeacon != null) Destroy(dropBeacon);
            foreach (var r in rivals) if (r != null) Destroy(r.gameObject);
        }

        static GameObject ZoneRing(Vector3 c, float r)
        {
            // A glowing gold ring painted on the road around the zone.
            var go = new GameObject("DK_ZoneRing");
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true; lr.useWorldSpace = true; lr.positionCount = 72;
            lr.widthMultiplier = 0.6f;
            lr.material = TyreFX.ParticleMaterial("DK_ZoneMat", Texture2D.whiteTexture, new Color(1f, 0.75f, 0.2f, 0.55f));
            lr.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2f;
                lr.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r));
            }
            return go;
        }
    }
}
