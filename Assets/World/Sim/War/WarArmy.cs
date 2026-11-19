using System.Collections.Generic;
using World.Sim.Plague;

namespace World.Sim.War
{
    /// <summary>Movement, duels, purging, raids and the army brain (WA-5..WA-18).</summary>
    public static partial class WarRules
    {
        private const int NodeSlots = PlagueGraphData.NodeCount + 1;

        private static int CornerStep(int corner) => -(corner + 1);
        private static int StepCorner(int step) => -step - 1;

        // ---- removal helpers

        /// <summary>Removes a soldier, ending its duel (the other soldier is freed).</summary>
        internal static void RemoveSoldier(WorldState s, Soldier x)
        {
            var w = s.War;
            if (x.DuelId != 0)
            {
                var d = w.Duels.Find(q => q.Id == x.DuelId);
                if (d != null)
                {
                    var other = WarApi.SoldierById(s, d.A == x.Id ? d.B : d.A);
                    if (other != null) other.DuelId = 0;
                    w.Duels.Remove(d);
                }
            }
            w.Soldiers.Remove(x);
        }

        private static void Kill(WorldState s, Soldier victim, Soldier killer, EventBuffer ev)
        {
            ev?.Add(new SoldierDied { Soldier = victim.Id, People = victim.People, Killer = killer?.Id ?? 0, KillerPeople = killer?.People ?? -1, Node = victim.Node, Corner = victim.Corner });
            s.War.Died++;
            RemoveSoldier(s, victim);
        }

        /// <summary>Puts a soldier standing at a corner back on a node of its hexes (where it came from if possible). Vanishes if the corner touches no node.</summary>
        private static void Withdraw(WorldState s, Soldier x)
        {
            var near = WarApi.NodesAtCorner(s, x.Corner);
            x.Path.Clear(); x.Order = OrderKind.None; x.OrderTarget = 0; x.PurgeLeft = 0;
            if (near.Count == 0) { RemoveSoldier(s, x); return; }
            if (x.DuelId != 0) EndDuelOf(s, x, null, "withdrawn");
            x.Node = near.Contains(x.PrevNode) ? x.PrevNode : near[0];
            x.Corner = -1;
        }

        private static void EndDuelOf(WorldState s, Soldier x, EventBuffer ev, string reason)
        {
            var w = s.War;
            var d = w.Duels.Find(q => q.Id == x.DuelId);
            if (d == null) { x.DuelId = 0; return; }
            var a = WarApi.SoldierById(s, d.A); var b = WarApi.SoldierById(s, d.B);
            if (a != null) a.DuelId = 0;
            if (b != null) b.DuelId = 0;
            w.Duels.Remove(d);
            ev?.Add(new DuelEnded { Duel = d.Id, A = d.A, B = d.B, Reason = reason });
        }

        /// <summary>Moves the soldiers standing at a corner (or just <paramref name="only"/>) onto a node; used when a settlement goes away.</summary>
        private static void EjectSoldiers(WorldState s, int corner, EventBuffer ev, Soldier only)
        {
            foreach (var x in s.War.Soldiers.ToArray())
                if (x.Corner == corner && !x.Moving && (only == null || x == only)) Withdraw(s, x);
        }

        // ---- movement (WA-17, WA-18)

        private static void TickMovement(WorldState s, EventBuffer ev)
        {
            foreach (var x in s.War.Soldiers.ToArray())
            {
                if (!x.Moving) continue;
                if (--x.MoveLeft > 0) continue;
                int loc = x.Path[0];
                x.Path.RemoveAt(0);
                x.Moving = false;
                if (loc > 0) { x.Node = loc; x.Corner = -1; }
                else
                {
                    x.Corner = StepCorner(loc); x.Node = 0;
                    if (WarApi.SettlementAt(s, x.Corner) == null) { Withdraw(s, x); continue; }
                }
                if (x.Path.Count > 0) continue;
                switch (x.Order)
                {
                    case OrderKind.Purge:
                        if (x.Node > 0 && PlagueApi.DemonsAt(s, x.Node) > 0) x.PurgeLeft = WarData.PurgeTicks;
                        else { x.Order = OrderKind.None; x.OrderTarget = 0; }
                        break;
                    case OrderKind.Return:
                        x.Order = OrderKind.None; x.OrderTarget = 0;
                        break;
                }
            }
        }

