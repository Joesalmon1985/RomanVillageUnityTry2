using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    // Mira's conversation (package I6): lines from the pack one at a time, her questions with at most three replies,
    // the opening, leads, calls, choices, thresholds, the tower, leaving and rejoining, the cues while away, the duel
    // stand-in, and keeping out of the prince's way.
    public sealed partial class ExpeditionDirector
    {
        // ---------------- lines, one at a time ----------------

        private sealed class Spoken
        {
            public string Speaker, Text, LineId;
            public Action OnShow;
        }

        private readonly List<Spoken> _script = new List<Spoken>();
        private int _scriptHandle;
        private readonly HashSet<string> _saidIds = new HashSet<string>();
        /// <summary>Lines waiting to be said (tests).</summary>
        public int ScriptPending => _script.Count + (_scriptHandle != 0 && SpeechBubbles.IsOpen(_scriptHandle) ? 1 : 0);

        /// <summary>Bubbles keep game time: at a faster clock they close sooner, so a fast run talks at the same game pace.</summary>
        private static float Real(float gameSeconds) => gameSeconds / Mathf.Max(1f, Time.timeScale);

        /// <summary>Queues a pack line (or a stand-in for a line the pack lacks). Returns false when it may not be said now.</summary>
        private bool SayLine(string lineId, string speakerIfMissing = "mira", Action onShow = null)
        {
            var text = ExpeditionLines.Get(S, lineId);
            if (string.IsNullOrEmpty(text)) return false;
            string speaker = ExpeditionLines.InPack(S, lineId) ? ExpeditionLines.SpeakerOf(S, lineId) : speakerIfMissing;
            _script.Add(new Spoken { Speaker = speaker, Text = text, LineId = lineId, OnShow = onShow });
            _saidIds.Add(lineId);
            return true;
        }

        private void SayText(string speaker, string text, Action onShow = null)
        {
            if (!string.IsNullOrEmpty(text)) _script.Add(new Spoken { Speaker = speaker, Text = text, OnShow = onShow });
        }

        /// <summary>The body that says a line: Mira, a living companion who is with the party, a camp villager, a keeper. Null = nobody here can say it.</summary>
        public Npc SpeakerNpc(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            if (speaker == "mira") return Mira;
            var m = MateOf(speaker);
            if (m != null) return m.Alive && !m.Out ? m.Npc : null;
            var role = D.Role(speaker);
            string actor = role != null && !string.IsNullOrEmpty(role.ActorId) ? role.ActorId : speaker;
            var pd = PrinceDirector.Instance;
            if (pd != null)
            {
                if (actor == NpcIds.Prince) return pd.Prince;
                if (actor == NpcIds.Sawyer) return pd.Sawyer;
                if (actor == NpcIds.Merchant) return pd.Merchant;
            }
            var keeper = Sites != null ? Sites.Person(actor) : null;
            return keeper != null ? keeper : Npc.Find(actor);
        }

        /// <summary>The space a speaker's body is in.</summary>
        private string SpaceOfSpeaker(Npc n)
        {
            if (n == Mira) return MiraSpace;
            foreach (var m in _mates) if (m.Npc == n) return m.Space;
            return Sites != null ? Sites.SpaceOfPerson(n) : "";
        }

        private bool Hears(Npc n)
        {
            if (n == null || Walker == null || SpaceOfSpeaker(n) != PlayerSpace) return false;
            // A viewpoint led with the party is with the party, wherever the last frame left it.
            return BoardSpace.Flat(n.transform.position - Walker.Position) <= 75f;
        }

        private void Record(string speaker, string text) => _said.Add(new Line { Tick = S.Tick, Speaker = speaker ?? "", Text = text ?? "" });

        private void PumpScript()
        {
            if (_scriptHandle != 0 && SpeechBubbles.IsOpen(_scriptHandle)) return;
            _scriptHandle = 0;
            // Out of earshot: the journal keeps what happened (and nothing waits behind a line nobody can hear).
            while (_script.Count > 0 && !Hears(SpeakerNpc(_script[0].Speaker))) _script.RemoveAt(0);
            if (_script.Count == 0 || PrinceBusy()) return;
            while (_script.Count > 0)
            {
                var line = _script[0];
                _script.RemoveAt(0);
                var npc = SpeakerNpc(line.Speaker);
                if (!Hears(npc)) continue;
                _scriptHandle = SpeechBubbles.Say(npc, line.Text, Real(ExpeditionLines.Seconds(line.Text)));
                Record(line.Speaker, line.Text);
                if (npc != Mira && Walker != null) npc.FaceTowards(Walker.Position);
                line.OnShow?.Invoke();
                return;
            }
        }

        private void ClearScript()
        {
            _script.Clear();
            if (_scriptHandle != 0) SpeechBubbles.Close(_scriptHandle);
            _scriptHandle = 0;
        }

        // ---------------- her question (one at a time) ----------------

        public const string AskOpening = "opening", AskLead = "lead", AskChoice = "choice", AskThreshold = "threshold",
            AskTower = "tower", AskMenu = "menu", AskCall = "call";

        private int _ask;
        private string _askKind;
        private int _askLead = -1;
        /// <summary>Mira's open question bubble (0 if none).</summary>
        public int AskHandle => _ask != 0 && SpeechBubbles.IsOpen(_ask) ? _ask : 0;
        /// <summary>What the open question is about (one of the Ask constants), or null.</summary>
        public string AskKind => AskHandle != 0 ? _askKind : null;
        /// <summary>Questions asked since this director started, by kind (tests).</summary>
        public readonly Dictionary<string, int> Asked = new Dictionary<string, int>();

        private void OpenAsk(string kind, ISpeaker who, string text, IList<string> replies, Action<int> onReply)
        {
            CloseAsk();
            _askKind = kind;
            int handle = 0;
            handle = SpeechBubbles.Ask(who, text, replies, i =>
            {
                if (_ask == handle) { _ask = 0; _askKind = null; }
                if (i >= 0) onReply(i);
            }, 0f);
            _ask = handle;
            Asked[kind] = Asked.TryGetValue(kind, out int n) ? n + 1 : 1;
            Record(who == (ISpeaker)Mira ? "mira" : "", text);
        }

        private void CloseAsk()
        {
            if (_ask != 0) SpeechBubbles.Close(_ask);
            _ask = 0; _askKind = null;
        }

        private string Reply(string id) => ExpeditionLines.Get(S, id, id);

        // ---------------- the prince and Mira, politely ----------------

        /// <summary>
        /// True while the prince's people have the player's ear: a question of theirs is open, one of them is speaking
        /// near the player, or the player is being led to a spot. Mira then says nothing new and closes her own question
        /// until they are done (she does not pester; her patience in the sim runs on).
        /// </summary>
        public bool PrinceBusy()
        {
            var pd = PrinceDirector.Instance;
            if (pd == null) return false;
            if (pd.AskHandle != 0) return true;
            if (pd.CurrentPhase == PrinceDirector.Phase.Leading || pd.CurrentPhase == PrinceDirector.Phase.AtSpot || pd.CurrentPhase == PrinceDirector.Phase.Ending) return true;
            if (Walker == null) return false;
            foreach (var n in new[] { pd.Prince, pd.Sawyer, pd.Merchant })
                if (n != null && n.HasShownBubble() && !IsOurBubble(n) && BoardSpace.Flat(n.transform.position - Walker.Position) < 40f) return true;
            return false;
        }

        /// <summary>A camp villager speaking one of the expedition's own lines (the sawyer's notches) is not the prince's business.</summary>
        private bool IsOurBubble(Npc n)
        {
            if (_scriptHandle == 0 || !SpeechBubbles.IsOpen(_scriptHandle)) return false;
            var b = SpeechBubbles.Find(_scriptHandle);
            return b != null && ReferenceEquals(b.Who, n);
        }

        /// <summary>
        /// For the prince's director (one line there): true while Mira has the player's ear, so he holds new pitches
        /// that are not urgent. Set as <c>PrinceDirector.Hush</c> by the installer.
        /// </summary>
        public bool MiraHasTheFloor
        {
            get
            {
                if (!_begun || C == null || C.Phase != CampaignPhase.Running) return false;
                if (_retreat != null) return true;
                if (!C.Party.PlayerPresent) return false;
                if (AskHandle != 0 || ScriptPending > 0) return true;
                // In her company on the road or at a site (not idling at the camp between leads).
                return PlayerNear(AwayDistance) && (C.Party.Mode != PartyMode.Idle || SceneStep < SceneUnderWay || !AtHome());
            }
        }

        private bool AtHome()
        {
            var loc = D.Location(C.Party.At.Location);
            return loc != null && loc.Kind == LocationKind.Camp && !C.Party.At.IsInterior;
        }

        // ---------------- company: present, away, following ----------------

        private bool _following;
        private float _awaySince = -1f, _lastCall = -100f;
        private string _prevMiraSpace = "";
        private bool _wasNearBeforeLink;
        private int _lastSetOutLead = -1, _acceptedLead = -1, _declinedLead = -1, _whyAsked = -1;
        /// <summary>Times Mira called the lagging player (tests).</summary>
        public int Calls { get; private set; }

        /// <summary>True when the player is in Mira's space and within <paramref name="metres"/> of her.</summary>
        public bool PlayerNear(float metres) =>
            Walker != null && PlayerSpace == MiraSpace && BoardSpace.Flat(Walker.Position - Mira.transform.position) <= metres;

        private void UpdateCompany()
        {
            var p = C.Party;
            if (Walker == null) return;
            bool present = p.PlayerPresent;

            // Through a stair with her.
            if (MiraSpace != _prevMiraSpace)
            {
                bool wasNear = _wasNearBeforeLink;
                _prevMiraSpace = MiraSpace;
                if (present && wasNear)
                {
                    if (_leg != null && (Interiors.Current ?? "") != MiraSpace) Interiors.Enter(_leg.To);
                }
            }
            _wasNearBeforeLink = PlayerNear(45f);
            if (_following && present && PlayerSpace == MiraSpace && !ReferenceEquals(Walker.Following, Mira) && _enteredSpace != MiraSpace)
            {
                // Arrived in her interior: take up the file again.
                _enteredSpace = MiraSpace;
                Walker.Follow(Mira);
            }

            if (C.Phase != CampaignPhase.Running || SceneStep < SceneUnderWay || _retreat != null) { _awaySince = -1f; return; }
            if (!present) { _awaySince = -1f; return; }

            // Walking off with the prince is "away" for Mira (PRD 5.9).
            var pd = PrinceDirector.Instance;
            if (pd != null && (pd.CurrentPhase == PrinceDirector.Phase.Leading || pd.CurrentPhase == PrinceDirector.Phase.AtSpot) && !PlayerNear(AnswerDistance))
            {
                LeaveCompany(false);
                return;
            }
            if (PlayerNear(AwayDistance)) { _awaySince = -1f; return; }
            if (_awaySince < 0f) { _awaySince = Now; return; }
            if (Now - _awaySince < AwaySeconds) return;
            LeaveCompany(false);
            // About the player, now: ahead of any other word that is waiting.
            string fell = ExpeditionLines.Get(S, ExpeditionLines.CueFellBehind);
            if (!string.IsNullOrEmpty(fell)) _cueQueue.Insert(0, fell);
        }

        private string _enteredSpace = "";

        private void LeaveCompany(bool said)
        {
            if (C.Party.PlayerPresent) Send(new SetCompany { Present = false });
            _following = false; _awaySince = -1f;
            _rejoinHintDue = true;
            if (Walker != null && ReferenceEquals(Walker.Following, Mira)) Walker.StopAuto();
            if (_askKind != AskMenu) CloseAsk();
            if (said) SayLine(ExpeditionLines.MiraLeave);
        }

        private void JoinCompany(bool said)
        {
            if (!C.Party.PlayerPresent) Send(new SetCompany { Present = true });
            _awaySince = -1f;
            if (said) SayLine(ExpeditionLines.MiraRejoin);
        }

        private void FollowMira()
        {
            _following = true;
            if (Walker != null && PlayerSpace == MiraSpace) Walker.Follow(Mira);
        }

        // ---------------- the conversation loop ----------------

        private bool _choiceAsked, _ifLoseAsked;
        private float _towerAskAfter, _thresholdAskAfter, _leadAskAfter;
        private string _earlyLine;

        private void UpdateTalk()
        {
            PumpScript();
            if (_ask != 0 && !SpeechBubbles.IsOpen(_ask)) { _ask = 0; _askKind = null; }
            var c = C; var p = c.Party;
            if (c.Phase != CampaignPhase.Running) { if (_askKind != AskMenu) CloseAsk(); return; }
            UpdatePrinceNews();
            if (SceneStep < SceneUnderWay) { UpdateOpening(); return; }

            bool near = PlayerNear(AskDistance);
            bool prince = PrinceBusy();
            if (_ask != 0)
            {
                bool own = _askKind == AskMenu;
                if (!own && !AskStillFits()) CloseAsk();
                else if (!own && prince) CloseAsk();                       // she waits her turn and asks again after
                else if (!own && _askKind != AskCall && !near) CloseAsk();  // the player walked off
                else if (_askKind == AskCall && (PlayerNear(12f) || ReferenceEquals(Walker != null ? Walker.Following : null, Mira) || p.Mode != PartyMode.Travelling)) CloseAsk();
                if (_ask != 0) return;
            }
            if (ScriptPending > 0 || prince || _retreat != null || Walker == null) return;
            if (!p.PlayerPresent) return; // away: she does not pester; a tap on her offers "I'm with you"

            switch (p.Mode)
            {
                case PartyMode.Idle:
                    if (near) OfferLead();
                    break;
                case PartyMode.WaitingForPlayer:
                    if (!near) break;
                    if (TowerCrystalHere(out var el)) { if (Now >= _towerAskAfter) AskTowerQuestion(el); }
                    else OfferLead();
                    break;
                case PartyMode.Travelling:
                    CallIfLagging();
                    break;
                case PartyMode.AskingChoice:
                    if (near && !_choiceAsked) AskChoiceQuestion();
                    break;
                case PartyMode.AtThreshold:
                    if (near && Now >= _thresholdAskAfter) AskThresholdQuestion();
                    break;
            }
        }

        private bool AskStillFits()
        {
            var p = C.Party;
            switch (_askKind)
            {
                case AskOpening: return SceneStep < SceneUnderWay;
                case AskLead:
                {
                    if (p.Mode != PartyMode.Idle && p.Mode != PartyMode.WaitingForPlayer) return false;
                    var top = CampaignApi.TopLead(S);
                    return top != null && top.Id == _askLead;
                }
                case AskChoice: return p.Mode == PartyMode.AskingChoice;
                case AskThreshold: return p.Mode == PartyMode.AtThreshold;
                case AskTower: return p.Mode == PartyMode.WaitingForPlayer && TowerCrystalHere(out _);
                case AskCall: return p.Mode == PartyMode.Travelling;
                default: return true;
            }
        }

        // ---- the opening (PRD 4.3) ----

        private int _openingStep;
        private float _openingSince;
        private bool _moreAsked;
        private static readonly string[] OpeningStory = { "mira_open_1", "mira_open_2", "mira_open_3", "mira_open_4" };
        private static readonly string[] OpeningFixed =
            { "mira_open_1", "mira_open_2", "mira_open_3", "mira_open_4", "mira_open_six", "mira_open_cost", "mira_open_practice", "mira_open_ask", "mira_open_more", "mira_open_more_2" };

        private void UpdateOpening()
        {
            var p = C.Party;
            // She went on by herself (nobody answered in time), or the player is gone: no opening left to play.
            if (p.Mode != PartyMode.Idle || !p.PlayerPresent) { EndOpening(); return; }
            if (Walker == null) return;
            if (PrinceBusy()) { if (_askKind == AskOpening) CloseAsk(); return; } // she waits her turn and asks again after
            switch (_openingStep)
            {
                case 0:
                    if (S.Tick - C.StartTick < OpeningStartTicks) return;
                    if (_sceneRestored && SceneStep == SceneOpening) { _openingStep = 3; return; } // a load mid-opening: the question again
                    SceneStep = SceneOpening;
                    _openingStep = 1; _openingSince = Now;
                    Debug.Log("[Expedition] opening: Mira comes to the player at tick " + S.Tick);
                    // To a point in front of the player, on Mira's side of them.
                    var to = Mira.transform.position - Walker.Position; to.y = 0;
                    if (to.magnitude > TalkDistance + 1f) _approach = ExpeditionSpace.Ground(Walker.Position + to.normalized * TalkDistance);
                    return;
                case 1:
                    if (_approach.HasValue && BoardSpace.Flat(Mira.transform.position - _approach.Value) > 1.2f && Now - _openingSince < 12f) return;
                    Mira.FaceTowards(Walker.Position);
                    Mira.Actor?.PlayOneShot(ActorKeys.Wave);
                    Debug.Log("[Expedition] opening: the story begins at tick " + S.Tick + ", Mira " + BoardSpace.Flat(Mira.transform.position - Walker.Position).ToString("0.0") + " m from the player");
                    foreach (var id in OpeningStory) SayLine(id);
                    // The routes' own opening lines (the Earth pack's: where the Deepstone lies).
                    foreach (var l in D.Lines)
                        if (l.Speaker == "mira" && l.Topic == "opening" && Array.IndexOf(OpeningFixed, l.Id) < 0) SayLine(l.Id);
                    SayLine("mira_open_six", "mira", () =>
                    {
                        Mira.PointAt(_hasStand ? _stand : (Vector3?)null);
                        foreach (var m in _mates) if (m.Alive) m.Npc.Actor?.PlayOneShot(ActorKeys.Wave);
                    });
                    SayLine("mira_open_cost", "mira", () => Mira.PointAt(null));
                    SayLine("mira_open_practice");
                    _openingStep = 2;
                    return;
                case 2:
                    if (ScriptPending > 0) return;
                    _openingStep = 3;
                    return;
                default:
                    if (_ask != 0 || ScriptPending > 0) return;
                    Mira.PointAt(null);
                    AskOpeningQuestion();
                    return;
            }
        }

        private void AskOpeningQuestion()
        {
            var replies = new List<string> { Reply(ExpeditionLines.ReplyLead) };
            if (!_moreAsked) replies.Add(Reply(ExpeditionLines.ReplyMore));
            replies.Add(Reply(ExpeditionLines.ReplyAhead));
            bool more = !_moreAsked;
            OpenAsk(AskOpening, Mira, ExpeditionLines.Get(S, "mira_open_ask", "Come with me?"), replies, i =>
            {
                if (i == 0)
                {
                    EndOpening();
                    var top = CampaignApi.TopLead(S);
                    if (top != null) AcceptLead(top.Id);
                }
                else if (more && i == 1)
                {
                    _moreAsked = true;
                    SayLine("mira_open_more"); SayLine("mira_open_more_2");
                }
                else
                {
                    EndOpening();
                    LeaveCompany(true);
                }
            });
        }

        private void EndOpening()
        {
            if (SceneStep >= SceneUnderWay) return;
            SceneStep = SceneUnderWay;
            _approach = null;
            Mira.PointAt(null);
            if (_askKind == AskOpening) CloseAsk();
        }

        // ---- leads ----

        private void OfferLead()
        {
            var top = CampaignApi.TopLead(S);
            if (top == null || top.Id == _declinedLead || Now < _leadAskAfter) return;
            if (top.Id == _acceptedLead && Now - _acceptedAt < 3f) return; // accepted: the sim sets out on its next tick
            _askLead = top.Id;
            var replies = new List<string> { Reply(ExpeditionLines.ReplyLead) };
            bool why = _whyAsked != top.Id;
            if (why) replies.Add(Reply(ExpeditionLines.ReplyWhy));
            replies.Add(Reply(ExpeditionLines.ReplyNot));
            int id = top.Id; string defId = top.DefId; string crystal = top.Crystal;
            OpenAsk(AskLead, Mira, ExpeditionLines.Sentence(string.IsNullOrEmpty(top.Reason) ? top.Title : top.Reason), replies, i =>
            {
                if (i == 0) AcceptLead(id);
                else if (why && i == 1)
                {
                    _whyAsked = id;
                    // The clue behind it: the lead's own "why" line, else what the party believes of its crystal.
                    if (!SayLine("mira_why_" + defId) && !SayLine("mira_where_" + crystal)) SayLine(ExpeditionLines.MiraWhyGeneric);
                }
                else
                {
                    _declinedLead = id;
                    Send(new DeclineLead { LeadId = id });
                    SayLine(ExpeditionLines.MiraDeclined);
                }
            });
        }

        private void AcceptLead(int leadId)
        {
            if (!C.Party.PlayerPresent) Send(new SetCompany { Present = true });
            _acceptedLead = leadId; _acceptedAt = Now;
            var r = Send(new AcceptLead { LeadId = leadId });
            if (!r.Ok) { SayText("mira", r.Message); return; }
            FollowMira();
        }

        private void OnSetOutTalk(PartySetOut so)
        {
            _declinedLead = -1;
            if (SceneStep < SceneUnderWay) { EndOpening(); ClearScript(); }
            if (_askKind == AskLead || _askKind == AskTower) CloseAsk();
            var p = C.Party;
            bool hers = so.LeadId >= 0 && so.LeadId != _acceptedLead && p.Purpose == RoutePurpose.Lead;
            if (hers && p.PlayerPresent && PlayerNear(AskDistance) && _goOnSaidFor != so.LeadId)
            {
                // Not answered in time: she goes, and says so (5.5).
                _goOnSaidFor = so.LeadId;
                SayLine(ExpeditionLines.MiraGoOn);
            }
        }

        private int _goOnSaidFor = -1;
        private float _acceptedAt = -100f;

        private void CallIfLagging()
        {
            if (Walker == null || PlayerSpace != MiraSpace) return;
            float dist = BoardSpace.Flat(Walker.Position - Mira.transform.position);
            if (dist <= LagDistance || ReferenceEquals(Walker.Following, Mira)) return;
            if (Now - _lastCall < CallEvery) return;
            _lastCall = Now;
            Calls++;
            Mira.Actor?.PlayOneShot(ActorKeys.Beckon);
            OpenAsk(AskCall, Mira, ExpeditionLines.Get(S, ExpeditionLines.MiraCall, "This way!"), new[] { Reply(ExpeditionLines.ReplyComing) }, i => FollowMira());
        }

        // ---- what happens at a place ----

        private void OnModeChanged(PartyMode mode)
        {
            var p = C.Party;
            if (mode == PartyMode.Acting)
            {
                // The first line of what she finds is said as she starts on it (reading the stone, seeing the bare pedestal).
                var ev = PredictEvent(p.At, p.ActingAction);
                _earlyLine = null;
                if (ev != null && ev.Lines != null && ev.Lines.Count >= 2 && !p.Studying)
                {
                    string first = ev.Lines[0];
                    if (ExpeditionLines.SpeakerOf(S, first) == "mira" && SayLine(first)) _earlyLine = ev.Id + "/" + first;
                }
            }
            if (mode == PartyMode.AtThreshold) { _ifLoseAsked = false; _thresholdAskAfter = 0f; }
            if (mode == PartyMode.WaitingForPlayer) _towerAskAfter = 0f;
        }

        /// <summary>The quest event that will fire when the party does this action here (the sim's own rule: requirements, guards, once).</summary>
        private QuestEventDef PredictEvent(MarkerRef at, QuestAction action)
        {
            var c = C;
            foreach (var ev in c.Definition.Events)
            {
                if (ev == null || ev.At != at || ev.Action != action) continue;
                if (ev.Once && c.FiredEvents.Contains(ev.Id)) continue;
                bool ok = true;
                foreach (var f in ev.Requires) if (!c.Knows(f)) { ok = false; break; }
                if (!ok || !CampaignGuards.AllHold(c, ev.Guards)) continue;
                return ev;
            }
            return null;
        }

        private void OnQuestEvent(string eventId)
        {
            var ev = D.Event(eventId);
            if (ev == null) return;
            foreach (var id in ev.Lines)
            {
                if (_earlyLine == eventId + "/" + id) continue; // already said as she began
                string speaker = ExpeditionLines.SpeakerOf(S, id);
                // The prince and other people far from the party say their line when the player next stands by them.
                if (speaker == "prince") { _princeNews = true; continue; }
                SayLine(id);
            }
            _earlyLine = null;
            if (Sites != null) Sites.OnQuestEvent(ev);
        }

        // ---- choices ----

        private void AskChoiceQuestion()
        {
            var p = C.Party;
            var ch = D.Choice(p.ChoiceId);
            if (ch == null) return;
            _choiceAsked = true;
            var replies = new List<string>();
            foreach (var a in ch.Answers) replies.Add(ExpeditionLines.Get(S, a.Line, a.Line));
            string choiceId = ch.Id;
            OpenAsk(AskChoice, Mira, ExpeditionLines.Get(S, ch.QuestionLine, "What do I answer?"), replies, i =>
            {
                if (!C.Party.PlayerPresent) Send(new SetCompany { Present = true });
                var r = Send(new AdviseMira { ChoiceId = choiceId, Answer = i });
                if (!r.Ok) SayText("mira", r.Message);
            });
        }

        private void OnChoiceAnswered(ChoiceAnswered ca)
        {
            if (_askKind == AskChoice) CloseAsk();
            if (ca.Correct) return;
            var ch = D.Choice(ca.ChoiceId);
            if (ch == null || !SayLine(ch.WrongLine)) SayLine(ExpeditionLines.MiraWrong);
            _choiceAsked = false; // the question stands: she asks again
        }

        // ---- thresholds (PRD 5.5, 5.6) ----

        private string _thresholdVolunteer = "";
        /// <summary>The companion named at the last threshold ("" = nobody left).</summary>
        public string ThresholdVolunteer => _thresholdVolunteer;

        private void OnThreshold(EncounterThreshold th)
        {
            _thresholdVolunteer = th.Volunteer ?? "";
            _ifLoseAsked = false;
            var m = MateOf(_thresholdVolunteer);
            if (m != null && m.Alive) m.Npc.Actor?.PlayOneShot(ActorKeys.Greet);
        }

        private void AskThresholdQuestion()
        {
            var p = C.Party;
            var e = D.Encounter(p.EncounterId);
            if (e == null) return;
            if (C.Scout != null && C.Scout.Out)
            {
                // No fight while the scout is out (5.5): the wait is real time.
                if (SayLine(ExpeditionLines.MiraWaitScout)) _thresholdAskAfter = Now + 15f;
                return;
            }
            var vol = CampaignApi.NextVolunteer(S);
            _thresholdVolunteer = vol != null ? vol.Id : "";
            string suffix = e.Id.StartsWith("keeper_") ? e.Id.Substring(7) : e.Id;
            string text = ExpeditionLines.First(S, out _, "mira_threshold_" + suffix, "mira_threshold_" + e.Id, "mira_threshold");
            if (text == null)
            {
                var any = CampaignApi.LinesFor(S, "mira", "threshold");
                text = any.Count > 0 ? any[0] : (string.IsNullOrEmpty(e.OpponentName) ? "Do we fight?" : e.OpponentName + " has seen us. Do we fight?");
            }
            // The stakes in the duel's own words (package I7), so the bubble and the duel's stakes card agree.
            string stakes = CampaignDuel.StakesLine(new WardDuelRequest { Mode = EncounterKind.Lethal, VolunteerName = vol?.Name });
            if (vol == null)
            {
                var alone = ExpeditionLines.Get(S, "mira_nobody_left");
                text = (alone ?? stakes) + " Do we fight?";
            }
            else
            {
                if (stakes != null && !text.Contains(CampaignDuel.FirstName(vol.Name))) text = text + " " + stakes;
                // The one who will cover the retreat steps forward as he is named.
                var m = MateOf(vol.Id);
                if (m != null) m.Npc.Actor?.PlayOneShot(ActorKeys.Greet);
            }
            var replies = new List<string> { Reply(ExpeditionLines.ReplyFight), Reply(ExpeditionLines.ReplyNotYet) };
            bool ifLose = !_ifLoseAsked;
            if (ifLose) replies.Add(Reply(ExpeditionLines.ReplyIfLose));
            string encounterId = e.Id;
            OpenAsk(AskThreshold, Mira, text, replies, i =>
            {
                if (i == 0)
                {
                    if (!C.Party.PlayerPresent) Send(new SetCompany { Present = true });
                    var r = Send(new CommitEncounter { EncounterId = encounterId });
                    _thresholdAskAfter = Now + 2f;
                    if (!r.Ok) { SayText("mira", r.Message); _thresholdAskAfter = Now + 6f; }
                }
                else if (i == 1)
                {
                    Send(new WithdrawFromEncounter { EncounterId = encounterId });
                    _thresholdAskAfter = Now + 2f;
                }
                else
                {
                    _ifLoseAsked = true;
                    if (!SayLine(CampaignApi.NextVolunteer(S) != null ? "mira_if_lose" : "mira_nobody_left")) SayLine("mira_if_lose");
                }
            });
        }

        private void OnWithdrawTalk(PartyRetreated pr)
        {
            if (_askKind == AskThreshold) CloseAsk();
            if (pr.Why == "withdraw") SayLine(ExpeditionLines.MiraNotYet);
        }

        // ---- the duel (package I7), or its stand-in ----

        private EncounterCommitted _committed;
        private string _lastResolvedEncounter;
        private DuelResult _lastResult;
        /// <summary>Attempts committed since this director started (tests).</summary>
        public int DuelsStarted { get; private set; }
        /// <summary>The last duel's result as the sim applied it (tests).</summary>
        public DuelResult LastResult => _lastResult;

        private void OnCommitted(EncounterCommitted ec)
        {
            if (_askKind == AskThreshold) CloseAsk();
            _committed = ec;
            _preResolvePos = Mira.transform.position;
            _preResolveSpace = MiraSpace;
            DuelsStarted++;
            Mira.Actor?.PlayOneShot(ActorKeys.Cast);
            // The duel is package I7's: its driver stages every EncounterCommitted on the tick (CampaignDuel.Start) and
            // sends the one ResolveEncounter. Not called again from here: this event is read a frame later, when a duel
            // left in the meantime must not be staged again with the old seed.
        }

        private void OnResolved(EncounterResolved er)
        {
            _lastResolvedEncounter = er.EncounterId;
            _lastResult = er.Result;
            if (er.Result != DuelResult.Victory) return;
            // A win: the keeper's word, then Mira's (the lines that the victory's facts unlock).
            var e = D.Encounter(er.EncounterId);
            Mira.Actor?.PlayOneShot(ActorKeys.Cheer);
            foreach (var m in _mates) if (m.Alive && !m.Out) m.Npc.Actor?.PlayOneShot(ActorKeys.Cheer);
            if (e == null) return;
            var facts = new HashSet<string>();
            foreach (var fx in e.OnVictory) if (fx.Kind == EffectKind.SetFact && !string.IsNullOrEmpty(fx.Ref)) facts.Add(fx.Ref);
            foreach (var l in D.Lines)
            {
                if (string.IsNullOrEmpty(l.Speaker) || l.Topic == "journal" || l.Topic == "lead") continue;
                bool unlocked = false;
                foreach (var g in l.Guards)
                    if (CampaignGuards.TryParse(g, out var kind, out var subject, out _) && kind == CampaignGuards.Fact && facts.Contains(subject)) unlocked = true;
                if (unlocked) SayLine(l.Id);
            }
        }

        private void OnRetreatBegins(RetreatScene sc)
        {
            ClearScript();
            CloseAsk();
            SayLine(sc.Volunteer.Length > 0 ? ExpeditionLines.MiraRetreat : ExpeditionLines.MiraDraw);
            if (sc.Volunteer.Length == 0) return;
            // What the one who stays says (the pack's "volunteer" topic). The body speaks it at the gap.
            var m = MateOf(sc.Volunteer);
            var lines = D.Lines.FindAll(l => l.Speaker == sc.Volunteer && l.Topic == "volunteer");
            if (m != null && lines.Count > 0)
            {
                // Said by hand: the sim already counts them dead, so the guarded lookup would refuse the line.
                string text = CampaignTokens.Fill(C, lines[0].Text ?? "");
                if (Hears(m.Npc))
                {
                    SpeechBubbles.Say(m.Npc, text, Real(4f));
                    // His last words belong to the tick he fell on (the scene plays a frame or two after it).
                    _said.Add(new Line { Tick = C.Companion(sc.Volunteer)?.FellTick ?? S.Tick, Speaker = sc.Volunteer, Text = text });
                }
            }
        }

        private void OnRetreatEnded(RetreatScene sc)
        {
            if (C.Party.PlayerPresent) Interiors.Enter(C.Party.At);
            // Mira's word for the fallen, once the player stands with the party again (the way to the safe point takes a moment).
            _memorialDue = true; _memorialBy = Time.unscaledTime + 6f;
        }

        private bool _memorialDue;
        private float _memorialBy;

        private void UpdateMemorial()
        {
            if (!_memorialDue) return;
            bool here = PlayerSpace == MiraSpace && (Interiors.Current == null || !InteriorDirector.Instance || !InteriorDirector.Instance.IsCrossing);
            if (!here && Time.unscaledTime < _memorialBy) return;
            _memorialDue = false;
            // The pack's "memorial" topic, lines without a custody guard.
            foreach (var l in D.Lines)
            {
                if (l.Speaker != "mira" || l.Topic != "memorial" || _saidIds.Contains(l.Id)) continue;
                if (l.Guards.Exists(g => g.StartsWith(CampaignGuards.Custody))) continue;
                if (SayLine(l.Id)) break;
            }
        }

        // ---- the tower (PRD 5.7) ----

        private bool TowerCrystalHere(out Element element)
        {
            element = Element.Earth;
            var c = C; var p = c.Party;
            if (p.At.IsInterior) return false;
            for (int i = 0; i < c.Crystals.Length; i++)
            {
                if (c.Crystals[i].Custody != Custody.Carried) continue;
                var cd = c.Definition.Crystal((Element)i);
                var tower = cd != null ? c.Definition.Tower(cd.Tower) : null;
                if (tower != null && tower.Location == p.At.Location) { element = (Element)i; return true; }
            }
            return false;
        }

        private void AskTowerQuestion(Element element)
        {
            OpenAsk(AskTower, Mira, ExpeditionLines.Get(S, "mira_tower_ask", "This is its tower. Shall I set it?"),
                new[] { Reply(ExpeditionLines.ReplySet), Reply(ExpeditionLines.ReplyWait) }, i =>
                {
                    if (i == 0)
                    {
                        if (!C.Party.PlayerPresent) Send(new SetCompany { Present = true });
                        var r = Send(new ConfirmRestore { Crystal = element });
                        _towerAskAfter = Now + 2f;
                        if (!r.Ok) { SayText("mira", r.Message); _towerAskAfter = Now + 8f; }
                    }
                    else
                    {
                        SayLine(ExpeditionLines.MiraTowerWait);
                        _towerAskAfter = Now + 25f;
                    }
                });
        }

        private void OnRestored(CrystalRestored cr)
        {
            if (_askKind == AskTower) CloseAsk();
            foreach (var m in _mates) if (m.Alive && !m.Out) m.Npc.Actor?.PlayOneShot(ActorKeys.Cheer);
            if (Sites != null) Sites.OnRestored(cr);
            // After the event's own lines: the fallen are remembered at the lit tower (memorial lines with a custody guard).
            foreach (var l in D.Lines)
            {
                if (l.Speaker != "mira" || l.Topic != "memorial" || _saidIds.Contains(l.Id)) continue;
                if (!l.Guards.Exists(g => g.StartsWith(CampaignGuards.Custody))) continue;
                _memorialAfterRestore = l.Id;
                break;
            }
            _princeNews = true;
        }

        private string _memorialAfterRestore;
        private bool _princeNews;

        /// <summary>
        /// What the camp says of a lit tower (the pack's prince lines, topic "state"): said by the prince the next time
        /// the player stands by him, when he is not in the middle of something.
        /// </summary>
        private void UpdatePrinceNews()
        {
            if (_memorialAfterRestore != null && ScriptPending == 0 && _ask == 0)
            {
                SayLine(_memorialAfterRestore);
                _memorialAfterRestore = null;
            }
            if (!_princeNews || Walker == null) return;
            var pd = PrinceDirector.Instance;
            var prince = pd != null ? pd.Prince : null;
            if (prince == null) { _princeNews = false; return; }
            if (PrinceBusy() || PlayerSpace.Length > 0 || BoardSpace.Flat(prince.transform.position - Walker.Position) > 12f) return;
            foreach (var l in D.Lines)
            {
                if (l.Speaker != "prince" || l.Topic != "state" || _saidIds.Contains(l.Id)) continue;
                var text = CampaignApi.Line(S, l.Id);
                if (text == null) continue;
                _saidIds.Add(l.Id);
                SpeechBubbles.Say(prince, text, Real(ExpeditionLines.Seconds(text)));
                Record("prince", text);
                return;
            }
            _princeNews = false;
        }

        // ---- tapping Mira and the companions ----

        private void OnMiraTapped(Npc n)
        {
            if (C == null || _retreat != null) return;
            if (C.Phase != CampaignPhase.Running) { n.OnTapped = null; n.Interact(); n.OnTapped = OnMiraTapped; return; }
            if (_ask != 0 && _askKind != AskMenu) { SpeechBubbles.Expand(_ask); return; }
            if (SceneStep < SceneUnderWay)
            {
                // Impatient: straight to her question.
                if (_openingStep >= 1) { ClearScript(); _openingStep = 3; }
                return;
            }
            OpenMenu();
        }

        private void OpenMenu()
        {
            ClearScript(); // a tap interrupts whatever was being said: her answer comes at once
            var p = C.Party;
            bool under = MiraSpace.Length > 0;
            var replies = new List<string>();
            var acts = new List<Action>();
            if (!p.PlayerPresent)
            {
                replies.Add(Reply(ExpeditionLines.ReplyWith)); acts.Add(() => JoinCompany(true));
                replies.Add(Reply(ExpeditionLines.ReplyKnow)); acts.Add(WhatWeKnow);
            }
            else
            {
                replies.Add(Reply(ExpeditionLines.ReplyKnow)); acts.Add(WhatWeKnow);
                replies.Add(Reply(ExpeditionLines.ReplyLeave)); acts.Add(() => LeaveCompany(true));
                if (under) { replies.Add(Reply(ExpeditionLines.ReplyLeadOut)); acts.Add(LeadOut); }
                else if ((p.Mode == PartyMode.Idle || p.Mode == PartyMode.WaitingForPlayer) && EncounterDirectory.IsRegistered(EncounterDirectory.WardMira))
                {
                    replies.Add(Reply(ExpeditionLines.ReplyPractice));
                    acts.Add(() => EncounterDirectory.TryStart(EncounterDirectory.WardMira, Mira));
                }
            }
            string text = ExpeditionLines.Get(S, p.PlayerPresent ? "mira_what" : ExpeditionLines.MiraAway, "What is it?");
            OpenAsk(AskMenu, Mira, text, replies, i => { if (i < acts.Count) acts[i](); });
            SpeechBubbles.Expand(_ask);
        }

        /// <summary>"What do we know?": the count, then the journal page (crystal by crystal, where to look next).</summary>
        private void WhatWeKnow()
        {
            SayLine("mira_status");
            if (Journal != null) Journal.OpenSoon();
        }

        private void LeadOut()
        {
            SayLine(ExpeditionLines.MiraLeadOut);
            Interiors.LeadOut();
        }

        private void OnCompanionTapped(Npc n)
        {
            var m = _mates.Find(x => x.Npc == n);
            if (m == null || C == null) return;
            if (!m.Alive)
            {
                SpeechBubbles.Inspect(m.FallenMark != null ? m.FallenMark.transform : n.transform, Vector3.zero, m.Def.Name, new[] { n.Role }, 6f);
                return;
            }
            string line = CampaignApi.Line(S, m.Def.Line);
            string text = m.Def.Role + "." + (line != null ? "\n\"" + line + "\"" : "");
            bool under = MiraSpace.Length > 0 && C.Party.PlayerPresent;
            if (under)
                SpeechBubbles.Ask(n, text, new[] { Reply(ExpeditionLines.ReplyLeadOut) }, i => { if (i == 0) LeadOut(); }, Real(9f));
            else SpeechBubbles.Say(n, text, Real(6f));
            Record(m.Id, text);
            if (Walker != null) n.FaceTowards(Walker.Position);
        }

        // ---------------- cues: what Mira did while you were away, and word of the Ash ----------------

        private readonly List<string> _cueQueue = new List<string>();
        private int _cueHandle;
        private float _cueGapUntil;
        private bool _reportNext, _rejoinHintDue;
        /// <summary>Every cue shown (the short line in front of the player), in order.</summary>
        public readonly List<string> Cues = new List<string>();
        public string LastCue => Cues.Count > 0 ? Cues[Cues.Count - 1] : null;
        /// <summary>The newest thing worth telling a player who is away: the last journal line (HUD line, map).</summary>
        public string LatestWord { get; private set; } = "";

        private void OnRivalReported(RivalReported rr)
        {
            _reportNext = true;
            if (Ash != null) Ash.OnReported(rr);
        }

        private void OnJournal(JournalAdded ja)
        {
            if (string.IsNullOrEmpty(ja.Text)) return;
            LatestWord = ja.Text;
            bool report = _reportNext;
            _reportNext = false;
            // Word of the Ash expedition reaches the player wherever they are; Mira's own doings only when they are not with
            // her, and then what she did, not what was said there (the journal also keeps the first line spoken at a place).
            if (report || (!C.Party.PlayerPresent && !IsSpokenLine(ja.Text))) Cue(ja.Text);
        }

        private HashSet<string> _spokenTexts;

        /// <summary>True when a journal sentence is a line somebody says in the pack (as written, tokens filled now).</summary>
        private bool IsSpokenLine(string text)
        {
            if (_spokenTexts == null)
            {
                _spokenTexts = new HashSet<string>();
                foreach (var l in D.Lines) if (!string.IsNullOrEmpty(l.Speaker)) _spokenTexts.Add(l.Text ?? "");
            }
            if (_spokenTexts.Contains(text)) return true;
            foreach (var l in D.Lines)
                if (!string.IsNullOrEmpty(l.Speaker) && (l.Text ?? "").IndexOf('{') >= 0 && CampaignTokens.Fill(C, l.Text) == text) return true;
            return false;
        }

        /// <summary>A short line hung in front of the player for a few seconds (never while the prince's people speak, never two at once).</summary>
        public void Cue(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_cueQueue.Count > 0 && _cueQueue[_cueQueue.Count - 1] == text) return;
            _cueQueue.Add(text);
            while (_cueQueue.Count > 4) _cueQueue.RemoveAt(0); // the journal keeps the rest
        }

        private void UpdateCues()
        {
            if (_cueHandle != 0 && SpeechBubbles.IsOpen(_cueHandle)) return;
            if (_cueHandle != 0) { _cueHandle = 0; _cueGapUntil = Time.unscaledTime + Real(1.5f); }
            // A cue is word from afar, not somebody speaking: it waits for Mira's own lines and questions, not for the prince's.
            if (_cueQueue.Count == 0 || Time.unscaledTime < _cueGapUntil || _ask != 0 && _askKind != AskCall || ScriptPending > 0 || MapScreen.IsOpen) return;
            string text = _cueQueue[0];
            _cueQueue.RemoveAt(0);
            if (_rejoinHintDue)
            {
                // Right after the first word of an absence: how to find her again.
                _rejoinHintDue = false;
                _cueQueue.Insert(0, ExpeditionLines.Get(S, ExpeditionLines.CueHowToRejoin, "The map shows where Mira is."));
            }
            _cueHandle = SpeechBubbles.Say(null, text, Real(ExpeditionLines.Seconds(text)));
            Cues.Add(text);
        }

        // ---------------- the end (Slice 2 stages the endings; Slice 1 only says the lines) ----------------

        private void OnEnded(CampaignEnded ce)
        {
            CloseAsk();
            var ending = D.Ending(ce.EndingId);
            if (ending == null) return;
            foreach (var id in ending.Lines)
            {
                var text = CampaignApi.Line(S, id);
                if (text != null) Cue(text);
            }
        }
    }
}
