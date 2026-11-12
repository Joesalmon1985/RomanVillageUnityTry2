using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bedsit.Rooms
{
    // =============================================================================================================
    // Data definitions for the configuration-driven room pipeline.
    // A room is ONE json file (Resources/Rooms/<id>.json) that references props by id (see PropCatalog). Nothing in
    // the game code is specific to the first room: shell size, openings (doors/windows), furniture, decor, clutter,
    // cleanable tasks, lighting and the exterior views are all data. Coordinates are metres, +X east, +Z north, +Y up.
    // =============================================================================================================

    /// <summary>One placed prop. <see cref="parent"/> (a placement tag) makes pos/euler local to that placement, so desk-top
    /// items follow the desk. <see cref="prop"/> is a PropCatalog id, e.g. "furniture_desk".</summary>
    [Serializable]
    public class PlacementDef
    {
        public string prop;
        public string tag;                       // optional name other placements can use as parent / bin target
        public string parent;                    // optional: tag of the placement this one sits on
        public Vector3 pos;
        public Vector3 euler;                    // degrees; yaw is euler.y. A prop's front faces +Z at yaw 0 (towards north)
        public float scale = 1f;
        public bool castShadows = true;
        public string variant;                   // free-form (e.g. material tweak), reserved for per-room dressing
    }

    /// <summary>A wall opening: a door or a window, with what can be seen through it.</summary>
    [Serializable]
    public class OpeningDef
    {
        public string id;
        public string kind = "window";           // window | door
        public string wall = "E";                // N | E | S | W  (the wall it is cut into)
        public float along;                      // world coordinate along the wall (z for E/W walls, x for N/S walls)
        public float sill = 1.0f;
        public float width = 1.04f;
        public float height = 1.62f;
        public string frameProp;                 // prop id of the frame, optional (Sims door model is used by the legacy door)
        public string view;                      // exterior view id seen through it ("shop", "club", "garden"...)
    }

    [Serializable]
    public class ShellDef
    {
        public string wallMaterial = "wall_plaster";
        public string floorMaterial = "floor_parquet";
        public string ceilingMaterial = "ceiling_white";
        public float skirtingHeight = 0.12f;
        public float skirtingThickness = 0.018f;
        public string skirtingMaterial = "paint_white_gloss";
        public bool cornice = true;
        public string rugProp;                   // optional floor rug
        public Vector3 rugPos;
        public float rugYaw;
    }

    [Serializable]
    public class LightDef
    {
        public string name;
        public string type = "point";            // point | spot | directional
        public Vector3 pos;
        public Vector3 euler;
        public Color color = Color.white;
        public float intensity = 1f;
        public float range = 6f;
        public float spotAngle = 60f;
        public string shadows = "none";          // none | soft | hard
        public string role = "room";             // room | sun | venue | shop | device  (how Act One / dressing treats it)
        public float pulseHz;                    // 0 = steady; > 0 pulses (club lights)
        public float pulseDepth = 0.4f;
    }

    [Serializable]
    public class ExteriorDef
    {
        public string id;
        public string prop;
        public Vector3 pos;
        public Vector3 euler;
        public float scale = 1f;
        public string view;                      // which opening view this belongs to
    }

    /// <summary>A tidy-up task: a messy arrangement of one prop that becomes a tidy arrangement when the player acts on it.</summary>
    [Serializable]
    public class CleanableDef
    {
        public string id;
        public string label;
        public string action = "swap";           // swap | make | fold | stack | bin | slide | zip | coil
        public string view = "N";                // N | E | S | W : the view that sees it best (compass pips)
        public PlacementDef messy;
        public PlacementDef tidy;                // swap/fold/make/stack/slide: arrangement shown afterwards (optional for bin)
        public List<PlacementDef> messyExtra = new List<PlacementDef>();   // further parts of the messy arrangement (e.g. pillows)
        public List<PlacementDef> tidyExtra = new List<PlacementDef>();    // further parts of the tidy arrangement (shown one after another)
        public string target;                    // bin: tag of the bin placement the item flies into
        public float radius = 0.25f;
        public float height = 0.2f;
        public string sound;                     // rustle | thunk | clink | paper | bin | zip (synthesised)
        public int weight = 1;
    }

    [Serializable]
    public class GroundDef
    {
        public string material;
        public Vector3 min, max;
        public float uvPerMetre = 0.3f;
    }

    [Serializable]
    public class EnvironmentDef
    {
        public Color sky = new Color(0.62f, 0.78f, 0.95f);
        public Color ambientSky = new Color(0.62f, 0.68f, 0.78f), ambientEquator = new Color(0.48f, 0.46f, 0.44f), ambientGround = new Color(0.30f, 0.26f, 0.22f);
        public bool fog;
        public Color fogColor = new Color(0.5f, 0.5f, 0.6f);
        public float fogDensity = 0.01f;
        public string skybox;                    // material name under Resources/Art/Materials, optional
    }

    [Serializable]
    public class RoomDef
    {
        public EnvironmentDef environment = new EnvironmentDef();
        public List<GroundDef> ground = new List<GroundDef>();
        public float standingPitch = 12f;       // resting view tilt (degrees, POSITIVE = looking down; negative looks up)
        public float wakeYaw = 270f;             // direction the player faces while lying in bed (degrees)
        public string id = "room01";
        public string title = "Bedsit";
        public string occupant;                  // character profile this room belongs to
        public float halfWidth = 2.1f, halfDepth = 2.34f, height = 2.8f;
        public ShellDef shell = new ShellDef();
        public List<OpeningDef> openings = new List<OpeningDef>();
        public List<PlacementDef> furniture = new List<PlacementDef>();
        public List<PlacementDef> decor = new List<PlacementDef>();
        public List<CleanableDef> cleanables = new List<CleanableDef>();
        public List<LightDef> lights = new List<LightDef>();
        public List<ExteriorDef> exteriors = new List<ExteriorDef>();
        public List<string> hideLegacy = new List<string>();     // legacy (Sims 2) object names to hide
        public Vector3 bedPillowEye = new Vector3(1.4f, 0.86f, 1.8f);
        public Vector3 mirrorCentre = new Vector3(0f, 1.15f, -2.34f);
    }

    public static class RoomLibrary
    {
        public const string ResourceFolder = "Rooms/";

        public static RoomDef Load(string id)
        {
            var ta = Resources.Load<TextAsset>(ResourceFolder + id);
            if (ta == null) return null;
            try { return JsonUtility.FromJson<RoomDef>(ta.text); }
            catch (Exception e) { Debug.LogError("[RoomLibrary] bad room json " + id + ": " + e.Message); return null; }
        }

        /// <summary>The room the game uses. -room=ID or BEDSIT_ROOM selects another; default room01.</summary>
        public static string SelectedId
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs()) if (a.StartsWith("-room=")) return a.Substring(6);
                var e = Environment.GetEnvironmentVariable("BEDSIT_ROOM");
                return string.IsNullOrEmpty(e) ? "room01" : e;
            }
        }

        public static bool LegacyRequested
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs()) if (a == "-legacyroom") return true;
                return Environment.GetEnvironmentVariable("BEDSIT_LEGACY_ROOM") == "1";
            }
        }
    }

    /// <summary>Runtime lookup of prop prefabs by id. Prefabs are produced from the Blender FBX files by the editor tool
    /// (Bedsit/Art/Build Props) into Resources/Art/Props.</summary>
    public static class PropCatalog
    {
        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        public const string Folder = "Art/Props/";

        public static GameObject Prefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_cache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>(Folder + id);
            _cache[id] = p;
            return p;
        }

        public static GameObject Instantiate(string id, Transform parent)
        {
            var p = Prefab(id);
            if (p == null) { Debug.LogWarning("[PropCatalog] missing prop '" + id + "'"); return null; }
            var go = UnityEngine.Object.Instantiate(p, parent, false);
            go.name = id;
            return go;
        }

        public static void ClearCache() { _cache.Clear(); }
    }
}
