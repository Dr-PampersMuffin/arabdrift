using UnityEngine;

namespace DriftKuwait
{
    /// Cash (in-game) and سمعة (reputation), earned only by play (blueprint section 10). Saved locally for now;
    /// the server will validate these when multiplayer arrives.
    public static class Wallet
    {
        public static long Cash { get => Get("dk_cash", 25000); private set => Set("dk_cash", value); }
        public static long Rep { get => Get("dk_rep", 0); private set => Set("dk_rep", value); }
        public static long Best { get => Get("dk_best", 0); set => Set("dk_best", value); }

        public static void Add(long cash, long rep) { Cash += cash; Rep += rep; }

        static long Get(string k, long d) { try { return long.Parse(PlayerPrefs.GetString(k, d.ToString())); } catch { return d; } }
        static void Set(string k, long v) { try { PlayerPrefs.SetString(k, v.ToString()); } catch { } }
    }

    /// Player settings (pause menu): control layout, drift assist, camera, volumes, numerals.
    public static class GameSettings
    {
        public enum Controls { Buttons, Wheel, Tilt }
        public static Controls Layout { get => (Controls)GetI("dk_ctrl", 0); set => SetI("dk_ctrl", (int)value); }
        public static float Assist { get => GetF("dk_assist", 0.75f); set => SetF("dk_assist", value); }
        public static float Music { get => GetF("dk_music", 0.55f); set { SetF("dk_music", value); Sfx.MusicVolume = value; } }
        public static float Effects { get => GetF("dk_sfx", 1f); set { SetF("dk_sfx", value); Sfx.SfxVolume = value; } }
        public static bool Haptics { get => GetI("dk_hap", 1) == 1; set => SetI("dk_hap", value ? 1 : 0); }
        public static bool EasternNumerals { get => GetI("dk_num", 1) == 1; set { SetI("dk_num", value ? 1 : 0); UIKit.EasternNumerals = value; } }

        public static void Load()
        {
            Sfx.MusicVolume = Music;
            Sfx.SfxVolume = Effects;
            UIKit.EasternNumerals = EasternNumerals;
        }

        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch { return d; } }
        static void SetI(string k, int v) { try { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); } catch { } }
        static float GetF(string k, float d) { try { return PlayerPrefs.GetFloat(k, d); } catch { return d; } }
        static void SetF(string k, float v) { try { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); } catch { } }
    }
}
