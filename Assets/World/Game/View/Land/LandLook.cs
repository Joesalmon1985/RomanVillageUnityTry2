using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>Colours, shared materials and cached meshes of the land views (package V).</summary>
    public static class LandLook
    {
        // ---- peoples ----

        /// <summary>Main colour of a people: yours sky blue, the Ash Clan blood red.</summary>
        public static Color People(int faction) => faction == PlayerIds.Rival ? new Color(0.72f, 0.14f, 0.10f) : new Color(0.16f, 0.50f, 0.80f);
        /// <summary>Second colour of a people (totem crest, trims): yours sun yellow, the Ash Clan ash grey.</summary>
        public static Color PeopleTrim(int faction) => faction == PlayerIds.Rival ? new Color(0.30f, 0.29f, 0.28f) : new Color(0.98f, 0.80f, 0.22f);
        public static string PeopleName(int faction) => faction == PlayerIds.Rival ? "the Ash Clan" : "your people";

        // ---- materials ----

        public static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f);
        public static readonly Color DarkWood = new Color(0.30f, 0.20f, 0.12f);
        public static readonly Color Thatch = new Color(0.78f, 0.66f, 0.38f);
        public static readonly Color Hide = new Color(0.72f, 0.58f, 0.42f);
        public static readonly Color Stone = new Color(0.60f, 0.58f, 0.55f);
        public static readonly Color Earth = new Color(0.42f, 0.32f, 0.22f);
        public static readonly Color ClayRed = new Color(0.66f, 0.38f, 0.24f);
        public static readonly Color Skin = new Color(0.80f, 0.60f, 0.45f);
        public static readonly Color Hair = new Color(0.20f, 0.13f, 0.09f);
        public static readonly Color Leaf = new Color(0.20f, 0.42f, 0.18f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.12f);

        private static Material _lit, _glow, _additive;
        /// <summary>Shared lit vertex-colour material (all land meshes).</summary>
        public static Material Lit => _lit != null ? _lit : (_lit = MaterialFactory.CreateVertexColored(Color.white, "Land lit"));
        /// <summary>Shared unlit vertex-colour material (fires, tokens, marks that must read at any light).</summary>
        public static Material Unlit => _glow != null ? _glow : (_glow = MaterialFactory.CreateUnlit(Color.white, "Land unlit"));
        /// <summary>Shared additive glow (beams, flares).</summary>
        public static Material Additive => _additive != null ? _additive : (_additive = MaterialFactory.CreateGlow(Color.white, "Land glow"));

        // ---- wares ----

        private static readonly Dictionary<Ware, Color> WareColours = new Dictionary<Ware, Color>();
        public static Color WareColor(Ware w)
        {
            if (WareColours.TryGetValue(w, out var c)) return c;
            if (!ColorUtility.TryParseHtmlString(EconomyApi.WareColour(w), out c)) c = Color.white;
            WareColours[w] = c;
            return c;
        }

        // ---- shared meshes ----

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        /// <summary>A mesh built once per key and shared by every view that asks for it (do not modify).</summary>
        public static Mesh Cached(string key, System.Action<MeshBuilder> build)
        {
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            build(mb);
            m = mb.ToMesh(key);
            Meshes[key] = m;
            return m;
        }

        /// <summary>A child object rendering a shared mesh.</summary>
        public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat != null ? mat : Lit;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Replaces the mesh of a part made by <see cref="Part"/> (or creates the part).</summary>
        public static GameObject SetMesh(ref GameObject part, Transform parent, string name, MeshBuilder mb, Material mat = null, bool shadows = true)
        {
            if (part == null) part = Part(parent, name, null, mat, shadows);
            var mf = part.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null && !Meshes.ContainsValue(mf.sharedMesh)) Object.Destroy(mf.sharedMesh);
            mf.sharedMesh = mb.VertexCount > 0 ? mb.ToMesh(name) : null;
            return part;
        }

        /// <summary>A trigger box collider for picking (does not block anything).</summary>
        public static BoxCollider PickBox(GameObject go, Vector3 centre, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = centre;
            bc.size = size;
            return bc;
        }

        public static Vector3 Ground(double x, double z, float y = 0f) => new Vector3((float)x, BoardSpace.GroundY + y, (float)z);

        public static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
