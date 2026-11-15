using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;
using L = World.Game.CouncilLayout;

namespace World.Game
{
    /// <summary>A person in the council scene whose speech bubble hangs over their head.</summary>
    public sealed class CouncilSpeaker : MonoBehaviour, ISpeaker, IFollowable
    {
        public string Id = "council";
        public string Name = "Someone";
        public string SpeakerId => Id;
        public string FollowName => Name;
        public Transform FollowTransform => transform;
        public Transform Head { get; set; }
    }

    /// <summary>Where the gathering stands (the camp's clear ground), chosen with the camp layout's obstruction check.</summary>
    public sealed class CouncilSite
    {
        public Vector3 Position;
        public float Yaw;
        public float Scale;
        public string Why;

        public Vector3 ToWorld(Vector3 local) => Position + Quaternion.Euler(0, Yaw, 0) * local;

        /// <summary>
        /// Like <see cref="CampLayout.Obstruction"/> but ignoring hex stock patches and the unbuilt road lines (full-size ground cannot sit between them; trees, crops and rocks are
        /// cleared with <c>LandViews.AddClearing</c>): keeps off plots, corners, the home camp's paths and the main roads.
        /// </summary>
        public static string Blocked(Board board, CampLayout layout, Vector3 p, float radius)
        {
            var t = board.Topology;
            p.y = 0;
            int home = board.StartCorners != null && board.StartCorners.Length > 0 ? board.StartCorners[0] : 0;
            int reserved = board.ReservedPlots != null && board.ReservedPlots.Length > 0 ? board.ReservedPlots[0] : -1;
            if (reserved >= 0)
            {
                var c = new Vector3((float)PlotLayout.PlotX(t, reserved), 0, (float)PlotLayout.PlotZ(t, reserved));
                if ((p - c).magnitude < PlotLayout.DanceCircleRadius + radius + 2f) return "dance circle";
            }
            for (int c = 0; c < t.CornerCount; c++)
            {
                var cp = BoardSpace.Vertex(t, c); cp.y = 0;
                float d = (p - cp).magnitude;
                if (d > 200f) continue;
                if (d < (c == home ? CampLayout.HomeCornerClear : CampLayout.CornerClear) + radius) return "corner " + c;
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                for (int k = 0; k < PlotLayout.PlotsPerCorner; k++)
                {
                    int plot = PlotLayout.PlotId(c, k / 3, k % 3);
                    if (plot == reserved) continue;
                    var pc = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
                    if ((p - pc).magnitude < PlotLayout.PlotRadius + CampLayout.PlotMargin + radius) return "plot " + plot;
                }
                if (c != home) continue;
                for (int k = 0; k < PlotLayout.PlotsPerCorner; k++)
                {
                    int plot = PlotLayout.PlotId(c, k / 3, k % 3);
                    var pts = PlotLayout.PathPoints(t, plot);
                    for (int i = 0; i + 3 < pts.Length; i += 2)
                    {
                        var a = new Vector3((float)pts[i], 0, (float)pts[i + 1]); var b = new Vector3((float)pts[i + 2], 0, (float)pts[i + 3]);
                        if (BoardSpace.DistanceToSegment(p, a, b) < CampLayout.PathMargin + radius) return "path " + plot;
                    }
                }
            }
            foreach (var pr in layout.Dressing) { var d2 = pr.Position - p; d2.y = 0; if (d2.magnitude < pr.Radius + radius + 1f) return "camp prop"; }
            foreach (var cl in layout.Clearings) { var d2 = new Vector3(cl.x - p.x, 0, cl.y - p.z); if (d2.magnitude < cl.z + radius * 0.5f) return "camp clearing"; }
            return null;
        }

        /// <summary>
        /// Finds the clear spot for the stage with the camp layout's obstruction check: the largest clear disc near the
        /// home camp's fire (grid search), the stage scaled to fit it (1 = full size, down to 0.3).
        /// </summary>
        public static CouncilSite Find(WorldState s)
        {
            var board = s.Board;
            var layout = new CampLayout(board);
            float prev = L.Scale;
            L.Scale = 1f;
            var samples = L.FootprintSamples(1f);
            L.Footprint(out var c1, out float r1);
            L.Scale = prev;
            var centres = new List<Vector3>();
            for (float x = -120f; x <= 120f; x += 4f)
                for (float z = -120f; z <= 120f; z += 4f) centres.Add(layout.Hearth + new Vector3(x, 0, z));
            centres.Sort((m, n) => (m - layout.Hearth).sqrMagnitude.CompareTo((n - layout.Hearth).sqrMagnitude));
            foreach (var c in centres)
            {
                if ((c - layout.Hearth).magnitude < 14f) continue;
                if (Blocked(board, layout, c, 3f) != null) continue; // quick reject
                for (int k = 0; k < 8; k++)
                {
                    float yaw = k * 45f;
                    var q = Quaternion.Euler(0, yaw, 0);
                    var origin = c - q * c1; origin.y = 0;
                    bool ok = true;
                    foreach (var lp in samples)
                    {
                        var w = origin + q * lp;
                        if (Blocked(board, layout, w, 1.6f) != null) { ok = false; break; }
                        int hx = BoardSpace.HexAt(w);
                        if (hx < 0) { ok = false; break; }
                        var tr = board.HexTerrain[hx];
                        if (tr == World.Sim.Terrain.Hills || tr == World.Sim.Terrain.Mountain) { ok = false; break; }
                    }
                    if (!ok) continue;
                    return new CouncilSite { Position = origin, Yaw = yaw, Scale = 1f, Why = "open ground " + (c - layout.Hearth).magnitude.ToString("0") + " m from the fire, full size" };
                }
            }
            L.Scale = 1f;
            return new CouncilSite { Position = layout.Hearth + new Vector3(0, 0, 40f), Yaw = 0f, Scale = 1f, Why = "fallback (no clear ground found)" };
        }
    }

