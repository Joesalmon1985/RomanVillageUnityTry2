using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Table;

/// <summary>G1-7 play-mode driver for captures (unity command script run --file AgentScripts/GodBoardG17.cs --entry GodBoardG17.X).</summary>
public static class GodBoardG17
{
    private static WorldBootstrap B => WorldBootstrap.Instance;
    private static GodBoardDirector D => GodBoardDirector.Instance;

    public static string Begin() { GodBoardDirector.RiseSeconds = 0.05f; return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }
    public static string Day() { if (DayNight.Instance != null) DayNight.Instance.SetHour(11); return "day"; }
    public static string Fast() { Time.timeScale = 3f; return "x3"; }
    public static string Slowmo() { Time.timeScale = 0.12f; return "slowmo"; }
    public static string Slow() { Time.timeScale = 1f; return "x1"; }

    public static string Guide()
    {
        var p = PrinceDirector.Instance;
        return p == null ? "no prince" : "guide=" + p.Guide + " prince=" + p.Prince.transform.position + " player=" + PlayerWalker.Instance.Position + " said=" + p.Said.Count;
    }

    /// <summary>Mira's dialogue open with her offer, player beside her.</summary>
    public static string Mira()
    {
        var mira = Npc.Find(NpcIds.Mira);
        var w = PlayerWalker.Instance;
        var at = mira.transform.position;
        var feet = at + new Vector3(0, 0, -5f);
        w.Teleport(feet, 0f, 6f);
        mira.DialogueBuilder?.Invoke(mira);
        DialoguePanel.Open(mira);
        return "options=" + string.Join("|", mira.Options.ConvertAll(o => o.Label));
    }

    public static string Open()
    {
        DialoguePanel.Close();
        return D.TrySacrifice(true) ? "sacrificed" : "refused";
    }

    public static string Place()
    {
        B.Clock.Pause("g17");
        D.TapAt(D.AdviseCorner >= 0 ? D.ScreenOfCorner(D.AdviseCorner) : Vector2.zero);
        for (int i = 0; i < 3; i++) B.Clock.StepOnce();
        if (D.EdgeSpots.Count > 0) D.TapAt(D.ScreenOfEdge(D.EdgeSpots[0]));
        var t = D.T; int g = 0;
        while (!(t.Phase == TablePhase.Main && t.Current == 0) && g++ < 30000) B.Clock.StepOnce();
        return "phase=" + t.Phase + " cur=" + t.Current + " steps=" + g;
    }

    /// <summary>Steps ticks until villagers are out on a producing hex of the player's.</summary>
    public static string UntilWalkers()
    {
        int g = 0;
        while (D.Collectors.Active.Count == 0 && g++ < 6000) B.Clock.StepOnce();
        return "sent=" + D.Collectors.Sent + " active=" + D.Collectors.Active.Count + " steps=" + g;
    }

    public static string UntilMany()
    {
        int g = 0;
        while (D.Collectors.Active.Count < 4 && g++ < 30000) B.Clock.StepOnce();
        return "sent=" + D.Collectors.Sent + " active=" + D.Collectors.Active.Count + " steps=" + g;
    }

    public static string Detail()
    {
        var l = new System.Collections.Generic.List<string>();
        foreach (var w in D.Collectors.Active) l.Add(w.Leg + "@" + w.Go.transform.position + " scr=" + D.BoardCam.WorldToScreenPoint(w.Go.transform.position) + " mesh=" + w.Go.GetComponent<MeshFilter>().sharedMesh.bounds.size + " active=" + w.Go.activeSelf + " scale=" + w.Go.transform.localScale.x + " r=" + (w.Go.GetComponent<MeshRenderer>() != null && w.Go.GetComponent<MeshRenderer>().enabled));
        return string.Join(" ; ", l);
    }

    public static string Walkers() { return "sent=" + D.Collectors.Sent + " active=" + D.Collectors.Active.Count + " delivered=" + D.Collectors.Delivered; }
    public static string ShotLead() { return Shot("g17_prince_leading"); }
    public static string ShotMira() { return Shot("g17_mira_offer"); }
    public static string ShotWalkers() { return Shot("g17_villagers_walking"); }
    public static string Reset() { D.ResetView(); return "reset"; }
    public static string ZoomIn() { D.Zoom(2.2f); return "zoom"; }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot"; }
}
