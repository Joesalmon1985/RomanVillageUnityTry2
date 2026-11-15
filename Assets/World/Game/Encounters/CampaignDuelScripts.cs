using System;
using System.Collections;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;
using World.Sim.Eras;

namespace World.Game
{
    /// <summary>
    /// AutoPlay scripts of the campaign duel (PRD_CRYSTALS I7) and the developer helpers they share with the PlayMode
    /// tests. The helpers act like a player and the F1 dev commands: they send commands and step the clock; they never
    /// write the state.
    /// <list type="bullet">
    /// <item><c>duel_keeper_win</c>: dev.historic, StartCampaign on the authored pack, the Ash expedition takes the Deepstone
    /// (DevRivalNextStep), Mira's leads are followed to the Tally Vault threshold, "Fight"; the duel is staged in the Vault
    /// and won by the scripted player; checks one resolution, Dolm beaten, the crystal carried, nobody dead.</item>
    /// <item><c>duel_keeper_loss</c>: the same to the threshold; the scripted player never casts, Dolm breaks Mira's Ward;
    /// checks the first volunteer (Garrick) dead once, then saves and loads at once and checks it again (still one death,
    /// one resolution, no pending attempt).</item>
    /// <item><c>duel_friendly</c>: a friendly duel with Orrin near the player: no stakes card, no attempt, nothing
    /// recorded, a rematch, return.</item>
    /// </list>
    /// Each logs <c>[AutoPlay] &lt;name&gt; OK</c> and quits 0 (in the editor: stays in Play Mode).
    /// </summary>
    public static class CampaignDuelScripts
    {
        public const string KeeperWin = "duel_keeper_win", KeeperLoss = "duel_keeper_loss", FriendlyName = "duel_friendly";
        public const string KeeperEncounter = "keeper_earth";
        /// <summary>Captures are written when true (AutoPlay captures go to Evidence/screenshots).</summary>
        public static bool Captures = true;

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static WorldState S => B.Sim.State;

        public static void Register()
        {
            AutoPlayScripts.Register(KeeperWin, () => Keeper(true));
            AutoPlayScripts.Register(KeeperLoss, () => Keeper(false));
            AutoPlayScripts.Register(FriendlyName, Friendly);
        }

        // ------------------------------------------------------------------ helpers (also used by tests)

        /// <summary>The authored pack's folder (StreamingAssets/world/campaigns/authored_v1).</summary>
        public static string AuthoredPack => Path.Combine(Application.streamingAssetsPath, "world", "campaigns", "authored_v1");

        /// <summary>StartCampaign on the authored pack and one tick. Returns the refusal sentence, or null when it runs.</summary>
        public static string StartAuthoredCampaign()
        {
            var d = CampaignLoader.LoadDirectory(AuthoredPack);
            var r = B.Sim.Submit(new StartCampaign { Definition = d });
            if (!r.Ok) return r.Reason + ": " + r.Message;
            B.Clock.StepOnce();
            return CampaignApi.IsRunning(S) ? null : "the campaign did not start";
        }

        /// <summary>The F1 dev command: the Ash expedition finishes steps until the crystal is on its altar.</summary>
        public static bool StealToAltar(Element e)
        {
            for (int i = 0; i < 8; i++)
            {
                var cv = CampaignApi.Crystal(S, e);
                if (cv != null && cv.Custody == Custody.AtRivalAltar) return true;
                B.Sim.Submit(new DevRivalNextStep());
                B.Clock.StepOnce();
            }
            var c = CampaignApi.Crystal(S, e);
            return c != null && c.Custody == Custody.AtRivalAltar;
        }

        /// <summary>
        /// Plays the player's part as fast as the sim allows (the clock paused, stepped by hand): "I'm with you", "Lead the
        /// way" on Mira's top lead, the right answer to her questions, until the party stands at the encounter's threshold.
        /// </summary>
        public static IEnumerator DriveToThreshold(string encounterId, int maxTicks, Action<string> fail, int ticksPerFrame = 400)
        {
            const string Hold = "CampaignDuelDrive";
            B.Clock.Pause(Hold);
            long end = S.Tick + maxTicks;
            bool done = false;
            try
            {
                while (S.Tick < end)
                {
                    for (int n = 0; n < ticksPerFrame && S.Tick < end; n++)
                    {
                        var c = S.Campaign;
                        var p = c?.Party;
                        if (p == null || c.Phase != CampaignPhase.Running) { fail("the campaign is not running"); yield break; }
                        if (p.Mode == PartyMode.AtThreshold && p.EncounterId == encounterId) { done = true; break; }
                        Decide(c, p);
                        B.Clock.StepOnce();
                    }
                    if (done) break;
                    yield return null;
                }
            }
            finally { B.Clock.Resume(Hold); }
            if (!done) fail("the party did not reach the " + encounterId + " threshold in " + maxTicks + " ticks (mode " + S.Campaign?.Party?.Mode + " at " + S.Campaign?.Party?.At + ")");
        }

