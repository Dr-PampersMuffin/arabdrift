using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// Screen flow: title -> garage -> drive (with pause) -> back. One scene; the Salwa world is built once at boot and
    /// the drive camera sits in it, the menus draw over it. Also registers the cloud-preview shots.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }

        public Camera worldCam;
        public DriftCamera driftCam;
        public GameObject car;
        public string carId;

        GameObject screen;          // current menu screen root
        DriveHUD hud;
        Pursuit pursuit;
        TrafficSystem traffic;
        FreeRoam freeRoam;
        AIDrifters drifters;
        CrowdSystem crowd;
        EventRunner runner;
        Image fade;

        public static GameFlow Create(Camera cam, DriftCamera dc)
        {
            var go = new GameObject("DK_GameFlow");
            var f = go.AddComponent<GameFlow>();
            f.worldCam = cam; f.driftCam = dc;
            I = f;
            var fc = UIKit.MakeCanvas("DK_Fade", 100);
            f.fade = UIKit.Img(fc.transform, "Fade", null, new Color(0, 0, 0, 0));
            UIKit.Stretch(f.fade.rectTransform);
            PreviewHooks.Register("01_title", () => f.PreviewScreen(() => f.ShowTitle()));
            PreviewHooks.Register("02_garage", () => f.PreviewScreen(() => f.ShowGarage()));
            foreach (var id in new[] { "ghazal", "faris", "wanait", "abusitta" })
            {
                string carId = id;
                PreviewHooks.Register("03_garage_" + carId, () => f.PreviewScreen(() => { CarCatalog.SelectedId = carId; f.ShowGarage(); }),
                                      () => CarCatalog.SelectedId = "shaheen");
            }
            PreviewHooks.Register("04_map", () => f.PreviewScreen(() => f.ShowMap()));
            PreviewHooks.Register("06_howto", () => f.PreviewScreen(() => { f.ShowTitle(); HowToPlay.Show(null); }),
                                  () => { var h = Object.FindAnyObjectByType<HowToPlay>(); if (h != null) Destroy(h.gameObject); });
            PreviewHooks.Register("07_story", () => f.PreviewScreen(() => StoryScene.Play(new[] { Story.Ch1Intro[3] }, null)),
                                  () => { var g = GameObject.Find("DK_Story"); if (g != null) Destroy(g); });
            PreviewHooks.Register("08_results", () => f.PreviewScreen(() => ResultsScreen.Show(f, Career.Events[1], 420, 0, 0, 0, false,
                                      "كنت أبطأ من ٢٥ كم/س أغلب الوقت .. التقحيص ما ينحسب تحت هالسرعة")),
                                  () => { foreach (var r in Object.FindObjectsByType<ResultsScreen>(FindObjectsSortMode.None)) Destroy(r.gameObject); });
            // M2 workshop: parts, paint + rims (shaheen in red on gold mesh), the dyno with a tuned engine, faris repainted
            System.Action resetTune = () =>
            {
                foreach (var k in new[] { "dk_paint_shaheen", "dk_rim_shaheen", "dk_paint_faris", "dk_t_shaheen_0", "dk_t_shaheen_1", "dk_t_shaheen_3", "dk_rimown_shaheen_1" }) PlayerPrefs.DeleteKey(k);
                CarCatalog.SelectedId = "shaheen";
            };
            PreviewHooks.Register("09a_parts", () => f.PreviewScreen(() => { PlayerPrefs.SetInt("dk_t_shaheen_0", 1); f.ShowGarage(); GarageScreen.Current?.Open(WorkshopPanel.Mode.Parts); }), resetTune);
            PreviewHooks.Register("09b_paint", () => f.PreviewScreen(() => { PlayerPrefs.SetInt("dk_paint_shaheen", 2); PlayerPrefs.SetInt("dk_rimown_shaheen_1", 1); PlayerPrefs.SetInt("dk_rim_shaheen", 1);
                                      PlayerPrefs.SetInt("dk_t_shaheen_3", 3); f.ShowGarage(); GarageScreen.Current?.Open(WorkshopPanel.Mode.Look); }), resetTune);
            PreviewHooks.Register("09c_dyno", () => f.PreviewScreen(() => { PlayerPrefs.SetInt("dk_t_shaheen_0", 2); PlayerPrefs.SetInt("dk_t_shaheen_1", 1); f.ShowGarage(); GarageScreen.Current?.Open(WorkshopPanel.Mode.Dyno); }), resetTune);
            PreviewHooks.Register("09d_faris_navy", () => f.PreviewScreen(() => { PlayerPrefs.SetInt("dk_paint_faris", 4); CarCatalog.SelectedId = "faris"; f.ShowGarage(); }), resetTune);
            PreviewHooks.Register("07b_story_ch2", () => f.PreviewScreen(() => StoryScene.Play(new[] { Story.Ch2Intro[0] }, null)),
                                  () => { var g = GameObject.Find("DK_Story"); if (g != null) Destroy(g); });
            PreviewHooks.Register("07c_history", () => f.PreviewScreen(() => StoryScene.Play(new[] { Story.History[2] }, null)),
                                  () => { var g = GameObject.Find("DK_Story"); if (g != null) Destroy(g); });
            PreviewHooks.Register("04b_map_ch2", () => f.PreviewScreen(() => { Story.Chapter = 2; f.ShowMap(); }), () => Story.Chapter = 1);
            PreviewHooks.StartDrive = () => f.StartDrive("shaheen", false);
            return f;
        }

        IEnumerator PreviewScreen(System.Action open)
        {
            open();
            yield return null;
        }

        void ClearScreen()
        {
            if (screen != null) Destroy(screen);
            screen = null;
        }

        public void ShowTitle()
        {
            EndDrive();
            ClearScreen();
            screen = TitleScreen.Build(this).gameObject;
            Sfx.Music("mus_menu");
        }

        public void ShowGarage()
        {
            EndDrive();
            ClearScreen();
            screen = GarageScreen.Build(this).gameObject;
            Sfx.Music("mus_garage");
        }

        public void ShowMap()
        {
            EndDrive();
            ClearScreen();
            screen = MapScreen.Build(this).gameObject;
            Sfx.Music("mus_menu");
        }

        /// PLAY: first time shows how to play, then the map.
        public void Play()
        {
            System.Action next = () => { if (!HowToPlay.Seen) HowToPlay.Show(ShowMap); else ShowMap(); };
            if (!Story.IntroSeen && !PreviewHooks.Active)
            {
                Story.BeginChapter1();
                StoryScene.Play(Story.Ch1Intro, next);
            }
            else next();
        }

        /// Pays the garage debt from the wallet; the first instalment closes Chapter 1 with its story beat.
        public void PayDebt()
        {
            bool before = Story.ToInstalment > 0 || Story.Chapter == 1;
            long paid = Story.PayFromWallet();
            // Already paid in full (e.g. paid under the old, bigger debt): move the story on instead of saying "no cash"
            bool alreadyClear = (Story.Chapter == 1 && Story.ToInstalment == 0) || (Story.Chapter == 2 && Story.Remaining == 0);
            if (paid <= 0 && !alreadyClear) { Toast("ما عندك كاش كافي .. اطلع قحّص واجمع"); return; }
            Sfx.Play("ui_reward", 0.9f);
            if (before && Story.ToInstalment == 0 && Story.Chapter == 1)
            {
                Story.Chapter = 2;
                var beats = new System.Collections.Generic.List<StoryScene.Beat>(Story.Ch1Done);
                beats.AddRange(Story.Ch2Intro);
                StoryScene.Play(beats.ToArray(), () => { ShowTitle(); Toast("الفصل الثاني: الكراج .. شغل الزباين انفتح بالخريطة"); });
            }
            else if (Story.Chapter == 2 && Story.Remaining <= 0)
            {
                Story.Chapter = 3;
                StoryScene.Play(Story.Ch2Done, () => { ShowTitle(); Toast("الفصل الثالث: الذيب .. قريباً"); });
            }
            else ShowTitle();
        }

        public void StartDrive(string id, bool withFade = true)
        {
            StartCoroutine(DriveRoutine(id, withFade, Career.Get("salwa_free")));
        }

        public void StartEvent(Career.EventDef ev)
        {
            StartCoroutine(DriveRoutine(ev.carId ?? CarCatalog.SelectedId, true, ev));   // repair jobs hand you the customer's car
        }

        IEnumerator DriveRoutine(string id, bool withFade, Career.EventDef ev)
        {
            if (withFade) yield return Fade(1f, 0.35f);
            ClearScreen();
            EndDrive();
            carId = id;
            if (PreviewHooks.Active) Debug.Log("[Drive] building car " + id);
            Vector3 spawn = ev != null ? ev.spawn : new Vector3(0f, 0.6f, -140f);
            car = CarFactory.Build(id, spawn, Quaternion.Euler(0f, ev != null ? ev.yaw : 0f, 0f), ev == null || ev.carId == null);
            car.GetComponent<DriftCarController>().counterSteerAssist = GameSettings.Assist;
            if (car.GetComponent<DriftFX>() == null) car.AddComponent<DriftFX>();   // flames, sparks, tyre scream
            if (car.GetComponent<StuckGuard>() == null) car.AddComponent<StuckGuard>();   // never stay stuck
            if (car.GetComponent<TyreWear>() == null) car.AddComponent<TyreWear>();   // hot, shredded and blown tyres
            driftCam.target = car.GetComponent<Rigidbody>();
            driftCam.enabled = true;
            worldCam.enabled = true;
            var scorer = car.AddComponent<DriftScorer>();
            scorer.cam = driftCam;
            if (PreviewHooks.Active) Debug.Log("[Drive] car ready, building HUD");
            hud = DriveHUD.Build(this, car.GetComponent<DriftCarController>(), scorer, driftCam);
            if (PreviewHooks.Active) Debug.Log("[Drive] HUD ready");
            pursuit = new GameObject("DK_Pursuit").AddComponent<Pursuit>();
            pursuit.player = car.GetComponent<DriftCarController>();
            pursuit.hud = hud;
            traffic = new GameObject("DK_Traffic").AddComponent<TrafficSystem>();
            traffic.player = pursuit.player; traffic.pursuit = pursuit;
            traffic.count = (ev != null && ev.kind != Career.Kind.FreeRoam) ? 6 : 14;
            traffic.patrol = ev == null || ev.kind == Career.Kind.FreeRoam;
            crowd = new GameObject("DK_Crowd").AddComponent<CrowdSystem>();   // spectators at the roundabouts and along the avenue
            crowd.player = car.GetComponent<DriftCarController>();
            Sfx.Music(Atmosphere.IsNight ? "mus_drive_night" : "mus_drive_day");
            Sfx.Play("shaheen_start", 0.8f);
            if (ev != null && ev.kind != Career.Kind.FreeRoam)
            {
                runner = new GameObject("DK_Event").AddComponent<EventRunner>();
                runner.ev = ev; runner.car = car.GetComponent<DriftCarController>(); runner.scorer = scorer;
                runner.hud = hud; runner.pursuit = pursuit; runner.flow = this;
            }
            if (withFade) yield return Fade(0f, 0.5f);
            if (runner == null)
            {
                freeRoam = new GameObject("DK_FreeRoam").AddComponent<FreeRoam>();
                freeRoam.car = car.GetComponent<DriftCarController>(); freeRoam.scorer = scorer; freeRoam.hud = hud;
                drifters = new GameObject("DK_AIDrifters").AddComponent<AIDrifters>();   // locals drifting the roundabouts: it can be done
                drifters.player = freeRoam.car; drifters.hud = hud;
            }
            if (!PreviewHooks.Active && runner == null && !HowToPlay.Seen) hud.PlayIntro();
        }

        void EndDrive()
        {
            if (runner != null) Destroy(runner.gameObject);
            runner = null;
            foreach (var r in Object.FindObjectsByType<ResultsScreen>(FindObjectsSortMode.None)) Destroy(r.gameObject);
            if (hud != null) Destroy(hud.gameObject);
            if (pursuit != null) { pursuit.Cleanup(); Destroy(pursuit.gameObject); }
            if (traffic != null) Destroy(traffic.gameObject);
            if (freeRoam != null) Destroy(freeRoam.gameObject);
            if (drifters != null) Destroy(drifters.gameObject);
            if (crowd != null) Destroy(crowd.gameObject);
            if (car != null) Destroy(car);
            hud = null; pursuit = null; car = null;
            Time.timeScale = 1f;
        }

        public IEnumerator Fade(float to, float dur)
        {
            float from = fade.color.a, t = 0f;
            fade.raycastTarget = true;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, to);
            fade.raycastTarget = to > 0.5f;
        }

        /// Short message in the middle of the screen ("قريباً" etc.).
        public void Toast(string ar)
        {
            var c = UIKit.MakeCanvas("DK_Toast", 90);
            var p = UIKit.Panel(c.transform, "Toast", UIKit.Ink);
            UIKit.Place(p.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1240f, 110f));
            UIKit.Outline(p.transform, new Color(1f, 0.7f, 0.2f, 0.6f));
            var t = UIKit.Label(p.transform, "T", ar, 42, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(t.rectTransform, 30f, 30f, 8f, 8f);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 28; t.resizeTextMaxSize = 42;   // always inside the box
            Destroy(c.gameObject, 2.4f);
        }
    }
}
