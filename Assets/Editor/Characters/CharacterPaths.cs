using System.IO;

namespace World.Characters.Editor
{
    /// <summary>Where the character pipeline's files live in the project (PRD_CHARACTERS 5.1, WP-6).</summary>
    public static class CharacterPaths
    {
        /// <summary>Local, git-ignored: meshes, materials, prefabs (rights not confirmed; Docs/PROVENANCE_CHARACTERS.md).</summary>
        public const string Local = "Assets/LocalBaked/Characters";
        public const string Models = Local + "/Models";
        public const string Materials = Local + "/Materials";
        public const string Meshes = Local + "/Meshes";
        public const string Prefabs = Local + "/Resources/Characters";
        public const string Manifest = Local + "/cast_manifest.json";
        /// <summary>Tracked: skeleton-and-clip FBX files, their manifests, avatar masks.</summary>
        public const string Animation = "Assets/Characters/Animation";
        public const string AnimManifest = Animation + "/anim_manifest.json";
        public const string Masks = Animation + "/Masks";
        public const string UpperBodyMask = Masks + "/UpperBody.mask";

        public static bool IsCharacterModel(string assetPath) => assetPath.Replace('\\', '/').StartsWith(Models + "/") && assetPath.EndsWith(".fbx");
        public static bool IsAnimFile(string assetPath) => assetPath.Replace('\\', '/').StartsWith(Animation + "/") && assetPath.EndsWith(".fbx");
        public static string ModelPath(string key, string costume) => Models + "/" + key + "@" + costume + ".fbx";
        public static string PrefabPath(string key, string costume) => Prefabs + "/" + key + "@" + costume + ".prefab";
        public static bool LocalArtPresent => File.Exists(Manifest);
    }
}
