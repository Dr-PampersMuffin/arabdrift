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

            // Accelerate down the straight, then a handbrake flick into a held drift.
            CarInput.TouchThrottle = 1f;
            yield return new WaitForSeconds(3.5f);
            Capture("11_speed");
            CarInput.TouchSteer = 1f; CarInput.TouchHandbrake = true;
            yield return new WaitForSeconds(0.35f);
            CarInput.TouchHandbrake = false;
            CarInput.TouchSteer = -0.25f;            // catch the slide with counter-steer
            yield return new WaitForSeconds(0.9f);
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
