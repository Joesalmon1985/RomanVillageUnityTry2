using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>One dialogue choice. If EncounterId is set, choosing it starts that encounter via EncounterDirectory.</summary>
    public sealed class DialogueOption
    {
        public string Label;
        public Action OnChoose;
        public string EncounterId;
        /// <summary>Close the dialogue before running the callback (default true).</summary>
        public bool CloseOnChoose = true;

        public bool IsAvailable => EncounterId == null || EncounterDirectory.IsRegistered(EncounterId);
        public string ShownLabel => IsAvailable ? Label : Label + "  (Not available yet)";

        public static DialogueOption Do(string label, Action onChoose) => new DialogueOption { Label = label, OnChoose = onChoose };
        public static DialogueOption Encounter(string label, string encounterId) => new DialogueOption { Label = label, EncounterId = encounterId };
        public static DialogueOption Leave(string label = "Leave") => new DialogueOption { Label = label };
    }

    /// <summary>
    /// An NPC's lines and up to three options, shown as a speech bubble above the NPC with the options as tappable
    /// replies (package P: the old bottom panel became <see cref="SpeechBubbles"/>; this class keeps the old surface so
    /// <c>Npc.Interact</c>, encounters and scripts keep working). Keys 1-3 or a tap on a reply choose; Esc or walking
    /// more than <see cref="WalkAwayDistance"/> m away closes it. A walking villager also offers "Follow" when there is
    /// room. It never holds the player or pauses the clock.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        public const int MaxOptions = 3;
        public const float WalkAwayDistance = 30f;
        public const string FollowLabel = "Follow";
        public static DialoguePanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        public string Speaker { get; private set; }
        public Npc SpeakerNpc { get; private set; }
        public IReadOnlyList<string> CurrentLines => _lines;
        public IReadOnlyList<DialogueOption> CurrentOptions => _options;
        /// <summary>The speech bubble showing the dialogue (0 when closed).</summary>
        public static int Handle => Instance != null && Instance._open ? Instance._handle : 0;

        public static event Action<DialoguePanel> Opened, Closed;

        private bool _open;
        private int _handle;
        private readonly List<string> _lines = new List<string>();
        private readonly List<DialogueOption> _options = new List<DialogueOption>();

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Opened = null; Closed = null; }

        public static void Open(Npc npc)
        {
            if (npc == null) return;
            Open(npc.DisplayName, npc.Lines.Count > 0 ? npc.Lines : new List<string> { npc.Greeting }, npc.Options, npc);
        }

        public static void Open(string speaker, IList<string> lines, IList<DialogueOption> options, Npc npc = null)
        {
            var p = Instance;
            if (p == null) return;
            if (p._open) Close();
            p.Speaker = speaker; p.SpeakerNpc = npc;
            p._lines.Clear(); if (lines != null) p._lines.AddRange(lines);
            p._options.Clear();
            if (options != null) for (int i = 0; i < options.Count && i < MaxOptions; i++) p._options.Add(options[i]);
            if (npc != null && npc.IsWalking && p._options.Count < MaxOptions)
                p._options.Add(DialogueOption.Do(FollowLabel, () => { if (PlayerWalker.Instance != null) PlayerWalker.Instance.Follow(npc); }));
            if (p._options.Count == 0) p._options.Add(DialogueOption.Leave());
            p._open = true;
            var replies = new List<string>();
            foreach (var o in p._options) replies.Add(o.ShownLabel);
            string text = string.Join("\n", p._lines);
            p._handle = SpeechBubbles.Ask(npc, text, replies, i => p.OnBubbleReply(i), 0f);
            SpeechBubbles.Expand(p._handle); // the player asked for it: show the replies at once
            if (npc != null && npc.Actor != null) npc.Actor.PlayOneShot(ActorKeys.Greet);
            Opened?.Invoke(p);
        }

        private void OnBubbleReply(int index)
        {
            if (!_open) return;
            _handle = 0; // the bubble closed itself
            if (index < 0) { Close(); return; }
            if (!Choose(index)) Close();
        }

        public static void Close()
        {
            var p = Instance;
            if (p == null || !p._open) return;
            p._open = false;
            if (p._handle != 0) SpeechBubbles.Close(p._handle);
            p._handle = 0;
            Closed?.Invoke(p);
        }

        /// <summary>Chooses option index (0-based). Returns false if out of range or unavailable.</summary>
        public static bool Choose(int index)
        {
            var p = Instance;
            if (p == null || !p._open || index < 0 || index >= p._options.Count) return false;
            var o = p._options[index];
            if (!o.IsAvailable) return false;
            var npc = p.SpeakerNpc;
            if (o.CloseOnChoose || o.EncounterId != null) Close();
            o.OnChoose?.Invoke();
            if (o.EncounterId != null) EncounterDirectory.TryStart(o.EncounterId, npc);
            return true;
        }

        private void Update()
        {
            if (!_open) return;
            if (_handle != 0 && !SpeechBubbles.IsOpen(_handle)) { _handle = 0; Close(); return; }
            var w = PlayerWalker.Instance;
            if (SpeakerNpc != null && w != null && BoardSpace.Flat(SpeakerNpc.transform.position - w.Position) > WalkAwayDistance) Close();
        }
    }
}
