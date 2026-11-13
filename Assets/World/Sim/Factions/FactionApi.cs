using System;
using System.Collections.Generic;
using World.Sim.Demons;
using World.Sim.Economy;

namespace World.Sim.Factions
{
    public struct StrengthView { public int Faction, Settlements, Cities, FedWarriors, HungryWarriors, Total; }

    public struct WarningView
    {
        public bool Active;
        public int Attacker, Defender;
        public long TicksLeft;
        public string Sentence;
    }

    public struct OutcomeView
    {
        public bool GameOver;
        public GameOutcome Outcome;
        public string Reason;
        public long EndedTick;
        public string Sentence;
    }

    /// <summary>Read helpers (REFACTOR_SPEC 5.4, A.3). Static, never mutate, null-safe.</summary>
    public static class FactionApi
    {
        /// <summary>Press areas (A.3): building plot radius, corner radius, road strip half width, road midpoint radius.</summary>
        public const double PlotPressRadius = 5, CornerPressRadius = 6, RoadStripHalfWidth = 3, RoadPressRadius = 30;

        /// <summary>Open proposals of a faction, by rank (top first).</summary>
        public static IReadOnlyList<Proposal> Proposals(WorldState s, int faction)
        {
            var list = new List<Proposal>();
            if (s?.Factions == null) return list;
            foreach (var p in s.Factions.Proposals) if (p.Faction == faction) list.Add(p);
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            return list;
        }

        public static Proposal Top(WorldState s, int faction) => s?.Factions == null ? null : FactionLogic.Top(s.Factions, faction);

        public static Proposal GetProposal(WorldState s, int proposalId) => FactionLogic.Find(s, proposalId);

        /// <summary>"Woodcutter's hut", "Road to the pasture corner", "Camp by the forest and hills", "Raise the home camp to a city", "Banish the demons from the home camp".</summary>
        public static string ProposalTitle(WorldState s, int proposalId)
        {
            var p = FactionLogic.Find(s, proposalId);
            if (p == null) return "";
            var t = s.Board.Topology;
            switch (p.Kind)
            {
                case ProposalKind.Building:
                {
                    var def = BuildingDefs.Get((BuildingType)p.BuildingType);
                    return def != null ? def.Name : "Building";
                }
                case ProposalKind.Road:
                {
                    if (p.HexEdge < 0 || p.HexEdge >= t.HexEdgeCount) return "Road";
                    int a = t.HexEdgeCornerA(p.HexEdge), b = t.HexEdgeCornerB(p.HexEdge);
                    // The far end: the corner not already reached by the people's network.
                    int far = EconomyLogic.OwnRoadEndsAt(s, p.Faction, a) || OwnSettlement(s, p.Faction, a) ? b : a;
                    return "Road to the " + EconomyApi.CornerTerrains(s, far) + " corner";
                }
                case ProposalKind.Settlement: return "Camp by the " + EconomyApi.CornerTerrains(s, p.Corner);
                case ProposalKind.City:
                {
                    var st = EconomyLogic.SettlementAt(s, p.Corner);
                    return "Raise " + (st != null ? EconomyApi.SettlementName(s, st.Id) : "the camp") + " to a city";
                }
                case ProposalKind.Banish: return "Banish the demons from " + DemonApi.SiteName(s, p.DemonSiteKind, p.DemonSiteId);
            }
            return "";
        }

        /// <summary>The point to walk to and press (metres), and the press radius around it (A.3). False when the proposal is gone.</summary>
        public static bool ProposalTarget(WorldState s, int proposalId, out double x, out double z, out double pressRadius)
        {
            x = z = pressRadius = 0;
            var p = FactionLogic.Find(s, proposalId);
            if (p == null) return false;
            var t = s.Board.Topology;
            switch (p.Kind)
            {
                case ProposalKind.Building:
                    x = PlotLayout.PlotX(t, p.Plot);
                    z = PlotLayout.PlotZ(t, p.Plot);
                    pressRadius = PlotPressRadius;
                    return true;
                case ProposalKind.Road:
                    return EdgeTarget(t, p.HexEdge, out x, out z, out pressRadius);
                case ProposalKind.Settlement:
                case ProposalKind.City:
                    return CornerTarget(t, p.Corner, out x, out z, out pressRadius);
                case ProposalKind.Banish:
                    return p.DemonSiteKind == DemonSiteKind.Corner
                        ? CornerTarget(t, p.DemonSiteId, out x, out z, out pressRadius)
                        : EdgeTarget(t, p.DemonSiteId, out x, out z, out pressRadius);
            }
            return false;
        }

