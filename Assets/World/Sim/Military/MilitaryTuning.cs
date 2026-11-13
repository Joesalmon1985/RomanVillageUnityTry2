namespace World.Sim.Military
{
    /// <summary>Military tuning with defaults in code (serialised in <c>MilitaryState.Tuning</c>).</summary>
    public sealed class MilitaryTuning
    {
        // ---- Power ----
        /// <summary>Power from each fuelled power plant.</summary>
        public int SupplyPerPlant = 4;
        public int RefineryDemand = 2;
        public int BarracksDemand = 2;

        // ---- Barracks ----
        /// <summary>Player-facing name of the one infantry type.</summary>
        public string UnitName = "Wardens";
        public int UnitRations = 1;
        public int UnitIngots = 1;
        public int TrainingTicks = 150;
        public int MaxQueue = 8;
        public int MaxSquadSize = 4;

        // ---- Movement and combat ----
        public int TicksPerEdge = 40;
        public int UnitHp = 10;
        public int UnitDamage = 3;
        public int AttackCooldown = 10;

        // ---- Rival ----
        public int OutpostHp = 60;
        public int GarrisonBase = 2;
        public int GarrisonPerAge = 1;
        public int GuardHp = 8;
        public int GuardDamage = 2;
        /// <summary>Raids start in this age.</summary>
        public int RaidFromAge = 2;
        public int RaidSize = 2;
    }
}
