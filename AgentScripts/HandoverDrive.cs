using System.Text;
using UnityEngine;
using World.Game;
using World.Sim;

/// <summary>
/// Runs AutoPlay `handover` (H1-4) in the editor's Play Mode and reports where it stands.
/// unity command script run --file AgentScripts/HandoverDrive.cs --entry HandoverDrive.Run   (and .Status)
/// </summary>
public static class HandoverDrive
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static string _name;

    public static string Open()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/World.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        return "opened World";
    }

    public static string Run() => Start("handover");
    public static string FirstScene() => Start("first_scene");
    public static string Follow() => Start("follow_prince");
    public static string Ignore() => Start("ignore_prince");

    static string Start(string name)
    {
        if (B == null) return "not in play mode";
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown script " + name;
        _name = name;
        Application.runInBackground = true;
        B.AutoPlay.Run(s);
        return "started " + name;
    }

    public static string Status()
    {
        if (B == null) return "not in play mode";
        var ap = B.AutoPlay;
        var sb = new StringBuilder();
        sb.Append(_name ?? "-").Append(": ").Append(ap.IsRunning ? "running" : ap.Failed ? "FAILED " + ap.FailMessage : "finished");
        sb.Append(" | game s ").Append((B.Sim.State.Tick / 10f).ToString("0"));
        var d = PrinceDirector.Instance;
        if (d != null)
        {
            sb.Append(" | after ").Append(d.AfterHandover).Append(" stage ").Append(d.HandoverStage).Append(" ann ").Append(d.Announcements.Count)
              .Append(" acts ").Append(d.Acts.Count).Append(" inv ").Append(d.Invitations).Append(" menu ").Append(d.MenuOpen).Append(" invite ").Append(d.InviteOpen);
            sb.Append(" | said: ");
            for (int i = System.Math.Max(0, d.Said.Count - 4); i < d.Said.Count; i++) sb.Append('[').Append(d.Said[i].Speaker).Append("] ").Append(d.Said[i].Text).Append(" / ");
        }
        return sb.ToString().Replace('"', '\'');
    }
}
