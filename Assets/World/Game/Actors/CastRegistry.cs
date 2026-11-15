using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Which character body plays which actor id (PRD_CHARACTERS 4.1 and 5.8 item 1). Bodies load by name from a
    /// Resources folder ("Characters/&lt;key&gt;@&lt;costume&gt;"), which exists only on machines that ran the local
    /// character sync and build (Assets/LocalBaked/Characters, git-ignored): the public repository has no prefab, so
    /// every lookup returns null and <see cref="ActorFactory"/> builds the placeholder as before, logging nothing.
    /// </summary>
    public static class CastRegistry
    {
        /// <summary>Cast keys that passed the owner's quality review; only these are used unless <see cref="EnableAllRoles"/>.</summary>
        public static readonly string[] Reviewed = { "prince", "mira", "champion" };

        /// <summary>Developer flag: use every built body (command line <c>-allCast</c>), not only the reviewed three.</summary>
        public static bool EnableAllRoles = HasArg("-allCast") || HasArg("--all-cast");

        /// <summary>Forces the placeholder everywhere (command line <c>-placeholder-actors</c>): the "before" footage.</summary>
        public static bool ForcePlaceholders = HasArg("-placeholder-actors") || HasArg("--placeholder-actors");

        /// <summary>
        /// Costume for a cast key in the current age. The costume package (WP-18 <c>CostumeSwitch</c>) replaces this
        /// function and calls <see cref="RiggedActor.SwapCostume"/> on live actors; the default is the prehistoric set.
        /// </summary>
        public static Func<string, string> CostumeFor = key => DefaultCostume;
        public const string DefaultCostume = "prehistoric";
        public const string ResourceFolder = "Characters/";

        /// <summary>The cast key an actor id maps to (null: none, the placeholder plays it).</summary>
        public static string KeyFor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            switch (actorId)
            {
                case NpcIds.Prince: return "prince";
                case NpcIds.Mira: return "mira";
                case NpcIds.RivalChief: return "champion";        // the Ash Clan's war leader = the Ash champion (Road Hero)
                case "rival_leader": return "champion";
                case NpcIds.Sawyer: return "carpenter";
                case NpcIds.Merchant: return "merchant";
                case NpcIds.Builder: return "woodcutter";
                case NpcIds.Host: return "sol";
                case NpcIds.MarketKeeper: return "hesta";
                case "ward_player": return "juno";
                case "dancer0": return "dancer_ochre";
                case "dancer1": return "dancer_ash";
                case "dancer2": return "dancer_moss";
                case "dancer3": return "dancer_plum";
                case "carrier": return "carrier";
            }
            if (actorId.StartsWith("camp_figure_"))
            {
                string[] pool = { "woodcutter", "carrier", "hesta", "juno", "carpenter" };
                int h = 17;
                foreach (char c in actorId) h = h * 31 + c;
                return pool[(h & 0x7fffffff) % pool.Length];
            }
            if (actorId.StartsWith("keeper_")) return "champion";
            return null;
        }

        /// <summary>True when the key may be used now (reviewed, or every key with the developer flag).</summary>
        public static bool IsEnabled(string key)
        {
            if (key == null || ForcePlaceholders) return false;
            return EnableAllRoles || Array.IndexOf(Reviewed, key) >= 0;
        }

        /// <summary>The body prefab for a key and costume, or null (no local art, or not built). Never logs.</summary>
        public static GameObject LoadPrefab(string key, string costume)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var go = Resources.Load<GameObject>(ResourceFolder + key + "@" + (costume ?? DefaultCostume));
            if (go == null && costume != DefaultCostume) go = Resources.Load<GameObject>(ResourceFolder + key + "@" + DefaultCostume);
            return go != null && go.GetComponent<RiggedCharacter>() != null ? go : null;
        }

        /// <summary>The prefab that would play this actor id now, or null for the placeholder.</summary>
        public static GameObject PrefabFor(string actorId)
        {
            var key = KeyFor(actorId);
            return IsEnabled(key) ? LoadPrefab(key, CostumeFor(key)) : null;
        }

        /// <summary>True if the local art for this key exists (any costume).</summary>
        public static bool HasArt(string key) => LoadPrefab(key, DefaultCostume) != null;

        private static bool HasArg(string a)
        {
            try { return Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0; }
            catch { return false; }
        }
    }
}
