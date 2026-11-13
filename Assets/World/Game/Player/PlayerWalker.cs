using System;
using System.Collections.Generic;
using UnityEngine;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The player's floating viewpoint on the walking lattice (REFACTOR_SPEC 2.D, appendix B). Owner: package P.
    /// F-0 stub: the members other packages use exist with their final signatures, but nothing creates a PlayerWalker
    /// yet (<see cref="Instance"/> is null) and the methods only update the stored pose. The interim viewpoint is
    /// <see cref="FirstPersonAvatar"/> until P replaces it.
    /// </summary>
    public sealed class PlayerWalker : MonoBehaviour
    {
        public static PlayerWalker Instance { get; private set; }

        public Cell Cell { get; private set; }
        /// <summary>0..5, see <see cref="WalkLattice.Yaw"/>.</summary>
        public int Heading { get; private set; }
        /// <summary>Feet position (on the ground).</summary>
        public Vector3 Position => transform.position;
        public Vector3 EyePosition => transform.position + Vector3.up * WorldBootstrap.EyeHeight;
        public float Yaw => transform.eulerAngles.y;
        public bool IsGliding { get; private set; }
        public IFollowable Following { get; private set; }

        /// <summary>Raised after every completed step (not turns).</summary>
        public event Action Stepped;

        private readonly HashSet<string> _locks = new HashSet<string>();
        public bool IsLocked => _locks.Count > 0;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Forward() { }
        public void Back() { }
        public void TurnLeft() { }
        public void TurnRight() { }
        public void WalkTo(Cell target) { }
        public void Follow(IFollowable who) { Following = who; }
        public void StopAuto() { Following = null; }

        /// <summary>Puts the viewpoint on a cell with a heading at once (load, tests).</summary>
        public void Teleport(Cell cell, int heading)
        {
            Cell = cell; Heading = ((heading % 6) + 6) % 6;
            transform.SetPositionAndRotation(WalkLattice.ToWorld(cell), Quaternion.Euler(0, WalkLattice.Yaw(Heading), 0));
        }

        /// <summary>Encounters: an exact pose off the lattice; <see cref="RestoreCell"/> snaps back to the cell.</summary>
        public void PlaceExact(Vector3 position, float yaw, float pitch)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        }

        public void RestoreCell() => Teleport(Cell, Heading);

        public void AddLock(string reason) => _locks.Add(reason);
        public void RemoveLock(string reason) => _locks.Remove(reason);

        // Keeps the event "used" until P raises it (avoids a compiler warning in the stub).
        private void RaiseStepped() => Stepped?.Invoke();
    }
}
