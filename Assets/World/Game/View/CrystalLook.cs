using UnityEngine;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// How an element crystal looks wherever it is drawn (pedestal, altar, Mira's hands, the Ash runners, a tower's
    /// crown): a rough, irregular cluster of uneven points growing from a lump of matrix rock, in the element's own
    /// palette, never a single clean gem. The same seed gives the same cluster every time. Crystals I8 (integration).
    /// </summary>
    public static class CrystalLook
    {
        /// <summary>Body (deep), tip (bright) and glow colours of an element's crystal.</summary>
        public struct Palette { public Color Body, Tip, Glow, Rock; }

        public static Palette For(Element e)
        {
            switch (e)
            {
                case Element.Earth: // the Deepstone: moss in the roots, amber at the points, a soft warm glow
                    return new Palette { Body = new Color(0.30f, 0.38f, 0.16f), Tip = new Color(0.92f, 0.62f, 0.24f), Glow = new Color(0.95f, 0.68f, 0.32f), Rock = new Color(0.27f, 0.22f, 0.17f) };
                case Element.Fire:
                    return new Palette { Body = new Color(0.45f, 0.10f, 0.06f), Tip = new Color(1.00f, 0.55f, 0.20f), Glow = new Color(1.00f, 0.45f, 0.18f), Rock = new Color(0.20f, 0.17f, 0.16f) };
                case Element.Water:
                    return new Palette { Body = new Color(0.08f, 0.22f, 0.36f), Tip = new Color(0.45f, 0.80f, 0.85f), Glow = new Color(0.40f, 0.70f, 0.95f), Rock = new Color(0.20f, 0.22f, 0.24f) };
                default: // Air
                    return new Palette { Body = new Color(0.50f, 0.50f, 0.62f), Tip = new Color(0.92f, 0.93f, 1.00f), Glow = new Color(0.80f, 0.85f, 1.00f), Rock = new Color(0.30f, 0.30f, 0.32f) };
            }
        }

        /// <summary>The glow colour of the crystal of a campaign crystal id (for lights and halos).</summary>
        public static Color Glow(CampaignDefinition d, string crystalId)
        {
            var cd = d?.Crystal(crystalId);
            return For(cd != null ? cd.Element : Element.Earth).Glow;
        }

        /// <summary>
        /// Adds a cluster of <paramref name="size"/> metres (height of the tallest point) to <paramref name="mb"/>, its
        /// base at <paramref name="baseCentre"/>.
        /// </summary>
        public static void Cluster(MeshBuilder mb, Vector3 baseCentre, float size, Element e, int seed = 7)
        {
            var p = For(e);
            var rng = new System.Random(seed * 7919 + (int)e * 131);
            float R() => (float)rng.NextDouble();
            // Matrix rock: a squat, lumpy base.
            var ring = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + R() * 0.3f;
                float r = size * (0.30f + R() * 0.12f);
                ring[i] = baseCentre + new Vector3(Mathf.Cos(a) * r, size * (0.02f + R() * 0.06f), Mathf.Sin(a) * r);
            }
            var top = baseCentre + Vector3.up * size * 0.14f;
            var bottom = baseCentre - Vector3.up * size * 0.06f;
            for (int i = 0; i < 7; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % 7];
                float sh = 0.8f + R() * 0.35f;
                mb.Tri(top, b, a, p.Rock * sh);
                mb.Tri(bottom, a, b, p.Rock * (sh * 0.7f));
            }
            // Points: one tall leaning one, then shorter ones leaning outwards at uneven angles.
            int n = 6 + rng.Next(3);
            for (int k = 0; k < n; k++)
            {
                float ang = k == 0 ? R() * 6.28f : k * (Mathf.PI * 2f / (n - 1)) + R() * 0.7f;
                float off = k == 0 ? size * 0.04f : size * (0.10f + R() * 0.14f);
                float h = k == 0 ? size : size * (0.35f + R() * 0.45f);
                float rad = h * (0.13f + R() * 0.07f);
                float lean = k == 0 ? 8f + R() * 10f : 18f + R() * 30f;
                var foot = baseCentre + new Vector3(Mathf.Cos(ang) * off, size * 0.06f, Mathf.Sin(ang) * off);
                var axis = Quaternion.AngleAxis(lean, new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang))) * Vector3.up;
                Point(mb, foot, axis, h, rad, p, rng);
            }
        }

        private static void Point(MeshBuilder mb, Vector3 foot, Vector3 axis, float h, float rad, Palette p, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            int sides = 5 + rng.Next(2);
            var side = Vector3.Cross(axis, Vector3.forward); if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize(); var side2 = Vector3.Cross(axis, side);
            var low = new Vector3[sides]; var high = new Vector3[sides];
            float shoulder = h * (0.62f + R() * 0.15f);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides + R() * 0.35f;
                float r = rad * (0.75f + R() * 0.45f);
                var o = (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * r;
                low[i] = foot + o * 0.85f;
                high[i] = foot + axis * shoulder * (0.9f + R() * 0.2f) + o;
            }
            var tip = foot + axis * h + side * rad * (R() - 0.5f) * 0.4f;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                float sh = 0.75f + R() * 0.4f;
                var mid = Color.Lerp(p.Body, p.Tip, 0.35f + R() * 0.2f);
                mb.Tri(low[i], high[j], high[i], Color.Lerp(p.Body, mid, 0.5f) * sh);
                mb.Tri(low[i], low[j], high[j], p.Body * sh);
                mb.Tri(high[i], high[j], tip, Color.Lerp(mid, p.Tip, 0.5f + R() * 0.4f) * (0.85f + R() * 0.3f));
            }
        }

        /// <summary>A cluster as its own object (unlit vertex colours, no shadows), its base at the parent's origin.</summary>
        public static GameObject Make(Transform parent, string name, Element e, float size, int seed = 7)
        {
            var mb = new MeshBuilder();
            Cluster(mb, Vector3.zero, size, e, seed);
            var go = mb.ToObject(name, parent, LandLook.Unlit);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
