using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Table;

/// <summary>G1-5 play-mode driver (unity command script run --file AgentScripts/GodBoardDrive.cs --entry GodBoardDrive.X).</summary>
public static class GodBoardDrive
{
    public static string Begin() { return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }

    public static string Info()
    {
        var d = GodBoardDirector.Instance;
        if (d == null) return "no director; boot=" + (WorldBootstrap.Instance != null) + " current=" + DevStart.Current;
        var t = d.T;
        var tp = d.OwnTemple;
        return "view=" + d.View + " phase=" + t.Phase + " current=" + t.Current + " paused=" + t.Paused + " temple=" + (tp != null ? tp.transform.position.ToString() : "none") + " corners=" + d.CornerSpots.Count + " edges=" + d.EdgeSpots.Count + " pieces=" + d.Pieces.SettlementCount + "/" + d.Pieces.RoadCount;
    }

    public static string Day()
    {
        if (DayNight.Instance != null) DayNight.Instance.SetHour(11);
        return "day";
    }

    /// <summary>Stands the player beside the altar looking at it.</summary>
    public static string ToAltar()
    {
        var d = GodBoardDirector.Instance;
        var at = d.OwnTemple.transform.position;
        var w = PlayerWalker.Instance;
        var corner = BoardSpace.Vertex(d.S.Board.Topology, d.S.Board.StartCorners[0]);
        var dir = (corner - at); dir.y = 0; dir.Normalize();
        var feet = at + dir * 12f;
        var look = at - feet; look.y = 0;
        w.Teleport(feet, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 8f);
        return "ok " + feet;
    }

    public static string Sacrifice() { return GodBoardDirector.Instance.TrySacrifice() ? "sacrificed" : "refused"; }
    public static string Leave() { GodBoardDirector.Instance.Leave(); return "leaving"; }
    public static string TapAdvice()
    {
        var d = GodBoardDirector.Instance;
        d.TapAt(d.AdviseCorner >= 0 ? d.ScreenOfCorner(d.AdviseCorner) : d.ScreenOfEdge(d.AdviseEdge));
        return "tapped corner=" + d.AdviseCorner + " edge=" + d.AdviseEdge + " msg=" + d.Message;
    }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot"; }
    public static string Fast() { GodBoardDirector.RiseSeconds = 0.05f; return "fast"; }
}
public static class GodBoardProbe
{
    public static string Scan()
    {
        var b = WorldBootstrap.Instance.Sim.State.Board;
        var sb = new System.Text.StringBuilder();
        for (int p = 0; p < 4; p++)
        {
            var c = BoardSpace.Vertex(b.Topology, b.StartCorners[p]);
            sb.Append("p" + p + " corner=" + c + ": ");
            foreach (float fr in new[] { 2f, 3f, 4f, 5f, 6.2f })
            {
                int n = 0; float bestR = -1;
                for (float r = 8; r <= 80; r += 2)
                    for (int k = 0; k < 36; k++)
                    {
                        var q = c + BoardSpace.DirOf(k * 10f) * r;
                        if (b.Topology.HexAt(q.x, q.z) < 0) continue;
                        if (CampLayout.Obstruction(b, q, fr) == null) { n++; if (bestR < 0) bestR = r; }
                    }
                sb.Append("fr" + fr + "=" + n + "@" + bestR + " ");
            }
            sb.Append(" | ");
        }
        return sb.ToString();
    }
}
