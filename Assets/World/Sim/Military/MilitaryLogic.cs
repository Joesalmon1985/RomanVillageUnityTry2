using System;
using System.Collections.Generic;
using World.Sim.Economy;
using World.Sim.Strategy;

namespace World.Sim.Military
{
    /// <summary>One power consumer in allocation order.</summary>
    public struct PowerConsumer
    {
        public int BuildingId;
        public BuildingType Type;
        public int Demand;
        public bool Powered;
    }

    /// <summary>Military rules shared by systems, commands, hooks and the API. Deterministic, id-ordered.</summary>
    internal static class MilitaryLogic
    {
        // ---------------- Setup ----------------

        internal static void Init(WorldState s)
        {
            var m = new MilitaryState();
            s.Military = m;
            var o = m.Outpost;
            o.Vertex = s.Board.RivalOutpostVertex;
            o.MaxHp = m.Tuning.OutpostHp;
            o.Hp = o.MaxHp;
            if (o.Vertex >= 0)
            {
                var g = NewSquad(s, PlayerIds.Rival, SquadKind.Garrison, o.Vertex);
                o.GarrisonSquadId = g.Id;
                for (int i = 0; i < m.Tuning.GarrisonBase; i++) g.Units.Add(NewUnit(s, m.Tuning.GuardHp));
            }
        }

        internal static Squad NewSquad(WorldState s, int owner, SquadKind kind, int vertex)
        {
            var sq = new Squad { Id = s.Military.Squads.Count, Owner = owner, Kind = kind, At = vertex };
            s.Military.Squads.Add(sq);
            return sq;
        }

        internal static Unit NewUnit(WorldState s, int hp) => new Unit { Id = s.Military.NextUnitId++, Hp = hp };

        internal static Squad GetSquad(WorldState s, int id) =>
            s.Military != null && id >= 0 && id < s.Military.Squads.Count ? s.Military.Squads[id] : null;

        internal static BarracksQueue Queue(WorldState s, int buildingId, bool create)
        {
            var qs = s.Military.Queues;
            for (int i = 0; i < qs.Count; i++)
            {
                if (qs[i].BuildingId == buildingId) return qs[i];
                if (qs[i].BuildingId > buildingId)
                {
                    if (!create) return null;
                    var q = new BarracksQueue { BuildingId = buildingId };
                    qs.Insert(i, q);
                    return q;
                }
            }
            if (!create) return null;
            var nq = new BarracksQueue { BuildingId = buildingId };
            qs.Add(nq);
            return nq;
        }

        internal static int TrainingTicks(WorldState s)
        {
            int pct = 100 + StrategyApi.Effects(s, PlayerIds.Human).TrainingPercent;
            return Math.Max(1, s.Military.Tuning.TrainingTicks * Math.Max(1, pct) / 100);
        }

        internal static int UnitRations(WorldState s) =>
            Math.Max(0, s.Military.Tuning.UnitRations - StrategyApi.Effects(s, PlayerIds.Human).UnitRationDiscount);

        // ---------------- Power ----------------

