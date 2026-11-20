using UnityEngine;
using World.Sim.War;

namespace World.Game
{
    /// <summary>What a soldier is doing right now (one-shot actions over the walk/idle layer).</summary>
    public enum SoldierAction { Idle, Walk, Swing, Block, Dodge, Hit, Purge, Cheer, Die }

    /// <summary>
    /// A soldier of the war (G3-3; PRD_GODBOARD WA-3, WA-9): a primitive body in the people's colour with gear that follows
    /// its look (Brute, Raider, Shieldbearer, Skirmisher, Warrior) and its stat levels. Animated procedurally: walk, idle, swing,
    /// block, dodge, hit stagger, purge slam, cheer, death. Wear is readable without bars: a stagger on a hit, a limp when
    /// Hurt, a dropped shield and a slow swing when BadlyHurt. On the god board it also shows a flat coloured marker.
    /// It only reads what <see cref="WarViews"/> gives it; it never touches the sim.
    /// </summary>
    public sealed class SoldierView : MonoBehaviour
    {
        public const float MarkerRadius = 9f, MarkerY = 14f;

        public int Id { get; private set; }
        public int People { get; private set; }
        public SoldierLook Look { get; private set; }
        public readonly int[] Levels = new int[4];
        public Color Colour { get; private set; }
        public Wear Wear { get; private set; }
        public bool HasShield { get; private set; }
        public bool ShieldDropped { get; private set; }
        public bool Dying { get; private set; }
        public bool Finished { get; private set; }
        public SoldierAction Action => _act != SoldierAction.Idle && _act != SoldierAction.Walk ? _act : (_walking ? SoldierAction.Walk : SoldierAction.Idle);
        public int SwingsPlayed, HitsTaken, BlocksPlayed, DodgesPlayed, PurgesPlayed, CheersPlayed;
        /// <summary>Where the owner wants the soldier to stand, and which way to face when standing (NaN = keep).</summary>
        public Vector3 Target;
        public float FaceYaw = float.NaN;
        public bool SimMoving;
        public bool PurgeLoop;
        public bool ShowMarker { set { if (_marker != null && _marker.activeSelf != value) _marker.SetActive(value); } }
        public bool MarkerShown => _marker != null && _marker.activeSelf;
        public Transform BodyPivot => _body;
        public float SlowFactor => Wear == Wear.BadlyHurt ? 1.7f : Wear == Wear.Hurt ? 1.2f : 1f;

        private Transform _body, _legL, _legR, _armR, _armL;
        private GameObject _shield, _marker;
        private SoldierAction _act = SoldierAction.Idle;
        private float _actT, _actDur, _pendingAt = -1f;
        private SoldierAction _pending;
        private bool _walking, _wasMoving;
        private float _moved;
        private float _phase, _speed, _dropT = -1f, _clock, _bulk = 1f, _purgeClock;
        private Vector3 _last;

        public static SoldierView Create(Transform parent, Soldier s, Color col, SoldierLook look)
        {
            var go = new GameObject("Soldier " + s.Id + " " + look);
            go.transform.SetParent(parent, false);
            go.layer = 2;
            var v = go.AddComponent<SoldierView>();
            v.Id = s.Id; v.People = s.People; v.Look = look; v.Colour = col;
            for (int i = 0; i < 4; i++) v.Levels[i] = s.Levels[i];
            v.Build();
            return v;
        }

        // ---------------------------------------------------------------- build

        private static string Key(string part, SoldierLook look, Color c, int[] lv)
            => "sol/" + part + "/" + (int)look + "/" + ColorUtility.ToHtmlStringRGB(c) + "/" + lv[0] + lv[1] + lv[2] + lv[3];

