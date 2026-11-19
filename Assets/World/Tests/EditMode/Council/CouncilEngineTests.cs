using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Encounters.Council;
using static World.Sim.Tests.CouncilTestKit;

namespace World.Sim.Tests
{
    /// <summary>The election rules (brief section 3 and 6).</summary>
    public class CouncilEngineTests
    {
        [Test]
        public void Tracks_HaveTheBriefsLengths()
        {
            CollectionAssert.AreEqual(new[] { 3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 }, CouncilRules.TrackLengths);
            Assert.AreEqual(83, CouncilRules.TrackLengths.Sum());
        }

        [Test]
        public void StarterIsFairlySpread_AndFixedBySeed()
        {
            var counts = new int[4];
            for (ulong s = 1; s <= 400; s++) counts[NewElection(s).Starter]++;
            foreach (int c in counts) Assert.That(c, Is.InRange(60, 140), string.Join(",", counts));
            Assert.AreEqual(NewElection(77).Starter, NewElection(77).Starter);
        }

        [Test]
        public void Example_Values1345_GiveTrays_4and9_5and8_6and7()
        {
            var e = NewElection();
            var trays = ElectionEngine.BuildTrays(e, 1, 3, 4, 5);
            Assert.AreEqual(3, trays.Count);
            CollectionAssert.AreEqual(new[] { 4, 9 }, SocketTracks(trays[0]));
            CollectionAssert.AreEqual(new[] { 5, 8 }, SocketTracks(trays[1]));
            CollectionAssert.AreEqual(new[] { 6, 7 }, SocketTracks(trays[2]));
            foreach (var t in trays) Assert.AreEqual(TrayMode.Both, t.Mode);
        }

        [Test]
        public void ThreePairings_AreAllConsidered_AndSumsMatch()
        {
            var e = NewElection();
            // 2,2,5,6: pairings (4,11), (7,8), (8,7) - the last two are the same move and merge.
            var trays = ElectionEngine.BuildTrays(e, 2, 2, 5, 6);
            Assert.AreEqual(2, trays.Count);
            CollectionAssert.AreEqual(new[] { 4, 11 }, SocketTracks(trays[0]));
            CollectionAssert.AreEqual(new[] { 7, 8 }, SocketTracks(trays[1]));
            // 1,2,3,6: (3,9), (4,8), (7,5): three different trays; sums are always ordered low to high.
            trays = ElectionEngine.BuildTrays(e, 1, 2, 3, 6);
            Assert.AreEqual(3, trays.Count);
            CollectionAssert.AreEqual(new[] { 3, 9 }, SocketTracks(trays[0]));
            CollectionAssert.AreEqual(new[] { 4, 8 }, SocketTracks(trays[1]));
            CollectionAssert.AreEqual(new[] { 5, 7 }, SocketTracks(trays[2]));
        }

        [Test]
        public void AllValuesEqual_GiveOneMergedDoubleTray()
        {
            var e = NewElection();
            var trays = ElectionEngine.BuildTrays(e, 3, 3, 3, 3);
            Assert.AreEqual(1, trays.Count);
            CollectionAssert.AreEqual(new[] { 6, 6 }, SocketTracks(trays[0]));
        }

        [Test]
        public void BothAdvances_MustBeUsedTogether_WhenBothLegal()
        {
            var e = NewElection();
            e.Active = 0;
            e.Temp[5 - 2] = 1; // one catspaw already on track 5
            Stage(e, 1, 3, 4, 5); // trays 4+9, 5+8, 6+7 with 2 catspaws free
            var ev = new List<CouncilEvent>();
            Assert.IsTrue(Do(e, ElectionAction.Choose(0, e.PushId, 1), ev).Ok); // 5 + 8
            Assert.AreEqual(2, e.Temp[5 - 2]);
            Assert.AreEqual(1, e.Temp[8 - 2], "a fresh catspaw one ahead of the banked position (none: first place)");
            var made = ev.OfType<CouncilAdvanceMade>().Single();
            Assert.AreEqual(2, made.Steps.Count);
            Assert.AreEqual(ElectionPhase.Ready, e.Phase);
            Assert.IsTrue(e.HasAdvanced);
        }