        private static void Cancel(Soldier x)
        {
            x.Path.Clear(); x.Order = OrderKind.None; x.OrderTarget = 0;
        }

        private static void TickDepart(WorldState s, EventBuffer ev)
        {
            foreach (var x in s.War.Soldiers.ToArray())
            {
                if (x.Moving || x.DuelId != 0 || x.Path.Count == 0 || x.PurgeLeft > 0) continue;
                if (x.Order == OrderKind.Purge && PlagueApi.DemonsAt(s, x.OrderTarget) == 0) { Cancel(x); continue; }
                int next = x.Path[0];
                if (x.Corner >= 0)
                {
                    var here = WarApi.SettlementAt(s, x.Corner);
                    // WA-5, WA-6: someone else must stay behind.
                    if (here != null && here.People == x.People && WarApi.PresentAt(s, x.Corner, x.People) < 2) { Cancel(x); continue; }
                }
                if (next > 0)
                {
                    bool finalPurge = x.Path.Count == 1 && x.Order == OrderKind.Purge && x.OrderTarget == next;
                    if (!PlagueApi.CanWalkThrough(s, next) && !finalPurge) { Cancel(x); continue; }
                }
                else
                {
                    var there = WarApi.SettlementAt(s, StepCorner(next));
                    if (there == null) { Cancel(x); continue; }
                    if (x.Order == OrderKind.Attack && !WarApi.AtWar(s, x.People, there.People)) { Cancel(x); continue; }
                }
                int from = x.Node > 0 ? x.Node : CornerStep(x.Corner);
                if (x.Node > 0) x.PrevNode = x.Node;
                x.Node = 0; x.Corner = -1;
                x.Moving = true; x.MoveLeft = WarData.MoveTicks;
                ev?.Add(new SoldierStepped { Soldier = x.Id, From = from, To = next, Ticks = WarData.MoveTicks });
            }
        }

        // ---- duels (WA-7, WA-8, WA-12)

        private static void TickEngage(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            bool anyWar = false;
            if (w.AtWar != null) foreach (bool b in w.AtWar) if (b) { anyWar = true; break; }
            if (!anyWar) return;
            var groups = new SortedDictionary<int, List<Soldier>>();
            foreach (var x in w.Soldiers)
            {
                if (x.Moving || x.DuelId != 0) continue;
                int key = x.Node > 0 ? x.Node : x.Corner >= 0 ? CornerStep(x.Corner) : 0;
                if (key == 0) continue;
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<Soldier>();
                l.Add(x);
            }
            foreach (var kv in groups)
            {
                var l = kv.Value;
                for (int i = 0; i < l.Count; i++)
                {
                    var a = l[i];
                    if (a.DuelId != 0) continue;
                    for (int j = i + 1; j < l.Count; j++)
                    {
                        var b = l[j];
                        if (b.DuelId != 0 || !WarApi.AtWar(s, a.People, b.People)) continue;
                        var d = new Duel { Id = w.NextDuelId++, A = a.Id, B = b.Id, Turn = 0, SwingLeft = WarData.SwingTicks };
                        w.Duels.Add(d);
                        a.DuelId = b.DuelId = d.Id;
                        a.PurgeLeft = b.PurgeLeft = 0;
                        ev?.Add(new DuelStarted { Duel = d.Id, A = a.Id, B = b.Id, Node = a.Node, Corner = a.Corner });
                        break;
                    }
                }
            }
        }

