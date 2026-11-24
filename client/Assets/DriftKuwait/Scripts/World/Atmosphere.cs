using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DriftKuwait
{
    /// Golden-hour Kuwait: low warm sun, dusty sky, haze, filmic grade.
    /// Presets are switchable at runtime (F1..F4 in editor) for the look tests.
    public static class Atmosphere
    {
        public enum Preset { ClearDay, GoldenHour, NoonHaze, Ghubar, Night }
        /// The look the game opens with: a clear Kuwaiti afternoon, blue sky and white clouds.
        public const Preset Day = Preset.ClearDay;
        static Light sun;
        static Material sky, photoSky, clearSky;
        static SplitToning split;

        /// Film-style split tone: cool teal into the shadows, warm amber into the highlights (grey = off).
        static void Tone(Color shadows, Color highlights)
        {
            if (split == null) return;
            split.shadows.Override(shadows);
            split.highlights.Override(highlights);
            split.balance.Override(0f);
        }
        static Volume volume;
        static ReflectionProbe probe;
        static Bloom bloomFx;
        public static bool IsNight { get; private set; }
        public static Preset Current { get; private set; }

        public static void Build(Transform parent)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.cullingMask = ~(1 << 30);     // the garage showroom has its own studio lights
            RenderSettings.sun = sun;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                sky = new Material(skyShader) { name = "DK_Sky" };
                RenderSettings.skybox = sky;
            }
            // Photographed golden-hour sky (tools/art/make_world_v3.py), used for the day presets.
            var skyTex = Resources.Load<Texture2D>("DriftKuwait/Tex/sky_golden");
            var pano = Shader.Find("Skybox/Panoramic");
            if (skyTex != null && pano != null)
            {
                photoSky = new Material(pano) { name = "DK_PhotoSky" };
                photoSky.SetTexture("_MainTex", skyTex);
                photoSky.SetFloat("_Mapping", 1f);      // latitude-longitude
                photoSky.SetFloat("_ImageType", 0f);    // 360
                photoSky.SetFloat("_Exposure", 1.05f);
                photoSky.SetFloat("_Rotation", 0f);
            }
            var clearTex = Resources.Load<Texture2D>("DriftKuwait/Tex/sky_clear");
            if (clearTex != null && pano != null)
            {
                clearSky = new Material(pano) { name = "DK_ClearSky" };
                clearSky.SetTexture("_MainTex", clearTex);
                clearSky.SetFloat("_Mapping", 1f);
                clearSky.SetFloat("_ImageType", 0f);
                clearSky.SetFloat("_Exposure", 1.0f);
                clearSky.SetFloat("_Rotation", 40f);
            }

            var volGo = new GameObject("DK_PostFX");
            volGo.transform.SetParent(parent, false);
            volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloomFx = bloom;
            profile.Add<ColorAdjustments>(true);
            split = profile.Add<SplitToning>(true);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);

            // Realtime reflections of the street and sky for the car paint (no baked lighting in a runtime-built world).
            QualitySettings.realtimeReflectionProbes = true;
            var pg = new GameObject("DK_Reflections");
            pg.transform.SetParent(parent, false);
            pg.transform.position = new Vector3(0f, 3f, 0f);
            probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(2000f, 400f, 2000f);
            probe.resolution = 128;
            probe.hdr = true;
            probe.boxProjection = false;
            probe.importance = 0;
            probe.cullingMask = 1;          // Default layer: the street, houses and sky (not cars, UI or showroom)
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.farClipPlane = 900f;

            Apply(Day);
            PreviewHooks.SetNight = night => Apply(night ? Preset.Night : Day);
        }

        public static void Apply(Preset p)
        {
            Current = p;
            IsNight = p == Preset.Night;
            CarLights.NightMode = IsNight;
            var ca = volume != null && volume.sharedProfile.TryGet(out ColorAdjustments c) ? c : null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            switch (p)
            {
                case Preset.ClearDay:
                    // A clear spring afternoon: high white sun with crisp shadows, cool blue sky fill, light clean haze,
                    // neutral grade with a touch of contrast and saturation (no colour filter over the scene).
                    sun.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
                    sun.color = new Color(1f, 0.97f, 0.92f);
                    sun.intensity = 2.0f;
                    RenderSettings.ambientSkyColor = new Color(0.56f, 0.66f, 0.86f);
                    RenderSettings.ambientEquatorColor = new Color(0.70f, 0.70f, 0.68f);
                    RenderSettings.ambientGroundColor = new Color(0.40f, 0.36f, 0.31f);
                    RenderSettings.fogColor = new Color(0.80f, 0.87f, 0.96f);
                    RenderSettings.fogDensity = 0.0009f;
                    SetSky(new Color(0.55f, 0.6f, 0.68f), 1.0f, 1.3f, 0.04f);
                    Grade(ca, 0.05f, 12f, 8f, new Color(1f, 1f, 1f));
                    break;
                case Preset.GoldenHour:
                    // Real late-afternoon light: a warm low sun, but cool blue-grey sky fill in the shadows, and a
                    // pale neutral dust haze (not an orange filter over everything).
                    sun.transform.rotation = Quaternion.Euler(9f, -38f, 0f);
                    sun.color = new Color(1f, 0.66f, 0.42f);
                    sun.intensity = 2.3f;
                    RenderSettings.ambientSkyColor = new Color(0.50f, 0.48f, 0.64f);
                    RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.48f);
                    RenderSettings.ambientGroundColor = new Color(0.32f, 0.27f, 0.22f);
                    RenderSettings.fogColor = new Color(0.86f, 0.62f, 0.52f);
                    RenderSettings.fogDensity = 0.0016f;   // lighter haze: Kuwait Towers read on the horizon
                    SetSky(new Color(0.62f, 0.62f, 0.62f), 1.3f, 1.2f, 0.05f);
                    Grade(ca, 0f, 22f, 14f, new Color(1f, 0.96f, 0.94f));
                    Tone(new Color(0.42f, 0.52f, 0.56f), new Color(0.60f, 0.53f, 0.46f));
                    break;
                case Preset.NoonHaze:
                    sun.transform.rotation = Quaternion.Euler(68f, -20f, 0f);
                    sun.color = new Color(1f, 0.96f, 0.88f);
                    sun.intensity = 2.6f;
                    RenderSettings.ambientSkyColor = new Color(0.78f, 0.76f, 0.72f);
                    RenderSettings.ambientEquatorColor = new Color(0.80f, 0.72f, 0.60f);
                    RenderSettings.ambientGroundColor = new Color(0.45f, 0.38f, 0.30f);
                    RenderSettings.fogColor = new Color(0.90f, 0.86f, 0.78f);
                    RenderSettings.fogDensity = 0.0035f;
                    SetSky(new Color(0.60f, 0.62f, 0.64f), 1.2f, 1.25f, 0.04f);
                    Grade(ca, 0.1f, 8f, -12f, new Color(1f, 0.98f, 0.94f));
                    break;
                case Preset.Ghubar:
                    sun.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                    sun.color = new Color(1f, 0.80f, 0.50f);
                    sun.intensity = 0.9f;
                    RenderSettings.ambientSkyColor = new Color(0.80f, 0.62f, 0.38f);
                    RenderSettings.ambientEquatorColor = new Color(0.78f, 0.60f, 0.36f);
                    RenderSettings.ambientGroundColor = new Color(0.50f, 0.38f, 0.24f);
                    RenderSettings.fogColor = new Color(0.82f, 0.64f, 0.38f);
                    RenderSettings.fogDensity = 0.016f; // 150-300 m visibility
                    SetSky(new Color(0.85f, 0.62f, 0.30f), 4.5f, 0.9f, 0.02f);
                    Grade(ca, 0.05f, 6f, -20f, new Color(1f, 0.88f, 0.68f));
                    break;
                case Preset.Night:
                    sun.transform.rotation = Quaternion.Euler(-12f, -40f, 0f);
                    sun.color = new Color(0.55f, 0.66f, 0.9f);    // moonlight
                    sun.intensity = 0.45f;
                    sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
                    RenderSettings.ambientSkyColor = new Color(0.19f, 0.22f, 0.33f);
                    RenderSettings.ambientEquatorColor = new Color(0.22f, 0.17f, 0.14f);
                    RenderSettings.ambientGroundColor = new Color(0.07f, 0.05f, 0.04f);
                    RenderSettings.fogColor = new Color(0.16f, 0.12f, 0.10f);
                    RenderSettings.fogDensity = 0.0055f;
                    SetSky(new Color(0.22f, 0.24f, 0.34f), 1.2f, 0.32f, 0.0f);
                    Grade(ca, 0.3f, 18f, -8f, new Color(1f, 0.90f, 0.80f));
                    break;
            }
            if (p != Preset.GoldenHour) Tone(new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f));
            RenderSettings.skybox = p == Preset.ClearDay && clearSky != null ? clearSky : (photoSky != null && (p == Preset.GoldenHour || p == Preset.NoonHaze)) ? photoSky : sky;
            foreach (var l in Object.FindObjectsByType<StreetLamp>(FindObjectsSortMode.None))
                l.SetOn(p == Preset.Night);
            SalwaBlockBuilder.SetNight(p == Preset.Night);
            if (bloomFx != null)
            {
                bloomFx.threshold.Override(IsNight ? 0.8f : 1.0f);
                bloomFx.intensity.Override(IsNight ? 0.9f : 0.55f);
            }
            DynamicGI.UpdateEnvironment();
            if (probe != null) probe.RenderProbe();
        }

        static void SetSky(Color tint, float atmosphere, float exposure, float sunSize)
        {
            if (sky == null) return;
            sky.SetColor("_SkyTint", tint);
            sky.SetColor("_GroundColor", new Color(0.55f, 0.45f, 0.35f));
            sky.SetFloat("_AtmosphereThickness", atmosphere);
            sky.SetFloat("_Exposure", exposure);
            sky.SetFloat("_SunSize", sunSize);
        }

        static void Grade(ColorAdjustments ca, float exposure, float contrast, float saturation, Color filter)
        {
            if (ca == null) return;
            ca.postExposure.Override(exposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(filter);
        }

        public static void ConfigureCamera(Camera cam)
        {
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 62f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.Medium;
            }
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.shadowDistance = 160f;
        }
    }

}
