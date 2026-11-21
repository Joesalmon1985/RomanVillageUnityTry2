using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The prince as guide in a god board game (G1-7; PRD_GODBOARD GB-7, section 3 step 1). At the start he comes to the
    /// player, greets them, leads them to Mira (the player follows), then points them to the temple. He does not pitch
    /// builds in a god board game (no first scene, no pitch loop, no announcements). At the board his adviser line comes
    /// from <see cref="GodBoardDirector"/> through <see cref="AdviseAtBoard"/>. Speech bubbles only; nothing here writes state.
    /// </summary>
    public sealed partial class PrinceDirector
    {
        public enum GuideStage { Waiting, Approach, Greeting, Leading, AtMira, Temple, Done }

        /// <summary>Where the opening guide is (Done once finished, skipped, or in any other ruleset).</summary>
        public GuideStage Guide { get; private set; } = GuideStage.Done;
        /// <summary>True in a god board game: the prince guides and advises instead of pitching.</summary>
        public bool IsGodBoardGame { get; private set; }
        public static float GuideBeat = 5f, GuideGreetTimeout = 14f;

        private float _guideAt, _guideCall = -100f;
        private int _guideAsk;
        private Vector3 _miraStand;

        private void BeginGodBoard()
        {
            IsGodBoardGame = true;
            FirstSceneStep = SceneDone; // no woodcutter's hut
            var t = S.Table;
            // A game already at the board (a load) needs no welcome.
            Guide = t != null && t.Phase != TablePhase.NotOpened ? GuideStage.Done : GuideStage.Waiting;
            var mira = Npc.Find(NpcIds.Mira);
            var at = mira != null ? mira.transform.position : _camp.MiraSpot;
            // Stand a few metres from Mira, on the side the prince comes from.
            var side = Prince.transform.position - at; side.y = 0;
            _miraStand = at + (side.sqrMagnitude > 1f ? side.normalized : Vector3.back) * 4f;
            var c = WalkLattice.ToCell(_miraStand);
            if (WalkGrid.IsBlocked(c)) { var f = WalkGrid.NearestFree(c, 4); if (f.HasValue) _miraStand = WalkLattice.ToWorld(f.Value); }
        }

        private bool Guiding => IsGodBoardGame && Guide != GuideStage.Done;

        private void UpdateGodGuide()
        {
            var t = S.Table;
            if (Guide != GuideStage.Done && t != null && t.Phase != TablePhase.NotOpened) { EndGuide(); return; }
            var walker = Walker;
            switch (Guide)
            {
                case GuideStage.Waiting:
                    if (walker == null || Hushed) return;
                    // Opening vista: no walk to Mira; a short welcome over the camp that points out each place in turn.
                    if (OpeningVista.Instance != null && OpeningVista.WillOpen) { if (OpeningVista.Up) VistaWelcome(); return; }
                    Guide = GuideStage.Approach;
                    return;
                case GuideStage.Approach:
                {
                    if (walker == null) return;
                    var spot = GuideSpot();
                    float d = BoardSpace.Flat(Prince.transform.position - PlayerPos);
                    if (d <= PitchDistance + 2.5f)
                    {
                        Prince.StopWalking();
                        Prince.FaceTowards(PlayerPos);
                        Greet();
                        return;
                    }
                    Prince.WalkSpeed = HurrySpeed;
                    if (!Prince.IsWalking || !Prince.Goal.HasValue || BoardSpace.Flat(Prince.Goal.Value - spot) > 4f) Prince.WalkTo(spot);
                    return;
                }
                case GuideStage.Greeting:
                    Prince.FaceTowards(PlayerPos);
                    if (_guideAsk != 0 && !SpeechBubbles.IsOpen(_guideAsk)) _guideAsk = 0;
                    if (Now - _guideAt > GuideGreetTimeout) StartGuideLead(); // unanswered: he simply goes
                    return;
                case GuideStage.Leading:
                    UpdateGuideLead();
                    return;
                case GuideStage.AtMira:
                    Prince.FaceTowards(PlayerPos);
                    if (Now - _guideAt >= GuideBeat) ToTemple();
                    return;
                case GuideStage.Temple:
                    if (Now - _guideAt >= GuideBeat + 3f) EndGuide();
                    return;
            }
        }

        private Vector3 GuideSpot()
        {
            var w = Walker;
            var fwd = w.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            var p = w.Position + Quaternion.Euler(0, 25f, 0) * fwd * PitchDistance;
            var c = WalkLattice.ToCell(p);
            if (WalkGrid.IsBlocked(c)) { var f = WalkGrid.NearestFree(c, 3); if (f.HasValue) p = WalkLattice.ToWorld(f.Value); }
            return p;
        }

        private void Greet()
        {
            Guide = GuideStage.Greeting;
            _guideAt = Now;
            CloseAsks();
            string text = PrinceLines.GodGreeting;
            _guideAsk = SpeechBubbles.Ask(Prince, text, new[] { PrinceLines.GodReplyLead, PrinceLines.GodReplyLater }, i =>
            {
                _guideAsk = 0;
                if (i == 0) StartGuideLead(); else { Say(Prince, PrinceLines.GodLater); EndGuide(); }
            }, 0f);
            Record(Prince, text, true);
            Prince.Actor?.PlayOneShot(ActorKeys.Greet);
        }

        private void StartGuideLead()
        {
            if (_guideAsk != 0) { SpeechBubbles.Close(_guideAsk); _guideAsk = 0; }
            Guide = GuideStage.Leading;
            Prince.WalkSpeed = LeadSpeed;
            Prince.PointAt(null);
            Prince.WalkTo(_miraStand);
            Say(Prince, PrinceLines.GodLead);
            if (Walker != null) Walker.Follow(Prince);
            _guideCall = Now;
        }

        private void UpdateGuideLead()
        {
            float dMira = BoardSpace.Flat(Prince.transform.position - _miraStand);
            if (dMira < 0.5f && !Prince.IsWalking) { ArriveAtMira(); return; }
            float d = BoardSpace.Flat(Prince.transform.position - PlayerPos);
            float dTarget = BoardSpace.Flat(_miraStand - PlayerPos);
            if (dTarget < dMira + 5f) d = Mathf.Min(d, dTarget); // a player heading there on their own is not lagging
            if (d > LagDistance)
            {
                Prince.StopWalking();
                Prince.FaceTowards(PlayerPos);
                if (Now - _guideCall >= CallEvery) { _guideCall = Now; Say(Prince, PrinceLines.GodWait); Prince.Actor?.PlayOneShot(ActorKeys.Beckon); }
            }
            else if (d <= ResumeDistance && !Prince.IsWalking) Prince.WalkTo(_miraStand);
        }

        private void ArriveAtMira()
        {
            Guide = GuideStage.AtMira;
            _guideAt = Now;
            var mira = Npc.Find(NpcIds.Mira);
            if (mira != null) Prince.PointAt(mira.transform.position + Vector3.up * 1.2f);
            Say(Prince, PrinceLines.GodMira);
        }

        private void ToTemple()
        {
            Guide = GuideStage.Temple;
            _guideAt = Now;
            var gb = GodBoardDirector.Instance;
            var temple = gb != null ? gb.OwnTemple : null;
            if (temple != null) Prince.PointAt(temple.transform.position + Vector3.up * 1.5f);
            Say(Prince, PrinceLines.GodTemple);
        }

        /// <summary>The opening vista's welcome (the prince's lines; the vista lights each place as it is named).</summary>
        private void VistaWelcome()
        {
            Guide = GuideStage.Done;
            var lines = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(WorldZones.HubTemple, PrinceLines.VistaTemple),
                new KeyValuePair<string, string>(WorldZones.HubCouncil, PrinceLines.VistaCouncil),
                new KeyValuePair<string, string>(WorldZones.HubRhythm, PrinceLines.VistaRhythm),
                new KeyValuePair<string, string>(WorldZones.HubHealer, PrinceLines.VistaHealer),
                new KeyValuePair<string, string>(WorldZones.HubCave, PrinceLines.VistaCave),
                new KeyValuePair<string, string>(WorldZones.HubDuel, PrinceLines.VistaDuel),
            };
            OpeningVista.Instance.PlayWelcome(Prince, PrinceLines.VistaGreeting, lines);
            Record(Prince, PrinceLines.VistaGreeting, true);
        }

        private void EndGuide()
        {
            if (_guideAsk != 0) { SpeechBubbles.Close(_guideAsk); _guideAsk = 0; }
            Guide = GuideStage.Done;
            Prince.PointAt(null);
        }

        private string GodHudLine()
        {
            switch (Guide)
            {
                case GuideStage.Approach: case GuideStage.Greeting: return Prince.DisplayName + " is coming to greet you.";
                case GuideStage.Leading: return Prince.DisplayName + " is leading you to Mira.";
                case GuideStage.AtMira: return "Mira can take you to the crystals.";
                case GuideStage.Temple: return "The temple: sacrifice a lamb at the altar.";
            }
            return null;
        }

        /// <summary>
        /// The prince's line when the board shows his hint (GB-7). Said in his bubble when he is in the world and returned
        /// for the board's feed. Null when he is not there.
        /// </summary>
        public string AdviseAtBoard(bool road, bool setup)
        {
            if (Prince == null) return null;
            string line = road ? PrinceLines.GodAdviseRoad : setup ? PrinceLines.GodAdviseSite : PrinceLines.GodAdviseBuild;
            Say(Prince, line, 5f);
            return line;
        }
    }
}
