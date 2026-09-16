using UnityEngine;
using UnityEditor;

// One-click: point the app icon and the engine splash at the Ultra Panda
// branding art. Icon/splash live in Player Settings, not in a scene, so this is
// a Player-Settings writer rather than part of Build Everything.
public static class BrandingSetup
{
    const string IconPath   = "Assets/Art/Branding/AppIcon.png";
    const string SplashPath = "Assets/Art/Branding/SplashLoading.png";

    [MenuItem("Ultra Panda/Dev/Apply Icon & Splash")]
    public static void Apply()
    {
        // Product name via the API — editing ProjectSettings.asset on disk while
        // the Editor is open doesn't stick, so set it here.
        PlayerSettings.productName = "Ultra Panda";

        // Landscape only (the game is built landscape).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogError("Missing app icon at " + IconPath); return; }

        // Default icon. Every platform without a platform-specific override
        // (Android + iOS here) falls back to this, so one call covers both.
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });

        var splash = LoadAsSprite(SplashPath);
        if (splash != null)
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;   // takes effect on Plus/Pro
            PlayerSettings.SplashScreen.background = splash;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.05f, 0.08f, 0.03f, 1f);
        }
        else Debug.LogError("Missing splash art at " + SplashPath);

        AssetDatabase.SaveAssets();
        Debug.Log("Ultra Panda icon + splash applied. On Unity Personal the Unity logo still flashes before our splash (license limit); a custom Boot scene removes it fully.");
    }

    static Sprite LoadAsSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp &&
            imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
