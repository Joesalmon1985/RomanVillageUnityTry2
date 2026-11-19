using System.Collections.Generic;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Plague;
using World.Sim.War;

/// <summary>
/// G3-3 play-mode driver for captures (unity command script run --file AgentScripts/WarDrive.cs --entry WarDrive.X).
/// It writes soldiers, wars and ruins straight into the state for the captures only; the game code never does.
/// </summary>
public static class WarDrive
{
    static WorldState S => WorldBootstrap.Instance.Sim.State;
    public static string Begin() { GodBoardDirector.RiseSeconds = 0.05f; return DevStart.Begin(GodBoardStart.Id, 7UL) ? "ok" : DevStart.LastError; }
    public static string Day() { if (DayNight.Instance != null) DayNight.Instance.SetHour(11); return "day"; }
    public static string Pause() { WorldBootstrap.Instance.Clock.Pause("cap"); return "paused"; }
    public static string Resume() { WorldBootstrap.Instance.Clock.Resume("cap"); return "resumed"; }
    public static string Step(string n) { for (int i = 0, k = int.Parse(n); i < k; i++) WorldBootstrap.Instance.Clock.StepOnce(); return "stepped " + n; }
    /// <summary>Stands the camera beside the two duellists, looking at them from the side.</summary>
    public static string CamDuel()
    {
        var l = new List<Vector3>();
        foreach (var v in WarViews.Instance.Soldiers) l.Add(v.Target);
        if (l.Count < 2) return "no duel";
        var mid = (l[0] + l[1]) * 0.5f; var ax = (l[1] - l[0]).normalized; var perp = new Vector3(ax.z, 0, -ax.x);
        Cam(mid + perp * 6f + Vector3.up * 1.5f, mid + Vector3.up * 1.0f);
        return "cam " + mid;
    }
    public static string Step1() => Step("1");
    public static string Step5() => Step("5");
    public static string Step10() => Step("10");
    public static string Step100() => Step("100");
    public static string Step350() => Step("350");
    public static string Step700() => Step("700");
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot " + name; }

