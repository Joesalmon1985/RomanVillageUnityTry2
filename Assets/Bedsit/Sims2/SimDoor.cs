using System.Collections;
using OpenTS2.Components;
using OpenTS2.Content.DBPF.Scenegraph;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// A genuine Sims 2 door object whose leaf swings using the game's own door animation resource
    /// ("o-door-open-CCW_anim" / "o-door-open-CW_anim", applied to the door's own r_door_rot hinge bone).
    /// The door can rest ajar (a paused, partly-opened animation frame) and later be opened fully.
    /// </summary>
    public class SimDoor : MonoBehaviour
    {
        public string OpenAnimationName = "o-door-open-CCW_anim";
        [Range(0f, 1f)] public float AjarNormalizedTime = 0.3f;

        public bool IsOpen { get; private set; }
        public bool IsOpening { get; private set; }
        public float OpenFraction { get; private set; }
        public string ResolvedAnimationResource { get; private set; }
        public Animation AnimationComponent => _animation;
        public string ClipName => _clipName;

        [SerializeField] Animation _animation;
        AnimationState _state;
        [SerializeField] string _clipName;

        /// <summary>Wire the door up. <paramref name="doorObject"/> is the root created from the door's CRES.</summary>
        public bool Setup(GameObject doorObject, string openAnimation)
        {
            OpenAnimationName = openAnimation;
            var scene = doorObject.GetComponentInChildren<ScenegraphComponent>();
            var asset = Sims2Runtime.GetAnimation(openAnimation);
            if (scene == null || asset == null) return false;
            if (!scene.gameObject.TryGetComponent(out _animation)) _animation = scene.gameObject.AddComponent<Animation>();
            _animation.playAutomatically = false;
            var clip = asset.CreateClipFromResource(scene.BoneNamesToRelativePaths, scene.BlendNamesToRelativePaths);
            clip.wrapMode = WrapMode.ClampForever;
            _clipName = openAnimation;
            _animation.AddClip(clip, _clipName);
            ResolvedAnimationResource = openAnimation;
            SetFraction(AjarNormalizedTime);
            return true;
        }

        /// <summary>Baked prefab path: the Animation component and clip were serialised at bake time; just set the rest pose.</summary>
        public bool RebindBaked()
        {
            if (_animation == null) return false;
            if (string.IsNullOrEmpty(_clipName) || _animation[_clipName] == null)
            {
                if (_animation.clip == null) return false;
                _clipName = _animation.clip.name;                 // baked clip may carry its asset file name
            }
            _animation.playAutomatically = false;
            ResolvedAnimationResource = _clipName;
            SetFraction(AjarNormalizedTime);
            return true;
        }

        /// <summary>Hold the door at a fraction (0 closed .. 1 fully open) of the original animation.</summary>
        public void SetFraction(float f)
        {
            if (_animation == null) return;
            f = Mathf.Clamp01(f);
            _animation.Play(_clipName);
            _state = _animation[_clipName];
            _state.speed = 0f;
            _state.normalizedTime = f;
            _animation.Sample();
            OpenFraction = f;
            IsOpen = f >= 0.999f;
        }

        /// <summary>Play the original animation forward from the current fraction to fully open.</summary>
        public Coroutine Open(System.Action done = null)
        {
            if (IsOpen || IsOpening) { done?.Invoke(); return null; }
            return StartCoroutine(OpenRoutine(done));
        }

        IEnumerator OpenRoutine(System.Action done)
        {
            IsOpening = true;
            float length = _state != null && _state.length > 0.05f ? _state.length : 1f;
            while (OpenFraction < 1f)
            {
                SetFraction(OpenFraction + Time.deltaTime / length);
                yield return null;
            }
            IsOpening = false;
            IsOpen = true;
            done?.Invoke();
        }
    }
}
