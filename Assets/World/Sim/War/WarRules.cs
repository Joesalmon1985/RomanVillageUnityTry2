using System;
using System.Collections.Generic;

namespace World.Sim.War
{
    /// <summary>
    /// The war rules (PRD_GODBOARD PR-1..PR-4, WA-1..WA-18) as plain functions. <see cref="Tick"/> advances everything one step;
    /// the rest is the explicit API the turn engine calls. Deterministic from <see cref="WarState.Rng"/>.
    /// </summary>
    public static partial class WarRules
    {
        // ---- settlements (PR-1, WA-4)

        /// <summary>
        /// Adds a settlement for a people with its huts and its starting garrison (1 soldier, 2 for a city, WA-4). A home camp also
        /// gets the people's lodge (WA-1). Refused (false) for a bad corner or people, an occupied corner, a ruin, or a second lodge.
        /// </summary>
        public static bool AddSettlement(WorldState s, int people, int corner, bool isCity = false, bool isHome = false, EventBuffer ev = null)
        {
            var w = s?.War;
            if (w == null || people < 0 || people >= w.PeopleCount) return false;
            var topo = s.Board?.Topology;
            if (topo == null || !topo.IsCorner(corner)) return false;
            if (WarApi.SettlementAt(s, corner) != null || WarApi.IsRuin(s, corner)) return false;
            if (isHome && WarApi.LodgeOf(s, people) != null) return false;
            var st = new WarSettlement { People = people, Corner = corner, IsCity = isCity, IsHome = isHome };
            foreach (int g in WarData.HutGoodsAt(s.Board, corner))
                st.Huts.Add(new Hut { Good = g, BuildLeft = WarData.HutBuildTicks, CycleLeft = WarData.HutCycleTicks });
            w.Settlements.Add(st);
            if (isHome) w.Lodges.Add(new Lodge { People = people, Corner = corner });
            NewSoldier(w, people, corner, false, null);
            if (isCity) NewSoldier(w, people, corner, false, null);
            return true;
        }

        /// <summary>A settlement becomes a city: +1 garrison soldier (WA-4). False if there is none or it already is a city.</summary>
        public static bool UpgradeCity(WorldState s, int corner, EventBuffer ev = null)
        {
            var st = WarApi.SettlementAt(s, corner);
            if (st == null || st.IsCity) return false;
            st.IsCity = true;
            NewSoldier(s.War, st.People, corner, false, null);
            return true;
        }

        /// <summary>Removes a settlement without a ruin (the caller's rules, not a burning). Its soldiers step out onto a node of its hexes, or vanish if there is none.</summary>
        public static bool RemoveSettlement(WorldState s, int corner)
        {
            var w = s?.War;
            var st = WarApi.SettlementAt(s, corner);
            if (w == null || st == null) return false;
            w.Settlements.Remove(st);
            if (st.IsHome) w.Lodges.RemoveAll(l => l.Corner == corner);
            EjectSoldiers(s, corner, null, null);
            return true;
        }

        /// <summary>CL-2: a collector delivers n units of a raw good to the settlement's store.</summary>
        public static bool Deliver(WorldState s, int corner, int rawGood, int n = 1)
        {
            var st = WarApi.SettlementAt(s, corner);
            if (st == null || rawGood < 0 || rawGood >= WarData.RawCount || n <= 0) return false;
            st.Raw[rawGood] += n;
            return true;
        }

        internal static Soldier NewSoldier(WarState w, int people, int corner, bool field, int[] levels)
        {
            var x = new Soldier
            {
                Id = w.NextSoldierId++,
                People = people,
                Corner = corner,
                BaseAttack = w.Rng.NextRange(WarData.AttackMin, WarData.AttackMax + 1),
                Field = field,
            };
            if (levels != null) Array.Copy(levels, x.Levels, Stat.Count);
            x.Hp = WarApi.MaxHealth(x);
            w.Soldiers.Add(x);
            return x;
        }

        // ---- war state (WA-11)

        /// <summary>Recomputes who is at war from the points (WA-11; ties per <see cref="WarState.TiedLeadersFight"/>). Emits <see cref="WarStateChanged"/> when the matrix changes; ends duels of peoples now at peace.</summary>
        public static void SetPoints(WorldState s, int[] points, EventBuffer ev)
        {
            var w = s?.War;
            if (w == null || points == null) return;
            int n = w.PeopleCount;
            var pts = new int[n];
            for (int i = 0; i < n && i < points.Length; i++) pts[i] = points[i];
            w.Points = pts;
            var war = new bool[n * n];
            int top = int.MinValue;
            foreach (int p in pts) top = Math.Max(top, p);
            var leaders = new List<int>();
            for (int i = 0; i < n; i++) if (pts[i] == top) leaders.Add(i);
            bool everyoneTied = leaders.Count == n;
            if (leaders.Count >= 2 && everyoneTied && w.TiedLeadersFight == TieRule.OnlyWhenNotEveryoneTied)
            {
                // G0-3 tuning flag: everyone shares the top score, so nobody is at war.
            }
            else if (leaders.Count >= 2)
            {
                foreach (int a in leaders) foreach (int b in leaders) if (a != b) war[a * n + b] = true;
            }
            else if (n >= 2)
            {
                int lead = leaders[0];
                int second = int.MinValue;
                for (int i = 0; i < n; i++) if (i != lead) second = Math.Max(second, pts[i]);
                if (pts[lead] - second >= WarData.WarLead)
                    for (int i = 0; i < n; i++) if (i != lead) { war[i * n + lead] = true; war[lead * n + i] = true; }
            }
            bool changed = w.AtWar == null || w.AtWar.Length != war.Length;
            if (!changed) for (int i = 0; i < war.Length; i++) if (war[i] != w.AtWar[i]) { changed = true; break; }
            w.AtWar = war;
            if (!changed) return;
            var pairs = new List<int>();
            for (int a = 0; a < n; a++) for (int b = a + 1; b < n; b++) if (war[a * n + b]) { pairs.Add(a); pairs.Add(b); }
            ev?.Add(new WarStateChanged { Pairs = pairs.ToArray(), Leader = WarApi.Leader(s) });
        }

