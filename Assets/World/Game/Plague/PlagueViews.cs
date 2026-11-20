using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;

namespace World.Game
{
    /// <summary>
    /// The plague in the world and on the god board (G2-2; PRD_GODBOARD PL-6, PL-11, PL-12). Builds, from
    /// <see cref="PlagueGraphData"/> and <see cref="WorldState.Plague"/> only (no position is hard-coded):
    /// <list type="bullet">
    /// <item>a cairn-shrine on the ground at every node, faint trail paths between them, and demons squatting at a node by kind (0-3);</item>
    /// <item>the overlay (key V or the "Plague" button): on the board a layer above the island with the four regions tinted, the trails
    /// (crossings gold with a diamond, the tunnel dashed), the nodes with a three-segment demon ring, and the HUD panel (rate track,
    /// outbreak counter, top of the discard pile); on foot, bright trails and beams over every node;</item>
    /// <item>pulses that run along the trails on an outbreak, a purge effect on every banish (the player's or the soldiers'), the
    /// storm on an epidemic (sky darkens, lightning, thunder; never a number);</item>
    /// <item>the banish spell on foot (key B, a tap on the shrine, or the HUD button): <c>Banish{Node}</c> for the nearest node with demons in range.</item>
    /// </list>
    /// It reads state every frame and never writes it: the only thing it sends is the command.
    /// Installed by <see cref="GodBoardInstaller"/> on a GodBoard game.
    /// </summary>
    public sealed class PlagueViews : MonoBehaviour
    {
        public const float OverlayY = 80f, BanishRange = 18f, StormSeconds = 9f;
        public static readonly string[] ElementName = { "Earth", "Fire", "Water", "Air" };
        /// <summary>Earth brown, Fire red, Water blue, Air violet (PL-12).</summary>
        public static readonly Color[] ElementColour =
        {
            new Color(0.58f, 0.37f, 0.18f), new Color(0.90f, 0.20f, 0.12f), new Color(0.20f, 0.48f, 0.92f), new Color(0.70f, 0.42f, 0.94f),
        };

        public static PlagueViews Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        public bool Overlay { get; private set; }
        /// <summary>The overlay layer is the one above the island (the board camera or the god camera is looking at it).</summary>
        public bool Elevated => (Dir != null && Dir.View != BoardView.Altar) || (_boot != null && _boot.Views != null && _boot.Views.CurrentView == ViewMode.God);
        public GodBoardDirector Dir => GodBoardDirector.Instance;
        public WorldBootstrap Boot => _boot;
        public WorldState S => _boot.Sim.State;
        public PlagueState P => S.Plague;
        public CommandResult LastBanish { get; private set; }
        public int BanishesSent { get; private set; }
        public int EpidemicsSeen { get; private set; }
        public int PurgesPlayed { get; private set; }
        public int OutbreaksSeen { get; private set; }
        /// <summary>0 to 1: how dark the storm is now.</summary>
        public float StormLevel => Envelope(Time.unscaledTime - _stormStart);
        public int OverlayNodeCount => _markers.Count;
        public int OverlayTrailCount { get; private set; }
        public int OverlayHexCount { get; private set; }
        public int PulsesActive => _pulses.Count;
        public GameObject OverlayRoot => _over;
        public GameObject BeaconRoot => _beacons;
        public IReadOnlyList<NodeView> Nodes => _nodes;
        public PlagueHud Hud { get; private set; }

        private WorldBootstrap _boot;
        private readonly List<NodeView> _nodes = new List<NodeView>();
        private readonly List<Transform> _markers = new List<Transform>();
        private GameObject _over, _beacons, _brightTrails, _ground;
        private readonly List<GameObject> _pulseObjs = new List<GameObject>();
        private readonly List<Pulse> _pulses = new List<Pulse>();
        private float _stormStart = -100f, _baseBoardBg; private Color _boardBg;
        private AudioSource _audio; private static AudioClip s_thunder;
        private Texture2D _dark, _white;
        private long _banishPendingTick = -10;

        private struct Pulse { public Vector3 A, B; public float Start, Dur; public Color Col; public GameObject Obj; public int Node; }

        // ---------------------------------------------------------------- install

        public static PlagueViews Install(WorldBootstrap boot)
        {
            if (boot?.Sim?.State?.Plague == null) return null;
            var go = new GameObject("PlagueViews");
            go.transform.SetParent(boot.transform, false);
            var v = go.AddComponent<PlagueViews>();
            v._boot = boot;
            Instance = v;
            v.Build();
            return v;
        }

        private static Vector3 NodePos(int i) => new Vector3(PlagueGraphData.X[i], BoardSpace.GroundY, PlagueGraphData.Z[i]);

        private void Build()
        {
            var topo = S.Board.Topology;
            _ground = new GameObject("Plague Ground"); _ground.transform.SetParent(transform, false);
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                var p = NodePos(i);
                var nv = NodeView.Create(_ground.transform, this, i + 1, p);
                _nodes.Add(nv);
                LandViews.AddClearing(p.x, p.z, 5f);
            }
            BuildGroundTrails();
            BuildOverlay(topo);
            BuildBeacons();
            _over.SetActive(false); _beacons.SetActive(false); _brightTrails.SetActive(false);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f; _audio.playOnAwake = false; _audio.volume = 0.8f;
            _dark = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; _dark.SetPixel(0, 0, Color.white); _dark.Apply();
            if (Dir != null && Dir.BoardCam != null) _boardBg = Dir.BoardCam.backgroundColor;
            Hud = gameObject.AddComponent<PlagueHud>();
            gameObject.AddComponent<BanishCharm>();
            _boot.Clock.Ticked += OnTicked;
            WorldActions.Machine.Changed += OnActionChanged;
            SyncNodes(true);
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            WorldActions.Machine.Changed -= OnActionChanged;
            if (Instance == this) Instance = null;
            if (_dark != null) Destroy(_dark);
            if (Dir != null && Dir.BoardCam != null && _boardBg.a > 0f) Dir.BoardCam.backgroundColor = _boardBg;
        }

