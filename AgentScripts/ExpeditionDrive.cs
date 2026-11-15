using System;
using System.IO;
using System.Text;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Campaign;

/// <summary>
/// Drives the expedition (crystals package I6) in a Play Mode session from the CLI: starts an AutoPlay script,
/// reports where it stands, takes captures. Options come from Temp/i6_cmd.txt ("timescale 4; captures 1; seed 7").
/// Run: unity command script run --file AgentScripts/ExpeditionDrive.cs --entry ExpeditionDrive.Status
/// </summary>
public static class ExpeditionDrive
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static bool? _ok;
    static string _name;

    static void Options()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "i6_cmd.txt");
        ExpeditionScripts.TimeScale = 1f; ExpeditionScripts.Captures = false; ExpeditionScripts.Seed = null;
        if (!File.Exists(path)) return;
        foreach (string raw in File.ReadAllText(path).Split(';'))
        {
            var a = raw.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length < 2) continue;
            if (a[0] == "timescale") ExpeditionScripts.TimeScale = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
            else if (a[0] == "captures") ExpeditionScripts.Captures = a[1] == "1";
            else if (a[0] == "seed") ExpeditionScripts.Seed = ulong.Parse(a[1]);
        }
    }

    static string Run(string name)
    {
        if (B == null) return "not in play mode";
        Options();
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown script " + name;
        _ok = null; _name = name;
        Application.runInBackground = true;
        B.AutoPlay.Finished += (sc, pass) => _ok = pass;
        B.AutoPlay.Run(s);
        return "started " + name + " (time x" + ExpeditionScripts.TimeScale + ", captures " + ExpeditionScripts.Captures + ")";
    }

    /// <summary>Opens the World scene in the editor (before entering Play Mode).</summary>
    public static string Open()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/World.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        return "opened World";
    }

    public static string EarthFirst() => Run("earth_first");
    public static string EarthStolen() => Run("earth_stolen");
    public static string EarthStolenWin() => Run("earth_stolen_win");
    public static string MiraAlone() => Run("mira_alone");
    public static string CaptureRun() => Run("expedition_captures");
    public static string AshRunners() => Run("ash_runners");

    public static string Status()
    {
        if (B == null) return "not in play mode";
        var sb = new StringBuilder();
        var ap = B.AutoPlay;
        sb.Append(_name ?? "-").Append(": ").Append(ap.IsRunning ? "running" : ap.Failed ? "FAILED " + ap.FailMessage : "finished").Append(" | ");
        sb.Append(ExpeditionScripts.Status());
        var d = ExpeditionDirector.Instance;
        if (d != null)
        {
            sb.Append(" | scene ").Append(d.SceneStep).Append(" script ").Append(d.ScriptPending).Append(" duelStandIn ").Append(d.DuelStandInOpen).Append(" last cmd ").Append(d.LastCommand);
            sb.Append(" | mira ").Append(d.Mira.transform.position.ToString("0")).Append(" player ").Append(B.Avatar.Position.ToString("0")).Append(" following ").Append(B.Avatar.Following != null);
            sb.Append(" | said: ");
            for (int i = Mathf.Max(0, d.Said.Count - 6); i < d.Said.Count; i++) sb.Append('[').Append(d.Said[i].Speaker).Append("] ").Append(d.Said[i].Text).Append(" / ");
            sb.Append(" | cues: ");
            for (int i = Mathf.Max(0, d.Cues.Count - 3); i < d.Cues.Count; i++) sb.Append(d.Cues[i]).Append(" / ");
        }
        var pd = PrinceDirector.Instance;
        if (pd != null) sb.Append(" | prince ").Append(pd.CurrentPhase).Append(" ask ").Append(pd.AskHandle).Append(" hushed ").Append(pd.Hushed).Append(" pitches ").Append(pd.PitchCount);
        if (ExpeditionScripts.LastReport != null) sb.Append(" || ").Append(ExpeditionScripts.LastReport);
        return sb.ToString().Replace('"', '\'');
    }

    /// <summary>Starts the campaign on the running world (dev.historic must be running) without a script.</summary>
    public static string StartCampaign()
    {
        if (B == null) return "not in play mode";
        var r = ExpeditionDev.StartCampaign();
        return r.Ok ? "StartCampaign sent" : "refused: " + r.Message;
    }

    public static string DevHistoric()
    {
        if (B == null) return "not in play mode";
        return DevStart.Begin("dev.historic") ? "dev.historic building" : "failed: " + DevStart.LastError;
    }

    public static string Shot()
    {
        if (B == null) return "not in play mode";
        string dir = AutoPlay.ScreenshotDir;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "i6_shot_" + DateTime.Now.ToString("HHmmss") + ".png");
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }

    public static string Night()
    {
        if (DayNight.Instance == null) return "no lighting";
        DayNight.Instance.SetHour(23.5);
        return "hour " + DayNight.Instance.Hour.ToString("0.0");
    }
}
