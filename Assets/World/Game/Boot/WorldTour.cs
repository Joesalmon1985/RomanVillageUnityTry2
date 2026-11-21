using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Table;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// WU-8: the world-first tour. From the hub of a god board game (dev.godboard) it enters and leaves all six destinations
    /// in a row (temple to god board, council, rhythm, healer's lodge inspection, the crystal expedition by Mira, the duel
    /// ring) and checks each time that the player stands on the same hub cell with the same heading, in the walking view,
    /// free to walk, and that the sim state is kept. Used by the PlayMode test <c>WorldTourTests</c> and by the packaged
    /// AutoPlay script <c>world_ui_tour</c>. It reads state and calls the same public methods the people-facing views call;
    /// the only sim writes are the commands those views send (sacrifice, StartCampaign by Mira's offer) and the council
    /// staging the council tests already use.
    /// </summary>
    public sealed class WorldTour
    {
        public const string Pause = "WorldTour";
        public const string ScriptName = "world_ui_tour";

        private readonly Action<string> _fail;
        public string Failure { get; private set; }
        public readonly List<string> Done = new List<string>();
        /// <summary>Optional hook called at named moments (captures).</summary>
        public Action<string> Moment;

        public WorldTour(Action<string> fail) { _fail = fail; }

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static WorldState S => B.Sim.State;
        private static PlayerWalker W => PlayerWalker.Instance;

        private bool C(bool ok, string msg)
        {
            if (ok || Failure != null) return ok && Failure == null;
            Failure = msg; _fail?.Invoke(msg);
            return false;
        }

        private IEnumerator Until(Func<bool> cond, float seconds, string what, bool ticking = false)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond())
            {
                if (ticking) B.Clock.StepOnce();
                if (Time.realtimeSinceStartup > end) { C(false, "timed out waiting for " + what); yield break; }
                yield return null;
            }
        }

        private static void Step(int n = 1) { for (int i = 0; i < n; i++) B.Clock.StepOnce(); }

        private struct Pose { public Cell Cell; public int Heading; }
        private static Pose Here() => new Pose { Cell = W.Cell, Heading = W.Heading };

        private bool Back(Pose p, string where)
        {
            bool ok = C(W.Cell == p.Cell, where + ": not on the same cell (" + W.Cell + " vs " + p.Cell + ")")
                && C(W.Heading == p.Heading, where + ": not the same heading (" + W.Heading + " vs " + p.Heading + ")")
                && C(!W.IsOffLattice, where + ": off the lattice")
                && C(Mathf.Abs(Mathf.DeltaAngle(W.Yaw, WalkLattice.Yaw(p.Heading))) < 0.05f, where + ": the yaw differs")
                && C(!W.IsLocked, where + ": the walker is still locked")
                && C(ViewSwitcher.Instance.CurrentView == ViewMode.FirstPerson, where + ": not in the walking view")
                && C(PointerInput.InputEnabled, where + ": the pointer is not back");
            if (ok) { Done.Add(where); Debug.Log("[WorldTour] back from " + where + " at " + p.Cell + " heading " + p.Heading); Moment?.Invoke("back_" + where); }
            return ok;
        }

        /// <summary>Runs the whole tour; call after the god board start has loaded. The failure (if any) is in <see cref="Failure"/>.</summary>
        public IEnumerator Run()
        {
            bool councilWas = CouncilDirector.Disabled;
            CouncilDirector.Disabled = true;
            PointerInput.InputEnabled = true;
            if (OpeningVista.Instance != null) OpeningVista.Instance.Leave(); // the tour walks from the hub cell in the walking view
            WorldActions.Cancel();
            B.Clock.Pause(Pause);
            if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
            W.StopAuto();
            if (!C(S.Ruleset == Ruleset.GodBoard && GodBoardDirector.Instance != null, "not a god board game")) yield break;
            var layout = new CampLayout(S.Board);
            if (!C(layout.Hub, "no hub")) yield break;
            W.Teleport(layout.Spawn, layout.SpawnHeading);
            yield return null; yield return null;
            var home = Here();
            Moment?.Invoke("hub");

            // 1. Temple: sacrifice, the camera rises to the god board, play a little, leave.
            var d = GodBoardDirector.Instance;
            if (!C(d.TrySacrifice(true), "sacrifice refused: " + d.LastResult.Message)) yield break;
            Step(2);
            yield return Until(() => d.View == BoardView.Board, 40f, "the board view");
            if (Failure != null) yield break;
            Moment?.Invoke("board");
            d.Leave(); Step(2);
            yield return Until(() => d.View == BoardView.Altar, 40f, "the altar view");
            if (Failure != null) yield break;
            yield return null;
            if (!Back(home, "god board")) yield break;
            if (!C(S.Table.Paused, "state: the table did not wait") || !C(S.Table.Phase == TablePhase.Setup, "state: the table left setup")) yield break;
            var phase = S.Table.Phase; long frozen = S.Table.ActiveTicks;

            // 2. Council.
            var cd = CouncilDirector.Instance;
            if (!C(cd != null && CouncilSteward.Instance != null, "no council in the hub")) yield break;
            CouncilFixture.StageExample(S, cd.HomeSettlement, 0);
            Step(2);
            cd.StewardOpen(CouncilSteward.Instance.Steward);
            if (!C(EncounterHost.Instance.IsRunning, "the council did not start")) yield break;
            yield return null; yield return null;
            Moment?.Invoke("council");
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the council to end");
            if (Failure != null) yield break;
            yield return null;
            if (!Back(home, "council")) yield break;

            // 3. Rhythm: the home dance circle.
            var starters = DanceCircle.Current != null ? DanceCircle.Current.GetComponentsInChildren<DanceStarter>(true) : new DanceStarter[0];
            if (!C(starters.Length > 1, "no dance circle starter")) yield break;
            starters[1].Interact();
            if (!C(RhythmPartyEncounter.Current != null, "the rhythm party did not start")) yield break;
            yield return null; yield return null;
            Moment?.Invoke("rhythm");
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the dance to end");
            if (Failure != null) yield break;
            yield return null;
            if (!Back(home, "rhythm")) yield break;

            // 4. Healer's lodge: inspection only, it leaves the player where he stands.
            if (!C(HealersLodge.Instance != null, "no healer's lodge")) yield break;
            HealersLodge.Instance.Tap();
            yield return null;
            Moment?.Invoke("lodge");
            foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
            if (!Back(home, "healer's lodge")) yield break;

            // 5. The practice duel at the ring (Mira walks over; she walks home when it is left).
            var ring = DuelRing.Instance;
            if (!C(ring != null && ring.StartDuel(), "the duel ring did not start")) yield break;
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 60f, "Mira to walk to the ring");
            if (Failure != null) yield break;
            yield return null; yield return null;
            Moment?.Invoke("duel");
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 90f, "Mira to walk home");
            if (Failure != null) yield break;
            yield return null;
            if (!Back(home, "duel ring")) yield break;

            // 6. The crystal expedition by Mira: her offer starts the campaign; down the glade's stair; "Lead me out".
            var mira = Npc.Find(NpcIds.Mira);
            if (!C(mira != null, "no Mira")) yield break;
            mira.DialogueBuilder?.Invoke(mira);
            var offer = mira.Options.FirstOrDefault(o => o.Label == MiraOffer.Label);
            if (!C(offer != null, "Mira has no expedition offer")) yield break;
            offer.OnChoose();
            Step(2);
            if (!C(S.Campaign != null, "the campaign did not start")) yield break;
            yield return Until(() => ExpeditionDirector.Instance != null && Interiors.All.Count > 0, 20f, "the expedition director and the interiors");
            if (Failure != null) yield break;
            if (!Back(home, "Mira's offer")) yield break;
            if (!MarkerRef.TryParse("glade/stair", out var stairRef)) { C(false, "no glade stair marker"); yield break; }
            var stair = Interiors.LinkAt(stairRef);
            // The glade is far from the hub (50 to 100 m): the Tab map's walk is shortened to the stair's mouth.
            W.StopAuto();
            W.Teleport(stair.Cell + WalkLattice.Dir(stair.OutHeading), stair.OutHeading + 3);
            yield return null; yield return null;
            Debug.Log("[WorldTour] stair open=" + stair.IsOpen + " cell " + stair.Cell + " out " + stair.OutHeading + "; player " + W.Cell + " heading " + W.Heading + " blocked " + WalkGrid.IsBlocked(stair.Cell));
            Interiors.FactsOverride = f => true; // the story has not opened the way yet (as the interior tests do)
            W.Forward();
            yield return Until(() => Interiors.Current != null && !Interiors.IsCrossing && !W.IsGliding, 20f, "the player to go down the stair (stair open " + stair.IsOpen + ", player at " + W.Cell + ", stair " + stair.Cell + ")");
            if (Failure != null) yield break;
            yield return null;
            W.Teleport(InteriorScripts.CellOf("glade/quarry_upper/safe"), 0); // a few cells into the quarry
            yield return null; yield return null; yield return null;
            if (!C(Interiors.Current == "quarry_upper", "not in the quarry")) yield break;
            Moment?.Invoke("crystal");
            if (!C(Interiors.LeadOut(), "Lead me out was refused")) yield break;
            yield return Until(() => Interiors.Current == null && !Interiors.IsCrossing && !Interiors.IsLeadingOut, 150f, "Lead me out to reach the surface (crossings " + InteriorDirector.Instance.CrossingCount + ", in " + Interiors.Current + ", crossing " + Interiors.IsCrossing + ", leading " + Interiors.IsLeadingOut + ", cell " + W.Cell + ", auto " + W.IsAutoWalking + ", gliding " + W.IsGliding + ", locked " + W.IsLocked + ")", true);
            if (Failure != null) yield break;
            yield return null;
            Interiors.FactsOverride = null;
            // Decision 11: leaving the dungeon puts the player back on the hub cell with the heading and view they set out with.
            if (!Back(home, "Lead me out of the crystal expedition")) yield break;
            Done.Add("crystal expedition");

            // State kept through all of it.
            if (!C(S.Table.Phase == phase && S.Table.Paused && S.Table.ActiveTicks == frozen, "state: the god board table changed during the tour")) yield break;
            if (!C(S.Campaign != null, "state: the campaign was lost")) yield break;
            if (!C(Application.isEditor || Type.GetType(string.Join("", new[] { "World.Game.Zone", "Outline", "Tool, World.Game" })) == null, "a wireframe overlay type is in the player")) yield break;
            CouncilDirector.Disabled = councilWas;
            Debug.Log("[WorldTour] all six destinations visited: " + string.Join(", ", Done));
        }
    }
}
