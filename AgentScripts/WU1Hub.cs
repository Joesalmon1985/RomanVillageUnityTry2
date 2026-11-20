using System.Text;
using UnityEngine;
using World.Game;

/// <summary>WU-1a drivers: geometry probe and hub captures (unity command script run --file AgentScripts/WU1Hub.cs --entry WU1Hub.X).</summary>
public static class WU1Hub
{
    private static WorldBootstrap B => WorldBootstrap.Instance;
    private static Vector3 _from; private static float _yaw;

    public static string Begin() { GodBoardDirector.RiseSeconds = 0.05f; return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }

    private static string Rel(Vector3 p)
    {
        var d = p - _from; d.y = 0;
        float ang = Mathf.DeltaAngle(_yaw, BoardSpace.YawOf(d));
        return p.ToString("0") + " dist=" + d.magnitude.ToString("0") + " ang=" + ang.ToString("0");
    }

    /// <summary>Where everything stands relative to the spawn cell and its heading (what the first view sees).</summary>
    public static string Probe()
    {
        var layout = new CampLayout(B.Sim.State.Board);
        _from = WalkLattice.ToWorld(layout.Spawn); _yaw = WalkLattice.Yaw(layout.SpawnHeading);
        var sb = new StringBuilder();
        sb.AppendLine("spawn " + _from.ToString("0") + " yaw=" + _yaw + " hub=" + layout.Hub);
        sb.AppendLine("hearth " + Rel(layout.Hearth));
        sb.AppendLine("circle " + Rel(layout.DanceCircleCentre));
        sb.AppendLine("mira " + Rel(layout.MiraSpot));
        foreach (var p in layout.Places) sb.AppendLine(p.Zone + " " + Rel(p.Position));
        var c = CouncilDirector.Instance;
        if (c != null) sb.AppendLine("tower " + Rel(c.Site.Position) + " herbs " + Rel(c.Site.Loc[4]));
        foreach (var n in Npc.All) sb.AppendLine("npc " + n.Id + " " + Rel(n.transform.position));
        return sb.ToString();
    }

    public static string Quiet() { foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h); return "quiet"; }
    public static string Night() { if (DayNight.Instance != null) DayNight.Instance.SetHour(22); return "night"; }
    public static string Pause() { B.Clock.Pause("wu1"); return "paused"; }

    /// <summary>Teleports to the spawn cell, heading offset by turns (each 60 degrees; negative = left).</summary>
    public static string Spawn() => Turn(0);
    public static string Left() => Turn(-1);
    public static string Right() => Turn(1);
    private static string Turn(int k)
    {
        var layout = new CampLayout(B.Sim.State.Board);
        if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
        PlayerWalker.Instance.StopAuto();
        PlayerWalker.Instance.Teleport(layout.Spawn, (layout.SpawnHeading + k + 6) % 6);
        return "at spawn turn " + k;
    }
}
public static class WU1Dbg
{
    public static string W() { var w = PlayerWalker.Instance; return w.Position + " yaw=" + w.Yaw + " heading=" + w.Heading + " cell=" + w.Cell + " locked=" + w.IsLocked + " glide=" + w.IsGliding + " auto=" + w.IsAutoWalking + " time=" + Time.timeScale + " encounter=" + (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) + " view=" + ViewSwitcher.Instance.CurrentView; }
}
public static class WU1Look
{
    private static string To(Vector3 target, Vector3 from, float pitch = 6f)
    {
        if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
        var w = PlayerWalker.Instance; w.StopAuto();
        from.y = 0; target.y = 0;
        w.Teleport(from, BoardSpace.YawOf(target - from), pitch);
        foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
        return "looking at " + target;
    }
    private static string Zone(string z, float dist, float side = 0f)
    {
        var l = new CampLayout(WorldBootstrap.Instance.Sim.State.Board);
        l.TryPlace(z, out var p);
        var f = new HubFrame(p.Position, p.Yaw);
        return To(p.Position, f.W(side, dist));
    }
    public static string Temple() => Zone("HUB-TEMPLE", 17f, 3f);
    public static string Lodge() => Zone("HUB-HEALER", 13f, 2f);
    public static string Ring() => Zone("HUB-DUEL", 14f, 2f);
    public static string Camp()
    {
        var l = new CampLayout(WorldBootstrap.Instance.Sim.State.Board);
        var f = new HubFrame(l.MiraSpot, l.MiraYaw);
        return To(l.MiraSpot, f.W(1f, 10f));
    }
    public static string Tower()
    {
        var c = CouncilDirector.Instance.StewardSpot;
        var l = new CampLayout(WorldBootstrap.Instance.Sim.State.Board);
        return To(c, c + (l.Hearth - c).normalized * 12f);
    }
}
