using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DriftKuwait.Preview
{
    /// Cloud preview: runs the real game in play mode on the CI machine, drives the car with scripted inputs, and saves
    /// screenshots to <repo>/previews/*.png (the preview workflow publishes them). No TestFlight build is used.
    /// Overlay canvases are switched to the camera so the HUD shows up in the captures.
    public class PreviewCapture : IPrebuildSetup
    {
        /// Runs in the editor before play mode: the same project set-up the iOS build does (URP, car materials).
        public void Setup()
        {
            var t = System.Type.GetType("BuildScript, Assembly-CSharp-Editor");
            var m = t?.GetMethod("PrepareForTests");
            if (m != null) m.Invoke(null, null);
            else Debug.LogWarning("[Preview] BuildScript.PrepareForTests not found");
        }

        const int W = 1920, H = 886;   // iPhone landscape aspect (19.5:9)
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../previews"));

        [UnityTest, Timeout(2700000)]
        public IEnumerator CaptureScenes()
        {
            Directory.CreateDirectory(OutDir);
            Debug.Log($"[Preview] graphics {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}, out {OutDir}");
            PreviewHooks.Active = true;

            // Menu and garage first.
            if (GameFlow.I == null) GameBootstrap.Boot();
            yield return new WaitForSeconds(1.0f);
            foreach (var shot in PreviewHooks.MenuShots)
            {
                yield return shot.setup();
                yield return new WaitForSeconds(1.0f);
                Capture(shot.name);
                shot.teardown?.Invoke();
                yield return null;
            }

            Debug.Log("[Preview] starting drive");
            PreviewHooks.StartDrive?.Invoke();
            for (int fr = 0; fr < 6; fr++)
            {
                float t0 = Time.realtimeSinceStartup;
                yield return null;
                Debug.Log($"[Preview] drive frame {fr}: {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms");
            }
            yield return new WaitForSeconds(2.0f);
            var car = Object.FindAnyObjectByType<DriftCarController>();
            Assert.IsNotNull(car, "no car spawned");
            // Start at the south end of the avenue: a long straight with room for a slide.
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -390f), Quaternion.identity);
            car.counterSteerAssist = 1f;
            yield return new WaitForSeconds(1.0f);
            Capture("10_start");
            {
                // Subtitle box layout check: a two-line line from بو نبيل
                var hudSay = Object.FindAnyObjectByType<DriveHUD>();
                if (hudSay != null)
                {
                    hudSay.Say("ev_salwa_first", "por_abu_rashid", "بو نبيل:", "الشارع الطويل والدوار الصغير. قرب من الجدران عشان نقاط الاقتراب.");
                    yield return null; yield return null;
                    Capture("10b_dialog");
                }
            }

            // Accelerate down the straight, then a handbrake flick into a held drift.
            CarInput.TouchThrottle = 1f;
            yield return new WaitForSeconds(3.5f);
            Capture("11_speed");
            // Timed in physics steps, so a slow CI renderer can't hold the handbrake longer than a player would
            int Steps(float sec) => Mathf.RoundToInt(sec / Time.fixedDeltaTime);
            float hb0 = Time.fixedTime;
            CarInput.TouchSteer = 1f; CarInput.TouchHandbrake = true;
            for (int k = Steps(0.35f); k > 0; k--) yield return new WaitForFixedUpdate();
            CarInput.TouchHandbrake = false;
            CarInput.TouchSteer = -0.25f;            // catch the slide with counter-steer
            Debug.Log($"[Preview] handbrake held {Time.fixedTime - hb0:F2}s (physics time)");
            for (int k = 0; k < 9; k++)
            {
                for (int j = Steps(0.1f); j > 0; j--) yield return new WaitForFixedUpdate();
                Debug.Log($"[Preview] drift t={Time.fixedTime - hb0:F2}s angle {car.DriftAngle:F0} speed {car.SpeedKmh:F0} yawRate {Vector3.Dot(car.Body.angularVelocity, car.transform.up):F2} steer {car.SteerAngle:F0}");
            }
            Capture("12_drift");
            yield return new WaitForSeconds(0.7f);
            Capture("13_drift_smoke");
            {
                int n = 0; string mat = "";
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                {
                    n += ps.particleCount;
                    if (ps.name == "TyreSmoke" && ps.particleCount > 0)
                    {
                        var arr = new ParticleSystem.Particle[1];
                        ps.GetParticles(arr, 1);
                        Debug.Log($"[Preview] smoke sample pos {arr[0].position} size {arr[0].GetCurrentSize(ps):F2} colour {arr[0].GetCurrentColor(ps)} car {car.transform.position} renderer enabled {ps.GetComponent<ParticleSystemRenderer>().enabled} layer {ps.gameObject.layer}");
                    }
                    var pr = ps.GetComponent<ParticleSystemRenderer>();
                    if (ps.name == "TyreSmoke" && pr != null && pr.sharedMaterial != null)
                        mat = pr.sharedMaterial.name + " / " + pr.sharedMaterial.shader.name + " supported=" + pr.sharedMaterial.shader.isSupported;
                }
                Debug.Log($"[Preview] particles alive {n}, smoke material {mat}, speed {car.SpeedKmh:F0} angle {car.DriftAngle:F0}");
            }
            {
                // Diagnostic: the same smoke with an unlit material, to tell a lighting/shader problem from an emission one.
                var swapped = new System.Collections.Generic.List<(ParticleSystemRenderer, Material)>();
                var unlit = TyreFX.ParticleMaterial("DK_SmokeDiag", Resources.Load<Texture2D>("DriftKuwait/FX/smoke_puff"), new Color(1f, 1f, 1f, 1f));
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    if (ps.name == "TyreSmoke") { var pr = ps.GetComponent<ParticleSystemRenderer>(); swapped.Add((pr, pr.sharedMaterial)); pr.sharedMaterial = unlit; }
                Capture("13b_smoke_unlit");
                foreach (var (pr, m) in swapped) pr.sharedMaterial = m;
            }
            CarInput.TouchSteer = -1f; CarInput.TouchHandbrake = true;
            yield return new WaitForSeconds(0.3f);
            CarInput.TouchHandbrake = false; CarInput.TouchSteer = 0.25f;
            yield return new WaitForSeconds(1.1f);
            Capture("14_transition");
            // Lift off high in the revs: exhaust flames and pops
            CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 0f;
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[Preview] before lift rpm {car.Rpm:F0}");
            CarInput.TouchThrottle = 0f;
            yield return new WaitForSeconds(0.05f);
            Capture("14b_backfire");
            CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f; CarInput.TouchBrake = 1f;
            yield return new WaitForSeconds(1.5f);
            CarInput.TouchBrake = 0f;

            // Showroom angles of the car itself (front three-quarter, rear three-quarter, side).
            var rb = car.Body;
            rb.isKinematic = true;
            car.transform.SetPositionAndRotation(new Vector3(-3f, car.transform.position.y, -300f), Quaternion.Euler(0f, 20f, 0f));
            yield return null;
            var cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.fieldOfView = 34f;
            var main = Camera.main;
            if (main != null) { cam.backgroundColor = main.backgroundColor; cam.clearFlags = main.clearFlags; }
            Vector3 c = car.transform.position + Vector3.up * 0.7f;
            (string name, Vector3 off)[] angles =
            {
                ("20_car_front34", car.transform.TransformDirection(new Vector3(3.6f, 0.9f, 5.6f))),
                ("21_car_rear34", car.transform.TransformDirection(new Vector3(-3.8f, 1.1f, -5.4f))),
                ("22_car_side", car.transform.TransformDirection(new Vector3(7.2f, 0.6f, 0.2f))),
                ("23_car_wheel", car.transform.TransformDirection(new Vector3(2.2f, 0.1f, 2.6f))),
            };
            foreach (var a in angles)
            {
                cam.transform.position = c + a.off;
                cam.transform.LookAt(a.name == "23_car_wheel" ? car.transform.TransformPoint(new Vector3(0.75f, 0.35f, 1.4f)) : c);
                yield return null;
                CaptureWith(cam, a.name, false);
            }

            // Street-level realism checks: a residential street of villas, the avenue, the dallah roundabout.
            (string name, Vector3 pos, Vector3 look)[] streets =
            {
                ("15_street_villas", new Vector3(-140f, 1.5f, -162f), new Vector3(-60f, 3.5f, -158f)),
                ("16_street_gate", new Vector3(-120f, 1.4f, -78f), new Vector3(-112f, 2.6f, -70f)),
                ("17_avenue", new Vector3(4f, 2.2f, -200f), new Vector3(-6f, 3f, -90f)),
                ("18_roundabout", new Vector3(30f, 4f, -32f), new Vector3(0f, 5f, 0f)),
                ("19_mosque", new Vector3(6f, 2.2f, -100f), new Vector3(-26f, 8f, -122f)),
                ("19b_shops", new Vector3(-6f, 2f, -104f), new Vector3(18f, 3f, -122f)),
                ("40_desert_highway", new Vector3(8f, 2.2f, -640f), new Vector3(40f, 2f, -820f)),
                ("41_desert_camp", new Vector3(10f, 3.5f, -1680f), new Vector3(-14f, 1f, -1734f)),
                ("42_kuwait_towers", new Vector3(4f, 2.2f, 380f), new Vector3(70f, 60f, 820f)),
            };
            cam.fieldOfView = 62f;
            foreach (var s in streets)
            {
                cam.transform.position = s.pos;
                cam.transform.LookAt(s.look);
                yield return null;
                CaptureWith(cam, s.name, false);
            }

            // The crowd: the group nearest the dallah roundabout, seen from the road
            {
                Transform best = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name == "CrowdGroup" && (best == null || t.position.sqrMagnitude < best.position.sqrMagnitude)) best = t;
                if (best != null)
                {
                    Vector3 toC = (-best.position).normalized; toC.y = 0f;
                    cam.transform.position = best.position + toC * 14f + Vector3.up * 1.8f;
                    cam.transform.LookAt(best.position + Vector3.up * 1.2f);
                    yield return null;
                    CaptureWith(cam, "43_crowd", false);
                }
                else Debug.LogWarning("[Preview] no crowd groups");
            }

            // Overview of the whole district from above (layout check).
            {
                var top = new GameObject("OverviewCam").AddComponent<Camera>();
                top.orthographic = true; top.orthographicSize = 260f;
                top.transform.position = new Vector3(0f, 400f, 0f);
                top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                top.farClipPlane = 800f;
                yield return null;
                CaptureWith(top, "05_overview", false);
                Object.Destroy(top.gameObject);
            }

            // Repair job car: a customer's taxi built as a drivable car (traffic body + physics), driven a few seconds.
            {
                var taxi = CarFactory.Build("tr_taxi", new Vector3(-95f, 0.6f, -120f), Quaternion.identity);
                if (taxi != null)
                {
                    var ti = taxi.GetComponent<CarInput>(); ti.ai = true; ti.aiThrottle = 0.7f;
                    foreach (var c2 in taxi.GetComponents<MonoBehaviour>()) if (c2 is EngineAudio || c2 is TyreAudio) Object.Destroy(c2);
                    yield return new WaitForSeconds(2.5f);
                    var tc = new GameObject("TaxiCam").AddComponent<Camera>();
                    tc.fieldOfView = 55f;
                    tc.transform.position = taxi.transform.TransformPoint(new Vector3(2.5f, 2.2f, -7.5f));
                    tc.transform.LookAt(taxi.transform.position + Vector3.up * 0.8f);
                    yield return null;
                    CaptureWith(tc, "50_job_taxi", false);
                    Debug.Log($"[Preview] taxi speed {taxi.GetComponent<DriftCarController>().SpeedKmh:F0} km/h at {taxi.transform.position}");
                    Object.Destroy(tc.gameObject); Object.Destroy(taxi);
                }
            }

            // Stress test: boxed in front and back while flooring it, then upside down. The StuckGuard must free the car both times.
            {
                rb.isKinematic = false;
                car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -250f), Quaternion.identity);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                var front = GameObject.CreatePrimitive(PrimitiveType.Cube); front.transform.position = new Vector3(-3f, 1f, -246.6f); front.transform.localScale = new Vector3(4f, 2f, 1f);
                var back = GameObject.CreatePrimitive(PrimitiveType.Cube); back.transform.position = new Vector3(-3f, 1f, -253.4f); back.transform.localScale = new Vector3(4f, 2f, 1f);
                yield return new WaitForSeconds(0.5f);
                Vector3 before = car.transform.position;
                CarInput.TouchThrottle = 1f;
                yield return new WaitForSeconds(3.2f);
                CarInput.TouchThrottle = 0f;
                float moved = Vector3.Distance(before, car.transform.position);
                Debug.Log($"[StressTest] boxed-in: moved {moved:F1} m -> {(moved > 3f ? "PASS (freed)" : "FAIL (still stuck)")}");
                Object.Destroy(front); Object.Destroy(back);
                yield return new WaitForSeconds(0.5f);
                car.transform.SetPositionAndRotation(car.transform.position + Vector3.up * 1.5f, Quaternion.Euler(0f, 0f, 180f));
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                yield return new WaitForSeconds(2.6f);
                float up = Vector3.Dot(car.transform.up, Vector3.up);
                Debug.Log($"[StressTest] flipped: up {up:F2} -> {(up > 0.9f ? "PASS (upright)" : "FAIL (still flipped)")}");
            }

            // Wall test (the reported glitch): slide into a real villa wall, then try to back out.
            // First with the StuckGuard off, to see whether the bare physics traps the car, then with it on.
            {
                var guard = car.GetComponent<StuckGuard>();
                Vector3 street = new Vector3(-140f, 0.6f, -162f);
                Vector3 wallDir = Vector3.zero; float wallDist = 99f;
                foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    if (Physics.Raycast(street + Vector3.up * 0.4f, d, out var wh, 30f, ~((1 << 2) | (1 << 29)), QueryTriggerInteraction.Ignore) && wh.distance < wallDist) { wallDist = wh.distance; wallDir = d; }
                Debug.Log($"[StressTest] wall at {wallDist:F1} m towards {wallDir}");
                for (int pass = 0; pass < 2 && wallDir != Vector3.zero; pass++)
                {
                    if (guard != null) guard.enabled = pass == 1;
                    car.transform.SetPositionAndRotation(street - wallDir * Mathf.Max(0f, 14f - wallDist), Quaternion.LookRotation(Quaternion.AngleAxis(25f, Vector3.up) * wallDir));
                    rb.linearVelocity = car.transform.forward * 14f; rb.angularVelocity = Vector3.zero;
                    CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 0.6f;
                    yield return new WaitForSeconds(2.0f);
                    CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f;
                    Debug.Log($"[StressTest] pass {pass}: after hitting wall pos {car.transform.position} up {Vector3.Dot(car.transform.up, Vector3.up):F2} grounded {car.GroundedCount} gear {car.Gear} speed {car.SpeedKmh:F1}");
                    Vector3 hitPos = car.transform.position;
                    CarInput.TouchBrake = 1f;   // automatic gearbox: holding the brake when stopped engages reverse
                    yield return new WaitForSeconds(3.0f);
                    CarInput.TouchBrake = 0f;
                    float back = Vector3.Distance(hitPos, car.transform.position);
                    Debug.Log($"[StressTest] pass {pass} ({(pass == 0 ? "guard off" : "guard on")}): reversed {back:F1} m, gear {car.Gear}, grounded {car.GroundedCount} -> {(back > 2f ? "PASS" : "STUCK")}");
                }
                if (guard != null) guard.enabled = true;
            }

            // Spin tests, driven like a player with the default assist (0.5): gas and steering only, no handbrake.
            // A car may slide big but must not spin all the way round.
            {
                float keepAssist = car.counterSteerAssist;
                car.counterSteerAssist = Wallet.Assist;
                var cases = new (string name, float startKmh, float steer, float secs)[]
                {
                    ("gas + half steer at 45 km/h", 45f, 0.6f, 3f),
                    ("gas + full steer at 45 km/h", 45f, 1f, 3f),
                    ("gas + full steer from a stop", 0f, 1f, 3f),
                    ("gas + full steer at 70 km/h", 70f, 1f, 3f),
                };
                foreach (var c in cases)
                {
                    car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -300f), Quaternion.identity);
                    rb.linearVelocity = car.transform.forward * (c.startKmh / 3.6f); rb.angularVelocity = Vector3.zero;
                    CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f; CarInput.TouchHandbrake = false; CarInput.TouchBrake = 0f;
                    for (int k = 0; k < 10; k++) yield return new WaitForFixedUpdate();
                    CarInput.TouchThrottle = 1f; CarInput.TouchSteer = c.steer;
                    float turned = 0f, maxA = 0f;
                    int n = Mathf.RoundToInt(c.secs / Time.fixedDeltaTime);
                    for (int k = 0; k < n; k++)
                    {
                        yield return new WaitForFixedUpdate();
                        turned += Vector3.Dot(car.Body.angularVelocity, Vector3.up) * Time.fixedDeltaTime * Mathf.Rad2Deg;
                        if (car.SpeedKmh > 10f) maxA = Mathf.Max(maxA, Mathf.Abs(car.DriftAngle));
                    }
                    bool ok = maxA < 88f && Mathf.Abs(turned) < 330f || (c.startKmh == 0f && Mathf.Abs(turned) < 360f);
                    Debug.Log($"[StressTest] spin '{c.name}': max angle {maxA:F0}, turned {turned:F0} deg in {c.secs:F0} s, speed {car.SpeedKmh:F0} -> {(ok ? "PASS" : "FAIL (spun)")}");
                    if (c.steer == 1f && c.startKmh == 45f) Capture("19_gas_slide");
                }
                CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f;
                // and the handbrake held a whole second (a player, not a tap)
                car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -300f), Quaternion.identity);
                rb.linearVelocity = car.transform.forward * (60f / 3.6f); rb.angularVelocity = Vector3.zero;
                for (int k = 0; k < 10; k++) yield return new WaitForFixedUpdate();
                CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 1f; CarInput.TouchHandbrake = true;
                float hbTurn = 0f, hbMax = 0f;
                for (int k = Mathf.RoundToInt(3f / Time.fixedDeltaTime); k > 0; k--)
                {
                    if (k == Mathf.RoundToInt(2f / Time.fixedDeltaTime)) CarInput.TouchHandbrake = false;   // held 1 s
                    yield return new WaitForFixedUpdate();
                    hbTurn += Vector3.Dot(car.Body.angularVelocity, Vector3.up) * Time.fixedDeltaTime * Mathf.Rad2Deg;
                    if (car.SpeedKmh > 10f) hbMax = Mathf.Max(hbMax, Mathf.Abs(car.DriftAngle));
                }
                CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f; CarInput.TouchHandbrake = false;
                Debug.Log($"[StressTest] spin 'handbrake held 1 s + gas + full steer at 60 km/h': max angle {hbMax:F0}, turned {hbTurn:F0} deg -> {(hbMax < 88f && Mathf.Abs(hbTurn) < 330f ? "PASS" : "FAIL (spun)")}");
                car.counterSteerAssist = keepAssist;
            }

            // Nitro test (the shop shots fitted nitro stage 2): a run up the avenue with and without the boost.
            if (car.nitroCapacity > 0f)
            {
                car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -300f), Quaternion.identity);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.5f);
                CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 0f; CarInput.TouchNitro = false;
                yield return new WaitForSeconds(2f);
                float plain = car.SpeedKmh;
                car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -300f), Quaternion.identity);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                car.nitroLeft = car.nitroCapacity;
                yield return new WaitForSeconds(0.5f);
                CarInput.TouchNitro = true;
                bool seen = false;
                for (float t = 0f; t < 2f; t += Time.deltaTime) { seen |= car.NitroActive; yield return null; }
                float boosted = car.SpeedKmh;
                Capture("18_nitro");
                CarInput.TouchNitro = false; CarInput.TouchThrottle = 0f;
                Debug.Log($"[StressTest] nitro: {(seen && boosted > plain + 3f ? "PASS" : "FAIL")} 2 s from rest {plain:F0} km/h plain vs {boosted:F0} km/h with nitro, left {car.nitroLeft:F1}/{car.nitroCapacity:F0} s");
            }

            // Tyre test: a fresh standing burnout heats the rears; then a nearly worn tyre is cooked until it blows.
            {
                var tw = car.GetComponent<TyreWear>();
                if (tw != null)
                {
                    tw.Refit();
                    car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -330f), Quaternion.identity);
                    rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.4f);
                    CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 1f;   // a donut: max wheelspin and slide
                    yield return new WaitForSeconds(4f);
                    Debug.Log($"[StressTest] tyres after 4 s donut: heat {tw.Heat(2):F2}/{tw.Heat(3):F2} health {tw.health[2]:F2}/{tw.health[3]:F2} spin {car.RearSpin:F1} grip {car.wheels[2].gripMul:F2}");
                    Capture("16_hot_tyres");
                    tw.health[2] = 0.04f; tw.health[3] = 0.04f; tw.heat[2] = 0.95f; tw.heat[3] = 0.95f;
                    yield return new WaitForSeconds(2.5f);
                    Debug.Log($"[StressTest] tyre blowout: {(tw.Blown(2) || tw.Blown(3) ? "PASS (blown)" : "FAIL (no blowout)")} grip {car.wheels[2].gripMul:F2}/{car.wheels[3].gripMul:F2}");
                    Capture("17_blowout_sparks");
                    CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f;
                    tw.Refit();
                    Debug.Log($"[StressTest] refit: grip {car.wheels[2].gripMul:F2} blown {tw.Blown(2)}");
                }
            }

            // Night, if the world supports it.
            if (PreviewHooks.SetNight != null)
            {
                Object.Destroy(cam.gameObject);
                rb.isKinematic = false;
                car.transform.SetPositionAndRotation(new Vector3(-3f, 0.6f, -390f), Quaternion.identity);
                rb.linearVelocity = car.transform.forward * 16f;
                PreviewHooks.SetNight(true);
                yield return new WaitForSeconds(0.5f);
                CarInput.TouchThrottle = 1f; CarInput.TouchSteer = 0.8f; CarInput.TouchHandbrake = true;
                yield return new WaitForSeconds(0.4f);
                CarInput.TouchHandbrake = false; CarInput.TouchSteer = 0.3f;
                yield return new WaitForSeconds(1.6f);
                Capture("30_night_drift");
                CarInput.TouchThrottle = 0f; CarInput.TouchSteer = 0f;
            }

            PreviewHooks.Active = false;
            Debug.Log("[Preview] done");
        }

        static void Capture(string name)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Preview] no main camera for " + name); return; }
            CaptureWith(cam, name, true);
        }

        static void CaptureWith(Camera cam, string name, bool withUi)
        {
            var canvases = withUi ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None) : new Canvas[0];
            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (canvases[i].isRootCanvas && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = cam;
                    canvases[i].planeDistance = cam.nearClipPlane + 0.05f;
                }
            }
            Canvas.ForceUpdateCanvases();

            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);

            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != null && canvases[i].isRootCanvas) canvases[i].renderMode = modes[i];
            Debug.Log("[Preview] saved " + name);
        }
    }
}
