using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>What the rhythm encounter is asked at a spire (EncounterContext.Request): which spire, in order of rising.</summary>
    public sealed class RhythmRequest
    {
        /// <summary>0-based order of rising: 0 = the first spire (today's Lv1 Warehouse chart).</summary>
        public int SpireIndex;
        public SpireStage Stage;
    }

    /// <summary>
    /// The crystal spires (CC-5, SP-1..SP-4), a view of <c>CampaignState.Spires</c> and the <see cref="SpireRaised"/> event:
    /// a tall original-primitive spire in the element's colour with a light beam, and beside it a stage (a dance circle with
    /// four drums and a fire, <see cref="SpireStage"/>) where the rhythm game is played. Spires are separate objects, so they
    /// stay when the settlement burns. The first spire retires the home camp's dance circle (SP-1). Installed by
    /// <see cref="GodBoardInstaller"/> on a god board game. Reads state only.
    /// </summary>
    public sealed class SpireDirector : MonoBehaviour
    {
        public const string SolidOwner = "spires";
        public static SpireDirector Instance { get; private set; }

        public static Color ElementColour(Element e)
        {
            switch (e)
            {
                case Element.Earth: return new Color(0.55f, 0.36f, 0.20f);
                case Element.Fire: return new Color(0.86f, 0.16f, 0.12f);
                case Element.Water: return new Color(0.16f, 0.42f, 0.90f);
                default: return new Color(0.58f, 0.30f, 0.88f);
            }
        }

        private readonly List<SpireStage> _spires = new List<SpireStage>();
        private WorldBootstrap _boot;
        public IReadOnlyList<SpireStage> Spires => _spires;
        public int Count => _spires.Count;
        public SpireStage Stage(int index) => index >= 0 && index < _spires.Count ? _spires[index] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static SpireDirector Install(WorldBootstrap boot)
        {
            var go = new GameObject("Spires");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<SpireDirector>();
            d.Init(boot);
            return d;
        }

        private void Init(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            var list = boot.Sim.State.Campaign?.Spires;
            if (list != null) foreach (var sp in list) Raise(sp.Element, sp.People, sp.Corner, false);
            boot.Clock.Ticked += OnTicked;
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            if (Instance == this) Instance = null;
            WalkGrid.RemoveStaticBlockers(SolidOwner);
        }

        /// <summary>Handles one tick's events (public so tests can deliver a <see cref="SpireRaised"/>).</summary>
        public void OnTicked(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events)
                if (e is SpireRaised sr)
                {
                    try { Raise(sr.Element, sr.People, sr.Corner, true); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
        }

        /// <summary>Builds the next spire at a corner (also for dev: a spire view without any campaign state). Returns it.</summary>
        public SpireStage Raise(Element element, int people, int corner, bool rose)
        {
            var s = _boot.Sim.State;
            var board = s.Board;
            if (corner < 0 || corner >= board.Topology.CornerCount) corner = board.StartCorners[0];
            int index = _spires.Count;
            var stage = SpireStage.Create(transform, board, corner, element, people, index, _spires);
            _spires.Add(stage);
            WalkGrid.AddSolid(stage.SpireBase, SpireStage.BaseRadius, SolidOwner);
            var l = stage.Layout;
            WalkGrid.AddStaticBlocker(l.DanceCircleCentre, CampLayout.FireBlockRadius, SolidOwner);
            for (int p = 1; p <= 4; p++) WalkGrid.AddStaticBlocker(l.DrumPosition(p), CampLayout.DrumBlockRadius, SolidOwner);
            try { LandViews.AddClearing(l.DanceCircleCentre.x, l.DanceCircleCentre.z, 9f); } catch (Exception) { }
            if (index == 0) RetireHomeCircle();
            return stage;
        }

        /// <summary>SP-1: the home camp's dance circle is retired when the first spire rises.</summary>
        private static void RetireHomeCircle()
        {
            var home = DanceCircle.Current;
            if (home == null || home.Retired) return;
            home.Retire();
            WalkGrid.RemoveStaticBlockers(CampInstaller.BlockerOwner);
        }
    }

    /// <summary>
    /// One spire and its rhythm stage. Tap it (or press E close by) to dance: <see cref="Interact"/> starts the rhythm
    /// encounter here (SP-4: any spire, rivals' too). The pack played depends on the order of rising (SP-2, SP-3).
    /// </summary>
    public sealed class SpireStage : MonoBehaviour, IInteractable, IInspectable
    {
        public const float BaseRadius = 2.0f, Height = 15f, StageDistance = 16f, Footprint = 9f, UseRange = 22f;

        public int Index { get; private set; }
        public int People { get; private set; }
        public int Corner { get; private set; }
        public Element Element { get; private set; }
        public Vector3 SpireBase { get; private set; }
        public CampLayout Layout { get; private set; }
        public DanceCircle Circle { get; private set; }
        public Transform Beam { get; private set; }
        public Color Colour { get; private set; }

        private Material _beamMat;
        private float _clock;

        public string Prompt => "Dance at the spire";
        public string Title => "Spire of " + Element + " (" + (Index + 1) + ")";
        public IEnumerable<string> Lines
        {
            get
            {
                yield return "A crystal spire. Four drums and a fire stand at its foot: dance here.";
                yield return LocalRhythmPacks.PackIdOfSpire(Index) == null ? "Track: Lv1 Warehouse." : "Track: " + LocalRhythmPacks.PackIdOfSpire(Index) + " (if the local pack is installed).";
            }
        }

        public static SpireStage Create(Transform parent, Board board, int corner, Element element, int people, int index, IReadOnlyList<SpireStage> others)
        {
            var go = new GameObject("Spire " + (index + 1) + " " + element);
            go.transform.SetParent(parent, false);
            var st = go.AddComponent<SpireStage>();
            st.Index = index; st.People = people; st.Corner = corner; st.Element = element; st.Colour = SpireDirector.ElementColour(element);
            var c = BoardSpace.Vertex(board.Topology, corner);
            st.Place(board, c, others);
            return st;
        }

        private void Place(Board board, Vector3 corner, IReadOnlyList<SpireStage> others)
        {
            var outward = corner; outward.y = 0;
            float baseYaw = outward.sqrMagnitude > 1 ? BoardSpace.YawOf(outward) : 0f;
            baseYaw += Index * 47f; // a second spire at the same corner (or a neighbour) takes a different side
            Vector3 dir = BoardSpace.DirOf(baseYaw), centre = corner + dir * StageDistance;
            bool found = false;
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                float footprint = pass == 0 ? Footprint : Footprint * 0.6f;
                for (float r = StageDistance; r <= 48f && !found; r += 3f)
                    for (int k = 0; k < 24 && !found; k++)
                    {
                        float off = ((k + 1) / 2) * 15f * (k % 2 == 0 ? 1 : -1);
                        var d = BoardSpace.DirOf(baseYaw + off);
                        var p = corner + d * r;
                        if (StageOk(board, p, d, footprint, others)) { dir = d; centre = p; found = true; }
                    }
            }
            SpireBase = corner + dir * 4.5f;
            centre.y = BoardSpace.GroundY;
            SpireBase = new Vector3(SpireBase.x, BoardSpace.GroundY, SpireBase.z);
            // The dancer stands on the far side of the fire from the spire, so the spire rises behind the fire.
            Layout = CampLayout.ForStage(centre, dir);
            BuildSpire();
            Circle = DanceCircle.Build(transform, Layout, false);
            BuildDais();
            var pick = new GameObject("Pick");
            pick.transform.SetParent(transform, false);
            LandLook.PickBox(pick, SpireBase + Vector3.up * (Height * 0.5f), new Vector3(BaseRadius * 2f, Height, BaseRadius * 2f));
        }

        private static bool StageOk(Board board, Vector3 p, Vector3 dir, float footprint, IReadOnlyList<SpireStage> others)
        {
            var t = board.Topology;
            if (t.HexAt(p.x, p.z) < 0) return false;
            var dancer = p + dir * (CampLayout.DancerDistance + 1f);
            if (t.HexAt(dancer.x, dancer.z) < 0) return false;
            for (int i = 0; i < 8; i++)
            {
                var q = p + BoardSpace.DirOf(i * 45f) * footprint;
                if (t.HexAt(q.x, q.z) < 0) return false;
            }
            if (CampLayout.Obstruction(board, p, footprint) != null) return false;
            if (CampLayout.Obstruction(board, dancer, 1.5f) != null) return false;
            foreach (var o in others) if (BoardSpace.Flat(o.Layout.DanceCircleCentre - p) < 18f) return false;
            var home = DanceCircle.Current;
            if (home != null && BoardSpace.Flat(home.Centre - p) < 16f) return false;
            var gb = GodBoardDirector.Instance;
            if (gb != null) foreach (var tv in gb.Temples) if (BoardSpace.Flat(tv.transform.position - p) < footprint + TempleView.Radius + 2f) return false;
            return true;
        }

        private void BuildSpire()
        {
            var stone = new Color(0.64f, 0.62f, 0.60f);
            var tint = Colour;
            var mb = new MeshBuilder();
            var b = SpireBase;
            mb.Cylinder(b, BaseRadius + 0.9f, BaseRadius + 0.4f, 0.45f, 10, stone * 0.8f);
            mb.Cylinder(b + Vector3.up * 0.45f, BaseRadius, BaseRadius - 0.35f, 0.5f, 10, stone);
            // Shaft in the element's colour, tapering, with two bands of stone.
            mb.Cylinder(b + Vector3.up * 0.95f, 1.35f, 0.95f, 6.5f, 6, Color.Lerp(tint, stone, 0.25f));
            mb.Cylinder(b + Vector3.up * 7.2f, 1.05f, 0.7f, 5.5f, 6, tint);
            mb.Cylinder(b + Vector3.up * 3.6f, 1.25f, 1.25f, 0.28f, 6, stone * 0.9f);
            mb.Cylinder(b + Vector3.up * 9.6f, 0.95f, 0.95f, 0.25f, 6, stone * 0.9f);
            mb.Cone(b + Vector3.up * 12.7f, 0.7f, 2.0f, 6, tint * 0.9f);
            // Floating crystal: two cones base to base.
            var top = b + Vector3.up * 15.0f;
            mb.Cone(top, 0.9f, 2.0f, 4, Color.Lerp(tint, Color.white, 0.35f));
            mb.Cylinder(top - Vector3.up * 0.5f, 0.35f, 0.9f, 0.5f, 4, tint);
            // Four buttresses.
            for (int i = 0; i < 4; i++)
            {
                var d = BoardSpace.DirOf(i * 90f + 45f);
                mb.Box(b + d * 1.5f + Vector3.up * 1.7f, new Vector3(0.55f, 2.4f, 0.55f), stone * 0.85f, i * 90f + 45f);
            }
            var body = mb.ToObject("Spire", transform, MaterialFactory.CreateVertexColored(Color.white, "Spire " + Element));
            body.layer = 2;

            // The beam: a tall additive cylinder, plus a point light under the crystal.
            _beamMat = MaterialFactory.CreateGlow(new Color(tint.r, tint.g, tint.b, 0.5f), "Spire beam");
            var bm = new MeshBuilder();
            bm.Cylinder(Vector3.zero, 1.2f, 0.7f, 70f, 10, new Color(1, 1, 1, 0.9f));
            Beam = bm.ToObject("Beam", transform, _beamMat).transform;
            Beam.position = top + Vector3.up * 1.2f;
            Beam.gameObject.layer = 2;
            var lgo = new GameObject("Spire light");
            lgo.transform.SetParent(transform, false);
            lgo.transform.position = top;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 26f; l.intensity = 2.2f; l.color = Color.Lerp(tint, Color.white, 0.3f);
        }

        /// <summary>The stage's dais: a low stone-and-colour platform under the dance circle.</summary>
        private void BuildDais()
        {
            var mb = new MeshBuilder();
            var c = Layout.DanceCircleCentre;
            var stone = new Color(0.60f, 0.57f, 0.52f);
            mb.Cylinder(c + Vector3.up * 0.005f, 7.6f, 7.4f, 0.08f, 20, Color.Lerp(Colour, stone, 0.6f));
            for (int i = 0; i < 8; i++)
            {
                var p = c + BoardSpace.DirOf(i * 45f) * 7.6f;
                mb.Cylinder(p, 0.32f, 0.26f, 1.1f, 6, stone);
                mb.Cone(p + Vector3.up * 1.1f, 0.3f, 0.5f, 6, Colour);
            }
            var o = mb.ToObject("Dais", transform, MaterialFactory.CreateVertexColored(Color.white, "Spire stage"));
            o.layer = 2;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            if (_beamMat != null)
            {
                float a = 0.42f + 0.10f * Mathf.Sin(_clock * 1.7f + Index);
                _beamMat.color = new Color(Colour.r, Colour.g, Colour.b, a);
            }
        }

        private void OnDestroy() { if (_beamMat != null) Destroy(_beamMat); }

        /// <summary>Distance (flat) from the player to this stage's fire, or a large number with no player.</summary>
        public float DistanceFromPlayer()
        {
            var b = WorldBootstrap.Instance;
            if (b == null || b.Avatar == null) return 9999f;
            return BoardSpace.Flat(b.Avatar.Position - Layout.DanceCircleCentre);
        }

        public void Interact()
        {
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            var gb = GodBoardDirector.Instance;
            if (gb != null && gb.IsOpen) return;
            if (DistanceFromPlayer() > UseRange)
            {
                SpeechBubbles.SayAt(Layout.DanceCircleCentre + Vector3.up * 3f, "Walk up to the spire's stage to dance.", 3f);
                return;
            }
            StartDance();
        }

        /// <summary>Starts the rhythm encounter here (also for tests and dev tools).</summary>
        public bool StartDance() => EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, null, new RhythmRequest { SpireIndex = Index, Stage = this });
    }
}
