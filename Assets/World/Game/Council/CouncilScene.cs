using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>A person in the council scene whose speech bubble hangs over their head.</summary>
    public sealed class CouncilSpeaker : MonoBehaviour, ISpeaker, IFollowable
    {
        public string Id = "council";
        public string Name = "Someone";
        public string SpeakerId => Id;
        public string FollowName => Name;
        public Transform FollowTransform => transform;
        public Transform Head { get; set; }
    }

    /// <summary>How a villager stands towards the candidate whose sitting is shown.</summary>
    public enum PState { Undecided, Listening, Settled, Away }

    /// <summary>One villager of a dispute place (the last of a place's people is its lead).</summary>
    public sealed class Person
    {
        public PlaceholderActor A;
        public CouncilSpeaker Speaker;
        public int Track, Index;
        public bool IsLead, SitsAtHome;
        public Vector3 Home; public float HomeYaw;
        public PState S;
        public Vector3 Target; public float TargetYaw;
        public float AwayUntil, NextPleased, Speed = 3f;
        public Vector3 AwayPoint;
        public bool Arrived;
    }

    /// <summary>A dispute location: its objective, its people and the spots the crowd uses.</summary>
    public sealed class Place
    {
        public int Track; public string DisputeId;
        public Vector3 C, Fwd, Right; public float R, Ground;
        public Person[] P;
        public Transform Root;
        public float ReactUntil;
        public Vector3 ListenSpot => C - Fwd * (0.55f * R) - Right * (0.45f * R);
        public Vector3 SettledSpot => C - Fwd * (0.35f * R) + Right * (0.45f * R);
        public Vector3 Prop => C;
    }

    /// <summary>One thing the player can tap, in screen pixels (origin bottom-left).</summary>
    public struct TapTarget { public string Name; public Vector2 Px; public float RadiusPx; public Vector3 World; }

    /// <summary>One thing the candidate can offer: the legal choice of a tray (both advances) or of one socket.</summary>
    public sealed class CouncilProposal
    {
        public int Tray, Socket; // Socket -1 = the whole tray
        public List<KeyValuePair<int, int>> Steps = new List<KeyValuePair<int, int>>(); // track, steps
        public bool Touches(int track) { foreach (var s in Steps) if (s.Key == track) return true; return false; }
    }

    /// <summary>
    /// The council village: a lookout tower near the home fire, eleven dispute places at normal size with their people
    /// (3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3), the shown candidate at the foot of the tower and their catspaws. During a
    /// sitting the whole village shows only that candidate's standing: supporters turn to them and stand close and
    /// pleased, the people a catspaw has won this sitting gather round it leaning in, the rest stay neutral. Driven only
    /// by <c>WorldState.Council</c> and its events; it never writes state.
    /// </summary>
    public sealed partial class CouncilScene : MonoBehaviour
    {
        public static CouncilScene Current { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public const float TowerH = 5.4f;
        public int SettlementId { get; private set; }
        public CouncilSite Site { get; private set; }
        public Transform Root { get; private set; }
        public CouncilBench Bench { get; private set; }
        public float GroundY { get; private set; }
        /// <summary>Set by the encounter while it is open; the candidate and the aide stand where this camera looks.</summary>
        public Camera ViewCamera;
        public float TowerGround { get; private set; }
        public Vector3 CamPos => new Vector3(Site.Position.x, TowerGround + TowerH + 1.2f, Site.Position.z);
        public static float GroundAt(Vector3 p) => PlayerWalker.GroundHeight(p) + 0.02f;

        readonly Place[] _places = new Place[11];
        readonly PlaceholderActor[] _cand = new PlaceholderActor[4];
        readonly CouncilSpeaker[] _candSpeaker = new CouncilSpeaker[4];
        PlaceholderActor _aide; GameObject _lantern;
        GameObject _seal, _petition;
        readonly List<GameObject> _notes = new List<GameObject>();
        sealed class Agent { public PlaceholderActor A; public int Track, Seat; public bool Leaving; public float LeaveAt; }
        readonly Dictionary<int, Agent> _agents = new Dictionary<int, Agent>();
        readonly List<Agent> _leaving = new List<Agent>();
        readonly List<KeyValuePair<Vector3, float>> _solids = new List<KeyValuePair<Vector3, float>>();
        readonly HashSet<int> _spoken = new HashSet<int>();
        public int ActiveSeatShown { get; private set; } = -1;
        int _shownLast = -1;
        int _leadBubble, _candBubble, _lineCounter;
        float _carryT = -1f, _beckonAt;
        public bool HoldPetition;
        public int MoversBusy { get; private set; }
        public bool Settled => MoversBusy == 0 && _carryT < 0f;
        public int LeadBubbles { get; private set; }
        public string LastLeadLine { get; private set; }
        public string LastCandidateLine { get; private set; }
        public int AgentsShown => _agents.Count;
        public List<CouncilProposal> Proposals { get; private set; } = new List<CouncilProposal>();
        public int PointedProposal { get; private set; } = -1;
        float _pointStart; int _pointPush = -1;
        public bool PetitionShown => _petition != null && _petition.activeSelf;
        public bool AideShown => _aide != null && _aide.gameObject.activeSelf;

        public WorldState State => WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;
        public SettlementCouncil Council => CouncilApi.Get(State, SettlementId);
        public Vector3 ToWorld(Vector3 local) => Root.TransformPoint(local);

        public Place PlaceOf(int track) => _places[track - 2];
        public Person Lead(int track) => _places[track - 2].P[_places[track - 2].P.Length - 1];
        public IReadOnlyList<Person> People(int track) => _places[track - 2].P;
        public PlaceholderActor Candidate(int seat) => _cand[seat];
        public PlaceholderActor Aide => _aide;
        public PlaceholderActor Catspaw(int track) => _agents.TryGetValue(track, out var a) ? a.A : null;
        public GameObject SealObject => _seal;
        public GameObject PetitionObject => _petition;

        // ================================================================== build

        public static CouncilScene Build(Transform parent, int settlement, CouncilSite site)
        {
            if (Current != null) { Destroy(Current.gameObject); Current = null; }
            float g = PlayerWalker.GroundHeight(site.Position);
            for (int i = 0; i < 11; i++) g = Mathf.Max(g, PlayerWalker.GroundHeight(site.Loc[i]));
            g = Mathf.Max(g, PlayerWalker.GroundHeight(site.Hearth));
            var go = new GameObject("CouncilScene");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(0, g + 0.02f, 0), Quaternion.identity);
            var sc = go.AddComponent<CouncilScene>();
            sc.SettlementId = settlement; sc.Site = site; sc.Root = go.transform; sc.GroundY = g + 0.02f; sc.TowerGround = GroundAt(site.Position);
            Current = sc;
            sc.BuildAll();
            return sc;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            WalkGrid.RemoveStaticBlockers("council");
        }

        public static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 size, Color c, Vector3 euler = default)
        {
            var go = Signs.Prim(PrimitiveType.Cube, parent, name, pos, size, MaterialFactory.Shared(c), euler);
            var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            return go;
        }

        public static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
        {
            var go = Signs.Prim(PrimitiveType.Cylinder, parent, name, pos, size, MaterialFactory.Shared(c));
            var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            return go;
        }

        public static GameObject Ball(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
        {
            var go = Signs.Prim(PrimitiveType.Sphere, parent, name, pos, size, MaterialFactory.Shared(c));
            var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            return go;
        }

        Color SeatColor(int seat) => CouncilLayout.EmblemColor(CouncilLayout.EmblemColours[seat]);

        PlaceholderActor Actor(string id, Color tunic, Vector3 world, float yaw, float scale = 1f)
        {
            var a = PlaceholderActor.Build(id, tunic, Root);
            var col = a.GetComponent<Collider>(); if (col != null) Destroy(col);
            a.transform.position = world;
            a.transform.rotation = Quaternion.Euler(0, yaw, 0);
            a.transform.localScale = Vector3.one * scale;
            a.SetYaw(yaw);
            return a;
        }

        CouncilSpeaker Speak(PlaceholderActor a, string id, string name)
        {
            var sp = a.gameObject.AddComponent<CouncilSpeaker>();
            sp.Id = id; sp.Name = name; sp.Head = a.HeadPivot != null ? a.HeadPivot : a.transform;
            return sp;
        }

        void BuildAll()
        {
            var sc = Council;
            BuildPlaces(sc);
            BuildTower();
            BuildPeople(sc);
            BuildCandidates(sc);
            Bench = CouncilBench.Build(this);
            RegisterClearings();
            Debug.Log("[Council] village built: tower at " + Site.Position + ", fan yaw " + Site.Yaw.ToString("0") + ", " + Site.Why);
        }

        void RegisterClearings()
        {
            for (int i = 0; i < 11; i++)
            {
                var c = Site.Loc[i]; float r = _places[i].R + 2.5f;
                LandViews.AddClearing(c.x, c.z, r);
                float fr = r * 0.7f;
                for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; LandViews.AddClearing(c.x + Mathf.Cos(a) * fr, c.z + Mathf.Sin(a) * fr, r * 0.55f); }
            }
            var t = Site.Position;
            LandViews.AddClearing(t.x, t.z, 12f);
            for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; LandViews.AddClearing(t.x + Mathf.Cos(a) * 9f, t.z + Mathf.Sin(a) * 9f, 6f); }
            if (Bench != null) { var w = ToWorld(Bench.Origin); LandViews.AddClearing(w.x, w.z, 12f); }
        }

        // ---- people ----

        static readonly Color[] Cloth =
        {
            new Color(0.52f, 0.44f, 0.34f), new Color(0.42f, 0.40f, 0.30f), new Color(0.50f, 0.38f, 0.30f), new Color(0.38f, 0.42f, 0.44f),
            new Color(0.56f, 0.50f, 0.40f), new Color(0.46f, 0.36f, 0.26f), new Color(0.60f, 0.52f, 0.38f),
        };

        static Vector3 Dir(Place pl, float alphaDeg) => pl.Fwd * Mathf.Cos(alphaDeg * Mathf.Deg2Rad) + pl.Right * Mathf.Sin(alphaDeg * Mathf.Deg2Rad);
        static float YawTo(Vector3 from, Vector3 to) { var d = to - from; d.y = 0; return d.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        void BuildPeople(SettlementCouncil sc)
        {
            for (int t = 2; t <= 12; t++)
            {
                var pl = _places[t - 2]; int len = CouncilRules.Length(t);
                pl.P = new Person[len];
                var homes = new List<Vector3>();
                var rnd = new PropRandom(t * 131 + 7);
                for (int i = 0; i < len; i++)
                {
                    bool lead = i == len - 1;
                    Vector3 home = pl.C;
                    if (lead) home = pl.C + pl.Right * (0.35f * pl.R) + pl.Fwd * (0.65f * pl.R);
                    else
                    {
                        for (int tries = 0; tries < 30; tries++)
                        {
                            float alpha = Mathf.Lerp(-95f, 22f, rnd.Next());
                            float rr = pl.R * Mathf.Lerp(0.32f, 1.0f, Mathf.Sqrt(rnd.Next()));
                            var c = pl.C + Dir(pl, alpha) * rr;
                            bool ok = (c - pl.C).magnitude > 1.2f;
                            foreach (var h in homes) if ((h - c).magnitude < 1.0f) { ok = false; break; }
                            if (ok || tries == 29) { home = c; break; }
                        }
                    }
                    homes.Add(home);
                    var tunic = lead ? CouncilLayout.LeadTunic(t) : Cloth[(int)(rnd.Next() * Cloth.Length) % Cloth.Length];
                    float scale = Mathf.Lerp(0.93f, 1.06f, rnd.Next());
                    var a = Actor("res" + t + "_" + i, tunic, new Vector3(home.x, pl.Ground, home.z), 180f, scale);
                    a.name = lead ? "Lead " + t : "Villager " + t + "." + i;
                    var p = new Person { A = a, Track = t, Index = i, IsLead = lead, Home = home, S = PState.Undecided, Speed = Mathf.Lerp(2.8f, 3.4f, rnd.Next()), NextPleased = Time.time + rnd.Range(1f, 9f) };
                    if (lead) p.Speaker = Speak(a, "lead" + t, sc != null && sc.Disputes.Count > t - 2 ? sc.Disputes[t - 2].LeadName : "Lead");
                    p.SitsAtHome = !lead && i % 4 == 2;
                    p.Target = home;
                    pl.P[i] = p;
                }
                // varied facing: pairs talk to each other, the rest look at the objective or off to the side
                for (int i = 0; i < len; i++)
                {
                    var p = pl.P[i];
                    if (p.IsLead) { p.HomeYaw = YawTo(p.Home, pl.C - pl.Right * 3f + pl.Fwd * 0.5f); }
                    else if (i % 3 == 0 && i + 1 < len - 1) p.HomeYaw = YawTo(p.Home, pl.P[i + 1].Home);
                    else if (i % 3 == 1 && i - 1 >= 0) p.HomeYaw = YawTo(p.Home, pl.P[i - 1].Home);
                    else p.HomeYaw = YawTo(p.Home, pl.C) + rnd.Range(-70f, 70f);
                    p.TargetYaw = p.HomeYaw;
                    p.A.transform.rotation = Quaternion.Euler(0, p.HomeYaw, 0);
                    p.A.Seated = p.SitsAtHome;
                }
            }
        }

        void BuildCandidates(SettlementCouncil sc)
        {
            var basePos = new Vector3(Site.Position.x, TowerGround, Site.Position.z) + CouncilSite.Dir(Site.Yaw) * 7f;
            for (int s = 0; s < 4; s++)
            {
                var a = Actor("cand" + s, Color.Lerp(SeatColor(s), new Color(0.5f, 0.4f, 0.3f), 0.25f), basePos + Vector3.right * (s - 1.5f), Site.Yaw);
                a.name = "Candidate " + s;
                Cap(a, SeatColor(s));
                _candSpeaker[s] = Speak(a, "cand" + s, sc != null && sc.Candidates.Count > s ? sc.Candidates[s].Name : "Candidate");
                _cand[s] = a; a.gameObject.SetActive(false);
            }
            _aide = Actor("aide", new Color(0.35f, 0.55f, 0.45f), basePos + Vector3.left * 3f, Site.Yaw);
            _aide.name = "Aide";
            _lantern = CouncilShapes.Make("Bench lantern", _aide.transform, CouncilShapes.LanternMesh(), new Vector3(0.45f, 0.85f, 0.35f), 0.5f);
            _lantern.GetComponent<Renderer>().sharedMaterial = MaterialFactory.CreateUnlit(new Color(1f, 0.9f, 0.6f), "Lantern glow");
            var lf = WorldLights.Add(_aide.transform, new Vector3(0.45f, 1.2f, 0.35f), new Color(1f, 0.75f, 0.4f), 1.0f, 7f, 1, "Aide lantern light");
            lf.GroundGlow = 1.5f; lf.Halo = 0.5f;
            _aide.gameObject.SetActive(false);
            // the seal at the candidate's belt is drawn per active candidate; the petition roll hangs at the other hip
            _seal = CouncilShapes.Make("Belt seal", Root, CouncilShapes.HeavySeal(false), Vector3.zero, 0.34f);
            _petition = CouncilShapes.Make("Petition", Root, CouncilShapes.Petition(false), Vector3.zero, 0.4f);
            _petition.SetActive(false);
            for (int k = 0; k < 6; k++)
            {
                var n = new GameObject("Note " + k).transform; n.SetParent(Root, false);
                var body = CouncilShapes.Make("Paper", n, CouncilShapes.FoldedNote(Color.white), Vector3.zero, 1.7f);
                n.gameObject.SetActive(false);
                _notes.Add(n.gameObject);
            }
        }

        void Cap(PlaceholderActor a, Color c)
        {
            var head = a.HeadPivot; if (head == null) return;
            Cyl(head, "Band", new Vector3(0, 0.27f, 0), new Vector3(0.33f, 0.04f, 0.33f), c);
            Cyl(head, "Cap", new Vector3(0, 0.33f, 0), new Vector3(0.28f, 0.03f, 0.28f), c * 0.8f);
        }

        // ================================================================== per frame

        void Update()
        {
            var sc = Council;
            if (sc == null || sc.Election == null || State == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Sync(sc, dt);
        }

        int ShownSeat(ElectionState e) => e.Phase == ElectionPhase.Complete && e.Winner >= 0 ? e.Winner : e.Active;

        /// <summary>How many of a place's people stand with the shown candidate: settled (banked, or the dispute secured), and listening (won this sitting, not yet banked).</summary>
        public void Expected(ElectionState e, int track, out int settled, out int listening)
        {
            int len = CouncilRules.Length(track);
            int shown = ShownSeat(e);
            if (e.Claimed[track - 2] >= 0) { settled = len; listening = 0; return; }
            settled = Mathf.Min(len, e.BankedAt(shown, track));
            int temp = shown == e.Active && e.Phase != ElectionPhase.Complete ? e.Temp[track - 2] : 0;
            listening = Mathf.Clamp(temp - settled, 0, len - settled);
        }

        void Sync(SettlementCouncil sc, float dt)
        {
            var e = sc.Election;
            int shown = ShownSeat(e);
            bool seatChanged = shown != _shownLast;
            if (shown != ActiveSeatShown)
            {
                ActiveSeatShown = shown;
                for (int s = 0; s < 4; s++) if (_cand[s] != null) _cand[s].gameObject.SetActive(s == shown && ViewCamera != null);
            }
            for (int s = 0; s < 4; s++) if (_cand[s] != null && _cand[s].gameObject.activeSelf != (s == shown && ViewCamera != null)) _cand[s].gameObject.SetActive(s == shown && ViewCamera != null);
            _shownLast = shown;
            float now = Time.time;
            MoversBusy = 0;
            foreach (var pl in _places) SyncPlace(pl, e, shown, seatChanged, now, dt);
            SyncAgents(e, shown, dt);
            SyncCandidate(sc, e, shown, dt);
            SyncProposals(sc, e);
        }

        void SyncPlace(Place pl, ElectionState e, int shown, bool seatChanged, float now, float dt)
        {
            Expected(e, pl.Track, out int settled, out int listening);
            int len = pl.P.Length;
            // listeners: rows of up to 5, 7, 9 round the catspaw's spot, on the side away from the settled group
            var O = pl.ListenSpot;
            var rowCount = new[] { 0, 0, 0 };
            int[] rowCap = { 5, 7, 9 }; int left = listening;
            for (int r = 0; r < 3; r++) { rowCount[r] = Mathf.Min(rowCap[r], left); left -= rowCount[r]; }
            var S0 = pl.SettledSpot; if (settled > 8) S0 = pl.C - pl.Fwd * (0.2f * pl.R) + pl.Right * (0.2f * pl.R);
            bool react = now < pl.ReactUntil;
            for (int i = 0; i < len; i++)
            {
                var p = pl.P[i];
                var ns = i < settled ? PState.Settled : (i < settled + listening ? PState.Listening : PState.Undecided);
                if (p.S == PState.Away && now < p.AwayUntil && ns == PState.Undecided) ns = PState.Away;
                if (!seatChanged && p.S == PState.Listening && ns == PState.Undecided)
                {
                    // a bust (or the sitting ending without a bank): the unsettled listeners walk back and away
                    ns = PState.Away; p.AwayUntil = now + 3.2f;
                    var d = p.A.transform.position - pl.C; d.y = 0;
                    if (d.sqrMagnitude < 0.5f) d = pl.Fwd;
                    p.AwayPoint = p.Home + d.normalized * (pl.R * 0.55f + 1f);
                    p.A.PlayOneShot(ActorKeys.Surprise);
                }
                if (!seatChanged && p.S != PState.Listening && ns == PState.Listening && p.A != null) p.A.PlayOneShot(ActorKeys.Greet);
                p.S = ns;
                switch (ns)
                {
                    case PState.Settled:
                    {
                        int k = i, row = k / 4, col = k % 4;
                        p.Target = S0 + pl.Right * ((col - 1.5f) * 0.85f + (row % 2) * 0.4f - 0.2f) + pl.Fwd * (row * 0.8f);
                        p.TargetYaw = YawTo(p.Target, Site.Position) + Mathf.Sin(i * 1.9f) * 12f;
                        break;
                    }
                    case PState.Listening:
                    {
                        int j = i - settled, r = 0, before = 0;
                        while (r < 2 && j >= before + rowCount[r]) { before += rowCount[r]; r++; }
                        int m = j - before, c = Mathf.Max(1, rowCount[r]);
                        float rad = 1.25f + 0.85f * r;
                        float step = Mathf.Min(40f, 0.85f / rad * Mathf.Rad2Deg);
                        float phi = -25f + step * (m - (c - 1) * 0.5f);
                        p.Target = O + (Quaternion.Euler(0, phi, 0) * pl.Fwd) * rad;
                        p.TargetYaw = YawTo(p.Target, O);
                        break;
                    }
                    case PState.Away:
                        p.Target = p.AwayPoint; p.TargetYaw = YawTo(p.AwayPoint, p.AwayPoint + (p.AwayPoint - pl.C));
                        break;
                    default:
                        p.Target = p.Home; p.TargetYaw = react && !p.IsLead ? YawTo(p.Home, Site.Position) : p.HomeYaw;
                        break;
                }
                bool moving = MoveActor(p.A.transform, p.Target, p.TargetYaw, p.Speed, dt, pl.Ground);
                p.Arrived = !moving;
                if (moving) MoversBusy++;
                p.A.Seated = ns == PState.Undecided && p.SitsAtHome && !moving;
                if (ns == PState.Settled && !moving && now >= p.NextPleased) { p.A.PlayOneShot(i % 2 == 0 ? ActorKeys.Clap : ActorKeys.Greet); p.NextPleased = now + UnityEngine.Random.Range(6f, 13f); }
            }
        }

        /// <summary>Moves a figure towards a spot on the ground and turns it, pushing it out of solid props. True while it is still walking or turning.</summary>
        bool MoveActor(Transform t, Vector3 target, float yaw, float speed, float dt, float ground)
        {
            var pos = t.position; var to = target; to.y = ground;
            pos.y = ground;
            var d = to - pos; float dist = d.magnitude;
            bool moving = dist > 0.12f;
            var before = pos;
            if (moving)
            {
                float step = Mathf.Min(dist, speed * dt);
                pos += d / dist * step;
                foreach (var s in _solids)
                {
                    var o = pos - s.Key; o.y = 0; float m = o.magnitude;
                    if (m < s.Value && m > 1e-3f) pos += o / m * (s.Value - m);
                }
            }
            t.position = pos;
            // a figure the solid props keep from its spot does not count as still walking
            if (moving && dist < 1.5f && (pos - before).magnitude < 0.004f) moving = false;
            float want = moving && dist > 0.5f ? YawTo(pos, to) : yaw;
            float cur = t.eulerAngles.y;
            t.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(cur, want, 420f * dt), 0);
            return moving;
        }

        // ---- catspaws ----

        void SyncAgents(ElectionState e, int shown, float dt)
        {
            var want = new HashSet<int>();
            if (shown == e.Active && e.Phase != ElectionPhase.Complete)
                for (int t = 2; t <= 12; t++) if (e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0) want.Add(t);
            foreach (var t in new List<int>(_agents.Keys))
            {
                var ag = _agents[t];
                if (!want.Contains(t) || ag.Seat != shown) { _agents.Remove(t); ag.Leaving = true; ag.LeaveAt = Time.time + 3f; _leaving.Add(ag); }
            }
            foreach (var t in want)
            {
                if (_agents.ContainsKey(t)) continue;
                var spawn = _cand[shown] != null ? _cand[shown].transform.position : Site.Position;
                var a = Actor("paw" + shown + "_" + t, SeatColor(shown), new Vector3(spawn.x, GroundAt(spawn), spawn.z), 0f);
                a.name = "Catspaw " + t; Cap(a, SeatColor(shown));
                _agents[t] = new Agent { A = a, Track = t, Seat = shown };
            }
            foreach (var kv in _agents)
            {
                var pl = _places[kv.Key - 2];
                var spot = pl.ListenSpot;
                bool mv = MoveActor(kv.Value.A.transform, spot, YawTo(spot, pl.C + pl.Fwd), 11f, dt, pl.Ground);
                if (mv) MoversBusy++;
            }
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                var ag = _leaving[i];
                var home = _cand[Mathf.Max(0, ag.Seat)] != null ? _cand[ag.Seat].transform.position : Site.Position;
                MoveActor(ag.A.transform, home, 0f, 11f, dt, GroundAt(ag.A.transform.position));
                if (Time.time >= ag.LeaveAt || (ag.A.transform.position - home).sqrMagnitude < 2f) { Destroy(ag.A.gameObject); _leaving.RemoveAt(i); }
                else MoversBusy++;
            }
        }

        // ---- the candidate at the foot of the tower ----

        /// <summary>The ground point seen at a screen fraction of the view camera (the candidate and the aide stand there).</summary>
        Vector3 AnchorOnGround(float u, float v, float minDist, float maxDist)
        {
            var cam = ViewCamera;
            var tp = new Vector3(Site.Position.x, TowerGround, Site.Position.z);
            if (cam == null) return tp;
            var ray = cam.ViewportPointToRay(new Vector3(u, 1f - v, 0f));
            var pl = new Plane(Vector3.up, new Vector3(0, TowerGround, 0));
            Vector3 hit;
            if (pl.Raycast(ray, out float en) && en > 0f) hit = ray.GetPoint(en);
            else hit = ray.origin + new Vector3(ray.direction.x, 0, ray.direction.z).normalized * maxDist;
            var flat = hit - tp; flat.y = 0;
            float dd = flat.magnitude;
            if (dd > maxDist) flat = flat / dd * maxDist; else if (dd < minDist && dd > 0.01f) flat = flat / dd * minDist;
            var res = new Vector3(tp.x + flat.x, 0, tp.z + flat.z); res.y = GroundAt(res); return res;
        }

        Vector3 CandidateAnchor => AnchorOnGround(0.15f, 0.83f, 5f, 14f);
        Vector3 AideAnchor => AnchorOnGround(0.86f, 0.83f, 5f, 14f);

        void SyncCandidate(SettlementCouncil sc, ElectionState e, int shown, float dt)
        {
            bool cam = ViewCamera != null;
            var c = shown >= 0 ? _cand[shown] : null;
            float camYaw = cam ? ViewCamera.transform.eulerAngles.y : Site.Yaw;
            if (c != null && cam)
            {
                var anchor = CandidateAnchor;
                if ((c.transform.position - anchor).magnitude > 25f) c.transform.position = anchor;
                MoveActor(c.transform, anchor, camYaw + 40f, 9f, dt, anchor.y);
                bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
                if (mine && e.Phase == ElectionPhase.Ready && Time.time >= _beckonAt) { c.PlayOneShot(ActorKeys.Beckon); _beckonAt = Time.time + 3.4f; }
                // belt seal and petition roll ride on the candidate
                _seal.SetActive(true);
                var tr = c.transform;
                var cr = ViewCamera.transform.right; cr.y = 0; cr.Normalize();
                _seal.transform.position = tr.position + Vector3.up * 0.95f - cr * 0.42f;
                _seal.transform.rotation = Quaternion.Euler(0, ViewCamera.transform.eulerAngles.y, 0);
                bool sealOn = (e.Phase == ElectionPhase.Ready && e.HasAdvanced && mine) || (e.Phase == ElectionPhase.BustPending && mine);
                var sm = _seal.GetComponent<MeshFilter>(); var want = CouncilShapes.HeavySeal(sealOn); if (sm.sharedMesh != want) sm.sharedMesh = want;
                bool petVisible = e.PetitionAvailable && _carryT < 0f && !HoldPetition && mine;
                if (_petition.activeSelf != petVisible) _petition.SetActive(petVisible);
                if (petVisible) { _petition.transform.position = tr.position + Vector3.up * 0.95f + cr * 0.45f; _petition.transform.rotation = Quaternion.Euler(0, ViewCamera.transform.eulerAngles.y, 80f); }
            }
            else { _seal.SetActive(false); _petition.SetActive(false); }
            // the aide with the lantern: on a rival's turn (the way to the listening bench) and while carrying a petition
            bool rival = e.PlayerSeat >= 0 && e.Active != e.PlayerSeat && e.Phase != ElectionPhase.Complete;
            bool aideOn = cam && (rival || _carryT >= 0f);
            if (_aide.gameObject.activeSelf != aideOn) { _aide.gameObject.SetActive(aideOn); if (aideOn) _aide.transform.position = AideAnchor; }
            if (aideOn)
            {
                if (_carryT >= 0f && c != null)
                {
                    _carryT += dt / 2.2f;
                    var from = AideAnchor; var to = c.transform.position + c.transform.right * 1.0f;
                    _aide.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_carryT)));
                    _aide.transform.rotation = Quaternion.Euler(0, YawTo(from, to), 0);
                    MoversBusy++;
                    if (_carryT >= 1f) { _carryT = -1f; HoldPetition = false; }
                }
                else { var an = AideAnchor; MoveActor(_aide.transform, an, camYaw - 40f, 9f, dt, an.y); }
            }
        }

        // ---- proposals: the candidate points and the notes ----

        public static List<CouncilProposal> BuildProposals(ElectionState e)
        {
            var l = new List<CouncilProposal>();
            if (e.Phase != ElectionPhase.Choosing) return l;
            for (int i = 0; i < e.Pending.Count; i++)
            {
                var t = e.Pending[i];
                if (t.Mode == TrayMode.Both)
                {
                    var p = new CouncilProposal { Tray = i, Socket = -1 };
                    for (int s = 0; s < 2; s++)
                    {
                        if (t.Sockets[s].IsEmpty) continue;
                        int idx = p.Steps.FindIndex(x => x.Key == t.Sockets[s].Track);
                        if (idx >= 0) p.Steps[idx] = new KeyValuePair<int, int>(p.Steps[idx].Key, p.Steps[idx].Value + Mathf.Max(1, t.Sockets[s].Steps));
                        else p.Steps.Add(new KeyValuePair<int, int>(t.Sockets[s].Track, Mathf.Max(1, t.Sockets[s].Steps)));
                    }
                    if (p.Steps.Count > 0) l.Add(p);
                }
                else
                    for (int s = 0; s < 2; s++)
                    {
                        if (t.Sockets[s].IsEmpty) continue;
                        var p = new CouncilProposal { Tray = i, Socket = s };
                        p.Steps.Add(new KeyValuePair<int, int>(t.Sockets[s].Track, Mathf.Max(1, t.Sockets[s].Steps)));
                        l.Add(p);
                    }
            }
            return l;
        }

        void SyncProposals(SettlementCouncil sc, ElectionState e)
        {
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            var c = ActiveSeatShown >= 0 ? _cand[ActiveSeatShown] : null;
            if (!(mine && e.Phase == ElectionPhase.Choosing) || c == null || ViewCamera == null)
            {
                if (Proposals.Count > 0) Proposals = new List<CouncilProposal>();
                PointedProposal = -1; _pointPush = -1; c?.PointAt(null);
                foreach (var n in _notes) if (n.activeSelf) n.SetActive(false);
                return;
            }
            if (_pointPush != e.PushId || Proposals.Count == 0 || Proposals.Count != BuildProposals(e).Count)
            {
                Proposals = BuildProposals(e); _pointPush = e.PushId; _pointStart = Time.time; PointedProposal = -1;
                // first look: the lead of the first place in the first proposal speaks, the gatherings concerned react
                if (Proposals.Count > 0)
                {
                    var first = Proposals[0].Steps[0].Key;
                    bool op = _spoken.Add(first);
                    SayLead(first, CouncilApi.DisputeLine(sc, first, op ? CouncilContent.OpeningLine : CouncilContent.ProgressLine, _lineCounter++, e.Active, -1));
                }
            }
            float cycle = 2.6f;
            int count = Proposals.Count;
            if (count > 0)
            {
                float t = Time.time - _pointStart;
                int idx = Mathf.FloorToInt(t / cycle) % count;
                var pr = Proposals[idx];
                int sub = pr.Steps.Count > 1 ? (Mathf.FloorToInt(t / (cycle * 0.5f)) % 2) : 0;
                if (idx != PointedProposal) { PointedProposal = idx; foreach (var s in pr.Steps) { var pl = _places[s.Key - 2]; pl.ReactUntil = Time.time + cycle + 0.4f; Lead(s.Key).A.PlayOneShot(ActorKeys.Wave); } }
                int tr = pr.Steps[Mathf.Min(sub, pr.Steps.Count - 1)].Key;
                c.PointAt(_places[tr - 2].C + Vector3.up * 1.3f);
                foreach (var s in pr.Steps) _places[s.Key - 2].ReactUntil = Time.time + 0.5f;
            }
            // the folded notes held at the candidate's left hand, one per proposal, each with a tiny stamp of its places
            var ct = c.transform;
            for (int k = 0; k < _notes.Count; k++)
            {
                bool on = k < Proposals.Count;
                if (_notes[k].activeSelf != on) { _notes[k].SetActive(on); if (on) StampNote(_notes[k], Proposals[k]); }
                if (!on) continue;
                var camR = ViewCamera.transform.right; camR.y = 0; camR.Normalize();
                var pos = ct.position + Vector3.up * (1.35f + 0.55f * (k / 3)) + camR * (0.7f + 0.6f * (k % 3));
                _notes[k].transform.position = pos;
                var toCam = (ViewCamera.transform.position - pos).normalized;
                _notes[k].transform.rotation = Quaternion.FromToRotation(Vector3.up, toCam) * Quaternion.Euler(0, ViewCamera.transform.eulerAngles.y, 0);
                float pulse = k == PointedProposal ? 1.12f : 1f;
                _notes[k].transform.localScale = Vector3.one * pulse;
            }
        }

        void StampNote(GameObject note, CouncilProposal p)
        {
            for (int i = note.transform.childCount - 1; i >= 1; i--) Destroy(note.transform.GetChild(i).gameObject);
            int n = 0;
            foreach (var s in p.Steps)
            {
                int copies = Mathf.Max(1, s.Value);
                for (int c = 0; c < copies && n < 3; c++, n++)
                {
                    var m = CouncilShapes.Make("Stamp", note.transform, CouncilShapes.Prop(s.Key, true), new Vector3(-0.10f + 0.12f * n, 0.06f, -0.02f), 0.17f);
                    m.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                }
            }
        }

        // ================================================================== taps

        /// <summary>Everything tappable in the forward view with its screen position and finger radius, for the camera in use.</summary>
        public List<TapTarget> Targets(Camera cam)
        {
            var l = new List<TapTarget>();
            var sc = Council; var e = sc?.Election;
            if (cam == null || e == null || ActiveSeatShown < 0) return l;
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            float minR = Mathf.Max(18f, cam.pixelHeight * 0.06f);
            void Add(string name, Vector3 world, float worldRadius)
            {
                var sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0.1f) return;
                var edge = cam.WorldToScreenPoint(world + cam.transform.right * worldRadius);
                float r = Mathf.Max(minR, Mathf.Abs(edge.x - sp.x));
                l.Add(new TapTarget { Name = name, Px = new Vector2(sp.x, sp.y), RadiusPx = r, World = world });
            }
            var c = _cand[ActiveSeatShown];
            if (mine && e.Phase == ElectionPhase.Ready && c != null)
            {
                var chr = cam.transform.right; chr.y = 0; chr.Normalize();
                Add("hand", c.transform.position + Vector3.up * 1.75f + chr * 0.85f, 0.45f);
                foreach (var kv in _agents) Add("paw " + kv.Key, kv.Value.A.transform.position + Vector3.up * 1.0f, 0.7f);
                if (e.HasAdvanced)
                {
                    Add("seal", _seal.transform.position, 0.4f);
                    foreach (var kv in _agents) Add("lead " + kv.Key, Lead(kv.Key).A.transform.position + Vector3.up * 1.0f, 0.7f);
                }
            }
            if (mine && e.Phase == ElectionPhase.Choosing)
            {
                for (int k = 0; k < Proposals.Count && k < _notes.Count; k++) Add("note " + k, _notes[k].transform.position, 0.3f);
                var seen = new HashSet<int>();
                foreach (var p in Proposals) foreach (var s in p.Steps) if (seen.Add(s.Key)) Add("place " + s.Key, _places[s.Key - 2].C + Vector3.up * 0.9f, Mathf.Max(1.6f, _places[s.Key - 2].R * 0.5f));
            }
            if (mine && e.Phase == ElectionPhase.BustPending)
            {
                Add("seal", _seal.transform.position, 0.4f);
                if (e.PetitionAvailable && _petition.activeSelf) Add("petition", _petition.transform.position, 0.4f);
            }
            if (_aide.gameObject.activeSelf && _carryT < 0f) Add("lantern", _aide.transform.position + Vector3.up * 1.3f + _aide.transform.right * 0.45f, 0.6f);
            return l;
        }

        /// <summary>The target a tap at a screen point hits: the nearest whose finger circle holds the point (smallest ratio wins).</summary>
        public static string Pick(List<TapTarget> targets, Vector2 px)
        {
            string best = null; float bestK = 1f;
            foreach (var t in targets)
            {
                float k = (t.Px - px).magnitude / t.RadiusPx;
                if (k <= bestK) { bestK = k; best = t.Name; }
            }
            return best;
        }

        // ================================================================== camera framing

        /// <summary>The view that shows the given places from the tower (all eleven when empty): yaw, pitch down and vertical field of view in degrees.</summary>
        public void Frame(IList<int> tracks, float aspect, out float yaw, out float pitchDown, out float vfov)
        {
            var cp = CamPos;
            float mn = 999f, mx = -999f, dsum = 0f; int n = 0; float refB = float.NaN;
            void Take(int t)
            {
                var d = Site.Loc[t - 2] - cp; d.y = 0;
                float dist = Mathf.Max(8f, d.magnitude);
                float b = YawTo(cp, Site.Loc[t - 2]);
                if (float.IsNaN(refB)) refB = b;
                b = refB + Mathf.DeltaAngle(refB, b);
                float hw = Mathf.Atan(_places[t - 2].R / dist) * Mathf.Rad2Deg + 2f;
                mn = Mathf.Min(mn, b - hw); mx = Mathf.Max(mx, b + hw); dsum += dist; n++;
            }
            if (tracks == null || tracks.Count == 0) for (int t = 2; t <= 12; t++) Take(t);
            else foreach (var t in tracks) Take(t);
            yaw = (mn + mx) * 0.5f;
            float hfov = (mx - mn) + 16f;
            hfov = Mathf.Clamp(hfov, 40f, 100f);
            vfov = 2f * Mathf.Atan(Mathf.Tan(hfov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.5f, aspect)) * Mathf.Rad2Deg;
            vfov = Mathf.Clamp(vfov, 26f, 50f);
            float dm = dsum / Mathf.Max(1, n);
            pitchDown = Mathf.Atan((TowerH + 1.2f) / dm) * Mathf.Rad2Deg + 0.12f * vfov;
            pitchDown = Mathf.Clamp(pitchDown, 8f, 40f);
        }

        // ================================================================== views for tests

        /// <summary>Logical counts (settled, listening, undecided) of a place as the people stand towards the shown candidate.</summary>
        public void Counts(int track, out int settled, out int listening, out int undecided)
        {
            settled = listening = undecided = 0;
            foreach (var p in _places[track - 2].P) { if (p.S == PState.Settled) settled++; else if (p.S == PState.Listening) listening++; else undecided++; }
        }

        /// <summary>Null when every place's people and the catspaws match the engine state for the shown candidate.</summary>
        public string ViewMismatch(SettlementCouncil sc)
        {
            var e = sc.Election; if (e == null) return "no election";
            for (int t = 2; t <= 12; t++)
            {
                Expected(e, t, out int s, out int l);
                Counts(t, out int cs, out int cl, out int cu);
                if (cs != s || cl != l) return "track " + t + " settled " + cs + "/" + s + " listening " + cl + "/" + l;
                bool paw = _agents.ContainsKey(t);
                bool wantPaw = ShownSeat(e) == e.Active && e.Phase != ElectionPhase.Complete && e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0;
                if (paw != wantPaw) return "catspaw on track " + t + " shown " + paw + " state " + wantPaw;
            }
            return null;
        }
    }
}
