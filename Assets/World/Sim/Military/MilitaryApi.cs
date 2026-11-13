using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim.Military
{
    /// <summary>Power totals and consumers in allocation order (the last ones lose power first).</summary>
    public struct PowerSummary
    {
        public int Supply;
        public int Demand;
        public int FuelledPlants;
        public PowerConsumer[] Consumers;
        /// <summary>"Power 4 of 6: the Barracks has no power."</summary>
        public string Sentence;
    }

    /// <summary>Barracks queue for the barracks panel.</summary>
    public struct BarracksInfo
    {
        public int BuildingId;
        public int Queued;
        public bool HeadSupplied;
        /// <summary>Training progress of the head order in percent.</summary>
        public int ProgressPercent;
        public int TrainingTicks;
        /// <summary>Reason code (<see cref="Reasons"/>) or null while training/idle.</summary>
        public string Reason;
        /// <summary>Wares the head order still waits for, e.g. "1 weapons" (empty when supplied).</summary>
        public string[] WaitingFor;
        /// <summary>One sentence: "The barracks is waiting for rations.", "The barracks has no power.", "Training Wardens (40%)."</summary>
        public string Status;
        public string CostText;
    }

    /// <summary>A squad as views and panels show it.</summary>
    public struct SquadView
    {
        public int Id;
        public int Owner;
        public SquadKind Kind;
        public int Units;
        public int TotalHp;
        public int MaxHp;
        /// <summary>At a flag: At; on an edge: From -> To with ProgressPermille of the edge done.</summary>
        public int At;
        public int From;
        public int To;
        public int ProgressPermille;
        public bool Moving;
        public bool Engaged;
        public bool Occupying;
        public OrderKind Order;
        public int Target;
        public string OrderText;
    }

    /// <summary>Shields on both sides for the military comparison panel.</summary>
    public struct ShieldSummary
    {
        public int PlayerShields;
        public int PlayerSoldiers;
        public int PlayerCardShields;
        public int RivalShields;
        public int RivalGuards;
        /// <summary>Rival soldiers out on raids: they do not count in the comparison.</summary>
        public int RivalRaiders;
        public int RivalCardShields;
        /// <summary>Age whose end brings the next comparison (0 in the prologue).</summary>
        public int NextComparisonAge;
        /// <summary>Rounds after the current one before that comparison.</summary>
        public int RoundsLeftInAge;
        /// <summary>Points a win would give now; LossVp is negative.</summary>
        public int WinVp;
        public int LossVp;
        /// <summary>"Shields 3 (2 soldiers, 1 from cards) against the rival's 2 (...): at the end of Age I you would win 1 point."</summary>
        public string Sentence;
    }

    public struct OutpostInfo
    {
        public int Vertex;
        public int Hp;
        public int MaxHp;
        public bool Destroyed;
        public int Guards;
        public string Status;
    }

    /// <summary>Public read surface of the military layer (plus the rival hooks used by the strategy round end).</summary>
    public static class MilitaryApi
    {
        // ---- Power ----

        public static bool IsPowered(WorldState s, int buildingId) => MilitaryLogic.IsPowered(s, buildingId);

        public static PowerSummary Power(WorldState s)
        {
            var list = MilitaryLogic.Allocate(s, out int supply, out int demand);
            int plants = 0;
            foreach (var b in s.Economy.Buildings)
                if (b.Owner == PlayerIds.Human && b.Type == BuildingType.PowerPlant && EconomyApi.IsFuelled(s, b.Id)) plants++;
            var sum = new PowerSummary { Supply = supply, Demand = demand, FuelledPlants = plants, Consumers = list.ToArray() };
            var off = new List<string>();
            foreach (var c in list) if (!c.Powered) off.Add("the " + EconomyApi.Def(s, c.Type).Name);
            sum.Sentence = "Power " + supply + " for " + demand + " needed" +
                           (off.Count == 0 ? "." : ": " + string.Join(" and ", off) + (off.Count == 1 ? " has" : " have") + " no power.");
            return sum;
        }

        // ---- Barracks ----

        public static int TrainingTicks(WorldState s) => MilitaryLogic.TrainingTicks(s);

        /// <summary>Wares per unit now (after cards).</summary>
        public static string UnitCostText(WorldState s) =>
            s.Military.Tuning.UnitWeapons + " weapons and " + MilitaryLogic.UnitRations(s) + " rations per " + UnitName(s, 1);

        public static string UnitName(WorldState s, int n) => n == 1 ? "Warden" : s.Military.Tuning.UnitName;

        public static BarracksInfo DescribeBarracks(WorldState s, int buildingId)
        {
            var info = new BarracksInfo { BuildingId = buildingId, TrainingTicks = TrainingTicks(s), CostText = UnitCostText(s), WaitingFor = new string[0] };
            var b = EconomyApi.GetBuilding(s, buildingId);
            var q = MilitaryLogic.Queue(s, buildingId, false);
            if (b == null || b.Type != BuildingType.Barracks)
            {
                info.Status = "That is not a barracks.";
                return info;
            }
            if (b.State != BuildingState.Active)
            {
                info.Status = EconomyApi.Describe(s, buildingId).Status;
                info.Reason = b.Reason;
                return info;
            }
            if (q == null || q.Orders.Count == 0)
            {
                info.Status = "The barracks is ready to train " + s.Military.Tuning.UnitName + ".";
                return info;
            }
            var head = q.Orders[0];
            info.Queued = q.Orders.Count;
            info.HeadSupplied = head.Supplied;
            info.ProgressPercent = 100 * head.Progress / System.Math.Max(1, info.TrainingTicks);
            info.Reason = q.Reason;
            var waiting = new List<string>();
            if (!head.Supplied)
            {
                int g = head.Weapons - b.Available(Ware.Weapons), r = head.Rations - b.Available(Ware.Rations);
                if (g > 0) waiting.Add(g + " weapons");
                if (r > 0) waiting.Add(r + " rations");
            }
            info.WaitingFor = waiting.ToArray();
            switch (q.Reason)
            {
                case Reasons.NoPower: info.Status = Reasons.Sentence(Reasons.NoPower, "The barracks"); break;
                case Reasons.Disabled: info.Status = Reasons.Sentence(Reasons.Disabled, "The barracks"); break;
                case Reasons.DemonOnRoute: info.Status = "A demon on its flag stops the barracks."; break;
                case Reasons.WaitingWare:
                    // Show why the delivery is not coming when the economy knows (no road, demon on the route).
                    if (b.Reason == Reasons.NoRoad || b.Reason == Reasons.DemonOnRoute)
                        info.Status = EconomyApi.Describe(s, buildingId).Status;
                    else
                        info.Status = "The barracks is waiting for " + string.Join(" and ", waiting) + ".";
                    break;
                default: info.Status = "Training " + s.Military.Tuning.UnitName + " (" + info.ProgressPercent + "%)."; break;
            }
            return info;
        }

        // ---- Squads ----

        public static SquadView DescribeSquad(WorldState s, int squadId)
        {
            var sq = MilitaryLogic.GetSquad(s, squadId);
            if (sq == null) return new SquadView { Id = squadId, OrderText = "Unknown squad." };
            int hp = 0;
            foreach (var u in sq.Units) hp += u.Hp;
            int unitHp = sq.Owner == PlayerIds.Human ? s.Military.Tuning.UnitHp : s.Military.Tuning.GuardHp;
            var v = new SquadView
            {
                Id = sq.Id,
                Owner = sq.Owner,
                Kind = sq.Kind,
                Units = sq.Units.Count,
                TotalHp = hp,
                MaxHp = sq.Units.Count * unitHp,
                At = sq.CurrentEdge >= 0 ? sq.FromVertex : sq.At,
                From = sq.CurrentEdge >= 0 ? sq.FromVertex : sq.At,
                To = sq.CurrentEdge >= 0 ? sq.ToVertex : sq.At,
                ProgressPermille = sq.CurrentEdge >= 0 ? 1000 * sq.Progress / System.Math.Max(1, s.Military.Tuning.TicksPerEdge) : 0,
                Moving = sq.CurrentEdge >= 0,
                Engaged = sq.Engaged,
                Occupying = sq.Occupying,
                Order = sq.Order,
                Target = sq.Target,
            };
            if (!sq.Alive) v.OrderText = "Destroyed.";
            else if (sq.Engaged) v.OrderText = "Fighting.";
            else if (sq.Kind == SquadKind.Garrison) v.OrderText = "Guarding the outpost.";
            else if (sq.Occupying) v.OrderText = "Holding a flag.";
            else switch (sq.Order)
            {
                case OrderKind.Defend: v.OrderText = sq.At == sq.Target && !v.Moving ? "Defending its flag." : "Marching to defend a flag."; break;
                case OrderKind.Attack: v.OrderText = sq.At == sq.Target && !v.Moving ? "Holding the attacked flag." : "Marching to attack."; break;
                case OrderKind.Return: v.OrderText = "Going home."; break;
                default: v.OrderText = "Waiting for orders."; break;
            }
            return v;
        }

        /// <summary>Living squads of a side (or both with player = -1), ascending id.</summary>
        public static List<SquadView> Squads(WorldState s, int player = -1)
        {
            var l = new List<SquadView>();
            foreach (var sq in s.Military.Squads)
                if (sq.Alive && sq.Units.Count > 0 && (player < 0 || sq.Owner == player)) l.Add(DescribeSquad(s, sq.Id));
            return l;
        }

        // ---- Outpost and rival ----

        public static OutpostInfo Outpost(WorldState s)
        {
            var o = s.Military.Outpost;
            var g = MilitaryLogic.GetSquad(s, o.GarrisonSquadId);
            int guards = g != null && g.Alive ? g.Units.Count : 0;
            return new OutpostInfo
            {
                Vertex = o.Vertex,
                Hp = o.Hp,
                MaxHp = o.MaxHp,
                Destroyed = o.Destroyed,
                Guards = guards,
                Status = o.Destroyed ? "The rival outpost is destroyed." : "The rival outpost: " + o.Hp + "/" + o.MaxHp + " strength, " + guards + " guards.",
            };
        }

        public static bool IsOutpostDestroyed(WorldState s) => s.Military != null && s.Military.Outpost.Destroyed;

        // ---- Shields (age-end military comparison) ----

        /// <summary>Shields of a side now: living soldiers (player field squads; rival guards and raiders) plus military card shields.</summary>
        public static int Shields(WorldState s, int player) => MilitaryLogic.Shields(s, player);

        /// <summary>Both sides' shields, what they are made of, and what the next comparison would give.</summary>
        public static ShieldSummary ShieldSummary(WorldState s)
        {
            var sum = new ShieldSummary();
            foreach (var sq in s.Military.Squads)
            {
                if (!sq.Alive) continue;
                if (sq.Owner == PlayerIds.Human) sum.PlayerSoldiers += sq.Units.Count;
                else if (sq.Kind == SquadKind.Raid) sum.RivalRaiders += sq.Units.Count;
                else sum.RivalGuards += sq.Units.Count;
            }
            sum.PlayerCardShields = Strategy.StrategyApi.Effects(s, PlayerIds.Human).Shields;
            sum.RivalCardShields = Strategy.StrategyApi.Effects(s, PlayerIds.Rival).Shields;
            sum.PlayerShields = sum.PlayerSoldiers + sum.PlayerCardShields;
            sum.RivalShields = sum.RivalGuards + sum.RivalCardShields; // raiders away on a raid do not defend
            var st = s.Strategy;
            int age = st == null ? 0 : st.Age;
            sum.NextComparisonAge = age;
            if (st != null && age >= 1)
            {
                var t = st.Tuning;
                sum.WinVp = t.MilitaryWinVp[System.Math.Min(age, t.MilitaryWinVp.Length) - 1];
                sum.LossVp = t.MilitaryLossVp;
                sum.RoundsLeftInAge = System.Math.Max(0, t.RoundsPerAge - st.RoundInAge);
            }
            string lead = sum.PlayerShields > sum.RivalShields ? "you would win " + sum.WinVp + (sum.WinVp == 1 ? " point" : " points")
                : sum.PlayerShields < sum.RivalShields ? "you would lose " + (-sum.LossVp) + " point" + (sum.LossVp == -1 ? "" : "s")
                : "it would be a tie (no points)";
            sum.Sentence = "Shields " + sum.PlayerShields + " (" + sum.PlayerSoldiers + " soldiers, " + sum.PlayerCardShields + " from cards) against the rival's "
                + sum.RivalShields + " (" + sum.RivalGuards + " guards at the outpost, " + sum.RivalCardShields + " from cards)"
                + (age >= 1 ? ": at the end of Age " + Strategy.StrategyApi.Roman(age) + " " + lead + "." : ".");
            return sum;
        }

        public static long OutpostDestroyedTick(WorldState s) => s.Military == null ? -1 : s.Military.Outpost.DestroyedTick;

        /// <summary>Flags currently held by a rival raid.</summary>
        public static List<int> RaidedFlags(WorldState s)
        {
            var l = new List<int>();
            foreach (var sq in s.Military.Squads) if (sq.Alive && sq.Occupying && !l.Contains(sq.At)) l.Add(sq.At);
            l.Sort();
            return l;
        }

        /// <summary>The rival's military turn at round end (garrison, raids). Called by the strategy round end.</summary>
        public static void RivalRoundEnd(WorldState s, EventBuffer ev) => MilitaryLogic.RivalRoundEnd(s, ev);
    }
}
