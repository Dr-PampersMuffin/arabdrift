#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Xcode project fixes after Unity exports (same approach as Sands of Empire): Game Center capability,
/// a privacy manifest, and export compliance in Info.plist. The game collects no data: progress stays on the iPhone,
/// scores go to Apple's Game Center, purchases go through Apple (Unity IAP / StoreKit). No tracking, no ads.
/// </summary>
public static class IOSPostBuild
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);
        string main = proj.GetUnityMainTargetGuid();
        string framework = proj.GetUnityFrameworkTargetGuid();
        proj.AddFrameworkToProject(framework, "GameKit.framework", false);

        // Privacy manifest: no tracking, no data collected; Unity itself reads UserDefaults (app settings).
        File.WriteAllText(Path.Combine(path, "PrivacyInfo.xcprivacy"), PrivacyManifest);
        string pm = proj.AddFile("PrivacyInfo.xcprivacy", "PrivacyInfo.xcprivacy");
        proj.AddFileToBuild(main, pm);
        proj.WriteToFile(projPath);

        var caps = new ProjectCapabilityManager(projPath, "ArabDrift.entitlements", null, main);
        caps.AddGameCenter();
        caps.WriteToFile();

        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }

    const string PrivacyManifest =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
        "<plist version=\"1.0\"><dict>" +
        "<key>NSPrivacyTracking</key><false/>" +
        "<key>NSPrivacyTrackingDomains</key><array/>" +
        "<key>NSPrivacyCollectedDataTypes</key><array/>" +
        "<key>NSPrivacyAccessedAPITypes</key><array>" +
        "<dict><key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryUserDefaults</string>" +
        "<key>NSPrivacyAccessedAPITypeReasons</key><array><string>CA92.1</string></array></dict>" +
        "</array></dict></plist>\n";
}
#endif
