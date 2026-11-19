using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using World.Sim.Plague;
using World.Sim.Table;
using World.Sim.War;

namespace World.Sim.GodBoard
{
    /// <summary>
    /// Joins the god board's modules in a game on <see cref="Ruleset.GodBoard"/> (PRD_GODBOARD G0-2): the table (turn engine,
    /// G1-1) and the plague (G2-1). InitState runs in <see cref="WorldSimFactory.CreateNew(ulong, GameSetup)"/>; Register runs
    /// in <see cref="Save.SaveCodec.CreateSim"/>, so a loaded game gets the same wiring (the seams are not saved).
    /// <list type="bullet">
    /// <item>TU-2, PL-9: a hex with demons on any of its plague nodes produces nothing (<see cref="PlagueApi.HexBlocked"/>).</item>
    /// <item>PL-6: every 7 (the table's Epidemic, after discards) is an epidemic (<see cref="PlagueRules.Epidemic"/>).</item>
    /// <item>PL-5: after the last people's turn of a round, the infection draw (<see cref="PlagueRules.InfectRound"/>).</item>
    /// <item>HI-8, HI-1: when the age ends the plague ends and the island steps into Historic (G4-2, HistoricModule.Switch). The view of the step
    /// itself (AdvanceChapter, temples falling) is the Historic stub's (G1-8).</item>
    /// </list>
    /// Seams left for later packages: the table AI (G1-2) wires its hooks in <see cref="WireAi"/>; the war (G3-2) reads the
    /// plague graph through <see cref="PlagueApi"/> and registers its own module beside this one.
    /// </summary>
    public static class GodBoardModule
    {
        private static readonly ConditionalWeakTable<WorldSim, TableSeams> SeamTable = new ConditionalWeakTable<WorldSim, TableSeams>();

        /// <summary>
        /// The table and the plague for a new god board game: one seat per people on the board's start corners (PE-2),
        /// the player on <paramref name="humanSeat"/> (-1 = an AI-only table, for proofs and tests), the board's ports.
        /// </summary>
        public static void InitState(WorldState s, int humanSeat = PlayerIds.Human)
        {
            if (s?.Board == null) throw new ArgumentException("The god board needs a board.");
            var starts = s.Board.StartCorners ?? new int[0];
            if (starts.Length < s.PeopleCount)
                throw new ArgumentException("The god board needs a start corner for every people (" + s.PeopleCount + " peoples, " + starts.Length + " corners).");
            TableModule.InitState(s, s.PeopleCount, starts, humanSeat, PortsFromBoard(s.Board));
            PlagueModule.InitState(s);
            // G0-3: the war after the plague (its own stream forked last), with the first camps as home camps and garrisons.
            WarModule.InitState(s, s.PeopleCount);
            SyncBoard(s, null);
        }

