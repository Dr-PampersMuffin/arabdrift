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

        void Start()
        {
            pursuit.allowHeat = false;
            if (ev.kind == Career.Kind.Zone) zoneRing = ZoneRing(ev.zone, ev.zoneRadius);
            pursuit.OnEnd += escaped => { if (ev.kind == Career.Kind.Escape && running) Finish(escaped ? (long)Mathf.Max(1f, elapsed) : 0); };
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
                hud.Say("ev_" + ev.id, "por_abu_rashid", "أبو راشد:", ev.howAr);
                yield return new WaitForSeconds(Mathf.Clamp(ev.howAr.Length / 15f, 2.5f, 6f));
                foreach (var n in new[] { "٣", "٢", "١" })
                {
                    hud.Callout(n, UIKit.Cream);
                    Sfx.Play("ui_click", 1f);
                    yield return new WaitForSeconds(0.8f);
                }
                hud.Callout("يلّا!", UIKit.Gold);
            }
            rb.isKinematic = false;
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

            long result = Result();
            long next = NextTarget(result);
            int s = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            string clock = UIKit.Num(s / 60, false) + ":" + (UIKit.EasternNumerals ? UIKit.Num(s % 60 / 10, false) + UIKit.Num(s % 10, false) : (s % 60).ToString("00"));
            if (ev.kind == Career.Kind.Escape) hud.MissionTime = clock;
            else hud.MissionTime = clock + "     " + UIKit.Ar(next > 0 ? UIKit.Num(result) + " من " + UIKit.Num(next) : UIKit.Num(result));

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
                default: return (long)(scorer.Total + scorer.Current);
            }
        }

        long NextTarget(long result)
        {
            if (ev.stars == null || ev.kind == Career.Kind.Escape) return 0;
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
            else if (ev.kind != Career.Kind.Escape) GameCenter.Report(GameCenter.BestRun, result);
            GameCenter.Report(GameCenter.TotalStars, Career.TotalStars);
            if (rec.stars == 3) Sfx.Voice("ab_stars3", 1f);
            else if (rec.stars > 0) Sfx.Voice("ab_good", 1f);
            else Sfx.Voice("ab_fail", 1f);
            StartCoroutine(ShowResults(result, rec));
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
            ResultsScreen.Show(flow, ev, result, rec.stars, rec.cash, rec.rep, rec.best);
        }

        void OnDestroy() { if (zoneRing != null) Destroy(zoneRing); }

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
