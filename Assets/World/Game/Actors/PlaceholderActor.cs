using System;
using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>Animation keys understood by the placeholder actor (Bedsit.AnimationKeys plus "cast").</summary>
    public static class ActorKeys
    {
        public const string Idle = AnimationKeys.Idle, Wave = AnimationKeys.Wave, Greet = AnimationKeys.Greet,
            Cheer = AnimationKeys.Cheer, Clap = AnimationKeys.Clap, Surprise = AnimationKeys.Surprise,
            Stretch = AnimationKeys.Stretch, Cast = "cast",
            /// <summary>Package N: a come-here wave (calling the player over) and a builder's hammering.</summary>
            Beckon = "beckon", Hammer = "hammer";
    }

    /// <summary>
    /// Default character body: capsule-ish torso in a coloured tunic, sphere head, arms and legs on pivots.
    /// Procedural idle bob, walk swing driven by how fast the root moves, and short one-shots
    /// (wave, greet, cheer, cast, clap, surprise, stretch). Root = feet, faces +Z.
    /// </summary>
    public sealed class PlaceholderActor : MonoBehaviour, ICharacterActor
    {
        public string ActorId { get; private set; }
        public Transform Root => transform;
        public string CurrentKey { get; private set; } = ActorKeys.Idle;
        public bool IsPlayingOneShot => _oneShot != null;
        public event Action<string> OneShotFinished;
        /// <summary>Turn speed in degrees per second for FaceTowards.</summary>
        public float TurnSpeed = 300f;
        public Color TunicColor { get; private set; }

        private Transform _body, _head, _armL, _armR, _legL, _legR;
        private string _oneShot; private float _oneShotT, _oneShotLen;
        private float _targetYaw; private bool _turning;
        private Vector3 _lastPos; private float _walkBlend, _phase;
        private Renderer[] _renderers;
        private Vector3? _pointAt;
        private float _pointBlend;

        /// <summary>The head pivot (accessories such as the prince's circlet hang here).</summary>
        public Transform HeadPivot => _head;
        /// <summary>The body pivot (moves with the walk bob).</summary>
        public Transform BodyPivot => _body;

        /// <summary>Raises the right arm towards a world point and holds it (null lowers it). Package N.</summary>
        public void PointAt(Vector3? worldPoint) { _pointAt = worldPoint; }

        public static PlaceholderActor Build(string id, Color tunic, Transform parent)
        {
            var go = new GameObject("Actor " + id);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<PlaceholderActor>();
            a.ActorId = id;
            a.TunicColor = tunic;
            a.BuildBody(tunic);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0); col.height = 1.8f; col.radius = 0.35f;
            return a;
        }

        private void BuildBody(Color tunic)
        {
            int h = 17;
            foreach (char c in ActorId ?? "") h = h * 31 + c;
            var rnd = new PropRandom(h);
            var skin = Color.Lerp(new Color(0.95f, 0.78f, 0.62f), new Color(0.55f, 0.38f, 0.26f), rnd.Next());
            var hair = Color.Lerp(new Color(0.12f, 0.08f, 0.05f), new Color(0.75f, 0.55f, 0.25f), rnd.Next() * rnd.Next());
            var trousers = new Color(0.22f, 0.20f, 0.18f);
            var mSkin = MaterialFactory.Shared(skin); var mTunic = MaterialFactory.Shared(tunic);
            var mHair = MaterialFactory.Shared(hair); var mTrousers = MaterialFactory.Shared(trousers);
            var mDark = MaterialFactory.Shared(new Color(0.05f, 0.05f, 0.05f));
            var mBelt = MaterialFactory.Shared(new Color(0.30f, 0.20f, 0.12f));

            _body = new GameObject("Body").transform; _body.SetParent(transform, false);
            _legL = Pivot(_body, "LegL", new Vector3(-0.12f, 0.85f, 0));
            _legR = Pivot(_body, "LegR", new Vector3(0.12f, 0.85f, 0));
            Signs.Prim(PrimitiveType.Capsule, _legL, "Leg", new Vector3(0, -0.42f, 0), new Vector3(0.17f, 0.43f, 0.17f), mTrousers);
            Signs.Prim(PrimitiveType.Capsule, _legR, "Leg", new Vector3(0, -0.42f, 0), new Vector3(0.17f, 0.43f, 0.17f), mTrousers);
            Signs.Prim(PrimitiveType.Capsule, _body, "Tunic", new Vector3(0, 1.12f, 0), new Vector3(0.48f, 0.42f, 0.34f), mTunic);
            Signs.Prim(PrimitiveType.Cylinder, _body, "Skirt", new Vector3(0, 0.86f, 0), new Vector3(0.52f, 0.12f, 0.40f), mTunic);
            Signs.Prim(PrimitiveType.Cylinder, _body, "Belt", new Vector3(0, 0.98f, 0), new Vector3(0.50f, 0.03f, 0.37f), mBelt);
            _head = new GameObject("HeadPivot").transform; _head.SetParent(_body, false); _head.localPosition = new Vector3(0, 1.5f, 0);
            Signs.Prim(PrimitiveType.Sphere, _head, "Head", new Vector3(0, 0.12f, 0), new Vector3(0.27f, 0.3f, 0.27f), mSkin);
            Signs.Prim(PrimitiveType.Sphere, _head, "Hair", new Vector3(0, 0.2f, -0.03f), new Vector3(0.29f, 0.2f, 0.29f), mHair);
            Signs.Prim(PrimitiveType.Cube, _head, "EyeL", new Vector3(-0.055f, 0.13f, 0.125f), new Vector3(0.04f, 0.045f, 0.03f), mDark);
            Signs.Prim(PrimitiveType.Cube, _head, "EyeR", new Vector3(0.055f, 0.13f, 0.125f), new Vector3(0.04f, 0.045f, 0.03f), mDark);
            Signs.Prim(PrimitiveType.Cube, _head, "Nose", new Vector3(0, 0.08f, 0.14f), new Vector3(0.035f, 0.06f, 0.05f), mSkin);
            _armL = Pivot(_body, "ArmL", new Vector3(-0.29f, 1.38f, 0));
            _armR = Pivot(_body, "ArmR", new Vector3(0.29f, 1.38f, 0));
            foreach (var arm in new[] { _armL, _armR })
            {
                Signs.Prim(PrimitiveType.Capsule, arm, "Sleeve", new Vector3(0, -0.26f, 0), new Vector3(0.13f, 0.28f, 0.13f), mTunic);
                Signs.Prim(PrimitiveType.Sphere, arm, "Hand", new Vector3(0, -0.58f, 0), Vector3.one * 0.1f, mSkin);
            }
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private void Start() { _lastPos = transform.position; _targetYaw = transform.eulerAngles.y; }

        public void PlayIdle()
        {
            _oneShot = null;
            CurrentKey = ActorKeys.Idle;
        }

        public bool PlayOneShot(string key)
        {
            float len;
            switch (key)
            {
                case ActorKeys.Wave: len = 1.6f; break;
                case ActorKeys.Greet: len = 1.3f; break;
                case ActorKeys.Cheer: len = 1.5f; break;
                case ActorKeys.Cast: len = 1.0f; break;
                case ActorKeys.Clap: len = 1.4f; break;
                case ActorKeys.Surprise: len = 1.0f; break;
                case ActorKeys.Stretch: len = 2.0f; break;
                case ActorKeys.Beckon: len = 1.4f; break;
                case ActorKeys.Hammer: len = 0.9f; break;
                default: return false;
            }
            _oneShot = key; _oneShotT = 0; _oneShotLen = len;
            CurrentKey = key;
            return true;
        }

        public void FaceTowards(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position; d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return;
            _targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _turning = true;
        }

        /// <summary>Sets facing at once (no turn).</summary>
        public void SetYaw(float yaw)
        {
            _targetYaw = yaw; _turning = false;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        public void SetVisible(bool visible)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_turning)
            {
                float y = Mathf.MoveTowardsAngle(transform.eulerAngles.y, _targetYaw, TurnSpeed * dt);
                transform.rotation = Quaternion.Euler(0, y, 0);
                if (Mathf.Abs(Mathf.DeltaAngle(y, _targetYaw)) < 0.5f) _turning = false;
            }

            var p = transform.position;
            var v = p - _lastPos; v.y = 0;
            _lastPos = p;
            float speed = dt > 0 ? v.magnitude / dt : 0;
            _walkBlend = Mathf.MoveTowards(_walkBlend, Mathf.Clamp01(speed / 2f), dt * 4f);
            _phase += dt * (2f + speed * 1.6f);

            float t = Time.time;
            float swing = Mathf.Sin(_phase) * 32f * _walkBlend;
            float bob = Mathf.Sin(t * 2.1f) * 0.012f * (1 - _walkBlend) + Mathf.Abs(Mathf.Sin(_phase)) * 0.04f * _walkBlend;
            Vector3 armL = new Vector3(swing * 0.8f, 0, -6f), armR = new Vector3(-swing * 0.8f, 0, 6f);
            float bodyPitch = 0, headPitch = Mathf.Sin(t * 0.7f) * 2f, lift = 0;

            if (_oneShot != null)
            {
                _oneShotT += dt;
                float u = Mathf.Clamp01(_oneShotT / _oneShotLen);
                float env = Mathf.Sin(u * Mathf.PI); // ease in and out
                switch (_oneShot)
                {
                    case ActorKeys.Wave:
                        armR = Vector3.Lerp(armR, new Vector3(0, 0, 150f + Mathf.Sin(_oneShotT * 14f) * 18f), Mathf.Clamp01(env * 2f));
                        break;
                    case ActorKeys.Greet:
                        bodyPitch = 18f * env; headPitch += 10f * env;
                        armR = Vector3.Lerp(armR, new Vector3(-60f, 0, 10f), env);
                        break;
                    case ActorKeys.Cheer:
                        armL = Vector3.Lerp(armL, new Vector3(0, 0, -160f), Mathf.Clamp01(env * 2f));
                        armR = Vector3.Lerp(armR, new Vector3(0, 0, 160f), Mathf.Clamp01(env * 2f));
                        lift = Mathf.Abs(Mathf.Sin(_oneShotT * 9f)) * 0.18f * env;
                        break;
                    case ActorKeys.Cast:
                    {
                        float push = u < 0.35f ? u / 0.35f : 1f - (u - 0.35f) / 0.65f;
                        armL = Vector3.Lerp(armL, new Vector3(-95f, 0, 15f), push);
                        armR = Vector3.Lerp(armR, new Vector3(-95f, 0, -15f), push);
                        bodyPitch = -6f * push;
                        break;
                    }
                    case ActorKeys.Clap:
                    {
                        float c = Mathf.Abs(Mathf.Sin(_oneShotT * 12f));
                        armL = Vector3.Lerp(armL, new Vector3(-70f, 0, 30f * c + 5f), env);
                        armR = Vector3.Lerp(armR, new Vector3(-70f, 0, -30f * c - 5f), env);
                        break;
                    }
                    case ActorKeys.Surprise:
                        armL = Vector3.Lerp(armL, new Vector3(-40f, 0, -60f), env);
                        armR = Vector3.Lerp(armR, new Vector3(-40f, 0, 60f), env);
                        bodyPitch = -8f * env; lift = 0.06f * env;
                        break;
                    case ActorKeys.Stretch:
                        armL = Vector3.Lerp(armL, new Vector3(0, 0, -170f), env);
                        armR = Vector3.Lerp(armR, new Vector3(0, 0, 170f), env);
                        bodyPitch = -5f * env;
                        break;
                    case ActorKeys.Beckon:
                        // Arm out in front, forearm waving back towards the body.
                        armR = Vector3.Lerp(armR, new Vector3(-80f + Mathf.Sin(_oneShotT * 11f) * 35f, 0, 12f), Mathf.Clamp01(env * 2f));
                        headPitch -= 4f * env;
                        break;
                    case ActorKeys.Hammer:
                    {
                        float k = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f));
                        armR = Vector3.Lerp(armR, new Vector3(-150f + 90f * k, 0, 8f), env);
                        bodyPitch = 14f * env;
                        break;
                    }
                }
                if (_oneShotT >= _oneShotLen)
                {
                    var k = _oneShot;
                    _oneShot = null;
                    CurrentKey = ActorKeys.Idle;
                    OneShotFinished?.Invoke(k);
                }
            }

            // Pointing (held): the right arm swings up towards the point, relative to the body's facing.
            _pointBlend = Mathf.MoveTowards(_pointBlend, _pointAt.HasValue && _oneShot == null ? 1f : 0f, dt * 4f);
            if (_pointBlend > 0f && _pointAt.HasValue)
            {
                var local = transform.InverseTransformPoint(_pointAt.Value);
                float side = Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg;
                float down = Mathf.Atan2(local.y - 1.4f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                var pose = new Vector3(-90f - Mathf.Clamp(down, -40f, 30f), 0, Mathf.Clamp(side, -60f, 60f) + 4f);
                armR = Vector3.Lerp(armR, pose, _pointBlend);
            }
            _body.localPosition = new Vector3(0, bob + lift, 0);
            _body.localRotation = Quaternion.Euler(bodyPitch, 0, 0);
            _head.localRotation = Quaternion.Euler(headPitch, 0, 0);
            _armL.localRotation = Quaternion.Euler(armL);
            _armR.localRotation = Quaternion.Euler(armR);
            _legL.localRotation = Quaternion.Euler(-swing, 0, 0);
            _legR.localRotation = Quaternion.Euler(swing, 0, 0);
        }
    }

    /// <summary>
    /// The single creation point for characters. Returns placeholder bodies today; an optional Sims 2
    /// layer can be swapped in here later without touching callers.
    /// </summary>
    public static class ActorFactory
    {
        public static ICharacterActor Create(string id, Color colour, Transform parent = null)
        {
            if (parent == null && WorldBootstrap.Instance != null) parent = WorldBootstrap.Instance.ActorsRoot;
            return PlaceholderActor.Build(id, colour, parent);
        }

        public static ICharacterActor Create(string id, Color colour, Vector3 position, float yaw, Transform parent = null)
        {
            var a = Create(id, colour, parent);
            a.Root.position = position;
            if (a is PlaceholderActor p) p.SetYaw(yaw); else a.Root.rotation = Quaternion.Euler(0, yaw, 0);
            return a;
        }
    }
}
