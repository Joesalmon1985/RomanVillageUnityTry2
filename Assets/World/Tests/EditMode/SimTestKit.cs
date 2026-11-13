using System.Collections.Generic;
using NUnit.Framework;
using World.Sim.Economy;

namespace World.Sim.Tests
{
    /// <summary>Small helpers for the simulation tests.</summary>
    public static class SimTestKit
    {
        public const ulong DefaultSeed = 20261113;

        /// <summary>Submit (must be accepted), step one tick, return that tick's events.</summary>
        public static List<SimEvent> Do(WorldSim sim, Command c, int playerId = PlayerIds.Human)
        {
            c.PlayerId = playerId;
            var r = sim.Submit(c);
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            return new List<SimEvent>(sim.DrainEvents());
        }

        public static List<SimEvent> Step(WorldSim sim, int ticks)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            return all;
        }

        /// <summary>Steps until the condition holds (true) or max ticks pass (false).</summary>
        public static bool StepUntil(WorldSim sim, System.Func<bool> done, int max)
        {
            for (int i = 0; i < max; i++)
            {
                if (done()) return true;
                sim.Step();
                sim.DrainEvents();
            }
            return done();
        }

        /// <summary>Adds goods to a settlement's storehouse (and to the ledger's starting stock, so conservation holds).</summary>
        public static void Give(WorldState s, int settlementId, Ware w, int n)
        {
            s.Economy.Settlements[settlementId].Stock[(int)w] += n;
            s.Economy.Ledger.Starting[(int)w] += n;
        }

        /// <summary>
        /// Builds two player roads from home to a free settlement corner two hex edges away and founds a camp there.
        /// Returns the new settlement id; path holds the three corners home..camp.
        /// </summary>
        public static int SecondCamp(WorldSim sim, out int[] path)
        {
            var s = sim.State;
            var t = s.Board.Topology;
            int home = s.Board.StartCorners[0];
            int target = -1;
            foreach (int c in t.Corners)
            {
                if (t.CornerDistance(home, c) != 2 || EconomyApi.CheckSettlement(s, 0, c).Reason != "not_connected") continue;
                bool free = true;
                foreach (int n in t.CornerNeighbours(c)) if (EconomyApi.SettlementAt(s, n) != null) free = false;
                if (free) { target = c; break; }
            }
            path = t.FindCornerPath(home, target);
            s.Demons.Tuning.GraceTicks = int.MaxValue; // no random demons in these set-ups
            Give(s, 0, Ware.Wood, 3);
            Give(s, 0, Ware.Clay, 3);
            Do(sim, new PlaceRoad { HexEdge = t.HexEdgeBetween(path[0], path[1]) });
            int e1 = t.HexEdgeBetween(path[0], path[1]), e2 = t.HexEdgeBetween(path[1], path[2]);
            Assert.IsTrue(StepUntil(sim, () => EconomyApi.RoadOn(s, e1).State == RoadState.Built, 1000));
            Do(sim, new PlaceRoad { HexEdge = e2 });
            Assert.IsTrue(StepUntil(sim, () => EconomyApi.RoadOn(s, e2).State == RoadState.Built, 1000));
            int id = s.Economy.Settlements.Count;
            Do(sim, new FoundSettlement { Corner = target });
            Assert.IsTrue(StepUntil(sim, () => s.Economy.Settlements[id].State == SettlementState.Active, 2000));
            return id;
        }

        /// <summary>Forces the next roll: the given dice come up at the next tick.</summary>
        public static void RigNextRoll(WorldState s, int d1, int d2)
        {
            var d = s.Economy.Dice;
            var deck = new List<int>(d.Deck ?? new int[0]) { d1 * 10 + d2 };
            d.Deck = deck.ToArray();
            d.NextRollTick = s.Tick + 1;
        }

        /// <summary>Wares in the world per ware, counted independently of the sim (REFACTOR_SPEC 4.4).</summary>
        public static int[] WaresInWorld(WorldState s)
        {
            var w = new int[Wares.Count];
            var e = s.Economy;
            foreach (var st in e.Settlements)
                for (int i = 0; i < Wares.Count; i++) w[i] += st.Stock[i] + st.Delivered[i] + st.UpgradeDelivered[i];
            foreach (var b in e.Buildings)
                for (int i = 0; i < Wares.Count; i++) w[i] += b.Stock[i] + b.Delivered[i];
            foreach (var r in e.Roads)
                for (int i = 0; i < Wares.Count; i++) w[i] += r.Delivered[i];
            foreach (var c in e.Carriers)
                if (c.HasLoad) w[(int)c.Load] += c.LoadCount;
            return w;
        }

