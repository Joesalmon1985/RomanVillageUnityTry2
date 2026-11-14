using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Helpers for the campaign runtime tests (package I1). They drive the campaign only through commands and read it
    /// through CampaignApi and the state, as the game and the proof do. Definitions come from CampaignSamples.Skeleton().
    /// </summary>
    public static class CampaignRunKit
    {
        /// <summary>The sample skeleton with Mira never going on by herself while the player is with her (the test answers for the player).</summary>
        public static CampaignDefinition Patient()
        {
            var d = CampaignSamples.Skeleton();
            d.Tuning.LeadPatience = int.MaxValue;
            return d;
        }

        public static CampaignState C(WorldSim sim) => sim.State.Campaign;

        /// <summary>No overrun can fire (the 3.9 N-2 test-start fallback), so the expedition clock is time and the test's own charges only.</summary>
        public static void Quiet(WorldSim sim) => sim.State.Factions.Tuning.OverrunMargin = 1000000;

        public static PartyMode Mode(WorldSim sim) => sim.State.Campaign.Party.Mode;

        public static Custody Held(WorldSim sim, Element e) => sim.State.Campaign.Crystal(e).Custody;

        public static LeadView Lead(WorldSim sim, string defId) => CampaignApi.Leads(sim.State).FirstOrDefault(l => l.DefId == defId);

        /// <summary>Steps one tick at a time until the condition holds, collecting the events (fails after max ticks).</summary>
        public static List<SimEvent> Until(WorldSim sim, Func<bool> done, int max, string what)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < max && !done(); i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            Assert.IsTrue(done(), "timed out waiting for: " + what + " (mode " + Mode(sim) + ", tick " + sim.State.Tick + ")");
            return all;
        }

        /// <summary>Submits a command (must be accepted), steps one tick, returns that tick's events.</summary>
        public static List<SimEvent> Do(WorldSim sim, Command c) => SimTestKit.Do(sim, c);

        public static bool Settled(WorldSim sim)
        {
            var m = Mode(sim);
            return m != PartyMode.Travelling && m != PartyMode.Acting;
        }

        /// <summary>
        /// The player answers "Lead the way" to the lead with this definition id: waits for it to be in Mira's queue
        /// (answering "Not that way" to the last one while the queue is full without it), accepts it, and steps until the
        /// party has done what it came for.
        /// </summary>
        public static List<SimEvent> Follow(WorldSim sim, string defId, int max = 6000)
        {
            var all = new List<SimEvent>();
            int n = Math.Max(1, sim.State.Campaign.Definition.Tuning.QueueLength);
            for (int i = 0; i < max && Lead(sim, defId) == null; i++)
            {
                var q = CampaignApi.Leads(sim.State);
                if (q.Count >= n && Settled(sim) && sim.Check(new DeclineLead { LeadId = q[q.Count - 1].Id }).Ok)
                    all.AddRange(Do(sim, new DeclineLead { LeadId = q[q.Count - 1].Id }));
                else
                {
                    sim.Step();
                    all.AddRange(sim.DrainEvents());
                }
            }
            var lead = Lead(sim, defId);
            Assert.IsNotNull(lead, "lead " + defId + " never offered; queue: " + string.Join(", ", CampaignApi.Leads(sim.State).Select(l => l.DefId)));
            all.AddRange(Do(sim, new AcceptLead { LeadId = lead.Id }));
            all.AddRange(Until(sim, () => Settled(sim), max, "the party to finish " + defId));
            return all;
        }

        /// <summary>The state as JSON (the save's own serialisation).</summary>
        public static string Json(WorldSim sim) => SaveCodec.StateJson(sim.State);

        /// <summary>Saves and loads (registering the campaign module, which SaveCodec.CreateSim does not do yet).</summary>
        public static WorldSim SaveLoad(WorldSim sim)
        {
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            CampaignModule.Register(loaded);
            Assert.AreEqual(Json(sim), Json(loaded), "the save holds the whole state");
            return loaded;
        }

        public static IEnumerable<CrystalCustodyChanged> CustodyOf(IEnumerable<SimEvent> ev, Element e) =>
            ev.OfType<CrystalCustodyChanged>().Where(x => x.Crystal == e);

        /// <summary>The chain of custody states a crystal went through, starting from its first From.</summary>
        public static Custody[] Chain(IEnumerable<SimEvent> ev, Element e)
        {
            var list = CustodyOf(ev, e).ToList();
            if (list.Count == 0) return new Custody[0];
            var chain = new List<Custody> { list[0].From };
            foreach (var x in list) chain.Add(x.To);
            return chain.ToArray();
        }
    }
}
