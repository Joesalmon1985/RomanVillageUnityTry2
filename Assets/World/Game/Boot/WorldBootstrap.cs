using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The only object in World.unity. Creates the WorldSim and builds the scene in code: clock, sun, camera, ground,
    /// the encounter host and AutoPlay, then calls the five package installers (REFACTOR_SPEC 8.0; the player walker
    /// and its UI come from <see cref="PlayerInstaller"/>). Owner after F-0: package P.
    /// World.Game never writes WorldState: read Sim.State, Submit commands, animate events.
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        public static WorldBootstrap Instance { get; private set; }

        [Tooltip("Map and RNG seed for a new game.")]
        public long Seed = 20261113;

        public bool ShowDebugOverlay = true;

        public WorldSim Sim { get; private set; }
        public GameClock Clock { get; private set; }
        public Camera MainCamera { get; private set; }
        public Light Sun { get; private set; }
        public GameObject Ground { get; private set; }

        /// <summary>Sea plane side length in metres (the board is about 950 m across, the island about 1,100 m).</summary>
        public const float GroundSize = 6000f;
        /// <summary>Sea level (the island ground is at -0.05, the hex tiles at 0).</summary>
        public const float SeaY = -0.3f;
        public const float EyeHeight = 1.7f;

        /// <summary>Seed for the next scene load (set by <see cref="NewGame"/>); null keeps the serialized Seed.</summary>
        public static long? NextSeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NextSeed = null;

        /// <summary>
        /// Opening vista (owner, 21 Nov 2026): what a new game is when no developer start or save is pending: the god board hub
        /// (<see cref="GodBoardStart.Id"/>, as <c>-start dev.godboard</c>). The legacy prehistoric economy is the developer start
        /// <see cref="DevStart.LegacyId"/> (<c>-start dev.legacy</c>). Not reset per play session: the PlayMode test assembly sets it to
        /// the legacy start for the older tests; a packaged AutoPlay run without <c>-start</c> keeps the legacy game its scripts were written for.
        /// </summary>
        public static string DefaultStart = GodBoardStart.Id;

        /// <summary>The sim of a new game (no save, no developer start): <see cref="DefaultStart"/>, or the legacy game for a packaged AutoPlay script.</summary>
        public static WorldSim CreateNewGame(ulong seed)
        {
            bool legacy = DefaultStart == DevStart.LegacyId || (!Application.isEditor && AutoPlay.CommandLineScript() != null);
            var sim = legacy ? WorldSimFactory.CreateNew(seed) : GodBoardStart.Create(seed);
            Debug.Log("[World] new game: " + (legacy ? "legacy prehistoric economy" : "god board hub") + ", seed " + seed);
            return sim;
        }

        /// <summary>
        /// Starts a new game with a fresh seed: reloads the scene, so every view, panel and NPC is rebuilt from the new
        /// state alone (nothing is carried over from the old game).
        /// </summary>
        public void NewGame(long seed)
        {
            NextSeed = seed;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        private void Awake()
        {
            Instance = this;
            if (NextSeed.HasValue) { Seed = NextSeed.Value; NextSeed = null; }
            // Sanctioned load path: a save picked up from SaveSystem.Load becomes this scene's sim; every view below
            // is built from it exactly as from a new game.
            var loaded = SaveSystem.TakePending();
            if (loaded != null)
            {
                Sim = World.Sim.Save.SaveCodec.CreateSim(loaded.State);
                Seed = unchecked((long)loaded.State.Seed);
            }
            else if ((Sim = DevStart.TakePending(unchecked((ulong)Seed))) != null) Seed = unchecked((long)Sim.State.Seed); // F1 / -start developer starts
            else Sim = CreateNewGame(unchecked((ulong)Seed)); // opening vista: the god board hub by default

            Clock = gameObject.AddComponent<GameClock>();
            Clock.Bind(Sim);

            BuildSun();
            BuildGround();
            BuildCamera();
            BuildViewpoint();

            // The packages' entry points, in the order of REFACTOR_SPEC 8.0 / appendix B. Each installer belongs to its
            // package; nothing else in this method should change when a package lands.
            PlayerInstaller.Install(this);   // P: walker, pointer, speech bubbles, spell caster, map, HUD toggle
            CampInstaller.Install(this);     // D: dance circle, camp dressing, Mira and Sol
            LandViewsInstaller.Install(this); // V: terrain and land views
            GradeViewsInstaller.Install(this); // ages H1-5: grade visuals, towns (shell)
            GodBoardInstaller.Install(this); // G1-5: temples and the god board (GodBoard games only)
            LightingInstaller.Install(this); // L: day-night cycle, fires and torches, night readability (after V: it dresses the sky and the views)
            PrinceInstaller.Install(this);   // N: prince, sawyer, merchant, first scene
            InteriorsInstaller.Install(this); // crystals I5: interiors (shell)
            ExpeditionInstaller.Install(this); // crystals I6: the expedition director (shell)
            CouncilDirector.Install(this);   // council: the village election (approach, gathering, encounter); before the F1 tools so its tool registers
            HubInstaller.Install(this);      // WU-1a: the hub clearing and its destinations (temple forecourt, council steward, healer's lodge, expedition camp, duel ring), god board games only
            DevToolsInstaller.Install(this); // P: F1 developer tools

            Saves = gameObject.AddComponent<SaveSystem>(); // F5 / F9, one slot; applies the loaded save's game-layer data in Start
            Saves.Bind(this, loaded);
            Debug.Log("[World] ready: " + (loaded != null ? "loaded save at tick " + Sim.State.Tick : "new game") + ", seed " + Seed + ", " + Screen.width + "x" + Screen.height);
        }

        /// <summary>
        /// One log line once the scene has run for two seconds (packaged launch checks read it): the tick, the time of
        /// day and the screen. "-quitAfterReady" quits the player then (a plain launch smoke run).
        /// </summary>
        private System.Collections.IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) yield return null;
            var dn = DayNight.Instance;
            Debug.Log("[Boot] playable: tick " + Sim.State.Tick + ", hour " + (dn != null ? dn.Hour.ToString("0.0") : "?") + ", night " + (dn != null ? dn.Night.ToString("0.00") : "?")
                + ", screen " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + ", fps " + (1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("0")
                + ", prince " + (PrinceDirector.Instance != null && PrinceDirector.Instance.Prince != null) + ", villagers " + Npc.All.Count);
            if (!Application.isEditor && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-quitAfterReady") >= 0) Application.Quit(0);
        }

        /// <summary>The save slot (F5 save, F9 load).</summary>
        public SaveSystem Saves { get; private set; }

        // ---- The viewpoint (package P: PlayerInstaller creates the walker, the F1 God View and the UI) ----

        /// <summary>The player's viewpoint on the walking lattice (the old name "Avatar" is kept for callers).</summary>
        public PlayerWalker Avatar { get; private set; }
        public PlayerWalker Walker => Avatar;
        /// <summary>The F1 developer view's camera.</summary>
        public GodCamera GodCam { get; private set; }
        public ViewSwitcher Views { get; private set; }
        public Hud Hud { get; private set; }
        public AutoPlay AutoPlay { get; private set; }
        public EncounterHost Encounters { get; private set; }
        /// <summary>Parent for characters created by ActorFactory.</summary>
        public Transform ActorsRoot { get; private set; }

        internal void SetViewpoint(PlayerWalker walker, GodCamera god, ViewSwitcher views) { Avatar = walker; GodCam = god; Views = views; }
        internal void SetHud(Hud hud) => Hud = hud;

        private void BuildViewpoint()
        {
            Ground.transform.position = new Vector3(0, SeaY, 0);
            Ground.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Create(new Color(0.22f, 0.42f, 0.55f), "Sea");
            ActorsRoot = new GameObject("Actors").transform;
            ActorsRoot.SetParent(transform, false);
            Encounters = EncounterHost.CreateInstance(gameObject);
            AutoPlay = gameObject.AddComponent<AutoPlay>();
            ShowDebugOverlay = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildSun()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.96f, 0.88f);
            Sun.intensity = 1.1f;
            Sun.shadows = LightShadows.Soft;
            RenderSettings.sun = Sun;
        }

        private void BuildGround()
        {
            Ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m mesh
            Ground.name = "Ground";
            Ground.transform.SetParent(transform, false);
            Ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
            Ground.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Create(new Color(0.36f, 0.50f, 0.24f), "Ground");
        }

        private void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            MainCamera = go.AddComponent<Camera>();
            MainCamera.clearFlags = CameraClearFlags.Skybox;
            MainCamera.nearClipPlane = 0.1f;
            MainCamera.farClipPlane = 1500f;
            MainCamera.fieldOfView = 70f;
            go.AddComponent<AudioListener>();

            // Stand at the player's start corner looking north-west across the board (the avatar takes over at once).
            var topo = Sim.State.Board.Topology;
            int home = Sim.State.Board.StartCorners[0];
            go.transform.position = new Vector3((float)topo.VertexX(home), EyeHeight, (float)topo.VertexZ(home));
            go.transform.rotation = Quaternion.Euler(5f, -45f, 0f);
        }

        private void OnGUI()
        {
            if (!ShowDebugOverlay || Sim == null) return;
            var r = new Rect(UiStyle.Px(20), UiStyle.Px(16), UiStyle.Px(700), UiStyle.Px(44));
            string state = Clock != null && Clock.IsPaused ? "paused" : "running";
            UiStyle.ShadowLabel(r, $"WorldMVP  tick {Sim.State.Tick}  ({state})", UiStyle.Small);
        }
    }
}