        private static void Decide(CampaignState c, PartyState p)
        {
            if (!p.PlayerPresent) { B.Sim.Submit(new SetCompany { Present = true }); return; }
            if (B.Sim.PendingCommands.Count > 0) return;
            switch (p.Mode)
            {
                case PartyMode.AskingChoice:
                {
                    var ch = c.Definition.Choice(p.ChoiceId);
                    int right = ch?.Answers?.FindIndex(a => a.Correct) ?? -1;
                    if (right >= 0) B.Sim.Submit(new AdviseMira { ChoiceId = p.ChoiceId, Answer = right });
                    return;
                }
                case PartyMode.Idle:
                case PartyMode.WaitingForPlayer:
                {
                    var top = CampaignApi.TopLead(S);
                    if (top != null && B.Sim.Check(new AcceptLead { LeadId = top.Id }).Ok) B.Sim.Submit(new AcceptLead { LeadId = top.Id });
                    return;
                }
            }
        }

        /// <summary>"Fight": CommitEncounter and one tick; the commit event stages the duel (CampaignDuel's driver).</summary>
        public static string Commit(string encounterId)
        {
            var r = B.Sim.Submit(new CommitEncounter { EncounterId = encounterId });
            if (!r.Ok) return r.Reason + ": " + r.Message;
            B.Clock.StepOnce();
            return null;
        }

