using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>One clip of a rigged character with the manifest facts the runtime needs (PRD_CHARACTERS 6, contract v2 2.6).</summary>
    [Serializable]
    public sealed class RigClip
    {
        /// <summary>Base clip name (e.g. "Walk"); the clip itself may be the character's variant ("Walk@prince").</summary>
        public string Name;
        public AnimationClip Clip;
        public bool Loop, Additive;
        public float Length, NativeSpeed;
        /// <summary>Ground distance (m) per frame at 30 fps for Start and Stop clips; empty otherwise.</summary>
        public float[] RootDist = new float[0];
        /// <summary>Yaw (degrees, + = left) per frame at 30 fps for turn clips; empty otherwise.</summary>
        public float[] RootYaw = new float[0];
        /// <summary>Gait phase a Start clip hands over at, or a Stop clip is entered from (-1 when not given).</summary>
        public float ExitPhase = -1f, EnterPhase = -1f;
        /// <summary>Held part of the clip in seconds (x..y), when the manifest gives hold frames; (-1,-1) otherwise.</summary>
        public Vector2 Hold = new Vector2(-1f, -1f);
        public string[] EventNames = new string[0];
        public float[] EventTimes = new float[0];
    }

    /// <summary>A named face expression: shape-key weights plus jaw and blink rate (manifest face.expressions).</summary>
    [Serializable]
    public sealed class RigExpression
    {
        public string Name;
        public string[] Keys = new string[0];
        public float[] Weights = new float[0];
        public float JawDeg;
        public float BlinkRate = 1f;
        public bool GazeDown;
    }

    /// <summary>Per-character face tuning (manifest face.tuning).</summary>
    [Serializable]
    public sealed class RigFaceTuning
    {
        public float BlinkClose = 0.06f, BlinkHold = 0.03f, BlinkOpen = 0.1f, BlinkMin = 2.5f, BlinkMax = 6f, DoubleBlink = 0.15f;
        public float GainBlink = 1f, GainBrow = 1f, GainJaw = 1f, GainMouth = 1f;
        public float EyeYaw = 12f, EyePitch = 5f, HeadYaw = 55f, HeadPitch = 25f, HeadShare = 0.4f, ChestShare = 0.15f;
        public float EyeLead = 0.06f, HeadLag = 0.15f, Saccade = 0.05f;
        public string TalkKey = "MouthOpen";
        public float TalkRate = 7f, TalkWeightMin = 0.15f, TalkWeightMax = 0.55f, TalkJawMin = 0f, TalkJawMax = 2f;
    }

    /// <summary>
    /// Data on a generated character prefab (written by the WP-6 builder from cast_manifest.json; the prefab lives in the
    /// git-ignored Assets/LocalBaked/Characters/Resources/Characters). Read by <see cref="RiggedActor"/>.
    /// </summary>
    public sealed class RiggedCharacter : MonoBehaviour
    {
        public string Key, Costume;
        public bool Humanoid;
        public Animator Animator;
        /// <summary>The single merged skinned mesh (one submesh per role slot).</summary>
        public SkinnedMeshRenderer Body;
        /// <summary>Role slot of each submesh of <see cref="Body"/> (skin, hair, cloth_main, ...).</summary>
        public string[] SubmeshSlots = new string[0];
        public RigClip[] Clips = new RigClip[0];
        public string[] SocketNames = new string[0];
        public Transform[] Sockets = new Transform[0];
        public Transform Hips, Spine, Chest, Neck, Head, Jaw, EyeL, EyeR, HandL, HandR, FootL, FootR;
        public Transform[] Cape = new Transform[0], Hood = new Transform[0], Skirt = new Transform[0];
        /// <summary>Blend shape names in mesh order, and the face channel of each (blink, brow, mouth).</summary>
        public string[] ShapeKeys = new string[0];
        public string[] ShapeChannels = new string[0];
        public RigExpression[] Expressions = new RigExpression[0];
        public RigFaceTuning Tuning = new RigFaceTuning();
        /// <summary>Visible triangles of this costume (manifest) and the measured height in metres.</summary>
        public int Triangles;
        public float Height = 1.8f;
        /// <summary>Character forward in the model's space at bind pose (the builder measures it from the eyes).</summary>
        public Vector3 ModelForward = Vector3.forward;
        /// <summary>Humanoid mask for gestures and talk while walking (head and arms): Assets/Characters/Animation/Masks/UpperBody.mask.</summary>
        public AvatarMask UpperBody;
        /// <summary>The characteristic talk loop of this character (Talk_A, Talk_B or Talk_Emphasis).</summary>
        public string TalkClip = "Talk_A";

        public RigClip Clip(string name)
        {
            for (int i = 0; i < Clips.Length; i++) if (Clips[i] != null && Clips[i].Name == name) return Clips[i];
            return null;
        }

        public Transform Socket(string name)
        {
            for (int i = 0; i < SocketNames.Length; i++) if (SocketNames[i] == name) return Sockets[i];
            return null;
        }

        public int Shape(string name) { return Array.IndexOf(ShapeKeys, name); }
    }
}
