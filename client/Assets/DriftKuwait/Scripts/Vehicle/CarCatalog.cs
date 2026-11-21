using UnityEngine;

namespace DriftKuwait
{
    /// The launch garage from the blueprint (section 3): five street cars with Arabic names, plus the patrol sedan.
    /// Bodies come from Resources/DriftKuwait/Cars/<id>/ (Tripo model -> tools/art/car_prep.py -> body.obj + maps).
    /// A car without a 3D body yet still shows in the garage with its reference art and "قريباً" (coming soon).
    public class CarSpec
    {
        public string id, nameAr, nameEn, taglineAr, cylinderAr;
        public float topSpeed, accel, handling, braking, drift;      // garage bars, 0..10
        public float mass, baseTorque, maxRpm, turboSpool, boost;     // physics
        public float frontGrip, rearGrip, handbrakeGrip;
        public bool turbo;
        public int rimSpokes;          // 0 = mesh/cross-spoke wheel
        public Color rimColor, rimLip;
        public string engineSet;       // ElevenLabs engine loop set (Audio/Engines/<set>_*)
        public bool police;
    }

    public static class CarCatalog
    {
        public static readonly CarSpec[] Garage =
        {
            new CarSpec { id = "shaheen", nameAr = "الشاهين", nameEn = "AL SHAHEEN", taglineAr = "تيربو .. انطلاقة قوية", cylinderAr = "تيربو ٤ سلندر",
                topSpeed = 7.4f, accel = 7.8f, handling = 7.2f, braking = 6.8f, drift = 8.0f,
                mass = 1150f, baseTorque = 175f, maxRpm = 7200f, turboSpool = 0.55f, boost = 0.55f, turbo = true,
                frontGrip = 1.15f, rearGrip = 1.0f, handbrakeGrip = 0.42f,
                rimSpokes = 8, rimColor = new Color(0.05f, 0.05f, 0.055f), rimLip = new Color(0.82f, 0.82f, 0.84f), engineSet = "shaheen" },
            new CarSpec { id = "ghazal", nameAr = "الغزال", nameEn = "AL GHAZAL", taglineAr = "زلق سلس .. زاوية طويلة", cylinderAr = "٦ سلندر",
                topSpeed = 7.8f, accel = 6.6f, handling = 7.6f, braking = 6.5f, drift = 8.4f,
                mass = 1250f, baseTorque = 215f, maxRpm = 6600f, turboSpool = 0.7f, boost = 0.35f, turbo = true,
                frontGrip = 1.12f, rearGrip = 1.0f, handbrakeGrip = 0.45f,
                rimSpokes = 0, rimColor = new Color(0.78f, 0.78f, 0.8f), rimLip = new Color(0.85f, 0.85f, 0.87f), engineSet = "ghazal" },
            new CarSpec { id = "faris", nameAr = "الفارس", nameEn = "AL FARIS", taglineAr = "عزم قوي .. تحكم صعب", cylinderAr = "V8 ٥٫٠",
                topSpeed = 8.0f, accel = 8.4f, handling = 5.8f, braking = 6.0f, drift = 7.6f,
                mass = 1380f, baseTorque = 360f, maxRpm = 6000f, turboSpool = 0.1f, boost = 0f, turbo = false,
                frontGrip = 1.1f, rearGrip = 0.92f, handbrakeGrip = 0.4f,
                rimSpokes = 10, rimColor = new Color(0.75f, 0.75f, 0.77f), rimLip = new Color(0.8f, 0.8f, 0.82f), engineSet = "faris" },
            new CarSpec { id = "wanait", nameAr = "الونيت", nameEn = "AL WANAIT", taglineAr = "ملك البر .. تطعيس", cylinderAr = "٤ سلندر",
                topSpeed = 5.6f, accel = 5.4f, handling = 5.2f, braking = 5.6f, drift = 6.2f,
                mass = 1320f, baseTorque = 190f, maxRpm = 5400f, turboSpool = 0.1f, boost = 0f, turbo = false,
                frontGrip = 1.05f, rearGrip = 0.95f, handbrakeGrip = 0.38f,
                rimSpokes = 5, rimColor = new Color(0.86f, 0.86f, 0.88f), rimLip = new Color(0.86f, 0.86f, 0.88f), engineSet = "wanait" },
            new CarSpec { id = "abusitta", nameAr = "أبو ستة", nameEn = "ABU SITTA", taglineAr = "قوة هادئة .. زلق طويل", cylinderAr = "٦ سلندر",
                topSpeed = 7.0f, accel = 6.5f, handling = 6.0f, braking = 6.8f, drift = 8.5f,
                mass = 1360f, baseTorque = 240f, maxRpm = 6200f, turboSpool = 0.1f, boost = 0f, turbo = false,
                frontGrip = 1.12f, rearGrip = 0.97f, handbrakeGrip = 0.44f,
                rimSpokes = 0, rimColor = new Color(0.78f, 0.62f, 0.30f), rimLip = new Color(0.88f, 0.88f, 0.9f), engineSet = "abusitta" },
        };

        public static readonly CarSpec Patrol = new CarSpec
        {
            id = "dawriya", nameAr = "دورية سيدان", nameEn = "PATROL", taglineAr = "الدورية", cylinderAr = "V8", police = true,
            mass = 1700f, baseTorque = 330f, maxRpm = 5200f, turboSpool = 0.1f, boost = 0f,
            frontGrip = 1.1f, rearGrip = 1.05f, handbrakeGrip = 0.5f,
            rimSpokes = 0, rimColor = new Color(0.08f, 0.08f, 0.08f), rimLip = new Color(0.6f, 0.6f, 0.62f), engineSet = "dawriya",
        };

        public static CarSpec Get(string id)
        {
            foreach (var c in Garage) if (c.id == id) return c;
            return id == Patrol.id ? Patrol : Garage[0];
        }

        public static bool Has3D(string id) => Resources.Load<TextAsset>($"DriftKuwait/Cars/{id}/car") != null;

        const string SelKey = "dk_selected_car";
        public static string SelectedId
        {
            get { try { return PlayerPrefs.GetString(SelKey, "shaheen"); } catch { return "shaheen"; } }
            set { try { PlayerPrefs.SetString(SelKey, value); PlayerPrefs.Save(); } catch { } }
        }
    }
}
