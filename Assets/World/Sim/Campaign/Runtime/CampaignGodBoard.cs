using System;
using System.Collections.Generic;
using World.Sim.Plague;
using World.Sim.Table;

namespace World.Sim.Campaign
{
    // G2-3: the crystal campaign rewired for the god board (PRD_GODBOARD section 8, CC-1 to CC-11). Additive: in a legacy
    // game (no table) nothing here has any effect, and every legacy rule plays exactly as before.

    /// <summary>Which rules the campaign follows.</summary>
    public static class CampaignRuleset
    {
        /// <summary>
        /// True in a god board game: the island has a turn table (<see cref="WorldState.Table"/>, G1-1). Legacy games and the
        /// legacy tests have none. G0-2 may point this at its ruleset field; every god board rule of the campaign asks here.
        /// </summary>
        public static bool GodBoard(WorldState s) => s?.Table != null;
    }

    /// <summary>
    /// CC-5: a crystal spire rose at the rewarded settlement (Kind "city" or "settlement", the free piece of CC-4). It
    /// survives burning. Views come later (G2-4).
    /// </summary>
    public sealed class SpireRaised : SimEvent { public Element Element; public int People; public int Corner; public string Kind; }

    /// <summary>
    /// CC-7: trouble at home turned the party back. Why: "settlement_burned" (Corner = the burned settlement) or
    /// "outbreak" (Node = the plague node on a home camp hex).
    /// </summary>
    public sealed class HomeTrouble : SimEvent { public string Why; public int Corner = -1; public int Node = -1; }

    /// <summary>A crystal spire (CC-5), saved with the run.</summary>
    public sealed class SpireState
    {
        public Element Element;
        public int People;
        public int Corner;
        public long Tick;
    }

    public sealed partial class CampaignTuning
    {
        /// <summary>CC-7: the expedition clock gains this much when trouble at home turns the party back.</summary>
        public int HomeTroubleCharge = 900;
    }

    public sealed partial class CampaignState
    {
        /// <summary>The crystal spires raised so far, in order (CC-5).</summary>
        public List<SpireState> Spires = new List<SpireState>();
        /// <summary>Tick trouble at home last turned the party back (-1 = never); one turn-back per tick.</summary>
        public long LastHomeTroubleTick = -1;
    }

    /// <summary>
    /// Seams for the god board's other modules (the wiring is G0-2's): call these when the war burns a settlement or the
    /// plague breaks out. The campaign slot also scans its own tick's <c>War.SettlementBurned</c>, <c>Plague.Outbreak</c>
    /// and spread <c>Plague.Infected</c> events, so a module that raises those events before the campaign slot (1250) needs
    /// no call; calling as well is harmless (one turn-back per tick).
    /// </summary>
    public static class CampaignHooks
    {
        /// <summary>CC-7: a settlement burned. Only the player's people's settlements are trouble at home.</summary>
        public static bool OnSettlementBurned(WorldState s, int people, int corner, EventBuffer ev)
        {
            if (people != PlayerIds.Human) return false;
            return CampaignRuntime.HomeTroubleNow(s, "settlement_burned", corner, -1,
                "Smoke from home: one of your settlements is burning. Mira turned the party back to ", ev);
        }

        /// <summary>CC-7: an outbreak reached a plague node (the outbreaking node, or a node a spread demon landed on).</summary>
        public static bool OnOutbreak(WorldState s, int node, EventBuffer ev)
        {
            if (!IsHomeNode(s, node)) return false;
            return CampaignRuntime.HomeTroubleNow(s, "outbreak", -1, node,
                "The plague broke out by the home camp. Mira turned the party back to ", ev);
        }

        /// <summary>Plague nodes on the home camp's hexes (the hexes round the player's start corner).</summary>
        public static List<int> HomeNodes(WorldState s)
        {
            var list = new List<int>();
            var b = s?.Board;
            if (b?.StartCorners == null || b.StartCorners.Length <= PlayerIds.Human) return list;
            foreach (int h in b.Topology.VertexHexes(b.StartCorners[PlayerIds.Human]))
                foreach (int n in PlagueApi.NodesOfHex(s, h))
                    if (!list.Contains(n)) list.Add(n);
            list.Sort();
            return list;
        }

