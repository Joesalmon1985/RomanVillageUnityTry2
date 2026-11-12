using System;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// The player's eyes. A rig root (position = the fixed standpoint) carries a yaw pivot, which carries a pitch pivot,
    /// which carries the Camera. There is no avatar, no third-person camera and no free look: only four compass
    /// facings, 90 degrees apart, reached by a short eased turn that never changes the camera's position.
    /// </summary>
    public class FirstPersonViewController : MonoBehaviour
    {
        [Tooltip("Seconds for a 90 degree turn (spec: ~0.2-0.4).")]
        public float TurnSeconds = 0.3f;

        public Transform YawPivot;
        public Transform PitchPivot;
        public Camera EyeCamera;

        public CompassDirection Facing { get; private set; } = CompassDirection.North;
        public bool IsTurning { get; private set; }
        public bool TurnsEnabled = true;

        /// <summary>Raised when a turn begins (target facing).</summary>
        public event Action<CompassDirection> TurnStarted;
        /// <summary>Raised when a turn finishes and the camera sits exactly on the compass heading.</summary>
        public event Action<CompassDirection> TurnCompleted;

        float _fromYaw, _toYaw, _t;

        public float CurrentYaw => YawPivot != null ? YawPivot.eulerAngles.y : 0f;

        void Awake()
        {
            if (YawPivot == null) YawPivot = transform;
            if (EyeCamera == null) EyeCamera = GetComponentInChildren<Camera>();
            if (PitchPivot == null && EyeCamera != null) PitchPivot = EyeCamera.transform.parent;
        }

        /// <summary>Snap to a facing without animation.</summary>
        public void SnapTo(CompassDirection facing)
        {
            Facing = facing;
            IsTurning = false;
            YawPivot.rotation = Quaternion.Euler(0f, facing.YawDegrees(), 0f);
        }

        public bool TurnRight() => BeginTurn(Facing.TurnedRight(), +90f);
        public bool TurnLeft() => BeginTurn(Facing.TurnedLeft(), -90f);

        bool BeginTurn(CompassDirection target, float deltaYaw)
        {
            if (!TurnsEnabled || IsTurning) return false;
            _fromYaw = YawPivot.eulerAngles.y;
            _toYaw = _fromYaw + deltaYaw;           // continuous angle, so we always take the short way round
            _t = 0f;
            Facing = target;
            IsTurning = true;
            TurnStarted?.Invoke(target);
            if (TurnSeconds <= 0.0001f) Finish();
            return true;
        }

        void Update()
        {
            if (!IsTurning) return;
            _t += Time.deltaTime / TurnSeconds;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
            YawPivot.rotation = Quaternion.Euler(0f, Mathf.Lerp(_fromYaw, _toYaw, k), 0f);
            if (_t >= 1f) Finish();
        }

        void Finish()
        {
            YawPivot.rotation = Quaternion.Euler(0f, Facing.YawDegrees(), 0f);   // exact, no float drift
            IsTurning = false;
            TurnCompleted?.Invoke(Facing);
        }

        public void SetPitch(float degrees)
        {
            if (PitchPivot != null) PitchPivot.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
