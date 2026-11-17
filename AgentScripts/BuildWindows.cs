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

    /// <summary>
    /// Council playtest build: identical to <see cref="Run"/> except it defines COUNCIL_PLAYTEST_BUILD, which makes
    /// CouncilDirector drop straight into the brief's example election on boot (no approach, no F1 tool needed).
    /// Never for release; output is a separate exe so WorldMVP.exe is untouched.
    /// Run: unity command script run --file AgentScripts/BuildWindows.cs --entry BuildWindows.RunCouncilPlaytest --timeout 900
    /// </summary>
    public const string CouncilOutput = "Builds/Windows/CouncilPlaytest.exe";

    public static string RunCouncilPlaytest() => Run(CouncilOutput, "Council Playtest", new[] { "COUNCIL_PLAYTEST_BUILD" });

    public static string Run() => Run(Output, "WorldMVP", null);

    private static string Run(string output, string productName, string[] extraDefines)
    {
        string prevProductName = PlayerSettings.productName;
        PlayerSettings.productName = productName;
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
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = extraDefines,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var r = report.summary;
            // Delete the Burst debug folder (not shipped).
            string burst = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_BurstDebugInformation_DoNotShip");
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
            PlayerSettings.productName = prevProductName;
            AssetDatabase.SaveAssets();
        }
    }
}
