using System.Text;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Historic;
using World.Sim.Eras;

/// <summary>G4-3 play-mode driver (unity command script run --file AgentScripts/HistoricDrive.cs --entry HistoricDrive.X).</summary>
public static class HistoricDrive
{
    private static WorldBootstrap B => WorldBootstrap.Instance;
    private static WorldState S => B.Sim.State;

    public static string BeginEve() { return DevStart.Begin(GodBoardStart.EveId, 7UL) ? "ok" : DevStart.LastError; }
    public static string BeginHistoric() { return DevStart.Begin(GodBoardStart.HistoricId, 7UL) ? "ok" : DevStart.LastError; }
    public static string Day() { if (DayNight.Instance != null) DayNight.Instance.SetHour(11); return "day"; }
    public static string Pause() { B.Clock.Pause("drive"); return "paused"; }
    public static string Run() { B.Clock.Resume("drive"); return "running"; }
    public static string Step() { return StepN(1); }
    public static string StepN(int n) { for (int i = 0; i < n; i++) B.Clock.StepOnce(); return "tick " + S.Tick; }
    public static string Steps600() { return StepN(600); }
    public static string Steps6000() { return StepN(6000); }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot " + name; }
    public static string Phone() { return GameViewPhoneRuntime(); }
    private static string GameViewPhoneRuntime() { return Screen.width + "x" + Screen.height; }

    public static string Info()
    {
        var h = S.Historic;
        var sb = new StringBuilder();
        sb.Append("tick=" + S.Tick + " era=" + AgesApi.Current(S) + " historic=" + (h != null));
        if (h != null)
        {
            sb.Append(" castles=");
            foreach (var p in h.Peoples) sb.Append(p.Castle.StagesDone + "/");
            sb.Append(" buildings=" + h.Buildings.Count + " jobs=" + h.Jobs.Count);
            sb.Append(" trees(m/y/s/stump)=" + HistoricApi.Trees(S, TreeStage.Mature) + "/" + HistoricApi.Trees(S, TreeStage.Young) + "/" + HistoricApi.Trees(S, TreeStage.Sapling) + "/" + HistoricApi.Trees(S, TreeStage.Stump));
        }
        var d = HistoricDirector.Instance;
        if (d != null) sb.Append(" views: treeHexes=" + d.TreeMeshHexes + " buildings=" + d.BuildingViews + " walkers=" + d.WalkersShown + " dust=" + d.DustPuffs + " templesDown=" + d.TemplesDown);
        return sb.ToString();
    }

    private static string Stand(Vector3 target, float dist, float yaw, float height)
    {
        var dir = BoardSpace.DirOf(yaw);
        var feet = target - dir * dist;
        PlayerWalker.Instance.Teleport(feet, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, height);
        return "at " + feet;
    }

    public static string Castle0() { return Stand(HistoricDirector.Instance.CastlePos(0), 30f, 200f, 14f); }
    public static string Castle1() { return Stand(HistoricDirector.Instance.CastlePos(1), 26f, 200f, 9f); }
    public static string Temple0() { var t = GodBoardDirector.Instance.OwnTemple; return t == null ? "no temple" : Stand(t.transform.position, 24f, 200f, 8f); }

    /// <summary>Stands before the forest hex with the most trees (or stumps) on it.</summary>
    public static string Forest()
    {
        int best = -1, bs = -1;
        for (int i = 0; i < S.Historic.Hexes.Length; i++)
        {
            var hr = S.Historic.Hexes[i];
            if (hr == null || hr.Terrain != World.Sim.Terrain.Forest) continue;
            int sc = 0; foreach (int t in hr.Trees) if (t == (int)TreeStage.Stump) sc += 3; else if (t != 0) sc++;
            foreach (var b in S.Historic.Buildings) if (b.Hex == i) sc += 4;
            if (sc > bs) { bs = sc; best = i; }
        }
        if (best < 0) return "no forest";
        Stand(BoardSpace.Hex(S.Board.Topology, best), 44f, 200f, 12f);
        return "forest hex " + best + " score " + bs;
    }

    private static Camera _cam;
    private static string Cam(Vector3 pos, Vector3 target, float fov)
    {
        if (_cam == null) { var go = new GameObject("DriveCam"); _cam = go.AddComponent<Camera>(); _cam.depth = 50; _cam.farClipPlane = 6000f; _cam.nearClipPlane = 0.5f; }
        _cam.enabled = true; _cam.fieldOfView = fov;
        _cam.transform.position = pos; _cam.transform.LookAt(target);
        if (DayNight.Instance != null) DayNight.Instance.SetHour(11);
        return "cam " + pos;
    }
    public static string CamOff() { if (_cam != null) _cam.enabled = false; return "off"; }
    private static string CastleCam(int p, float dist, float up)
    {
        var d = HistoricDirector.Instance; var c = d.CastlePos(p);
        var toward = new Vector3(c.x, 0, c.z).normalized; // from the island centre
        return Cam(c + toward * dist + Vector3.up * up + Vector3.Cross(Vector3.up, toward) * dist * 0.3f, c + Vector3.up * 4f, 40f);
    }
    public static string CC0() { return CastleCam(0, 30f, 16f); }
    public static string CC1() { return CastleCam(1, 30f, 16f); }
    public static string CC2() { return CastleCam(2, 30f, 16f); }
    public static string CC3() { return CastleCam(3, 30f, 16f); }
    public static string TempleCam() { var t = GodBoardDirector.Instance.OwnTemple; var c = t.transform.position; var o = new Vector3(c.x, 0, c.z).normalized; return Cam(c + o * 26f + Vector3.up * 9f, c + Vector3.up * 3f, 40f); }
    public static string ForestCam()
    {
        int best = -1, bs = -1;
        for (int i = 0; i < S.Historic.Hexes.Length; i++)
        {
            var hr = S.Historic.Hexes[i];
            if (hr == null || hr.Terrain != World.Sim.Terrain.Forest) continue;
            int sc = 0; foreach (int t in hr.Trees) if (t == (int)TreeStage.Stump) sc += 3; else if (t != 0) sc++;
            foreach (var b in S.Historic.Buildings) if (b.Hex == i) sc += 6;
            if (sc > bs) { bs = sc; best = i; }
        }
        var c = BoardSpace.Hex(S.Board.Topology, best);
        var o = new Vector3(c.x, 0, c.z).normalized;
        Cam(c + o * 70f + Vector3.up * 48f, c, 45f);
        return "forest hex " + best + " score " + bs;
    }
    public static string IslandCam() { return Cam(new Vector3(0, 1050f, -760f), Vector3.zero, 40f); }
    public static string Island()
    {
        var w = PlayerWalker.Instance;
        w.Teleport(new Vector3(0, 260f, -420f), 0f, 0f);
        return "high";
    }
}
public static class HistoricEve
{
    public static string Hold() { HistoricDirector.EveHoldSeconds = 1e6f; return "hold"; }
    public static string Release() { HistoricDirector.Instance.ReleaseEve(); return "released"; }
}
