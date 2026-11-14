using System.Text;
using UnityEngine;
using World.Game;
using World.Sim.Campaign;

/// <summary>
/// Package I5 (interiors) helpers for driving a Play Mode session from the CLI.
/// unity command script run --file AgentScripts/I5Run.cs --entry I5Run.QuarryWalk   (then I5Run.Status)
/// </summary>
public static class I5Run
{
    public static string OpenWorld()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/World.unity");
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }

    public static string ClearConsole()
    {
        var t = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        t?.GetMethod("Clear")?.Invoke(null, null);
        return "cleared";
    }

    public static string QuarryWalk()
    {
        var b = WorldBootstrap.Instance;
        if (b == null) return "not in play mode";
        if (!AutoPlayScripts.TryCreate(InteriorScripts.QuarryWalk, out var s)) return "unknown script";
        b.AutoPlay.LogFailuresAsErrors = true;
        b.AutoPlay.Run(s);
        return "started quarry_walk";
    }

    public static string Status()
    {
        var b = WorldBootstrap.Instance;
        if (b == null) return "not in play mode";
        var w = b.Avatar;
        var sb = new StringBuilder();
        sb.Append("autoplay running ").Append(b.AutoPlay.IsRunning).Append(" failed ").Append(b.AutoPlay.Failed).Append(' ').Append(b.AutoPlay.FailMessage);
        sb.Append(" | start ").Append(DevStart.Current).Append(" tick ").Append(b.Sim.State.Tick);
        sb.Append(" | walker ").Append(w.Cell).Append(" h").Append(w.Heading).Append(" auto ").Append(w.IsAutoWalking).Append(" locked ").Append(w.IsLocked);
        sb.Append(" | interior ").Append(Interiors.Current ?? "-").Append(" built ").Append(Interiors.All.Count).Append(" links ").Append(Interiors.Links.Count);
        var s = b.Sim.State;
        if (s.Campaign != null)
        {
            var p = CampaignApi.Party(s);
            sb.Append(" | party ").Append(p.Mode).Append(" at ").Append(p.At).Append(" facts ").Append(string.Join(",", s.Campaign.Facts));
        }
        if (WorldLights.Instance != null) sb.Append(" | pixel lights ").Append(WorldLights.Instance.PixelLightsNow);
        sb.Append(" | fps ").Append((1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("0"));
        return sb.ToString();
    }
}