        // ---------------------------------------------------------------- ground layer: trails

        private void BuildGroundTrails()
        {
            var mb = new MeshBuilder();
            var glow = new MeshBuilder();
            for (int t = 0; t < PlagueGraphData.TrailCount; t++)
            {
                int kind = PlagueGraphData.TrailKind[t];
                var a = NodePos(PlagueGraphData.TrailA[t] - 1); var b = NodePos(PlagueGraphData.TrailB[t] - 1);
                if (kind == 2) continue; // the tunnel runs under the ground: only the overlay draws it
                mb.Strip(a + Vector3.up * 0.14f, b + Vector3.up * 0.14f, kind == 1 ? 2.6f : 1.8f, kind == 1 ? new Color(0.70f, 0.58f, 0.34f) : new Color(0.62f, 0.53f, 0.38f));
                glow.Strip(a + Vector3.up * 0.3f, b + Vector3.up * 0.3f, 3.2f, kind == 1 ? new Color(0.9f, 0.7f, 0.2f, 1f) : new Color(0.55f, 0.50f, 0.34f, 1f));
            }
            var pathObj = LandLook.Part(_ground.transform, "Trails", mb.ToMesh("plague trails"), LandLook.Unlit, false);
            _brightTrails = LandLook.Part(_ground.transform, "Bright trails", glow.ToMesh("plague trails bright"), LandLook.Additive, false);
            pathObj.layer = 2; _brightTrails.layer = 2;
        }

        // ---------------------------------------------------------------- the overlay layer above the island

