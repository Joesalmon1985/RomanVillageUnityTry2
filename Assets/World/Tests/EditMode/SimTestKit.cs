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
                    if (st.Reserved[w] > st.Stock[w] || st.Stock[w] < 0) return "settlement " + st.Id + " stock " + (Ware)w;
            return null;
        }
    }
}
