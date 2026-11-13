using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The readable parts of any construction site: one block per unit of its cost, solid in the ware's colour when
    /// delivered and a dark frame while missing, a progress bar on a post, and a builder figure while it is being
    /// built. Owned by a building, road or camp view (package V).
    /// </summary>
    public sealed class SiteParts
    {
        public const float BarLength = 2.0f;

        private readonly Transform _root;
        private GameObject _piles, _barBack, _barFill, _post;
        private string _sig;
        public LandFigure Builder { get; private set; }
        public int DeliveredShown { get; private set; }
        public int MissingShown { get; private set; }
        public float ProgressShown { get; private set; } = -1f;

        public SiteParts(Transform root) { _root = root; }

        /// <param name="origin">Local position of the first pile row.</param>
        /// <param name="along">Local direction the piles line up in.</param>
        public void Sync(int[] cost, int[] delivered, float progress, bool building, Vector3 origin, Vector3 along, Vector3 barPos, int faction)
        {
            var sb = new System.Text.StringBuilder();
            int have = 0, miss = 0;
            for (int w = 0; w < cost.Length && w < Wares.Count; w++)
            {
                if (cost[w] <= 0) continue;
                int d = Mathf.Min(delivered != null && w < delivered.Length ? delivered[w] : 0, cost[w]);
                have += d; miss += cost[w] - d;
                sb.Append(w).Append(':').Append(d).Append('/').Append(cost[w]).Append(';');
            }
            string sig = sb.ToString();
            DeliveredShown = have; MissingShown = miss;
            if (sig != _sig)
            {
                _sig = sig;
                var mb = new MeshBuilder();
                var side = Vector3.Cross(Vector3.up, along.normalized);
                int col = 0;
                for (int w = 0; w < cost.Length && w < Wares.Count; w++)
                {
                    if (cost[w] <= 0) continue;
                    int d = Mathf.Min(delivered != null && w < delivered.Length ? delivered[w] : 0, cost[w]);
                    var c = LandLook.WareColor((Ware)w);
                    for (int i = 0; i < cost[w]; i++)
                    {
                        var p = origin + along.normalized * (col * 0.75f) + side * ((i % 2) * 0.62f) + Vector3.up * (0.27f + (i / 2) * 0.52f);
                        if (i < d) { mb.Box(p, new Vector3(0.55f, 0.5f, 0.55f), c); mb.Box(p + Vector3.up * 0.26f, new Vector3(0.58f, 0.04f, 0.58f), c * 0.75f); }
                        else Frame(mb, p, 0.55f, new Color(0.18f, 0.14f, 0.10f));
                    }
                    col++;
                }
                LandLook.SetMesh(ref _piles, _root, "SitePiles", mb);
            }

            if (_post == null)
            {
                _post = LandLook.Part(_root, "ProgressPost", LandLook.Cached("site/post", m =>
                {
                    LandShapes.Beam(m, Vector3.zero, Vector3.up * 2.6f, 0.06f, 5, LandLook.DarkWood);
                    m.Box(new Vector3(0, 2.6f, 0), new Vector3(BarLength + 0.2f, 0.36f, 0.12f), LandLook.DarkWood);
                }));
                _barBack = LandLook.Part(_post.transform, "BarBack", LandLook.Cached("site/barback", m =>
                {
                    m.Box(Vector3.zero, new Vector3(BarLength, 0.22f, 0.16f), new Color(0.25f, 0.12f, 0.08f));
                }), LandLook.Unlit, false);
                _barBack.transform.localPosition = new Vector3(0, 2.6f, 0);
                _barFill = LandLook.Part(_post.transform, "BarFill", LandLook.Cached("site/barfill", m =>
                {
                    m.Box(new Vector3(0.5f, 0, 0), new Vector3(1f, 0.24f, 0.2f), new Color(0.45f, 0.95f, 0.30f));
                }), LandLook.Unlit, false);
            }
            _post.transform.localPosition = barPos;
            _post.transform.localRotation = Quaternion.LookRotation(side(along), Vector3.up);
            // Progress: goods count for the first half, build time for the second.
            float total = have + miss;
            float shown = 0.5f * (total > 0 ? have / total : 1f) + 0.5f * Mathf.Clamp01(progress);
            if (!Mathf.Approximately(shown, ProgressShown))
            {
                ProgressShown = shown;
                _barFill.transform.localPosition = new Vector3(-BarLength / 2, 2.6f, 0);
                _barFill.transform.localScale = new Vector3(Mathf.Max(0.001f, shown * BarLength), 1, 1);
            }

            if (building && Builder == null)
            {
                Builder = LandFigure.Create(_root, "Builder", faction);
                Builder.Pose = FigurePose.Work;
            }
            if (Builder != null)
            {
                Builder.gameObject.SetActive(building);
                Builder.transform.localPosition = origin - along.normalized * 1.3f + Vector3.Cross(Vector3.up, along.normalized) * 1.4f;
                Builder.transform.localRotation = Quaternion.LookRotation(-Vector3.Cross(Vector3.up, along.normalized), Vector3.up);
            }
        }

        private static Vector3 side(Vector3 along) { var s = Vector3.Cross(Vector3.up, along.normalized); return s.sqrMagnitude > 0 ? s : Vector3.forward; }

        public void Animate(float now)
        {
            if (Builder != null && Builder.gameObject.activeSelf) Builder.Animate(now);
            if (_post != null)
            {
                // The bar board turns to the viewer so it never shows edge-on.
                var d = _post.transform.position - LandViews.Eye; d.y = 0;
                if (d.sqrMagnitude > 0.01f) _post.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
        }

        public void Clear()
        {
            if (_piles != null) Object.Destroy(_piles);
            if (_post != null) Object.Destroy(_post);
            if (Builder != null) Object.Destroy(Builder.gameObject);
            _piles = _post = null; Builder = null; _sig = null; ProgressShown = -1f;
        }

        /// <summary>A wire box (missing goods).</summary>
        public static void Frame(MeshBuilder mb, Vector3 c, float s, Color col)
        {
            float h = s / 2, t = 0.05f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -h : h), z = (i < 2 ? -h : h);
                mb.Box(c + new Vector3(x, 0, z), new Vector3(t, s, t), col);
            }
            foreach (float y in new[] { -h, h })
            {
                mb.Box(c + new Vector3(0, y, -h), new Vector3(s, t, t), col);
                mb.Box(c + new Vector3(0, y, h), new Vector3(s, t, t), col);
                mb.Box(c + new Vector3(-h, y, 0), new Vector3(t, t, s), col);
                mb.Box(c + new Vector3(h, y, 0), new Vector3(t, t, s), col);
            }
        }
    }
}