        [Test]
        public void FirstStep_IsOneAheadOfTheBankedPosition()
        {
            var e = NewElection();
            e.Active = 1;
            e.Banked[1 * 11 + (7 - 2)] = 4;
            Stage(e, 3, 4, 1, 2); // 7+3... pairing (3+4=7, 1+2=3)
            var trays = e.Pending;
            CollectionAssert.AreEqual(new[] { 3, 7 }, SocketTracks(trays[0]));
            Assert.IsTrue(Do(e, ElectionAction.Choose(1, e.PushId, 0)).Ok);
            Assert.AreEqual(5, e.Temp[7 - 2]);
        }

        [Test]
        public void OnlyOneUsable_ShowsOneCarvingAndAnEmptySocket()
        {
            var e = NewElection();
            e.Claimed[4 - 2] = 2; // dispute 4 is claimed and closed
            var trays = ElectionEngine.BuildTrays(e, 1, 3, 4, 5); // 4+9: only 9 usable
            Assert.AreEqual(3, trays.Count);
            Assert.IsTrue(trays[0].Sockets[0].IsEmpty);
            Assert.AreEqual(9, trays[0].Sockets[1].Track);
            Assert.AreEqual(TrayMode.Both, trays[0].Mode);
        }

        [Test]
        public void OneFreeCatspaw_TwoNewTracks_AreTwoSeparateChoices_AndTheOtherIsLeftBehind()
        {
            var e = NewElection();
            e.Active = 0;
            e.Temp[5 - 2] = 1;
            e.Temp[6 - 2] = 1; // one catspaw left
            Stage(e, 1, 3, 4, 5);
            var t0 = e.Pending[0];
            Assert.AreEqual(TrayMode.Either, t0.Mode);
            CollectionAssert.AreEqual(new[] { 4, 9 }, SocketTracks(t0));
            Assert.AreEqual(TrayMode.Both, e.Pending[1].Mode, "5+8 uses a catspaw already placed on 5");

            // choosing the tray without a socket is refused, as is an empty one
            var r = ElectionEngine.Validate(e, ElectionAction.Choose(0, e.PushId, 0));
            Assert.AreEqual(CouncilReasons.SocketRequired, r.Reason);
            r = ElectionEngine.Validate(e, ElectionAction.Choose(0, e.PushId, 0, 2));
            Assert.AreEqual(CouncilReasons.BadSocket, r.Reason);

            // either single advance can be chosen
            foreach (int socket in new[] { 0, 1 })
            {
                var copy = RoundTrip(e);
                Assert.IsTrue(Do(copy, ElectionAction.Choose(0, copy.PushId, 0, socket)).Ok);
                int chosen = t0.Sockets[socket].Track, other = t0.Sockets[1 - socket].Track;
                Assert.AreEqual(1, copy.Temp[chosen - 2]);
                Assert.AreEqual(0, copy.Temp[other - 2], "the unused carving is left behind");
                Assert.AreEqual(3, copy.CatspawsInPlay);
            }
        }

        [Test]
        public void Double_MovesOneCatspawTwoPlaces_WhenRoomRemains_OneWhenFirstReachesTheTop()
        {
            var e = NewElection();
            var trays = ElectionEngine.BuildTrays(e, 1, 3, 2, 2); // 4+4 double, 3+5
            Assert.AreEqual(2, trays.Count);
            Assert.AreEqual(4, trays[0].Sockets[0].Track);
            Assert.AreEqual(4, trays[0].Sockets[1].Track);
            e.Active = 0;
            Stage(e, 1, 3, 2, 2);
            Assert.IsTrue(Do(e, ElectionAction.Choose(0, e.PushId, 0)).Ok);
            Assert.AreEqual(2, e.Temp[4 - 2], "two places on one catspaw");
            Assert.AreEqual(1, e.CatspawsInPlay);

            // track 2 has 3 places: from banked 2 only one step remains
            var f = NewElection();
            f.Banked[f.Active * 11] = 2;
            var tr = ElectionEngine.BuildTrays(f, 1, 1, 1, 1);
            Assert.AreEqual(1, tr.Count);
            Assert.AreEqual(2, tr[0].Sockets[0].Track);
            Assert.IsTrue(tr[0].Sockets[1].IsEmpty);
            Stage(f, 1, 1, 1, 1);
            Do(f, ElectionAction.Choose(f.Active, f.PushId, 0));
            Assert.AreEqual(3, f.Temp[0]);
        }

