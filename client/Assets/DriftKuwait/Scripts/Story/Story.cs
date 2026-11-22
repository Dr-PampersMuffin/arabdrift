using UnityEngine;

namespace DriftKuwait
{
    /// The Streets Remember: chapter state and the garage debt (the one goal the player always knows).
    public static class Story
    {
        public const long Debt = 50000;            // what the workshop owes
        public const long Instalment = 20000;      // Chapter 1 goal: the first instalment

        public static int Chapter { get => PlayerPrefs.GetInt("dk_chapter", 1); set { PlayerPrefs.SetInt("dk_chapter", value); PlayerPrefs.Save(); } }
        public static bool IntroSeen { get => PlayerPrefs.GetInt("dk_st1_intro", 0) == 1; set { PlayerPrefs.SetInt("dk_st1_intro", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static long Paid { get => long.TryParse(PlayerPrefs.GetString("dk_debt_paid", "0"), out var v) ? v : 0; set { PlayerPrefs.SetString("dk_debt_paid", value.ToString()); PlayerPrefs.Save(); } }
        public static long Remaining => Debt - Paid;
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
            new StoryScene.Beat("story_ch1_arrive", "st1_01", "طبجون عود"),
            new StoryScene.Beat("story_ch1_return", "st1_02", "طبجون عود"),
            new StoryScene.Beat("story_ch1_uncle", "st1_03", "بو شبيب"),
            new StoryScene.Beat("story_ch1_mutheeb", "st1_04", "مثيب"),
            new StoryScene.Beat("story_ch1_street", "st1_05", "طبجون عود"),
        };

        public static StoryScene.Beat[] Ch1Done => new[]
        {
            new StoryScene.Beat("story_ch1_uncle", "st1_06", "بو شبيب"),
        };
    }
}
