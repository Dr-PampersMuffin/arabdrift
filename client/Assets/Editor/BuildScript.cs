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
        "Universal Render Pipeline/Particles/Unlit",
        "Skybox/Procedural",
        "Sprites/Default",
    };

    [MenuItem("Arab Drift/Build iOS (Xcode project)")]
    public static void BuildIOS()
    {
        string outDir = Arg("-customBuildPath", "build/iOS");
        string buildNumber = Arg("-buildNumber", "1");
        string teamId = Arg("-appleTeamId", "");
        string version = Arg("-appVersion", "0.1.0");

        EnsureUrp();
        EnsureShaderRefs();
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
            AssetDatabase.CreateAsset(data, SettingsDir + "/DK_URP_Renderer.asset");
            asset = UniversalRenderPipelineAsset.Create(data);
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 90f;
            asset.renderScale = 1f;
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] Created " + assetPath);
        }
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
        PlayerSettings.productName = "Arab Drift";
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
