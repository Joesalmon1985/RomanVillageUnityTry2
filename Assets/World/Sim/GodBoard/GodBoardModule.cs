using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using World.Sim.Plague;
using World.Sim.Table;

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
    /// <item>HI-8, HI-1: when the age ends the plague ends and the Historic chapter is marked ready (ChapterReady). The step
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
            SeamTable.Add(sim, seams);
            return seams;
        }

        /// <summary>The table's seams of a sim built by <see cref="Register"/> (null for a Legacy game). Tests and the AI may replace hooks.</summary>
        public static TableSeams Seams(WorldSim sim) => sim != null && SeamTable.TryGetValue(sim, out var seams) ? seams : null;

        /// <summary>Wires the plague and the age's end into the table's seams.</summary>
        public static void Wire(TableSeams seams)
        {
            seams.HexBlocked = PlagueApi.HexBlocked;
            seams.OnEpidemic = (s, ev) => PlagueRules.Epidemic(s, ev);
            seams.OnRoundEnded = (s, round, ev) => PlagueRules.InfectRound(s, ev);
            seams.OnAgeEnded = AgeEnded;
            WireAi(seams);
        }

        /// <summary>
        /// The table AI's hooks (AiSetup, AiTurn, AiAnswer, ChooseDiscard). G1-2 sets its brain here, so a loaded game gets
        /// it too. Until then the table's defaults play (<see cref="TableBasicAi"/>).
        /// </summary>
        public static void WireAi(TableSeams seams)
        {
        }

        /// <summary>HI-8 and HI-1: the plague ends, and the Historic chapter becomes ready for the step.</summary>
        private static void AgeEnded(WorldState s, int winner, EventBuffer ev)
        {
            PlagueRules.End(s, ev);
            Eras.ErasLogic.MarkReady(s, Eras.ChapterIds.Historic, ev);
        }
    }
}
