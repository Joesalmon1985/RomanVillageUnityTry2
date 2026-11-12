using System.Collections.Generic;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>Handles to the things gameplay needs from the built world. No gameplay logic lives here.</summary>
    public class BedsitWorld : MonoBehaviour
    {
        public Vector3 BedsitStandpoint;      // fixed eye position inside the bedsit
        public Vector3 CorridorStandpoint;    // fixed eye position in the corridor
        public Vector3 BedPillowEye;          // where the player's eyes are when lying in bed
        public float StandingPitch = 0f;      // resting downward tilt (degrees, positive = down) so surfaces below eye level are in frame
        public float WakeYaw = 270f;          // heading (degrees) of the player's view while lying in bed
        public Vector3 DoorwayPoint;          // centre of the northern doorway (floor level)

        public SimDoor Door;
        public Transform MirrorFrame;         // position = glass centre, forward = normal into the room
        public GameObject MirrorFrameObject;  // the genuine frame model (never reflected in its own glass)
        public Vector2 MirrorGlassSize;

        public Vector3 CorridorNpcSpot, EastNpcSpot, WestNpcSpot;
        public Vector3 CorridorNpcLookAt, EastNpcLookAt, WestNpcLookAt;

        public Transform DoorCueAnchor, EastWindowCueAnchor, WestWindowCueAnchor, MirrorCueAnchor, CorridorNpcCueAnchor, CorridorBackDoorCueAnchor;

        public Light Sun, RoomLight, CorridorLight;
        public List<string> Report = new List<string>();      // provenance: which original resources were used
    }
}
