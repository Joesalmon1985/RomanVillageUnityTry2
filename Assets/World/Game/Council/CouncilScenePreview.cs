using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>Names of the eleven places, by track (2 to 12): words only, never their sums (WU-4, zone CNC-STATUS).</summary>
    public static class CouncilNames
    {
        public static readonly string[] PlaceByTrack = { "crossing", "boundary", "forest", "timber", "herbs", "grain", "tools", "shares", "water", "witness", "deed" };
        public static string Place(int track) => track >= 2 && track <= 12 ? PlaceByTrack[track - 2] : "somewhere";
    }

    /// <summary>
    /// WU-4 additions to the council village: the preview of a proposal (ghost catspaws, listeners turning, both places
    /// framed), the preview of a bank (the recorder lifts the seal, listeners step forward), the ladder, the recorder's
    /// table with its tablet, and the coloured ground rings. Presentation only: nothing here writes state.
    /// </summary>
    public sealed partial class CouncilScene
    {
        // ---- preview state (set by <see cref="CouncilAction"/> through the shared WorldAction machine)
        public CouncilProposal PreviewProposal { get; private set; }
        public bool PreviewBank { get; private set; }
        public bool PreviewLeave { get; private set; }
        public bool Previewing => PreviewProposal != null || PreviewBank || PreviewLeave;
        /// <summary>Ghost catspaws standing now (one per place the previewed proposal reaches).</summary>
        public int GhostCount { get { int n = 0; foreach (var g in _ghosts) if (g.activeSelf) n++; return n; } }
        /// <summary>Steps of the previewed proposal at a place (0 = not previewed there): a double shows 2.</summary>
        public int GhostSteps(int track) => _turn.TryGetValue(track, out int n) ? n : 0;
        /// <summary>People turned towards a previewed catspaw at a place (still undecided in the engine's terms).</summary>
        public int PreviewTurned(int track) { int n = 0; foreach (var p in _places[track - 2].P) if (p.Turned) n++; return n; }
        /// <summary>People who have stepped forward in a bank preview.</summary>
        public int PreviewStepped(int track) { int n = 0; foreach (var p in _places[track - 2].P) if (p.Stepped) n++; return n; }
        /// <summary>The places a preview frames (both places of a proposal, the places a bank would settle).</summary>
        public List<int> PreviewTracks { get; } = new List<int>();

        public void SetPreview(CouncilProposal p, bool bank, bool leave)
        {
            PreviewProposal = p; PreviewBank = bank; PreviewLeave = leave;
            RefreshPreview();
        }
        public void ClearPreview() => SetPreview(null, false, false);

        readonly Dictionary<int, int> _turn = new Dictionary<int, int>();
        readonly List<GameObject> _ghosts = new List<GameObject>();
        readonly List<GameObject> _stones = new List<GameObject>();
        readonly List<GameObject> _frames = new List<GameObject>();
        readonly Dictionary<int, Material> _ringMats = new Dictionary<int, Material>();
        Transform _fx;
        PlaceholderActor _recorder; GameObject _recTable, _tablet, _recSeal, _ladder, _ladderRing, _sealRing;
        readonly GameObject[] _claimSeals = new GameObject[3];
        float _recLift;

        // ---- the closer camera (Q-7)
        /// <summary>Metres the camera has moved from the tower top towards the worked places, and the direction (set by the encounter).</summary>
        public float CamPush; public Vector3 PushDir = Vector3.forward;
        /// <summary>Tests and captures: true / false forces the closer camera on or off; null = on at phone size.</summary>
        public static bool? ForceCloser;
        public static bool CloserCamera(float screenHeight) => ForceCloser ?? (screenHeight <= 520f);

        Material RingMat(Color c)
        {
            int key = ((Color32)c).GetHashCode();
            if (!_ringMats.TryGetValue(key, out var m) || m == null) { m = MaterialFactory.CreateUnlit(c, "Council ring"); _ringMats[key] = m; }
            return m;
        }

        GameObject Flat(string name, Transform parent, Mesh mesh, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = RingMat(c); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        void BuildWu4()
        {
            _fx = new GameObject("Council marks").transform; _fx.SetParent(Root, false);
            // the recorder at a stone table with a clay tablet and a seal, and the ladder: both stand where the camera's
            // lower middle meets the ground, so they are always in the picture
            _recorder = Actor("recorder", new Color(0.55f, 0.46f, 0.32f), Site.Position, 0f);
            _recorder.name = "Recorder";
            _recTable = Cube(Root, "Recorder table", Vector3.zero, new Vector3(1.9f, 0.75f, 0.8f), new Color(0.50f, 0.48f, 0.44f));
            _tablet = Cube(Root, "Clay tablet", Vector3.zero, new Vector3(0.72f, 0.09f, 0.52f), new Color(0.72f, 0.52f, 0.36f));
            _recSeal = CouncilShapes.Make("Recorder seal", Root, CouncilShapes.HeavySeal(false), Vector3.zero, 0.34f);
            for (int k = 0; k < 3; k++) _claimSeals[k] = CouncilShapes.Make("Claim seal " + k, Root, CouncilShapes.Seal(new Color(0.3f, 0.3f, 0.3f)), Vector3.zero, 0.26f);
            _ladder = new GameObject("Tower ladder").transform.gameObject; _ladder.transform.SetParent(Root, false);
            var wood = new Color(0.50f, 0.36f, 0.22f);
            Cube(_ladder.transform, "Rail L", new Vector3(-0.32f, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f), wood);
            Cube(_ladder.transform, "Rail R", new Vector3(0.32f, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f), wood);
            for (int k = 0; k < 6; k++) Cube(_ladder.transform, "Rung " + k, new Vector3(0, 0.3f + k * 0.38f, 0), new Vector3(0.64f, 0.07f, 0.08f), wood * 1.15f);
            _ladderRing = Flat("Ladder ring", _fx, CouncilShapes.GroundRing(), new Color(1f, 0.9f, 0.5f)); _ladderRing.SetActive(false);
            _sealRing = Flat("Seal ring", _fx, CouncilShapes.GroundRing(), new Color(1f, 0.9f, 0.5f)); _sealRing.SetActive(false);
            SetRecorderVisible(false);
            for (int t = 2; t <= 12; t++)
            {
                var pl = _places[t - 2];
                var f = Flat("Place frame " + t, _fx, CouncilShapes.GroundRing(0.93f), Color.white); f.SetActive(false); _frames.Add(f);
                foreach (var p in pl.P) { p.Ring = Flat("Ring " + p.A.name, _fx, CouncilShapes.GroundRing(), Color.white); p.Ring.SetActive(false); }
            }
        }

        void SetRecorderVisible(bool on)
        {
            foreach (var g in new[] { _recorder != null ? _recorder.gameObject : null, _recTable, _tablet, _recSeal, _ladder }) if (g != null && g.activeSelf != on) g.SetActive(on);
            foreach (var s in _claimSeals) if (s != null && s.activeSelf != on) s.SetActive(on);
        }

        /// <summary>Where the camera's anchor (u from the left, v from the top) meets the ground, measured from the camera's own foot.</summary>
        Vector3 CamFoot => new Vector3(CamPos.x, TowerGround, CamPos.z);

        void RefreshPreview()
        {
            _turn.Clear(); PreviewTracks.Clear();
            if (PreviewProposal != null)
                foreach (var s in PreviewProposal.Steps) { _turn[s.Key] = s.Value; if (!PreviewTracks.Contains(s.Key)) PreviewTracks.Add(s.Key); }
            else if (PreviewBank)
            {
                var e = Council?.Election;
                if (e != null) for (int t = 2; t <= 12; t++) if (e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0) PreviewTracks.Add(t);
            }
            // ghosts: one catspaw per place of the proposal, in the candidate's colour, standing where its catspaw would
            int seat = Mathf.Max(0, ActiveSeatShown);
            var col = Color.Lerp(SeatColor(seat), Color.white, 0.35f);
            int want = PreviewProposal != null ? PreviewProposal.Steps.Count : 0;
            while (_ghosts.Count < want)
            {
                var g = CouncilShapes.Make("Ghost catspaw", _fx, CouncilShapes.Catspaw(col), Vector3.zero, 1.8f, true);
                _ghosts.Add(g);
            }
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i]; bool on = i < want;
                if (g.activeSelf != on) g.SetActive(on);
                if (on) g.GetComponent<Renderer>().sharedMaterial = RingMat(col);
            }
            var stepsNeeded = 0; if (PreviewProposal != null) foreach (var s in PreviewProposal.Steps) stepsNeeded += s.Value;
            while (_stones.Count < stepsNeeded) _stones.Add(Flat("Ghost step", _fx, CouncilShapes.GroundDisc(), col));
            for (int i = 0; i < _stones.Count; i++) { bool on = i < stepsNeeded; if (_stones[i].activeSelf != on) _stones[i].SetActive(on); if (on) _stones[i].GetComponent<Renderer>().sharedMaterial = RingMat(col); }
        }

        /// <summary>Per frame: recorder, ladder, ghosts, rings. Called from <c>Sync</c>.</summary>
        void SyncWu4(SettlementCouncil sc, ElectionState e, int shown, float dt)
        {
            bool cam = ViewCamera != null && shown >= 0;
            SetRecorderVisible(cam);
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat && e.Phase != ElectionPhase.Complete;
            // a preview that no longer fits the state (the turn moved on) goes away by itself
            if (PreviewProposal != null && !(mine && e.Phase == ElectionPhase.Choosing)) ClearPreview();
            if (PreviewBank && !(mine && e.Phase == ElectionPhase.Ready && e.HasAdvanced)) ClearPreview();
            var seatCol = shown >= 0 ? SeatColor(shown) : Color.white;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            if (cam)
            {
                var rp = AnchorOnGround(0.47f, AnchorV, AnchorNear, AnchorFar);
                var cr = ViewCamera.transform.right; cr.y = 0; cr.Normalize();
                float yaw = ViewCamera.transform.eulerAngles.y;
                _recTable.transform.position = rp + Vector3.up * 0.38f; _recTable.transform.rotation = Quaternion.Euler(0, yaw, 0);
                _tablet.transform.position = rp + Vector3.up * 0.80f - cr * 0.5f; _tablet.transform.rotation = Quaternion.Euler(0, yaw, 0);
                _recLift = Mathf.MoveTowards(_recLift, PreviewBank ? 1f : 0f, dt * 3f);
                var sealRest = rp + Vector3.up * 0.82f + cr * 0.55f;
                var sealUp = rp + Vector3.up * 1.45f - cr * 0.2f;
                _recSeal.transform.position = Vector3.Lerp(sealRest, sealUp, _recLift);
                _recSeal.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var sm = _recSeal.GetComponent<MeshFilter>(); var want = CouncilShapes.HeavySeal(PreviewBank); if (sm.sharedMesh != want) sm.sharedMesh = want;
                var behind = rp - ViewCamera.transform.forward.normalized; behind.y = 0;
                _recorder.transform.position = new Vector3(rp.x, rp.y, rp.z) + new Vector3(ViewCamera.transform.forward.x, 0, ViewCamera.transform.forward.z).normalized * 1.0f;
                _recorder.SetYaw(yaw + 180f);
                _recorder.transform.rotation = Quaternion.Euler(0, yaw + 180f, 0);
                _recorder.Pose = PreviewBank ? ActorPose.Offer : ActorPose.None;
                // three carved seals on the rail: a lit one for each claim of the shown candidate
                int claims = e.ClaimsOf(shown);
                for (int k = 0; k < 3; k++)
                {
                    _claimSeals[k].transform.position = rp + Vector3.up * 1.15f + cr * (-0.45f + 0.45f * k) + new Vector3(ViewCamera.transform.forward.x, 0, ViewCamera.transform.forward.z).normalized * 0.25f;
                    _claimSeals[k].transform.rotation = Quaternion.Euler(0, yaw, 0);
                    var cm = _claimSeals[k].GetComponent<MeshFilter>(); var cw = CouncilShapes.Seal(k < claims ? seatCol : new Color(0.32f, 0.30f, 0.28f)); if (cm.sharedMesh != cw) cm.sharedMesh = cw;
                }
                var lp = AnchorOnGround(0.68f, AnchorV, AnchorNear, AnchorFar);
                _ladder.transform.position = lp; _ladder.transform.rotation = Quaternion.Euler(0, yaw, 0);
                _ladderRing.SetActive(PreviewLeave); _sealRing.SetActive(PreviewBank);
                if (PreviewLeave) { _ladderRing.transform.position = lp + Vector3.up * 0.05f; _ladderRing.transform.localScale = Vector3.one * (1.4f + 0.2f * pulse); }
                if (PreviewBank) { _sealRing.transform.position = rp + Vector3.up * 0.05f; _sealRing.transform.localScale = Vector3.one * (1.6f + 0.2f * pulse); }
            }
            // ghost catspaws and their steps
            if (PreviewProposal != null)
            {
                int gi = 0, si = 0;
                foreach (var st in PreviewProposal.Steps)
                {
                    var pl = _places[st.Key - 2];
                    var spot = pl.ListenSpot; if (_agents.ContainsKey(st.Key)) spot += pl.Right * 1.6f;
                    spot.y = pl.Ground + 0.02f;
                    var g = _ghosts[gi++];
                    g.transform.position = spot + Vector3.up * (0.02f + 0.08f * pulse);
                    g.transform.rotation = Quaternion.Euler(0, YawTo(spot, spot - pl.Right), 0);
                    for (int k = 0; k < st.Value; k++)
                    {
                        var s = _stones[si++];
                        s.transform.position = spot + pl.Fwd * (1.1f * (k + 1)) + Vector3.up * 0.04f;
                        s.transform.localScale = new Vector3(0.45f, 1f, 0.45f);
                    }
                }
            }
            // the coloured ground rings: settled people a filled disc, listeners an open ring, a frame round a place in play
            for (int t = 2; t <= 12; t++)
            {
                var pl = _places[t - 2];
                int claimed = e.Claimed[t - 2];
                var col = claimed >= 0 ? SeatColor(claimed) : seatCol;
                foreach (var p in pl.P)
                {
                    bool on = cam && (p.S == PState.Settled || p.S == PState.Listening || p.Turned || p.Stepped);
                    if (p.Ring.activeSelf != on) p.Ring.SetActive(on);
                    if (!on) continue;
                    bool filled = p.S == PState.Settled;
                    var mf = p.Ring.GetComponent<MeshFilter>(); var wm = filled ? CouncilShapes.GroundDisc() : CouncilShapes.GroundRing();
                    if (mf.sharedMesh != wm) mf.sharedMesh = wm;
                    p.Ring.GetComponent<Renderer>().sharedMaterial = RingMat(p.S == PState.Listening || p.Turned ? Color.Lerp(col, Color.white, 0.25f) : col);
                    var fp = p.A.transform.position; p.Ring.transform.position = new Vector3(fp.x, fp.y + 0.04f, fp.z);
                    p.Ring.transform.localScale = Vector3.one * (filled ? 0.5f : 0.65f);
                }
                bool inPlay = cam && (PreviewTracks.Contains(t) || _agents.ContainsKey(t) || claimed >= 0);
                var fr = _frames[t - 2];
                if (fr.activeSelf != inPlay) fr.SetActive(inPlay);
                if (inPlay)
                {
                    bool prev = PreviewTracks.Contains(t);
                    fr.GetComponent<Renderer>().sharedMaterial = RingMat(prev ? Color.Lerp(col, Color.white, 0.4f + 0.3f * pulse) : (claimed >= 0 ? col : col * 0.8f));
                    fr.transform.position = new Vector3(pl.C.x, pl.Ground + 0.05f, pl.C.z);
                    fr.transform.localScale = Vector3.one * (pl.R + 3.2f);
                }
            }
            // a ring under each catspaw
            foreach (var kv in _agents)
            {
                // drawn through the place frame; the catspaw itself keeps its coloured head band
            }
        }

        /// <summary>The ground point under the camera is the reference for the candidate's anchors (the tower foot when not pushed).</summary>
        static Vector3 FlatDir(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
    }
}
