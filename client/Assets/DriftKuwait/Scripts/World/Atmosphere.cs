using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DriftKuwait
{
    /// Golden-hour Kuwait: low warm sun, dusty sky, haze, filmic grade.
    /// Presets are switchable at runtime (F1..F4 in editor) for the look tests.
    public static class Atmosphere
    {
        public enum Preset { GoldenHour, NoonHaze, Ghubar, Night }
        static Light sun;
        static Material sky;
        static Volume volume;

        public static void Build(Transform parent)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                sky = new Material(skyShader) { name = "DK_Sky" };
                RenderSettings.skybox = sky;
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
            profile.Add<ColorAdjustments>(true);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);

            Apply(Preset.GoldenHour);
        }

        public static void Apply(Preset p)
        {
            var ca = volume != null && volume.sharedProfile.TryGet(out ColorAdjustments c) ? c : null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            switch (p)
            {
                case Preset.GoldenHour:
                    sun.transform.rotation = Quaternion.Euler(13f, -38f, 0f);
                    sun.color = new Color(1f, 0.74f, 0.48f);
                    sun.intensity = 2.3f;
                    RenderSettings.ambientSkyColor = new Color(0.62f, 0.55f, 0.48f);
                    RenderSettings.ambientEquatorColor = new Color(0.66f, 0.50f, 0.36f);
                    RenderSettings.ambientGroundColor = new Color(0.30f, 0.24f, 0.18f);
                    RenderSettings.fogColor = new Color(0.86f, 0.70f, 0.52f);
                    RenderSettings.fogDensity = 0.0042f;
                    SetSky(new Color(0.78f, 0.62f, 0.45f), 1.9f, 1.15f, 0.06f);
                    Grade(ca, 0.15f, 14f, -6f, new Color(1f, 0.93f, 0.84f));
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
                    SetSky(new Color(0.70f, 0.66f, 0.60f), 1.6f, 1.25f, 0.04f);
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
                    sun.color = new Color(0.45f, 0.55f, 0.75f);
                    sun.intensity = 0.06f;
                    RenderSettings.ambientSkyColor = new Color(0.06f, 0.07f, 0.11f);
                    RenderSettings.ambientEquatorColor = new Color(0.10f, 0.08f, 0.07f);
                    RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.03f);
                    RenderSettings.fogColor = new Color(0.10f, 0.08f, 0.07f);
                    RenderSettings.fogDensity = 0.006f;
                    SetSky(new Color(0.20f, 0.18f, 0.22f), 1.0f, 0.15f, 0.0f);
                    Grade(ca, 0.3f, 18f, -8f, new Color(1f, 0.90f, 0.80f));
                    break;
            }
            foreach (var l in Object.FindObjectsByType<StreetLamp>(FindObjectsSortMode.None))
                l.SetOn(p == Preset.Night);
            DynamicGI.UpdateEnvironment();
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
