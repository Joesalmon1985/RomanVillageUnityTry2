using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>
    /// Ward duel with Mira ("ward_mira", PRD 4.2). Setup: pick a secret three-locus Ward. Active: build weaves and cast while
    /// Mira casts on her own cadence; both histories show the three totals only. Result: outcome and both Wards revealed,
    /// Rematch (R) or Return (Enter/Esc). Rules live in WardDuelSession; this class draws, animates and forwards input.
    /// Keys and buttons call the same public methods (AddEssence, RemoveLast, LockWard, Cast, Rematch, Return), which tests use.
    /// </summary>
    public sealed class WardDuelEncounter : IEncounter
    {
        public enum Screen { Setup, Active, Result }

        /// <summary>Test hooks: replace the tuning / seed of the next duels (null = normal).</summary>
        public static WardTuning TuningOverride;
        public static ulong? SeedOverride;
        /// <summary>The encounter currently running (for tests and AutoPlay).</summary>
        public static WardDuelEncounter Current { get; private set; }

        public const float DuelDistance = 5.5f;
        public const int IdleWarnMs = 15000;

        public string Title => "Ward duel with Mira";
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
        /// <summary>True once the current duel's result went into WardProgress.</summary>
        public bool Recorded { get; private set; }
        public int DuelsPlayed { get; private set; }
        public bool HelpVisible => _firstTimer && (Phase == Screen.Setup || Phase == Screen.Active);
        public WardDuelStage Stage => _stage;
        public string Toast => Time.unscaledTime < _toastUntil ? _toast : null;

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

        private WorldState State => _ctx != null && _ctx.Sim != null ? _ctx.Sim.State : null;

        // ------------------------------------------------------------------ lifecycle

        public void Begin(EncounterContext ctx)
        {
            _ctx = ctx;
            Current = this;
            _beginFrame = Time.frameCount;
            Tuning = TuningOverride ?? EncounterTuningFiles.Ward();
            _root = new GameObject("WardDuel");
            if (ctx.World != null) _root.transform.SetParent(ctx.World.transform, false);
            _sfx = EncounterSfx.Create(_root.transform);

            _mira = ctx.Npc != null ? ctx.Npc : Npc.Find(NpcIds.Mira);
            Vector3 miraPos = _mira != null ? _mira.HomePosition : ctx.Stage.Position;
            Vector3 fwd = _mira != null ? BoardSpace.DirOf(_mira.HomeYaw) : ctx.Stage.Forward;
            Vector3 playerPos = miraPos + fwd * DuelDistance;
            playerPos.y = miraPos.y;
            _stage = WardDuelStage.Build(_root.transform, playerPos, _mira != null ? _mira.Actor : null, _sfx);

            // Over-the-shoulder first-person view: both wizards and both Wards in frame.
            if (ctx.Avatar != null)
            {
                var toMira = (miraPos - playerPos).normalized;
                var right = Vector3.Cross(Vector3.up, toMira).normalized;
                var camPos = playerPos - toMira * 3.0f + right * 1.8f;
                var target = Vector3.Lerp(playerPos, miraPos, 0.62f) + Vector3.up * 0.9f;
                var d = target - (camPos + Vector3.up * WorldBootstrap.EyeHeight);
                float yaw = BoardSpace.YawOf(d);
                float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                ctx.Avatar.Teleport(camPos + Vector3.up * 0.05f, yaw, pitch);
            }
            if (ctx.World != null && ctx.World.GodCam != null) { _prevPan = ctx.World.GodCam.KeyboardPan; ctx.World.GodCam.KeyboardPan = false; }

            _firstTimer = State == null || State.Ward == null || State.Ward.CompletedDuelsVsMira == 0;
            NewDuel();
        }

        public void End()
        {
            if (Session != null && Session.Phase != WardPhase.Complete) Session.Forfeit(); // leaving mid-duel: forfeit, nothing recorded
            if (_ctx != null && _ctx.World != null && _ctx.World.GodCam != null) _ctx.World.GodCam.KeyboardPan = _prevPan;
            if (_mira != null && _mira.Actor != null) { _mira.Actor.PlayIdle(); _mira.Actor.FaceTowards(_mira.HomePosition + BoardSpace.DirOf(_mira.HomeYaw)); }
            if (_root != null) Object.Destroy(_root);
            if (Current == this) Current = null;
        }

        private void NewDuel()
        {
            var s = State;
            int completed = s != null && s.Ward != null ? s.Ward.CompletedDuelsVsMira : 0;
            Tuning = TuningOverride ?? EncounterTuningFiles.Ward(); // re-read per duel: ward.json edits apply on rematch
            Pool = WardProgressApi.NextPool(s, Tuning);
            Seed = SeedOverride ?? new Rng(s != null ? s.Seed : 1UL).Fork("ward_duel_" + completed + "_" + DuelsPlayed).NextULong();
            Session = new WardDuelSession(Tuning, Pool, Seed);
            Phase = Screen.Setup;
            _building.Clear();
            PlayerWard = null;
            Recorded = false;
            LastOutcome = WardOutcome.None;
            _lastPlayerCastMs = 0;
            _msAcc = 0;
            _stage.ResetWards();
            _stage.PlayerActor.PlayIdle();
            if (_lightJustUnlocked && Pool.Length > 3) ShowToast("New essence: Light joins the pool.", 4f);
            _lightJustUnlocked = false;
        }

        // ------------------------------------------------------------------ actions (keys, buttons and tests)

        /// <summary>Adds an essence to the Ward (setup) or weave (active). False if full or not in this duel's pool.</summary>
        public bool AddEssence(Essence e)
        {
            if (Phase == Screen.Result || _building.Count >= Tuning.Loci) return false;
            bool inPool = false;
            foreach (var p in Pool) if (p == e) inPool = true;
            if (!inPool) return false;
            _building.Add((int)e);
            return true;
        }

        public bool RemoveLast()
        {
            if (Phase == Screen.Result || _building.Count == 0) return false;
            _building.RemoveAt(_building.Count - 1);
            return true;
        }

        /// <summary>Locks the secret Ward and starts the duel clock.</summary>
        public bool LockWard()
        {
            if (Phase != Screen.Setup) return false;
            if (_building.Count != Tuning.Loci) { ShowToast("Pick " + Tuning.Loci + " essences for your Ward first."); return false; }
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

        public bool Rematch()
        {
            if (Phase != Screen.Result) return false;
            DuelsPlayed++;
            NewDuel();
            return true;
        }

        public bool Return()
        {
            if (Phase != Screen.Result) return false;
            IsFinished = true;
            return true;
        }

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
            if (!blocked) ReadKeys();
        }

        private void ReadKeys()
        {
            if (Phase == Screen.Result)
            {
                if (Input.GetKeyDown(KeyCode.R)) Rematch();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Return();
                else if (Input.GetKeyDown(KeyCode.Escape)) { EncounterKeys.ConsumeEscape(); Return(); }
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
                    _stage.Cast(true, pc.Entry.Weave, pc.Entry.Fractures, pc.Entry.Fractures >= Tuning.Loci);
                    if (pc.Auto) ShowToast("No cast for " + (Tuning.IdleAutoCastMs / 1000) + " s: a random weave was cast for you.", 3.5f);
                }
                else if (ev is RivalCastEvent rc)
                {
                    _stage.Cast(false, rc.Entry.Weave, rc.Entry.Fractures, rc.Entry.Fractures >= Tuning.Loci);
                }
                else if (ev is DuelEndedEvent de) OnEnded(de.Outcome);
            }
            if (Session.Phase == WardPhase.Complete && Phase != Screen.Result) OnEnded(Session.Outcome);
        }

        private void OnEnded(WardOutcome o)
        {
            if (Phase == Screen.Result) return;
            Phase = Screen.Result;
            LastOutcome = o;
            _building.Clear();
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
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;

            // Title.
            var title = EncounterUi.Fit(new Rect((sw - px(1000)) / 2, px(14), px(1000), px(96)));
            EncounterUi.Panel(title);
            EncounterUi.Label(new Rect(title.x, title.y + px(6), title.width, px(46)), "Ward duel  -  practice with Mira", EncounterUi.Header, null, TextAnchor.MiddleCenter);
            string sub = Phase == Screen.Setup ? "Choose your secret Ward: three essences, repeats allowed."
                : Phase == Screen.Active ? "Break Mira's Ward before she breaks yours."
                : "The duel is over.";
            EncounterUi.Label(new Rect(title.x, title.y + px(52), title.width, px(36)), sub, EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);

            DrawSide(new Rect(px(20), px(124), px(510), px(700)), true);
            DrawSide(new Rect(sw - px(20) - px(510), px(124), px(510), px(700)), false);

            if (Phase == Screen.Result) DrawResult();
            else DrawBuilder();

            var toast = Toast;
            if (toast != null)
            {
                var tr = EncounterUi.Fit(new Rect((sw - px(760)) / 2, px(124), px(760), px(56)));
                EncounterUi.Panel(tr, new Color(0.25f, 0.08f, 0.06f, 0.92f));
                EncounterUi.Label(tr, toast, EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            }
            var footer = new Rect(px(24), sh - px(40), sw - px(48), px(32));
            EncounterUi.Shadow(footer, "Tab: switch camera (the duel keeps running)     Esc: pause / leave", EncounterUi.Hint, EncounterUi.Dim, TextAnchor.MiddleLeft);
        }

        private void DrawSide(Rect r, bool player)
        {
            float px(float v) => EncounterUi.Px(v);
            r = EncounterUi.Fit(r);
            var hist = player ? Session.PlayerHistory : Session.RivalHistory;
            float rowH = px(46);
            r.height = Mathf.Min(r.height, px(250) + Mathf.Max(1, hist.Count) * rowH);
            EncounterUi.Panel(r);
            float x = r.x + px(18), y = r.y + px(12), w = r.width - px(36);
            var accent = player ? WardDuelStage.PlayerWardColor : WardDuelStage.MiraWardColor;
            EncounterUi.Label(new Rect(x, y, w, px(44)), player ? "You" : "Mira", EncounterUi.Header, accent);
            int left = player ? Session.PlayerCastsLeft : Session.RivalCastsLeft;
            EncounterUi.Label(new Rect(x, y, w, px(44)), "Casts left  <b>" + left + "</b> / " + Tuning.MaxCasts, EncounterUi.Body, null, TextAnchor.MiddleRight);
            y += px(52);

            // Ward line: yours is known to you; Mira's stays hidden until the end.
            EncounterUi.Label(new Rect(x, y, px(150), px(40)), "Ward", EncounterUi.Hint);
            int[] ward = player ? PlayerWard : Session.RevealedRivalWard;
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var cr = new Rect(x + px(90) + i * px(112), y, px(104), px(40));
                if (ward != null) EncounterUi.EssenceChip(cr, ward[i]);
                else EncounterUi.EssenceChip(cr, -1, player && Phase == Screen.Setup ? "..." : "?");
            }
            y += px(50);

            // Status line: recovery bar / Mira weaving.
            var status = new Rect(x, y, w, px(36));
            if (Phase == Screen.Active)
            {
                if (player)
                {
                    int ms = Session.MsUntilPlayerMayCast;
                    if (left <= 0) EncounterUi.Label(status, "Out of casts", EncounterUi.Body, EncounterUi.Bad);
                    else if (ms <= 0) EncounterUi.Label(status, "Ready to cast", EncounterUi.Body, EncounterUi.Good);
                    else
                    {
                        float u = 1f - ms / (float)Tuning.PlayerRecoveryMs;
                        EncounterUi.Label(new Rect(x, y, px(170), px(36)), "Recovering", EncounterUi.Body, EncounterUi.Dim);
                        var bar = new Rect(x + px(170), y + px(10), w - px(170), px(18));
                        EncounterUi.Fill(bar, new Color(1, 1, 1, 0.12f));
                        EncounterUi.Fill(new Rect(bar.x, bar.y, bar.width * u, bar.height), accent);
                    }
                }
                else
                {
                    string dots = new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
                    EncounterUi.Label(status, left > 0 ? "Mira is weaving" + dots : "Mira is out of casts", EncounterUi.Body, left > 0 ? accent : EncounterUi.Bad);
                }
            }
            y += px(46);

            // History header and rows: weave + three totals, never positions.
            EncounterUi.Fill(new Rect(x, y, w, Mathf.Max(1, px(2))), new Color(1, 1, 1, 0.15f));
            y += px(6);
            float colW = px(66), numX = x + w - 3 * colW;
            EncounterUi.Label(new Rect(x, y, px(200), px(32)), "Weave", EncounterUi.Hint);
            string[] heads = { "Fract.", "Echo", "Fade" };
            Color[] cols = { new Color(1f, 0.55f, 0.35f), new Color(1f, 0.85f, 0.4f), new Color(0.7f, 0.7f, 0.75f) };
            for (int k = 0; k < 3; k++) EncounterUi.Label(new Rect(numX + k * colW, y, colW, px(32)), heads[k], EncounterUi.Hint, cols[k], TextAnchor.MiddleCenter);
            y += px(36);
            if (hist.Count == 0)
                EncounterUi.Label(new Rect(x, y, w, rowH), Phase == Screen.Setup ? "No casts yet." : (player ? "Build a weave and cast." : "Waiting for Mira's first cast..."), EncounterUi.Hint);
            for (int i = 0; i < hist.Count; i++)
            {
                var e = hist[i];
                float ry = y + i * rowH;
                if (i % 2 == 0) EncounterUi.Fill(new Rect(x - px(6), ry, w + px(12), rowH), new Color(1, 1, 1, 0.04f));
                EncounterUi.Label(new Rect(x, ry, px(34), rowH), (i + 1).ToString(), EncounterUi.Hint);
                for (int k = 0; k < e.Weave.Length; k++)
                    EncounterUi.EssenceChip(new Rect(x + px(34) + k * px(78), ry + px(5), px(74), rowH - px(10)), e.Weave[k], null, true);
                int[] v = { e.Fractures, e.Echoes, e.Fades };
                for (int k = 0; k < 3; k++)
                    EncounterUi.Label(new Rect(numX + k * colW, ry, colW, rowH), "<b>" + v[k] + "</b>", EncounterUi.Body, v[k] > 0 && k < 2 ? cols[k] : EncounterUi.Text, TextAnchor.MiddleCenter);
            }
        }

        private void DrawBuilder()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            float bw = px(760), bh = px(232);
            var r = EncounterUi.Fit(new Rect((sw - bw) / 2, sh - px(52) - bh, bw, bh));

            if (HelpVisible)
            {
                var hr = EncounterUi.Fit(new Rect(r.x, r.y - px(146), r.width, px(136)));
                EncounterUi.Panel(hr, new Color(0.10f, 0.09f, 0.05f, 0.88f));
                string[] lines =
                {
                    "<color=#FF8C59><b>Fracture</b></color> = right essence, right place",
                    "<color=#FFD966><b>Echo</b></color> = right essence, wrong place",
                    "<color=#B3B3BF><b>Fade</b></color> = that essence is not in the Ward",
                };
                for (int i = 0; i < 3; i++)
                    EncounterUi.Label(new Rect(hr.x + px(24), hr.y + px(10) + i * px(40), hr.width - px(48), px(38)), lines[i], EncounterUi.Body);
            }

            int idle = MsUntilAutoCast;
            if (idle >= 0 && idle <= IdleWarnMs)
            {
                var ir = EncounterUi.Fit(new Rect(r.x, r.y - px(HelpVisible ? 206 : 64), r.width, px(54)));
                EncounterUi.Shadow(ir, "Auto-cast in " + Mathf.CeilToInt(idle / 1000f) + " s", EncounterUi.Big, EncounterUi.Bad, TextAnchor.MiddleCenter);
            }

            EncounterUi.Panel(r);
            float x = r.x + px(20), y = r.y + px(14);
            bool setup = Phase == Screen.Setup;
            EncounterUi.Label(new Rect(x, y, px(400), px(40)), setup ? "Your secret Ward" : "Your next weave", EncounterUi.Header);
            EncounterUi.Label(new Rect(x, y, r.width - px(40), px(40)), "Backspace: remove", EncounterUi.Hint, null, TextAnchor.MiddleRight);
            y += px(50);
            float slotW = px(140), slotH = px(64);
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var sr = new Rect(x + i * (slotW + px(14)), y, slotW, slotH);
                if (i < _building.Count) EncounterUi.EssenceChip(sr, _building[i]);
                else EncounterUi.EssenceChip(sr, -1, (i + 1).ToString());
            }
            var go = new Rect(r.xMax - px(20) - px(250), y, px(250), slotH);
            bool ready = _building.Count == Tuning.Loci && (setup || Session.MsUntilPlayerMayCast <= 0);
            var oldColor = GUI.color;
            if (!ready) GUI.color = new Color(1, 1, 1, 0.5f);
            if (EncounterUi.KeyButton(go, setup ? "Lock Ward (Enter)" : "Cast (Enter)")) { if (setup) LockWard(); else Cast(); }
            GUI.color = oldColor;
            y += slotH + px(18);
            float ew = (r.width - px(40) - px(10) * 3) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var er = new Rect(x + i * (ew + px(10)), y, ew, px(64));
                if (i < Pool.Length)
                {
                    var c = EncounterUi.EssenceColor((int)Pool[i]);
                    EncounterUi.Fill(er, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.95f));
                    EncounterUi.Fill(new Rect(er.x, er.yMax - px(5), er.width, px(5)), c);
                    if (GUI.Button(er, "[" + (i + 1) + "]  " + EssenceInfo.DisplayName(Pool[i]), EncounterUi.Chip)) AddEssence(Pool[i]);
                }
                else
                {
                    EncounterUi.Fill(er, new Color(1, 1, 1, 0.05f));
                    EncounterUi.Label(er, "Light: after 3 duels", EncounterUi.Hint, null, TextAnchor.MiddleCenter);
                }
            }
        }

        private void DrawResult()
        {
            float px(float v) => EncounterUi.Px(v);
            var r = EncounterUi.Centered(760, 500, 60);
            EncounterUi.Panel(r, new Color(0.06f, 0.05f, 0.08f, 0.94f));
            string head, line; Color hc;
            switch (LastOutcome)
            {
                case WardOutcome.Victory: head = "Victory!"; line = "You broke Mira's Ward."; hc = EncounterUi.Good; break;
                case WardOutcome.Defeat: head = "Defeat"; line = "Mira broke your Ward."; hc = EncounterUi.Bad; break;
                case WardOutcome.Stalemate: head = "Stalemate"; line = "Both sides ran out of casts."; hc = EncounterUi.Gold; break;
                case WardOutcome.Clash: head = "Clash - a draw"; line = "Both Wards broke at the same moment."; hc = EncounterUi.Gold; break;
                default: head = "Duel left"; line = "Forfeited (practice: no penalty)."; hc = EncounterUi.Dim; break;
            }
            float x = r.x + px(30), w = r.width - px(60), y = r.y + px(16);
            EncounterUi.Label(new Rect(r.x, y, r.width, px(70)), head, EncounterUi.Big, hc, TextAnchor.MiddleCenter);
            y += px(72);
            EncounterUi.Label(new Rect(r.x, y, r.width, px(40)), line, EncounterUi.Body, null, TextAnchor.MiddleCenter);
            y += px(60);
            var pw = Session.RevealedPlayerWard; var mw = Session.RevealedRivalWard;
            DrawWardRow(new Rect(x, y, w, px(56)), "Your Ward", pw, WardDuelStage.PlayerWardColor); y += px(66);
            DrawWardRow(new Rect(x, y, w, px(56)), "Mira's Ward", mw, WardDuelStage.MiraWardColor); y += px(74);
            var p = State != null ? State.Ward : null;
            if (p != null)
            {
                EncounterUi.Label(new Rect(r.x, y, r.width, px(36)), "Duels completed: " + p.CompletedDuelsVsMira + "    (won " + p.Wins + ", lost " + p.Losses + ", drawn " + p.Draws + ")", EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleCenter);
                y += px(38);
                if (_lightJustUnlocked)
                    EncounterUi.Label(new Rect(r.x, y, r.width, px(36)), "Light essence unlocked for your next duel.", EncounterUi.Body, EncounterUi.EssenceColor((int)Essence.Light), TextAnchor.MiddleCenter);
            }
            float bw = (w - px(20)) / 2f, by = r.yMax - px(86);
            if (EncounterUi.KeyButton(new Rect(x, by, bw, px(64)), "Rematch  [R]")) Rematch();
            if (EncounterUi.KeyButton(new Rect(x + bw + px(20), by, bw, px(64)), "Return  [Enter / Esc]")) Return();
        }

        private void DrawWardRow(Rect r, string label, int[] ward, Color accent)
        {
            float px(float v) => EncounterUi.Px(v);
            EncounterUi.Label(new Rect(r.x, r.y, px(220), r.height), label, EncounterUi.Body, accent);
            for (int i = 0; i < Tuning.Loci; i++)
            {
                var cr = new Rect(r.x + px(230) + i * px(160), r.y, px(150), r.height);
                EncounterUi.EssenceChip(cr, ward != null && i < ward.Length ? ward[i] : -1);
            }
        }
    }
}