    /// <summary>
    /// The council scene built from flat-shaded primitives and driven only by <c>WorldState.Council</c> and its events:
    /// back beam with four bays, eleven leads and their props, eleven rows of standing places, the low table with three
    /// trays, the clerk's end, banked emblem posts and catspaws, seals and banners. It never writes state. It exists
    /// while an election is in progress (a gathering the player can pass by) and the encounter only adds a camera and
    /// input to it.
    /// </summary>
    public sealed class CouncilScene : MonoBehaviour
    {
        public static CouncilScene Current { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public int SettlementId { get; private set; }
        public CouncilSite Site { get; private set; }
        public Transform Root { get; private set; }
        public CouncilBench Bench { get; private set; }

        // ---- table geometry (local) ----
        float _tableY;
        public Vector3 TrayCentre(int i) => L.OnFloor((33f + 18f * i) / 100f, 0.77f, _tableY);
        public Vector3 SocketFoot(int i, int s) => L.OnFloor((29f + 18f * i + 8f * s) / 100f, 0.83f, _tableY);
        public Vector3 SealFoot => L.OnFloor(0.845f, 0.86f, _tableY);
        public Vector3 PetitionFoot => L.OnFloor(0.94f, 0.80f, _tableY);
        public Vector3 HandFoot => L.OnFloor(0.17f, 0.84f, _tableY * 0.0f + 0.0f);
        public Vector3 PawSpawn => L.OnFloor(0.17f, 0.90f);

        // ---- parts ----
        readonly GameObject[] _bayEmblem = new GameObject[4];
        readonly GameObject[] _bayHalo = new GameObject[4];
        readonly GameObject[] _bayFrame = new GameObject[4];
        readonly Vector3[,] _hookPos = new Vector3[4, 3];
        readonly PlaceholderActor[] _lead = new PlaceholderActor[11];
        readonly CouncilSpeaker[] _leadSpeaker = new CouncilSpeaker[11];
        readonly GameObject[] _leadProp = new GameObject[11];
        readonly Vector3[] _leadPropHome = new Vector3[11];
        readonly float[] _leadPropScale = new float[11];
        readonly GameObject[] _leadRing = new GameObject[11];
        readonly GameObject[] _banner = new GameObject[11];
        readonly float[] _bannerT = new float[11];
        readonly float[] _react = new float[11];
        readonly float[] _pulse = new float[11];
        readonly GameObject[][] _stand = new GameObject[11][];
        readonly Vector3[][] _standHome = new Vector3[11][];
        readonly float[][] _standWalk = new float[11][];
        readonly PlaceholderActor[] _cand = new PlaceholderActor[4];
        readonly CouncilSpeaker[] _candSpeaker = new CouncilSpeaker[4];
        PlaceholderActor _clerk;
        PlaceholderActor _aide;
        GameObject _hand, _seal, _petition, _handHalo, _sealHalo, _petitionHalo, _lantern;
        readonly GameObject[] _tray = new GameObject[3];
        readonly GameObject[] _trayGlow = new GameObject[3];
        readonly List<GameObject>[] _carvings = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        readonly string[] _traySig = new string[3];
        readonly float[] _trayPop = new float[3];
        readonly GameObject[] _fires = new GameObject[2];

        sealed class Mover { public GameObject Go; public Vector3 Target; public Vector3 Pos; public bool Leaving; public Vector3 LeaveTo; public float Height; public int Seat; public int Track; public int Place; public float Born; public bool Paw; }
        readonly Dictionary<int, Mover> _posts = new Dictionary<int, Mover>();
        readonly Dictionary<int, Mover> _paws = new Dictionary<int, Mover>();
        readonly List<Mover> _leaving = new List<Mover>();
        readonly Dictionary<int, GameObject> _seals = new Dictionary<int, GameObject>();
        readonly HashSet<int> _spoken = new HashSet<int>();
        readonly List<int> _preview = new List<int>();
        int _previewTray = -1, _previewSocket = -1;
        float _handPress, _sealPress, _petitionLift, _carryT = -1f, _winT = -1f;
        int _lastActive = -1, _lastPhase = -1, _lineCounter;
        int _leadBubble, _candBubble;
        public int ActiveSeatShown { get; private set; } = -1;
        /// <summary>Set while the aide is still on the way with an earned petition: the petition is not shown yet.</summary>
        public bool HoldPetition;
        public int MoversBusy { get; private set; }
        public bool Settled => MoversBusy == 0 && _carryT < 0f && _handPress <= 0.01f && _sealPress <= 0.01f;
        public int CatspawsShown => _paws.Count;
        public int PostsShown => _posts.Count;
        public int SealsShown => _seals.Count;
        public int BannersShown { get { int n = 0; foreach (var b in _banner) if (b != null && b.activeSelf) n++; return n; } }
        public int TraysShown { get { int n = 0; foreach (var t in _tray) if (t != null && t.activeSelf) n++; return n; } }
        public int CarvingsShown { get { int n = 0; foreach (var l in _carvings) foreach (var c in l) if (c != null && c.activeSelf) n++; return n; } }
        public bool PetitionShown => _petition != null && _petition.activeSelf;
        public int WitnessesAway { get; private set; }
        public int Handshakes { get; private set; }
        public PlaceholderActor Candidate(int seat) => _cand[seat];
        public PlaceholderActor Lead(int track) => _lead[track - 2];
        public PlaceholderActor Aide => _aide;
        public GameObject LeadProp(int track) => _leadProp[track - 2];
        public GameObject Stander(int track, int place) => _stand[track - 2][place - 1];
        public GameObject TrayObject(int i) => _tray[i];
        public GameObject HandObject => _hand;
        public GameObject SealObject => _seal;
        public GameObject PetitionObject => _petition;
        public GameObject LanternObject => _lantern;
        public GameObject BayEmblem(int seat) => _bayEmblem[seat];
        public int LeadBubbles { get; private set; }
        public string LastLeadLine { get; private set; }
        public string LastCandidateLine { get; private set; }

        public WorldState State => WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;
        public SettlementCouncil Council => CouncilApi.Get(State, SettlementId);
        public Vector3 ToWorld(Vector3 local) => Root.TransformPoint(local);

        // ================================================================== build

        public static CouncilScene Build(Transform parent, int settlement, CouncilSite site)
        {
            if (Current != null) { Destroy(Current.gameObject); Current = null; }
            L.Scale = site.Scale;
            var go = new GameObject("CouncilScene");
            go.transform.SetParent(parent, false);
            // the floor must clear the hex tiles' own surface everywhere under the stage
            float groundY = 0f;
            var rot = Quaternion.Euler(0, site.Yaw, 0);
            foreach (var lp in L.FootprintSamples(1f)) groundY = Mathf.Max(groundY, PlayerWalker.GroundHeight(site.Position + rot * lp));
            groundY = Mathf.Max(groundY, PlayerWalker.GroundHeight(site.Position));
            go.transform.SetPositionAndRotation(new Vector3(site.Position.x, groundY + 0.05f, site.Position.z), rot);
            var sc = go.AddComponent<CouncilScene>();
            sc.SettlementId = settlement; sc.Site = site; sc.Root = go.transform;
            Current = sc;
            sc.BuildAll();
            return sc;
        }

        void OnDestroy() { if (Current == this) Current = null; }

        // ---- small helpers ----
        static Material M(Color c) => MaterialFactory.Shared(c);

        public static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 size, Color c, Vector3 euler = default)
        {
            var go = Signs.Prim(PrimitiveType.Cube, parent, name, pos, size, M(c), euler);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            return go;
        }

        public static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
        {
            var go = Signs.Prim(PrimitiveType.Cylinder, parent, name, pos, size, M(c));
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            return go;
        }

