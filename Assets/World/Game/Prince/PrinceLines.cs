using System;
using System.Collections.Generic;

namespace World.Game
{
    /// <summary>How hard a villager is pressing: rises with each unanswered pitch, urgent under an overrun warning.</summary>
    public enum PitchMood { Calm = 0, Keen = 1, Pressing = 2, Urgent = 3 }

    /// <summary>
    /// What the prince, the sawyer, the merchant and the builder say (package N). Original text. A line is picked from
    /// a bank by topic ("speaker/topic"); a speaker without its own bank for a topic uses the prince's. Placeholders in
    /// braces are filled by <see cref="Voice.Fill"/>: {thing} (what is proposed, with its article), {where}, {site},
    /// {camp}, {name}, {secs}.
    /// </summary>
    public static class PrinceLines
    {
        // ---- the first scene (REFACTOR_SPEC 2.C.5: exact) ----
        public const string FirstLine = "I'm the prince. Come with me. We're building a woodcutter's hut.";
        public const string ReplyLead = "Lead the way", ReplyWhat = "What's it for?", ReplyCatchUp = "I'll catch up.";

        // ---- replies to a pitch (REFACTOR_SPEC 8.5: exact) ----
        public const string ShowMe = "Show me", NotThis = "Not this", Later = "Later";
        public const string Coming = "Coming", Follow = "Follow", NotNow = "Not now";

