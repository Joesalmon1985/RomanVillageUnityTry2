using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// Collecting villagers (G1-7; PRD_GODBOARD CL-1, CL-2, AB-3). On every <see cref="Produced"/> event each producing
    /// settlement sends one villager per card (a settlement makes 1, a city 2 beside a hex), at most
    /// <see cref="CapPerSettlement"/> per settlement, walking from the settlement to the producing hex and back carrying
    /// a load of that good. Purely visual: the cards are in the hand already and the sim delivered the goods to the
    /// settlement's store (<c>TableSeams.OnProduced</c>). Placeholder figures (a body, a head and a load box in the
    /// good's colour), drawn larger than life like the board pieces. Reads state, never writes it.
    /// </summary>
    public sealed class CollectWalkers : MonoBehaviour
    {
        /// <summary>PRD CollectWalkersCap: villagers a settlement sends per production.</summary>
        public const int CapPerSettlement = 3;
        /// <summary>Most figures alive at once (a long fast-forward must not pile up figures).</summary>
        public const int MaxActive = 90;
        /// <summary>Metres per second on the board scale, seconds between two villagers of one settlement, seconds at the hex.</summary>
        public static float Speed = 36f, Stagger = 0.45f, Gather = 0.6f;
        public const float Scale = 14f;

        public enum Leg { Waiting, Out, Gathering, Back, Done }

        public sealed class Walker
        {
            public int Corner, Hex, People;
            public Ware Good;
            public Leg Leg;
            public Vector3 Home, Target;
            public GameObject Go, Load, Halo, Trail;
            /// <summary>P1: the jointed body (legs and arms swing with the ground covered).</summary>
            public MeshFigure Fig;
            public float Delay, Clock;
            public bool Loaded;
            public int Index;
        }

        public static CollectWalkers Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; Speed = 36f; Stagger = 0.45f; Gather = 0.6f; }

        /// <summary>Villagers sent since the game began, and the number that got home with their load.</summary>
        public int Sent { get; private set; }
        public int Delivered { get; private set; }
        public IReadOnlyList<Walker> Active => _active;

        private WorldState _s;
        private readonly List<Walker> _active = new List<Walker>();
        private Material _mat;
        private Transform _root;

        public static CollectWalkers Create(Transform parent, WorldState s)
        {
            var go = new GameObject("CollectWalkers");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CollectWalkers>();
            c._s = s;
            c._root = go.transform;
            c._mat = MaterialFactory.CreateVertexColored(Color.white, "CollectWalker");
            Instance = c;
            return c;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_mat != null) Destroy(_mat);
            foreach (var m in _meshes.Values) if (m != null) Destroy(m);
        }

        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();

        private Mesh Cached(string key, System.Action<MeshBuilder> make)
        {
            if (_meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(); make(mb);
            m = mb.ToMesh(key); _meshes[key] = m;
            return m;
        }

        private Color PeopleColour(int p)
        {
            foreach (var pe in _s.Peoples) if (pe.Id == p) return TempleView.PeopleColour(pe);
            return Color.white;
        }

        /// <summary>
        /// A production happened: sends the villagers of every producing settlement. Returns how many were sent. Reads the
        /// table's corners as they are now (the event is handled in the tick it was raised in).
        /// </summary>
        public int OnProduced(Produced pr)
        {
            var t = _s.Table;
            if (pr == null || pr.Hexes == null || t == null || t.CornerOwner == null) return 0;
            var topo = _s.Board.Topology;
            var sentBy = new Dictionary<int, int>(); // corner -> villagers already sent this event
            int n = 0;
            foreach (int h in pr.Hexes)
            {
                if (h < 0 || h >= topo.HexCount) continue;
                var w = TerrainInfo.RawWare(_s.Board.HexTerrain[h]);
                if (!w.HasValue) continue;
                foreach (int c in topo.HexCorners(h))
                {
                    int owner = t.CornerOwner[c];
                    if (owner < 0) continue;
                    sentBy.TryGetValue(c, out int already);
                    int cards = t.CornerLevel[c];
                    for (int k = 0; k < cards && already < CapPerSettlement; k++, already++)
                    {
                        Spawn(c, h, owner, w.Value, already);
                        n++;
                    }
                    sentBy[c] = already;
                }
            }
            return n;
        }

        private void Spawn(int corner, int hex, int people, Ware good, int index)
        {
            Sent++;
            if (_active.Count >= MaxActive) return;
            var topo = _s.Board.Topology;
            var home = BoardSpace.Corner(topo, corner);
            var centre = BoardSpace.Hex(topo, hex);
            // Stop beside the hex's centre (the number post stands there), a little differently for each villager.
            var away = home - centre; away.y = 0;
            if (away.sqrMagnitude < 1f) away = Vector3.right;
            var target = centre + Quaternion.Euler(0, index * 40f, 0) * away.normalized * 9f;
            var w = new Walker
            {
                Corner = corner, Hex = hex, People = people, Good = good, Home = home, Target = target,
                Leg = Leg.Waiting, Delay = index * Stagger, Index = index
            };
            w.Go = Build(w);
            w.Go.transform.position = home;
            w.Go.SetActive(false);
            w.Trail = BuildTrail(w);
            w.Trail.SetActive(false);
            _active.Add(w);
        }

        /// <summary>G1-6b: a dotted trail on the ground from the settlement to the hex, in the good's colour, so collecting reads from the top view.</summary>
        private GameObject BuildTrail(Walker w)
        {
            var d = w.Target - w.Home; d.y = 0;
            float len = d.magnitude;
            var dir = len > 0.01f ? d / len : Vector3.forward;
            var mb = new MeshBuilder();
            var gc = PortMarkers.GoodColor(w.Good);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            int n = Mathf.Max(2, Mathf.FloorToInt(len / 20f));
            for (int i = 0; i < n; i++)
            {
                var p = dir * ((i + 0.5f) * len / n);
                mb.Box(p + Vector3.up * 0.2f, new Vector3(13f, 0.5f, 15f), new Color(0.1f, 0.09f, 0.08f), yaw);
                mb.Box(p + Vector3.up * 0.7f, new Vector3(9f, 0.5f, 11f), Color.Lerp(gc, Color.white, 0.2f), yaw);
            }
            var go = mb.ToObject("Trail " + w.Corner + "->" + w.Hex, _root, _mat);
            go.transform.position = w.Home + Vector3.up * (BoardSpace.GroundY + 5f);
            go.layer = 2;
            return go;
        }

        private GameObject Build(Walker w)
        {
            var col = PeopleColour(w.People);
            var skin = new Color(0.88f, 0.70f, 0.52f);
            string ck = ColorUtility.ToHtmlStringRGB(col);
            // P1: a jointed figure (torso, head, cap and pin in one mesh; legs and arms on pivots) so the walk reads.
            var torso = Cached("cw/torso/" + ck + "/" + (int)w.Good, mb =>
            {
                mb.Box(new Vector3(0, 1.3f, 0), new Vector3(1.2f, 1.1f, 0.75f), col);
                mb.Box(new Vector3(0, 0.82f, 0), new Vector3(1.05f, 0.25f, 0.68f), col * 0.75f);
                mb.Box(new Vector3(0, 2.2f, 0), new Vector3(0.72f, 0.72f, 0.72f), skin);
                mb.Cylinder(new Vector3(0, 2.6f, 0), 0.9f, 0.9f, 0.15f, 10, Color.Lerp(col, Color.white, 0.6f)); // a bright cap, so they read from above
                // G1-6b: an overhead pin in the good's colour with a dark rim, so the villager reads from the whole-island view
                mb.Cylinder(new Vector3(0, 3.4f, 0), 1.45f, 1.45f, 0.12f, 14, new Color(0.08f, 0.07f, 0.06f));
                mb.Cylinder(new Vector3(0, 3.55f, 0), 1.15f, 1.15f, 0.12f, 14, PortMarkers.GoodColor(w.Good));
            });
            var leg = Cached("cw/leg/" + ck, mb => { mb.Box(new Vector3(0, -0.4f, 0), new Vector3(0.32f, 0.8f, 0.34f), col * 0.6f); mb.Box(new Vector3(0, -0.76f, 0.06f), new Vector3(0.34f, 0.1f, 0.46f), new Color(0.22f, 0.15f, 0.09f)); });
            var arm = Cached("cw/arm/" + ck, mb => { mb.Box(new Vector3(0, -0.36f, 0), new Vector3(0.26f, 0.72f, 0.28f), col * 0.85f); mb.Box(new Vector3(0, -0.78f, 0), new Vector3(0.24f, 0.18f, 0.24f), skin); });
            var f = MeshFigure.Build("Collector " + w.People + "/" + w.Corner + "->" + w.Hex, _root, _mat, torso, leg, arm, 0.8f, 0.26f, 1.78f, 0.74f, 2);
            f.Scale = Scale; f.Seed = (w.Corner * 0.37f + w.Index * 0.21f) % 1f;
            w.Fig = f;
            var go = f.Root.gameObject;
            go.transform.localScale = Vector3.one * Scale;
            var lm = Cached("cw/load/" + (int)w.Good, mb => mb.Box(Vector3.zero, new Vector3(0.8f, 0.6f, 0.6f), PortMarkers.GoodColor(w.Good)));
            var load = new GameObject("Load " + w.Good); load.layer = 2;
            load.transform.SetParent(f.Body, false);
            load.AddComponent<MeshFilter>().sharedMesh = lm;
            load.AddComponent<MeshRenderer>().sharedMaterial = _mat;
            w.Load = load;
            w.Load.transform.localPosition = new Vector3(0, 1.25f, 0.62f); // held in both arms in front of the chest
            w.Load.SetActive(false);
            return go;
        }

        private void Update() { Step(Time.deltaTime); }

        /// <summary>Moves every villager by <paramref name="dt"/> seconds (tests drive it directly).</summary>
        public void Step(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var w = _active[i];
                if (w.Leg == Leg.Done || w.Go == null) { Remove(i); continue; }
                Advance(w, dt);
                if (w.Leg == Leg.Done) Remove(i);
            }
        }

        private void Remove(int i)
        {
            var w = _active[i];
            if (w.Go != null) Destroy(w.Go);
            if (w.Trail != null) Destroy(w.Trail);
            _active.RemoveAt(i);
        }

        private void Advance(Walker w, float dt)
        {
            var before = w.Go.transform.position;
            AdvanceLeg(w, dt);
            if (w.Fig != null && w.Go != null && w.Go.activeSelf)
            {
                var d = w.Go.transform.position - before; d.y = 0;
                w.Fig.Step(dt, d.magnitude, w.Leg == Leg.Gathering ? MeshFigure.Hold.Work : w.Loaded ? MeshFigure.Hold.Load : MeshFigure.Hold.Swing, w.Clock);
            }
        }

        private void AdvanceLeg(Walker w, float dt)
        {
            w.Clock += dt;
            switch (w.Leg)
            {
                case Leg.Waiting:
                    w.Delay -= dt;
                    if (w.Delay <= 0f) { w.Leg = Leg.Out; w.Go.SetActive(true); if (w.Trail != null) w.Trail.SetActive(true); }
                    return;
                case Leg.Out:
                    if (Walk(w, w.Target, dt)) { w.Leg = Leg.Gathering; w.Clock = 0f; }
                    return;
                case Leg.Gathering:
                    if (w.Clock >= Gather) { w.Leg = Leg.Back; w.Loaded = true; w.Load.SetActive(true); }
                    return;
                case Leg.Back:
                    if (Walk(w, w.Home, dt)) { w.Leg = Leg.Done; Delivered++; }
                    return;
            }
        }

        private bool Walk(Walker w, Vector3 to, float dt)
        {
            var p = w.Go.transform.position;
            var d = to - p; d.y = 0;
            float step = Speed * dt;
            bool arrived = d.magnitude <= step;
            var np = arrived ? to : p + d.normalized * step;
            np.y = BoardSpace.GroundY; // P1: the gait's own bob (the body dips over the stepping legs)
            w.Go.transform.position = np;
            if (d.sqrMagnitude > 0.01f) w.Go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            return arrived;
        }
    }
}