        GameObject Halo(Transform parent, Vector3 localPos, float size, Color c)
        {
            var go = new GameObject("Halo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = LightShapes.RadialDisc(true);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = LightShapes.Glow;
            var pb = new MaterialPropertyBlock(); pb.SetColor("_Color", new Color(c.r, c.g, c.b, 0.8f)); r.SetPropertyBlock(pb);
            return go;
        }

        float DepthPlane => L.DepthOf(L.OnFloor(0.5f, 0.22f)) + 0.6f * L.Scale;

        /// <summary>Upright figure feet at the floor point of (u, vFeet), top at vTop.</summary>
        float FigureHeight(Vector3 feet, float vTop) => L.HeightFor(feet, vTop);

        PlaceholderActor Person(string id, Color tunic, Vector3 feet, float height, float yaw)
        {
            var a = PlaceholderActor.Build(id, tunic, Root);
            var col = a.GetComponent<Collider>(); if (col != null) Destroy(col);
            a.transform.localPosition = feet;
            a.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            a.transform.localScale = Vector3.one * (height / 1.85f);
            a.SetYaw(Root.eulerAngles.y + yaw);
            return a;
        }

        Mesh Emblem(int seat) => CouncilShapes.Emblem(L.EmblemShapes[seat], L.EmblemColor(L.EmblemColours[seat]));
        Color SeatColor(int seat) => L.EmblemColor(L.EmblemColours[seat]);

        void BuildAll()
        {
            _tableY = 0.9f * L.Scale;
            var sc = Council;
            BuildFloor();
            BuildBeam();
            BuildLeads(sc);
            BuildRows();
            BuildTable();
            BuildPeople(sc);
            BuildBenchEnd();
            BuildFires();
            Bench = CouncilBench.Build(this);
            RegisterClearings();
            Debug.Log("[Council] scene built at " + Site.Position + " yaw " + Site.Yaw.ToString("0") + ", " + Site.Why);
        }

        void RegisterClearings()
        {
            // Ask the terrain to keep trees, crops and rocks off the stage and the bench set.
            float r = 2.6f * L.Scale + 2f;
            for (float z = 0f; z <= 22f * L.Scale; z += 3f * L.Scale)
                for (float x = -13f * L.Scale; x <= 13f * L.Scale; x += 4f * L.Scale)
                { var w = ToWorld(new Vector3(x, 0, z)); LandViews.AddClearing(w.x, w.z, r); }
            if (Bench != null) { var w = ToWorld(Bench.Origin); LandViews.AddClearing(w.x, w.z, 9f); }
        }

        void BuildFloor()
        {
            var mb = new MeshBuilder();
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float v0 = Mathf.Lerp(0.08f, 1.0f, i / (float)n), v1 = Mathf.Lerp(0.08f, 1.0f, (i + 1) / (float)n);
                var col = (i % 2 == 0) ? new Color(0.36f, 0.27f, 0.18f) : new Color(0.31f, 0.23f, 0.15f);
                float u0 = -0.15f, u1 = 1.15f;
                Vector3 P(float u, float v) { var p = L.OnFloor(u, Mathf.Clamp(v, 0f, 0.999f)); p.y = 0.03f; return p; }
                mb.Quad(P(u0, v1), P(u0, v0), P(u1, v0), P(u1, v1), col);
            }
            var go = mb.ToObject("Floor", Root, CouncilShapes.VertexMaterial);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
        }

        void BuildBeam()
        {
            float d = DepthPlane;
            var camRot = L.CamRot;
            float H(float vfrac) => vfrac * 2f * L.TanV * d;
            float W(float ufrac) => ufrac * 2f * L.TanH * d;
            var beam = new GameObject("Beam").transform; beam.SetParent(Root, false);
            beam.localPosition = L.OnPlane(0.5f, 0.05f, d);
            beam.localRotation = camRot;
            // main beam board and a darker bay panel for each seat
            Cube(beam, "Board", Vector3.zero, new Vector3(W(0.90f), H(0.095f), 0.35f * L.Scale), new Color(0.34f, 0.22f, 0.12f));
            for (int s = 0; s < 4; s++)
            {
                float uc = (6f + 22f * s + 11f) / 100f;
                var bay = Cube(beam, "Bay " + s, new Vector3(W(uc - 0.5f), 0, -0.12f * L.Scale), new Vector3(W(0.205f), H(0.082f), 0.1f * L.Scale), new Color(0.22f, 0.14f, 0.08f));
                // the emblem
                var emb = CouncilShapes.Make("Emblem " + s, beam, Emblem(s), new Vector3(W(uc - 0.5f - 0.058f), 0, -0.22f * L.Scale), H(0.062f));
                _bayEmblem[s] = emb;
                _bayHalo[s] = Halo(beam, new Vector3(W(uc - 0.5f - 0.058f), 0, -0.1f * L.Scale), H(0.18f), SeatColor(s));
                _bayHalo[s].SetActive(false);
                // three hooks
                for (int k = 0; k < 3; k++)
                {
                    float hu = uc + 0.015f + 0.034f * (k - 1) + 0.03f;
                    var hookLocal = new Vector3(W(hu - 0.5f), H(0.03f), -0.2f * L.Scale);
                    Cyl(beam, "Hook " + s + "." + k, hookLocal, new Vector3(H(0.012f), H(0.012f), H(0.012f)), new Color(0.80f, 0.64f, 0.25f));
                    _hookPos[s, k] = Root.InverseTransformPoint(beam.TransformPoint(hookLocal + new Vector3(0, -H(0.032f), -0.2f * L.Scale)));
                }
                // frame that marks the player's bay
                var frame = new GameObject("PlayerFrame " + s).transform; frame.SetParent(beam, false);
                float fw = W(0.208f), fh = H(0.088f), fz = -0.3f * L.Scale, ft = H(0.006f);
                var gold = new Color(1f, 0.85f, 0.25f);
                Cube(frame, "t", new Vector3(W(uc - 0.5f), fh / 2, fz), new Vector3(fw, ft, ft), gold);
                Cube(frame, "b", new Vector3(W(uc - 0.5f), -fh / 2, fz), new Vector3(fw, ft, ft), gold);
                Cube(frame, "l", new Vector3(W(uc - 0.5f) - fw / 2, 0, fz), new Vector3(ft, fh, ft), gold);
                Cube(frame, "r", new Vector3(W(uc - 0.5f) + fw / 2, 0, fz), new Vector3(ft, fh, ft), gold);
                _bayFrame[s] = frame.gameObject; frame.gameObject.SetActive(false);
            }
            // posts down to the floor at both ends
            foreach (float uu in new[] { 0.045f, 0.955f })
            {
                var top = beam.TransformPoint(new Vector3(W(uu - 0.5f), 0, 0));
                var foot = ToWorldFloor(uu, 0.24f);
                Cube(Root, "Beam post", Root.InverseTransformPoint((top + foot) * 0.5f), new Vector3(0.25f * L.Scale, Mathf.Abs(Root.InverseTransformPoint(top).y - Root.InverseTransformPoint(foot).y), 0.25f * L.Scale), new Color(0.28f, 0.18f, 0.10f));
            }
        }

        Vector3 ToWorldFloor(float u, float v) => Root.TransformPoint(L.OnFloor(u, v));

        void BuildLeads(SettlementCouncil sc)
        {
            for (int t = 2; t <= 12; t++)
            {
                int i = t - 2;
                var feet = L.LeadFeet(t) + new Vector3(-0.02f * 2f * L.TanH * L.DepthOf(L.LeadFeet(t)), 0, 0);
                float h = FigureHeight(feet, 0.105f);
                var a = Person("lead" + t, L.LeadTunic(t), feet, h, 180f);
                a.name = "Lead " + t;
                _lead[i] = a;
                var sp = a.gameObject.AddComponent<CouncilSpeaker>();
                sp.Id = "lead" + t; sp.Name = sc != null && sc.Disputes.Count > i ? sc.Disputes[i].LeadName : "Lead"; sp.Head = a.HeadPivot != null ? a.HeadPivot : a.transform;
                _leadSpeaker[i] = sp;
                // the big identifying prop, right of the lead
                float ph = h * 0.95f;
                var pfoot = feet + new Vector3(0.62f * (h / 1.85f) * 1.6f, 0, -0.1f * L.Scale);
                var prop = CouncilShapes.Make("Prop " + t, Root, CouncilShapes.Prop(t, false), pfoot, ph);
                _leadProp[i] = prop; _leadPropHome[i] = pfoot; _leadPropScale[i] = ph;
                // ring on the floor showing which dispute a tray touches
                var ring = Halo(Root, feet + new Vector3(0.28f * h, 0.06f, 0), h * 0.95f, new Color(1f, 0.9f, 0.5f));
                ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
                ring.SetActive(false); _leadRing[i] = ring;
                // banner pole (raised when the dispute is secured)
                var bn = CouncilShapes.Make("Banner " + t, Root, CouncilShapes.Banner(Color.white), feet + new Vector3(-0.5f * h, 0, -0.05f), h * 1.15f);
                bn.SetActive(false); _banner[i] = bn;
            }
        }