        [Test]
        public void Double_WithNoFreeCatspaw_NeedsAnExistingOneOnThatTrack()
        {
            var e = NewElection();
            e.Temp[3] = 1; e.Temp[4] = 1; e.Temp[5] = 1; // three catspaws on 5, 6, 7
            var trays = ElectionEngine.BuildTrays(e, 1, 3, 2, 2); // 4+4: no catspaw on 4 and none free; 3+5: 5 only
            Assert.AreEqual(1, trays.Count);
            Assert.AreEqual(5, trays[0].Sockets[1].Track);
            Assert.IsTrue(trays[0].Sockets[0].IsEmpty);
        }

        [Test]
        public void TopOfTrack_CannotAdvanceFurther_AndIsNotClaimedUntilBanked()
        {
            var e = NewElection();
            e.Active = 0;
            e.Banked[1 * 11 + 0] = 2; // another candidate's marker on dispute 2
            e.Temp[0] = 3; // catspaw on the last place of track 2
            var trays = ElectionEngine.BuildTrays(e, 1, 1, 1, 1); // all sum 2
            Assert.AreEqual(0, trays.Count, "a catspaw at the top cannot advance");
            Assert.AreEqual(-1, e.Claimed[0], "not claimed before banking");
            e.HasAdvanced = true;
            var ev = new List<CouncilEvent>();
            Assert.IsTrue(Do(e, ElectionAction.Bank(0), ev).Ok);
            Assert.AreEqual(0, e.Claimed[0]);
            Assert.AreEqual(3, e.BankedAt(0, 2));
            Assert.AreEqual(0, e.BankedAt(1, 2), "other contenders' markers are removed");
            Assert.That(ev.OfType<CouncilTrackClaimed>().Single().RemovedFrom, Is.EquivalentTo(new[] { 1 }));
            Assert.AreEqual(1, e.Active, "banking passes the turn");
        }

        [Test]
        public void ClaimedTracks_AreClosedToEveryone()
        {
            var e = NewElection();
            e.Claimed[7 - 2] = 3;
            for (int seat = 0; seat < 4; seat++)
            {
                e.Active = seat;
                var trays = ElectionEngine.BuildTrays(e, 3, 4, 4, 3); // 7+7 is a double on a closed track
                foreach (var t in trays) foreach (var s in t.Sockets) Assert.AreNotEqual(7, s.Track);
                Assert.AreEqual(1, trays.Count, "only 6+8 remains");
                trays = ElectionEngine.BuildTrays(e, 3, 4, 1, 2); // 7+3: only 3
                Assert.AreEqual(3, trays[0].Sockets[0].Track); Assert.IsTrue(trays[0].Sockets[1].IsEmpty);
            }
        }

        [Test]
        public void MoreThanThreeTracksIsImpossible_AtMostThreeCatspaws()
        {
            var e = NewElection();
            e.Temp[0] = 1; e.Temp[1] = 1; e.Temp[2] = 1;
            Assert.AreEqual(0, e.CatspawsLeft);
            foreach (var t in ElectionEngine.BuildTrays(e, 5, 6, 6, 5))
                foreach (var s in t.Sockets) Assert.That(s.IsEmpty || s.Track <= 4, "no catspaw can be placed on a fourth track");
        }

        [Test]
        public void Bust_DropsUnbankedProgress_KeepsBanked_PassesTheTurn()
        {
            var e = NewElection(5, -1);
            e.Active = 0;
            CloseAllExcept(e, 2);
            e.Temp[0] = 3; // the only open track: its catspaw is at the top, so every push is a bust
            e.Banked[0 * 11 + 7] = 3; // banked place on a (claimed) track stays as is
            e.HasAdvanced = true;
            var ev = new List<CouncilEvent>();
            Assert.IsTrue(Do(e, ElectionAction.Push(0), ev).Ok);
            Assert.AreEqual(0, e.Temp[0]);
            Assert.AreEqual(3, e.Banked[0 * 11 + 7]);
            Assert.AreEqual(1, e.Active);
            Assert.AreEqual(1, e.Busts);
            Assert.IsFalse(e.HasAdvanced);
            CollectionAssert.AreEqual(
                new[] { typeof(CouncilPushMade), typeof(CouncilOptionsOffered), typeof(CouncilBust), typeof(CouncilTurnPassed) },
                ev.Select(x => x.GetType()).ToArray());
            Assert.AreEqual(0, ev.OfType<CouncilOptionsOffered>().Single().Trays.Count);
        }

