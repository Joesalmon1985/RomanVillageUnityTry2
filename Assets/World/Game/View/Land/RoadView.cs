using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A main road along a whole hex edge (REFACTOR_SPEC 2.B, 8.4). Site: pegs and a rope line along the edge, the
    /// goods at its middle and a progress bar. Constructing: the road laid from one end as the build time runs, a
    /// builder at work. Built: a 3 m packed-earth road with kerb stones and a people's pennant at its middle.
    /// </summary>
    public sealed class RoadView : MonoBehaviour, IInspectable
    {
        public const float Width = 3f;

        public int RoadId { get; private set; }
        public int HexEdge { get; private set; }
        public int Faction { get; private set; }
        public RoadState ShownState { get; private set; } = (RoadState)(-1);
        public float ShownLaid { get; private set; } = -1f;
        public SiteParts Site { get; private set; }
        public Vector3 A { get; private set; }
        public Vector3 B { get; private set; }

        private GameObject _surface, _pegs, _pennant;

        public string Title => ShownState == RoadState.Built ? "Main road" : "Road site";
        public IEnumerable<string> Lines => LandText.State != null ? LandText.RoadLines(LandText.State, RoadId) : new string[0];

        public static RoadView Create(Transform parent, WorldState s, Road r)
        {
            var t = s.Board.Topology;
            var go = new GameObject("Road " + r.Id + " (edge " + r.HexEdge + ")");
            go.transform.SetParent(parent, false);
            BoardSpace.HexEdgeEnds(t, r.HexEdge, out var a, out var b);
            var v = go.AddComponent<RoadView>();
            v.RoadId = r.Id; v.HexEdge = r.HexEdge; v.Faction = r.Faction;
            v.A = a; v.B = b;
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            v.Site = new SiteParts(go.transform);
            LandLook.PickBox(go, Vector3.up * 0.5f, new Vector3(Width + 1f, 1f, (b - a).magnitude - 6f));
            return v;
        }

        public void Sync(WorldState s, Road r)
        {
            var tu = s.Economy.Tuning;
            float len = (B - A).magnitude;
            float laid = r.State == RoadState.Built ? 1f : r.State == RoadState.Constructing ? r.BuildProgress / (float)Mathf.Max(1, tu.RoadBuildTicks) : 0f;
            laid = Mathf.Round(laid * 20f) / 20f;
            if (r.State != ShownState)
            {
                ShownState = r.State;
                bool site = r.State != RoadState.Built;
                if (site && _pegs == null)
                    _pegs = LandLook.Part(transform, "Pegs", LandLook.Cached("road/pegs/" + Mathf.RoundToInt(len), mb =>
                    {
                        float half = len / 2 - 3.5f;
                        foreach (float x in new[] { -Width / 2, Width / 2 })
                        {
                            for (float z = -half; z <= half + 0.01f; z += 6f)
                                LandShapes.Beam(mb, new Vector3(x, 0, z), new Vector3(x, 0.8f, z), 0.05f, 4, LandLook.DarkWood);
                            LandShapes.Beam(mb, new Vector3(x, 0.65f, -half), new Vector3(x, 0.65f, half), 0.02f, 3, LandLook.Thatch, false);
                        }
                    }));
                if (_pegs != null) _pegs.SetActive(site);
                if (!site) Site.Clear();
                if (!site && _pennant == null)
                    _pennant = LandLook.Part(transform, "Pennant", LandLook.Cached("road/pennant/" + Faction, mb =>
                    {
                        for (int i = 0; i < 5; i++) mb.Rock(new Vector3(Width / 2 + 0.9f + (i % 2) * 0.3f, i * 0.12f, (i - 2) * 0.25f), 0.35f, LandLook.Stone, new PropRandom(i + 7).Next);
                        LandShapes.Pennant(mb, new Vector3(Width / 2 + 1.0f, 0.3f, 0), 2.4f, Faction);
                    }));
            }
            if (!Mathf.Approximately(laid, ShownLaid))
            {
                ShownLaid = laid;
                var mb = new MeshBuilder();
                if (laid > 0f)
                {
                    float half = len / 2 - 2.5f;
                    float z0 = -half, z1 = Mathf.Lerp(-half, half, laid);
                    var earth = new Color(0.58f, 0.47f, 0.33f);
                    mb.Strip(new Vector3(0, 0.07f, z0), new Vector3(0, 0.07f, z1), Width, earth);
                    mb.Strip(new Vector3(0, 0.075f, z0), new Vector3(0, 0.075f, z1), Width * 0.45f, earth * 1.08f);
                    var rnd = new PropRandom(HexEdge * 11 + 3);
                    for (float z = z0; z < z1; z += 2.6f)
                        foreach (float x in new[] { -Width / 2 - 0.1f, Width / 2 + 0.1f })
                            mb.Box(new Vector3(x, 0.07f, z + rnd.Range(-0.4f, 0.4f)), new Vector3(0.22f, 0.12f, 0.32f), LandLook.Stone * rnd.Range(0.85f, 1.05f), rnd.Range(-25f, 25f));
                }
                LandLook.SetMesh(ref _surface, transform, "Surface", mb, null, false);
            }
            if (r.State != RoadState.Built)
                Site.Sync(tu.RoadCost, r.State == RoadState.Constructing ? tu.RoadCost : r.Delivered, laid, r.State == RoadState.Constructing,
                    new Vector3(Width / 2 + 1.2f, 0, -0.6f), Vector3.forward, new Vector3(Width / 2 + 1.6f, 0, 3.0f), Faction);
        }

        public void Animate(float now) { Site.Animate(now); }
    }
}
