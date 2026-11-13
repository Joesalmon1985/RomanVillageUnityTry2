using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>Demons on settlement corners and road hex edges.</summary>
        public Demons.DemonsState Demons;
    }
}

namespace World.Sim.Demons
{
    public enum DemonSiteKind { Corner = 0, HexEdge = 1 }

    public sealed class DemonTuning
    {
        /// <summary>A 7 before this tick does nothing.</summary>
        public int GraceTicks = 1800;
        /// <summary>Most demons on one site; one more causes an outbreak.</summary>
        public int Cap = 3;
        /// <summary>Demons added per 7.</summary>
        public int PerSeven = 1;
    }

    /// <summary>All demon state (WorldState.Demons).</summary>
    public sealed class DemonsState
    {
        public DemonTuning Tuning = new DemonTuning();
        /// <summary>Demons per corner id (150).</summary>
        public int[] Corner = new int[0];
        /// <summary>Demons per hex edge id (210).</summary>
        public int[] Edge = new int[0];
        public Rng Rng = new Rng();
        public int Placed;
        public int Banished;
        public int Outbreaks;
    }

    /// <summary>Removes every demon from one of your sites (F1, tests, CastSpell).</summary>
    public sealed class Banish : Command { public DemonSiteKind SiteKind; public int SiteId; }

    public static class DemonRefusals
    {
        public const string GameOver = "game_over";
        public const string NotOwner = "not_owner";
        public const string NoDemons = "no_demons";