        private static void TickDuels(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            foreach (var d in w.Duels.ToArray())
            {
                var a = WarApi.SoldierById(s, d.A); var b = WarApi.SoldierById(s, d.B);
                if (a == null || b == null)
                {
                    if (a != null) a.DuelId = 0;
                    if (b != null) b.DuelId = 0;
                    w.Duels.Remove(d);
                    continue;
                }
                if (!WarApi.AtWar(s, a.People, b.People))
                {
                    a.DuelId = b.DuelId = 0;
                    w.Duels.Remove(d);
                    ev?.Add(new DuelEnded { Duel = d.Id, A = d.A, B = d.B, Reason = "peace" });
                    continue;
                }
                if (--d.SwingLeft > 0) continue;
                var att = d.Turn == 0 ? a : b;
                var tgt = d.Turn == 0 ? b : a;
                int dmg = DuelRules.Swing(WarApi.CombatantOf(att), WarApi.CombatantOf(tgt), w.Rng, out bool hit);
                tgt.Hp -= dmg;
                if (tgt.Hp < 0) tgt.Hp = 0;
                ev?.Add(new DuelSwing { Duel = d.Id, Attacker = att.Id, Target = tgt.Id, Hit = hit, Damage = dmg, TargetHp = tgt.Hp, TargetWear = WarApi.WearOf(tgt) });
                if (tgt.Hp <= 0)
                {
                    att.DuelId = 0;
                    tgt.DuelId = 0;
                    w.Duels.Remove(d);
                    Kill(s, tgt, att, ev);
                }
                else { d.Turn ^= 1; d.SwingLeft = WarData.SwingTicks; }
            }
        }

        // ---- purging (WA-10)

        private static void TickPurge(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            foreach (var x in w.Soldiers)
            {
                if (x.PurgeLeft <= 0 || x.Moving || x.DuelId != 0) continue;
                if (x.Node <= 0 || PlagueApi.DemonsAt(s, x.Node) == 0) { x.PurgeLeft = 0; Cancel(x); continue; }
                if (--x.PurgeLeft > 0) continue;
                if (PlagueRules.BanishAt(s, x.Node, -1, ev))
                {
                    w.Purged++;
                    ev?.Add(new DemonPurged { Soldier = x.Id, People = x.People, Node = x.Node });
                }
                if (PlagueApi.DemonsAt(s, x.Node) > 0 && s.Plague != null && !s.Plague.Ended) x.PurgeLeft = WarData.PurgeTicks;
                else { x.PurgeLeft = 0; Cancel(x); }
            }
        }

        // ---- burning and looting (WA-14, WA-15)

        private static void TickRaids(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            foreach (var x in w.Soldiers.ToArray())
            {
                if (x.Moving || x.DuelId != 0 || x.Corner < 0) continue;
                var st = WarApi.SettlementAt(s, x.Corner);
                if (st != null && st.People != x.People && WarApi.AtWar(s, st.People, x.People) && WarApi.PresentAt(s, x.Corner, st.People) == 0)
                {
                    if (st.IsHome) Loot(s, st, x, ev); else Burn(s, st, x, ev);
                }
                else if (x.Order == OrderKind.Attack && x.Path.Count == 0) Cancel(x);
            }
        }

        private static void Burn(WorldState s, WarSettlement st, Soldier by, EventBuffer ev)
        {
            var w = s.War;
            int goods = WarApi.GoodsInStore(st);
            w.Settlements.Remove(st);
            w.Ruins.Add(new Ruin { Corner = st.Corner, People = st.People, ExpireClock = w.Clock + WarData.RuinTicks });
            w.Burned++;
            ev?.Add(new SettlementBurned { Corner = st.Corner, People = st.People, AttackerPeople = by.People, Soldier = by.Id, WasCity = st.IsCity, PointsLost = st.IsCity ? 2 : 1, GoodsLost = goods });
            EjectSoldiers(s, st.Corner, ev, null);
        }

        private static void Loot(WorldState s, WarSettlement st, Soldier by, EventBuffer ev)
        {
            int goods = WarApi.GoodsInStore(st);
            System.Array.Clear(st.Raw, 0, st.Raw.Length);
            System.Array.Clear(st.Proc, 0, st.Proc.Length);
            st.CarryGood = -1; st.CarryLeft = 0;
            if (goods > 0)
            {
                s.War.Looted++;
                ev?.Add(new CampLooted { Corner = st.Corner, People = st.People, AttackerPeople = by.People, Soldier = by.Id, GoodsLost = goods });
            }
            Withdraw(s, by);
        }

        // ---- the army brain (WA-13)

        private sealed class Reach
        {
            public readonly int[] Dist = new int[NodeSlots];
            public readonly int[] Prev = new int[NodeSlots];
        }