        [Test]
        public void Bust_WithPetition_WaitsForAChoice_AndSpendingGivesAFreshPushKeepingProgress()
        {
            var e = NewElection(9, 0);
            e.Active = 0;
            CloseAllExcept(e, 2);
            e.Temp[0] = 3;
            e.HasAdvanced = true;
            e.PetitionEarned = true;
            var ev = new List<CouncilEvent>();
            Do(e, ElectionAction.Push(0), ev);
            Assert.AreEqual(ElectionPhase.BustPending, e.Phase);
            Assert.AreEqual(3, e.Temp[0], "nothing is lost yet");
            Assert.AreEqual(0, e.Active);
            Assert.AreEqual(1, e.PushId);
            Assert.IsTrue(e.PetitionAvailable);
            Assert.IsTrue(ev.Any(x => x is CouncilBustPending));

            // a stand-in position where a fresh push cannot bust: catspaw on 7 with two free and everything open
            var g = NewElection(9, 0);
            g.Active = 0;
            g.PetitionEarned = true;
            g.Temp[7 - 2] = 2;
            g.HasAdvanced = true;
            g.Phase = ElectionPhase.BustPending;
            Assert.IsTrue(Do(g, ElectionAction.SpendPetition(0)).Ok);
            Assert.IsTrue(g.PetitionSpent);
            Assert.IsFalse(g.PetitionAvailable);
            Assert.AreEqual(ElectionPhase.Choosing, g.Phase);
            Assert.AreEqual(2, g.Temp[7 - 2], "the sitting's progress is kept");
            Assert.AreEqual(1, g.Pushes);

            // refusing the petition takes the setback
            Assert.IsTrue(Do(e, ElectionAction.AcceptBust(0)).Ok);
            Assert.AreEqual(0, e.Temp[0]);
            Assert.AreEqual(1, e.Active);
            Assert.IsTrue(e.PetitionAvailable, "an unspent petition stays in hand");
        }

        [Test]
        public void Petition_CannotBeSpentWithoutBeingEarned_OrOutsideABust()
        {
            var e = NewElection(3, 0);
            e.Active = 0;
            Assert.AreEqual(CouncilReasons.WrongPhase, ElectionEngine.Validate(e, ElectionAction.SpendPetition(0)).Reason);
            e.Phase = ElectionPhase.BustPending;
            Assert.AreEqual(CouncilReasons.NoPetition, ElectionEngine.Validate(e, ElectionAction.SpendPetition(0)).Reason);
            // an AI seat never holds the petition even if the player's flag is set
            e.PetitionEarned = true; e.Active = 1;
            Assert.AreEqual(CouncilReasons.NoPetition, ElectionEngine.Validate(e, ElectionAction.SpendPetition(1)).Reason);
        }

        [Test]
        public void ThirdClaim_WinsTheElection()
        {
            var e = NewElection(11, -1);
            e.Active = 2;
            e.Claimed[3] = 2; e.Claimed[5] = 2;
            e.Temp[0] = 3;
            e.HasAdvanced = true;
            var ev = new List<CouncilEvent>();
            Assert.IsTrue(Do(e, ElectionAction.Bank(2), ev).Ok);
            Assert.AreEqual(2, e.Winner);
            Assert.AreEqual(ElectionPhase.Complete, e.Phase);
            Assert.IsTrue(ev.Any(x => x is CouncilElectionWon));
            Assert.IsFalse(ev.Any(x => x is CouncilTurnPassed));
            Assert.AreEqual(CouncilReasons.ElectionOver, ElectionEngine.Validate(e, ElectionAction.Push(2)).Reason);
        }