        /// <summary>The best-ranked open proposal of the faction whose press area holds the point, or -1. Roads test their 6 m strip.</summary>
        public static int ProposalAt(WorldState s, int faction, double x, double z)
        {
            foreach (var p in Proposals(s, faction))
            {
                if (!ProposalTarget(s, p.Id, out double px, out double pz, out double r)) continue;
                bool edge = p.Kind == ProposalKind.Road || (p.Kind == ProposalKind.Banish && p.DemonSiteKind == DemonSiteKind.HexEdge);
                if (edge)
                {
                    int he = p.Kind == ProposalKind.Road ? p.HexEdge : p.DemonSiteId;
                    var t = s.Board.Topology;
                    int a = t.HexEdgeCornerA(he), b = t.HexEdgeCornerB(he);
                    if (SegmentDistance(x, z, t.VertexX(a), t.VertexZ(a), t.VertexX(b), t.VertexZ(b)) <= RoadStripHalfWidth) return p.Id;
                }
                else
                {
                    double dx = x - px, dz = z - pz;
                    if (dx * dx + dz * dz <= r * r) return p.Id;
                }
            }
            return -1;
        }

        /// <summary>How long the rival waits now before it casts its top proposal (ticks); it may shrink over the game.</summary>
        public static int RivalCastDelay(WorldState s) => s?.Factions == null ? 0 : FactionLogic.RivalDelay(s);

        public static double CastRangeMetres(WorldState s) => s?.Factions?.Tuning != null ? s.Factions.Tuning.CastRangeMetres : 15;

        public static StrengthView Strength(WorldState s, int faction)
        {
            var v = new StrengthView { Faction = faction };
            if (s?.Factions == null || faction < 0 || faction >= s.Factions.Factions.Count) return v;
            var fa = s.Factions.Factions[faction];
            v.Settlements = fa.Settlements;
            v.Cities = fa.Cities;
            v.FedWarriors = fa.FedWarriors;
            v.HungryWarriors = fa.HungryWarriors;
            v.Total = fa.Strength;
            return v;
        }

        public static WarningView Warning(WorldState s)
        {
            var w = s?.Factions?.Warning;
            if (w == null || !w.Active) return new WarningView { Attacker = -1, Defender = -1 };
            long left = Math.Max(0, w.EndTick - s.Tick);
            long secs = (left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond;
            string sentence = w.Attacker == PlayerIds.Human
                ? "Your people are gathering against the Ash Clan: they fall in " + secs + " s unless they grow stronger."
                : "The Ash Clan is gathering against your people: " + secs + " s to grow stronger.";
            return new WarningView { Active = true, Attacker = w.Attacker, Defender = w.Defender, TicksLeft = left, Sentence = sentence };
        }

        public static OutcomeView Outcome(WorldState s)
        {
            var f = s?.Factions;
            if (f == null || !f.GameOver) return new OutcomeView { Outcome = GameOutcome.None, EndedTick = -1, Sentence = "" };
            string sentence;
            if (f.OutcomeReason == OutcomeReasons.Overrun)
                sentence = f.Outcome == GameOutcome.Victory ? "Your people overran the Ash Clan." : "The Ash Clan overran your people.";
            else
                sentence = f.Outcome == GameOutcome.Victory ? "Time is up: your people are the stronger."
                    : f.Outcome == GameOutcome.Defeat ? "Time is up: the Ash Clan is the stronger."
                    : "Time is up: both peoples are equally strong.";
            return new OutcomeView { GameOver = true, Outcome = f.Outcome, Reason = f.OutcomeReason, EndedTick = f.EndedTick, Sentence = sentence };
        }

        /// <summary>One line for the HUD: the ending, else the warning, else the strengths.</summary>
        public static string Headline(WorldState s)
        {
            var o = Outcome(s);
            if (o.GameOver) return o.Sentence;
            var w = Warning(s);
            if (w.Active) return w.Sentence;
            return "Your people " + Strength(s, PlayerIds.Human).Total + ", the Ash Clan " + Strength(s, PlayerIds.Rival).Total + ".";
        }

        // ---- helpers ----

        private static bool OwnSettlement(WorldState s, int faction, int corner)
        {
            var st = EconomyLogic.SettlementAt(s, corner);
            return st != null && st.Faction == faction;
        }

        private static bool CornerTarget(BoardTopology t, int corner, out double x, out double z, out double r)
        {
            x = z = r = 0;
            if (!t.IsCorner(corner)) return false;
            x = t.VertexX(corner);
            z = t.VertexZ(corner);
            r = CornerPressRadius;
            return true;
        }

        private static bool EdgeTarget(BoardTopology t, int hexEdge, out double x, out double z, out double r)
        {
            x = z = r = 0;
            if (hexEdge < 0 || hexEdge >= t.HexEdgeCount) return false;
            x = t.HexEdgeMidX(hexEdge);
            z = t.HexEdgeMidZ(hexEdge);
            r = RoadPressRadius;
            return true;
        }

        private static double SegmentDistance(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            double len2 = dx * dx + dz * dz;
            double u = len2 <= 0 ? 0 : ((px - ax) * dx + (pz - az) * dz) / len2;
            u = Math.Max(0, Math.Min(1, u));
            double cx = ax + u * dx - px, cz = az + u * dz - pz;
            return Math.Sqrt(cx * cx + cz * cz);
        }
    }
}
