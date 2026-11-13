namespace World.Sim.Military
{
    /// <summary>Military tuning with defaults in code (serialised in <c>MilitaryState.Tuning</c>).</summary>
    public sealed class MilitaryTuning
    {
        // ---- Power ----
        /// <summary>Power from each fuelled power plant.</summary>
        public int SupplyPerPlant = 4;
        public int SmelterDemand = 2;
        public int BarracksDemand = 2;

        // ---- Barracks ----
        /// <summary>Player-facing name of the one infantry type.</summary>
        public string UnitName = "Wardens";
        /// <summary>A soldier costs 1 Weapons + 1 Rations, delivered to the barracks (design section 4).</summary>
        public int UnitRations = 1;
        public int UnitWeapons = 1;
        public int TrainingTicks = 150;
        public int MaxQueue = 8;
        public int MaxSquadSize = 4;

        // ---- Movement and combat ----
        /// <summary>
        /// Ticks a squad needs to walk one road segment (20 m); squads walk the fine flag graph. 20 (was 40 per
        /// old 20 m edge): the outpost is now 12-15 segments away instead of about 6 edges, so marches keep their old length.
        /// </summary>
        public int TicksPerEdge = 20;
        public int UnitHp = 10;
        public int UnitDamage = 3;
        public int AttackCooldown = 10;

        // ---- Rival ----
        public int OutpostHp = 60;
        public int GarrisonBase = 1;
        public int GarrisonPerAge = 1;
        public int GuardHp = 8;
        public int GuardDamage = 2;
        /// <summary>Raids start in this age.</summary>
        public int RaidFromAge = 2;
        public int RaidSize = 1;
    }
}