        // ---- the tick

        /// <summary>Advances the module one step.</summary>
        public static void Tick(WorldState s, EventBuffer ev, Func<WorldState, int, bool> linked = null)
        {
            var w = s?.War;
            if (w == null || w.Stopped) return;
            w.Clock++;
            TickProcessing(s, ev, linked);
            TickLodges(s, ev);
            TickMovement(s, ev);
            TickEngage(s, ev);
            TickDuels(s, ev);
            TickPurge(s, ev);
            TickRaids(s, ev);
            if (w.Clock % WarData.ArmyPlanTicks == 0) PlanArmies(s, ev);
            TickDepart(s, ev);
            TickRuins(s, ev);
        }

        // ---- processing (PR-1..PR-3)

        private static void TickProcessing(WorldState s, EventBuffer ev, Func<WorldState, int, bool> linked)
        {
            var w = s.War;
            foreach (var st in w.Settlements)
            {
                foreach (var h in st.Huts)
                {
                    if (h.BuildLeft > 0)
                    {
                        h.BuildLeft--;
                        if (h.BuildLeft == 0) { h.CycleLeft = WarData.HutCycleTicks; ev?.Add(new HutBuilt { Corner = st.Corner, Good = h.Good }); }
                        continue;
                    }
                    int a = WarData.GoodRawA[h.Good], b = WarData.GoodRawB[h.Good];
                    if (st.Raw[a] < 1 || st.Raw[b] < 1) continue;
                    h.CycleLeft--;
                    if (h.CycleLeft > 0) continue;
                    st.Raw[a]--; st.Raw[b]--;
                    st.Proc[h.Good]++;
                    h.CycleLeft = WarData.HutCycleTicks;
                    ev?.Add(new GoodProcessed { Corner = st.Corner, Good = h.Good });
                }
                // PR-3 stand-in: the lodge settlement hands goods over at once, others only when linked.
                var lodge = WarApi.LodgeOf(s, st.People);
                if (lodge == null) continue;
                if (st.CarryGood >= 0)
                {
                    if (--st.CarryLeft <= 0) { Lodge(lodge, st.CarryGood, ev); st.CarryGood = -1; }
                    continue;
                }
                int g = -1;
                for (int i = 0; i < WarData.GoodCount; i++) if (st.Proc[i] > 0) { g = i; break; }
                if (g < 0) continue;
                if (st.Corner == lodge.Corner) { st.Proc[g]--; Lodge(lodge, g, ev); }
                else if (linked == null || linked(s, st.Corner)) { st.Proc[g]--; st.CarryGood = g; st.CarryLeft = WarData.CarryTicks; }
            }
        }

        private static void Lodge(Lodge l, int good, EventBuffer ev)
        {
            l.Queue.Add(good);
            ev?.Add(new GoodLodged { People = l.People, Corner = l.Corner, Good = good });
        }

        // ---- the lodge (WA-1)

        private static void TickLodges(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            foreach (var l in w.Lodges)
            {
                if (l.Training.Count == 0 && l.Queue.Count >= WarData.GoodsPerSoldier)
                {
                    l.Training.AddRange(l.Queue.GetRange(0, WarData.GoodsPerSoldier));
                    l.Queue.RemoveRange(0, WarData.GoodsPerSoldier);
                    l.TrainLeft = WarData.TrainTicks;
                    ev?.Add(new TrainingStarted { People = l.People });
                }
                if (l.Training.Count == 0) continue;
                if (--l.TrainLeft > 0) continue;
                var levels = new int[Stat.Count];
                foreach (int g in l.Training)
                {
                    int stat = WarData.GoodStat[g];
                    levels[stat] = Math.Min(WarData.MaxLevel, levels[stat] + 1);
                }
                l.Training.Clear();
                var x = NewSoldier(w, l.People, l.Corner, true, levels);
                w.Trained++;
                ev?.Add(new SoldierTrained { Soldier = x.Id, People = x.People, Corner = x.Corner, Look = WarApi.LookOf(x) });
            }
        }

        // ---- ruins (WA-14)

        private static void TickRuins(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            for (int i = w.Ruins.Count - 1; i >= 0; i--)
                if (w.Clock >= w.Ruins[i].ExpireClock)
                {
                    int c = w.Ruins[i].Corner;
                    w.Ruins.RemoveAt(i);
                    ev?.Add(new RuinCleared { Corner = c });
                }
        }
    }
}
