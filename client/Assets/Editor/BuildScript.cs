using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Command-line build used by CI (game-ci/unity-builder with buildMethod: BuildScript.BuildIOS).
/// Same pipeline as Sands of Empire. Arguments: -customBuildPath (set by unity-builder), -buildNumber, -appleTeamId, -appVersion.
/// The game builds its whole world in code (GameBootstrap), so the scene is an empty entry point named "Salwa".
/// Arab Drift also needs: the Universal Render Pipeline switched on, and the shaders the code looks up by name kept in the build.
/// </summary>
public static class BuildScript
{
    public const string BundleId = "world.embur.arabdrift";
    const string ScenePath = "Assets/Scenes/Salwa.unity";
    const string SettingsDir = "Assets/Settings";
    const string ShaderRefDir = "Assets/DriftKuwait/Resources/DK_ShaderRefs";

    // Shaders found with Shader.Find at runtime. A shader no material references is stripped from the build,
    // so each gets a small material in Resources (Resources are always included).
    static readonly string[] RuntimeShaders =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Complex Lit",
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Particles/Lit",
        "Universal Render Pipeline/Unlit",
        "Skybox/Procedural",
        "Skybox/Panoramic",
        "Sprites/Default",
    };

    [MenuItem("Arab Drift/Build iOS (Xcode project)")]
    public static void BuildIOS()
    {
        string outDir = Arg("-customBuildPath", "build/iOS");
        string buildNumber = Arg("-buildNumber", "1");
        string teamId = Arg("-appleTeamId", "").Trim();
        string version = Arg("-appVersion", "0.1.0");

        EnsureUrp();
        EnsureShaderRefs();
        EnsureCarMaterials();
        EnsureScene();
        ConfigurePlayer(version, buildNumber, teamId);

        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[Build] {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB, to {outDir}");
        if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// Everything BuildIOS sets up except the build itself (URP, shader keepers, car materials, scene).
    /// The cloud preview test calls this before play mode (IPrebuildSetup).
    public static void PrepareOnly()
    {
        EnsureUrp();
        EnsureShaderRefs();
        EnsureCarMaterials();
        EnsureScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Build] PrepareOnly done");
    }

    /// Test-runner variant: same project set-up, but never opens or saves a scene and never refreshes the asset
    /// database (either one replaces the test runner's own scene and the run hangs).
    public static void PrepareForTests()
    {
        EnsureUrp();
        EnsureShaderRefs();
        EnsureCarMaterials();
        AssetDatabase.SaveAssets();
        Debug.Log("[Build] PrepareForTests done");
    }

    /// <summary>Creates the URP asset and renderer once, and makes URP the active pipeline for every quality level.</summary>
    [MenuItem("Arab Drift/Set up URP")]
    public static void EnsureUrp()
    {
        string assetPath = SettingsDir + "/DK_URP.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null)
        {
            Directory.CreateDirectory(SettingsDir);
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            data.renderingMode = RenderingMode.ForwardPlus;   // many street lamps at night
            AssetDatabase.CreateAsset(data, SettingsDir + "/DK_URP_Renderer.asset");
            asset = UniversalRenderPipelineAsset.Create(data);
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 90f;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = true;    // soft particles (smoke meets the road softly)
            asset.supportsCameraOpaqueTexture = false;
            asset.shadowCascadeCount = 2;
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            asset.maxAdditionalLightsCount = 8;
            AssetDatabase.CreateAsset(asset, assetPath);
            var so = new SerializedObject(asset);
            var soft = so.FindProperty("m_SoftShadowsSupported");
            if (soft != null) { soft.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] Created " + assetPath);
        }
        EnsureSsao();
        GraphicsSettings.defaultRenderPipeline = asset;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();
    }

    /// Screen-space ambient occlusion: contact darkening under cars, kerbs, gates and walls. The feature class is
    /// internal to URP, so it is created by name.
    static void EnsureSsao()
    {
        try
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/DK_URP_Renderer.asset");
            if (rd == null) return;
            foreach (var f in rd.rendererFeatures) if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return;
            var t = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (t == null) { Debug.LogWarning("[Build] SSAO type not found"); return; }
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
            feature.name = "SSAO";
            AssetDatabase.AddObjectToAsset(feature, rd);
            rd.rendererFeatures.Add(feature);
            var so = new SerializedObject(rd);
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Settings: half-res, moderate radius, works without normals texture on mobile.
            var fso = new SerializedObject(feature);
            var settings = fso.FindProperty("m_Settings");
            if (settings != null)
            {
                void SetF(string n, float v) { var p = settings.FindPropertyRelative(n); if (p != null) p.floatValue = v; }
                void SetB(string n, bool v) { var p = settings.FindPropertyRelative(n); if (p != null) p.boolValue = v; }
                SetF("Intensity", 1.1f); SetF("Radius", 0.45f); SetF("DirectLightingStrength", 0.2f);
                SetB("Downsample", true);
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            Debug.Log("[Build] SSAO feature added");
        }
        catch (System.Exception e) { Debug.LogWarning("[Build] SSAO skipped: " + e.Message); }
    }

    static void EnsureShaderRefs()
    {
        Directory.CreateDirectory(ShaderRefDir);
        foreach (string name in RuntimeShaders)
        {
            var sh = Shader.Find(name);
            if (sh == null) { Debug.LogWarning("[Build] Shader not found: " + name); continue; }
            string path = ShaderRefDir + "/" + name.Replace('/', '_').Replace(' ', '_') + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                AssetDatabase.CreateAsset(new Material(sh), path);
        }
        AssetDatabase.SaveAssets();
    }

    /// Materials made at runtime with keywords (normal maps, emission, transparency) need their shader variants in the
    /// build: URP strips variants no material uses. These keeper materials sit in Resources so the variants ship.
    static void KeepVariant(string shader, string name, params string[] keywords)
    {
        var sh = Shader.Find(shader);
        if (sh == null) return;
        string path = ShaderRefDir + "/keep_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        foreach (var k in keywords) m.EnableKeyword(k);
        if (System.Array.IndexOf(keywords, "_SURFACE_TYPE_TRANSPARENT") >= 0)
        {
            m.SetFloat("_Surface", 1f);
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
    }

    /// One paint material per car from its generated maps (Complex Lit with clear coat: the CarX-style paint shine).
    [MenuItem("Arab Drift/Make car materials")]
    public static void EnsureCarMaterials()
    {
        Directory.CreateDirectory(ShaderRefDir);
        KeepVariant("Universal Render Pipeline/Lit", "lit_emission", "_EMISSION");
        KeepVariant("Universal Render Pipeline/Lit", "lit_normal", "_NORMALMAP");
        KeepVariant("Universal Render Pipeline/Lit", "lit_normal_metal", "_NORMALMAP", "_METALLICSPECGLOSSMAP");
        KeepVariant("Universal Render Pipeline/Lit", "lit_transparent", "_SURFACE_TYPE_TRANSPARENT");
        KeepVariant("Universal Render Pipeline/Lit", "lit_alphatest", "_ALPHATEST_ON");
        KeepVariant("Universal Render Pipeline/Particles/Unlit", "pu_transparent", "_SURFACE_TYPE_TRANSPARENT");
        KeepVariant("Universal Render Pipeline/Particles/Unlit", "pu_soft", "_SURFACE_TYPE_TRANSPARENT", "_SOFTPARTICLES_ON", "_FADING_ON");
        KeepVariant("Universal Render Pipeline/Particles/Lit", "pl_soft", "_SURFACE_TYPE_TRANSPARENT", "_SOFTPARTICLES_ON", "_FADING_ON");
        KeepVariant("Universal Render Pipeline/Particles/Lit", "pl_transparent", "_SURFACE_TYPE_TRANSPARENT");

        const string carsDir = "Assets/DriftKuwait/Resources/DriftKuwait/Cars";
        if (!Directory.Exists(carsDir)) return;
        var complex = Shader.Find("Universal Render Pipeline/Complex Lit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string dir in Directory.GetDirectories(carsDir))
        {
            string d = dir.Replace('\\', '/');
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(d + "/albedo.png");
            if (albedo == null) continue;
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(d + "/normal.png");
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(d + "/metallic.png");
            string path = d + "/paint.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(complex != null ? complex : lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = complex != null ? complex : lit;
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.35f); m.EnableKeyword("_NORMALMAP"); }
            if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); }
            else { m.SetFloat("_Metallic", 0.35f); m.SetFloat("_Smoothness", 0.8f); }
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            if (complex != null)
            {
                m.SetFloat("_ClearCoat", 1f);
                m.SetFloat("_ClearCoatMask", 0.85f);
                m.SetFloat("_ClearCoatSmoothness", 0.93f);
                m.EnableKeyword("_CLEARCOAT");
            }
            EditorUtility.SetDirty(m);
            Debug.Log("[Build] Car material " + path);
        }
        AssetDatabase.SaveAssets();
    }

    static void EnsureScene()
    {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    static void ConfigurePlayer(string version, string buildNumber, string teamId)
    {
        PlayerSettings.companyName = "Embur Inc.";
        PlayerSettings.productName = "Q8 Drift";
        PlayerSettings.bundleVersion = version;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.iOS.buildNumber = buildNumber;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.iOS.hideHomeButton = false;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        if (teamId.Length > 0) PlayerSettings.iOS.appleDeveloperTeamID = teamId;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.statusBarHidden = true;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SplashScreen.backgroundColor = new Color(0.10f, 0.07f, 0.05f);

        AssetDatabase.ImportAsset("Assets/AppIcon/icon.png", ImportAssetOptions.ForceUpdate);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AppIcon/icon.png");
        if (icon != null)
        {
            int n = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application).Length;
            PlayerSettings.SetIcons(NamedBuildTarget.iOS, Enumerable.Repeat(icon, n).ToArray(), IconKind.Application);
            int m = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Store).Length;
            if (m > 0) PlayerSettings.SetIcons(NamedBuildTarget.iOS, Enumerable.Repeat(icon, m).ToArray(), IconKind.Store);
        }
        else
        {
            Debug.LogWarning("[Build] Assets/AppIcon/icon.png missing");
        }
    }

    static string Arg(string name, string fallback)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == name && !a[i + 1].StartsWith("-")) return a[i + 1];
        return fallback;
    }
}
