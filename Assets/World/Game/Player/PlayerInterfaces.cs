using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    // Shared interfaces between packages (REFACTOR_SPEC appendix B). Owner: package P. V and N implement them.

    /// <summary>Anything selectable: a tap shows its title and lines (inspect bubble; InspectPanel in God View).</summary>
    public interface IInspectable
    {
        string Title { get; }
        IEnumerable<string> Lines { get; }
    }

    /// <summary>Anything the player can use with E in first person (interim, until P's pointer replaces the E key).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact();
    }

    /// <summary>A person the player can tap and "Follow" (villagers, carriers).</summary>
    public interface IFollowable
    {
        string FollowName { get; }
        Transform FollowTransform { get; }
    }

    /// <summary>Someone who talks in speech bubbles (<see cref="SpeechBubbles"/>).</summary>
    public interface ISpeaker
    {
        /// <summary>Villager id, e.g. "prince", "sawyer", "merchant", "mira".</summary>
        string SpeakerId { get; }
        /// <summary>Where the bubble hangs (above the head).</summary>
        Transform Head { get; }
    }
}
