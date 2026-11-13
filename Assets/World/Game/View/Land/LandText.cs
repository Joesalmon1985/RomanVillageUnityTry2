using System.Collections.Generic;
using System.Text;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>Inspect sentences of the land views, built from the sim's own read helpers (package V).</summary>
    public static class LandText
    {
        public static WorldState State => WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;

        public static string HexTitle(WorldState s, int hex)
        {
            var v = EconomyApi.HexStock(s, hex);
            string t = LandLook.Capitalise(TerrainInfo.DisplayName(v.Terrain));
            return v.Number > 0 ? t + " " + v.Number : t;
        }

        public static IEnumerable<string> HexLines(WorldState s, int hex)
        {
            var v = EconomyApi.HexStock(s, hex);
            if (!v.Ware.HasValue) { yield return "Open meadow: it gives nothing."; yield break; }
            string ware = Wares.DisplayName(v.Ware.Value);
            yield return v.Mature + " " + ware + " ready to take, " + v.Young + " still growing (at most " + v.Cap + ").";
            if (v.Stopped) yield return "Demons hold every camp beside it: rolls add nothing here.";
            else yield return "A roll of " + v.Number + " adds " + (s.Economy != null ? s.Economy.Tuning.BatchSize : 4) + " young " + ware
                + " when a camp stands beside it (" + v.Pips + " chances in 36).";
        }

        public static string BuildingTitle(WorldState s, int buildingId)
        {
            var b = s.Economy != null && buildingId >= 0 && buildingId < s.Economy.Buildings.Count ? s.Economy.Buildings[buildingId] : null;
            var info = EconomyApi.DescribeBuilding(s, buildingId);
            if (b != null && (b.State == BuildingState.Site || b.State == BuildingState.Constructing)) return info.Title + " (site)";
            return info.Title;
        }

        public static IEnumerable<string> BuildingLines(WorldState s, int buildingId)
        {
            var info = EconomyApi.DescribeBuilding(s, buildingId);
            var b = s.Economy.Buildings[buildingId];
            yield return info.Status;
            if (b.State == BuildingState.Site) yield return "Needs " + info.CostText + "; " + Delivered(b.Delivered) + ".";
            else if (b.State == BuildingState.Active) yield return LandLook.Capitalise(info.RecipeText) + ".";
        }

        public static string Delivered(int[] have)
        {
            var sb = new StringBuilder();
            for (int w = 0; w < have.Length; w++)
            {
                if (have[w] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(have[w]).Append(' ').Append(Wares.DisplayName((Ware)w));
            }
            return sb.Length == 0 ? "nothing delivered yet" : sb.Append(" delivered").ToString();
        }

        public static string StoreTitle(WorldState s, int settlementId)
        {
            var st = s.Economy.Settlements[settlementId];
            string name = LandLook.Capitalise(EconomyApi.SettlementName(s, settlementId));
            if (st.State == SettlementState.Site) return "Camp site";
            return st.IsCity ? name + " (city)" : name;
        }

        public static IEnumerable<string> StoreLines(WorldState s, int settlementId)
        {
            var st = s.Economy.Settlements[settlementId];
            if (st.State == SettlementState.Site)
            {
                yield return EconomyApi.WaitingSentence(s, SiteKind.Settlement, settlementId) ?? "The camp is being built.";
                yield return "Needs " + EconomyApi.CostText(s.Economy.Tuning.SettlementCost) + "; " + Delivered(st.Delivered) + ".";
                yield break;
            }
            var sb = new StringBuilder("The storehouse holds ");
            int n = 0;
            for (int w = 0; w < Wares.Count; w++)
            {
                if (st.Stock[w] <= 0) continue;
                if (n++ > 0) sb.Append(", ");
                sb.Append(st.Stock[w]).Append(' ').Append(Wares.DisplayName((Ware)w));
            }
            yield return n == 0 ? "The storehouse is empty." : sb.Append('.').ToString();
            int demons = DemonApi.DemonsAt(s, DemonSiteKind.Corner, st.Corner);
            if (demons > 0) yield return demons + " demon" + (demons > 1 ? "s" : "") + " sit here: " + DemonApi.Effect(DemonSiteKind.Corner) + ".";
            else if (st.Upgrade != UpgradeState.None)
                yield return EconomyApi.WaitingSentence(s, SiteKind.City, settlementId) ?? "It is being raised to a city.";
        }

        public static IEnumerable<string> RoadLines(WorldState s, int roadId)
        {
            var r = s.Economy.Roads[roadId];
            if (r.State == RoadState.Built) yield return "A main road of " + LandLook.PeopleName(r.Faction) + ": carriers walk it between camps.";
            else
            {
                yield return EconomyApi.WaitingSentence(s, SiteKind.Road, roadId) ?? "The road is being laid.";
                yield return "Needs " + EconomyApi.CostText(s.Economy.Tuning.RoadCost) + "; " + Delivered(r.Delivered) + ".";
            }
        }

        public static IEnumerable<string> DemonLines(WorldState s, DemonSiteKind kind, int id)
        {
            int n = DemonApi.DemonsAt(s, kind, id);
            yield return n + " demon" + (n == 1 ? "" : "s") + " sit on " + DemonApi.SiteName(s, kind, id) + ": " + DemonApi.Effect(kind) + ".";
            yield return "A banishing spell drives them away.";
        }

        public static IEnumerable<string> WarriorLines(WorldState s, int warriorId)
        {
            var w = s.Economy.Warriors[warriorId];
            yield return "A warrior of " + EconomyApi.SettlementName(s, w.Settlement) + ".";
            yield return w.Fed ? "Fed: counts towards the people's strength." : "Hungry: does not count until the next meal.";
        }
    }
}
