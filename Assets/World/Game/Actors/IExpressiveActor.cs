using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The abilities a rigged character adds on top of <see cref="Bedsit.ICharacterActor"/> (PRD_CHARACTERS CH-16, 5.8).
    /// Implemented by <see cref="RiggedActor"/>; the placeholder does not implement it, so callers test with
    /// <c>actor is IExpressiveActor x</c> and keep their placeholder path.
    /// </summary>
    public interface IExpressiveActor
    {
        /// <summary>Cast key of the body (prince, mira, champion, ...) and its costume (prehistoric, historic).</summary>
        string CastKey { get; }
        string Costume { get; }

        /// <summary>Looks at a transform (eyes first, then head, then chest; clamped). Null returns to automatic gaze.</summary>
        void SetGaze(Transform target);
        /// <summary>Looks at a world point; null returns to automatic gaze (the player when near or spoken to, else ahead).</summary>
        void SetGaze(Vector3? point);
        /// <summary>Where the eyes currently aim (world point), for tests and directors.</summary>
        Vector3 GazePoint { get; }

        /// <summary>Blends to a named expression (neutral, attentive, concentrating, surprised, pleased, pained, determined).</summary>
        bool SetExpression(string name, float weight = 1f);
        /// <summary>Back to neutral.</summary>
        void ResetExpression();
        string CurrentExpression { get; }

        /// <summary>Overrides the ground speed the locomotion uses (m/s); a negative value returns to the measured speed.</summary>
        void SetSpeedHint(float metresPerSecond);
        /// <summary>Measured ground speed of the root (m/s), smoothed.</summary>
        float GroundSpeed { get; }

        /// <summary>Named socket (Sock.Hand.R, Sock.Hand.L, Sock.StaffTip, Sock.SpellOrigin, Sock.Back, Sock.Hip.R, Sock.HeadTop, Sock.Foot.L/R).</summary>
        Transform Socket(string name);
        /// <summary>Parents a prop to a socket (local pose kept as given); false if the socket does not exist.</summary>
        bool Attach(Transform prop, string socket, Vector3 localPosition = default, Quaternion? localRotation = null);
        /// <summary>Removes (destroys) props attached to a socket; null = every socket.</summary>
        void DetachAll(string socket = null);

        /// <summary>Plays a clip of the character's set by name (Greet, Beckon, PointGround, CastAnticipate, Block, ...). False if unknown.</summary>
        bool PlayBeat(string clipName);
        /// <summary>Points at a world point and holds (null releases). The prince's PointGround.</summary>
        void PointAt(Vector3? worldPoint);
        /// <summary>True while speaking (jaw ticker and talk gestures). Null = automatic (a speech bubble of this NPC is on screen).</summary>
        void SetTalking(bool? talking);
        bool IsTalking { get; }

        /// <summary>Stops gestures and beats now and returns to idle.</summary>
        void ForceIdle();
        /// <summary>Clears gaze, expression, props attached at run time, talking and pointing (duel exit, rematch).</summary>
        void ResetPresentation();
        /// <summary>Sets facing at once (no turn clip).</summary>
        void SetYaw(float yaw);

        /// <summary>Animation events of the playing clips: (name, beat id). Names: Release, Impact, FootL, FootR, PropHit, BeatHit.</summary>
        event Action<string, int> BeatEvent;
    }

    /// <summary>
    /// Optional: an actor that shapes how fast its walker moves at the start and end of a walk so the feet can plant
    /// (weight shift before the first step, settle at the stop). <see cref="Npc"/> asks it each frame.
    /// </summary>
    public interface IMotionShaper
    {
        /// <summary>Multiplier (0..1) on the walker's speed this frame. <paramref name="remaining"/> is the distance left to the goal (m), infinity when following.</summary>
        float SpeedScale(bool walking, float speed, float remaining);
    }
}
