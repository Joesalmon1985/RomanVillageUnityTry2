using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace World.Characters.Editor
{
    /// <summary>
    /// Import settings for the character pipeline's FBX files (PRD_CHARACTERS 5.6, WP-6), applied on every import so
    /// nothing depends on hand clicking:
    /// character models (Assets/LocalBaked/Characters/Models, local only) and animation-only files
    /// (Assets/Characters/Animation, tracked). The rig type and Humanoid avatar are set by <see cref="CharacterBuilder"/>
    /// (it needs the imported hierarchy) and persist in the .meta file; this class never changes them.
    /// </summary>
    public sealed class CharacterImport : AssetPostprocessor
    {
        public override uint GetVersion() => 4;

        /// <summary>Prefix given to the part meshes on import (the mesh "Head" would clash with the bone "Head").</summary>
        public const string MeshPrefix = "Mesh.";

        /// <summary>
        /// Renames the part mesh objects (Body, Cloth, Head, Legs, Prop) to "Mesh.&lt;part&gt;" before the avatar is made:
        /// Humanoid needs every mapped bone name to be unique in the hierarchy, and the mesh "Head" shares the bone's name.
        /// </summary>
        private void OnPostprocessMeshHierarchy(GameObject root)
        {
            if (!CharacterPaths.IsCharacterModel(assetPath)) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<SkinnedMeshRenderer>() == null && t.GetComponent<MeshFilter>() == null) continue;
                if (t.name.StartsWith(MeshPrefix) || t.name.StartsWith("Ov_")) continue;
                t.name = MeshPrefix + t.name;
            }
        }

        private void OnPreprocessModel()
        {
            bool model = CharacterPaths.IsCharacterModel(assetPath), anim = CharacterPaths.IsAnimFile(assetPath);
            if (!model && !anim) return;
            var imp = (ModelImporter)assetImporter;
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.bakeAxisConversion = false;
            // Flat shading as authored: every FBX corner normal equals its face normal (WP-1 inspection).
            imp.importNormals = ModelImporterNormals.Import;
            imp.importBlendShapeNormals = ModelImporterNormals.None;
            imp.importTangents = ModelImporterTangents.None;
            imp.importBlendShapes = model;
            imp.isReadable = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.optimizeMeshVertices = false;
            imp.optimizeMeshPolygons = false;
            imp.weldVertices = false;
            imp.importVisibility = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importConstraints = false;
            // Sockets, eye and jaw bones and the cloth chains must stay reachable as transforms.
            imp.optimizeGameObjects = false;
            imp.skinWeights = ModelImporterSkinWeights.Standard;
            imp.importAnimation = anim;
            imp.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
            imp.animationRotationError = 0.1f;
            imp.animationPositionError = 0.1f;
            imp.animationScaleError = 0.1f;
            imp.resampleCurves = true;
            if (anim)
            {
                // Skeleton-and-clip files: no materials at all.
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
            }
            else
            {
                // Materials come from the project (role slot colours, World/Lit); CharacterBuilder adds the remaps.
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!CharacterPaths.IsAnimFile(assetPath)) return;
            var imp = (ModelImporter)assetImporter;
            var defaults = imp.defaultClipAnimations;
            var records = ClipRecords();
            var list = new List<ModelImporterClipAnimation>();
            foreach (var d in defaults)
            {
                string name = StripName(d.takeName);
                if (name.StartsWith("__preview__")) continue;
                d.name = name;
                ApplyRecord(d, records.TryGetValue(name, out var rec) ? rec : null);
                list.Add(d);
            }
            imp.clipAnimations = list.ToArray();
        }

        /// <summary>Blender action names come through as "Armature|Walk"; Unity clip name = text after the last '|'.</summary>
        public static string StripName(string take)
        {
            int i = take.LastIndexOf('|');
            return i >= 0 ? take.Substring(i + 1) : take;
        }

        /// <summary>Loop, root, additive and event settings from a manifest clip record (null: in-place, not looping).</summary>
        public static void ApplyRecord(ModelImporterClipAnimation c, JObject rec)
        {
            bool loop = rec != null && (bool?)rec["loop"] == true;
            bool additive = rec != null && (bool?)rec["additive"] == true;
            c.loopTime = loop;
            c.loopPose = false; // authored loops already match first and last pose (verify.py)
            c.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            // All clips are authored in place; the game moves the root (PRD 5.5). Every root channel is baked into the
            // pose with its original values, so the hips keep their sway and height and Unity extracts no root motion.
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
            c.rotationOffset = 0; c.heightOffset = 0; c.cycleOffset = 0;
            c.mirror = false;
            c.hasAdditiveReferencePose = additive;
            c.additiveReferencePoseFrame = 0;
            var events = new List<AnimationEvent>();
            if (rec != null && rec["events"] is JArray ev)
            {
                float frames = Mathf.Max(1f, (float?)rec["frames"] ?? 1f);
                foreach (var e in ev)
                {
                    float f = (float?)e["frame"] ?? 0f;
                    events.Add(new AnimationEvent
                    {
                        functionName = "OnActorEvent",
                        stringParameter = (string)e["name"],
                        time = Mathf.Clamp01(f / frames),
                        messageOptions = SendMessageOptions.DontRequireReceiver,
                    });
                }
            }
            c.events = events.ToArray();
        }

        private static Dictionary<string, JObject> _records;
        private static long _recordsStamp;

        /// <summary>Every clip record by name: base clips (anim_manifest.json) and variants (anim_variants_*.json).</summary>
        public static Dictionary<string, JObject> ClipRecords()
        {
            long stamp = 0;
            var files = new List<string>();
            if (File.Exists(CharacterPaths.AnimManifest)) files.Add(CharacterPaths.AnimManifest);
            if (Directory.Exists(CharacterPaths.Animation))
                files.AddRange(Directory.GetFiles(CharacterPaths.Animation, "anim_variants_*.json"));
            foreach (var f in files) stamp ^= File.GetLastWriteTimeUtc(f).Ticks ^ f.GetHashCode();
            if (_records != null && stamp == _recordsStamp) return _records;
            var d = new Dictionary<string, JObject>();
            foreach (var f in files)
            {
                var root = JObject.Parse(File.ReadAllText(f));
                if (root["clips"] is JArray clips)
                    foreach (JObject c in clips) d[(string)c["name"]] = c;
            }
            _records = d; _recordsStamp = stamp;
            return d;
        }
    }
}