        void BuildRows()
        {
            var skin = new[] { new Color(0.92f, 0.76f, 0.60f), new Color(0.78f, 0.58f, 0.42f), new Color(0.58f, 0.40f, 0.28f), new Color(0.86f, 0.68f, 0.52f) };
            var cloth = new[] { new Color(0.52f, 0.44f, 0.34f), new Color(0.42f, 0.40f, 0.30f), new Color(0.50f, 0.38f, 0.30f), new Color(0.38f, 0.42f, 0.44f), new Color(0.56f, 0.50f, 0.40f) };
            var rnd = new PropRandom(77);
            for (int t = 2; t <= 12; t++)
            {
                int len = CouncilRules.Length(t);
                _stand[t - 2] = new GameObject[len]; _standHome[t - 2] = new Vector3[len]; _standWalk[t - 2] = new float[len];
                for (int p = 1; p <= len; p++)
                {
                    var uv = L.PlaceUV(t, p);
                    float lat = 0.026f;
                    var feet = L.OnFloor(uv.x + lat, uv.y);
                    float h = FigureHeight(feet, uv.y - 0.05f);
                    var go = CouncilShapes.Make("Stander " + t + "." + p, Root, CouncilShapes.Stander(cloth[(int)(rnd.Next() * cloth.Length)], skin[(int)(rnd.Next() * skin.Length)]), feet, h / 0.96f);
                    go.transform.localScale = new Vector3(go.transform.localScale.x * 1.35f, go.transform.localScale.y, go.transform.localScale.z * 1.35f);
                    go.transform.localRotation = Quaternion.Euler(0, rnd.Range(-25f, 25f), 0);
                    _stand[t - 2][p - 1] = go; _standHome[t - 2][p - 1] = feet;
                }
            }
        }

        void BuildTable()
        {
            var mb = new MeshBuilder();
            Vector3 P(float u, float v, float y) => L.OnFloor(u, v, y);
            float u0 = 0.235f, u1 = 1.02f, v0 = 0.585f, v1 = 0.935f;
            var topC = new Color(0.58f, 0.40f, 0.22f); var side = new Color(0.36f, 0.24f, 0.14f);
            mb.Quad(P(u0, v1, _tableY), P(u0, v0, _tableY), P(u1, v0, _tableY), P(u1, v1, _tableY), topC);
            var a = P(u0, v1, _tableY); var b = P(u1, v1, _tableY);
            mb.Quad(new Vector3(a.x, 0.03f, a.z), a, b, new Vector3(b.x, 0.03f, b.z), side);
            var c = P(u0, v0, _tableY);
            mb.Quad(new Vector3(a.x, 0.03f, a.z), new Vector3(c.x, 0.03f, c.z), c, a, side * 0.9f);
            var go = mb.ToObject("Table", Root, CouncilShapes.VertexMaterial);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            // three trays with their sockets
            for (int i = 0; i < 3; i++)
            {
                var centre = TrayCentre(i);
                var l = L.OnFloor((24.6f + 18f * i) / 100f, 0.77f, _tableY); var r = L.OnFloor((41.4f + 18f * i) / 100f, 0.77f, _tableY);
                var near = L.OnFloor(0.33f + 0.18f * i, 0.88f, _tableY); var far = L.OnFloor(0.33f + 0.18f * i, 0.66f, _tableY);
                float w = Mathf.Abs(r.x - l.x), dep = Mathf.Abs(far.z - near.z);
                var tr = new GameObject("Tray " + i).transform; tr.SetParent(Root, false); tr.localPosition = centre;
                Cube(tr, "Board", new Vector3(0, 0.04f, 0), new Vector3(w, 0.08f, dep), new Color(0.46f, 0.30f, 0.16f));
                Cube(tr, "Rim", new Vector3(0, 0.09f, 0), new Vector3(w * 0.98f, 0.04f, dep * 0.96f), new Color(0.38f, 0.25f, 0.13f));
                for (int s = 0; s < 2; s++)
                {
                    var sf = SocketFoot(i, s) - centre;
                    Cyl(tr, "Socket " + s, new Vector3(sf.x, 0.11f, sf.z), new Vector3(w * 0.36f, 0.01f, dep * 0.46f), new Color(0.16f, 0.10f, 0.06f));
                }
                var glow = Halo(tr, new Vector3(0, 0.12f, 0), w * 0.8f, new Color(1f, 0.85f, 0.45f));
                glow.transform.localRotation = Quaternion.Euler(90, 0, 0); glow.transform.localScale = new Vector3(w * 0.7f, dep * 0.6f, 1f);
                glow.SetActive(false); _trayGlow[i] = glow;
                _tray[i] = tr.gameObject; tr.gameObject.SetActive(false);
            }
            // the hand (press on), the heavy seal (bank) and the spare petition
            float hh = FigureHeight(HandFoot, 0.64f);
            _hand = CouncilShapes.Make("Hand", Root, CouncilShapes.Hand(false), HandFoot + new Vector3(0, 0, 0), hh);
            _handHalo = Halo(Root, HandFoot + new Vector3(0, hh * 0.45f, 0.2f), hh * 1.5f, new Color(1f, 0.85f, 0.4f)); _handHalo.SetActive(false);
            float sh = FigureHeight(SealFoot, 0.64f);
            _seal = CouncilShapes.Make("Heavy seal", Root, CouncilShapes.HeavySeal(false), SealFoot, sh);
            _sealHalo = Halo(Root, SealFoot + new Vector3(0, sh * 0.4f, 0.2f), sh * 1.5f, new Color(1f, 0.8f, 0.3f)); _sealHalo.SetActive(false);
            float ph = FigureHeight(PetitionFoot, 0.72f);
            _petition = CouncilShapes.Make("Petition", Root, CouncilShapes.Petition(false), PetitionFoot, ph);
            _petitionHalo = Halo(Root, PetitionFoot + new Vector3(0, ph * 0.3f, 0.2f), ph * 2f, new Color(1f, 0.95f, 0.6f)); _petitionHalo.SetActive(false);
            _petition.SetActive(false);
        }

