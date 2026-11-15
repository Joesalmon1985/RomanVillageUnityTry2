using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Face and gaze of a <see cref="RiggedActor"/> (PRD_CHARACTERS 5.4, contract v2 2.4), run in LateUpdate after the
    /// animation: blinking (random interval, sometimes double), named expressions through the head's blend shapes with
    /// the character's channel gains, the talk ticker (mouth key and jaw), and gaze: the eyes take what they can
    /// (+-EyeYaw, +-EyePitch) at once, the head follows with a lag and the chest with a longer one, by the manifest's
    /// shares, all clamped. Rotations are applied in world space round the character's own up and right axes, so the
    /// bone roll of the import does not matter.
    /// </summary>
    public sealed class ActorFace
    {
        private readonly RiggedCharacter _rc;
        private readonly Transform _root;
        private readonly float[] _w, _target;
        private readonly Vector3 _eyeFwdL, _eyeFwdR, _bindHeadFwd;
        private readonly int _blinkL, _blinkR, _talk;
        private RigExpression _expr;
        private float _exprWeight;
        private float _nextBlink, _blinkT = -1f; private bool _double;
        private float _talkPhase, _talkNoise;
        // Gaze state (degrees relative to the root's facing): what the chest, head and eyes have reached.
        private float _chestYaw, _chestPitch, _headYaw, _headPitch, _eyeYaw, _eyePitch, _gazeWeight;
        public float GazeWeight => _gazeWeight;
        private float _hyV, _hpV, _cyV, _cpV;

        public Vector3? Target;
        /// <summary>0..1: how strongly the gaze overrides the animated head (1 = looks exactly at the target).</summary>
        public float WantWeight;
        public bool Talking;
        public float JawExtra;
        public string ExpressionName => _expr != null ? _expr.Name : "neutral";

        public ActorFace(RiggedCharacter rc, Transform root)
        {
            _rc = rc; _root = root;
            int n = rc.ShapeKeys.Length;
            _w = new float[n]; _target = new float[n];
            _blinkL = rc.Shape("Blink.L"); _blinkR = rc.Shape("Blink.R"); _talk = rc.Shape(rc.Tuning.TalkKey);
            // Eye forward in each eye bone's local space, measured at bind pose (character forward).
            var fwd = root.TransformDirection(rc.ModelForward);
            _eyeFwdL = rc.EyeL != null ? Quaternion.Inverse(rc.EyeL.rotation) * fwd : Vector3.forward;
            _eyeFwdR = rc.EyeR != null ? Quaternion.Inverse(rc.EyeR.rotation) * fwd : Vector3.forward;
            _bindHeadFwd = rc.Head != null ? Quaternion.Inverse(rc.Head.rotation) * fwd : Vector3.forward;
            _nextBlink = Time.time + Random.Range(0.5f, rc.Tuning.BlinkMax);
        }

        public bool SetExpression(string name, float weight)
        {
            if (string.IsNullOrEmpty(name) || name == "neutral") { _expr = null; _exprWeight = 0f; return true; }
            foreach (var e in _rc.Expressions)
                if (e.Name == name) { _expr = e; _exprWeight = Mathf.Clamp01(weight); return true; }
            return false;
        }

        /// <summary>World direction the left eye looks along now.</summary>
        public Vector3 EyeForward => _rc.EyeL != null ? _rc.EyeL.rotation * _eyeFwdL : _root.forward;
        public Vector3 EyeCentre => _rc.EyeL != null && _rc.EyeR != null ? (_rc.EyeL.position + _rc.EyeR.position) * 0.5f : _root.position + Vector3.up * 1.65f;

        private float Gain(int i)
        {
            var t = _rc.Tuning;
            switch (_rc.ShapeChannels[i]) { case "blink": return t.GainBlink; case "brow": return t.GainBrow; default: return t.GainMouth; }
        }

        public void Tick(float dt, bool faceOn)
        {
            var t = _rc.Tuning;
            var body = _rc.Body;
            if (faceOn && body != null && _w.Length > 0)
            {
                for (int i = 0; i < _target.Length; i++) _target[i] = 0f;
                if (_expr != null)
                    for (int k = 0; k < _expr.Keys.Length; k++)
                    {
                        int i = _rc.Shape(_expr.Keys[k]);
                        if (i >= 0) _target[i] = _expr.Weights[k] * _exprWeight * Gain(i);
                    }
                // Blink.
                float rate = _expr != null ? Mathf.Max(0.1f, _expr.BlinkRate) : 1f;
                if (_blinkT < 0f && Time.time >= _nextBlink) { _blinkT = 0f; _double = Random.value < t.DoubleBlink; }
                float blink = 0f;
                if (_blinkT >= 0f)
                {
                    _blinkT += dt;
                    float a = t.BlinkClose, h = t.BlinkHold, o = t.BlinkOpen;
                    blink = _blinkT < a ? _blinkT / a : _blinkT < a + h ? 1f : 1f - (_blinkT - a - h) / o;
                    if (_blinkT >= a + h + o)
                    {
                        if (_double) { _double = false; _blinkT = 0f; }
                        else { _blinkT = -1f; _nextBlink = Time.time + Random.Range(t.BlinkMin, t.BlinkMax) / rate; }
                        blink = 0f;
                    }
                    blink = Mathf.Clamp01(blink) * t.GainBlink;
                }
                // Talk ticker: a noisy open-close at about TalkRate on the mouth key.
                float talk = 0f;
                if (Talking)
                {
                    _talkPhase += dt * t.TalkRate * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 1.3f, _talkNoise));
                    float s = 0.5f + 0.5f * Mathf.Sin(_talkPhase * Mathf.PI * 2f);
                    float env = Mathf.PerlinNoise(Time.time * 2.1f, 7.3f + _talkNoise);
                    talk = Mathf.Lerp(t.TalkWeightMin, t.TalkWeightMax, s * Mathf.Lerp(0.4f, 1f, env));
                }
                else _talkNoise = Random.value * 10f;
                for (int i = 0; i < _w.Length; i++)
                {
                    float goal = _target[i];
                    if (i == _blinkL || i == _blinkR) goal = Mathf.Max(goal, blink);
                    if (i == _talk && Talking) goal = Mathf.Max(goal, talk * t.GainMouth);
                    bool fast = i == _blinkL || i == _blinkR || i == _talk;
                    _w[i] = fast ? goal : Mathf.MoveTowards(_w[i], goal, dt * 5f);
                    body.SetBlendShapeWeight(i, _w[i] * 100f);
                }
                // Jaw: expression jaw plus the talk ticker (degrees, chin down).
                if (_rc.Jaw != null)
                {
                    float jaw = (_expr != null ? _expr.JawDeg * _exprWeight : 0f) + JawExtra;
                    if (Talking) jaw += Mathf.Lerp(t.TalkJawMin, t.TalkJawMax, Mathf.InverseLerp(t.TalkWeightMin, t.TalkWeightMax, talk));
                    jaw *= t.GainJaw;
                    if (Mathf.Abs(jaw) > 0.01f) _rc.Jaw.rotation = Quaternion.AngleAxis(jaw, _root.right) * _rc.Jaw.rotation;
                }
            }
            Gaze(dt);
        }

        private static float Smooth(float cur, float goal, ref float vel, float time, float dt)
        {
            return Mathf.SmoothDamp(cur, goal, ref vel, Mathf.Max(0.01f, time), Mathf.Infinity, dt);
        }

        private void YawPitch(Vector3 worldDir, out float yaw, out float pitch)
        {
            var local = _root.InverseTransformDirection(worldDir);
            yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; // + = to the character's right
            pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg; // + = down
        }

        /// <summary>Yaw and pitch (degrees, root frame) of the target as seen from the eyes; false when there is none.</summary>
        public bool TargetAngles(out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            if (!Target.HasValue) return false;
            YawPitch(Target.Value - EyeCentre, out yaw, out pitch);
            return true;
        }

        private void Gaze(float dt)
        {
            var t = _rc.Tuning;
            if (_rc.Head == null) return;
            float yaw = 0f, pitch = 0f;
            bool has = TargetAngles(out yaw, out pitch);
            float maxYaw = t.HeadYaw / (1f - t.ChestShare) + t.EyeYaw;
            // Behind the shoulder: do not crank round (the body turn is the caller's job); look ahead.
            if (has && Mathf.Abs(yaw) > maxYaw + 30f) has = false;
            float want = has ? WantWeight : 0f;
            _gazeWeight = Mathf.MoveTowards(_gazeWeight, want, dt * 6f);
            if (!has) { yaw = 0f; pitch = 0f; }
            yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch, -(t.HeadPitch + t.EyePitch), t.HeadPitch + t.EyePitch);
            // Shares: the chest a fixed share; the head at least its share and enough that the eyes can cover the rest.
            float chestYawGoal = t.ChestShare * yaw, chestPitchGoal = 0.1f * pitch;
            float restY = yaw - chestYawGoal, restP = pitch - chestPitchGoal;
            float headYawGoal = Mathf.Clamp(Mathf.Sign(restY) * Mathf.Max(Mathf.Abs(yaw) * t.HeadShare, Mathf.Abs(restY) - t.EyeYaw * 0.8f), -t.HeadYaw, t.HeadYaw);
            float headPitchGoal = Mathf.Clamp(Mathf.Sign(restP) * Mathf.Max(Mathf.Abs(pitch) * 0.6f, Mathf.Abs(restP) - t.EyePitch * 0.8f), -t.HeadPitch, t.HeadPitch);
            _chestYaw = Smooth(_chestYaw, chestYawGoal, ref _cyV, t.HeadLag * 2.2f, dt);
            _chestPitch = Smooth(_chestPitch, chestPitchGoal, ref _cpV, t.HeadLag * 2.2f, dt);
            _headYaw = Smooth(_headYaw, headYawGoal + _chestYaw, ref _hyV, t.HeadLag, dt);
            _headPitch = Smooth(_headPitch, headPitchGoal + _chestPitch, ref _hpV, t.HeadLag, dt);
            float gw = _gazeWeight;
            if (gw <= 0.001f) { _eyeYaw = _eyePitch = 0f; return; }
            var up = _root.up; var right = _root.right;
            if (_rc.Chest != null)
                _rc.Chest.rotation = Quaternion.AngleAxis(_chestYaw * gw, up) * Quaternion.AngleAxis(_chestPitch * gw, right) * _rc.Chest.rotation;
            // The head is turned to its goal from wherever the animation (and the chest) left it, keeping a little of the animation.
            YawPitch(_rc.Head.rotation * HeadFwdLocal, out float ay, out float ap);
            const float keep = 0.15f; // share of the animated head motion left on top of the gaze
            float dy = Mathf.DeltaAngle(ay, _headYaw) * (1f - keep);
            float dp = Mathf.DeltaAngle(ap, _headPitch) * (1f - keep);
            _rc.Head.rotation = Quaternion.AngleAxis(dy * gw, up) * Quaternion.AngleAxis(dp * gw, right) * _rc.Head.rotation;
            // Eyes: the rest of the way from where the head now points, as a quick saccade, clamped to the sclera.
            YawPitch(_rc.Head.rotation * HeadFwdLocal, out float hy, out float hp);
            float eyeYawGoal = has ? Mathf.Clamp(Mathf.DeltaAngle(hy, yaw), -t.EyeYaw, t.EyeYaw) : 0f;
            float eyePitchGoal = has ? Mathf.Clamp(pitch - hp, -t.EyePitch, t.EyePitch) : 0f;
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, t.Saccade));
            _eyeYaw = Mathf.Lerp(_eyeYaw, eyeYawGoal, k);
            _eyePitch = Mathf.Lerp(_eyePitch, eyePitchGoal, k);
            AimEye(_rc.EyeL, _eyeFwdL, hy, hp, gw);
            AimEye(_rc.EyeR, _eyeFwdR, hy, hp, gw);
        }

        private Vector3 _headFwd; private bool _headFwdSet;
        private Vector3 HeadFwdLocal
        {
            get
            {
                if (!_headFwdSet) { _headFwd = _bindHeadFwd; _headFwdSet = true; }
                return _headFwd;
            }
        }

        private void AimEye(Transform eye, Vector3 localFwd, float headYaw, float headPitch, float gw)
        {
            if (eye == null) return;
            // Put the eye where the head looks plus its own offset (it rides on the head, so its animated aim is the head's).
            YawPitch(eye.rotation * localFwd, out float ey, out float ep);
            float dy = Mathf.DeltaAngle(ey, headYaw + _eyeYaw), dp = (headPitch + _eyePitch) - ep;
            eye.rotation = Quaternion.AngleAxis(dy * gw, _root.up) * Quaternion.AngleAxis(dp * gw, _root.right) * eye.rotation;
        }
    }
}