        private static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float timeout, string message)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) { a.Fail(message); yield break; }
                yield return null;
            }
        }

        /// <summary>As <see cref="AutoPlayScript.Check"/> with the failure message built only when it fails (it may read state that does not exist yet).</summary>
        public static AutoPlayScript Verify(this AutoPlayScript s, Func<bool> check, Func<string> message) =>
            s.Step(a => a.DoCall(() => { if (!check()) a.Fail(message()); }));

        /// <summary>A capture named for the window size it was taken at (i7_stakes_844x390.png).</summary>
        private static AutoPlayScript Shot(AutoPlayScript s, string name) =>
            Captures ? s.Step(a => a.DoCapture(name + "_" + Screen.width + "x" + Screen.height)) : s;

        // ------------------------------------------------------------------ duel_keeper_win / duel_keeper_loss

        private static int _attempt;
        private static string _volunteer;
        private static int _alive;
        private static long _rival;

        private static AutoPlayScript Keeper(bool win)
        {
            string name = win ? KeeperWin : KeeperLoss;
            var s = new AutoPlayScript(name);
            // F1 developer start "dev.historic": the scene reloads; the rest runs in the new scene.
            s.Wait(0.5f).Call(() =>
            {
                try
                {
                    // The duel alone, as package I7 built it: the expedition's director (I6) stays off for these scripts,
                    // since its party, scout and company rules would fight the commands this script sends (I8).
                    ExpeditionInstaller.Disabled = true;
                    AutoPlay.ContinueAfterLoad = KeeperAfterStart(name, win);
                    if (!DevStart.Begin(ScenarioStarts.DevHistoric)) Debug.LogWarning("[AutoPlay] " + name + ": " + DevStart.LastError);
                }
                catch (Exception e) { Debug.LogException(e); throw; }
            }).Wait(30f).Check(() => false, "dev.historic did not start");
            return s;
        }

        private static AutoPlayScript KeeperAfterStart(string name, bool win)
        {
            var s = new AutoPlayScript(name + " (dev.historic)");
            string err = null;
            s.Wait(1f)
             .Check(() => DevStart.Current == ScenarioStarts.DevHistoric, "not on dev.historic")
             .Call(() => err = StartAuthoredCampaign())
             .Check(() => err == null, "StartCampaign refused")
             .Check(() => StealToAltar(Element.Earth), "the Ash expedition did not take the Deepstone to its altar")
             .Step(a => DriveToThreshold(KeeperEncounter, 40000, a.Fail))
             .Call(() =>
             {
                 var vol = CampaignApi.NextVolunteer(S);
                 _volunteer = vol?.Id; _alive = AliveCount(); _rival = S.Campaign.RivalProgress;
                 Debug.Log("[AutoPlay] " + name + ": at the Tally Vault threshold at tick " + S.Tick + ", volunteer " + _volunteer + ", " + _alive + " alive");
             })
             .Call(() => { var bot = WardBot.Attach(B.gameObject); bot.CastInterval = win ? 2.1f : 2.4f; bot.MaxCastsPerDuel = -1; bot.FixedWard = null; bot.AutoReturn = false; bot.Passive = !win; bot.WaitForRival = win; bot.StakesReadSeconds = 2.5f; })
             .Call(() => err = Commit(KeeperEncounter))
             .Check(() => err == null, "CommitEncounter refused")
             .Check(() => WardDuelEncounter.Current != null && WardDuelEncounter.Current.IsLethal, "the lethal duel was not staged from EncounterCommitted")
             .Call(() => _attempt = WardDuelEncounter.Current.Request.AttemptId)
             .Check(() => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Stakes && WardDuelEncounter.Current.StakesText != null && WardDuelEncounter.Current.StakesText.Contains("Garrick"), "no stakes card naming the volunteer")
             .Wait(0.8f);
            Shot(s, win ? "i7_stakes" : "i7_stakes_loss");
            s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Active, 15f, "Mira's Ward was not set"));
            if (win)
            {
                s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.Session.RivalHistory.Count >= 1 || WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 40f, "no casts"))
                 .Wait(0.35f);
                Shot(s, "i7_mid_duel");
            }
            else
            {
                s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.StruckCue != null && WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Active, 40f, "no struck cue"));
                Shot(s, "i7_struck");
            }
            s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 240f, "the duel did not finish"))
             .Check(() => WardDuelEncounter.Current.ResolutionSent, "ResolveEncounter was not sent at the end event")
             .Check(() => CampaignApi.Attempt(S) == null, "the attempt is still pending after the end event")
             .Wait(win ? 0.45f : 1.7f);
            Shot(s, win ? "i7_ward_break" : "i7_loss_volunteer_holds");
            s.Call(() => Debug.Log("[AutoPlay] " + name + " duel " + WardDuelEncounter.Current.LastOutcome + " -> " + WardDuelEncounter.Current.Resolution + " after " + WardDuelEncounter.Current.Session.PlayerHistory.Count + " casts by Mira, " + WardDuelEncounter.Current.Session.RivalHistory.Count + " by Dolm"));
            if (win) s.Check(() => WardDuelEncounter.Current.LastOutcome == WardOutcome.Victory, "the scripted player did not win");
            else s.Check(() => WardDuelEncounter.Current.LastOutcome == WardOutcome.Defeat, "the passive player did not lose");
            s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.HoldOver, 5f, "the presentation hold did not end")).Wait(0.4f);
            Shot(s, win ? "i7_result_win" : "i7_result_loss");
            s.Check(() => !WardDuelEncounter.Current.Rematch(), "a lethal duel offered a rematch")
             .Call(() => WardDuelEncounter.Current.Return())
             .Step(a => WaitFor(a, () => !EncounterHost.Instance.IsRunning, 3f, "the duel did not end on Return"))
             .Check(() => !B.Clock.IsPausedFor(PauseReasons.Encounter), "the clock did not resume")
             .Verify(() => CampaignDuel.ResolvesSent == 1, () => "ResolveEncounter sent " + CampaignDuel.ResolvesSent + " times")
             .Wait(1.0f)
             .Check(() => WardDuelEncounter.Current == null && CampaignApi.Attempt(S) == null, "a duel was staged again after the result");
            if (win)
            {
                s.Check(() => AliveCount() == _alive, "somebody died after a victory")
                 .Check(() => CampaignApi.Crystal(S, Element.Earth).Custody == Custody.Carried, "the Deepstone is not carried after beating Dolm")
                 .Verify(() => S.Campaign.Resolved.Count == 1, () => "attempts recorded: " + S.Campaign.Resolved.Count)
                 .Log("[AutoPlay] " + KeeperWin + " OK")
                 .Quit(0);
            }
            else
            {
                Verify(s, () => !IsAlive(_volunteer), () => "the volunteer " + _volunteer + " is alive after the defeat")
                 .Verify(() => AliveCount() == _alive - 1, () => "alive " + AliveCount() + ", expected " + (_alive - 1))
                 .Check(() => S.Campaign.RivalProgress >= _rival + 900, "the defeat did not charge the expedition")
                 .Call(() => { _rival = S.Campaign.RivalProgress; })
                 // Save and load at once: the death is applied once and survives the load.
                 .Call(() => SaveSystem.PathOverride = Path.Combine(Application.temporaryCachePath, "worldmvp_i7_autoplay_save.json"))
                 .Verify(() => B.Saves.Save(), () => "save after the loss refused: " + SaveSystem.LastMessage)
                 .Call(() => { AutoPlay.ContinueAfterLoad = LossAfterLoad(); B.Saves.Load(); })
                 .Wait(20f).Check(() => false, "the save did not load");
            }
            return s;
        }

        private static AutoPlayScript LossAfterLoad()
        {
            var s = new AutoPlayScript(KeeperLoss + " (after load)");
            s.Wait(1.5f)
             .Check(() => S.Campaign != null && S.Campaign.Phase == CampaignPhase.Running, "no running campaign after the load")
             .Check(() => !IsAlive(_volunteer), "the volunteer came back to life after the load")
             .Verify(() => AliveCount() == _alive - 1, () => "alive after the load " + AliveCount() + ", expected " + (_alive - 1))
             .Verify(() => S.Campaign.Resolved.Count == 1, () => "attempts recorded after the load: " + S.Campaign.Resolved.Count)
             .Check(() => CampaignApi.Attempt(S) == null, "an attempt is pending after the load")
             .Verify(() => CampaignApi.Record(S).Fallen.Count == 1, () => "fallen " + CampaignApi.Record(S).Fallen.Count)
             .Wait(1.5f)
             .Check(() => WardDuelEncounter.Current == null && CampaignDuel.RestartsSent == 0, "a duel was restarted after a resolved loss")
             .Check(() => S.Campaign.RivalProgress >= _rival, "rival progress went back")
             .Call(() => { try { File.Delete(SaveSystem.SlotPath); } catch { } SaveSystem.PathOverride = null; })
             .Log("[AutoPlay] " + KeeperLoss + " OK")
             .Quit(0);
            return s;
        }

        private static int AliveCount() { int n = 0; foreach (var c in CampaignApi.Companions(S)) if (c.Alive) n++; return n; }
        private static bool IsAlive(string id) { foreach (var c in CampaignApi.Companions(S)) if (c.Id == id) return c.Alive; return false; }

        // ------------------------------------------------------------------ duel_friendly

        private static int _wardBefore;
        private static long _tickBefore;

        private static AutoPlayScript Friendly()
        {
            var s = new AutoPlayScript(FriendlyName);
            s.Wait(1f)
             .Call(() => { _wardBefore = S.Ward.CompletedDuelsVsMira; })
             .Call(() => { var bot = WardBot.Attach(B.gameObject); bot.CastInterval = 2.2f; bot.MaxCastsPerDuel = -1; bot.FixedWard = null; bot.AutoReturn = false; bot.Passive = false; })
             .Check(() => CampaignDuel.StartRequest(CampaignDuel.Friendly(WardOpponents.FriendlyOrrin, "Orrin of Gannet Watch", "orrin")), "the friendly duel did not start")
             .Call(() => _tickBefore = S.Tick)
             .Check(() => WardDuelEncounter.Current != null && WardDuelEncounter.Current.IsFriendly, "not a friendly duel")
             .Check(() => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Setup && WardDuelEncounter.Current.StakesText == null, "a friendly duel showed stakes")
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current.Session.PlayerHistory.Count >= 1, 30f, "no cast"))
             .Wait(1.2f);
            Shot(s, "i7_friendly");
            s.Step(a => WaitFor(a, () => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 240f, "the friendly duel did not finish"))
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current.HoldOver, 5f, "no hold end"))
             .Call(() => Debug.Log("[AutoPlay] " + FriendlyName + " duel 1: " + WardDuelEncounter.Current.LastOutcome))
             .Check(() => WardDuelEncounter.Current.Resolution == null && !WardDuelEncounter.Current.ResolutionSent, "a friendly duel sent a result")
             .Check(() => WardDuelEncounter.Current.Rematch(), "no rematch in a friendly duel")
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 240f, "the rematch did not finish"))
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current.HoldOver, 5f, "no hold end"))
             .Call(() => Debug.Log("[AutoPlay] " + FriendlyName + " duel 2: " + WardDuelEncounter.Current.LastOutcome))
             .Call(() => WardDuelEncounter.Current.Return())
             .Step(a => WaitFor(a, () => !EncounterHost.Instance.IsRunning, 3f, "the duel did not end"))
             .Call(() => { var bot = B.GetComponent<WardBot>(); if (bot != null) bot.enabled = false; })
             .Check(() => S.Ward.CompletedDuelsVsMira == _wardBefore, "a friendly duel was recorded as practice")
             .Check(() => S.Tick == _tickBefore, "the village clock moved during the friendly duel")
             .Check(() => CampaignDuel.ResolvesSent == 0, "a friendly duel resolved an attempt")
             .Log("[AutoPlay] " + FriendlyName + " OK")
             .Quit(0);
            return s;
        }
    }
}