        void BuildPeople(SettlementCouncil sc)
        {
            // Candidates take the same place at the left of the table, one seat at a time (the active one shows).
            var feet = L.OnFloor(0.088f, 0.91f);
            float h = FigureHeight(feet, 0.625f);
            for (int s = 0; s < 4; s++)
            {
                var a = Person("cand" + s, Color.Lerp(SeatColor(s), new Color(0.5f, 0.4f, 0.3f), 0.4f), feet, h, 180f - 14f);
                a.name = "Candidate " + s;
                var emb = CouncilShapes.Make("Chest emblem", a.BodyPivot != null ? a.BodyPivot : a.transform, Emblem(s), new Vector3(0, 1.12f, 0.22f), 0.34f);
                emb.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var sp = a.gameObject.AddComponent<CouncilSpeaker>(); sp.Id = "cand" + s; sp.Name = sc != null && sc.Candidates.Count > s ? sc.Candidates[s].Name : "Candidate"; sp.Head = a.HeadPivot != null ? a.HeadPivot : a.transform;
                _candSpeaker[s] = sp; _cand[s] = a;
                a.gameObject.SetActive(false);
            }
            var cf = L.OnFloor(0.955f, 0.60f);
            _clerk = Person("clerk", new Color(0.55f, 0.45f, 0.30f), cf, FigureHeight(cf, 0.395f), 180f);
            _clerk.name = "Clerk";
            var af = L.OnFloor(0.07f, 0.985f);
            _aide = Person("aide", new Color(0.35f, 0.55f, 0.45f), af, FigureHeight(af, 0.80f), 180f - 30f);
            _aide.name = "Aide";
            _aide.gameObject.SetActive(false);
        }

        void BuildBenchEnd()
        {
            var foot = L.OnFloor(0.065f, 0.985f);
            float h = FigureHeight(foot, 0.935f);
            // the bench end: a low plank, with the lantern standing on it (the cue for turning left)
            Cube(Root, "Bench end", L.OnFloor(0.055f, 0.99f) + new Vector3(0, 0.12f, 0), new Vector3(0.28f * L.Scale * 3f, 0.24f, 0.5f * L.Scale), new Color(0.30f, 0.20f, 0.12f));
            _lantern = CouncilShapes.Make("Bench lantern", Root, CouncilShapes.LanternMesh(), foot + new Vector3(0, 0.22f, 0), h);
            _lantern.GetComponent<Renderer>().sharedMaterial = MaterialFactory.CreateUnlit(new Color(1f, 0.9f, 0.6f), "Lantern glow");
            var f = WorldLights.Add(Root, foot + new Vector3(0, h * 0.8f, -0.4f), new Color(1f, 0.75f, 0.4f), 1.2f, 9f * L.Scale, 1, "Bench lantern light");
            f.GroundGlow = 2.5f; f.Halo = 0.6f;
        }

        void BuildFires()
        {
            // the council fire: a warm light high over the middle of the gathering, so firelight reaches every row
            var mid = L.OnFloor(0.5f, 0.45f) + new Vector3(0, 5.5f * L.Scale, 0);
            var cf = WorldLights.Add(Root, mid, new Color(1f, 0.70f, 0.38f), 1.5f, 34f * L.Scale, 3, "Council fire light");
            cf.GroundGlow = 0f; cf.Halo = 0f; cf.Sparks = false;
            for (int k = 0; k < 2; k++)
            {
                float u = k == 0 ? 0.095f : 0.905f;
                var p = L.OnFloor(u, 0.47f);
                var t = WorldLights.Torch(Root, ToWorld(p), false, true, "Council brazier " + k);
                _fires[k] = t.gameObject;
                t.transform.parent.localScale = Vector3.one * Mathf.Max(0.9f, 1.5f * L.Scale);
                t.Intensity = 2.3f; t.Range = 26f * Mathf.Max(0.6f, L.Scale); t.Priority = 3;
            }
        }

        // ================================================================== per frame

        public void SetPreview(IEnumerable<int> tracks, int tray, int socket)
        {
            _preview.Clear(); if (tracks != null) _preview.AddRange(tracks);
            _previewTray = tray; _previewSocket = socket;
        }

        public void ClearPreview() { _preview.Clear(); _previewTray = -1; _previewSocket = -1; }

        void Update()
        {
            var sc = Council;
            if (sc == null || sc.Election == null || State == null) return;
            float dt = Time.unscaledDeltaTime;
            Sync(sc, dt);
            Animate(sc, dt);
        }

        bool ActiveHandAvailable(ElectionState e) => e.Phase == ElectionPhase.Ready;
        public bool PlayerCanPush(SettlementCouncil sc) { var e = sc.Election; return e != null && e.PlayerSeat >= 0 && e.Active == e.PlayerSeat && e.Phase == ElectionPhase.Ready; }

        void Sync(SettlementCouncil sc, float dt)
        {
            var e = sc.Election;
            // which candidate stands at the table: the active seat's
            int act = e.Phase == ElectionPhase.Complete && e.Winner >= 0 ? e.Winner : e.Active;
            if (act != ActiveSeatShown)
            {
                ActiveSeatShown = act;
                for (int s = 0; s < 4; s++) if (_cand[s] != null) _cand[s].gameObject.SetActive(s == act);
            }
            for (int s = 0; s < 4; s++)
            {
                _bayHalo[s].SetActive(s == act && e.Phase != ElectionPhase.Complete);
                _bayFrame[s].SetActive(s == e.PlayerSeat);
            }
            SyncMarkers(sc);
            SyncSeals(sc);
            SyncLeadBanners(sc, dt);
            SyncTrays(sc);
            // hand, seal, petition: lit and pulsing when they can be pressed
            bool myTurn = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            bool handOn = e.Phase == ElectionPhase.Ready && myTurn;
            bool sealOn = (e.Phase == ElectionPhase.Ready && e.HasAdvanced && myTurn) || e.Phase == ElectionPhase.BustPending;
            bool petOn = e.Phase == ElectionPhase.BustPending && e.PetitionAvailable;
            SetLit(_hand, _handHalo, CouncilShapes.Hand(true), CouncilShapes.Hand(false), handOn, _handPress);
            SetLit(_seal, _sealHalo, CouncilShapes.HeavySeal(true), CouncilShapes.HeavySeal(false), sealOn, _sealPress);
            bool petVisible = e.PetitionAvailable && _carryT < 0f && !HoldPetition;
            if (_petition.activeSelf != petVisible) _petition.SetActive(petVisible);
            if (petVisible) SetLit(_petition, _petitionHalo, CouncilShapes.Petition(true), CouncilShapes.Petition(false), petOn, _petitionLift);
            else _petitionHalo.SetActive(false);
        }

