using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>
    /// Ward duel ("ward_mira", PRD 4.2; "ward_campaign", PRD_CRYSTALS 5.12 / I7). Setup: pick a secret three-locus Ward.
    /// Active: build weaves and cast while the opponent casts on its own cadence; both histories show the three totals
    /// only. Result: outcome and both Wards revealed, Rematch (R) or Return (Enter/Esc). Rules live in WardDuelSession;
    /// this class draws, animates and forwards input. Keys and buttons call the same public methods (AddEssence,
    /// RemoveLast, LockWard, Cast, Rematch, Return, BeginWard, Surrender, ToggleHistory), which tests use.
    /// <para>
    /// With no request, or a <see cref="EncounterKind.Practice"/> request, it is the practice duel exactly as shipped: the
    /// player's own wizard against Mira, results into <c>WardProgress</c>. With a <see cref="WardDuelRequest"/> in
    /// <see cref="EncounterKind.Lethal"/> or <see cref="EncounterKind.Friendly"/> mode the player casts through Mira against
    /// the request's opponent (profile timing and pool, <see cref="WardOpponents"/>), staged from the request's
    /// <see cref="DuelStageSpec"/>. Lethal: the stakes card first ("X will hold the way out"), no rematch, the confirmed
    /// "Leave encounter" is a surrender, and the result goes out once as <c>ResolveEncounter</c> at the end event
    /// (<see cref="CampaignDuel"/>), before the presentation hold. Friendly: no stakes, no attempt, nothing recorded.
    /// </para>
    /// </summary>
    public sealed class WardDuelEncounter : IEncounter
    {
        public enum Screen { Setup, Active, Result, Stakes }

        /// <summary>Test hooks: replace the tuning / seed of the next duels (null = normal). In campaign modes the override replaces the profile's tuning.</summary>
        public static WardTuning TuningOverride;
        public static ulong? SeedOverride;
        /// <summary>The encounter currently running (for tests and AutoPlay).</summary>
        public static WardDuelEncounter Current { get; private set; }

        public const float DuelDistance = 5.5f;
        public const int IdleWarnMs = 15000;
        /// <summary>The presentation hold after a final break before the result screen (PRD_CHARACTERS 7.4).</summary>
        public const float BreakHoldSeconds = 1.6f;
        /// <summary>The hold after any other ending (stalemate, surrender).</summary>
        public const float EndHoldSeconds = 0.8f;
        /// <summary>The hold after a lethal loss: the volunteer steps into the gap before the result screen.</summary>
        public const float VolunteerHoldSeconds = 3.0f;
        /// <summary>The duel's camera in campaign modes (narrower than the walking view so faces read on a phone).</summary>
        public const float CampaignFieldOfView = 48f;

        public string Title => IsCampaignMode ? CasterName + "'s Ward against " + OpponentName : "Ward duel with Mira";
        public bool IsFinished { get; private set; }
        public Screen Phase { get; private set; }
        public WardTuning Tuning { get; private set; }
        public WardDuelSession Session { get; private set; }
        public Essence[] Pool { get; private set; }
        public ulong Seed { get; private set; }
        public IReadOnlyList<int> Building => _building;
        /// <summary>The player's Ward of the current duel (known to the player), null during setup.</summary>
        public int[] PlayerWard { get; private set; }
        public WardOutcome LastOutcome { get; private set; }
        /// <summary>True once the current duel's result went into WardProgress (practice only).</summary>
        public bool Recorded { get; private set; }
        public int DuelsPlayed { get; private set; }
        public bool HelpVisible => _firstTimer && (Phase == Screen.Setup || Phase == Screen.Active);
        public WardDuelStage Stage => _stage;
        public string Toast => Time.unscaledTime < _toastUntil ? _toast : null;

        // ---- campaign modes (I7)
        /// <summary>The request this duel was started with (null for the plain practice duel).</summary>
        public WardDuelRequest Request { get; private set; }
        public EncounterKind Mode => _campaign ? Request.Mode : EncounterKind.Practice;
        public bool IsCampaignMode => _campaign;
        public bool IsLethal => _campaign && Request.Mode == EncounterKind.Lethal;
        public bool IsFriendly => _campaign && Request.Mode == EncounterKind.Friendly;
        /// <summary>The opponent's profile (campaign modes), else null.</summary>
        public WardOpponentProfile Profile { get; private set; }
        /// <summary>Who casts for the player: "Mira" in campaign modes, "You" in practice.</summary>
        public string CasterName => _campaign ? "Mira" : "You";
        /// <summary>The opponent's short name ("Dolm", "Orrin", "Mira").</summary>
        public string OpponentName { get; private set; } = "Mira";
        /// <summary>The opponent's full name ("Dolm the Tally-Keeper").</summary>
        public string OpponentFullName { get; private set; } = "Mira";
        /// <summary>The stakes line shown before a lethal duel, null otherwise.</summary>
        public string StakesText { get; private set; }
        /// <summary>The campaign result this duel produced (lethal), null until the end event.</summary>
        public DuelResult? Resolution { get; private set; }
        /// <summary>True when this duel's end event sent ResolveEncounter (lethal; once per attempt).</summary>
        public bool ResolutionSent { get; private set; }
        /// <summary>The player chose "Leave encounter" and confirmed (lethal: counts as a defeat).</summary>
        public bool Surrendered { get; private set; }
        /// <summary>Unscaled time when the result screen may show and be left (the presentation hold).</summary>
        public float HoldUntil { get; private set; }
        public bool HoldOver => Time.unscaledTime >= HoldUntil;
        /// <summary>Both histories expanded (compact layout; a tap on either side card).</summary>
        public bool HistoryExpanded { get; private set; }
        /// <summary>The struck cue on screen now ("Dolm's Ward struck: 1 fracture"), or null.</summary>
        public string StruckCue => Time.unscaledTime < _cueUntil ? _cue : null;
        /// <summary>Every struck cue shown in this encounter, in order (tests: no locus is ever named).</summary>
        public readonly List<string> CueLog = new List<string>();
        /// <summary>The layout used by the last GUI pass (tests, captures).</summary>
        public DuelLayout LastLayout { get; private set; }

        // ---- WU-7: the world objects (essence stones, hand, beads, tally sticks, referee, gate)
        /// <summary>The duel's world objects; hidden while the Clear view setting is on (then <see cref="DuelLayout"/> draws the panel).</summary>
        public DuelWorldProps Props { get; private set; }
        /// <summary>True when the world objects (not the screen panel) carry the duel: <see cref="GodBoardSettings.ClearView"/> is off.</summary>
        public bool WorldFirst => !GodBoardSettings.ClearView;
        private readonly List<Vector2> _tapQueue = new List<Vector2>();

        private readonly List<int> _building = new List<int>();
        private EncounterContext _ctx;
        private GameObject _root;
        private WardDuelStage _stage;
        private EncounterSfx _sfx;
        private Npc _mira;
        private double _msAcc;
        private int _beginFrame;
        private bool _menuWasOpen;
        private int _lastPlayerCastMs;
        private bool _firstTimer;
        private bool _prevPan = true;
        private string _toast; private float _toastUntil;
        private bool _lightJustUnlocked;
        private bool _campaign;
        private bool _miraHidden;
        private float _prevFov = -1f;
        private Color _cueColor;
        private string _cue; private float _cueUntil;

        private WorldState State => _ctx != null && _ctx.Sim != null ? _ctx.Sim.State : null;

        // ------------------------------------------------------------------ lifecycle

        public void Begin(EncounterContext ctx)
        {
            _ctx = ctx;
            Current = this;
            _beginFrame = Time.frameCount;
            Request = ctx.Request as WardDuelRequest;
            _campaign = Request != null && Request.Mode != EncounterKind.Practice;
            Tuning = TuningOverride ?? EncounterTuningFiles.Ward();
            _root = new GameObject("WardDuel");
            if (ctx.World != null) _root.transform.SetParent(ctx.World.transform, false);
            _sfx = EncounterSfx.Create(_root.transform);

            if (_campaign) BeginCampaign(ctx);
            else
            {
                _mira = ctx.Npc != null ? ctx.Npc : Npc.Find(NpcIds.Mira);
                Vector3 miraPos = _mira != null ? _mira.HomePosition : ctx.Stage.Position;
                Vector3 fwd = _mira != null ? BoardSpace.DirOf(_mira.HomeYaw) : ctx.Stage.Forward;
                Vector3 playerPos = miraPos + fwd * DuelDistance;
                playerPos.y = miraPos.y;
                _stage = WardDuelStage.Build(_root.transform, playerPos, _mira != null ? _mira.Actor : null, _sfx);

                // P2b: the same wide over-the-shoulder framing as the campaign duels (both wizards, Wards and posts in frame).
                FrameDuelCamera(ctx, playerPos, miraPos);
            }
            _stage.Struck += OnStruck;
            if (ctx.World != null && ctx.World.GodCam != null) { _prevPan = ctx.World.GodCam.KeyboardPan; ctx.World.GodCam.KeyboardPan = false; }

            _firstTimer = State == null || State.Ward == null || State.Ward.CompletedDuelsVsMira == 0;
            NewDuel();
            LedgerCard.DockTop = true; // the referee's card is a strip along the top edge, never over the stage
            Props = DuelWorldProps.Build(this, _root.transform, ctx.FirstPersonCamera != null ? ctx.FirstPersonCamera : Camera.main);
        }

        private void BeginCampaign(EncounterContext ctx)
        {
            Profile = CampaignDuel.Opponents().GetOrDefault(Request.ProfileId);
            OpponentFullName = !string.IsNullOrEmpty(Request.OpponentName) ? Request.OpponentName : Profile?.DisplayName ?? "the opponent";
            OpponentName = !string.IsNullOrEmpty(Profile?.DisplayName) && OpponentFullName.StartsWith(Profile.DisplayName) ? Profile.DisplayName : CampaignDuel.FirstName(OpponentFullName);
            StakesText = CampaignDuel.StakesLine(Request);
            var spec = Request.Stage ?? CampaignDuel.StageNearPlayer(Request.ProfileId, null);
            if (string.IsNullOrEmpty(spec.CasterActorId)) spec.CasterActorId = NpcIds.Mira;

            // Mira's camp body steps aside while her duel body (same actor id, from the factory) stands on the caster mark.
            _mira = Npc.Find(NpcIds.Mira);
            if (_mira != null && _mira.Actor != null) { _mira.Actor.SetVisible(false); _miraHidden = true; }

            _stage = WardDuelStage.BuildCampaign(_root.transform, spec, ColourOf(spec.OpponentActorId, 0.35f), ColourOf(spec.VolunteerActorId, 0.6f), _sfx);
            // Pacing: the player's inputs animate Mira with a short wind-up; the opponent's tempo comes from its profile.
            _stage.CasterWindUp = 0.25f;
            _stage.OpponentWindUp = WindUpFor(Profile);

            FrameDuelCamera(ctx, spec.CasterPos, spec.OpponentPos);
        }

        /// <summary>Seconds between an opponent's cast and the bolt leaving its hands, from the profile's tempo and animation length.</summary>
        public static float WindUpFor(WardOpponentProfile p)
        {
            if (p == null) return 0.35f;
            float k;
            switch (p.CastTempo)
            {
                case "slow": k = 0.40f; break;
                case "brisk": k = 0.22f; break;
                case "hasty": k = 0.15f; break;
                default: k = 0.30f; break;
            }
            return Mathf.Clamp(p.CastAnimMs / 1000f * k, 0.12f, 0.8f);
        }

        /// <summary>A stable placeholder colour for an actor id (keepers, companions) until the cast package gives bodies.</summary>
        public static Color ColourOf(string id, float sat)
        {
            int h = 7;
            foreach (char c in id ?? "") h = h * 31 + c;
            float hue = ((h & 0x7fffffff) % 360) / 360f;
            return Color.HSVToRGB(hue, sat, 0.75f);
        }

        /// <summary>
        /// Over Mira's shoulder: the camera behind and to her right, a little above her head, looking past her at the
        /// opponent, so Mira is left of centre and the opponent's face in the right half; widened (pulled back and turned)
        /// until the volunteer at the way out is in frame too.
        /// </summary>
        private void FrameDuelCamera(EncounterContext ctx, Vector3 casterPos, Vector3 opponentPos)
        {
            var cam = ctx.FirstPersonCamera;
            if (cam != null) { _prevFov = cam.fieldOfView; cam.fieldOfView = CampaignFieldOfView; }
            float aspect = cam != null && cam.aspect > 0.1f ? cam.aspect : UnityEngine.Screen.width / (float)Mathf.Max(1, UnityEngine.Screen.height);
            float halfH = Mathf.Atan(Mathf.Tan(CampaignFieldOfView * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg - 5f;
            Vector3 caster = casterPos, opp = opponentPos;
            var f = opp - caster; f.y = 0; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, f).normalized;
            // Who must be in frame: Mira, the opponent, and the volunteer standing by.
            var pts = new List<Vector3> { caster, opp };
            if (_stage.VolunteerActor != null) pts.Add(_stage.VolunteerStandBy);
            Vector3 eye = default; float lo = 0, hi = 0;
            for (int pass = 0; pass < 8; pass++)
            {
                float back = CameraBack + pass * 0.6f;
                eye = caster - f * back + right * 1.3f + Vector3.up * 2.2f;
                lo = float.MaxValue; hi = float.MinValue;
                foreach (var p in pts)
                {
                    var d = p - eye; d.y = 0;
                    float a = Vector3.SignedAngle(f, d, Vector3.up);
                    lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a);
                }
                if (hi - lo <= 2f * halfH) break;
            }
            // The rightmost figure (usually the opponent) a little right of centre, so Mira stands left of centre; but the
            // leftmost figure stays in frame.
            float yawOff = hi - 0.22f * halfH;
            if (lo < yawOff - 0.95f * halfH) yawOff = (lo + hi) * 0.5f;
            var look = Quaternion.Euler(0, yawOff, 0) * f;
            var mid = Vector3.Lerp(caster, opp, 0.6f) + Vector3.up * 1.15f;
            float dist = Vector3.Dot(mid - eye, look);
            var target = eye + look * Mathf.Max(2f, dist);
            target.y = mid.y;
            var dd = target - eye;
            float yaw = BoardSpace.YawOf(dd);
            float pitch = -Mathf.Atan2(dd.y, new Vector2(dd.x, dd.z).magnitude) * Mathf.Rad2Deg;
            // P2b: the stage sits in the lower middle of the picture: the top strip is for the referee's docked card, the
            // bottom row for the stones and the hand.
            pitch -= StageLiftDegrees;
            ctx.PlaceAvatar(eye - Vector3.up * WorldBootstrap.EyeHeight, yaw, pitch);
        }

        /// <summary>Degrees the duel camera looks up from the stage, so the wizards sit below the docked card.</summary>
        public const float StageLiftDegrees = 4f;
        /// <summary>Metres behind the caster the duel camera starts (the whole caster, feet included, reads above the stones).</summary>
        public const float CameraBack = 4.6f;

        public void End()
        {
            if (Session != null && Session.Phase != WardPhase.Complete)
            {
                // Leaving mid-duel without the confirmed surrender (a reload, a new game, a host abort): practice and friendly
                // forfeit with nothing recorded; a lethal attempt stays pending and continues by RestartAttempt, no penalty.
                Session.Forfeit();
                if (IsLethal) Debug.Log("[CampaignDuel] left attempt " + Request.AttemptId + " without a result: it continues with a new seed");
            }
            if (_ctx != null && _ctx.World != null && _ctx.World.GodCam != null) _ctx.World.GodCam.KeyboardPan = _prevPan;
            if (_ctx != null && _ctx.FirstPersonCamera != null && _prevFov > 0f) _ctx.FirstPersonCamera.fieldOfView = _prevFov;
            if (_stage != null) _stage.Struck -= OnStruck;
            LedgerCard.DockTop = false;
            if (Props != null) Props.Machine.Cancel();
            if (_miraHidden && _mira != null && _mira.Actor != null) _mira.Actor.SetVisible(true);
            if (!_campaign && _mira != null && _mira.Actor != null) { _mira.Actor.PlayIdle(); _mira.Actor.FaceTowards(_mira.HomePosition + BoardSpace.DirOf(_mira.HomeYaw)); }
            if (_root != null) Object.Destroy(_root);
            if (Current == this) Current = null;
        }

        private void NewDuel()
        {
            var s = State;
            int completed = s != null && s.Ward != null ? s.Ward.CompletedDuelsVsMira : 0;
            if (_campaign)
            {
                ulong seed = SeedOverride ?? CampaignSeed(s);
                var spec = CampaignDuel.Opponents().Spec(Request.ProfileId, seed, completed, EncounterTuningFiles.Ward());
                if (TuningOverride != null) spec.Tuning = TuningOverride;
                Tuning = spec.Tuning;
                Pool = spec.Pool;
                Seed = seed;
                Session = WardDuelSession.Create(spec);
            }
            else
            {
                Tuning = TuningOverride ?? EncounterTuningFiles.Ward(); // re-read per duel: ward.json edits apply on rematch
                Pool = WardProgressApi.NextPool(s, Tuning);
                Seed = SeedOverride ?? new Rng(s != null ? s.Seed : 1UL).Fork("ward_duel_" + completed + "_" + DuelsPlayed).NextULong();
                Session = new WardDuelSession(Tuning, Pool, Seed);
            }
            Phase = IsLethal && DuelsPlayed == 0 ? Screen.Stakes : Screen.Setup;
            _building.Clear();
            PlayerWard = null;
            Recorded = false;
            LastOutcome = WardOutcome.None;
            Resolution = null; ResolutionSent = false; HoldUntil = 0f;
            _lastPlayerCastMs = 0;
            _msAcc = 0;
            _stage.ResetWards();
            _stage.PlayerActor.PlayIdle();
            if (_lightJustUnlocked && Pool.Length > 3) ShowToast("New essence: Light joins the pool.", 4f);
            _lightJustUnlocked = false;
        }

        private ulong CampaignSeed(WorldState s)
        {
            // Lethal: the attempt's seed (a restart brings a new one). Friendly rematches fork it.
            ulong baseSeed = Request.Seed != 0 ? Request.Seed : new Rng(s != null ? s.Seed : 1UL).Fork("ward_friendly_" + (Request.ProfileId ?? "")).NextULong();
            return DuelsPlayed == 0 ? baseSeed : new Rng(baseSeed).Fork("rematch_" + DuelsPlayed).NextULong();
        }

        // ------------------------------------------------------------------ actions (keys, buttons and tests)

        /// <summary>Lethal duels: past the stakes card to setting Mira's Ward.</summary>
        public bool BeginWard()
        {
            if (Phase != Screen.Stakes) return false;
            Phase = Screen.Setup;
            return true;
        }

        /// <summary>Adds an essence to the Ward (setup) or weave (active). False if full or not in this duel's pool.</summary>
        public bool AddEssence(Essence e)
        {
            if (Phase == Screen.Result || Phase == Screen.Stakes || _building.Count >= Tuning.Loci) return false;
            bool inPool = false;
            foreach (var p in Pool) if (p == e) inPool = true;
            if (!inPool) return false;
            _building.Add((int)e);
            return true;
        }

        public bool RemoveLast()
        {
            if (Phase == Screen.Result || Phase == Screen.Stakes || _building.Count == 0) return false;
            _building.RemoveAt(_building.Count - 1);
            return true;
        }

        /// <summary>Locks the secret Ward and starts the duel clock.</summary>
        public bool LockWard()
        {
            if (Phase != Screen.Setup) return false;
            if (_building.Count != Tuning.Loci) { ShowToast("Pick " + Tuning.Loci + " essences for " + (_campaign ? "Mira's" : "your") + " Ward first."); return false; }
            var ward = _building.ToArray();
            if (!Session.SetPlayerWard(ward)) { ShowToast("That Ward is not allowed."); return false; }
            PlayerWard = ward;
            _building.Clear();
            Phase = Screen.Active;
            _msAcc = 0;
            return true;
        }

        /// <summary>Casts the weave being built. Returns the session's result (null if not in the active phase).</summary>
        public CastResult Cast()
        {
            if (Phase != Screen.Active) return null;
            if (_building.Count != Tuning.Loci) { ShowToast("Build a weave of " + Tuning.Loci + " essences, then cast."); return new CastResult { Ok = false, Reason = WardReasons.BadWeave, Message = "incomplete weave" }; }
            var r = Session.PlayerCast(_building.ToArray());
            if (r.Ok) _building.Clear();
            else ShowToast(r.Reason == WardReasons.Recovering ? "Still recovering..." : r.Message);
            ProcessEvents();
            return r;
        }

        /// <summary>Takes the orb at this place in the hand back (a tap on a socket). False if there is none.</summary>
        public bool RemoveAt(int index)
        {
            if (Phase == Screen.Result || Phase == Screen.Stakes || index < 0 || index >= _building.Count) return false;
            _building.RemoveAt(index);
            return true;
        }

        /// <summary>A short line from the referee or the world (the toast).</summary>
        public void Say(string line, float seconds = 2.2f) => ShowToast(line, seconds);

        /// <summary>A tap at a screen point (pixels, origin bottom-left) on the world objects; tests and captures use this. Nothing in the Clear view.</summary>
        public DuelTarget TapAt(Vector2 screen) => Props != null && WorldFirst ? Props.TapAt(screen) : DuelTarget.None;

        /// <summary>Queues a tap for the next frame, as the pointer would deliver it.</summary>
        public void InjectTap(Vector2 screen) => _tapQueue.Add(screen);

        private void PollTaps(bool blocked)
        {
            if (Input.GetMouseButtonDown(0)) _tapQueue.Add(Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++) { var t = Input.GetTouch(i); if (t.phase == TouchPhase.Began) _tapQueue.Add(t.position); }
            if (_tapQueue.Count == 0) return;
            var taps = _tapQueue.ToArray(); _tapQueue.Clear();
            if (blocked || !WorldFirst) return;
            foreach (var tp in taps) { TapAt(tp); if (IsFinished) return; }
        }

        /// <summary>A new duel against the same opponent. Never in a lethal duel (one attempt, one result).</summary>
        public bool Rematch()
        {
            if (Phase != Screen.Result || IsLethal) return false;
            DuelsPlayed++;
            NewDuel();
            return true;
        }

        /// <summary>Back to the world. In campaign modes only after the presentation hold.</summary>
        public bool Return()
        {
            if (Phase != Screen.Result) return false;
            if (_campaign && !HoldOver) return false;
            IsFinished = true;
            return true;
        }

        /// <summary>
        /// "Leave encounter", confirmed. Lethal: the duel is forfeited and counts as a defeat (Surrender), resolved at once;
        /// the result screen shows the volunteer holding the way. Practice and friendly: leaving is harmless (as today).
        /// </summary>
        public bool Surrender()
        {
            if (IsFinished) return false;
            if (!IsLethal)
            {
                if (EncounterHost.Instance != null && EncounterHost.Instance.Current == this) EncounterHost.Instance.Abort();
                return true;
            }
            if (Phase == Screen.Result) return false;
            Surrendered = true;
            Session.Forfeit();
            ProcessEvents();
            return true;
        }

        /// <summary>Expands or folds both histories (compact layout; a tap on a side card).</summary>
        public void ToggleHistory() => HistoryExpanded = !HistoryExpanded;

        // ------------------------------------------------------------------ frame

        public void Tick(float unscaledDt)
        {
            if (IsFinished) return;
            bool menu = Hud.MenuOpen;
            bool blocked = menu || _menuWasOpen || Time.frameCount == _beginFrame;
            _menuWasOpen = menu;
            if (Phase == Screen.Active && !menu)
            {
                _msAcc += unscaledDt * 1000.0;
                int step = (int)_msAcc;
                _msAcc -= step;
                if (step > 0) Session.Advance(step);
            }
            ProcessEvents();
            _stage.SetGathering(Phase == Screen.Active ? _building : null);
            if (Props != null) Props.Tick(unscaledDt);
            PollTaps(blocked || Phase == Screen.Stakes);
            if (!blocked) ReadKeys();
        }

        private void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.H)) GodBoardSettings.ClearView = !GodBoardSettings.ClearView; // Clear view: the screen panel instead of the world objects
            if (Phase == Screen.Result)
            {
                if (Input.GetKeyDown(KeyCode.R)) Rematch();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Return();
                else if (Input.GetKeyDown(KeyCode.Escape)) { EncounterKeys.ConsumeEscape(); Return(); }
                return;
            }
            if (Phase == Screen.Stakes)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) BeginWard();
                return;
            }
            for (int i = 0; i < Pool.Length && i < 4; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) AddEssence(Pool[i]);
            if (Input.GetKeyDown(KeyCode.Backspace)) RemoveLast();
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (Phase == Screen.Setup && enter) LockWard();
            else if (Phase == Screen.Active && (enter || Input.GetKeyDown(KeyCode.Space))) Cast();
        }

        private void ProcessEvents()
        {
            if (Session == null) return;
            foreach (var ev in Session.DrainEvents())
            {
                if (ev is PlayerCastEvent pc)
                {
                    _lastPlayerCastMs = pc.AtMs;
                    _stage.Cast(true, pc.Entry.Weave, pc.Entry.Fractures, pc.Entry.Echoes, pc.Entry.Fractures >= Tuning.Loci);
                    if (pc.Auto) ShowToast("No cast for " + (Tuning.IdleAutoCastMs / 1000) + " s: a random weave was cast for " + (_campaign ? "Mira." : "you."), 3.5f);
                }
                else if (ev is RivalCastEvent rc)
                {
                    _stage.Cast(false, rc.Entry.Weave, rc.Entry.Fractures, rc.Entry.Echoes, rc.Entry.Fractures >= Tuning.Loci);
                }
                else if (ev is DuelEndedEvent de) OnEnded(de.Outcome);
            }
            if (Session.Phase == WardPhase.Complete && Phase != Screen.Result) OnEnded(Session.Outcome);
        }

        /// <summary>
        /// The duel's end event. Guarded: a second end event for the same duel does nothing, and the campaign result is
        /// sent at most once per attempt (<see cref="CampaignDuel.SendResolve"/>).
        /// </summary>
        internal void OnEnded(WardOutcome o)
        {
            if (Phase == Screen.Result) return;
            Phase = Screen.Result;
            LastOutcome = o;
            _building.Clear();
            if (_campaign) { OnCampaignEnded(o); return; }
            if (o != WardOutcome.Forfeited && !Recorded)
            {
                int before = State != null && State.Ward != null ? State.Ward.CompletedDuelsVsMira : 0;
                Recorded = WardProgressApi.RecordDuel(State, o);
                int after = State != null && State.Ward != null ? State.Ward.CompletedDuelsVsMira : 0;
                _lightJustUnlocked = before < Tuning.LightUnlockAfterDuels && after >= Tuning.LightUnlockAfterDuels;
                if (Recorded) _firstTimer = false;
            }
            if (o == WardOutcome.Victory) { _stage.PlayerActor.PlayOneShot(ActorKeys.Cheer); _stage.MiraActor?.PlayOneShot(ActorKeys.Surprise); }
            else if (o == WardOutcome.Defeat) { _stage.MiraActor?.PlayOneShot(ActorKeys.Cheer); _stage.PlayerActor.PlayOneShot(ActorKeys.Surprise); }
            else { _stage.MiraActor?.PlayOneShot(ActorKeys.Clap); }
        }

        /// <summary>Tests: a second (duplicate) end event for the current duel, as a stray event or a focus switch might raise.</summary>
        public void SimulateEndEvent(WardOutcome o) => OnEnded(o);

        private void OnCampaignEnded(WardOutcome o)
        {
            bool broken = o == WardOutcome.Victory || o == WardOutcome.Defeat || o == WardOutcome.Clash;
            HoldUntil = Time.unscaledTime + (broken ? BreakHoldSeconds : EndHoldSeconds);
            if (IsLethal && DuelResults.TryFrom(o, out var r))
            {
                Resolution = r;
                // Sent now, at the end event, before the presentation hold (PRD_CRYSTALS 5.6 step 3).
                ResolutionSent = CampaignDuel.SendResolve(Request.AttemptId, r);
                if ((r == DuelResult.Defeat || r == DuelResult.Surrender) && _stage.VolunteerActor != null)
                {
                    _stage.VolunteerHolds();
                    HoldUntil = Mathf.Max(HoldUntil, Time.unscaledTime + VolunteerHoldSeconds); // let the step into the gap be seen
                }
            }
            if (o != WardOutcome.Forfeited) _firstTimer = false;
            if (o == WardOutcome.Victory) _stage.CasterActor.PlayOneShot(ActorKeys.Cheer);
            else if (o == WardOutcome.Defeat || o == WardOutcome.Forfeited) _stage.OpponentActor?.PlayOneShot(ActorKeys.Cheer);
        }

        private void OnStruck(bool byCaster, int fractures, int echoes, bool broken)
        {
            if (_stage == null) return;
            string whose = byCaster ? Possessive(OpponentName) : (_campaign ? "Mira's" : "Your");
            string what = broken ? " Ward breaks!" : " Ward struck";
            string totals = broken ? "" : ":  " + fractures + (fractures == 1 ? " fracture" : " fractures") + ",  " + echoes + (echoes == 1 ? " echo" : " echoes");
            _cue = whose + what + totals;
            _cueColor = byCaster ? (_campaign ? WardDuelStage.OpponentWardColor : WardDuelStage.MiraWardColor) : WardDuelStage.PlayerWardColor;
            _cueUntil = Time.unscaledTime + (broken ? 2.5f : 1.6f);
            CueLog.Add(_cue);
        }

        public static string Possessive(string name) => string.IsNullOrEmpty(name) ? "The opponent's" : name.EndsWith("s") ? name + "'" : name + "'s";

        private void ShowToast(string s, float seconds = 2.2f) { _toast = s; _toastUntil = Time.unscaledTime + seconds; }

        /// <summary>Milliseconds until the idle auto-cast (W11), or -1 when not counting.</summary>
        public int MsUntilAutoCast
        {
            get
            {
                if (Phase != Screen.Active || Session.PlayerCastsLeft <= 0) return -1;
                return Mathf.Max(0, _lastPlayerCastMs + Tuning.IdleAutoCastMs - Session.ElapsedMs);
            }
        }

        // ------------------------------------------------------------------ GUI

        public void DrawGui()
        {
            if (Session == null) return;
            var L = LastLayout = DuelLayout.ForScreen(HelpVisible);
            EncounterUi.CompactUnit = L.Compact ? L.U : 0f;
            try
            {
                if (WorldFirst) DrawWorldFirst(L);
                else if (L.Compact) DrawCompact(L); else DrawDesktop(L);
            }
            finally { EncounterUi.CompactUnit = 0f; }
        }

        /// <summary>World first: only the lethal stakes card (one readable line is needed for correctness) and a one-line toast stay on the screen.</summary>
        private void DrawWorldFirst(DuelLayout L)
        {
            if (Phase == Screen.Stakes) { DrawStakes(L); return; }
            var toast = Toast;
            if (toast == null) return;
            EncounterUi.Panel(L.Toast, new Color(0.25f, 0.08f, 0.06f, 0.92f));
            EncounterUi.Label(L.Toast, toast, L.Compact ? EncounterUi.Hint : EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
        }

        /// <summary>The referee's words for the end of the duel (the result head, line and footnote).</summary>
        public string ResultHead() { ResultText(out var h, out _, out _); return h; }
        public string ResultLine() { ResultText(out _, out var l, out _); return l; }
        public string ResultNote() => ResultFootnote();

        private string Subtitle()
        {
            if (Phase == Screen.Stakes) return "Mira will cast. You choose her Ward and her weaves.";
            if (Phase == Screen.Setup) return _campaign ? "Choose Mira's secret Ward: three essences, repeats allowed." : "Choose your secret Ward: three essences, repeats allowed.";
            if (Phase == Screen.Active) return _campaign ? "Break " + Possessive(OpponentName) + " Ward before Mira's breaks." : "Break Mira's Ward before she breaks yours.";
            return "The duel is over.";
        }

        private string HeaderText() => _campaign ? (IsFriendly ? "Friendly duel  -  " : "") + "Mira's Ward  vs  " + OpponentName : "Ward duel  -  practice with Mira";

        private void DrawDesktop(DuelLayout L)
        {
            float px(float v) => EncounterUi.Px(v);
            var title = L.Title;
            EncounterUi.Panel(title);
            EncounterUi.Label(new Rect(title.x, title.y + px(6), title.width, px(46)), HeaderText(), EncounterUi.Header, null, TextAnchor.MiddleCenter);
            EncounterUi.Label(new Rect(title.x, title.y + px(52), title.width, px(36)), Subtitle(), EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);

            if (Phase == Screen.Stakes) { DrawStakes(L); return; }

            DrawSide(L.CasterPanel, true, L);
            DrawSide(L.OpponentPanel, false, L);

            if (Phase == Screen.Result) { if (!_campaign || HoldOver) DrawResult(L); }
            else DrawBuilder(L);

            DrawCue(L);
            var toast = Toast;
            if (toast != null)
            {
                EncounterUi.Panel(L.Toast, new Color(0.25f, 0.08f, 0.06f, 0.92f));
                EncounterUi.Label(L.Toast, toast, EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            }
            // Key hints are HUD text: shown only with the HUD on (H). (Tab no longer switches the camera: it is the map.)
            if (Hud.Visible) EncounterUi.Shadow(L.Footer, "Esc: pause / leave", EncounterUi.Hint, EncounterUi.Dim, TextAnchor.MiddleLeft);
        }

        private void DrawCue(DuelLayout L)
        {
            var cue = StruckCue;
            if (cue == null || Phase == Screen.Stakes || (Phase == Screen.Result && (!_campaign || HoldOver))) return;
            EncounterUi.Panel(L.Cue, new Color(0.05f, 0.05f, 0.08f, 0.82f));
            EncounterUi.Fill(new Rect(L.Cue.x, L.Cue.y, Mathf.Max(3f, L.Cue.height * 0.12f), L.Cue.height), _cueColor);
            EncounterUi.Label(L.Cue, cue, EncounterUi.Body, _cueColor, TextAnchor.MiddleCenter);
        }

        private Color SideAccent(bool player) => player ? WardDuelStage.PlayerWardColor : (_campaign ? WardDuelStage.OpponentWardColor : WardDuelStage.MiraWardColor);
        private string SideName(bool player) => player ? CasterName : OpponentName;

        private void DrawSide(Rect r, bool player, DuelLayout L)
        {
            float px(float v) => EncounterUi.Px(v);
            var hist = player ? Session.PlayerHistory : Session.RivalHistory;
            float rowH = L.RowH;
            r.height = Mathf.Min(r.height, px(250) + Mathf.Max(1, hist.Count) * rowH);
            EncounterUi.Panel(r);
            float x = r.x + px(18), y = r.y + px(12), w = r.width - px(36);
            var accent = SideAccent(player);
            EncounterUi.Label(new Rect(x, y, w, px(44)), SideName(player), EncounterUi.Header, accent);
            int left = player ? Session.PlayerCastsLeft : Session.RivalCastsLeft;
            EncounterUi.Label(new Rect(x, y, w, px(44)), "Casts left  <b>" + left + "</b> / " + Tuning.MaxCasts, EncounterUi.Body, null, TextAnchor.MiddleRight);
            y += px(52);

            // Ward line: the player's own is known; the opponent's stays hidden until the end.
            EncounterUi.Label(new Rect(x, y, px(150), px(40)), "Ward", EncounterUi.Hint);
            int[] ward = player ? PlayerWard : Session.RevealedRivalWard;
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var cr = new Rect(x + px(90) + i * px(112), y, px(104), px(40));
                if (ward != null) EncounterUi.EssenceChip(cr, ward[i]);
                else EncounterUi.EssenceChip(cr, -1, player && Phase == Screen.Setup ? "..." : "?");
            }
            y += px(50);

            DrawStatus(new Rect(x, y, w, px(36)), player, left, accent, px(170));
            y += px(46);

            // History header and rows: weave + three totals, never positions.
            EncounterUi.Fill(new Rect(x, y, w, Mathf.Max(1, px(2))), new Color(1, 1, 1, 0.15f));
            y += px(6);
            float colW = px(66), numX = x + w - 3 * colW;
            EncounterUi.Label(new Rect(x, y, px(200), px(32)), "Weave", EncounterUi.Hint);
            for (int k = 0; k < 3; k++) EncounterUi.Label(new Rect(numX + k * colW, y, colW, px(32)), Heads[k], EncounterUi.Hint, Cols[k], TextAnchor.MiddleCenter);
            y += px(36);
            if (hist.Count == 0)
                EncounterUi.Label(new Rect(x, y, w, rowH), EmptyHistory(player), EncounterUi.Hint);
            for (int i = 0; i < hist.Count; i++) DrawHistoryRow(new Rect(x, y + i * rowH, w, rowH), i, hist[i], px(34), px(78), colW, i % 2 == 0);
        }

        private static readonly string[] Heads = { "Fract.", "Echo", "Fade" };
        private static readonly Color[] Cols = { new Color(1f, 0.55f, 0.35f), new Color(1f, 0.85f, 0.4f), new Color(0.7f, 0.7f, 0.75f) };

        private string EmptyHistory(bool player) =>
            Phase == Screen.Setup || Phase == Screen.Stakes ? "No casts yet." : (player ? "Build a weave and cast." : "Waiting for " + Possessive(OpponentName) + " first cast...");

        /// <summary>One history row: index, the weave (the caster's own essences), and the three totals. Never a position.</summary>
        private void DrawHistoryRow(Rect row, int i, WardCastEntry e, float indexW, float chipW, float colW, bool stripe)
        {
            float px(float v) => EncounterUi.Px(v);
            if (stripe) EncounterUi.Fill(new Rect(row.x - px(6), row.y, row.width + px(12), row.height), new Color(1, 1, 1, 0.04f));
            EncounterUi.Label(new Rect(row.x, row.y, indexW, row.height), (i + 1).ToString(), EncounterUi.Hint);
            float pad = Mathf.Max(2f, row.height * 0.11f);
            for (int k = 0; k < e.Weave.Length; k++)
                EncounterUi.EssenceChip(new Rect(row.x + indexW + k * chipW, row.y + pad, chipW - Mathf.Max(2f, chipW * 0.05f), row.height - 2 * pad), e.Weave[k], EncounterUi.CompactUnit > 0f ? EssenceInfo.Symbol((Essence)e.Weave[k]).ToString() : null, true);
            float numX = row.xMax - 3 * colW;
            int[] v = { e.Fractures, e.Echoes, e.Fades };
            for (int k = 0; k < 3; k++)
                EncounterUi.Label(new Rect(numX + k * colW, row.y, colW, row.height), "<b>" + v[k] + "</b>", EncounterUi.Body, v[k] > 0 && k < 2 ? Cols[k] : EncounterUi.Text, TextAnchor.MiddleCenter);
        }

        private void DrawStatus(Rect status, bool player, int left, Color accent, float labelW)
        {
            if (Phase != Screen.Active) return;
            if (player)
            {
                int ms = Session.MsUntilPlayerMayCast;
                if (left <= 0) EncounterUi.Label(status, "Out of casts", EncounterUi.Body, EncounterUi.Bad);
                else if (ms <= 0) EncounterUi.Label(status, "Ready to cast", EncounterUi.Body, EncounterUi.Good);
                else
                {
                    float u = 1f - ms / (float)Tuning.PlayerRecoveryMs;
                    EncounterUi.Label(new Rect(status.x, status.y, labelW, status.height), "Recovering", EncounterUi.Body, EncounterUi.Dim);
                    var bar = new Rect(status.x + labelW, status.y + status.height * 0.28f, status.width - labelW, status.height * 0.5f);
                    EncounterUi.Fill(bar, new Color(1, 1, 1, 0.12f));
                    EncounterUi.Fill(new Rect(bar.x, bar.y, bar.width * u, bar.height), accent);
                }
            }
            else
            {
                string dots = new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
                EncounterUi.Label(status, left > 0 ? OpponentName + " is weaving" + dots : OpponentName + " is out of casts", EncounterUi.Body, left > 0 ? accent : EncounterUi.Bad);
            }
        }

        private void DrawBuilder(DuelLayout L)
        {
            float px(float v) => L.Compact ? v * L.U : EncounterUi.Px(v);
            var r = L.Builder;

            if (HelpVisible)
            {
                var hr = L.Help;
                EncounterUi.Panel(hr, new Color(0.10f, 0.09f, 0.05f, 0.88f));
                if (L.Compact)
                {
                    EncounterUi.Label(new Rect(hr.x + px(8), hr.y + px(2), hr.width - px(16), hr.height / 2f), "<color=#FF8C59><b>Fracture</b></color> right place   <color=#FFD966><b>Echo</b></color> wrong place", EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);
                    EncounterUi.Label(new Rect(hr.x + px(8), hr.y + hr.height / 2f - px(2), hr.width - px(16), hr.height / 2f), "<color=#B3B3BF><b>Fade</b></color> not in the Ward", EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);
                }
                else
                {
                    string[] lines =
                    {
                        "<color=#FF8C59><b>Fracture</b></color> = right essence, right place",
                        "<color=#FFD966><b>Echo</b></color> = right essence, wrong place",
                        "<color=#B3B3BF><b>Fade</b></color> = that essence is not in the Ward",
                    };
                    for (int i = 0; i < 3; i++)
                        EncounterUi.Label(new Rect(hr.x + px(24), hr.y + px(10) + i * px(40), hr.width - px(48), px(38)), lines[i], EncounterUi.Body);
                }
            }

            int idle = MsUntilAutoCast;
            if (idle >= 0 && idle <= IdleWarnMs)
                EncounterUi.Shadow(L.Idle, "Auto-cast in " + Mathf.CeilToInt(idle / 1000f) + " s", L.Compact ? EncounterUi.Header : EncounterUi.Big, EncounterUi.Bad, TextAnchor.MiddleCenter);

            EncounterUi.Panel(r);
            bool setup = Phase == Screen.Setup;
            if (!L.Compact)
            {
                float x = r.x + px(20), y = r.y + px(14);
                EncounterUi.Label(new Rect(x, y, px(400), px(40)), setup ? (_campaign ? "Mira's secret Ward" : "Your secret Ward") : (_campaign ? "Mira's next weave" : "Your next weave"), EncounterUi.Header);
                EncounterUi.Label(new Rect(x, y, r.width - px(40), px(40)), "Backspace: remove", EncounterUi.Hint, null, TextAnchor.MiddleRight);
            }
            for (int i = 0; i < Tuning.Loci && i < 3; i++)
            {
                var sr = L.Slots[i];
                if (i < _building.Count) EncounterUi.EssenceChip(sr, _building[i]);
                else EncounterUi.EssenceChip(sr, -1, L.Compact && i == 0 && _building.Count == 0 ? (setup ? "Ward" : "Weave") : (i + 1).ToString());
                // A tap on a filled slot takes the last essence back (phones have no Backspace).
                if (i < _building.Count && GUI.Button(sr, GUIContent.none, GUIStyle.none)) RemoveLast();
            }
            bool ready = _building.Count == Tuning.Loci && (setup || Session.MsUntilPlayerMayCast <= 0);
            var oldColor = GUI.color;
            if (!ready) GUI.color = new Color(1, 1, 1, 0.5f);
            string go = setup ? (_campaign ? "Lock Mira's Ward" : "Lock Ward") : "Cast";
            if (!L.Compact) go += " (Enter)";
            if (EncounterUi.KeyButton(L.Go, go)) { if (setup) LockWard(); else Cast(); }
            GUI.color = oldColor;
            for (int i = 0; i < 4; i++)
            {
                var er = L.Essences[i];
                if (i < Pool.Length)
                {
                    var c = EncounterUi.EssenceColor((int)Pool[i]);
                    EncounterUi.Fill(er, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.95f));
                    EncounterUi.Fill(new Rect(er.x, er.yMax - px(5), er.width, px(5)), c);
                    string label = L.Compact ? EssenceInfo.DisplayName(Pool[i]) : "[" + (i + 1) + "]  " + EssenceInfo.DisplayName(Pool[i]);
                    if (GUI.Button(er, label, EncounterUi.Chip)) AddEssence(Pool[i]);
                }
                else
                {
                    EncounterUi.Fill(er, new Color(1, 1, 1, 0.05f));
                    EncounterUi.Label(er, _campaign ? "-" : (L.Compact ? "Light: after 3" : "Light: after 3 duels"), EncounterUi.Hint, null, TextAnchor.MiddleCenter);
                }
            }
        }

        private void DrawStakes(DuelLayout L)
        {
            float px(float v) => L.Compact ? v * L.U : EncounterUi.Px(v);
            var r = L.Stakes;
            EncounterUi.Panel(r, new Color(0.10f, 0.04f, 0.04f, 0.95f));
            float y = r.y + px(L.Compact ? 8 : 24);
            float lh = px(L.Compact ? 30 : 60);
            EncounterUi.Label(new Rect(r.x, y, r.width, lh), OpponentFullName, L.Compact ? EncounterUi.Header : EncounterUi.Big, EncounterUi.Gold, TextAnchor.MiddleCenter);
            y += lh + px(L.Compact ? 2 : 6);
            string style = Profile != null && !string.IsNullOrEmpty(Profile.StyleNote) ? "Casts " + Profile.StyleNote + "." : "";
            EncounterUi.Label(new Rect(r.x, y, r.width, px(L.Compact ? 20 : 36)), style, EncounterUi.Hint, null, TextAnchor.MiddleCenter);
            y += px(L.Compact ? 30 : 56);
            EncounterUi.Label(new Rect(r.x, y, r.width, px(L.Compact ? 30 : 56)), StakesText ?? "", L.Compact ? EncounterUi.Header : EncounterUi.Big, EncounterUi.Bad, TextAnchor.MiddleCenter);
            y += px(L.Compact ? 34 : 66);
            string v = CampaignDuel.FirstName(Request?.VolunteerName);
            string sub = v != null ? "If Mira's Ward breaks, " + v + " stays behind so the rest of you get away." : "There is no retreat this time.";
            EncounterUi.Label(new Rect(r.x + px(10), y, r.width - px(20), px(L.Compact ? 22 : 40)), sub, EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            if (EncounterUi.KeyButton(L.StakesGo, L.Compact ? "Set Mira's Ward" : "Set Mira's Ward  (Enter)")) BeginWard();
        }

        private void DrawResult(DuelLayout L)
        {
            float px(float v) => L.Compact ? v * L.U : EncounterUi.Px(v);
            var r = L.Result;
            EncounterUi.Panel(r, new Color(0.06f, 0.05f, 0.08f, 0.94f));
            string head, line; Color hc;
            ResultText(out head, out line, out hc);
            float x = r.x + px(L.Compact ? 12 : 30), w = r.width - px(L.Compact ? 24 : 60), y = r.y + px(L.Compact ? 6 : 16);
            float headH = px(L.Compact ? 40 : 70);
            EncounterUi.Label(new Rect(r.x, y, r.width, headH), head, EncounterUi.Big, hc, TextAnchor.MiddleCenter);
            y += headH + px(2);
            EncounterUi.Label(new Rect(r.x, y, r.width, px(L.Compact ? 24 : 40)), line, EncounterUi.Body, null, TextAnchor.MiddleCenter);
            y += px(L.Compact ? 30 : 60);
            float rowH = px(L.Compact ? 40 : 56), step = px(L.Compact ? 46 : 66);
            DrawWardRow(new Rect(x, y, w, rowH), _campaign ? "Mira's Ward" : "Your Ward", Session.RevealedPlayerWard, WardDuelStage.PlayerWardColor, L); y += step;
            DrawWardRow(new Rect(x, y, w, rowH), Possessive(OpponentName) + " Ward", Session.RevealedRivalWard, SideAccent(false), L); y += step + px(L.Compact ? 0 : 8);
            string foot = ResultFootnote();
            if (foot != null) { EncounterUi.Label(new Rect(r.x, y, r.width, px(L.Compact ? 24 : 36)), foot, EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter); y += px(L.Compact ? 24 : 38); }
            if (!_campaign && _lightJustUnlocked)
                EncounterUi.Label(new Rect(r.x, y, r.width, px(36)), "Light essence unlocked for your next duel.", EncounterUi.Body, EncounterUi.EssenceColor((int)Essence.Light), TextAnchor.MiddleCenter);
            if (IsLethal)
            {
                var full = new Rect(L.ResultRematch.x, L.ResultRematch.y, L.ResultReturn.xMax - L.ResultRematch.x, L.ResultReturn.height);
                if (EncounterUi.KeyButton(full, L.Compact ? "Return" : "Return  [Enter / Esc]")) Return();
            }
            else
            {
                if (EncounterUi.KeyButton(L.ResultRematch, L.Compact ? "Rematch" : "Rematch  [R]")) Rematch();
                if (EncounterUi.KeyButton(L.ResultReturn, L.Compact ? "Return" : "Return  [Enter / Esc]")) Return();
            }
        }

        private void ResultText(out string head, out string line, out Color hc)
        {
            string opp = OpponentName, v = CampaignDuel.FirstName(Request?.VolunteerName);
            switch (LastOutcome)
            {
                case WardOutcome.Victory: head = "Victory!"; line = _campaign ? "Mira broke " + Possessive(opp) + " Ward." : "You broke Mira's Ward."; hc = EncounterUi.Good; break;
                case WardOutcome.Defeat:
                    head = "Defeat"; hc = EncounterUi.Bad;
                    line = !_campaign ? "Mira broke your Ward." : IsLethal ? (v != null ? opp + " broke Mira's Ward. " + v + " holds the way out." : opp + " broke Mira's Ward.") : opp + " broke Mira's Ward.";
                    break;
                case WardOutcome.Stalemate: head = "Stalemate"; line = "Both sides ran out of casts."; hc = EncounterUi.Gold; break;
                case WardOutcome.Clash: head = "Clash - a draw"; line = "Both Wards broke at the same moment."; hc = EncounterUi.Gold; break;
                default:
                    if (IsLethal) { head = "You left the fight"; line = v != null ? v + " holds the way out while the others run." : "Mira falls back alone."; hc = EncounterUi.Bad; }
                    else { head = "Duel left"; line = "Forfeited (practice: no penalty)."; hc = EncounterUi.Dim; }
                    break;
            }
        }

        private string ResultFootnote()
        {
            if (!_campaign)
            {
                var p = State != null ? State.Ward : null;
                return p != null ? "Duels completed: " + p.CompletedDuelsVsMira + "    (won " + p.Wins + ", lost " + p.Losses + ", drawn " + p.Draws + ")" : null;
            }
            if (IsFriendly) return "A friendly duel: nobody is at risk.";
            if (Resolution == null) return null;
            switch (Resolution.Value)
            {
                case DuelResult.Victory: return "The way is open.";
                case DuelResult.Clash: case DuelResult.Stalemate: return "Nobody fell. The party falls back to try again.";
                default: return "The party falls back. The Ash expedition gains ground.";
            }
        }

        private void DrawWardRow(Rect r, string label, int[] ward, Color accent, DuelLayout L)
        {
            float px(float v) => L.Compact ? v * L.U : EncounterUi.Px(v);
            float labelW = px(L.Compact ? 130 : 220), chipW = (r.width - labelW - px(L.Compact ? 4 : 10)) / 3f;
            if (!L.Compact) chipW = Mathf.Min(chipW, px(160));
            EncounterUi.Label(new Rect(r.x, r.y, labelW, r.height), label, EncounterUi.Body, accent);
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var cr = new Rect(r.x + labelW + px(L.Compact ? 4 : 10) + i * chipW, r.y, chipW - px(L.Compact ? 4 : 10), r.height);
                EncounterUi.EssenceChip(cr, ward != null && i < ward.Length ? ward[i] : -1);
            }
        }

        // ---- compact (phones)

        private void DrawCompact(DuelLayout L)
        {
            float u = L.U;
            bool resultUp = Phase == Screen.Result && (!_campaign || HoldOver);
            if (resultUp) { DrawResult(L); return; } // the result card covers the screen: nothing drawn under it

            EncounterUi.Panel(L.Title, new Color(0.07f, 0.06f, 0.08f, 0.80f));
            EncounterUi.Label(new Rect(L.Title.x, L.Title.y + 2 * u, L.Title.width, 22 * u), HeaderText(), EncounterUi.Header, null, TextAnchor.MiddleCenter);
            var sub = new GUIStyle(EncounterUi.Hint) { wordWrap = true, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            EncounterUi.Label(new Rect(L.Title.x + 4 * u, L.Title.y + 22 * u, L.Title.width - 8 * u, 20 * u), Subtitle(), sub, EncounterUi.Text);

            if (Phase == Screen.Stakes) { DrawStakes(L); return; }

            DrawCard(L.CasterPanel, true, L);
            DrawCard(L.OpponentPanel, false, L);

            if (Phase == Screen.Result) { }
            else if (HistoryExpanded) DrawHistoryOverlay(L);
            else DrawBuilder(L);

            DrawCue(L);
            var toast = Toast;
            if (toast != null)
            {
                EncounterUi.Panel(L.Toast, new Color(0.25f, 0.08f, 0.06f, 0.92f));
                EncounterUi.Label(L.Toast, toast, EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>A side card: name, casts, Ward, status and the last cast only; a tap expands both histories.</summary>
        private void DrawCard(Rect r, bool player, DuelLayout L)
        {
            float u = L.U;
            EncounterUi.Panel(r, new Color(0.07f, 0.06f, 0.08f, 0.78f));
            var accent = SideAccent(player);
            EncounterUi.Fill(new Rect(r.x, r.y, r.width, 3 * u), accent);
            float x = r.x + 8 * u, w = r.width - 16 * u, y = r.y + 4 * u;
            int left = player ? Session.PlayerCastsLeft : Session.RivalCastsLeft;
            EncounterUi.Label(new Rect(x, y, w, 22 * u), SideName(player), EncounterUi.Header, accent);
            EncounterUi.Label(new Rect(x, y, w, 22 * u), "Casts <b>" + left + "</b>/" + Tuning.MaxCasts, EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleRight);
            y += 24 * u;
            int[] ward = player ? PlayerWard : Session.RevealedRivalWard;
            float cw = (w - 44 * u) / 3f;
            EncounterUi.Label(new Rect(x, y, 44 * u, 24 * u), "Ward", EncounterUi.Hint);
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var cr = new Rect(x + 44 * u + i * cw, y, cw - 3 * u, 24 * u);
                if (ward != null) EncounterUi.EssenceChip(cr, ward[i], null, true);
                else EncounterUi.EssenceChip(cr, -1, player && Phase == Screen.Setup ? "..." : "?");
            }
            y += 28 * u;
            DrawStatus(new Rect(x, y, w, 22 * u), player, left, accent, 84 * u);
            y += 24 * u;
            var hist = player ? Session.PlayerHistory : Session.RivalHistory;
            var lastRow = new Rect(x, y, w, r.yMax - y - 4 * u);
            if (hist.Count == 0) EncounterUi.Label(lastRow, Phase == Screen.Active ? "No casts yet" : "", EncounterUi.Hint);
            else DrawHistoryRow(lastRow, hist.Count - 1, hist[hist.Count - 1], 18 * u, 26 * u, 22 * u, false);
            if (Phase != Screen.Result && GUI.Button(r, GUIContent.none, GUIStyle.none)) ToggleHistory();
        }

        private void DrawHistoryOverlay(DuelLayout L)
        {
            float u = L.U;
            var r = L.HistoryOverlay;
            EncounterUi.Panel(r, new Color(0.05f, 0.05f, 0.07f, 0.95f));
            float colGap = 12 * u, colW = (r.width - 3 * colGap) / 2f, top = r.y + 8 * u;
            float rowH = Mathf.Min(28 * u, (L.HistoryClose.y - top - 52 * u) / Mathf.Max(1, Tuning.MaxCasts));
            for (int side = 0; side < 2; side++)
            {
                bool player = side == 0;
                float x = r.x + colGap + side * (colW + colGap), y = top;
                EncounterUi.Label(new Rect(x, y, colW, 24 * u), SideName(player) + "  -  every cast", EncounterUi.Header, SideAccent(player));
                y += 26 * u;
                float numW = 30 * u;
                for (int k = 0; k < 3; k++) EncounterUi.Label(new Rect(x + colW - (3 - k) * numW, y, numW, 18 * u), Heads[k].Substring(0, 2), EncounterUi.Hint, Cols[k], TextAnchor.MiddleCenter);
                y += 20 * u;
                var hist = player ? Session.PlayerHistory : Session.RivalHistory;
                if (hist.Count == 0) EncounterUi.Label(new Rect(x, y, colW, rowH), "No casts yet.", EncounterUi.Hint);
                for (int i = 0; i < hist.Count; i++) DrawHistoryRow(new Rect(x, y + i * rowH, colW, rowH), i, hist[i], 22 * u, 38 * u, numW, i % 2 == 0);
            }
            if (EncounterUi.KeyButton(L.HistoryClose, "Close")) HistoryExpanded = false;
        }
    }
}
