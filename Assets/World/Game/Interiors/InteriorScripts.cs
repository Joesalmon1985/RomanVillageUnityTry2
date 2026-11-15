using System;
using System.Collections;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// AutoPlay <c>quarry_walk</c> (package I5) and the helpers it shares with the interior tests: on the developer start
    /// <c>dev.historic</c> with the authored pack, the campaign is driven by commands only (accepting Mira's leads, and
    /// later a developer custody change and a resolved keeper duel, since the expedition director and the campaign duel
    /// are other packages). The player walks to the Stonemark Glade, takes the stair down, walks the Old Quarry to the
    /// survey marks and the Root Chamber, opens the interior map, is led out to the surface; then, with the Deepstone
    /// marked stolen, goes down again, sees the bare pedestal and the tally stick, follows the stair down to the Tally
    /// Vault's threshold and altar, and leaves by the Ash cut once it opens. Captures go to Evidence/screenshots (i5_*).
    /// </summary>
    public static class InteriorScripts
    {
        public const string QuarryWalk = "quarry_walk";
        public const string AuthoredPack = "authored_v1", SkeletonPack = "skeleton_v1";

        public static void Register() => AutoPlayScripts.Register(QuarryWalk, QuarryWalkScript);

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static PlayerWalker W => WorldBootstrap.Instance.Avatar;
        private static WorldState S => WorldBootstrap.Instance.Sim.State;

        /// <summary>Reads a shipped pack from StreamingAssets.</summary>
        public static CampaignDefinition LoadPack(string id) =>
            CampaignLoader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "world", "campaigns", id));

        /// <summary>Submits StartCampaign with a shipped pack and steps the clock once so it applies. Returns the refusal, or null.</summary>
        public static string StartCampaign(string packId)
        {
            var r = B.Sim.Submit(new StartCampaign { Definition = LoadPack(packId) });
            if (!r.Ok) return r.ToString();
            B.Clock.StepOnce();
            return S.Campaign != null ? null : "the campaign did not start";
        }

        /// <summary>
        /// Runs the sim fast (ticks per frame) while accepting Mira's top lead whenever she stands idle, until a
        /// condition holds. Nothing but commands reach the state.
        /// </summary>
        public static IEnumerator Drive(Func<bool> until, float timeoutSeconds, int ticksPerFrame, Action<string> fail)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!until())
            {
                if (Time.realtimeSinceStartup > end) { fail("the campaign did not get there in time (party " + CampaignApi.Party(S)?.Mode + " at " + CampaignApi.Party(S)?.At + ")"); yield break; }
                for (int k = 0; k < ticksPerFrame && !until(); k++)
                {
                    var p = CampaignApi.Party(S);
                    var top = CampaignApi.TopLead(S);
                    if (p != null && p.Mode == PartyMode.Idle && top != null && B.Sim.PendingCommands.Count == 0)
                        B.Sim.Submit(new AcceptLead { LeadId = top.Id });
                    B.Clock.StepOnce();
                }
                yield return null;
            }
        }

        public static bool Knows(string fact) => S.Campaign != null && S.Campaign.Knows(fact);

        /// <summary>The world cell of a marker given as text ("glade/stair", "glade/quarry_upper/marks").</summary>
        public static Cell CellOf(string marker)
        {
            if (!MarkerRef.TryParse(marker, out var r) || !Interiors.TryCell(r, out var c)) throw new InvalidOperationException("no cell for " + marker);
            return c;
        }

        /// <summary>A free floor cell next to a marker (the side nearest the player), to stand and look at it.</summary>
        public static Cell Beside(string marker, string notOn = null)
        {
            var c = CellOf(marker);
            Cell? skip = notOn != null ? CellOf(notOn) : (Cell?)null;
            Cell best = c; float bd = float.MaxValue;
            for (int h = 0; h < 6; h++)
            {
                var n = c + WalkLattice.Dir(h);
                if (WalkGrid.IsBlocked(n) || WalkGrid.IsAvoided(n) || n == skip) continue;
                float d = WalkGrid.Distance(n, W.Cell) + h * 0.01f;
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        /// <summary>The cell in front of a way's end on its open side (where one stands to go through it).</summary>
        public static Cell Approach(string marker)
        {
            MarkerRef.TryParse(marker, out var r);
            var l = Interiors.LinkAt(r);
            if (l == null) return Beside(marker);
            // In front of the way; if a site prop of the expedition stands there, the nearest free side (I8).
            for (int k = 0; k < 6; k++)
            {
                int h = l.OutHeading + ((k + 1) / 2) * (k % 2 == 0 ? 1 : -1);
                var c = l.Cell + WalkLattice.Dir(((h % 6) + 6) % 6);
                if (!WalkGrid.IsBlocked(c)) return c;
            }
            return l.Cell + WalkLattice.Dir(l.OutHeading);
        }

        private static IEnumerator WaitCrossed(AutoPlay a, string expectInterior, float timeout = 20f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while ((Interiors.IsCrossing || W.IsAutoWalking || W.IsGliding) && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            if (Interiors.Current != expectInterior) a.Fail("expected to be in " + (expectInterior ?? "the open") + ", in " + (Interiors.Current ?? "the open") + " at " + W.Cell);
        }

        private static IEnumerator Face(AutoPlay a, string marker)
        {
            var c = CellOf(marker);
            yield return a.DoFace(() => WalkLattice.ToWorld(c));
        }

        public static AutoPlayScript QuarryWalkScript()
        {
            var s = new AutoPlayScript(QuarryWalk);
            float t0 = 0f;
            s.Wait(1f)
             .Step(a => EnsureDevHistoric(a))
             .Check(() => DevStart.Current == ScenarioStarts.DevHistoric, "not on dev.historic")
             .Call(() => { var why = StartCampaign(AuthoredPack); if (why != null) throw new InvalidOperationException("StartCampaign: " + why); })
             .Check(() => Interiors.All.Count == 2, "the authored pack's two interiors were not built")
             .Call(() => { B.Clock.Pause("quarry_walk"); DayNight.Instance?.SetHour(23.0); })
             // Mira finds the stair (her leads, accepted by command).
             .Step(a => Drive(() => Knows("earth.stair_open"), 120f, 40, a.Fail))
             .Call(() => { Debug.Log("[AutoPlay] quarry_walk: stair open at tick " + S.Tick); t0 = Time.time; })
             // The player walks to the glade and down the stair.
             .WalkTo(() => Approach("glade/stair"), 120f)
             .Step(a => Face(a, "glade/stair"))
             .Wait(1.2f).Capture("i5_glade_stair_mouth")
             .Call(() => Debug.Log("[AutoPlay] quarry_walk: walked to the glade in " + (Time.time - t0).ToString("0.0") + " s"))
             .Step(LatticeMove.Forward)
             .Wait(0.25f).Capture("i5_descent")
             .Step(a => WaitCrossed(a, "quarry_upper"))
             .Check(() => W.Cell == CellOf("glade/quarry_upper/exit"), "not on the quarry's exit after the descent")
             .Wait(0.4f).Capture("i5_quarry_arrival")
             .Check(() => InteriorLighting.Instance.Underground && InteriorLighting.Instance.SkyDark, "the sky lights the quarry")
             // The survey gallery and its marks.
             .WalkTo(() => Beside("glade/quarry_upper/marks"), 60f)
             .Step(a => Face(a, "glade/quarry_upper/marks"))
             .Wait(0.4f).Capture("i5_survey_marks")
             .Call(() => PointerInput.TapObject(Thing("glade/quarry_upper/marks")))
             .Wait(0.5f).Capture("i5_survey_marks_read")
             .Call(() => { foreach (var h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h); })
             // The way out, seen from inside: the lit stair up.
             .Step(a => Face(a, "glade/quarry_upper/exit"))
             .Wait(0.4f).Capture("i5_way_out_from_inside")
             // The Root Chamber with the Deepstone on its pedestal.
             .WalkTo(() => Beside("glade/quarry_upper/pedestal"), 60f)
             .Step(a => Face(a, "glade/quarry_upper/pedestal"))
             .Check(() => S.Campaign.Crystal(Element.Earth).Custody == Custody.AtSite, "the Deepstone is not on its pedestal")
             .Wait(0.5f).Capture("i5_root_chamber_deepstone")
             .OpenMap().Wait(0.5f).Check(() => MapScreen.Mode == MapMode.Interior, "the map did not open on the quarry").Capture("i5_interior_map").CloseMap()
             // Led out to the surface.
             .Call(() => { if (!Interiors.LeadOut()) throw new InvalidOperationException("Lead me out found no way"); })
             .Step(a => WaitLeadOut(a, 60f))
             .Check(() => Interiors.Current == null && W.Cell == CellOf("glade/stair"), "Lead me out did not reach the glade")
             .Turn(3).Wait(0.6f).Capture("i5_glade_after_lead_out").Turn(3)
             // The theft: the Deepstone goes to the Ash altar (developer command), Mira follows the trail.
             .Call(() => { var r = B.Sim.Submit(new DevSetCustody { Crystal = Element.Earth, To = Custody.AtRivalAltar }); if (!r.Ok) throw new InvalidOperationException(r.ToString()); B.Clock.StepOnce(); })
             .Step(a => Drive(() => Knows("earth.vault_known"), 180f, 40, a.Fail))
             .Call(() => { Debug.Log("[AutoPlay] quarry_walk: the vault is known at tick " + S.Tick); DayNight.Instance?.SetHour(23.0); })
             // Off the stair and back onto it (from whichever side is free of the expedition's props).
             .WalkTo(() => Approach("glade/stair"), 30f)
             .Step(a => Face(a, "glade/stair"))
             .Step(LatticeMove.Forward)
             .Step(a => WaitCrossed(a, "quarry_upper"))
             .WalkTo(() => Beside("glade/quarry_upper/pedestal", "glade/quarry_upper/tally"), 60f)
             .Step(a => Face(a, "glade/quarry_upper/pedestal"))
             .Wait(0.5f).Capture("i5_root_chamber_bare")
             .Check(() => !StandHasCrystal("glade/quarry_upper/pedestal"), "the pedestal still shows the crystal")
             .Check(() => TallyShown("glade/quarry_upper/tally"), "no tally stick by the bare pedestal")
             // Down to the Tally Vault.
             .WalkTo(() => CellOf("glade/quarry_upper/stair_down"), 60f)
             .Step(a => WaitCrossed(a, "tally_vault"))
             .Wait(0.4f).Capture("i5_vault_arrival")
             .Step(LatticeMove.Forward, 3).Wait(0.3f).Capture("i5_vault_turnings")
             .WalkTo(() => CellOf("ash_cut/tally_vault/threshold"), 90f)
             .Wait(0.4f).Capture("i5_vault_threshold")
             .Step(a => Face(a, "ash_cut/tally_vault/altar"))
             .Wait(0.4f).Capture("i5_vault_altar")
             .Check(() => StandHasCrystal("ash_cut/tally_vault/altar"), "the Deepstone is not on the Ash altar")
             .Check(() => WalkGrid.IsBlocked(CellOf("ash_cut/tally_vault/bar")), "the barred door is open before Dolm is beaten")
             .OpenMap().Wait(0.5f).Capture("i5_vault_map").CloseMap()
             // The keeper's duel is the campaign duel's (another package): resolved here by commands as a victory.
             // (I8: the expedition's scout must be back before a fight; the duel itself is not staged here.)
             .Step(a => Drive(() => CampaignApi.Party(S).Mode == PartyMode.AtThreshold && (S.Campaign.Scout == null || !S.Campaign.Scout.Out), 240f, 40, a.Fail))
             .Call(() =>
             {
                 CampaignDuel.AutoStage = false;
                 if (!S.Campaign.Party.PlayerPresent) { B.Sim.Submit(new SetCompany { Present = true }); B.Clock.StepOnce(); } // the expedition counts a player who walked off as away
                 var r = B.Sim.Submit(new CommitEncounter { EncounterId = "keeper_earth" });
                 if (!r.Ok) throw new InvalidOperationException("commit: " + r);
                 B.Clock.StepOnce();
                 var at = CampaignApi.Attempt(S);
                 r = B.Sim.Submit(new ResolveEncounter { AttemptId = at.Id, Result = DuelResult.Victory });
                 if (!r.Ok) throw new InvalidOperationException("resolve: " + r);
                 B.Clock.StepOnce();
                 CampaignDuel.AutoStage = true;
             })
             .Check(() => Knows("earth.ash_cut_open"), "the Ash cut did not open")
             .Wait(2.0f)
             .Check(() => !WalkGrid.IsBlocked(CellOf("ash_cut/tally_vault/bar")), "the barred door did not open")
             .WalkTo(() => Beside("ash_cut/tally_vault/bar"), 30f)
             .Step(a => Face(a, "ash_cut/tally_vault/bar"))
             .Wait(0.3f).Capture("i5_vault_door_open")
             .WalkTo(() => CellOf("ash_cut/tally_vault/cut_exit"), 30f)
             .Call(() => DayNight.Instance?.SetHour(23.5))
             .Step(a => WaitCrossed(a, null))
             .Check(() => W.Cell == CellOf("ash_cut/cut"), "not at the Ash cut on the surface")
             .Turn(3).Wait(0.6f).Capture("i5_ash_cut_surface")
             .MeasureFps("quarry_walk surface", 2f)
             .Call(() => B.Clock.Resume("quarry_walk"))
             .Log("[AutoPlay] quarry_walk OK")
             .Quit(0);
            return s;
        }

        private static IEnumerator EnsureDevHistoric(AutoPlay a)
        {
            if (DevStart.Current == ScenarioStarts.DevHistoric) yield break;
            // Rebuild the world from the developer start and go on in the new scene.
            AutoPlay.ContinueAfterLoad = QuarryWalkScript();
            if (!DevStart.Begin(ScenarioStarts.DevHistoric)) { AutoPlay.ContinueAfterLoad = null; a.Fail("dev.historic: " + DevStart.LastError); yield break; }
            while (true) yield return null; // the scene reloads under this script
        }

        private static IEnumerator WaitLeadOut(AutoPlay a, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while ((Interiors.IsLeadingOut || Interiors.IsCrossing || W.IsAutoWalking) && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            if (Interiors.Current != null) a.Fail("still underground after Lead me out, at " + W.Cell);
        }

        public static Component Thing(string marker)
        {
            MarkerRef.TryParse(marker, out var r);
            foreach (var t in UnityEngine.Object.FindObjectsByType<InteriorThing>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.Ref == r) return t;
            return null;
        }

        public static bool StandHasCrystal(string marker)
        {
            MarkerRef.TryParse(marker, out var r);
            foreach (var v in UnityEngine.Object.FindObjectsByType<CrystalStandView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (v.Ref == r) return v.HasCrystal;
            return false;
        }

        public static bool TallyShown(string marker)
        {
            MarkerRef.TryParse(marker, out var r);
            foreach (var t in UnityEngine.Object.FindObjectsByType<InteriorThing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.Ref == r) { var v = t.GetComponent<TallyView>(); return v != null && v.Shown; }
            return false;
        }
    }
}
