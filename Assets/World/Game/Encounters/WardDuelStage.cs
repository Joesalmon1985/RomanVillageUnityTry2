using System.Collections.Generic;
using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The 3D side of the Ward duel: the player's wizard body facing Mira, a shimmering Ward in front of each wizard that
    /// cracks as fractures land (never showing which locus), and a weave-coloured bolt per cast. Purely visual.
    /// <para>
    /// Campaign modes (<see cref="BuildCampaign"/>, PRD_CRYSTALS I7): the "player" side is Mira, the caster the player
    /// plays through (a stand-in body made by the actor factory with Mira's id), the "Mira" side is the opponent (a keeper,
    /// Orrin, the champion), and the volunteer companion stands by at the way out. The names <see cref="PlayerActor"/> /
    /// <see cref="MiraActor"/> are kept for the practice duel's tests; <see cref="CasterActor"/> / <see cref="OpponentActor"/>
    /// say the same thing for every mode.
    /// </para>
    /// Hooks for the character package's duel director (WP-9, from tag cry-I7): <see cref="CastQueued"/> (a cast was
    /// accepted: side, wind-up), <see cref="BoltReleased"/>, <see cref="Struck"/> (impact on a Ward: side, totals), and
    /// <see cref="ResetWards"/> for a rematch. Every cue uses aggregate totals only, never a locus (<see cref="Log"/>).
    /// </summary>
    public sealed class WardDuelStage : MonoBehaviour
    {
        public static readonly Color PlayerWardColor = new Color(0.30f, 0.85f, 1.00f);
        public static readonly Color MiraWardColor = new Color(0.80f, 0.45f, 1.00f);
        /// <summary>The opponent's Ward in campaign modes (Mira's own Ward keeps the player's blue).</summary>
        public static readonly Color OpponentWardColor = new Color(1.00f, 0.50f, 0.28f);
        public static readonly Color MiraTunic = new Color(0.45f, 0.25f, 0.70f);

        public ICharacterActor PlayerActor { get; private set; }
        public ICharacterActor MiraActor { get; private set; }
        public Vector3 PlayerPos { get; private set; }
        public Vector3 MiraPos { get; private set; }
        public WardShield PlayerWard { get; private set; }
        public WardShield MiraWard { get; private set; }
        public int BoltsInFlight => _bolts.Count;
        /// <summary>Casts accepted but not yet released (the caster's wind-up).</summary>
        public int CastsWindingUp => _pending.Count;

        /// <summary>The body casting for the player: the player's wizard (practice) or Mira (campaign modes).</summary>
        public ICharacterActor CasterActor => PlayerActor;
        /// <summary>The body across the stage: Mira (practice) or the opponent (campaign modes).</summary>
        public ICharacterActor OpponentActor => MiraActor;
        public WardShield CasterWard => PlayerWard;
        public WardShield OpponentWard => MiraWard;
        /// <summary>The companion who holds the way out (lethal duels), or null.</summary>
        public ICharacterActor VolunteerActor { get; private set; }
        public Vector3 VolunteerPos { get; private set; }
        /// <summary>True when the stage was built by <see cref="BuildCampaign"/>.</summary>
        public bool IsCampaign { get; private set; }
        /// <summary>The volunteer has stepped into the gap (a lethal defeat or surrender).</summary>
        public bool VolunteerHolding { get; private set; }
        /// <summary>Essence orbs gathered at the caster's hand (the weave being built).</summary>
        public int GatheredOrbs => _gather.Count;

        /// <summary>A cast was accepted (byPlayer side, seconds of wind-up before the bolt leaves).</summary>
        public event System.Action<bool, float> CastQueued;
        /// <summary>A bolt left the caster's hand (byPlayer side).</summary>
        public event System.Action<bool> BoltReleased;
        /// <summary>A bolt struck a Ward: byPlayer = the player's (caster's) bolt hit the opponent's Ward; then fractures, echoes, broken.</summary>
        public event System.Action<bool, int, int, bool> Struck;

        /// <summary>
        /// Every presentation cue in order (cast, release, hit, sound) with the side and the aggregate totals only. Tests
        /// compare it across weaves that match on different loci: it must be identical.
        /// </summary>
        public readonly List<string> Log = new List<string>();

        /// <summary>Wind-up before the bolt leaves: the caster's (player inputs) and the opponent's (profile tempo).</summary>
        public float CasterWindUp = 0f, OpponentWindUp = 0f;

        private readonly List<Bolt> _bolts = new List<Bolt>();
        private readonly List<PendingCast> _pending = new List<PendingCast>();
        private readonly List<Transform> _gather = new List<Transform>();
        private int[] _gatherShown = new int[0];
        private EncounterSfx _sfx;
        private Light _volunteerLight;
        private Vector3 _volunteerTarget;

        private sealed class Bolt
        {
            public Transform Root; public Transform[] Orbs; public Light Light;
            public Vector3 From, To; public float T, Duration;
            public WardShield Target; public int Fractures, Echoes; public bool Broken, ByPlayer;
        }

        private sealed class PendingCast
        {
            public bool ByPlayer; public int[] Weave; public int Fractures, Echoes; public bool Broken; public float At;
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

        /// <summary>
        /// The campaign stage (lethal, friendly): Mira (a factory body with her id) on the caster mark, the opponent facing
        /// her, the volunteer standing by. Positions come from the request's <see cref="DuelStageSpec"/> only, so it works
        /// on the surface and inside an interior alike.
        /// </summary>
        public static WardDuelStage BuildCampaign(Transform parent, DuelStageSpec spec, Color opponentColour, Color volunteerColour, EncounterSfx sfx)
        {
            var go = new GameObject("WardDuelStage");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<WardDuelStage>();
            s._sfx = sfx;
            s.IsCampaign = true;
            s.PlayerPos = spec.CasterPos;
            s.MiraPos = spec.OpponentPos;
            var dir = spec.OpponentPos - spec.CasterPos; dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            float yaw = BoardSpace.YawOf(dir);
            s.PlayerActor = ActorFactory.Create(spec.HasCaster ? spec.CasterActorId : NpcIds.Mira, MiraTunic, spec.CasterPos, yaw, go.transform);
            s.PlayerActor.Root.name = "Caster Mira";
            s.MiraActor = ActorFactory.Create(string.IsNullOrEmpty(spec.OpponentActorId) ? "opponent" : spec.OpponentActorId, opponentColour, spec.OpponentPos, yaw + 180f, go.transform);
            s.MiraActor.Root.name = "Opponent " + spec.OpponentActorId;
            if (spec.HasVolunteer)
            {
                s.VolunteerPos = spec.VolunteerPos;
                var mid = (spec.CasterPos + spec.OpponentPos) * 0.5f;
                s.VolunteerActor = ActorFactory.Create(spec.VolunteerActorId, volunteerColour, spec.VolunteerPos, BoardSpace.YawOf(mid - spec.VolunteerPos), go.transform);
                s.VolunteerActor.Root.name = "Volunteer " + spec.VolunteerActorId;
            }
            s.PlayerWard = WardShield.Build(go.transform, "CasterWard", spec.CasterPos + dir * 1.05f + Vector3.up * 1.25f, dir, PlayerWardColor);
            s.MiraWard = WardShield.Build(go.transform, "OpponentWard", spec.OpponentPos - dir * 1.05f + Vector3.up * 1.25f, -dir, OpponentWardColor);
            var lgo = new GameObject("DuelLight");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.position = (spec.CasterPos + spec.OpponentPos) / 2f + Vector3.up * 3f;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 10f; l.intensity = 1.3f; l.color = new Color(0.85f, 0.75f, 1f);
            return s;
        }

        /// <summary>Whole Wards again (rematch): bolts, wind-ups and shards gone, everyone idle.</summary>
        public void ResetWards()
        {
            PlayerWard.ResetWard();
            MiraWard.ResetWard();
            foreach (var b in _bolts) if (b.Root != null) Destroy(b.Root.gameObject);
            _bolts.Clear();
            _pending.Clear();
            SetGathering(null);
            PlayerActor?.PlayIdle();
            MiraActor?.PlayIdle();
            Log.Add("reset");
        }

        /// <summary>
        /// Orbs gather at the caster's hand while the player builds a weave (campaign modes): one small orb per essence, in
        /// the order picked. Null or empty clears them.
        /// </summary>
        public void SetGathering(IReadOnlyList<int> weave)
        {
            int n = weave != null ? weave.Count : 0;
            bool same = n == _gatherShown.Length;
            for (int i = 0; same && i < n; i++) same = _gatherShown[i] == weave[i];
            if (same) return;
            foreach (var t in _gather) if (t != null) Destroy(t.gameObject);
            _gather.Clear();
            _gatherShown = new int[n];
            for (int i = 0; i < n; i++)
            {
                _gatherShown[i] = weave[i];
                var c = EncounterUi.EssenceColor(weave[i]);
                var mb = new MeshBuilder();
                Octahedron(mb, Vector3.zero, 0.07f, new Color(c.r, c.g, c.b, 1f));
                Octahedron(mb, Vector3.zero, 0.13f, new Color(c.r, c.g, c.b, 0.3f));
                _gather.Add(mb.ToObject("Gather" + i, transform, MaterialFactory.CreateGlow(Color.white)).transform);
            }
        }

        /// <summary>The volunteer steps into the gap between the opponent and the way out and holds it (a lethal loss).</summary>
        public void VolunteerHolds()
        {
            if (VolunteerActor == null || VolunteerHolding) return;
            VolunteerHolding = true;
            var gap = Vector3.Lerp(PlayerPos, MiraPos, 0.45f);
            var side = Vector3.Cross(Vector3.up, (MiraPos - PlayerPos).normalized);
            _volunteerTarget = gap + side * 0.6f;
            _volunteerTarget.y = Mathf.Lerp(PlayerPos.y, MiraPos.y, 0.45f);
            VolunteerActor.FaceTowards(MiraPos);
            var lgo = new GameObject("VolunteerLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.position = _volunteerTarget + Vector3.up * 2.2f;
            _volunteerLight = lgo.AddComponent<Light>();
            _volunteerLight.type = LightType.Point; _volunteerLight.range = 4.5f; _volunteerLight.intensity = 2.2f; _volunteerLight.color = new Color(1f, 0.55f, 0.35f);
            PlayerActor?.FaceTowards(VolunteerPos);
            Log.Add("volunteer holds");
        }

        /// <summary>
        /// Plays a cast: the caster animates, and after its wind-up a bolt coloured by the weave flies to the other Ward and
        /// cracks it on arrival. Several casts may overlap; each has its own bolt.
        /// </summary>
        public void Cast(bool byPlayer, int[] weave, int fractures, bool broken) => Cast(byPlayer, weave, fractures, 0, broken);

        /// <summary>As <see cref="Cast(bool, int[], int, bool)"/> with the echo total for the struck cue.</summary>
        public void Cast(bool byPlayer, int[] weave, int fractures, int echoes, bool broken)
        {
            var caster = byPlayer ? PlayerActor : MiraActor;
            caster?.PlayOneShot(ActorKeys.Cast);
            float wind = byPlayer ? CasterWindUp : OpponentWindUp;
            Log.Add("cast " + Side(byPlayer) + " wind " + wind.ToString("0.00"));
            CastQueued?.Invoke(byPlayer, wind);
            if (byPlayer) SetGathering(null);
            var p = new PendingCast { ByPlayer = byPlayer, Weave = (int[])weave.Clone(), Fractures = fractures, Echoes = echoes, Broken = broken, At = Time.unscaledTime + wind };
            if (wind <= 0f) Release(p); else _pending.Add(p);
        }

        private static string Side(bool byPlayer) => byPlayer ? "caster" : "opponent";

        private void Release(PendingCast p)
        {
            bool byPlayer = p.ByPlayer; var weave = p.Weave; int fractures = p.Fractures; bool broken = p.Broken;
            var target = byPlayer ? MiraWard : PlayerWard;
            var from = (byPlayer ? PlayerPos : MiraPos) + Vector3.up * 1.45f;
            Log.Add("release " + Side(byPlayer));
            BoltReleased?.Invoke(byPlayer);
            var b = new Bolt { From = from, To = target.transform.position, Duration = 0.6f, Target = target, Fractures = fractures, Echoes = p.Echoes, Broken = broken, ByPlayer = byPlayer };
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
            for (int i = 0; i < _pending.Count; i++)
            {
                if (Time.unscaledTime < _pending[i].At) continue;
                var p = _pending[i];
                _pending.RemoveAt(i--);
                Release(p);
            }
            if (_gather.Count > 0 && PlayerActor != null)
            {
                // Orbs circle a point in front of Mira's right hand.
                var root = PlayerActor.Root;
                var hand = root.position + root.forward * 0.45f + root.right * 0.25f + Vector3.up * 1.2f;
                for (int k = 0; k < _gather.Count; k++)
                {
                    float a = Time.unscaledTime * 4f + k * Mathf.PI * 2f / _gather.Count;
                    _gather[k].position = hand + (root.right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * 0.16f;
                }
            }
            if (VolunteerHolding && VolunteerActor != null)
            {
                var vr = VolunteerActor.Root;
                vr.position = Vector3.MoveTowards(vr.position, _volunteerTarget, dt * 2.6f);
                if ((vr.position - _volunteerTarget).sqrMagnitude < 0.01f && !VolunteerActor.IsPlayingOneShot && VolunteerActor.CurrentKey != ActorKeys.Cheer)
                    VolunteerActor.PlayOneShot(ActorKeys.Cheer); // arms up across the way: holding it
                if (_volunteerLight != null) _volunteerLight.intensity = 2.0f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            }
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
                    bool crack = b.Fractures > 0 || b.Broken;
                    if (crack) _sfx?.Play(EncounterSfx.CrackClip, b.Broken ? 0.9f : 0.5f);
                    Log.Add("hit " + (b.ByPlayer ? "opponent" : "caster") + " ward fr " + b.Fractures + " ec " + b.Echoes + " broken " + b.Broken
                        + " crack level " + b.Target.CrackLevel + " sfx " + (crack ? (b.Broken ? "crack 0.9" : "crack 0.5") : "none"));
                    if (b.Broken && IsCampaign) (b.ByPlayer ? MiraActor : PlayerActor)?.PlayOneShot(ActorKeys.Surprise);
                    Destroy(b.Root.gameObject);
                    _bolts.RemoveAt(i);
                    Struck?.Invoke(b.ByPlayer, b.Fractures, b.Echoes, b.Broken);
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
