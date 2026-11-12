using System;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// Rendering-agnostic handle on an animated character. Gameplay code (interactions, scene logic) talks only to this
    /// interface using logical animation keys ("wave", "greet", "idle"); the Sims 2 implementation maps those keys to
    /// original game animation resources. A sprite-baked or Godot-side implementation could replace it later.
    /// </summary>
    public interface ICharacterActor
    {
        string ActorId { get; }
        Transform Root { get; }

        /// <summary>The logical key currently playing (null if none).</summary>
        string CurrentKey { get; }
        bool IsPlayingOneShot { get; }

        /// <summary>Raised when a one-shot animation finishes (key).</summary>
        event Action<string> OneShotFinished;

        /// <summary>Play a looping idle (the actor's resting animation).</summary>
        void PlayIdle();

        /// <summary>Play a one-shot animation by logical key, then return to idle. False if unknown.</summary>
        bool PlayOneShot(string key);

        /// <summary>Turn to face a world position around the vertical axis.</summary>
        void FaceTowards(Vector3 worldPoint);

        /// <summary>Show/hide all renderers of this actor.</summary>
        void SetVisible(bool visible);
    }

    public static class AnimationKeys
    {
        public const string Pose = "pose";
        public const string Idle = "idle";
        public const string Wave = "wave";
        public const string Greet = "greet";
        public const string Clap = "clap";
        public const string Cheer = "cheer";
        public const string Surprise = "surprise";
        public const string Stretch = "stretch";
    }
}
