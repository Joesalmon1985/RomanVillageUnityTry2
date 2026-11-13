using UnityEngine;

namespace World.Game
{
    /// <summary>2D one-shot sound effects for encounters (synthesised once, cached).</summary>
    public sealed class EncounterSfx : MonoBehaviour
    {
        private static AudioClip _cheer, _cast, _crack;
        public static AudioClip CheerClip => _cheer != null ? _cheer : (_cheer = SynthAudio.Cheer());
        public static AudioClip CastClip => _cast != null ? _cast : (_cast = SynthAudio.CastWhoosh());
        public static AudioClip CrackClip => _crack != null ? _crack : (_crack = SynthAudio.Crack());

        private AudioSource _src;

        public static EncounterSfx Create(Transform parent)
        {
            var go = new GameObject("EncounterSfx");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<EncounterSfx>();
            s._src = go.AddComponent<AudioSource>();
            s._src.playOnAwake = false;
            s._src.spatialBlend = 0f;
            return s;
        }

        public void Play(AudioClip clip, float volume = 0.8f)
        {
            if (clip != null && _src != null && AudioGuard.CanWriteControls) _src.PlayOneShot(clip, volume);
        }

        public void Stop() { if (_src != null && AudioGuard.CanWriteControls) _src.Stop(); }
    }
}