        [Test]
        public void TwoClaims_DoNotWinYet()
        {
            var e = NewElection(11, -1);
            e.Active = 2;
            e.Claimed[3] = 2;
            e.Temp[0] = 3;
            e.HasAdvanced = true;
            Do(e, ElectionAction.Bank(2));
            Assert.AreEqual(-1, e.Winner);
            Assert.AreEqual(3, e.Active);
        }

        [Test]
        public void Bank_NeedsAnAdvance()
        {
            var e = NewElection();
            Assert.AreEqual(CouncilReasons.NothingToBank, ElectionEngine.Validate(e, ElectionAction.Bank(e.Active)).Reason);
        }

        [Test]
        public void RepeatedAndStaleActions_AreRefusedWithAReason()
        {
            var e = NewElection();
            int seat = e.Active;
            e.Active = 0; seat = 0;
            Stage(e, 1, 3, 4, 5);
            int push = e.PushId;
            Assert.IsTrue(Do(e, ElectionAction.Choose(seat, push, 0)).Ok);
            string json = Json(e);
            // the same tap again (a held or double tap)
            var r = Do(e, ElectionAction.Choose(seat, push, 0));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(CouncilReasons.WrongPhase, r.Reason);
            // a push, then a tap quoting the earlier push
            Stage(e, 1, 3, 4, 5);
            r = Do(e, ElectionAction.Choose(seat, push, 0));
            Assert.AreEqual(CouncilReasons.StalePush, r.Reason);
            r = Do(e, ElectionAction.Choose(seat, e.PushId, 7));
            Assert.AreEqual(CouncilReasons.BadTray, r.Reason);
            r = Do(e, ElectionAction.Choose(1, e.PushId, 0));
            Assert.AreEqual(CouncilReasons.NotYourTurn, r.Reason);
            var stale = ElectionAction.Choose(seat, e.PushId, 0);
            stale.ExpectedSeq = e.ActionSeq - 1;
            Assert.AreEqual(CouncilReasons.StaleAction, Do(e, stale).Reason);
            // a refused action changes nothing
            Assert.AreEqual(Json(e), Json(e));
            e.Phase = ElectionPhase.Ready;
            Assert.IsTrue(Do(e, ElectionAction.Bank(seat)).Ok);
            Assert.AreEqual(CouncilReasons.NotYourTurn, Do(e, ElectionAction.Bank(seat)).Reason, "a second bank tap is refused");
            Assert.AreNotEqual(json, Json(e));
        }

        [Test]
        public void RefusedAction_ChangesNothing()
        {
            var e = NewElection(4, 0);
            Stage(e, 1, 3, 4, 5);
            e.Active = 0;
            string before = Json(e);
            Assert.IsFalse(Do(e, ElectionAction.Choose(0, e.PushId, 9)).Ok);
            Assert.IsFalse(Do(e, ElectionAction.Bank(0)).Ok);
            Assert.IsFalse(Do(e, ElectionAction.Push(0)).Ok);
            Assert.AreEqual(before, Json(e));
        }

        [Test]
        public void PendingPush_IsPartOfTheSavedState()
        {
            var e = NewElection(6, 0);
            e.Active = 0;
            Assert.IsTrue(Do(e, ElectionAction.Push(0)).Ok);
            if (e.Phase != ElectionPhase.Choosing) Assert.Inconclusive("first push had no legal move");
            var copy = RoundTrip(e);
            Assert.AreEqual(Json(e), Json(copy));
            Assert.AreEqual(e.Pending.Count, copy.Pending.Count);
            for (int i = 0; i < e.Pending.Count; i++)
                CollectionAssert.AreEqual(SocketTracks(e.Pending[i]), SocketTracks(copy.Pending[i]));
            Assert.AreEqual(e.PushId, copy.PushId);
        }

