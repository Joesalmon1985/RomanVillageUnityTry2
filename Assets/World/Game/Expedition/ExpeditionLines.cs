using System.Collections.Generic;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The expedition's words (package I6). Every line comes from the campaign pack through
    /// <see cref="CampaignApi.Line"/> (tokens filled, guards applied, never a dead speaker). A few lines the game
    /// needs are not in the authored pack yet (<see cref="Missing"/>): they are used only while the pack has no line
    /// of that id, so a pack writer adds the id and the pack's text wins. Ids actually taken from that table are
    /// listed in <see cref="UsedFallbacks"/>.
    /// </summary>
    public static class ExpeditionLines
    {
        // Replies of the pack (lines.json, topic "reply").
        public const string ReplyLead = "reply_lead", ReplyMore = "reply_more", ReplyAhead = "reply_ahead", ReplyWhy = "reply_why",
            ReplyNot = "reply_not", ReplyKnow = "reply_know", ReplyLeave = "reply_leave", ReplyPractice = "reply_practice",
            ReplyWith = "reply_with", ReplyFight = "reply_fight", ReplyNotYet = "reply_not_yet", ReplyIfLose = "reply_if_lose",
            ReplySet = "reply_set", ReplyWait = "reply_wait", ReplyLeadOut = "reply_lead_out";

        // Ids the game asks for that the authored pack lacked until the integration (15 Nov 2026: now in authored_v1/lines.json).
        public const string ReplyComing = "reply_coming", ReplyGoDown = "reply_go_down",
            MiraCall = "mira_call", MiraGoOn = "mira_go_on", MiraDeclined = "mira_declined", MiraLeave = "mira_leave",
            MiraRejoin = "mira_rejoin", MiraNotYet = "mira_not_yet", MiraTowerWait = "mira_tower_wait", MiraRetreat = "mira_retreat",
            MiraDraw = "mira_draw", MiraAway = "mira_away_here", MiraNothing = "mira_nothing_open",
            MiraWhyGeneric = "mira_why", MiraWrong = "mira_wrong_answer", CueFellBehind = "cue_fell_behind", CueHowToRejoin = "cue_rejoin",
            MiraLeadOut = "mira_lead_out", CompanionFallen = "companion_fallen",
            MiraWaitScout = "mira_wait_scout";

        /// <summary>Fallbacks for a pack without these ids (the authored pack has them all). Original text.</summary>
        public static readonly Dictionary<string, string> Missing = new Dictionary<string, string>
        {
            [ReplyComing] = "Coming",
            [ReplyGoDown] = "Go down",
            [MiraCall] = "This way! Keep up.",
            [MiraGoOn] = "I'm going on. Catch me up when you can.",
            [MiraDeclined] = "Not that way, then. Let me think.",
            [MiraLeave] = "Go, then. I'll leave word of where I am.",
            [MiraRejoin] = "Good. Stay close.",
            [MiraNotYet] = "Not yet, then. We step back and breathe.",
            [MiraTowerWait] = "It will keep. Tell me when.",
            [MiraRetreat] = "Back! Back the way we came!",
            [MiraDraw] = "Neither Ward broke. Back, and we try again.",
            [MiraAway] = "You found me. Are you with me?",
            [MiraNothing] = "Nothing more to chase just now. We wait for word.",
            [MiraWhyGeneric] = "It's the best lead we have.",
            [MiraWrong] = "That wasn't it. We lost time.",
            [MiraLeadOut] = "The way out is lit. Follow the light.",
            [MiraWaitScout] = "Wait for {companion.tansy.name}. I won't fight with one of us still out there.",
            [CueFellBehind] = "You fell behind. Mira has gone on without you.",
            [CueHowToRejoin] = "The map (Tab) shows where Mira is. Find her and tap her to rejoin.",
            [CompanionFallen] = "fell here, holding the way out.",
        };

        private static readonly HashSet<string> s_used = new HashSet<string>();
        /// <summary>Ids taken from <see cref="Missing"/> since the last domain load (for the hand-off note).</summary>
        public static IReadOnlyCollection<string> UsedFallbacks => s_used;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_used.Clear();

        /// <summary>True when the running campaign's pack has a line of that id.</summary>
        public static bool InPack(WorldState s, string id) => s?.Campaign?.Definition?.Line(id) != null;

        /// <summary>
        /// The line to say now: the pack's (null when guarded out or its speaker is dead), else the game's stand-in for
        /// an id the pack lacks (tokens filled), else null.
        /// </summary>
        public static string Get(WorldState s, string id)
        {
            if (string.IsNullOrEmpty(id) || s?.Campaign == null) return null;
            if (InPack(s, id)) return Sentence(CampaignApi.Line(s, id));
            if (!Missing.TryGetValue(id, out var text)) return null;
            s_used.Add(id);
            return Sentence(CampaignTokens.Fill(s.Campaign, text));
        }

        /// <summary>A line that begins with a token ("{crystal.earth.name} lies under...") begins with a capital when said.</summary>
        public static string Sentence(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>The line, or the fallback text when there is none (replies must never be empty).</summary>
        public static string Get(WorldState s, string id, string fallback) => Get(s, id) ?? fallback;

        /// <summary>Speaker id of a pack line ("" = narration or an id the pack lacks).</summary>
        public static string SpeakerOf(WorldState s, string id) => s?.Campaign?.Definition?.Line(id)?.Speaker ?? "";

        /// <summary>The first line of the pack that may be said now among the ids (null when none).</summary>
        public static string First(WorldState s, out string usedId, params string[] ids)
        {
            usedId = null;
            foreach (var id in ids)
            {
                var t = Get(s, id);
                if (t != null) { usedId = id; return t; }
            }
            return null;
        }

        /// <summary>Seconds a line stays up: long enough to read, short enough to keep the party moving.</summary>
        public static float Seconds(string text) => UnityEngine.Mathf.Clamp(1.5f + (text ?? "").Length * 0.036f, 2.2f, 5.5f);
    }
}
