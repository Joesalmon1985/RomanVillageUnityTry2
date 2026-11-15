using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Light secondary motion for the cloth chains of a <see cref="RiggedActor"/> (PRD_CHARACTERS CH-13): the cape
    /// (Cape.1-3) trails with speed and swings with turns and steps, the hood (Hood.1-2) lags the head a little, the
    /// skirt panels (Skirt.F/B/L/R) follow the thighs so long robes do not let the legs through. A damped spring per
    /// chain, a few multiplies per bone, run after the animation. The Humanoid clips carry no curves for these bones
    /// (they are not in the human map), so this is what moves them; it starts from their rest pose every frame.
    /// </summary>
    public sealed class ActorCloth
    {
        private readonly RiggedCharacter _rc;
        private readonly Transform _root;
        private readonly Quaternion[] _capeRest, _hoodRest, _skirtRest;
        private Vector3 _lastPos, _vel;
        private float _lastYaw, _yawRate;
        // Cape spring: pitch (back) and roll (side).
        private float _cp, _cpV, _cr, _crV;
        private float _hp, _hpV, _hr, _hrV;
        private Quaternion _lastHead;
        private readonly float[] _sk = new float[4], _skV = new float[4];
        private float _time;

        public ActorCloth(RiggedCharacter rc, Transform root)
        {
            _rc = rc; _root = root;
            _capeRest = Rest(rc.Cape); _hoodRest = Rest(rc.Hood); _skirtRest = Rest(rc.Skirt);
            _lastPos = root.position; _lastYaw = root.eulerAngles.y;
            if (rc.Head != null) _lastHead = rc.Head.rotation;
        }

        private static Quaternion[] Rest(Transform[] chain)
        {
            var r = new Quaternion[chain.Length];
            for (int i = 0; i < chain.Length; i++) r[i] = chain[i] != null ? chain[i].localRotation : Quaternion.identity;
            return r;
        }

        private static void Spring(ref float x, ref float v, float goal, float stiffness, float damping, float dt)
        {
            // Semi-implicit Euler, sub-stepped so a long frame cannot explode it.
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 8);
            float h = dt / n;
            for (int i = 0; i < n; i++) { v += (stiffness * (goal - x) - damping * v) * h; x += v * h; }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _time += dt;
            var p = _root.position;
            var v = (p - _lastPos) / dt; _lastPos = p;
            var accel = (v - _vel) / dt;
            _vel = Vector3.Lerp(_vel, v, 1f - Mathf.Exp(-dt / 0.06f));
            float yaw = _root.eulerAngles.y;
            _yawRate = Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, yaw) / dt, 1f - Mathf.Exp(-dt / 0.08f));
            _lastYaw = yaw;
            var localV = _root.InverseTransformDirection(_vel);
            var localA = _root.InverseTransformDirection(accel);
            var up = _root.up; var right = _root.right; var fwd = _root.forward;

            if (_rc.Cape.Length > 0)
            {
                // Trails back with forward speed (about 4 degrees per m/s, at most 35), kicks back when starting off,
                // swings out of a turn; a slow breeze sway at rest.
                float goalP = Mathf.Clamp(localV.z * 4.2f, -6f, 35f) + Mathf.Clamp(localA.z * 0.6f, -8f, 10f) + Mathf.Sin(_time * 0.9f) * 1.2f;
                float goalR = Mathf.Clamp(-_yawRate * 0.05f - localA.x * 0.5f, -14f, 14f);
                Spring(ref _cp, ref _cpV, goalP, 60f, 9f, dt);
                Spring(ref _cr, ref _crV, goalR, 50f, 8f, dt);
                ApplyChain(_rc.Cape, _capeRest, _cp, _cr, right, fwd, new[] { 0.55f, 0.3f, 0.25f });
            }
            if (_rc.Hood.Length > 0 && _rc.Head != null)
            {
                // The hood lags the head's own turn a little and settles back.
                var dq = _rc.Head.rotation * Quaternion.Inverse(_lastHead);
                _lastHead = _rc.Head.rotation;
                dq.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                var w = axis * (ang / dt);
                float goalP = Mathf.Clamp(Vector3.Dot(w, right) * 0.03f + localV.z * 1.2f, -8f, 12f);
                float goalR = Mathf.Clamp(-Vector3.Dot(w, up) * 0.03f, -8f, 8f);
                Spring(ref _hp, ref _hpV, goalP, 80f, 11f, dt);
                Spring(ref _hr, ref _hrV, goalR, 80f, 11f, dt);
                ApplyChain(_rc.Hood, _hoodRest, _hp, _hr, right, fwd, new[] { 0.5f, 0.5f });
            }
            if (_rc.Skirt.Length > 0) Skirt(dt, right, fwd, localV);
        }

        private void ApplyChain(Transform[] chain, Quaternion[] rest, float pitch, float roll, Vector3 right, Vector3 fwd, float[] share)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == null) continue;
                chain[i].localRotation = rest[i];
                float s = i < share.Length ? share[i] : share[share.Length - 1];
                chain[i].rotation = Quaternion.AngleAxis(pitch * s, right) * Quaternion.AngleAxis(roll * s, fwd) * chain[i].rotation;
            }
        }

        private float ThighSwing(Transform upper, out float side)
        {
            side = 0f;
            if (upper == null || upper.childCount == 0) return 0f;
            Transform knee = null;
            foreach (Transform c in upper) if (c.name.StartsWith("LowerLeg")) { knee = c; break; }
            if (knee == null) return 0f;
            var d = _root.InverseTransformDirection(knee.position - upper.position).normalized;
            side = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
            return Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg; // + = thigh forward
        }

        private Transform _thighL, _thighR; private bool _thighs;

        private void Skirt(float dt, Vector3 right, Vector3 fwd, Vector3 localV)
        {
            if (!_thighs)
            {
                _thighs = true;
                var anim = _rc.Animator;
                if (anim != null && anim.isHuman)
                {
                    _thighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    _thighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                }
            }
            float sideL, sideR;
            float l = ThighSwing(_thighL, out sideL), r = ThighSwing(_thighR, out sideR);
            // Contract v2: the skirt follows the forward thigh by 68 percent, sideways by 26 percent.
            float front = Mathf.Max(0f, Mathf.Max(l, r)) * 0.68f;
            float back = Mathf.Max(0f, Mathf.Max(-l, -r)) * 0.68f + Mathf.Clamp(localV.z * 2.5f, 0f, 14f);
            float outL = Mathf.Max(0f, -sideL) * 0.26f, outR = Mathf.Max(0f, sideR) * 0.26f;
            for (int i = 0; i < _rc.Skirt.Length; i++)
            {
                var b = _rc.Skirt[i];
                if (b == null) continue;
                string n = b.name;
                float goal = n.EndsWith(".F") ? -front : n.EndsWith(".B") ? back : n.EndsWith(".L") ? outL : outR;
                Spring(ref _sk[i % 4], ref _skV[i % 4], goal, 160f, 18f, dt);
                b.localRotation = _skirtRest[i];
                // F and B swing round the right axis (+ = hem back); L and R round the forward axis, outward.
                if (n.EndsWith(".F") || n.EndsWith(".B")) b.rotation = Quaternion.AngleAxis(_sk[i % 4], right) * b.rotation;
                else b.rotation = Quaternion.AngleAxis(n.EndsWith(".L") ? -_sk[i % 4] : _sk[i % 4], fwd) * b.rotation;
            }
        }
    }
}
