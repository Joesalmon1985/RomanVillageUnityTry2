using System.Collections.Generic;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>
    /// A small hand-built campaign definition for tests (PRD_CRYSTALS I0, "a sample definition from I0's tests"). It uses
    /// every part of the format once or more: the five roles, the six companions of 4.2 with their services, the home
    /// camp, the four towers (rim corners by the bearing rule), one site per crystal (appendix C hexes), the Old Quarry as
    /// an interior with a pedestal, an Ash altar, a threshold, a safe point, a barred door and an exit, a stair
    /// connection, the authored rival plan of 5.3, an opening clue, a lead per crystal, a choice, the four keeper
    /// encounters, Orrin's friendly duel, the finale bound to the last tower, quest events, both endings and guarded lines.
    /// It is not a playable story; I2's skeleton and authored packs replace it for the proof.
    /// </summary>
    public static class CampaignSamples
    {
        public static readonly string[] CompanionIds = { "garrick", "ivo", "petra", "nell", "odo", "tansy" };

        private static MarkerDef M(string id, MarkerKind kind, int i = 0, int j = 0, string to = "", string openWhen = "", string crystal = "") =>
            new MarkerDef { Id = id, Kind = kind, I = i, J = j, To = to, OpenWhen = openWhen, Crystal = crystal, Name = id };

        private static LocationDef Site(string id, string name, LocationKind kind, int q, int r, params MarkerDef[] markers)
        {
            var l = new LocationDef { Id = id, Name = name, Kind = kind, Anchor = new AnchorDef { Kind = AnchorKind.MeadowHexCentre, Q = q, R = r } };
            l.Markers.AddRange(markers);
            l.Markers.Add(M("safe", MarkerKind.SafePoint, 1, 0));
            l.SafePoint = "safe";
            return l;
        }

        private static LocationDef Tower(Element e, string name)
        {
            string id = "tower_" + e.ToString().ToLowerInvariant();
            var l = new LocationDef { Id = id, Name = name, Kind = LocationKind.Tower, Anchor = new AnchorDef { Kind = AnchorKind.RimCorner, Corner = -1 } };
            l.Markers.Add(M("door", MarkerKind.Inscription));
            l.Markers.Add(M("caster", MarkerKind.DuelCaster, 0, -1));
            l.Markers.Add(M("opponent", MarkerKind.DuelOpponent, 1, 0));
            l.Markers.Add(M("safe", MarkerKind.SafePoint, -1, 0));
            l.SafePoint = "safe";
            return l;
        }

        private static EncounterDef Keeper(string crystal, string name, MarkerRef altar, MarkerRef threshold, MarkerRef safe)
        {
            var e = new EncounterDef
            {
                Id = "keeper_" + crystal, Name = name, Kind = EncounterKind.Lethal, Profile = "keeper", OpponentActorId = "keeper_" + crystal,
                OpponentName = name, Threshold = threshold, SafePoint = safe,
                Stage = new DuelStageDef { Caster = threshold, Opponent = altar, Volunteer = safe },
            };
            e.OnVictory.Add(new EffectDef { Kind = EffectKind.TakeCrystal, Crystal = crystal });
            e.OnVictory.Add(new EffectDef { Kind = EffectKind.SetFact, Ref = "tower." + crystal + "_known" });
            return e;
        }

        /// <summary>A fresh copy each call (tests may change it).</summary>
        public static CampaignDefinition Skeleton()
        {
            var d = new CampaignDefinition { Id = "sample_v1", Title = "The sample telling", Status = PackStatus.Partial, Template = "", Seed = 0, GeneratorVersion = "" };

            d.Roles.Add(new RoleDef { Id = "mira", ActorId = "mira", Name = "Mira" });
            d.Roles.Add(new RoleDef { Id = "prince", ActorId = "prince", Name = "the prince" });
            d.Roles.Add(new RoleDef { Id = "rival_leader", ActorId = "rival_leader", Name = "Hesk Varr" });
            d.Roles.Add(new RoleDef { Id = "final_enemy", ActorId = "rival_leader", Name = "the Unweaver, in Hesk Varr" });
            d.Roles.Add(new RoleDef { Id = "wizard", ActorId = "wizard", Name = "Orrin" });

            string[] names = { "Garrick Holt", "Ivo Quill", "Petra Coalhand", "Nell Farrow", "Odo Birch", "Tansy Reed" };
            string[] roles = { "Shield-bearer", "Lamp-bearer", "Smith's daughter", "Ferry hand", "Forester", "Scout" };
            string[] services = { CompanionServices.Shield, CompanionServices.Lamp, CompanionServices.Fire, CompanionServices.Water, CompanionServices.Earth, CompanionServices.Scout };
            for (int k = 0; k < 6; k++)
                d.Companions.Add(new CompanionDef
                {
                    Id = CompanionIds[k], Name = names[k], Role = roles[k], Line = "line_" + CompanionIds[k], VolunteerOrder = k + 1,
                    Service = services[k], ActorId = "companion_" + CompanionIds[k],
                });

            var home = new LocationDef { Id = "home", Name = "the home camp", Kind = LocationKind.Camp, Anchor = new AnchorDef { Kind = AnchorKind.HomeCamp } };
            home.Markers.Add(M("fire", MarkerKind.SafePoint));
            home.Markers.Add(M("sawyer", MarkerKind.Witness, 1, 0));
            home.SafePoint = "fire";
            d.Locations.Add(home);
            d.Locations.Add(Site("glade", "the Stonemark Glade", LocationKind.Site, 0, -1,
                M("stone", MarkerKind.Inscription), M("stair", MarkerKind.Stairs, 0, 1, "glade/quarry/exit", "earth.stair_open")));
            d.Locations.Add(Site("ash_cut", "the Ash cut", LocationKind.Site, -2, -2, M("cut", MarkerKind.Stairs, 0, 0, "glade/quarry/door")));
            d.Locations.Add(Site("kiln", "the Cold Kiln", LocationKind.Site, 4, -1, M("pedestal", MarkerKind.Pedestal, 0, 0, "", "", "fire")));
            d.Locations.Add(Site("forge", "the Black Forge", LocationKind.Site, -4, 2, M("altar", MarkerKind.Altar, 0, 0, "", "", "fire"), M("threshold", MarkerKind.Threshold, 0, -1)));
            d.Locations.Add(Site("causeway", "the Low Causeway", LocationKind.Shore, 1, -4, M("pedestal", MarkerKind.Pedestal, 0, 0, "", "", "water")));
            d.Locations.Add(Site("cistern", "the Old Cistern", LocationKind.Site, 2, -3, M("altar", MarkerKind.Altar, 0, 0, "", "", "water"), M("threshold", MarkerKind.Threshold, 0, -1)));
            d.Locations.Add(Site("watch", "Gannet Watch", LocationKind.Watchtower, 2, 2, M("door", MarkerKind.Inscription), M("orrin", MarkerKind.Witness, 0, 1)));
            d.Locations.Add(Site("shoulder", "the Bare Shoulder", LocationKind.Site, 3, 1, M("pedestal", MarkerKind.Pedestal, 0, 0, "", "", "air")));
            d.Locations.Add(Site("cliff", "the Cliff Stair", LocationKind.Site, 1, 3, M("altar", MarkerKind.Altar, 0, 0, "", "", "air"), M("threshold", MarkerKind.Threshold, 0, -1)));
            d.Locations.Add(Tower(Element.Earth, "the Rooted Tower"));
            d.Locations.Add(Tower(Element.Fire, "the Beacon Tower"));
            d.Locations.Add(Tower(Element.Water, "the Tide Tower"));
            d.Locations.Add(Tower(Element.Air, "the Whistling Tower"));

            var quarry = new InteriorDef { Id = "quarry", Name = "the Old Quarry" };
            quarry.Rows.AddRange(new[]
            {
                "#########",
                "#..P#.A.#",
                "#.##..T.#",
                "#S...#X##",
                "###E#####",
            });
            quarry.Legend['E'] = M("exit", MarkerKind.Exit, 0, 0, "glade/stair");
            quarry.Legend['P'] = M("pedestal", MarkerKind.Pedestal, 0, 0, "", "", "earth");
            quarry.Legend['A'] = M("altar", MarkerKind.Altar, 0, 0, "", "", "earth");
            quarry.Legend['T'] = M("threshold", MarkerKind.Threshold);
            quarry.Legend['S'] = M("safe", MarkerKind.SafePoint);
            quarry.Legend['X'] = M("door", MarkerKind.Door, 0, 0, "ash_cut/cut", "earth.ash_cut_open");
            d.Interiors.Add(quarry);

            d.Connections.Add(new ConnectionDef { Id = "glade_stair", A = MarkerRef.Surface("glade", "stair"), B = new MarkerRef("glade", "quarry", "exit"), OpenWhen = "earth.stair_open" });
            d.Connections.Add(new ConnectionDef { Id = "ash_cut", A = MarkerRef.Surface("ash_cut", "cut"), B = new MarkerRef("glade", "quarry", "door"), OpenWhen = "earth.ash_cut_open" });

            d.Crystals.Add(new CrystalDef { Id = "earth", Element = Element.Earth, Name = "the Deepstone", Site = new MarkerRef("glade", "quarry", "pedestal"), Tower = "tower_earth", Underground = true });
            d.Crystals.Add(new CrystalDef { Id = "fire", Element = Element.Fire, Name = "the Kilnheart", Site = MarkerRef.Surface("kiln", "pedestal"), Tower = "tower_fire" });
            d.Crystals.Add(new CrystalDef { Id = "water", Element = Element.Water, Name = "the Tideglass", Site = MarkerRef.Surface("causeway", "pedestal"), Tower = "tower_water" });
            d.Crystals.Add(new CrystalDef { Id = "air", Element = Element.Air, Name = "the Skyshard", Site = MarkerRef.Surface("shoulder", "pedestal"), Tower = "tower_air" });
            d.Towers.Add(new TowerDef { Id = "tower_earth", Location = "tower_earth", Element = Element.Earth, Sign = "sign_earth", BeamColour = "#B8A040" });
            d.Towers.Add(new TowerDef { Id = "tower_fire", Location = "tower_fire", Element = Element.Fire, Sign = "sign_fire", BeamColour = "#E06020" });
            d.Towers.Add(new TowerDef { Id = "tower_water", Location = "tower_water", Element = Element.Water, Sign = "sign_water", BeamColour = "#3080D0" });
            d.Towers.Add(new TowerDef { Id = "tower_air", Location = "tower_air", Element = Element.Air, Sign = "sign_air", BeamColour = "#D0E0F0" });

            var earthAltar = new MarkerRef("ash_cut", "quarry", "altar");
            d.Rival.Steps.Add(new RivalStepDef { Crystal = "earth", MarchCost = 4800, CarryCost = 900, Altar = earthAltar });
            d.Rival.Steps.Add(new RivalStepDef { Crystal = "fire", MarchCost = 3600, CarryCost = 900, Altar = MarkerRef.Surface("forge", "altar") });
            d.Rival.Steps.Add(new RivalStepDef { Crystal = "water", MarchCost = 3600, CarryCost = 900, Altar = MarkerRef.Surface("cistern", "altar") });
            d.Rival.Steps.Add(new RivalStepDef { Crystal = "air", MarchCost = 3600, CarryCost = 900, Altar = MarkerRef.Surface("cliff", "altar") });

            d.Clues.Add(new ClueDef { Id = "earth_opening", Source = new ClueSourceDef { Kind = SourceKind.Opening, Ref = "mira" }, Gives = { "earth.marks_heard" }, Essential = true });
            d.Clues.Add(new ClueDef { Id = "earth_odo", Source = new ClueSourceDef { Kind = SourceKind.Companion, Ref = "odo" }, Requires = { "earth.marks_heard" }, Gives = { "earth.glade_known" }, Fallback = "earth_sawyer" });
            d.Clues.Add(new ClueDef { Id = "earth_sawyer", Source = new ClueSourceDef { Kind = SourceKind.Witness, Ref = "home/sawyer" }, Requires = { "earth.marks_heard" }, Gives = { "earth.glade_known" }, Essential = true });

            d.Leads.Add(new LeadDef { Id = "earth_stone", Title = "The boundary stone", Goal = new GoalDef { At = MarkerRef.Surface("glade", "stone"), Action = QuestAction.Examine }, Requires = { "earth.glade_known" }, DoneWhen = "earth.stair_open", ReasonLine = "mira_reason_stone", Priority = 50, Crystal = "earth" });
            d.Leads.Add(new LeadDef { Id = "fire_kiln", Title = "The Cold Kiln", Goal = new GoalDef { At = MarkerRef.Surface("kiln", "pedestal"), Action = QuestAction.Light }, DoneWhen = "fire.kiln_lit", ReasonLine = "mira_reason_kiln", Priority = 40, Crystal = "fire" });
            d.Leads.Add(new LeadDef { Id = "water_causeway", Title = "The Low Causeway", Goal = new GoalDef { At = MarkerRef.Surface("causeway", "pedestal"), Action = QuestAction.Take }, DoneWhen = "water.taken", ReasonLine = "mira_reason_causeway", Priority = 40, Crystal = "water" });
            d.Leads.Add(new LeadDef { Id = "air_watch", Title = "Gannet Watch", Goal = new GoalDef { At = MarkerRef.Surface("watch", "orrin"), Action = QuestAction.Talk }, DoneWhen = "air.markers_known", ReasonLine = "mira_reason_watch", Priority = 40, Crystal = "air" });

            var test = new ChoiceDef { Id = "air_test", QuestionLine = "orrin_question", RevealedBy = MarkerRef.Surface("watch", "door"), WrongLine = "orrin_wrong", Sets = { "air.markers_known" } };
            test.Answers.Add(new AnswerDef { Line = "answer_easterly" });
            test.Answers.Add(new AnswerDef { Line = "answer_westerly", Correct = true });
            test.Answers.Add(new AnswerDef { Line = "answer_northerly" });
            d.Choices.Add(test);

            d.Encounters.Add(Keeper("earth", "Dolm the Tally-Keeper", earthAltar, new MarkerRef("glade", "quarry", "threshold"), new MarkerRef("glade", "quarry", "safe")));
            d.Encounters.Add(Keeper("fire", "Brask the Forge-Warden", MarkerRef.Surface("forge", "altar"), MarkerRef.Surface("forge", "threshold"), MarkerRef.Surface("forge", "safe")));
            d.Encounters.Add(Keeper("water", "Nual of the Cistern", MarkerRef.Surface("cistern", "altar"), MarkerRef.Surface("cistern", "threshold"), MarkerRef.Surface("cistern", "safe")));
            d.Encounters.Add(Keeper("air", "Pike the Signal-Warden", MarkerRef.Surface("cliff", "altar"), MarkerRef.Surface("cliff", "threshold"), MarkerRef.Surface("cliff", "safe")));
            d.Encounter("keeper_earth").OnVictory.Add(new EffectDef { Kind = EffectKind.SetFact, Ref = "earth.ash_cut_open" });
            d.Encounters.Add(new EncounterDef { Id = "orrin_demo", Name = "Orrin's demonstration", Kind = EncounterKind.Friendly, Profile = "friendly_orrin", OpponentActorId = "wizard", OpponentName = "Orrin", Threshold = MarkerRef.Surface("watch", "orrin"), SafePoint = MarkerRef.Surface("watch", "safe") });
            d.Encounters.Add(new EncounterDef { Id = "finale", Name = "The last tower", Kind = EncounterKind.Lethal, Profile = "finale_unweaver", OpponentActorId = "rival_leader", OpponentName = "the Unweaver, in Hesk Varr", BoundTo = EncounterBindings.LastTower });

            d.Events.Add(new QuestEventDef { Id = "earth_read_stone", At = MarkerRef.Surface("glade", "stone"), Action = QuestAction.Examine, Requires = { "earth.glade_known" }, Sets = { "earth.stair_open" }, Lines = { "mira_stone" } });
            var take = new QuestEventDef { Id = "earth_take", At = new MarkerRef("glade", "quarry", "pedestal"), Action = QuestAction.Take, Guards = { "custody: earth = AtSite" }, Sets = { "tower.earth_known" } };
            take.Effects.Add(new EffectDef { Kind = EffectKind.TakeCrystal, Crystal = "earth" });
            d.Events.Add(take);
            var empty = new QuestEventDef { Id = "earth_empty", At = new MarkerRef("glade", "quarry", "pedestal"), Action = QuestAction.Examine, Guards = { "nofact: earth.stolen_seen", "custody: earth = AtRivalAltar" }, Sets = { "earth.stolen_seen", "earth.tally_read" } };
            empty.Effects.Add(new EffectDef { Kind = EffectKind.SetBelief, Crystal = "earth" });
            d.Events.Add(empty);
            var ask = new QuestEventDef { Id = "air_ask_orrin", At = MarkerRef.Surface("watch", "orrin"), Action = QuestAction.Talk, Lines = { "orrin_skyshard" } };
            ask.Effects.Add(new EffectDef { Kind = EffectKind.OfferChoice, Ref = "air_test" });
            d.Events.Add(ask);

            var restored = new EndingDef { Id = "restored", Outcome = CampaignPhase.Won, Lines = { "end_restored" } };
            restored.Variants.Add(new EndingVariantDef { Survivors = SurvivorBand.All, Lines = { "end_all_lived" } });
            restored.Variants.Add(new EndingVariantDef { Survivors = SurvivorBand.None, Lines = { "end_none_lived" } });
            d.Endings.Add(restored);
            d.Endings.Add(new EndingDef { Id = "unwoven", Outcome = CampaignPhase.Lost, Lines = { "end_unwoven" } });

            void Line(string id, string speaker, string text, string topic = "", params string[] guards)
            {
                var l = new LineDef { Id = id, Speaker = speaker, Text = text, Topic = topic };
                l.Guards.AddRange(guards);
                d.Lines.Add(l);
            }
            Line("mira_open_1", "mira", "{crystal.earth.name} lies under old workings, at {crystal.earth.place}.", "opening");
            Line("mira_reason_stone", "mira", "The foresters' marks point to the stone in the glade.");
            Line("mira_reason_kiln", "mira", "Hod says the kiln will show what it holds if it is lit right.");
            Line("mira_reason_causeway", "mira", "Old Marl knows when the causeway is dry.");
            Line("mira_reason_watch", "mira", "Orrin watches the whole ridge.");
            Line("mira_stone", "mira", "Three cuts and a ring. There.");
            Line("mira_stakes", "mira", "If this goes badly, {volunteer.name} holds the way out.", "threshold");
            Line("mira_count", "mira", "{count.restored} set, {count.alive} still with us.", "status");
            Line("odo_marks", "odo", "Every cut on a tree is somebody saying something.", "earth", "alive: odo");
            Line("mira_for_garrick", "mira", "Garrick held the stair.", "memorial", "dead: garrick");
            Line("mira_one_lit", "mira", "One tower lit.", "status", "restored: 1");
            Line("orrin_skyshard", "wizard", "Your Skyshard is at {crystal.air.trueplace}.", "air");
            Line("orrin_question", "wizard", "Three winds cross this ridge. Name the one the gannets ride home.");
            Line("orrin_wrong", "wizard", "Look about you, and come back.");
            Line("answer_easterly", "mira", "The Easterly.");
            Line("answer_westerly", "mira", "The Westerly.");
            Line("answer_northerly", "mira", "The Northerly.");
            foreach (var e in new[] { "earth", "fire", "water", "air" }) Line("sign_" + e, "", "This tower keeps {crystal." + e + ".name}.");
            foreach (var k in CompanionIds) Line("line_" + k, k, "Ready.", "", "alive: " + k);
            Line("end_restored", "mira", "Four beams meet over the island.");
            Line("end_all_lived", "mira", "No cairn was needed.");
            Line("end_none_lived", "mira", "Six fires, then.");
            Line("end_unwoven", "prince", "The towers are going out.");
            return d;
        }

        /// <summary>A new game with the campaign started from <see cref="Skeleton"/> (one tick stepped so the start applied).</summary>
        public static WorldSim Started(ulong seed = 7, CampaignDefinition d = null)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var r = sim.Submit(new StartCampaign { Definition = d ?? Skeleton() });
            NUnit.Framework.Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            return sim;
        }
    }
}
