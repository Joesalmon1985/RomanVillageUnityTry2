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
        /// <summary>Close the panel before running the callback (default true).</summary>
        public bool CloseOnChoose = true;

        public bool IsAvailable => EncounterId == null || EncounterDirectory.IsRegistered(EncounterId);
        public string ShownLabel => IsAvailable ? Label : Label + "  (Not available yet)";

        public static DialogueOption Do(string label, Action onChoose) => new DialogueOption { Label = label, OnChoose = onChoose };
        public static DialogueOption Encounter(string label, string encounterId) => new DialogueOption { Label = label, EncounterId = encounterId };
        public static DialogueOption Leave(string label = "Leave") => new DialogueOption { Label = label };
    }

    /// <summary>
    /// IMGUI dialogue box at the bottom of the screen: speaker, a few lines and up to three options
    /// (keys 1/2/3 or click). Esc (handled by the Hud) or a Leave option closes it. It frees the cursor
    /// and holds the avatar still while open; it does not pause the village clock.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        public const string AvatarLock = "Dialogue";
        public const int MaxOptions = 3;
        public static DialoguePanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        public string Speaker { get; private set; }
        public Npc SpeakerNpc { get; private set; }
        public IReadOnlyList<string> CurrentLines => _lines;
        public IReadOnlyList<DialogueOption> CurrentOptions => _options;

        public static event Action<DialoguePanel> Opened, Closed;

        private bool _open;
        private readonly List<string> _lines = new List<string>();
        private readonly List<DialogueOption> _options = new List<DialogueOption>();

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Open(Npc npc)
        {
            if (npc == null) return;
            Open(npc.DisplayName, npc.Lines.Count > 0 ? npc.Lines : new List<string> { npc.Greeting }, npc.Options, npc);
        }

        public static void Open(string speaker, IList<string> lines, IList<DialogueOption> options, Npc npc = null)
        {
            var p = Instance;
            if (p == null) return;
            p.Speaker = speaker; p.SpeakerNpc = npc;
            p._lines.Clear(); if (lines != null) p._lines.AddRange(lines);
            p._options.Clear();
            if (options != null) for (int i = 0; i < options.Count && i < MaxOptions; i++) p._options.Add(options[i]);
            if (p._options.Count == 0) p._options.Add(DialogueOption.Leave());
            p._open = true;
            var av = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.Avatar : null;
            if (av != null) av.AddLock(AvatarLock);
            if (npc != null && npc.Actor != null) npc.Actor.PlayOneShot(ActorKeys.Greet);
            Opened?.Invoke(p);
        }

        public static void Close()
        {
            var p = Instance;
            if (p == null || !p._open) return;
            p._open = false;
            var av = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.Avatar : null;
            if (av != null) av.RemoveLock(AvatarLock);
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
            for (int i = 0; i < _options.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Choose(i); return; }
        }

        private void OnGUI()
        {
            if (!_open) return;
            GUI.depth = -10;
            float w = UiStyle.Px(1100);
            float pad = UiStyle.Px(22);
            var label = UiStyle.Label;
            float textH = 0;
            foreach (var l in _lines) textH += label.CalcHeight(new GUIContent(l), w - 2 * pad) + UiStyle.Px(6);
            float btnH = UiStyle.Px(54);
            float h = pad + UiStyle.Px(50) + textH + UiStyle.Px(12) + _options.Count * (btnH + UiStyle.Px(8)) + UiStyle.Px(40) + pad;
            var r = new Rect((Screen.width - w) / 2, Screen.height - h - UiStyle.Px(40), w, h);
            UiStyle.DrawSolidPanel(r);
            float y = r.y + pad;
            GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, UiStyle.Px(48)), Speaker, UiStyle.Title);
            y += UiStyle.Px(50);
            foreach (var l in _lines)
            {
                float lh = label.CalcHeight(new GUIContent(l), w - 2 * pad);
                GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, lh), l, label);
                y += lh + UiStyle.Px(6);
            }
            y += UiStyle.Px(12);
            var btn = new GUIStyle(UiStyle.Button) { alignment = TextAnchor.MiddleLeft };
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                bool was = GUI.enabled;
                GUI.enabled = o.IsAvailable;
                if (GUI.Button(new Rect(r.x + pad, y, w - 2 * pad, btnH), (i + 1) + "   " + o.ShownLabel, btn)) Choose(i);
                GUI.enabled = was;
                y += btnH + UiStyle.Px(8);
            }
            var hint = new GUIStyle(UiStyle.Small); hint.normal.textColor = new Color(0.8f, 0.78f, 0.72f);
            GUI.Label(new Rect(r.x + pad, y + UiStyle.Px(4), w - 2 * pad, UiStyle.Px(32)), "Press 1-" + _options.Count + " or click.  Esc to leave.", hint);
        }
    }
}
