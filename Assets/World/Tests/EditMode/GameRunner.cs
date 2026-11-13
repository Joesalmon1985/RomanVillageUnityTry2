using System;
using System.Collections.Generic;
using System.Linq;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Sim.Tests
{
    /// <summary>What one scripted game did (for the proof and the tuning tests).</summary>
    public sealed class GameResult
    {
        public ulong Seed;
        public string Player;
        public GameOutcome Outcome;
        public string Reason;
        public long EndTick;
        public int PlayerStrength, RivalStrength;
        public int PlayerCasts, RivalCasts, Declines;
        /// <summary>Milestone ticks of the player's people (-1 never): hut, second camp, first workshop, city, first warrior, first warning.</summary>
        public long Hut = -1, SecondCamp = -1, FirstWorkshop = -1, City = -1, FirstWarrior = -1, Warning = -1;
        public long RivalSecondCamp = -1, RivalCity = -1;
        public int[] Produced = new int[Wares.Count], Consumed = new int[Wares.Count], Collected = new int[Wares.Count];
        public int[] StockAdded = new int[Wares.Count], StockLost = new int[Wares.Count];
        /// <summary>Building types completed, per people.</summary>
        public HashSet<BuildingType>[] Built = { new HashSet<BuildingType>(), new HashSet<BuildingType>() };
        /// <summary>Ticks construction sites of the player's people spent waiting for each ware.</summary>
        public int[] SiteWaitTicks = new int[Wares.Count];
        public int[] DiceSums = new int[13];
        public int DemonsPlaced, DemonsBanished, Outbreaks;
        /// <summary>Strength per minute: [minute][people].</summary>
        public List<int[]> StrengthPerMinute = new List<int[]>();
        /// <summary>Longest stretch (ticks) the rival had no open proposal.</summary>
        public long RivalLongestWithoutProposal;
        public string InvariantFailure;
        public long LongestJobWait;
        public string StateJson;

        public double Minutes => EndTick / 600.0;
    }

    public static class GameRunner
    {
        /// <summary>
        /// Plays one game to its end (or maxTicks). hook runs before every step (paired-run variants). Invariants are
        /// checked every 100 ticks when checkInvariants is set (the first failure is recorded).
        /// </summary>
        public static GameResult Play(ulong seed, PlayerStyle style, double delay = 0, Action<WorldSim> setup = null,
            Action<WorldSim> hook = null, int maxTicks = 12000, bool checkInvariants = false, bool keepJson = false,
            Action<WorldSim, IReadOnlyList<SimEvent>> onEvents = null)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            setup?.Invoke(sim);
            var s = sim.State;
            var player = new ScriptedPlayer(sim, style, delay, seed);
            var r = new GameResult { Seed = seed, Player = ScriptedPlayer.Name(style, delay) };
            long rivalEmptySince = -1;
            while (s.Tick < maxTicks && !s.Factions.GameOver)
            {
                player.Act();
                hook?.Invoke(sim);
                sim.Step();
                var ev = sim.DrainEvents();
                onEvents?.Invoke(sim, ev);
                foreach (var e in ev) Record(s, r, e);

                foreach (var b in s.Economy.Buildings)
                    if (b.Faction == 0 && b.State == BuildingState.Site && b.ReasonWare >= 0) r.SiteWaitTicks[b.ReasonWare]++;
                foreach (var j in s.Economy.Jobs) r.LongestJobWait = Math.Max(r.LongestJobWait, j.State == JobState.Waiting ? s.Tick - j.CreatedTick : 0);

                bool rivalHas = FactionApi.Proposals(s, PlayerIds.Rival).Count > 0;
                if (rivalHas) rivalEmptySince = -1;
                else
                {
                    if (rivalEmptySince < 0) rivalEmptySince = s.Tick;
                    r.RivalLongestWithoutProposal = Math.Max(r.RivalLongestWithoutProposal, s.Tick - rivalEmptySince);
                }
                if (s.Tick % 600 == 0)
                    r.StrengthPerMinute.Add(new[] { FactionApi.Strength(s, 0).Total, FactionApi.Strength(s, 1).Total });
                if (checkInvariants && r.InvariantFailure == null && s.Tick % 100 == 0)
                {
                    var f = SimTestKit.CheckInvariants(s);
                    if (f != null) r.InvariantFailure = "tick " + s.Tick + ": " + f;
                }
            }
            r.Outcome = s.Factions.Outcome;
            r.Reason = s.Factions.OutcomeReason;
            r.EndTick = s.Factions.GameOver ? s.Factions.EndedTick : s.Tick;
            r.PlayerStrength = FactionApi.Strength(s, 0).Total;
            r.RivalStrength = FactionApi.Strength(s, 1).Total;
            r.PlayerCasts = s.Factions.Casts[0];
            r.RivalCasts = s.Factions.Casts[1];
            r.Declines = player.Declines;
            var l = s.Economy.Ledger;
            for (int w = 0; w < Wares.Count; w++)
            {
                r.Produced[w] = l.Produced[w];
                r.Consumed[w] = l.Consumed[w];
                r.Collected[w] = l.Collected[w];
                r.StockAdded[w] = l.StockAdded[w];
                r.StockLost[w] = l.StockLost[w];
            }
            r.DemonsPlaced = s.Demons.Placed;
            r.DemonsBanished = s.Demons.Banished;
            r.Outbreaks = s.Demons.Outbreaks;
            if (keepJson) r.StateJson = Save.SaveCodec.StateJson(s);
            return r;
        }

        private static void Record(WorldState s, GameResult r, SimEvent e)
        {
            switch (e)
            {
                case SiteCompleted c when c.Kind == SiteKind.Building:
                {
                    var b = s.Economy.Buildings[c.Id];
                    r.Built[b.Faction].Add(b.Type);
                    if (b.Faction != 0) break;
                    if (b.Type == BuildingType.WoodcutterHut && r.Hut < 0) r.Hut = e.Tick;
                    var cls = BuildingDefs.Get(b.Type).Class;
                    if ((cls == BuildingClass.Workshop || cls == BuildingClass.Lodge) && r.FirstWorkshop < 0) r.FirstWorkshop = e.Tick;
                    break;
                }
                case SettlementFounded f:
                    if (f.Faction == 0 && r.SecondCamp < 0) r.SecondCamp = e.Tick;
                    if (f.Faction == 1 && r.RivalSecondCamp < 0) r.RivalSecondCamp = e.Tick;
                    break;
                case CityUpgraded cu:
                    if (cu.Faction == 0 && r.City < 0) r.City = e.Tick;
                    if (cu.Faction == 1 && r.RivalCity < 0) r.RivalCity = e.Tick;
                    break;
                case WarriorTrained wt when wt.Faction == 0 && r.FirstWarrior < 0:
                    r.FirstWarrior = e.Tick;
                    break;
                case OverrunWarningStarted ow when r.Warning < 0:
                    r.Warning = e.Tick;
                    break;
                case DiceRolled d:
                    r.DiceSums[d.Sum]++;
                    break;
            }
        }

        public static double Median(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => x).ToArray();
            if (a.Length == 0) return 0;
            return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
        }
    }
}
