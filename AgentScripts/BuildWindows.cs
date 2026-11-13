using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows player build of the World scene to Builds/Windows/WorldMVP.exe (release build: no development watermark).
/// Fullscreen window at the desktop's native resolution (Alt+Enter or the -screen-fullscreen 0 flag gives a window).
/// The inherited Sims 2 probe's Resources folder (a list of Sims animation names, used only by the probe scene) is moved
/// out of the build for its duration and moved back afterwards, so the player contains nothing Sims-derived.
/// Run: unity command script run --file AgentScripts/BuildWindows.cs --entry BuildWindows.Run --timeout 900
/// </summary>
public static class BuildWindows
{
    public const string Output = "Builds/Windows/WorldMVP.exe";
    private const string ProbeResources = "Assets/RomanVillage/Probe/Resources";
    private const string ProbeParked = "Assets/RomanVillage/Probe/ProbeListNotInBuild";

    public static string Run()
    {
        PlayerSettings.productName = "WorldMVP";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
        EditorUserBuildSettings.development = false;

        bool parked = false;
        if (AssetDatabase.IsValidFolder(ProbeResources))
        {
            string err = AssetDatabase.MoveAsset(ProbeResources, ProbeParked);
            if (!string.IsNullOrEmpty(err)) return "Failed: could not move the probe resources out of the build: " + err;
            parked = true;
        }
        try
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/World.unity" },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var r = report.summary;
            // Delete the Burst debug folder (not shipped).
            string burst = Path.Combine(Path.GetDirectoryName(Output), "WorldMVP_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(burst)) Directory.Delete(burst, true);
            return $"{r.result} size={r.totalSize / (1024 * 1024)}MB errors={r.totalErrors} warnings={r.totalWarnings} time={r.totalTime} ended={r.buildEndedAt:u}";
        }
        finally
        {
            if (parked)
            {
                string err = AssetDatabase.MoveAsset(ProbeParked, ProbeResources);
                if (!string.IsNullOrEmpty(err)) Debug.LogError("[BuildWindows] could not move the probe resources back: " + err);
            }
        }
    }
}
