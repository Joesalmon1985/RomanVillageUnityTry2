using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.War;

namespace World.Game
{
    // ======================================================================== effects

    /// <summary>A column of grey puffs rising (burning ruins, huts at work, a looted camp). Intensity 0..1.</summary>
    public sealed class SmokeColumn : MonoBehaviour
    {
        public float Intensity = 1f, Height = 10f, Size = 1.4f;
        public Color Tone = new Color(0.30f, 0.29f, 0.29f);
        private Transform[] _puffs; private Material _mat; private float _t;
        public int PuffsShown { get; private set; }

        public static SmokeColumn Create(Transform parent, Vector3 local, float height, float size, int puffs = 6, Color? tone = null)
        {
            var go = new GameObject("Smoke"); go.transform.SetParent(parent, false); go.transform.localPosition = local; go.layer = 2;
            var s = go.AddComponent<SmokeColumn>();
            s.Height = height; s.Size = size;
            s._mat = MaterialFactory.CreateUnlit(tone ?? s.Tone, "WarSmoke");
            s._puffs = new Transform[puffs];
            var mesh = LandLook.Cached("war/puff", mb => mb.Cylinder(Vector3.zero, 1f, 0.6f, 1.2f, 7, Color.white));
            for (int i = 0; i < puffs; i++)
            {
                var p = LandLook.Part(go.transform, "Puff", mesh, s._mat, false); p.layer = 2; p.SetActive(false);
                s._puffs[i] = p.transform;
            }
            return s;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            int shown = 0;
            int n = _puffs.Length;
            for (int i = 0; i < n; i++)
            {
                var t = _puffs[i];
                if (Intensity <= 0.01f || i >= Mathf.CeilToInt(n * Intensity)) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                float u = Mathf.Repeat(_t * 0.2f + i / (float)n, 1f);
                float s = Mathf.Sin(u * Mathf.PI);
                t.gameObject.SetActive(s > 0.03f);
                if (s > 0.03f) shown++;
                t.localPosition = new Vector3(Mathf.Sin(i * 2.1f + u * 2f) * 0.6f * Size + u * 1.5f, 0.5f + u * Height, Mathf.Cos(i * 1.7f) * 0.5f * Size);
                t.localScale = Vector3.one * Size * (0.5f + 1.6f * s);
            }
            PuffsShown = shown;
        }

        private void OnDestroy() { if (_mat != null) Destroy(_mat); }
    }

    /// <summary>Flickering flames (unlit cones). Intensity 0..1 scales them; 0 hides them.</summary>
    public sealed class FlameFx : MonoBehaviour
    {
        public float Intensity = 1f, Radius = 1f, Height = 2f;
        private Transform[] _f; private float _t;
        public bool Burning => Intensity > 0.02f;