        internal static int[] HexRegions(BoardTopology topo, out bool[] soft)
        {
            var reg = new int[topo.HexCount]; soft = new bool[topo.HexCount];
            for (int h = 0; h < reg.Length; h++) reg[h] = -1;
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                int h = topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[i], PlagueGraphData.HexR[i]));
                if (h >= 0) reg[h] = PlagueGraphData.Element[i];
            }
            var res = (int[])reg.Clone();
            int[] dq = { 1, -1, 0, 0, 1, -1 }, dr = { 0, 0, 1, -1, -1, 1 };
            for (int h = 0; h < reg.Length; h++)
            {
                if (reg[h] >= 0) continue;
                var c = topo.Hex(h);
                var cnt = new int[4];
                for (int k = 0; k < 6; k++)
                {
                    int n = topo.HexIndex(new HexCoord(c.Q + dq[k], c.R + dr[k]));
                    if (n >= 0 && reg[n] >= 0) cnt[reg[n]]++;
                }
                int best = 0; for (int e = 1; e < 4; e++) if (cnt[e] > cnt[best]) best = e;
                if (cnt[best] >= 2) { res[h] = best; soft[h] = true; }
            }
            return res;
        }

        private void BuildOverlay(BoardTopology topo)
        {
            _over = new GameObject("Plague Overlay"); _over.transform.SetParent(transform, false);
            _over.layer = 2;
            // Regions: tinted hexes (additive).
            var reg = HexRegions(topo, out var soft);
            var tint = new MeshBuilder();
            int hexes = 0;
            for (int h = 0; h < reg.Length; h++)
            {
                if (reg[h] < 0) continue;
                var centre = BoardSpace.Hex(topo, h); centre.y = OverlayY - 1f;
                var ring = new List<Vector3>();
                foreach (int c in topo.HexCorners(h)) { var v = BoardSpace.Corner(topo, c); v.y = OverlayY - 1f; ring.Add(Vector3.Lerp(centre, v, 0.96f)); }
                var col = ElementColour[reg[h]] * (soft[h] ? 0.16f : 0.34f); col.a = 1f;
                tint.FanUp(centre, ring, col);
                hexes++;
            }
            OverlayHexCount = hexes;
            LandLook.Part(_over.transform, "Regions", tint.ToMesh("plague regions"), LandLook.Additive, false);

            // Trails.
            var tr = new MeshBuilder();
            var dark = new Color(0.07f, 0.06f, 0.06f);
            for (int t = 0; t < PlagueGraphData.TrailCount; t++)
            {
                int kind = PlagueGraphData.TrailKind[t];
                var a = NodePos(PlagueGraphData.TrailA[t] - 1); var b = NodePos(PlagueGraphData.TrailB[t] - 1);
                a.y = b.y = OverlayY;
                var mid = (a + b) * 0.5f;
                if (kind == 0) { tr.Strip(a, b, 7f, new Color(0.96f, 0.93f, 0.80f)); }
                else if (kind == 1)
                {
                    tr.Strip(a, b, 11f, dark); tr.Strip(a + Vector3.up * 0.1f, b + Vector3.up * 0.1f, 7.5f, new Color(1f, 0.80f, 0.20f));
                    Diamond(tr, mid + Vector3.up * 0.3f, 12f, dark); Diamond(tr, mid + Vector3.up * 0.5f, 8.5f, new Color(1f, 0.80f, 0.20f));
                }
                else
                {
                    var d = b - a; float len = d.magnitude; var dir = d / Mathf.Max(0.01f, len);
                    for (float s = 0; s < len; s += 24f)
                    {
                        float e = Mathf.Min(len, s + 14f);
                        tr.Strip(a + dir * s, a + dir * e, 8f, new Color(0.80f, 0.45f, 0.95f));
                    }
                    Diamond(tr, mid + Vector3.up * 0.3f, 12f, dark); Diamond(tr, mid + Vector3.up * 0.5f, 8.5f, new Color(0.80f, 0.45f, 0.95f));
                }
            }
            OverlayTrailCount = PlagueGraphData.TrailCount;
            LandLook.Part(_over.transform, "Trails", tr.ToMesh("plague overlay trails"), LandLook.Unlit, false);

            // Nodes: an element disc and the three-segment demon ring (rebuilt when the demons change).
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                var m = new GameObject("OverlayNode " + (i + 1)).transform;
                m.SetParent(_over.transform, false);
                m.position = new Vector3(PlagueGraphData.X[i], OverlayY + 1f, PlagueGraphData.Z[i]);
                int e = PlagueGraphData.Element[i];
                LandLook.Part(m, "Disc", LandLook.Cached("plague/node/" + e, mb =>
                {
                    LandShapes.Disc(mb, Vector3.zero, 13f, 20, new Color(0.06f, 0.05f, 0.05f));
                    LandShapes.Disc(mb, Vector3.up * 0.2f, 10.5f, 20, ElementColour[e]);
                    LandShapes.Disc(mb, Vector3.up * 0.4f, 3.2f, 14, Color.Lerp(ElementColour[e], Color.white, 0.55f));
                }), LandLook.Unlit, false);
                _markers.Add(m);
            }
        }

        private static void Diamond(MeshBuilder mb, Vector3 c, float r, Color col)
        {
            mb.Quad(c + new Vector3(-r, 0, 0), c + new Vector3(0, 0, r), c + new Vector3(r, 0, 0), c + new Vector3(0, 0, -r), col);
        }

        /// <summary>The demon ring around a node marker: three segments, filled by the kind of each demon.</summary>
        internal static Mesh DemonRing(int[] kinds)
        {
            var mb = new MeshBuilder();
            int total = kinds[0] + kinds[1] + kinds[2] + kinds[3];
            var slots = new List<int>();
            for (int k = 0; k < 4; k++) for (int j = 0; j < kinds[k]; j++) slots.Add(k);
            Arc(mb, Vector3.zero, 13.5f, 20.5f, 0f, 360f, 36, new Color(0.05f, 0.04f, 0.04f));
            for (int i = 0; i < 3; i++)
            {
                float a0 = 90f - i * 120f - 54f, a1 = 90f - i * 120f + 54f;
                bool full = i < slots.Count;
                var col = full ? Color.Lerp(ElementColour[slots[i]], Color.white, 0.25f) : new Color(0.20f, 0.18f, 0.18f);
                Arc(mb, Vector3.up * 0.3f, 14.5f, 19.5f, a0, a1, 10, col);
            }
            if (total >= 3) Arc(mb, Vector3.up * 0.1f, 20.5f, 24f, 0f, 360f, 36, new Color(1f, 0.15f, 0.08f));
            return mb.ToMesh("plague ring");
        }

        private static void Arc(MeshBuilder mb, Vector3 c, float r0, float r1, float deg0, float deg1, int seg, Color col)
        {
            for (int k = 0; k < seg; k++)
            {
                float a0 = Mathf.Deg2Rad * Mathf.Lerp(deg0, deg1, k / (float)seg), a1 = Mathf.Deg2Rad * Mathf.Lerp(deg0, deg1, (k + 1) / (float)seg);
                Vector3 D(float a) => new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                mb.Quad(c + D(a0) * r0, c + D(a1) * r0, c + D(a1) * r1, c + D(a0) * r1, col);
            }
        }

        // ---------------------------------------------------------------- the beacons (overlay on foot)

        private void BuildBeacons()
        {
            _beacons = new GameObject("Plague Beacons"); _beacons.transform.SetParent(transform, false);
            // The beams themselves are children of the nodes; the root only marks "beacons are on".
            foreach (var n in _nodes) n.EnsureBeacon();
        }

        // ---------------------------------------------------------------- input

        private bool InputBlocked()
        {
            if (global::World.Game.Hud.MenuOpen || MapScreen.IsOpen || DialoguePanel.IsOpen) return true;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return true;
            if (_boot != null && _boot.Views != null && _boot.Views.CurrentView == ViewMode.God) return true; // the F1 tools own the keys
            return false;
        }

        public void SetOverlay(bool on) { Overlay = on; }
        public void ToggleOverlay() { Overlay = !Overlay; }

        /// <summary>The overlay is on (the board's totem reads this for its glow).</summary>
        public static bool OverlayOn => Instance != null && Instance.Overlay;

        /// <summary>The one public toggle for the overlay (WU-3): the board's demon-ward totem calls this; V does the same.
        /// False when there is no plague in this game.</summary>
        public static bool ToggleOverlayFromTotem() { if (Instance == null) return false; Instance.ToggleOverlay(); return Instance.Overlay; }

        private void Update()
        {
            if (!InputBlocked())
            {
                if (Input.GetKeyDown(KeyCode.V)) ToggleOverlay();
                if (Input.GetKeyDown(KeyCode.B) && (Dir == null || !Dir.IsOpen)) TryBanishNear();
            }
            bool elevated = Elevated;
            if (_over.activeSelf != (Overlay && elevated)) _over.SetActive(Overlay && elevated);
            bool onFoot = Overlay && !elevated;
            if (_beacons.activeSelf != onFoot) _beacons.SetActive(onFoot);
            if (_brightTrails.activeSelf != onFoot) _brightTrails.SetActive(onFoot);
            SyncNodes(false);
            UpdatePulses(elevated);
        }

        // ---------------------------------------------------------------- state

        private readonly int[] _kinds = new int[4];

        private void SyncNodes(bool force)
        {
            var p = P;
            if (p?.KindCount == null) return;
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int k = 0; k < 4; k++) _kinds[k] = p.KindCount[i * 4 + k];
                _nodes[i].Sync(_kinds, force, Overlay && !Elevated, _markers[i]);
            }
        }

        /// <summary>Demon figures standing at a node now (tests, captures).</summary>
        public int FiguresAt(int node) => node >= 1 && node <= _nodes.Count ? _nodes[node - 1].FigureCount : 0;
        public NodeView NodeOf(int node) => node >= 1 && node <= _nodes.Count ? _nodes[node - 1] : null;
        public Vector3 NodePosition(int node) => NodePos(node - 1);

        // ---------------------------------------------------------------- events

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case Infected inf:
                        if (inf.Node >= 1 && inf.Node <= _nodes.Count) _nodes[inf.Node - 1].Pop();
                        break;
                    case Epidemic ep:
                        EpidemicsSeen++;
                        BeginStorm();
                        if (ep.Node >= 1 && ep.Node <= _nodes.Count) _nodes[ep.Node - 1].Pop();
                        break;
                    case Outbreak ob:
                        OutbreaksSeen++;
                        StartChain(ob);
                        break;
                    case Banished bn:
                        PurgeAt(bn.Node, bn.Kind);
                        break;
                    case IslandFell _:
                        BeginStorm();
                        break;
                }
            }
        }

        /// <summary>A refused shrine tap is said aloud as well as written on the ledger (WU-3).</summary>
        private void OnActionChanged(WorldActionMachine m)
        {
            if (m.State == WorldActionState.Refused && m.Current is NodeView nv && !string.IsNullOrEmpty(m.Reason))
                SpeechBubbles.SayAt(nv.transform.position + Vector3.up * 3f, m.Reason, 3.5f);
        }

        private void PurgeAt(int node, int kind)
        {
            if (node < 1 || node > _nodes.Count) return;
            PurgesPlayed++;
            var col = kind >= 0 && kind < 4 ? Color.Lerp(ElementColour[kind], Color.white, 0.55f) : Color.white;
            PlagueFx.Purge(transform, NodePos(node - 1), col);
        }

        // ---------------------------------------------------------------- outbreak chains

        private void StartChain(Outbreak ob)
        {
            if (ob.Node < 1 || ob.Node > _nodes.Count) return;
            float now = Time.unscaledTime;
            float delay = ob.Chain * 0.55f;
            var col = ob.Kind >= 0 && ob.Kind < 4 ? Color.Lerp(ElementColour[ob.Kind], Color.white, 0.4f) : Color.white;
            _nodes[ob.Node - 1].Flash(now + delay);
            foreach (int n in PlagueApi.Neighbours(ob.Node))
                _pulses.Add(new Pulse { A = NodePos(ob.Node - 1), B = NodePos(n - 1), Start = now + delay, Dur = 0.75f, Col = col, Node = ob.Node });
        }

        private GameObject PulseObject(int idx)
        {
            while (_pulseObjs.Count <= idx)
            {
                var go = new GameObject("Pulse"); go.transform.SetParent(transform, false); go.layer = 2;
                LandLook.Part(go.transform, "Disc", LandLook.Cached("plague/pulse/disc", mb => LandShapes.Disc(mb, Vector3.zero, 3.4f, 14, new Color(1f, 1f, 1f, 1f))), LandLook.Additive, false);
                LandLook.Part(go.transform, "Beam", LandLook.Cached("plague/pulse/beam", mb => LandShapes.FadeBeam(mb, Vector3.zero, 0.9f, 0.1f, 7f, 8, new Color(1f, 1f, 1f, 1f))), LandLook.Additive, false);
                _pulseObjs.Add(go);
            }
            return _pulseObjs[idx];
        }

        private void UpdatePulses(bool elevated)
        {
            float now = Time.unscaledTime;
            for (int i = _pulses.Count - 1; i >= 0; i--) if (now > _pulses[i].Start + _pulses[i].Dur) _pulses.RemoveAt(i);
            for (int i = 0; i < _pulseObjs.Count; i++) _pulseObjs[i].SetActive(false);
            for (int i = 0; i < _pulses.Count; i++)
            {
                var pu = _pulses[i];
                float u = (now - pu.Start) / pu.Dur;
                if (u < 0f) continue;
                var go = PulseObject(i);
                go.SetActive(true);
                var pos = Vector3.Lerp(pu.A, pu.B, u * u * (3f - 2f * u));
                pos.y = elevated ? OverlayY + 2f : 1.4f;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * (elevated ? 3.4f : 1f) * (0.7f + 0.3f * Mathf.Sin(u * Mathf.PI));
                PlagueFx.Tint(go, pu.Col);
            }
        }

        // ---------------------------------------------------------------- the storm (PL-6)

        private static float Envelope(float t)
        {
            if (t < 0f) return 0f;
            if (t < 0.5f) return Mathf.SmoothStep(0f, 1f, t / 0.5f);
            if (t < 3.5f) return 1f;
            if (t < StormSeconds) return 1f - Mathf.SmoothStep(0f, 1f, (t - 3.5f) / (StormSeconds - 3.5f));
            return 0f;
        }

        /// <summary>Darkens the sky and rolls thunder (tests and the epidemic event call this).</summary>
        public void BeginStorm()
        {
            _stormStart = Time.unscaledTime;
            if (_audio != null && !Application.isBatchMode)
            {
                if (s_thunder == null) s_thunder = MakeThunder();
                _audio.clip = s_thunder; _audio.PlayDelayed(0.25f);
            }
        }

        public bool ThunderPlaying => _audio != null && _audio.isPlaying;

        private static AudioClip MakeThunder()
        {
            const int rate = 22050; int n = rate * 3;
            var d = new float[n];
            var rnd = new System.Random(7);
            float lp = 0f, lp2 = 0f, peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Lerp(0.55f, 0.025f, Mathf.Clamp01(t / 1.1f));
                lp2 += (lp - lp2) * 0.18f;
                float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 1.5f);
                float roll = 0.65f + 0.35f * Mathf.Sin(t * 17f) * Mathf.Sin(t * 5.3f);
                float v = (lp * 0.6f + lp2 * 1.7f) * env * roll;
                d[i] = v; peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i] / peak * 0.9f, -1f, 1f);
            var clip = AudioClip.Create("thunder", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        private void LateUpdate()
        {
            float s = StormLevel;
            var cam = Dir != null ? Dir.BoardCam : null;
            if (cam != null && _boardBg.a > 0f) cam.backgroundColor = Color.Lerp(_boardBg, new Color(0.05f, 0.04f, 0.08f), s * 0.85f);
            if (s <= 0.001f) return;
            var dn = DayNight.Instance;
            if (dn != null)
            {
                if (dn.Sun != null) dn.Sun.intensity *= 1f - 0.8f * s;
                if (dn.Moon != null) dn.Moon.intensity *= 1f - 0.7f * s;
            }
            RenderSettings.ambientSkyColor *= 1f - 0.65f * s;
            RenderSettings.ambientEquatorColor *= 1f - 0.65f * s;
            RenderSettings.ambientGroundColor *= 1f - 0.65f * s;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.06f, 0.05f, 0.09f), 0.85f * s);
        }

        private void OnGUI()
        {
            float t = Time.unscaledTime - _stormStart;
            float s = Envelope(t);
            if (s <= 0.001f) return;
            GUI.depth = 90;
            var old = GUI.color;
            GUI.color = new Color(0.10f, 0.05f, 0.16f, 0.5f * s);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dark);
            float flash = Mathf.Max(Flash(t, 0.12f, 0.14f), Flash(t, 0.70f, 0.10f));
            if (flash > 0f)
            {
                GUI.color = new Color(0.85f, 0.88f, 1f, 0.55f * flash);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dark);
            }
            GUI.color = old;
        }

        private static float Flash(float t, float at, float dur) => t < at || t > at + dur ? 0f : 1f - (t - at) / dur;

        // ---------------------------------------------------------------- banish on foot (PL-11)

        /// <summary>The nearest node with demons within range of a point on the ground, or 0.</summary>
        public int NearestDemonNode(Vector3 from, float range)
        {
            var p = P; if (p?.Demons == null) return 0;
            int best = 0; float bd = range;
            for (int i = 0; i < PlagueGraphData.NodeCount; i++)
            {
                if (p.Demons[i] <= 0) continue;
                float d = BoardSpace.Flat(NodePos(i) - from);
                if (d <= bd) { bd = d; best = i + 1; }
            }
            return best;
        }

        public int NodeNearPlayer => PlayerWalker.Instance != null ? NearestDemonNode(PlayerWalker.Instance.Position, BanishRange) : 0;

        /// <summary>The cast (key B, the button): banishes one demon from the nearest node with demons in range.</summary>
        public CommandResult TryBanishNear()
        {
            var w = PlayerWalker.Instance;
            int n = w != null ? NearestDemonNode(w.Position, BanishRange) : 0;
            if (n == 0)
            {
                var r = CommandResult.Reject(PlagueRefusals.NoDemons, w != null && NearestDemonNode(w.Position, 1000f) > 0 ? "Walk closer to a shrine with demons." : PlagueRefusals.Sentence(PlagueRefusals.NoDemons));
                if (w != null) SpeechBubbles.SayAt(w.Position + Vector3.up * 2.2f, r.Message, 3f);
                LastBanish = r;
                return r;
            }
            return TryBanishAt(n, false);
        }

        /// <summary>A banish at a given node (a tap on its shrine): the player must stand within range.</summary>
        public CommandResult TryBanishAt(int node, bool ignoreRange)
        {
            var sim = _boot.Sim; var w = PlayerWalker.Instance;
            var pos = NodePos(node - 1);
            if (!ignoreRange && w != null && BoardSpace.Flat(pos - w.Position) > BanishRange)
            {
                var far = CommandResult.Reject("too_far", "Walk closer to cast.");
                SpeechBubbles.SayAt(pos + Vector3.up * 3f, far.Message, 3f);
                LastBanish = far; return far;
            }
            if (sim.State.Tick <= _banishPendingTick + 1) return CommandResult.Reject("already_cast", "The spell is already taking hold.");
            var r = sim.Submit(new Banish { PlayerId = PlayerIds.Human, Node = node, Kind = -1 });
            LastBanish = r;
            if (r.Ok)
            {
                _banishPendingTick = sim.State.Tick; BanishesSent++;
                SpellEffect.Play(pos, true);
            }
            else SpeechBubbles.SayAt(pos + Vector3.up * 3f, string.IsNullOrEmpty(r.Message) ? r.Reason : r.Message, 3.5f);
            return r;
        }

        public long CooldownTicksLeft => PlagueApi.BanishCooldownLeft(S);
    }

    /// <summary>The shrine of one node: a cairn on the ground, its demons and its beacon (PRD_GODBOARD PL-12). Tapping it casts the banish when near.</summary>
    public sealed class NodeView : MonoBehaviour, IWorldAction, IWorldActionAnchor
    {
        public int Id { get; private set; }
        public int Element { get; private set; }
        public int[] Kinds { get; } = new int[4];
        public int FigureCount => _figures.Count;
        public int Total => Kinds[0] + Kinds[1] + Kinds[2] + Kinds[3];

        private PlagueViews _owner;
        private readonly List<GameObject> _figures = new List<GameObject>();
        private GameObject _scorch, _spire, _beam, _ring;
        private MaterialPropertyBlock _mpb;
        private float _popUntil, _flashAt = -10f;
        private bool _hasState;

        public string Prompt => Total > 0 ? "Banish a demon" : "Shrine of " + PlagueViews.ElementName[Element];
        public string Title => PlagueViews.ElementName[Element] + " shrine";
        public IEnumerable<string> Lines
        {
            get
            {
                var l = new List<string>();
                if (Total == 0) l.Add("No demons here.");
                for (int k = 0; k < 4; k++) if (Kinds[k] > 0) l.Add(Kinds[k] + " " + PlagueViews.ElementName[k].ToLower() + " demon" + (Kinds[k] > 1 ? "s" : "") + ".");
                if (Total > 0) l.Add("Stand close and cast to banish one.");
                return l;
            }
        }

        // WU-3: the banish is a world action: tap the shrine to see it, tap again to cast (once).
        public string PreviewLine
        {
            get
            {
                int k = DominantKind();
                return k >= 0 ? "Banish a " + PlagueViews.ElementName[k].ToLower() + " demon from here?" : "Banish a demon from here?";
            }
        }

        public Transform Anchor => transform;
        public float KeyReach => 10f;

        public WorldActionCheck Check()
        {
            var s = _owner.S;
            if (s.Plague == null || s.Plague.Ended) return WorldActionCheck.Disable("The sickness is gone from the island.");
            if (s.Plague.IslandFell) return WorldActionCheck.Disable("The island has fallen to the sickness.");
            if (Total == 0) return WorldActionCheck.Refuse("No demons stand at this shrine.");
            var w = PlayerWalker.Instance;
            if (w != null && BoardSpace.Flat(transform.position - w.Position) > PlagueViews.BanishRange) return WorldActionCheck.Refuse("Walk closer to cast.");
            long left = _owner.CooldownTicksLeft;
            if (left > 0) return WorldActionCheck.Refuse("The charm is still smouldering: ready in " + ((left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond) + " seconds.");
            return WorldActionCheck.Allowed;
        }

        public bool Previewing { get; private set; }
        public void ShowPreview(bool on) { Previewing = on; if (_glowRing != null) _glowRing.SetActive(on); }

        public bool Commit() { var r = _owner.TryBanishAt(Id, false); return r.Ok; }

        private GameObject _glowRing;
        private bool _registered;

        private void OnDisable() { WorldActions.Unregister(this); _registered = false; }

        public static NodeView Create(Transform parent, PlagueViews owner, int id, Vector3 at)
        {
            var go = new GameObject("Node " + id);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var v = go.AddComponent<NodeView>();
            v._owner = owner; v.Id = id; v.Element = PlagueApi.ElementOf(id);
            int e = v.Element;
            LandLook.Part(go.transform, "Cairn", LandLook.Cached("plague/cairn/" + e, mb =>
            {
                var el = PlagueViews.ElementColour[e];
                var stone = new Color(0.58f, 0.56f, 0.52f);
                LandShapes.Ring(mb, new Vector3(0, 0.1f, 0), 3.1f, 0.6f, 20, el);
                mb.Box(new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 2.4f), stone, 8f);
                mb.Box(new Vector3(0.1f, 0.95f, 0), new Vector3(1.7f, 0.7f, 1.7f), stone * 0.92f, 28f);
                mb.Box(new Vector3(-0.05f, 1.5f, 0.05f), new Vector3(1.1f, 0.5f, 1.1f), stone * 0.85f, 52f);
                mb.Cone(new Vector3(0, 1.75f, 0), 0.55f, 1.0f, 6, el);
                LandShapes.Beam(mb, new Vector3(1.2f, 0.6f, 0), new Vector3(1.2f, 4.6f, 0), 0.06f, 4, LandLook.DarkWood);
                mb.Quad(new Vector3(1.2f, 4.5f, 0), new Vector3(1.2f, 3.6f, 0), new Vector3(2.4f, 3.9f, 0), new Vector3(2.4f, 4.5f, 0), el);
                mb.Quad(new Vector3(1.2f, 4.5f, 0), new Vector3(2.4f, 4.5f, 0), new Vector3(2.4f, 3.9f, 0), new Vector3(1.2f, 3.6f, 0), el * 0.8f);
            }), null, true);
            LandLook.PickBox(go, Vector3.up * 1.6f, new Vector3(7f, 3.4f, 7f));
            v._glowRing = LandLook.Part(go.transform, "Charm glow", LandLook.Cached("plague/charm-ring", mb => LandShapes.Ring(mb, new Vector3(0, 0.35f, 0), 4.6f, 0.9f, 28, Color.white)), LandLook.Additive, false);
            v._glowRing.layer = 2; v._glowRing.SetActive(false);
            WorldZone.Tag(go, WorldZones.PlgCity);
            return v;
        }

        public void EnsureBeacon()
        {
            if (_beam != null) return;
            _beam = LandLook.Part(transform, "Beacon", LandLook.Cached("plague/beacon", mb => LandShapes.FadeBeam(mb, Vector3.zero, 0.9f, 0.25f, 36f, 8, new Color(1f, 1f, 1f, 1f))), LandLook.Additive, false);
            _beam.layer = 2;
            _beam.SetActive(false);
        }

        public void Pop() { _popUntil = Time.unscaledTime + 0.7f; }
        public void Flash(float at) { _flashAt = at; }

        private int DominantKind()
        {
            int best = -1, bc = 0;
            for (int k = 0; k < 4; k++) if (Kinds[k] > bc) { bc = Kinds[k]; best = k; }
            return best;
        }

        public void Sync(int[] kinds, bool force, bool beaconOn, Transform marker)
        {
            bool changed = force || !_hasState;
            for (int k = 0; k < 4; k++) if (Kinds[k] != kinds[k]) { Kinds[k] = kinds[k]; changed = true; }
            _hasState = true;
            if (changed)
            {
                Rebuild();
                if (marker != null) RebuildRing(marker);
            }
            if (!_registered && Total > 0) { WorldActions.Register(this); _registered = true; }
            else if (_registered && Total == 0) { WorldActions.Unregister(this); _registered = false; }
            Animate(beaconOn);
        }

        private void RebuildRing(Transform marker)
        {
            var ring = marker.Find("Ring");
            if (ring == null) { _ring = LandLook.Part(marker, "Ring", null, LandLook.Unlit, false); _ring.name = "Ring"; _ring.layer = 2; ring = _ring.transform; ring.localPosition = Vector3.up * 0.8f; }
            var mf = ring.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = PlagueViews.DemonRing(Kinds);
        }

        private void Rebuild()
        {
            int total = Total;
            // Figures: one per demon, by kind.
            var order = new List<int>();
            for (int k = 0; k < 4; k++) for (int j = 0; j < Kinds[k]; j++) order.Add(k);
            while (_figures.Count > order.Count) { Destroy(_figures[_figures.Count - 1]); _figures.RemoveAt(_figures.Count - 1); }
            for (int i = 0; i < order.Count; i++)
            {
                int k = order[i];
                if (i < _figures.Count) Destroy(_figures[i]);
                var f = new GameObject("Demon " + i + " " + PlagueViews.ElementName[k]);
                f.transform.SetParent(transform, false);
                LandLook.Part(f.transform, "Mesh", LandLook.Cached("plague/demon/" + k, mb =>
                    LandShapes.Person(mb, Color.Lerp(PlagueViews.ElementColour[k], new Color(0.1f, 0.03f, 0.05f), 0.35f), PlagueViews.ElementColour[k] * 0.5f, false, true)), null, false);
                LandLook.Part(f.transform, "Eyes", LandLook.Cached("person/demon-eyes", LandShapes.DemonEyes), LandLook.Unlit, false);
                f.transform.localScale = Vector3.one * 1.45f;
                float a = (40f + i * 120f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 4.2f;
                f.transform.localPosition = p;
                f.transform.localRotation = Quaternion.LookRotation(-p.normalized + new Vector3(0.001f, 0, 0), Vector3.up);
                if (i < _figures.Count) _figures[i] = f; else _figures.Add(f);
            }
            // The scorch and the spire show while any demon stands here (a hex with demons produces nothing: PL-9).
            if (total > 0 && _scorch == null)
            {
                _scorch = LandLook.Part(transform, "Scorch", LandLook.Cached("plague/scorch", mb =>
                {
                    LandShapes.Ring(mb, new Vector3(0, 0.09f, 0), 5.4f, 1.0f, 28, new Color(0.30f, 0.04f, 0.06f));
                    for (int i = 0; i < 6; i++) mb.Cone(Quaternion.Euler(0, i * 60f + 15f, 0) * Vector3.forward * 5.4f, 0.35f, 0.9f, 4, new Color(0.15f, 0.03f, 0.05f));
                }), LandLook.Unlit, false);
                _spire = LandLook.Part(transform, "Spire", LandLook.Cached("plague/spire", mb => LandShapes.FadeBeam(mb, Vector3.up * 2.5f, 0.8f, 0.25f, 16f, 8, new Color(1f, 1f, 1f, 1f))), LandLook.Additive, false);
                _spire.layer = 2;
            }
            if (_scorch != null) { _scorch.SetActive(total > 0); _spire.SetActive(total > 0); }
            int dom = DominantKind();
            if (_spire != null && dom >= 0) PlagueFx.Tint(_spire, PlagueViews.ElementColour[dom] * 0.9f);
            if (_beam != null && dom >= 0) PlagueFx.Tint(_beam, Color.Lerp(PlagueViews.ElementColour[dom], Color.white, 0.2f));
        }

        private void Animate(bool beaconOn)
        {
            float now = Time.unscaledTime;
            float pop = Mathf.Clamp01((_popUntil - now) / 0.7f);
            if (_glowRing != null && _glowRing.activeSelf)
            {
                // The charm's glow at the shrine: gold and pulsing when it is ready, a dull ember while it cools.
                bool ready = _owner.CooldownTicksLeft <= 0;
                PlagueFx.Tint(_glowRing, ready ? new Color(1f, 0.82f, 0.35f) * (0.7f + 0.3f * Mathf.Sin(now * 6f)) : new Color(0.45f, 0.10f, 0.05f));
                float sc = 1f + 0.08f * Mathf.Sin(now * 6f);
                _glowRing.transform.localScale = new Vector3(sc, 1f, sc);
            }
            float flinch = Previewing ? 1f : 0f;
            for (int i = 0; i < _figures.Count; i++)
            {
                var f = _figures[i].transform;
                float s = 1.45f * (1f + 0.6f * pop * Mathf.Sin(pop * Mathf.PI)) * (1f - 0.18f * flinch);
                f.localScale = Vector3.one * s;
                var b = f.GetChild(0);
                b.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(now * 3f + i * 2f)) * 0.22f, 0);
                b.localRotation = Quaternion.Euler(10f + 22f * flinch, Mathf.Sin(now * (1.7f + 3f * flinch) + i) * 25f, 0);
            }
            if (_spire != null && _spire.activeSelf)
            {
                float w = 1f + 0.15f * Mathf.Sin(now * 2.3f + Id);
                _spire.transform.localScale = new Vector3(w, 1f, w);
            }
            if (_beam != null)
            {
                if (_beam.activeSelf != beaconOn) _beam.SetActive(beaconOn);
                if (beaconOn)
                {
                    float h = Total > 0 ? 1f : 0.55f;
                    float fl = Mathf.Clamp01(1f - Mathf.Abs(now - _flashAt - 0.2f) / 0.5f);
                    _beam.transform.localScale = new Vector3(1f + fl * 3f, h * (1f + fl * 0.5f), 1f + fl * 3f);
                    if (Total == 0) PlagueFx.Tint(_beam, PlagueViews.ElementColour[Element] * 0.8f);
                }
            }
        }
    }

    /// <summary>Small additive effects: the purge on a banish, and the tint of shared glow parts.</summary>
    public sealed class PlagueFx : MonoBehaviour
    {
        private float _t; private Material _mat; private Color _col; private Transform _ring, _pillar; private Transform[] _sparks; private Vector3[] _vel;
        public const float Duration = 1.3f;

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Colours a shared-material glow part (property block: the vertex colours stay white).</summary>
        public static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
            {
                foreach (var rr in go.GetComponentsInChildren<Renderer>()) Tint(rr.gameObject, c);
                return;
            }
            Block.SetColor(ColorId, c);
            r.SetPropertyBlock(Block);
        }

        public static PlagueFx Purge(Transform parent, Vector3 at, Color col)
        {
            var go = new GameObject("Purge"); go.transform.SetParent(parent, false); go.transform.position = at; go.layer = 2;
            var fx = go.AddComponent<PlagueFx>();
            fx._col = col;
            fx._mat = MaterialFactory.CreateGlow(col, "Purge");
            fx._ring = LandLook.Part(go.transform, "Ring", LandLook.Cached("plague/fx/ring", mb => LandShapes.Ring(mb, new Vector3(0, 0.3f, 0), 1f, 0.22f, 32, Color.white)), fx._mat, false).transform;
            fx._pillar = LandLook.Part(go.transform, "Pillar", LandLook.Cached("plague/fx/pillar", mb => LandShapes.FadeBeam(mb, Vector3.zero, 1.6f, 0.2f, 26f, 10, Color.white)), fx._mat, false).transform;
            fx._sparks = new Transform[8]; fx._vel = new Vector3[8];
            var sparkMesh = LandLook.Cached("plague/fx/spark", mb => mb.Box(Vector3.zero, Vector3.one * 0.35f, Color.white, 45f));
            for (int i = 0; i < 8; i++)
            {
                fx._sparks[i] = LandLook.Part(go.transform, "Spark", sparkMesh, fx._mat, false).transform;
                float a = i * Mathf.PI * 2f / 8f;
                fx._vel[i] = new Vector3(Mathf.Sin(a) * 2.2f, 4f + (i % 3), Mathf.Cos(a) * 2.2f);
            }
            fx.Apply(0f);
            return fx;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            Apply(_t / Duration);
            if (_t >= Duration) Destroy(gameObject);
        }

        private void Apply(float u)
        {
            u = Mathf.Clamp01(u);
            float fade = 1f - u;
            _mat.color = new Color(_col.r, _col.g, _col.b, 1f) * (fade * 1.1f);
            _ring.localScale = new Vector3(1f, 1f, 1f) * (1.5f + u * 9f);
            _pillar.localScale = new Vector3(1f - u * 0.6f, Mathf.Sin(Mathf.Min(1f, u * 3f) * Mathf.PI * 0.5f), 1f - u * 0.6f);
            for (int i = 0; i < _sparks.Length; i++) _sparks[i].localPosition = _vel[i] * (u * 2.4f) + Vector3.up * (0.4f - u * u * 1.5f);
        }

        private void OnDestroy() { if (_mat != null) Destroy(_mat); }
    }
}