        private static readonly Dictionary<string, string[]> Banks = new Dictionary<string, string[]>
        {
            // ======== the prince ========
            ["prince/open/Calm"] = new[] { "A word, friend.", "There you are.", "I've been thinking.", "Good, you're here.", "Walk with me a moment." },
            ["prince/open/Keen"] = new[] { "Me again.", "About what I said before.", "I haven't forgotten, and I hope you haven't.", "Still on my mind:", "Back to my idea." },
            ["prince/open/Pressing"] = new[] { "Please, listen this time.", "We are falling behind.", "I keep asking because it matters.", "Every minute we wait, the Ash Clan grows.", "I won't stop asking." },
            ["prince/open/Urgent"] = new[] { "The Ash Clan is gathering!", "No time left!", "They are coming for us!", "Quickly, before it's too late!", "Hurry, I beg you!" },

            ["prince/body/hut"] = new[] { "Let's put a woodcutter's hut {where}.", "A woodcutter's hut {where} would double our wood.", "I want a woodcutter's hut {where}." },
            ["prince/body/collector"] = new[] { "Let's put {thing} {where}.", "{Thing} {where} would double what we gather there.", "I want {thing} {where}.", "Come and start {thing} {where}." },
            ["prince/body/workshop"] = new[] { "We should build {thing} {where}.", "{Thing} {where} would turn raw goods into better ones.", "Time for {thing} {where}.", "I'd like {thing} {where}." },
            ["prince/body/sawmill"] = new[] { "We should build a sawmill {where}.", "A sawmill {where} would give us planks." },
            ["prince/body/lodge"] = new[] { "We need a warrior lodge {where}.", "Let's raise a warrior lodge {where}. Fed warriors keep us safe.", "A warrior lodge {where}, and the Ash Clan thinks twice." },
            ["prince/body/road"] = new[] { "We need {thing}.", "Let's lay {thing}.", "I want {thing}." },
            ["prince/body/camp"] = new[] { "Let's found a new camp {where}.", "The road reaches far enough: a camp {where} next.", "I've chosen a spot for a new camp, {where}.", "A new camp {where} would make us stronger." },
            ["prince/body/city"] = new[] { "Let's raise {camp} to a city.", "{Camp} is ready to become a city.", "Make {camp} a city and we count double against the Ash Clan." },
            ["prince/body/banish"] = new[] { "Demons sit on {site}. Banish them!", "We have to drive the demons off {site}.", "{Site} is held by demons. Only your spell can clear it.", "Come and banish the demons from {site}." },

            ["prince/lead"] = new[] { "Follow me.", "This way.", "Come on, it isn't far.", "Stay close.", "Right behind me, then." },
            ["prince/wait"] = new[] { "This way!", "Over here!", "Keep up!", "Don't get lost now!", "Here, follow me!" },
            ["prince/fetch"] = new[] { "You wandered off. Come, I'll show you again.", "Lost? Let me take you there.", "Not that way. The spot is with me.", "Wrong way, friend. Shall I lead again?" },
            ["prince/press"] = new[] { "Press the ground where it glows.", "Right here. Press the ground and cast.", "This is the spot. Touch the ground here.", "Here, where I'm pointing. Press it." },
            ["prince/nudge"] = new[] { "Go on, press the glowing ground.", "Just press the ground here.", "Here. Right where I'm pointing.", "Cast it, please, while we can." },
            ["prince/far"] = new[] { "Over here! The spot is by me.", "Come back, it's here!", "This way, the ground is here!" },
            ["prince/cast"] = new[] { "That's the way!", "Done! The carriers will bring what it needs.", "Good. Now the building can start.", "There it goes. Thank you.", "Splendid. One more thing standing." },
            ["prince/toofar"] = new[] { "Closer! The spell won't reach from there.", "A little closer, by the mark.", "Too far. Stand nearer the spot." },
            ["prince/refused"] = new[] { "Hm. That can't be done any more. Let me think again.", "Ah, too late for that one. I'll find another." },
            ["prince/decline"] = new[] { "Not that, then. Let me think.", "All right. I have another idea.", "Fair enough. How about something else?", "As you like. Here's another thought." },
            ["prince/later"] = new[] { "Later, then. I'll hold you to it.", "Very well. I'll ask again in a while.", "A minute, then. No more." },
            ["prince/expired"] = new[] { "Forget what I said. Things have changed.", "Never mind that one; it can't be done now.", "Scratch that. Something better came up." },
            ["prince/resigned"] = new[] { "Fine. I'll walk behind you for a while. Think it over.", "As you wish. I'll be close if you change your mind.", "I'll stop talking. For now.", "You're the one with the magic. I'll wait." },
            ["prince/done"] = new[] { "{Name} is finished.", "{Name} stands. Good work.", "Look, {name} is done." },
            ["prince/idle"] = new[] { "I'm thinking about what to build next.", "Nothing to show you yet. Walk on, I'll keep up.", "We wait for wood and clay. Then I'll have plans.", "Just keeping an eye on things." },
            ["prince/demon"] = new[] { "Demons have come down on {site}!", "There are demons at {site}. Watch out.", "Demons at {site}! That will hurt us." },
            ["prince/warn.defend"] = new[] { "The Ash Clan is gathering against us! {secs} seconds to grow stronger.", "A war camp on the horizon! We have {secs} seconds.", "They mean to overrun us. {secs} seconds: build strength, fast." },
            ["prince/warn.attack"] = new[] { "We are stronger than the Ash Clan now. Hold on {secs} seconds and they are ours.", "The Ash Clan is weak. {secs} seconds and we march." },
            ["prince/warn.cancel"] = new[] { "They're pulling back. We held.", "The war camp is breaking up. Phew.", "Strong enough. They won't dare now." },
            ["prince/warn.lost"] = new[] { "They got stronger. Our chance has passed, for now.", "The moment's gone. Keep building." },

            // first scene (REFACTOR_SPEC 2.C.5; the first line itself is FirstLine)
            ["prince/first.again"] = new[] { "Still here. Come with me: we're building a woodcutter's hut.", "The woodcutter's hut won't build itself. Shall we?", "Come on, the forest is just there. Let's build that hut.", "I need you for this. The hut, remember?" },
            ["prince/first.press"] = new[] { "Press the ground here, where it glows, to cast the spell." },
            ["prince/first.cast"] = new[] { "That's it! Builder, over here!" },
            ["prince/first.wood"] = new[] { "Here comes the wood for it.", "See the carrier? That's our wood arriving." },
            ["prince/first.done"] = new[] { "The hut stands. Watch it: the first wood comes out soon." },
            ["prince/first.busy"] = new[] { "The carriers are all busy. Our wood waits its turn.", "Patience: every carrier is out. The hut's wood is next." },
            ["prince/first.load"] = new[] { "There goes our first load of wood. Follow the carrier to the storehouse?" },
            ["prince/first.home"] = new[] { "That's how wood gets home: every load walks to the storehouse, and from there it builds everything else." },
            ["prince/first.notnow"] = new[] { "Another time, then. I'll find us the next job." },

            // ======== the sawyer ========
            ["sawyer/open/Calm"] = new[] { "Oi, a moment.", "Got a minute?", "Sawdust and good news.", "You there, about wood." },
            ["sawyer/open/Keen"] = new[] { "Me again, about the wood.", "Still thinking about it?", "Don't mind me asking twice." },
            ["sawyer/open/Pressing"] = new[] { "Logs won't cut themselves.", "Look, we need this.", "I'm asking again, and I'll keep asking." },
            ["sawyer/open/Urgent"] = new[] { "Quick, the Ash Clan's coming!", "No time to lose!", "Hurry, friend!" },
            ["sawyer/body/hut"] = new[] { "Another woodcutter's hut {where}, and we'd never run short of wood.", "Give me a woodcutter's hut {where} and I'll double the logs.", "The forest {where} wants a woodcutter's hut." },
            ["sawyer/body/sawmill"] = new[] { "Give me a sawmill {where} and I'll give you planks.", "Logs are piling up. A sawmill {where} turns them into planks.", "Planks build the fine things. A sawmill {where}, please." },
            ["sawyer/lead"] = new[] { "Follow me, it's by the trees.", "This way.", "Come on, mind the stumps." },
            ["sawyer/wait"] = new[] { "Oi, this way!", "Over here!", "Keep up!" },
            ["sawyer/fetch"] = new[] { "You took a wrong turn. Back this way.", "Lost? Follow the sound of the saw." },
            ["sawyer/press"] = new[] { "Here. Press the ground where it shines.", "Right by my feet. Press the ground." },
            ["sawyer/nudge"] = new[] { "Go on, press it.", "Right here, friend.", "The ground, there. Press it." },
            ["sawyer/cast"] = new[] { "Ha! That's the stuff.", "Good! I'll sharpen the saws.", "Now we're cutting." },
            ["sawyer/decline"] = new[] { "Suit yourself. Ask the prince, then.", "Fine, fine. Another time." },
            ["sawyer/later"] = new[] { "Later. I'll be at the woodpile.", "I'll hold you to that." },
            ["sawyer/resigned"] = new[] { "Right. I'll stop pestering. For now.", "I'll just walk behind you, quiet as a log." },
            ["sawyer/idle"] = new[] { "Counting logs. Always counting logs.", "Sharpening my saw.", "Waiting for the next good tree." },

            // ======== the merchant ========
            ["merchant/open/Calm"] = new[] { "Friend! A proposition.", "Trade waits for no one.", "Listen, I have a deal for you.", "Ah, just the person I wanted." },
            ["merchant/open/Keen"] = new[] { "Still interested in my offer?", "About that road again.", "Think of the trade." },
            ["merchant/open/Pressing"] = new[] { "Every day without a road costs us.", "I'm not leaving until you hear me.", "The rival lays roads. Why don't we?" },
            ["merchant/open/Urgent"] = new[] { "The Ash Clan is coming. Quickly!", "No time for haggling!", "Now, before it's too late!" },
            ["merchant/body/road"] = new[] { "Lay {thing} and the goods will flow.", "With {thing} we can reach new land.", "{Thing}: that's where our future lies.", "Build {thing} and I'll fill it with trade." },
            ["merchant/lead"] = new[] { "Follow me, I know the way.", "This way to the road.", "Come, it's along the edge." },
            ["merchant/wait"] = new[] { "This way!", "Over here!", "Keep up, friend!" },
            ["merchant/fetch"] = new[] { "You went off the road. Come back.", "Lost? I'll walk you there." },
            ["merchant/press"] = new[] { "Press the ground along the edge here.", "Here. Press where the road will run." },
            ["merchant/nudge"] = new[] { "Go on, press it.", "Right along here.", "Here, where the road runs." },
            ["merchant/cast"] = new[] { "A road! Excellent business.", "Wonderful. The carriers will love it.", "That's money in the bank, so to speak." },
            ["merchant/decline"] = new[] { "Your loss. I'll find another deal.", "No? Well, there are other roads." },
            ["merchant/later"] = new[] { "Later. I'll keep the offer warm.", "Fine. I'll be back." },
            ["merchant/resigned"] = new[] { "I'll stop. But the offer stands.", "Fine, I'll just walk along." },
            ["merchant/idle"] = new[] { "Counting what we could trade.", "Waiting for the next road.", "Thinking about prices." },

            // ======== the builder (first scene) ========
            ["builder/called"] = new[] { "On my way!", "Coming, coming!" },
            ["builder/arrive"] = new[] { "Right. Wood first, then walls.", "Let's get this hut standing." },
            ["builder/idle"] = new[] { "Resting until the next site.", "Waiting for someone to cast a new site." },

            // ======== endings ========
            ["prince/end.win"] = new[] { "Look! The Ash Clan is laying down their spears.", "Their camps are ours now. The island is ours.", "You did this. Every hut and every road started with your spell." },
            ["prince/end.loss"] = new[] { "They came over the ridge with more than we could hold.", "Too little was built, too late. We are overrun.", "It's over. Our camps belong to the Ash Clan now." },
            ["prince/end.timewin"] = new[] { "The seasons have turned, and we are the stronger.", "The Ash Clan won't risk a fight now. We won." },
            ["prince/end.timeloss"] = new[] { "The seasons have turned, and the Ash Clan is the stronger.", "We held on, but they lead the island now." },
            ["prince/end.draw"] = new[] { "The seasons have turned, and neither side is stronger.", "A standoff. Neither people rules the island." },
            ["rival/end.loss"] = new[] { "Your camps are ours now, valley people.", "You built too little. The island belongs to the Ash Clan." },
            ["sawyer/end.win"] = new[] { "Ha! More trees for us!", "I'll carve this day into a plank." },
            ["merchant/end.win"] = new[] { "New roads, new trade! Splendid.", "Peace is very good for business." },
        };

