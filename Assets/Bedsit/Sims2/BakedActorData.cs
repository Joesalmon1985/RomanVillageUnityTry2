using System;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// Attached to a baked character prefab: the full-body animation clips (sampled from the original Sims 2 animations
    /// through OpenTS2 at bake time) keyed by logical animation key. Lets the character play on devices that have no Sims 2
    /// install and no OpenTS2 run-time data (for example a phone).
    /// </summary>
    public class BakedActorData : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public AnimationClip Clip;
            public string SourceResource;      // original Sims 2 animation resource name
        }

        public Entry[] Clips = new Entry[0];
        public string SourceNote;               // e.g. "N001_User00012.package"

        public bool TryGet(string key, out AnimationClip clip)
        {
            foreach (var e in Clips)
                if (e.Key == key && e.Clip != null) { clip = e.Clip; return true; }
            clip = null;
            return false;
        }

        public bool Has(string key) => TryGet(key, out _);
    }
}
