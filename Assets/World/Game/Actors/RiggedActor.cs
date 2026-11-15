using System;
using System.Collections.Generic;
using Bedsit;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace World.Game
{
    /// <summary>
    /// A rigged character body (PRD_CHARACTERS WP-7, 5.8): implements <see cref="ICharacterActor"/> so every existing
    /// call site works unchanged, plus <see cref="IExpressiveActor"/> (gaze, expressions, props, beats) and
    /// <see cref="IMotionShaper"/> (the walker's speed at the start and end of a walk, so the weight shifts before the
    /// first step and the feet plant at the stop).
    ///
    /// Animation is a PlayableGraph evaluated by hand in LateUpdate (after <see cref="Npc"/> has moved the root):
    /// layer 0 = idle variety, the gait blend (Walk, WalkBrisk, Jog, Run, phase-synchronised and played at the speed
    /// that matches the measured ground speed), the start, stop and turn clips (timed by their manifest root curves) and
    /// full-body gestures; layer 1 = gestures on the upper body while walking; layer 2 = the talk loop (upper body);
    /// layer 3 = additive clips (nod, lean into turns, glance back). Clip events come from the manifest, so each fires
    /// exactly once per crossing, with a beat id. Face, gaze and cloth run after the graph.
    /// </summary>
    public sealed class RiggedActor : MonoBehaviour, ICharacterActor, IExpressiveActor, IMotionShaper
    {
        // ---------------- creation ----------------

        /// <summary>Instantiates the body prefab for an actor id. Null on any failure (the factory then builds the placeholder).</summary>
        public static RiggedActor Build(string id, Color colour, GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false);
            go.name = "Actor " + id;
            var rc = go.GetComponent<RiggedCharacter>();
            if (rc == null || rc.Animator == null || rc.Body == null) { Destroy(go); return null; }
            var a = go.AddComponent<RiggedActor>();
            a.ActorId = id; a.TunicColor = colour;
            a.Init(rc);
            return a;
        }

        // ---------------- ICharacterActor ----------------

        public string ActorId { get; private set; }
        public Transform Root => transform;
        public string CurrentKey { get; private set; } = ActorKeys.Idle;
        public bool IsPlayingOneShot => _oneShotKey != null;
        public event Action<string> OneShotFinished;
        public Color TunicColor { get; private set; }
        public RiggedCharacter Character => _rc;
        public string CastKey => _rc != null ? _rc.Key : null;
        public string Costume => _rc != null ? _rc.Costume : null;
        public event Action<string, int> BeatEvent;

        /// <summary>Distance from the camera beyond which the face, gaze and cloth stop, and the graph runs every other frame.</summary>
        public static float NearLod = 25f;
        /// <summary>Beyond this (or off screen) the graph runs every fourth frame.</summary>
        public static float FarLod = 60f;
        /// <summary>Turn rate while walking and for small turns standing (degrees per second).</summary>
        public float WalkTurnRate = 420f, StandTurnRate = 220f;
        /// <summary>Acceleration used to reach the walker's speed after the start clip and to slow to a walk before a stop (m/s^2).</summary>
        public float Accel = 9f;

        private RiggedCharacter _rc;
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layers;
        private AnimationMixerPlayable _base;
        private Slot _trans, _gestFull, _gestUpper, _talk, _add;
        private readonly List<Slot> _idle = new List<Slot>();
        private readonly List<Slot> _gaits = new List<Slot>();
        private ActorFace _face;
        private ActorCloth _cloth;
        private Npc _npc;
        private readonly List<Transform> _props = new List<Transform>();

        private sealed class Slot
        {
            public AnimationMixerPlayable Mixer; public int Index; public bool Layer;
            public AnimationLayerMixerPlayable Layers;
            public AnimationClipPlayable Playable; public RigClip Clip; public float Time, PrevTime;
            public float Weight;
        }

        private void Init(RiggedCharacter rc)
        {
            _rc = rc;
            _npc = GetComponent<Npc>();
            var anim = rc.Animator;
            anim.applyRootMotion = false;
            anim.runtimeAnimatorController = null;
            _graph = PlayableGraph.Create("Actor " + ActorId);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", anim);
            _layers = AnimationLayerMixerPlayable.Create(_graph, 4);
            output.SetSourcePlayable(_layers);
            var idleNames = rc.Key == "mira" ? new[] { "Idle_Staff", "Idle_B", "Idle_Look" } : new[] { "Idle_A", "Idle_B", "Idle_Look" };
            var gaitNames = new[] { "Walk", "WalkBrisk", "Jog", "Run" };
            int n = idleNames.Length + gaitNames.Length + 2;
            _base = AnimationMixerPlayable.Create(_graph, n);
            _graph.Connect(_base, 0, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            int i = 0;
            foreach (var nm in idleNames) { var c = rc.Clip(nm); if (c != null) _idle.Add(MakeSlot(_base, i, c)); i++; }
            foreach (var nm in gaitNames) { var c = rc.Clip(nm); if (c != null) _gaits.Add(MakeSlot(_base, i, c)); i++; }
            _trans = MakeSlot(_base, i++, null);
            _gestFull = MakeSlot(_base, i++, null);
            var upper = AnimationMixerPlayable.Create(_graph, 1); _graph.Connect(upper, 0, _layers, 1);
            _gestUpper = MakeSlot(upper, 0, null);
            var talk = AnimationMixerPlayable.Create(_graph, 1); _graph.Connect(talk, 0, _layers, 2);
            _talk = MakeSlot(talk, 0, rc.Clip(rc.TalkClip) ?? rc.Clip("Talk_A"));
            var add = AnimationMixerPlayable.Create(_graph, 1); _graph.Connect(add, 0, _layers, 3);
            _add = MakeSlot(add, 0, null);
            if (rc.UpperBody != null && rc.Humanoid)
            {
                _layers.SetLayerMaskFromAvatarMask(1, rc.UpperBody);
                _layers.SetLayerMaskFromAvatarMask(2, rc.UpperBody);
            }
            _layers.SetLayerAdditive(3, true);
            _idleIndex = 0;
            _idleSwitchAt = Time.time + UnityEngine.Random.Range(7f, 12f);
            if (_idle.Count > 0) _idle[0].Time = UnityEngine.Random.Range(0f, _idle[0].Clip.Length);
            _yaw = transform.eulerAngles.y;
            _lastPos = transform.position;
            NightMaterials();
            _graph.Evaluate(0f);
            _face = new ActorFace(rc, transform);
            _cloth = new ActorCloth(rc, transform);
        }

        private Slot MakeSlot(AnimationMixerPlayable mixer, int index, RigClip clip)
        {
            var s = new Slot { Mixer = mixer, Index = index };
            SetClip(s, clip);
            return s;
        }

        private void SetClip(Slot s, RigClip clip)
        {
            if (s.Clip == clip && s.Playable.IsValid()) return;
            if (s.Playable.IsValid())
            {
                _graph.Disconnect(s.Mixer, s.Index);
                s.Playable.Destroy();
            }
            s.Clip = clip; s.Time = 0f; s.PrevTime = 0f;
            if (clip == null || clip.Clip == null) { s.Mixer.SetInputWeight(s.Index, 0f); return; }
            s.Playable = AnimationClipPlayable.Create(_graph, clip.Clip);
            s.Playable.SetApplyFootIK(true);
            s.Playable.Pause();
            _graph.Connect(s.Playable, 0, s.Mixer, s.Index);
            s.Mixer.SetInputWeight(s.Index, 0f);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        // ---------------- materials and lights ----------------

        private static readonly Dictionary<long, Material> NightCache = new Dictionary<long, Material>();
        private static readonly int SelfLitId = Shader.PropertyToID("_SelfLit"), RimId = Shader.PropertyToID("_Rim");

        /// <summary>
        /// Night readability from the lighting package (LightShapes' Person variant: self-lit share and cool rim, both
        /// scaled by the global _WorldNight, so daylight is untouched), per role slot: the eye whites and irises keep more of
        /// their own colour so eyes read in firelight, the glow slot stays fully self-lit.
        /// </summary>
        private void NightMaterials()
        {
            var mats = _rc.Body.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string slot = i < _rc.SubmeshSlots.Length ? _rc.SubmeshSlots[i] : "";
                mats[i] = NightVariant(mats[i], slot);
            }
            _rc.Body.sharedMaterials = mats;
        }

        public static Material NightVariant(Material src, string slot)
        {
            if (src == null || slot == "emissive" || !src.HasProperty(SelfLitId)) return src;
            long key = ((long)src.GetInstanceID() << 8) ^ slot.GetHashCode();
            if (NightCache.TryGetValue(key, out var m) && m != null) return m;
            float baseSelf = LightShapes.SelfLit(LightShapes.Read.Person);
            float self = slot == "eye_white" ? 0.6f : slot == "iris" ? 0.4f : slot == "skin" ? baseSelf + 0.06f : slot == "eye_dark" ? 0.05f : baseSelf;
            var rim = slot == "eye_white" || slot == "iris" || slot == "eye_dark" ? Color.black : LightShapes.Rim(LightShapes.Read.Person);
            m = new Material(src) { name = src.name + " (night)" };
            m.SetFloat(SelfLitId, self);
            m.SetColor(RimId, rim);
            NightCache[key] = m;
            return m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NightCache.Clear();

        // ---------------- ICharacterActor methods ----------------

        private string _oneShotKey;
        private readonly List<string> _queue = new List<string>();
        private RigClip _gesture;
        private float _gestureW, _gestureT;
        private bool _gestureAdditive;
        private Vector3? _pointAt;
        private int _beatId;

        public void PlayIdle()
        {
            ForceIdle();
        }

        /// <summary>Logical keys (Bedsit.AnimationKeys plus cast, beckon, hammer) to clips of the PRD's set.</summary>
        public bool PlayOneShot(string key)
        {
            _queue.Clear();
            switch (key)
            {
                case ActorKeys.Wave:
                case ActorKeys.Greet: return Begin(key, "Greet");
                case ActorKeys.Beckon: return Begin(key, "Beckon");
                case ActorKeys.Cheer: return Begin(key, "Victory");
                case ActorKeys.Surprise: return Begin(key, "Surprise");
                case ActorKeys.Clap: return Begin(key, "Talk_Emphasis");
                case ActorKeys.Stretch: return Begin(key, "Idle_Look");
                case ActorKeys.Hammer: return Begin(key, "Nod");
                case ActorKeys.Cast:
                    if (!Begin(key, "CastAnticipate")) return false;
                    _queue.Add("CastRelease"); _queue.Add("CastRecover");
                    return true;
                default:
                    return false;
            }
        }

        public bool PlayBeat(string clipName)
        {
            _queue.Clear();
            return Begin(clipName, clipName);
        }

        private bool Begin(string key, string clipName)
        {
            var c = _rc.Clip(clipName);
            if (c == null) return false;
            if (_oneShotKey != null && _oneShotKey != key) { var old = _oneShotKey; _oneShotKey = null; OneShotFinished?.Invoke(old); }
            _oneShotKey = key;
            CurrentKey = key;
            StartGesture(c);
            return true;
        }

        private void StartGesture(RigClip c)
        {
            _beatId++;
            _gesture = c; _gestureT = 0f;
            _gestureAdditive = c.Additive;
            if (c.Additive) { SetClip(_add, c); _add.Time = 0f; }
            else
            {
                SetClip(_gestFull, c); SetClip(_gestUpper, c);
                _gestFull.Time = _gestUpper.Time = 0f;
                _gestFull.PrevTime = _gestUpper.PrevTime = -1e-4f;
            }
        }

        private void EndGesture()
        {
            var key = _oneShotKey;
            _gesture = null; _oneShotKey = null; _queue.Clear();
            CurrentKey = ActorKeys.Idle;
            if (key != null) OneShotFinished?.Invoke(key);
        }

        public void FaceTowards(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position; d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return;
            _targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _turning = true;
        }

        public void SetYaw(float yaw)
        {
            _yaw = _targetYaw = yaw; _turning = false; _turnClip = null;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        // ---------------- IExpressiveActor ----------------

        private Transform _gazeTransform;
        private Vector3? _gazeWorld;
        private bool _explicitGaze;
        private string _expression; private float _expressionWeight; private bool _explicitExpression;
        private float _speedHint = -1f;
        private bool? _talkOverride;

        public void SetGaze(Transform target) { _gazeTransform = target; _gazeWorld = null; _explicitGaze = target != null; }
        public void SetGaze(Vector3? point) { _gazeTransform = null; _gazeWorld = point; _explicitGaze = point.HasValue; }
        public Vector3 GazePoint
        {
            get
            {
                if (_face == null) return transform.position + transform.forward * 3f;
                float dist = _face.Target.HasValue ? Vector3.Distance(_face.EyeCentre, _face.Target.Value) : 3f;
                return _face.EyeCentre + _face.EyeForward * dist;
            }
        }
        /// <summary>The point the gaze is currently going for (null: none).</summary>
        public Vector3? GazeTarget => _face != null ? _face.Target : null;
        public ActorFace Face => _face;

        public bool SetExpression(string name, float weight = 1f)
        {
            if (_face == null || !_face.SetExpression(name, weight)) return false;
            _expression = name; _expressionWeight = weight; _explicitExpression = name != null && name != "neutral";
            return true;
        }

        public void ResetExpression() { _explicitExpression = false; _expression = null; _face?.SetExpression(null, 0f); }
        public string CurrentExpression => _face != null ? _face.ExpressionName : "neutral";
        public void SetSpeedHint(float metresPerSecond) { _speedHint = metresPerSecond; }
        public float GroundSpeed => _speed;

        public Transform Socket(string name) => _rc != null ? _rc.Socket(name) : null;

        public bool Attach(Transform prop, string socket, Vector3 localPosition = default, Quaternion? localRotation = null)
        {
            var s = Socket(socket);
            if (s == null || prop == null) return false;
            prop.SetParent(s, false);
            prop.localPosition = localPosition;
            prop.localRotation = localRotation ?? Quaternion.identity;
            if (!_props.Contains(prop)) _props.Add(prop);
            return true;
        }

        public void DetachAll(string socket = null)
        {
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                var p = _props[i];
                if (p == null) { _props.RemoveAt(i); continue; }
                if (socket != null && (p.parent == null || p.parent.name != socket)) continue;
                _props.RemoveAt(i);
                Destroy(p.gameObject);
            }
        }

        public void PointAt(Vector3? worldPoint)
        {
            if (worldPoint.HasValue && !_pointAt.HasValue)
            {
                _pointAt = worldPoint;
                if (_gesture == null || _gesture.Name != "PointGround") { _queue.Clear(); Begin("point", "PointGround"); }
            }
            _pointAt = worldPoint;
        }

        public void SetTalking(bool? talking) { _talkOverride = talking; }
        public bool IsTalking { get; private set; }

        public void ForceIdle()
        {
            _pointAt = null;
            if (_gesture != null || _oneShotKey != null) EndGesture();
            _gestureW = 0f;
            CurrentKey = ActorKeys.Idle;
        }

        public void ResetPresentation()
        {
            ForceIdle();
            SetGaze((Vector3?)null);
            ResetExpression();
            _talkOverride = null;
            _speedHint = -1f;
            DetachAll();
        }

        /// <summary>Receiver for animation events stored in the FBX clips (unused: events are fired from the manifest times).</summary>
        public void OnActorEvent(string name) { }

        /// <summary>
        /// The hook a costume switch needs (WP-18, PRD_CHARACTERS C1): swaps this live actor's body to another costume
        /// of the same cast key (same rig, same clips) without touching its position, facing or state. False when that
        /// costume has no prefab.
        /// </summary>
        public bool SwapCostume(string costume)
        {
            if (_rc == null || costume == _rc.Costume) return false;
            var prefab = CastRegistry.LoadPrefab(_rc.Key, costume);
            if (prefab == null || prefab.GetComponent<RiggedCharacter>().Costume != costume) return false;
            var tmp = Instantiate(prefab);
            var nrc = tmp.GetComponent<RiggedCharacter>();
            // Move the new body's children (mesh and armature) under this root, drop the old ones, take its data.
            var oldChildren = new List<Transform>();
            foreach (Transform c in transform) if (!_props.Contains(c)) oldChildren.Add(c);
            var keepProps = new List<(Transform prop, string socket)>();
            foreach (var p in _props) if (p != null && p.parent != null) keepProps.Add((p, p.parent.name));
            foreach (var kp in keepProps) kp.prop.SetParent(null, true);
            var moved = new List<Transform>();
            foreach (Transform c in tmp.transform) moved.Add(c);
            foreach (var c in moved) c.SetParent(transform, false);
            foreach (var c in oldChildren) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            var anim = _rc.Animator;
            anim.avatar = nrc.Animator.avatar;
            var data = JsonUtility.ToJson(nrc);
            JsonUtility.FromJsonOverwrite(data, _rc);
            _rc.Animator = anim;
            _rc.Body = nrc.Body;
            foreach (var f in typeof(RiggedCharacter).GetFields())
                if (typeof(Transform).IsAssignableFrom(f.FieldType) || f.FieldType == typeof(Transform[]) || f.FieldType == typeof(SkinnedMeshRenderer))
                    f.SetValue(_rc, f.GetValue(nrc));
            Destroy(tmp);
            if (_graph.IsValid()) _graph.Destroy();
            _idle.Clear(); _gaits.Clear();
            anim.Rebind();
            Init(_rc);
            foreach (var kp in keepProps) Attach(kp.prop, kp.socket, kp.prop.localPosition, kp.prop.localRotation);
            return true;
        }

        // ---------------- motion shaping (called by Npc before it moves) ----------------

        private enum Phase { Stand, Lead, StartClip, Cruise, Stop }
        private Phase _phase = Phase.Stand;
        private RigClip _startClip, _stopClip;
        private float _phaseT, _shapeV, _shapeDist, _stopFrom, _stopTravel;
        private bool _walkingNow, _leftNext = true;
        private float _remaining;

        /// <summary>
        /// Shapes the walker's speed: a short weight shift before the first step (the start clip's frames with no ground
        /// distance), then the start clip's own distance curve, then a steady acceleration to the walker's speed; near
        /// the goal a steady deceleration to walking pace and the stop clip's distance curve, so the stop lands on the
        /// goal with the planted foot settling. Turns of 40 degrees or more from standing finish before the first step.
        /// </summary>
        public float SpeedScale(bool walking, float speed, float remaining)
        {
            float dt = Time.deltaTime;
            _walkingNow = walking;
            _remaining = remaining;
            if (!walking || speed <= 0f || dt <= 0f)
            {
                if (_phase != Phase.Stop) _phase = Phase.Stand;
                return 1f;
            }
            var startClip = _rc.Clip(_leftNext ? "StartL" : "StartR");
            float walkNative = Native("Walk", 1.4f);
            switch (_phase)
            {
                case Phase.Stand:
                case Phase.Stop:
                    if (_phase == Phase.Stop && _stopClip != null && _phaseT < _stopClip.Length * 0.5f) { _phase = Phase.Cruise; _shapeV = walkNative; break; }
                    if (startClip == null || startClip.RootDist.Length < 2 || remaining < 0.6f) { _phase = Phase.Cruise; _shapeV = speed; break; }
                    _phase = Phase.Lead; _startClip = startClip; _phaseT = 0f; _shapeDist = 0f; _leftNext = !_leftNext;
                    break;
            }
            float scale = 1f;
            switch (_phase)
            {
                case Phase.Lead:
                    // Hold still while a standing turn finishes, then play the weight shift (no ground distance yet).
                    if (_turnClip != null) return 0f;
                    _phaseT += dt;
                    if (DistAt(_startClip, _phaseT) > 0.0005f) _phase = Phase.StartClip;
                    return 0f;
                case Phase.StartClip:
                {
                    _phaseT += dt;
                    float want = DistAt(_startClip, _phaseT) * Mathf.Max(1f, Mathf.Min(speed, walkNative * 1.6f) / walkNative);
                    float step = Mathf.Max(0f, want - _shapeDist);
                    _shapeDist = want;
                    if (_phaseT >= _startClip.Length) { _phase = Phase.Cruise; _shapeV = Mathf.Max(walkNative, step / dt); }
                    scale = Mathf.Clamp01(step / (speed * dt));
                    break;
                }
                case Phase.Cruise:
                    _shapeV = Mathf.MoveTowards(_shapeV, speed, Accel * dt);
                    scale = Mathf.Clamp01(_shapeV / speed);
                    break;
            }
            if (_phase == Phase.Cruise || _phase == Phase.StartClip)
            {
                // Slow to walking pace before the stop: v^2 = vw^2 + 2 a (remaining - stop travel).
                var stop = _rc.Clip("StopL");
                float travel = stop != null && stop.RootDist.Length > 0 ? stop.RootDist[stop.RootDist.Length - 1] : 0.3f;
                if (!float.IsInfinity(remaining))
                {
                    float vAllowed = Mathf.Sqrt(walkNative * walkNative + 2f * Accel * Mathf.Max(0f, remaining - travel));
                    scale = Mathf.Min(scale, vAllowed / speed);
                    _shapeV = Mathf.Min(_shapeV, vAllowed);
                    if (remaining <= travel + 0.02f && _phase == Phase.Cruise && stop != null)
                    {
                        // Stop clip by foot phase: StopL enters at phase 0, StopR at 0.5.
                        var l = _rc.Clip("StopL"); var r = _rc.Clip("StopR");
                        float pl = l != null ? l.EnterPhase : 0f, pr = r != null ? r.EnterPhase : 0.5f;
                        float dl = Mathf.Abs(Mathf.DeltaAngle(_gaitPhase * 360f, pl * 360f)), dr = Mathf.Abs(Mathf.DeltaAngle(_gaitPhase * 360f, pr * 360f));
                        _stopClip = (dl <= dr ? l : r) ?? stop;
                        _phase = Phase.Stop; _phaseT = 0f; _shapeDist = 0f; _stopFrom = remaining;
                        _stopTravel = Mathf.Max(0.01f, _stopClip.RootDist.Length > 0 ? _stopClip.RootDist[_stopClip.RootDist.Length - 1] : travel);
                    }
                }
            }
            if (_phase == Phase.Stop)
            {
                _phaseT += dt;
                float want = DistAt(_stopClip, _phaseT) / _stopTravel * _stopFrom;
                float step = Mathf.Max(0f, want - _shapeDist);
                _shapeDist = want;
                scale = _phaseT >= _stopClip.Length ? 1f : Mathf.Clamp01(step / (speed * dt));
                // Never stall short of the goal: once the clip's travel is spent, finish the last millimetres.
                if (DistAt(_stopClip, _phaseT) >= _stopTravel - 1e-4f) scale = Mathf.Max(scale, Mathf.Clamp01(remaining / (speed * dt)));
            }
            return scale;
        }

        private float Native(string clip, float fallback)
        {
            var c = _rc.Clip(clip);
            return c != null && c.NativeSpeed > 0f ? c.NativeSpeed : fallback;
        }

        /// <summary>Root distance of a start or stop clip at time t (linear between the 30 fps samples).</summary>
        public static float DistAt(RigClip c, float t)
        {
            if (c == null || c.RootDist.Length == 0) return 0f;
            float f = Mathf.Clamp(t * 30f, 0f, c.RootDist.Length - 1);
            int i = Mathf.FloorToInt(f);
            if (i >= c.RootDist.Length - 1) return c.RootDist[c.RootDist.Length - 1];
            return Mathf.Lerp(c.RootDist[i], c.RootDist[i + 1], f - i);
        }

        public static float YawAt(RigClip c, float t)
        {
            if (c == null || c.RootYaw.Length == 0) return 0f;
            float f = Mathf.Clamp(t * 30f, 0f, c.RootYaw.Length - 1);
            int i = Mathf.FloorToInt(f);
            if (i >= c.RootYaw.Length - 1) return c.RootYaw[c.RootYaw.Length - 1];
            return Mathf.Lerp(c.RootYaw[i], c.RootYaw[i + 1], f - i);
        }

        // ---------------- per frame ----------------

        private Vector3 _lastPos;
        private float _speed, _rawSpeed, _gaitPhase, _moveW, _transW, _talkW, _addW;
        private float _yaw, _targetYaw; private bool _turning;
        private RigClip _turnClip; private float _turnT, _turnFrom, _turnDelta, _turnDur;
        private int _idleIndex; private float _idleSwitchAt; private readonly float[] _idleW = new float[3];
        private int _frame; private float _pendingDt; private float _lodCheckAt; private int _lodStep = 1; private bool _faceOn = true;
        private float _talkCheckAt; private float _greetUntil;
        public string Phase_ => _phase.ToString();
        /// <summary>Current gait phase 0..1 (0 = left foot down) and the blend weights, for tests.</summary>
        public float GaitPhase => _gaitPhase;
        public bool IsTurningInPlace => _turnClip != null;

        private void LateUpdate()
        {
            if (_rc == null || !_graph.IsValid()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) { _graph.Evaluate(0f); return; }
            _frame++;
            // Speed from how far the root moved (or the hint).
            var p = transform.position;
            var dv = p - _lastPos; dv.y = 0f; _lastPos = p;
            float dist = dv.magnitude;
            if (dist > 3f) dist = 0f; // teleport
            _rawSpeed = dist / dt;
            float measured = _speedHint >= 0f ? _speedHint : _rawSpeed;
            _speed = Mathf.Lerp(_speed, measured, 1f - Mathf.Exp(-dt / 0.06f));
            if (_npc == null) _npc = GetComponent<Npc>();

            UpdateFacing(dt, dist);
            UpdateTalk();
            UpdateLod();
            UpdateWeights(dt, dist);
            _pendingDt += dt;
            if (_frame % _lodStep != 0) return;
            _graph.Evaluate(0f);
            _pendingDt = 0f;
            UpdateFaceAndGaze(dt * _lodStep);
            if (_faceOn) _cloth.Tick(dt * _lodStep);
        }

        private void UpdateLod()
        {
            if (Time.time < _lodCheckAt) return;
            _lodCheckAt = Time.time + 0.25f;
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
            bool visible = _rc.Body == null || _rc.Body.isVisible;
            bool duel = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            _faceOn = duel || d < NearLod;
            _lodStep = duel ? 1 : !visible || d > FarLod ? 4 : d > NearLod ? 2 : 1;
        }

        private void UpdateFacing(float dt, float dist)
        {
            bool moving = _walkingNow || _speed > 0.15f;
            if (_turnClip != null)
            {
                _turnT += dt;
                float k = Mathf.Clamp01(_turnT / _turnDur);
                float clipYaw = YawAt(_turnClip, k * _turnClip.Length); // + = left
                float end = YawAt(_turnClip, _turnClip.Length);
                float frac = Mathf.Abs(end) > 0.01f ? clipYaw / end : k;
                _yaw = _turnFrom + _turnDelta * frac;
                transform.rotation = Quaternion.Euler(0, _yaw, 0);
                if (k >= 1f) { _turnClip = null; _turning = Mathf.Abs(Mathf.DeltaAngle(_yaw, _targetYaw)) > 0.5f; }
                return;
            }
            _yaw = transform.eulerAngles.y;
            if (!_turning) return;
            float delta = Mathf.DeltaAngle(_yaw, _targetYaw);
            if (Mathf.Abs(delta) < 0.3f) { _turning = false; return; }
            bool standing = !moving && (_phase == Phase.Stand || _phase == Phase.Lead);
            bool busy = _gesture != null && !_gestureAdditive && _gesture.Name != "Greet" && _gesture.Name != "Beckon";
            if (standing && Mathf.Abs(delta) >= 40f && !busy)
            {
                var clip = _rc.Clip(delta < 0f ? "Turn60L" : "Turn60R");
                if (clip != null && clip.RootYaw.Length > 1)
                {
                    _turnClip = clip; _turnT = 0f; _turnFrom = _yaw; _turnDelta = delta;
                    _turnDur = clip.Length * Mathf.Clamp(Mathf.Abs(delta) / 60f, 0.8f, 2f);
                    SetClip(_trans, clip); _trans.Time = 0f; _trans.PrevTime = -1e-4f;
                    return;
                }
            }
            float rate = moving ? WalkTurnRate : StandTurnRate;
            _yaw = Mathf.MoveTowardsAngle(_yaw, _targetYaw, rate * dt);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
        }

        private void UpdateTalk()
        {
            if (Time.time < _talkCheckAt) return;
            _talkCheckAt = Time.time + 0.1f;
            bool talk = _talkOverride ?? (_npc != null && _npc.HasShownBubble());
            IsTalking = talk;
        }

        private void UpdateWeights(float dt, float dist)
        {
            // ---- gaits: phase-synchronised blend by speed, playback matched to ground speed ----
            float v = _speed;
            int a = 0, b = 0; float w = 0f;
            if (_gaits.Count > 0)
            {
                if (v <= _gaits[0].Clip.NativeSpeed) { a = b = 0; w = 0f; }
                else if (v >= _gaits[_gaits.Count - 1].Clip.NativeSpeed) { a = b = _gaits.Count - 1; }
                else
                    for (int i = 0; i < _gaits.Count - 1; i++)
                        if (v <= _gaits[i + 1].Clip.NativeSpeed)
                        {
                            a = i; b = i + 1;
                            w = Mathf.InverseLerp(_gaits[i].Clip.NativeSpeed, _gaits[i + 1].Clip.NativeSpeed, v);
                            break;
                        }
                float cycA = _gaits[a].Clip.NativeSpeed * _gaits[a].Clip.Length, cycB = _gaits[b].Clip.NativeSpeed * _gaits[b].Clip.Length;
                float cycle = Mathf.Lerp(cycA, cycB, w);
                // The ground covered drives the phase: one cycle per stride length, so planted feet stay planted.
                float adv = cycle > 0.01f ? (_speedHint >= 0f ? _speedHint * dt : dist) / cycle : 0f;
                _gaitPhase = Mathf.Repeat(_gaitPhase + adv, 1f);
            }
            bool starting = _phase == Phase.Lead || _phase == Phase.StartClip;
            bool stopping = _phase == Phase.Stop && _stopClip != null;
            float moveGoal = Mathf.Clamp01(Mathf.InverseLerp(0.05f, 0.5f, v));
            if (_walkingNow && _phase == Phase.Cruise) moveGoal = 1f;
            _moveW = Mathf.MoveTowards(_moveW, moveGoal, dt * 6f);

            // ---- transitions: start, stop, turn ----
            float transGoal = 0f;
            if (_turnClip != null) { transGoal = 1f; _trans.Time = Mathf.Clamp01(_turnT / _turnDur) * _turnClip.Length; }
            else if (starting && _startClip != null)
            {
                if (_trans.Clip != _startClip) { SetClip(_trans, _startClip); _trans.PrevTime = -1e-4f; }
                transGoal = 1f; _trans.Time = Mathf.Min(_phaseT, _startClip.Length);
                // Hand over to the gait at the clip's exit phase.
                _gaitPhase = _startClip.ExitPhase >= 0f ? _startClip.ExitPhase : _gaitPhase;
            }
            else if (_phase == Phase.Cruise && _trans.Clip == _startClip && _startClip != null && _transW > 0f)
            {
                transGoal = 0f; _trans.Time = _startClip.Length;
            }
            else if (stopping)
            {
                if (_trans.Clip != _stopClip) { SetClip(_trans, _stopClip); _trans.PrevTime = -1e-4f; }
                transGoal = 1f;
                _phaseT = _walkingNow ? _phaseT : _phaseT + dt; // after the walker has arrived, the settle plays on in real time
                _trans.Time = Mathf.Min(_phaseT, _stopClip.Length);
                if (!_walkingNow && _phaseT >= _stopClip.Length) { _phase = Phase.Stand; }
            }
            float fadeIn = 12f, fadeOut = (_turnClip == null && _trans.Clip != null && _trans.Clip.Name.StartsWith("Stop")) ? 3f : 7f;
            _transW = Mathf.MoveTowards(_transW, transGoal, dt * (transGoal > _transW ? fadeIn : fadeOut));
            if (_transW <= 0f && _trans.Clip != null && !starting && !stopping && _turnClip == null) SetClip(_trans, null);

            // ---- gestures ----
            float gestGoal = 0f;
            if (_gesture != null)
            {
                _gestureT += dt;
                var g = _gesture;
                bool holding = _pointAt.HasValue && g.Name == "PointGround" && g.Hold.x >= 0f;
                if (holding && _gestureT > g.Hold.y) _gestureT = g.Hold.x + Mathf.Repeat(_gestureT - g.Hold.x, Mathf.Max(0.05f, g.Hold.y - g.Hold.x));
                if (!_pointAt.HasValue && g.Name == "PointGround" && _oneShotKey == "point" && g.Hold.y > 0f && _gestureT < g.Hold.y) _gestureT = Mathf.Max(_gestureT, g.Hold.y);
                float left = g.Length - _gestureT;
                gestGoal = left < 0.25f && _queue.Count == 0 ? Mathf.Clamp01(left / 0.25f) : 1f;
                if (_gestureAdditive) { _add.Time = Mathf.Min(_gestureT, g.Length); }
                else { _gestFull.Time = _gestUpper.Time = Mathf.Min(_gestureT, g.Length); }
                if (_gestureT >= g.Length)
                {
                    if (_queue.Count > 0) { var next = _rc.Clip(_queue[0]); _queue.RemoveAt(0); if (next != null) StartGesture(next); else EndGesture(); }
                    else EndGesture();
                }
            }
            float rateIn = _queue.Count > 0 || (_gesture != null && _gesture.Name.StartsWith("Cast")) ? 20f : 6f;
            _gestureW = Mathf.MoveTowards(_gestureW, gestGoal, dt * (gestGoal > _gestureW ? rateIn : 4f));
            float gw = _gestureAdditive ? 0f : _gestureW;
            _addW = Mathf.MoveTowards(_addW, _gestureAdditive && _gesture != null ? 1f : 0f, dt * 6f);

            // ---- idle variety ----
            if (_idle.Count > 0)
            {
                if (Time.time >= _idleSwitchAt && _moveW < 0.1f && _gesture == null)
                {
                    int next = UnityEngine.Random.value < 0.55f ? 0 : 1 + UnityEngine.Random.Range(0, Mathf.Max(1, _idle.Count - 1));
                    if (next != _idleIndex) { _idleIndex = Mathf.Min(next, _idle.Count - 1); _idle[_idleIndex].Time = 0f; }
                    _idleSwitchAt = Time.time + UnityEngine.Random.Range(6f, 12f);
                }
                for (int i = 0; i < _idle.Count; i++)
                {
                    _idleW[i] = Mathf.MoveTowards(_idleW[i], i == _idleIndex ? 1f : 0f, dt * 1.5f);
                    _idle[i].Time += dt;
                    if (_idle[i].Time > _idle[i].Clip.Length) _idle[i].Time -= _idle[i].Clip.Length;
                }
            }

            // ---- talk (upper body loop while a bubble of ours shows, unless a gesture plays) ----
            _talkW = Mathf.MoveTowards(_talkW, IsTalking && _gesture == null ? 0.75f : 0f, dt * 3f);
            if (_talk.Clip != null) { _talk.Time += dt; if (_talk.Time > _talk.Clip.Length) _talk.Time -= _talk.Clip.Length; }

            // ---- weights into the mixers ----
            float locoShare = 1f - _transW;
            float fullGest = gw * (1f - _moveW);
            float baseShare = locoShare * (1f - fullGest);
            for (int i = 0; i < _idle.Count; i++) _base.SetInputWeight(_idle[i].Index, baseShare * (1f - _moveW) * _idleW[i] / Mathf.Max(0.001f, IdleSum()));
            for (int i = 0; i < _gaits.Count; i++)
            {
                float gwt = i == a ? (a == b ? 1f : 1f - w) : i == b ? w : 0f;
                _base.SetInputWeight(_gaits[i].Index, baseShare * _moveW * gwt);
                _gaits[i].Time = _gaitPhase * _gaits[i].Clip.Length;
            }
            if (_trans.Clip != null) _base.SetInputWeight(_trans.Index, _transW * (1f - fullGest));
            if (_gestFull.Clip != null) _base.SetInputWeight(_gestFull.Index, fullGest);
            _layers.SetInputWeight(1, _gestUpper.Clip != null ? gw * _moveW : 0f);
            _layers.SetInputWeight(2, _talk.Clip != null ? _talkW * (1f - gw) : 0f);
            _layers.SetInputWeight(3, _add.Clip != null ? _addW : 0f);
            if (_gestUpper.Clip != null) _gestUpper.Mixer.SetInputWeight(0, 1f);
            if (_talk.Clip != null) _talk.Mixer.SetInputWeight(0, 1f);
            if (_add.Clip != null) _add.Mixer.SetInputWeight(0, 1f);

            // ---- push times, fire manifest events once per crossing ----
            foreach (var s in _idle) Push(s, false);
            foreach (var s in _gaits) Push(s, _base.GetInputWeight(s.Index) > 0.3f);
            Push(_trans, _transW > 0.3f);
            Push(_gestFull, gw > 0.01f && !_gestureAdditive);
            Push(_gestUpper, false);
            Push(_talk, false);
            Push(_add, false);
        }

        private float IdleSum()
        {
            float s = 0f;
            for (int i = 0; i < _idle.Count; i++) s += _idleW[i];
            return s;
        }

        private void Push(Slot s, bool events)
        {
            if (s.Clip == null || !s.Playable.IsValid()) return;
            float t = s.Time;
            if (events && s.Clip.EventNames.Length > 0)
            {
                float prev = s.PrevTime, len = s.Clip.Length;
                for (int i = 0; i < s.Clip.EventNames.Length; i++)
                {
                    float et = s.Clip.EventTimes[i];
                    bool crossed = t >= prev ? (et > prev && et <= t) : (et > prev || et <= t); // wrapped loop
                    if (crossed) BeatEvent?.Invoke(s.Clip.EventNames[i], _beatId);
                }
            }
            s.PrevTime = t;
            s.Playable.SetTime(t);
        }

        private void UpdateFaceAndGaze(float dt)
        {
            // Gaze target: explicit, else the player when we speak, greet or they stand close in front, else the pointed spot.
            Vector3? target = null; float want = 0f;
            var walker = PlayerWalker.Instance;
            Vector3? player = walker != null ? walker.EyePosition : (Vector3?)null;
            if (_explicitGaze)
            {
                target = _gazeTransform != null ? _gazeTransform.position : _gazeWorld; want = 1f;
                if (_gazeTransform == null && !_gazeWorld.HasValue) _explicitGaze = false;
            }
            else if (_pointAt.HasValue && _gesture != null && _gesture.Name == "PointGround" && _gestureT < 0.9f)
            {
                target = _pointAt.Value; want = 0.9f;
            }
            else if (player.HasValue)
            {
                var d = player.Value - transform.position; d.y = 0f;
                float dist = d.magnitude;
                bool front = Vector3.Dot(transform.forward, d.normalized) > -0.2f;
                bool engaged = IsTalking || (_gesture != null && (_gesture.Name == "Greet" || _gesture.Name == "Beckon")) || (_npc != null && _npc.IsGreetingShown);
                if ((engaged && dist < 30f) || (dist < 7f && front && _moveW < 0.5f)) { target = player.Value; want = engaged ? 1f : 0.8f; }
            }
            _face.Target = target;
            _face.WantWeight = want;
            _face.Talking = IsTalking && _faceOn;
            // Expression: explicit, else from what the body is doing.
            if (!_explicitExpression)
            {
                string e = "neutral";
                if (_gesture != null)
                {
                    switch (_gesture.Name)
                    {
                        case "Greet": e = "pleased"; break;
                        case "Surprise": case "WardBreak": e = "surprised"; break;
                        case "PointGround": case "Beckon": e = "determined"; break;
                        case "CastAnticipate": case "CastRelease": case "CastRecover": case "CastWeave_Loop": e = _rc.Key == "mira" ? "concentrating" : "determined"; break;
                        case "RecoilLight": case "RecoilHeavy": case "Defeat": e = "pained"; break;
                        case "Victory": e = "pleased"; break;
                    }
                }
                else if (IsTalking) e = "attentive";
                if (e != _face.ExpressionName) _face.SetExpression(e, e == "attentive" ? 0.7f : 1f);
            }
            _face.Tick(dt, _faceOn);
        }

        // ---------------- measurement helpers for tests ----------------

        /// <summary>World position of a foot's ground-contact socket.</summary>
        public Vector3 FootContact(bool left)
        {
            var s = Socket(left ? "Sock.Foot.L" : "Sock.Foot.R");
            var f = left ? _rc.FootL : _rc.FootR;
            return s != null ? s.position : f != null ? f.position : transform.position;
        }
    }
}
