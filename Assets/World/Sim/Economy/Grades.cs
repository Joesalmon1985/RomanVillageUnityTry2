using World.Sim.Eras;

namespace World.Sim.Economy
{
    /// <summary>Building and settlement grades (PRD_AGES AG-11, 5.6). Grade 0 to 1 in goods from Historic; 1 to 2 in credits from Modern.</summary>
    public enum Grade { Prehistoric = 0, Historic = 1, Modern = 2 }

    /// <summary>
    /// One grade of a building type (PRD_AGES 5.6, A.4): what it costs to reach and what it changes. Goods is indexed by
    /// ware id (length <see cref="Wares.Count"/>). The worker's tool (<see cref="BuildingDef.ToolWare"/>) is charged on top
    /// of Goods once that ware exists (H2-1). A field of 0 means "as the grade below" (CycleTicks, YieldPerUnit,
    /// InputBufferCycles: the EconomyTuning default; Garrison, Sight: none).
    /// </summary>
    public sealed class GradeDef
    {
        public Grade To;
        public int[] Goods = new int[Wares.Count];
        public int Credits;
        /// <summary>Build ticks once everything has arrived (H1-0 choice: the type's BuildTicks, at least 100).</summary>
        public int Ticks;
        /// <summary>One sentence for the inspect panel and the proposal reason.</summary>
        public string Benefit;
        public int CycleTicks;
        public int YieldPerUnit;
        public int InputBufferCycles;
        public int Garrison;
        public int Sight;
    }

    /// <summary>
    /// The grade table of the 16 Prehistoric types (PRD_AGES 5.6), data only. H1-2 implements grade 1, M1-2 grade 2;
    /// H2-1 adds the rows of types 16-39. Index = grade: [0] the type as built, [1] Historic, [2] Modern.
    /// </summary>
    public static class GradeDefs
    {
        // Future ware ids of the worker tools (PRD_AGES 5.1); not yet in the Ware enum in H1.
        public const int Axe = 22, Pick = 23, Hammer = 24, Saw = 25, Sickle = 26, Knife = 27, Tongs = 28;

        /// <summary>Sets BornGrade, Era, ToolWare and Grades of a def of types 0-15.</summary>
        internal static void Attach(BuildingDef d)
        {
            d.BornGrade = Grade.Prehistoric;
            d.Era = Era.Prehistoric;
            d.ToolWare = ToolOf(d.Type);
            int ticks = d.BuildTicks > 100 ? d.BuildTicks : 100;
            var g0 = new GradeDef { To = Grade.Prehistoric, Goods = (int[])d.Cost.Clone(), Ticks = d.BuildTicks, Benefit = d.Purpose,
                CycleTicks = d.WorkTicks, YieldPerUnit = d.Yield };
            GradeDef g1, g2;
            switch (d.Class)
            {
                case BuildingClass.PrimitiveCollector:
                    g1 = G1(ticks, "It works faster: a cycle takes 70 ticks instead of 100.", 70, 0, 0, (Ware.Planks, 1));
                    g2 = G2(30, ticks, "It works faster again: a cycle takes 50 ticks.", 50, 0);
                    break;
                case BuildingClass.ImprovedCollector:
                    g1 = G1(ticks, "It takes three from each unit of stock instead of two.", 0, 3, 0, (Ware.Planks, 2), (Ware.Bricks, 1));
                    g2 = G2(120, ticks, "It works faster: a cycle takes 40 ticks instead of 60.", 40, 3);
                    break;
                default:
                    switch (d.Type)
                    {
                        case BuildingType.Sawmill:
                            g1 = G1(ticks, "It saws faster (65 ticks instead of 100) and keeps three cycles of wood on hand.", 65, 0, 3, (Ware.Planks, 2), (Ware.Bricks, 2));
                            g2 = G2(150, ticks, "It saws faster again: 50 ticks.", 50, 0);
                            break;
                        case BuildingType.Kiln:
                            g1 = G1(ticks, "It fires faster: 100 ticks instead of 150.", 100, 0, 0, (Ware.Planks, 2), (Ware.CutStone, 2));
                            g2 = G2(150, ticks, "It fires faster again: 75 ticks.", 75, 0);
                            break;
                        case BuildingType.Bakery:
                            g1 = G1(ticks, "It bakes faster: 65 ticks instead of 100.", 65, 0, 0, (Ware.Planks, 2), (Ware.Bricks, 2));
                            g2 = G2(120, ticks, "It bakes faster again: 50 ticks.", 50, 0);
                            break;
                        case BuildingType.Smokehouse:
                            g1 = G1(ticks, "It smokes faster (100 ticks instead of 150) and also takes game.", 100, 0, 0, (Ware.Planks, 2), (Ware.Bricks, 1));
                            g2 = G2(120, ticks, "It smokes faster again: 75 ticks.", 75, 0);
                            break;
                        case BuildingType.Stonecutter:
                            g1 = G1(ticks, "It cuts faster: 100 ticks instead of 150.", 100, 0, 0, (Ware.Planks, 2), (Ware.Bricks, 1));
                            g2 = G2(150, ticks, "It cuts faster again: 75 ticks.", 75, 0);
                            break;
                        case BuildingType.WarriorLodge:
                            g1 = G1(ticks, "It becomes a barracks: no cap of three, a recruit every 200 ticks.", 200, 0, 0, (Ware.Planks, 3), (Ware.Bricks, 3), (Ware.CutStone, 2));
                            g2 = G2(250, ticks, "It trains two recruits at once, every 120 ticks.", 120, 0);
                            break;
                        default:
                            g1 = G1(ticks, d.Purpose, 0, 0, 0);
                            g2 = G2(150, ticks, d.Purpose, 0, 0);
                            break;
                    }
                    break;
            }
            d.Grades = new[] { g0, g1, g2 };
        }