        private static Reach Bfs(WorldState s, Soldier x, bool honourBlocks)
        {
            var r = new Reach();
            for (int i = 0; i < NodeSlots; i++) r.Dist[i] = -1;
            var q = new Queue<int>();
            if (x.Node > 0) { r.Dist[x.Node] = 0; q.Enqueue(x.Node); }
            else if (x.Corner >= 0)
                foreach (int n in WarApi.NodesAtCorner(s, x.Corner)) { r.Dist[n] = 1; q.Enqueue(n); }
            while (q.Count > 0)
            {
                int n = q.Dequeue();
                if (honourBlocks && n != x.Node && !PlagueApi.CanWalkThrough(s, n)) continue;
                foreach (int nb in PlagueApi.Neighbours(n))
                    if (r.Dist[nb] < 0) { r.Dist[nb] = r.Dist[n] + 1; r.Prev[nb] = n; q.Enqueue(nb); }
            }
            return r;
        }

        private static List<int> Route(Reach r, int goal, Soldier x)
        {
            var l = new List<int>();
            for (int n = goal; n != 0; n = r.Prev[n]) l.Add(n);
            l.Reverse();
            if (x.Node > 0 && l.Count > 0 && l[0] == x.Node) l.RemoveAt(0);
            return l;
        }

        private static void PlanArmies(WorldState s, EventBuffer ev)
        {
            var w = s.War;
            var topo = s.Board?.Topology;
            if (topo == null) return;
            int[] demons = new int[NodeSlots];
            int[] nodeHex = new int[NodeSlots];
            for (int n = 1; n < NodeSlots; n++)
            {
                demons[n] = PlagueApi.DemonsAt(s, n);
                nodeHex[n] = topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[n - 1], PlagueGraphData.HexR[n - 1]));
            }
            // which peoples have a settlement touching each hex
            var touched = new int[topo.HexCount];
            foreach (var st in w.Settlements)
                foreach (int h in topo.VertexHexes(st.Corner)) touched[h] |= 1 << st.People;
            int leader = WarApi.Leader(s);
            int notLeader = leader < 0 ? -1 : ~(1 << leader);

