using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// Moves the first-person rig through doorways. The camera stays at the player's eye height for the whole move:
    /// only X/Z change, there is no bob and no avatar, and turning is locked while moving.
    /// </summary>
    public class PlayerTransitionController : MonoBehaviour
    {
        public Transform Rig;
        public FirstPersonViewController View;
        [Tooltip("Walking speed in metres per second.")]
        public float Speed = 1.35f;

        public bool IsMoving { get; private set; }
        public float MinEyeHeightDuringMove { get; private set; }
        public float MaxEyeHeightDuringMove { get; private set; }

        public event Action MoveStarted;
        public event Action MoveFinished;

        /// <summary>Walk through the waypoints (their Y is ignored: eye height is preserved). Optional callback on arrival.</summary>
        public Coroutine MoveAlong(IList<Vector3> waypoints, Action onArrived = null)
        {
            if (IsMoving) return null;
            return StartCoroutine(MoveRoutine(new List<Vector3>(waypoints), onArrived));
        }

        IEnumerator MoveRoutine(List<Vector3> waypoints, Action onArrived)
        {
            IsMoving = true;
            bool turnsWere = View != null && View.TurnsEnabled;
            if (View != null) View.TurnsEnabled = false;
            float eyeY = Rig.position.y;
            MinEyeHeightDuringMove = MaxEyeHeightDuringMove = eyeY;
            MoveStarted?.Invoke();

            foreach (var wp in waypoints)
            {
                var target = new Vector3(wp.x, eyeY, wp.z);
                while ((Rig.position - target).sqrMagnitude > 1e-6f)
                {
                    
                    Rig.position = Vector3.MoveTowards(Rig.position, target, Speed * Time.deltaTime);
                    MinEyeHeightDuringMove = Mathf.Min(MinEyeHeightDuringMove, Rig.position.y);
                    MaxEyeHeightDuringMove = Mathf.Max(MaxEyeHeightDuringMove, Rig.position.y);
                    yield return null;
                }
            }

            if (View != null) View.TurnsEnabled = turnsWere;
            IsMoving = false;
            MoveFinished?.Invoke();
            onArrived?.Invoke();
        }
    }
}
