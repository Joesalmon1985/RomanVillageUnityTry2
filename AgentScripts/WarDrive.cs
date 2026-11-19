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
    static void Look(Vector3 at, float back = 11f, float yaw = 0f)
    {
        var feet = at + BoardSpace.DirOf(yaw + 180f) * back;
        PlayerWalker.Instance.Teleport(feet, yaw, 9f);
    }
    static int HomeNode() => NodeNear(BoardSpace.Vertex(S.Board.Topology, S.Board.StartCorners[0]) + new Vector3(0, 0, 40));

    /// <summary>Five looks in a ring at a node beside the home camp, gear levels differing.</summary>
    public static string Lineup()
    {
        S.War.Soldiers.Clear(); S.War.Duels.Clear();
        int node = HomeNode();
        Make(0, node, new[] { 3, 1, 0, 0 }); Make(0, node, new[] { 0, 3, 1, 0 }); Make(0, node, new[] { 0, 1, 3, 0 }); Make(0, node, new[] { 0, 0, 1, 3 }); Make(0, node, new[] { 0, 0, 0, 0 });
        Look(WarViews.NodePos(node), 11f, 0f);
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
        WarRules.AddSettlement(S, 0, corner, false, false);
        var st2 = WarApi.SettlementAt(S, corner);
        for (int i = 0; i < 5; i++) WarRules.Deliver(S, corner, i, 6);
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
    public static string Info() { var v = WarViews.Instance; return v == null ? "none" : "soldiers=" + v.SoldierCount + " sites=" + v.SiteCount + " elevated=" + v.Elevated + " duels=" + v.DuelsStarted + " swings=" + v.SwingsPlayed + " deaths=" + v.DeathsPlayed; }
}
