using System.Text;
using UnityEngine;
using World.Game;

/// <summary>WU-1a drivers: geometry probe and hub captures (unity command script run --file AgentScripts/WU1Hub.cs --entry WU1Hub.X).</summary>
public static class WU1Hub
{
    private static WorldBootstrap B => WorldBootstrap.Instance;

    public static string Begin() { GodBoardDirector.RiseSeconds = 0.05f; return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }

    private static string Rel(Vector3 p)
    {
        var w = PlayerWalker.Instance;
        var d = p - w.Position; d.y = 0;
        float ang = Mathf.DeltaAngle(w.Yaw, BoardSpace.YawOf(d));
        return p.ToString("0") + " dist=" + d.magnitude.ToString("0") + " ang=" + ang.ToString("0");
    }

    public static string Probe()
    {
        var w = PlayerWalker.Instance;
        var layout = new CampLayout(B.Sim.State.Board);
        var sb = new StringBuilder();
        sb.AppendLine("player " + w.Position.ToString("0") + " yaw=" + w.Yaw.ToString("0"));
        sb.AppendLine("hearth " + Rel(layout.Hearth));
        sb.AppendLine("circle " + Rel(layout.DanceCircleCentre));
        sb.AppendLine("mira " + Rel(layout.MiraSpot));
        sb.AppendLine("sol " + Rel(layout.DrummerSpot));
        var d = GodBoardDirector.Instance;
        if (d != null && d.OwnTemple != null) sb.AppendLine("temple " + Rel(d.OwnTemple.transform.position) + " yaw=" + d.OwnTemple.transform.eulerAngles.y.ToString("0"));
        var c = CouncilDirector.Instance;
        if (c != null) sb.AppendLine("tower " + Rel(c.Site.Position) + " gather " + Rel(c.GatherSpot));
        foreach (var n in Npc.All) sb.AppendLine("npc " + n.Id + " " + Rel(n.transform.position));
        for (int k = 0; k < 6; k++) { var dir = BoardSpace.DirOf(w.Yaw + 60 * k); sb.AppendLine("sector " + k + " dir " + dir.ToString("0.00")); }
        return sb.ToString();
    }

    public static string Night() { if (DayNight.Instance != null) DayNight.Instance.SetHour(22); return "night"; }
}
