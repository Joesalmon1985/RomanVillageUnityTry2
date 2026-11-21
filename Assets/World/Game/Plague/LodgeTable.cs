using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;

namespace World.Game
{
    /// <summary>What a vessel on the healer's shelf shows (PLG-CURES): empty, cured (clear light), eradicated (sealed lid), strengthened (dark and cracked).</summary>
    public enum VesselState { Empty, Cured, Eradicated, Strengthened }

    /// <summary>A lodge prop that only tells: a tap opens its <see cref="LedgerCard"/> (inspection only, owner's answer Q-4).</summary>
    public sealed class LodgeInspect : MonoBehaviour, IInteractable, IInspectable, IWorldActionAnchor
    {
        public string PromptText = "Look closer";
        public Func<string> TitleFn;
        public Func<IEnumerable<string>> LinesFn;
        public float Reach = 6f;
        public string Prompt => PromptText;
        public string Title => TitleFn != null ? TitleFn() : name;
        public IEnumerable<string> Lines => LinesFn != null ? LinesFn() : new string[0];
        public Transform Anchor => transform;
        public float KeyReach => Reach;
        public void Interact() { WorldActions.Inspect(this); }
    }

    /// <summary>
    /// The plague at the healer's lodge (WU-3, WORLD_UI_PLAN 2.3), inspection only: a carved map table of the island (48 shrine
    /// pebbles, trails, crossings, the tunnel, demon figures by kind), a warning post (a lantern per outbreak, of 8), a rate stick
    /// with a peg, four element vessels, the runner's satchel, the banish charm's peg and a round-draw sign. Built from
    /// <see cref="PlagueGraphData"/> and <see cref="PlagueState"/>; reads state every frame, never writes it. The props stand in
    /// the lodge's yard in front of the door (the lodge itself is a closed hut). They show only while the plague runs.
    /// </summary>
    public sealed class LodgeTable : MonoBehaviour
    {
        public static LodgeTable Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public const float TableY = 1.05f, TableW = 2.9f, TableD = 2.9f; // P2b: the island is as deep as it is wide, so the table is square and the island fills it
        public static readonly Vector2 TableSpot = new Vector2(0f, 13f), PostSpot = new Vector2(-8.2f, 8.5f), StickSpot = new Vector2(-5.9f, 8.7f),
            ShelfSpot = new Vector2(7.4f, 8.0f), SatchelSpot = new Vector2(3.6f, 9.8f), CharmSpot = new Vector2(0.7f, 8.4f), SignSpot = new Vector2(-3.2f, 10.2f);

        public HealersLodge Lodge { get; private set; }
        public Transform Props { get; private set; }
        public Vector3 TableCentre { get; private set; }
        public LodgeInspect Table, Post, Stick, Satchel, Charm, Sign;
        public readonly LodgeInspect[] Vessels = new LodgeInspect[4];
        public readonly List<LodgeInspect> NodeTaps = new List<LodgeInspect>();
        private readonly LodgeInspect[] _nodeLi = new LodgeInspect[PlagueGraphData.NodeCount];
        /// <summary>Half the island's size on the table, in metres (east-west, north-south).</summary>
        public float IslandHalfW { get; private set; }
        public float IslandHalfD { get; private set; }
        public bool PropsShown => Props != null && Props.gameObject.activeSelf;

        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private PlagueState P => S.Plague;
        private float _scale, _cx, _cz;
        private Transform _tableRoot;
        private readonly Transform[] _nodeT = new Transform[PlagueGraphData.NodeCount];
        private readonly List<GameObject>[] _figs = new List<GameObject>[PlagueGraphData.NodeCount];
        private readonly int[] _tk = new int[PlagueGraphData.NodeCount * 4];
        private readonly float[] _pop = new float[PlagueGraphData.NodeCount];
        private readonly GameObject[] _flames = new GameObject[PlagueRules.OutbreaksToFall];
        private Light _postLight;
        private Transform _peg;
        private GameObject _charmTip;
        private readonly GameObject[] _vLight = new GameObject[4], _vLid = new GameObject[4], _vSoot = new GameObject[4], _vCrack = new GameObject[4];
        private int _litShown = -1, _pegShown = -1; private readonly int[] _vesselShown = { -1, -1, -1, -1 };
        private bool _anyPop;

        public static LodgeTable Build(HealersLodge lodge, WorldBootstrap boot)
        {
            if (boot?.Sim?.State?.Plague == null) return null;
            var holder = new GameObject("Lodge plague");
            holder.transform.SetParent(lodge.transform, false);
            var t = holder.AddComponent<LodgeTable>();
            t.Lodge = lodge; t._boot = boot; Instance = t;
            var go = new GameObject("Lodge plague props");
            go.transform.SetParent(holder.transform, false);
            t.Props = go.transform;
            go.transform.SetPositionAndRotation(lodge.Frame.O, Quaternion.Euler(0, lodge.Frame.Yaw, 0));
            t.BuildTable();
            t.BuildPost();
            t.BuildStick();
            t.BuildVessels();
            t.BuildSatchel();
            t.BuildCharm();
            t.BuildSign();
            LandViews.AddClearing(t.TableCentre.x, t.TableCentre.z, 5f);
            boot.Clock.Ticked += t.OnTicked;
            t.SyncAll(true);
            return t;
        }

        private void OnDestroy() { if (_leaning) EndLean(false); if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked; if (Instance == this) Instance = null; }

        private Vector3 L(Vector2 spot, float y = 0f) => new Vector3(spot.x, y, spot.y);

        private Transform Child(string name, Vector2 spot)
        {
            var g = new GameObject(name); g.transform.SetParent(Props, false); g.transform.localPosition = L(spot); return g.transform;
        }

        private LodgeInspect Tap(Transform t, string zone, Vector3 centre, Vector3 size, string prompt, Func<string> title, Func<IEnumerable<string>> lines, float reach = 7f)
        {
            LandLook.PickBox(t.gameObject, centre, size);
            var li = t.gameObject.AddComponent<LodgeInspect>();
            li.PromptText = prompt; li.TitleFn = title; li.LinesFn = lines; li.Reach = reach;
            WorldZone.Tag(t.gameObject, zone);
            WorldActions.Register(li);
            return li;
        }

