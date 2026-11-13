using System.Linq;
using NUnit.Framework;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The rules S added in tuning (spec A.5): each one on, and off with the spec's values.</summary>
    public class TuningRulesTests
    {
        [Test]
        public void DemonHeldCamp_CountsNothing_UntilBanished()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            Assert.IsFalse(s.Factions.Tuning.DemonHeldCampsCount);
            s.Demons.Corner[s.Board.StartCorners[0]] = 1;
            K.Step(sim, 1);
            Assert.AreEqual(0, FactionApi.Strength(s, 0).Total);
            Assert.AreEqual(1, s.Factions.Factions[0].HeldCamps);
            s.Demons.Corner[s.Board.StartCorners[0]] = 0;
            K.Step(sim, 1);
            Assert.AreEqual(1, FactionApi.Strength(s, 0).Total);

            s.Factions.Tuning.DemonHeldCampsCount = true; // spec
            s.Demons.Corner[s.Board.StartCorners[0]] = 1;
            K.Step(sim, 1);
            Assert.AreEqual(1, FactionApi.Strength(s, 0).Total);
        }

        [Test]
        public void NoWarning_BeforeTheGrace()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Factions.Tuning.OverrunMargin = 0;
            int grace = s.Factions.Tuning.WarningGraceTicks;
            Assert.Greater(grace, 0);
            s.Factions.Tuning.TimeLimitTicks = int.MaxValue;
            s.Demons.Tuning.GraceTicks = int.MaxValue;
            var ev = K.Step(sim, grace - 1);
            Assert.IsEmpty(ev.OfType<OverrunWarningStarted>());
            ev = K.Step(sim, 1);
            Assert.AreEqual(grace, ev.OfType<OverrunWarningStarted>().Single().Tick);
        }

        [Test]
        public void RivalDelay_ShrinksWithItsLead()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            var tu = s.Factions.Tuning;
            Assert.AreEqual(tu.RivalCastDelayTicks, FactionApi.RivalCastDelay(s), "no lead: the base delay");
            s.Factions.Factions[1].Strength = 3;
            s.Factions.Factions[0].Strength = 1;
            Assert.AreEqual(System.Math.Max(tu.RivalMinDelayTicks, tu.RivalCastDelayTicks - 2 * tu.RivalBoldTicksPerLead), FactionApi.RivalCastDelay(s));
            s.Factions.Factions[0].Strength = 5;
            Assert.AreEqual(tu.RivalCastDelayTicks, FactionApi.RivalCastDelay(s), "behind: no boldness");
            s.Factions.Tuning = new FactionTuning();
            Assert.AreEqual(450, FactionApi.RivalCastDelay(s), "spec: a flat 45 s");
        }

        [Test]
        public void RivalDelay_CanRampOverTime()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            s.Factions.Tuning = new FactionTuning { RivalCastDelayTicks = 1200, RivalCastDelayEndTicks = 300, RivalRampTicks = 9000 };
            Assert.AreEqual(1200, FactionApi.RivalCastDelay(s));
            s.Tick = 4500;
            Assert.AreEqual(750, FactionApi.RivalCastDelay(s));
            s.Tick = 20000;
            Assert.AreEqual(300, FactionApi.RivalCastDelay(s));
        }

        [Test]
        public void DemonDraw_PicksEachPeopleEquallyOften()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _); // the player now has 2 camps and 2 roads, the rival 1 camp
            var d = s.Demons;
            d.Tuning = DemonTunings.Tuned();
            d.Tuning.GraceTicks = 0;
            d.Tuning.SevensPerDraw = 1;
            d.Tuning.Cap = 1000;
            int[] perPeople = new int[2];
            for (int i = 0; i < 400; i++)
            {
                var before = (int[])d.Corner.Clone();
                var beforeE = (int[])d.Edge.Clone();
                DemonLogic.OnRolled(s, new EventBuffer(), 3, 4);
                for (int c = 0; c < d.Corner.Length; c++) if (d.Corner[c] > before[c]) perPeople[EconomyApi.SettlementAt(s, c).Faction]++;
                for (int e = 0; e < d.Edge.Length; e++) if (d.Edge[e] > beforeE[e]) perPeople[EconomyApi.RoadOn(s, e).Faction]++;
            }
            Assert.AreEqual(400, perPeople[0] + perPeople[1]);
            Assert.That(perPeople[1] > 160 && perPeople[0] > 160, "about half each: " + perPeople[0] + " / " + perPeople[1]);
            Assert.Greater(camp, 0);
        }

        [Test]
        public void Lodge_OpensWithBakeryAndSmokehouse_WithoutACity()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            K.Give(s, 0, Ware.Planks, 20);
            K.Give(s, 0, Ware.Bricks, 20);
            var e = s.Economy;
            foreach (var t in new[] { BuildingType.Bakery, BuildingType.Smokehouse })
                e.Buildings.Add(new Building { Id = e.Buildings.Count, Type = t, Faction = 0, Settlement = 0, Plot = t == BuildingType.Bakery ? 634 : 637, Hex = -1, State = BuildingState.Active });
            K.Step(sim, 20);
            Assert.IsTrue(FactionApi.Proposals(s, 0).Any(p => p.BuildingType == (int)BuildingType.WarriorLodge), "the lodge is proposed without a city");
            s.Factions.Tuning.LodgeWithoutCity = false;
            K.Step(sim, 20);
            Assert.IsFalse(FactionApi.Proposals(s, 0).Any(p => p.BuildingType == (int)BuildingType.WarriorLodge), "spec: only with a city");
        }

        [Test]
        public void Cast_RemembersItsTarget_ForWalkablePitches()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var top = FactionApi.Top(s, 0);
            K.Do(sim, new CastSpell { ProposalId = top.Id });
            var fa = s.Factions.Factions[0];
            Assert.IsTrue(fa.HasLastCast);
            Assert.AreEqual((int)System.Math.Round(PlotLayout.PlotX(s.Board.Topology, 631)), fa.LastCastX);
            Assert.AreEqual((int)System.Math.Round(PlotLayout.PlotZ(s.Board.Topology, 631)), fa.LastCastZ);
            Assert.AreEqual(s.Tick, fa.LastCastTick);
        }

        [Test]
        public void BlockingRoadDemon_RanksLikeACampBanish()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int camp = K.SecondCamp(sim, out var path);
            var t = s.Board.Topology;
            int edge = t.HexEdgeBetween(path[1], path[2]);
            s.Demons.Edge[edge] = 1;
            s.Factions.Tuning.QueueLength = 50; // see every candidate
            s.Factions.Tuning.MaxOpenSites = 50;
            K.Step(sim, 20);
            var banish = FactionApi.Proposals(s, 0).FirstOrDefault(p => p.Kind == ProposalKind.Banish);
            Assert.IsNotNull(banish);
            Assert.AreEqual(s.Factions.Tuning.BanishEdgeScore + 10, banish.Score, "nothing waits on it: a low priority");
            // A carrier now waits on that edge: the banish ranks like a camp's.
            var c = s.Economy.Carriers.First(x => x.Home == 0);
            c.State = CarrierState.Returning;
            c.LegFromKind = PlaceKind.Storehouse; c.LegFromId = camp; c.LegToKind = PlaceKind.Storehouse; c.LegToId = 0;
            c.LegCorners = new[] { path[2], path[1], path[0] };
            c.LegTicks = 2 * 12 * s.Economy.Tuning.CarrierTicksPerStep;
            c.Progress = 5;
            K.Step(sim, 20);
            banish = FactionApi.Proposals(s, 0).First(p => p.Kind == ProposalKind.Banish);
            Assert.AreEqual(s.Factions.Tuning.BanishCornerScore + 10, banish.Score);
        }
    }
}
