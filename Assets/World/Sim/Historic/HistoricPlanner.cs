using System;
using System.Collections.Generic;

namespace World.Sim.Historic
{
    /// <summary>
    /// HI-4, HI-6: the Historic brain of every people (the player's included). A needs-based planner in the spirit of the
    /// Widelands computer player (written fresh): every <see cref="HistoricTuning.PlanIntervalTicks"/> it looks at what the
    /// people has and what the castle still needs, and places at most one new site, by the first unmet need in this order:
    /// <list type="number">
    /// <item>a first woodcutter;</item>
    /// <item>foresters ahead of woodcutters: at least one forester per woodcutter, one more when the young and grown trees in
    /// the woodcutters' reach run low;</item>
    /// <item>a second woodcutter;</item>
    /// <item>a working quarry (an exhausted one is replaced);</item>
    /// <item>a clay pit, then a kiln (bricks for the second stage);</item>
    /// <item>a farm, then a sheep farm (the third stage wants grain and sheep);</item>
    /// <item>more woodcutters while wood is short and grown trees stand in reach (up to <see cref="HistoricTuning.MaxWoodcutters"/>);</item>
    /// <item>a fresh clay pit while the castle wants bricks, the store has no clay and our pits stand on dry hexes (a hex with
    /// clay first; when every Hills hex in reach is dry, a dry one whose clay seeps back, shared or not);</item>
    /// <item>a second quarry while the castle still wants stone.</item>
    /// </list>
    /// Sites are chosen by a score per kind (resources in reach, crowding, distance to the castle); ties go to the lowest hex.
    /// No more than <see cref="HistoricTuning.MaxOpenSites"/> sites are open at once.
    /// </summary>
    public static class HistoricPlanner
    {
        public static void Plan(WorldState s, int p, EventBuffer ev)
        {
            var h = s.Historic;
            if (h == null || p < 0 || p >= h.Peoples.Count) return;
            var kind = NextNeed(s, p);
            if (kind == null) return;
            int hex = BestHex(s, p, kind.Value);
            if (hex >= 0) HistoricRules.PlaceSite(s, p, kind.Value, hex, ev);
        }

        /// <summary>Buildings of a kind that are sites or active (exhausted quarries do not count).</summary>
        public static int CountOf(HistoricState h, int p, HistoricKind k)
        {
            int n = 0;
            foreach (var b in h.Buildings)
                if (b.People == p && b.Kind == k && b.State != HistoricBuildingState.Exhausted) n++;
            return n;
        }

        private static int OpenSites(HistoricState h, int p)
        {
            int n = 0;
            foreach (var b in h.Buildings) if (b.People == p && b.State == HistoricBuildingState.Site) n++;
            return n;
        }

        /// <summary>Young and grown trees (and saplings) in reach of the people's woodcutters.</summary>
        public static int TreesForWoodcutters(WorldState s, int p, out int mature)
        {
            var h = s.Historic;
            var seen = new bool[h.Hexes.Length];
            int trees = 0;
            mature = 0;
            foreach (var b in h.Buildings)
            {
                if (b.People != p || b.Kind != HistoricKind.Woodcutter) continue;
                foreach (int x in HistoricRules.Reach(s, b))
                {
                    if (seen[x]) continue;
                    seen[x] = true;
                    foreach (int t in h.Hexes[x].Trees)
                    {
                        if (t == (int)TreeStage.Mature) { mature++; trees++; }
                        else if (t == (int)TreeStage.Sapling || t == (int)TreeStage.Young) trees++;
                    }
                }
            }
            return trees;
        }

        /// <summary>The first unmet need (see the class summary), or null.</summary>
        public static HistoricKind? NextNeed(WorldState s, int p)
        {
            var h = s.Historic;
            var tu = h.Tuning;
            var hp = h.Peoples[p];
            if (OpenSites(h, p) >= tu.MaxOpenSites) return null;
            int cutters = CountOf(h, p, HistoricKind.Woodcutter);
            int foresters = CountOf(h, p, HistoricKind.Forester);
            if (cutters == 0) return Try(s, p, HistoricKind.Woodcutter);
            int trees = TreesForWoodcutters(s, p, out int mature);
            int wantForesters = cutters + (trees < 6 * cutters ? 1 : 0);
            if (foresters < wantForesters) { var k = Try(s, p, HistoricKind.Forester); if (k != null) return k; }
            if (cutters < 2) { var k = Try(s, p, HistoricKind.Woodcutter); if (k != null) return k; }
            if (CountOf(h, p, HistoricKind.Quarry) == 0) { var k = Try(s, p, HistoricKind.Quarry); if (k != null) return k; }
            if (CountOf(h, p, HistoricKind.ClayPit) == 0) { var k = Try(s, p, HistoricKind.ClayPit); if (k != null) return k; }
            if (CountOf(h, p, HistoricKind.Kiln) == 0) { var k = Try(s, p, HistoricKind.Kiln); if (k != null) return k; }
            if (CountOf(h, p, HistoricKind.Farm) == 0) { var k = Try(s, p, HistoricKind.Farm); if (k != null) return k; }
            if (CountOf(h, p, HistoricKind.SheepFarm) == 0) { var k = Try(s, p, HistoricKind.SheepFarm); if (k != null) return k; }
            bool woodShort = hp.Stock[(int)Ware.Wood] < tu.SiteReserveWood + 4 || HistoricRules.CastleNeed(h, hp, (int)Ware.Wood) > 0;
            if (woodShort && cutters < tu.MaxWoodcutters && mature < 3 * cutters)
            {
                // Grown trees are short in the present reach: a woodcutter elsewhere, if a hex with grown trees exists.
                var k = Try(s, p, HistoricKind.Woodcutter);
                if (k != null) return k;
            }
            if (HistoricRules.CastleNeed(h, hp, (int)Ware.Bricks) > 0 && hp.Stock[(int)Ware.Clay] < 2
                && CountOf(h, p, HistoricKind.ClayPit) < tu.MaxClayPits && OwnPitsDry(h, p))
            {
                // Clay seeps back slowly and pits of several peoples may share a hex: dig a fresh hex when ours are dry.
                var k = Try(s, p, HistoricKind.ClayPit);
                if (k != null) return k;
            }
            if (HistoricRules.CastleNeed(h, hp, (int)Ware.Stone) > 0 && CountOf(h, p, HistoricKind.Quarry) < tu.MaxQuarries)
            {
                var k = Try(s, p, HistoricKind.Quarry);
                if (k != null) return k;
            }
            return null;
        }