        private void Build()
        {
            _phase = (Id * 0.73f) % 6.28f;
            // Bulk by look and health level; a Skirmisher is lean, a Brute broad.
            float h = Look == SoldierLook.Brute ? 1.1f : Look == SoldierLook.Skirmisher ? 0.95f : 1f;
            float w = (Look == SoldierLook.Brute ? 1.3f : Look == SoldierLook.Skirmisher ? 0.88f : 1f) * (1f + 0.05f * Levels[Stat.Health]);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _body.localScale = new Vector3(w, h, w);
            _bulk = h;
            HasShield = Look == SoldierLook.Shieldbearer || Look == SoldierLook.Warrior || Levels[Stat.Defence] >= 2;
            var lv = Levels; var col = Colour; var look = Look; bool shield = HasShield;
            LandLook.Part(_body, "Torso", LandLook.Cached(Key("torso", look, col, lv), mb => SoldierMeshes.Torso(mb, look, col, lv)), null, true);
            _legL = Pivot("LegL", new Vector3(-0.12f, 0.84f, 0));
            _legR = Pivot("LegR", new Vector3(0.12f, 0.84f, 0));
            LandLook.Part(_legL, "Mesh", LandLook.Cached("sol/leg/" + ColorUtility.ToHtmlStringRGB(col), mb => SoldierMeshes.Leg(mb, col)), null, true);
            LandLook.Part(_legR, "Mesh", LandLook.Cached("sol/leg/" + ColorUtility.ToHtmlStringRGB(col), mb => SoldierMeshes.Leg(mb, col)), null, true);
            _armR = Pivot("ArmR", new Vector3(0.33f, 1.32f, 0));
            LandLook.Part(_armR, "Mesh", LandLook.Cached(Key("armR", look, col, lv), mb => SoldierMeshes.WeaponArm(mb, look, col, lv)), null, true);
            _armL = Pivot("ArmL", new Vector3(-0.33f, 1.32f, 0));
            LandLook.Part(_armL, "Mesh", LandLook.Cached(Key("armL", look, col, lv), mb => SoldierMeshes.ShieldArm(mb, look, col, lv, shield)), null, true);
            if (shield)
                _shield = LandLook.Part(_armL, "Shield", LandLook.Cached(Key("shield", look, col, lv), mb => SoldierMeshes.Shield(mb, look, col, lv)), null, true);
            if (shield) { _shield.transform.localPosition = new Vector3(0, -0.5f, 0.22f); _shield.transform.localRotation = Quaternion.Euler(90f, 0, 0); }

            _marker = new GameObject("Marker");
            _marker.transform.SetParent(transform, false);
            _marker.transform.localPosition = new Vector3(0, MarkerY, 0);
            _marker.layer = 2;
            LandLook.Part(_marker.transform, "Disc", LandLook.Cached("sol/marker/" + ColorUtility.ToHtmlStringRGB(col) + "/" + (int)look, mb =>
            {
                LandShapes.Disc(mb, Vector3.zero, MarkerRadius + 1.6f, 16, new Color(0.05f, 0.04f, 0.04f));
                LandShapes.Disc(mb, Vector3.up * 0.2f, MarkerRadius, 16, col);
                // A pip in the look's colour: red Raider, blue Shieldbearer, green Skirmisher, brown Brute, none for the plain Warrior.
                if (look != SoldierLook.Warrior) LandShapes.Disc(mb, Vector3.up * 0.4f, 3.8f, 10, SoldierMeshes.LookColour(look));
            }), LandLook.Unlit, false);
            _marker.SetActive(false);
            _last = transform.position;
        }

        private Transform Pivot(string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_body, false);
            t.localPosition = at;
            return t;
        }

        // ---------------------------------------------------------------- commands from the owner

        public void SetWear(Wear w)
        {
            if (w == Wear) return;
            Wear = w;
            if (w == Wear.BadlyHurt && HasShield && !ShieldDropped) DropShield();
        }

        private void DropShield()
        {
            ShieldDropped = true;
            if (_shield == null) return;
            // The shield falls off the arm and lies on the ground where it dropped.
            var wp = _shield.transform.position; var wr = _shield.transform.rotation;
            _shield.transform.SetParent(transform.parent, true);
            _shield.transform.SetPositionAndRotation(wp, wr);
            _shield.AddComponent<FallingGear>().Begin(wp.y);
            _shield = null;
        }

        /// <summary>Starts an action now (replaces a running one, except death).</summary>
        public void Play(SoldierAction a, float delay = 0f)
        {
            if (Dying) return;
            if (delay > 0f) { _pending = a; _pendingAt = _clock + delay; return; }
            _act = a; _actT = 0f;
            float k = SlowFactor;
            switch (a)
            {
                case SoldierAction.Swing: _actDur = 0.7f * k; SwingsPlayed++; break;
                case SoldierAction.Hit: _actDur = 0.55f; HitsTaken++; break;
                case SoldierAction.Block: _actDur = 0.45f; BlocksPlayed++; break;
                case SoldierAction.Dodge: _actDur = 0.5f; DodgesPlayed++; break;
                case SoldierAction.Purge: _actDur = 1.0f * k; PurgesPlayed++; break;
                case SoldierAction.Cheer: _actDur = 1.5f; CheersPlayed++; break;
                case SoldierAction.Die: _actDur = FallEnd; Dying = true; break;
                default: _actDur = 0f; break;
            }
        }

