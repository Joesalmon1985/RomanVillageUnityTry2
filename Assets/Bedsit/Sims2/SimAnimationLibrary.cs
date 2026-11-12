using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTS2.Common;
using OpenTS2.Content;
using OpenTS2.Content.DBPF.Scenegraph;
using OpenTS2.Files.Formats.DBPF;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// Maps logical animation keys to ORIGINAL Sims 2 animation resources (ANIM, type 0xFB00791E, group 0x1C0532FA)
    /// held in the base game's Sims3D packages. Nothing here is generic Unity animation: each entry is a resource
    /// authored by Maxis for the "auskel" Sims skeleton, decoded by OpenTS2 and converted into a legacy AnimationClip
    /// that drives the character's real bones (plus IK/blend shapes).
    /// Candidates are tried in order, so the library degrades gracefully across game editions.
    /// </summary>
    public static class SimAnimationLibrary
    {
        public sealed class Def
        {
            public string Key;
            public string[] Candidates;
            public bool Loop;
            public string Note;
        }

        public static readonly Dictionary<string, Def> Definitions = new Dictionary<string, Def>
        {
            [AnimationKeys.Pose] = new Def { Key = AnimationKeys.Pose, Loop = true, Note = "neutral standing pose (base layer every other animation is applied over)",
                Candidates = new[] { "a-pose-neutral-stand_anim" } },
            [AnimationKeys.Idle] = new Def { Key = AnimationKeys.Idle, Loop = true, Note = "standing idle, weight centred, breathing",
                Candidates = new[] { "a-idle-neutral-armsSide-wtC-breatheLoop_anim", "a-pose-neutral-stand_anim" } },
            [AnimationKeys.Wave] = new Def { Key = AnimationKeys.Wave, Note = "friendly wave, full body",
                Candidates = new[] { "a-react-wave-fullBody_anim", "a-idle-nice-wave_anim", "a-gestures-outgoing-socialLeadIn-wave_anim", "a-soc-wave-40Frames_anim" } },
            [AnimationKeys.Greet] = new Def { Key = AnimationKeys.Greet, Note = "greeting nod",
                Candidates = new[] { "a-soc-greet-nod_anim", "a-idle-nice-wave_anim" } },
            [AnimationKeys.Clap] = new Def { Key = AnimationKeys.Clap, Note = "standing applause (one pass of the clap loop)",
                Candidates = new[] { "a-clapSmall-loop-standing_anim", "a-clapMed-loop-standing_anim", "a-clapSmall-start-standing_anim" } },
            [AnimationKeys.Cheer] = new Def { Key = AnimationKeys.Cheer, Note = "big applause (one pass of the clap loop)",
                Candidates = new[] { "a-clapBig-loop-standing_anim", "a-clapMed-loop-standing_anim", "a-clapBig-start-standing_anim" } },
            [AnimationKeys.Surprise] = new Def { Key = AnimationKeys.Surprise, Note = "surprised reaction",
                Candidates = new[] { "a-hairChange-head-surprise_anim", "a-facePose-surprise-start_anim" } },
            [AnimationKeys.Stretch] = new Def { Key = AnimationKeys.Stretch, Note = "shoulder stretch",
                Candidates = new[] { "a-idle-neutral-armsSide-wtC-stretchShoulders_anim" } },
        };

        public sealed class Resolved
        {
            public Def Def;
            public string ResourceName;
            public ScenegraphAnimationAsset Asset;
            public string Package;
            public int Channels;
            public float DurationSeconds;
        }

        static readonly Dictionary<string, Resolved> Cache = new Dictionary<string, Resolved>();

        public static bool Has(string key) => Definitions.ContainsKey(key);

        public static Resolved Resolve(string key)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            if (!Definitions.TryGetValue(key, out var def)) return null;
            foreach (var name in def.Candidates)
            {
                var asset = Sims2Runtime.GetAnimation(name);
                if (asset == null) continue;
                var res = new Resolved
                {
                    Def = def,
                    ResourceName = name,
                    Asset = asset,
                    Package = Sims2Runtime.PackageOf(new ResourceKey(name, GroupIDs.Scenegraph, TypeIDs.SCENEGRAPH_ANIM)),
                };
                var target = asset.AnimResource.AnimTargets.FirstOrDefault(t => t.TagName.ToLower() == "auskel");
                if (target != null)
                {
                    res.Channels = target.Channels.Length;
                    uint maxTicks = 0;
                    foreach (var ch in target.Channels) maxTicks = Math.Max(maxTicks, ch.DurationTicks);
                    res.DurationSeconds = (float)(maxTicks * OpenTS2.Files.Formats.DBPF.Scenegraph.Block.AnimResourceConstBlock.FramesPerTick / 24.0);
                }
                Cache[key] = res;
                return res;
            }
            Debug.LogWarning($"[SimAnimationLibrary] none of the candidate animations for '{key}' exist: {string.Join(", ", def.Candidates)}");
            return null;
        }

        /// <summary>Human-readable provenance table (resolved resource, package, channel count) for the README.</summary>
        public static string DescribeAll()
        {
            var sb = new StringBuilder();
            foreach (var key in Definitions.Keys)
            {
                var r = Resolve(key);
                sb.AppendLine(r == null
                    ? $"{key}: UNRESOLVED"
                    : $"{key}: {r.ResourceName} | package {r.Package} | {r.Channels} auskel channels | ~{r.DurationSeconds:0.00}s | {r.Def.Note}");
            }
            return sb.ToString();
        }
    }
}
