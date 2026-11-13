using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Speech bubbles above heads with tappable replies (REFACTOR_SPEC 2.D "Screen", appendix B). Keys 1-3 answer the
    /// nearest open Ask. Replaces <see cref="DialoguePanel"/>. Owner: package P.
    /// F-0 stub: handles are issued and tracked (so callers can be written and tested for bookkeeping), nothing is drawn,
    /// no reply ever arrives and timeouts are not run.
    /// </summary>
    public static class SpeechBubbles
    {
        private static int _next = 1;
        private static readonly HashSet<int> _open = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _next = 1; _open.Clear(); }

        /// <summary>Shows a line above the speaker for a while; returns a handle.</summary>
        public static int Say(ISpeaker who, string text, float seconds)
        {
            int h = _next++;
            _open.Add(h);
            return h;
        }

        /// <summary>Shows a question with up to three replies; <paramref name="onReply"/> gets the reply index (0-based), or -1 on timeout.</summary>
        public static int Ask(ISpeaker who, string text, IList<string> replies, Action<int> onReply, float timeoutSeconds)
        {
            int h = _next++;
            _open.Add(h);
            return h;
        }

        public static void Close(int handle) => _open.Remove(handle);

        public static bool IsOpen(int handle) => _open.Contains(handle);
    }
}