        void SetLit(GameObject go, GameObject halo, Mesh lit, Mesh dim, bool on, float press)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf.sharedMesh != (on ? lit : dim)) mf.sharedMesh = on ? lit : dim;
            if (halo != null)
            {
                halo.SetActive(on);
                if (on) halo.transform.localScale = halo.transform.localScale.normalized * 0 + Vector3.one * Mathf.Max(0.001f, halo.transform.localScale.x);
            }
            float pulse = on ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            var home = go.GetComponent<CouncilHome>();
            if (home == null) { home = go.AddComponent<CouncilHome>(); home.Scale = go.transform.localScale; home.Pos = go.transform.localPosition; }
            go.transform.localScale = home.Scale * pulse * (1f - 0.12f * press);
            go.transform.localPosition = home.Pos + new Vector3(0, -0.12f * press * home.Scale.y + (on ? 0.02f * Mathf.Sin(Time.unscaledTime * 5f) * home.Scale.y : 0f), 0);
        }

        sealed class CouncilHome : MonoBehaviour { public Vector3 Scale, Pos; }

        // ---- markers: banked posts and temporary catspaws ----

        Vector3 MarkerPos(int seat, int track, int place, bool paw)
        {
            var uv = L.PlaceUV(track, place);
            float lat = paw ? -0.004f : (seat - 1.5f) * 0.010f - 0.010f;
            float dv = paw ? 0.0035f : 0f;
            return L.OnFloor(uv.x + lat, uv.y + dv);
        }

        float MarkerHeight(Vector3 pos, bool paw)
        {
            var uv = L.Project(pos);
            return L.HeightFor(pos, uv.y - (paw ? 0.135f : 0.125f));
        }

        void SyncMarkers(SettlementCouncil sc)
        {
            var e = sc.Election;
            var wanted = new HashSet<int>();
            for (int s = 0; s < 4; s++)
                for (int t = 2; t <= 12; t++)
                {
                    int claimed = e.Claimed[t - 2];
                    int b = e.BankedAt(s, t);
                    int place = 0;
                    if (claimed >= 0) { if (s == claimed) place = CouncilRules.Length(t); }
                    else place = b;
                    if (place <= 0) continue;
                    int key = s * 16 + t; wanted.Add(key);
                    if (!_posts.TryGetValue(key, out var m))
                    {
                        var go = new GameObject("Post " + s + "." + t); go.transform.SetParent(Root, false);
                        CouncilShapes.Make("Pole", go.transform, CouncilShapes.Post(), Vector3.zero, 0.75f);
                        CouncilShapes.Make("Emblem", go.transform, Emblem(s), new Vector3(0, 1.0f, -0.04f), 0.95f);
                        m = new Mover { Go = go, Seat = s, Track = t, Place = place };
                        m.Pos = MarkerPos(s, t, place, false);
                        go.transform.localPosition = m.Pos; _posts[key] = m;
                        m.Target = m.Pos;
                    }
                    m.Place = place; m.Target = MarkerPos(s, t, place, false);
                }
            foreach (var key in new List<int>(_posts.Keys)) if (!wanted.Contains(key)) { Destroy(_posts[key].Go); _posts.Remove(key); }

            // the temporary catspaws wear the active seat's emblem
            var wantedPaw = new HashSet<int>();
            for (int t = 2; t <= 12; t++)
            {
                int p = e.Temp[t - 2];
                if (p <= 0 || e.Phase == ElectionPhase.Complete) continue;
                wantedPaw.Add(t);
                if (!_paws.TryGetValue(t, out var m) || m.Seat != e.Active)
                {
                    if (m != null) { Destroy(m.Go); _paws.Remove(t); }
                    var go = new GameObject("Catspaw " + t); go.transform.SetParent(Root, false);
                    var body = CouncilShapes.Make("Body", go.transform, CouncilShapes.Catspaw(SeatColor(e.Active)), Vector3.zero, 1f);
                    var emb = CouncilShapes.Make("Emblem", go.transform, Emblem(e.Active), new Vector3(0, 0.34f, -0.31f), 0.30f);
                    m = new Mover { Go = go, Seat = e.Active, Track = t, Place = p, Paw = true, Born = Time.unscaledTime };
                    m.Pos = PawSpawn; go.transform.localPosition = m.Pos; _paws[t] = m;
                }
                m.Place = p; m.Target = MarkerPos(e.Active, t, p, true);
            }
            foreach (var t in new List<int>(_paws.Keys))
                if (!wantedPaw.Contains(t))
                {
                    var m = _paws[t]; _paws.Remove(t);
                    m.Leaving = true; m.LeaveTo = PawSpawn; _leaving.Add(m);
                }
        }

        void SyncSeals(SettlementCouncil sc)
        {
            var e = sc.Election;
            var perSeat = new int[4];
            var wanted = new HashSet<int>();
            for (int t = 2; t <= 12; t++)
            {
                int w = e.Claimed[t - 2]; if (w < 0) continue;
                int k = perSeat[w]++; if (k > 2) k = 2;
                int key = w * 16 + t; wanted.Add(key);
                if (!_seals.TryGetValue(key, out var go))
                {
                    go = new GameObject("Seal " + w + "." + t); go.transform.SetParent(Root, false);
                    float beamH = L.HeightFor(_hookPos[w, k], 0.105f);
                    var disc = CouncilShapes.Make("Seal", go.transform, CouncilShapes.Seal(SeatColor(w)), Vector3.zero, 1f);
                    CouncilShapes.Make("Carving", go.transform, CouncilShapes.Prop(t, true), new Vector3(0, -0.28f, -0.14f), 0.56f);
                    go.transform.localPosition = SealFoot + new Vector3(0, 0.5f, 0);
                    go.transform.localScale = Vector3.one * 1.2f;
                    var home = go.AddComponent<SealFly>(); home.Target = _hookPos[w, k] + new Vector3(0, -0.3f, 0); home.Scale = Mathf.Max(0.3f, L.HeightFor(_hookPos[w, k], 0.045f + 0.0f) );
                    _seals[key] = go;
                }
                else { var f = go.GetComponent<SealFly>(); if (f != null) f.Target = _hookPos[w, k] + new Vector3(0, -0.3f, 0); }
            }
            foreach (var key in new List<int>(_seals.Keys)) if (!wanted.Contains(key)) { Destroy(_seals[key]); _seals.Remove(key); }
        }

        sealed class SealFly : MonoBehaviour
        {
            public Vector3 Target; public float Scale = 1f; float _t;
            void Update()
            {
                _t += Time.unscaledDeltaTime;
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, Target, Time.unscaledDeltaTime * 14f * CouncilLayout.Scale);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * Scale, Time.unscaledDeltaTime * 4f);
            }
        }

        void SyncLeadBanners(SettlementCouncil sc, float dt)
        {
            var e = sc.Election;
            for (int t = 2; t <= 12; t++)
            {
                int w = e.Claimed[t - 2];
                var b = _banner[t - 2];
                bool on = w >= 0;
                if (b.activeSelf != on) { b.SetActive(on); _bannerT[t - 2] = 0f; }
                if (on)
                {
                    var mf = b.GetComponent<MeshFilter>();
                    var want = CouncilShapes.Banner(SeatColor(w));
                    if (mf.sharedMesh != want) mf.sharedMesh = want;
                    _bannerT[t - 2] = Mathf.MoveTowards(_bannerT[t - 2], 1f, dt * 1.8f);
                    var home = _leadPropHome[t - 2];
                    float k = Mathf.SmoothStep(0.05f, 1f, _bannerT[t - 2]);
                    b.transform.localScale = new Vector3(1, k, 1) * (_leadPropScale[t - 2] * 1.2f);
                }
            }
        }

        // ---- trays and carvings ----

        void SyncTrays(SettlementCouncil sc)
        {
            var e = sc.Election;
            int count = e.Phase == ElectionPhase.Choosing ? e.Pending.Count : 0;
            for (int i = 0; i < 3; i++)
            {
                bool on = i < count;
                if (_tray[i].activeSelf != on) { _tray[i].SetActive(on); _trayPop[i] = 0f; if (!on) ClearCarvings(i); _traySig[i] = null; }
                if (!on) continue;
                string sig = e.PushId + ":" + i + ":" + Sig(e.Pending[i]);
                if (_traySig[i] != sig) { _traySig[i] = sig; RebuildCarvings(i, e.Pending[i]); }
            }
        }

        static string Sig(Tray t) => t.Mode + "/" + t.Sockets[0].Track + "x" + t.Sockets[0].Steps + "/" + t.Sockets[1].Track + "x" + t.Sockets[1].Steps;

        void ClearCarvings(int i) { foreach (var c in _carvings[i]) if (c != null) Destroy(c); _carvings[i].Clear(); }

        void RebuildCarvings(int i, Tray tray)
        {
            ClearCarvings(i);
            for (int s = 0; s < 2; s++)
            {
                var sock = tray.Sockets[s];
                if (sock == null || sock.Track == 0) continue;
                int copies = Mathf.Max(1, sock.Steps);
                var foot = SocketFoot(i, s);
                float hgt = L.HeightFor(foot, 0.675f);
                for (int c = 0; c < copies; c++)
                {
                    float off = copies == 1 ? 0f : (c - 0.5f * (copies - 1)) * 0.36f * hgt;
                    var go = CouncilShapes.Make("Carving " + i + "." + s + "." + c, _tray[i].transform, CouncilShapes.Prop(sock.Track, true), Vector3.zero, hgt * (copies == 1 ? 1f : 0.82f));
                    go.transform.position = Root.TransformPoint(foot + new Vector3(off, 0.1f, -0.02f * c));
                    go.transform.rotation = Root.rotation;
                    _carvings[i].Add(go);
                }
            }
        }

        // ---- animation ----

        void Animate(SettlementCouncil sc, float dt)
        {
            var e = sc.Election;
            MoversBusy = 0;
            float speed = 9f * L.Scale;
            foreach (var m in _posts.Values) MoveMarker(m, dt, speed * 1.2f, false);
            foreach (var m in _paws.Values) MoveMarker(m, dt, speed, true);
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                var m = _leaving[i];
                m.Target = m.LeaveTo;
                MoveMarker(m, dt, speed * 1.4f, true);
                if ((m.Pos - m.LeaveTo).sqrMagnitude < 0.01f) { Destroy(m.Go); _leaving.RemoveAt(i); }
            }
            _handPress = Mathf.MoveTowards(_handPress, 0f, dt * 3f);
            _sealPress = Mathf.MoveTowards(_sealPress, 0f, dt * 3f);
            _petitionLift = Mathf.MoveTowards(_petitionLift, 0f, dt * 2f);
            if (_carryT >= 0f) { _carryT += dt / 2.4f; AnimateCarry(); if (_carryT >= 1f) { _carryT = -1f; _aide.gameObject.SetActive(false); } }
            // trays rise in; the preview glow and the leads' reaction follow the pointer
            for (int i = 0; i < 3; i++)
            {
                if (!_tray[i].activeSelf) continue;
                _trayPop[i] = Mathf.MoveTowards(_trayPop[i], 1f, dt * 4f);
                float k = Mathf.SmoothStep(0, 1, _trayPop[i]);
                _tray[i].transform.localScale = new Vector3(1, Mathf.Max(0.01f, k), 1);
                bool hover = _previewTray == i;
                _trayGlow[i].SetActive(hover);
                foreach (var c in _carvings[i])
                    if (c != null)
                    {
                        var bobY = hover ? 0.02f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
                        c.transform.localScale = c.transform.localScale; // size is fixed at creation
                        c.SetActive(k > 0.3f);
                    }
                if (_trayPop[i] < 1f) MoversBusy++;
            }
            for (int t = 2; t <= 12; t++)
            {
                int i = t - 2;
                bool on = _preview.Contains(t);
                _react[i] = Mathf.MoveTowards(_react[i], on ? 1f : 0f, dt * 6f);
                _pulse[i] = Mathf.MoveTowards(_pulse[i], 0f, dt * 1.5f);
                float lift = _react[i] * 0.18f + _pulse[i] * 0.16f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f));
                var prop = _leadProp[i];
                prop.transform.localPosition = _leadPropHome[i] + new Vector3(0, lift * _leadPropScale[i], 0);
                prop.transform.localScale = Vector3.one * _leadPropScale[i] * (1f + 0.12f * _react[i]);
                _leadRing[i].SetActive(_react[i] > 0.05f);
                _leadRing[i].transform.localScale = _leadRing[i].transform.localScale.normalized * 0 + Vector3.one * _leadPropScale[i] * 0.95f * (0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f)) * Mathf.Max(0.01f, _react[i]);
                // the lead turns the head to the camera and leans in while previewed
                var lead = _lead[i];
                if (lead.HeadPivot != null) lead.HeadPivot.localRotation = Quaternion.Euler(-12f * _react[i], 0, 6f * _react[i]);
            }
            // residents: those at or below a marker turn towards the candidate's side; witnesses who walked off return
            for (int t = 2; t <= 12; t++)
            {
                int len = CouncilRules.Length(t);
                int top = 0;
                for (int s = 0; s < 4; s++) top = Mathf.Max(top, e.BankedAt(s, t));
                top = Mathf.Max(top, e.Temp[t - 2]);
                for (int p = 1; p <= len; p++)
                {
                    var go = _stand[t - 2][p - 1];
                    float walk = _standWalk[t - 2][p - 1];
                    if (walk > 0f) { _standWalk[t - 2][p - 1] = Mathf.MoveTowards(walk, 0f, dt * 0.35f); }
                    float away = walk > 0f ? Mathf.Sin(Mathf.Clamp01(walk) * Mathf.PI * 0.5f) : 0f;
                    var home = _standHome[t - 2][p - 1];
                    go.transform.localPosition = home + new Vector3(away * 3.2f * L.Scale, 0, 0);
                    bool supporting = p <= top;
                    float yaw = walk > 0f ? 90f : (supporting ? Mathf.Sin(p * 1.7f) * 8f - 22f : Mathf.Sin(p * 2.3f + t) * 25f + 10f);
                    var q = Quaternion.Euler(0, yaw, 0);
                    go.transform.localRotation = Quaternion.Slerp(go.transform.localRotation, q, dt * 4f);
                    float bob = supporting ? 0.02f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f + p + t)) : 0f;
                    go.transform.localPosition += new Vector3(0, bob, 0);
                }
            }
            // the figure at the table follows the turn
            if (ActiveSeatShown >= 0 && _cand[ActiveSeatShown] != null)
            {
                var c = _cand[ActiveSeatShown];
                if (_winT >= 0f) { _winT += dt; c.transform.localPosition = Vector3.Lerp(c.transform.localPosition, L.OnFloor(0.32f, 0.56f), dt * 1.5f); }
            }
            _clerk.PlayIdle();
            if (_lastPhase != (int)e.Phase)
            {
                _lastPhase = (int)e.Phase;
            }
            if (MoversBusy == 0 && _leaving.Count > 0) MoversBusy++;
        }

        void MoveMarker(Mover m, float dt, float speed, bool paw)
        {
            var before = m.Pos;
            m.Pos = Vector3.MoveTowards(m.Pos, m.Target, speed * dt);
            bool moving = (m.Pos - m.Target).sqrMagnitude > 1e-4f;
            if (moving) MoversBusy++;
            float bob = moving && paw ? 0.18f * L.Scale * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 12f)) : 0f;
            m.Go.transform.localPosition = m.Pos + new Vector3(0, bob, 0);
            float h = MarkerHeight(m.Pos, paw);
            m.Go.transform.localScale = Vector3.one * (paw ? h / 0.95f : h / 1.5f);
            var d = m.Target - before; d.y = 0;
            m.Go.transform.localRotation = Quaternion.identity;
        }

        void AnimateCarry()
        {
            var from = L.OnFloor(0.07f, 0.985f); var to = L.OnFloor(0.90f, 0.96f);
            float t = Mathf.Clamp01(_carryT);
            _aide.transform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
            if (t > 0.92f) _petition.SetActive(true);
        }

        // ================================================================== events

        public void BeginCarry()
        {
            _carryT = 0f; _aide.gameObject.SetActive(true);
            _aide.transform.localPosition = L.OnFloor(0.07f, 0.985f);
            _petition.SetActive(false);
        }

        public void Press(string what)
        {
            if (what == "hand") { _handPress = 1f; Candidate()?.PlayOneShot(ActorKeys.Beckon); }
            else if (what == "seal") { _sealPress = 1f; _clerk.PlayOneShot(ActorKeys.Hammer); }
            else if (what == "petition") _petitionLift = 1f;
        }

        PlaceholderActor Candidate() => ActiveSeatShown >= 0 ? _cand[ActiveSeatShown] : null;

        public void Pulse(int track) { if (track >= 2 && track <= 12) _pulse[track - 2] = 1f; }

        public void Cheer(int track) { if (track >= 2 && track <= 12) _lead[track - 2].PlayOneShot(ActorKeys.Cheer); }

        public void Witness(int track, int place)
        {
            if (track < 2 || track > 12) return;
            int len = CouncilRules.Length(track);
            int p = Mathf.Clamp(place, 1, len);
            _standWalk[track - 2][p - 1] = 1f; WitnessesAway++;
            _lead[track - 2].PlayOneShot(ActorKeys.Surprise);
        }

        /// <summary>Residents greet and shake hands at a place when the dispute moves (cosmetic).</summary>
        public void Greet(int track, int place)
        {
            if (track < 2 || track > 12) return;
            Handshakes++;
            _lead[track - 2].PlayOneShot(ActorKeys.Greet);
        }

        public void CandidateSay(string text, float seconds = 4f)
        {
            var c = ActiveSeatShown >= 0 ? _candSpeaker[ActiveSeatShown] : null; if (c == null) return;
            LastCandidateLine = text;
            { if (_candBubble != 0) SpeechBubbles.Close(_candBubble); _candBubble = CouncilBubbles.Say(c, text, seconds); }
        }

        public void SayLead(int track, string text, float seconds = 3.6f)
        {
            if (track < 2 || track > 12 || string.IsNullOrEmpty(text)) return;
            if (_leadBubble != 0) SpeechBubbles.Close(_leadBubble);
            _leadBubble = CouncilBubbles.Say(_leadSpeaker[track - 2], text, seconds);
            LeadBubbles++; LastLeadLine = text;
        }

        public void CloseBubbles() { if (_leadBubble != 0) SpeechBubbles.Close(_leadBubble); _leadBubble = 0; if (_candBubble != 0) SpeechBubbles.Close(_candBubble); _candBubble = 0; }

        public void StartWin() { _winT = 0f; }

        /// <summary>Plays the scene's reaction to one council event (animation and dialogue only).</summary>
        public void Handle(CouncilEvent ev, SettlementCouncil sc)
        {
            if (sc == null) return;
            int seat = Mathf.Max(0, ev.Seat);
            switch (ev)
            {
                case CouncilPushMade _: Press("hand"); break;
                case CouncilAdvanceMade a:
                    foreach (var st in a.Steps)
                    {
                        Pulse(st.Track); Greet(st.Track, st.To);
                    }
                    if (a.Steps.Count > 0)
                    {
                        int t = a.Steps[0].Track;
                        bool first = _spoken.Add(t);
                        SayLead(t, CouncilApi.DisputeLine(sc, t, first ? CouncilContent.OpeningLine : CouncilContent.ProgressLine, _lineCounter++, a.Seat, -1));
                    }
                    break;
                case CouncilBanked b:
                    Press("seal");
                    foreach (var p in b.Places) Pulse(p.Track);
                    break;
                case CouncilTrackClaimed c:
                    Pulse(c.Track); Cheer(c.Track);
                    SayLead(c.Track, CouncilApi.DisputeLine(sc, c.Track, CouncilContent.AgreementLine, _lineCounter++, c.Seat, c.Seat), 4.2f);
                    break;
                case CouncilBust bust:
                    foreach (var l in bust.Lost) Witness(l.Track, l.Place);
                    if (bust.Lost.Count > 0)
                        SayLead(bust.Lost[0].Track, CouncilApi.DisputeLine(sc, bust.Lost[0].Track, CouncilContent.SetbackLine, _lineCounter++, bust.Seat, -1));
                    break;
                case CouncilPetitionSpent _: Press("petition"); break;
                case CouncilTurnPassed tp:
                    // a lead whose dispute was left open revisits it
                    for (int t = 2; t <= 12; t++)
                        if (sc.Election.Claimed[t - 2] < 0 && _spoken.Contains(t) && sc.Election.BankedAt(tp.To, t) > 0)
                        { SayLead(t, CouncilApi.DisputeLine(sc, t, CouncilContent.RevisitLine, _lineCounter++, tp.To, -1)); break; }
                    break;
                case CouncilElectionWon _: StartWin(); break;
            }
        }

        /// <summary>The solid mesh bounds of a part in the forward camera's safe-area coordinates (for tests).</summary>
        public static bool ProjectBounds(GameObject go, out Rect safe)
        {
            safe = default;
            var rs = go != null ? go.GetComponentsInChildren<Renderer>() : null;
            if (rs == null || rs.Length == 0) return false;
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            var scene = Current;
            if (scene == null) return false;
            float minU = 9, minV = 9, maxU = -9, maxV = -9;
            for (int xi = -1; xi <= 1; xi += 2) for (int yi = -1; yi <= 1; yi += 2) for (int zi = -1; zi <= 1; zi += 2)
            {
                var w = b.center + Vector3.Scale(b.extents, new Vector3(xi, yi, zi));
                var p = L.Project(scene.Root.InverseTransformPoint(w));
                minU = Mathf.Min(minU, p.x); maxU = Mathf.Max(maxU, p.x); minV = Mathf.Min(minV, p.y); maxV = Mathf.Max(maxV, p.y);
            }
            safe = Rect.MinMaxRect(minU, minV, maxU, maxV);
            return true;
        }

        /// <summary>Waits-for helper for tests and scripts: the catspaw and post views equal the engine state.</summary>
        public string ViewMismatch(SettlementCouncil sc)
        {
            var e = sc.Election;
            if (e == null) return "no election";
            for (int t = 2; t <= 12; t++)
            {
                int p = e.Temp[t - 2];
                bool shown = _paws.TryGetValue(t, out var m);
                if ((p > 0 && e.Phase != ElectionPhase.Complete) != shown) return "catspaw on track " + t + " shown=" + shown + " state=" + p;
                if (shown && m.Place != p) return "catspaw track " + t + " at " + m.Place + " state " + p;
                if (shown && (m.Pos - m.Target).sqrMagnitude > 1e-3f) return "catspaw track " + t + " still walking";
            }
            for (int s = 0; s < 4; s++)
                for (int t = 2; t <= 12; t++)
                {
                    int claimed = e.Claimed[t - 2];
                    int want = claimed >= 0 ? (s == claimed ? CouncilRules.Length(t) : 0) : e.BankedAt(s, t);
                    bool shown = _posts.TryGetValue(s * 16 + t, out var m);
                    if ((want > 0) != shown) return "post seat " + s + " track " + t + " shown=" + shown + " state=" + want;
                    if (shown && m.Place != want) return "post seat " + s + " track " + t + " at " + m.Place + " state " + want;
                }
            return null;
        }
    }
}
