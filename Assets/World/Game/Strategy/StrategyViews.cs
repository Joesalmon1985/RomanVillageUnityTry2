using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// Strategy and military world views, reconciled with the state after every tick like <see cref="EconomyViews"/>
    /// (create / update / remove, dictionaries keyed by id, <see cref="RebuildFromState"/> from state alone):
    /// cities with their territory ring, demons on flags, robbed hexes, the hero, squads (soldiers, guards, raiders),
    /// the outpost's damage, the wonder's stages, power signs on smelters, barracks and plants, outbreak bursts.
    /// Labels are IMGUI tags culled by distance (first person) and zoom (God View).
    /// </summary>
    public sealed class StrategyViews : MonoBehaviour
    {
        public static StrategyViews Instance { get; private set; }

        private WorldBootstrap _boot;
        private Transform _root;
        private readonly Dictionary<int, CityView> _cities = new Dictionary<int, CityView>();
        private readonly Dictionary<int, DemonView> _demons = new Dictionary<int, DemonView>();
        private readonly Dictionary<int, SquadView> _squads = new Dictionary<int, SquadView>();
        private readonly Dictionary<int, GameObject> _robbed = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, PowerSign> _power = new Dictionary<int, PowerSign>();
        private readonly Dictionary<int, GameObject> _wonderStages = new Dictionary<int, GameObject>();
        private readonly List<BurstFx> _bursts = new List<BurstFx>();
        private HeroView _hero;
        private GameObject _outpostRuin;
        private readonly List<GameObject> _flames = new List<GameObject>();
        private int _powerTick = -1000;

        public int CityViewCount => _cities.Count;
        public int DemonViewCount => _demons.Count;
        public int SquadViewCount => _squads.Count;
        public int RobbedHexCount => _robbed.Count;
        public int PowerSignCount => _power.Count;
        public int WonderStagesShown { get; private set; }
        public HeroView Hero => _hero;
        public CityView City(int vertex) => _cities.TryGetValue(vertex, out var v) ? v : null;
        public DemonView Demon(int vertex) => _demons.TryGetValue(vertex, out var v) ? v : null;
        public SquadView Squad(int id) => _squads.TryGetValue(id, out var v) ? v : null;
        public PowerSign Power(int buildingId) => _power.TryGetValue(buildingId, out var v) ? v : null;
        public IEnumerable<SquadView> Squads => _squads.Values;
        public IEnumerable<DemonView> Demons => _demons.Values;
        public bool OutpostShownDestroyed => _outpostRuin != null;

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            _root = new GameObject("StrategyViews").transform;
            _root.SetParent(boot.transform, false);
            boot.Clock.Ticked += OnTicked;
            if (OutpostBuilder.Outpost != null) OutpostBuilder.Outpost.LinesProvider = OutpostLines;
            RebuildFromState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        private WorldState S => _boot.Sim.State;
        private bool God => _boot.Views != null && _boot.Views.CurrentView == ViewMode.God;

        private IEnumerable<string> OutpostLines()
        {
            var s = S;
            var o = MilitaryApi.Outpost(s);
            yield return o.Status;
            yield return "The rival's plan: +" + s.Strategy.Tuning.RivalPlanVp + " points at the end of each age; raids from Age " + StrategyApi.Roman(s.Military.Tuning.RaidFromAge) + ".";
            var built = StrategyApi.BuiltCards(s, PlayerIds.Rival);
            if (built.Count > 0)
            {
                var n = new List<string>();
                foreach (var c in built) n.Add(c.Name);
                yield return "Cards built here: " + string.Join(", ", n) + ".";
            }
            yield return "Rival score: " + StrategyApi.ScoreText(s, PlayerIds.Rival);
            if (!o.Destroyed) yield return "Destroy it for +" + s.Strategy.Tuning.OutpostVp + " points: send a squad (Army tool, Attack outpost).";
        }

        // ---------------- reconcile ----------------

        public void DestroyAllViews()
        {
            foreach (var v in _cities.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _demons.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _squads.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _robbed.Values) if (v != null) Destroy(v);
            foreach (var v in _power.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _wonderStages.Values) if (v != null) Destroy(v);
            foreach (var b in _bursts) if (b.Go != null) Destroy(b.Go);
            _cities.Clear(); _demons.Clear(); _squads.Clear(); _robbed.Clear(); _power.Clear(); _wonderStages.Clear(); _bursts.Clear();
            if (_hero != null) Destroy(_hero.gameObject);
            _hero = null;
            WonderStagesShown = 0;
        }

        public void RebuildFromState()
        {
            DestroyAllViews();
            _powerTick = -1000;
            SyncAll(null);
        }

        private void OnTicked(IReadOnlyList<SimEvent> events) => SyncAll(events);

        public void SyncAll(IReadOnlyList<SimEvent> events)
        {
            var s = S;
            if (events != null)
                foreach (var e in events)
                {
                    if (e is OutbreakOccurred o) Burst(o.Vertex, o.Neighbours, new Color(0.8f, 0.2f, 1f));
                    else if (e is EpidemicStruck ep) Burst(ep.Vertex, null, new Color(1f, 0.2f, 0.2f));
                    else if (e is HeroMoved hm && _hero != null) _hero.Walk(s, hm.From, hm.To);
                    else if (e is UnitHit uh) foreach (var q in _squads.Values) if (q != null && q.At == uh.Vertex) q.Flash();
                    else if (e is DemonsCleared dc) Burst(dc.Vertex, null, new Color(1f, 0.9f, 0.4f));
                }

            // Cities (player cities; the rival outpost has its own model).
            var seen = new HashSet<int>();
            foreach (var c in s.Economy.Cities)
            {
                if (c.Owner != PlayerIds.Human || c.Vertex == s.Board.HomeCityVertex) continue;
                seen.Add(c.Vertex);
                if (!_cities.TryGetValue(c.Vertex, out var cv) || cv == null) _cities[c.Vertex] = CityView.Create(_root, s, c.Vertex);
            }
            RemoveMissing(_cities, seen);
            foreach (var cv in _cities.Values) cv.Sync(s);

            // Demons.
            seen.Clear();
            var d = s.Strategy.Demons;
            for (int v = 0; v < d.Length; v++)
            {
                if (d[v] <= 0) continue;
                seen.Add(v);
                if (!_demons.TryGetValue(v, out var dv) || dv == null) _demons[v] = dv = DemonView.Create(_root, v);
                dv.SetCount(s, d[v]);
            }
            RemoveMissing(_demons, seen);

            // Robbed hexes.
            for (int h = 0; h < s.Board.HexTerrain.Length; h++)
            {
                bool robbed = StrategyApi.IsHexRobbed(s, h);
                bool have = _robbed.TryGetValue(h, out var go) && go != null;
                if (robbed && !have) _robbed[h] = RobbedMarker(h);
                else if (!robbed && _robbed.ContainsKey(h)) { if (have) Destroy(go); _robbed.Remove(h); }
            }

            // Hero.
            if (s.Strategy.Started && s.Strategy.HeroVertex >= 0)
            {
                if (_hero == null) _hero = HeroView.Create(_root);
                _hero.Sync(s);
            }
            else if (_hero != null) { Destroy(_hero.gameObject); _hero = null; }

            // Squads.
            seen.Clear();
            foreach (var q in s.Military.Squads)
            {
                if (!q.Alive || q.Units.Count == 0) continue;
                seen.Add(q.Id);
                if (!_squads.TryGetValue(q.Id, out var sv) || sv == null) _squads[q.Id] = sv = SquadView.Create(_root, q);
                sv.Sync(s, q);
            }
            RemoveMissing(_squads, seen);
            int k = 0;
            foreach (var sv in _squads.Values) sv.Slot = k++;

            SyncOutpost(s);
            SyncWonder(s);
            if (s.Tick - _powerTick >= 5 || events == null || HasPowerEvent(events)) { _powerTick = (int)s.Tick; SyncPower(s); }
        }

        private static bool HasPowerEvent(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events) if (e is PowerChanged) return true;
            return false;
        }

        private void RemoveMissing<T>(Dictionary<int, T> map, HashSet<int> keep) where T : Component
        {
            List<int> gone = null;
            foreach (var kv in map) if (!keep.Contains(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone == null) return;
            foreach (int key in gone) { if (map[key] != null) Destroy(map[key].gameObject); map.Remove(key); }
        }

        private void SyncOutpost(WorldState s)
        {
            var info = OutpostBuilder.Outpost;
            if (info == null) return;
            var o = s.Military.Outpost;
            if (o.Destroyed && _outpostRuin == null)
            {
                foreach (var f in _flames) if (f != null) Destroy(f);
                _flames.Clear();
                foreach (Transform child in info.transform)
                    if (child.GetComponent<Renderer>() != null || child.GetComponentInChildren<MeshFilter>() != null)
                    {
                        child.localScale = new Vector3(1f, 0.3f, 1f);
                        MaterialFactory.Paint(child.gameObject, new Color(0.18f, 0.15f, 0.13f));
                    }
                _outpostRuin = new GameObject("OutpostRuin");
                _outpostRuin.transform.SetParent(info.transform, false);
                var mb = new MeshBuilder();
                var rnd = new PropRandom(o.Vertex);
                for (int i = 0; i < 14; i++)
                    mb.Box(new Vector3(rnd.Range(-8, 8), 0.4f, rnd.Range(-8, 8)), new Vector3(rnd.Range(0.4f, 1f), 0.4f, rnd.Range(2f, 4.5f)), new Color(0.12f, 0.1f, 0.08f), rnd.Range(0, 180));
                mb.ToObject("Rubble", _outpostRuin.transform, StrategyModels.VertexMat);
                return;
            }
            if (o.Destroyed) return;
            int want = o.MaxHp <= 0 ? 0 : Mathf.CeilToInt(6f * (o.MaxHp - o.Hp) / o.MaxHp);
            var rr = new PropRandom(o.Vertex + 7);
            while (_flames.Count < want)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(f.GetComponent<Collider>());
                f.name = "Flame";
                f.transform.SetParent(info.transform, false);
                float a = _flames.Count * 61f * Mathf.Deg2Rad;
                f.transform.localPosition = new Vector3(Mathf.Cos(a) * 7.5f, 3.5f, Mathf.Sin(a) * 7.5f);
                f.transform.localScale = new Vector3(1f, 2.2f, 1f) * rr.Range(0.8f, 1.3f);
                f.GetComponent<Renderer>().sharedMaterial = StrategyModels.Glow(new Color(1f, 0.45f, 0.1f));
                _flames.Add(f);
            }
        }

        private void SyncWonder(WorldState s)
        {
            int site = EconomyApi.FindBuilding(s, BuildingType.WonderSite, true);
            int done = s.Strategy.Wonder.StagesDone;
            var bv = site >= 0 && EconomyViews.Instance != null ? EconomyViews.Instance.Building(site) : null;
            if (bv == null || bv.Model == null || bv.Model.Root == null) { WonderStagesShown = 0; return; }
            for (int st = 1; st <= done; st++)
            {
                if (_wonderStages.TryGetValue(st, out var go) && go != null) continue;
                _wonderStages[st] = StrategyModels.WonderStage(bv.Model.Root.transform, st);
            }
            WonderStagesShown = 0;
            foreach (var kv in _wonderStages) if (kv.Value != null) WonderStagesShown++;
        }

        private void SyncPower(WorldState s)
        {
            if (EconomyViews.Instance == null) return;
            var sum = MilitaryApi.Power(s);
            var want = new Dictionary<int, int>(); // building -> 1 powered, 0 unpowered, 2 plant fuelled, 3 plant idle
            if (sum.Consumers != null) foreach (var c in sum.Consumers) want[c.BuildingId] = c.Powered ? 1 : 0;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State == BuildingState.Removed) continue;
                if (b.Type == BuildingType.PowerPlant && b.State == BuildingState.Active) want[b.Id] = EconomyApi.IsFuelled(s, b.Id) ? 2 : 3;
                else if ((b.Type == BuildingType.Smelter || b.Type == BuildingType.Barracks) && b.State == BuildingState.Active && !want.ContainsKey(b.Id))
                    want[b.Id] = MilitaryApi.IsPowered(s, b.Id) ? 1 : 4; // 4: needs no power right now
            }
            var keep = new HashSet<int>(want.Keys);
            foreach (var kv in want)
            {
                var bv = EconomyViews.Instance.Building(kv.Key);
                if (bv == null) { keep.Remove(kv.Key); continue; }
                if (!_power.TryGetValue(kv.Key, out var ps) || ps == null) _power[kv.Key] = ps = PowerSign.Create(bv);
                ps.Set(kv.Value);
            }
            RemoveMissing(_power, keep);
        }

        private GameObject RobbedMarker(int hex)
        {
            var topo = S.Board.Topology;
            var mb = new MeshBuilder();
            var dark = new Color(0.16f, 0.14f, 0.16f);
            var c = new Vector3[6];
            for (int k = 0; k < 6; k++) c[k] = BoardSpace.HexCorner(topo, hex, k, 0.9f) + Vector3.up * 0.16f;
            for (int k = 0; k < 6; k++) mb.Strip(c[k], c[(k + 1) % 6], 3f, dark);
            for (int k = 0; k < 3; k++) mb.Strip(c[k], c[k + 3], 2.2f, dark);
            var go = mb.ToObject("Robbed " + hex, _root, MaterialFactory.SharedUnlit(Color.white));
            var tag = go.AddComponent<RobbedHexTag>();
            tag.Hex = hex;
            return go;
        }

        // ---------------- bursts ----------------

        private sealed class BurstFx { public GameObject Go; public float Born; public List<GameObject> Lines; }

        private void Burst(int vertex, int[] neighbours, Color c)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(_root, false);
            go.transform.position = BoardSpace.Vertex(vertex);
            StrategyModels.GroundRing(go.transform, 1f, 0.25f, c, 0.25f, 48);
            var b = new BurstFx { Go = go, Born = Time.time, Lines = new List<GameObject>() };
            if (neighbours != null)
                foreach (int n in neighbours)
                {
                    var mb = new MeshBuilder();
                    mb.Strip(BoardSpace.Vertex(vertex) + Vector3.up * 0.3f, BoardSpace.Vertex(n) + Vector3.up * 0.3f, 1.4f, c);
                    var l = mb.ToObject("Spread", _root, MaterialFactory.SharedUnlit(Color.white));
                    b.Lines.Add(l);
                }
            _bursts.Add(b);
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            float f = EconomyViews.Instance != null ? EconomyViews.Instance.TickFraction : 0f;
            bool god = God;
            float zoom = god ? Mathf.Clamp(_boot.GodCam.Size / 30f, 1f, 5f) : 1f;
            var cam = _boot.Views != null ? _boot.Views.ActiveCamera : null;
            foreach (var q in _squads.Values) if (q != null) q.Animate(S, f, zoom, god);
            foreach (var dv in _demons.Values) if (dv != null) dv.Animate(zoom, god, cam);
            foreach (var cv in _cities.Values) if (cv != null) cv.SetGod(god);
            foreach (var ps in _power.Values) if (ps != null) ps.Animate(zoom);
            if (_hero != null) _hero.Animate(zoom, god);
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                float age = Time.time - b.Born;
                if (age > 2.5f || b.Go == null)
                {
                    if (b.Go != null) Destroy(b.Go);
                    foreach (var l in b.Lines) if (l != null) Destroy(l);
                    _bursts.RemoveAt(i);
                    continue;
                }
                b.Go.transform.localScale = Vector3.one * (1f + age * 14f);
                foreach (var l in b.Lines) if (l != null) l.SetActive(Mathf.Repeat(age, 0.4f) < 0.28f);
            }
            for (int i = 0; i < _flames.Count; i++)
                if (_flames[i] != null) _flames[i].transform.localScale = new Vector3(1f, 2.2f + Mathf.Sin(Time.time * 9f + i) * 0.5f, 1f);
        }

        // ---------------- labels ----------------

        private GUIStyle _tag;
        private int _tagFor = -1;
        private readonly List<Rect> _placed = new List<Rect>();

        private void OnGUI()
        {
            if (_boot == null || Hud.MenuOpen || Event.current.type != EventType.Repaint) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            if (StrategyHud.Instance != null && StrategyHud.Instance.EndScreenOpen) return;
            GUI.depth = 9;
            var cam = _boot.Views.ActiveCamera;
            if (cam == null) return;
            if (_tagFor != Screen.height) { _tagFor = Screen.height; _tag = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(18), alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true, fontStyle = FontStyle.Bold }; }
            bool god = God;
            float size = god ? _boot.GodCam.Size : 0f;
            var eye = cam.transform.position;
            var s = S;
            _placed.Clear();
            int cap = StrategyApi.DemonCap(s);
            foreach (var dv in _demons.Values)
            {
                if (dv == null) continue;
                if (!god && (dv.transform.position - eye).magnitude > 60f) continue;
                Tag(cam, dv.transform.position + Vector3.up * (god ? 4f * Mathf.Clamp(size / 30f, 1f, 5f) : 3.2f),
                    dv.Count + (dv.Count == 1 ? " demon" : " demons") + (dv.Count >= cap ? " - FULL" : ""), dv.Count >= cap ? new Color(1f, 0.25f, 0.2f) : new Color(0.75f, 0.35f, 1f));
            }
            if (_hero != null && (god || (_hero.transform.position - eye).magnitude < 50f))
                Tag(cam, _hero.transform.position + Vector3.up * (god ? 5f * Mathf.Clamp(size / 30f, 1f, 4f) : 3.3f), "Hero (" + s.Strategy.HeroMovesLeft + " moves)", new Color(1f, 0.82f, 0.3f));
            foreach (var q in _squads.Values)
            {
                if (q == null) continue;
                if (!god && (q.Centre - eye).magnitude > 60f) continue;
                if (god && size > 260f) continue;
                Tag(cam, q.Centre + Vector3.up * (god ? 4f * Mathf.Clamp(size / 30f, 1f, 5f) : 2.8f), q.Caption, q.Owner == PlayerIds.Human ? StrategyModels.PlayerBlue : StrategyModels.RivalRed);
            }
            if (god && size <= 200f)
                foreach (var kv in _robbed)
                    Tag(cam, BoardSpace.Hex(s.Board.Topology, kv.Key) + Vector3.up * 2f, "Robbed: no batches", new Color(0.6f, 0.6f, 0.6f));
            foreach (var cv in _cities.Values)
            {
                if (cv == null) continue;
                if (!god && (cv.transform.position - eye).magnitude > 80f) continue;
                Tag(cam, cv.transform.position + Vector3.up * (god ? 12f + 3f * Mathf.Clamp(size / 30f, 1f, 5f) : 15f), cv.Caption, new Color(0.55f, 0.75f, 1f));
            }
            if (OutpostBuilder.Outpost != null && (god || (OutpostBuilder.Outpost.transform.position - eye).magnitude < 90f))
            {
                var o = MilitaryApi.Outpost(s);
                Tag(cam, OutpostBuilder.Outpost.transform.position + Vector3.up * (god ? 10f + 4f * Mathf.Clamp(size / 30f, 1f, 5f) : 12f),
                    o.Destroyed ? "Outpost destroyed" : "Outpost " + o.Hp + "/" + o.MaxHp + ", " + o.Guards + " guards", StrategyModels.RivalRed);
            }
        }

        private void Tag(Camera cam, Vector3 world, string text, Color col)
        {
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0 || sp.x < -100 || sp.x > Screen.width + 100 || sp.y < -50 || sp.y > Screen.height + 50) return;
            var sz = _tag.CalcSize(new GUIContent(text));
            var r = new Rect(sp.x - sz.x / 2 - UiStyle.Px(8), Screen.height - sp.y - sz.y, sz.x + UiStyle.Px(16), sz.y + UiStyle.Px(2));
            if (r.y < StrategyHud.StripBottom) return; // keep the strip and objective clear
            for (int g = 0; g < 6; g++)
            {
                bool hit = false;
                foreach (var o in _placed) if (o.Overlaps(r)) { r.y = o.y - r.height - 2; hit = true; }
                if (!hit) break;
            }
            _placed.Add(r);
            GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(0.05f, 0.04f, 0.05f, 0.82f)));
            GUI.DrawTexture(new Rect(r.x, r.yMax - UiStyle.Px(4), r.width, UiStyle.Px(4)), UiStyle.SolidTexture(col));
            GUI.Label(r, text, _tag);
        }
    }

    /// <summary>Inspectable tag on a robbed-hex marker.</summary>
    public sealed class RobbedHexTag : MonoBehaviour
    {
        public int Hex;
    }

    // ======================================================================== city

    public sealed class CityView : MonoBehaviour, IInspectable
    {
        public int Vertex { get; private set; }
        public string Caption { get; private set; } = "City";
        private GameObject _ring;
        private GameObject _gate;

        public static CityView Create(Transform parent, WorldState s, int vertex)
        {
            var go = new GameObject("City " + vertex);
            go.transform.SetParent(parent, false);
            go.transform.position = BoardSpace.Vertex(vertex);
            var v = go.AddComponent<CityView>();
            v.Vertex = vertex;
            StrategyModels.City(go.transform, vertex);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 4f, 0); col.size = new Vector3(15f, 8f, 15f); col.isTrigger = true;
            float r = s.Economy.Tuning.TerritorySegments * BoardSpace.SegmentLength;
            v._ring = StrategyModels.GroundRing(go.transform, r, 1.6f, new Color(0.35f, 0.6f, 1f), 0.2f, 120);
            v._ring.SetActive(false);
            v.Sync(s);
            return v;
        }

        public void Sync(WorldState s)
        {
            int cards = StrategyApi.CardsAtCity(s, PlayerIds.Human, Vertex).Count;
            int demons = StrategyApi.DemonsAt(s, Vertex);
            Caption = "City (" + cards + "/" + s.Strategy.Tuning.MaxCardsPerCity + " cards)" + (demons > 0 ? " <color=#FF8070>under demons</color>" : "");
        }

        public void SetGod(bool god) { if (_ring != null && _ring.activeSelf != god) _ring.SetActive(god); }

        public string Title => "Your city";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = WorldBootstrap.Instance.Sim.State;
                yield return "Worth " + s.Strategy.Tuning.VpPerCity + " points. Its hexes send batches when their numbers are drawn; the ring in God View is its land.";
                var cards = StrategyApi.CardsAtCity(s, PlayerIds.Human, Vertex);
                yield return "Cards here (" + cards.Count + "/" + s.Strategy.Tuning.MaxCardsPerCity + "): " + (cards.Count == 0 ? "none" : string.Join(", ", cards.ConvertAll(c => c.Name))) + ".";
                int d = StrategyApi.DemonsAt(s, Vertex);
                if (d > 0) yield return "<color=#FF8070>" + d + " demons hold the city: a hex whose every city holds demons gets no batches (robbed).</color>";
                var terr = new List<string>();
                foreach (int h in s.Board.Topology.VertexHexes(Vertex)) terr.Add(s.Board.HexTerrain[h] + (s.Board.HexNumber[h] > 0 ? " " + s.Board.HexNumber[h] : ""));
                yield return "Hexes: " + string.Join(", ", terr) + ".";
            }
        }
    }

    // ======================================================================== demons

    public sealed class DemonView : MonoBehaviour, IInspectable
    {
        public int Vertex { get; private set; }
        public int Count { get; private set; }
        private readonly List<StrategyModels.DemonParts> _figures = new List<StrategyModels.DemonParts>();
        private GameObject _disc;
        private float _seed;

        public static DemonView Create(Transform parent, int vertex)
        {
            var go = new GameObject("Demons " + vertex);
            go.transform.SetParent(parent, false);
            go.transform.position = BoardSpace.Vertex(vertex);
            var v = go.AddComponent<DemonView>();
            v.Vertex = vertex;
            v._seed = vertex * 0.37f;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.2f, 0); col.size = new Vector3(5f, 2.4f, 5f); col.isTrigger = true;
            var mb = new MeshBuilder();
            TerrainBuilder.Disc(mb, new Vector3(0, 0.22f, 0), 3.2f, 20, new Color(0.35f, 0.05f, 0.45f));
            v._disc = mb.ToObject("DemonDisc", go.transform, MaterialFactory.SharedUnlit(Color.white));
            return v;
        }

        public void SetCount(WorldState s, int n)
        {
            Count = n;
            int shown = Mathf.Min(n, StrategyApi.DemonCap(s) + 1);
            while (_figures.Count < shown)
            {
                var f = StrategyModels.Demon(transform, Vertex * 7 + _figures.Count);
                float a = (_figures.Count * 120f + Vertex * 13f) * Mathf.Deg2Rad;
                f.Root.transform.localPosition = _figures.Count == 0 ? new Vector3(0.6f, 0, 0.4f) : new Vector3(Mathf.Cos(a) * 2.1f, 0, Mathf.Sin(a) * 2.1f);
                f.Root.transform.localRotation = Quaternion.Euler(0, (Vertex * 47 + _figures.Count * 130) % 360, 0);
                _figures.Add(f);
            }
            while (_figures.Count > shown) { Destroy(_figures[_figures.Count - 1].Root); _figures.RemoveAt(_figures.Count - 1); }
        }

        public void Animate(float zoom, bool god, Camera cam)
        {
            float t = Time.time + _seed;
            for (int i = 0; i < _figures.Count; i++)
            {
                var f = _figures[i];
                // Uneasy breathing and twitching; in first person they turn to watch the player.
                float tw = Mathf.PerlinNoise(t * 3f, i * 1.7f) > 0.82f ? Mathf.Sin(t * 60f) * 6f : 0f;
                f.Body.localRotation = Quaternion.Euler(8f + Mathf.Sin(t * 1.3f + i) * 5f, tw, Mathf.Sin(t * 0.9f + i) * 3f);
                f.Body.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2.1f + i) * 0.04f, 1f) * (god ? Mathf.Lerp(1.6f, 3.4f, (zoom - 1f) / 4f) : 1f);
                if (!god && cam != null)
                {
                    var d = cam.transform.position - f.Root.transform.position; d.y = 0;
                    if (d.sqrMagnitude > 1f && d.sqrMagnitude < 900f)
                        f.Root.transform.rotation = Quaternion.RotateTowards(f.Root.transform.rotation, Quaternion.LookRotation(d), 90f * Time.deltaTime);
                }
                bool on = Mathf.PerlinNoise(t * 2f, i) > 0.12f;
                foreach (var e in f.Eyes) if (e.enabled != on) e.enabled = on;
            }
            if (_disc != null) _disc.transform.localScale = Vector3.one * (god ? Mathf.Pow(zoom, 0.8f) * (1f + 0.12f * Mathf.Sin(Time.time * 4f)) : 0.8f);
        }

        public string Title => Count + (Count == 1 ? " demon" : " demons");
        public IEnumerable<string> Lines
        {
            get
            {
                var s = WorldBootstrap.Instance.Sim.State;
                yield return "On " + StrategyUi.SiteName(s, Vertex) + ". Carriers cannot pass a flag with demons, and a building on it stops.";
                int cap = StrategyApi.DemonCap(s);
                var n = StrategyApi.OutbreakNeighbours(s, Vertex);
                yield return Count >= cap ? "<color=#FF8070>Full: one more demon here outbreaks (breach track +1) and spreads to " + (n.Count == 0 ? "no neighbour" : StrategyUi.SiteList(s, n)) + ".</color>"
                                          : "Holds up to " + cap + "; then it outbreaks to " + (n.Count == 0 ? "no neighbour" : StrategyUi.SiteList(s, n)) + ".";
                yield return "Clear it with the hero (God View, Hero tool) or a squad (Army tool).";
            }
        }
    }

    // ======================================================================== hero

    public sealed class HeroView : MonoBehaviour, IInspectable
    {
        private Bedsit.ICharacterActor _actor;
        private GameObject _disc;
        private readonly List<Vector3> _path = new List<Vector3>();
        private float _walkStart = -1f;
        private const float WalkSeconds = 1.6f;
        public int Vertex { get; private set; } = -1;
        public bool Walking => _walkStart >= 0f;

        public static HeroView Create(Transform parent)
        {
            var go = new GameObject("Hero");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HeroView>();
            v._actor = ActorFactory.Create("hero", new Color(0.92f, 0.75f, 0.2f), go.transform);
            v._actor.Root.localScale = Vector3.one * 1.15f;
            StrategyModels.HeroStaff(v._actor.Root);
            var mb = new MeshBuilder();
            TerrainBuilder.Ring(mb, new Vector3(0, 0.25f, 0), 2.2f, 0.5f, 24, new Color(1f, 0.82f, 0.3f));
            v._disc = mb.ToObject("HeroRing", go.transform, MaterialFactory.SharedUnlit(Color.white));
            return v;
        }

        private static Vector3 Stand(int v) => BoardSpace.Vertex(v) + new Vector3(2.5f, 0, -2.5f);

        public void Sync(WorldState s)
        {
            if (Vertex < 0) { Vertex = s.Strategy.HeroVertex; transform.position = Stand(Vertex); }
            if (!Walking && Vertex != s.Strategy.HeroVertex) { Vertex = s.Strategy.HeroVertex; transform.position = Stand(Vertex); }
        }

        /// <summary>Walks the hex edge (its four flags) from one corner to the next.</summary>
        public void Walk(WorldState s, int from, int to)
        {
            var topo = s.Board.Topology;
            _path.Clear();
            _path.Add(transform.position);
            int he = topo.HexEdgeBetween(from, to);
            if (he >= 0)
            {
                var flags = new List<int>(topo.HexEdgeFlags(he));
                if (flags.Count > 0 && flags[0] != from) flags.Reverse();
                for (int i = 1; i < flags.Count - 1; i++) _path.Add(BoardSpace.Vertex(flags[i]));
            }
            _path.Add(Stand(to));
            Vertex = to;
            _walkStart = Time.time;
            if (_actor != null) _actor.PlayOneShot(ActorKeys.Cast);
        }

        public void Animate(float zoom, bool god)
        {
            if (_disc != null) _disc.transform.localScale = Vector3.one * (god ? zoom : 1f);
            if (_actor != null) _actor.Root.localScale = Vector3.one * (god ? 1.15f * Mathf.Lerp(1f, 2.2f, (zoom - 1f) / 4f) : 1.15f);
            if (!Walking) return;
            float u = (Time.time - _walkStart) / WalkSeconds;
            if (u >= 1f) { _walkStart = -1f; transform.position = _path[_path.Count - 1]; return; }
            float seg = u * (_path.Count - 1);
            int i = Mathf.Min((int)seg, _path.Count - 2);
            var a = _path[i]; var b = _path[i + 1];
            transform.position = Vector3.Lerp(a, b, seg - i);
            var d = b - a; d.y = 0;
            if (d.sqrMagnitude > 0.01f && _actor != null) _actor.Root.rotation = Quaternion.LookRotation(d);
        }

        public string Title => "The hero";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = WorldBootstrap.Instance.Sim.State;
                yield return s.Strategy.HeroMovesLeft + " moves left this round. Each move walks one hex edge and clears the demons on its flags.";
                yield return "At " + StrategyUi.SiteName(s, s.Strategy.HeroVertex) + ". God View, Hero tool (J): click a glowing corner.";
            }
        }
    }

    // ======================================================================== squads

    public sealed class SquadView : MonoBehaviour, IInspectable
    {
        public int Id { get; private set; }
        public int Owner { get; private set; }
        public SquadKind Kind { get; private set; }
        public int At { get; private set; }
        public int Slot;
        public string Caption { get; private set; } = "";
        public Vector3 Centre => transform.position;
        public bool Engaged { get; private set; }
        public bool Moving { get; private set; }
        public int FigureCount => _figures.Count;
        private readonly List<StrategyModels.SoldierParts> _figures = new List<StrategyModels.SoldierParts>();
        private int _from = -1, _to = -1, _progress, _ticksPerEdge = 20;
        private float _flash;
        private GameObject _disc;

        public static SquadView Create(Transform parent, Squad q)
        {
            var go = new GameObject("Squad " + q.Id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SquadView>();
            v.Id = q.Id; v.Owner = q.Owner; v.Kind = q.Kind;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1f, 0); col.size = new Vector3(4f, 2f, 4f); col.isTrigger = true;
            var mb = new MeshBuilder();
            TerrainBuilder.Disc(mb, new Vector3(0, 0.24f, 0), 1.9f, 16, q.Owner == PlayerIds.Human ? StrategyModels.PlayerBlue : StrategyModels.RivalRed);
            v._disc = mb.ToObject("SquadDisc", go.transform, MaterialFactory.SharedUnlit(Color.white));
            return v;
        }

        public void Sync(WorldState s, Squad q)
        {
            At = q.At; Engaged = q.Engaged; Moving = q.CurrentEdge >= 0;
            _from = q.FromVertex; _to = q.ToVertex; _progress = q.Progress; _ticksPerEdge = Mathf.Max(1, s.Military.Tuning.TicksPerEdge);
            bool guard = q.Kind == SquadKind.Garrison;
            var team = q.Owner == PlayerIds.Human ? StrategyModels.PlayerBlue : StrategyModels.RivalRed;
            while (_figures.Count < q.Units.Count) _figures.Add(StrategyModels.Soldier(transform, team, guard));
            while (_figures.Count > q.Units.Count) { Destroy(_figures[_figures.Count - 1].Root); _figures.RemoveAt(_figures.Count - 1); }
            var view = MilitaryApi.DescribeSquad(s, q.Id);
            string who = q.Owner == PlayerIds.Human ? "Squad " + q.Id : q.Kind == SquadKind.Garrison ? "Guards" : "Raiders";
            Caption = who + ": " + view.Units + ", " + view.TotalHp + "/" + view.MaxHp + (q.Engaged ? " - fighting" : q.Owner == PlayerIds.Human && q.Order != OrderKind.None ? " - " + q.Order.ToString().ToLowerInvariant() : "");
        }

        public void Flash() => _flash = 0.25f;

        public void Animate(WorldState s, float frac, float zoom, bool god)
        {
            Vector3 pos;
            Vector3 dir = Vector3.forward;
            if (Moving && _from >= 0 && _to >= 0)
            {
                var a = BoardSpace.Vertex(_from); var b = BoardSpace.Vertex(_to);
                float u = Mathf.Clamp01((_progress + frac) / _ticksPerEdge);
                pos = Vector3.Lerp(a, b, u);
                dir = (b - a); dir.y = 0; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            }
            else
            {
                pos = BoardSpace.Vertex(At);
                // Spread several squads at one flag; the garrison stands round the outpost.
                // At the outpost a fight meets in front of the gate: guards a step out, attackers facing them.
                if (Kind == SquadKind.Garrison) pos += new Vector3(0, 0, Engaged ? -10f : -11.5f);
                else if (At == s.Board.RivalOutpostVertex) pos += new Vector3(0, 0, Engaged ? -13.5f : -16f);
                else pos += Quaternion.Euler(0, Slot * 72f, 0) * new Vector3(3.2f, 0, 0) * (Engaged ? 0.6f : 1f);
                var face = Owner == PlayerIds.Human ? BoardSpace.Vertex(s.Board.RivalOutpostVertex) - pos
                         : Engaged && Kind == SquadKind.Garrison ? new Vector3(0, 0, -1f) : BoardSpace.Vertex(s.Board.HomeCityVertex) - pos;
                face.y = 0;
                if (face.sqrMagnitude > 1f) dir = face.normalized;
            }
            transform.position = pos;
            var right = Vector3.Cross(Vector3.up, dir);
            float scale = god ? Mathf.Lerp(1f, 2.4f, (zoom - 1f) / 4f) : 1f;
            for (int i = 0; i < _figures.Count; i++)
            {
                var f = _figures[i];
                int row = i / 2, colm = i % 2;
                var off = right * ((colm - 0.5f) * 1.3f * scale) - dir * (row * 1.4f * scale);
                f.Root.transform.position = pos + off;
                f.Root.transform.rotation = Quaternion.LookRotation(dir);
                f.Root.transform.localScale = Vector3.one * scale;
                if (Engaged)
                {
                    float jab = Mathf.Max(0f, Mathf.Sin(Time.time * 9f + i * 1.9f));
                    f.Spear.localPosition = new Vector3(0.36f, 1.15f, jab * 0.6f);
                    f.Body.localRotation = Quaternion.Euler(jab * 10f, 0, 0);
                }
                else if (Moving)
                {
                    float step = Mathf.Sin(Time.time * 10f + i);
                    f.Body.localPosition = new Vector3(0, Mathf.Abs(step) * 0.08f, 0);
                    f.Spear.localPosition = new Vector3(0.36f, 1.15f, 0f);
                    f.Body.localRotation = Quaternion.identity;
                }
                else { f.Spear.localPosition = new Vector3(0.36f, 1.15f, 0f); f.Body.localRotation = Quaternion.identity; f.Body.localPosition = Vector3.zero; }
            }
            if (_disc != null) _disc.transform.localScale = Vector3.one * (god ? Mathf.Pow(zoom, 0.7f) : 1f) * (_flash > 0 ? 1.4f : 1f);
            _flash -= Time.deltaTime;
        }

        public string Title => Owner == PlayerIds.Human ? "Squad " + Id : Kind == SquadKind.Garrison ? "Rival guards" : "Rival raiders";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = WorldBootstrap.Instance.Sim.State;
                var v = MilitaryApi.DescribeSquad(s, Id);
                yield return v.Units + " " + (v.Units == 1 ? "fighter" : "fighters") + ", health " + v.TotalHp + "/" + v.MaxHp + ".";
                yield return v.OrderText + (v.Target >= 0 && v.Order != OrderKind.None ? " Target: " + StrategyUi.SiteName(s, v.Target) + "." : "");
                if (Owner == PlayerIds.Human) yield return "Orders: God View, Army tool (N).";
            }
        }
    }

    // ======================================================================== power

    public sealed class PowerSign : MonoBehaviour
    {
        /// <summary>1 powered, 0 unpowered, 2 plant fuelled, 3 plant idle, 4 needs no power now.</summary>
        public int State { get; private set; } = -1;
        private GameObject _bolt;
        private BuildingView _bv;

        public bool ShowsNoPower => State == 0;

        public static PowerSign Create(BuildingView bv)
        {
            var go = new GameObject("PowerSign");
            go.transform.SetParent(bv.transform, false);
            var p = go.AddComponent<PowerSign>();
            p._bv = bv;
            p._bolt = StrategyModels.Bolt(go.transform);
            return p;
        }

        public void Set(int state)
        {
            if (state == State) return;
            State = state;
            var c = state == 1 || state == 2 ? new Color(1f, 0.9f, 0.2f) : state == 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.55f, 0.55f, 0.55f);
            StrategyModels.SetMaterial(_bolt, state == 3 || state == 4 ? MaterialFactory.Shared(c) : StrategyModels.Glow(c));
        }

        public void Animate(float zoom)
        {
            if (_bv == null) return;
            // Beside the building, level with its roof (the name tag sits above it).
            float h = _bv.ModelHeight * 0.75f + 1.5f + (zoom - 1f) * 1.5f;
            transform.position = _bv.transform.position + Vector3.up * h + _bv.transform.right * (4.5f + (zoom - 1f) * 1.5f);
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : null;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float blink = State == 0 && Mathf.Repeat(Time.time, 0.8f) < 0.3f ? 0.6f : 1f;
            transform.localScale = Vector3.one * zoom * blink * 1.3f;
        }
    }
}
