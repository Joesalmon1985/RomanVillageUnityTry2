using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Dresses one <see cref="BuildingView"/> from its state: the model of its grade and owner (swapped into the base
    /// view's "Model" part, so a captured camp's buildings take the new people's colours too), and, while a grade
    /// upgrade runs, scaffolding round the standing building, the delivered goods and a progress bar (the sim's
    /// goods), a builder while the work is under way, and a "closed" board while it waits for goods.
    /// </summary>
    public sealed class BuildingDress : MonoBehaviour
    {
        public Grade ShownGrade { get; private set; } = (Grade)(-1);
        public bool ScaffoldShown => _scaffold != null && _scaffold.activeSelf;
        public bool PausedShown => _barrier != null && _barrier.activeSelf;
        public int GoodsShown => _site != null ? _site.DeliveredShown : 0;
        public int GoodsMissingShown => _site != null ? _site.MissingShown : 0;
        public float ProgressShown => _site != null ? _site.ProgressShown : -1f;
        public bool BuilderShown => _site != null && _site.Builder != null && _site.Builder.gameObject.activeSelf;
        public string ModelKey { get; private set; } = "";
        public Mesh ModelMesh { get { var m = transform.Find("Model"); var mf = m != null ? m.GetComponent<MeshFilter>() : null; return mf != null ? mf.sharedMesh : null; } }

        private BuildingView _view;
        private GameObject _root, _scaffold, _barrier;
        private SiteParts _site;
        private float _scaffoldH;

        public void Apply(WorldState s, BuildingView v, Building b)
        {
            _view = v;
            if (_root == null)
            {
                _root = new GameObject("GradeDress");
                _root.transform.SetParent(transform, false);
                _site = new SiteParts(_root.transform);
                var old = GetComponent<BoxCollider>();
                if (old != null) old.enabled = false;
                var pick = new GameObject("GradePick");
                pick.transform.SetParent(transform, false);
                LandLook.PickBox(pick, Vector3.up * 1.5f, new Vector3(8f, 3f, 8f));
                pick.AddComponent<GradeInspect>().View = v;
            }
            bool modelState = b.State == BuildingState.Constructing || b.State == BuildingState.Active || b.State == BuildingState.Retiring;
            var model = transform.Find("Model");
            if (modelState && model != null)
            {
                var grade = b.State == BuildingState.Active || b.State == BuildingState.Retiring ? b.Grade : Grade.Prehistoric;
                string key = (int)b.Type + "/" + (int)grade + "/" + b.Faction;
                if (key != ModelKey)
                {
                    ModelKey = key; ShownGrade = grade;
                    var type = b.Type; int faction = b.Faction;
                    var mesh = grade >= Grade.Historic
                        ? LandLook.Cached("gbld/" + key, mb => HistoricShapes.Build(mb, type, faction))
                        : LandLook.Cached("bld/" + (int)type + "/" + faction, mb => LandShapes.Building(mb, type, faction));
                    model.GetComponent<MeshFilter>().sharedMesh = mesh;
                }
            }
            SyncUpgrade(s, b, model);
        }

        private void SyncUpgrade(WorldState s, Building b, Transform model)
        {
            bool up = b.State == BuildingState.Active && b.Upgrading;
            var def = BuildingDefs.Get(b.Type);
            var g = up && def.Grades != null && (int)b.Grade + 1 < def.Grades.Length ? def.Grades[(int)b.Grade + 1] : null;
            if (!up || g == null)
            {
                if (_scaffold != null) _scaffold.SetActive(false);
                if (_barrier != null) _barrier.SetActive(false);
                _site.Clear();
                return;
            }
            float top = model != null && model.GetComponent<MeshFilter>().sharedMesh != null ? model.GetComponent<MeshFilter>().sharedMesh.bounds.max.y : LandShapes.Height(b.Type);
            float h = Mathf.Max(2.5f, Mathf.Ceil(top * 2f) / 2f);
            if (_scaffold == null || !Mathf.Approximately(h, _scaffoldH))
            {
                if (_scaffold != null) Destroy(_scaffold);
                _scaffoldH = h;
                _scaffold = LandLook.Part(_root.transform, "Scaffold", LandLook.Cached("gbld/scaffold/" + h, mb => Scaffold(mb, h)));
            }
            _scaffold.SetActive(true);
            if (_barrier == null) _barrier = LandLook.Part(_root.transform, "WaitingBoard", LandLook.Cached("gbld/closed", ClosedBoard));

            var needed = GradeApi.UpgradeStillNeeded(s, b.Id);
            var cost = new int[Wares.Count]; var have = new int[Wares.Count];
            bool missing = false;
            for (int w = 0; w < Wares.Count; w++)
            {
                cost[w] = g.Goods[w];
                have[w] = Mathf.Min(b.UpgradeDelivered[w], g.Goods[w]);
                if (needed[w] > 0) missing = true;
            }
            _barrier.SetActive(missing);
            float progress = b.UpgradeProgress / (float)Mathf.Max(1, g.Ticks);
            bool working = !missing && b.UpgradeProgress > 0;
            _site.Sync(cost, have, progress, working, new Vector3(-3.9f, 0, -2.2f), Vector3.forward, new Vector3(3.7f, 0, -2.4f), b.Faction);
        }

        public void Animate(float now)
        {
            if (_site != null) _site.Animate(now);
        }

        public string Describe()
        {
            var s = LandText.State;
            return "grade " + _view.BuildingId + " " + ModelKey + " scaffold " + ScaffoldShown + " goods " + GoodsShown + "/" + GoodsMissingShown + " paused " + PausedShown;
        }

        /// <summary>Four corner poles round the building, braces, two plank decks and a ladder (the building inside stays in view).</summary>
        static void Scaffold(MeshBuilder mb, float h)
        {
            var wood = LandLook.Wood; var dark = LandLook.DarkWood;
            float r = 2.7f;
            var corners = new[] { new Vector3(-r, 0, -r), new Vector3(r, 0, -r), new Vector3(r, 0, r), new Vector3(-r, 0, r) };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                LandShapes.Beam(mb, a, a + Vector3.up * (h + 0.6f), 0.08f, 5, wood);
                for (float y = 1.0f; y <= h; y += 1.0f)
                    LandShapes.Beam(mb, a + Vector3.up * y, b + Vector3.up * y, 0.05f, 4, wood * 1.08f, false);
                LandShapes.Beam(mb, a + Vector3.up * 0.2f, b + Vector3.up * Mathf.Min(h, 2.0f), 0.035f, 3, dark, false);
            }
            foreach (float y in new[] { 1.0f, Mathf.Min(2.0f, h) })
            {
                mb.Box(new Vector3(0, y + 0.04f, -r), new Vector3(2 * r, 0.08f, 0.7f), wood * 1.15f);
                mb.Box(new Vector3(-r, y + 0.04f, 0), new Vector3(0.7f, 0.08f, 2 * r), wood * 1.15f);
                mb.Box(new Vector3(r, y + 0.04f, 0), new Vector3(0.7f, 0.08f, 2 * r), wood * 1.15f);
            }
            // Ladder on the right side.
            LandShapes.Beam(mb, new Vector3(r + 0.5f, 0, 1.0f), new Vector3(r + 0.2f, Mathf.Min(2.0f, h), 1.0f), 0.04f, 4, dark);
            LandShapes.Beam(mb, new Vector3(r + 0.5f, 0, 1.5f), new Vector3(r + 0.2f, Mathf.Min(2.0f, h), 1.5f), 0.04f, 4, dark);
            for (int i = 1; i < 6; i++) LandShapes.Beam(mb, new Vector3(r + 0.5f - 0.3f * i / 6f, 0.33f * i, 1.0f), new Vector3(r + 0.5f - 0.3f * i / 6f, 0.33f * i, 1.5f), 0.03f, 3, dark, false);
        }

        /// <summary>A short post with a board and a red cross: work is waiting for goods.</summary>
        static void ClosedBoard(MeshBuilder mb)
        {
            var p = new Vector3(-1.4f, 0, 3.0f);
            LandShapes.Beam(mb, p, p + Vector3.up * 1.4f, 0.05f, 4, LandLook.DarkWood);
            mb.Box(p + new Vector3(0, 1.3f, 0.03f), new Vector3(1.0f, 0.6f, 0.07f), new Color(0.82f, 0.72f, 0.52f));
            var red = new Color(0.75f, 0.12f, 0.08f);
            LandShapes.Beam(mb, p + new Vector3(-0.35f, 1.1f, 0.09f), p + new Vector3(0.35f, 1.5f, 0.09f), 0.045f, 4, red, false);
            LandShapes.Beam(mb, p + new Vector3(-0.35f, 1.5f, 0.09f), p + new Vector3(0.35f, 1.1f, 0.09f), 0.045f, 4, red, false);
        }
    }
}
