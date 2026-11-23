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
            scorer.OnBank += pts => { long cash = pts / 4; if (cash > 0) hud.Callout("+" + UIKit.Dig(cash) + " كاش", UIKit.Gold); };
            var mat = TyreFX.ParticleMaterial("DK_BeaconMat", Texture2D.whiteTexture, new Color(1f, 0.74f, 0.2f, 0.4f));
            foreach (var ev in Career.Events)
            {
                if (ev.kind == Career.Kind.FreeRoam || !Career.Unlocked(ev)) continue;
                if ((new Vector3(ev.spawn.x, 0f, ev.spawn.z) - new Vector3(car.transform.position.x, 0f, car.transform.position.z)).sqrMagnitude < 400f) continue;   // not on top of the start
                var b = new Beacon { ev = ev, pos = new Vector3(ev.spawn.x, 0f, ev.spawn.z) };
                b.go = new GameObject("Beacon_" + ev.id);
                b.go.transform.position = b.pos;
                // A soft gold light column and a ring on the road
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
                beacons.Add(b);
            }
        }

        void Update()
        {
            if (car == null || hud == null || hud.InPursuit) return;
            // The goal, always on screen
            long need = Story.Chapter == 1 ? Story.ToInstalment : Story.Remaining;
            hud.MissionTitle = "أنقذ الكراج";
            if (need <= 0) { hud.MissionSub = "خلصت الديون .. عفيه!"; hud.MissionTime = ""; }
            else if (Wallet.Cash >= need)
            {
                hud.MissionSub = "عندك المبلغ! ارجع الكراج وسدّد";
                hud.MissionTime = UIKit.Dig(Wallet.Cash) + " / " + UIKit.Dig(need);
                if (!toldReady) { toldReady = true; hud.Pop("ارجع وسدّد!", UIKit.Gold); }
            }
            else
            {
                hud.MissionSub = "قحّص واجمع كاش لأول قسط";
                hud.MissionTime = UIKit.Dig(Wallet.Cash) + " / " + UIKit.Dig(need);
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
            foreach (var b in beacons) if (b.go != null) Destroy(b.go);
        }
    }
}
