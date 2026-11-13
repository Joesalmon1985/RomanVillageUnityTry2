using System.Collections.Generic;
using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The 3D side of the Ward duel: the player's wizard body facing Mira, a shimmering Ward in front of each wizard that
    /// cracks as fractures land (never showing which locus), and a weave-coloured bolt per cast. Purely visual.
    /// </summary>
    public sealed class WardDuelStage : MonoBehaviour
    {
        public static readonly Color PlayerWardColor = new Color(0.30f, 0.85f, 1.00f);
        public static readonly Color MiraWardColor = new Color(0.80f, 0.45f, 1.00f);

        public ICharacterActor PlayerActor { get; private set; }
        public ICharacterActor MiraActor { get; private set; }
        public Vector3 PlayerPos { get; private set; }
        public Vector3 MiraPos { get; private set; }
        public WardShield PlayerWard { get; private set; }
        public WardShield MiraWard { get; private set; }
        public int BoltsInFlight => _bolts.Count;

        private readonly List<Bolt> _bolts = new List<Bolt>();
        private EncounterSfx _sfx;

        private sealed class Bolt
        {
            public Transform Root; public Transform[] Orbs; public Light Light;
            public Vector3 From, To; public float T, Duration;
            public WardShield Target; public int Fractures; public bool Broken;
        }

        public static WardDuelStage Build(Transform parent, Vector3 playerPos, ICharacterActor mira, EncounterSfx sfx)
        {
            var go = new GameObject("WardDuelStage");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<WardDuelStage>();
            s._sfx = sfx;
            s.MiraActor = mira;
            s.MiraPos = mira != null ? mira.Root.position : playerPos + Vector3.forward * 5f;
            s.PlayerPos = playerPos;
            var toMira = s.MiraPos - playerPos; toMira.y = 0;
            float yaw = BoardSpace.YawOf(toMira);
            s.PlayerActor = ActorFactory.Create("ward_player", new Color(0.18f, 0.42f, 0.62f), playerPos, yaw, go.transform);
            s.PlayerActor.Root.name = "Player wizard";
            mira?.FaceTowards(playerPos);
            var dir = toMira.normalized;
            s.PlayerWard = WardShield.Build(go.transform, "PlayerWard", playerPos + dir * 1.05f + Vector3.up * 1.25f, dir, PlayerWardColor);
            s.MiraWard = WardShield.Build(go.transform, "MiraWard", s.MiraPos - dir * 1.05f + Vector3.up * 1.25f, -dir, MiraWardColor);
            // a soft pool of light between the duellists
            var lgo = new GameObject("DuelLight");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.position = (playerPos + s.MiraPos) / 2f + Vector3.up * 3f;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 9f; l.intensity = 1.2f; l.color = new Color(0.85f, 0.75f, 1f);
            return s;
        }

        /// <summary>Whole Wards again (rematch).</summary>
        public void ResetWards()
        {
            PlayerWard.ResetWard();
            MiraWard.ResetWard();
            foreach (var b in _bolts) if (b.Root != null) Destroy(b.Root.gameObject);
            _bolts.Clear();
        }

        /// <summary>Plays a cast: the caster animates, a bolt coloured by the weave flies to the other Ward and cracks it on arrival.</summary>
        public void Cast(bool byPlayer, int[] weave, int fractures, bool broken)
        {
            var caster = byPlayer ? PlayerActor : MiraActor;
            caster?.PlayOneShot(ActorKeys.Cast);
            var target = byPlayer ? MiraWard : PlayerWard;
            var from = (byPlayer ? PlayerPos : MiraPos) + Vector3.up * 1.45f;
            var b = new Bolt { From = from, To = target.transform.position, Duration = 0.6f, Target = target, Fractures = fractures, Broken = broken };
            b.Root = new GameObject("Bolt").transform;
            b.Root.SetParent(transform, false);
            b.Root.position = from;
            b.Orbs = new Transform[weave.Length];
            Color avg = Color.black;
            for (int i = 0; i < weave.Length; i++)
            {
                var c = EncounterUi.EssenceColor(weave[i]);
                avg += c / weave.Length;
                var mb = new MeshBuilder();
                Octahedron(mb, Vector3.zero, 0.16f, new Color(c.r, c.g, c.b, 1f));
                Octahedron(mb, Vector3.zero, 0.28f, new Color(c.r, c.g, c.b, 0.25f));
                b.Orbs[i] = mb.ToObject("Orb" + i, b.Root, MaterialFactory.CreateGlow(Color.white)).transform;
            }
            var lgo = new GameObject("BoltLight"); lgo.transform.SetParent(b.Root, false);
            b.Light = lgo.AddComponent<Light>();
            b.Light.type = LightType.Point; b.Light.range = 4f; b.Light.intensity = 3f; b.Light.color = avg;
            _bolts.Add(b);
            _sfx?.Play(EncounterSfx.CastClip, 0.45f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                b.T += dt;
                float u = Mathf.Clamp01(b.T / b.Duration);
                var p = Vector3.Lerp(b.From, b.To, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.6f;
                b.Root.position = p;
                var fwd = (b.To - b.From).normalized;
                var side = Vector3.Cross(fwd, Vector3.up).normalized;
                for (int k = 0; k < b.Orbs.Length; k++)
                {
                    float a = Time.unscaledTime * 14f + k * Mathf.PI * 2f / b.Orbs.Length;
                    b.Orbs[k].position = p + (side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * 0.22f;
                }
                if (u >= 1f)
                {
                    b.Target.Hit(b.Fractures, b.Broken);
                    if (b.Fractures > 0 || b.Broken) _sfx?.Play(EncounterSfx.CrackClip, b.Broken ? 0.9f : 0.5f);
                    Destroy(b.Root.gameObject);
                    _bolts.RemoveAt(i);
                }
            }
        }

        internal static void Octahedron(MeshBuilder mb, Vector3 c, float r, Color col)
        {
            Vector3 px = c + Vector3.right * r, nx = c - Vector3.right * r, py = c + Vector3.up * r, ny = c - Vector3.up * r, pz = c + Vector3.forward * r, nz = c - Vector3.forward * r;
            mb.Tri(py, pz, px, col); mb.Tri(py, px, nz, col); mb.Tri(py, nz, nx, col); mb.Tri(py, nx, pz, col);
            mb.Tri(ny, px, pz, col); mb.Tri(ny, nz, px, col); mb.Tri(ny, nx, nz, col); mb.Tri(ny, pz, nx, col);
        }
    }

    /// <summary>One wizard's Ward: a shimmering hexagon that shows crack lines for the best fracture count so far and shatters when broken.</summary>
    public sealed class WardShield : MonoBehaviour
    {
        public const float Radius = 0.95f;
        public int CrackLevel { get; private set; }
        public bool IsBroken { get; private set; }

        private Material _fill, _rim, _crack;
        private Color _color;
        private readonly List<GameObject> _crackLevels = new List<GameObject>();
        private GameObject _body;
        private float _flash;
        private readonly List<Transform> _shards = new List<Transform>();
        private readonly List<Vector3> _shardVel = new List<Vector3>();

        public static WardShield Build(Transform parent, string name, Vector3 centre, Vector3 facing, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            var s = go.AddComponent<WardShield>();
            s._color = color;
            s.BuildMeshes();
            return s;
        }

        private void BuildMeshes()
        {
            _body = new GameObject("Body");
            _body.transform.SetParent(transform, false);
            _fill = MaterialFactory.CreateGlow(new Color(_color.r, _color.g, _color.b, 0.28f));
            _rim = MaterialFactory.CreateGlow(new Color(_color.r, _color.g, _color.b, 1f));
            _crack = MaterialFactory.CreateGlow(new Color(1f, 1f, 1f, 1f));

            var fill = new MeshBuilder();
            var ring = HexRing(Radius);
            for (int i = 0; i < 6; i++) fill.Tri(Vector3.zero, ring[i], ring[(i + 1) % 6], new Color(1, 1, 1, 0.55f));
            var inner = HexRing(Radius * 0.55f);
            for (int i = 0; i < 6; i++) fill.Tri(Vector3.zero, inner[i], inner[(i + 1) % 6], new Color(1, 1, 1, 0.35f));
            fill.ToObject("Fill", _body.transform, _fill);

            var rim = new MeshBuilder();
            var outer = HexRing(Radius * 1.04f);
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                rim.Quad(ring[i], outer[i], outer[j], ring[j], Color.white);
            }
            var ringIn = HexRing(Radius * 0.55f); var ringIn2 = HexRing(Radius * 0.58f);
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                rim.Quad(ringIn[i], ringIn2[i], ringIn2[j], ringIn[j], new Color(1, 1, 1, 0.6f));
            }
            rim.ToObject("Rim", _body.transform, _rim);

            // Crack levels 1 and 2 (3 = broken). Jagged lines from near the centre outwards; never tied to a locus.
            var rnd = new PropRandom(name.GetHashCode());
            for (int level = 0; level < 2; level++)
            {
                var mb = new MeshBuilder();
                for (int k = 0; k < 3; k++)
                {
                    float ang = (level * 3 + k) * 60f + rnd.Next() * 40f;
                    Vector3 p = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0) * 0.08f;
                    float a = ang;
                    for (int seg = 0; seg < 4; seg++)
                    {
                        a += (rnd.Next() - 0.5f) * 50f;
                        var q = p + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0) * (0.18f + rnd.Next() * 0.06f);
                        if (q.magnitude > Radius * 0.9f) break;
                        Segment(mb, p, q, 0.022f);
                        p = q;
                    }
                }
                var obj = mb.ToObject("Cracks" + (level + 1), _body.transform, _crack);
                obj.SetActive(false);
                _crackLevels.Add(obj);
            }
        }

        private static void Segment(MeshBuilder mb, Vector3 a, Vector3 b, float w)
        {
            var d = (b - a).normalized;
            var n = new Vector3(-d.y, d.x, 0) * w;
            var col = Color.white;
            mb.Quad(a - n + Vector3.back * 0.01f, b - n + Vector3.back * 0.01f, b + n + Vector3.back * 0.01f, a + n + Vector3.back * 0.01f, col);
        }

        private static Vector3[] HexRing(float r)
        {
            var v = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (60f * i + 30f) * Mathf.Deg2Rad;
                v[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            }
            return v;
        }

        public void ResetWard()
        {
            CrackLevel = 0; IsBroken = false; _flash = 0;
            _body.SetActive(true);
            foreach (var c in _crackLevels) c.SetActive(false);
            foreach (var s in _shards) if (s != null) Destroy(s.gameObject);
            _shards.Clear(); _shardVel.Clear();
        }

        /// <summary>A cast landed with this many fractures.</summary>
        public void Hit(int fractures, bool broken)
        {
            _flash = 1f;
            if (broken) { Shatter(); return; }
            CrackLevel = Mathf.Max(CrackLevel, Mathf.Min(2, fractures));
            for (int i = 0; i < _crackLevels.Count; i++) _crackLevels[i].SetActive(i < CrackLevel);
        }

        private void Shatter()
        {
            if (IsBroken) return;
            IsBroken = true; CrackLevel = 3;
            _body.SetActive(false);
            var ring = HexRing(Radius);
            for (int i = 0; i < 6; i++)
            {
                for (int half = 0; half < 2; half++)
                {
                    var mb = new MeshBuilder();
                    var a = half == 0 ? Vector3.zero : (ring[i] + ring[(i + 1) % 6]) * 0.5f;
                    mb.Tri(a, ring[i], ring[(i + 1) % 6], new Color(1, 1, 1, 0.8f));
                    var shard = mb.ToObject("Shard", transform, _rim).transform;
                    _shards.Add(shard);
                    var outDir = transform.TransformDirection((ring[i] + ring[(i + 1) % 6]).normalized);
                    _shardVel.Add(outDir * (1.5f + half) + Vector3.up * 1.2f + transform.forward * -0.8f);
                }
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            _flash = Mathf.Max(0, _flash - Time.unscaledDeltaTime * 2.5f);
            if (_body != null && _body.activeSelf)
            {
                float shimmer = 0.22f + 0.08f * Mathf.Sin(t * 3.1f + transform.position.x) + 0.5f * _flash;
                _fill.color = new Color(_color.r, _color.g, _color.b, shimmer);
                _rim.color = new Color(_color.r, _color.g, _color.b, 0.75f + 0.25f * Mathf.Sin(t * 5.3f) + _flash);
                _body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.7f) * 4f);
                _body.transform.localScale = Vector3.one * (1f + 0.06f * _flash);
            }
            for (int i = 0; i < _shards.Count; i++)
            {
                if (_shards[i] == null) continue;
                _shardVel[i] += Vector3.down * 6f * Time.unscaledDeltaTime;
                _shards[i].position += _shardVel[i] * Time.unscaledDeltaTime;
                _shards[i].Rotate(200f * Time.unscaledDeltaTime, 140f * Time.unscaledDeltaTime, 0);
                if (_shards[i].position.y < 0.05f) _shardVel[i] = Vector3.zero;
            }
        }
    }
}
