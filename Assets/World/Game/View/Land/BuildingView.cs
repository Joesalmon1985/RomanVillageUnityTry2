using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A building or building site on its plot (REFACTOR_SPEC 8.4). Site: stakes and rope, one block per unit of its
    /// cost (solid when delivered), a progress bar. Constructing: the model rising inside scaffold poles with a
    /// builder at work. Active: the model; collectors send a gatherer to the hex's stock patch while a cycle runs and
    /// bring a bundle back; workshops show a worker at the bench while producing; a lodge shows its warriors (fed
    /// standing with spears, hungry slumped by an empty bowl). Retiring: the model packed up with a lowered pennant.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour, IInspectable
    {
        public const float GathererSpeed = 6f, WorkSeconds = 1.1f;

        public int BuildingId { get; private set; }
        public BuildingType Type { get; private set; }
        public int Faction { get; private set; }
        public int Plot { get; private set; }
        public int Hex { get; private set; }
        public BuildingState ShownState { get; private set; } = (BuildingState)(-1);
        public float ShownBuildFraction { get; private set; }
        public bool IsCollector { get; private set; }
        public bool Working { get; private set; }
        public LandFigure Gatherer { get; private set; }
        public LandFigure Worker { get; private set; }
        public SiteParts Site { get; private set; }
        public int WarriorsShown => _warriors.Count;
        public int FedWarriorsShown { get; private set; }
        public Vector3 PatchSpot { get; private set; }

        private GameObject _model, _stakes, _scaffold, _packed;
        private readonly List<LandFigure> _warriors = new List<LandFigure>();
        private readonly List<GameObject> _bowls = new List<GameObject>();

        // Gatherer loop.
        private enum Leg { Home, Out, Work, Back }
        private Leg _leg = Leg.Home;
        private float _legStart;
        private Vector3 _home;

        public string Title => LandText.State != null ? LandText.BuildingTitle(LandText.State, BuildingId) : Type.ToString();
        public IEnumerable<string> Lines => LandText.State != null ? LandText.BuildingLines(LandText.State, BuildingId) : new string[0];

        public static BuildingView Create(Transform parent, WorldState s, Building b)
        {
            var t = s.Board.Topology;
            var def = BuildingDefs.Get(b.Type);
            var go = new GameObject(def.Name + " " + b.Id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(LandLook.Ground(PlotLayout.PlotX(t, b.Plot), PlotLayout.PlotZ(t, b.Plot)),
                Quaternion.Euler(0, (float)PlotLayout.PlotYaw(t, b.Plot), 0));
            var v = go.AddComponent<BuildingView>();
            v.BuildingId = b.Id; v.Type = b.Type; v.Faction = b.Faction; v.Plot = b.Plot; v.Hex = b.Hex;
            v.IsCollector = def.IsCollector;
            v.Site = new SiteParts(go.transform);
            v._home = go.transform.position + go.transform.forward * 3.4f;
            if (b.Hex >= 0)
            {
                var c = BoardSpace.Hex(t, b.Hex);
                var d = go.transform.position - c; d.y = 0;
                v.PatchSpot = c + d.normalized * 6.9f;
            }
            LandLook.PickBox(go, Vector3.up * 1.5f, new Vector3(8f, 3f, 8f));
            return v;
        }

        public void Sync(WorldState s, Building b, List<Warrior> warriors)
        {
            var def = BuildingDefs.Get(b.Type);
            if (b.State != ShownState)
            {
                var old = ShownState;
                ShownState = b.State;
                bool site = b.State == BuildingState.Site;
                bool model = b.State == BuildingState.Constructing || b.State == BuildingState.Active || b.State == BuildingState.Retiring;
                if (model && _model == null)
                    _model = LandLook.Part(transform, "Model", LandLook.Cached("bld/" + (int)b.Type + "/" + b.Faction, mb => LandShapes.Building(mb, b.Type, b.Faction)));
                if (_model != null) _model.SetActive(model);
                if (site || b.State == BuildingState.Constructing)
                {
                    if (_stakes == null)
                        _stakes = LandLook.Part(transform, "Stakes", LandLook.Cached("bld/stakes", mb =>
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                var p = Quaternion.Euler(0, 45f + i * 90f, 0) * Vector3.forward * 4.2f;
                                var q = Quaternion.Euler(0, 135f + i * 90f, 0) * Vector3.forward * 4.2f;
                                LandShapes.Beam(mb, p, p + Vector3.up * 1.0f, 0.06f, 4, LandLook.DarkWood);
                                LandShapes.Beam(mb, p + Vector3.up * 0.85f, q + Vector3.up * 0.85f, 0.02f, 3, LandLook.Thatch, false);
                            }
                            LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 3.9f, 16, new Color(0.50f, 0.40f, 0.28f));
                        }));
                    _stakes.SetActive(true);
                }
                else if (_stakes != null) _stakes.SetActive(false);
                if (b.State == BuildingState.Constructing && _scaffold == null)
                {
                    float h = LandShapes.Height(b.Type);
                    _scaffold = LandLook.Part(transform, "Scaffold", LandLook.Cached("bld/scaffold/" + h, mb =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var p = Quaternion.Euler(0, 45f + i * 90f, 0) * Vector3.forward * 3.4f;
                            LandShapes.Beam(mb, p, p + Vector3.up * (h + 0.6f), 0.07f, 4, LandLook.Wood);
                            var q = Quaternion.Euler(0, 135f + i * 90f, 0) * Vector3.forward * 3.4f;
                            LandShapes.Beam(mb, p + Vector3.up * h * 0.5f, q + Vector3.up * h * 0.5f, 0.05f, 4, LandLook.Wood);
                        }
                    }));
                }
                if (_scaffold != null) _scaffold.SetActive(b.State == BuildingState.Constructing);
                if (!site && b.State != BuildingState.Constructing) Site.Clear();
                if (b.State == BuildingState.Retiring && _packed == null)
                    _packed = LandLook.Part(transform, "Packed", LandLook.Cached("bld/packed", mb =>
                    {
                        for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.8f + i * 0.8f, 0.3f, 3.0f), new Vector3(0.6f, 0.6f, 0.5f), LandLook.Hide * 0.9f, i * 20f);
                        LandShapes.Beam(mb, new Vector3(1.8f, 0, 3.0f), new Vector3(1.8f, 1.0f, 3.0f), 0.05f, 4, LandLook.DarkWood);
                    }));
                if (_packed != null) _packed.SetActive(b.State == BuildingState.Retiring);
                if (_model != null && b.State == BuildingState.Retiring) _model.transform.localScale = new Vector3(0.92f, 0.75f, 0.92f);
            }

            // Site goods and progress; the rising model while constructing.
            if (b.State == BuildingState.Site || b.State == BuildingState.Constructing)
            {
                float frac = b.State == BuildingState.Constructing ? b.BuildProgress / (float)Mathf.Max(1, def.BuildTicks) : 0f;
                var delivered = b.State == BuildingState.Constructing ? def.Cost : b.Delivered;
                Site.Sync(def.Cost, delivered, frac, b.State == BuildingState.Constructing,
                    new Vector3(-2.2f, 0, 2.6f), Vector3.right, new Vector3(2.9f, 0, -2.9f), b.Faction);
                if (_model != null && b.State == BuildingState.Constructing)
                {
                    ShownBuildFraction = frac;
                    _model.transform.localScale = new Vector3(1f, Mathf.Lerp(0.12f, 1f, frac), 1f);
                }
            }
            else if (_model != null && b.State == BuildingState.Active) { ShownBuildFraction = 1f; _model.transform.localScale = Vector3.one; }

            // Work: collectors gather, workshops produce.
            Working = b.State == BuildingState.Active && b.WorkProgress > 0;
            if (IsCollector && (b.State == BuildingState.Active || b.State == BuildingState.Retiring) && Gatherer == null)
            {
                Gatherer = LandFigure.Create(transform.parent, "Gatherer " + b.Id, b.Faction);
                LandInspectTag.Add(Gatherer.gameObject, () => "Gatherer", () => GathererLines(), Vector3.up, new Vector3(0.9f, 2f, 0.9f));
                Gatherer.Place(_home, transform.eulerAngles.y);
                _leg = Leg.Home;
            }
            if (!IsCollector && def.Class == BuildingClass.Workshop && b.State == BuildingState.Active)
            {
                if (Worker == null)
                {
                    Worker = LandFigure.Create(transform, "Worker", b.Faction);
                    Worker.transform.localPosition = new Vector3(1.6f, 0, 2.2f);
                    Worker.transform.localRotation = Quaternion.Euler(0, 200f, 0);
                }
                Worker.Pose = Working ? FigurePose.Work : FigurePose.Stand;
            }

            if (b.Type == BuildingType.WarriorLodge) SyncWarriors(warriors);
        }

        private IEnumerable<string> GathererLines()
        {
            var s = LandText.State;
            if (s == null) yield break;
            var b = s.Economy.Buildings[BuildingId];
            var def = BuildingDefs.Get(b.Type);
            string ware = Wares.DisplayName(TerrainInfo.RawWare(def.Terrain).Value);
            yield return "Works the " + def.Name.ToLowerInvariant() + ": " + (Working ? "taking " + ware + " from the " + TerrainInfo.DisplayName(def.Terrain) + "." : "waiting for the next cycle.");
        }

        private void SyncWarriors(List<Warrior> warriors)
        {
            int n = warriors != null ? Mathf.Min(warriors.Count, 6) : 0;
            while (_warriors.Count < n)
            {
                int i = _warriors.Count;
                var f = LandFigure.Create(transform, "Warrior " + i, Faction, LandFigure.Kind.Warrior);
                f.transform.localPosition = new Vector3(-1.6f + (i % 3) * 1.6f, 0, 3.3f + (i / 3) * 1.2f);
                f.transform.localRotation = Quaternion.identity;
                int idx = i;
                LandInspectTag.Add(f.gameObject, () => "Warrior", () => WarriorLinesAt(idx), Vector3.up, new Vector3(0.9f, 2f, 0.9f));
                var bowl = LandLook.Part(f.transform, "Bowl", LandLook.Cached("warrior/bowl", mb =>
                {
                    mb.Cylinder(new Vector3(0, 0, 0.7f), 0.18f, 0.26f, 0.14f, 8, LandLook.Wood);
                    LandShapes.Disc(mb, new Vector3(0, 0.12f, 0.7f), 0.2f, 8, new Color(0.1f, 0.08f, 0.06f));
                }), null, false);
                _warriors.Add(f); _bowls.Add(bowl);
            }
            while (_warriors.Count > n)
            {
                Destroy(_warriors[_warriors.Count - 1].gameObject);
                _warriors.RemoveAt(_warriors.Count - 1); _bowls.RemoveAt(_bowls.Count - 1);
            }
            FedWarriorsShown = 0;
            for (int i = 0; i < n; i++)
            {
                bool fed = warriors[i].Fed;
                if (fed) FedWarriorsShown++;
                _warriors[i].Pose = fed ? FigurePose.Stand : FigurePose.Slump;
                _bowls[i].SetActive(!fed);
                _warriorIds[i % _warriorIds.Length] = warriors[i].Id;
            }
        }

        private readonly int[] _warriorIds = new int[6];
        private IEnumerable<string> WarriorLinesAt(int i) => LandText.State != null ? LandText.WarriorLines(LandText.State, _warriorIds[i]) : new string[0];

        /// <summary>The gatherer's loop and the figures' animation (every frame, from LandViews).</summary>
        public void Animate(float now, bool halted)
        {
            Site.Animate(now);
            if (Worker != null) Worker.Animate(now);
            foreach (var w in _warriors) w.Animate(now);
            if (Gatherer == null) return;
            var g = Gatherer;
            float dist = (PatchSpot - _home).magnitude;
            float walk = dist / GathererSpeed;
            switch (_leg)
            {
                case Leg.Home:
                    if (Working && !halted && Hex >= 0) { _leg = Leg.Out; _legStart = now; g.SetLoad(false, Ware.Wood, 0, false); }
                    else { g.Pose = FigurePose.Stand; g.Place(_home, transform.eulerAngles.y + 180f); }
                    break;
                case Leg.Out:
                {
                    float u = Mathf.Clamp01((now - _legStart) / walk);
                    g.Pose = FigurePose.Walk;
                    g.Place(Vector3.Lerp(_home, PatchSpot, u), BoardSpace.YawOf(PatchSpot - _home));
                    if (u >= 1f) { _leg = Leg.Work; _legStart = now; }
                    break;
                }
                case Leg.Work:
                    g.Pose = FigurePose.Work;
                    if (now - _legStart >= WorkSeconds)
                    {
                        _leg = Leg.Back; _legStart = now;
                        var ware = TerrainInfo.RawWare(BuildingDefs.Get(Type).Terrain);
                        if (ware.HasValue) g.SetLoad(true, ware.Value, 1, false);
                    }
                    break;
                case Leg.Back:
                {
                    float u = Mathf.Clamp01((now - _legStart) / walk);
                    g.Pose = FigurePose.Walk;
                    g.Place(Vector3.Lerp(PatchSpot, _home, u), BoardSpace.YawOf(_home - PatchSpot));
                    if (u >= 1f) { _leg = Leg.Home; g.SetLoad(false, Ware.Wood, 0, false); }
                    break;
                }
            }
            g.Animate(now);
        }

        /// <summary>True while the gatherer is away from the building (walking or working at the patch).</summary>
        public bool GathererOut => Gatherer != null && _leg != Leg.Home;

        private void OnDestroy()
        {
            if (Gatherer != null) Destroy(Gatherer.gameObject);
        }
    }
}
