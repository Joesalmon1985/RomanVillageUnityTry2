using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The plague on its own graph (PRD_GODBOARD section 7). Null until <see cref="Plague.PlagueModule.InitState"/> runs.</summary>
        public Plague.PlagueState Plague;
    }
}

namespace World.Sim.Plague
{
    /// <summary>
    /// WorldState.Plague. Plain serializable fields only. Node ids are 1-based (Appendix B); per-node arrays are indexed
    /// by id - 1. Element order: 0 Earth, 1 Fire, 2 Water, 3 Air (Campaign.Element). Lists are null until InitState so a
    /// loaded save is never appended to twice.
    /// </summary>
    public sealed class PlagueState
    {
        /// <summary>Demons on each node, 0..3 (PL-8).</summary>
        public int[] Demons;
        /// <summary>Demons by kind: index (id - 1) * 4 + element. Sums to Demons. A spread demon keeps its source's kind.</summary>
        public int[] KindCount;
        /// <summary>The infection deck, node ids. Index 0 is the top, the last card is the bottom (PL-5, PL-6).</summary>
        public List<int> Deck;
        public List<int> Discards;
        /// <summary>Position on PlagueRules.RateTrack (PL-7).</summary>
        public int RateIndex;
        public int Outbreaks;
        public bool IslandFell;
        /// <summary>The plague has ended (HI-8): everything is cleared and stopped.</summary>
        public bool Ended;
        public bool[] Cured;
        public bool[] Eradicated;
        public bool[] Strengthened;
        /// <summary>First tick at which the next banish is allowed (PL-11).</summary>
        public long BanishReadyTick;
        /// <summary>Infection rounds drawn so far. An epidemic does nothing until it reaches PlagueRules.GraceRounds (PL-6).</summary>
        public int Rounds;
        public int Epidemics;
        /// <summary>
        /// Tuning field (PL-5, balance F1): the infection draw happens in every N-th round (1 = every round). A new god board game
        /// gets <see cref="PlagueRules.DefaultInfectEvery"/> (3); a save from before version 9 lacks it and keeps 1 (the old behaviour).
        /// </summary>
        public int InfectEvery = 1;
        public int Banished;
        public Rng Rng;
    }
}
