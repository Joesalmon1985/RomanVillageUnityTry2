using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// PLACEHOLDER (package N; delete this folder when package V's land views land). Package V draws camps, plots,
    /// buildings, roads, carriers and the pulsing "cast here" ground mark (REFACTOR_SPEC 8.4); none of that is on N's
    /// branch, so the prince's scenes would point at empty grass. This draws the bare minimum from state, with plain
    /// primitives, only while <see cref="LandViews.Instance"/> is null (V creates it in its installer, which switches
    /// this off by itself):
    /// <list type="bullet">
    /// <item>a store pile and hearth on every finished camp corner (blue yours, red the Ash Clan's), a hollow frame
    /// on a camp site;</item>
    /// <item>a box per building (low frame while a site, half height while constructing, full with a roof when active),
    /// tapping one tells what it is;</item>
    /// <item>main roads and road sites as flat strips;</item>
    /// <item>carriers as small figures carrying a block in the ware's colour, at <c>EconomyApi.CarrierPosition</c>;
    /// they implement <see cref="IFollowable"/> and <see cref="IInspectable"/>, so a tap offers "Follow";</item>
    /// <item>a pulsing ring (a strip for roads) on the ground of every open proposal of your people, the top one
    /// brighter.</item>
    /// </list>
    /// Object names start with "NPlaceholder" so nothing collides with V's names or the boot test's checks.
    /// Nothing here writes state.
    /// </summary>
    public sealed class NPlaceholderViews : MonoBehaviour
    {
        public static NPlaceholderViews Instance { get; private set; }

        private static readonly Color Yours = new Color(0.25f, 0.45f, 0.85f), Theirs = new Color(0.80f, 0.22f, 0.18f);
        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f), Roof = new Color(0.62f, 0.50f, 0.30f);
        private static readonly Color MarkColour = new Color(0.45f, 1f, 0.55f, 1f);

        private WorldBootstrap _boot;
        private readonly Dictionary<int, GameObject> _settlements = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, string> _settlementKey = new Dictionary<int, string>();
        private readonly Dictionary<int, GameObject> _buildings = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, BuildingState> _buildingState = new Dictionary<int, BuildingState>();
        private readonly Dictionary<int, GameObject> _roads = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, RoadState> _roadState = new Dictionary<int, RoadState>();
        private readonly Dictionary<int, NPlaceholderCarrier> _carriers = new Dictionary<int, NPlaceholderCarrier>();
        private readonly Dictionary<int, GameObject> _marks = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Material> _markMats = new Dictionary<int, Material>();
        private int _topId = -1;
        private float _lastTickTime;
        private Transform _root;

        /// <summary>Creates the placeholder views when V's land views are missing; returns null otherwise.</summary>
        public static NPlaceholderViews InstallIfNeeded(WorldBootstrap boot)
        {
            if (LandViews.Instance != null) return null;
            var go = new GameObject("NPlaceholder views (package N, until V lands)");
            go.transform.SetParent(boot.transform, false);
            var v = go.AddComponent<NPlaceholderViews>();
            v._boot = boot;
            v._root = go.transform;
            boot.Clock.Ticked += v.OnTicked;
            v.Sync();
            return v;
        }

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        private WorldState S => _boot.Sim.State;

        /// <summary>The carrier figure (null if none).</summary>
        public NPlaceholderCarrier Carrier(int carrierId) => _carriers.TryGetValue(carrierId, out var c) ? c : null;
        /// <summary>The ground mark of an open proposal (null if none).</summary>
        public Transform ProposalMark(int proposalId) => _marks.TryGetValue(proposalId, out var m) && m != null ? m.transform : null;
        /// <summary>The building's box (null if none).</summary>
        public Transform Building(int buildingId) => _buildings.TryGetValue(buildingId, out var b) && b != null ? b.transform : null;

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            _lastTickTime = Time.time;
            if (LandViews.Instance != null) { Destroy(gameObject); return; } // V landed: step aside
            Sync();
        }

        private void Sync()
        {
            var s = S;
            var t = s.Board.Topology;
            // Camps.
            foreach (var st in EconomyApi.Settlements(s))
            {
                string key = st.State + "|" + st.IsCity + "|" + st.Upgrade;
                if (_settlementKey.TryGetValue(st.Id, out var k) && k == key) continue;
                if (_settlements.TryGetValue(st.Id, out var old) && old != null) Destroy(old);
                _settlementKey[st.Id] = key;
                if (st.State == SettlementState.Cancelled) { _settlements.Remove(st.Id); continue; }
                _settlements[st.Id] = BuildCamp(st, BoardSpace.Vertex(t, st.Corner));
            }
            // Buildings.
            foreach (var b in s.Economy.Buildings)
            {
                bool gone = b.State == BuildingState.Retired || b.State == BuildingState.Cancelled;
                if (_buildingState.TryGetValue(b.Id, out var was) && was == b.State) continue;
                _buildingState[b.Id] = b.State;
                if (_buildings.TryGetValue(b.Id, out var old) && old != null) Destroy(old);
                _buildings.Remove(b.Id);
                if (gone) continue;
                _buildings[b.Id] = BuildBuilding(b, t);
            }
            // Roads.
            foreach (var r in s.Economy.Roads)
            {
                if (_roadState.TryGetValue(r.Id, out var was) && was == r.State) continue;
                _roadState[r.Id] = r.State;
                if (_roads.TryGetValue(r.Id, out var old) && old != null) Destroy(old);
                _roads.Remove(r.Id);
                if (r.State == RoadState.Cancelled) continue;
                _roads[r.Id] = BuildRoad(r, t);
            }
            // Carriers (created once; positioned every frame).
            foreach (var c in s.Economy.Carriers)
                if (!_carriers.ContainsKey(c.Id)) _carriers[c.Id] = NPlaceholderCarrier.Create(_root, c.Id, c.Faction == PlayerIds.Human ? Yours : Theirs);
            // Proposal marks (your people only).
            var open = FactionApi.Proposals(s, PlayerIds.Human);
            var keep = new HashSet<int>();
            foreach (var p in open)
            {
                keep.Add(p.Id);
                if (!_marks.ContainsKey(p.Id)) _marks[p.Id] = BuildMark(p.Id);
            }
            var drop = new List<int>();
            foreach (var kv in _marks) if (!keep.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (int id in drop) { if (_marks[id] != null) Destroy(_marks[id]); _marks.Remove(id); _markMats.Remove(id); }
            var top = FactionApi.Top(s, PlayerIds.Human);
            _topId = top != null ? top.Id : -1;
        }

        private void Update()
        {
            if (_boot == null || _boot.Sim == null) return;
            var s = S;
            float frac = Mathf.Clamp01((Time.time - _lastTickTime) * WorldSim.TicksPerSecond);
            if (_boot.Clock.IsPaused) frac = 0f;
            foreach (var kv in _carriers)
            {
                if (kv.Value == null) continue;
                if (!EconomyApi.CarrierPosition(s, kv.Key, frac, out double x, out double z, out double yaw)) continue;
                var p = new Vector3((float)x, 0, (float)z);
                p.y = PlayerWalker.GroundHeight(p);
                kv.Value.Place(p, (float)yaw, s.Economy.Carriers[kv.Key]);
            }
            float pulse = 0.5f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var kv in _markMats)
            {
                bool top = kv.Key == _topId;
                var c = MarkColour; c.a = top ? pulse : 0.25f + 0.1f * pulse;
                kv.Value.color = c;
            }
        }

        // ---------------- builders ----------------

        private GameObject BuildCamp(Settlement st, Vector3 corner)
        {
            var go = new GameObject("NPlaceholder camp " + st.Id);
            go.transform.SetParent(_root, false);
            corner.y = PlayerWalker.GroundHeight(corner);
            go.transform.position = corner;
            var col = st.Faction == PlayerIds.Human ? Yours : Theirs;
            if (st.State == SettlementState.Site)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f;
                    Signs.Box(go.transform, "Frame", Quaternion.Euler(0, a, 0) * new Vector3(0, 0.3f, 2f), new Vector3(4f, 0.25f, 0.25f), Wood, false, a);
                }
                Signs.Box(go.transform, "Flag", new Vector3(0, 1.5f, 0), new Vector3(0.1f, 3f, 0.1f), col, false);
                return go;
            }
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Pile", new Vector3(0, 0.6f, 0), new Vector3(3.2f, 0.6f, 3.2f), MaterialFactory.Shared(Wood));
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Band", new Vector3(0, 1.25f, 0), new Vector3(3.3f, 0.08f, 3.3f), MaterialFactory.Shared(col));
            Signs.Prim(PrimitiveType.Sphere, go.transform, "Hearth", new Vector3(1.9f, 0.3f, 1.9f), new Vector3(0.7f, 0.5f, 0.7f), MaterialFactory.CreateGlow(new Color(1f, 0.55f, 0.15f, 0.9f), "NPlaceholderFire"));
            Signs.Box(go.transform, "Pole", new Vector3(0, 3f, 0), new Vector3(0.15f, 4.5f, 0.15f), Wood, false);
            Signs.Box(go.transform, "Pennant", new Vector3(0.6f, 4.8f, 0), new Vector3(1.2f, 0.7f, 0.05f), col, false);
            if (st.IsCity)
                Signs.Prim(PrimitiveType.Cylinder, go.transform, "Wall", new Vector3(0, 0.5f, 0), new Vector3(5.5f, 0.5f, 5.5f), MaterialFactory.Shared(new Color(0.6f, 0.58f, 0.52f)));
            var c = go.AddComponent<SphereCollider>(); c.center = Vector3.up; c.radius = 2.4f;
            var insp = go.AddComponent<NPlaceholderInspect>();
            insp.Settlement = st.Id;
            return go;
        }

        private GameObject BuildBuilding(Building b, BoardTopology t)
        {
            var go = new GameObject("NPlaceholder building " + b.Id);
            go.transform.SetParent(_root, false);
            var p = new Vector3((float)PlotLayout.PlotX(t, b.Plot), 0, (float)PlotLayout.PlotZ(t, b.Plot));
            p.y = PlayerWalker.GroundHeight(p);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0, (float)PlotLayout.PlotYaw(t, b.Plot), 0);
            var col = b.Faction == PlayerIds.Human ? Yours : Theirs;
            var def = BuildingDefs.Get(b.Type);
            bool primitive = def != null && def.Class == BuildingClass.PrimitiveCollector;
            float w = primitive ? 2.4f : 4f;
            switch (b.State)
            {
                case BuildingState.Site:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f;
                        Signs.Box(go.transform, "Frame", Quaternion.Euler(0, a, 0) * new Vector3(0, 0.2f, w / 2), new Vector3(w, 0.2f, 0.2f), Wood, false, a);
                    }
                    Signs.Box(go.transform, "Peg", new Vector3(0, 0.8f, 0), new Vector3(0.12f, 1.6f, 0.12f), col, false);
                    break;
                case BuildingState.Constructing:
                    Signs.Box(go.transform, "Walls", new Vector3(0, 0.6f, 0), new Vector3(w, 1.2f, w * 0.8f), Wood, false);
                    for (int i = 0; i < 4; i++)
                        Signs.Box(go.transform, "Post", new Vector3((i % 2 == 0 ? -1 : 1) * w / 2, 1.5f, (i < 2 ? -1 : 1) * w * 0.4f), new Vector3(0.15f, 3f, 0.15f), Wood, false);
                    break;
                default:
                    float h = primitive ? 1.0f : 2.4f;
                    Signs.Box(go.transform, "Walls", new Vector3(0, h / 2, 0), new Vector3(w, h, w * 0.8f), primitive ? Wood : Color.Lerp(Wood, col, 0.25f), false);
                    if (!primitive)
                        Signs.Prim(PrimitiveType.Cylinder, go.transform, "Roof", new Vector3(0, h + 0.5f, 0), new Vector3(w * 1.1f, 0.5f, w * 0.95f), MaterialFactory.Shared(Roof));
                    Signs.Box(go.transform, "Tag", new Vector3(0, h + (primitive ? 0.2f : 1.1f), 0), new Vector3(0.5f, 0.3f, 0.5f), col, false);
                    if (b.State == BuildingState.Retiring) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = MaterialFactory.Shared(new Color(0.4f, 0.38f, 0.35f));
                    break;
            }
            var c = go.AddComponent<BoxCollider>(); c.center = Vector3.up; c.size = new Vector3(w, 2f, w);
            var insp = go.AddComponent<NPlaceholderInspect>();
            insp.Building = b.Id;
            return go;
        }

        private GameObject BuildRoad(Road r, BoardTopology t)
        {
            BoardSpace.HexEdgeEnds(t, r.HexEdge, out var a, out var b);
            var mb = new MeshBuilder();
            var col = r.State == RoadState.Built ? new Color(0.62f, 0.52f, 0.38f) : new Color(0.75f, 0.68f, 0.5f);
            a.y = 0.06f; b.y = 0.06f;
            var d = (b - a).normalized;
            mb.Strip(a + d * 3f, b - d * 3f, r.State == RoadState.Built ? 2.4f : 1.2f, col);
            var go = mb.ToObject("NPlaceholder road " + r.Id, _root, MaterialFactory.CreateVertexColored(Color.white, "NPlaceholderRoad"));
            go.layer = 2;
            return go;
        }

        private GameObject BuildMark(int proposalId)
        {
            var s = S;
            var p = FactionApi.GetProposal(s, proposalId);
            if (p == null || !FactionApi.ProposalTarget(s, proposalId, out double x, out double z, out double r)) return null;
            var mb = new MeshBuilder();
            bool edge = p.Kind == ProposalKind.Road || (p.Kind == ProposalKind.Banish && p.DemonSiteKind == World.Sim.Demons.DemonSiteKind.HexEdge);
            var centre = new Vector3((float)x, 0, (float)z);
            float y = PlayerWalker.GroundHeight(centre) + 0.12f;
            if (edge)
            {
                int he = p.Kind == ProposalKind.Road ? p.HexEdge : p.DemonSiteId;
                BoardSpace.HexEdgeEnds(s.Board.Topology, he, out var a, out var b);
                var d = (b - a).normalized;
                a.y = y; b.y = y;
                mb.Strip(a + d * 4f, b - d * 4f, 2f * (float)FactionApi.RoadStripHalfWidth, Color.white);
            }
            else
            {
                float rr = Mathf.Min((float)r, 6f);
                const int N = 40;
                for (int i = 0; i < N; i++)
                {
                    float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                    var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                    var c = centre; c.y = y;
                    mb.Quad(c + o0 * rr * 0.8f, c + o0 * rr, c + o1 * rr, c + o1 * rr * 0.8f, Color.white);
                    mb.Quad(c + o0 * rr * 0.8f, c + o1 * rr * 0.8f, c + o1 * rr, c + o0 * rr, Color.white);
                }
            }
            var mat = MaterialFactory.CreateGlow(MarkColour, "NPlaceholderMark");
            var go = mb.ToObject("NPlaceholder mark " + proposalId, _root, mat);
            go.layer = 2;
            _markMats[proposalId] = mat;
            return go;
        }
    }

    /// <summary>PLACEHOLDER (package N): a carrier figure with its load; tap it to "Follow". Removed with V's views.</summary>
    public sealed class NPlaceholderCarrier : MonoBehaviour, IFollowable, IInspectable
    {
        public int CarrierId { get; private set; }
        private Transform _load;
        private Renderer _loadRenderer;
        private Ware _loadWare = (Ware)(-1);
        private PlaceholderActor _body;

        public string FollowName => "Carrier";
        public Transform FollowTransform => transform;
        public string Title => "Carrier";
        public IEnumerable<string> Lines
        {
            get
            {
                var b = WorldBootstrap.Instance;
                if (b != null) yield return EconomyApi.DescribeCarrier(b.Sim.State, CarrierId);
            }
        }

        public static NPlaceholderCarrier Create(Transform parent, int id, Color colour)
        {
            var go = new GameObject("NPlaceholder carrier " + id);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<NPlaceholderCarrier>();
            c.CarrierId = id;
            c._body = PlaceholderActor.Build("carrier" + id, Color.Lerp(colour, new Color(0.6f, 0.5f, 0.35f), 0.45f), go.transform);
            c._body.transform.localScale = Vector3.one * 0.85f;
            var load = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(load.GetComponent<Collider>());
            load.name = "Load";
            load.transform.SetParent(go.transform, false);
            load.transform.localPosition = new Vector3(0, 1.95f, 0);
            load.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
            c._load = load.transform;
            c._loadRenderer = load.GetComponent<Renderer>();
            load.SetActive(false);
            return c;
        }

        public void Place(Vector3 p, float yaw, Carrier state)
        {
            transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            bool has = state.HasLoad && state.LoadCount > 0;
            if (_load.gameObject.activeSelf != has) _load.gameObject.SetActive(has);
            if (has && state.Load != _loadWare)
            {
                _loadWare = state.Load;
                ColorUtility.TryParseHtmlString(EconomyApi.WareColour(state.Load), out var col);
                _loadRenderer.sharedMaterial = MaterialFactory.Shared(col);
            }
        }
    }

    /// <summary>PLACEHOLDER (package N): tap text for a placeholder camp or building.</summary>
    public sealed class NPlaceholderInspect : MonoBehaviour, IInspectable
    {
        public int Settlement = -1, Building = -1;
        private static WorldState S => WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;

        public string Title
        {
            get
            {
                var s = S;
                if (s == null) return "";
                if (Building >= 0) return EconomyApi.DescribeBuilding(s, Building).Title;
                return EconomyApi.SettlementName(s, Settlement);
            }
        }

        public IEnumerable<string> Lines
        {
            get
            {
                var s = S;
                if (s == null) yield break;
                if (Building >= 0)
                {
                    var info = EconomyApi.DescribeBuilding(s, Building);
                    yield return info.Status;
                    if (!string.IsNullOrEmpty(info.RecipeText)) yield return info.RecipeText;
                    yield break;
                }
                var st = s.Economy.Settlements[Settlement];
                var sb = new System.Text.StringBuilder("Store: ");
                bool any = false;
                foreach (var w in Wares.All)
                {
                    int n = st.Stock[(int)w];
                    if (n <= 0) continue;
                    if (any) sb.Append(", ");
                    sb.Append(n).Append(' ').Append(EconomyApi.WareName(w));
                    any = true;
                }
                yield return any ? sb.ToString() : "Store: empty";
            }
        }
    }
}
