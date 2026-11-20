using System.IO;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Encounters.Rhythm;

/// <summary>
/// WU-5 dev driver (play mode, scene World): start the dance, tap the world props, pause, capture.
/// unity command script run --file AgentScripts/Wu5Drive.cs --entry Wu5Drive.Dance   (then Auto, Pause, Resume, Tap, Seat, Shot, Info)
/// Captures go to Evidence/worldui/ (local only, never committed).
/// </summary>
public static class Wu5Drive
{
    public static string Dance()
    {
        if (RhythmPartyEncounter.Current != null) return "already";
        var ok = EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, Npc.Find(NpcIds.Host));
        return ok ? "dancing" : "refused";
    }

    public static string Auto(bool on) { var r = RhythmPartyEncounter.Current; if (r == null) return "none"; r.AutoPlayEnabled = on; r.AutoRoute = World.Sim.Encounters.Rhythm.Route.Bar; return "auto " + on; }
    public static string Skip(int block) { var r = RhythmPartyEncounter.Current; r.AutoSkipBlock = b => b == block; return "skip block " + block; }
    public static string Pause() => RhythmPartyEncounter.Current.Pause() ? "paused at " + RhythmPartyEncounter.Current.SongTime.ToString("0.00") : "refused";
    public static string Resume() => RhythmPartyEncounter.Current.Resume() ? "resuming" : "refused";
    public static string TapSol() => Tap(RhythmPartyEncounter.Current.Props.SolPoint);
    public static string TapSeat() => Tap(RhythmPartyEncounter.Current.Props.SeatPoint);
    public static string TapBell() => Tap(RhythmPartyEncounter.Current.Props.BellPoint);
    public static string TapTorch(int i) => Tap(RhythmPartyEncounter.Current.Props.TorchPoint(i));

    private static string Tap(Vector3 world)
    {
        var sp = Camera.main.WorldToScreenPoint(world);
        RhythmPartyEncounter.Current.InjectTap(new Vector2(sp.x, sp.y));
        return "tap at " + sp.x.ToString("0") + "," + sp.y.ToString("0") + " of " + Screen.width + "x" + Screen.height;
    }

    public static string Where()
    {
        var r = RhythmPartyEncounter.Current; var c = Camera.main;
        string o = "screen " + Screen.width + "x" + Screen.height + " band<" + Screen.height * TouchStrips.BandFraction;
        void P(string n, Vector3 w) { var s = c.WorldToScreenPoint(w); o += "\n" + n + " " + s.x.ToString("0") + "," + s.y.ToString("0") + (s.z > 0 ? "" : " BEHIND"); }
        P("sol", r.Props.SolPoint); P("seat", r.Props.SeatPoint); P("bell", r.Props.BellPoint);
        for (int i = 0; i < 4; i++) P("torch" + i, r.Props.TorchPoint(i));
        for (int p = 1; p <= 4; p++) P("drum" + p, r.Circle.DrumTop(p));
        return o;
    }

    public static string Info()
    {
        var r = RhythmPartyEncounter.Current;
        if (r == null) return "no encounter";
        return "phase " + r.Phase + " pause " + r.PauseState + " song " + r.SongTime.ToString("0.00") + " card " + r.OpenCard + " tally P" + r.TallyPerfect + " G" + r.TallyGreat + " O" + r.TallyOk + " M" + r.TallyMiss + " cal " + r.Tuning.CalibrationMs;
    }

    public static string Shot(string name)
    {
        Directory.CreateDirectory("Evidence/worldui");
        ScreenCapture.CaptureScreenshot("Evidence/worldui/" + name + ".png");
        return "shot " + Screen.width + "x" + Screen.height;
    }
}
