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

        public const string UnlitPath = "World/WorldUnlit";
        public const string GlowPath = "World/WorldGlow";
        private static Material _unlit, _glow;
        private static readonly Dictionary<Color32, Material> UnlitCache = new Dictionary<Color32, Material>();
        private static Material UnlitBase => _unlit != null ? _unlit : (_unlit = LoadBase(UnlitPath, "World/Unlit"));
        private static Material GlowBase => _glow != null ? _glow : (_glow = LoadBase(GlowPath, "World/Glow"));

        /// <summary>New unlit (self-lit, fogged) material: vertex colour x colour. Use for glowing fixtures.</summary>
        public static Material CreateUnlit(Color color, string name = null)
        {
            var m = new Material(UnlitBase) { color = color };
            m.name = name ?? "World unlit";
            return m;
        }

        /// <summary>Shared cached unlit material for a colour. Do not modify the returned instance.</summary>
        public static Material SharedUnlit(Color color)
        {
            Color32 key = color;
            if (!UnlitCache.TryGetValue(key, out var m) || m == null)
            {
                m = CreateUnlit(color);
                UnlitCache[key] = m;
            }
            return m;
        }

        public const string TextPath = "World/WorldText";
        private static Material _text;

        /// <summary>Depth-tested material for TextMesh using the given font's atlas (shared per font).</summary>
        public static Material TextMaterial(Font font)
        {
            if (_text != null && _text.mainTexture == font.material.mainTexture) return _text;
            var baseMat = LoadBase(TextPath, "World/Text");
            _text = new Material(baseMat) { name = "World text", mainTexture = font.material.mainTexture };
            return _text;
        }

        /// <summary>New additive glow material (light spill, beams). Colour alpha scales brightness.</summary>
        public static Material CreateGlow(Color color, string name = null)
        {
            var m = new Material(GlowBase) { color = color };
            m.name = name ?? "World glow";
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
