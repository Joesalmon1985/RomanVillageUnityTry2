using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Campaign;

/// <summary>P2a capture helpers (play mode): stand near the standing tuning bell, tap it. Captures stay local (Evidence/ is never committed).</summary>
public static class P2aDrive
{
    public static string NearBell()
    {
        StandingBell bell = null;
        foreach (var b in Object.FindObjectsByType<StandingBell>(FindObjectsSortMode.None)) bell = b;
        if (bell == null) return "no bell";
        var w = PlayerWalker.Instance; w.StopAuto();
        var circle = bell.Circle.Layout.DanceCircleCentre;
        var dir = bell.transform.position - circle; dir.y = 0; dir.Normalize();
        var feet = bell.transform.position + dir * 5.5f + Vector3.Cross(dir, Vector3.up) * 1.5f;
        feet.y = PlayerWalker.GroundHeight(feet);
        w.Teleport(feet, BoardSpace.YawOf(bell.transform.position - feet), 10f);
        WorldActions.Cancel();
        return "near bell " + bell.Available;
    }

    public static string TapBell()
    {
        StandingBell bell = null;
        foreach (var b in Object.FindObjectsByType<StandingBell>(FindObjectsSortMode.None)) bell = b;
        if (bell == null) return "no bell";
        WorldActions.Machine.MinConfirmSeconds = 0f; WorldActions.Machine.GuardSeconds = 0f;
        return "tap " + PointerInput.TapObject(bell) + " " + WorldActions.State;
    }

    public static string Enter()
    {
        MarkerRef.TryParse("glade/quarry_upper/exit", out var r);
        return "enter " + Interiors.Enter(r);
    }

    public static string TapWay()
    {
        InteriorWay way = null;
        foreach (var w in Object.FindObjectsByType<InteriorWay>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (w.Thing != null && w.Thing.Link != null && w.Thing.Link.IsOpen && !w.Thing.Link.Up) way = w;
        if (way == null) return "no way";
        WorldActions.Machine.MinConfirmSeconds = 0f; WorldActions.Machine.GuardSeconds = 0f; WorldActions.Cancel();
        var pw = PlayerWalker.Instance; pw.StopAuto();
        var c = way.Thing.Link.Cell + WalkLattice.Dir(way.Thing.Link.OutHeading) + WalkLattice.Dir(way.Thing.Link.OutHeading);
        pw.Teleport(c, way.Thing.Link.OutHeading + 3);
        return "way " + way.Title + " " + PointerInput.TapObject(way);
    }

    public static string Book() { ExpeditionDirector.Instance.Journal.Open(); return "book"; }

    public static string TapJetty()
    {
        var d = GodBoardDirector.Instance;
        for (int i = 0; i < d.S.Board.Ports.Count; i++)
        {
            var sp = d.ScreenOfPort(i);
            if (sp.x < 10 || sp.y < 10 || sp.x > Screen.width - 10 || sp.y > Screen.height * 0.8f) continue;
            if (d.PortAt(sp) != i) continue;
            return "jetty " + i + " " + d.TapAt(sp);
        }
        return "no visible jetty";
    }
}
