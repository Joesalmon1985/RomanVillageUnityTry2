using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// P1 polish: two or more people standing together hold a wordless conversation. Every few seconds the turn passes
    /// to another member, who gestures as in speech and looks at a listener; the listeners look at the speaker, now and
    /// then one nods or reacts. Works on <see cref="Npc"/>s (through <see cref="Npc.Chatting"/> and
    /// <see cref="Npc.LookPartner"/>, so a near player still draws their eyes) and on bare <see cref="PlaceholderActor"/>s.
    /// Pure presentation: no bubbles, no state outside the bodies. A member that walks away sits out until it stands again.
    /// </summary>
    public sealed class ConverseGroup : MonoBehaviour
    {
        private readonly List<Transform> _members = new List<Transform>();
        private int _speaker = -1;
        private float _next;
        private PropRandom _rnd;
        /// <summary>Turns taken (tests).</summary>
        public int Turns { get; private set; }
        public int Speaker => _speaker;
        public IReadOnlyList<Transform> Members => _members;

        /// <summary>Starts a conversation between the given people (null entries are skipped); returns the component.</summary>
        public static ConverseGroup Start(Transform host, params Transform[] people)
        {
            var g = host.gameObject.AddComponent<ConverseGroup>();
            foreach (var p in people) if (p != null) g._members.Add(p);
            int seed = 11; foreach (var p in g._members) seed = seed * 31 + p.name.GetHashCode();
            g._rnd = new PropRandom(seed);
            g._next = Time.time + 0.5f + g._rnd.Next() * 2f;
            return g;
        }

        private static bool Busy(Transform t)
        {
            var n = t.GetComponent<Npc>();
            if (n != null) return n.IsWalking || n.IsGreetingShown || n.HasShownBubble();
            return false;
        }

        private void Update()
        {
            if (_members.Count < 2 || Time.time < _next) return;
            _next = Time.time + _rnd.Range(2.2f, 4.8f);
            int pick = Mathf.FloorToInt(_rnd.Next() * (_members.Count - 1));
            if (_speaker >= 0 && pick >= _speaker) pick++;
            _speaker = Mathf.Clamp(pick, 0, _members.Count - 1);
            Turns++;
            var sp = _members[_speaker];
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m == null) continue;
                bool talking = i == _speaker && !Busy(m);
                Transform look = i == _speaker ? _members[(i + 1 + Mathf.FloorToInt(_rnd.Next() * (_members.Count - 1))) % _members.Count] : sp;
                var n = m.GetComponent<Npc>();
                if (n != null) { n.Chatting = talking; n.LookPartner = look; }
                else if (m.TryGetComponent<PlaceholderActor>(out var pa)) { pa.Talking = talking; pa.LookAt(look != null ? look.position + Vector3.up * 1.6f : (Vector3?)null); }
                // Listeners react now and then: a nod (a small bow) or, rarely, a laugh with the hands up.
                if (i != _speaker && !Busy(m) && _rnd.Next() < 0.3f)
                {
                    var a = n != null ? n.Actor : m.GetComponent<PlaceholderActor>();
                    if (a != null && !a.IsPlayingOneShot) a.PlayOneShot(_rnd.Next() < 0.8f ? ActorKeys.Nod : ActorKeys.Surprise);
                }
                else if (i == _speaker && talking && _rnd.Next() < 0.35f)
                {
                    var a = n != null ? n.Actor : m.GetComponent<PlaceholderActor>();
                    if (a != null && !a.IsPlayingOneShot) a.PlayOneShot(ActorKeys.Gesture);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var m in _members)
            {
                if (m == null) continue;
                var n = m.GetComponent<Npc>();
                if (n != null) { n.Chatting = false; n.LookPartner = null; }
                else if (m.TryGetComponent<PlaceholderActor>(out var pa)) { pa.Talking = false; pa.LookAt(null); }
            }
        }
    }
}