        public void Die() { Play(SoldierAction.Die); _actT = 0f; }
        public void SnapTo(Vector3 p) { transform.position = p; _last = p; }
        public bool Busy => _act != SoldierAction.Idle && _act != SoldierAction.Walk;

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            if (_pendingAt >= 0f && _clock >= _pendingAt) { _pendingAt = -1f; Play(_pending); }

            // Position: follow the target (a step in the sim is a straight line, so it follows closely).
            if (!Dying)
            {
                var pos = transform.position;
                float maxStep = (SimMoving ? 120f : 10f) * dt;
                pos = Vector3.MoveTowards(pos, new Vector3(Target.x, Target.y, Target.z), maxStep);
                transform.position = pos;
                var d = pos - _last; d.y = 0;
                _moved = d.magnitude;
                float sp = dt > 0f ? d.magnitude / dt : 0f;
                _speed = Mathf.Lerp(_speed, sp, 0.3f);
                _walking = SimMoving || _speed > 0.6f;
                if (d.sqrMagnitude > 1e-6f && (SimMoving || float.IsNaN(FaceYaw) || _speed > 2f) && !Busy)
                    FaceToward(BoardSpace.YawOf(d), 540f * dt);
                else if (!float.IsNaN(FaceYaw)) FaceToward(FaceYaw, 480f * dt);
                _last = pos;
            }
            if (PurgeLoop && !Dying && !Busy)
            {
                _purgeClock += dt;
                if (_purgeClock > 1.7f * SlowFactor) { _purgeClock = 0f; Play(SoldierAction.Purge); }
            }
            Animate(dt);
            if (Dying && _actT >= FadeStart) Dissolve(Mathf.Clamp01((_actT - FadeStart) / (DeathSeconds - FadeStart)));
            if (Dying && _actT > DeathSeconds) Finished = true;
        }

        private void FaceToward(float yaw, float maxDeg)
        {
            float cur = transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(cur, yaw, maxDeg), 0);
        }

        private static float Ease(float u) => u * u * (3f - 2f * u);

        private void Animate(float dt)
        {
            if (_act != SoldierAction.Idle && _act != SoldierAction.Walk)
            {
                _actT += dt;
                if (_act != SoldierAction.Die && _actT >= _actDur) { _act = SoldierAction.Idle; _actT = 0f; }
            }
            float walkAmp = _walking ? 1f : 0f;
            float slow = Wear == Wear.BadlyHurt ? 0.6f : Wear == Wear.Hurt ? 0.85f : 1f;
            // P1: the stride follows the ground covered (feet do not slide), within a sane cadence; a march on the spot keeps stepping.
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);
            float stepLen = Gait.StepLength(0.84f, 32f * slow);
            float rate = _walking ? Mathf.Clamp(Mathf.PI * (_moved / scale) / stepLen, dt * 5f * slow, dt * 16f) : dt * 1.2f;
            _phase += rate; _moved = 0f;
            float s = Mathf.Sin(_phase);
            // The limp: the left leg takes short, stiff steps; the body dips when the weight lands on it. Worse when badly hurt.
            float limp = Wear == Wear.BadlyHurt ? 1f : Wear == Wear.Hurt ? 0.55f : 0f;
            float legL = s * 38f * walkAmp * (1f - 0.55f * limp);
            float legR = -s * 38f * walkAmp;
            float dip = limp * (_walking ? Mathf.Max(0f, -s) * 0.14f : 0.05f);
            float lean = limp * (Wear == Wear.BadlyHurt ? 16f : 7f);
            float armRx = Wear == Wear.BadlyHurt ? 28f : 8f, armLx = -25f; // a badly hurt soldier lets the weapon sag
            if (_walking) armRx += -s * 22f * walkAmp;
            float bodyPitch = lean + (_walking ? 3f : 0f);
            float bodyRoll = limp * 6f * (_walking ? s : 0f);
            float bodyYaw = 0f;
            var bodyPos = new Vector3(0, -dip + (_walking && limp < 0.1f ? Mathf.Abs(s) * 0.07f : 0f), 0);
            float armLz = 0f, armLy = 0f, armRz = 0f;
            float u = _actDur > 0f ? Mathf.Clamp01(_actT / _actDur) : 0f;

            switch (_act)
            {
                case SoldierAction.Swing:
                    if (u < 0.45f) { float k = Ease(u / 0.45f); armRx = Mathf.Lerp(armRx, -75f, k); bodyPitch -= 8f * k; bodyYaw = -18f * k; }
                    else if (u < 0.68f) { float k = (u - 0.45f) / 0.23f; armRx = Mathf.Lerp(-75f, 115f, k * k); bodyPitch += 22f * k - 8f; bodyYaw = Mathf.Lerp(-18f, 20f, k); bodyPos.z = 0.45f * k; }
                    else { float k = Ease((u - 0.68f) / 0.32f); armRx = Mathf.Lerp(115f, 8f, k); bodyPitch += 14f * (1f - k); bodyYaw = Mathf.Lerp(20f, 0f, k); bodyPos.z = 0.45f * (1f - k); }
                    break;
                case SoldierAction.Purge:
                    if (u < 0.5f) { float k = Ease(u / 0.5f); armRx = Mathf.Lerp(armRx, -155f, k); armLx = Mathf.Lerp(armLx, -140f, k); bodyPitch -= 10f * k; }
                    else { float k = Mathf.Clamp01((u - 0.5f) / 0.18f); armRx = Mathf.Lerp(-155f, 95f, k); armLx = Mathf.Lerp(-140f, -30f, k); bodyPitch += 26f * k - 10f; bodyPos.z = 0.3f * k; if (u > 0.85f) { float r = (u - 0.85f) / 0.15f; armRx = Mathf.Lerp(95f, 8f, r); bodyPitch = Mathf.Lerp(bodyPitch, lean, r); bodyPos.z *= 1f - r; } }
                    break;
                case SoldierAction.Hit:
                {
                    float shake = Mathf.Sin(u * 22f) * (1f - u);
                    float k = Mathf.Sin(u * Mathf.PI);
                    float mag = Wear == Wear.Fresh ? 1f : 1.35f; // a hurt soldier staggers harder
                    bodyPitch += -22f * k * mag; bodyPos.z = -0.55f * k * mag; bodyRoll += shake * 9f; armRx += -18f * k; armLx += 12f * k;
                    break;
                }
                case SoldierAction.Block:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(u * 1.4f) * Mathf.PI * 0.5f) * (1f - Mathf.Clamp01((u - 0.55f) / 0.45f));
                    armLx = Mathf.Lerp(armLx, -85f, k); armLy = -35f * k; armLz = 10f * k; bodyPitch += -6f * k; bodyPos.z = -0.18f * Mathf.Sin(u * Mathf.PI);
                    break;
                }
                case SoldierAction.Dodge:
                {
                    float k = Mathf.Sin(u * Mathf.PI);
                    bodyPos.x = 0.9f * k; bodyRoll += -16f * k; bodyPitch += 5f * k; armRx += -25f * k;
                    break;
                }
                case SoldierAction.Cheer:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(u * 6f) * Mathf.PI * 0.5f) * (1f - Mathf.Clamp01((u - 0.85f) / 0.15f));
                    float bounce = Mathf.Abs(Mathf.Sin(_actT * 9f)) * 0.22f * k;
                    armRx = Mathf.Lerp(armRx, -165f, k); armLx = Mathf.Lerp(armLx, -165f, k); bodyPos.y += bounce;
                    break;
                }
                case SoldierAction.Die:
                {
                    // P3: a real death. Stagger back a step or two (arms thrown out), the knees give (the body drops), fall
                    // to the ground with a small bounce, lie still, then dissolve (Dissolve, no shrinking).
                    float fwd = (Id & 1) == 0 ? 1f : -1f; // some fall on their face, some on their back
                    float st = Mathf.Clamp01(_actT / StaggerEnd);
                    float stagger = Mathf.Sin(st * Mathf.PI);
                    float drop = Ease(Mathf.Clamp01((_actT - StaggerEnd * 0.6f) / 0.35f));
                    float fall = Mathf.Clamp01((_actT - StaggerEnd) / (FallEnd - StaggerEnd));
                    float k = fall * fall; // accelerates into the ground
                    float bounce = _actT > FallEnd ? Mathf.Sin(Mathf.Clamp01((_actT - FallEnd) / 0.25f) * Mathf.PI) * 0.12f : 0f;
                    bodyPitch = Mathf.Lerp(-16f * stagger * fwd - 6f, 86f * fwd, k) + bounce * 20f * -fwd;
                    bodyRoll = Mathf.Sin(st * Mathf.PI * 3f) * 10f * (1f - k) + 8f * k;
                    bodyPos.z = -0.45f * st * fwd + (fwd > 0 ? 0.15f : -0.35f) * k;
                    bodyPos.y = -0.28f * drop * (1f - k) + 0.16f * k + bounce * 0.15f;
                    armRx = Mathf.Lerp(Mathf.Lerp(armRx, -70f, stagger), fwd > 0 ? -150f : -30f, k);
                    armLx = Mathf.Lerp(Mathf.Lerp(armLx, -100f, stagger), fwd > 0 ? -140f : 20f, k);
                    armRz = 30f * stagger + 25f * k; armLz = -30f * stagger - 20f * k;
                    legL = 22f * stagger * fwd * (1f - k) + 10f * k; legR = -12f * stagger * fwd * (1f - k) - 6f * k;
                    break;
                }
            }
            _body.localPosition = bodyPos;
            float baseScaleY = _bulk;
            var sc = _body.localScale; _body.localScale = new Vector3(sc.x, baseScaleY, sc.z);
            _body.localRotation = Quaternion.Euler(bodyPitch, bodyYaw, bodyRoll);
            _legL.localRotation = Quaternion.Euler(legL, 0, 0);
            _legR.localRotation = Quaternion.Euler(legR, 0, 0);
            _armR.localRotation = Quaternion.Euler(armRx, 0, 8f + armRz);
            _armL.localRotation = Quaternion.Euler(armLx * (HasShield ? 1f : 0.6f), armLy, -4f + armLz);
            if (_marker != null && Dying && _marker.activeSelf) _marker.SetActive(false);
        }

        /// <summary>P3 death timing (s): stagger, fall onto the ground, lie still, dissolve, gone.</summary>
        public const float StaggerEnd = 0.55f, FallEnd = 1.15f, FadeStart = 2.3f, DeathSeconds = 3.4f;
        /// <summary>Test probe: how far the body has dissolved (0..1).</summary>
        public float Dissolved { get; private set; }
        /// <summary>Test probe: the body's scale (the death never shrinks it).</summary>
        public Vector3 BodyScale => _body != null ? _body.localScale : Vector3.one;

        private static Material _dissolveMat;
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private MaterialPropertyBlock _mpb;

        /// <summary>The body's parts switch to the dissolving material (same look, WORLD_DISSOLVE) and vanish in blocks.</summary>
        private void Dissolve(float f)
        {
            if (_dissolveMat == null)
            {
                _dissolveMat = new Material(LandLook.Lit) { name = "Land lit dissolve" };
                _dissolveMat.EnableKeyword("WORLD_DISSOLVE");
            }
            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
                foreach (var r in _body.GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial == LandLook.Lit) r.sharedMaterial = _dissolveMat;
            }
            Dissolved = f;
            _mpb.SetFloat(DissolveId, f);
            foreach (var r in _body.GetComponentsInChildren<MeshRenderer>()) r.SetPropertyBlock(_mpb);
        }

        /// <summary>A pose probe for tests: the arm's pitch now.</summary>
        public float WeaponArmPitch => _armR != null ? NormAngle(_armR.localEulerAngles.x) : 0f;
        public float BodyPitch => _body != null ? NormAngle(_body.localEulerAngles.x) : 0f;
        public float LegSpread => _legL != null ? Mathf.Abs(NormAngle(_legL.localEulerAngles.x) - NormAngle(_legR.localEulerAngles.x)) : 0f;
        private static float NormAngle(float a) => a > 180f ? a - 360f : a;
    }

    /// <summary>A dropped shield: falls, lies flat for a while, then goes.</summary>
    public sealed class FallingGear : MonoBehaviour
    {
        private float _t, _y0, _life = 30f;
        public void Begin(float y) { _y0 = y; }
        private void Update()
        {
            _t += Time.deltaTime;
            var p = transform.position;
            float ground = 0.12f;
            p.y = Mathf.MoveTowards(p.y, ground, 6f * Time.deltaTime);
            transform.position = p;
            var e = transform.eulerAngles;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(80f, e.y, 0f), Mathf.Clamp01(Time.deltaTime * 5f));
            if (_t > _life) { transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, Time.deltaTime); if (transform.localScale.sqrMagnitude < 0.001f) Destroy(gameObject); }
        }
    }
}
