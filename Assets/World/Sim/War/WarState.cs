using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The war module's state (PRD_GODBOARD section 6). Null until <see cref="War.WarModule.InitState"/> runs.</summary>
        public War.WarState War;
    }
}

namespace World.Sim.War
{
    /// <summary>
    /// WorldState.War. Plain serializable fields only. Peoples are 0-based indices below <see cref="PeopleCount"/>. Plague node
    /// ids are 1-based. A path step is a node id (positive) or a settlement corner encoded as -(corner + 1).
    /// </summary>
    public sealed class WarState
    {
        public int PeopleCount;
        /// <summary>The module's own clock, one per <see cref="WarRules.Tick"/> call. Ruins and timers count in it.</summary>
        public long Clock;
        public Rng Rng;
        public List<WarSettlement> Settlements = new List<WarSettlement>();
        public List<Lodge> Lodges = new List<Lodge>();
        public List<Soldier> Soldiers = new List<Soldier>();
        public List<Duel> Duels = new List<Duel>();
        public List<Ruin> Ruins = new List<Ruin>();
        public int NextSoldierId = 1;
        public int NextDuelId = 1;
        /// <summary>Last points given to <see cref="WarRules.SetPoints"/>, one per people.</summary>
        public int[] Points;
        /// <summary>Symmetric war matrix, index a * PeopleCount + b (WA-11).</summary>
        public bool[] AtWar;
        public int Trained, Died, Purged, Burned, Looted;
        /// <summary>
        /// Tuning flag for WA-11's tie rule (G0-3; owner has not ruled yet). Literal: two or more peoples sharing the top score
        /// are at war, so a fresh game with everyone tied is at war. OnlyWhenNotEveryoneTied (default): tied leaders fight only
        /// when not every people shares the top score.
        /// </summary>
        public TieRule TiedLeadersFight = TieRule.OnlyWhenNotEveryoneTied;
        /// <summary>True once the war has been stopped for good (G0-3: the island fell, PL-10). Tick then does nothing.</summary>
        public bool Stopped;
    }

    /// <summary>How WA-11 treats peoples tied at the top (see <see cref="WarState.TiedLeadersFight"/>).</summary>
    public enum TieRule
    {
        /// <summary>WA-11 read literally: any tie at the top is a war between the tied peoples.</summary>
        Literal = 0,
        /// <summary>Tied leaders fight only when at least one people is below the top score.</summary>
        OnlyWhenNotEveryoneTied = 1,
        /// <summary>G1-3 option, not the default: peoples tied at the top never fight; only a single leader WarLead ahead is at war.</summary>
        NeverWhenTied = 2,
    }

    public sealed class WarSettlement
    {
        public int People;
        public int Corner;
        public bool IsCity;
        /// <summary>A home camp: it is looted, never burned (WA-15).</summary>
        public bool IsHome;
        /// <summary>Raw goods delivered by the collectors, index = raw good (WarData).</summary>
        public int[] Raw = new int[WarData.RawCount];
        /// <summary>Processed goods in store, index = good.</summary>
        public int[] Proc = new int[WarData.GoodCount];
        public List<Hut> Huts = new List<Hut>();
        /// <summary>Processed good on its way to the lodge (-1 none) and ticks left (PR-3 stand-in).</summary>
        public int CarryGood = -1;
        public int CarryLeft;
    }

    public sealed class Hut
    {
        /// <summary>The processed good it makes.</summary>
        public int Good;
        /// <summary>Ticks until built (0 = built, PR-1).</summary>
        public int BuildLeft;
        /// <summary>Ticks until the next good, counted only while both inputs are in store (PR-2).</summary>
        public int CycleLeft;
    }

    /// <summary>A people's war lodge (WA-1).</summary>
    public sealed class Lodge
    {
        public int People;
        public int Corner;
        /// <summary>Processed goods delivered, in arrival order, waiting for training.</summary>
        public List<int> Queue = new List<int>();
        /// <summary>The goods of the soldier in training (empty = none) and ticks left.</summary>
        public List<int> Training = new List<int>();
        public int TrainLeft;
    }

    public enum OrderKind { None = 0, Purge = 1, Attack = 2, Return = 3 }

    public sealed class Soldier
    {
        public int Id;
        public int People;
        /// <summary>Level per stat (Stat.*), 0..3 each (WA-1).</summary>
        public int[] Levels = new int[Stat.Count];
        /// <summary>Attack before levels, 12..16 (WA-2).</summary>
        public int BaseAttack;
        public int Hp;
        /// <summary>Settlement corner the soldier stands at, or -1.</summary>
        public int Corner = -1;
        /// <summary>Plague node the soldier stands on (1-based), or 0.</summary>
        public int Node;
        /// <summary>While a step is under way, Corner is -1 and Node is 0.</summary>
        public bool Moving;
        public int MoveLeft;
        /// <summary>Remaining steps: node ids, or -(corner + 1) for a settlement.</summary>
        public List<int> Path = new List<int>();
        public OrderKind Order;
        public int OrderTarget;
        public int PurgeLeft;
        /// <summary>Id of the duel it is in, 0 for none.</summary>
        public int DuelId;
        /// <summary>The last node it stood on before stepping into a settlement (where it withdraws to).</summary>
        public int PrevNode;
        /// <summary>Trained at the lodge (WA-6) as opposed to a base or city garrison soldier.</summary>
        public bool Field;
    }

    /// <summary>A one-on-one duel (WA-7, WA-8). Turn 0: A swings next, 1: B.</summary>
    public sealed class Duel
    {
        public int Id;
        public int A, B;
        public int Turn;
        public int SwingLeft;
    }

    /// <summary>A burned settlement's ruin (WA-14).</summary>
    public sealed class Ruin
    {
        public int Corner;
        public int People;
        public long ExpireClock;
    }
}
