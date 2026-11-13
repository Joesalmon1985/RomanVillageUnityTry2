using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWindows
{
    public static string Run()
    {
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/World.unity" },
            locationPathName = "Builds/Windows/WorldMVP.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var r = BuildPipeline.BuildPlayer(opts).summary;
        return $"{r.result} size={r.totalSize / (1024 * 1024)}MB errors={r.totalErrors} warnings={r.totalWarnings} time={r.totalTime}";
    }
}
