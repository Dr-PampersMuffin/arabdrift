using System.Collections.Generic;
using UnityEngine;

namespace DriftKuwait
{
    /// Free roam with a purpose: the HUD always shows the Chapter goal (cash toward the garage instalment), every banked
    /// drift pays cash with a "+N كاش" note, and each career challenge stands in the street as a gold beacon: roll into
    /// it slowly to start that challenge.
    public class FreeRoam : MonoBehaviour
    {
        public DriftCarController car;
        public DriftScorer scorer;
        public DriveHUD hud;

        class Beacon { public Career.EventDef ev; public Vector3 pos; public GameObject go; }
        readonly List<Beacon> beacons = new List<Beacon>();
        bool toldReady;
        float enterHold;

        void Start()
        {
            scorer.OnBank += pts => { long cash = pts / 2; if (cash > 0) hud.Callout("+" + UIKit.Dig(cash) + " كاش", UIKit.Gold); };
            var mat = TyreFX.ParticleMaterial("DK_BeaconMat", Texture2D.whiteTexture, new Color(1f, 0.74f, 0.2f, 0.4f));
            foreach (var ev in Career.Events)
            {
                if (ev.kind == Career.Kind.FreeRoam || !Career.Unlocked(ev)) continue;
                if ((new Vector3(ev.spawn.x, 0f, ev.spawn.z) - new Vector3(car.transform.position.x, 0f, car.transform.position.z)).sqrMagnitude < 400f) continue;   // not on top of the start
                var b = new Beacon { ev = ev, pos = new Vector3(ev.spawn.x, 0f, ev.spawn.z) };
                b.go = MakeBeacon("Beacon_" + ev.id, b.pos, mat);
                if (ev == Story.Next().ev) b.go.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);   // the next level's beacon stands taller
                beacons.Add(b);
            }
        }

        /// A soft light column with a ring on the road and a warm glow: event starts, delivery drop-offs.
        public static GameObject MakeBeacon(string name, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            {
                var b = new Beacon { go = go };
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(col.GetComponent<Collider>());
                col.transform.SetParent(b.go.transform, false);
                col.transform.localPosition = new Vector3(0f, 9f, 0f);
                col.transform.localScale = new Vector3(3.2f, 9f, 3.2f);
                col.GetComponent<Renderer>().sharedMaterial = mat;
                col.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ring = new GameObject("Ring").AddComponent<LineRenderer>();
                ring.transform.SetParent(b.go.transform, false);
                ring.loop = true; ring.useWorldSpace = false; ring.positionCount = 48; ring.widthMultiplier = 0.35f;
                ring.material = mat; ring.alignment = LineAlignment.TransformZ;
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                for (int i = 0; i < 48; i++) { float a = i / 48f * Mathf.PI * 2f; ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 5f, Mathf.Sin(a) * 5f, 0f)); }
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(b.go.transform, false); l.transform.localPosition = Vector3.up * 3f;
                l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.3f); l.range = 14f; l.intensity = 6f; l.shadows = LightShadows.None;
            }
            return go;
        }

        Transform arrow;
        Story.Goal goal;
        float goalCheck;
        string lastGoalKey;

        /// A gold chevron floating over the car that points the way to the next level's beacon.
        Transform MakeArrow()
        {
            var mk = new MeshKit();
            void Arm(float s)
            {
                Vector3 a0 = new Vector3(s * 0.95f, 0f, -0.45f), a1 = new Vector3(0f, 0f, 0.55f);
                Vector3 w = new Vector3(0f, 0f, -0.42f);
                Vector3 b0 = a0 + w, b1 = a1 + w;
                mk.Quad(a0, a1, b1, b0, Vector3.up);
                mk.Quad(b0, b1, a1, a0, Vector3.up);   // both windings: always visible
            }
            Arm(1f); Arm(-1f);
            var go = mk.ToObject("DK_GuideArrow", null, TyreFX.ParticleMaterial("DK_GuideMat", Texture2D.whiteTexture, new Color(1f, 0.78f, 0.18f, 0.95f)));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * 1.3f;
            return go.transform;
        }

        void Update()
        {
            if (car == null || hud == null || hud.InPursuit) { if (arrow != null) arrow.gameObject.SetActive(false); return; }

            // The next level, always on screen
            goalCheck -= Time.deltaTime;
            if (goalCheck <= 0f) { goalCheck = 0.5f; goal = Story.Next(); }
            hud.MissionTitle = goal.title;
            hud.MissionSub = goal.sub;
            Vector3 cp = car.transform.position;
            if (goal.ev != null)
            {
                Vector3 tgt = new Vector3(goal.ev.spawn.x, 0f, goal.ev.spawn.z);
                Vector3 to = tgt - cp; to.y = 0f;
                hud.MissionTime = UIKit.Dig((long)to.magnitude) + " م";
                if (arrow == null) arrow = MakeArrow();
                bool show = to.magnitude > 14f;
                arrow.gameObject.SetActive(show);
                if (show)
                {
                    arrow.position = cp + Vector3.up * (2.6f + Mathf.Sin(Time.time * 3f) * 0.12f);
                    arrow.rotation = Quaternion.Slerp(arrow.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
                string key = goal.ev.id;
                if (key != lastGoalKey) { lastGoalKey = key; hud.Pop(goal.ev.titleAr, UIKit.Gold); hud.Callout("اتبع السهم الذهبي", UIKit.Gold); }
            }
            else
            {
                if (arrow != null) arrow.gameObject.SetActive(false);
                long need = Story.Chapter == 1 ? Story.ToInstalment : Story.Remaining;
                hud.MissionTime = goal.done ? "" : UIKit.Dig(Wallet.Cash) + " / " + UIKit.Dig(need);
                if (goal.ready && !toldReady) { toldReady = true; hud.Pop("ارجع وسدّد!", UIKit.Gold); }
            }

            // Roll into a beacon to start its challenge
            Vector3 p = car.transform.position; p.y = 0f;
            Beacon at = null;
            foreach (var b in beacons) if ((b.pos - p).sqrMagnitude < 25f) at = b;
            if (at != null && car.SpeedKmh < 35f)
            {
                if (enterHold <= 0f) hud.Callout(at.ev.titleAr, UIKit.Gold);
                enterHold += Time.deltaTime;
                if (enterHold > 1.2f && GameFlow.I != null) { enterHold = -999f; GameFlow.I.StartEvent(at.ev); }
            }
            else if (enterHold > 0f) enterHold = 0f;
        }

        void OnDestroy()
        {
            if (arrow != null) Destroy(arrow.gameObject);
            foreach (var b in beacons) if (b.go != null) Destroy(b.go);
        }
    }
}
