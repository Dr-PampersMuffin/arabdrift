using UnityEngine;

namespace DriftKuwait
{
    /// The Streets Remember: chapter state and the garage debt (the one goal the player always knows).
    public static class Story
    {
        public const long Debt = 30000;            // what the workshop owes
        public const long Instalment = 10000;      // Chapter 1 goal: the first instalment

        // The levels of each chapter, in order: earn one star in each, then pay. The HUD, the title screen and the
        // results screen always show the next one, and an arrow in the street points to it.
        public static readonly string[] Ch1Ladder = { "salwa_first", "salwa_dallah", "salwa_desert_run", "salwa_chain" };
        public static readonly string[] Ch2Ladder = { "salwa_job_taxi", "salwa_job_pickup", "salwa_job_sedan" };

        public struct Goal
        {
            public int step, steps;          // 1-based level number and how many in the chapter
            public string title, sub;        // "المرحلة ٢ من ٥", what to do
            public Career.EventDef ev;       // the challenge to play (null = pay the instalment / chapter done)
            public bool pay, ready, done;    // pay step; enough cash to pay; chapter finished
        }

        public static Goal Next()
        {
            var g = new Goal();
            if (Chapter >= 3) { g.done = true; g.title = "الفصل الثالث قريباً"; g.sub = "حسّن نجومك وعدّل سيارتك"; return g; }
            var ladder = Chapter == 1 ? Ch1Ladder : Ch2Ladder;
            g.steps = ladder.Length + 1;
            for (int i = 0; i < ladder.Length; i++)
            {
                var ev = Career.Get(ladder[i]);
                if (ev == null || Career.Stars(ev.id) >= 1) continue;
                g.step = i + 1; g.ev = ev;
                g.title = "المرحلة " + UIKit.Num(g.step) + " من " + UIKit.Num(g.steps);
                g.sub = ev.titleAr + " .. خذ نجمة وحدة";
                return g;
            }
            long need = Chapter == 1 ? ToInstalment : Remaining;
            g.step = g.steps; g.pay = true; g.ready = Wallet.Cash >= need;
            g.title = "المرحلة " + UIKit.Num(g.step) + " من " + UIKit.Num(g.steps);
            g.sub = g.ready ? "عندك المبلغ! ارجع وسدّد " + (Chapter == 1 ? "القسط" : "الدين")
                            : "جمّع " + UIKit.Dig(need) + " كاش وسدّد " + (Chapter == 1 ? "القسط" : "الدين");
            return g;
        }

        public static int Chapter { get => PlayerPrefs.GetInt("dk_chapter", 1); set { PlayerPrefs.SetInt("dk_chapter", value); PlayerPrefs.Save(); } }
        public static bool IntroSeen { get => PlayerPrefs.GetInt("dk_st1_intro", 0) == 1; set { PlayerPrefs.SetInt("dk_st1_intro", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static long Paid { get => long.TryParse(PlayerPrefs.GetString("dk_debt_paid", "0"), out var v) ? v : 0; set { PlayerPrefs.SetString("dk_debt_paid", value.ToString()); PlayerPrefs.Save(); } }
        public static long Remaining => System.Math.Max(0, Debt - Paid);   // never negative (saves from before the debt changed)
        public static long ToInstalment => System.Math.Max(0, Instalment - Paid);

        /// Pays what the wallet can toward the current instalment. Returns the amount paid.
        public static long PayFromWallet()
        {
            long want = Chapter == 1 ? ToInstalment : Remaining;
            long pay = System.Math.Min(want, Wallet.Cash);
            if (pay <= 0) return 0;
            Wallet.Add(-pay, 0);
            Paid += pay;
            return pay;
        }

        /// The story starts you with almost nothing: a new save begins at 3,000 cash.
        public static void BeginChapter1()
        {
            if (IntroSeen) return;
            IntroSeen = true;
            if (Wallet.Cash > 3000) Wallet.Add(3000 - Wallet.Cash, 0);
        }

        public static StoryScene.Beat[] Ch1Intro => new[]
        {
            new StoryScene.Beat("story_ch1_arrive", "st1_01", "بو نبيل"),
            new StoryScene.Beat("story_ch1_return", "st1_02", "بو نبيل"),
            new StoryScene.Beat("story_ch1_uncle", "st1_03", "بو شبيب"),
            new StoryScene.Beat("story_ch1_mutheeb", "st1_04", "مثيب"),
            new StoryScene.Beat("story_ch1_street", "st1_05", "بو نبيل"),
        };

        public static StoryScene.Beat[] Ch1Done => new[]
        {
            new StoryScene.Beat("story_ch1_uncle", "st1_06", "بو شبيب"),
        };

        /// Chapter 2, The Workshop: هيفي comes back, the workshop takes customers again, مثيب keeps the pressure on.
        public static StoryScene.Beat[] Ch2Intro => new[]
        {
            new StoryScene.Beat("story_ch2_haifi", "st2_01", "هيفي"),
            new StoryScene.Beat("story_ch2_jobs", "st2_02", "بو نبيل"),
            new StoryScene.Beat("story_ch2_phone", "st2_03", "مثيب"),
            new StoryScene.Beat("story_ch2_parts", "st2_04", "بو شبيب"),
        };

        /// حكايتنا: the garage's history in five slides, narrated by بو نبيل (1965 to the day you come back).
        public static StoryScene.Beat[] History => new[]
        {
            new StoryScene.Beat("story_hist_founding", "hs_01", "بو نبيل", "١٩٦٥ · أبوك .. وبو نبيل صبي الكراج"),
            new StoryScene.Beat("story_hist_taghees", "hs_02", "بو نبيل", "الثمانينات · تقحيص سلوى"),
            new StoryScene.Beat("story_hist_boys", "hs_03", "بو نبيل", "انت .. وهيفي أبو النظارة"),
            new StoryScene.Beat("story_hist_uncle", "hs_04", "بو نبيل", "بو شبيب .. وأبوك"),
            new StoryScene.Beat("story_hist_mutheeb", "hs_05", "بو نبيل", "مثيب"),
        };

        /// Replay from the title: the history, then every story scene of the chapters reached so far.
        public static StoryScene.Beat[] Replay()
        {
            var l = new System.Collections.Generic.List<StoryScene.Beat>(History);
            l.AddRange(Ch1Intro);
            if (Chapter >= 2) { l.AddRange(Ch1Done); l.AddRange(Ch2Intro); }
            if (Chapter >= 3) l.AddRange(Ch2Done);
            return l.ToArray();
        }

        /// The debt cleared, and the first sign that هيفي is not what he seems.
        public static StoryScene.Beat[] Ch2Done => new[]
        {
            new StoryScene.Beat("story_ch2_paid", "st2_05", "بو شبيب"),
            new StoryScene.Beat("story_ch2_betrayal", "st2_06", "مثيب"),
        };
    }
}
