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
        /// <summary>Package N's villagers (the speakers match <c>World.Sim.Factions.Speakers</c>).</summary>
        public const string Prince = "prince", Sawyer = "sawyer", Merchant = "merchant", Builder = "builder", RivalChief = "ash_chief";
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
        /// <summary>Greets within this range (6 m: one 5 m lattice step away counts as near).</summary>
        public const float GreetRange = 6f;
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

        // ---- REFACTOR_SPEC appendix B (owner: package N): walking on the lattice, speaker, followable. ----

        /// <summary>Strolling speed in metres per second (villagers about their business).</summary>
        public const float StrollSpeed = 3f;
        /// <summary>Walking speed in metres per second; directors raise it to hurry (see PrinceDirector.HurrySpeed).</summary>
        public float WalkSpeed = StrollSpeed;
        public bool IsWalking { get; private set; }
        /// <summary>
        /// True while a director (package N: <see cref="PrinceDirector"/>) controls this villager: it then does not turn
        /// to greet the player or show its greeting box on its own.
        /// </summary>
        public bool Directed;
        /// <summary>While true the villager stands still where it is (a director holds it while the clock is paused).</summary>
        public bool Hold;
        /// <summary>Optional tap handler: replaces the dialogue when the player taps this villager.</summary>
        public Action<Npc> OnTapped;
        /// <summary>Optional: what this villager is doing, said in a bubble when tapped (used when it has no Lines or Options).</summary>
        public Func<string> Doing;
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
        private readonly List<Vector3> _waypoints = new List<Vector3>();
        private Vector3 _plannedFor;
        private float _replanAt;
        /// <summary>Metres walked since spawn (tests, pace numbers).</summary>
        public float MetresWalked { get; private set; }
        /// <summary>Where the current walk ends (the followed transform's position while following); null when standing.</summary>
        public Vector3? Goal => _followTarget != null ? _followTarget.position : _walkTarget;

        /// <summary>
        /// Walks to a ground point along the walking lattice (round blocked cells: camps, buildings, peaks), ending
        /// exactly on the point. Falls back to a straight line when the lattice has no way.
        /// </summary>
        public void WalkTo(Vector3 target)
        {
            _followTarget = null; _walkTarget = target;
            Plan(target);
            IsWalking = true;
        }

        /// <summary>Keeps within <paramref name="distance"/> metres of a moving transform (re-planned on the lattice as it moves) until <see cref="WalkTo"/> or <see cref="StopWalking"/>.</summary>
        public void Follow(Transform target, float distance)
        {
            if (_followTarget == target && Mathf.Abs(_followDistance - distance) < 0.01f) return;
            _walkTarget = null; _followTarget = target; _followDistance = distance;
            _waypoints.Clear(); _replanAt = 0f;
        }

        /// <summary>The transform followed (null when not following).</summary>
        public Transform FollowingTarget => _followTarget;

        public void StopWalking() { _walkTarget = null; _followTarget = null; _waypoints.Clear(); IsWalking = false; }

        /// <summary>Turns to face a point (the body turns smoothly).</summary>
        public void FaceTowards(Vector3 point) { Actor?.FaceTowards(point); }

        /// <summary>Raises an arm towards a point and keeps it there (null lowers it).</summary>
        public void PointAt(Vector3? point) { if (Actor is PlaceholderActor p) p.PointAt(point); }

        private void Plan(Vector3 target)
        {
            _waypoints.Clear();
            _plannedFor = target;
            var from = WalkLattice.ToCell(transform.position);
            var to = WalkLattice.ToCell(target);
            if (from != to)
            {
                var path = WalkGrid.FindPath(from, to, 6000);
                if (path == null)
                {
                    var free = WalkGrid.NearestFree(to, 3);
                    if (free.HasValue) path = WalkGrid.FindPath(from, free.Value, 6000);
                }
                if (path != null)
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (i == path.Count - 1 && path[i] == to) break; // the exact point ends the walk
                        _waypoints.Add(WalkLattice.ToWorld(path[i]));
                    }
            }
            _waypoints.Add(target);
        }

        private void UpdateWalk()
        {
            if (Hold) return;
            float stop = 0.05f;
            if (_followTarget != null)
            {
                var tp = _followTarget.position;
                stop = _followDistance;
                if (BoardSpace.Flat(tp - transform.position) <= stop) { _waypoints.Clear(); IsWalking = false; return; }
                if (_waypoints.Count == 0 || (Time.time >= _replanAt && BoardSpace.Flat(tp - _plannedFor) > 2.5f))
                {
                    Plan(tp);
                    _replanAt = Time.time + 0.4f;
                }
            }
            else if (!_walkTarget.HasValue) { IsWalking = false; return; }
            if (_waypoints.Count == 0)
            {
                IsWalking = false;
                if (_walkTarget.HasValue) { HomePosition = transform.position; _walkTarget = null; }
                return;
            }
            IsWalking = true;
            float budget = WalkSpeed * Time.deltaTime;
            var pos = transform.position;
            while (budget > 0f && _waypoints.Count > 0)
            {
                if (_followTarget != null && BoardSpace.Flat(_followTarget.position - pos) <= stop) break;
                var wp = _waypoints[0];
                var d = wp - pos; d.y = 0;
                float dist = d.magnitude;
                if (dist <= budget)
                {
                    pos = new Vector3(wp.x, pos.y, wp.z); budget -= dist; MetresWalked += dist;
                    _waypoints.RemoveAt(0);
                    if (dist > 0.01f) Actor?.FaceTowards(pos + d);
                    continue;
                }
                pos += d / dist * budget; MetresWalked += budget; budget = 0f;
                Actor?.FaceTowards(pos + d);
            }
            pos.y = PlayerWalker.GroundHeight(pos);
            transform.position = pos;
            if (_waypoints.Count == 0 && _walkTarget.HasValue)
            {
                IsWalking = false; HomePosition = transform.position; _walkTarget = null;
            }
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
            if (OnTapped != null) { OnTapped(this); return; }
            DialogueBuilder?.Invoke(this);
            if (Lines.Count == 0 && Options.Count == 0 && Doing != null)
            {
                SpeechBubbles.Say(this, Doing(), 4f);
                return;
            }
            DialoguePanel.Open(this);
        }

        private bool _wasNear;

        private void Update()
        {
            UpdateWalk();
            if (IsWalking || Directed) { _wasNear = false; IsGreetingShown = false; return; }
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

        /// <summary>True while one of this villager's speech bubbles is on screen (its name plate then hides).</summary>
        public bool HasShownBubble()
        {
            foreach (var b in SpeechBubbles.All)
                if (ReferenceEquals(b.Who, this) && SpeechBubbles.IsShown(b)) return true;
            return false;
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
            // A bubble already carries the speaker's name: no plate then (it sat on the bubble's tail).
            if (HasShownBubble()) return;
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
