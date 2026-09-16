using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// One-click Android builds. A debug APK is installable straight onto a device
/// or emulator for testing (auto debug-signed, no keystore needed); the release
/// AAB is what you upload to the Play Store and needs a keystore configured in
/// Player Settings > Publishing Settings.
public static class AndroidBuild
{
    const string OutDir = "Builds/Android";

    [MenuItem("Ultra Panda/Build/Android APK (debug)")]
    public static void BuildDebugApk()
    {
        Build(development: true, aab: false);
    }

    [MenuItem("Ultra Panda/Build/Android AAB (release)")]
    public static void BuildReleaseAab()
    {
        Build(development: false, aab: true);
    }

    // Also runnable headless:
    //   Unity -batchmode -quit -projectPath . -executeMethod AndroidBuild.Cli
    public static void Cli()
    {
        bool aab = Environment.GetCommandLineArgs().Contains("--aab");
        Build(development: !aab, aab: aab);
    }

    static void Build(bool development, bool aab)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("Android Build Support is not installed. In Unity Hub > Installs > " +
                           "6000.6.0f1 > Add Modules, add 'Android Build Support' with its " +
                           "OpenJDK and Android SDK & NDK Tools child modules, then retry.");
            return;
        }

        // Make sure every scene the game needs is registered (Boot > Lobby > machines).
        SlotGameBuilder.RegisterScenes();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes in the build. Run Ultra Panda > Build Everything first.");
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("Switching active build target to Android…");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("Could not switch to the Android build target.");
                return;
            }
        }

        EditorUserBuildSettings.buildAppBundle = aab;

        Directory.CreateDirectory(OutDir);
        string ext = aab ? "aab" : "apk";
        string tag = development ? "debug" : "release";
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
        string outPath = Path.Combine(OutDir, $"UltraPanda-{tag}-{PlayerSettings.bundleVersion}-{stamp}.{ext}");

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development
                ? (BuildOptions.Development | BuildOptions.AllowDebugging)
                : BuildOptions.None
        };

        Debug.Log($"Building Android {ext.ToUpper()} ({tag}) -> {outPath}");
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"BUILD OK: {summary.outputPath}  ({summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalTime.TotalSeconds:F0}s)");
        else
            Debug.LogError($"BUILD {summary.result}: {summary.totalErrors} error(s). See the log above.");
    }
}
