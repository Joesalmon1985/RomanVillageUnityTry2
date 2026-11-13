using System;
using System.Collections.Generic;
using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>Well-known NPC ids (see Docs/GAME_API.md for where they stand).</summary>
    public static class NpcIds
    {
        public const string Mira = "mira", Doorman = "doorman", Host = "host", Quartermaster = "quartermaster",
            MarketKeeper = "market_keeper", Captain = "captain", Scribe = "scribe";
    }

    /// <summary>A position and facing where an encounter happens.</summary>
    public struct EncounterStage
    {
        public Vector3 Position;
        public float Yaw;
        public Vector3 Forward => BoardSpace.DirOf(Yaw);
        public EncounterStage(Vector3 position, float yaw) { Position = position; Yaw = yaw; }
    }

    /// <summary>
    /// A villager: within <see cref="GreetRange"/> m it turns to face the player and shows its name and
    /// greeting; E opens the DialoguePanel with <see cref="Lines"/> and up to three <see cref="Options"/>.
    /// Later agents change Lines/Options (or set <see cref="DialogueBuilder"/>) to wire behaviour.
    /// </summary>
    public sealed class Npc : MonoBehaviour, IInteractable, IInspectable, ISpeaker, IFollowable
    {
        public const float GreetRange = 4f;
        private static readonly List<Npc> _all = new List<Npc>();
        public static IReadOnlyList<Npc> All => _all;

        public string Id;
        public string DisplayName;
        public string Role;
        public string Greeting;
        public List<string> Lines = new List<string>();
        public List<DialogueOption> Options = new List<DialogueOption>();
        /// <summary>Optional: rebuilds Lines/Options each time the dialogue opens.</summary>
        public Action<Npc> DialogueBuilder;
        /// <summary>Where this NPC's encounter takes place (defaults to in front of the NPC).</summary>
        public EncounterStage Stage;

        public ICharacterActor Actor { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public float HomeYaw { get; private set; }
        /// <summary>True while the player is within GreetRange (first person, no encounter running).</summary>
        public bool IsGreetingShown { get; private set; }

        public string Prompt => "Talk to " + DisplayName;
        public string Title => DisplayName;
        IEnumerable<string> IInspectable.Lines
        {
            get
            {
                if (!string.IsNullOrEmpty(Role)) yield return Role;
                if (!string.IsNullOrEmpty(Greeting)) yield return "\"" + Greeting + "\"";
            }
        }

        // ---- REFACTOR_SPEC appendix B (owner: package N). F-0 gives a minimal straight-line walk so P and D can use it. ----

        /// <summary>Walking speed in metres per second (F-0 placeholder; N tunes it).</summary>
        public float WalkSpeed = 3f;
        public bool IsWalking { get; private set; }
        public string SpeakerId => Id;
        public string FollowName => DisplayName;
        public Transform FollowTransform => transform;
        public Transform Head
        {
            get
            {
                if (_head == null)
                {
                    _head = new GameObject("Head").transform;
                    _head.SetParent(transform, false);
                    _head.localPosition = Vector3.up * 2.15f;
                }
                return _head;
            }
        }
        private Transform _head;
        private Vector3? _walkTarget;
        private Transform _followTarget;
        private float _followDistance;

        /// <summary>Walks in a straight line to a ground point (F-0: no path finding, no blockers).</summary>
        public void WalkTo(Vector3 target) { _followTarget = null; _walkTarget = target; IsWalking = true; }

        /// <summary>Keeps <paramref name="distance"/> metres behind a moving transform until <see cref="WalkTo"/> or <see cref="StopWalking"/>.</summary>
        public void Follow(Transform target, float distance) { _walkTarget = null; _followTarget = target; _followDistance = distance; }

        public void StopWalking() { _walkTarget = null; _followTarget = null; IsWalking = false; }

        private void UpdateWalk()
        {
            Vector3? goal = _walkTarget;
            float stop = 0.05f;
            if (_followTarget != null) { goal = _followTarget.position; stop = _followDistance; }
            if (!goal.HasValue) { IsWalking = false; return; }
            var d = goal.Value - transform.position; d.y = 0;
            float dist = d.magnitude;
            if (dist <= stop)
            {
                IsWalking = false;
                if (_walkTarget.HasValue) { HomePosition = transform.position; _walkTarget = null; }
                return;
            }
            IsWalking = true;
            float step = Mathf.Min(WalkSpeed * Time.deltaTime, dist - stop);
            transform.position += d / dist * step;
            Actor?.FaceTowards(transform.position + d);
        }

        public static Npc Find(string id)
        {
            foreach (var n in _all) if (n != null && n.Id == id) return n;
            return null;
        }

        public static Npc Spawn(string id, string name, string role, string greeting, Color tunic, Vector3 pos, float yaw)
        {
            var actor = ActorFactory.Create(id, tunic, pos, yaw);
            var npc = actor.Root.gameObject.AddComponent<Npc>();
            npc.Id = id; npc.DisplayName = name; npc.Role = role; npc.Greeting = greeting;
            npc.Actor = actor;
            npc.HomePosition = pos; npc.HomeYaw = yaw;
            npc.Stage = new EncounterStage(pos + BoardSpace.DirOf(yaw) * 3f, yaw + 180f);
            actor.Root.name = "NPC " + name;
            return npc;
        }

        /// <summary>Moves the NPC (and its home) to a new spot.</summary>
        public void PlaceAt(Vector3 pos, float yaw)
        {
            HomePosition = pos; HomeYaw = yaw;
            transform.position = pos;
            if (Actor is PlaceholderActor p) p.SetYaw(yaw); else transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        public void Interact()
        {
            DialogueBuilder?.Invoke(this);
            DialoguePanel.Open(this);
        }

        private bool _wasNear;

        private void Update()
        {
            UpdateWalk();
            if (IsWalking) return;
            var views = ViewSwitcher.Instance;
            if (views == null || Actor == null) return;
            var player = views.Avatar.Position;
            var d = player - transform.position; d.y = 0;
            bool near = d.magnitude <= GreetRange;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            IsGreetingShown = near && !encounter;
            if (encounter) { _wasNear = false; return; }
            if (near)
            {
                Actor.FaceTowards(player);
                if (!_wasNear) Actor.PlayOneShot(ActorKeys.Wave);
            }
            else if (_wasNear)
            {
                Actor.FaceTowards(transform.position + BoardSpace.DirOf(HomeYaw));
            }
            _wasNear = near;
        }

        private void OnGUI()
        {
            var views = ViewSwitcher.Instance;
            if (views == null || Hud.MenuOpen || DialoguePanel.IsOpen) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            var cam = views.ActiveCamera;
            if (cam == null) return;
            var head = transform.position + Vector3.up * 2.15f;
            var sp = cam.WorldToScreenPoint(head);
            if (sp.z <= 0) return;
            float x = sp.x, y = Screen.height - sp.y;
            if (views.CurrentView == ViewMode.God)
            {
                // Names only when zoomed in on the village; at map scale they would pile up on one spot.
                if (views.God != null && views.God.Size > GodCamera.NameplateMaxSize) return;
                var r = new Rect(x - UiStyle.Px(150), y - UiStyle.Px(34), UiStyle.Px(300), UiStyle.Px(30));
                var s = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter };
                UiStyle.ShadowLabel(r, DisplayName, s);
                return;
            }
            float dist = (views.Avatar.Position - transform.position).magnitude;
            if (dist > 32f) return;
            var nameStyle = new GUIStyle(UiStyle.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            nameStyle.normal.textColor = UiStyle.AccentColor;
            UiStyle.ShadowLabel(new Rect(x - UiStyle.Px(250), y - UiStyle.Px(40), UiStyle.Px(500), UiStyle.Px(36)), DisplayName, nameStyle);
            if (IsGreetingShown && !string.IsNullOrEmpty(Greeting))
            {
                var gs = new GUIStyle(UiStyle.Label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                var content = new GUIContent(Greeting);
                float w = UiStyle.Px(520);
                float h = gs.CalcHeight(content, w - UiStyle.Px(24)) + UiStyle.Px(16);
                var box = new Rect(x - w / 2, y - UiStyle.Px(48) - h, w, h);
                UiStyle.DrawPanel(box);
                GUI.Label(new Rect(box.x + UiStyle.Px(12), box.y + UiStyle.Px(8), box.width - UiStyle.Px(24), box.height), content, gs);
            }
        }
    }
}