        [Test]
        public void SaveAndLoad_AtEveryStep_ContinuesIdentically()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var e = NewElection(seed, (int)(seed % 4));
                e.PetitionEarned = seed % 2 == 0; // some runs exercise the pending-bust phase
                var phases = new HashSet<ElectionPhase>();
                int guard = 0;
                while (e.Phase != ElectionPhase.Complete && guard++ < 3000)
                {
                    phases.Add(e.Phase);
                    var copy = RoundTrip(e);
                    Assert.AreEqual(Json(e), Json(copy));
                    Step(e);
                    Step(copy);
                    Assert.AreEqual(Json(e), Json(copy), "seed " + seed + " step " + guard);
                }
                Assert.IsTrue(phases.Contains(ElectionPhase.Ready) && phases.Contains(ElectionPhase.Choosing));
            }
        }

        [Test]
        public void RollStream_DependsOnlyOnTheSeedAndPushCount()
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var e = NewElection(seed, 0);
                e.PetitionEarned = true;
                PlayOut(e);
                var r = new Rng(seed).Fork("rolls");
                r.NextInt(4);
                for (int i = 0; i < e.Pushes * 4; i++) r.NextRange(1, 7);
                Assert.AreEqual(r.State, e.Rolls.State, "nothing but pushes draws from the election's stream");
            }
        }

        [Test]
        public void Determinism_SameSeedSamePlay()
        {
            string a, b;
            { var e = NewElection(42, -1); PlayOut(e); a = Json(e); }
            { var e = NewElection(42, -1); PlayOut(e); b = Json(e); }
            Assert.AreEqual(a, b);
            { var e = NewElection(43, -1); PlayOut(e); Assert.AreNotEqual(a, Json(e)); }
        }

        [Test]
        public void AllAiElection_EveryActionGoesThroughTheSharedValidator()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var e = NewElection(seed, -1);
                int guard = 0;
                while (e.Phase != ElectionPhase.Complete && guard++ < 5000)
                {
                    Assert.AreNotEqual(ElectionPhase.BustPending, e.Phase, "an AI candidate never holds a petition");
                    var p = CouncilContent.BuiltIn.Personality(e.Personality[e.Active]);
                    var a = CouncilAi.Decide(e, p);
                    Assert.AreEqual(e.Active, a.Seat);
                    Assert.IsTrue(ElectionEngine.Validate(e, a).Ok, "AI proposed an illegal action: " + a.Kind);
                    // a hostile twin of the same action by the wrong seat is refused by the same validator
                    var wrong = new ElectionAction { Kind = a.Kind, Seat = (a.Seat + 1) % 4, PushId = a.PushId, Tray = a.Tray, Socket = a.Socket };
                    Assert.AreEqual(CouncilReasons.NotYourTurn, ElectionEngine.Validate(e, wrong).Reason);
                    Assert.IsTrue(ElectionEngine.Apply(e, a, null).Ok);
                }
                Assert.AreEqual(ElectionPhase.Complete, e.Phase);
                Assert.That(e.Winner, Is.InRange(0, 3));
                Assert.AreEqual(3, e.ClaimsOf(e.Winner));
            }
        }

        [Test]
        public void Personalities_DifferInTheirStoppingBehaviour()
        {
            var c = CouncilContent.BuiltIn;
            var avgPushes = new Dictionary<string, double>();
            foreach (var p in c.Personalities)
            {
                double sum = 0; int sittings = 0;
                for (ulong seed = 1; seed <= 25; seed++)
                {
                    var e = ElectionEngine.Create(seed, -1, new[] { p.Id, p.Id, p.Id, p.Id });
                    PlayOut(e);
                    sum += e.Pushes; sittings += e.Sittings;
                }
                avgPushes[p.Id] = sum / sittings;
            }
            Assert.Less(avgPushes["careful"], avgPushes["bold"], string.Join(", ", avgPushes.Select(kv => kv.Key + "=" + kv.Value.ToString("0.00"))));
            Assert.Less(avgPushes["careful"], avgPushes["steady"]);
        }

        [Test]
        public void BustOdds_AreExactAndPlausible()
        {
            var e = NewElection();
            Assert.AreEqual(0, ElectionEngine.BustOutcomes(e));
            CloseAllExcept(e, 2);
            e.Temp[0] = 3;
            Assert.AreEqual(1296, ElectionEngine.BustOutcomes(e));
            var f = NewElection();
            f.Temp[0] = 1; f.Temp[1] = 1; f.Temp[2] = 1; // catspaws on 2, 3, 4
            double pct = ElectionEngine.BustPercent(f);
            Assert.That(pct, Is.GreaterThan(0).And.LessThan(100));
        }
    }
}