        /// <summary>Null when the invariants of 4.4 hold, else a description of the first drift.</summary>
        public static string CheckInvariants(WorldState s)
        {
            var e = s.Economy;
            var l = e.Ledger;
            var world = WaresInWorld(s);
            for (int w = 0; w < Wares.Count; w++)
            {
                int lhs = l.Starting[w] + l.Collected[w] + l.Produced[w];
                int rhs = l.Consumed[w] + world[w];
                if (lhs != rhs) return "ware " + (Ware)w + ": " + lhs + " made vs " + rhs + " used or held";
            }
            var young = new int[Wares.Count];
            var mature = new int[Wares.Count];
            foreach (var hs in e.Hexes)
            {
                foreach (var c in hs.Young) young[(int)c.Ware] += c.Count;
                var rw = TerrainInfo.RawWare(s.Board.HexTerrain[hs.Hex]);
                if (rw != null) mature[(int)rw.Value] += hs.Mature;
            }
            for (int w = 0; w < Wares.Count; w++)
            {
                if (l.StockAdded[w] != l.StockLost[w] + l.StockTaken[w] + young[w] + mature[w])
                    return "hex stock " + (Ware)w + " drifts";
            }
            foreach (var st in e.Settlements)
                for (int w = 0; w < Wares.Count; w++)
                    if (st.Reserved[w] > st.Stock[w] || st.Stock[w] < 0 || st.Reserved[w] < 0) return "settlement " + st.Id + " stock " + (Ware)w;
            foreach (var b in e.Buildings)
                for (int w = 0; w < Wares.Count; w++)
                    if (b.Reserved[w] > b.Stock[w] || b.Reserved[w] < 0 || b.Delivered[w] < 0) return "building " + b.Id + " stock " + (Ware)w;

            // Incoming equals the open jobs heading there; reservations equal the jobs not yet picked up.
            var incS = new int[e.Settlements.Count, Wares.Count];
            var incB = new int[e.Buildings.Count, Wares.Count];
            var incR = new int[e.Roads.Count, Wares.Count];
            var resS = new int[e.Settlements.Count, Wares.Count];
            var resB = new int[e.Buildings.Count, Wares.Count];
            foreach (var j in e.Jobs)
            {
                int w = (int)j.Ware;
                if (j.ToKind == PlaceKind.Building) incB[j.ToId, w] += j.Count;
                else if (j.ToKind == PlaceKind.RoadSite) incR[j.ToId, w] += j.Count;
                else incS[j.ToId, w] += j.Count;
                if (j.State != JobState.Carrying)
                {
                    if (j.FromKind == PlaceKind.Building) resB[j.FromId, w] += j.Count;
                    else resS[j.FromId, w] += j.Count;
                }
            }
            for (int w = 0; w < Wares.Count; w++)
            {
                for (int i = 0; i < e.Settlements.Count; i++)
                {
                    if (e.Settlements[i].Incoming[w] != incS[i, w]) return "settlement " + i + " incoming " + (Ware)w;
                    if (e.Settlements[i].Reserved[w] != resS[i, w]) return "settlement " + i + " reserved " + (Ware)w;
                }
                for (int i = 0; i < e.Buildings.Count; i++)
                {
                    if (e.Buildings[i].Incoming[w] != incB[i, w]) return "building " + i + " incoming " + (Ware)w;
                    if (e.Buildings[i].Reserved[w] != resB[i, w]) return "building " + i + " reserved " + (Ware)w;
                }
                for (int i = 0; i < e.Roads.Count; i++)
                    if (e.Roads[i].Incoming[w] != incR[i, w]) return "road " + i + " incoming " + (Ware)w;
            }
            foreach (var c in e.Carriers)
            {
                if (c.HasLoad && (c.JobId < 0 || !e.Jobs.Exists(j => j.Id == c.JobId))) return "carrier " + c.Id + " holds a load without a job";
                if (c.State == CarrierState.Idle && c.HasLoad) return "idle carrier " + c.Id + " holds a load";
            }
            return null;
        }
    }
}
