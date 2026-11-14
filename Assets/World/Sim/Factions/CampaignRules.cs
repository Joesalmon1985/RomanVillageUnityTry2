using World.Sim.Economy;
using World.Sim.Eras;

namespace World.Sim.Factions
{
    /// <summary>
    /// The campaign overrun rule (PRD_AGES 4.3; crystals PRD N-2, N-3). Only reached when AgesApi.IsCampaign: in skirmish
    /// the overrun stays fatal (StrengthSystem). H1-0 FIRST VERSION: target choice, the home-loot branch, the halt, the
    /// setback grace and CampOverrun are real; the capture branch waits on EconomySeams.TransferSettlement (H1-0 STUB in
    /// the economy, implemented by H1-2): until then a capture falls back to looting the home camp. H1-1 owns this file.
    /// </summary>
    internal static class CampaignRules
    {
        public static void Overrun(WorldState s, int attacker, int defender, EventBuffer ev)
        {
            var f = s.Factions;
            int home = HomeSettlement(s, defender);
            int target = CaptureTarget(s, attacker, defender, home);
            bool captured = target >= 0 && EconomySeams.TransferSettlement(s, target, attacker, ev);
            if (captured)
            {
                ev.Add(new CampOverrun { Attacker = attacker, Defender = defender, SettlementId = target, Kind = CampOverrunKind.CampLost });
            }
            else if (home >= 0)
            {
                EconomySeams.LootSettlement(s, home);
                f.Halts.RemoveAll(h => h.SettlementId == home);
                f.Halts.Add(new SettlementHalt { SettlementId = home, UntilTick = s.Tick + f.Tuning.LootHaltTicks });
                f.Halts.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
                ev.Add(new CampOverrun { Attacker = attacker, Defender = defender, SettlementId = home, Kind = CampOverrunKind.Looted });
            }
            f.Factions[defender].SetbackUntilTick = s.Tick + f.Tuning.SetbackGraceTicks;
        }

        /// <summary>The defender's home settlement (on its start corner), or -1.</summary>
        public static int HomeSettlement(WorldState s, int faction)
        {
            var starts = s.Board?.StartCorners;
            if (starts == null || faction < 0 || faction >= starts.Length) return -1;
            var st = EconomyLogic.SettlementAt(s, starts[faction]);
            return st != null && st.Faction == faction ? st.Id : -1;
        }

        /// <summary>The defender's active non-home settlement nearest the attacker's home corner (hex edges, ties lower id), or -1.</summary>
        public static int CaptureTarget(WorldState s, int attacker, int defender, int home)
        {
            var starts = s.Board?.StartCorners;
            if (s.Economy == null || starts == null || attacker < 0 || attacker >= starts.Length) return -1;
            var t = s.Board.Topology;
            int best = -1, bestD = int.MaxValue;
            foreach (var st in s.Economy.Settlements)
            {
                if (st.Faction != defender || st.State != SettlementState.Active || st.Id == home) continue;
                int d = t.CornerDistance(starts[attacker], st.Corner);
                if (d < 0) continue;
                if (d < bestD) { bestD = d; best = st.Id; }
            }
            return best;
        }

        /// <summary>EconomyHooks.IsSettlementHalted: a looted home camp stops until its halt ends.</summary>
        public static bool IsHalted(WorldState s, int settlementId)
        {
            var halts = s?.Factions?.Halts;
            if (halts == null || halts.Count == 0) return false;
            foreach (var h in halts) if (h.SettlementId == settlementId && h.UntilTick > s.Tick) return true;
            return false;
        }
    }
}