        // ---------------------------------------------------------------- the carved map table (PLG-MAP)

        private void BuildTable()
        {
            var topo = S.Board.Topology;
            var reg = PlagueViews.HexRegions(topo, out var soft);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int h = 0; h < reg.Length; h++)
            {
                if (reg[h] < 0) continue;
                foreach (int c in topo.HexCorners(h)) { var v = BoardSpace.Corner(topo, c); minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z); }
            }
            for (int i = 0; i < PlagueGraphData.NodeCount; i++) { minX = Mathf.Min(minX, PlagueGraphData.X[i]); maxX = Mathf.Max(maxX, PlagueGraphData.X[i]); minZ = Mathf.Min(minZ, PlagueGraphData.Z[i]); maxZ = Mathf.Max(maxZ, PlagueGraphData.Z[i]); }
            _cx = (minX + maxX) * 0.5f; _cz = (minZ + maxZ) * 0.5f;
            _scale = Mathf.Min((TableW - 0.2f) / (maxX - minX), (TableD - 0.2f) / (maxZ - minZ));
            IslandHalfW = (maxX - minX) * 0.5f * _scale; IslandHalfD = (maxZ - minZ) * 0.5f * _scale;

            var root = Child("Map table", TableSpot);
            root.rotation = Quaternion.identity; // the carved island keeps the world's compass
            _tableRoot = root;
            TableCentre = root.position;
            var mb = new MeshBuilder();
            var wood = CampBuilder.Wood; var light = CampBuilder.WoodLight;
            for (int i = 0; i < 4; i++)
                mb.Box(new Vector3((i % 2 - 0.5f) * (TableW - 0.3f), (TableY - 0.1f) * 0.5f, (i / 2 - 0.5f) * (TableD - 0.3f)), new Vector3(0.2f, TableY - 0.1f, 0.2f), wood);
            mb.Box(new Vector3(0, TableY - 0.1f, 0), new Vector3(TableW, 0.2f, TableD), light);
            // A raised rim and the clay bed the island is carved in.
            float rim = 0.07f;
            mb.Box(new Vector3(0, TableY + 0.0f, -TableD * 0.5f + rim * 0.5f), new Vector3(TableW, 0.05f, rim), wood);
            mb.Box(new Vector3(0, TableY + 0.0f, TableD * 0.5f - rim * 0.5f), new Vector3(TableW, 0.05f, rim), wood);
            mb.Box(new Vector3(-TableW * 0.5f + rim * 0.5f, TableY + 0.0f, 0), new Vector3(rim, 0.05f, TableD), wood);
            mb.Box(new Vector3(TableW * 0.5f - rim * 0.5f, TableY + 0.0f, 0), new Vector3(rim, 0.05f, TableD), wood);
            mb.Box(new Vector3(0, TableY - 0.01f, 0), new Vector3(TableW - 0.14f, 0.04f, TableD - 0.14f), new Color(0.34f, 0.27f, 0.20f));
            Vector3 T(float x, float z, float y) => new Vector3((x - _cx) * _scale, TableY + y, (z - _cz) * _scale);
            // Hex plates by region (the carved island).
            for (int h = 0; h < reg.Length; h++)
            {
                if (reg[h] < 0) continue;
                var hc = BoardSpace.Hex(topo, h);
                var centre = T(hc.x, hc.z, 0.012f);
                var ring = new List<Vector3>();
                foreach (int c in topo.HexCorners(h)) { var v = BoardSpace.Corner(topo, c); ring.Add(Vector3.Lerp(centre, T(v.x, v.z, 0.012f), 0.95f)); }
                var col = Color.Lerp(new Color(0.55f, 0.46f, 0.34f), PlagueViews.ElementColour[reg[h]], soft[h] ? 0.14f : 0.34f);
                mb.FanUp(centre, ring, col);
            }
            // Trails: cream cords; crossings gold; the tunnel a dark groove.
            for (int t = 0; t < PlagueGraphData.TrailCount; t++)
            {
                int kind = PlagueGraphData.TrailKind[t];
                int a = PlagueGraphData.TrailA[t] - 1, b = PlagueGraphData.TrailB[t] - 1;
                var pa = T(PlagueGraphData.X[a], PlagueGraphData.Z[a], 0.018f); var pb = T(PlagueGraphData.X[b], PlagueGraphData.Z[b], 0.018f);
                if (kind == 0) mb.Strip(pa, pb, 0.022f, new Color(0.90f, 0.85f, 0.68f));
                else if (kind == 1) { mb.Strip(pa, pb, 0.04f, new Color(0.25f, 0.18f, 0.08f)); mb.Strip(pa + Vector3.up * 0.002f, pb + Vector3.up * 0.002f, 0.026f, new Color(1f, 0.80f, 0.22f)); }
                else
                {
                    // P2b: the tunnel is a groove with a pale violet lip, a black floor and a glowing seam: it reads from standing distance.
                    mb.Strip(pa + Vector3.up * 0.001f, pb + Vector3.up * 0.001f, 0.085f, new Color(0.78f, 0.62f, 0.92f));
                    mb.Strip(pa + Vector3.up * 0.003f, pb + Vector3.up * 0.003f, 0.06f, new Color(0.02f, 0.01f, 0.03f));
                    mb.Strip(pa + Vector3.up * 0.005f, pb + Vector3.up * 0.005f, 0.022f, new Color(0.62f, 0.32f, 0.85f));
                }
            }
            // The pebbles: one per node, coloured by its element.
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                int e = PlagueGraphData.Element[i];
                var c = T(PlagueGraphData.X[i], PlagueGraphData.Z[i], 0.024f);
                LandShapes.Disc(mb, c, 0.075f, 14, new Color(0.06f, 0.05f, 0.05f));
                LandShapes.Disc(mb, c + Vector3.up * 0.004f, 0.06f, 14, PlagueViews.ElementColour[e]);
                LandShapes.Disc(mb, c + Vector3.up * 0.008f, 0.02f, 8, Color.Lerp(PlagueViews.ElementColour[e], Color.white, 0.55f));
            }
            var body = mb.ToObject("Table", root, MaterialFactory.CreateVertexColored(Color.white, "Lodge table"));
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            Table = Tap(root, WorldZones.PlgMap, new Vector3(0, TableY + 0.1f, 0), new Vector3(TableW, 0.35f, TableD), "Read the map table", () => "The map table", TableLines, 6f);
            // A tap target and a ledger anchor on each shrine pebble.
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                int node = i + 1;
                var g = new GameObject("Table node " + node);
                g.transform.SetParent(root, false);
                g.transform.localPosition = T(PlagueGraphData.X[i], PlagueGraphData.Z[i], 0f);
                _nodeT[i] = g.transform; _figs[i] = new List<GameObject>();
                LandLook.PickBox(g, new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.2f, 0.12f));
                var li = g.AddComponent<LodgeInspect>();
                li.PromptText = "Look at this shrine"; li.Reach = 6f;
                li.TitleFn = () => PlagueViews.ElementName[PlagueApi.ElementOf(node)] + " shrine " + node;
                li.LinesFn = () => NodeLines(node);
                NodeTaps.Add(li);
                _nodeLi[i] = li;
            }
            HubArt.Glow(Props, root.position + Vector3.up * 2.4f, 9f, 1.3f);
            WalkGrid.AddSolid(root.position, 1.85f, HealersLodge.WalkSolidOwner);
        }

        private IEnumerable<string> TableLines()
        {
            var l = new List<string>
            {
                "The island carved in clay: 48 shrines joined by trails.",
                "Gold cords are crossings; the dark groove is the tunnel.",
            };
            int total = PlagueApi.TotalDemons(S);
            l.Add(total == 0 ? "No demons are loose." : total + " demon" + (total > 1 ? "s" : "") + " loose on the island.");
            string by = "";
            for (int k = 0; k < 4; k++) { int n = PlagueApi.DemonsOfKind(S, k); if (n > 0) by += (by.Length > 0 ? ", " : "") + PlagueViews.ElementName[k] + " " + n; }
            if (by.Length > 0) l.Add(by + ".");
            l.Add("Tap a pebble to see its shrine.");
            return l;
        }

        private IEnumerable<string> NodeLines(int node)
        {
            var l = new List<string>();
            int e = PlagueApi.ElementOf(node);
            l.Add("A shrine of the " + PlagueViews.ElementName[e].ToLower() + " region.");
            int total = PlagueApi.DemonsAt(S, node);
            if (total == 0) l.Add("No demons here.");
            for (int k = 0; k < 4; k++)
            {
                int n = PlagueApi.DemonsOfKindAt(S, node, k);
                if (n > 0) l.Add(n + " " + PlagueViews.ElementName[k].ToLower() + " demon" + (n > 1 ? "s" : "") + ".");
            }
            var topo = S.Board.Topology;
            int h = topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[node - 1], PlagueGraphData.HexR[node - 1]));
            if (h >= 0)
            {
                l.Add(PlagueApi.HexBlocked(S, h) ? "Its hex is blocked: nothing is gathered there." : "Its hex still gives its yield.");
            }
            if (total >= 3) l.Add("One more spirit here and the sickness breaks out.");
            return l;
        }

        // ---------------------------------------------------------------- demon figurines on the table

        private static Mesh FigureMesh(int kind) => LandLook.Cached("lodge/fig/" + kind, mb =>
        {
            var c = PlagueViews.ElementColour[kind];
            var body = Color.Lerp(c, new Color(0.1f, 0.03f, 0.05f), 0.3f);
            mb.Cylinder(Vector3.zero, 0.034f, 0.022f, 0.09f, 7, body);
            mb.Cylinder(Vector3.up * 0.09f, 0.03f, 0.02f, 0.04f, 7, Color.Lerp(body, Color.white, 0.15f));
            mb.Cone(new Vector3(-0.016f, 0.125f, 0), 0.008f, 0.04f, 4, new Color(0.96f, 0.92f, 0.8f));
            mb.Cone(new Vector3(0.016f, 0.125f, 0), 0.008f, 0.04f, 4, new Color(0.96f, 0.92f, 0.8f));
        });

        private void RebuildFigures(int i)
        {
            var list = _figs[i];
            foreach (var g in list) Destroy(g);
            list.Clear();
            int slot = 0;
            for (int k = 0; k < 4; k++)
                for (int j = 0; j < _tk[i * 4 + k]; j++, slot++)
                {
                    var g = new GameObject("Demon " + PlagueViews.ElementName[k]);
                    g.transform.SetParent(_nodeT[i], false);
                    // P2b: figurines stand on a ring wide enough that three of a mixed kind do not overlap (base radius 0.034).
                    int count = _tk[i * 4] + _tk[i * 4 + 1] + _tk[i * 4 + 2] + _tk[i * 4 + 3];
                    float a = (40f + slot * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                    float ring = count <= 1 ? 0f : count == 2 ? 0.045f : 0.068f;
                    g.transform.localPosition = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * ring + Vector3.up * 0.03f;
                    g.transform.localRotation = Quaternion.Euler(0, 180f + slot * 360f / Mathf.Max(1, count), 0);
                    LandLook.Part(g.transform, "Mesh", FigureMesh(k), null, false);
                    list.Add(g);
                }
        }

        /// <summary>Demons of a kind standing on the table's pebble for a node (the figures actually shown).</summary>
        public int TableDemons(int node, int kind) => node >= 1 && node <= PlagueGraphData.NodeCount ? _tk[(node - 1) * 4 + kind] : 0;
        public int TableFigures(int node) => node >= 1 && node <= PlagueGraphData.NodeCount ? _figs[node - 1].Count : 0;
        public Vector3 NodeOnTable(int node) => _nodeT[node - 1].position + Vector3.up * 0.04f;

        // ---------------------------------------------------------------- warning post (PLG-DANGER)

        private void BuildPost()
        {
            var root = Child("Warning post", PostSpot);
            var mb = new MeshBuilder();
            LandShapes.Beam(mb, new Vector3(0, 0, 0), new Vector3(0, 4.6f, 0), 0.13f, 6, CampBuilder.Wood);
            foreach (float y in new[] { 4.0f, 3.2f })
            {
                mb.Box(new Vector3(0, y, 0), new Vector3(2.5f, 0.1f, 0.1f), CampBuilder.WoodLight);
                mb.Box(new Vector3(0, y - 0.35f, 0), new Vector3(0.04f, 0.04f, 0.04f), CampBuilder.WoodLight);
            }
            mb.Cone(new Vector3(0, 4.55f, 0), 0.4f, 0.5f, 6, HubArt.Straw);
            LandLook.Part(root, "Post", mb.ToMesh("warning post"), null, true);
            var bodyMesh = LandLook.Cached("lodge/lantern", m =>
            {
                var wd = new Color(0.30f, 0.22f, 0.15f);
                m.Box(new Vector3(0, 0.02f, 0), new Vector3(0.26f, 0.04f, 0.26f), wd);
                foreach (float cx in new[] { -0.115f, 0.115f }) foreach (float cz in new[] { -0.115f, 0.115f }) m.Box(new Vector3(cx, 0.17f, cz), new Vector3(0.03f, 0.3f, 0.03f), wd);
                m.Cone(new Vector3(0, 0.32f, 0), 0.19f, 0.14f, 4, new Color(0.22f, 0.15f, 0.10f));
            });
            var flameMesh = LandLook.Cached("lodge/flame", m => m.Box(new Vector3(0, 0.17f, 0), new Vector3(0.17f, 0.24f, 0.17f), Color.white));
            for (int i = 0; i < PlagueRules.OutbreaksToFall; i++)
            {
                float y = i < 4 ? 4.0f : 3.2f;
                float x = (i % 4 - 1.5f) * 0.6f;
                var g = new GameObject("Lantern " + (i + 1));
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(x, y - 0.55f, 0.04f);
                LandLook.Part(g.transform, "Body", bodyMesh, null, false);
                var f = LandLook.Part(g.transform, "Flame", flameMesh, LandLook.Additive, false);
                PlagueFx.Tint(f, new Color(1f, 0.55f, 0.15f));
                _flames[i] = f;
            }
            var lg = new GameObject("Post light"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0, 3.2f, 1.2f);
            _postLight = lg.AddComponent<Light>(); _postLight.type = LightType.Point; _postLight.range = 9f; _postLight.color = new Color(1f, 0.45f, 0.25f); _postLight.intensity = 0f;
            Post = Tap(root, WorldZones.PlgDanger, new Vector3(0, 2.4f, 0), new Vector3(2.6f, 4.8f, 0.8f), "Read the warning post", () => "Warning post", PostLines, 9f);
        }

        private IEnumerable<string> PostLines()
        {
            var p = P; var l = new List<string>();
            l.Add("Outbreaks: " + p.Outbreaks + " of " + PlagueRules.OutbreaksToFall + ".");
            l.Add("One lantern is lit for each outbreak.");
            l.Add(p.IslandFell ? "The eighth lantern is lit: the island has fallen." : "At eight the island falls, for everyone.");
            l.Add("Rate: " + PlagueApi.Rate(S) + " demon" + (PlagueApi.Rate(S) > 1 ? "s" : "") + " drawn each round (step " + (p.RateIndex + 1) + " of " + PlagueRules.RateTrack.Length + ").");
            return l;
        }

        /// <summary>Lanterns lit now (tests).</summary>
        public int LanternsLit { get { int n = 0; foreach (var f in _flames) if (f != null && f.activeSelf) n++; return n; } }

        // ---------------------------------------------------------------- rate stick (PLG-DANGER)

        private void BuildStick()
        {
            var root = Child("Rate stick", StickSpot);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.2f, 0.4f), CampBuilder.Stone);
            mb.Box(new Vector3(0, 1.05f, 0), new Vector3(0.14f, 1.9f, 0.14f), CampBuilder.WoodLight);
            for (int i = 0; i < PlagueRules.RateTrack.Length; i++)
            {
                float y = StickY(i);
                float w = 0.1f + 0.05f * PlagueRules.RateTrack[i];
                mb.Box(new Vector3(0, y, 0.075f), new Vector3(w, 0.035f, 0.02f), new Color(0.12f, 0.08f, 0.05f));
            }
            LandLook.Part(root, "Stick", mb.ToMesh("rate stick"), null, true);
            var pegMesh = LandLook.Cached("lodge/peg", m => { m.Box(new Vector3(0, 0, 0.14f), new Vector3(0.07f, 0.07f, 0.34f), new Color(0.95f, 0.72f, 0.2f)); m.Box(new Vector3(0, 0, 0.32f), new Vector3(0.12f, 0.12f, 0.05f), new Color(0.85f, 0.25f, 0.1f)); });
            var pg = new GameObject("Peg"); pg.transform.SetParent(root, false);
            LandLook.Part(pg.transform, "Mesh", pegMesh, null, false);
            _peg = pg.transform;
            Stick = Tap(root, WorldZones.PlgDanger, new Vector3(0, 1.05f, 0.1f), new Vector3(0.9f, 2.0f, 0.7f), "Read the rate stick", () => "Rate stick", StickLines, 7f);
        }

        private static float StickY(int i) => 0.35f + 0.18f * i;

        private IEnumerable<string> StickLines()
        {
            var l = new List<string>();
            int step = P.RateIndex + 1;
            l.Add("Each round " + PlagueApi.Rate(S) + " shrine" + (PlagueApi.Rate(S) > 1 ? "s are" : " is") + " struck.");
            l.Add("The peg is at notch " + step + " of " + PlagueRules.RateTrack.Length + ".");
            l.Add("It climbs each time the sickness surges, and never falls back.");
            return l;
        }

        /// <summary>The notch the peg sits on, 1-based (tests).</summary>
        public int PegStep => _pegShown + 1;

        // ---------------------------------------------------------------- vessels (PLG-CURES)

        private void BuildVessels()
        {
            var shelf = Child("Vessel shelf", ShelfSpot);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.85f, 0), new Vector3(3.0f, 0.1f, 0.6f), CampBuilder.WoodLight);
            foreach (float x in new[] { -1.35f, 1.35f }) { mb.Box(new Vector3(x, 0.4f, -0.2f), new Vector3(0.1f, 0.8f, 0.1f), CampBuilder.Wood); mb.Box(new Vector3(x, 0.4f, 0.2f), new Vector3(0.1f, 0.8f, 0.1f), CampBuilder.Wood); }
            LandLook.Part(shelf, "Shelf", mb.ToMesh("vessel shelf"), null, true);
            for (int e = 0; e < 4; e++)
            {
                int el = e;
                var jar = new GameObject("Vessel " + PlagueViews.ElementName[e]);
                jar.transform.SetParent(shelf, false);
                jar.transform.localPosition = new Vector3(-1.05f + e * 0.7f, 0.9f, 0f);
                LandLook.Part(jar.transform, "Jar", LandLook.Cached("lodge/jar/" + e, m =>
                {
                    m.Cylinder(Vector3.zero, 0.2f, 0.26f, 0.46f, 10, new Color(0.62f, 0.50f, 0.38f));
                    m.Cylinder(Vector3.up * 0.46f, 0.15f, 0.15f, 0.08f, 10, new Color(0.55f, 0.44f, 0.33f));
                    m.Cylinder(Vector3.up * 0.22f, 0.265f, 0.265f, 0.07f, 10, PlagueViews.ElementColour[el]);
                }), null, false);
                _vSoot[e] = LandLook.Part(jar.transform, "Soot", LandLook.Cached("lodge/soot", m => m.Cylinder(Vector3.up * 0.01f, 0.272f, 0.272f, 0.45f, 10, new Color(0.05f, 0.04f, 0.04f))), null, false);
                _vCrack[e] = LandLook.Part(jar.transform, "Cracks", LandLook.Cached("lodge/cracks", m =>
                {
                    var red = new Color(1f, 0.28f, 0.1f);
                    m.Quad(new Vector3(-0.02f, 0.45f, 0.275f), new Vector3(0.03f, 0.45f, 0.275f), new Vector3(0.0f, 0.30f, 0.28f), new Vector3(-0.04f, 0.32f, 0.28f), red);
                    m.Quad(new Vector3(-0.04f, 0.32f, 0.28f), new Vector3(0.0f, 0.30f, 0.28f), new Vector3(0.09f, 0.14f, 0.28f), new Vector3(0.05f, 0.14f, 0.28f), red);
                    m.Quad(new Vector3(0.0f, 0.30f, 0.28f), new Vector3(0.04f, 0.31f, 0.28f), new Vector3(-0.1f, 0.2f, 0.28f), new Vector3(-0.12f, 0.19f, 0.28f), red);
                }), LandLook.Additive, false);
                _vLid[e] = LandLook.Part(jar.transform, "Lid", LandLook.Cached("lodge/lid", m => { m.Cylinder(Vector3.up * 0.5f, 0.2f, 0.19f, 0.1f, 10, new Color(0.50f, 0.50f, 0.52f)); m.Cylinder(Vector3.up * 0.6f, 0.06f, 0.05f, 0.06f, 8, new Color(0.40f, 0.40f, 0.42f)); }), null, false);
                _vLight[e] = LandLook.Part(jar.transform, "Light", LandLook.Cached("lodge/jarlight", m => LandShapes.FadeBeam(m, Vector3.up * 0.5f, 0.14f, 0.05f, 0.9f, 8, Color.white)), LandLook.Additive, false);
                PlagueFx.Tint(_vLight[e], new Color(0.75f, 0.95f, 1f));
                Vessels[e] = Tap(jar.transform, WorldZones.PlgCures, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.9f, 0.6f), "Look at the vessel",
                    () => PlagueViews.ElementName[el] + " vessel", () => VesselLines(el), 6f);
            }
        }

        public VesselState VesselOf(int element)
        {
            if (PlagueApi.IsStrengthened(S, element)) return VesselState.Strengthened;
            if (PlagueApi.IsEradicated(S, element)) return VesselState.Eradicated;
            if (PlagueApi.IsCured(S, element)) return VesselState.Cured;
            return VesselState.Empty;
        }

        /// <summary>The state the vessel is drawn in (tests: it must follow <see cref="VesselOf"/>).</summary>
        public VesselState VesselShown(int e)
        {
            if (_vCrack[e] != null && _vCrack[e].activeSelf) return VesselState.Strengthened;
            if (_vLid[e] != null && _vLid[e].activeSelf) return VesselState.Eradicated;
            if (_vLight[e] != null && _vLight[e].activeSelf) return VesselState.Cured;
            return VesselState.Empty;
        }

        private IEnumerable<string> VesselLines(int e)
        {
            string n = PlagueViews.ElementName[e], lo = n.ToLower();
            switch (VesselOf(e))
            {
                case VesselState.Strengthened: return new[] { "Dark and cracked: the Ash set it on their altar.", "Its " + lo + " demons now come in pairs." };
                case VesselState.Eradicated: return new[] { "Sealed with a stone lid.", "No " + lo + " demon is left on the island, and none can return." };
                case VesselState.Cured: return new[] { "Clear light: Mira restored the " + lo + " crystal.", "The " + lo + " sickness is cured: its demons are no longer placed." };
                default: return new[] { "Empty: no " + lo + " crystal has been given to it." };
            }
        }

        // ---------------------------------------------------------------- the runner's satchel (PLG-DECK)

        private void BuildSatchel()
        {
            var root = Child("Runner's satchel", SatchelSpot);
            var mb = new MeshBuilder();
            LandShapes.Beam(mb, Vector3.zero, new Vector3(0, 1.5f, 0), 0.07f, 6, CampBuilder.Wood);
            LandShapes.Beam(mb, new Vector3(0, 1.4f, 0), new Vector3(0, 1.4f, 0.45f), 0.045f, 5, CampBuilder.WoodLight);
            mb.Box(new Vector3(0, 1.0f, 0.5f), new Vector3(0.46f, 0.36f, 0.16f), CampBuilder.Hide);
            mb.Box(new Vector3(0, 1.2f, 0.51f), new Vector3(0.48f, 0.1f, 0.18f), CampBuilder.HideDark);
            for (int i = 0; i < 3; i++) mb.Cylinder(new Vector3(-0.1f + i * 0.1f, 1.22f, 0.4f), 0.04f, 0.03f, 0.1f, 5, new Color(0.7f, 0.68f, 0.62f));
            LandLook.Part(root, "Satchel", mb.ToMesh("satchel"), null, true);
            Satchel = Tap(root, WorldZones.PlgDeck, new Vector3(0, 0.9f, 0.4f), new Vector3(1.0f, 1.8f, 1.0f), "Look in the satchel", () => "Runner's satchel", SatchelLines, 7f);
        }

        private IEnumerable<string> SatchelLines()
        {
            var d = P.Discards; var l = new List<string>();
            if (d == null || d.Count == 0) { l.Add("Nothing has been drawn yet."); return l; }
            int last = d[d.Count - 1];
            l.Add("Last draw: shrine " + last + ", " + PlagueViews.ElementName[PlagueApi.ElementOf(last)] + ".");
            string recent = "";
            for (int i = d.Count - 2, n = 0; i >= 0 && n < 8; i--, n++) recent += (recent.Length > 0 ? ", " : "") + d[i];
            if (recent.Length > 0) l.Add("Before that: " + recent + ".");
            return l;
        }

        // ---------------------------------------------------------------- the banish charm's peg (PLG-ACTIONS)

        private void BuildCharm()
        {
            var root = Child("Charm peg", CharmSpot);
            var mb = new MeshBuilder();
            LandShapes.Beam(mb, Vector3.zero, new Vector3(0, 1.3f, 0), 0.06f, 6, CampBuilder.Wood);
            LandShapes.Beam(mb, new Vector3(0, 1.25f, 0), new Vector3(0.3f, 1.25f, 0), 0.04f, 5, CampBuilder.WoodLight);
            LandLook.Part(root, "Peg", mb.ToMesh("charm peg"), null, true);
            var c = new GameObject("Charm"); c.transform.SetParent(root, false); c.transform.localPosition = new Vector3(0.3f, 1.25f, 0);
            BanishCharm.Model(c.transform, out _charmTip);
            Charm = Tap(root, WorldZones.PlgActions, new Vector3(0.1f, 0.8f, 0), new Vector3(0.9f, 1.7f, 0.7f), "Look at the charm", () => "The banish charm", CharmLines, 7f);
        }

        private IEnumerable<string> CharmLines()
        {
            var l = new List<string>();
            long left = PlagueApi.BanishCooldownLeft(S);
            l.Add("A smouldering herb bundle the healer gives you.");
            if (left > 0) l.Add("Smouldering: ready in " + ((left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond) + " seconds.");
            else l.Add("Glowing: ready.");
            l.Add("Cast it at a shrine with demons out in the world.");
            return l;
        }

        // ---------------------------------------------------------------- the round-draw sign (PLG-PHASE)

        private void BuildSign()
        {
            var root = Child("Round-draw sign", SignSpot);
            var mb = new MeshBuilder();
            LandShapes.Beam(mb, Vector3.zero, new Vector3(0, 1.9f, 0), 0.07f, 6, CampBuilder.Wood);
            mb.Box(new Vector3(0, 1.6f, 0.06f), new Vector3(1.1f, 0.65f, 0.06f), CampBuilder.WoodLight);
            // Three pebbles dropping onto a cord: the runner's draw.
            for (int i = 0; i < 3; i++)
            {
                float x = -0.3f + i * 0.3f;
                mb.Cylinder(new Vector3(x, 1.5f, 0.1f), 0.07f, 0.05f, 0.05f, 8, new Color(0.45f, 0.43f, 0.40f));
                mb.Box(new Vector3(x, 1.72f, 0.1f), new Vector3(0.03f, 0.14f, 0.02f), new Color(0.12f, 0.08f, 0.05f));
            }
            mb.Box(new Vector3(0, 1.43f, 0.1f), new Vector3(0.95f, 0.025f, 0.02f), new Color(0.9f, 0.85f, 0.65f));
            LandLook.Part(root, "Sign", mb.ToMesh("draw sign"), null, true);
            Sign = Tap(root, WorldZones.PlgPhase, new Vector3(0, 1.1f, 0.1f), new Vector3(1.3f, 2.2f, 0.7f), "Read the draw sign", () => "The round-draw sign", SignLines, 7f);
        }

        private IEnumerable<string> SignLines()
        {
            int r = PlagueApi.Rate(S);
            return new[] { "Every round the runner draws " + r + " shrine" + (r > 1 ? "s" : "") + " and drops pebbles on the table.", "A demon appears at each shrine struck." };
        }

        // ---------------------------------------------------------------- state

        /// <summary>Every sentence the lodge's props and the healer can say now (tests: nothing may speak of what is left to come).</summary>
        public IEnumerable<string> AllTexts()
        {
            var all = new List<LodgeInspect> { Table, Post, Stick, Satchel, Charm, Sign };
            all.AddRange(Vessels); all.AddRange(NodeTaps);
            foreach (var li in all)
            {
                if (li == null) continue;
                yield return li.Title;
                foreach (var s in li.Lines) yield return s;
            }
            if (Lodge != null) foreach (var s in Lodge.AllSayings()) yield return s;
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events)
            {
                if (e is Infected inf && inf.Node >= 1 && inf.Node <= _pop.Length) { _pop[inf.Node - 1] = Time.unscaledTime + 0.9f; _anyPop = true; }
                else if (e is Epidemic ep && ep.Node >= 1 && ep.Node <= _pop.Length) { _pop[ep.Node - 1] = Time.unscaledTime + 0.9f; _anyPop = true; }
            }
        }

        private void Update()
        {
            var p = P;
            bool show = p != null && !p.Ended && S.Historic == null;
            if (Props.gameObject.activeSelf != show) Props.gameObject.SetActive(show);
            if (!show) { if (_leaning) EndLean(false); return; }
            SyncAll(false);
            UpdateLean();
        }

        // ---------------------------------------------------------------- the lean over the table (P2b)

        /// <summary>True while the camera leans over the map table (the pebbles are enlarged tap targets, the card docks at the right).</summary>
        public static bool LeanActive { get; private set; }
        public bool Leaning => _leaning;
        private bool _leaning; private PlayerWalker _lw;
        /// <summary>The share of the safe width the island may take in the table view (the ledger column takes the rest).</summary>
        public const float LeanMapShare = 0.56f;
        public static float LeanPitch = 70f;

        /// <summary>The table surface at the middle of the island.</summary>
        public Vector3 TableTop => TableCentre + Vector3.up * TableY;

        /// <summary>The eye position and yaw that fit the island into the left of the safe area (pure geometry).</summary>
        public static Vector3 LeanEye(Vector3 top, float halfW, float halfD, float pitchDeg, float vfovDeg, float screenW, float screenH, Rect safe, out float yaw)
        {
            yaw = 0f; // the island keeps the compass of the world: north (+z) is up the picture
            float pad = Mathf.Max(8f, screenH * 0.03f);
            var target = Rect.MinMaxRect(safe.xMin + pad, safe.yMin + pad, safe.xMin + safe.width * LeanMapShare - pad, safe.yMax - pad);
            float f = screenH * 0.5f / Mathf.Tan(vfovDeg * 0.5f * Mathf.Deg2Rad);
            var fwd = Quaternion.Euler(pitchDeg, yaw, 0) * Vector3.forward;
            var right = Vector3.right;
            var up = Vector3.Cross(fwd, right).normalized; // camera up
            Vector3 aim = top; float dist = 3.2f;
            var corners = new[] { new Vector3(-halfW, 0, -halfD), new Vector3(halfW, 0, -halfD), new Vector3(-halfW, 0, halfD), new Vector3(halfW, 0, halfD) };
            for (int it = 0; it < 24; it++)
            {
                var eye = aim - fwd * dist;
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                foreach (var c in corners)
                {
                    var d = top + c - eye;
                    float z = Mathf.Max(0.2f, Vector3.Dot(d, fwd));
                    float px = screenW * 0.5f + Vector3.Dot(d, right) / z * f;
                    float py = screenH * 0.5f - Vector3.Dot(d, up) / z * f; // GUI y (down)
                    x0 = Mathf.Min(x0, px); x1 = Mathf.Max(x1, px); y0 = Mathf.Min(y0, py); y1 = Mathf.Max(y1, py);
                }
                float k = Mathf.Max((x1 - x0) / target.width, (y1 - y0) / target.height);
                dist *= Mathf.Clamp(k, 0.5f, 2f);
                // move the aim so the middle of the island's projected box lands on the middle of the target
                float zc = Mathf.Max(0.2f, dist);
                aim += right * (((x0 + x1) * 0.5f - target.center.x) / f * zc) - up * (((y0 + y1) * 0.5f - target.center.y) / f * zc);
            }
            return aim - fwd * dist;
        }

        private void UpdateLean()
        {
            var w = PlayerWalker.Instance;
            if (!_leaning)
            {
                if (w == null || w.IsLocked || WorldActions.State != WorldActionState.Inspect) return;
                var cur = WorldActions.Machine.Current as LodgeInspect;
                if (cur == null || (cur != Table && System.Array.IndexOf(_nodeLi, cur) < 0)) return;
                BeginLean(w);
                return;
            }
            if (w == null || w.IsLocked || w.IsGliding || w.IsAutoWalking || Input.GetKeyDown(KeyCode.Escape)) EndLean(w != null && !w.IsLocked);
        }

        /// <summary>Leans the camera over the table (the first tap on the table or a pebble; tests call it too).</summary>
        public bool BeginLean(PlayerWalker w)
        {
            if (_leaning || w == null || w.Camera == null) return false;
            var eye = LeanEye(TableTop, IslandHalfW, IslandHalfD, LeanPitch, w.Camera.fieldOfView, Screen.width, Screen.height, ScreenSafeArea.Current(), out float yaw);
            w.StopAuto();
            w.PlaceExact(eye - Vector3.up * WorldBootstrap.EyeHeight, yaw, LeanPitch);
            WorldActions.NoteCameraMove(0.6f);
            _leaning = true; _lw = w; LeanActive = true;
            LedgerCard.DockRight = true;
            foreach (var li in _nodeLi) if (li != null) WorldActions.Register(li);
            return true;
        }

        /// <summary>Stands up again: the walker goes back to the cell it stood on.</summary>
        public void EndLean(bool restore)
        {
            if (!_leaning) return;
            _leaning = false; LeanActive = false;
            LedgerCard.DockRight = false;
            foreach (var li in _nodeLi) if (li != null) WorldActions.Unregister(li);
            if (restore && _lw != null) _lw.RestoreCell();
            _lw = null;
        }

        /// <summary>The tap component of a shrine pebble (tests: the pebbles in the table view).</summary>
        public LodgeInspect NodeTap(int node) => _nodeLi[node - 1];

        private void SyncAll(bool force)
        {
            var p = P; if (p?.KindCount == null) return;
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                bool ch = force;
                for (int k = 0; k < 4; k++) { int v = p.KindCount[i * 4 + k]; if (_tk[i * 4 + k] != v) { _tk[i * 4 + k] = v; ch = true; } }
                if (ch) RebuildFigures(i);
            }
            if (_anyPop)
            {
                bool still = false; float now = Time.unscaledTime;
                for (int i = 0; i < _pop.Length; i++)
                {
                    if (_pop[i] <= 0f) continue;
                    float u = Mathf.Clamp01((_pop[i] - now) / 0.9f);
                    foreach (var g in _figs[i]) { var lp = g.transform.localPosition; lp.y = 0.03f + 0.45f * u * u; g.transform.localPosition = lp; }
                    if (u <= 0f) _pop[i] = 0f; else still = true;
                }
                _anyPop = still;
            }
            int lit = Mathf.Clamp(p.Outbreaks, 0, PlagueRules.OutbreaksToFall);
            if (lit != _litShown || force)
            {
                _litShown = lit;
                for (int i = 0; i < _flames.Length; i++) _flames[i].SetActive(i < lit);
            }
            if (_postLight != null) _postLight.intensity = lit == 0 ? 0.15f : 0.3f + 0.28f * lit;
            if (p.RateIndex != _pegShown || force)
            {
                _pegShown = p.RateIndex;
                _peg.localPosition = new Vector3(0, StickY(p.RateIndex), 0.075f);
            }
            for (int e = 0; e < 4; e++)
            {
                int st = (int)VesselOf(e);
                if (st == _vesselShown[e] && !force) continue;
                _vesselShown[e] = st;
                _vLight[e].SetActive(st == (int)VesselState.Cured || st == (int)VesselState.Eradicated);
                _vLid[e].SetActive(st == (int)VesselState.Eradicated);
                _vSoot[e].SetActive(st == (int)VesselState.Strengthened);
                _vCrack[e].SetActive(st == (int)VesselState.Strengthened);
                // Sealed (eradicated) shows its lid and no light.
                if (st == (int)VesselState.Eradicated) _vLight[e].SetActive(false);
            }
            if (_charmTip != null) BanishCharm.Tint(_charmTip, BanishCharm.GlowColour(S));
        }
    }

    /// <summary>
    /// The banish charm (WU-3, PLG-ACTIONS): a smouldering herb bundle the healer gives you, held at the lower right of the
    /// first-person view. Its tip glows gold and pulses when the spell is ready and dims to a cooling ember while it is not (the
    /// single shared banish cooldown), so the cooldown is read in the world, not on a bar. Tapping the charm's peg at the
    /// lodge, or the healer, says the exact seconds. View only.
    /// </summary>
    public sealed class BanishCharm : MonoBehaviour
    {
        public static BanishCharm Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>The charm is drawn in front of the camera now.</summary>
        public bool Held => _root != null && _root.activeSelf;
        /// <summary>0 (just cast) to 1 (ready).</summary>
        public float Ready01 { get; private set; } = 1f;

        private PlagueViews _v; private GameObject _root, _tip; private Camera _cam;

        private void Awake() { Instance = this; _v = GetComponent<PlagueViews>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; if (_root != null) Destroy(_root); }

        /// <summary>The charm's mesh: a bound herb bundle with a glowing tip. <paramref name="tip"/> is the glow part.</summary>
        public static void Model(Transform parent, out GameObject tip)
        {
            LandLook.Part(parent, "Bundle", LandLook.Cached("lodge/charm", m =>
            {
                m.Cylinder(new Vector3(0, -0.34f, 0), 0.025f, 0.05f, 0.3f, 6, HubArt.Herb);
                m.Cylinder(new Vector3(0.02f, -0.32f, 0), 0.03f, 0.045f, 0.24f, 6, HubArt.HerbDry);
                m.Cylinder(new Vector3(0, -0.2f, 0), 0.054f, 0.054f, 0.025f, 6, CampBuilder.HideDark);
                m.Cylinder(new Vector3(0, -0.46f, 0), 0.012f, 0.012f, 0.14f, 5, CampBuilder.Wood);
            }), null, false);
            tip = LandLook.Part(parent, "Tip", LandLook.Cached("lodge/charm-tip", m => { m.Cone(new Vector3(0, -0.06f, 0), 0.06f, 0.14f, 6, Color.white); m.Cone(new Vector3(0, -0.06f, 0), 0.04f, 0.2f, 6, Color.white); }), LandLook.Additive, false);
            PlagueFx.Tint(tip, new Color(1f, 0.8f, 0.3f));
        }

        public static Color GlowColour(WorldState s)
        {
            long left = PlagueApi.BanishCooldownLeft(s);
            if (left <= 0) return new Color(1f, 0.82f, 0.35f) * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));
            float f = 1f - Mathf.Clamp01(left / (float)PlagueRules.BanishCooldownTicks);
            return Color.Lerp(new Color(0.28f, 0.06f, 0.03f), new Color(0.9f, 0.35f, 0.1f), f);
        }

        public static void Tint(GameObject tip, Color c) => PlagueFx.Tint(tip, c);

        private void LateUpdate()
        {
            var w = PlayerWalker.Instance;
            var s = _v != null ? _v.S : null;
            if (w == null || w.Camera == null || s == null || s.Plague == null) { if (_root != null) _root.SetActive(false); return; }
            if (_root == null)
            {
                _root = new GameObject("Banish charm");
                Model(_root.transform, out _tip);
            }
            if (_cam != w.Camera) { _cam = w.Camera; _root.transform.SetParent(_cam.transform, false); _root.transform.localPosition = new Vector3(0.2f, -0.2f, 0.55f); _root.transform.localRotation = Quaternion.Euler(-25f, 20f, 15f); _root.transform.localScale = Vector3.one * 0.4f; }
            bool show = PlagueApi.Active(s) && s.Historic == null && !_v.Elevated && (_v.Dir == null || !_v.Dir.IsOpen)
                        && !(EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) && !OpeningVista.Active; // not in the hand while looking out over the camp
            if (_root.activeSelf != show) _root.SetActive(show);
            long left = PlagueApi.BanishCooldownLeft(s);
            Ready01 = left <= 0 ? 1f : 1f - Mathf.Clamp01(left / (float)PlagueRules.BanishCooldownTicks);
            if (show) { Tint(_tip, GlowColour(s)); _tip.transform.localScale = Vector3.one * (0.6f + 0.4f * Ready01); }
        }
    }
}
