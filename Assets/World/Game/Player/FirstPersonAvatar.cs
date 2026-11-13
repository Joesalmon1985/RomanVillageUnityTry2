using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// First-person walker: CharacterController, WASD, mouse look, Shift run, gravity.
    /// Movement and look are blocked while any lock is held (God View, encounter, dialogue, menu...).
    /// Scripts drive it through <see cref="SetScriptedMove"/>, <see cref="SetLook"/> and <see cref="Teleport"/>.
    /// Transform position = feet; yaw on this transform, pitch on <see cref="Head"/>.
    /// Interim viewpoint after F-0: package P replaces it with <see cref="PlayerWalker"/> (REFACTOR_SPEC 7.3).
    /// </summary>
    public sealed class FirstPersonAvatar : MonoBehaviour
    {
        public const float WalkSpeed = 5f, RunSpeed = 8.5f, Gravity = -20f, Height = 1.8f, Radius = 0.35f;
        public const string ExternalLock = "External";

        public float MouseSensitivity = 2.2f;
        public CharacterController Controller { get; private set; }
        public Transform Head { get; private set; }
        public Camera Camera { get; private set; }
        public GameObject Marker { get; private set; }

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public Vector3 Position => transform.position;
        public Vector3 EyePosition => Head != null ? Head.position : transform.position + Vector3.up * WorldBootstrap.EyeHeight;
        public Vector3 Forward => Head != null ? Head.forward : transform.forward;
        public Vector3 Velocity { get; private set; }
        public bool CanMove => _moveLocks.Count == 0;
        public bool CanLook => _lookLocks.Count == 0;
        /// <summary>Area the feet are kept inside (centre XZ, radius).</summary>
        public Vector3 BoundsCentre; public float BoundsRadius = 1000f;

        private float _yaw, _pitch, _vy;
        private readonly HashSet<string> _moveLocks = new HashSet<string>();
        private readonly HashSet<string> _lookLocks = new HashSet<string>();
        private bool _scripted; private Vector3 _scriptedDir; private bool _scriptedRun;

        public static FirstPersonAvatar Create(Transform parent, Camera cam, Vector3 pos, float yaw)
        {
            var go = new GameObject("Avatar");
            go.layer = 2; // Ignore Raycast: picking never hits the player
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<FirstPersonAvatar>();
            a.Controller = go.AddComponent<CharacterController>();
            a.Controller.height = Height;
            a.Controller.radius = Radius;
            a.Controller.center = new Vector3(0, Height / 2, 0);
            a.Controller.stepOffset = 0.4f;
            a.Controller.slopeLimit = 50f;
            a.Controller.skinWidth = 0.04f;
            a.Head = new GameObject("Head").transform;
            a.Head.SetParent(go.transform, false);
            a.Head.localPosition = new Vector3(0, WorldBootstrap.EyeHeight, 0);
            a.Camera = cam;
            cam.transform.SetParent(a.Head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            a.BuildMarker();
            a.Teleport(pos, yaw, 0f);
            return a;
        }

        private void BuildMarker()
        {
            Marker = new GameObject("GodViewMarker");
            Marker.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var col = new Color(1f, 0.85f, 0.2f);
            // Arrow pointing along +Z (the avatar's facing), lying flat, plus a ring and a beacon.
            mb.Quad(new Vector3(-1.2f, 0.4f, -2f), new Vector3(-1.2f, 0.4f, 1f), new Vector3(1.2f, 0.4f, 1f), new Vector3(1.2f, 0.4f, -2f), col);
            mb.Tri(new Vector3(-2.8f, 0.4f, 1f), new Vector3(0, 0.4f, 4.2f), new Vector3(2.8f, 0.4f, 1f), col);
            mb.Cylinder(Vector3.zero, 0.5f, 0.5f, 6f, 8, new Color(1f, 0.55f, 0.1f));
            mb.Box(new Vector3(0, 6.5f, 0), new Vector3(2f, 2f, 2f), col);
            mb.ToObject("Arrow", Marker.transform, MaterialFactory.CreateUnlit(Color.white, "AvatarMarker"));
            Marker.SetActive(false);
        }

        public void SetControlEnabled(bool enabled)
        {
            if (enabled) { RemoveLock(ExternalLock); }
            else { AddLock(ExternalLock); }
        }

        /// <summary>Blocks both movement and look under a named reason.</summary>
        public void AddLock(string reason) { _moveLocks.Add(reason); _lookLocks.Add(reason); }
        public void RemoveLock(string reason) { _moveLocks.Remove(reason); _lookLocks.Remove(reason); }
        public void AddMoveLock(string reason) => _moveLocks.Add(reason);
        public void RemoveMoveLock(string reason) => _moveLocks.Remove(reason);
        public void AddLookLock(string reason) => _lookLocks.Add(reason);
        public void RemoveLookLock(string reason) => _lookLocks.Remove(reason);
        public bool HasLock(string reason) => _moveLocks.Contains(reason) || _lookLocks.Contains(reason);

        /// <summary>Places the feet at pos with the given yaw/pitch (degrees) exactly.</summary>
        public void Teleport(Vector3 pos, float yaw, float pitch = 0f)
        {
            if (Controller != null) Controller.enabled = false;
            transform.position = pos;
            SetLook(yaw, pitch);
            _vy = 0;
            if (Controller != null) Controller.enabled = true;
            Physics.SyncTransforms();
        }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            if (Head != null) Head.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        /// <summary>Turns yaw and pitch so the eye looks at a world point.</summary>
        public void LookAt(Vector3 point)
        {
            var d = point - EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            SetLook(yaw, pitch);
        }

        /// <summary>Scripted movement for the next frame: a world direction (y ignored, length clamped to 1).</summary>
        public void SetScriptedMove(Vector3 worldDir, bool run = false)
        {
            worldDir.y = 0;
            _scriptedDir = Vector3.ClampMagnitude(worldDir, 1f);
            _scriptedRun = run;
            _scripted = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            if (CanLook && cursorLocked)
            {
                float mx = Input.GetAxisRaw("Mouse X"), my = Input.GetAxisRaw("Mouse Y");
                if (mx != 0 || my != 0) SetLook(_yaw + mx * MouseSensitivity, _pitch - my * MouseSensitivity);
            }

            if (!CanMove)
            {
                _scripted = false;
                Velocity = Vector3.zero;
                return;
            }

            Vector3 dir; bool run;
            if (_scripted)
            {
                dir = _scriptedDir; run = _scriptedRun; _scripted = false;
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
                dir = transform.right * h + transform.forward * v;
                dir.y = 0;
                dir = Vector3.ClampMagnitude(dir, 1f);
                run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
            var horiz = dir * (run ? RunSpeed : WalkSpeed);
            if (Controller.isGrounded && _vy < 0) _vy = -2f;
            else _vy += Gravity * dt;
            var before = transform.position;
            Controller.Move((horiz + Vector3.up * _vy) * dt);

            // Safety: keep inside the world bounds and above the ground.
            var p = transform.position;
            var off = new Vector3(p.x - BoundsCentre.x, 0, p.z - BoundsCentre.z);
            if (off.magnitude > BoundsRadius || p.y < -5f)
            {
                off = Vector3.ClampMagnitude(off, BoundsRadius);
                Teleport(new Vector3(BoundsCentre.x + off.x, Mathf.Max(p.y, 0.05f), BoundsCentre.z + off.z), _yaw, _pitch);
            }
            Velocity = dt > 0 ? (transform.position - before) / dt : Vector3.zero;
        }
    }
}