        public static FlameFx Create(Transform parent, Vector3 local, float radius, float height, int count = 5)
        {
            var go = new GameObject("Flames"); go.transform.SetParent(parent, false); go.transform.localPosition = local; go.layer = 2;
            var f = go.AddComponent<FlameFx>(); f.Radius = radius; f.Height = height;
            var mesh = LandLook.Cached("war/flames", mb => LandShapes.Flames(mb, Vector3.zero, 1f, 1f));
            f._f = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var p = LandLook.Part(go.transform, "Flame", mesh, LandLook.Unlit, false); p.layer = 2;
                float a = i * 2.4f;
                p.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * 0.7f;
                f._f[i] = p.transform;
            }
            return f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            for (int i = 0; i < _f.Length; i++)
            {
                bool on = Intensity > 0.02f;
                if (_f[i].gameObject.activeSelf != on) _f[i].gameObject.SetActive(on);
                if (!on) continue;
                float fl = 0.75f + 0.25f * Mathf.Sin(_t * (9f + i * 2.3f) + i) + 0.1f * Mathf.Sin(_t * 23f + i * 5f);
                float k = Intensity * (i == 0 ? 1f : 0.7f);
                _f[i].localScale = new Vector3(Radius * (0.7f + 0.3f * k), Height * k * fl, Radius * (0.7f + 0.3f * k));
            }
        }
    }

    /// <summary>A pulse ring that grows and fades (a soldier trained, a good made). Unlit glow.</summary>
    public sealed class PulseRing : MonoBehaviour
    {
        private float _t, _dur, _r; private Material _mat; private Color _c;
        public static PulseRing Spawn(Transform parent, Vector3 at, Color c, float radius, float dur = 1.2f)
        {
            var go = new GameObject("PulseRing"); go.transform.SetParent(parent, false); go.transform.position = at + Vector3.up * 0.3f; go.layer = 2;
            var p = go.AddComponent<PulseRing>(); p._dur = dur; p._r = radius; p._c = c;
            p._mat = MaterialFactory.CreateGlow(c, "PulseRing");
            LandLook.Part(go.transform, "Ring", LandLook.Cached("war/ring", mb => LandShapes.Ring(mb, Vector3.zero, 1f, 0.12f, 28, Color.white)), p._mat, false);
            LandLook.Part(go.transform, "Beam", LandLook.Cached("war/ringbeam", mb => LandShapes.FadeBeam(mb, Vector3.zero, 0.7f, 0.1f, 7f, 8, Color.white)), p._mat, false);
            return p;
        }
        private void Update()
        {
            _t += Time.deltaTime; float u = Mathf.Clamp01(_t / _dur);
            transform.localScale = new Vector3(0.4f + u * _r, 1f, 0.4f + u * _r);
            _mat.color = new Color(_c.r, _c.g, _c.b, 1f) * (1f - u);
            if (_t >= _dur) Destroy(gameObject);
        }
        private void OnDestroy() { if (_mat != null) Destroy(_mat); }
    }

    // ======================================================================== the processing hut

    /// <summary>The ten processed goods as small props beside a hut, and their colours for goods piles (PRD section 5).</summary>
    public static class HutProps
    {
        public static readonly Color[] GoodColour =
        {
            new Color(0.35f, 0.24f, 0.14f), new Color(0.58f, 0.56f, 0.52f), new Color(0.50f, 0.52f, 0.56f), new Color(0.66f, 0.50f, 0.32f), new Color(0.70f, 0.42f, 0.28f),
            new Color(0.85f, 0.70f, 0.42f), new Color(0.95f, 0.82f, 0.45f), new Color(0.82f, 0.62f, 0.30f), new Color(0.85f, 0.45f, 0.12f), new Color(0.50f, 0.42f, 0.28f),
        };

        public static void Build(MeshBuilder mb, int good)
        {
            var wood = LandLook.Wood; var dark = LandLook.DarkWood; var c = GoodColour[Mathf.Clamp(good, 0, 9)];
            switch (good)
            {
                case 0: // fire-hardened spears leaning on a tripod
                    for (int i = 0; i < 3; i++) { float a = i * 2.1f; LandShapes.Beam(mb, new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f), new Vector3(Mathf.Cos(a) * 0.1f, 2.0f, Mathf.Sin(a) * 0.1f), 0.04f, 4, dark); }
                    mb.Cone(new Vector3(0, 2.0f, 0), 0.07f, 0.35f, 4, new Color(0.2f, 0.12f, 0.08f));
                    mb.Box(new Vector3(0.7f, 0.1f, 0), new Vector3(0.5f, 0.1f, 0.3f), new Color(0.3f, 0.12f, 0.05f)); // coals
                    break;
                case 1: // hafted axe in a stump
                    mb.Cylinder(Vector3.zero, 0.5f, 0.45f, 0.7f, 8, wood);
                    LandShapes.Beam(mb, new Vector3(0, 0.7f, 0), new Vector3(0.1f, 1.7f, 0), 0.05f, 4, dark);
                    mb.Box(new Vector3(0.2f, 1.6f, 0), new Vector3(0.1f, 0.36f, 0.5f), c);
                    break;
                case 2: // flint knives on a hide board
                    mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.1f, 0.8f), LandLook.Hide);
                    mb.Box(new Vector3(-0.5f, 0.2f, 0), new Vector3(0.1f, 0.4f, 0.1f), dark); mb.Box(new Vector3(0.5f, 0.2f, 0), new Vector3(0.1f, 0.4f, 0.1f), dark);
                    mb.Box(new Vector3(-0.2f, 0.55f, 0), new Vector3(0.5f, 0.04f, 0.1f), c, 20f); mb.Box(new Vector3(0.3f, 0.55f, 0.1f), new Vector3(0.4f, 0.04f, 0.1f), c, -15f);
                    mb.Cylinder(new Vector3(0.45f, 0.5f, -0.2f), 0.12f, 0.12f, 0.08f, 6, new Color(0.8f, 0.75f, 0.6f));
                    break;
                case 3: // hide shield on a frame
                    mb.Box(new Vector3(-0.55f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark); mb.Box(new Vector3(0.55f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark);
                    mb.Box(new Vector3(0, 1.75f, 0), new Vector3(1.2f, 0.08f, 0.08f), dark);
                    mb.Box(new Vector3(0, 1.0f, 0.05f), new Vector3(0.8f, 0.8f, 0.07f), c); mb.Box(new Vector3(0, 1.0f, 0.05f), new Vector3(0.8f, 0.8f, 0.08f), c * 0.9f, 45f);
                    mb.Box(new Vector3(0, 1.0f, 0.1f), new Vector3(0.18f, 0.18f, 0.06f), LandLook.Stone);
                    break;
                case 4: // clay-plate vest on a stand
                    mb.Box(new Vector3(0, 0.9f, 0), new Vector3(0.1f, 1.8f, 0.1f), dark); mb.Box(new Vector3(0, 1.55f, 0), new Vector3(1.0f, 0.08f, 0.1f), dark);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.32f + i * 0.32f, 1.1f, 0.08f), new Vector3(0.28f, 0.7f, 0.06f), c * (0.9f + 0.05f * i));
                    break;
                case 5: // hearth cakes on a warm slab
                    mb.Box(new Vector3(0, 0.12f, 0), new Vector3(1.3f, 0.24f, 1.0f), LandLook.Stone);
                    for (int i = 0; i < 3; i++) mb.Cylinder(new Vector3(-0.35f + i * 0.35f, 0.24f, (i % 2) * 0.2f - 0.1f), 0.22f, 0.2f, 0.1f, 8, c);
                    break;
                case 6: // cheese wheel and dried meat on a rack
                    mb.Cylinder(new Vector3(-0.5f, 0, 0), 0.4f, 0.4f, 0.28f, 10, c);
                    mb.Box(new Vector3(0.3f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark); mb.Box(new Vector3(1.0f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark);
                    mb.Box(new Vector3(0.65f, 1.75f, 0), new Vector3(0.8f, 0.06f, 0.06f), dark);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(0.4f + i * 0.25f, 1.4f, 0), new Vector3(0.1f, 0.6f, 0.06f), new Color(0.45f, 0.18f, 0.14f));
                    break;
                case 7: // millstone and loaves
                    mb.Cylinder(Vector3.zero, 0.7f, 0.7f, 0.25f, 12, LandLook.Stone);
                    mb.Cylinder(new Vector3(0, 0.25f, 0), 0.55f, 0.55f, 0.2f, 12, LandLook.Stone * 0.9f);
                    mb.Box(new Vector3(0.95f, 0.15f, 0.2f), new Vector3(0.5f, 0.28f, 0.3f), c); mb.Box(new Vector3(0.95f, 0.15f, -0.2f), new Vector3(0.5f, 0.28f, 0.3f), c * 0.92f);
                    break;
                case 8: // ochre pot and tallow
                    mb.Cylinder(Vector3.zero, 0.4f, 0.35f, 0.5f, 8, LandLook.ClayRed);
                    mb.Cylinder(new Vector3(0, 0.48f, 0), 0.34f, 0.34f, 0.04f, 8, c);
                    mb.Cylinder(new Vector3(0.7f, 0, 0.1f), 0.2f, 0.2f, 0.25f, 7, new Color(0.95f, 0.92f, 0.8f));
                    mb.Box(new Vector3(-0.7f, 0.05f, 0), new Vector3(0.4f, 0.1f, 0.3f), c, 20f);
                    break;
                default: // straw-and-mud cloaks on a peg bar
                    mb.Box(new Vector3(-0.6f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark); mb.Box(new Vector3(0.6f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), dark);
                    mb.Box(new Vector3(0, 1.75f, 0), new Vector3(1.3f, 0.08f, 0.08f), dark);
                    mb.Box(new Vector3(-0.3f, 1.1f, 0.06f), new Vector3(0.45f, 1.2f, 0.06f), c); mb.Box(new Vector3(0.3f, 1.15f, 0.06f), new Vector3(0.45f, 1.1f, 0.06f), c * 0.85f);
                    break;
            }
        }

        public static void Pile(MeshBuilder mb, int good, int count)
        {
            var c = GoodColour[Mathf.Clamp(good, 0, 9)];
            for (int i = 0; i < Mathf.Min(4, count); i++)
            {
                int row = i / 2, col = i % 2;
                mb.Box(new Vector3(-0.3f + col * 0.62f + row * 0.15f, 0.2f + row * 0.4f, 0), new Vector3(0.55f, 0.38f, 0.5f), c * (0.92f + 0.04f * i), row * 12f);
                mb.Box(new Vector3(-0.3f + col * 0.62f + row * 0.15f, 0.4f + row * 0.4f, 0), new Vector3(0.58f, 0.05f, 0.1f), Color.Lerp(c, Color.black, 0.5f));
            }
        }
    }

    /// <summary>
    /// One processing hut (PR-1, PR-2): a round hut with the prop of the good it makes, scaffold poles while it is built, a
    /// chimney that smokes while both inputs are in store, and the small pile of its goods in the store.
    /// </summary>
    public sealed class HutView : MonoBehaviour
    {
        public int Good { get; private set; }
        public bool Built { get; private set; }
        public bool Working => _smoke != null && _smoke.Intensity > 0.01f;
        public int PileShown { get; private set; }
        public bool PropShown => _prop != null && _prop.activeSelf;
        public float BuildScale => _walls != null ? _walls.transform.localScale.y : 0f;
        private GameObject _walls, _scaffold, _prop, _pile; private SmokeColumn _smoke; private float _pop;

        public static HutView Create(Transform parent, Vector3 at, float yaw, Color people, int good)
        {
            var go = new GameObject("Hut " + WarData.GoodNames[good]); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0)); go.layer = 2;
            var h = go.AddComponent<HutView>(); h.Good = good;
            var wall = LandLook.Hide; var roof = LandLook.Thatch;
            h._walls = new GameObject("Walls"); h._walls.transform.SetParent(go.transform, false);
            LandLook.Part(h._walls.transform, "Mesh", LandLook.Cached("war/hut/" + ColorUtility.ToHtmlStringRGB(people), mb =>
            {
                LandShapes.Hut(mb, Vector3.zero, 2.1f, 1.6f, 1.7f, Color.Lerp(wall, people, 0.25f), roof);
                mb.Box(new Vector3(0, 0.8f, 2.05f), new Vector3(0.7f, 1.4f, 0.12f), LandLook.DarkWood);
                mb.Cylinder(new Vector3(1.0f, 2.3f, -0.6f), 0.3f, 0.25f, 1.0f, 6, LandLook.Stone); // chimney
                mb.Box(new Vector3(-1.9f, 1.9f, 0), new Vector3(0.08f, 1.3f, 0.08f), LandLook.DarkWood);
                mb.Box(new Vector3(-1.5f, 2.35f, 0), new Vector3(0.8f, 0.45f, 0.04f), people); // pennant
            }), null, true);
            h._scaffold = LandLook.Part(go.transform, "Scaffold", LandLook.Cached("war/hut/scaffold", mb =>
            {
                for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; LandShapes.Beam(mb, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.3f, new Vector3(Mathf.Cos(a), 1.9f, Mathf.Sin(a)) * 2.0f, 0.06f, 4, LandLook.DarkWood); }
                mb.Box(new Vector3(0, 1.2f, 0), new Vector3(4.2f, 0.06f, 0.06f), LandLook.DarkWood); mb.Box(new Vector3(0, 1.2f, 0), new Vector3(0.06f, 0.06f, 4.2f), LandLook.DarkWood);
            }), null, false);
            h._prop = LandLook.Part(go.transform, "Prop", LandLook.Cached("war/prop/" + good, mb => HutProps.Build(mb, good)), null, true);
            h._prop.transform.localPosition = new Vector3(2.9f, 0, 1.4f); h._prop.transform.localScale = Vector3.one * 1.4f;
            h._prop.transform.localRotation = Quaternion.Euler(0, -25f, 0);
            h._pile = new GameObject("Pile"); h._pile.transform.SetParent(go.transform, false); h._pile.transform.localPosition = new Vector3(-2.2f, 0, 2.9f);
            h._smoke = SmokeColumn.Create(go.transform, new Vector3(1.0f, 3.3f, -0.6f), 6f, 0.45f, 4, new Color(0.78f, 0.78f, 0.78f));
            h._smoke.Intensity = 0f;
            h.Apply(0, 0, 0, 0);
            return h;
        }

        private int _pileSig = -1;

        /// <summary>buildLeft ticks to go (0 = built); processing means both inputs are in store; goodsInStore of this good.</summary>
        public void Apply(int buildLeft, int cycleLeft, int working, int goodsInStore)
        {
            Built = buildLeft <= 0;
            float p = Built ? 1f : Mathf.Clamp01(1f - buildLeft / (float)WarData.HutBuildTicks);
            _walls.transform.localScale = new Vector3(1f, Mathf.Max(0.05f, p), 1f);
            _scaffold.SetActive(!Built);
            _prop.SetActive(Built);
            _smoke.Intensity = Built && working > 0 ? 1f : 0f;
            int sig = Built ? Mathf.Min(4, goodsInStore) : 0;
            if (sig != _pileSig)
            {
                _pileSig = sig; PileShown = sig;
                for (int i = _pile.transform.childCount - 1; i >= 0; i--) Destroy(_pile.transform.GetChild(i).gameObject);
                if (sig > 0) { var good = Good; var n = sig; LandLook.Part(_pile.transform, "Goods", LandLook.Cached("war/pile/" + good + "/" + n, mb => HutProps.Pile(mb, good, n)), null, true); }
            }
        }

        /// <summary>A good was made: the prop hops.</summary>
        public void Pop() { _pop = 1f; }

        private void Update()
        {
            if (_pop > 0f)
            {
                _pop = Mathf.Max(0f, _pop - Time.deltaTime * 2.2f);
                float s = 1f + 0.35f * Mathf.Sin(_pop * Mathf.PI);
                _prop.transform.localScale = Vector3.one * (1.4f * s);
                _prop.transform.localPosition = new Vector3(2.9f, Mathf.Sin(_pop * Mathf.PI) * 0.5f, 1.4f);
            }
        }
    }

    // ======================================================================== the lodge

    /// <summary>A people's war lodge (WA-1): a long house with the people's roof, a banner, a weapon rack, a training post and a fire pit.</summary>
    public sealed class LodgeView : MonoBehaviour
    {
        public int People { get; private set; }
        public bool Training => _fire != null && _fire.Intensity > 0.02f;
        public int QueueShown { get; private set; }
        public int Celebrations { get; private set; }
        private FlameFx _fire; private GameObject _queue, _banner; private int _qSig = -1; private float _flag;

        public static LodgeView Create(Transform parent, Vector3 at, float yaw, Color people, int person)
        {
            var go = new GameObject("War lodge " + person); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0)); go.layer = 2;
            var l = go.AddComponent<LodgeView>(); l.People = person;
            LandLook.Part(go.transform, "House", LandLook.Cached("war/lodge/" + ColorUtility.ToHtmlStringRGB(people), mb =>
            {
                var wood = LandLook.Wood; var dark = LandLook.DarkWood;
                mb.Box(new Vector3(0, 0.15f, 0), new Vector3(9.4f, 0.3f, 5.6f), new Color(0.40f, 0.34f, 0.26f));
                mb.Box(new Vector3(0, 1.4f, 0), new Vector3(8.4f, 2.4f, 4.4f), Color.Lerp(LandLook.Hide, people, 0.2f));
                for (int i = 0; i < 5; i++) mb.Box(new Vector3(-3.6f + i * 1.8f, 1.5f, 2.25f), new Vector3(0.3f, 2.7f, 0.3f), dark);
                // gabled roof in the people's colour (two slopes, both faces, and the gable ends)
                float x0 = -4.9f, x1 = 4.9f, ze = 3.1f, y0 = 3.3f, yr = 5.2f;
                var rc = people; var rd = people * 0.72f;
                mb.Quad(new Vector3(x0, y0, -ze), new Vector3(x1, y0, -ze), new Vector3(x1, yr, 0), new Vector3(x0, yr, 0), rd);
                mb.Quad(new Vector3(x0, yr, 0), new Vector3(x1, yr, 0), new Vector3(x1, y0, -ze), new Vector3(x0, y0, -ze), rd * 0.8f);
                mb.Quad(new Vector3(x0, yr, 0), new Vector3(x1, yr, 0), new Vector3(x1, y0, ze), new Vector3(x0, y0, ze), rc);
                mb.Quad(new Vector3(x0, y0, ze), new Vector3(x1, y0, ze), new Vector3(x1, yr, 0), new Vector3(x0, yr, 0), rc * 0.8f);
                mb.Tri(new Vector3(x0, y0, -ze), new Vector3(x0, yr, 0), new Vector3(x0, y0, ze), rd); mb.Tri(new Vector3(x1, y0, ze), new Vector3(x1, yr, 0), new Vector3(x1, y0, -ze), rd);
                mb.Box(new Vector3(0, yr + 0.1f, 0), new Vector3(9.9f, 0.25f, 0.35f), dark);
                for (int i = 0; i < 4; i++) mb.Box(new Vector3(-3.6f + i * 2.4f, 4.3f, ze - 0.8f), new Vector3(0.12f, 0.12f, 1.8f), Color.Lerp(people, Color.white, 0.5f), 0f); // roof ribbons
                // door and skulls over it
                mb.Box(new Vector3(0, 1.0f, 2.3f), new Vector3(1.4f, 2.0f, 0.15f), dark);
                for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.7f + i * 0.7f, 2.65f, 2.3f), new Vector3(0.3f, 0.3f, 0.15f), new Color(0.88f, 0.84f, 0.72f));
                // weapon rack beside the door
                mb.Box(new Vector3(3.2f, 0.55f, 3.2f), new Vector3(2.2f, 0.1f, 0.15f), dark); mb.Box(new Vector3(3.2f, 1.2f, 3.2f), new Vector3(2.2f, 0.1f, 0.15f), dark);
                for (int i = 0; i < 4; i++) { LandShapes.Beam(mb, new Vector3(2.4f + i * 0.5f, 0.2f, 3.1f), new Vector3(2.4f + i * 0.5f, 2.1f, 3.1f), 0.04f, 4, wood); mb.Cone(new Vector3(2.4f + i * 0.5f, 2.1f, 3.1f), 0.07f, 0.3f, 4, LandLook.Stone); }
                // training post: a stake with a straw dummy and a dirt ring
                LandShapes.Disc(mb, new Vector3(-4.0f, 0.05f, 4.8f), 2.4f, 14, LandLook.Earth * 0.8f);
                LandShapes.Beam(mb, new Vector3(-4.0f, 0f, 4.8f), new Vector3(-4.0f, 2.0f, 4.8f), 0.09f, 5, dark);
                mb.Box(new Vector3(-4.0f, 1.5f, 4.8f), new Vector3(0.9f, 0.18f, 0.18f), wood);
                mb.Box(new Vector3(-4.0f, 1.65f, 4.8f), new Vector3(0.45f, 0.7f, 0.3f), LandLook.Thatch);
                // palisade behind
                for (int i = 0; i < 14; i++) { float a = Mathf.Lerp(-1.0f, 1.0f, i / 13f); var p = new Vector3(Mathf.Sin(a) * 6.8f, 0, -2.2f - Mathf.Cos(a) * 1.6f); LandShapes.Beam(mb, p, p + Vector3.up * 2.4f, 0.14f, 5, dark); mb.Cone(p + Vector3.up * 2.4f, 0.14f, 0.35f, 5, dark); }
                mb.Cylinder(new Vector3(3.6f, 0.3f, -3.6f), 0.5f, 0.5f, 0.3f, 8, LandLook.Stone);
            }), null, true);
            // Tall banner pole with a flag that waves harder when a soldier is trained.
            var pole = new GameObject("Banner"); pole.transform.SetParent(go.transform, false); pole.transform.localPosition = new Vector3(-4.2f, 0, 2.6f);
            LandLook.Part(pole.transform, "Pole", LandLook.Cached("war/lodge/pole", mb => LandShapes.Beam(mb, Vector3.zero, Vector3.up * 9f, 0.1f, 6, LandLook.DarkWood)), null, true);
            l._banner = LandLook.Part(pole.transform, "Flag", LandLook.Cached("war/lodge/flag/" + ColorUtility.ToHtmlStringRGB(people), mb =>
            {
                mb.Quad(new Vector3(0, 8.8f, 0), new Vector3(2.6f, 8.4f, 0), new Vector3(2.6f, 7.2f, 0), new Vector3(0, 7.2f, 0), people);
                mb.Quad(new Vector3(0, 8.8f, 0), new Vector3(0, 7.2f, 0), new Vector3(2.6f, 7.2f, 0), new Vector3(2.6f, 8.4f, 0), people * 0.85f);
                mb.Box(new Vector3(1.0f, 8.0f, 0.02f), new Vector3(0.7f, 0.7f, 0.04f), Color.Lerp(people, Color.white, 0.7f), 45f);
            }), null, false);
            l._fire = FlameFx.Create(go.transform, new Vector3(3.6f, 0.6f, -3.6f), 0.35f, 0.8f, 3);
            l._fire.Intensity = 0f;
            l._queue = new GameObject("Queue"); l._queue.transform.SetParent(go.transform, false); l._queue.transform.localPosition = new Vector3(0.0f, 0, 3.8f);
            return l;
        }

        public void Apply(Lodge lodge)
        {
            if (lodge == null) return;
            _fire.Intensity = lodge.Training != null && lodge.Training.Count > 0 && lodge.TrainLeft > 0 ? 1f : 0f;
            int sig = Mathf.Min(6, lodge.Queue.Count);
            if (sig != _qSig)
            {
                _qSig = sig; QueueShown = sig;
                for (int i = _queue.transform.childCount - 1; i >= 0; i--) Destroy(_queue.transform.GetChild(i).gameObject);
                if (sig > 0)
                {
                    var goods = new List<int>(lodge.Queue.GetRange(0, sig));
                    LandLook.Part(_queue.transform, "Goods", LandLook.Cached("war/queue/" + string.Join(",", goods), mb =>
                    {
                        for (int i = 0; i < goods.Count; i++) mb.Box(new Vector3(-1.4f + i * 0.55f, 0.2f, 0), new Vector3(0.42f, 0.36f, 0.4f), HutProps.GoodColour[goods[i]]);
                    }), null, true);
                }
            }
        }

        /// <summary>The "soldier trained" moment: a ring, the flag waves, the fire flares.</summary>
        public void Celebrate(Color col)
        {
            Celebrations++; _flag = 2.5f;
            PulseRing.Spawn(transform.parent, transform.position + transform.TransformDirection(new Vector3(0, 0, 4.5f)), Color.Lerp(col, Color.white, 0.4f), 10f, 1.6f);
        }

        private void Update()
        {
            _flag = Mathf.Max(0f, _flag - Time.deltaTime);
            float w = 3f + _flag * 5f;
            if (_banner != null) _banner.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * (2f + _flag * 3f)) * w, 0);
        }
    }

    // ======================================================================== a settlement corner

    public enum SiteState { Standing, Burning, Ruin }

    /// <summary>
    /// Everything on the ground at a war settlement's corner: the dressing of a settlement (when no other view draws one), its
    /// processing huts, the lodge at a home camp, and what happens when it burns (fire, collapse, ruin, smoke) or is looted.
    /// </summary>
    public sealed class SiteView : MonoBehaviour
    {
        public const float BurnSeconds = 7f, FireSeconds = 40f, LootSeconds = 150f;

        public int Corner { get; private set; }
        public int People { get; private set; }
        public bool IsHome { get; private set; }
        public bool IsCity { get; private set; }
        public SiteState State { get; private set; } = SiteState.Standing;
        public Color Colour { get; private set; }
        public LodgeView Lodge { get; private set; }
        public readonly Dictionary<int, HutView> Huts = new Dictionary<int, HutView>();
        public bool HasRuinModel => _ruin != null && _ruin.activeSelf;
        public bool HasFire => _fire != null && _fire.Burning;
        public bool Looted => _lootT > 0f;
        public float BurnTime { get; private set; }
        public bool OwnsDressing => _dressing != null;
        public int HutCount => Huts.Count;
        public SmokeColumn Smoke => _smoke;

        private GameObject _dressing, _ruin, _loot;
        private FlameFx _fire; private SmokeColumn _smoke;
        private float _lootT;
        private readonly List<Vector3> _placed = new List<Vector3>();
        private WarViews _owner;

        public static SiteView Create(WarViews owner, Transform parent, Vector3 corner, int cornerId, int people, Color col, bool home, bool city, bool wantDressing)
        {
            var go = new GameObject("Site " + cornerId); go.transform.SetParent(parent, false); go.transform.position = corner; go.layer = 2;
            var s = go.AddComponent<SiteView>();
            s._owner = owner; s.Corner = cornerId; s.People = people; s.Colour = col; s.IsHome = home; s.IsCity = city;
            if (wantDressing) s.BuildDressing();
            s._fire = FlameFx.Create(go.transform, Vector3.up * 0.2f, 1.6f, 4.2f, 6); s._fire.Intensity = 0f;
            s._smoke = SmokeColumn.Create(go.transform, Vector3.up * 1.5f, 16f, 2.2f, 8); s._smoke.Intensity = 0f;
            s._ruin = BuildRuin(go.transform, col); s._ruin.SetActive(false);
            s._loot = BuildLoot(go.transform, col, home); s._loot.SetActive(false);
            return s;
        }

        private void BuildDressing()
        {
            var col = Colour; bool home = IsHome, city = IsCity;
            _dressing = LandLook.Part(transform, "Dressing", LandLook.Cached("war/site/" + (home ? "h" : city ? "c" : "s") + ColorUtility.ToHtmlStringRGB(col), mb =>
            {
                var hide = LandLook.Hide; var dark = LandLook.DarkWood;
                if (home)
                {
                    for (int i = 0; i < 3; i++) { float a = i * 2.1f + 0.6f; LandShapes.Tent(mb, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5.5f, 2.2f, 2.8f, Color.Lerp(hide, col, 0.5f)); }
                    mb.Cylinder(Vector3.zero, 1.2f, 1.1f, 0.35f, 9, new Color(0.35f, 0.32f, 0.3f));
                    mb.Cylinder(Vector3.up * 0.3f, 0.7f, 0f, 0.6f, 6, new Color(1f, 0.55f, 0.15f));
                }
                else
                {
                    int n = city ? 5 : 3;
                    for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n + 0.4f; LandShapes.Hut(mb, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (city ? 6.4f : 4.6f), city ? 2.4f : 2.0f, 1.6f, 1.7f, Color.Lerp(hide, col, 0.45f), LandLook.Thatch); }
                    if (city) { mb.Box(new Vector3(0, 3.0f, 0), new Vector3(2.2f, 6f, 2.2f), Color.Lerp(LandLook.Stone, col, 0.2f)); mb.Cone(new Vector3(0, 6f, 0), 1.9f, 2.4f, 4, col * 0.7f); }
                }
                var pole = new Vector3(home ? 2.2f : 0.6f, 0, home ? -2.4f : 0.6f);
                LandShapes.Beam(mb, pole, pole + Vector3.up * 6f, 0.09f, 5, dark);
                mb.Quad(pole + Vector3.up * 5.9f, pole + new Vector3(1.8f, 5.9f, 0), pole + new Vector3(1.8f, 4.7f, 0), pole + Vector3.up * 4.7f, col);
                mb.Quad(pole + Vector3.up * 5.9f, pole + Vector3.up * 4.7f, pole + new Vector3(1.8f, 4.7f, 0), pole + new Vector3(1.8f, 5.9f, 0), col * 0.85f);
            }), null, true);
        }

        private static GameObject BuildRuin(Transform parent, Color col)
        {
            return LandLook.Part(parent, "Ruin", LandLook.Cached("war/ruin", mb =>
            {
                var ash = new Color(0.12f, 0.10f, 0.09f); var char1 = new Color(0.08f, 0.06f, 0.05f);
                LandShapes.Disc(mb, Vector3.up * 0.06f, 7.2f, 16, ash);
                LandShapes.Disc(mb, Vector3.up * 0.09f, 4.0f, 12, new Color(0.2f, 0.17f, 0.15f));
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f + 0.5f; float r = 2.2f + (i % 4) * 1.1f; float h = 0.7f + (i % 3) * 0.7f;
                    var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    mb.Box(p + Vector3.up * h * 0.5f, new Vector3(0.28f, h, 0.28f), char1, i * 33f);
                }
                for (int i = 0; i < 5; i++) { float a = i * 1.3f; mb.Box(new Vector3(Mathf.Cos(a) * 3f, 0.22f, Mathf.Sin(a) * 3f), new Vector3(3.2f, 0.28f, 0.3f), char1 * 1.3f, i * 41f); }
                for (int i = 0; i < 4; i++) { float a = i * 1.7f + 0.3f; mb.Box(new Vector3(Mathf.Cos(a) * 5.4f, 0.2f, Mathf.Sin(a) * 5.4f), new Vector3(1.4f, 0.4f, 0.9f), new Color(0.3f, 0.28f, 0.26f), i * 50f); } // fallen stones
                mb.Box(new Vector3(0.4f, 0.25f, -0.2f), new Vector3(1.6f, 0.1f, 1.0f), Color.Lerp(col, Color.black, 0.7f), 18f); // scrap of the people's cloth
            }), null, true);
        }

        private static GameObject BuildLoot(Transform parent, Color col, bool home)
        {
            return LandLook.Part(parent, "Looted", LandLook.Cached("war/loot", mb =>
            {
                var scorch = new Color(0.1f, 0.08f, 0.07f);
                LandShapes.Disc(mb, new Vector3(3.5f, 0.07f, 2.0f), 1.6f, 10, scorch);
                LandShapes.Disc(mb, new Vector3(-3.0f, 0.07f, -2.5f), 1.2f, 10, scorch);
                // overturned baskets and spilled sacks
                mb.Box(new Vector3(-2.0f, 0.3f, 2.8f), new Vector3(1.1f, 0.6f, 0.9f), new Color(0.55f, 0.42f, 0.24f), 25f);
                mb.Box(new Vector3(-1.4f, 0.1f, 3.6f), new Vector3(0.9f, 0.2f, 0.5f), LandLook.Thatch, -30f);
                mb.Box(new Vector3(2.6f, 0.15f, -3.2f), new Vector3(1.3f, 0.3f, 0.7f), LandLook.Hide, 70f);
                mb.Box(new Vector3(1.0f, 0.1f, 4.0f), new Vector3(0.5f, 0.2f, 0.5f), new Color(0.85f, 0.70f, 0.42f), 10f);
                mb.Box(new Vector3(0.3f, 0.1f, 3.4f), new Vector3(0.5f, 0.2f, 0.4f), new Color(0.58f, 0.56f, 0.52f), 40f);
                // a toppled, torn banner
                mb.Box(new Vector3(-3.6f, 0.15f, 0.6f), new Vector3(5.0f, 0.12f, 0.12f), LandLook.DarkWood, 12f);
                mb.Quad(new Vector3(-6f, 0.2f, 0.9f), new Vector3(-4.0f, 0.2f, 1.2f), new Vector3(-4.2f, 0.2f, 2.3f), new Vector3(-5.8f, 0.2f, 2.0f), col * 0.8f);
            }), null, true);
        }

        // ------------------------------------------------------------ driven by WarViews

        public void SyncStanding(WorldState s, WarSettlement st, Lodge lodge, Transform root)
        {
            if (State != SiteState.Standing) return;
            IsCity = st.IsCity;
            // huts by good
            for (int i = 0; i < st.Huts.Count; i++)
            {
                var h = st.Huts[i];
                if (!Huts.TryGetValue(h.Good, out var hv))
                {
                    if (!_owner.PlaceNear(this, 2.9f, i, out var at, out float yaw)) continue;
                    Huts[h.Good] = hv = HutView.Create(root, at, yaw, Colour, h.Good);
                    var hh = hv;
                }
                int a = WarData.GoodRawA[h.Good], b = WarData.GoodRawB[h.Good];
                int working = st.Raw[a] > 0 && st.Raw[b] > 0 ? 1 : 0;
                hv.Apply(h.BuildLeft, h.CycleLeft, working, st.Proc[h.Good]);
            }
            if (lodge != null)
            {
                if (Lodge == null && _owner.PlaceNear(this, 6.5f, 7, out var at, out float yaw, 15f))
                    Lodge = LodgeView.Create(root, at, yaw, Colour, People);
                if (Lodge != null) Lodge.Apply(lodge);
            }
        }

        public void BeginBurn(bool animate)
        {
            if (State == SiteState.Burning && animate) return;
            State = animate ? SiteState.Burning : SiteState.Ruin;
            BurnTime = animate ? 0f : FireSeconds;
            foreach (var h in Huts.Values) if (h != null) Destroy(h.gameObject);
            Huts.Clear();
            if (Lodge != null) { Destroy(Lodge.gameObject); Lodge = null; }
            if (!animate) { if (_dressing != null) _dressing.SetActive(false); _ruin.SetActive(true); _smoke.Intensity = 0.45f; }
        }

        public void Loot() { _lootT = LootSeconds; }

        private void Update()
        {
            if (_lootT > 0f)
            {
                _lootT -= Time.deltaTime;
                if (!_loot.activeSelf && State == SiteState.Standing) _loot.SetActive(true);
                if (State == SiteState.Standing) { _smoke.Intensity = Mathf.Clamp01(_lootT / 40f) * 0.35f; }
                if (_lootT <= 0f) { _loot.SetActive(false); if (State == SiteState.Standing) _smoke.Intensity = 0f; }
            }
            if (State == SiteState.Burning)
            {
                BurnTime += Time.deltaTime;
                if (_dressing != null)
                {
                    // The buildings sink and lean as they burn, then give way to the ruin.
                    float k = Mathf.Clamp01(BurnTime / BurnSeconds);
                    _dressing.transform.localScale = new Vector3(1f + 0.1f * k, Mathf.Lerp(1f, 0.12f, k * k), 1f + 0.1f * k);
                    _dressing.transform.localRotation = Quaternion.Euler(6f * k, 0, 5f * k);
                    if (k >= 1f) _dressing.SetActive(false);
                }
                if (BurnTime > BurnSeconds * 0.45f) _ruin.SetActive(true);
                _fire.Intensity = BurnTime < FireSeconds * 0.5f ? 1f : Mathf.Clamp01(1f - (BurnTime - FireSeconds * 0.5f) / (FireSeconds * 0.5f));
                _smoke.Intensity = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(BurnTime / FireSeconds));
                if (BurnTime >= FireSeconds) { _fire.Intensity = 0f; State = SiteState.Ruin; }
            }
            else if (State == SiteState.Ruin) { _fire.Intensity = 0f; }
        }
    }
}
