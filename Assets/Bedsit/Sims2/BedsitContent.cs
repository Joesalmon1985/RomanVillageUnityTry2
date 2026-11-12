using System;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// Where the world and characters come from.
    ///  * Live : built at run time from the user's own Sims 2 install through OpenTS2 (desktop only).
    ///  * Baked: ordinary Unity assets (meshes, textures, pre-sampled animation clips) produced locally from the live content by
    ///           the editor tool ContentBaker and stored under Assets/LocalBaked/Resources/Baked (git-ignored; derived from EA assets).
    ///           This is what runs on a phone, or on a PC without Sims 2 installed.
    /// Mode Auto: baked content is used when it exists and no Sims 2 config.json can be found, otherwise live.
    /// Override with ForceMode or the BEDSIT_CONTENT environment variable (live | baked).
    /// </summary>
    public static class BedsitContent
    {
        public enum ContentMode { Auto, Live, Baked }

        public static ContentMode ForceMode = ContentMode.Auto;
        public static bool UsingBaked { get; private set; }

        public const string WorldPath = "Baked/World";
        public static string ActorPath(uint simId) => "Baked/Actor_" + simId;

        public static bool BakedAvailable => Resources.Load<GameObject>(WorldPath) != null;

        static ContentMode Effective()
        {
            if (ForceMode != ContentMode.Auto) return ForceMode;
            var env = Environment.GetEnvironmentVariable("BEDSIT_CONTENT");
            if (string.Equals(env, "live", StringComparison.OrdinalIgnoreCase)) return ContentMode.Live;
            if (string.Equals(env, "baked", StringComparison.OrdinalIgnoreCase)) return ContentMode.Baked;
            if (Application.isMobilePlatform) return ContentMode.Baked;
            return Sims2Runtime.FindConfig() == null && BakedAvailable ? ContentMode.Baked : ContentMode.Live;
        }

        public static BedsitWorld CreateWorld()
        {
            UsingBaked = Effective() == ContentMode.Baked;
            if (!UsingBaked) return BedsitWorldBuilder.Build();

            var prefab = Resources.Load<GameObject>(WorldPath);
            if (prefab == null) { Debug.LogError("[BedsitContent] baked world not found (run Bedsit/Bake in the Editor first)"); return null; }
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "BedsitWorld";
            var world = go.GetComponent<BedsitWorld>();
            BedsitWorldBuilder.ApplyEnvironment();
            if (world.Door != null) world.Door.RebindBaked();
            if (world.Report == null) world.Report = new System.Collections.Generic.List<string>();
            world.Report.Add("baked content (see Docs): original Sims 2 geometry/textures converted to Unity assets");
            return world;
        }

        public static SimCharacterActor CreateActor(string actorId, uint simId)
        {
            if (!UsingBaked) return SimCharacterActor.CreateFromNeighborhood(actorId, "N001", simId);
            var prefab = Resources.Load<GameObject>(ActorPath(simId));
            if (prefab == null) { Debug.LogError("[BedsitContent] baked actor missing for Sim " + simId); return null; }
            return SimCharacterActor.InstantiateBaked(prefab, actorId);
        }
    }
}
