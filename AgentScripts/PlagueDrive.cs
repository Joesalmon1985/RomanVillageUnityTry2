using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Plague;

/// <summary>G2-2 play-mode driver (unity command script run --file AgentScripts/PlagueDrive.cs --entry PlagueDrive.X).</summary>
public static class PlagueDrive
{
    public static string Begin() { GodBoardDirector.RiseSeconds = 0.05f; return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }
    public static string Day() { if (DayNight.Instance != null) DayNight.Instance.SetHour(11); return "day"; }
    public static string Infect()
    {
        var s = WorldBootstrap.Instance.Sim.State; var ev = new EventBuffer();
        for (int i = 0; i < 9; i++) PlagueRules.InfectRound(s, ev);
        return "demons=" + PlagueApi.TotalDemons(s);
    }
    public static string Overlay(string on) { PlagueViews.Instance.SetOverlay(on == "1"); return "overlay " + on; }
    public static string Sacrifice() { return GodBoardDirector.Instance.TrySacrifice(true) ? "sacrificed" : "refused"; }
    public static string Pause() { WorldBootstrap.Instance.Clock.Pause("cap"); return "paused"; }
    public static string ToDemon()
    {
        var v = PlagueViews.Instance; var s = WorldBootstrap.Instance.Sim.State;
        int best = 0, bc = 0;
        for (int n = 1; n <= 48; n++) { int d = PlagueApi.DemonsAt(s, n); if (d > bc) { bc = d; best = n; } }
        var at = v.NodePosition(best);
        var feet = at + new Vector3(0, 0, -16f);
        PlayerWalker.Instance.Teleport(feet, 0f, 8f);
        return "node " + best + " demons " + bc + " at " + at;
    }
    /// <summary>Capture helper only: writes demons straight into the state of the demon node (a mix of kinds).</summary>
    public static string Mix()
    {
        var s = WorldBootstrap.Instance.Sim.State; var p = s.Plague;
                System.Array.Clear(p.KindCount, 0, p.KindCount.Length); System.Array.Clear(p.Demons, 0, p.Demons.Length);
        void Put(int node, int kind, int c) { p.KindCount[(node - 1) * 4 + kind] += c; p.Demons[node - 1] += c; }
        Put(4, 1, 2); Put(4, 3, 1); Put(3, 0, 3); Put(2, 2, 1); Put(25, 2, 2); Put(13, 1, 1); Put(40, 3, 3); Put(45, 0, 1); Put(20, 1, 2); Put(30, 2, 1);
        p.Outbreaks = 3; p.RateIndex = 3;
        return "mixed";
    }
    public static string Storm() { PlagueViews.Instance.BeginStorm(); return "storm"; }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot " + name; }
    public static string ShotNode() => Shot("plague_node_demons");
    public static string ShotWorldOverlay() => Shot("plague_overlay_world");
    public static string ShotBoard() => Shot("plague_overlay_board");
    public static string ShotStorm() => Shot("plague_epidemic_sky");
    public static string OverlayOn() => Overlay("1");
    public static string OverlayOff() => Overlay("0");
    public static string Info() { var v = PlagueViews.Instance; return v == null ? "none" : "overlay=" + v.Overlay + " elevated=" + v.Elevated + " storm=" + v.StormLevel + " texts=" + string.Join("|", v.Hud.Texts); }
}