        /// <summary>
        /// The table's ports from the board deal (BD-5, TR-1): each port may be used from every corner of its Meadow hex that
        /// is a legal settlement corner (<see cref="PlotLayout.IsSettlementCorner"/>, three hexes meet), ascending.
        /// CornerA and CornerB are the first two of them (-1 when missing). Empty for a board without ports.
        /// </summary>
        public static TablePort[] PortsFromBoard(Board b)
        {
            if (b?.Ports == null) return new TablePort[0];
            var topo = b.Topology;
            var list = new List<TablePort>(b.Ports.Count);
            foreach (var port in b.Ports)
            {
                var corners = new List<int>();
                foreach (int c in topo.HexCorners(port.Hex)) if (PlotLayout.IsSettlementCorner(topo, c)) corners.Add(c);
                corners.Sort();
                list.Add(new TablePort
                {
                    Hex = port.Hex,
                    Corners = corners.ToArray(),
                    CornerA = corners.Count > 0 ? corners[0] : -1,
                    CornerB = corners.Count > 1 ? corners[1] : -1,
                    Good = port.Good.HasValue ? (int)port.Good.Value : -1,
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// Adds the plague's handler, the table's engine and handlers, and wires the seams. Idempotent per sim. Does nothing
        /// for a state without a table (a Legacy game). Returns the seams (null when there is no table).
        /// </summary>
        public static TableSeams Register(WorldSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (SeamTable.TryGetValue(sim, out var existing)) return existing;
            if (sim.State.Table == null) return null;
            if (sim.State.Plague != null) PlagueModule.Register(sim);
            var seams = TableModule.Register(sim);
            Wire(seams);
            // G0-3: the war runs after the plague and the table; a sync system right after it carries burnings, ruins and
            // the island's fall back to the table.
            if (sim.State.War != null)
            {
                WarModule.Register(sim, Linked);
                sim.AddSystem(SyncOrder, new WarTableSync(seams));
            }
            // G4-2: the Historic age's system (idle until the age turns).
            Historic.HistoricModule.Register(sim);
            SeamTable.Add(sim, seams);
            return seams;
        }

        /// <summary>The table's seams of a sim built by <see cref="Register"/> (null for a Legacy game). Tests and the AI may replace hooks.</summary>
        public static TableSeams Seams(WorldSim sim) => sim != null && SeamTable.TryGetValue(sim, out var seams) ? seams : null;

        /// <summary>Wires the plague and the age's end into the table's seams.</summary>
        public static void Wire(TableSeams seams)
        {
            seams.HexBlocked = PlagueApi.HexBlocked;
            seams.OnEpidemic = (s, ev) => { PlagueRules.Epidemic(s, ev); CheckIslandFell(s, seams, ev); };
            seams.OnRoundEnded = (s, round, ev) => { PlagueRules.InfectRound(s, ev); CheckIslandFell(s, seams, ev); };
            seams.OnAgeEnded = AgeEnded;
            // G0-3: the war follows the table (garrisons, war state, CL-2 deliveries).
            seams.OnBoardChanged = SyncBoard;
            seams.OnProduced = (s, corner, good, n, ev) => WarRules.Deliver(s, corner, good, n);
            WireAi(seams);
        }

        /// <summary>Tick slot of the war-to-table sync: right after the war system (Combat + 10).</summary>
        public const int SyncOrder = SystemOrder.Combat + 11;

        /// <summary>
        /// PL-10 (G0-3): when the plague has fallen, the table closes for good with <see cref="GameLost"/> and the war stops.
        /// Called after every plague step the table drives, and each tick by the sync system as a safety net.
        /// </summary>
        public static void CheckIslandFell(WorldState s, TableSeams seams, EventBuffer ev)
        {
            if (s?.Plague == null || !s.Plague.IslandFell || s.Table == null || s.Table.IslandFell) return;
            if (seams?.Engine != null) seams.Engine.IslandFalls(s, ev);
            if (s.War != null) s.War.Stopped = true;
        }

        /// <summary>
        /// G0-3: the war module follows the table after every board change. Every table settlement or city has a war
        /// settlement (home camps on the start corners are flagged, WA-15; new ones get their garrison, WA-4; a city its second
        /// soldier); war settlements no longer on the table are removed; then the war state is recomputed from the table's
        /// points (WA-11). Idempotent.
        /// </summary>
        public static void SyncBoard(WorldState s, EventBuffer ev)
        {
            var t = s?.Table;
            var w = s?.War;
            if (t == null || w == null || w.Stopped) return;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                int owner = t.CornerOwner[c];
                if (owner < 0) continue;
                var st = WarApi.SettlementAt(s, c);
                bool city = t.CornerLevel[c] == 2;
                if (st == null) WarRules.AddSettlement(s, owner, c, city, c == t.Peoples[owner].StartCorner, ev);
                else if (city && !st.IsCity) WarRules.UpgradeCity(s, c, ev);
            }
            for (int i = w.Settlements.Count - 1; i >= 0; i--)
            {
                int c = w.Settlements[i].Corner;
                if (c < 0 || c >= t.CornerOwner.Length || t.CornerOwner[c] != w.Settlements[i].People) WarRules.RemoveSettlement(s, c);
            }
            var pts = new int[t.PeopleCount];
            for (int p = 0; p < t.PeopleCount; p++) pts[p] = t.Peoples[p].Points;
            WarRules.SetPoints(s, pts, ev);
        }

        /// <summary>
        /// PR-3 stand-in for the carriers: a settlement is linked to its people's lodge when the people's own roads join them
        /// (a road path that does not pass through another people's settlement). The lodge's own corner is always linked.
        /// </summary>
        public static bool Linked(WorldState s, int corner)
        {
            var t = s?.Table;
            if (t == null || corner < 0 || corner >= t.CornerOwner.Length) return true;
            int p = t.CornerOwner[corner];
            if (p < 0) return false;
            var lodge = WarApi.LodgeOf(s, p);
            if (lodge == null) return false;
            if (lodge.Corner == corner) return true;
            var topo = s.Board.Topology;
            var seen = new bool[t.CornerOwner.Length];
            var queue = new Queue<int>();
            queue.Enqueue(corner);
            seen[corner] = true;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                foreach (int e in topo.CornerHexEdges(c))
                {
                    if (t.RoadOwner[e] != p) continue;
                    int a = topo.HexEdgeCornerA(e), b = topo.HexEdgeCornerB(e);
                    int n = a == c ? b : a;
                    if (seen[n]) continue;
                    seen[n] = true;
                    if (n == lodge.Corner) return true;
                    if (t.CornerOwner[n] >= 0 && t.CornerOwner[n] != p) continue; // another people's settlement cuts the road
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        /// <summary>
        /// G0-3: carries the war back to the table each tick. A table settlement whose war settlement burned (a ruin stands on
        /// its corner) leaves the table with its points (TU-6, WA-14) and its corner stays closed; a closed corner whose ruin
        /// was cleared opens again; and the island's fall closes the table (PL-10). Nothing changes once the table has ended.
        /// </summary>
        private sealed class WarTableSync : ISimSystem
        {
            private readonly TableSeams _seams;
            public WarTableSync(TableSeams seams) { _seams = seams; }

            public void Tick(WorldState s, EventBuffer ev)
            {
                var t = s.Table;
                var w = s.War;
                if (t == null) return;
                CheckIslandFell(s, _seams, ev);
                // G2-3 grants (TableGrants) change the board and may end the age without the seams: catch up here.
                CatchUpAgeEnded(s, ev);
                if (w == null) return;
                if (!WarInSync(s)) SyncBoard(s, ev);
                if (t.Phase == TablePhase.Ended || _seams.Engine == null) return;
                for (int i = 0; i < w.Ruins.Count; i++)
                {
                    var r = w.Ruins[i];
                    if (r.Corner < t.CornerOwner.Length && t.CornerOwner[r.Corner] == r.People && WarApi.SettlementAt(s, r.Corner) == null)
                        _seams.Engine.LoseSettlement(s, r.Corner, "burned", true, ev);
                }
                for (int i = t.ClosedCorners.Count - 1; i >= 0; i--)
                {
                    int c = t.ClosedCorners[i];
                    if (!WarApi.IsRuin(s, c)) _seams.Engine.ReopenCorner(s, c, ev);
                }
            }
        }

        /// <summary>
        /// The table AI's hooks (AiSetup, AiTurn, AiAnswer, ChooseDiscard). G1-2 sets its brain here, so a loaded game gets
        /// it too. Until then the table's defaults play (<see cref="TableBasicAi"/>).
        /// </summary>
        public static void WireAi(TableSeams seams)
        {
        }

        /// <summary>
        /// G0-3: the age ended without <see cref="TableSeams.OnAgeEnded"/> (a free grant from the campaign, TableGrants, ends
        /// it through the seamless AfterChange): run the same age-end handling once. Detected by a won table (not fallen)
        /// whose plague has not ended. Called each tick by the sync system, so at most one tick late.
        /// </summary>
        public static void CatchUpAgeEnded(WorldState s, EventBuffer ev)
        {
            var t = s?.Table;
            if (t == null || t.Phase != TablePhase.Ended || t.IslandFell || t.Winner < 0) return;
            if (s.Plague == null || s.Plague.Ended) return;
            AgeEnded(s, t.Winner, ev);
        }

        /// <summary>
        /// G0-3: true when every table settlement and city has its war settlement (same people, same level), no war settlement
        /// is left without its table piece, and the war's points are the table's. Cheap; read each tick by the sync system.
        /// </summary>
        public static bool WarInSync(WorldState s)
        {
            var t = s?.Table;
            var w = s?.War;
            if (t == null || w == null || w.Stopped) return true;
            int owned = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                int owner = t.CornerOwner[c];
                if (owner < 0) continue;
                owned++;
                var st = WarApi.SettlementAt(s, c);
                if (st == null)
                {
                    if (WarApi.IsRuin(s, c)) { owned--; continue; } // burned this tick: the sync takes it off the table
                    return false;
                }
                if (st.People != owner || st.IsCity != (t.CornerLevel[c] == 2)) return false;
            }
            if (owned != w.Settlements.Count) return false;
            if (w.Points == null || w.Points.Length != t.PeopleCount) return false;
            for (int p = 0; p < t.PeopleCount; p++) if (w.Points[p] != t.Peoples[p].Points) return false;
            return true;
        }

        /// <summary>
        /// HI-8 and HI-1: the plague ends, and the island steps into Historic at once (G1-8 sim part, G4-2): the Historic
        /// chapter is marked ready and advanced to, the temples fall, castles and resources begin (<see cref="Historic.HistoricModule.Switch"/>).
        /// </summary>
        private static void AgeEnded(WorldState s, int winner, EventBuffer ev)
        {
            PlagueRules.End(s, ev);
            Historic.HistoricModule.Switch(s, winner, ev);
        }
    }
}
