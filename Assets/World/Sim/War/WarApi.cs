using System.Collections.Generic;
using World.Sim.Plague;

namespace World.Sim.War
{
    /// <summary>Read helpers for the war module. Static, null-safe, never mutate.</summary>
    public static class WarApi
    {
        public static WarSettlement SettlementAt(WorldState s, int corner)
        {
            var l = s?.War?.Settlements;
            if (l == null) return null;
            for (int i = 0; i < l.Count; i++) if (l[i].Corner == corner) return l[i];
            return null;
        }

        public static Lodge LodgeOf(WorldState s, int people)
        {
            var l = s?.War?.Lodges;
            if (l == null) return null;
            for (int i = 0; i < l.Count; i++) if (l[i].People == people) return l[i];
            return null;
        }

        public static Soldier SoldierById(WorldState s, int id)
        {
            var l = s?.War?.Soldiers;
            if (l == null) return null;
            for (int i = 0; i < l.Count; i++) if (l[i].Id == id) return l[i];
            return null;
        }

        public static bool IsRuin(WorldState s, int corner)
        {
            var l = s?.War?.Ruins;
            if (l == null) return false;
            for (int i = 0; i < l.Count; i++) if (l[i].Corner == corner) return true;
            return false;
        }

        // ---- stats (WA-2, WA-3)

        public static int MaxHealth(Soldier x) => WarData.BaseHealth + WarData.HealthPerLevel * x.Levels[Stat.Health];
        public static int AttackOf(Soldier x) => x.BaseAttack + WarData.AttackPerLevel * x.Levels[Stat.Attack];
        public static int DefencePct(Soldier x) => WarData.DefencePerLevel * x.Levels[Stat.Defence];
        public static int EvadePct(Soldier x) => WarData.BaseEvade + WarData.EvadePerLevel * x.Levels[Stat.Evade];
        public static Combatant CombatantOf(Soldier x) => new Combatant(x.Hp, AttackOf(x), DefencePct(x), EvadePct(x));
        public static Wear WearOf(Soldier x) => DuelRules.WearOf(x.Hp, MaxHealth(x));

        /// <summary>WA-3. Brute (Health), Raider (Attack), Shieldbearer (Defence), Skirmisher (Evade); Warrior when no stat strictly leads.</summary>
        public static SoldierLook LookOf(Soldier x)
        {
            int best = -1, bestLevel = -1;
            bool tie = false;
            for (int i = 0; i < Stat.Count; i++)
            {
                if (x.Levels[i] > bestLevel) { best = i; bestLevel = x.Levels[i]; tie = false; }
                else if (x.Levels[i] == bestLevel) tie = true;
            }
            if (tie || bestLevel <= 0) return SoldierLook.Warrior;
            switch (best)
            {
                case Stat.Health: return SoldierLook.Brute;
                case Stat.Attack: return SoldierLook.Raider;
                case Stat.Defence: return SoldierLook.Shieldbearer;
                default: return SoldierLook.Skirmisher;
            }
        }

        // ---- war state (WA-11)

        public static bool AtWar(WorldState s, int a, int b)
        {
            var w = s?.War;
            if (w?.AtWar == null || a == b || a < 0 || b < 0 || a >= w.PeopleCount || b >= w.PeopleCount) return false;
            return w.AtWar[a * w.PeopleCount + b];
        }

        /// <summary>The unique top scorer, or -1 when two or more share the top (or no points are set).</summary>
        public static int Leader(WorldState s)
        {
            var p = s?.War?.Points;
            if (p == null || p.Length == 0) return -1;
            int best = 0; bool tie = false;
            for (int i = 1; i < p.Length; i++)
            {
                if (p[i] > p[best]) { best = i; tie = false; }
                else if (p[i] == p[best]) tie = true;
            }
            return tie ? -1 : best;
        }

        // ---- positions

        /// <summary>Soldiers of a people standing (not stepping) at a corner.</summary>
        public static int PresentAt(WorldState s, int corner, int people)
        {
            int n = 0;
            var l = s?.War?.Soldiers;
            if (l == null) return 0;
            for (int i = 0; i < l.Count; i++)
                if (l[i].Corner == corner && !l[i].Moving && (people < 0 || l[i].People == people)) n++;
            return n;
        }

        /// <summary>The plague nodes (ascending) on the hexes that touch a corner: where a soldier steps to and from it (WA-17).</summary>
        public static List<int> NodesAtCorner(WorldState s, int corner)
        {
            var list = new List<int>();
            var topo = s?.Board?.Topology;
            if (topo == null || !topo.IsCorner(corner)) return list;
            foreach (int h in topo.VertexHexes(corner))
                foreach (int n in PlagueApi.NodesOfHex(s, h)) if (!list.Contains(n)) list.Add(n);
            list.Sort();
            return list;
        }

        /// <summary>The coarse wear state of a soldier id (Fresh for an unknown id).</summary>
        public static Wear WearOfId(WorldState s, int id)
        {
            var x = SoldierById(s, id);
            return x == null ? Wear.Fresh : WearOf(x);
        }

        public static int GoodsInStore(WarSettlement st)
        {
            int n = 0;
            foreach (int v in st.Raw) n += v;
            foreach (int v in st.Proc) n += v;
            return n;
        }
    }
}
