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
            bool before = Story.ToInstalment > 0;
            long paid = Story.PayFromWallet();
            if (paid <= 0) { Toast("ما عندك كاش كافي .. اطلع قحّص واجمع"); return; }
            Sfx.Play("ui_reward", 0.9f);
            if (before && Story.ToInstalment == 0 && Story.Chapter == 1)
            {
                Story.Chapter = 2;
                StoryScene.Play(Story.Ch1Done, () => { ShowTitle(); Toast("الفصل الثاني: الكراج .. قريباً"); });
            }
            else ShowTitle();
        }

        public void StartDrive(string id, bool withFade = true)
        {
            StartCoroutine(DriveRoutine(id, withFade, Career.Get("salwa_free")));
        }

        public void StartEvent(Career.EventDef ev)
        {
            StartCoroutine(DriveRoutine(CarCatalog.SelectedId, true, ev));
        }

        IEnumerator DriveRoutine(string id, bool withFade, Career.EventDef ev)
        {
            if (withFade) yield return Fade(1f, 0.35f);
            ClearScreen();
            EndDrive();
            carId = id;
            if (PreviewHooks.Active) Debug.Log("[Drive] building car " + id);
            Vector3 spawn = ev != null ? ev.spawn : new Vector3(0f, 0.6f, -140f);
            car = CarFactory.Build(id, spawn, Quaternion.Euler(0f, ev != null ? ev.yaw : 0f, 0f));
            car.GetComponent<DriftCarController>().counterSteerAssist = GameSettings.Assist;
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
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 24; t.resizeTextMaxSize = 42;   // always inside the box
            Destroy(c.gameObject, 2.4f);
        }
    }
}
