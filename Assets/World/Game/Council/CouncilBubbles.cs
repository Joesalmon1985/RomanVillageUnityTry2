using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Speech bubbles of the council. Handles made here are remembered; while the council encounter is open
    /// <see cref="Sweep"/> closes every other bubble and edge indicator ("The prince calls you", villagers' lines), so only
    /// the council's people speak. The other systems simply find their bubble gone and ask again later.
    /// </summary>
    public static class CouncilBubbles
    {
        static readonly HashSet<int> Mine = new HashSet<int>();
        public static int SweptCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Mine.Clear(); SweptCount = 0; }

        public static int Say(ISpeaker who, string text, float seconds)
        {
            int h = SpeechBubbles.Say(who, text, seconds);
            Mine.Add(h);
            return h;
        }

        public static void Sweep()
        {
            foreach (int h in SpeechBubbles.OpenHandles())
                if (!Mine.Contains(h)) { SpeechBubbles.Close(h); SweptCount++; }
            Mine.RemoveWhere(h => !SpeechBubbles.IsOpen(h));
        }
    }
}
