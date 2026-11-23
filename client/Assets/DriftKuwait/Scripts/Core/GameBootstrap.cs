// ARAB DRIFT - روح الشارع
// Stage 1 prototype bootstrap: builds the whole test level from code when you press Play
// in an empty scene whose name starts with "Salwa" (or any scene with no camera).
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftKuwait
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (Object.FindAnyObjectByType<DriftCarController>() != null || GameFlow.I != null) return;
            if (GameObject.Find("DK_NoAutoBoot") != null) return;

            string scene = SceneManager.GetActiveScene().name;
            bool isOurScene = scene.StartsWith("Salwa");
            bool emptyScene = Camera.main == null;
            if (!isOurScene && !emptyScene) return;

            Boot();
        }

        public static void Boot()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 100f;   // stable tyre forces

            // Remove template camera / light so ours own the scene.
            foreach (var old in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.Destroy(l.gameObject);

            var root = new GameObject("DK_World");
            Atmosphere.Build(root.transform);
            SalwaBlockBuilder.Build(root.transform);
            DesertBuilder.Build(root.transform);
            StreetProps.Build(root.transform);

            var camGo = new GameObject("DK_Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            var dc = camGo.AddComponent<DriftCamera>();
            Atmosphere.ConfigureCamera(cam);

            // Salwa street ambience under everything (ElevenLabs loop).
            var amb = Resources.Load<AudioClip>("DriftKuwait/Audio/amb_salwa_golden");
            if (amb != null)
            {
                var a = camGo.AddComponent<AudioSource>();
                a.clip = amb; a.loop = true; a.volume = 0.35f; a.spatialBlend = 0f;
                a.Play();
            }

            GameSettings.Load();
            var flow = GameFlow.Create(cam, dc);
            flow.ShowTitle();
            GameCenter.SignIn();
        }
    }
}