        public static string Sentence(string code)
        {
            switch (code)
            {
                case GameOver: return "The game is over.";
                case NotOwner: return "That belongs to another people.";
                case NoDemons: return "There are no demons there.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code) => CommandResult.Reject(code, Sentence(code));
    }

    // Events (REFACTOR_SPEC 5.3).

    public sealed class DemonPlaced : SimEvent { public DemonSiteKind SiteKind; public int SiteId; public int Count; }

    public sealed class DemonOutbreak : SimEvent
    {
        public DemonSiteKind SiteKind;
        public int SiteId;
        public DemonSiteKind[] SpreadKinds = new DemonSiteKind[0];
        public int[] SpreadIds = new int[0];
    }

    public sealed class DemonsBanished : SimEvent { public DemonSiteKind SiteKind; public int SiteId; public int Count; public int Faction; }

    public struct DemonSiteView
    {
        public DemonSiteKind Kind;
        public int Id, Count, Faction;
        public double X, Z;
        public string Effect;
    }

    /// <summary>Read helpers (REFACTOR_SPEC A.3). Static, never mutate.</summary>
    public static class DemonApi
    {
        public static int DemonsAt(WorldState s, DemonSiteKind kind, int id) =>
            s == null ? 0 : kind == DemonSiteKind.Corner ? EconomyLogic.CornerDemons(s, id) : EconomyLogic.EdgeDemons(s, id);

        /// <summary>Every site holding demons: corners ascending, then hex edges ascending.</summary>
        public static IReadOnlyList<DemonSiteView> DemonSites(WorldState s)
        {
            var list = new List<DemonSiteView>();
            var d = s?.Demons;
            if (d == null || s.Board == null) return list;
            var t = s.Board.Topology;
            for (int c = 0; c < d.Corner.Length; c++)
            {
                if (d.Corner[c] <= 0) continue;
                list.Add(new DemonSiteView
                {
                    Kind = DemonSiteKind.Corner, Id = c, Count = d.Corner[c], Faction = DemonLogic.SiteFaction(s, DemonSiteKind.Corner, c),
                    X = t.VertexX(c), Z = t.VertexZ(c), Effect = Effect(DemonSiteKind.Corner),
                });
            }
            for (int e = 0; e < d.Edge.Length; e++)
            {
                if (d.Edge[e] <= 0) continue;
                list.Add(new DemonSiteView
                {
                    Kind = DemonSiteKind.HexEdge, Id = e, Count = d.Edge[e], Faction = DemonLogic.SiteFaction(s, DemonSiteKind.HexEdge, e),
                    X = t.HexEdgeMidX(e), Z = t.HexEdgeMidZ(e), Effect = Effect(DemonSiteKind.HexEdge),
                });
            }
            return list;
        }

        /// <summary>"the home camp", "the road between the forest and the hills".</summary>
        public static string SiteName(WorldState s, DemonSiteKind kind, int id)
        {
            if (s?.Board == null) return "";
            if (kind == DemonSiteKind.Corner)
            {
                var st = EconomyLogic.SettlementAt(s, id);
                return st != null ? EconomyApi.SettlementName(s, st.Id) : "the corner by the " + EconomyApi.CornerTerrains(s, id);
            }
            var t = s.Board.Topology;
            if (id < 0 || id >= t.HexEdgeCount) return "a road";
            var shared = new List<int>();
            var hb = t.VertexHexes(t.HexEdgeCornerB(id));
            foreach (int h in t.VertexHexes(t.HexEdgeCornerA(id))) if (Contains(hb, h)) shared.Add(h);
            shared.Sort();
            if (shared.Count == 2)
                return "the road between the " + TerrainInfo.DisplayName(s.Board.HexTerrain[shared[0]]) + " and the "
                    + TerrainInfo.DisplayName(s.Board.HexTerrain[shared[1]]);
            if (shared.Count == 1) return "the road along the " + TerrainInfo.DisplayName(s.Board.HexTerrain[shared[0]]);
            return "a road";
        }

        /// <summary>What demons do there (one clause, for reasons and bubbles).</summary>
        public static string Effect(DemonSiteKind kind) => kind == DemonSiteKind.Corner
            ? "its collectors stop and it trades with no one"
            : "no carrier can pass and no road is built there";

        private static bool Contains(IReadOnlyList<int> list, int x)
        {
            foreach (int v in list) if (v == x) return true;
            return false;
        }
    }

    /// <summary>Demon rules (REFACTOR_SPEC 2.C.4).</summary>
    internal static class DemonLogic
    {
        /// <summary>The people owning a site (settlement on the corner, road on the edge), or -1.</summary>
        public static int SiteFaction(WorldState s, DemonSiteKind kind, int id)
        {
            if (kind == DemonSiteKind.Corner)
            {
                var st = EconomyLogic.SettlementAt(s, id);
                return st != null ? st.Faction : -1;
            }
            var r = EconomyLogic.RoadOnEdge(s, id);
            return r != null ? r.Faction : -1;
        }

        public static CommandResult CheckBanish(WorldState s, int faction, DemonSiteKind kind, int id)
        {
            if (s == null || s.Demons == null) return DemonRefusals.Reject(DemonRefusals.NoDemons);
            if (EconomyLogic.IsGameOver(s)) return DemonRefusals.Reject(DemonRefusals.GameOver);
            if (DemonApi.DemonsAt(s, kind, id) <= 0) return DemonRefusals.Reject(DemonRefusals.NoDemons);
            if (SiteFaction(s, kind, id) != faction) return DemonRefusals.Reject(DemonRefusals.NotOwner);
            return CommandResult.Accept();
        }

        public static void Banish(WorldState s, int faction, DemonSiteKind kind, int id, EventBuffer ev)
        {
            var d = s.Demons;
            var arr = kind == DemonSiteKind.Corner ? d.Corner : d.Edge;
            int n = arr[id];
            arr[id] = 0;
            d.Banished += n;
            ev?.Add(new DemonsBanished { SiteKind = kind, SiteId = id, Count = n, Faction = faction });
        }

        /// <summary>Sites: finished settlement corners ascending, then built road hex edges ascending (both peoples).</summary>
        public static List<(DemonSiteKind kind, int id)> Sites(WorldState s)
        {
            var list = new List<(DemonSiteKind, int)>();
            var e = s.Economy;
            if (e == null) return list;
            var corners = new List<int>();
            foreach (var st in e.Settlements) if (st.State == SettlementState.Active) corners.Add(st.Corner);
            corners.Sort();
            foreach (int c in corners) list.Add((DemonSiteKind.Corner, c));
            for (int he = 0; he < e.RoadOnEdge.Length; he++)
            {
                var r = EconomyLogic.RoadOnEdge(s, he);
                if (r != null && r.State == RoadState.Built) list.Add((DemonSiteKind.HexEdge, he));
            }
            return list;
        }

        /// <summary>On a 7 after the grace period: draw one site uniformly and add PerSeven demons (with outbreaks).</summary>
        public static void OnRolled(WorldState s, EventBuffer ev, int d1, int d2)
        {
            var d = s.Demons;
            if (d == null || d1 + d2 != 7 || s.Tick < d.Tuning.GraceTicks) return;
            var sites = Sites(s);
            if (sites.Count == 0) return;
            var site = sites[d.Rng.NextInt(sites.Count)];
            var outbroken = new HashSet<(DemonSiteKind, int)>();
            for (int i = 0; i < d.Tuning.PerSeven; i++) AddDemon(s, site.kind, site.id, ev, outbroken);
        }

        private static void AddDemon(WorldState s, DemonSiteKind kind, int id, EventBuffer ev, HashSet<(DemonSiteKind, int)> outbroken)
        {
            var d = s.Demons;
            var queue = new Queue<(DemonSiteKind kind, int id)>();
            queue.Enqueue((kind, id));
            while (queue.Count > 0)
            {
                var (k, x) = queue.Dequeue();
                var arr = k == DemonSiteKind.Corner ? d.Corner : d.Edge;
                if (arr[x] < d.Tuning.Cap)
                {
                    arr[x]++;
                    d.Placed++;
                    ev.Add(new DemonPlaced { SiteKind = k, SiteId = x, Count = arr[x] });
                    continue;
                }
                if (!outbroken.Add((k, x))) continue; // each site outbreaks at most once per draw
                d.Outbreaks++;
                var spread = Neighbours(s, k, x);
                var kinds = new DemonSiteKind[spread.Count];
                var ids = new int[spread.Count];
                for (int i = 0; i < spread.Count; i++) { kinds[i] = spread[i].kind; ids[i] = spread[i].id; }
                ev.Add(new DemonOutbreak { SiteKind = k, SiteId = x, SpreadKinds = kinds, SpreadIds = ids });
                foreach (var n in spread) queue.Enqueue(n);
            }
        }

        /// <summary>Corner: built road edges touching it. Edge: its end corners holding finished settlements, and built road edges sharing an end.</summary>
        private static List<(DemonSiteKind kind, int id)> Neighbours(WorldState s, DemonSiteKind kind, int id)
        {
            var t = s.Board.Topology;
            var list = new List<(DemonSiteKind, int)>();
            if (kind == DemonSiteKind.Corner)
            {
                var edges = new List<int>(t.CornerHexEdges(id));
                edges.Sort();
                foreach (int e in edges) if (Built(s, e)) list.Add((DemonSiteKind.HexEdge, e));
                return list;
            }
            int a = t.HexEdgeCornerA(id), b = t.HexEdgeCornerB(id);
            var corners = new List<int> { a, b };
            corners.Sort();
            foreach (int c in corners)
            {
                var st = EconomyLogic.SettlementAt(s, c);
                if (st != null && st.State == SettlementState.Active) list.Add((DemonSiteKind.Corner, c));
            }
            var others = new SortedSet<int>();
            foreach (int c in corners)
                foreach (int e in t.CornerHexEdges(c))
                    if (e != id && Built(s, e)) others.Add(e);
            foreach (int e in others) list.Add((DemonSiteKind.HexEdge, e));
            return list;
        }

        private static bool Built(WorldState s, int hexEdge)
        {
            var r = EconomyLogic.RoadOnEdge(s, hexEdge);
            return r != null && r.State == RoadState.Built;
        }
    }

    internal sealed class BanishHandler : CommandHandler<Banish>
    {
        protected override CommandResult Validate(WorldState s, Banish c) => DemonLogic.CheckBanish(s, c.PlayerId, c.SiteKind, c.SiteId);

        protected override CommandResult Apply(WorldState s, Banish c, EventBuffer ev)
        {
            DemonLogic.Banish(s, c.PlayerId, c.SiteKind, c.SiteId, ev);
            return CommandResult.Accept();
        }
    }

    /// <summary>Entry point of the Demons module (REFACTOR_SPEC A.4).</summary>
    public static class DemonsModule
    {
        public static void InitState(WorldState s)
        {
            var t = s.Board.Topology;
            s.Demons = new DemonsState
            {
                Corner = new int[t.CornerCount],
                Edge = new int[t.HexEdgeCount],
                Rng = s.Rng.Fork("demons"),
            };
        }

        public static void Register(WorldSim sim)
        {
            var hooks = EconomyApi.Hooks(sim);
            hooks.IsCornerBlocked.Add((s, c) => EconomyLogic.CornerDemons(s, c) > 0);
            hooks.IsEdgeBlocked.Add((s, e) => EconomyLogic.EdgeDemons(s, e) > 0);
            hooks.IsSettlementHalted.Add(EconomyLogic.SettlementHalted);
            hooks.OnRolled.Add(DemonLogic.OnRolled);
            sim.RegisterHandler(new BanishHandler());
        }
    }
}
