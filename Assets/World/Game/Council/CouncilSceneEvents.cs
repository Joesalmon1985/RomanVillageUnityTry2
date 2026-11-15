using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>Reactions of the village to council events: gestures and the few lines of the content file. Animation and dialogue only.</summary>
    public sealed partial class CouncilScene
    {
        public void BeginCarry() { _carryT = 0f; _aide.gameObject.SetActive(true); }

        public void Press(string what)
        {
            var c = ActiveSeatShown >= 0 ? _cand[ActiveSeatShown] : null;
            if (c == null) return;
            if (what == "hand") { c.PlayOneShot(ActorKeys.Beckon); _beckonAt = Time.time + 3.4f; }
            else if (what == "seal") c.PlayOneShot(ActorKeys.Hammer);
            else if (what == "petition") c.PlayOneShot(ActorKeys.Greet);
        }

        public void CandidateSay(string text, float seconds = 4f)
        {
            var c = ActiveSeatShown >= 0 ? _candSpeaker[ActiveSeatShown] : null; if (c == null || ViewCamera == null) return;
            LastCandidateLine = text;
            if (_candBubble != 0) SpeechBubbles.Close(_candBubble);
            _candBubble = CouncilBubbles.Say(c, text, seconds);
        }

        public void SayLead(int track, string text, float seconds = 3.6f)
        {
            if (track < 2 || track > 12 || string.IsNullOrEmpty(text)) return;
            if (_leadBubble != 0) SpeechBubbles.Close(_leadBubble);
            _leadBubble = CouncilBubbles.Say(Lead(track).Speaker, text, seconds);
            LeadBubbles++; LastLeadLine = text;
        }

        public void CloseBubbles()
        {
            if (_leadBubble != 0) SpeechBubbles.Close(_leadBubble); _leadBubble = 0;
            if (_candBubble != 0) SpeechBubbles.Close(_candBubble); _candBubble = 0;
        }

        public void Pleased(int track, bool listeners, bool settled, float stagger = 0.18f)
        {
            float t = 0f;
            foreach (var p in _places[track - 2].P)
            {
                if ((p.S == PState.Listening && listeners) || (p.S == PState.Settled && settled)) { p.NextPleased = Time.time + t; t += stagger; }
            }
        }

        /// <summary>Plays the scene's reaction to one council event (animation and dialogue only).</summary>
        public void Handle(CouncilEvent ev, SettlementCouncil sc)
        {
            if (sc == null) return;
            switch (ev)
            {
                case CouncilPushMade _: Press("hand"); break;
                case CouncilAdvanceMade a:
                    foreach (var st in a.Steps) { Pleased(st.Track, true, false); _places[st.Track - 2].ReactUntil = Time.time + 1.5f; }
                    if (a.Steps.Count > 0)
                    {
                        int t = a.Steps[0].Track;
                        bool first = _spoken.Add(t);
                        SayLead(t, CouncilApi.DisputeLine(sc, t, first ? CouncilContent.OpeningLine : CouncilContent.ProgressLine, _lineCounter++, a.Seat, -1));
                    }
                    break;
                case CouncilBanked b:
                    Press("seal");
                    foreach (var p in b.Places) { Pleased(p.Track, true, true, 0.12f); }
                    break;
                case CouncilTrackClaimed c:
                    foreach (var p in _places[c.Track - 2].P) { p.A.PlayOneShot(ActorKeys.Cheer); }
                    SayLead(c.Track, CouncilApi.DisputeLine(sc, c.Track, CouncilContent.AgreementLine, _lineCounter++, c.Seat, c.Seat), 4.2f);
                    break;
                case CouncilBust bust:
                    if (bust.Lost.Count > 0)
                    {
                        Lead(bust.Lost[0].Track).A.PlayOneShot(ActorKeys.Surprise);
                        SayLead(bust.Lost[0].Track, CouncilApi.DisputeLine(sc, bust.Lost[0].Track, CouncilContent.SetbackLine, _lineCounter++, bust.Seat, -1));
                    }
                    break;
                case CouncilPetitionSpent _: Press("petition"); break;
                case CouncilTurnPassed tp:
                    for (int t = 2; t <= 12; t++)
                        if (sc.Election.Claimed[t - 2] < 0 && _spoken.Contains(t) && sc.Election.BankedAt(tp.To, t) > 0)
                        { SayLead(t, CouncilApi.DisputeLine(sc, t, CouncilContent.RevisitLine, _lineCounter++, tp.To, -1)); break; }
                    break;
                case CouncilElectionWon w:
                    if (ActiveSeatShown >= 0) _cand[ActiveSeatShown].PlayOneShot(ActorKeys.Cheer);
                    for (int t = 2; t <= 12; t++) foreach (var p in _places[t - 2].P) if (p.S == PState.Settled) p.A.PlayOneShot(ActorKeys.Cheer);
                    break;
            }
        }
    }
}
