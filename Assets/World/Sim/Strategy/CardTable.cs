using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>
    /// One card row. Effects are plain numbers summed over a side's built cards (<see cref="CardEffects"/>)
    /// and read by the systems that own the decision (batch modifier, market, hero, infection, barracks...).
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public int Age;
        /// <summary>Wares taken from the storehouse when built (index = (int)Ware).</summary>
        public int[] Cost = new int[Wares.Count];
        public int CoinCost;
        /// <summary>Player-facing effect sentence.</summary>
        public string Text;

        public int ForestBatch, HillsBatch, FieldsBatch, MountainBatch, AllBatch;
        public int TradeRateBonus;
        public int HeroMoves;
        public int InfectionMinus;
        public int BreachThresholdBonus;
        /// <summary>Percent added to training ticks (negative = faster).</summary>
        public int TrainingPercent;
        public int Vp;
        /// <summary>VP per city beyond the first (at most 2).</summary>
        public int VpPerExtraCity;
        public int CoinPerRound;
        public int DamageBonus;
        /// <summary>Sell 1 ware for 1 coin.</summary>
        public bool SellOneForOne;
        public int UnitRationDiscount;
    }

    /// <summary>Sum of a side's built card effects.</summary>
    public struct CardEffects
    {
        public int ForestBatch, HillsBatch, FieldsBatch, MountainBatch, AllBatch;
        public int TradeRateBonus, HeroMoves, InfectionMinus, BreachThresholdBonus, TrainingPercent;
        public int Vp, VpPerExtraCity, CoinPerRound, DamageBonus, UnitRationDiscount;
        public bool SellOneForOne;

        public void Add(CardDef d)
        {
            ForestBatch += d.ForestBatch;
            HillsBatch += d.HillsBatch;
            FieldsBatch += d.FieldsBatch;
            MountainBatch += d.MountainBatch;
            AllBatch += d.AllBatch;
            TradeRateBonus += d.TradeRateBonus;
            HeroMoves += d.HeroMoves;
            InfectionMinus += d.InfectionMinus;
            BreachThresholdBonus += d.BreachThresholdBonus;
            TrainingPercent += d.TrainingPercent;
            Vp += d.Vp;
            VpPerExtraCity += d.VpPerExtraCity;
            CoinPerRound += d.CoinPerRound;
            DamageBonus += d.DamageBonus;
            UnitRationDiscount += d.UnitRationDiscount;
            SellOneForOne |= d.SellOneForOne;
        }

        /// <summary>Batch bonus for a hex of this terrain.</summary>
        public int BatchBonus(Terrain t)
        {
            int b = AllBatch;
            switch (t)
            {
                case Terrain.Forest: b += ForestBatch; break;
                case Terrain.Hills: b += HillsBatch; break;
                case Terrain.Fields: b += FieldsBatch; break;
                case Terrain.Mountain: b += MountainBatch; break;
            }
            return b;
        }
    }

    /// <summary>The card table: 18 original cards, 6 per age (each age deals 3 to each side).</summary>
    public static class CardTable
    {
        public static readonly CardDef[] All = Create();

        private static Dictionary<string, CardDef> _byId;

        public static CardDef Get(string id)
        {
            if (_byId == null)
            {
                var d = new Dictionary<string, CardDef>();
                foreach (var c in All) d[c.Id] = c;
                _byId = d;
            }
            return id != null && _byId.TryGetValue(id, out var r) ? r : null;
        }

        public static List<CardDef> ForAge(int age)
        {
            var l = new List<CardDef>();
            foreach (var c in All) if (c.Age == age) l.Add(c);
            return l;
        }

        private static CardDef C(string id, string name, int age, int[] cost, int coin, string text)
        {
            return new CardDef { Id = id, Name = name, Age = age, Cost = cost, CoinCost = coin, Text = text };
        }

        private static int[] W(params object[] pairs) => EconomyTuning.W(pairs);

        private static CardDef[] Create()
        {
            var t = new List<CardDef>();
            // ---- Age I ----
            var c = C("lumber_camp", "Lumber Camp", 1, W(Ware.Planks, 2), 0, "Forest batches at your cities are 2 larger.");
            c.ForestBatch = 2; t.Add(c);
            c = C("stone_cutters", "Stone Cutters", 1, W(Ware.Planks, 2), 0, "Hills batches at your cities are 2 larger.");
            c.HillsBatch = 2; t.Add(c);
            c = C("granary", "Granary", 1, W(Ware.Planks, 1, Ware.Stone, 1), 0, "Fields batches at your cities are 2 larger.");
            c.FieldsBatch = 2; t.Add(c);
            c = C("trade_charter", "Trade Charter", 1, W(), 2, "The market rate improves by 1.");
            c.TradeRateBonus = 1; t.Add(c);
            c = C("ranger_post", "Ranger Post", 1, W(Ware.Planks, 1, Ware.Stone, 1), 0, "The hero gets 1 more move each round.");
            c.HeroMoves = 1; t.Add(c);
            c = C("toll_gate", "Toll Gate", 1, W(Ware.Stone, 2), 0, "Gain 2 coin at the start of each round.");
            c.CoinPerRound = 2; t.Add(c);
            // ---- Age II ----
            c = C("drill_yard", "Drill Yard", 2, W(Ware.Planks, 2, Ware.Stone, 2), 0, "Training at your barracks takes 40% less time.");
            c.TrainingPercent = -40; t.Add(c);
            c = C("ore_survey", "Ore Survey", 2, W(Ware.Planks, 2, Ware.Stone, 1), 0, "Mountain batches at your cities are 2 larger.");
            c.MountainBatch = 2; t.Add(c);
            c = C("ward_stones", "Ward Stones", 2, W(Ware.Stone, 3), 0, "A flag needs 4 demons before it breaches.");
            c.BreachThresholdBonus = 1; t.Add(c);
            c = C("monument", "Monument", 2, W(Ware.Stone, 3), 2, "Worth 1 victory point.");
            c.Vp = 1; t.Add(c);
            c = C("beacon_tower", "Beacon Tower", 2, W(Ware.Planks, 2, Ware.Stone, 2), 0, "One fewer infection is drawn each round (at least 1).");
            c.InfectionMinus = 1; t.Add(c);
            c = C("mess_hall", "Mess Hall", 2, W(Ware.Planks, 2, Ware.Stone, 1), 0, "Each unit needs 1 fewer rations.");
            c.UnitRationDiscount = 1; t.Add(c);
            // ---- Age III ----
            c = C("great_hall", "Great Hall", 3, W(Ware.Planks, 4, Ware.Stone, 4), 3, "Worth 2 victory points.");
            c.Vp = 2; t.Add(c);
            c = C("census_hall", "Census Hall", 3, W(Ware.Planks, 3, Ware.Stone, 2), 0, "1 victory point for each city beyond your first (at most 2).");
            c.VpPerExtraCity = 1; t.Add(c);
            c = C("armoury", "Armoury", 3, W(Ware.Planks, 2, Ware.Stone, 2), 2, "Your units deal 1 more damage.");
            c.DamageBonus = 1; t.Add(c);
            c = C("harvest_festival", "Harvest Festival", 3, W(Ware.Planks, 2, Ware.Grain, 2), 0, "Every batch at your cities is 1 larger.");
            c.AllBatch = 1; t.Add(c);
            c = C("merchant_guild", "Merchant Guild", 3, W(), 3, "Sell 1 ware for 1 coin at the market.");
            c.SellOneForOne = true; t.Add(c);
            c = C("laurel_arch", "Laurel Arch", 3, W(Ware.Stone, 4), 2, "Worth 1 victory point.");
            c.Vp = 1; t.Add(c);
            return t.ToArray();
        }
    }
}
