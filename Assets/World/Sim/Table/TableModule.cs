using System;

namespace World.Sim.Table
{
    /// <summary>
    /// Entry point of the Table module (G1-1). Not yet part of <see cref="WorldSimFactory"/> or the save; the board deal
    /// and ruleset packages wire it in.
    /// </summary>
    public static class TableModule
    {
        /// <summary>Tick slot: the turn engine runs with the old dice slot (it replaces the dice clock in prehistoric).</summary>
        public const int Order = SystemOrder.Dice + 10;

        /// <summary>
        /// Creates the table for N peoples. Each people already has its first camp (a settlement) on its start corner
        /// (PE-2). humanSeat = -1 makes an AI-only table that is open from the start (no altar to sacrifice at).
        /// Ports are optional (BD-5); without them only the bank's 4:1 is offered.
        /// </summary>
        public static void InitState(WorldState s, int peopleCount, int[] startCorners, int humanSeat = 0, TablePort[] ports = null)
        {
            if (s?.Board == null) throw new ArgumentException("The table needs a board.");
            if (peopleCount < 1) throw new ArgumentOutOfRangeException(nameof(peopleCount));
            if (startCorners == null || startCorners.Length < peopleCount) throw new ArgumentException("One start corner per people.");
            if (humanSeat < -1 || humanSeat >= peopleCount) throw new ArgumentOutOfRangeException(nameof(humanSeat));
            var topo = s.Board.Topology;
            var t = new TableState
            {
                PeopleCount = peopleCount,
                HumanSeat = humanSeat,
                CornerOwner = Filled(topo.CornerCount, -1),
                CornerLevel = new int[topo.CornerCount],
                RoadOwner = Filled(topo.HexEdgeCount, -1),
                RollRng = (s.Rng ?? new Rng(s.Seed)).Fork("table.roll"),
                DiscardOwed = new int[peopleCount],
            };
            for (int p = 0; p < peopleCount; p++)
            {
                int c = startCorners[p];
                if (!topo.IsCorner(c) || t.CornerOwner[c] >= 0) throw new ArgumentException("Bad start corner " + c);
                t.Peoples.Add(new TablePeople { Id = p, StartCorner = c });
                t.CornerOwner[c] = p;
                t.CornerLevel[c] = 1;
            }
            if (ports != null)
                foreach (var port in ports) t.Ports.Add(new TablePort { CornerA = port.CornerA, CornerB = port.CornerB, Good = port.Good });
            foreach (var pe in t.Peoples) pe.Points = t.Tuning.SettlementPoints;
            s.Table = t;
        }

        private static int[] Filled(int n, int v)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        /// <summary>Adds the engine and the command handlers. Returns the seams for other modules and the AI to wire.</summary>
        public static TableSeams Register(WorldSim sim, TableSeams seams = null)
        {
            var engine = new TableEngine(seams);
            sim.AddSystem(Order, engine);
            sim.RegisterHandler(new Handler<Sacrifice>(engine));
            sim.RegisterHandler(new Handler<LeaveTable>(engine));
            sim.RegisterHandler(new Handler<PlaceTableSettlement>(engine));
            sim.RegisterHandler(new Handler<PlaceTableRoad>(engine));
            sim.RegisterHandler(new Handler<UpgradeTableCity>(engine));
            sim.RegisterHandler(new Handler<BankTrade>(engine));
            sim.RegisterHandler(new Handler<OfferTrade>(engine));
            sim.RegisterHandler(new Handler<AnswerOffer>(engine));
            sim.RegisterHandler(new Handler<EndTurn>(engine));
            sim.RegisterHandler(new Handler<DiscardCards>(engine));
            // An AI-only table needs no sacrifice: it opens on the first tick.
            if (sim.State.Table != null && sim.State.Table.HumanSeat < 0 && sim.State.Table.Phase == TablePhase.NotOpened)
                sim.AddSystem(Order - 1, new AutoOpen(engine));
            return engine.Seams;
        }

        private sealed class Handler<T> : CommandHandler<T> where T : Command
        {
            private readonly TableEngine _e;
            public Handler(TableEngine e) { _e = e; }
            protected override CommandResult Validate(WorldState s, T c) => _e.Check(s, c);
            protected override CommandResult Apply(WorldState s, T c, EventBuffer ev) => _e.Apply(s, c, ev);
        }

        private sealed class AutoOpen : ISimSystem
        {
            private readonly TableEngine _e;
            public AutoOpen(TableEngine e) { _e = e; }

            public void Tick(WorldState s, EventBuffer ev)
            {
                if (s.Table != null && s.Table.HumanSeat < 0 && s.Table.Phase == TablePhase.NotOpened) _e.Open(s, ev);
            }
        }
    }
}
