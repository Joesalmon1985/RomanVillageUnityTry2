using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The only object in World.unity. Creates the WorldSim and builds the scene in code:
    /// clock, sun, camera, ground. Later milestones add their builders here (one call each).
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

        private void Awake()
        {
            Instance = this;
            Sim = WorldSimFactory.CreateNew(unchecked((ulong)Seed));

            Clock = gameObject.AddComponent<GameClock>();
            Clock.Bind(Sim);

            BuildSun();
            BuildGround();
            BuildCamera();
            BuildVillage();
            EconomyInstaller.Install(this); // road economy: views, God View tools, quartermaster (Docs/ECONOMY_UI.md)
        }

        // ---- Milestone 1: walkable village ----

        public VillageLayout Layout { get; private set; }
        public FirstPersonAvatar Avatar { get; private set; }
        public GodCamera GodCam { get; private set; }
        public ViewSwitcher Views { get; private set; }
        public Picker Picker { get; private set; }
        public WarehouseSet Warehouse { get; private set; }
        /// <summary>Placeholder rival outpost at Board.RivalOutpostVertex.</summary>
        public BuildingInfo Outpost { get; private set; }
        public Hud Hud { get; private set; }
        public AutoPlay AutoPlay { get; private set; }
        public EncounterHost Encounters { get; private set; }
        /// <summary>Parent for characters created by ActorFactory.</summary>
        public Transform ActorsRoot { get; private set; }

        private void BuildVillage()
        {
            var board = Sim.State.Board;
            Layout = new VillageLayout(board);
            TerrainBuilder.BuildSky(transform, Sun, MainCamera);
            Ground.transform.position = new Vector3(0, SeaY, 0);
            Ground.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Create(new Color(0.22f, 0.42f, 0.55f), "Sea");
            TerrainBuilder.Build(transform, board, Layout, out var overlay);
            VillageBuilder.Build(transform, board, Layout);
            Warehouse = WarehouseSet.Build(transform, Layout);
            Outpost = OutpostBuilder.Build(transform, board, Layout, overlay.transform);
            ActorsRoot = new GameObject("Actors").transform;
            ActorsRoot.SetParent(transform, false);

            Avatar = FirstPersonAvatar.Create(transform, MainCamera, Layout.Spawn + Vector3.up * 0.05f, Layout.SpawnYaw);
            Avatar.BoundsCentre = Vector3.zero;
            Avatar.BoundsRadius = BoardSpace.BoardRadius + 4f;
            GodCam = GodCamera.Create(transform);
            GodCam.ClampRadius = BoardSpace.BoardRadius;
            GodCam.MaxSize = GodCamera.SizeToFrame(BoardSpace.IslandRadius);
            GodCam.HomeFocus = Layout.VillageCentre;
            Views = gameObject.AddComponent<ViewSwitcher>();
            Views.Bind(Avatar, GodCam);
            Views.AddGodViewOnly(overlay);
            Picker = gameObject.AddComponent<Picker>();
            Picker.Bind(Views);
            gameObject.AddComponent<InspectPanel>();
            gameObject.AddComponent<DialoguePanel>();
            Hud = gameObject.AddComponent<Hud>();
            Encounters = EncounterHost.CreateInstance(gameObject);
            AutoPlay = gameObject.AddComponent<AutoPlay>();
            VillageNpcs.Spawn(this);
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

            // Stand at the home city (the village square) looking north-west across the board.
            var topo = Sim.State.Board.Topology;
            int home = Sim.State.Board.HomeCityVertex;
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