        /// <summary>The bank for a speaker and topic, else the prince's, else null.</summary>
        public static string[] Bank(string speaker, string topic)
        {
            if (Banks.TryGetValue(speaker + "/" + topic, out var b)) return b;
            if (Banks.TryGetValue(NpcIds.Prince + "/" + topic, out b)) return b;
            return null;
        }

        /// <summary>Every line text in the banks (tests: originality and length checks).</summary>
        public static IEnumerable<string> AllLines() { foreach (var kv in Banks) foreach (var l in kv.Value) yield return l; }

        /// <summary>Number of banks (tests).</summary>
        public static int BankCount => Banks.Count;
    }

    /// <summary>
    /// One speaker's voice: picks lines from <see cref="PrinceLines"/> so that the same sentence is never said twice
    /// in a row by that speaker (and the same bank does not give the same line twice running).
    /// </summary>
    public sealed class Voice
    {
        public readonly string Speaker;
        private readonly Random _rng;
        private readonly Dictionary<string, int> _lastIndex = new Dictionary<string, int>();
        /// <summary>The last sentence this speaker said.</summary>
        public string Last { get; private set; }

        public Voice(string speaker, int seed) { Speaker = speaker; _rng = new Random(seed); }

        /// <summary>A line of a topic with its placeholders filled (null if no bank).</summary>
        public string Pick(string topic, IDictionary<string, string> args = null)
        {
            var bank = PrinceLines.Bank(Speaker, topic);
            if (bank == null || bank.Length == 0) return null;
            int i = PickIndex(topic, bank.Length);
            return Fill(bank[i], args);
        }