            for (int p = 0; p < w.PeopleCount; p++)
            {
                var assigned = new int[NodeSlots];
                foreach (var x in w.Soldiers) if (x.People == p && x.Order == OrderKind.Purge) assigned[x.OrderTarget]++;
                var leaving = new Dictionary<int, int>();
                var candidates = new List<Soldier>();
                foreach (var x in w.Soldiers)
                    if (x.People == p && !x.Moving && x.DuelId == 0 && x.Path.Count == 0 && x.PurgeLeft == 0) candidates.Add(x);
                candidates.Sort((a, b) => b.Id.CompareTo(a.Id));
                int mine = 1 << p;
                foreach (var x in candidates)
                {
                    if (x.Corner >= 0)
                    {
                        var here = WarApi.SettlementAt(s, x.Corner);
                        if (here != null && here.People == p)
                        {
                            leaving.TryGetValue(x.Corner, out int gone);
                            if (WarApi.PresentAt(s, x.Corner, p) - gone < 2) continue;
                        }
                    }
                    var rb = Bfs(s, x, true);
                    Reach ru = null;
                    bool Tier(int tier)
                    {
                        int best = -1;
                        for (int n = 1; n < NodeSlots; n++)
                        {
                            if (demons[n] - assigned[n] <= 0) continue;
                            int mask = touched[nodeHex[n]];
                            bool inTier = tier == 1 ? (mask & mine) != 0 : (mask & notLeader) != 0;
                            if (!inTier || rb.Dist[n] < 0) continue;
                            if (best < 0 || rb.Dist[n] < rb.Dist[best]) best = n;
                        }
                        if (best > 0) return OrderPurge(x, rb, best, assigned);
                        // a trio of demons across the way counts as priority 1 (WA-13)
                        ru = ru ?? Bfs(s, x, false);
                        best = -1;
                        for (int n = 1; n < NodeSlots; n++)
                        {
                            if (demons[n] - assigned[n] <= 0) continue;
                            int mask = touched[nodeHex[n]];
                            bool inTier = tier == 1 ? (mask & mine) != 0 : (mask & notLeader) != 0;
                            if (!inTier || ru.Dist[n] < 0) continue;
                            if (best < 0 || ru.Dist[n] < ru.Dist[best]) best = n;
                        }
                        return best > 0 && OrderPurgeBlocker(s, x, Route(ru, best, x), assigned, demons);
                    }

                    bool done = Tier(1) || Tier(2) || Attack(s, x, p, rb, ref ru, assigned, demons) || ReturnHome(s, x, p, rb);
                    if (done && x.Corner >= 0 && x.Path.Count > 0)
                    {
                        leaving.TryGetValue(x.Corner, out int g);
                        leaving[x.Corner] = g + 1;
                    }
                }
            }
        }

        private static bool OrderPurge(Soldier x, Reach r, int node, int[] assigned)
        {
            x.Order = OrderKind.Purge; x.OrderTarget = node;
            x.Path = Route(r, node, x);
            assigned[node]++;
            if (x.Path.Count == 0 && x.Node == node) x.PurgeLeft = WarData.PurgeTicks;
            return true;
        }

        /// <summary>Walks the route and orders a purge at the first node that holds 3 demons (it cannot be passed).</summary>
        private static bool OrderPurgeBlocker(WorldState s, Soldier x, List<int> route, int[] assigned, int[] demons)
        {
            for (int i = 0; i < route.Count; i++)
            {
                int n = route[i];
                if (n == x.Node || PlagueApi.CanWalkThrough(s, n) || demons[n] - assigned[n] <= 0) continue;
                x.Order = OrderKind.Purge; x.OrderTarget = n;
                x.Path = route.GetRange(0, i + 1);
                assigned[n]++;
                return true;
            }
            return false;
        }

        private static bool Attack(WorldState s, Soldier x, int p, Reach rb, ref Reach ru, int[] assigned, int[] demons)
        {
            var w = s.War;
            WarSettlement best = null; int bestWeak = 0, bestDist = 0, bestNode = 0;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                var r = pass == 0 ? rb : (ru = ru ?? Bfs(s, x, false));
                foreach (var st in w.Settlements)
                {
                    if (!WarApi.AtWar(s, p, st.People)) continue;
                    if (st.IsHome && WarApi.GoodsInStore(st) == 0) continue;
                    int dist = -1, node = 0;
                    foreach (int n in WarApi.NodesAtCorner(s, st.Corner))
                        if (r.Dist[n] >= 0 && (dist < 0 || r.Dist[n] < dist)) { dist = r.Dist[n]; node = n; }
                    if (dist < 0) continue;
                    int weak = WarApi.PresentAt(s, st.Corner, st.People);
                    if (best == null || weak < bestWeak || (weak == bestWeak && dist < bestDist) || (weak == bestWeak && dist == bestDist && st.Corner < best.Corner))
                    { best = st; bestWeak = weak; bestDist = dist; bestNode = node; }
                }
                if (best != null && pass == 1) return OrderPurgeBlocker(s, x, Route(r, bestNode, x), assigned, demons);
            }
            if (best == null) return false;
            x.Order = OrderKind.Attack; x.OrderTarget = best.Corner;
            x.Path = Route(rb, bestNode, x);
            x.Path.Add(CornerStep(best.Corner));
            return true;
        }

        private static bool ReturnHome(WorldState s, Soldier x, int p, Reach rb)
        {
            if (x.Node <= 0) return false;
            WarSettlement best = null; int bestDist = 0, bestNode = 0;
            foreach (var st in s.War.Settlements)
            {
                if (st.People != p) continue;
                foreach (int n in WarApi.NodesAtCorner(s, st.Corner))
                {
                    if (rb.Dist[n] < 0) continue;
                    if (best == null || rb.Dist[n] < bestDist || (rb.Dist[n] == bestDist && st.Corner < best.Corner)) { best = st; bestDist = rb.Dist[n]; bestNode = n; }
                }
            }
            if (best == null) return false;
            x.Order = OrderKind.Return; x.OrderTarget = best.Corner;
            x.Path = Route(rb, bestNode, x);
            x.Path.Add(CornerStep(best.Corner));
            return true;
        }
    }
}