        internal static List<PowerConsumer> Allocate(WorldState s, out int supply, out int demand)
        {
            var t = s.Military.Tuning;
            supply = 0;
            demand = 0;
            var list = new List<PowerConsumer>();
            var prio = new List<int>();
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Owner != PlayerIds.Human || b.State != BuildingState.Active) continue;
                if (b.Type == BuildingType.PowerPlant && EconomyApi.IsFuelled(s, b.Id)) supply += t.SupplyPerPlant;
                if (!b.Enabled) continue;
                int d = 0;
                if (b.Type == BuildingType.Refinery) d = t.RefineryDemand;
                else if (b.Type == BuildingType.Barracks)
                {
                    var q = Queue(s, b.Id, false);
                    if (q != null && q.Orders.Count > 0) d = t.BarracksDemand;
                }
                if (d <= 0) continue;
                list.Add(new PowerConsumer { BuildingId = b.Id, Type = b.Type, Demand = d });
                prio.Add((int)b.Priority);
            }
            // Highest priority first, then lowest id: the last ones lose power first.
            var idx = new List<int>();
            for (int i = 0; i < list.Count; i++) idx.Add(i);
            idx.Sort((a, b) => prio[a] != prio[b] ? prio[b].CompareTo(prio[a]) : list[a].BuildingId.CompareTo(list[b].BuildingId));
            var sorted = new List<PowerConsumer>();
            int left = supply;
            foreach (int i in idx)
            {
                var c = list[i];
                demand += c.Demand;
                c.Powered = left >= c.Demand;
                if (c.Powered) left -= c.Demand;
                sorted.Add(c);
            }
            return sorted;
        }

        internal static bool IsPowered(WorldState s, int buildingId)
        {
            if (s.Military == null) return true;
            var list = Allocate(s, out _, out _);
            foreach (var c in list) if (c.BuildingId == buildingId) return c.Powered;
            return true;
        }

        // ---------------- Barracks ----------------

        internal static void TickBarracks(WorldState s, EventBuffer ev)
        {
            var m = s.Military;
            List<PowerConsumer> power = null;
            foreach (var q in m.Queues)
            {
                var b = EconomyApi.GetBuilding(s, q.BuildingId);
                string reason = null;
                int ware = -1;
                if (b == null || b.State != BuildingState.Active)
                {
                    SetQueueReason(q, null, -1, ev);
                    continue;
                }
                if (q.Orders.Count > 0)
                {
                    var head = q.Orders[0];
                    if (!b.Enabled) reason = Reasons.Disabled;
                    else
                    {
                        if (!head.Supplied)
                        {
                            if (b.Available(Ware.Rations) >= head.Rations && b.Available(Ware.Ingots) >= head.Ingots)
                            {
                                if (head.Rations > 0) EconomyApi.TryConsume(s, b.Id, Ware.Rations, head.Rations);
                                if (head.Ingots > 0) EconomyApi.TryConsume(s, b.Id, Ware.Ingots, head.Ingots);
                                m.ConsumedForUnits[(int)Ware.Rations] += head.Rations;
                                m.ConsumedForUnits[(int)Ware.Ingots] += head.Ingots;
                                head.Supplied = true;
                                ev?.Add(new TrainingStarted { BarracksId = b.Id });
                            }
                            else
                            {
                                reason = Reasons.WaitingWare;
                                ware = b.Available(Ware.Rations) < head.Rations ? (int)Ware.Rations : (int)Ware.Ingots;
                            }
                        }
                        if (head.Supplied)
                        {
                            if (power == null) power = Allocate(s, out _, out _);
                            bool powered = true;
                            foreach (var c in power) if (c.BuildingId == b.Id) powered = c.Powered;
                            if (!powered) reason = Reasons.NoPower;
                            else
                            {
                                head.Progress++;
                                if (head.Progress >= TrainingTicks(s))
                                {
                                    q.Orders.RemoveAt(0);
                                    SpawnUnit(s, q, b, ev);
                                    power = null;
                                }
                            }
                        }
                    }
                }
                SetQueueReason(q, reason, ware, ev);
            }
        }

        private static void SetQueueReason(BarracksQueue q, string reason, int ware, EventBuffer ev)
        {
            if (q.Reason == reason && q.ReasonWare == ware) return;
            q.Reason = reason;
            q.ReasonWare = ware;
            ev?.Add(new QueueStateChanged { BarracksId = q.BuildingId, Reason = reason, ReasonWare = ware });
        }

        private static void SpawnUnit(WorldState s, BarracksQueue q, Building b, EventBuffer ev)
        {
            var m = s.Military;
            var sq = GetSquad(s, q.HomeSquadId);
            bool join = sq != null && sq.Alive && sq.Kind == SquadKind.Field && sq.CurrentEdge < 0 && sq.At == b.Vertex
                        && sq.Order == OrderKind.None && sq.Units.Count < m.Tuning.MaxSquadSize;
            if (!join)
            {
                sq = NewSquad(s, PlayerIds.Human, SquadKind.Field, b.Vertex);
                sq.BarracksId = b.Id;
                q.HomeSquadId = sq.Id;
            }
            var u = NewUnit(s, m.Tuning.UnitHp);
            sq.Units.Add(u);
            m.UnitsTrained++;
            ev?.Add(new UnitTrained { BarracksId = b.Id, SquadId = sq.Id, UnitId = u.Id });
        }

        // ---------------- Power events ----------------

        internal static void TickPowerEvents(WorldState s, EventBuffer ev, List<int> unpowered)
        {
            var list = Allocate(s, out _, out _);
            var now = new List<int>();
            foreach (var c in list) if (!c.Powered) now.Add(c.BuildingId);
            now.Sort();
            foreach (int id in now) if (!unpowered.Contains(id)) ev?.Add(new PowerChanged { BuildingId = id, Powered = false });
            foreach (int id in unpowered) if (!now.Contains(id)) ev?.Add(new PowerChanged { BuildingId = id, Powered = true });
            unpowered.Clear();
            unpowered.AddRange(now);
        }

        // ---------------- Squads ----------------

        internal static bool EnemyAt(WorldState s, Squad sq, int v)
        {
            foreach (var o in s.Military.Squads)
                if (o.Alive && o.Owner != sq.Owner && o.CurrentEdge < 0 && o.At == v && o.Units.Count > 0) return true;
            var op = s.Military.Outpost;
            return sq.Owner == PlayerIds.Human && !op.Destroyed && v == op.Vertex;
        }

        internal static void TickSquads(WorldState s, EventBuffer ev)
        {
            var m = s.Military;
            var topo = s.Board.Topology;
            for (int i = 0; i < m.Squads.Count; i++)
            {
                var sq = m.Squads[i];
                if (!sq.Alive) continue;
                if (sq.CurrentEdge >= 0)
                {
                    sq.Progress++;
                    if (sq.Progress < m.Tuning.TicksPerEdge) continue;
                    sq.At = sq.ToVertex;
                    sq.CurrentEdge = -1;
                    sq.FromVertex = -1;
                    sq.ToVertex = -1;
                    sq.Progress = 0;
                    ev?.Add(new SquadArrived { SquadId = sq.Id, Vertex = sq.At });
                    if (sq.Owner == PlayerIds.Human) StrategyApi.ClearDemonsBySquad(s, sq.At, ev);
                    continue;
                }
                if (sq.Engaged || EnemyAt(s, sq, sq.At)) continue;
                int target = sq.Order != OrderKind.None ? sq.Target : -1;
                if (target < 0 || target == sq.At)
                {
                    if (sq.Kind == SquadKind.Raid && sq.Order == OrderKind.Attack && sq.At == sq.Target && !sq.Occupying)
                    {
                        sq.Occupying = true;
                        ev?.Add(new RaidOccupied { SquadId = sq.Id, Vertex = sq.At });
                    }
                    else if (sq.Kind == SquadKind.Raid && sq.Order == OrderKind.Return && sq.At == m.Outpost.Vertex) MergeRaid(s, sq, ev);
                    continue;
                }
                var path = topo.FindPath(sq.At, target);
                if (path == null || path.Length < 2) continue;
                sq.Occupying = false;
                sq.FromVertex = sq.At;
                sq.ToVertex = path[1];
                sq.CurrentEdge = topo.EdgeBetween(path[0], path[1]);
                sq.Progress = 0;
                ev?.Add(new SquadMoved { SquadId = sq.Id, From = sq.FromVertex, To = sq.ToVertex });
            }
        }

        private static void MergeRaid(WorldState s, Squad raid, EventBuffer ev)
        {
            var m = s.Military;
            var g = GetSquad(s, m.Outpost.GarrisonSquadId);
            if (g == null || !g.Alive)
            {
                raid.Kind = SquadKind.Garrison;
                raid.Order = OrderKind.None;
                raid.Target = -1;
                m.Outpost.GarrisonSquadId = raid.Id;
            }
            else
            {
                g.Units.AddRange(raid.Units);
                raid.Units.Clear();
                raid.Alive = false;
            }
            ev?.Add(new RaidEnded { SquadId = raid.Id, How = "merged" });
        }

        // ---------------- Combat ----------------

        internal static void TickCombat(WorldState s, EventBuffer ev)
        {
            var m = s.Military;
            var t = m.Tuning;
            foreach (var sq in m.Squads)
                if (sq.Alive)
                    foreach (var u in sq.Units)
                        if (u.Cooldown > 0) u.Cooldown--;

            // Flags with a fight, ascending.
            var flags = new List<int>();
            foreach (var sq in m.Squads)
            {
                if (!sq.Alive || sq.Owner != PlayerIds.Human || sq.CurrentEdge >= 0 || sq.Units.Count == 0) continue;
                if (EnemyAt(s, sq, sq.At) && !flags.Contains(sq.At)) flags.Add(sq.At);
            }
            flags.Sort();
            var wasEngaged = new bool[m.Squads.Count];
            for (int i = 0; i < m.Squads.Count; i++)
            {
                wasEngaged[i] = m.Squads[i].Engaged;
                m.Squads[i].Engaged = false;
            }
            int playerDmg = t.UnitDamage + StrategyApi.Effects(s, PlayerIds.Human).DamageBonus;
            int rivalDmg = t.GuardDamage + StrategyApi.Effects(s, PlayerIds.Rival).DamageBonus;

            foreach (int v in flags)
            {
                var mine = new List<Squad>();
                var theirs = new List<Squad>();
                foreach (var sq in m.Squads)
                {
                    if (!sq.Alive || sq.CurrentEdge >= 0 || sq.At != v || sq.Units.Count == 0) continue;
                    (sq.Owner == PlayerIds.Human ? mine : theirs).Add(sq);
                }
                var engagedIds = new List<int>();
                bool fresh = false;
                foreach (var sq in mine) { sq.Engaged = true; engagedIds.Add(sq.Id); fresh |= !wasEngaged[sq.Id]; }
                foreach (var sq in theirs) { sq.Engaged = true; engagedIds.Add(sq.Id); fresh |= !wasEngaged[sq.Id]; }
                if (fresh) ev?.Add(new SquadEngaged { Vertex = v, SquadIds = engagedIds.ToArray() });

                // Simultaneous: choose all targets from the state at the start of the tick, then apply.
                var hits = new List<(Unit attacker, Unit target, int dmg)>();
                int outpostDmg = 0;
                Unit firstTheirs = FirstUnit(theirs), firstMine = FirstUnit(mine);
                bool outpostHere = !m.Outpost.Destroyed && v == m.Outpost.Vertex;
                foreach (var sq in mine)
                    foreach (var u in sq.Units)
                    {
                        if (u.Cooldown > 0) continue;
                        if (firstTheirs != null) hits.Add((u, firstTheirs, playerDmg));
                        else if (outpostHere) { outpostDmg += playerDmg; hits.Add((u, null, playerDmg)); }
                        else continue;
                        u.Cooldown = t.AttackCooldown;
                    }
                foreach (var sq in theirs)
                    foreach (var u in sq.Units)
                    {
                        if (u.Cooldown > 0 || firstMine == null) continue;
                        hits.Add((u, firstMine, rivalDmg));
                        u.Cooldown = t.AttackCooldown;
                    }
                foreach (var h in hits)
                {
                    if (h.target != null)
                    {
                        h.target.Hp -= h.dmg;
                        ev?.Add(new UnitHit { Vertex = v, AttackerUnitId = h.attacker.Id, TargetUnitId = h.target.Id, Damage = h.dmg, TargetHpLeft = Math.Max(0, h.target.Hp) });
                    }
                }
                if (outpostDmg > 0)
                {
                    m.Outpost.Hp = Math.Max(0, m.Outpost.Hp - outpostDmg);
                    foreach (var h in hits)
                        if (h.target == null)
                            ev?.Add(new UnitHit { Vertex = v, AttackerUnitId = h.attacker.Id, TargetUnitId = -1, Damage = h.dmg, TargetHpLeft = m.Outpost.Hp });
                    ev?.Add(new OutpostDamaged { Hp = m.Outpost.Hp, MaxHp = m.Outpost.MaxHp });
                }
                RemoveDead(s, mine, v, ev);
                RemoveDead(s, theirs, v, ev);
                if (outpostHere && m.Outpost.Hp <= 0) DestroyOutpost(s, ev);
            }
        }

        private static Unit FirstUnit(List<Squad> squads)
        {
            foreach (var sq in squads) foreach (var u in sq.Units) if (u.Hp > 0) return u;
            return null;
        }

        private static void RemoveDead(WorldState s, List<Squad> squads, int v, EventBuffer ev)
        {
            foreach (var sq in squads)
            {
                for (int i = 0; i < sq.Units.Count; i++)
                {
                    if (sq.Units[i].Hp > 0) continue;
                    ev?.Add(new UnitDied { SquadId = sq.Id, UnitId = sq.Units[i].Id, Vertex = v });
                    sq.Units.RemoveAt(i);
                    i--;
                }
                if (sq.Units.Count == 0)
                {
                    sq.Alive = false;
                    sq.Engaged = false;
                    sq.Occupying = false;
                    ev?.Add(new SquadDestroyed { SquadId = sq.Id, Owner = sq.Owner, Vertex = v });
                }
            }
        }

        private static void DestroyOutpost(WorldState s, EventBuffer ev)
        {
            var m = s.Military;
            if (m.Outpost.Destroyed) return;
            m.Outpost.Destroyed = true;
            m.Outpost.DestroyedTick = s.Tick;
            ev?.Add(new OutpostDestroyed { Vertex = m.Outpost.Vertex });
            foreach (var sq in m.Squads)
            {
                if (!sq.Alive || sq.Owner != PlayerIds.Rival) continue;
                sq.Alive = false;
                sq.Occupying = false;
                sq.Units.Clear();
                if (sq.Kind == SquadKind.Raid) ev?.Add(new RaidEnded { SquadId = sq.Id, How = "disbanded" });
            }
            foreach (var sq in m.Squads) if (sq.Owner == PlayerIds.Human) sq.Engaged = false;
        }

        // ---------------- Rival ----------------

        internal static int GarrisonTarget(WorldState s)
        {
            var t = s.Military.Tuning;
            return t.GarrisonBase + Math.Max(0, s.Strategy.Age - 1) * t.GarrisonPerAge;
        }

        internal static void RivalRoundEnd(WorldState s, EventBuffer ev)
        {
            var m = s.Military;
            if (m == null || m.Outpost.Destroyed || m.Outpost.Vertex < 0) return;
            var t = m.Tuning;
            int ageRound = StrategyApi.AgeRound(s);

            // Reinforce the garrison.
            var g = GetSquad(s, m.Outpost.GarrisonSquadId);
            if (g == null || !g.Alive)
            {
                g = NewSquad(s, PlayerIds.Rival, SquadKind.Garrison, m.Outpost.Vertex);
                m.Outpost.GarrisonSquadId = g.Id;
            }
            int added = 0;
            while (g.Units.Count < GarrisonTarget(s))
            {
                g.Units.Add(NewUnit(s, t.GuardHp));
                added++;
            }
            if (added > 0) ev?.Add(new GarrisonReinforced { Guards = g.Units.Count });

            // Raids that held a flag for a round go home.
            bool raidOut = false;
            foreach (var sq in m.Squads)
            {
                if (!sq.Alive || sq.Kind != SquadKind.Raid) continue;
                if (sq.Order == OrderKind.Attack && sq.LeaveAfterRound <= ageRound)
                {
                    sq.Order = OrderKind.Return;
                    sq.Target = m.Outpost.Vertex;
                    sq.Occupying = false;
                    ev?.Add(new RaidEnded { SquadId = sq.Id, How = "leaving" });
                }
                else if (sq.Order == OrderKind.Attack) raidOut = true;
            }

            // A new raid from the configured age.
            if (raidOut || s.Strategy.Age < t.RaidFromAge || t.RaidSize <= 0) return;
            int target = RaidTarget(s);
            if (target < 0) return;
            var raid = NewSquad(s, PlayerIds.Rival, SquadKind.Raid, m.Outpost.Vertex);
            for (int i = 0; i < t.RaidSize; i++) raid.Units.Add(NewUnit(s, t.GuardHp));
            raid.Order = OrderKind.Attack;
            raid.Target = target;
            raid.LeaveAfterRound = ageRound + 1;
            ev?.Add(new RaidLaunched { SquadId = raid.Id, Target = target, Units = raid.Units.Count });
        }

        /// <summary>Nearest flag on the player's roads to the outpost (not the storehouse), lowest id on ties.</summary>
        internal static int RaidTarget(WorldState s)
        {
            var topo = s.Board.Topology;
            int from = s.Military.Outpost.Vertex;
            int store = s.Board.StorehouseVertex;
            int best = -1, bestD = int.MaxValue;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                if (v == store) continue;
                bool onRoad = false;
                foreach (int e in topo.VertexEdges(v)) if (EconomyApi.HasRoad(s, e)) onRoad = true;
                if (!onRoad) continue;
                int d = topo.Distance(from, v);
                if (d >= 0 && d < bestD)
                {
                    best = v;
                    bestD = d;
                }
            }
            return best;
        }

        internal static bool RaidBlocks(WorldState s, int v)
        {
            if (s.Military == null) return false;
            foreach (var sq in s.Military.Squads) if (sq.Alive && sq.Occupying && sq.At == v) return true;
            return false;
        }
    }
}
