using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>Score and breach state at the start of a round (for the ending timelines).</summary>
    internal sealed class RoundLine
    {
        public int Round, Age, RoundInAge, Breaches;
        public long Tick;
        public ScoreBreakdown Player, Rival;
    }

    /// <summary>Everything the playthrough tests and the proof report need from one scripted game.</summary>
    internal sealed class GameRecord
    {
        public ulong Seed;
        public string Mode;
        public long Ticks;
        public GameOutcome Outcome;
        public string Reason;
        public ScoreBreakdown Player, Rival;
        public int Breaches, Cities, CardsBuilt, WonderStages, UnitsTrained, Rounds, Outbreaks, Epidemics;
        public bool OutpostDestroyed;
        /// <summary>Card definition ids built by the player and by the rival (in build order).</summary>
        public List<string> PlayerCards = new List<string>(), RivalCards = new List<string>();
        /// <summary>Card definition ids the player drafted (both picks of every age).</summary>
        public List<string> PlayerPicks = new List<string>();
        /// <summary>Shields the rival's built cards give.</summary>
        public int RivalCardShields => RivalCards.Sum(id => CardTable.Get(id).Shields);
        /// <summary>Distinct card families among the player's built cards.</summary>
        public int Families => PlayerCards.Select(id => CardTable.Get(id).Family).Distinct().Count();
        /// <summary>Ticks from the first round of an age to the first round of the next (or the end); -1 if the age was not reached.</summary>
        public long AgeTicks(int age)
        {
            var first = Timeline.FirstOrDefault(x => x.Age == age && x.RoundInAge == 1);
            if (first == null) return -1;
            var next = Timeline.FirstOrDefault(x => x.Age == age + 1 && x.RoundInAge == 1);
            return (next != null ? next.Tick : Ticks) - first.Tick;
        }
        /// <summary>Length in ticks of every round of the ages that was ended by the bell.</summary>
        public List<long> AgeRoundTicks()
        {
            var l = new List<long>();
            for (int i = 0; i + 1 < Timeline.Count; i++)
                if (Timeline[i].Age > 0) l.Add(Timeline[i + 1].Tick - Timeline[i].Tick);
            return l;
        }
        public SortedDictionary<string, long> Milestones = new SortedDictionary<string, long>(StringComparer.Ordinal);
        public Dictionary<string, int> EventCounts = new Dictionary<string, int>();
        public List<RoundLine> Timeline = new List<RoundLine>();
        public List<MilitaryComparison> Comparisons = new List<MilitaryComparison>();
        /// <summary>Building-ticks spent waiting for each ware (reasons waiting_ware and input_missing naming it).</summary>
        public long[] StallTicksByWare = new long[Wares.Count];
        public long CarrierBusyTicks, CarrierTicks;
        public int Carriers;
        public WareLedgerCopy Ledger;
        /// <summary>Every event (kept only when asked, for the traced chains).</summary>
        public List<SimEvent> Events;
        /// <summary>Building id -> (type, flag) at the end, for describing events.</summary>
        public List<(BuildingType type, int vertex)> Buildings = new List<(BuildingType, int)>();

        public int PlayerVp => Player.Total;
        public int RivalVp => Rival.Total;
        public double Minutes => Ticks / 600.0;
        public long Milestone(string name) => Milestones.TryGetValue(name, out long t) ? t : -1;

        /// <summary>The ware named most in stall reasons, weighted by ticks.</summary>
        public Ware Bottleneck
        {
            get
            {
                int best = 0;
                for (int w = 1; w < Wares.Count; w++) if (StallTicksByWare[w] > StallTicksByWare[best]) best = w;
                return (Ware)best;
            }
        }

        public int CarrierUsePercent => CarrierTicks == 0 ? 0 : (int)(100 * CarrierBusyTicks / CarrierTicks);

        public override string ToString() =>
            $"seed {Seed} {Mode}: {Outcome} ({Reason}) at tick {Ticks} = {Minutes:0.0} min, VP {PlayerVp}-{RivalVp}, rounds {Rounds}, cities {Cities}, cards {CardsBuilt}, " +
            $"wonder {WonderStages}, units {UnitsTrained}, outpost {(OutpostDestroyed ? "destroyed" : "standing")}, breaches {Breaches}; " +
            string.Join(", ", Milestones.Select(kv => kv.Key + "@" + kv.Value));
    }

    /// <summary>Copy of the economy ledger at the end of a game.</summary>
    internal sealed class WareLedgerCopy
    {
        public int[] Harvested, Produced, Consumed, Construction, Roads, External, BatchAuthorised, BatchLost;
        public int[][] RecipeIn, RecipeOut;
        public int[] Cycles;
    }

    /// <summary>
    /// Scripted full games shared by the playthrough tests and the proof report (run once per test run, cached).
    /// Modes: competent, full (also attacks the outpost in Age III), careless (never moves the hero), passive (only
    /// drafts and ends rounds), noarmy (competent without barracks or soldiers), firstpick (competent, but drafts the first card offered).
    /// </summary>
    internal static class GameRuns
    {
        public const int TickBudget = 20000; // 33 minutes of sim time
        public static readonly string[] Modes = { "competent", "full", "careless", "passive" };

        public static ulong OtherSeed(ulong i) => i * 104729UL + 11UL;

        /// <summary>The default seed and ten others.</summary>
        public static readonly ulong[] Seeds = new[] { EconomyTestKit.DefaultSeed }.Concat(Enumerable.Range(1, 10).Select(i => OtherSeed((ulong)i))).ToArray();

        private static readonly Dictionary<string, GameRecord> Cache = new Dictionary<string, GameRecord>();

        public static GameRecord Get(ulong seed, string mode, bool keepEvents = false)
        {
            string key = seed + "/" + mode;
            if (Cache.TryGetValue(key, out var r) && (!keepEvents || r.Events != null)) return r;
            r = Play(seed, mode, keepEvents);
            Cache[key] = r;
            return r;
        }

        public static StrategyPlayer NewPlayer(WorldSim sim, string mode) =>
            new StrategyPlayer(sim, mode == "passive", mode == "full") { IgnoreDemons = mode == "careless", NoArmy = mode == "noarmy", NaiveDraft = mode == "firstpick" };

        public static GameRecord Play(ulong seed, string mode, bool keepEvents)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            var player = NewPlayer(sim, mode);
            var rep = new GameRecord { Seed = seed, Mode = mode, Events = keepEvents ? new List<SimEvent>() : null };
            for (int t = 0; t < TickBudget && !s.Strategy.GameOver; t++)
            {
                player.Act();
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    rep.Events?.Add(e);
                    string k = e.GetType().Name;
                    rep.EventCounts[k] = rep.EventCounts.TryGetValue(k, out int n) ? n + 1 : 1;
                    if (e is CommandRejected cr && !(cr.Command is EndRound))
                        rep.EventCounts["rejected:" + cr.Command.GetType().Name + ":" + cr.Result.Reason] = 1;
                    // (After the game ended the economy still opens the next round in the same tick: not a round that was played.)
                    if (e is RoundStarted && !s.Strategy.GameOver)
                        rep.Timeline.Add(new RoundLine
                        {
                            Round = s.Economy.Round, Age = s.Strategy.Age, RoundInAge = s.Strategy.RoundInAge, Tick = e.Tick,
                            Player = StrategyApi.Score(s, 0), Rival = StrategyApi.Score(s, 1), Breaches = s.Strategy.Breaches,
                        });
                    if (e is OutbreakOccurred) rep.Outbreaks++;
                    if (e is DraftPicked dp && dp.PlayerId == PlayerIds.Human) rep.PlayerPicks.Add(dp.DefId);
                }
                foreach (var b in s.Economy.Buildings)
                    if ((b.Reason == Reasons.WaitingWare || b.Reason == Reasons.InputMissing) && b.ReasonWare >= 0) rep.StallTicksByWare[b.ReasonWare]++;
                foreach (var q in s.Military.Queues)
                    if (q.Reason == Reasons.WaitingWare && q.ReasonWare >= 0) rep.StallTicksByWare[q.ReasonWare]++;
                foreach (var c in s.Economy.Carriers)
                {
                    rep.CarrierTicks++;
                    if (c.JobId >= 0) rep.CarrierBusyTicks++;
                }
                if (t % 25 == 0)
                {
                    var err = StrategyTestKit.CheckInvariants(s);
                    if (err != null) Assert.Fail("seed " + seed + " " + mode + " tick " + s.Tick + ": " + err);
                }
            }
            var final = StrategyTestKit.CheckInvariants(s);
            if (final != null) Assert.Fail("seed " + seed + " " + mode + " end: " + final);
            rep.Ticks = s.Tick;
            rep.Outcome = s.Strategy.Outcome;
            rep.Reason = s.Strategy.OutcomeReason;
            rep.Player = StrategyApi.Score(s, 0);
            rep.Rival = StrategyApi.Score(s, 1);
            rep.Breaches = s.Strategy.Breaches;
            rep.Epidemics = s.Strategy.Epidemics;
            rep.Cities = StrategyApi.PlayerCities(s).Count;
            rep.CardsBuilt = s.Strategy.Sides[0].Built.Count;
            foreach (var b in s.Strategy.Sides[0].Built) rep.PlayerCards.Add(s.Strategy.Cards[b.CardId].DefId);
            foreach (var b in s.Strategy.Sides[1].Built) rep.RivalCards.Add(s.Strategy.Cards[b.CardId].DefId);
            rep.WonderStages = s.Strategy.Wonder.StagesDone;
            rep.UnitsTrained = s.Military.UnitsTrained;
            rep.OutpostDestroyed = s.Military.Outpost.Destroyed;
            rep.Rounds = s.Economy.Round;
            rep.Carriers = s.Economy.Carriers.Count;
            rep.Comparisons.AddRange(s.Strategy.Comparisons);
            foreach (var kv in player.Milestones) rep.Milestones[kv.Key] = kv.Value;
            foreach (var b in s.Economy.Buildings) rep.Buildings.Add((b.Type, b.Vertex));
            var l = s.Economy.Ledger;
            rep.Ledger = new WareLedgerCopy
            {
                Harvested = (int[])l.Harvested.Clone(), Produced = (int[])l.Produced.Clone(), Consumed = (int[])l.Consumed.Clone(),
                Construction = (int[])l.Construction.Clone(), Roads = (int[])l.Roads.Clone(), External = (int[])l.External.Clone(),
                BatchAuthorised = (int[])l.BatchAuthorised.Clone(), BatchLost = (int[])l.BatchLost.Clone(),
                RecipeIn = l.RecipeIn.Select(x => (int[])x.Clone()).ToArray(), RecipeOut = l.RecipeOut.Select(x => (int[])x.Clone()).ToArray(),
                Cycles = (int[])l.Cycles.Clone(),
            };
            return rep;
        }
    }
}
