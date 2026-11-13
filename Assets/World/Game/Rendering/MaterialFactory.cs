using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Creates materials from the World shaders. The shaders reach player builds because the base
    /// materials live in Resources (Assets/World/Game/Resources/World/*.mat). Never use the default
    /// primitive material or Shader.Find on a shader that is not referenced: it renders magenta in builds.
    /// </summary>
    public static class MaterialFactory
    {
        public const string LitPath = "World/WorldLit";
        public const string VertexColorPath = "World/WorldLitVertexColor";

        private static Material _lit, _vertex;
        private static readonly Dictionary<Color32, Material> Cache = new Dictionary<Color32, Material>();

        private static Material LitBase => _lit != null ? _lit : (_lit = LoadBase(LitPath, "World/Lit"));
        private static Material VertexBase => _vertex != null ? _vertex : (_vertex = LoadBase(VertexColorPath, "World/LitVertexColor"));

        private static Material LoadBase(string resourcePath, string shaderName)
        {
            var m = Resources.Load<Material>(resourcePath);
            if (m != null) return m;
            Debug.LogError("MaterialFactory: missing Resources material " + resourcePath);
            var s = Shader.Find(shaderName);
            return new Material(s != null ? s : Shader.Find("Standard"));
        }

        /// <summary>New opaque lit material with a flat colour (not shared).</summary>
        public static Material Create(Color color, string name = null)
        {
            var m = new Material(LitBase) { color = color };
            m.name = name ?? "World " + ColorUtility.ToHtmlStringRGB(color);
            return m;
        }

        /// <summary>Shared cached material for a colour. Do not modify the returned instance.</summary>
        public static Material Shared(Color color)
        {
            Color32 key = color;
            if (!Cache.TryGetValue(key, out var m) || m == null)
            {
                m = Create(color);
                Cache[key] = m;
            }
            return m;
        }

        /// <summary>New lit material that multiplies mesh vertex colours by a tint.</summary>
        public static Material CreateVertexColored(Color tint, string name = null)
        {
            var m = new Material(VertexBase) { color = tint };
            m.name = name ?? "World vertex colour";
            return m;
        }

        /// <summary>Applies a shared colour material to every renderer under a GameObject.</summary>
        public static void Paint(GameObject go, Color color)
        {
            var mat = Shared(color);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }
    }
}
