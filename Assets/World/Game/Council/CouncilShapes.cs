using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Flat-shaded primitive meshes of the council: the eleven dispute props (and their miniature carvings, which are
    /// the same mesh in carved wood), the four emblems, seals, the open hand, the heavy seal, the petition roll, the
    /// lantern, the leaf and gate seals, catspaws and standing residents. Front faces -Z (towards the camera), base at
    /// y = 0, about one unit tall; callers scale them. Original shapes, nothing taken from elsewhere.
    /// </summary>
    public static class CouncilShapes
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        static Material _vcol;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Cache.Clear(); _vcol = null; }

        public static Material VertexMaterial => _vcol != null ? _vcol : (_vcol = MaterialFactory.CreateVertexColored(Color.white, "Council vertex"));

        static Mesh Cached(string key, System.Action<MeshBuilder> build)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            build(mb);
            m = mb.ToMesh("council/" + key);
            Cache[key] = m;
            return m;
        }

        public static GameObject Make(string name, Transform parent, Mesh mesh, Vector3 localPos, float scale = 1f, bool glow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = glow ? MaterialFactory.CreateUnlit(Color.white, "Council glow") : VertexMaterial;
            if (!glow) LightShapes.Apply(r, LightShapes.Read.Prop);
            return go;
        }

        // ---------------------------------------------------------------- building blocks (XY plane, thickness along z)

        const float T = 0.14f;

        static void Bar(MeshBuilder mb, Vector2 a, Vector2 b, float w, Color c, float t = T)
        {
            var d = b - a; float len = d.magnitude; if (len < 1e-4f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var mid = (a + b) * 0.5f;
            Rot(mb, new Vector3(mid.x, mid.y, 0), new Vector3(len, w, t), c, ang);
        }

        static void Rot(MeshBuilder mb, Vector3 centre, Vector3 size, Color c, float zDeg)
        {
            var q = Quaternion.Euler(0, 0, zDeg);
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => centre + q * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p010 = P(-1, 1, -1), p110 = P(1, 1, -1);
            Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p011 = P(-1, 1, 1), p111 = P(1, 1, 1);
            mb.Quad(p000, p010, p110, p100, c);                 // front (-z)
            mb.Quad(p101, p111, p011, p001, c * 0.75f);         // back
            mb.Quad(p010, p011, p111, p110, c * 1.08f);         // top
            mb.Quad(p000, p100, p101, p001, c * 0.7f);          // bottom
            mb.Quad(p001, p011, p010, p000, c * 0.88f);
            mb.Quad(p100, p110, p111, p101, c * 0.88f);
        }

        static void Poly(MeshBuilder mb, Vector2[] pts, Color c, float t = T)
        {
            // Convex polygon prism, counter-clockwise when seen from the front (-z).
            var cen = Vector2.zero; foreach (var p in pts) cen += p; cen /= pts.Length;
            float area = 0; for (int i = 0; i < pts.Length; i++) { var a0 = pts[i]; var b0 = pts[(i + 1) % pts.Length]; area += a0.x * b0.y - b0.x * a0.y; }
            if (area > 0) { pts = (Vector2[])pts.Clone(); System.Array.Reverse(pts); } // clockwise on screen = front face
            float z0 = -t * 0.5f, z1 = t * 0.5f;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Length];
                mb.Tri(new Vector3(cen.x, cen.y, z0), new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), c);
                mb.Tri(new Vector3(cen.x, cen.y, z1), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1), c * 0.75f);
                mb.Quad(new Vector3(a.x, a.y, z0), new Vector3(a.x, a.y, z1), new Vector3(b.x, b.y, z1), new Vector3(b.x, b.y, z0), c * 0.85f);
            }
        }

        static Vector2[] Ngon(Vector2 c, float rx, float ry, int n, float rot = 0f)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) { float a = rot + i * Mathf.PI * 2f / n; p[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); }
            // Wind clockwise on screen for a front face looking down +z: the Tri order above expects CCW from -z, i.e. clockwise in x/y.
            System.Array.Reverse(p);
            return p;
        }

        static void Disc(MeshBuilder mb, Vector2 c, float r, Color col, int n = 14, float t = T) => Poly(mb, Ngon(c, r, r, n), col, t);

        static void Ring(MeshBuilder mb, Vector2 c, float r0, float r1, Color col, int n = 16, float t = T)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p00 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0; var p01 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1;
                var p10 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r0; var p11 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                float z0 = -t * 0.5f, z1 = t * 0.5f;
                mb.Quad(new Vector3(p00.x, p00.y, z0), new Vector3(p10.x, p10.y, z0), new Vector3(p11.x, p11.y, z0), new Vector3(p01.x, p01.y, z0), col);
                mb.Quad(new Vector3(p01.x, p01.y, z1), new Vector3(p11.x, p11.y, z1), new Vector3(p10.x, p10.y, z1), new Vector3(p00.x, p00.y, z1), col * 0.75f);
                mb.Quad(new Vector3(p01.x, p01.y, z0), new Vector3(p11.x, p11.y, z0), new Vector3(p11.x, p11.y, z1), new Vector3(p01.x, p01.y, z1), col * 1.05f);
            }
        }

        // ---------------------------------------------------------------- palette

        static readonly Color Wood = new Color(0.55f, 0.36f, 0.20f), LightWood = new Color(0.72f, 0.52f, 0.30f), DarkWood = new Color(0.30f, 0.19f, 0.10f);
        static readonly Color Stone = new Color(0.58f, 0.58f, 0.60f), Rope = new Color(0.80f, 0.68f, 0.42f), Leaf = new Color(0.32f, 0.72f, 0.28f);
        static readonly Color Clay = new Color(0.78f, 0.40f, 0.24f), Sack = new Color(0.82f, 0.72f, 0.50f), Bark = new Color(0.50f, 0.34f, 0.20f);
        static readonly Color Carved = new Color(0.86f, 0.66f, 0.38f);

        // ---------------------------------------------------------------- the eleven props (tracks 2 to 12)

        /// <summary>The prop of a track as a large object (coloured), or its miniature carving (all carved wood). Same silhouette.</summary>
        public static Mesh Prop(int track, bool carving)
        {
            return Cached("prop" + track + (carving ? "c" : ""), mb =>
            {
                Color C(Color c) => carving ? Carved * (0.88f + 0.12f * c.grayscale) : c;
                switch (track)
                {
                    case 2: // cracked paddle: long shaft, flat blade with a notch, tilted
                        Bar(mb, new Vector2(0, 0), new Vector2(0.05f, 0.62f), 0.07f, C(Wood));
                        Poly(mb, new[] { new Vector2(0.20f, 0.62f), new Vector2(0.05f, 1.02f), new Vector2(-0.10f, 0.62f), new Vector2(-0.12f, 0.50f), new Vector2(0.25f, 0.50f) }, C(LightWood));
                        Poly(mb, new[] { new Vector2(0.10f, 0.50f), new Vector2(0.04f, 0.74f), new Vector2(0.0f, 0.50f) }, C(DarkWood), T * 1.3f);
                        break;
                    case 3: // boundary rope: a coil between two stakes
                        Ring(mb, new Vector2(0, 0.28f), 0.14f, 0.30f, C(Rope), 14);
                        Ring(mb, new Vector2(0, 0.28f), 0.34f, 0.40f, C(Rope * 0.9f), 14);
                        Poly(mb, new[] { new Vector2(-0.50f, 0), new Vector2(-0.38f, 0), new Vector2(-0.44f, 0.9f) }, C(Wood));
                        Poly(mb, new[] { new Vector2(0.38f, 0), new Vector2(0.50f, 0), new Vector2(0.44f, 0.9f) }, C(Wood));
                        break;
                    case 4: // sapling: slim stem and a leafy tuft
                        Bar(mb, new Vector2(0, 0), new Vector2(0, 0.62f), 0.07f, C(Bark));
                        Poly(mb, new[] { new Vector2(0, 0.55f), new Vector2(-0.22f, 0.78f), new Vector2(0, 1.0f) }, C(Leaf));
                        Poly(mb, new[] { new Vector2(0, 0.55f), new Vector2(0, 1.0f), new Vector2(0.22f, 0.78f) }, C(Leaf * 0.85f));
                        Poly(mb, new[] { new Vector2(0, 0.40f), new Vector2(-0.30f, 0.50f), new Vector2(-0.06f, 0.62f) }, C(Leaf * 0.9f));
                        Poly(mb, new[] { new Vector2(0, 0.40f), new Vector2(0.06f, 0.62f), new Vector2(0.30f, 0.50f) }, C(Leaf * 1.05f));
                        break;
                    case 5: // split log: upright with a V split at the top
                        Poly(mb, new[] { new Vector2(-0.16f, 0), new Vector2(-0.16f, 0.62f), new Vector2(0.16f, 0.62f), new Vector2(0.16f, 0) }, C(Wood));
                        Poly(mb, new[] { new Vector2(-0.16f, 0.60f), new Vector2(-0.22f, 1.0f), new Vector2(-0.03f, 0.66f) }, C(LightWood));
                        Poly(mb, new[] { new Vector2(0.03f, 0.66f), new Vector2(0.22f, 1.0f), new Vector2(0.16f, 0.60f) }, C(LightWood * 0.9f));
                        break;
                    case 6: // empty clay pot: round belly, narrow neck, flared rim
                        Poly(mb, Ngon(new Vector2(0, 0.34f), 0.34f, 0.32f, 14), C(Clay));
                        Poly(mb, new[] { new Vector2(-0.10f, 0.58f), new Vector2(-0.10f, 0.84f), new Vector2(0.10f, 0.84f), new Vector2(0.10f, 0.58f) }, C(Clay * 0.9f));
                        Poly(mb, new[] { new Vector2(-0.20f, 0.84f), new Vector2(-0.20f, 0.94f), new Vector2(0.20f, 0.94f), new Vector2(0.20f, 0.84f) }, C(Clay * 1.1f));
                        break;
                    case 7: // empty grain basket: wide and low, open at the top
                        Poly(mb, new[] { new Vector2(-0.50f, 0.52f), new Vector2(-0.38f, 0), new Vector2(0.38f, 0), new Vector2(0.50f, 0.52f) }, C(Rope * 0.85f));
                        Poly(mb, new[] { new Vector2(-0.44f, 0.52f), new Vector2(-0.44f, 0.60f), new Vector2(0.44f, 0.60f), new Vector2(0.44f, 0.52f) }, C(Wood));
                        Poly(mb, new[] { new Vector2(-0.36f, 0.14f), new Vector2(-0.30f, 0.46f), new Vector2(0.30f, 0.46f), new Vector2(0.36f, 0.14f) }, C(DarkWood), T * 1.25f);
                        break;
                    case 8: // cracked stone pick: T shape with a heavy head
                        Bar(mb, new Vector2(0, 0), new Vector2(0, 0.88f), 0.08f, C(Wood));
                        Poly(mb, new[] { new Vector2(-0.46f, 0.82f), new Vector2(-0.38f, 0.98f), new Vector2(0.38f, 0.98f), new Vector2(0.46f, 0.82f), new Vector2(0.20f, 0.74f), new Vector2(-0.20f, 0.74f) }, C(Stone));
                        Bar(mb, new Vector2(-0.05f, 0.98f), new Vector2(0.04f, 0.76f), 0.03f, C(DarkWood), T * 1.3f);
                        break;
                    case 9: // grain skin bag: slumped sack tied at the neck
                        Poly(mb, new[] { new Vector2(-0.34f, 0), new Vector2(-0.42f, 0.28f), new Vector2(-0.20f, 0.64f), new Vector2(0.20f, 0.64f), new Vector2(0.42f, 0.28f), new Vector2(0.34f, 0) }, C(Sack));
                        Poly(mb, new[] { new Vector2(-0.10f, 0.62f), new Vector2(-0.16f, 0.84f), new Vector2(0.02f, 0.92f), new Vector2(0.16f, 0.82f), new Vector2(0.10f, 0.62f) }, C(Sack * 0.9f));
                        Bar(mb, new Vector2(-0.14f, 0.64f), new Vector2(0.14f, 0.64f), 0.06f, C(Rope * 0.7f), T * 1.3f);
                        break;
                    case 10: // bark bucket: open vessel and a curved handle
                        Poly(mb, new[] { new Vector2(-0.30f, 0.52f), new Vector2(-0.22f, 0), new Vector2(0.22f, 0), new Vector2(0.30f, 0.52f) }, C(Bark));
                        Poly(mb, new[] { new Vector2(-0.30f, 0.52f), new Vector2(-0.30f, 0.60f), new Vector2(0.30f, 0.60f), new Vector2(0.30f, 0.52f) }, C(LightWood));
                        Ring(mb, new Vector2(0, 0.58f), 0.26f, 0.32f, C(DarkWood), 12);
                        break;
                    case 11: // log roller: a disc stood on its edge
                        Ring(mb, new Vector2(0, 0.46f), 0.34f, 0.46f, C(Wood), 16);
                        Disc(mb, new Vector2(0, 0.46f), 0.34f, C(LightWood), 16, T * 0.8f);
                        Disc(mb, new Vector2(0, 0.46f), 0.08f, C(DarkWood), 8, T * 1.3f);
                        Bar(mb, new Vector2(-0.30f, 0.46f), new Vector2(0.30f, 0.46f), 0.05f, C(DarkWood), T * 1.2f);
                        break;
                    default: // 12 tally stick: a slim rolled bar with notches and a ribbon
                        Bar(mb, new Vector2(0, 0.04f), new Vector2(0, 0.96f), 0.15f, C(LightWood));
                        Poly(mb, Ngon(new Vector2(0, 0.97f), 0.12f, 0.06f, 8), C(Wood));
                        Poly(mb, Ngon(new Vector2(0, 0.03f), 0.12f, 0.06f, 8), C(Wood));
                        Bar(mb, new Vector2(-0.14f, 0.5f), new Vector2(0.14f, 0.5f), 0.07f, C(Clay), T * 1.3f);
                        Bar(mb, new Vector2(-0.10f, 0.30f), new Vector2(0.10f, 0.30f), 0.03f, C(DarkWood), T * 1.3f);
                        Bar(mb, new Vector2(-0.10f, 0.72f), new Vector2(0.10f, 0.72f), 0.03f, C(DarkWood), T * 1.3f);
                        break;
                }
            });
        }

        // ---------------------------------------------------------------- emblems

        /// <summary>Emblem silhouette by shape name (circle acorn, triangle arrowhead, square hearthstone, diamond willow leaf), about 1 unit across, centred at the origin.</summary>
        public static Mesh Emblem(string shape, Color col)
        {
            return Cached("emb" + shape + ColorUtility.ToHtmlStringRGB(col), mb =>
            {
                switch (shape)
                {
                    case "circle": Disc(mb, Vector2.zero, 0.5f, col, 16, 0.18f); Poly(mb, Ngon(new Vector2(0, 0.30f), 0.38f, 0.18f, 10), col * 0.6f, 0.26f); break;
                    case "triangle": Poly(mb, new[] { new Vector2(-0.52f, -0.42f), new Vector2(0, 0.56f), new Vector2(0.52f, -0.42f) }, col, 0.18f); break;
                    case "square": Poly(mb, new[] { new Vector2(-0.44f, -0.44f), new Vector2(-0.44f, 0.44f), new Vector2(0.44f, 0.44f), new Vector2(0.44f, -0.44f) }, col, 0.18f); Poly(mb, new[] { new Vector2(-0.22f, -0.22f), new Vector2(-0.22f, 0.22f), new Vector2(0.22f, 0.22f), new Vector2(0.22f, -0.22f) }, col * 0.6f, 0.26f); break;
                    default: Poly(mb, new[] { new Vector2(-0.30f, 0), new Vector2(0, 0.64f), new Vector2(0.30f, 0), new Vector2(0, -0.64f) }, col, 0.18f); Bar(mb, new Vector2(0, -0.5f), new Vector2(0, 0.5f), 0.04f, col * 0.6f, 0.26f); break;
                }
            });
        }

        public static Mesh Seal(Color col) => Cached("seal" + ColorUtility.ToHtmlStringRGB(col), mb =>
        {
            Disc(mb, Vector2.zero, 0.5f, col * 0.7f, 18, 0.14f);
            Disc(mb, Vector2.zero, 0.36f, col, 18, 0.22f);
            Poly(mb, new[] { new Vector2(-0.10f, -0.2f), new Vector2(-0.10f, 0.2f), new Vector2(0.10f, 0.2f), new Vector2(0.10f, -0.2f) }, col * 0.6f, 0.3f);
        });

        public static Mesh Hook() => Cached("hook", mb =>
        {
            Bar(mb, new Vector2(0, 0), new Vector2(0, -0.5f), 0.4f, DarkWood, 0.5f);
        });

        // ---------------------------------------------------------------- hand, heavy seal, petition, lantern

        public static Mesh Hand(bool lit) => Cached("hand" + lit, mb =>
        {
            var skin = lit ? new Color(1f, 0.82f, 0.62f) : new Color(0.86f, 0.68f, 0.52f);
            Poly(mb, new[] { new Vector2(-0.22f, 0), new Vector2(-0.26f, 0.34f), new Vector2(0.26f, 0.34f), new Vector2(0.22f, 0) }, skin, 0.16f);
            float[] xs = { -0.19f, -0.065f, 0.065f, 0.19f }; float[] hs = { 0.40f, 0.52f, 0.54f, 0.44f };
            for (int i = 0; i < 4; i++) Bar(mb, new Vector2(xs[i] + (i - 1.5f) * 0.02f, 0.30f), new Vector2(xs[i] + (i - 1.5f) * 0.05f, 0.30f + hs[i]), 0.095f, skin * 1.04f, 0.14f);
            Bar(mb, new Vector2(-0.22f, 0.10f), new Vector2(-0.46f, 0.38f), 0.10f, skin * 0.96f, 0.14f);
            Poly(mb, Ngon(new Vector2(0, 0.04f), 0.20f, 0.06f, 8), skin * 0.8f, 0.17f);
        });

        public static Mesh HeavySeal(bool lit) => Cached("heavyseal" + lit, mb =>
        {
            var brass = lit ? new Color(1f, 0.82f, 0.30f) : new Color(0.78f, 0.60f, 0.22f);
            Poly(mb, new[] { new Vector2(-0.42f, 0), new Vector2(-0.42f, 0.22f), new Vector2(0.42f, 0.22f), new Vector2(0.42f, 0) }, DarkWood, 0.5f);
            Poly(mb, new[] { new Vector2(-0.40f, 0.22f), new Vector2(-0.40f, 0.30f), new Vector2(0.40f, 0.30f), new Vector2(0.40f, 0.22f) }, brass, 0.52f);
            Poly(mb, new[] { new Vector2(-0.16f, 0.30f), new Vector2(-0.12f, 0.80f), new Vector2(0.12f, 0.80f), new Vector2(0.16f, 0.30f) }, Wood, 0.34f);
            Poly(mb, Ngon(new Vector2(0, 0.90f), 0.28f, 0.14f, 10), brass, 0.40f);
        });

        public static Mesh Petition(bool lit) => Cached("petition" + lit, mb =>
        {
            var paper = lit ? new Color(1f, 0.96f, 0.80f) : new Color(0.90f, 0.84f, 0.66f);
            Bar(mb, new Vector2(-0.46f, 0.18f), new Vector2(0.46f, 0.18f), 0.34f, paper, 0.34f);
            Poly(mb, Ngon(new Vector2(-0.48f, 0.18f), 0.10f, 0.17f, 8), paper * 0.8f, 0.36f);
            Poly(mb, Ngon(new Vector2(0.48f, 0.18f), 0.10f, 0.17f, 8), paper * 0.8f, 0.36f);
            Bar(mb, new Vector2(-0.06f, 0.18f), new Vector2(0.06f, 0.18f), 0.40f, new Color(0.70f, 0.18f, 0.20f), 0.38f);
        });

        public static Mesh LanternMesh() => Cached("lantern", mb =>
        {
            Bar(mb, new Vector2(0, 0), new Vector2(0, 0.46f), 0.06f, DarkWood, 0.1f);
            Poly(mb, new[] { new Vector2(-0.22f, 0.44f), new Vector2(-0.22f, 0.50f), new Vector2(0.22f, 0.50f), new Vector2(0.22f, 0.44f) }, DarkWood, 0.34f);
            Poly(mb, new[] { new Vector2(-0.18f, 0.50f), new Vector2(-0.18f, 0.90f), new Vector2(0.18f, 0.90f), new Vector2(0.18f, 0.50f) }, new Color(1f, 0.82f, 0.45f), 0.30f);
            Poly(mb, new[] { new Vector2(-0.24f, 0.90f), new Vector2(0, 1.04f), new Vector2(0.24f, 0.90f) }, DarkWood, 0.36f);
        });

        public static Mesh LeafSeal(bool lit) => Cached("leafseal" + lit, mb =>
        {
            var g = lit ? new Color(0.55f, 0.95f, 0.40f) : new Color(0.36f, 0.70f, 0.28f);
            Poly(mb, new[] { new Vector2(-0.30f, 0), new Vector2(0, 0.52f), new Vector2(0.30f, 0), new Vector2(0, -0.52f) }, g, 0.2f);
            Bar(mb, new Vector2(0, -0.50f), new Vector2(0, 0.46f), 0.045f, g * 0.55f, 0.28f);
            Bar(mb, new Vector2(0, -0.50f), new Vector2(0.16f, -0.70f), 0.05f, g * 0.55f, 0.24f);
        });

        public static Mesh GateSeal(bool lit) => Cached("gateseal" + lit, mb =>
        {
            var g = lit ? new Color(0.95f, 0.78f, 0.55f) : new Color(0.74f, 0.58f, 0.38f);
            Poly(mb, new[] { new Vector2(-0.42f, -0.5f), new Vector2(-0.42f, 0.5f), new Vector2(-0.24f, 0.5f), new Vector2(-0.24f, -0.5f) }, g, 0.2f);
            Poly(mb, new[] { new Vector2(0.24f, -0.5f), new Vector2(0.24f, 0.5f), new Vector2(0.42f, 0.5f), new Vector2(0.42f, -0.5f) }, g, 0.2f);
            Poly(mb, new[] { new Vector2(-0.46f, 0.36f), new Vector2(-0.46f, 0.58f), new Vector2(0.46f, 0.58f), new Vector2(0.46f, 0.36f) }, g * 0.85f, 0.24f);
            Bar(mb, new Vector2(-0.24f, -0.3f), new Vector2(0.24f, -0.3f), 0.06f, g * 0.6f, 0.14f);
        });

        public static Mesh Banner(Color col) => Cached("banner" + ColorUtility.ToHtmlStringRGB(col), mb =>
        {
            Bar(mb, new Vector2(0, 0), new Vector2(0, 1.0f), 0.05f, DarkWood, 0.08f);
            Poly(mb, new[] { new Vector2(0.03f, 0.98f), new Vector2(0.03f, 0.50f), new Vector2(0.30f, 0.58f), new Vector2(0.55f, 0.50f), new Vector2(0.55f, 0.98f) }, col, 0.04f);
        });

        public static Mesh Post() => Cached("post", mb => Bar(mb, new Vector2(0, 0), new Vector2(0, 1f), 0.10f, DarkWood, 0.10f));

        // ---------------------------------------------------------------- people-ish things (volumes)

        /// <summary>A catspaw: a little cloaked figure with pointed ears, about one unit tall, in the emblem colour.</summary>
        public static Mesh Catspaw(Color col) => Cached("paw" + ColorUtility.ToHtmlStringRGB(col), mb =>
        {
            mb.Cylinder(Vector3.zero, 0.30f, 0.16f, 0.62f, 8, col);
            mb.Cylinder(new Vector3(0, 0.60f, 0), 0.19f, 0.15f, 0.20f, 8, new Color(0.94f, 0.80f, 0.64f));
            mb.Cone(new Vector3(-0.11f, 0.78f, 0), 0.07f, 0.16f, 5, col * 0.9f);
            mb.Cone(new Vector3(0.11f, 0.78f, 0), 0.07f, 0.16f, 5, col * 0.9f);
            mb.Box(new Vector3(0, 0.67f, -0.17f), new Vector3(0.12f, 0.04f, 0.04f), new Color(0.1f, 0.1f, 0.1f));
        });

        /// <summary>A resident standing in a place: body and head, facing -Z, about one unit tall.</summary>
        public static Mesh Stander(Color body, Color skin) => Cached("stand" + ColorUtility.ToHtmlStringRGB(body) + ColorUtility.ToHtmlStringRGB(skin), mb =>
        {
            mb.Cylinder(Vector3.zero, 0.28f, 0.18f, 0.66f, 7, body);
            mb.Cylinder(new Vector3(0, 0.64f, 0), 0.17f, 0.14f, 0.22f, 7, skin);
            mb.Box(new Vector3(0, 0.74f, -0.13f), new Vector3(0.08f, 0.06f, 0.07f), skin * 0.9f);
            mb.Box(new Vector3(0, 0.82f, 0.02f), new Vector3(0.30f, 0.10f, 0.30f), body * 0.55f);
        });

        public static Mesh Slab(Color c) => Cached("slab" + ColorUtility.ToHtmlStringRGB(c), mb =>
        {
            mb.Box(new Vector3(0, 0.12f, 0), new Vector3(1f, 0.24f, 0.62f), c);
            mb.Box(new Vector3(0, 0.26f, 0), new Vector3(0.94f, 0.06f, 0.56f), c * 1.1f);
        });

        public static Mesh Disc3(Color c, int sides = 18) => Cached("disc3" + ColorUtility.ToHtmlStringRGB(c) + sides, mb => mb.Cylinder(Vector3.zero, 0.5f, 0.5f, 0.06f, sides, c));
        public static Mesh BoxMesh(Color c) => Cached("box" + ColorUtility.ToHtmlStringRGB(c), mb => mb.Box(new Vector3(0, 0.5f, 0), Vector3.one, c));
    }
}