        private int PickIndex(string topic, int n)
        {
            int last = _lastIndex.TryGetValue(topic, out var l) ? l : -1;
            int i = _rng.Next(n);
            if (n > 1 && i == last) i = (i + 1 + _rng.Next(n - 1)) % n;
            _lastIndex[topic] = i;
            return i;
        }

        /// <summary>
        /// A pitch: an opener for the mood and a body for the kind of proposal, never equal to the last sentence said.
        /// </summary>
        public string Pitch(PitchMood mood, string kind, IDictionary<string, string> args)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                string open = Pick("open/" + mood, args);
                string body = Pick("body/" + kind, args) ?? Pick("body/collector", args);
                string line = string.IsNullOrEmpty(open) ? body : open + " " + body;
                if (line != Last) return line;
            }
            return Pick("body/" + kind, args) + " " + Pick("open/" + PitchMood.Keen, args);
        }

        /// <summary>Records what was said (the no-repeat rule).</summary>
        public void Said(string line) { Last = line; }

        /// <summary>
        /// Fills {key} placeholders; a capitalised key ({Thing}) gets the value with its first letter capitalised.
        /// </summary>
        public static string Fill(string text, IDictionary<string, string> args)
        {
            if (args == null || text.IndexOf('{') < 0) return text;
            foreach (var kv in args)
            {
                if (kv.Value == null) continue;
                text = text.Replace("{" + kv.Key + "}", kv.Value);
                string cap = kv.Key.Length > 0 ? char.ToUpperInvariant(kv.Key[0]) + kv.Key.Substring(1) : kv.Key;
                string capVal = kv.Value.Length > 0 ? char.ToUpperInvariant(kv.Value[0]) + kv.Value.Substring(1) : kv.Value;
                text = text.Replace("{" + cap + "}", capVal);
            }
            return text;
        }
    }
}
