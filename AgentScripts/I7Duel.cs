using UnityEngine;
using World.Game;
using World.Sim.Campaign;

/// <summary>
/// Drives the campaign duel's AutoPlay scripts in a Play Mode session (crystals I7).
/// Run: unity command script run --file AgentScripts/I7Duel.cs --entry I7Duel.KeeperWin   (then I7Duel.Status)
/// </summary>
public static class I7Duel
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static bool? _ok;
    static string _name;

    static string Run(string name)
    {
        if (B == null) return "not in play mode";
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown script " + name;
        _ok = null; _name = name;
        CampaignDuel.ResetSession();
        B.AutoPlay.Finished += OnFinished;
        B.AutoPlay.Run(s);
        return "started " + name;
    }

    static void OnFinished(AutoPlayScript s, bool pass) { _ok = pass; Debug.Log("[I7Duel] finished " + s.Name + " pass=" + pass); }

    public static string KeeperWin() => Run(CampaignDuelScripts.KeeperWin);
    public static string KeeperLoss() => Run(CampaignDuelScripts.KeeperLoss);
    public static string Friendly() => Run(CampaignDuelScripts.FriendlyName);
    public static string DuelDance() => Run("duel_dance");

    public static string Status()
    {
        if (B == null) return "not in play mode";
        var ap = B.AutoPlay;
        var e = WardDuelEncounter.Current;
        var s = B.Sim.State;
        var p = s.Campaign?.Party;
        return "script " + _name + " running=" + (ap != null && ap.IsRunning) + " failed=" + (ap != null && ap.Failed) + " msg=" + (ap != null ? ap.FailMessage : "") + " ok=" + _ok
            + " | tick " + s.Tick + " start " + DevStart.Current + " campaign " + (s.Campaign != null ? s.Campaign.Phase.ToString() : "-")
            + " party " + (p != null ? p.Mode + " at " + p.At : "-")
            + " | duel " + (e != null ? e.Mode + " " + e.Phase + " casts " + e.Session.PlayerHistory.Count + "/" + e.Session.RivalHistory.Count + " outcome " + e.LastOutcome + " res " + e.Resolution : "-")
            + " | resolves " + CampaignDuel.ResolvesSent + " restarts " + CampaignDuel.RestartsSent + " screen " + Screen.width + "x" + Screen.height;
    }
}
