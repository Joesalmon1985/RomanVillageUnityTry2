using System;
using System.Collections.Generic;
using World.Sim.Economy;
using World.Sim.Eras;

namespace World.Sim.Encounters.Council
{
    /// <summary>A named person derived from a settlement's real state. Not a new worker: nothing here enters the economy.</summary>
    public sealed class Resident
    {
        public string Key;
        public string Name;
        public string Occupation;
        public string OccupationId;
        public bool Living = true;
        public bool Eligible = true;
        public bool WantsToStand;
    }

    /// <summary>
    /// Deterministic residents of a settlement: one per active building, one per carrier living there and the storekeeper.
    /// Names come from a seeded list. Read-only: never touches the economy.
    /// </summary>
    public static class CouncilRoster
    {
        public const int StandPercent = 65;
        public const int MinimumCandidates = 4;

        static ulong Hash(string text)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL; h ^= h >> 33;
            return h;
        }

        public static ulong Hash(WorldState s, int settlement, string key, string salt) =>
            Hash(s.Seed + "|" + settlement + "|" + key + "|" + salt);

        /// <summary>Occupation id and display name of the person who works a building type.</summary>
        public static void OccupationOf(BuildingType t, out string id, out string display)
        {
            switch (t)
            {
                case BuildingType.LogPile: case BuildingType.WoodcutterHut: id = "woodcutter"; display = "woodcutter"; break;
                case BuildingType.ClayScrape: case BuildingType.ClayPit: id = "claydigger"; display = "clay digger"; break;
                case BuildingType.GrainBasket: id = "gleaner"; display = "gleaner"; break;
                case BuildingType.Farm: id = "farmer"; display = "farmer"; break;
                case BuildingType.ThornFold: case BuildingType.ShepherdHut: id = "shepherd"; display = "shepherd"; break;
                case BuildingType.RockHeap: case BuildingType.Quarry: id = "quarrier"; display = "quarrier"; break;
                case BuildingType.Sawmill: id = "sawyer"; display = "sawyer"; break;
                case BuildingType.Kiln: id = "potter"; display = "potter"; break;
                case BuildingType.Bakery: id = "baker"; display = "baker"; break;
                case BuildingType.Smokehouse: id = "curer"; display = "meat curer"; break;
                case BuildingType.Stonecutter: id = "stonecutter"; display = "stonecutter"; break;
                case BuildingType.WarriorLodge: id = "warden"; display = "warden"; break;
                default: id = "worker"; display = "worker"; break;
            }
        }

        public static Settlement ActiveSettlement(WorldState s, int settlementId)
        {
            var e = s?.Economy;
            if (e == null || settlementId < 0 || settlementId >= e.Settlements.Count) return null;
            var st = e.Settlements[settlementId];
            return st.State == SettlementState.Active ? st : null;
        }

        /// <summary>All residents, sorted by key. Names are unique within a settlement.</summary>
        public static List<Resident> Build(WorldState s, int settlementId, CouncilContent content)
        {
            var list = new List<Resident>();
            var st = ActiveSettlement(s, settlementId);
            if (st == null) return list;
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                if (b.Settlement != settlementId || b.State != BuildingState.Active) continue;
                OccupationOf(b.Type, out var id, out var disp);
                list.Add(new Resident { Key = "b" + b.Id, Occupation = disp, OccupationId = id });
            }
            foreach (var c in e.Carriers)
                if (c.Home == settlementId) list.Add(new Resident { Key = "c" + c.Id, Occupation = "carrier", OccupationId = "carrier" });
            list.Add(new Resident { Key = "s" + settlementId, Occupation = "storekeeper", OccupationId = "storekeeper" });
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            var names = content.Names;
            var used = new HashSet<int>();
            foreach (var r in list)
            {
                int n = names.Count;
                int idx = (int)(Hash(s, settlementId, r.Key, "name") % (ulong)n);
                while (used.Contains(idx)) idx = (idx + 1) % n;
                used.Add(idx);
                r.Name = names[idx];
                r.Eligible = true;
                r.WantsToStand = (int)(Hash(s, settlementId, r.Key, "stand") % 100UL) < StandPercent;
            }
            if (list.Count > names.Count) // more people than names: later ones get a numeral
                for (int i = names.Count; i < list.Count; i++) list[i].Name = list[i].Name + " " + (i / names.Count + 1);
            return list;
        }

        public static List<Resident> WouldStand(List<Resident> residents)
        {
            var r = new List<Resident>();
            foreach (var x in residents) if (x.Living && x.Eligible && x.WantsToStand) r.Add(x);
            return r;
        }

        /// <summary>True when an election may be offered for the settlement now (four living, eligible residents want to stand).</summary>
        public static bool IsEligible(WorldState s, int settlementId, CouncilContent content, out string reason)
        {
            reason = null;
            if (ActiveSettlement(s, settlementId) == null) { reason = CouncilReasons.UnknownSettlement; return false; }
            if (WouldStand(Build(s, settlementId, content)).Count < MinimumCandidates) { reason = CouncilReasons.NotEligible; return false; }
            return true;
        }

        public static Era EraOf(WorldState s) => s.Eras != null ? s.Eras.Current : Era.Prehistoric;
    }
}