    static int NodeNear(Vector3 p) { int best = 1; float bd = 1e9f; for (int n = 1; n <= 48; n++) { float d = BoardSpace.Flat(WarViews.NodePos(n) - p); if (d < bd) { bd = d; best = n; } } return best; }
    static Soldier Make(int people, int node, int[] lv, int hp = -1, int atk = 14)
    {
        var w = S.War;
        var x = new Soldier { Id = w.NextSoldierId++, People = people, BaseAttack = atk, Node = node };
        for (int i = 0; i < 4; i++) x.Levels[i] = lv[i];
        x.Hp = hp >= 0 ? hp : WarApi.MaxHealth(x);
        w.Soldiers.Add(x);
        return x;
    }
    static Camera _cap;
    /// <summary>A free capture camera drawn over the game camera (the walker's camera is bound to its lattice).</summary>
    static void Cam(Vector3 pos, Vector3 look)
    {
        if (_cap == null)
        {
            CamOff();
            var go = new GameObject("CapCam");
            _cap = go.AddComponent<Camera>();
            _cap.CopyFrom(Camera.main);
            _cap.depth = Camera.main.depth + 10; _cap.clearFlags = CameraClearFlags.Skybox;
            var al = go.AddComponent<AudioListener>(); al.enabled = false;
        }
        _cap.fieldOfView = 55f;
        _cap.transform.position = pos; _cap.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }
    public static string CamOff() { foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.name == "CapCam") Object.Destroy(c.gameObject); _cap = null; return "camoff"; }
    static void Look(Vector3 at, float back = 11f, float yaw = 0f)
    {
        var feet = at + BoardSpace.DirOf(yaw + 180f) * back;
        PlayerWalker.Instance.Teleport(feet, yaw, 9f);
    }
    static int HomeNode()
    {
        var home = BoardSpace.Vertex(S.Board.Topology, S.Board.StartCorners[0]);
        int best = 1; float bd = 1e9f;
        for (int n = 1; n <= 48; n++) { float d = BoardSpace.Flat(WarViews.NodePos(n) - home); bool nearT = false; foreach (var t in GodBoardDirector.Instance.Temples) if (BoardSpace.Flat(t.transform.position - WarViews.NodePos(n)) < 45f) nearT = true;
            if (d > 110f && d < bd && !nearT) { bd = d; best = n; } }
        return best;
    }

    /// <summary>Five looks in a row facing the camera, gear levels differing.</summary>
    public static string Lineup()
    {
        S.War.Soldiers.Clear(); S.War.Duels.Clear();
        int node = HomeNode();
        var ids = new List<Soldier>();
        ids.Add(Make(0, node, new[] { 3, 1, 0, 0 })); ids.Add(Make(0, node, new[] { 0, 3, 1, 0 })); ids.Add(Make(0, node, new[] { 0, 1, 3, 0 })); ids.Add(Make(0, node, new[] { 0, 0, 1, 3 })); ids.Add(Make(0, node, new[] { 0, 0, 0, 0 }));
        var o = WarViews.NodePos(node) + new Vector3(16f, 0, -16f);
        for (int i = 0; i < 5; i++) WarViews.Instance.Pinned[ids[i].Id] = new Vector4(o.x + (i - 2) * 1.9f, 0, o.z, 180f);
        Cam(o + new Vector3(0, 1.4f, -6.5f), o + new Vector3(0, 1.0f, 0));
        return "lineup at node " + node;
    }

    /// <summary>Two peoples at war with a soldier each at a node; B starts hurt. Step the clock to play it.</summary>
    public static string Duel()
    {
        S.War.Soldiers.Clear(); S.War.Duels.Clear();
        int node = HomeNode();
        int n = S.War.PeopleCount;
        S.War.AtWar = new bool[n * n]; S.War.AtWar[0 * n + 1] = S.War.AtWar[1 * n + 0] = true;
        Make(0, node, new[] { 1, 2, 2, 0 }, -1, 15);
        var b = Make(1, node, new[] { 1, 1, 2, 0 }, 60, 13);
        Look(WarViews.NodePos(node), 9f, 0f);
        return "duel at node " + node + " hp " + b.Hp;
    }

    /// <summary>A people-1 settlement with huts a short way from the home camp; goods delivered so the huts work.</summary>
    public static string Huts()
    {
        var topo = S.Board.Topology;
        int corner = -1;
        foreach (var st in S.Table != null ? new List<int>() : new List<int>()) { }
        var home = BoardSpace.Vertex(topo, S.Board.StartCorners[0]);
        float bd = 1e9f;
        for (int c = 0; c < topo.CornerCount; c++)
        {
            if (!PlotLayout.IsSettlementCorner(topo, c) || WarApi.SettlementAt(S, c) != null) continue;
            float d = BoardSpace.Flat(BoardSpace.Vertex(topo, c) - home);
            if (d > 90f && d < bd) { bd = d; corner = c; }
        }
        S.Table.CornerOwner[corner] = 1; S.Table.CornerLevel[corner] = 1; // a real table settlement: the table's sync gives it its war settlement
        WarRules.AddSettlement(S, 1, corner, false, false);
        var st2 = WarApi.SettlementAt(S, corner);
        for (int i = 0; i < 5; i++) WarRules.Deliver(S, corner, i, 12);
        Look(BoardSpace.Vertex(topo, corner), 24f, 0f);
        return "settlement at corner " + corner + " huts=" + st2.Huts.Count;
    }
    public static string HutsLook() { var c = WarViews.Instance.SiteCorners(); return c; }

    public static string ToLodge()
    {
        var site = WarViews.Instance.SiteOf(S.Board.StartCorners[0]);
        if (site == null || site.Lodge == null) return "no lodge";
        var at = site.Lodge.transform.position;
        PlayerWalker.Instance.Teleport(at + new Vector3(0, 0, -20f) + site.Lodge.transform.forward * 0f, 0f, 9f);
        return "lodge at " + at;
    }

    /// <summary>Burns the most recent non-home settlement: removed from the state, a ruin added (what the war module does).</summary>
    public static string Burn()
    {
        WarSettlement st = null;
        foreach (var x in S.War.Settlements) if (!x.IsHome) st = x;
        if (st == null) return "none";
        int corner = st.Corner;
        WarRules.RemoveSettlement(S, corner);
        S.War.Ruins.Add(new Ruin { Corner = corner, People = st.People, ExpireClock = S.War.Clock + WarData.RuinTicks });
        return "burned " + corner;
    }

    public static string BoardOpen() { return GodBoardDirector.Instance.TrySacrifice(true) ? "sacrificed" : "refused"; }
    public static string ArmyAcrossIsland()
    {
        // twelve soldiers of the four peoples at nodes across the island, for the top-down marker capture
        S.War.Soldiers.Clear();
        int[] nodes = { 3, 9, 14, 20, 26, 31, 36, 41, 45, 6, 17, 38 };
        for (int i = 0; i < nodes.Length; i++)
        {
            var lv = new[] { i % 4 == 0 ? 3 : 0, i % 4 == 1 ? 3 : 0, i % 4 == 2 ? 3 : 0, i % 4 == 3 ? 3 : 0 };
            Make(i % 4, nodes[i], lv); if (i % 3 == 0) Make(i % 4, nodes[i], lv);
        }
        return "army";
    }
    public static string ShotLineup() => Shot("war_lineup");
    public static string ShotDuel() => Shot("war_duel");
    public static string ShotDuel2() => Shot("war_duel2");
    public static string ShotBurn() => Shot("war_burning");
    public static string ShotBurn2() => Shot("war_ruin");
    public static string ShotHuts() => Shot("war_huts");
    public static string ShotLodge() => Shot("war_lodge");
    public static string ShotBoard() => Shot("war_board_markers");
    public static string ShotLoot() => Shot("war_looted");
    public static string Loot() { var site = WarViews.Instance.SiteOf(S.Board.StartCorners[1]); site.Loot(); var c = BoardSpace.Vertex(S.Board.Topology, S.Board.StartCorners[1]); PlayerWalker.Instance.Teleport(c + new Vector3(0, 0, -16f), 0f, 9f); return "loot"; }
    public static string Info() { var v = WarViews.Instance; return v == null ? "none" : "soldiers=" + v.SoldierCount + " sites=" + v.SiteCount + " elevated=" + v.Elevated + " duels=" + v.DuelsStarted + " swings=" + v.SwingsPlayed + " deaths=" + v.DeathsPlayed + Each(v); }
    static string Each(WarViews v) { var sb = new System.Text.StringBuilder(); foreach (var s in v.Soldiers) sb.Append(" [" + s.Id + " " + s.Look + " " + s.Wear + " shield=" + s.HasShield + "/" + s.ShieldDropped + " " + s.Action + " hp=" + WarApi.SoldierById(WorldBootstrap.Instance.Sim.State, s.Id)?.Hp + "]"); return sb.ToString(); }
}
public static class WarDrive2
{
    static WorldState S => WorldBootstrap.Instance.Sim.State;
    static Camera Cap => GameObject.Find("CapCam")?.GetComponent<Camera>();
    static void Cam(Vector3 pos, Vector3 look)
    {
        var c = Cap;
        if (c == null) { WarDrive.CamOff(); var go = new GameObject("CapCam"); c = go.AddComponent<Camera>(); c.CopyFrom(Camera.main); c.depth = Camera.main.depth + 10; c.clearFlags = CameraClearFlags.Skybox; var al = go.AddComponent<AudioListener>(); al.enabled = false; }
        c.fieldOfView = 55f; c.transform.position = pos; c.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }
    public static string CamSite() => CamSiteAt(0f);
    public static string CamSiteA() => CamSiteAt(90f);
    public static string CamSiteB() => CamSiteAt(180f);
    public static string CamSiteC() => CamSiteAt(270f);
    static string CamSiteAt(float yaw)
    {
        int corner = -1; foreach (var x in S.War.Settlements) if (!x.IsHome) corner = x.Corner; foreach (var r in S.War.Ruins) corner = r.Corner;
        var c = BoardSpace.Vertex(S.Board.Topology, corner);
        var site = WarViews.Instance.SiteOf(corner);
        var cen = c; int n = 1; foreach (var h in site.Huts.Values) { cen += h.transform.position; n++; }
        cen /= n;
        Cam(cen + BoardSpace.DirOf(yaw) * 20f + Vector3.up * 9f, cen + Vector3.up * 0.5f);
        return "camsite " + corner + " huts " + site.HutCount + " centre " + cen;
    }
    public static string CamLodge()
    {
        var site = WarViews.Instance.SiteOf(S.Board.StartCorners[0]);
        if (site == null || site.Lodge == null) return "no lodge";
        var l = site.Lodge.transform; var at = l.position;
        Cam(at + l.forward * 17f + l.right * 7f + Vector3.up * 5f, at + Vector3.up * 2f);
        return "camlodge " + at;
    }
    public static string CamLoot()
    {
        var c = BoardSpace.Vertex(S.Board.Topology, S.Board.StartCorners[1]);
        Cam(c + new Vector3(0, 7f, -15f), c + Vector3.up * 0.5f);
        WarViews.Instance.SiteOf(S.Board.StartCorners[1]).Loot();
        return "camloot " + c;
    }
}
