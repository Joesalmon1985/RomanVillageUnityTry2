using System.Text;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;

/// <summary>
/// Integration helpers for driving a Play Mode session from the CLI (captures, AutoPlay runs, status).
/// Run: unity command script run --file AgentScripts/Integ.cs --entry Integ.Status
/// </summary>
public static class Integ
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static string Run(string name)
    {
        if (B == null) return "not in play mode";
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown script";
        _ok = null;
        B.AutoPlay.Finished += (sc, pass) => _ok = pass;
        B.AutoPlay.Run(s);
        return "started " + name;
    }
    static bool? _ok;

    public static string WalkTour() => Run("walk_tour");
    public static string DuelDance() => Run("duel_dance");
    public static string CastAll1() { CastAll.Speed = 1f; return Run("cast_all"); }
    public static string CastAll4() { CastAll.Speed = 4f; return Run("cast_all"); }
    public static string CastAll8() { CastAll.Speed = 8f; return Run("cast_all"); }
    public static string Speed1() { Time.timeScale = 1f; return "x1"; }
    public static string Speed0() { Time.timeScale = 0f; return "x0"; }
    public static string Speed8() { Time.timeScale = 8f; return "x8"; }
    public static string HudToggle() { Hud.Visible = !Hud.Visible; return "hud " + Hud.Visible; }
    public static string MapToggle() { MapScreen.Toggle(); return "map " + MapScreen.IsOpen; }

    public static string Status()
    {
        if (B == null) return "not in play mode";
        var s = B.Sim.State;
        var sb = new StringBuilder();
        var ap = B.AutoPlay;
        sb.Append("tick ").Append(s.Tick).Append(" autoplay running=").Append(ap.IsRunning).Append(" failed=").Append(ap.Failed).Append(" msg=").Append(ap.FailMessage).Append(" ok=").Append(_ok);
        sb.Append(" | strength ").Append(FactionApi.Strength(s, 0).Total).Append(" v ").Append(FactionApi.Strength(s, 1).Total);
        sb.Append(" | casts ").Append(CastAll.Casts).Append(" screen ").Append(CastAll.ScreenCasts).Append(" refused ").Append(CastAll.Refused);
        sb.Append(" | over ").Append(s.Factions.GameOver).Append(' ').Append(s.Factions.Outcome).Append(' ').Append(s.Factions.OutcomeReason);
        int camps = 0, b = 0, roads = 0, moving = 0, ship = 0;
        foreach (var st in s.Economy.Settlements) if (st.State == SettlementState.Active) camps++;
        foreach (var bd in s.Economy.Buildings) if (bd.State == BuildingState.Active) b++;
        foreach (var r in s.Economy.Roads) if (r.State == RoadState.Built) roads++;
        foreach (var c in s.Economy.Carriers) { if (c.State != CarrierState.Idle) moving++; if (c.LegCorners != null && c.LegCorners.Length > 0) ship++; }
        sb.Append(" | camps ").Append(camps).Append(" buildings ").Append(b).Append(" roads ").Append(roads).Append(" carriers ").Append(s.Economy.Carriers.Count).Append(" moving ").Append(moving).Append(" onroad ").Append(ship);
        var top = FactionApi.Top(s, 0);
        sb.Append(" | top ").Append(top != null ? FactionApi.ProposalTitle(s, top.Id) : "-");
        sb.Append(" | pos ").Append(B.Avatar.Position).Append(" h").Append(B.Avatar.Heading).Append(" fps ").Append((1f / Time.smoothDeltaTime).ToString("0")).Append(" at ").Append(Screen.width).Append('x').Append(Screen.height).Append(" x").Append(Time.timeScale);
        return sb.ToString();
    }

    /// <summary>Stand ~10 m from a carrier of ours that is on a main-road leg (or any moving loaded carrier with local=true), facing it.</summary>
    static string LookAtCarrier(bool road)
    {
        if (B == null) return "not in play mode";
        var s = B.Sim.State;
        foreach (var c in s.Economy.Carriers)
        {
            if (c.Faction != 0 || c.State == CarrierState.Idle) continue;
            bool onRoad = c.LegCorners != null && c.LegCorners.Length > 0;
            if (onRoad != road || (!road && !c.HasLoad)) continue;
            if (!EconomyApi.CarrierPosition(s, c.Id, 0, out double x, out double z, out double yaw)) continue;
            var p = new Vector3((float)x, 0, (float)z);
            var ahead = p + BoardSpace.DirOf((float)yaw) * 12f + new Vector3(3f, 0, 2f);
            var cell = WalkGrid.NearestFree(WalkLattice.ToCell(ahead), 4);
            if (!cell.HasValue) continue;
            var from = WalkLattice.ToWorld(cell.Value);
            B.Avatar.StopAuto();
            B.Avatar.Teleport(cell.Value, WalkLattice.HeadingOf(BoardSpace.YawOf(p - from)));
            return "carrier " + c.Id + " state " + c.State + " load " + c.LoadCount + " " + c.Load + " at " + p + " " + EconomyApi.DescribeCarrier(s, c.Id);
        }
        return "none";
    }
    public static string LookAtPorter() => LookAtCarrier(true);
    public static string LookAtLocalCarrier() => LookAtCarrier(false);

    public static string GoHome()
    {
        var camp = new CampLayout(B.Sim.State.Board);
        B.Avatar.StopAuto();
        B.Avatar.Teleport(camp.Spawn, camp.SpawnHeading);
        return "home";
    }
    public static string MeasureFps() { B.AutoPlay.Run(new AutoPlayScript("fps").MeasureFps("late game first person", 5f)); return "measuring"; }
    public static string TurnRight() { B.Avatar.TurnRight(); return "h" + B.Avatar.Heading; }
    public static string TurnLeft() { B.Avatar.TurnLeft(); return "h" + B.Avatar.Heading; }
    public static string Forward() { B.Avatar.Forward(); return B.Avatar.Cell.ToString(); }
}
