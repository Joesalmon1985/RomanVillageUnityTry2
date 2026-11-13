using System.Collections.Generic;
using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The dance circle at the home camp's reserved plot (REFACTOR_SPEC 2.E, 4.2, 8.3): a fire in a stone ring, four drums
    /// on a 4 m circle on the dancer's side (drums 1-4 left to right, keys D F J K painted on them), Sol's big drum beyond
    /// the fire, log seats and six dancers of the camp. It is scenery in normal play; during the dance
    /// (<see cref="RhythmPartyEncounter"/>) the encounter drives it every frame: drum glow for approaching cues, embers
    /// flying from the fire to the drums, flashes on judgements, the dancers' level, the ending-choice marks and the
    /// fire-leap or spirit-call ending. Purely visual: no audio, no state. Owner: package D.
    /// </summary>
    public sealed class DanceCircle : MonoBehaviour, IInspectable
    {
        public enum Effect { None, FireLeap, SpiritCall }

        public static DanceCircle Current { get; private set; }

        public const float SkinHeight = 0.82f, SkinRadius = 0.48f, FlameBase = 0.25f;
        /// <summary>The outer drums (1 and 4) stand further from the dancer and carry most cues (kick and snare), so they are
        /// built larger to read about as big on screen as the near ones.</summary>
        public static float DrumScale(int pad) => pad == 1 || pad == 4 ? 1.3f : 1f;
        /// <summary>Show a shrinking firelight ring on each drum skin that closes on the drum at the cue time.</summary>
        public static bool ApproachRings = true;

        public CampLayout Layout { get; private set; }
        public Vector3 Centre { get; private set; }
        public Vector3 Forward => Layout.Forward;
        public Vector3 Right => Layout.Right;
        public Effect LastEffect { get; private set; }
        public float EffectStarted { get; private set; } = -100f;
        public bool ChoiceVisible { get; private set; }
        public bool ChoiceFireLeap { get; private set; }

        public string Title => "Dance circle";
        public IEnumerable<string> Lines { get { yield return "A fire, four drums and Sol. Talk to Sol to dance."; } }

        private sealed class Drum
        {
            public Transform Root, Skin, Ring, Halo;
            public Material SkinMat, RingMat, HaloMat;
            public Light Light;
            public Vector3 Top;
            public float Glow, RingScale; public Color GlowColor = Color.white; public bool Held;
            public float FlashAt = -100f, FlashDur = 0.3f, FlashPower; public Color FlashColor; public bool Burst, Miss;
        }

        private readonly Drum[] _drums = new Drum[5];
        private readonly List<Transform> _flames = new List<Transform>();
        private readonly List<Material> _flameMats = new List<Material>();
        private Light _fireLight;
        // Seven flame tongues: a tall yellow-white heart, then orange and red tongues round it, shorter towards the rim.
        private static readonly Color[] FlameColors = { new Color(1f, 0.88f, 0.45f), new Color(1f, 0.62f, 0.14f), new Color(0.96f, 0.36f, 0.07f),
            new Color(1f, 0.55f, 0.12f), new Color(0.92f, 0.30f, 0.06f), new Color(1f, 0.70f, 0.20f), new Color(0.95f, 0.40f, 0.08f) };
        private static readonly float[] FlameWidths = { 0.26f, 0.34f, 0.36f, 0.32f, 0.36f, 0.30f, 0.34f };
        private static readonly float[] FlameHeights = { 2.0f, 1.55f, 1.05f, 1.4f, 0.95f, 1.25f, 1.1f };
        private static Vector3 FlameOffset(int i) => i == 0 ? Vector3.zero : new Vector3(Mathf.Sin(i * 1.047f + 0.4f), 0, Mathf.Cos(i * 1.047f + 0.4f)) * 0.3f;
        private Transform _bassSkin; private Material _bassSkinMat; private float _bassPulse;
        private Material _leapMarkMat, _spiritMarkMat;
        private Transform _leapMark, _spiritMark;
        private float _choicePulse; private bool _choiceLocked;
        private float _fireBoost, _beatPulse; private int _level; private bool _live;
        private Mesh _sparkMesh, _coreMesh;
        private readonly List<Vector3> _v = new List<Vector3>(), _cv = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>(), _cc = new List<Color>();
        private readonly List<int> _t = new List<int>(), _ct = new List<int>();
        private static readonly Color HideSkin = new Color(0.80f, 0.71f, 0.55f);
        public static readonly Color FireOrange = new Color(1f, 0.55f, 0.15f), SpiritBlue = new Color(0.35f, 0.6f, 1f);

        private sealed class Dancer
        {
            public ICharacterActor Actor;
            public Vector3 Home, Spot; public float HomeYaw;
            public Vector3 Pos; public float LeapStart = -1f, LeapDelay; public bool Leaping;
        }
        private readonly List<Dancer> _dancers = new List<Dancer>();
        public int DancerCount => _dancers.Count;
        /// <summary>How many dancers are up in the ring (2 at the first mix stage, all six at the last).</summary>
        public int ActiveDancers { get; private set; }

        // ------------------------------------------------------------------ build

        public static DanceCircle Build(Transform parent, CampLayout layout)
        {
            var go = new GameObject("DanceCircle");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.DanceCircleCentre;
            var dc = go.AddComponent<DanceCircle>();
            dc.Layout = layout;
            dc.Centre = layout.DanceCircleCentre;
            dc.BuildScenery();
            Current = dc;
            return dc;
        }

        private void OnDestroy() { if (Current == this) Current = null; if (_sparkMesh != null) Destroy(_sparkMesh); if (_coreMesh != null) Destroy(_coreMesh); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; ApproachRings = true; }

        private Vector3 L(Vector3 world) => transform.InverseTransformPoint(world);

        private void BuildScenery()
        {
            var mat = MaterialFactory.CreateVertexColored(Color.white, "Dance circle");
            var mb = new MeshBuilder();
            var rnd = new PropRandom(635);
            // Trampled ground ring, darker towards the fire.
            int seg = 40;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 P(float a, float r, float y) => new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
                var outer = new Color(0.47f, 0.38f, 0.26f); var inner = new Color(0.33f, 0.26f, 0.19f);
                mb.Quad(P(a0, 2.2f, 0.035f), P(a0, 7.0f, 0.03f), P(a1, 7.0f, 0.03f), P(a1, 2.2f, 0.035f), Color.Lerp(outer, inner, 0.15f * rnd.Next()));
                mb.Tri(Vector3.up * 0.04f, P(a0, 2.2f, 0.035f), P(a1, 2.2f, 0.035f), inner);
            }
            // Stone ring and crossed logs of the fire.
            for (int i = 0; i < 11; i++)
            {
                float a = i * Mathf.PI * 2f / 11f;
                mb.Rock(new Vector3(Mathf.Sin(a) * 1.15f, 0.02f, Mathf.Cos(a) * 1.15f), 0.28f, CampBuilder.Stone * (0.85f + 0.3f * rnd.Next()), rnd.Next);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                CampBuilder.Beam(mb, d * 0.85f + Vector3.up * 0.1f, -d * 0.1f + Vector3.up * 0.55f, 0.16f, CampBuilder.Charcoal * 1.6f);
            }
            mb.Cylinder(Vector3.up * 0.03f, 0.9f, 0.8f, 0.08f, 12, new Color(0.18f, 0.12f, 0.09f));

            // Log seats on the far side and the flanks (the dancer's side stays open).
            foreach (float sth in new[] { -56f, 58f, -112f, 112f, CampLayout.DrummerAngle })
            {
                float r = sth == CampLayout.DrummerAngle ? 6.45f : CampLayout.SeatRadius + 0.05f;
                var c = L(Layout.CirclePoint(sth, r));
                var tan = L(Layout.CirclePoint(sth + 90f, 1f)) - L(Layout.CirclePoint(sth, 0f)); tan.y = 0; tan.Normalize();
                float half = sth == CampLayout.DrummerAngle ? 0.9f : 1.2f;
                CampBuilder.Beam(mb, c - tan * half + Vector3.up * 0.2f, c + tan * half + Vector3.up * 0.2f, 0.36f, Color.Lerp(CampBuilder.Wood, CampBuilder.WoodLight, 0.4f));
            }

            // Drum bodies (skins are separate so they can glow).
            for (int p = 1; p <= 4; p++)
            {
                var pos = L(Layout.DrumPosition(p));
                float k = DrumScale(p);
                mb.Cylinder(pos, 0.5f * k, 0.46f * k, SkinHeight - 0.02f, 10, CampBuilder.Wood);
                foreach (float y in new[] { 0.14f, 0.62f })
                    mb.Cylinder(pos + Vector3.up * y, (0.515f - y * 0.06f) * k, (0.51f - y * 0.06f) * k, 0.07f, 10, p % 2 == 0 ? CampBuilder.Ochre : CampBuilder.Clay, false);
            }
            // Sol's big drum beyond the fire.
            var bass = L(Layout.CirclePoint(CampLayout.DrummerAngle, 4.4f));
            mb.Cylinder(bass, 0.72f, 0.66f, 0.95f, 12, CampBuilder.Wood * 0.9f);
            mb.Cylinder(bass + Vector3.up * 0.45f, 0.73f, 0.72f, 0.1f, 12, CampBuilder.Ochre, false);
            mb.ToObject("Circle", transform, mat);

            // Drum skins, halos, approach rings, lights and painted key letters.
            string[] keys = { "", "D", "F", "J", "K" };
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int p = 1; p <= 4; p++)
            {
                var d = new Drum();
                var world = Layout.DrumPosition(p);
                d.Root = new GameObject("Drum " + p).transform;
                d.Root.SetParent(transform, false);
                d.Root.position = world;
                d.Top = world + Vector3.up * SkinHeight;
                d.SkinMat = MaterialFactory.CreateUnlit(HideSkin, "Drum skin " + p);
                var skin = new MeshBuilder();
                skin.Cylinder(Vector3.zero, SkinRadius * DrumScale(p), SkinRadius * DrumScale(p), 0.04f, 16, Color.white);
                d.Skin = skin.ToObject("Skin", d.Root, d.SkinMat).transform;
                d.Skin.localPosition = Vector3.up * (SkinHeight - 0.03f);
                d.HaloMat = MaterialFactory.CreateGlow(new Color(1, 1, 1, 0), "Drum halo " + p);
                d.Halo = Disc(1f, 24).ToObject("Halo", d.Root, d.HaloMat).transform;
                d.Halo.localPosition = Vector3.up * (SkinHeight + 0.04f);
                d.Halo.localScale = Vector3.one * 0.85f * DrumScale(p);
                d.RingMat = MaterialFactory.CreateGlow(new Color(1, 1, 1, 0), "Drum ring " + p);
                d.Ring = Annulus(0.92f, 1f, 40).ToObject("Approach ring", d.Root, d.RingMat).transform;
                d.Ring.localPosition = Vector3.up * (SkinHeight + 0.06f);
                d.Ring.gameObject.SetActive(false);
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(d.Root, false);
                lgo.transform.localPosition = Vector3.up * (SkinHeight + 0.7f);
                d.Light = lgo.AddComponent<Light>();
                d.Light.type = LightType.Point; d.Light.range = 3.5f; d.Light.intensity = 0f;
                // Key letter painted on the side facing the dancer.
                var tgo = new GameObject("Key " + keys[p]);
                tgo.transform.SetParent(d.Root, false);
                var toDancer = Layout.DancerSpot - world; toDancer.y = 0; toDancer.Normalize();
                tgo.transform.position = world + toDancer * 0.5f * DrumScale(p) + Vector3.up * 0.4f;
                tgo.transform.rotation = Quaternion.LookRotation(-toDancer, Vector3.up);
                var tm = tgo.AddComponent<TextMesh>();
                tm.font = font; tm.text = keys[p]; tm.fontSize = 96; tm.characterSize = 0.32f * 10f / 96f;
                tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = CampBuilder.Clay;
                tgo.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.TextMaterial(font);
                var col = d.Root.gameObject.AddComponent<CapsuleCollider>();
                col.isTrigger = true; col.center = Vector3.up * 0.45f; col.radius = 0.5f * DrumScale(p); col.height = 0.9f;
                var info = d.Root.gameObject.AddComponent<CampPropInfo>();
                info.Kind = "drum"; info.TitleText = "Drum " + p + " (" + keys[p] + ")"; info.Line = "Played in the dance: key " + keys[p] + " or touch strip " + p + ".";
                _drums[p] = d;
            }
            {
                _bassSkinMat = MaterialFactory.CreateUnlit(HideSkin, "Big drum skin");
                var skin = new MeshBuilder();
                skin.Cylinder(Vector3.zero, 0.66f, 0.66f, 0.04f, 18, Color.white);
                _bassSkin = skin.ToObject("Big drum skin", transform, _bassSkinMat).transform;
                _bassSkin.position = Layout.CirclePoint(CampLayout.DrummerAngle, 4.4f) + Vector3.up * 0.93f;
            }

            // Flames: four low-poly self-lit cones of different size and colour (opaque, so they read in daylight),
            // flickered in Update, inside a soft additive glow.
            for (int i = 0; i < FlameColors.Length; i++)
            {
                var m = MaterialFactory.CreateUnlit(FlameColors[i], "Flame " + i);
                var cone = new MeshBuilder();
                cone.Cone(Vector3.zero, 1f, 1f, 7, Color.white);
                var f = cone.ToObject("Flame " + i, transform, m).transform;
                f.localPosition = Vector3.up * FlameBase + FlameOffset(i);
                f.localScale = new Vector3(FlameWidths[i], FlameHeights[i], FlameWidths[i]);
                f.localRotation = Quaternion.Euler(0, i * 23f, 0);
                _flames.Add(f); _flameMats.Add(m);
            }
            var fl = new GameObject("Fire light");
            fl.transform.SetParent(transform, false);
            fl.transform.localPosition = Vector3.up * 1.4f;
            _fireLight = fl.AddComponent<Light>();
            _fireLight.type = LightType.Point; _fireLight.range = 15f; _fireLight.intensity = 1.6f; _fireLight.color = FireOrange;
            var fcol = gameObject.AddComponent<CapsuleCollider>();
            fcol.isTrigger = true; fcol.center = Vector3.up * 0.8f; fcol.radius = 1.2f; fcol.height = 1.6f;

            // Ending-choice marks: an ember-strewn patch left of the fire, a blue mist patch right of it.
            _leapMarkMat = MaterialFactory.CreateGlow(new Color(FireOrange.r, FireOrange.g, FireOrange.b, 0f), "Fire leap mark");
            _spiritMarkMat = MaterialFactory.CreateGlow(new Color(SpiritBlue.r, SpiritBlue.g, SpiritBlue.b, 0f), "Spirit call mark");
            _leapMark = Disc(1f, 28).ToObject("Fire leap mark", transform, _leapMarkMat).transform;
            _leapMark.position = Layout.FireLeapMark + Vector3.up * 0.06f; _leapMark.localScale = Vector3.one * 0.9f;
            _spiritMark = Disc(1f, 28).ToObject("Spirit call mark", transform, _spiritMarkMat).transform;
            _spiritMark.position = Layout.SpiritCallMark + Vector3.up * 0.06f; _spiritMark.localScale = Vector3.one * 0.9f;

            // Sparks and embers: one dynamic additive mesh, rebuilt every frame.
            _sparkMesh = new Mesh { name = "Dance sparks" };
            _sparkMesh.MarkDynamic();
            var sgo = new GameObject("Sparks");
            sgo.transform.SetParent(transform, false);
            sgo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sgo.AddComponent<MeshFilter>().sharedMesh = _sparkMesh;
            sgo.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateGlow(Color.white, "Dance sparks");
            _sparksRoot = sgo.transform;
            _coreMesh = new Mesh { name = "Dance ember cores" };
            _coreMesh.MarkDynamic();
            var cgo = new GameObject("Ember cores");
            cgo.transform.SetParent(transform, false);
            cgo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            cgo.AddComponent<MeshFilter>().sharedMesh = _coreMesh;
            cgo.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(Color.white, "Dance ember cores");

            // The camp's dancers at their seats.
            Color[] tunics = { new Color(0.62f, 0.38f, 0.22f), new Color(0.55f, 0.45f, 0.30f), new Color(0.70f, 0.52f, 0.30f),
                               new Color(0.46f, 0.34f, 0.26f), new Color(0.66f, 0.30f, 0.20f), new Color(0.58f, 0.50f, 0.36f) };
            float[] th = { -32f, 40f, -68f, 74f, -100f, 104f };
            for (int i = 0; i < th.Length; i++)
            {
                var home = Layout.CirclePoint(th[i], CampLayout.SeatRadius - 0.6f);
                var spot = Layout.CirclePoint(th[i], i < 4 ? CampLayout.DanceRingRadius : 5.7f);
                float yaw = BoardSpace.YawOf(Centre - home);
                var a = ActorFactory.Create("dancer" + i, tunics[i], home, yaw, transform);
                a.Root.name = "Dancer " + i;
                _dancers.Add(new Dancer { Actor = a, Home = home, Spot = spot, HomeYaw = yaw, Pos = home });
            }
        }

        private Transform _sparksRoot;

        private static MeshBuilder Disc(float r, int seg)
        {
            // MeshBuilder colours whole triangles, so the soft edge is three bands of falling brightness.
            var mb = new MeshBuilder();
            float[] radii = { 0.55f, 0.8f, 1f };
            float[] alpha = { 1f, 0.55f, 0.22f };
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 P(float a, float rr) => new Vector3(Mathf.Sin(a) * rr * r, 0, Mathf.Cos(a) * rr * r);
                mb.Tri(Vector3.zero, P(a0, radii[0]), P(a1, radii[0]), new Color(1, 1, 1, alpha[0]));
                mb.Quad(P(a0, radii[0]), P(a0, radii[1]), P(a1, radii[1]), P(a1, radii[0]), new Color(1, 1, 1, alpha[1]));
                mb.Quad(P(a0, radii[1]), P(a0, radii[2]), P(a1, radii[2]), P(a1, radii[1]), new Color(1, 1, 1, alpha[2]));
            }
            return mb;
        }

        private static MeshBuilder Annulus(float r0, float r1, int seg)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 P(float a, float r) => new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                mb.Quad(P(a0, r0), P(a0, r1), P(a1, r1), P(a1, r0), Color.white);
            }
            return mb;
        }

        // ------------------------------------------------------------------ drums

        /// <summary>Approach of a cue: 0 when it appears at the fire (cue time minus the fall time), 1 at the cue time.</summary>
        public static double Approach(double cueTime, double songTime, double fallSeconds) => (songTime - (cueTime - fallSeconds)) / fallSeconds;

        public Vector3 DrumTop(int pad) => _drums[Mathf.Clamp(pad, 1, 4)].Top;
        public Vector3 FireTop => Centre + Vector3.up * 1.1f;

        /// <summary>Where an ember flying to drum <paramref name="pad"/> is at approach u (0 at the fire, 1 on the skin): a low arc.</summary>
        public Vector3 EmberPath(int pad, float u)
        {
            var top = DrumTop(pad) + Vector3.up * 0.08f;
            var dir = top - Centre; dir.y = 0; dir.Normalize();
            var from = Centre + dir * 0.5f + Vector3.up * 1.15f;
            u = Mathf.Clamp01(u);
            return Vector3.Lerp(from, top, u) + Vector3.up * (0.9f * Mathf.Sin(u * Mathf.PI));
        }

        /// <summary>Glow 0..1 set by the dance this frame (0 = dark).</summary>
        public float DrumGlow(int pad) => pad >= 1 && pad <= 4 ? _drums[pad].Glow : 0f;
        public bool IsLit(int pad) => pad >= 1 && pad <= 4 && (_drums[pad].Glow > 0.001f || _drums[pad].Held);
        public bool IsHeld(int pad) => pad >= 1 && pad <= 4 && _drums[pad].Held;
        /// <summary>Approach-ring scale on the skin (1 = closed on the drum at the cue time, 0 = none).</summary>
        public float RingScale(int pad) => pad >= 1 && pad <= 4 ? _drums[pad].RingScale : 0f;

        /// <summary>Sets a drum's cue glow for this frame (call every frame while it should stay lit).</summary>
        public void SetDrum(int pad, float glow, Color color, bool held, float ringU = -1f)
        {
            if (pad < 1 || pad > 4) return;
            var d = _drums[pad];
            d.Glow = Mathf.Clamp01(glow); d.GlowColor = color; d.Held = held;
            d.RingScale = ringU >= 0f && ringU <= 1.05f ? Mathf.Lerp(2.6f, 1f, Mathf.Clamp01(ringU)) : 0f;
        }

        /// <summary>A judgement on a drum: perfect, great and accepted flash in their colours (perfect and great burst sparks); a miss turns the skin dark red and puffs smoke.</summary>
        public void Flash(int pad, Color color, float power, bool burst, bool miss = false)
        {
            if (pad < 1 || pad > 4) return;
            var d = _drums[pad];
            d.FlashAt = Time.unscaledTime; d.FlashColor = color; d.FlashPower = power; d.Burst = burst; d.Miss = miss;
            d.FlashDur = miss ? 0.45f : 0.22f + 0.18f * power;
        }

        /// <summary>A soft white tap on every drum and the fire (count-in beats).</summary>
        public void CountPulse()
        {
            for (int p = 1; p <= 4; p++) Flash(p, new Color(1f, 0.95f, 0.85f), 0.35f, false);
            _beatPulse = 1f;
        }

        public void StrikeBigDrum(float power) { _bassPulse = Mathf.Max(_bassPulse, power); }

        public void SetChoice(bool visible, bool fireLeap, bool locked, float beatPhase)
        {
            ChoiceVisible = visible; ChoiceFireLeap = fireLeap; _choiceLocked = locked;
            _choicePulse = 1f - beatPhase;
        }

        // ------------------------------------------------------------------ embers

        private readonly List<(Vector3 p, float s, Color c)> _embers = new List<(Vector3, float, Color)>();
        public int EmbersThisFrame { get; private set; }

        /// <summary>Adds one ember for this frame only (world position, size in metres, colour; alpha scales brightness).</summary>
        public void AddEmber(Vector3 p, float size, Color c) => _embers.Add((p, size, c));

        private void Spark(Vector3 p, float s, Color c)
        {
            // An opaque self-lit core (fading = shrinking, so it reads against sky and ground alike) inside an additive halo.
            float a = Mathf.Clamp01(c.a);
            Oct(_cv, _cc, _ct, p, s * (0.35f + 0.65f * a), new Color(Mathf.Min(1f, c.r * 1.1f), Mathf.Min(1f, c.g * 1.1f), Mathf.Min(1f, c.b * 1.1f), 1f));
            Oct(_v, _c, _t, p, s * 2.6f, new Color(c.r, c.g, c.b, a * 0.3f));
        }

        /// <summary>A soft additive puff without a core (smoke, spirit wisps).</summary>
        private void Puff(Vector3 p, float s, Color c)
        {
            Oct(_v, _c, _t, p, s, new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a) * 0.45f));
            Oct(_v, _c, _t, p, s * 0.55f, new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a) * 0.35f));
        }

        private void Oct(List<Vector3> v, List<Color> cl, List<int> tl, Vector3 c, float r, Color col)
        {
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color k)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                cl.Add(k); cl.Add(k); cl.Add(k);
                tl.Add(i); tl.Add(i + 1); tl.Add(i + 2);
            }
            Vector3 px = c + Vector3.right * r, nx = c - Vector3.right * r, py = c + Vector3.up * r, ny = c - Vector3.up * r, pz = c + Vector3.forward * r, nz = c - Vector3.forward * r;
            Tri(py, pz, px, col); Tri(py, px, nz, col); Tri(py, nz, nx, col); Tri(py, nx, pz, col);
            Tri(ny, px, pz, col); Tri(ny, nz, px, col); Tri(ny, nx, nz, col); Tri(ny, pz, nx, col);
        }


        // ------------------------------------------------------------------ dancers and fire

        /// <summary>Called by the dance every frame: live (count-in or phrase), beat (song beats) and the mix stage 0-3.</summary>
        public void SetDance(bool live, double beat, int stage)
        {
            _live = live; _beat = beat; _level = live ? Mathf.Clamp(stage, 0, 3) : -1;
            ActiveDancers = !live || beat < 0 ? 0 : stage <= 0 ? 2 : stage <= 2 ? 4 : 6;
        }
        private double _beat;

        /// <summary>A block kept: the dancers cheer and the fire jumps.</summary>
        public void Cheer() { _beatPulse = 2.2f; _falter = 0f; foreach (var d in _dancers) if (!d.Leaping) d.Actor.PlayOneShot(ActorKeys.Cheer); }
        /// <summary>A block missed: the fire sinks for a moment and the dancers start back.</summary>
        public void Falter() { _falter = 1f; Falters++; foreach (var d in _dancers) if (!d.Leaping) d.Actor.PlayOneShot(ActorKeys.Surprise); }
        public int Falters { get; private set; }
        private float _falter;
        public void Clap(int i) { if (_dancers.Count > 0) _dancers[((i % _dancers.Count) + _dancers.Count) % _dancers.Count].Actor.PlayOneShot(ActorKeys.Clap); }

        /// <summary>The successful ending: dancers leap the fire with an orange flare, or blue spirit smoke rises.</summary>
        public void PlayEffect(Effect e)
        {
            LastEffect = e; EffectStarted = Time.unscaledTime;
            if (e == Effect.FireLeap)
            {
                for (int i = 0; i < 2 && i < _dancers.Count; i++) { _dancers[i].Leaping = true; _dancers[i].LeapStart = Time.unscaledTime; _dancers[i].LeapDelay = i * 0.55f; }
            }
            else if (e == Effect.SpiritCall) Cheer();
        }

        /// <summary>Back to scenery: drums dark, dancers at their seats, no marks or effects.</summary>
        public void ResetDance()
        {
            for (int p = 1; p <= 4; p++) { var d = _drums[p]; d.Glow = 0; d.Held = false; d.RingScale = 0; d.FlashAt = -100f; }
            _live = false; _level = -1; ActiveDancers = 0; ChoiceVisible = false; _fireBoost = 0; _bassPulse = 0; _falter = 0;
            EffectStarted = -100f; LastEffect = Effect.None;
            foreach (var d in _dancers)
            {
                d.Leaping = false; d.Pos = d.Home;
                d.Actor.Root.position = d.Home; d.Actor.Root.localScale = Vector3.one;
                d.Actor.FaceTowards(Centre); d.Actor.PlayIdle();
            }
            _embers.Clear();
        }

        private void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float since = now - EffectStarted;
            bool flare = LastEffect == Effect.FireLeap && since < 2.2f;
            bool spirit = LastEffect == Effect.SpiritCall && since < 4.5f;
            _beatPulse = Mathf.Max(0, _beatPulse - dt * 3f);
            _bassPulse = Mathf.Max(0, _bassPulse - dt * 4f);
            float phase = (float)(_beat - System.Math.Floor(_beat));
            float beatKick = _live ? Mathf.Pow(1f - phase, 3f) : 0f;
            float level = _level < 0 ? 0f : (_level + 1) / 4f;

            // Fire: calm flicker in normal play; during the dance it pumps on the beat and grows with the mix level.
            float flick = Mathf.PerlinNoise(now * 3.1f, 0.3f);
            float boost = flare ? Mathf.Clamp01(1.6f - since * 0.7f) : 0f;
            _falter = Mathf.Max(0, _falter - dt * 0.55f);
            float grow = (1f + (_live ? 0.2f * level + 0.18f * beatKick : 0f) + 0.15f * _beatPulse + 1.1f * boost) * (1f - 0.55f * Mathf.Clamp01(_falter));
            for (int i = 0; i < _flames.Count; i++)
            {
                var f = _flames[i];
                float n = Mathf.PerlinNoise(now * (2.3f + i * 0.7f), i * 1.7f);
                float h = FlameHeights[i] * (0.7f + 0.6f * n) * grow;
                float w = FlameWidths[i] * (0.9f + 0.2f * flick) * Mathf.Sqrt(grow);
                f.localScale = new Vector3(w, h, w);
                float s = spirit ? Mathf.Clamp01(1.4f - Mathf.Abs(since - 1.6f) * 0.5f) : 0f;
                _flameMats[i].color = Color.Lerp(FlameColors[i], Color.Lerp(SpiritBlue, Color.white, i * 0.2f), s);
            }
            _fireLight.intensity = 1.4f + 0.6f * flick + (_live ? 1.2f * beatKick * (0.4f + level) : 0f) + 4f * boost + _beatPulse - 1.1f * Mathf.Clamp01(_falter);
            _fireLight.color = spirit ? Color.Lerp(FireOrange, SpiritBlue, Mathf.Clamp01(since < 1f ? since : 3.5f - since)) : FireOrange;

            // Big drum: Sol's beat after the bass joins.
            _bassSkinMat.color = Color.Lerp(HideSkin, new Color(1f, 0.45f, 0.95f), _bassPulse * 0.8f);
            _bassSkin.localScale = new Vector3(1, 1f + _bassPulse * 0.6f, 1);

            // Ending-choice marks.
            if (ChoiceVisible)
            {
                float hi = _choiceLocked ? 0.85f : 0.55f + 0.4f * _choicePulse, lo = 0.12f;
                _leapMarkMat.color = new Color(FireOrange.r, FireOrange.g, FireOrange.b, ChoiceFireLeap ? hi : lo);
                _spiritMarkMat.color = new Color(SpiritBlue.r, SpiritBlue.g, SpiritBlue.b, !ChoiceFireLeap ? hi : lo);
                float sc = 1.0f + (_choiceLocked ? 0.25f : 0.15f * _choicePulse);
                _leapMark.localScale = Vector3.one * (ChoiceFireLeap ? sc : 0.8f);
                _spiritMark.localScale = Vector3.one * (!ChoiceFireLeap ? sc : 0.8f);
            }
            else
            {
                _leapMarkMat.color = new Color(0, 0, 0, 0); _spiritMarkMat.color = new Color(0, 0, 0, 0);
            }

            AnimateDancers(now, dt, beatKick, phase, flare, since);
        }

        private void AnimateDancers(float now, float dt, float beatKick, float phase, bool flare, float since)
        {
            for (int i = 0; i < _dancers.Count; i++)
            {
                var d = _dancers[i];
                var root = d.Actor.Root;
                if (d.Leaping)
                {
                    // Run to the left of the fire, leap over it, land on the right, walk back to the seat.
                    float t = now - d.LeapStart - d.LeapDelay;
                    var a = Centre - Right * 2.7f; var b = Centre + Right * 2.7f;
                    if (t < 0f) { }
                    else if (t < 0.5f) d.Pos = Vector3.Lerp(d.Pos, a, Mathf.Clamp01(dt * 9f));
                    else if (t < 1.35f)
                    {
                        float u = (t - 0.5f) / 0.85f;
                        d.Pos = Vector3.Lerp(a, b, u) + Vector3.up * (2.1f * Mathf.Sin(u * Mathf.PI));
                        if (t - dt < 0.5f) d.Actor.PlayOneShot(ActorKeys.Cheer);
                    }
                    else if (t < 2.6f) { var g = b; d.Pos = Vector3.MoveTowards(d.Pos, g, dt * 4f); }
                    else { d.Pos = Vector3.MoveTowards(d.Pos, d.Home, dt * 3.5f); if ((d.Pos - d.Home).sqrMagnitude < 0.01f) d.Leaping = false; }
                    root.position = d.Pos;
                    var face = t >= 0.5f && t < 1.35f ? b : (t >= 2.6f ? d.Home : Centre);
                    if ((face - root.position).sqrMagnitude > 0.01f) d.Actor.FaceTowards(face);
                    root.localScale = Vector3.one;
                    continue;
                }
                bool active = i < ActiveDancers;
                var target = active ? d.Spot : d.Home;
                if (active)
                {
                    // Circle a little round the fire, faster at higher levels.
                    float sway = Mathf.Sin((float)_beat * Mathf.PI * 0.25f + i) * (6f + 4f * _level);
                    var off = d.Spot - Centre;
                    target = Centre + Quaternion.Euler(0, sway, 0) * off;
                }
                d.Pos = Vector3.MoveTowards(d.Pos, target, dt * 3.2f);
                float hop = active ? (0.05f + 0.06f * Mathf.Max(0, _level)) * Mathf.Abs(Mathf.Sin(((float)_beat + i * 0.25f) * Mathf.PI)) : 0f;
                root.position = d.Pos + Vector3.up * hop;
                float sq = _live ? 1f - (active ? 0.08f : 0.03f) * Mathf.Pow(1f - Mathf.Repeat(phase + i * 0.11f, 1f), 3f) : 1f;
                root.localScale = new Vector3(2f - sq, sq, 2f - sq);
                if ((d.Pos - target).sqrMagnitude > 0.04f) d.Actor.FaceTowards(target);
                else d.Actor.FaceTowards(Centre);
            }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            _v.Clear(); _c.Clear(); _t.Clear(); _cv.Clear(); _cc.Clear(); _ct.Clear();

            // Drums: cue glow, flashes, rings, lights.
            for (int p = 1; p <= 4; p++)
            {
                var d = _drums[p];
                float fa = now - d.FlashAt;
                float flash = fa >= 0 && fa < d.FlashDur ? 1f - fa / d.FlashDur : 0f;
                Color skin = HideSkin * 0.92f;
                float glow = d.Held ? 1f : d.Glow;
                if (glow > 0f) skin = Color.Lerp(skin, Color.Lerp(d.GlowColor, Color.white, 0.25f), 0.35f + 0.65f * glow);
                if (flash > 0f)
                    skin = d.Miss ? Color.Lerp(skin, new Color(0.25f, 0.05f, 0.04f), flash) : Color.Lerp(skin, Color.Lerp(d.FlashColor, Color.white, 0.4f * d.FlashPower), flash);
                d.SkinMat.color = skin;
                float bounce = flash > 0f && !d.Miss ? flash * 0.06f * (0.5f + d.FlashPower) : 0f;
                d.Skin.localPosition = Vector3.up * (SkinHeight - 0.03f - bounce);
                float haloA = Mathf.Max(glow * 0.55f, d.Miss ? 0f : flash * d.FlashPower);
                var hc = flash > 0f && !d.Miss ? d.FlashColor : d.GlowColor;
                d.HaloMat.color = new Color(hc.r, hc.g, hc.b, haloA);
                d.Halo.localScale = Vector3.one * DrumScale(p) * (0.62f + 0.18f * glow + (flash > 0f && !d.Miss ? 0.4f * flash * d.FlashPower : 0f));
                bool ring = ApproachRings && d.RingScale > 0f;
                d.Ring.gameObject.SetActive(ring);
                if (ring)
                {
                    d.Ring.localScale = Vector3.one * (SkinRadius * DrumScale(p) * d.RingScale);
                    d.RingMat.color = new Color(d.GlowColor.r, d.GlowColor.g, d.GlowColor.b, 0.35f + 0.55f * Mathf.Clamp01(glow));
                }
                d.Light.color = flash > 0f && !d.Miss ? d.FlashColor : d.GlowColor;
                d.Light.intensity = 2.2f * glow + (d.Miss ? 0f : 3f * flash * d.FlashPower);

                if (d.Burst && flash > 0f)
                {
                    float u = 1f - flash;
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * Mathf.PI * 2f / 10f + p;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var pos = d.Top + dir * (0.3f + 1.1f * u * (0.6f + 0.4f * d.FlashPower)) + Vector3.up * (0.15f + 0.9f * u - 1.2f * u * u);
                        Spark(pos, 0.05f * (1f - u) + 0.015f, new Color(d.FlashColor.r, d.FlashColor.g, d.FlashColor.b, 1f - u));
                    }
                }
                if (d.Miss && flash > 0f)
                {
                    float u = 1f - flash;
                    for (int k = 0; k < 5; k++)
                    {
                        var pos = d.Top + new Vector3(Mathf.Sin(k * 2.1f) * 0.2f, 0.2f + u * (0.6f + 0.15f * k), Mathf.Cos(k * 2.1f) * 0.2f);
                        Puff(pos, 0.12f + 0.1f * u, new Color(0.45f, 0.42f, 0.4f, 0.7f * (1f - u)));
                    }
                }
            }

            // Ambient sparks rising from the fire (more during the dance), and the dance's embers.
            int ambient = _live ? 14 : 6;
            for (int k = 0; k < ambient; k++)
            {
                float seed = k * 12.9898f;
                float life = 1.6f + (k % 3) * 0.4f;
                float t = Mathf.Repeat(now + k * 0.37f, life) / life;
                var pos = Centre + new Vector3(Mathf.Sin(seed + t * 3f) * (0.3f + 0.4f * t), 0.6f + t * (2.8f + (k % 4) * 0.4f), Mathf.Cos(seed * 1.3f + t * 2f) * (0.3f + 0.4f * t));
                Spark(pos, 0.035f * (1f - t) + 0.01f, new Color(1f, 0.6f, 0.2f, 0.9f * (1f - t)));
            }
            float since = now - EffectStarted;
            if (LastEffect == Effect.SpiritCall && since < 4.5f)
            {
                for (int k = 0; k < 36; k++)
                {
                    float start = k * 0.08f;
                    float t = (since - start) / 3.2f;
                    if (t < 0f || t > 1f) continue;
                    float a = k * 0.9f + t * 6f;
                    float r = 0.4f + 1.6f * t;
                    var pos = Centre + new Vector3(Mathf.Sin(a) * r, 1f + t * 9f, Mathf.Cos(a) * r);
                    var c = Color.Lerp(SpiritBlue, Color.white, 0.3f * Mathf.Sin(k));
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.9f;
                    Puff(pos, 0.25f + 0.6f * t, c);
                }
            }
            if (LastEffect == Effect.FireLeap && since < 2.2f)
            {
                for (int k = 0; k < 24; k++)
                {
                    float t = Mathf.Repeat(since * 1.4f + k / 24f, 1f);
                    float a = k * 2.4f;
                    var pos = Centre + new Vector3(Mathf.Sin(a) * (0.5f + 2f * t), 0.8f + 4f * t - 2.5f * t * t, Mathf.Cos(a) * (0.5f + 2f * t));
                    Spark(pos, 0.07f * (1f - t) + 0.02f, new Color(1f, 0.5f + 0.4f * (k % 2), 0.15f, 1f - t));
                }
            }
            EmbersThisFrame = _embers.Count;
            foreach (var e in _embers) Spark(e.p, e.s, e.c);
            _embers.Clear();

            _sparkMesh.Clear();
            if (_v.Count > 65000) _sparkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _sparkMesh.SetVertices(_v);
            _sparkMesh.SetColors(_c);
            _sparkMesh.SetTriangles(_t, 0);
            _sparkMesh.bounds = new Bounds(Centre + Vector3.up * 5f, new Vector3(30f, 30f, 30f));
            _coreMesh.Clear();
            if (_cv.Count > 65000) _coreMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _coreMesh.SetVertices(_cv);
            _coreMesh.SetColors(_cc);
            _coreMesh.SetTriangles(_ct, 0);
            _coreMesh.bounds = _sparkMesh.bounds;
        }
    }
}
