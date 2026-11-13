using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>The five card families (ECONOMY_DESIGN section 6), each tied to a real hook in the simulation.</summary>
    public enum CardFamily
    {
        /// <summary>Brown/grey: bigger harvest batches on one terrain, or more output at one workshop type.</summary>
        RawAndCrafted = 0,
        /// <summary>Yellow: better market rate, coin per round, cheaper roads.</summary>
        Commerce = 1,
        /// <summary>Blue: victory points.</summary>
        Civic = 2,
        /// <summary>Red: shields, faster training, harder-hitting soldiers.</summary>
        Military = 3,
        /// <summary>Green: hero and ward improvements against the demons.</summary>
        Lore = 4,
    }

    /// <summary>
    /// One card row. Effects are plain numbers summed over a side's built cards (<see cref="CardEffects"/>)
    /// and read by the systems that own the decision (batch modifier, workshop output, market, roads, hero,
    /// infection, outbreak cap, barracks, military comparison, score).
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public int Age;
        public CardFamily Family;
        /// <summary>Wares taken from the storehouse when built (index = (int)Ware).</summary>
        public int[] Cost = new int[Wares.Count];
        public int CoinCost;
        /// <summary>Player-facing effect sentence.</summary>
        public string Text;

        // Raw and crafted.
        public int ForestBatch, HillsBatch, FieldsBatch, PastureBatch, MountainBatch, AllBatch;
        /// <summary>Workshop that makes ExtraOutput more of its main output each cycle (when ExtraOutput &gt; 0).</summary>
        public BuildingType Workshop;
        public int ExtraOutput;
        // Commerce.
        public int TradeRateBonus;
        public int CoinPerRound;
        /// <summary>Extra road segments one road payment covers.</summary>
        public int RoadSegments;
        // Civic.
        public int Vp;
        /// <summary>VP per city you hold (home included).</summary>
        public int VpPerCity;
        // Military.
        public int Shields;
        /// <summary>Percent added to training ticks (negative = faster).</summary>
        public int TrainingPercent;
        public int DamageBonus;
        // Lore.
        public int HeroMoves;
        public int InfectionMinus;
        /// <summary>Extra demons a site holds before it outbreaks.</summary>
        public int DemonCapBonus;
    }

    /// <summary>Sum of a side's built card effects.</summary>
    public struct CardEffects
    {
        public int ForestBatch, HillsBatch, FieldsBatch, PastureBatch, MountainBatch, AllBatch;
        public int TradeRateBonus, CoinPerRound, RoadSegments;
        public int Vp, VpPerCity;
        public int Shields, TrainingPercent, DamageBonus;
        public int HeroMoves, InfectionMinus, DemonCapBonus;
        /// <summary>Per building type: extra units of its main output per cycle (null when no card adds any).</summary>
        public int[] ExtraOutput;

        public void Add(CardDef d)
        {
            ForestBatch += d.ForestBatch;
            HillsBatch += d.HillsBatch;
            FieldsBatch += d.FieldsBatch;
            PastureBatch += d.PastureBatch;
            MountainBatch += d.MountainBatch;
            AllBatch += d.AllBatch;
            TradeRateBonus += d.TradeRateBonus;
            CoinPerRound += d.CoinPerRound;
            RoadSegments += d.RoadSegments;
            Vp += d.Vp;
            VpPerCity += d.VpPerCity;
            Shields += d.Shields;
            TrainingPercent += d.TrainingPercent;
            DamageBonus += d.DamageBonus;
            HeroMoves += d.HeroMoves;
            InfectionMinus += d.InfectionMinus;
            DemonCapBonus += d.DemonCapBonus;
            if (d.ExtraOutput > 0)
            {
                if (ExtraOutput == null) ExtraOutput = new int[BuildingDefs.Count];
                ExtraOutput[(int)d.Workshop] += d.ExtraOutput;
            }
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
                case Terrain.Pasture: b += PastureBatch; break;
                case Terrain.Mountain: b += MountainBatch; break;
            }
            return b;
        }
    }

    /// <summary>
    /// The card table: 18 original cards, 6 per age (each age deals 3 to each side), every family in every age.
    /// Costs follow design section 4: Age I Rations or coin, Age II Rations + Bricks, Age III Rations + Iron.
    /// </summary>
    public static class CardTable
    {
        public static readonly CardDef[] All = Create();

        private static readonly Dictionary<string, CardDef> ById = Index(All);

        private static Dictionary<string, CardDef> Index(CardDef[] all)
        {
            var d = new Dictionary<string, CardDef>();
            foreach (var c in all) d[c.Id] = c;
            return d;
        }

        /// <summary>Card row by id (e.g. "lumber_camp"), or null.</summary>
        public static CardDef Get(string id) => id != null && ById.TryGetValue(id, out var r) ? r : null;

        public static List<CardDef> ForAge(int age)
        {
            var l = new List<CardDef>();
            foreach (var c in All) if (c.Age == age) l.Add(c);
            return l;
        }

        public static List<CardDef> ForFamily(CardFamily f)
        {
            var l = new List<CardDef>();
            foreach (var c in All) if (c.Family == f) l.Add(c);
            return l;
        }

        /// <summary>"Raw and crafted", "Commerce", "Civic", "Military", "Lore".</summary>
        public static string FamilyName(CardFamily f) => f == CardFamily.RawAndCrafted ? "Raw and crafted" : f.ToString();

        /// <summary>Suggested colour per family as "#RRGGBB" (brown, yellow, blue, red, green).</summary>
        public static string FamilyColour(CardFamily f)
        {
            switch (f)
            {
                case CardFamily.RawAndCrafted: return "#8B5A2B";
                case CardFamily.Commerce: return "#D4A017";
                case CardFamily.Civic: return "#2F6DB5";
                case CardFamily.Military: return "#B22222";
                default: return "#2E8B57";
            }
        }

        private static CardDef C(string id, string name, int age, CardFamily family, int[] cost, int coin, string text) =>
            new CardDef { Id = id, Name = name, Age = age, Family = family, Cost = cost, CoinCost = coin, Text = text };

        private static int[] W(params object[] pairs) => EconomyTuning.W(pairs);

        private static CardDef[] Create()
        {
            const CardFamily Raw = CardFamily.RawAndCrafted, Com = CardFamily.Commerce, Civ = CardFamily.Civic,
                Mil = CardFamily.Military, Lore = CardFamily.Lore;
            var t = new List<CardDef>();
            // ---- Age I: 1 Rations or coin ----
            var c = C("lumber_camp", "Lumber Camp", 1, Raw, W(Ware.Rations, 1), 0, "Forest batches at your cities are 2 larger.");
            c.ForestBatch = 2; t.Add(c);
            c = C("clay_diggers", "Clay Diggers", 1, Raw, W(), 2, "Hills batches at your cities are 2 larger.");
            c.HillsBatch = 2; t.Add(c);
            c = C("toll_gate", "Toll Gate", 1, Com, W(), 2, "Gain 2 coin at the start of each round.");
            c.CoinPerRound = 2; t.Add(c);
            c = C("shrine", "Shrine", 1, Civ, W(Ware.Rations, 1), 1, "Worth 1 victory point.");
            c.Vp = 1; t.Add(c);
            c = C("palisade", "Palisade", 1, Mil, W(), 2, "1 shield in every military comparison.");
            c.Shields = 1; t.Add(c);
            c = C("ranger_post", "Ranger Post", 1, Lore, W(), 2, "The hero gets 1 more move each round.");
            c.HeroMoves = 1; t.Add(c);
            // ---- Age II: Rations + Bricks ----
            c = C("granary", "Granary", 2, Raw, W(Ware.Rations, 1, Ware.Bricks, 2), 0, "Fields and pasture batches at your cities are 2 larger.");
            c.FieldsBatch = 2; c.PastureBatch = 2; t.Add(c);
            c = C("charcoal_guild", "Charcoal Guild", 2, Raw, W(Ware.Rations, 1, Ware.Bricks, 2), 0, "Your charcoal kilns make 1 more coal each cycle.");
            c.Workshop = BuildingType.CharcoalKiln; c.ExtraOutput = 1; t.Add(c);
            c = C("road_guild", "Road Guild", 2, Com, W(Ware.Rations, 1, Ware.Bricks, 1), 0, "One road payment covers 3 more segments.");
            c.RoadSegments = 3; t.Add(c);
            c = C("monument", "Monument", 2, Civ, W(Ware.Rations, 1, Ware.Bricks, 3), 0, "Worth 2 victory points.");
            c.Vp = 2; t.Add(c);
            c = C("drill_yard", "Drill Yard", 2, Mil, W(Ware.Rations, 1, Ware.Bricks, 2), 0, "1 shield; training at your barracks takes 40% less time.");
            c.Shields = 1; c.TrainingPercent = -40; t.Add(c);
            c = C("ward_stones", "Ward Stones", 2, Lore, W(Ware.Rations, 1, Ware.Bricks, 2), 0, "A site holds 4 demons before it outbreaks.");
            c.DemonCapBonus = 1; t.Add(c);
            // ---- Age III: Rations + Iron ----
            c = C("master_smiths", "Master Smiths", 3, Raw, W(Ware.Rations, 1, Ware.Iron, 1), 0, "Your smithies make 1 more weapons each cycle.");
            c.Workshop = BuildingType.Smithy; c.ExtraOutput = 1; t.Add(c);
            c = C("merchant_guild", "Merchant Guild", 3, Com, W(Ware.Rations, 1, Ware.Iron, 1), 0, "The market rate improves by 1 and you gain 1 coin each round.");
            c.TradeRateBonus = 1; c.CoinPerRound = 1; t.Add(c);
            c = C("great_hall", "Great Hall", 3, Civ, W(Ware.Rations, 1, Ware.Iron, 1), 2, "Worth 3 victory points.");
            c.Vp = 3; t.Add(c);
            c = C("census_hall", "Census Hall", 3, Civ, W(Ware.Rations, 1, Ware.Iron, 1), 0, "1 victory point for each city you hold.");
            c.VpPerCity = 1; t.Add(c);
            c = C("armoury", "Armoury", 3, Mil, W(Ware.Rations, 1, Ware.Iron, 1), 0, "2 shields; your soldiers deal 1 more damage.");
            c.Shields = 2; c.DamageBonus = 1; t.Add(c);
            c = C("beacon_tower", "Beacon Tower", 3, Lore, W(Ware.Rations, 1, Ware.Iron, 1), 0, "One fewer infection card is drawn each round (at least 1).");
            c.InfectionMinus = 1; t.Add(c);
            return t.ToArray();
        }
    }
}
