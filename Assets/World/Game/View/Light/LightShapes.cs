using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Meshes and materials of package L (Docs/LIGHTING.md): soft additive glow discs, the torch and brazier models,
    /// and the night-readability material variants (self-lit and rim versions of other packages' materials).
    /// Everything is built in code from primitives; nothing is imported.
    /// </summary>
    public static class LightShapes
    {
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static Material _glow;

        /// <summary>Shared additive material of every glow disc, halo and spark (brightness per renderer by property block).</summary>
        public static Material Glow => _glow != null ? _glow : (_glow = MaterialFactory.CreateGlow(Color.white, "Light glow"));


        private static readonly float[] FallR = { 0f, 0.18f, 0.38f, 0.62f, 1f };
        private static readonly float[] FallA = { 1f, 0.62f, 0.30f, 0.10f, 0f };

        /// <summary>A disc whose vertex alpha falls from 1 at the centre to 0 at the rim (unit radius). Flat on the ground (XZ) or upright (XY).</summary>
        public static Mesh RadialDisc(bool upright)
        {
            string key = upright ? "radial/xy" : "radial/xz";
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            const int seg = 20;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            v.Add(Vector3.zero); c.Add(new Color(1, 1, 1, FallA[0]));
            for (int r = 1; r < FallR.Length; r++)
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float x = Mathf.Cos(a) * FallR[r], y = Mathf.Sin(a) * FallR[r];
                    v.Add(upright ? new Vector3(x, y, 0) : new Vector3(x, 0, y));
                    c.Add(new Color(1, 1, 1, FallA[r]));
                }
            for (int i = 0; i < seg; i++) { t.Add(0); t.Add(1 + (i + 1) % seg); t.Add(1 + i); }
            for (int r = 1; r < FallR.Length - 1; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = 1 + (r - 1) * seg + i, a1 = 1 + (r - 1) * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                    t.Add(a0); t.Add(a1); t.Add(b1); t.Add(a0); t.Add(b1); t.Add(b0);
                }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            Meshes[key] = m;
            return m;
        }

        /// <summary>A soft ring on the ground (unit radius): alpha 0, 1, 0 across its width.</summary>
        public static Mesh SoftRing(float width)
        {
            string key = "softring/" + width;
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            const int seg = 40;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            float[] rr = { 1f - width, 1f - width * 0.25f, 1f, 1f + width * 0.6f };
            float[] aa = { 0f, 0.85f, 1f, 0f };
            for (int r = 0; r < rr.Length; r++)
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(a) * rr[r], 0, Mathf.Sin(a) * rr[r]));
                    c.Add(new Color(1, 1, 1, aa[r]));
                }
            for (int r = 0; r < rr.Length - 1; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = r * seg + i, a1 = r * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                    t.Add(a0); t.Add(a1); t.Add(b1); t.Add(a0); t.Add(b1); t.Add(b0);
                }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            Meshes[key] = m;
            return m;
        }

        /// <summary>A slim vertical beam for the top "cast here" mark: a bright thin core in a faint sleeve, fading out upwards.</summary>
        public static Mesh SlimBeam(Color col)
        {
            string key = "slimbeam/" + ColorUtility.ToHtmlStringRGB(col);
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            LandShapes.FadeBeam(mb, Vector3.up * 0.1f, 0.34f, 0.10f, 26f, 10, col * 0.16f);
            LandShapes.FadeBeam(mb, Vector3.up * 0.1f, 0.10f, 0.03f, 40f, 6, col * 0.55f);
            m = mb.ToMesh(key);
            Meshes[key] = m;
            return m;
        }

        /// <summary>A torch on a post (lit vertex-colour mesh): the flame sits at <see cref="TorchFlameY"/>.</summary>
        public const float TorchFlameY = 1.72f, BrazierFlameY = 0.98f;

        public static Mesh Torch()
        {
            return LandLook.Cached("light/torch", mb =>
            {
                LandShapes.Beam(mb, Vector3.zero, Vector3.up * 1.6f, 0.055f, 5, LandLook.DarkWood);
                mb.Cylinder(new Vector3(0, 1.52f, 0), 0.07f, 0.13f, 0.2f, 6, new Color(0.22f, 0.16f, 0.11f));
                mb.Cylinder(new Vector3(0, 1.44f, 0), 0.075f, 0.075f, 0.06f, 6, LandLook.Thatch * 0.8f);
                LandShapes.Disc(mb, new Vector3(0, 0.02f, 0), 0.3f, 8, LandLook.Stone * 0.7f);
            });
        }

        public static Mesh Brazier()
        {
            return LandLook.Cached("light/brazier", mb =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var d = Quaternion.Euler(0, i * 120f + 15f, 0) * Vector3.forward;
                    LandShapes.Beam(mb, d * 0.42f, -d * 0.08f + Vector3.up * 0.86f, 0.035f, 4, LandLook.DarkWood);
                }
                mb.Cylinder(new Vector3(0, 0.74f, 0), 0.2f, 0.36f, 0.22f, 8, new Color(0.24f, 0.22f, 0.21f));
                LandShapes.Disc(mb, new Vector3(0, 0.955f, 0), 0.33f, 8, new Color(0.08f, 0.05f, 0.04f));
            });
        }

        /// <summary>An unlit flame (three nested cones) of unit size; scale it per fixture.</summary>
        public static Mesh Flame() => LandLook.Cached("light/flame", mb =>
        {
            // Nested cones, each taller and paler than the one round it: red-orange skirt, orange body, yellow tip.
            mb.Cone(Vector3.zero, 1f, 0.55f, 7, new Color(0.92f, 0.30f, 0.07f));
            mb.Cone(new Vector3(0.06f, 0.02f, 0.04f), 0.74f, 0.80f, 6, new Color(1f, 0.56f, 0.10f));
            mb.Cone(new Vector3(-0.05f, 0.04f, 0.02f), 0.44f, 1.0f, 5, new Color(1f, 0.90f, 0.42f));
        });

        /// <summary>The Ash Clan's flame: the same shape in cold greens with a pale core.</summary>
        public static Mesh AshFlame() => LandLook.Cached("light/flame-ash", mb =>
        {
            mb.Cone(Vector3.zero, 1f, 0.55f, 7, new Color(0.06f, 0.50f, 0.36f));
            mb.Cone(new Vector3(0.06f, 0.02f, 0.04f), 0.74f, 0.80f, 6, new Color(0.25f, 0.92f, 0.55f));
            mb.Cone(new Vector3(-0.05f, 0.04f, 0.02f), 0.44f, 1.0f, 5, new Color(0.80f, 1f, 0.86f));
        });

        // ---------------- night-readability variants ----------------

        /// <summary>How a renderer should read at night (Docs/LIGHTING.md, "Readability").</summary>
        public enum Read { Person, Load, Demon, Stock, Prop }

        private static readonly Dictionary<long, Material> Variants = new Dictionary<long, Material>();
        private static readonly HashSet<int> VariantIds = new HashSet<int>();
        private static readonly Dictionary<long, Material> Dimmed = new Dictionary<long, Material>();
        private static readonly int SelfLitId = Shader.PropertyToID("_SelfLit"), RimId = Shader.PropertyToID("_Rim"), NightDimId = Shader.PropertyToID("_NightDim");

        public static float SelfLit(Read r)
        {
            switch (r)
            {
                case Read.Person: return 0.22f;
                case Read.Load: return 0.40f;
                case Read.Demon: return 0.30f;
                case Read.Stock: return 0.15f;
                default: return 0.08f;
            }
        }

        public static Color Rim(Read r)
        {
            switch (r)
            {
                case Read.Person: return new Color(0.42f, 0.50f, 0.72f);
                case Read.Load: return new Color(0.30f, 0.34f, 0.42f);
                case Read.Demon: return new Color(1.0f, 0.10f, 0.22f);
                default: return Color.black;
            }
        }

        public static bool IsVariant(Material m) => m != null && VariantIds.Contains(m.GetInstanceID());

        /// <summary>
        /// The night version of a lit World material: same look by day; at night it keeps a little of its own colour
        /// (so it is never pure black) and, for people and demons, a rim against the dark ground. Cached per source material.
        /// </summary>
        public static Material LitVariant(Material src, Read read)
        {
            if (src == null || IsVariant(src) || !src.HasProperty(SelfLitId)) return src;
            long key = ((long)src.GetInstanceID() << 4) | (long)(int)read;
            if (Variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + " (night " + read + ")" };
            m.SetFloat(SelfLitId, SelfLit(read));
            m.SetColor(RimId, Rim(read));
            Variants[key] = m;
            VariantIds.Add(m.GetInstanceID());
            return m;
        }

        /// <summary>An unlit World material that dims at night by <paramref name="amount"/> (number tokens, flags, smoke).</summary>
        public static Material DimVariant(Material src, float amount)
        {
            if (src == null || IsVariant(src) || !src.HasProperty(NightDimId)) return src;
            long key = ((long)src.GetInstanceID() << 8) | (long)Mathf.RoundToInt(amount * 100f);
            if (Dimmed.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + " (night dim)" };
            m.SetFloat(NightDimId, amount);
            Dimmed[key] = m;
            VariantIds.Add(m.GetInstanceID());
            return m;
        }

        /// <summary>Swaps one renderer's shared material for its night variant. Returns true if it changed.</summary>
        public static bool Apply(Renderer r, Read read)
        {
            if (r == null) return false;
            var src = r.sharedMaterial;
            var v = LitVariant(src, read);
            if (v == src) return false;
            r.sharedMaterial = v;
            return true;
        }

        public static bool ApplyDim(Renderer r, float amount)
        {
            if (r == null) return false;
            var src = r.sharedMaterial;
            var v = DimVariant(src, amount);
            if (v == src) return false;
            r.sharedMaterial = v;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Meshes.Clear(); Variants.Clear(); VariantIds.Clear(); Dimmed.Clear(); _glow = null;
        }
    }
}
