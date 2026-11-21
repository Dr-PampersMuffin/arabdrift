using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace DriftKuwait
{
    /// Sign-in and leaderboards through Apple Game Center (the player's Apple account; no separate password).
    /// Leaderboards are created in App Store Connect by tools/asc/gamecenter.py.
    public static class GameCenter
    {
        public const string BestRun = "q8drift.best_run";
        public const string BestChain = "q8drift.best_chain";
        public const string TotalStars = "q8drift.total_stars";

        public static bool SignedIn => Application.platform == RuntimePlatform.IPhonePlayer && Social.localUser.authenticated;
        public static string PlayerName => SignedIn ? Social.localUser.userName : null;
        public static event System.Action Changed;

        static bool asked;

        public static void SignIn()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer || PreviewHooks.Active || asked) return;
            asked = true;
            Social.localUser.Authenticate(ok =>
            {
                Debug.Log("[GameCenter] signed in: " + ok);
                if (ok) Report(TotalStars, Career.TotalStars);
                Changed?.Invoke();
            });
        }

        public static void Report(string board, long value)
        {
            if (!SignedIn || value <= 0) return;
            Social.ReportScore(value, board, ok => Debug.Log($"[GameCenter] {board} {value} -> {ok}"));
        }

        /// Opens Apple's leaderboard screen; if not signed in, tries to sign in first.
        public static void ShowLeaderboards()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer) { GameFlow.I?.Toast("الترتيب يشتغل على الآيفون"); return; }
            if (!SignedIn)
            {
                asked = false;
                Social.localUser.Authenticate(ok =>
                {
                    Changed?.Invoke();
                    if (ok) Social.ShowLeaderboardUI();
                    else GameFlow.I?.Toast("سجّل دخول Game Center من الإعدادات");
                });
                return;
            }
            Social.ShowLeaderboardUI();
        }
    }
}
