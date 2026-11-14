using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// The shipped packs (PRD_CRYSTALS I2): the authored pack loads and validates as Partial with only the expected
    /// warnings for the routes not yet written; the skeleton validates as Complete with no problem; both start a
    /// campaign; the definition round-trips and its hash is deterministic; the Earth content matches section 4 and
    /// appendix D.
    /// </summary>
    public class PackTests
    {
        private static string Codes(IEnumerable<Problem> ps) => string.Join("\n", ps.Select(p => (p.IsError ? "E " : "w ") + p));

        // ---------------------------------------------------------------- validation

        [Test]
        public void Skeleton_IsComplete_AndValidatesWithNoProblemAtAll()
        {
            var d = PackFiles.Load(PackFiles.Skeleton);
            Assert.AreEqual(PackStatus.Complete, d.Status);
            var problems = CampaignValidator.Validate(d);
            Assert.AreEqual(0, problems.Count, Codes(problems));
        }

        [Test]
        public void Authored_IsPartial_NoErrors_AndWarnsOnlyAboutUnwrittenRoutesFinaleAndEndings()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            Assert.AreEqual(PackStatus.Partial, d.Status);
            var problems = CampaignValidator.Validate(d);
            Assert.AreEqual(0, problems.Count(p => p.IsError), Codes(problems));
            var expected = new[]
            {
                ProblemCodes.UnreachableClue + ":fire", ProblemCodes.UnreachableClue + ":water", ProblemCodes.UnreachableClue + ":air",
                ValidationCodes.FinaleUnreachable + ":", ValidationCodes.NoEnding + ":", ValidationCodes.NoEnding + ":",
            };
            CollectionAssert.AreEquivalent(expected, problems.Select(p => p.Code + ":" + p.Id).ToArray(), Codes(problems));
            Assert.IsFalse(problems.Any(p => p.Id == "earth" || p.Id.StartsWith("keeper_earth") || p.Id.StartsWith("quarry") || p.Id.StartsWith("tally")),
                "the Earth route itself is clean: " + Codes(problems));
        }

        [Test]
        public void Authored_AsCompleteWouldFailOnlyOnTheUnwrittenParts()
        {
            // The same pack marked Complete: the warnings become errors, and nothing about Earth appears.
            var d = PackFiles.Load(PackFiles.Authored);
            d.Status = PackStatus.Complete;
            var errors = CampaignValidator.Errors(CampaignValidator.Validate(d));
            Assert.AreEqual(6, errors.Count, Codes(errors));
            Assert.IsFalse(errors.Any(p => p.Id == "earth"));
        }

        [TestCase(PackFiles.Skeleton)]
        [TestCase(PackFiles.Authored)]
        public void Pack_StartsACampaign(string pack)
        {
            var d = PackFiles.Load(pack);
            var sim = CampaignSamples.DevGame(11);
            var r = sim.Submit(new StartCampaign { Definition = d });
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            Assert.IsTrue(CampaignApi.IsRunning(sim.State));
            Assert.AreEqual(CampaignLoader.Hash(d), sim.State.Campaign.DefinitionHash);
            Assert.AreEqual(Custody.AtSite, CampaignApi.Crystal(sim.State, Element.Earth).Custody);
            Assert.AreEqual(6, CampaignApi.Companions(sim.State).Count);
            Assert.AreEqual("garrick", CampaignApi.NextVolunteer(sim.State).Id);
        }

        // ---------------------------------------------------------------- loader round trip and hash

        [TestCase(PackFiles.Skeleton)]
        [TestCase(PackFiles.Authored)]
        public void Loader_RoundTrip_CanonicalJsonIsAFixedPoint(string pack)
        {
            var d = PackFiles.Load(pack);
            string json = CampaignLoader.CanonicalJson(d);
            var again = JsonConvert.DeserializeObject<CampaignDefinition>(json, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.AreEqual(json, CampaignLoader.CanonicalJson(again));
            Assert.AreEqual(CampaignLoader.Hash(d), CampaignLoader.Hash(again));
            Assert.AreEqual(0, CampaignValidator.Errors(CampaignValidator.Validate(again)).Count);
        }

        [Test]
        public void Loader_RoundTrip_ThroughTheSave()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var sim = CampaignSamples.DevGame(5);
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = d }).Ok);
            sim.Step();
            string save = SaveCodec.ToJson(sim.State);
            var loaded = SaveCodec.FromJson(save);
            Assert.AreEqual(CampaignLoader.Hash(d), CampaignLoader.Hash(loaded.State.Campaign.Definition));
            Assert.AreEqual(sim.State.Campaign.DefinitionHash, loaded.State.Campaign.DefinitionHash);
            Assert.AreEqual("glade", loaded.State.Campaign.Definition.Interior("quarry_upper").Location);
        }

        [Test]
        public void Hash_IsDeterministic_AndIndependentOfTheOrderFilesAreListed()
        {
            string folder = PackFiles.PackFolder(PackFiles.Authored);
            var names = Directory.GetFiles(folder, "*.json").Select(Path.GetFileName).ToList();
            string a = CampaignLoader.Hash(CampaignLoader.LoadFolder(n => File.ReadAllText(Path.Combine(folder, n)), CampaignLoader.PackOrder(names)));
            names.Reverse();
            string b = CampaignLoader.Hash(CampaignLoader.LoadFolder(n => File.ReadAllText(Path.Combine(folder, n)), CampaignLoader.PackOrder(names)));
            string c = CampaignLoader.Hash(PackFiles.Load(PackFiles.Authored));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a, c);
            Assert.AreEqual(64, a.Length);
            Assert.AreEqual("campaign.json", CampaignLoader.PackOrder(names)[0]);
            CollectionAssert.DoesNotContain(CampaignLoader.PackOrder(new[] { "x.json.meta", "a.json" }), "x.json.meta");

            var changed = PackFiles.Load(PackFiles.Authored);
            changed.Line("mira_stone_1").Text += " ";
            Assert.AreNotEqual(a, CampaignLoader.Hash(changed));
            Assert.AreNotEqual(a, CampaignLoader.Hash(PackFiles.Load(PackFiles.Skeleton)));
        }

        // ---------------------------------------------------------------- the Earth content (section 4, appendix D)

        [Test]
        public void Authored_Companions_AreSection4InVolunteerOrder()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var byOrder = d.Companions.OrderBy(k => k.VolunteerOrder).ToList();
            CollectionAssert.AreEqual(new[] { "Garrick Holt", "Ivo Quill", "Petra Coalhand", "Nell Farrow", "Odo Birch", "Tansy Reed" }, byOrder.Select(k => k.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, byOrder.Select(k => k.VolunteerOrder).ToArray());
            CollectionAssert.AreEqual(new[] { "shield", "lamp", "fire", "water", "earth", "scout" }, byOrder.Select(k => k.Service).ToArray());
            foreach (var k in byOrder)
            {
                Assert.AreEqual("companion_" + k.Id, k.ActorId);
                var line = d.Line(k.Line);
                Assert.AreEqual(k.Id, line.Speaker);
                CollectionAssert.Contains(line.Guards, "alive: " + k.Id);
            }
            StringAssert.AreEqualIgnoringCase("Somebody has to be last out of a bad place. I've had the practice.", d.Line("companion_garrick_line").Text);
        }

        [Test]
        public void Authored_Cast_IsSection4()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            Assert.AreEqual("Hesk Varr", d.Role("rival_leader").Name);
            Assert.AreEqual("the Unweaver, in Hesk Varr", d.Role("final_enemy").Name);
            Assert.AreEqual("Orrin the Watcher", d.Role("wizard").Name);
            Assert.AreEqual("Dolm the Tally-Keeper", d.Role("keeper_earth").Name);
            Assert.AreEqual("the Deepstone", d.Crystal("earth").Name);
            Assert.AreEqual("the Rooted Tower", d.Location("tower_earth").Name);
            Assert.AreEqual("the Stonemark Glade", d.Location("glade").Name);
            Assert.AreEqual("the Old Quarry", d.Interior("quarry_upper").Name);
            Assert.AreEqual("the Tally Vault", d.Interior("tally_vault").Name);
            var keeper = d.Encounter("keeper_earth");
            Assert.AreEqual(EncounterKind.Lethal, keeper.Kind);
            Assert.AreEqual("keeper_earth", keeper.Profile, "the Ward opponent profile id the keeper uses (I3)");
            Assert.AreEqual("keeper_earth", keeper.OpponentActorId);
            Assert.AreEqual("glade/quarry_upper/safe", keeper.SafePoint.ToString(), "the safe point is the survey gallery");
            Assert.AreEqual("ash_cut/tally_vault/altar", d.Rival.Steps[0].Altar.ToString());
            // The race tuning (sim-H1-cry-S1-tuned): the opening march is drawn per run in 2250 +- 600 (2:45 to 4:45), was 4800.
            Assert.AreEqual(2250, d.Rival.Steps[0].MarchCost);
            Assert.AreEqual(600, d.Rival.Steps[0].MarchSpread);
            CollectionAssert.AreEqual(new[] { "earth" }, d.Rival.OpenWith, "the Partial pack's expedition always opens with the Deepstone");
        }

        [Test]
        public void Authored_EarthFacts_AreAppendixD()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var given = new HashSet<string>();
            foreach (var c in d.Clues) given.UnionWith(c.Gives);
            foreach (var e in d.Events) given.UnionWith(e.Sets);
            foreach (var e in d.Encounters) foreach (var fx in e.OnVictory) if (fx.Kind == EffectKind.SetFact) given.Add(fx.Ref);
            foreach (var f in new[] { "earth.marks_heard", "earth.glade_known", "earth.stair_open", "earth.root_way_known", "tower.earth_known",
                                      "earth.stolen_seen", "earth.tally_read", "earth.vault_known", "earth.ash_cut_open" })
                CollectionAssert.Contains(given, f);
            Assert.AreEqual("earth.stair_open", d.Event("earth_read_stone").Sets.Single());
            CollectionAssert.AreEquivalent(new[] { "earth.site_seen", "earth.stolen_seen", "earth.tally_read" }, d.Event("earth_empty").Sets);
            Assert.AreEqual("earth_posts", d.Clue("earth_sawyer").Fallback, "the essential clue's fallback is in the world");
            Assert.AreEqual(SourceKind.Marker, d.Clue("earth_posts").Source.Kind);
            Assert.AreEqual(SourceKind.Companion, d.Clue("earth_odo").Source.Kind, "Odo reads the marks on sight");
        }

        [Test]
        public void Authored_Lines_FitABubble_AndCompanionsSpeakOnlyAlive()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            Assert.GreaterOrEqual(d.Lines.Count, 80);
            foreach (var l in d.Lines)
            {
                Assert.LessOrEqual(l.Text.Length, CampaignValidator.MaxLineChars, l.Id);
                if (d.Companion(l.Speaker) != null) CollectionAssert.Contains(l.Guards, "alive: " + l.Speaker, l.Id);
            }
        }

        [Test]
        public void Authored_LinesFollowState_InARun()
        {
            var sim = CampaignSamples.DevGame(3);
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) }).Ok);
            sim.Step();
            var s = sim.State;
            StringAssert.Contains("Hesk Varr", CampaignApi.Line(s, "mira_open_4"));
            Assert.AreEqual("Last we heard, the Deepstone was at the Old Quarry.", CampaignApi.Line(s, "mira_where_earth"));
            Assert.IsNull(CampaignApi.Line(s, "journal_earth_recovered"), "guarded until it happened");
            Assert.IsNull(CampaignApi.Line(s, "mira_after_garrick"), "Garrick is alive");
            Assert.IsNotNull(CampaignApi.Line(s, "companion_odo_line"));
            StringAssert.Contains("Garrick Holt holds the way out", CampaignApi.Line(s, "mira_threshold_earth"));

            Assert.IsTrue(sim.Submit(new DevSetCompanion { Id = "garrick", Alive = false }).Ok);
            Assert.IsTrue(sim.Submit(new DevSetCompanion { Id = "odo", Alive = false }).Ok);
            sim.Step();
            Assert.IsNull(CampaignApi.Line(s, "companion_odo_line"), "no dead speaker");
            Assert.IsNotNull(CampaignApi.Line(s, "mira_after_garrick"));
            StringAssert.Contains("Ivo Quill holds the way out", CampaignApi.Line(s, "mira_threshold_earth"));

            Assert.IsTrue(sim.Submit(new DevSetCustody { Crystal = Element.Earth, To = Custody.AtRivalAltar }).Ok);
            sim.Step();
            Assert.AreEqual("Last we heard, the Deepstone was at the Old Quarry.", CampaignApi.Line(s, "mira_where_earth"), "belief, not truth");
            Assert.IsNull(CampaignApi.Line(s, "prince_earth_lit"));
            Assert.IsTrue(sim.Submit(new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored }).Ok);
            sim.Step();
            StringAssert.Contains("south-west shore", CampaignApi.Line(s, "prince_earth_lit"));
            Assert.AreEqual("Mira set the Deepstone in the Rooted Tower. The tower is lit.", CampaignApi.Line(s, "journal_earth_restored"));
        }
    }
}
