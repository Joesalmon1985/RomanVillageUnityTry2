using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>
    /// Validator v1 (PRD_CRYSTALS 6.5, package I2). Every broken fixture is rejected with exactly its code; every check
    /// has a passing case (the skeleton, which has no problem at all) and a failing case (a fixture or a small edit
    /// here); Partial packs get warnings where Complete packs get errors; interior ways in and out.
    /// </summary>
    public class ValidatorTests
    {
        private static CampaignDefinition Skeleton() => PackFiles.Load(PackFiles.Skeleton);

        private static string Show(IEnumerable<Problem> ps) => string.Join("\n", ps.Select(p => (p.IsError ? "E " : "w ") + p + " [" + p.Id + "]"));

        private static IReadOnlyList<Problem> Check(CampaignDefinition d, ValidationOptions o = null) => CampaignValidator.Validate(d, o);

        private static void AssertError(CampaignDefinition d, string code, ValidationOptions o = null)
        {
            var ps = Check(d, o);
            Assert.IsTrue(ps.Any(p => p.IsError && p.Code == code), "expected " + code + ":\n" + Show(ps));
            foreach (var p in ps) Assert.IsFalse(string.IsNullOrWhiteSpace(p.Sentence), p.Code);
        }

        private static IEnumerable<string> FixtureFiles() => FixturePack.All();

        // ---------------------------------------------------------------- the broken fixtures

        [TestCaseSource(nameof(FixtureFiles))]
        public void Fixture_IsRejectedWithExactlyItsCode(string path)
        {
            var fx = FixturePack.Read(path);
            Assert.AreEqual(fx.Name, fx.Expect, "a fixture file is named after its code");
            var d = fx.Build();
            var errors = CampaignValidator.Errors(CampaignValidator.Validate(d));
            Assert.IsNotEmpty(errors, fx.Why);
            CollectionAssert.AreEquivalent(new[] { fx.Expect }, errors.Select(p => p.Code).Distinct().ToArray(), fx.Why + ":\n" + Show(errors));

            var sim = WorldSimFactory.CreateNew(1);
            var r = sim.Submit(new StartCampaign { Definition = d });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(CampaignRefusals.InvalidDefinition, r.Reason);
            StringAssert.Contains(errors[0].Sentence, r.Message);
        }

        [Test]
        public void Fixtures_CoverEverySixPointFiveCode()
        {
            var codes = FixturePack.All().Select(p => FixturePack.Read(p).Expect).ToList();
            CollectionAssert.AreEquivalent(ProblemCodes.All.Where(c => c != ProblemCodes.Missing).ToArray(), codes);
        }

        [Test]
        public void Skeleton_PassesEveryCheck()
        {
            var ps = Check(Skeleton());
            Assert.AreEqual(0, ps.Count, Show(ps));
        }

        // ---------------------------------------------------------------- form

        [Test]
        public void Form_MissingAndVersion()
        {
            Assert.AreEqual(ProblemCodes.Missing, CampaignValidator.Validate(null).Single().Code);
            var d = Skeleton();
            d.FormatVersion = 0;
            Assert.AreEqual(ProblemCodes.BadVersion, Check(d).Single().Code);
            d = Skeleton();
            d.Lines = null;
            Assert.AreEqual(ProblemCodes.Missing, Check(d).Single().Code);
            d = Skeleton();
            d.Leads.Add(null);
            AssertError(d, ProblemCodes.Missing);
            d = Skeleton();
            d.Clues[0].Id = "";
            AssertError(d, ProblemCodes.Missing);
        }

        [Test]
        public void Form_DuplicateIds_InEveryScope()
        {
            var d = Skeleton();
            d.Lines.Add(new LineDef { Id = "mira_take", Speaker = "mira", Text = "Again." });
            AssertError(d, ProblemCodes.DuplicateId);
            d = Skeleton();
            d.Roles.Add(new RoleDef { Id = "odo", ActorId = "x", Name = "a second Odo" });   // people share one namespace
            AssertError(d, ProblemCodes.DuplicateId);
            d = Skeleton();
            d.Location("forge").Markers.Add(new MarkerDef { Id = "altar", Kind = MarkerKind.Inscription, Name = "x" });
            AssertError(d, ProblemCodes.DuplicateId);
            d = Skeleton();
            d.Interior("skel_upper").Rows[2] = "#SPD#";   // the pedestal's character twice
            AssertError(d, ProblemCodes.DuplicateId);
        }

        [Test]
        public void Form_Names()
        {
            var d = Skeleton();
            d.Location("kiln").Name = "";
            AssertError(d, ValidationCodes.NoName);
            d = Skeleton();
            d.Interior("skel_lower").Legend['A'].Name = "";
            AssertError(d, ValidationCodes.NoName);
            d = Skeleton();
            d.Lead("fire_site").Title = "";
            AssertError(d, ValidationCodes.NoName);
        }

        [Test]
        public void Form_References()
        {
            void Dangles(Action<CampaignDefinition> edit)
            {
                var d = Skeleton();
                edit(d);
                AssertError(d, ProblemCodes.DanglingReference);
            }
            Dangles(d => d.Lead("fire_site").ReasonLine = "no_such_line");
            Dangles(d => d.Crystal("fire").Site = MarkerRef.Surface("kiln", "safe"));                  // not a pedestal
            Dangles(d => d.Location("glade").Markers[0].To = "glade/skel_upper/nowhere");
            Dangles(d => d.Connections[0].B = new MarkerRef("glade", "skel_lower", "up"));             // the markers do not lead to each other
            Dangles(d => d.Encounter("keeper_fire").Threshold = MarkerRef.Surface("forge", "gate"));
            Dangles(d => d.Event("fire_take").Effects.Add(new EffectDef { Kind = EffectKind.OfferChoice, Ref = "no_choice" }));
            Dangles(d => d.Clue("fire_elder").Source.Ref = "home/nobody");
            Dangles(d => d.Clue("fire_petra").Source.Ref = "hod");
            Dangles(d => d.Line("mira_take").Speaker = "hod");
            Dangles(d => d.Line("mira_take").Guards.Add("alive: hod"));
            Dangles(d => d.Line("mira_take").Text = "Got {crystal.moon.name}.");
            Dangles(d => d.Companions[0].Service = "juggling");
            Dangles(d => d.Towers[0].Sign = "nope");
            var o = new ValidationOptions { KnownProfiles = new[] { "keeper_earth", "keeper_fire", "keeper_water", "finale_unweaver" } };
            AssertError(Skeleton(), ProblemCodes.DanglingReference, o);   // keeper_air is not a known profile
            o.KnownProfiles = new[] { "keeper_earth", "keeper_fire", "keeper_water", "keeper_air", "finale_unweaver" };
            Assert.AreEqual(0, Check(Skeleton(), o).Count);
        }

        [Test]
        public void Form_GuardsTokensAndLengths()
        {
            var d = Skeleton();
            d.Line("mira_take").Guards.Add("alive odo");
            AssertError(d, ValidationCodes.BadGuard);
            d = Skeleton();
            d.Line("mira_take").Guards.Add("custody: earth = Lost");
            AssertError(d, ValidationCodes.BadGuard);
            d = Skeleton();
            d.Event("air_take").Guards.Add("restored: 9");
            AssertError(d, ValidationCodes.BadGuard);
            d = Skeleton();
            d.Line("mira_take").Text = "Got {crystal.earth.colour}.";
            AssertError(d, ValidationCodes.BadToken);
            d = Skeleton();
            d.Line("mira_take").Text = "Got {volunteer.name.";
            AssertError(d, ValidationCodes.BadToken);
            d = Skeleton();
            d.Line("mira_take").Text = new string('a', CampaignValidator.MaxLineChars + 1);
            AssertError(d, ValidationCodes.LineTooLong);
            d = Skeleton();
            d.Line("answer_west").Text = "The wind that blows from the far west";
            AssertError(d, ValidationCodes.LineTooLong);
        }

        [Test]
        public void Form_ChoicesAndEncounters()
        {
            var d = Skeleton();
            d.Choice("air_wind").Answers[0].Correct = true;
            AssertError(d, ValidationCodes.BadChoice);
            d = Skeleton();
            d.Encounter("keeper_air").Profile = "";
            AssertError(d, ValidationCodes.BadEncounter);
            d = Skeleton();
            d.Encounters.Add(new EncounterDef { Id = "finale_2", Name = "x", Kind = EncounterKind.Lethal, Profile = "p", OpponentActorId = "a", OpponentName = "b", BoundTo = EncounterBindings.LastTower });
            AssertError(d, ValidationCodes.BadEncounter);
        }

        // ---------------------------------------------------------------- roles, crystals, towers, rival

        [Test]
        public void Roles_AndCompanions()
        {
            var d = Skeleton();
            d.Roles.RemoveAll(r => r.Id == "wizard");
            AssertError(d, ValidationCodes.RoleUnbound);
            d = Skeleton();
            d.Role("prince").ActorId = "";
            AssertError(d, ValidationCodes.RoleUnbound);
            d = Skeleton();
            d.Companions.Add(new CompanionDef { Id = "seventh", Name = "x", Role = "y", Line = "mira_take", VolunteerOrder = 7, ActorId = "z" });
            AssertError(d, ProblemCodes.CompanionCount);
            d = Skeleton();
            d.Companion("tansy").VolunteerOrder = 0;
            AssertError(d, ProblemCodes.VolunteerOrder);
        }

        [Test]
        public void Crystals_AndTowers()
        {
            var d = Skeleton();
            d.Crystal("air").Element = Element.Water;
            AssertError(d, ValidationCodes.CrystalCount);
            d = Skeleton();
            d.Crystal("fire").Tower = "tower_water";
            d.Crystal("water").Tower = "tower_fire";
            AssertError(d, ProblemCodes.MissingTower);   // a tower of the wrong element
            d = Skeleton();
            d.Crystal("earth").Underground = false;
            AssertError(d, ValidationCodes.Underground);
            d = Skeleton();
            d.Location("tower_water").Markers.RemoveAll(m => m.Kind == MarkerKind.SafePoint);
            d.Location("tower_water").SafePoint = "";
            AssertError(d, ProblemCodes.NoFinaleStage);
        }

        [Test]
        public void RivalPlan()
        {
            var d = Skeleton();
            d.Rival.Steps[1].MarchCost = 0;
            AssertError(d, ProblemCodes.RivalPlan);
            d = Skeleton();
            d.Rival.Steps[1].Altar = MarkerRef.Surface("kiln", "pedestal");   // not an altar
            AssertError(d, ProblemCodes.RivalPlan);
            d = Skeleton();
            d.Rival.StolenAtStart.Add("moon");
            AssertError(d, ProblemCodes.RivalPlan);
            d = Skeleton();
            d.Rival.LeaderRole = "nobody";
            AssertError(d, ProblemCodes.RivalPlan);
        }

        [Test]
        public void RivalPlan_StolenAtStart_IsRecoverable()
        {
            var d = Skeleton();
            d.Rival.StolenAtStart.Add("water");
            var ps = Check(d);
            Assert.AreEqual(0, ps.Count, Show(ps));
            d.Lead("water_altar").Requires = new List<string> { "water.never" };
            AssertError(d, ProblemCodes.NoRecovery);
        }

        // ---------------------------------------------------------------- places (landmark and plot collisions via the board)

        [Test]
        public void Places()
        {
            var d = Skeleton();
            d.Location("cistern").Anchor.Q = 1;
            d.Location("cistern").Anchor.R = -4;   // the causeway's hex
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("kiln").Markers[1].I = 2;   // 10 m from the hex centre
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("tower_air").Anchor.Corner = 16;   // the harbour landmark's corner
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("tower_air").Anchor.Corner = 70;   // the player's start corner, where three hexes meet
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("tower_air").Anchor.Corner = 74;   // the fire tower's corner
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("tower_earth").Anchor.Corner = -1;   // the bearing rule: still corner 17
            Assert.AreEqual(0, Check(d).Count);
            d.Location("kiln").Anchor = new AnchorDef { Kind = AnchorKind.RimCorner, Corner = -1 };   // no tower there: no rule
            AssertError(d, ProblemCodes.BadAnchor);
            d = Skeleton();
            d.Location("tower_fire").Markers[0].I = 2;   // off the shore: a warning only
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => !p.IsError && p.Code == ProblemCodes.BadAnchor), Show(ps));
            Assert.AreEqual(0, ps.Count(p => p.IsError));
            d = Skeleton();
            d.Locations.RemoveAll(l => l.Id == "home");
            AssertError(d, ProblemCodes.BadAnchor);
        }

        [Test]
        public void Places_PlotCollision()
        {
            // A plot centre of the home camp, written as a marker offset of a site: the validator finds the plot.
            var d = Skeleton();
            var t = CampaignPlaces.FixedBoard.Topology;
            int corner = CampaignPlaces.FixedBoard.StartCorners[0];
            int plot = PlotLayout.PlotId(corner, 0, PlotLayout.CitySlot);
            Lattice.CellOf(PlotLayout.PlotX(t, plot), PlotLayout.PlotZ(t, plot), out int pi, out int pj);
            var tower = d.Location("tower_fire");
            Lattice.CellOf(t.VertexX(74), t.VertexZ(74), out int ai, out int aj);
            tower.Markers[0].I = pi - ai;
            tower.Markers[0].J = pj - aj;
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => p.IsError && p.Code == ProblemCodes.BadAnchor && p.Sentence.Contains("plot")), Show(ps));
        }

        // ---------------------------------------------------------------- interiors: shape, ways in and out

        [Test]
        public void Interiors_Shape()
        {
            var d = Skeleton();
            d.Interior("skel_upper").Rows[0] = new string('#', 17);
            AssertError(d, ValidationCodes.BadInterior);
            d = Skeleton();
            var big = d.Interior("skel_lower");
            big.Rows.Clear();
            big.Rows.Add("################");
            for (int j = 0; j < 14; j++) big.Rows.Add(j == 0 ? "#U.H..........##" : j == 1 ? "#AKC...........#" : "#..............#");
            big.Rows.Add("################");
            AssertError(d, ValidationCodes.BadInterior);   // more than 80 floor cells
            d = Skeleton();
            d.Interior("skel_lower").Legend['#'] = new MarkerDef { Id = "x", Kind = MarkerKind.Lever, Name = "x" };
            AssertError(d, ValidationCodes.BadInterior);
        }

        [Test]
        public void Interiors_NoWayIn()
        {
            var d = Skeleton();
            var orphan = new InteriorDef { Id = "orphan", Name = "a sealed room", Location = "glade" };
            orphan.Rows.AddRange(new[] { "####", "#..#", "####" });
            d.Interiors.Add(orphan);
            AssertError(d, ProblemCodes.NoWayOut);
        }

        [Test]
        public void Interiors_DoorThatNeverOpens_ShutsTheOnlyWayOut()
        {
            var d = Skeleton();
            var lower = d.Interior("skel_lower");
            lower.Rows.Clear();
            lower.Rows.AddRange(new[] { "#######", "#U.X.H#", "###AKC#", "#######" });
            lower.Legend['X'] = new MarkerDef { Id = "gate", Kind = MarkerKind.Door, Name = "a gate", OpenWhen = "never.open" };
            AssertError(d, ProblemCodes.NoWayOut);
        }

        [Test]
        public void Earth_Interiors_LeadInAndOut()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var upper = d.Interior("quarry_upper");
            var vault = d.Interior("tally_vault");
            Assert.LessOrEqual(InteriorMaps.FloorCells(upper).Length / 2, InteriorMaps.MaxFloorCells);
            Assert.IsTrue(InteriorMaps.TryFind(upper, "exit", out int ei, out int ej));
            Assert.IsTrue(InteriorMaps.TryFind(upper, "pedestal", out int pi, out int pj));
            Assert.IsTrue(InteriorMaps.TryFind(upper, "stair_down", out int di, out int dj));
            Assert.IsTrue(InteriorMaps.TryFind(upper, "rubble", out int ri, out int rj));
            Assert.IsTrue(InteriorMaps.TryFind(upper, "marks", out int mi, out int mj));
            Assert.IsTrue(InteriorMaps.TryFind(vault, "stair_up", out int ui, out int uj));
            Assert.IsTrue(InteriorMaps.TryFind(vault, "altar", out int ai, out int aj));
            Assert.IsTrue(InteriorMaps.TryFind(vault, "cut_exit", out int xi, out int xj));
            Func<string, bool> none = _ => false;

            // In: the entry stair to the pedestal, and on to the stair down.
            Assert.IsNotEmpty(InteriorMaps.Path(upper, ei, ej, pi, pj, none));
            Assert.IsNotEmpty(InteriorMaps.Path(upper, ei, ej, di, dj, none));
            // Out from the Root Chamber, and from the Tally Vault by the stair before Dolm is beaten.
            Assert.IsNotEmpty(InteriorMaps.Path(upper, pi, pj, ei, ej, none));
            Assert.IsNotEmpty(InteriorMaps.Path(vault, ai, aj, ui, uj, none));
            // The Ash cut is barred from inside until the bar is off.
            Assert.IsEmpty(InteriorMaps.Path(vault, ai, aj, xi, xj, none));
            Assert.IsNotEmpty(InteriorMaps.Path(vault, ai, aj, xi, xj, f => f == "earth.ash_cut_open"));
            // The left fork is a dead end; the survey marks are on the way to the Root Chamber.
            var toRubble = InteriorMaps.Path(upper, ei, ej, ri, rj, none);
            Assert.IsNotEmpty(toRubble);
            var toPedestal = InteriorMaps.Path(upper, ei, ej, pi, pj, none);
            bool passesMarks = false;
            for (int k = 0; k < toPedestal.Length; k += 2) if (Lattice.Distance(toPedestal[k], toPedestal[k + 1], mi, mj) <= 1) passesMarks = true;
            Assert.IsTrue(passesMarks, "the way to the Root Chamber runs past the survey marks");

            // The validator's own way-out check passes on the Earth pack (no no_way_out at all).
            Assert.IsFalse(Check(d).Any(p => p.Code == ProblemCodes.NoWayOut));
        }

        // ---------------------------------------------------------------- routes

        [Test]
        public void Routes_UnreachableSite_IsAnErrorWhenComplete_AWarningWhenPartial()
        {
            var d = Skeleton();
            d.Lead("water_site").Requires = new List<string> { "water.never" };
            AssertError(d, ProblemCodes.UnreachableClue);
            d.Status = PackStatus.Partial;
            var ps = Check(d);
            Assert.AreEqual(0, ps.Count(p => p.IsError), Show(ps));
            Assert.IsTrue(ps.Any(p => p.Code == ProblemCodes.UnreachableClue && p.Id == "water"));
        }

        [Test]
        public void Routes_ZeroCompanions_NeedAFallbackInTheWorld()
        {
            // Only Petra can tell where the fire went: fine while she lives, not at zero companions.
            var d = Skeleton();
            d.Clue("fire_elder").Requires = new List<string> { "never.told" };
            d.Clue("fire_sign").Requires = new List<string> { "never.told" };
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => p.IsError && p.Code == ProblemCodes.CompanionOnlyClue && p.Id == "fire"), Show(ps));
            Assert.IsFalse(ps.Any(p => p.Code == ProblemCodes.UnreachableClue), "with companions alive the site is found");
        }

        [Test]
        public void Routes_GuardsOnCompanions_CountAtZeroCompanions()
        {
            // The trail to the cistern altar shows only while Nell lives.
            var d = Skeleton();
            d.Event("water_empty").Guards.Add("alive: nell");
            d.Event("water_empty_transit").Guards.Add("alive: nell");
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => p.IsError && p.Code == ProblemCodes.CompanionOnlyClue && p.Id == "water"), Show(ps));
        }

        [Test]
        public void Routes_KeeperMustExist()
        {
            var d = Skeleton();
            d.Encounter("keeper_air").Kind = EncounterKind.Friendly;
            AssertError(d, ProblemCodes.NoRecovery);
        }

        [Test]
        public void Routes_AlternateRecovery_CountsAsAWayBack()
        {
            var d = Skeleton();
            d.Encounters.RemoveAll(e => e.Id == "keeper_water");
            d.Location("cistern").Markers.Add(new MarkerDef { Id = "sluice", Kind = MarkerKind.Lever, Name = "the sluice" });
            d.Leads.Add(new LeadDef { Id = "water_sluice", Title = "The sluice", Goal = new GoalDef { At = MarkerRef.Surface("cistern", "sluice"), Action = QuestAction.Open }, Requires = { "water.stolen_seen" }, DoneWhen = "water.recovered", ReasonLine = "reason_altar", Priority = 70 });
            var sluice = new QuestEventDef { Id = "water_open_sluice", At = MarkerRef.Surface("cistern", "sluice"), Action = QuestAction.Open, Guards = { "custody: water = AtRivalAltar" }, Sets = { "water.recovered", "water.in_hand", "tower.water_known" } };
            sluice.Effects.Add(new EffectDef { Kind = EffectKind.TakeCrystal, Crystal = "water" });
            d.Events.Add(sluice);
            var ps = Check(d);
            Assert.AreEqual(0, ps.Count, Show(ps));
        }

        [Test]
        public void Routes_EveryRestorationOrder_ReachesTheFinale()
        {
            // The Whistling Tower opens only once three are lit: every order with air not last gets stuck.
            var d = Skeleton();
            d.Events.Add(new QuestEventDef { Id = "air_last", At = MarkerRef.Surface("shoulder", "cairn"), Action = QuestAction.Examine, Guards = { "restored: 3" }, Sets = { "air.tower_open" } });
            d.Lead("air_cairn").DoneWhen = "air.cairn_done";
            d.Lead("air_tower").Requires.Add("air.tower_open");
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => p.IsError && p.Code == ValidationCodes.FinaleUnreachable && p.Id == "air"), Show(ps));
            // No finale at all.
            d = Skeleton();
            d.Encounters.RemoveAll(e => e.BoundTo == EncounterBindings.LastTower);
            AssertError(d, ValidationCodes.FinaleUnreachable);
        }

        [Test]
        public void Endings_BothMustExist()
        {
            var d = Skeleton();
            d.Endings.RemoveAll(e => e.Outcome == CampaignPhase.Lost);
            AssertError(d, ValidationCodes.NoEnding);
            d = Skeleton();
            d.Endings.RemoveAll(e => e.Outcome == CampaignPhase.Won);
            AssertError(d, ValidationCodes.NoEnding);
        }

        // ---------------------------------------------------------------- narrative

        [Test]
        public void Narrative_DeadSpeaker_AndFixedPlaces()
        {
            var d = Skeleton();
            d.Line("companion_ivo_line").Guards.Clear();
            AssertError(d, ProblemCodes.DeadSpeaker);
            d = Skeleton();
            d.Line("mira_take").Text = "{crystal.earth.name} waits in the Black Forge.";
            AssertError(d, ProblemCodes.FixedLocationText);
            d.Line("mira_take").Guards.Add("custody: earth = AtRivalAltar");
            Assert.IsFalse(Check(d).Any(p => p.Code == ProblemCodes.FixedLocationText), "bound to state by a guard");
            d.Line("mira_take").Guards.Clear();
            d.Line("mira_take").Text = "{crystal.earth.name} is at {crystal.earth.place}.";
            Assert.AreEqual(0, Check(d).Count, "a token is never fixed text");
        }

        [Test]
        public void InteriorLocation_IsAWarning()
        {
            var d = Skeleton();
            d.Crystal("earth").Site = new MarkerRef("kiln", "skel_upper", "pedestal");
            var ps = Check(d);
            Assert.IsTrue(ps.Any(p => !p.IsError && p.Code == ValidationCodes.InteriorLocation), Show(ps));
            Assert.AreEqual(0, ps.Count(p => p.IsError), Show(ps));
        }

        [Test]
        public void Validation_IsDeterministic_AndFast()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string a = Show(Check(d)), b = Show(Check(d));
            Assert.AreEqual(a, b);
            string c = Show(Check(Skeleton()));
            Assert.Less(sw.ElapsedMilliseconds, 5000, "two authored and one skeleton validation");
            Assert.AreEqual("", c);
        }
    }
}
