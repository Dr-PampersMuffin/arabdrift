using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// The career: events per district, each with a goal, three star targets and a reward. Stars unlock the next events
    /// (and later districts). Saved locally; the server validates results once online competition arrives.
    public static class Career
    {
        public enum Kind { FreeRoam, Score, Zone, Chain, Escape, Run }

        public class EventDef
        {
            public string id, district, titleAr, goalAr, howAr;
            public Kind kind;
            public float seconds;              // time limit (0 = none)
            public long[] stars;               // score for 1, 2, 3 stars (Escape: seconds, lower is better)
            public int cash, rep;              // reward for the first time at each star level
            public int needStars;              // career stars needed to unlock
            public Vector3 spawn; public float yaw;
            public Vector3 zone; public float zoneRadius;
            public Vector2 mapPos;             // 0..1 on the district map
        }

        public static readonly List<EventDef> Events = new List<EventDef>
        {
            new EventDef { id = "salwa_free", district = "salwa", kind = Kind.FreeRoam, titleAr = "تقحيص حر", goalAr = "سوق براحتك وتعلّم على السيارة",
                howAr = "ماكو وقت ولا نجوم. تدرّب على الهاندبريك وعكس السكّان.", spawn = new Vector3(-3f, 0.6f, -140f), mapPos = new Vector2(0.5f, 0.68f) },
            new EventDef { id = "salwa_first", district = "salwa", kind = Kind.Score, titleAr = "أول تقحيص", goalAr = "جمّع نقاط تقحيص خلال دقيقة",
                howAr = "اسحب الهاندبريك عشان تبدأ التقحيص، وعاكس السكّان مع البانزين عشان تكمل.", seconds = 60f, stars = new long[] { 600, 2000, 4500 },
                cash = 1500, rep = 20, spawn = new Vector3(-3f, 0.6f, -390f), mapPos = new Vector2(0.5f, 0.92f) },
            new EventDef { id = "salwa_dallah", district = "salwa", kind = Kind.Zone, titleAr = "دوّار الدلّة", goalAr = "قحّص حول دوّار الدلّة",
                howAr = "النقاط تنحسب بس داخل الدائرة حول الدوّار. لف عليه وخل الزاوية كبيرة.", seconds = 75f, stars = new long[] { 1200, 3500, 7000 },
                cash = 2500, rep = 35, needStars = 1, spawn = new Vector3(-4f, 0.6f, -70f), zone = new Vector3(0f, 0f, 0f), zoneRadius = 38f, mapPos = new Vector2(0.5f, 0.5f) },
            new EventDef { id = "salwa_chain", district = "salwa", kind = Kind.Chain, titleAr = "السلسلة الطويلة", goalAr = "أطول سلسلة تقحيص بدون ما توقف",
                howAr = "تنحسب أكبر سلسلة وحدة. لا تخلي السيارة تعتدل ولا تصدم.", seconds = 90f, stars = new long[] { 1200, 3500, 7000 },
                cash = 3000, rep = 45, needStars = 3, spawn = new Vector3(3f, 0.6f, 380f), yaw = 180f, mapPos = new Vector2(0.5f, 0.1f) },
            new EventDef { id = "salwa_block5", district = "salwa", kind = Kind.Score, titleAr = "شارع قطعة ٥", goalAr = "أكبر رصيد نقاط خلال دقيقة ونص",
                howAr = "الشارع الطويل والدوّار الصغير. قرّب من الجدران عشان نقاط الاقتراب.", seconds = 90f, stars = new long[] { 3000, 8000, 15000 },
                cash = 4000, rep = 60, needStars = 5, spawn = new Vector3(-150f, 0.6f, -240f), yaw = 90f, zone = new Vector3(0f, 0f, -240f), mapPos = new Vector2(0.18f, 0.78f) },
            new EventDef { id = "salwa_escape", district = "salwa", kind = Kind.Escape, titleAr = "هروب من الدورية", goalAr = "اهرب من الدورية بأسرع وقت",
                howAr = "الدورية وراك من البداية. ابتعد عنها لين تفقدك. لو وقفت جنبها تنحجز السيارة.", seconds = 150f, stars = new long[] { 150, 80, 45 },
                cash = 5000, rep = 80, needStars = 7, spawn = new Vector3(3f, 0.6f, 200f), yaw = 180f, mapPos = new Vector2(0.82f, 0.3f) },
            // Need for Speed style: race the crew out of Salwa down the desert highway to the camp, lose the patrol on the way.
            new EventDef { id = "salwa_desert_run", district = "salwa", kind = Kind.Run, titleAr = "سباق البر", goalAr = "اسبق الربع للمخيم واهرب من الدورية",
                howAr = "اطلع من سلوى جنوب للبر. اسبقهم للمخيم، ولو لحقتك الدورية لا توقف.", seconds = 210f, stars = new long[] { 1, 2, 3 },
                cash = 6000, rep = 90, needStars = 2, spawn = new Vector3(2f, 0.6f, -392f), yaw = 180f, mapPos = new Vector2(0.5f, 0.98f) },
        };

        public static EventDef Get(string id) => Events.Find(e => e.id == id);
        public static int Stars(string id) { try { return PlayerPrefs.GetInt("dk_star_" + id, 0); } catch { return 0; } }
        public static long Best(string id) { try { return long.Parse(PlayerPrefs.GetString("dk_best_" + id, "0")); } catch { return 0; } }
        public static int TotalStars { get { int n = 0; foreach (var e in Events) n += Stars(e.id); return n; } }
        public static bool Unlocked(EventDef e) => TotalStars >= e.needStars;

        public static int StarsFor(EventDef e, long result)
        {
            if (e.stars == null) return 0;
            int n = 0;
            for (int i = 0; i < 3; i++)
                if (e.kind == Kind.Escape ? (result > 0 && result <= e.stars[i]) : result >= e.stars[i]) n = i + 1;
            return n;
        }

        /// Saves the result; returns (stars earned now, cash, rep, new best?).
        public static (int stars, int cash, int rep, bool best) Record(EventDef e, long result)
        {
            int got = StarsFor(e, result), had = Stars(e.id);
            long best = Best(e.id);
            bool isBest = e.kind == Kind.Escape ? (result > 0 && (best == 0 || result < best)) : result > best;
            int cash = 0, rep = 0;
            if (got > had)
            {
                // Reward each newly reached star level once; replays still pay a little.
                cash = e.cash * (got - had) / 3 + e.cash / 6 * (got - had);
                rep = e.rep * (got - had) / 3;
            }
            else if (got > 0) { cash = e.cash / 10; rep = 1; }
            try
            {
                if (got > had) PlayerPrefs.SetInt("dk_star_" + e.id, got);
                if (isBest) PlayerPrefs.SetString("dk_best_" + e.id, result.ToString());
                PlayerPrefs.Save();
            }
            catch { }
            Wallet.Add(cash, rep);
            return (got, cash, rep, isBest);
        }
    }
}
