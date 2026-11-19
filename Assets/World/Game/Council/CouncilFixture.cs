using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// Test and developer staging. Everything here edits the simulation state directly and exists only for tests, the
    /// F1 "Council" tool and the AutoPlay scripts; the normal game never calls it (the game layer proper only sends
    /// commands). <see cref="MakeEligible"/> gives a village enough finished buildings that four residents can stand;
    /// <see cref="StageExample"/> stages an election in progress with the brief's example trays (internal values
    /// 1, 3, 4, 5 give 4+9, 5+8 and 6+7).
    /// </summary>
    public static class CouncilFixture
    {
        static readonly BuildingType[] Trades =
        {
            BuildingType.WoodcutterHut, BuildingType.Farm, BuildingType.ShepherdHut, BuildingType.Quarry,
            BuildingType.Sawmill, BuildingType.Kiln, BuildingType.Bakery, BuildingType.Smokehouse, BuildingType.Stonecutter,
        };

        /// <summary>Adds finished buildings to a settlement until an election is possible. Returns how many people could stand.</summary>
        public static int MakeEligible(WorldState s, int settlement, int wanted = 6)
        {
            var content = CouncilContent.BuiltIn;
            var st = CouncilRoster.ActiveSettlement(s, settlement);
            if (st == null) return 0;
            int nextId = 1;
            foreach (var b in s.Economy.Buildings) nextId = Mathf.Max(nextId, b.Id + 1);
            var used = new HashSet<int>();
            foreach (var b in s.Economy.Buildings) used.Add(b.Plot);
            int reserved = s.Board.ReservedPlots != null && s.Board.ReservedPlots.Length > 0 ? s.Board.ReservedPlots[0] : -1;
            int ti = 0;
            for (int k = 0; k < PlotLayout.PlotsPerCorner && ti < Trades.Length; k++)
            {
                if (CouncilRoster.WouldStand(CouncilRoster.Build(s, settlement, content)).Count >= wanted) break;
                int plot = PlotLayout.PlotId(st.Corner, k / PlotLayout.SlotsPerSector, k % PlotLayout.SlotsPerSector);
                if (used.Contains(plot) || plot == reserved) continue;
                var b = new Building { Id = nextId++, Type = Trades[ti++], Faction = st.Faction, Settlement = settlement, Plot = plot, Hex = -1, State = BuildingState.Active };
                s.Economy.Buildings.Add(b);
            }
            return CouncilRoster.WouldStand(CouncilRoster.Build(s, settlement, content)).Count;
        }

        /// <summary>Offers and starts an election by hand (as the commands would) with the player advising seat <paramref name="playerSeat"/>.</summary>
        public static SettlementCouncil StageElection(WorldState s, int settlement, int playerSeat = -1, bool help = true)
        {
            var content = CouncilContent.BuiltIn;
            if (s.Council == null) s.Council = new CouncilState();
            var existing = CouncilApi.Get(s, settlement);
            if (existing != null) return existing;
            MakeEligible(s, settlement);
            var sc = CouncilSetup.Offer(s, settlement, content);
            s.Council.Settlements.Add(sc);
            s.Council.Settlements.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            CouncilSetup.Start(sc, help, content);
            if (help && playerSeat >= 0) { sc.AskerSeat = playerSeat; sc.Election.PlayerSeat = playerSeat; }
            return sc;
        }

        /// <summary>The brief's example: the player's turn with three trays on the table (4+9, 5+8, 6+7), all advances legal.</summary>
        public static SettlementCouncil StageExample(WorldState s, int settlement, int playerSeat = 0)
        {
            var sc = StageElection(s, settlement, playerSeat, true);
            var e = sc.Election;
            e.PlayerSeat = playerSeat; e.Active = playerSeat; e.Starter = playerSeat;
            StageValues(e, 1, 3, 4, 5);
            return sc;
        }

        /// <summary>Puts a push with the given internal values on the table as if it had just been made (the engine never shows them).</summary>
        public static void StageValues(ElectionState e, int v0, int v1, int v2, int v3)
        {
            var trays = ElectionEngine.BuildTrays(e, v0, v1, v2, v3);
            e.PushId++;
            e.ActionSeq++;
            e.Pending = new List<Tray>(trays);
            e.Phase = trays.Count > 0 ? ElectionPhase.Choosing : ElectionPhase.Ready;
        }
    }
}