        public static bool IsHomeNode(WorldState s, int node) => node > 0 && HomeNodes(s).Contains(node);

        /// <summary>
        /// CC-3, CC-4, CC-5 for one crystal: the people's crystal cures (player) or strengthens (Ash Clan) its kind, and the
        /// people gets its free city or settlement with a spire there. Called by the campaign on Restored and on the first
        /// AtRivalAltar; public for tests and dev tools.
        /// </summary>
        public static TableGrant CrystalGained(WorldState s, Element e, int people, bool restored, EventBuffer ev)
        {
            if (!CampaignRuleset.GodBoard(s)) return TableGrant.None;
            if (PlagueApi.Active(s))
            {
                if (restored) PlagueRules.Cure(s, (int)e, ev);
                else PlagueRules.Strengthen(s, (int)e, ev);
            }
            if (!s.IsPeople(people) || people >= s.Table.PeopleCount) return TableGrant.None;
            var g = TableGrants.GrantCityOrSettlement(s, people, ev);
            if (!g.Granted) return g;
            s.Campaign?.Spires.Add(new SpireState { Element = e, People = people, Corner = g.Corner, Tick = s.Tick });
            ev?.Add(new SpireRaised { Element = e, People = people, Corner = g.Corner, Kind = g.Kind });
            return g;
        }
    }

    internal static partial class CampaignRuntime
    {
        /// <summary>CC-7: the party walks home and the expedition clock gains HomeTroubleCharge. Once per tick.</summary>
        internal static bool HomeTroubleNow(WorldState s, string why, int corner, int node, string journalLead, EventBuffer ev)
        {
            var c = Run(s);
            if (c == null || c.Phase != CampaignPhase.Running || !CampaignRuleset.GodBoard(s)) return false;
            if (c.LastHomeTroubleTick == s.Tick) return false;
            c.LastHomeTroubleTick = s.Tick;
            ev?.Add(new HomeTrouble { Why = why, Corner = corner, Node = node });
            Charge(s, c, c.Definition.Tuning.HomeTroubleCharge, RivalCharges.HomeTrouble, ev);
            // A duel in progress is not interrupted (the world clock is paused during it).
            if (c.Attempt != null || c.Party.Mode == PartyMode.InEncounter) return true;
            TurnHome(s, c, "home_trouble", journalLead, ev);
            return true;
        }

        /// <summary>
        /// The god board's trouble at home (CC-7), read from this tick's events raised before the campaign slot: the war's
        /// burnings and the plague's outbreaks. Replaces the overrun link of legacy games.
        /// </summary>
        private static void ScanHomeTrouble(WorldState s, CampaignState c, EventBuffer ev)
        {
            var items = ev?.Items;
            if (items == null || items.Count == 0) return;
            int first = items.Count;
            while (first > 0 && items[first - 1].Tick == s.Tick) first--;
            int end = items.Count;
            for (int i = first; i < end && c.Phase == CampaignPhase.Running; i++)
            {
                switch (items[i])
                {
                    case War.SettlementBurned sb:
                        CampaignHooks.OnSettlementBurned(s, sb.People, sb.Corner, ev);
                        break;
                    case Plague.Outbreak ob:
                        CampaignHooks.OnOutbreak(s, ob.Node, ev);
                        break;
                    case Plague.Infected inf when inf.Reason == "spread":
                        CampaignHooks.OnOutbreak(s, inf.Node, ev);
                        break;
                }
            }
        }

        /// <summary>The god board's side of a custody change (CC-3, CC-4, CC-5): Restored for the player, first altar for the Ash Clan.</summary>
        private static void GodBoardCustody(WorldState s, Element e, Custody to, bool firstAltar, EventBuffer ev)
        {
            if (to == Custody.Restored) CampaignHooks.CrystalGained(s, e, PlayerIds.Human, true, ev);
            else if (to == Custody.AtRivalAltar && firstAltar) CampaignHooks.CrystalGained(s, e, PlayerIds.AshClan, false, ev);
        }
    }
}