        /// <summary>The worker's tool of a type (5.2), as a future ware id, or -1.</summary>
        public static int ToolOf(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.WoodcutterHut: return Axe;
                case BuildingType.ClayPit: return Pick;
                case BuildingType.Farm: return Sickle;
                case BuildingType.ShepherdHut: return Knife;
                case BuildingType.Quarry: return Pick;
                case BuildingType.Sawmill: return Saw;
                case BuildingType.Kiln: return Tongs;
                case BuildingType.Smokehouse: return Knife;
                case BuildingType.Stonecutter: return Hammer;
                default: return -1;
            }
        }

        private static GradeDef G1(int ticks, string benefit, int cycle, int yield, int buffer, params (Ware w, int n)[] goods)
        {
            var g = new GradeDef { To = Grade.Historic, Ticks = ticks, Benefit = benefit, CycleTicks = cycle, YieldPerUnit = yield, InputBufferCycles = buffer };
            foreach (var (w, n) in goods) g.Goods[(int)w] += n;
            return g;
        }

        private static GradeDef G2(int credits, int ticks, string benefit, int cycle, int yield) =>
            new GradeDef { To = Grade.Modern, Credits = credits, Ticks = ticks, Benefit = benefit, CycleTicks = cycle, YieldPerUnit = yield };
    }

    // Commands (PRD_AGES 5.6, A.4). PlayerId = people.

    /// <summary>Raises a finished building one grade (goods from Historic, credits from Modern).</summary>
    public sealed class UpgradeBuilding : Command { public int BuildingId; }

    /// <summary>Stops an upgrade: delivered goods go back to the storehouse, reserved credits are released.</summary>
    public sealed class CancelUpgrade : Command { public int BuildingId; }

    /// <summary>City to Town (Historic) or Town to District (Modern). Camp to City stays UpgradeCity.</summary>
    public sealed class UpgradeSettlement : Command { public int Corner; }

    // Events (PRD_AGES A.4; Tick is stamped).

    /// <summary>An upgrade was accepted; Grade is the grade it is heading for.</summary>
    public sealed class UpgradeStarted : SimEvent { public int BuildingId; public Grade Grade; }

    /// <summary>The upgrade finished: the building now has Grade (the view re-dresses it).</summary>
    public sealed class BuildingUpgraded : SimEvent { public int BuildingId; public Grade Grade; }

    public sealed class UpgradeCancelled : SimEvent { public int BuildingId; }

    /// <summary>A City became a Town (like CityUpgraded).</summary>
    public sealed class TownUpgraded : SimEvent { public int SettlementId; public int Corner; public int Faction; }

    /// <summary>A Town became a District (M1).</summary>
    public sealed class DistrictUpgraded : SimEvent { public int SettlementId; public int Corner; public int Faction; }

    /// <summary>
    /// Validation and flow of the grade commands. Real from H1-2 for grade 1 (goods) and City to Town; a grade 2 upgrade
    /// and Town to District still end in "disabled" ("That action is not available.") until M1-2 (credits).
    /// </summary>
    internal static class GradeLogic
    {
        private static CommandResult NotYet() => CommandResult.Reject(Reasons.Disabled, "That action is not available.");

        private static Building Live(WorldState s, int id)
        {
            var b = EconomyLogic.GetBuilding(s, id);
            if (b == null || b.State == BuildingState.Retired || b.State == BuildingState.Cancelled) return null;
            return b;
        }

        public static CommandResult CheckUpgradeBuilding(WorldState s, int faction, int buildingId)
        {
            if (s?.Economy == null) return EconomyRefusals.Reject(EconomyRefusals.UnknownBuilding);
            if (s.Factions != null && s.Factions.GameOver) return EconomyRefusals.Reject(EconomyRefusals.GameOver);
            var b = Live(s, buildingId);
            if (b == null) return EconomyRefusals.Reject(EconomyRefusals.UnknownBuilding);
            if (b.Faction != faction) return EconomyRefusals.Reject(EconomyRefusals.NotOwner);
            if (b.State != BuildingState.Active) return EconomyRefusals.Reject(EconomyRefusals.NotActive);
            if (b.Upgrading) return EconomyRefusals.Reject(EconomyRefusals.UpgradeInProgress, "building");
            var def = BuildingDefs.Get(b.Type);
            int next = (int)b.Grade + 1;
            if (def?.Grades == null || next >= def.Grades.Length || next > (int)Grade.Modern) return EconomyRefusals.Reject(EconomyRefusals.MaxGrade);
            var era = next == (int)Grade.Historic ? Era.Historic : Era.Modern;
            if (!AgesApi.AtLeast(s, era)) return EconomyRefusals.Reject(EconomyRefusals.WrongEra, AgesApi.EraName(era));
            if (next == (int)Grade.Modern) return NotYet(); // M1-2 checks credits (grade 2, no_credits).
            // Grade 1 is paid in goods the building's own carriers bring from its storehouse (no road): it needs a live camp.
            var home = EconomyLogic.GetSettlement(s, b.Settlement);
            if (home == null || home.State != SettlementState.Active) return EconomyRefusals.Reject(EconomyRefusals.NoSettlement);
            return CommandResult.Accept();
        }

        public static CommandResult CheckCancelUpgrade(WorldState s, int faction, int buildingId)
        {
            if (s?.Economy == null) return EconomyRefusals.Reject(EconomyRefusals.UnknownBuilding);
            if (s.Factions != null && s.Factions.GameOver) return EconomyRefusals.Reject(EconomyRefusals.GameOver);
            var b = Live(s, buildingId);
            if (b == null) return EconomyRefusals.Reject(EconomyRefusals.UnknownBuilding);
            if (b.Faction != faction) return EconomyRefusals.Reject(EconomyRefusals.NotOwner);
            if (!b.Upgrading) return EconomyRefusals.Reject(EconomyRefusals.NotUpgrading);
            return CommandResult.Accept();
        }

        public static CommandResult CheckUpgradeSettlement(WorldState s, int faction, int corner)
        {
            if (s?.Economy == null) return EconomyRefusals.Reject(EconomyRefusals.NotASettlement);
            if (s.Factions != null && s.Factions.GameOver) return EconomyRefusals.Reject(EconomyRefusals.GameOver);
            var st = EconomyLogic.SettlementAt(s, corner);
            if (st == null || st.Faction != faction || st.State != SettlementState.Active) return EconomyRefusals.Reject(EconomyRefusals.NotASettlement);
            if (st.Upgrade != UpgradeState.None) return EconomyRefusals.Reject(EconomyRefusals.UpgradeInProgress, st.IsCity ? "town" : null);
            if (!st.IsCity) return EconomyRefusals.Reject(EconomyRefusals.NotACity);
            if (st.IsDistrict) return EconomyRefusals.Reject(EconomyRefusals.MaxGrade);
            var era = st.IsTown ? Era.Modern : Era.Historic;
            if (!AgesApi.AtLeast(s, era)) return EconomyRefusals.Reject(EconomyRefusals.WrongEra, AgesApi.EraName(era));
            if (st.IsTown) return NotYet(); // M1-2 (Town to District, no_credits).
            return CommandResult.Accept();
        }

        // ---- applying (H1-2) ----

        /// <summary>The GradeDef a building is heading for, or null.</summary>
        public static GradeDef Target(Building b)
        {
            var def = BuildingDefs.Get(b.Type);
            int next = (int)b.Grade + 1;
            return def?.Grades != null && next < def.Grades.Length ? def.Grades[next] : null;
        }

        public static void StartBuildingUpgrade(WorldState s, Building b, EventBuffer ev)
        {
            b.Upgrading = true;
            b.UpgradeProgress = 0;
            System.Array.Clear(b.UpgradeDelivered, 0, b.UpgradeDelivered.Length);
            ev?.Add(new UpgradeStarted { BuildingId = b.Id, Grade = (Grade)((int)b.Grade + 1) });
        }

        /// <summary>Ends an upgrade without finishing it: goods already delivered go back to the storehouse, goods on the way too (CancelUpgrade, or the building went away).</summary>
        public static void AbortBuildingUpgrade(WorldState s, Building b, EventBuffer ev)
        {
            Logistics.OnSiteCancelled(s, PlaceKind.BuildingUpgrade, b.Id);
            var home = EconomyLogic.GetSettlement(s, b.Settlement);
            for (int w = 0; w < b.UpgradeDelivered.Length; w++)
            {
                if (home != null) home.Stock[w] += b.UpgradeDelivered[w];
                else { s.Economy.Ledger.Consumed[w] += b.UpgradeDelivered[w]; s.Economy.Ledger.Looted[w] += b.UpgradeDelivered[w]; }
                b.UpgradeDelivered[w] = 0;
            }
            b.Upgrading = false;
            b.UpgradeProgress = 0;
            b.UpgradeCredits = 0; // M1-2 releases the reserved credits here
            ev?.Add(new UpgradeCancelled { BuildingId = b.Id });
        }

        /// <summary>One tick of a running upgrade (ConstructionSystem): waits for all goods, then for a running cycle to end, then builds for the grade's ticks.</summary>
        public static void TickBuildingUpgrade(WorldState s, Building b, EventBuffer ev)
        {
            if (b.State != BuildingState.Active) { AbortBuildingUpgrade(s, b, ev); return; }
            var g = Target(b);
            if (g == null) { AbortBuildingUpgrade(s, b, ev); return; }
            if (!ConstructionSystem.Covered(b.UpgradeDelivered, g.Goods)) return;
            if (b.WorkProgress > 0) return; // the running cycle finishes first; none starts meanwhile
            b.UpgradeProgress++;
            if (b.UpgradeProgress < g.Ticks) return;
            var e = s.Economy;
            for (int w = 0; w < b.UpgradeDelivered.Length; w++)
            {
                int n = g.Goods[w];
                if (n > 0) { e.Ledger.Consumed[w] += n; e.Ledger.Construction[w] += n; }
                b.UpgradeDelivered[w] -= n;
            }
            b.Grade = g.To;
            b.Upgrading = false;
            b.UpgradeProgress = 0;
            ev?.Add(new BuildingUpgraded { BuildingId = b.Id, Grade = b.Grade });
        }

        // ---- the grade's effect, as numbers every system reads (H1-2) ----

        /// <summary>A grade field that is 0 means "as the grade below": the last non-zero value up to grade g, else the base.</summary>
        private static int Walk(BuildingDef def, Grade g, int baseValue, System.Func<GradeDef, int> field)
        {
            int v = baseValue;
            if (def.Grades == null) return v;
            for (int i = 1; i <= (int)g && i < def.Grades.Length; i++)
            {
                int x = field(def.Grades[i]);
                if (x > 0) v = x;
            }
            return v;
        }

        /// <summary>Ticks of one cycle of a building: its grade's, less the type's CycleBonusPermille (at least 1).</summary>
        public static int CycleTicks(EconomyTuning tu, BuildingDef def, Grade g)
        {
            int ticks = Walk(def, g, def.WorkTicks, x => x.CycleTicks);
            int type = (int)def.Type;
            int bonus = tu?.CycleBonusPermille != null && type >= 0 && type < tu.CycleBonusPermille.Length ? tu.CycleBonusPermille[type] : 0;
            if (bonus <= 0) return ticks;
            if (bonus > 900) bonus = 900;
            return System.Math.Max(1, (int)((long)ticks * (1000 - bonus) / 1000));
        }

        /// <summary>Wares a collector makes from one unit taken, at a grade.</summary>
        public static int YieldPerUnit(BuildingDef def, Grade g) => Walk(def, g, def.Yield, x => x.YieldPerUnit);

        /// <summary>Cycles of inputs a workshop keeps on hand (the tuning's InputBuffer unless the grade says otherwise).</summary>
        public static int InputBufferCycles(EconomyTuning tu, BuildingDef def, Grade g) => Walk(def, g, tu.InputBuffer, x => x.InputBufferCycles);

        /// <summary>Warriors a lodge may hold: WarriorsPerLodge, none above grade 0 (a barracks has no cap).</summary>
        public static int WarriorCap(EconomyTuning tu, Grade g) => g >= Grade.Historic ? int.MaxValue : tu.WarriorsPerLodge;

        // ---- settlements ----

        public static int[] SettlementUpgradeCost(EconomyTuning tu, Settlement st) => st.IsCity ? tu.TownCost : tu.CityCost;

        public static int SettlementUpgradeTicks(EconomyTuning tu, Settlement st) => st.IsCity ? tu.TownBuildTicks : tu.CityBuildTicks;

        /// <summary>Largest shipment a storehouse sends: a town 6, a district 8, else ShipmentSize.</summary>
        public static int ShipmentSize(EconomyTuning tu, Settlement st) =>
            st.IsDistrict ? tu.DistrictShipmentSize : st.IsTown ? tu.TownShipmentSize : tu.ShipmentSize;
    }

    /// <summary>
    /// The grade of a building and its effects for views and tests (PRD_AGES 5.6; H1-2): what a cycle takes now.
    /// </summary>
    public static class GradeApi
    {
        /// <summary>Ticks of one cycle of the building now (grade and CycleBonusPermille included); 0 for an unknown building.</summary>
        public static int CycleTicks(WorldState s, int buildingId)
        {
            var b = s == null ? null : EconomyLogic.GetBuilding(s, buildingId);
            var def = b == null ? null : BuildingDefs.Get(b.Type);
            return def == null ? 0 : GradeLogic.CycleTicks(s.Economy.Tuning, def, b.Grade);
        }

        /// <summary>Wares one mature unit becomes in a collector now; 0 for a non-collector or unknown building.</summary>
        public static int YieldPerUnit(WorldState s, int buildingId)
        {
            var b = s == null ? null : EconomyLogic.GetBuilding(s, buildingId);
            var def = b == null ? null : BuildingDefs.Get(b.Type);
            return def == null || !def.IsCollector ? 0 : GradeLogic.YieldPerUnit(def, b.Grade);
        }

        /// <summary>Cycles of inputs a workshop keeps on hand now.</summary>
        public static int InputBufferCycles(WorldState s, int buildingId)
        {
            var b = s == null ? null : EconomyLogic.GetBuilding(s, buildingId);
            var def = b == null ? null : BuildingDefs.Get(b.Type);
            return def == null ? 0 : GradeLogic.InputBufferCycles(s.Economy.Tuning, def, b.Grade);
        }

        /// <summary>The goods a building's next grade still needs (cost less delivered), by ware; empty when it is not upgrading.</summary>
        public static int[] UpgradeStillNeeded(WorldState s, int buildingId)
        {
            var b = s == null ? null : EconomyLogic.GetBuilding(s, buildingId);
            var g = b == null || !b.Upgrading ? null : GradeLogic.Target(b);
            var r = new int[Wares.Count];
            if (g == null) return r;
            for (int w = 0; w < r.Length; w++) r[w] = System.Math.Max(0, g.Goods[w] - b.UpgradeDelivered[w]);
            return r;
        }
    }

    internal sealed class UpgradeBuildingHandler : CommandHandler<UpgradeBuilding>
    {
        protected override CommandResult Validate(WorldState s, UpgradeBuilding c) => GradeLogic.CheckUpgradeBuilding(s, c.PlayerId, c.BuildingId);

        protected override CommandResult Apply(WorldState s, UpgradeBuilding c, EventBuffer ev)
        {
            var r = Validate(s, c);
            if (!r.Ok) return r;
            GradeLogic.StartBuildingUpgrade(s, EconomyLogic.GetBuilding(s, c.BuildingId), ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class CancelUpgradeHandler : CommandHandler<CancelUpgrade>
    {
        protected override CommandResult Validate(WorldState s, CancelUpgrade c) => GradeLogic.CheckCancelUpgrade(s, c.PlayerId, c.BuildingId);

        protected override CommandResult Apply(WorldState s, CancelUpgrade c, EventBuffer ev)
        {
            var r = Validate(s, c);
            if (!r.Ok) return r;
            GradeLogic.AbortBuildingUpgrade(s, EconomyLogic.GetBuilding(s, c.BuildingId), ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class UpgradeSettlementHandler : CommandHandler<UpgradeSettlement>
    {
        protected override CommandResult Validate(WorldState s, UpgradeSettlement c) => GradeLogic.CheckUpgradeSettlement(s, c.PlayerId, c.Corner);

        // City to Town: a SiteKind.City site on the settlement (TownUpgraded on completion); Town to District is M1-2.
        protected override CommandResult Apply(WorldState s, UpgradeSettlement c, EventBuffer ev)
        {
            var r = Validate(s, c);
            if (!r.Ok) return r;
            EconomyLogic.PlaceCitySite(s, c.PlayerId, c.Corner, ev);
            return CommandResult.Accept();
        }
    }
}