        /// <summary>True when every clay pit of the people stands on a hex with less than 2 clay left.</summary>
        public static bool OwnPitsDry(HistoricState h, int p)
        {
            foreach (var b in h.Buildings)
                if (b.People == p && b.Kind == HistoricKind.ClayPit && h.Hexes[b.Hex].Clay >= 2) return false;
            return true;
        }

        private static HistoricKind? Try(WorldState s, int p, HistoricKind k) => BestHex(s, p, k) >= 0 ? k : (HistoricKind?)null;

        /// <summary>
        /// The best hex for a new site of a kind: in the near territory (one ring around the holdings) first, and only when
        /// nothing there serves, in the far territory (<see cref="HistoricRules.FarRings"/>). -1 when none is worth it.
        /// </summary>
        public static int BestHex(WorldState s, int p, HistoricKind kind)
        {
            int near = BestHex(s, p, kind, 1);
            return near >= 0 ? near : BestHex(s, p, kind, HistoricRules.FarRings);
        }

        private static int BestHex(WorldState s, int p, HistoricKind kind, int rings)
        {
            var h = s.Historic;
            var hp = h.Peoples[p];
            var d = HistoricDefs.Get(kind);
            int best = -1, bestScore = int.MinValue;
            foreach (int hex in HistoricRules.Territory(s, p, rings))
            {
                var r = h.Hexes[hex];
                if (!HistoricDefs.Allows(d, r.Terrain)) continue;
                int onHex = 0, sameKind = 0;
                foreach (var b in h.Buildings)
                {
                    if (b.Hex != hex || b.State == HistoricBuildingState.Exhausted) continue;
                    onHex++;
                    if (b.Kind == kind) sameKind++;
                }
                if (onHex >= h.Tuning.MaxBuildingsPerHex) continue;
                int score;
                switch (kind)
                {
                    case HistoricKind.Woodcutter:
                    {
                        int mature = 0, cutters = 0;
                        var reach = new List<int> { hex };
                        reach.AddRange(HistoricRules.Neighbours(s.Board, hex));
                        foreach (int x in reach)
                        {
                            foreach (int t in h.Hexes[x].Trees) if (t == (int)TreeStage.Mature) mature++;
                            foreach (var b in h.Buildings) if (b.Hex == x && b.Kind == HistoricKind.Woodcutter) cutters++;
                        }
                        if (mature < 3) continue;
                        score = 10 * mature - 40 * cutters;
                        break;
                    }
                    case HistoricKind.Forester:
                    {
                        if (r.Trees.Length == 0) continue;
                        int free = 0, cutters = 0, foresters = 0;
                        var reach = new List<int> { hex };
                        reach.AddRange(HistoricRules.Neighbours(s.Board, hex));
                        foreach (int x in reach)
                        {
                            foreach (int t in h.Hexes[x].Trees) if (t == (int)TreeStage.Empty || t == (int)TreeStage.Stump) free++;
                            foreach (var b in h.Buildings)
                            {
                                if (b.Hex != x || b.People != p) continue;
                                if (b.Kind == HistoricKind.Woodcutter) cutters++;
                                if (b.Kind == HistoricKind.Forester) foresters++;
                            }
                        }
                        if (cutters == 0 || free == 0) continue; // plant where our woodcutters will fell
                        score = 50 * cutters - 30 * foresters + 2 * free;
                        break;
                    }
                    case HistoricKind.Quarry:
                        if (r.Rock < 4) continue;
                        score = r.Rock - 30 * sameKind;
                        break;
                    case HistoricKind.ClayPit:
                        // A hex that is dry right now still serves: its clay seeps back (ClayRefillTicks), also when another people's
                        // pit shares it. Hexes with clay in them come first (the tier), dry ones are the fallback.
                        if (r.ClayCap < 2) continue;
                        score = (r.Clay >= 2 ? 1000 : 0) + r.ClayCap - 20 * sameKind;
                        break;
                    case HistoricKind.Farm:
                        if (r.Fields.Length == 0) continue;
                        score = 10 * r.Fields.Length - 40 * sameKind;
                        break;
                    case HistoricKind.SheepFarm:
                        score = (r.Terrain == Terrain.Pasture ? 30 : 0) - 20 * sameKind;
                        break;
                    default:
                        score = 0;
                        break;
                }
                score -= 5 * HistoricRules.Distance(s.Board, hp.StoreCorner, hex);
                if (score > bestScore) { bestScore = score; best = hex; }
            }
            return best;
        }
    }
}
