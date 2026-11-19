using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Campaign;

/// <summary>
/// G2-4 dev driver (play mode): raise spires without playing the campaign, walk to them, dance, take captures.
/// unity command script run --file AgentScripts/SpireDrive.cs --entry SpireDrive.Begin   (then Raise, ToSpire, Dance, Shot)
/// Raise only builds the views (SpireDirector.Raise); no sim state changes, so a save does not remember it.
/// </summary>
public static class SpireDrive
{
    public static string Begin() { return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }
    public static string Day() { if (DayNight.Instance != null) DayNight.Instance.SetHour(11); return "day"; }

    /// <summary>Raises n spires (Earth, Fire, Water, Air order) at people 0, 1, 2, 3 start corners.</summary>
    public static string Raise(int n)
    {
        var d = SpireDirector.Instance;
        var b = WorldBootstrap.Instance.Sim.State.Board;
        var el = new[] { Element.Earth, Element.Fire, Element.Water, Element.Air };
        for (int i = 0; i < n && d.Count < 4; i++) d.Raise(el[d.Count], d.Count, b.StartCorners[d.Count], true);
        return "spires=" + d.Count;
    }

    /// <summary>Stands the player at the stage of spire i (0-based), looking at the fire with the spire behind it.</summary>
    public static string ToSpire(int i)
    {
        var s = SpireDirector.Instance.Stage(i);
        PlayerWalker.Instance.Teleport(s.Layout.DancerSpot, s.Layout.DancerYaw, 10f);
        return "at " + s.Layout.DancerSpot;
    }

    /// <summary>Stands the player some way off the spire (the approach view).</summary>
    public static string Approach(int i, float metres)
    {
        var s = SpireDirector.Instance.Stage(i);
        var dir = s.Layout.DancerSpot - s.SpireBase; dir.y = 0; dir.Normalize();
        var feet = s.SpireBase + dir * metres;
        var look = s.SpireBase + Vector3.up * 6f - feet;
        PlayerWalker.Instance.Teleport(feet, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 8f);
        return "at " + feet;
    }

    public static string Dance(int i, bool auto)
    {
        var ok = SpireDirector.Instance.Stage(i).StartDance();
        var r = RhythmPartyEncounter.Current;
        if (r != null) r.AutoPlayEnabled = auto;
        return ok ? "dancing, pack=" + (r != null ? r.PackId : "?") + " fellBack=" + (r != null && r.PackFellBack) : "refused";
    }

    public static string Info()
    {
        var r = RhythmPartyEncounter.Current;
        return "spires=" + SpireDirector.Instance.Count + (r != null ? " pack=" + r.PackId + " bpm=" + r.Chart.Bpm + " song=" + r.SongTime.ToString("0.0") + " synth=" + r.Stems.IsSynth : " no encounter");
    }

    public static string Stop() { if (EncounterHost.Instance != null) EncounterHost.Instance.Abort(); return "stopped"; }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot"; }
}
