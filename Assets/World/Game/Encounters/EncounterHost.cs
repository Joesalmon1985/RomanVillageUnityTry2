using System;
using System.Collections.Generic;
using Bedsit;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>A self-contained activity (Ward duel, rhythm phrase) run by the EncounterHost.</summary>
    public interface IEncounter
    {
        string Title { get; }
        /// <summary>Called once when the host starts it (clock already paused, avatar movement locked).</summary>
        void Begin(EncounterContext ctx);
        /// <summary>Called every frame with unscaled delta time. Read your own keys here.</summary>
        void Tick(float unscaledDt);
        /// <summary>Called from OnGUI every GUI event while running. Draw with UiStyle.</summary>
        void DrawGui();
        /// <summary>When true the host calls End and restores the village.</summary>
        bool IsFinished { get; }
        /// <summary>Called once, on finish or abort. Undo your scene changes here.</summary>
        void End();
    }

    /// <summary>Everything an encounter may touch in the village.</summary>
    public sealed class EncounterContext
    {
        public WorldBootstrap World;
        public WorldSim Sim;
        public GameClock Clock;
        /// <summary>The player's viewpoint (package P's walker). Encounters place it only through <see cref="PlaceAvatar"/>.</summary>
        public PlayerWalker Avatar;
        public ViewSwitcher Views;
        public Camera FirstPersonCamera;
        public Camera GodCamera;
        /// <summary>The NPC that offered the encounter (may be null).</summary>
        public Npc Npc;
        /// <summary>The NPC's body (may be null).</summary>
        public ICharacterActor NpcActor;
        /// <summary>Where the encounter happens: position + facing (yaw, degrees).</summary>
        public EncounterStage Stage;
        /// <summary>Set before or in Begin: if true the player keeps mouse look and the cursor stays locked. Default false (cursor free for GUI).</summary>
        public bool AllowMouseLook;
        /// <summary>True if the host is aborting (End called without IsFinished).</summary>
        public bool Aborted { get; internal set; }

        /// <summary>Moves the avatar onto the stage (restored automatically when the encounter ends).</summary>
        public void MoveAvatarToStage(float pitch = 0f) => PlaceAvatar(Stage.Position, Stage.Yaw, pitch);

        /// <summary>
        /// Puts the player's viewpoint at an exact pose for the encounter (restored automatically when it ends).
        /// Encounters call this (or <see cref="MoveAvatarToStage"/>) instead of touching <see cref="Avatar"/>, so package P
        /// can switch the viewpoint to the PlayerWalker (PlaceExact) without editing the encounters (REFACTOR_SPEC appendix B).
        /// </summary>
        public void PlaceAvatar(Vector3 position, float yaw, float pitch)
        {
            if (Avatar != null) Avatar.PlaceExact(position, yaw, pitch);
        }

        public static EncounterContext Create(Npc npc = null)
        {
            var b = WorldBootstrap.Instance;
            var ctx = new EncounterContext
            {
                World = b,
                Sim = b != null ? b.Sim : null,
                Clock = b != null ? b.Clock : null,
                Avatar = b != null ? b.Avatar : null,
                Views = b != null ? b.Views : null,
                FirstPersonCamera = b != null ? b.MainCamera : null,
                GodCamera = b != null && b.GodCam != null ? b.GodCam.Camera : null,
                Npc = npc,
                NpcActor = npc != null ? npc.Actor : null,
            };
            if (npc != null) ctx.Stage = npc.Stage;
            else if (ctx.Avatar != null) ctx.Stage = new EncounterStage(ctx.Avatar.Position, ctx.Avatar.Yaw);
            return ctx;
        }
    }

    /// <summary>
    /// Runs one encounter at a time. Start pauses the clock (PauseReasons.Encounter), stops any auto-walk, records the
    /// player's pose and locks the walker; each frame it calls Tick and DrawGui; on finish or Abort it calls End, puts
    /// the walker back (on its lattice cell and heading with <see cref="PlayerWalker.RestoreCell"/>, or at the exact
    /// pose if it stood off the lattice), unlocks and resumes the clock. F1 still switches to the God View during an
    /// encounter. Not a MonoBehaviour (a driver component pumps it).
    /// </summary>
    public sealed class EncounterHost
    {
        public const string AvatarLock = "Encounter";
        public static EncounterHost Instance { get; private set; }

        public IEncounter Current { get; private set; }
        public EncounterContext Context { get; private set; }
        public bool IsRunning => Current != null;

        public event Action<IEncounter> Started;
        /// <summary>Raised after End and restore. bool = aborted.</summary>
        public event Action<IEncounter, bool> Ended;

        private Vector3 _savedPos; private float _savedYaw, _savedPitch; private bool _savedOffLattice;

        internal static EncounterHost CreateInstance(GameObject host)
        {
            Instance = new EncounterHost();
            host.AddComponent<EncounterHostDriver>().Host = Instance;
            return Instance;
        }

        /// <summary>Starts an encounter. Returns false if one is already running.</summary>
        public bool Start(IEncounter encounter, EncounterContext ctx = null)
        {
            if (encounter == null || IsRunning) return false;
            ctx = ctx ?? EncounterContext.Create();
            DialoguePanel.Close();
            var av = ctx.Avatar;
            if (av != null)
            {
                av.StopAuto();
                _savedPos = av.Position; _savedYaw = av.Yaw; _savedPitch = av.Pitch; _savedOffLattice = av.IsOffLattice;
                av.AddLock(AvatarLock);
            }
            if (ctx.Clock != null) ctx.Clock.Pause(PauseReasons.Encounter);
            Current = encounter;
            Context = ctx;
            encounter.Begin(ctx);
            Started?.Invoke(encounter);
            return true;
        }

        /// <summary>Stops the running encounter now (counts as aborted).</summary>
        public void Abort()
        {
            if (!IsRunning) return;
            Finish(true);
        }

        internal void Tick(float dt)
        {
            if (!IsRunning) return;
            Current.Tick(dt);
            if (IsRunning && Current.IsFinished) Finish(false);
        }

        internal void DrawGui()
        {
            if (IsRunning) Current.DrawGui();
        }

        private void Finish(bool aborted)
        {
            var e = Current; var ctx = Context;
            ctx.Aborted = aborted;
            try { e.End(); }
            catch (Exception ex) { Debug.LogException(ex); }
            Current = null; Context = null;
            var av = ctx.Avatar;
            if (av != null)
            {
                if (_savedOffLattice) av.Teleport(_savedPos, _savedYaw, _savedPitch);
                else av.RestoreCell();
                av.RemoveLock(AvatarLock);
            }
            if (ctx.Clock != null) ctx.Clock.Resume(PauseReasons.Encounter);
            Ended?.Invoke(e, aborted);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }

    /// <summary>Pumps the EncounterHost from Unity's Update and OnGUI.</summary>
    public sealed class EncounterHostDriver : MonoBehaviour
    {
        public EncounterHost Host;
        private void Update() { if (Host != null) Host.Tick(Time.unscaledDeltaTime); }
        private void OnGUI()
        {
            if (Host == null || !Host.IsRunning) return;
            GUI.depth = -5;
            Host.DrawGui();
        }
    }

    /// <summary>Registry of encounters NPC dialogue can start by id ("ward_mira", "rhythm_party").</summary>
    public static class EncounterDirectory
    {
        public const string WardMira = "ward_mira", RhythmParty = "rhythm_party";
        private static readonly Dictionary<string, Func<IEncounter>> Factories = new Dictionary<string, Func<IEncounter>>();

        public static void Register(string id, Func<IEncounter> factory) => Factories[id] = factory;
        public static void Unregister(string id) => Factories.Remove(id);
        public static bool IsRegistered(string id) => id != null && Factories.ContainsKey(id);

        /// <summary>Creates and starts the encounter registered under id. False if missing or one is running.</summary>
        public static bool TryStart(string id, Npc source = null)
        {
            if (!Factories.TryGetValue(id, out var f) || EncounterHost.Instance == null) return false;
            var enc = f();
            return enc != null && EncounterHost.Instance.Start(enc, EncounterContext.Create(source));
        }
    }

    /// <summary>Test-only encounter: finishes after three presses of Space (or three calls to Press).</summary>
    public sealed class DebugEncounter : IEncounter
    {
        public int Presses { get; private set; }
        public bool Began { get; private set; }
        public bool Ended { get; private set; }
        public int Ticks { get; private set; }
        public string Title => "Debug encounter";
        public bool IsFinished => Presses >= 3;

        public void Begin(EncounterContext ctx) { Began = true; }
        public void Press() => Presses++;
        public void Tick(float unscaledDt)
        {
            Ticks++;
            if (Input.GetKeyDown(KeyCode.Space)) Press();
        }
        public void DrawGui()
        {
            var r = new Rect(UiStyle.Px(40), UiStyle.Px(140), UiStyle.Px(520), UiStyle.Px(110));
            UiStyle.DrawPanel(r);
            GUI.Label(new Rect(r.x + UiStyle.Px(16), r.y + UiStyle.Px(12), r.width, UiStyle.Px(44)), Title, UiStyle.Title);
            GUI.Label(new Rect(r.x + UiStyle.Px(16), r.y + UiStyle.Px(60), r.width, UiStyle.Px(36)), "Press Space (" + Presses + "/3)", UiStyle.Label);
        }
        public void End() { Ended = true; }
    }
}
