using System.Collections.Generic;

namespace World.Sim.Plague
{
    /// <summary>Read helpers for the plague (PRD_GODBOARD 7). Static, null-safe, never mutate. Node ids are 1-based.</summary>
    public static class PlagueApi
    {
        private static readonly int[][] NeighbourTable = BuildNeighbours();

        private static int[][] BuildNeighbours()
        {
            var lists = new List<int>[PlagueGraphData.NodeCount];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<int>();
            for (int t = 0; t < PlagueGraphData.TrailCount; t++)
            {
                lists[PlagueGraphData.TrailA[t] - 1].Add(PlagueGraphData.TrailB[t]);
                lists[PlagueGraphData.TrailB[t] - 1].Add(PlagueGraphData.TrailA[t]);
            }
            var table = new int[lists.Length][];
            for (int i = 0; i < lists.Length; i++) { lists[i].Sort(); table[i] = lists[i].ToArray(); }
            return table;
        }

        public static bool IsNode(int node) => node >= 1 && node <= PlagueGraphData.NodeCount;

        /// <summary>Nodes one trail away, sorted by id. Empty for an unknown node. Do not modify the array.</summary>
        public static int[] Neighbours(int node) => IsNode(node) ? NeighbourTable[node - 1] : new int[0];

        /// <summary>The node's element (its demon kind): 0 Earth, 1 Fire, 2 Water, 3 Air. -1 for an unknown node.</summary>
        public static int ElementOf(int node) => IsNode(node) ? PlagueGraphData.Element[node - 1] : -1;

        public static int DemonsAt(WorldState s, int node) => s?.Plague?.Demons != null && IsNode(node) ? s.Plague.Demons[node - 1] : 0;

        public static int DemonsOfKindAt(WorldState s, int node, int kind) =>
            s?.Plague?.KindCount != null && IsNode(node) && kind >= 0 && kind < 4 ? s.Plague.KindCount[(node - 1) * 4 + kind] : 0;

        /// <summary>Demons of one kind on the whole island.</summary>
        public static int DemonsOfKind(WorldState s, int kind)
        {
            var p = s?.Plague;
            if (p?.KindCount == null || kind < 0 || kind > 3) return 0;
            int n = 0;
            for (int i = 0; i < PlagueGraphData.NodeCount; i++) n += p.KindCount[i * 4 + kind];
            return n;
        }

        public static int TotalDemons(WorldState s)
        {
            int n = 0;
            if (s?.Plague?.Demons != null) foreach (int d in s.Plague.Demons) n += d;
            return n;
        }

        /// <summary>Soldiers and walkers cannot pass a node with 3 demons (WA-18).</summary>
        public static bool CanWalkThrough(WorldState s, int node) => DemonsAt(s, node) < 3;

        /// <summary>Node ids on a board hex (0, 1 or 2 of them).</summary>
        public static List<int> NodesOfHex(WorldState s, int hexIndex)
        {
            var list = new List<int>(2);
            var topo = s?.Board?.Topology;
            if (topo == null || hexIndex < 0) return list;
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
                if (topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[i], PlagueGraphData.HexR[i])) == hexIndex) list.Add(i + 1);
            return list;
        }

        /// <summary>True while any node on the hex holds a demon: the hex produces nothing (PL-9).</summary>
        public static bool HexBlocked(WorldState s, int hexIndex)
        {
            if (s?.Plague?.Demons == null) return false;
            foreach (int n in NodesOfHex(s, hexIndex)) if (s.Plague.Demons[n - 1] > 0) return true;
            return false;
        }

        /// <summary>The current draw count per round.</summary>
        public static int Rate(WorldState s) => PlagueRules.RateTrack[s?.Plague == null ? 0 : s.Plague.RateIndex];

        public static bool IsCured(WorldState s, int element) => s?.Plague?.Cured != null && element >= 0 && element < 4 && s.Plague.Cured[element];

        public static bool IsEradicated(WorldState s, int element) => s?.Plague?.Eradicated != null && element >= 0 && element < 4 && s.Plague.Eradicated[element];

        public static bool IsStrengthened(WorldState s, int element) => s?.Plague?.Strengthened != null && element >= 0 && element < 4 && s.Plague.Strengthened[element];

        /// <summary>True while the plague runs (initialised, not ended, island not fallen).</summary>
        public static bool Active(WorldState s) => s?.Plague != null && !s.Plague.Ended && !s.Plague.IslandFell;

        /// <summary>Ticks until the banish spell can be cast again (0 = ready).</summary>
        public static long BanishCooldownLeft(WorldState s) => s?.Plague == null ? 0 : System.Math.Max(0, s.Plague.BanishReadyTick - s.Tick);
    }

    /// <summary>Refusal codes of the plague and their plain sentences.</summary>
    public static class PlagueRefusals
    {
        public const string PlagueOver = "plague_over";
        public const string UnknownNode = "unknown_node";
        public const string NoDemons = "no_demons_here";
        public const string BanishCooling = "banish_cooling";
        public const string AlreadyCured = "already_cured";

        public static string Sentence(string code, long n = 0)
        {
            switch (code)
            {
                case PlagueOver: return "The plague is over.";
                case UnknownNode: return "There is no shrine there.";
                case NoDemons: return "There are no demons here.";
                case BanishCooling: return "The spell needs " + ((n + 9) / 10) + " more seconds to gather.";
                case AlreadyCured: return "That kind of demon is already cured.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code, long n = 0) => CommandResult.Reject(code, Sentence(code, n));
    }
}
