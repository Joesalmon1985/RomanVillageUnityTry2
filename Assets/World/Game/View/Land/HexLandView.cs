using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// One hex's number token and stock patch (REFACTOR_SPEC 8.4): a post at the hex centre with the number disc
    /// (turned to the camera, lit for 3 s when its number is rolled), and the 6 m patch around it with one item per
    /// unit of stock up to the cap: saplings to trees, seedlings to ripe grain, lambs to sheep in three young stages
    /// and mature; clay and stone only as dug piles when mature, with a "ready" flag. Rebuilt only when the counts
    /// change.
    /// </summary>
    public sealed class HexLandView : MonoBehaviour, IInspectable
    {
        public const float TokenHeight = 4.4f, TokenRadius = 1.05f;
        public const float FlareSeconds = 3f, PaidSeconds = 3f;

        public int HexId { get; private set; }
        public Terrain Terrain { get; private set; }
        public int Number { get; private set; }
        /// <summary>Items drawn now: mature, stage 2, stage 1, stage 0 (young of mined terrain are not drawn).</summary>
        public int ShownMature { get; private set; }
        public int ShownStage0 { get; private set; }
        public int ShownStage1 { get; private set; }
        public int ShownStage2 { get; private set; }
        public bool ShownStopped { get; private set; }
        public bool ReadyMarkShown => _ready != null && _ready.activeSelf;
        /// <summary>Unscaled time the number last flared (-1 never).</summary>
        public float LastFlare { get; private set; } = -100f;
        public float LastPaid { get; private set; } = -100f;
        public bool IsFlaring => Time.time - LastFlare < FlareSeconds;
        public bool IsPaying => Time.time - LastPaid < PaidSeconds;
        public Transform Token => _token;

        private Transform _token;
        private Renderer _disc;
        private GameObject _halo, _stopMark, _items, _ready, _beam, _ring;
        private TextMesh _paidText;
        private MaterialPropertyBlock _mpb;
        private int _sig = -1;
        private Ware? _ware;

        public string Title => LandText.State != null ? LandText.HexTitle(LandText.State, HexId) : "Hex";
        public IEnumerable<string> Lines => LandText.State != null ? LandText.HexLines(LandText.State, HexId) : new string[0];

        public static HexLandView Create(Transform parent, Board board, int hex)
        {
            var t = board.Topology;
            var go = new GameObject("Hex " + hex + " stock");
            go.transform.SetParent(parent, false);
            go.transform.position = BoardSpace.Hex(t, hex);
            var v = go.AddComponent<HexLandView>();
            v.HexId = hex;
            v.Terrain = board.HexTerrain[hex];
            v.Number = board.HexNumber[hex];
            v._ware = TerrainInfo.RawWare(v.Terrain);
            v._mpb = new MaterialPropertyBlock();
            v.BuildStatic();
            return v;
        }

        private void BuildStatic()
        {
            if (!_ware.HasValue) return; // meadow: no number, no patch
            // Patch ground (static).
            var mb = new MeshBuilder();
            PatchGround(mb, Terrain, HexId);
            LandLook.Part(transform, "PatchGround", mb.ToMesh("patch " + HexId), null, true);

            // Number post and token.
            var post = LandLook.Cached("token/post", m =>
            {
                LandShapes.Beam(m, Vector3.zero, Vector3.up * (TokenHeight - TokenRadius + 0.1f), 0.09f, 6, LandLook.DarkWood);
                LandShapes.Disc(m, new Vector3(0, 0.02f, 0), 0.5f, 8, LandLook.Stone * 0.8f);
            });
            LandLook.Part(transform, "Post", post);
            _token = new GameObject("Token").transform;
            _token.SetParent(transform, false);
            _token.localPosition = Vector3.up * TokenHeight;
            var disc = LandLook.Cached("token/disc", m =>
            {
                // Faces -Z (towards a camera that the token is turned to look away from); drawn both sides.
                DiscXY(m, 0f, TokenRadius, new Color(0.30f, 0.20f, 0.12f), -0.02f);
                DiscXY(m, 0f, TokenRadius * 0.88f, new Color(0.97f, 0.92f, 0.78f), -0.04f);
                DiscXY(m, 0f, TokenRadius, new Color(0.30f, 0.20f, 0.12f), 0.02f, true);
            });
            _disc = LandLook.Part(_token, "Disc", disc, LandLook.Unlit, false).GetComponent<Renderer>();
            bool hot = Number == 6 || Number == 8;
            var ink = hot ? new Color(0.78f, 0.12f, 0.08f) : new Color(0.12f, 0.09f, 0.07f);
            var num = Signs.Text(_token, Number.ToString(), new Vector3(0, 0.14f, -0.06f), 0.95f, ink, 0f);
            num.name = "Number";
            int pips = EconomyApi.Pips(Number);
            var dots = new MeshBuilder();
            for (int i = 0; i < pips; i++)
            {
                float x = (i - (pips - 1) * 0.5f) * 0.17f;
                DiscXY(dots, 0f, 0.055f, ink, -0.07f, false, new Vector3(x, -0.55f, 0));
            }
            if (pips > 0) LandLook.Part(_token, "Pips", dots.ToMesh("pips " + Number), LandLook.Unlit, false);
            _halo = LandLook.Part(_token, "Halo", LandLook.Cached("token/halo", m => DiscXY(m, 0f, TokenRadius * 1.8f, new Color(1f, 0.75f, 0.25f), 0.05f, true)), LandLook.Additive, false);
            _halo.SetActive(false);
            _stopMark = LandLook.Part(_token, "Stopped", LandLook.Cached("token/stop", m =>
            {
                var c = new Color(0.25f, 0.05f, 0.30f);
                foreach (float a in new[] { 45f, -45f })
                {
                    var q = Quaternion.Euler(0, 0, a);
                    var p0 = q * new Vector3(-TokenRadius, -0.09f, -0.1f); var p1 = q * new Vector3(TokenRadius, -0.09f, -0.1f);
                    var p2 = q * new Vector3(TokenRadius, 0.09f, -0.1f); var p3 = q * new Vector3(-TokenRadius, 0.09f, -0.1f);
                    m.Quad(p0, p3, p2, p1, c); m.Quad(p0, p1, p2, p3, c);
                }
            }), LandLook.Unlit, false);
            _stopMark.SetActive(false);
            LandLook.PickBox(_token.gameObject, Vector3.zero, new Vector3(TokenRadius * 2, TokenRadius * 2, 0.3f));
            LandLook.PickBox(gameObject, Vector3.up * 1.2f, new Vector3(9f, 2.4f, 9f));

            // Roll effects (hidden until a roll pays here).
            var wc = LandLook.WareColor(_ware.Value);
            _beam = LandLook.Part(transform, "PaidBeam", LandLook.Cached("token/beam/" + (int)_ware.Value, m =>
            {
                LandShapes.FadeBeam(m, Vector3.zero, 5.8f, 4.5f, 22f, 16, new Color(wc.r, wc.g, wc.b) * 0.6f);
                LandShapes.FadeBeam(m, Vector3.zero, 2.2f, 1.0f, 34f, 12, new Color(1f, 0.9f, 0.6f) * 0.5f);
            }), LandLook.Additive, false);
            _beam.SetActive(false);
            _ring = LandLook.Part(transform, "PaidRing", LandLook.Cached("token/ring/" + (int)_ware.Value, m =>
                LandShapes.Ring(m, new Vector3(0, 0.15f, 0), 6.2f, 0.7f, 32, new Color(wc.r, wc.g, wc.b))), LandLook.Additive, false);
            _ring.SetActive(false);
            _paidText = Signs.Text(transform, "+4", new Vector3(0, TokenHeight + 1.8f, 0), 1.5f, new Color(1f, 0.85f, 0.15f), 0f);
            _paidText.name = "PaidText";
            _paidText.gameObject.SetActive(false);
        }

        /// <summary>A disc in the XY plane at z, facing -Z (or +Z with back).</summary>
        private static void DiscXY(MeshBuilder m, float _, float r, Color col, float z, bool back = false, Vector3 offset = default)
        {
            int seg = 24;
            var c = offset + new Vector3(0, 0, z);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = c + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0);
                var p1 = c + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0);
                if (back) m.Tri(c, p0, p1, col); else m.Tri(c, p1, p0, col);
            }
        }

        // ---------------- reconcile ----------------

        /// <summary>Redraws the patch when the visible counts changed.</summary>
        public void Sync(in HexStockView v)
        {
            if (!_ware.HasValue) return;
            int cap = Mathf.Max(1, v.Cap > 0 ? v.Cap : 12);
            int m = Mathf.Min(v.Mature, cap);
            int s2 = v.YoungVisible ? Mathf.Min(v.Stage2, cap - m) : 0;
            int s1 = v.YoungVisible ? Mathf.Min(v.Stage1, cap - m - s2) : 0;
            int s0 = v.YoungVisible ? Mathf.Min(v.Stage0, cap - m - s2 - s1) : 0;
            int sig = m | s2 << 5 | s1 << 10 | s0 << 15 | (v.Stopped ? 1 << 20 : 0);
            if (sig == _sig) return;
            _sig = sig;
            ShownMature = m; ShownStage2 = s2; ShownStage1 = s1; ShownStage0 = s0; ShownStopped = v.Stopped;
            var mb = new MeshBuilder();
            int slot = 0;
            for (int i = 0; i < m; i++) Item(mb, Terrain, 3, Slot(slot++), slot);
            for (int i = 0; i < s2; i++) Item(mb, Terrain, 2, Slot(slot++), slot);
            for (int i = 0; i < s1; i++) Item(mb, Terrain, 1, Slot(slot++), slot);
            for (int i = 0; i < s0; i++) Item(mb, Terrain, 0, Slot(slot++), slot);
            LandLook.SetMesh(ref _items, transform, "Items", mb);
            bool mined = TerrainInfo.IsMined(Terrain);
            if (mined)
            {
                if (_ready == null)
                {
                    var wc = LandLook.WareColor(_ware.Value);
                    _ready = LandLook.Part(transform, "ReadyMark", LandLook.Cached("token/ready/" + (int)_ware.Value, mm =>
                    {
                        var p = new Vector3(3.2f, 0, -3.2f);
                        LandShapes.Beam(mm, p, p + Vector3.up * 3.4f, 0.07f, 5, LandLook.DarkWood);
                        mm.Quad(p + new Vector3(0, 3.35f, 0), p + new Vector3(0, 2.55f, 0), p + new Vector3(1.4f, 2.75f, 0), p + new Vector3(1.4f, 3.25f, 0), new Color(1f, 0.85f, 0.2f));
                        mm.Quad(p + new Vector3(0, 3.35f, 0), p + new Vector3(1.4f, 3.25f, 0), p + new Vector3(1.4f, 2.75f, 0), p + new Vector3(0, 2.55f, 0), new Color(1f, 0.85f, 0.2f));
                        // Crossed pick and shovel leaning on the post.
                        LandShapes.Beam(mm, p + new Vector3(-0.6f, 0, 0.3f), p + new Vector3(0.1f, 1.6f, 0.05f), 0.04f, 4, LandLook.Wood);
                        mm.Box(p + new Vector3(0.1f, 1.6f, 0.05f), new Vector3(0.7f, 0.1f, 0.1f), wc * 0.8f + LandLook.Stone * 0.2f);
                        LandShapes.Beam(mm, p + new Vector3(0.6f, 0, 0.3f), p + new Vector3(-0.1f, 1.5f, 0.05f), 0.04f, 4, LandLook.Wood);
                    }), LandLook.Unlit, true);
                }
                _ready.SetActive(m > 0);
            }
            if (_stopMark != null) _stopMark.SetActive(v.Stopped);
        }

        /// <summary>Slot positions: 8 round the outside (4.6 m), then 4 inside (2.5 m), clear of the post.</summary>
        public static Vector3 Slot(int i)
        {
            if (i < 8) { float a = (22.5f + i * 45f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * 4.6f, 0, Mathf.Sin(a) * 4.6f); }
            float b = (i - 8) * 90f * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(b) * 2.4f, 0, Mathf.Sin(b) * 2.4f);
        }

        /// <summary>One unit of stock at a stage (0..2 young, 3 mature).</summary>
        public static void Item(MeshBuilder mb, Terrain terrain, int stage, Vector3 p, int seed)
        {
            var rnd = new PropRandom(seed * 37 + stage);
            switch (terrain)
            {
                case Terrain.Forest:
                {
                    // Broadleaf stock trees (round crowns, lighter), unlike the dark conifers of the wild forest.
                    float[] h = { 0.8f, 1.7f, 2.8f, 4.3f };
                    float s = h[stage];
                    mb.Cylinder(p, 0.05f + s * 0.05f, 0.04f + s * 0.035f, s * 0.45f, 5, new Color(0.45f, 0.30f, 0.17f));
                    var g = (stage == 3 ? new Color(0.36f, 0.62f, 0.16f) : new Color(0.50f, 0.78f, 0.28f)) * rnd.Range(0.94f, 1.06f);
                    float cr = s * (stage == 0 ? 0.22f : 0.3f);
                    var c0 = p + Vector3.up * (s * 0.35f);
                    mb.Cylinder(c0, cr * 0.45f, cr, s * 0.25f, 8, g * 0.9f, false);
                    mb.Cylinder(c0 + Vector3.up * (s * 0.25f), cr, cr * 0.9f, s * 0.2f, 8, g, false);
                    mb.Cylinder(c0 + Vector3.up * (s * 0.45f), cr * 0.9f, cr * 0.25f, s * 0.22f, 8, g * 1.08f, true);
                    if (stage == 3) mb.Rock(p + Vector3.up * (s * 0.98f), s * 0.07f, new Color(0.75f, 0.2f, 0.12f), rnd.Next); // a ripe seed head
                    if (stage == 0) mb.Box(p + Vector3.up * 0.25f, new Vector3(0.05f, 0.5f, 0.05f), LandLook.Wood); // stake
                    break;
                }
                case Terrain.Fields:
                {
                    if (stage == 3) { LandShapes.Sheaf(mb, p, 1.5f, new Color(0.93f, 0.76f, 0.30f)); break; }
                    float[] h = { 0.3f, 0.65f, 1.05f };
                    Color[] c = { new Color(0.40f, 0.70f, 0.25f), new Color(0.42f, 0.66f, 0.22f), new Color(0.72f, 0.70f, 0.28f) };
                    for (int k = 0; k < 5; k++)
                    {
                        var q = p + new Vector3(Mathf.Cos(k * 1.26f) * 0.32f, 0, Mathf.Sin(k * 1.26f) * 0.32f);
                        mb.Cone(q, 0.07f + stage * 0.02f, h[stage] * rnd.Range(0.85f, 1.1f), 4, c[stage]);
                    }
                    if (stage == 2) mb.Cone(p, 0.12f, h[stage] * 1.15f, 4, new Color(0.85f, 0.75f, 0.30f));
                    break;
                }
                case Terrain.Pasture:
                {
                    float[] sc = { 0.42f, 0.58f, 0.76f, 1.05f };
                    Sheep(mb, p, (seed * 73f) % 360f, sc[stage], stage == 3);
                    break;
                }
                case Terrain.Hills: // dug clay pile
                    mb.Cylinder(p, 0.95f, 0.35f, 0.75f, 8, LandLook.ClayRed * rnd.Range(0.92f, 1.08f));
                    mb.Rock(p + new Vector3(0.6f, 0, 0.4f), 0.35f, LandLook.ClayRed * 0.85f, rnd.Next);
                    break;
                case Terrain.Mountain: // dug stone pile
                    mb.Box(p + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 0.7f), LandLook.Stone * rnd.Range(0.95f, 1.1f), seed * 31f);
                    mb.Box(p + new Vector3(0.1f, 0.8f, 0), new Vector3(0.6f, 0.4f, 0.5f), LandLook.Stone * 1.1f, seed * 17f);
                    break;
            }
        }

        public static void Sheep(MeshBuilder mb, Vector3 p, float yaw, float scale, bool adult)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var wool = adult ? new Color(0.96f, 0.95f, 0.90f) : new Color(1f, 1f, 0.97f);
            var dark = new Color(0.16f, 0.14f, 0.13f);
            Vector3 P(float x, float y, float z) => p + q * (new Vector3(x, y, z) * scale);
            mb.Box(P(0, 0.75f, 0), new Vector3(0.8f, 0.65f, 1.25f) * scale, wool, yaw);
            mb.Box(P(0, 0.95f, 0.72f), new Vector3(0.36f, 0.4f, 0.42f) * scale, adult ? dark : new Color(0.85f, 0.8f, 0.75f), yaw);
            foreach (float x in new[] { -0.25f, 0.25f }) foreach (float z in new[] { -0.4f, 0.4f })
                mb.Box(P(x, 0.22f, z), new Vector3(0.12f, 0.44f, 0.12f) * scale, dark, yaw);
        }

        private static void PatchGround(MeshBuilder mb, Terrain terrain, int hex)
        {
            var rnd = new PropRandom(hex * 13 + 5);
            switch (terrain)
            {
                case Terrain.Forest:
                    LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 6.0f, 20, new Color(0.30f, 0.24f, 0.15f));
                    for (int i = 0; i < 6; i++) mb.Cylinder(new Vector3(rnd.Range(-5f, 5f), 0, rnd.Range(-5f, 5f)), 0.3f, 0.28f, 0.25f, 6, LandLook.Wood * 0.9f); // stumps
                    break;
                case Terrain.Fields:
                    LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 6.0f, 20, new Color(0.45f, 0.33f, 0.20f));
                    for (int i = -4; i <= 4; i++)
                    {
                        float half = Mathf.Sqrt(Mathf.Max(0, 36f - (i * 1.25f) * (i * 1.25f))) - 0.3f;
                        if (half > 0.3f) mb.Box(new Vector3(i * 1.25f, 0.06f, 0), new Vector3(0.35f, 0.08f, half * 2), new Color(0.38f, 0.27f, 0.16f));
                    }
                    break;
                case Terrain.Pasture:
                    LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 6.2f, 20, new Color(0.40f, 0.62f, 0.28f));
                    for (int i = 0; i < 18; i++)
                    {
                        float a0 = i * 20f, a1 = (i + 1) * 20f;
                        if (i == 13) continue; // gate
                        var p0 = Quaternion.Euler(0, a0, 0) * Vector3.forward * 6.4f;
                        var p1 = Quaternion.Euler(0, a1, 0) * Vector3.forward * 6.4f;
                        LandShapes.Beam(mb, p0, p0 + Vector3.up * 0.95f, 0.05f, 4, LandLook.DarkWood, false);
                        LandShapes.Beam(mb, p0 + Vector3.up * 0.45f, p1 + Vector3.up * 0.45f, 0.035f, 4, LandLook.Wood, false);
                        LandShapes.Beam(mb, p0 + Vector3.up * 0.85f, p1 + Vector3.up * 0.85f, 0.035f, 4, LandLook.Wood, false);
                    }
                    break;
                case Terrain.Hills:
                    LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 6.0f, 20, new Color(0.52f, 0.32f, 0.20f));
                    LandShapes.Ring(mb, new Vector3(0, 0.05f, 0), 6.0f, 0.6f, 20, LandLook.ClayRed * 0.9f);
                    break;
                case Terrain.Mountain:
                    LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 6.0f, 20, new Color(0.52f, 0.51f, 0.49f));
                    for (int i = 0; i < 14; i++) mb.Rock(new Vector3(rnd.Range(-5.5f, 5.5f), 0, rnd.Range(-5.5f, 5.5f)), rnd.Range(0.12f, 0.25f), LandLook.Stone, rnd.Next);
                    break;
            }
        }

        // ---------------- roll effects ----------------

        public void Flare() { if (_token != null) { LastFlare = Time.time; _halo.SetActive(true); } }

        public void Paid(int count)
        {
            if (_beam == null) return;
            LastPaid = Time.time;
            _beam.SetActive(true); _ring.SetActive(true);
            _paidText.text = "+" + count;
            _paidText.gameObject.SetActive(true);
        }

        /// <summary>Turns the token to the camera and runs the flare (called every frame by LandViews).</summary>
        public void Animate(Vector3 eye, float now)
        {
            if (_token == null) return;
            var d = _token.position - eye; d.y = 0;
            if (d.sqrMagnitude > 0.01f) _token.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            float f = now - LastFlare;
            if (f < FlareSeconds)
            {
                float k = 1f - f / FlareSeconds;
                float pulse = 0.5f + 0.5f * Mathf.Sin(f * 12f);
                _token.localScale = Vector3.one * (1f + 0.45f * k * (0.6f + 0.4f * pulse));
                _mpb.SetColor("_Color", Color.Lerp(Color.white, new Color(1.6f, 1.3f, 0.5f), k * pulse));
                _disc.SetPropertyBlock(_mpb);
            }
            else if (_halo.activeSelf)
            {
                _halo.SetActive(false);
                _token.localScale = Vector3.one;
                _mpb.SetColor("_Color", Color.white);
                _disc.SetPropertyBlock(_mpb);
            }
            float p = now - LastPaid;
            if (p < PaidSeconds)
            {
                float k = p / PaidSeconds;
                _ring.transform.localScale = new Vector3(0.4f + k * 1.4f, 1, 0.4f + k * 1.4f);
                _beam.transform.localScale = new Vector3(1, 1f - k * 0.7f, 1);
                _paidText.transform.localPosition = new Vector3(0, TokenHeight + 1.6f + k * 2.5f, 0);
                var d2 = _paidText.transform.position - eye; d2.y = 0;
                if (d2.sqrMagnitude > 0.01f) _paidText.transform.rotation = Quaternion.LookRotation(d2.normalized, Vector3.up);
            }
            else if (_beam.activeSelf) { _beam.SetActive(false); _ring.SetActive(false); _paidText.gameObject.SetActive(false); }
        }
    }
}
