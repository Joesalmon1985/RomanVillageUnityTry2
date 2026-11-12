using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenTS2.Components;
using OpenTS2.Content;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// A Sims 2 character in the Unity scene: genuine game skeleton ("auskel"), genuine body/head/hair/clothing meshes
    /// and genuine game animations, assembled by OpenTS2's SimCharacterComponent. Implements ICharacterActor so
    /// gameplay code never touches Sims 2 types.
    /// </summary>
    public class SimCharacterActor : MonoBehaviour, ICharacterActor
    {
        public string ActorId { get; private set; }
        public Transform Root => transform;
        public string CurrentKey { get; private set; }
        public bool IsPlayingOneShot { get; private set; }
        public event Action<string> OneShotFinished;

        /// <summary>Where this appearance came from (for provenance): "naked-base" or "N001_User00005.package".</summary>
        public string AppearanceSource { get; private set; }

        public SimCharacterComponent Sim { get; private set; }
        BakedActorData _baked;                                   // non-null when running from baked assets (no OpenTS2 data)
        Dictionary<string, Transform> _bakedBones;
        public bool IsBaked => _baked != null;
        Animation _animation;
        readonly Dictionary<string, string> _clipNames = new Dictionary<string, string>();   // key -> clip name on this actor
        Coroutine _oneShot;
        string _resolvedIdleName;

        // ------------------------------------------------------------------------------------------------ creation

        /// <summary>The unclothed base Sim (the exact proof used by OpenTS2's own SimAnimationTest).</summary>
        public static SimCharacterActor CreateNakedBase(string actorId)
        {
            if (!Sims2Runtime.Initialise()) return null;
            var sim = SimCharacterComponent.CreateNakedBaseSim();
            return Wrap(actorId, sim, "naked-base (amBodyNaked_cres / amHairBald_cres / amFace_cres, auskel skeleton)");
        }

        /// <summary>
        /// A premade neighbourhood Sim with its genuine CAS appearance and clothing, rigged with the original skeleton so
        /// the original animations can drive it. Reads Neighborhoods/{prefix}/Characters/{prefix}_User{id:D5}.package
        /// from the configured user directory (a local copy of the game's own N001 Pleasantview characters).
        /// </summary>
        public static SimCharacterActor CreateFromNeighborhood(string actorId, string neighborhoodPrefix, uint userInstanceId)
        {
            if (!Sims2Runtime.Initialise()) return null;
            var outfit = SimCharacterComponent.CreatePersonOutfit(neighborhoodPrefix, userInstanceId, default, skinnedParts: true);
            if (outfit == null)
            {
                Debug.LogWarning($"[SimCharacterActor] no outfit for {neighborhoodPrefix}_User{userInstanceId:D5}");
                return null;
            }
            var sim = SimCharacterComponent.CreateAnimatedFromOutfit(outfit);
            if (sim == null)
            {
                Debug.LogWarning($"[SimCharacterActor] outfit for {neighborhoodPrefix}_User{userInstanceId:D5} has no rigged skeleton");
                UnityEngine.Object.Destroy(outfit);
                return null;
            }
            return Wrap(actorId, sim, $"{neighborhoodPrefix}_User{userInstanceId:D5}.package (CAS outfit, Casual1)");
        }

        /// <summary>Instantiate a baked character prefab (see ContentBaker): plain skinned meshes + sampled animation clips.</summary>
        public static SimCharacterActor InstantiateBaked(GameObject prefab, string actorId)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "Actor_" + actorId;
            var actor = go.GetComponent<SimCharacterActor>();
            if (actor == null) actor = go.AddComponent<SimCharacterActor>();
            actor.ActorId = actorId;
            actor._baked = go.GetComponent<BakedActorData>();
            actor.AppearanceSource = "baked: " + (actor._baked != null ? actor._baked.SourceNote : "?");
            actor._animation = go.GetComponentInChildren<Animation>(true);
            if (actor._animation != null) actor._animation.cullingType = AnimationCullingType.AlwaysAnimate;
            actor._bakedBones = new Dictionary<string, Transform>();
            if (actor._animation != null)
                foreach (var t in actor._animation.GetComponentsInChildren<Transform>(true))
                    if (!actor._bakedBones.ContainsKey(t.name)) actor._bakedBones[t.name] = t;
            return actor;
        }

        static SimCharacterActor Wrap(string actorId, SimCharacterComponent sim, string source)
        {
            var go = new GameObject("Actor_" + actorId);
            var actor = go.AddComponent<SimCharacterActor>();
            actor.ActorId = actorId;
            actor.AppearanceSource = source;
            actor.Sim = sim;
            sim.transform.SetParent(go.transform, false);
            actor._animation = sim.GetComponentInChildren<Animation>();
            if (actor._animation == null) Debug.LogWarning("[SimCharacterActor] no Animation component on " + actorId);
            else actor._animation.cullingType = AnimationCullingType.AlwaysAnimate;

            // Register every IK chain the library's animations use up front: SimCharacterComponent rebuilds its
            // animation graph whenever a new chain appears, and rebuilding mid-playback can crash the editor.
            foreach (var key in SimAnimationLibrary.Definitions.Keys)
            {
                var res = SimAnimationLibrary.Resolve(key);
                if (res != null) sim.AdjustInverseKinematicWeightsForAnimations(res.Asset.AnimResource);
            }
            return actor;
        }

        // ------------------------------------------------------------------------------------------------ animation

        // Legacy-Animation layers: 0 = neutral pose, 1 = idle overlay, 2 = one-shot gestures. Original Sims 2 animations are
        // authored over the neutral standing pose, so every Sim keeps that pose running underneath.
        const int LayerPose = 0, LayerIdle = 1, LayerOneShot = 2;

        /// <summary>Ensure the library animation for this key exists as a clip on this actor. Returns the clip name.</summary>
        string EnsureClip(string key, int layer)
        {
            if (_clipNames.TryGetValue(key, out var existing)) return existing;
            if (IsBaked)
            {
                if (_animation == null || !_baked.TryGet(key, out var bakedClip)) return null;
                bakedClip.legacy = true;
                bakedClip.wrapMode = key == AnimationKeys.Idle ? WrapMode.Loop : WrapMode.Once;
                _animation.AddClip(bakedClip, key);
                _animation[key].layer = key == AnimationKeys.Idle ? 0 : 1;
                _clipNames[key] = key;
                return key;
            }
            var res = SimAnimationLibrary.Resolve(key);
            if (res == null || _animation == null || Sim == null) return null;
            var clip = res.Asset.CreateClipFromResource(Sim.Scenegraph.BoneNamesToRelativePaths, Sim.Scenegraph.BlendNamesToRelativePaths,
                minimumDuration: key == AnimationKeys.Pose ? 0.5f : 0f);
            // The neutral pose is a single frame (length 0): hold it instead of looping it.
            clip.wrapMode = key == AnimationKeys.Pose ? WrapMode.ClampForever : (res.Def.Loop ? WrapMode.Loop : WrapMode.Once);
            string clipName = res.ResourceName;
            _animation.AddClip(clip, clipName);
            _animation[clipName].layer = layer;
            _clipNames[key] = clipName;
            return clipName;
        }

        /// <summary>Activate the IK chains needed by the base pose plus the given animation (they play together).</summary>
        void SetIk(string key)
        {
            if (Sim == null) return;
            var pose = SimAnimationLibrary.Resolve(AnimationKeys.Pose);
            var res = SimAnimationLibrary.Resolve(key);
            Sim.AdjustInverseKinematicWeightsForAnimations(pose?.Asset.AnimResource, res?.Asset.AnimResource);
        }

        void EnsureBasePose()
        {
            if (IsBaked)
            {
                var idleBase = EnsureClip(AnimationKeys.Idle, 0);
                if (idleBase != null && !_animation.IsPlaying(idleBase)) { _animation.Play(idleBase); _animation[idleBase].weight = 1f; }
                return;
            }
            var pose = EnsureClip(AnimationKeys.Pose, LayerPose);
            if (pose != null && !_animation.IsPlaying(pose))
            {
                _animation.Play(pose);
                _animation[pose].weight = 1f;
            }
        }

        /// <summary>Stop everything (including frozen scrubbed states) and return to the idle loop.</summary>
        public void ResetToIdle()
        {
            if (_animation != null) _animation.Stop();
            _activeOneShotClip = null;
            PlayIdle();
        }

        public void PlayIdle()
        {
            if (_animation == null) return;
            if (_oneShot != null) { StopCoroutine(_oneShot); _oneShot = null; }
            if (_activeOneShotClip != null) { _animation.Blend(_activeOneShotClip, 0f, 0.25f); _activeOneShotClip = null; }
            IsPlayingOneShot = false;
            EnsureBasePose();
            if (IsBaked) { CurrentKey = AnimationKeys.Idle; return; }
            var idle = EnsureClip(AnimationKeys.Idle, LayerIdle);
            if (idle != null)
            {
                SetIk(AnimationKeys.Idle);
                _animation.CrossFade(idle, 0.25f, PlayMode.StopSameLayer);
            }
            CurrentKey = AnimationKeys.Idle;
        }

        public bool PlayOneShot(string key)
        {
            if (_animation == null) return false;
            if (!IsBaked && !SimAnimationLibrary.Has(key)) return false;
            if (EnsureClip(key, LayerOneShot) == null) return false;
            if (_oneShot != null) StopCoroutine(_oneShot);
            _oneShot = StartCoroutine(OneShotRoutine(key));
            return true;
        }

        string _activeOneShotClip;

        IEnumerator OneShotRoutine(string key)
        {
            IsPlayingOneShot = true;
            EnsureBasePose();
            var clipName = _clipNames[key];
            _activeOneShotClip = clipName;
            SetIk(key);
            CurrentKey = key;
            _animation[clipName].time = 0f;
            _animation.CrossFade(clipName, 0.15f, PlayMode.StopSameLayer);
            yield return null;
            var state = _animation[clipName];
            const float fadeOut = 0.3f;
            while (_animation != null && _animation.IsPlaying(clipName) && state.time < state.length - fadeOut) yield return null;
            if (_animation != null) _animation.Blend(clipName, 0f, fadeOut);
            yield return new WaitForSeconds(fadeOut);
            if (_animation != null) _animation.Stop(clipName);
            _activeOneShotClip = null;
            IsPlayingOneShot = false;
            _oneShot = null;
            SetIk(AnimationKeys.Idle);
            CurrentKey = AnimationKeys.Idle;
            OneShotFinished?.Invoke(key);
        }

        /// <summary>Freeze a library animation at a normalised time (0..1) over the base pose. Used by the sprite baker; the
        /// pose is applied by the Animation/IK graph on the following frame(s).</summary>
        public bool ScrubTo(string key, float normalizedTime)
        {
            if (_animation == null) return false;
            if (_oneShot != null) { StopCoroutine(_oneShot); _oneShot = null; }
            EnsureBasePose();
            var clipName = EnsureClip(key, LayerOneShot);
            if (clipName == null) return false;
            SetIk(key);
            var st = _animation[clipName];
            _animation.Play(clipName);
            st.weight = 1f;
            st.speed = 0f;
            st.normalizedTime = Mathf.Clamp01(normalizedTime);
            var idle = EnsureClip(AnimationKeys.Idle, LayerIdle);
            if (idle != null && idle != clipName) _animation[idle].weight = 0f;
            CurrentKey = key;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ pose + placement

        /// <summary>Local rotation of every skeleton bone: used to prove an original animation visibly changes the pose.</summary>
        public Quaternion[] CapturePose()
        {
            if (IsBaked)
                return _bakedBones.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value.localRotation).ToArray();
            if (Sim == null) return new Quaternion[0];
            return Sim.Scenegraph.BoneNamesToTransform.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value != null ? kv.Value.localRotation : Quaternion.identity).ToArray();
        }

        /// <summary>Largest per-bone angular difference (degrees) between two poses.</summary>
        public static float MaxPoseDelta(Quaternion[] a, Quaternion[] b)
        {
            float max = 0f;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) max = Mathf.Max(max, Quaternion.Angle(a[i], b[i]));
            return max;
        }

        public Transform Bone(string name) =>
            IsBaked ? (_bakedBones.TryGetValue(name, out var bt) ? bt : null) :
            Sim != null && Sim.Scenegraph.BoneNamesToTransform.TryGetValue(name, out var t) ? t : null;

        public void FaceTowards(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        /// <summary>Put every renderer of this actor on a layer (used to make the player body mirror-only).</summary>
        public void SetLayerRecursive(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>World-space bounds of all renderers (for scaling/placement checks).</summary>
        public Bounds RendererBounds()
        {
            var rs = GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
