using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Eras;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// After the handover (PRD_AGES 4.2, 8.4; package H1-4). Once the player's people are autonomous the prince stops
    /// pitching: he comes to the player and says so once, then announces each action in one short line and walks to
    /// its spot himself (the sim carries it out after the announce window unless the player casts it first), answers a
    /// tap with at most three replies at a time (what he is doing, do this first, not that, what should come first),
    /// and when the sim says the Historic chapter is ready he invites the player on ("Show me" sends
    /// <c>AdvanceChapter</c>). A short beat at the fire marks the step and then Mira's expedition may begin
    /// (<see cref="CampaignStart"/>). Everything is read from the state (so a load resumes); only <c>Advise</c> and
    /// <c>AdvanceChapter</c> are sent. Hush (Mira has the player's ear) keeps him quiet.
    /// </summary>
    public sealed partial class PrinceDirector
    {
        // ---------------- tuning (game seconds) ----------------
        public static float AnnounceWalkSpeed = 8f;
        public static float MenuTimeout = 25f, InviteTimeout = 25f;
        public static float BeatSeconds = 8f, CampaignDelay = 1.5f;
        /// <summary>False: no story trigger for Mira (tests of other things). Reset on every domain load.</summary>
        public static bool StartsCampaign = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAfterStatics() { StartsCampaign = true; }

        // ---------------- read-only state (tests, AutoPlay) ----------------
        public struct Announcement { public int ProposalId; public long Tick, ExecuteTick; public string Text; public bool Spoken; }
        public struct Act { public int ProposalId; public long Tick; public bool ByPlayer; public bool Gesture; }

        public readonly List<Announcement> Announcements = new List<Announcement>();
        public readonly List<Act> Acts = new List<Act>();
        /// <summary>Every <c>Advise</c> command this director sent.</summary>
        public readonly List<Advise> AdviceSent = new List<Advise>();
        /// <summary>The player's people are autonomous (the handover happened) and the first scene is over.</summary>
        public bool AfterHandover => _boot != null && FirstSceneStep >= SceneDone && FactionApi.IsAutonomous(S, PlayerIds.Human);
        /// <summary>0 not yet, 1 coming to the player, 2 speaking, 3 done (also after a load).</summary>
        public int HandoverStage => _hoStage;
        public bool HandoverSceneDone => _hoStage >= 3;
        public int Invitations { get; private set; }
        public int InvitationsDeclined { get; private set; }
        public bool InviteOpen => _inviteState == 2 && AskHandle != 0;
        public bool MenuOpen => _menuOpen && AskHandle != 0;
        public bool StepBeatPlayed { get; private set; }
        public bool CampaignTriggered { get; private set; }
        public string LastExplanation { get; private set; }
        /// <summary>The prince has walked out to an announced spot and not yet acted.</summary>
        public bool WalkingToAnnounced => _ann != null && !_ann.Cast && _ann.HasSpot;
        public Vector3 AnnouncedSpot => _ann != null ? _ann.Spot : Vector3.zero;

        // ---------------- internals ----------------
        private sealed class Ann
        {
            public int ProposalId; public long ExecuteTick;
            public string Text; public bool Spoken, HasSpot, Cast;
            public Vector3 Spot, Target;
        }

        private Ann _ann;
        private bool _afterEntered, _menuOpen, _adviceNote;
        private int _hoStage;
        private float _hoEnd, _actUntil, _beatUntil, _nextInvite = float.MaxValue, _campaignAt = -1f, _askAt;
        private int _inviteState; // 0 none, 1 coming, 2 asked
        private Era _eraSeen;
        private string _pendingAck;
        private Light _glow;
        private float _glowStart;
        private readonly Dictionary<Npc, Vector3> _fire = new Dictionary<Npc, Vector3>();

        private static Faction Fa(WorldState s) => s.Factions.Factions[PlayerIds.Human];

        /// <summary>True while the prince is occupied after the handover (walking out, inviting, at the fire) or a gathered villager.</summary>
        private bool AfterBusy(Npc n)
        {
            if (!AfterHandover) return false;
            if (_beatUntil > Now) return true;
            if (n != Prince) return false;
            return _hoStage is 1 or 2 || _inviteState != 0 || _ann != null || Now < _actUntil || MenuOpen;
        }

        // =====================================================================================================
        // The frame
        // =====================================================================================================

        private void UpdateAfter()
        {
            var s = S;
            if (!_afterEntered) AfterEnter();
            var era = AgesApi.Current(s);
            if (era != _eraSeen) { if (_eraSeen == Era.Prehistoric && era >= Era.Historic) BeginStepBeat(); _eraSeen = era; }
            TrackAnnouncement();
            UpdateGlow();

            if (_campaignAt >= 0f && Now >= _campaignAt && _beatUntil <= Now) TryStartCampaign();

            if (_hoStage == 0) _hoStage = 1;
            if (_hoStage == 1)
            {
                if (Hushed) return;
                if (ApproachPlayer(Prince))
                {
                    Prince.Actor?.PlayOneShot(ActorKeys.Greet);
                    float total = 0f;
                    for (int i = 0; i < PrinceLines.HandoverLines.Length; i++)
                    {
                        string l = PrinceLines.HandoverLines[i];
                        Say(Prince, l, -1f, i > 0);
                        total += Mathf.Clamp(2.2f + l.Length * 0.055f, 3f, 7.5f);
                    }
                    _hoStage = 2; _hoEnd = Now + total + 0.5f;
                }
                return;
            }
            if (_hoStage == 2)
            {
                Prince.FaceTowards(PlayerPos);
                if (Now >= _hoEnd) { _hoStage = 3; _nextInvite = Now + 3f; }
                return;
            }

            UpdateMenu();
            UpdateInvite();
            DriveAnnouncement();
            if (_beatUntil > Now) UpdateBeat();
        }

        private void AfterEnter()
        {
            _afterEntered = true;
            if (_p != null) EndPitch(Now);
            CloseAsks();
            CurrentPhase = Phase.Waiting; _resignedWho = null; Unanswered = 0;
            var s = S;
            long ho = AgesApi.HandoverTick(s, PlayerIds.Human);
            // A handover seen live gets its scene; a game loaded long after it does not replay it.
            _hoStage = ho >= 0 && s.Tick - ho <= 600 ? 0 : 3;
            if (_hoStage == 3) _nextInvite = Now + 2f;
            _eraSeen = AgesApi.Current(s);
            // A story game already in Historic with no expedition yet (a load between the step and Mira): she comes.
            if (MayStartCampaign) _campaignAt = Now + 6f;
        }

        // =====================================================================================================
        // Events
        // =====================================================================================================

        private void AfterEvent(SimEvent ev)
        {
            if (!AfterHandover) return;
            switch (ev)
            {
                case AdviceTaken t when t.Faction == PlayerIds.Human:
                    _adviceNote = true;
                    if (!string.IsNullOrEmpty(_pendingAck)) { SayAnswer(_pendingAck); _pendingAck = null; }
                    break;
                case AdviceDeclined d when d.Faction == PlayerIds.Human:
                    _pendingAck = null;
                    SayAnswer(d.Sentence);
                    break;
            }
        }

        // =====================================================================================================
        // Announcements
        // =====================================================================================================

        private void TrackAnnouncement()
        {
            var s = S;
            var a = FactionApi.Announced(s, PlayerIds.Human);
            long exec = Fa(s).AnnounceExecuteTick;
            if (a == null)
            {
                if (_ann != null && !_ann.Cast) { _ann = null; if (Prince.IsWalking) Prince.StopWalking(); Prince.PointAt(null); }
                return;
            }
            if (_ann != null && _ann.ProposalId == a.Id && _ann.ExecuteTick == exec) return;
            var v = VoiceOf(Prince);
            string text = v.Pick("announce/" + KindOf(a), Args(a)) ?? "I'm starting " + FactionApi.ProposalTitle(s, a.Id).ToLowerInvariant() + ".";
            if (_adviceNote)
                text = v.Pick("advised") + " " + LowerFirst(text);
            _adviceNote = false;
            _ann = new Ann { ProposalId = a.Id, ExecuteTick = exec, Text = text };
            if (FactionApi.ProposalTarget(s, a.Id, out double x, out double z, out _))
            {
                _ann.Target = new Vector3((float)x, 0, (float)z);
                _ann.Target.y = PlayerWalker.GroundHeight(_ann.Target);
                _ann.Spot = StandFor(_ann.Target, Prince.transform.position);
                _ann.HasSpot = true;
            }
            Announcements.Add(new Announcement { ProposalId = a.Id, Tick = s.Tick, ExecuteTick = exec, Text = text });
        }

        private void DriveAnnouncement()
        {
            if (_ann == null) return;
            if (_ann.Cast)
            {
                if (Now >= _actUntil) { Prince.PointAt(null); _ann = null; }
                return;
            }
            if (_inviteState != 0 || MenuOpen) return;
            if (!_ann.Spoken)
            {
                _ann.Spoken = true;
                if (!Hushed)
                {
                    Say(Prince, _ann.Text);
                    var list = Announcements;
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i].ProposalId == _ann.ProposalId) { var x = list[i]; x.Spoken = true; list[i] = x; break; }
                }
            }
            if (!_ann.HasSpot) return;
            float d = BoardSpace.Flat(Prince.transform.position - _ann.Spot);
            if (d > 1.2f)
            {
                if (!Prince.IsWalking || !Prince.Goal.HasValue || BoardSpace.Flat(Prince.Goal.Value - _ann.Spot) > 2f)
                {
                    Prince.WalkSpeed = AnnounceWalkSpeed; Prince.PointAt(null); Prince.WalkTo(_ann.Spot);
                }
            }
            else
            {
                if (Prince.IsWalking) Prince.StopWalking();
                Prince.FaceTowards(_ann.Target);
                Prince.PointAt(_ann.Target);
            }
        }

        /// <summary>A cast of the player's people after the handover: the player's press, or the prince's own action.</summary>
        private void AfterCast(SpellCast e)
        {
            var s = S;
            bool bySim = Fa(s).LastAutoCastTick == e.Tick;
            bool mine = _ann != null && _ann.ProposalId == e.ProposalId;
            Vector3 target = mine ? _ann.Target : Prince.transform.position;
            if (!mine && FactionApi.ProposalTarget(s, e.ProposalId, out double x, out double z, out _))
            { target = new Vector3((float)x, 0, (float)z); target.y = PlayerWalker.GroundHeight(target); }
            Acts.Add(new Act { ProposalId = e.ProposalId, Tick = e.Tick, ByPlayer = !bySim, Gesture = bySim });
            if (mine) { _ann.Cast = true; _actUntil = Now + 2.5f; }
            else _actUntil = Mathf.Max(_actUntil, Now + 2.5f);
            if (Prince.IsWalking && mine) Prince.StopWalking();
            if (bySim)
            {
                // His own action: a visible gesture at the spot.
                Prince.FaceTowards(target);
                Prince.PointAt(target);
                Prince.Actor?.PlayOneShot(ActorKeys.Cast);
                SpellEffect.Play(target, true);
            }
            else
            {
                Prince.PointAt(null);
                Prince.FaceTowards(PlayerPos);
                Prince.Actor?.PlayOneShot(ActorKeys.Cheer);
                if (!Hushed && BoardSpace.Flat(Prince.transform.position - PlayerPos) < 40f) Say(Prince, VoiceOf(Prince).Pick("cast"));
            }
        }

        // =====================================================================================================
        // Walking up to the player
        // =====================================================================================================

        private bool ApproachPlayer(Npc who)
        {
            var w = Walker;
            if (w == null) return true;
            var me = PlayerPos;
            float d = BoardSpace.Flat(who.transform.position - me);
            var spot = PitchSpot(who);
            bool inView = Vector3.Angle(Flat(w.transform.forward), Flat(who.transform.position - me)) <= 40f;
            bool onSpot = !who.IsWalking && BoardSpace.Flat(who.transform.position - spot) < 3f;
            if ((d <= PitchDistance + 2.5f && inView) || (onSpot && d <= NearDistance))
            {
                who.StopWalking();
                who.FaceTowards(me);
                return true;
            }
            who.WalkSpeed = HurrySpeed;
            if (!who.IsWalking || !who.Goal.HasValue || BoardSpace.Flat(who.Goal.Value - spot) > 4f) who.WalkTo(spot);
            return false;
        }

        // =====================================================================================================
        // Questions (menus and the invitation)
        // =====================================================================================================

        private void AskAfter(string text, IList<string> replies, Action<int> onReply)
        {
            CloseAsks();
            // Whatever he was still saying gives way: the question shows at once.
            var drop = new List<int>();
            foreach (var b in SpeechBubbles.All) if (ReferenceEquals(b.Who, Prince)) drop.Add(b.Handle);
            foreach (int h in drop) SpeechBubbles.Close(h);
            _ask = SpeechBubbles.Ask(Prince, text, replies, onReply, 0f);
            Record(Prince, text, true);
            _askAt = Now;
            Prince.FaceTowards(PlayerPos);
            Prince.Actor?.PlayOneShot(ActorKeys.Greet);
        }

        private void SayAnswer(string text, bool queue = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            VoiceOf(Prince).Said(null); // an answer may repeat itself
            Say(Prince, text, -1f, queue);
        }

        private void UpdateMenu()
        {
            if (!_menuOpen) return;
            if (AskHandle == 0) { _menuOpen = false; return; }
            Prince.FaceTowards(PlayerPos);
            if (Prince.IsWalking) Prince.StopWalking();
            if (Now - _askAt >= MenuTimeout) { SpeechBubbles.Close(_ask); _ask = 0; _menuOpen = false; }
        }

        private sealed class Item { public string Label; public Action Do; }

        private void ShowMenu(string text, List<Item> items, int start)
        {
            _menuOpen = true;
            int remaining = items.Count - start;
            var labels = new List<string>();
            var shown = new List<Item>();
            if (remaining <= 3) for (int i = start; i < items.Count; i++) { labels.Add(items[i].Label); shown.Add(items[i]); }
            else
            {
                labels.Add(items[start].Label); shown.Add(items[start]);
                labels.Add(items[start + 1].Label); shown.Add(items[start + 1]);
                labels.Add(PrinceLines.More); shown.Add(null);
            }
            AskAfter(text, labels, i =>
            {
                _ask = 0;
                if (i < 0 || i >= shown.Count) { _menuOpen = false; return; }
                if (shown[i] == null) { ShowMenu(text, items, start + 2); return; }
                _menuOpen = false;
                shown[i].Do();
            });
        }

        private void AfterTapped()
        {
            if (_ask != 0 && SpeechBubbles.IsOpen(_ask)) { SpeechBubbles.Expand(_ask); return; }
            var items = new List<Item>
            {
                new Item { Label = PrinceLines.WhatDoing, Do = AnswerExplain },
                new Item { Label = PrinceLines.DoFirst, Do = () => SendProposalAdvice(AdviceKind.Favour) },
                new Item { Label = PrinceLines.NotThat, Do = () => SendProposalAdvice(AdviceKind.Veto) },
                new Item { Label = PrinceLines.WhatFirst, Do = OpenPriorities },
            };
            ShowMenu(VoiceOf(Prince).Pick("ask.menu"), items, 0);
        }

        private void AnswerExplain()
        {
            var x = FactionApi.Explain(S, PlayerIds.Human);
            LastExplanation = x.Doing;
            SayAnswer(x.Doing);
            if (!string.IsNullOrEmpty(x.Why)) SayAnswer(x.Why, true);
            if (!string.IsNullOrEmpty(x.Blocked)) SayAnswer(x.Blocked, true);
        }

        private int AdviceTarget()
        {
            var a = FactionApi.Announced(S, PlayerIds.Human);
            if (a != null) return a.Id;
            var t = FactionApi.Top(S, PlayerIds.Human);
            return t != null ? t.Id : -1;
        }

        private void SendProposalAdvice(AdviceKind kind)
        {
            int id = AdviceTarget();
            if (id < 0) { SayAnswer(VoiceOf(Prince).Pick("nothing")); return; }
            Send(new Advise { Faction = PlayerIds.Human, Kind = kind, ProposalId = id },
                kind == AdviceKind.Favour ? "Then that goes first." : "Then not that one.");
        }

        private void Send(Advise a, string ack)
        {
            AdviceSent.Add(a);
            _pendingAck = ack;
            var r = _boot.Sim.Submit(a);
            if (!r.Ok) { _pendingAck = null; SayAnswer(string.IsNullOrEmpty(r.Message) ? "I can't do that now." : r.Message); }
        }

        private static readonly string[] PriorityLabels = { "Economy", "Growing out", "Soldiers", "Defence", "Trade" };
        private static readonly string[] PriorityWords = { "the economy", "growing outward", "soldiers", "defence", "trade" };

        private void OpenPriorities()
        {
            var items = new List<Item>();
            foreach (PriorityClass pc in Enum.GetValues(typeof(PriorityClass)))
            {
                var c = pc;
                int i = (int)c;
                if (i < 0 || i >= PriorityLabels.Length) continue;
                items.Add(new Item
                {
                    Label = PriorityLabels[i],
                    Do = () => Send(new Advise { Faction = PlayerIds.Human, Kind = AdviceKind.Priority, Priority = c }, "Then " + PriorityWords[i] + " comes first."),
                });
            }
            items.Add(new Item { Label = "A ware", Do = OpenWares });
            ShowMenu("What first?", items, 0);
        }

        private void OpenWares()
        {
            var items = new List<Item>();
            foreach (var w in Wares.All)
            {
                var ware = w;
                string name = Wares.DisplayName(ware);
                items.Add(new Item
                {
                    Label = char.ToUpperInvariant(name[0]) + name.Substring(1),
                    Do = () => Send(new Advise { Faction = PlayerIds.Human, Kind = AdviceKind.FocusWare, Ware = (int)ware }, "Then I'll work towards " + name.ToLowerInvariant() + "."),
                });
            }
            ShowMenu("Which ware?", items, 0);
        }

        // =====================================================================================================
        // The invitation
        // =====================================================================================================

        private bool InviteWanted => AgesApi.Current(S) == Era.Prehistoric && AgesApi.IsReady(S, ChapterIds.Historic);

        private float InviteRepeat
        {
            get
            {
                int t = S.Eras?.Tuning != null ? S.Eras.Tuning.InviteRepeatTicks : 1800;
                return t / (float)WorldSim.TicksPerSecond;
            }
        }

        private void UpdateInvite()
        {
            if (_inviteState == 0)
            {
                if (InviteWanted && Now >= _nextInvite && !Hushed && !MenuOpen) _inviteState = 1;
                return;
            }
            if (!InviteWanted) { if (_ask != 0 && !_menuOpen) { SpeechBubbles.Close(_ask); _ask = 0; } _inviteState = 0; return; }
            if (_inviteState == 1)
            {
                if (Hushed) { _inviteState = 0; _nextInvite = Now + 5f; return; }
                if (ApproachPlayer(Prince))
                {
                    bool again = Invitations > 0;
                    Invitations++;
                    string text = VoiceOf(Prince).Pick(again ? "invite.again" : "invite");
                    AskAfter(text, new[] { PrinceLines.ShowMe, PrinceLines.NotYet, PrinceLines.WhatChanges }, OnInviteReply);
                    _inviteState = 2;
                }
                return;
            }
            // asked
            if (AskHandle == 0 || Now - _askAt >= InviteTimeout)
            {
                if (AskHandle != 0) { SpeechBubbles.Close(_ask); _ask = 0; }
                Declined();
                return;
            }
            Prince.FaceTowards(PlayerPos);
        }

        private void Declined()
        {
            _inviteState = 0; InvitationsDeclined++;
            _nextInvite = Now + InviteRepeat;
        }

        private void OnInviteReply(int i)
        {
            _ask = 0;
            if (i < 0) return; // closed: UpdateInvite counts it
            switch (i)
            {
                case 0: AcceptInvite(); break;
                case 1: Declined(); break;
                default:
                    AskAfter(PrinceLines.InviteWhat, new[] { PrinceLines.ShowMe, PrinceLines.NotYet }, j =>
                    {
                        _ask = 0;
                        if (j < 0) return;
                        if (j == 0) AcceptInvite(); else Declined();
                    });
                    break;
            }
        }

        private void AcceptInvite()
        {
            _inviteState = 0;
            var r = _boot.Sim.Submit(new AdvanceChapter { To = ChapterIds.Historic });
            if (!r.Ok)
            {
                SayAnswer(string.IsNullOrEmpty(r.Message) ? "Not yet, it seems." : r.Message);
                _nextInvite = Now + 30f;
            }
        }

        // =====================================================================================================
        // The step: a beat at the fire, then Mira
        // =====================================================================================================

        private void BeginStepBeat()
        {
            StepBeatPlayed = true;
            _beatUntil = Now + BeatSeconds;
            _glowStart = Now;
            var go = new GameObject("StepGlow");
            go.transform.SetParent(transform, false);
            go.transform.position = _camp.Hearth + Vector3.up * 3f;
            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point; _glow.color = new Color(1f, 0.72f, 0.38f); _glow.range = 45f; _glow.intensity = 0f;
            if (_ask != 0) { SpeechBubbles.Close(_ask); _ask = 0; _menuOpen = false; }
            _inviteState = 0;
            _ann = null;
            Say(Prince, VoiceOf(Prince).Pick("step"));
            Prince.Actor?.PlayOneShot(ActorKeys.Cheer);
            int k = 0;
            foreach (var n in new[] { Prince, Sawyer, Merchant })
            {
                float ang = (k++ * 120f + 30f) * Mathf.Deg2Rad;
                var p = _camp.Hearth + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 4f;
                var c = WalkLattice.ToCell(p);
                if (WalkGrid.IsBlocked(c)) { var f = WalkGrid.NearestFree(c, 3); if (f.HasValue) p = WalkLattice.ToWorld(f.Value); }
                _fire[n] = p;
                n.WalkSpeed = HurrySpeed; n.PointAt(null); n.WalkTo(p);
            }
            if (MayStartCampaign) _campaignAt = Now + BeatSeconds + CampaignDelay;
        }

        private void UpdateBeat()
        {
            foreach (var kv in _fire)
                if (kv.Key != null && !kv.Key.IsWalking && BoardSpace.Flat(kv.Key.transform.position - kv.Value) < 1.5f) kv.Key.FaceTowards(_camp.Hearth);
        }

        private void UpdateGlow()
        {
            if (_glow == null) return;
            float t = (Now - _glowStart) / BeatSeconds;
            if (t >= 1f) { Destroy(_glow.gameObject); _glow = null; return; }
            _glow.intensity = 6f * Mathf.Sin(Mathf.PI * t);
        }

        private bool MayStartCampaign => StartsCampaign && AgesApi.CampaignMayStart(S) && S.Campaign == null && (S.Ruleset == Ruleset.GodBoard || AgesApi.Mode(S) == World.Sim.Eras.PlayMode.Skirmish);

        private void TryStartCampaign()
        {
            if (!MayStartCampaign) { _campaignAt = -1f; return; }
            if (Hushed) { _campaignAt = Now + 3f; return; }
            _campaignAt = -1f;
            CampaignTriggered = CampaignStart.StartNow(CampaignStart.DefaultPack);
            if (!CampaignTriggered) _campaignAt = Now + 20f; // try again (a pack read or refusal)
        }

        // =====================================================================================================
        // HUD
        // =====================================================================================================

        private string AfterHudLine()
        {
            var s = S;
            var o = FactionApi.Outcome(s);
            if (o.GameOver) return o.Sentence;
            if (_ann != null && !_ann.Cast)
            {
                long left = Math.Max(0, _ann.ExecuteTick - s.Tick);
                return Prince.DisplayName + " will: " + FactionApi.ProposalTitle(s, _ann.ProposalId) + " (" + (left + 9) / 10 + " s)";
            }
            if (InviteWanted) return Prince.DisplayName + " is ready to show you the next age.";
            return Prince.DisplayName + ": the people are building on their own.";
        }
    }
}
