using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Accumulates simple vertex-coloured shapes (boxes, cylinders, cones, quads, polygons) into one mesh,
    /// so a whole hex of trees or a run of edge lines is a single draw call. Flat shaded (no shared vertices).
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            var n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            _n.Add(n); _n.Add(n); _n.Add(n);
            _c.Add(col); _c.Add(col); _c.Add(col);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
        }

        /// <summary>Quad a-b-c-d in clockwise order seen from the front (Unity winding).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        /// <summary>Horizontal convex polygon facing up (points in counter-clockwise order seen from above).</summary>
        public void FanUp(Vector3 centre, IList<Vector3> ring, Color col)
        {
            for (int i = 0; i < ring.Count; i++)
                Tri(centre, ring[(i + 1) % ring.Count], ring[i], col);
        }

        /// <summary>Axis-aligned (then yaw-rotated) box. centre is the box centre.</summary>
        public void Box(Vector3 centre, Vector3 size, Color col, float yawDeg = 0f)
        {
            var q = Quaternion.Euler(0, yawDeg, 0);
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => centre + q * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p010 = P(-1, 1, -1), p110 = P(1, 1, -1);
            Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p011 = P(-1, 1, 1), p111 = P(1, 1, 1);
            Quad(p010, p011, p111, p110, col);           // top
            Quad(p000, p100, p101, p001, col * 0.8f);    // bottom
            Quad(p000, p010, p110, p100, col * 0.9f);    // -z
            Quad(p101, p111, p011, p001, col * 0.9f);    // +z
            Quad(p001, p011, p010, p000, col * 0.95f);   // -x
            Quad(p100, p110, p111, p101, col * 0.95f);   // +x
        }

        /// <summary>Vertical cylinder/frustum from base y to base y + height.</summary>
        public void Cylinder(Vector3 baseCentre, float rBottom, float rTop, float height, int sides, Color col, bool cap = true)
        {
            var top = baseCentre + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCentre + d0 * rBottom, b1 = baseCentre + d1 * rBottom;
                Vector3 t0 = top + d0 * rTop, t1 = top + d1 * rTop;
                if (rTop > 1e-4f) Quad(b0, t0, t1, b1, col);
                else Tri(b0, top, b1, col);
                if (cap && rTop > 1e-4f) Tri(top, t1, t0, col * 1.05f);
            }
        }

        /// <summary>Cone with apex up.</summary>
        public void Cone(Vector3 baseCentre, float radius, float height, int sides, Color col)
            => Cylinder(baseCentre, radius, 0f, height, sides, col, false);

        /// <summary>Rough rock: a squashed octahedron-like lump with jittered points.</summary>
        public void Rock(Vector3 baseCentre, float size, Color col, System.Func<float> rand)
        {
            int sides = 6;
            var ring = new Vector3[sides];
            float h = size * (0.5f + rand() * 0.4f);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides + rand() * 0.4f;
                float r = size * (0.7f + rand() * 0.4f);
                ring[i] = baseCentre + new Vector3(Mathf.Cos(a) * r, h * 0.45f, Mathf.Sin(a) * r);
            }
            var top = baseCentre + new Vector3((rand() - 0.5f) * size * 0.3f, h, (rand() - 0.5f) * size * 0.3f);
            for (int i = 0; i < sides; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % sides];
                Tri(top, b, a, col * (0.9f + 0.2f * rand()));
                Tri(a, b, baseCentre + (b - baseCentre) * 0.9f - Vector3.up * h * 0.45f + Vector3.up * 0.0f, col * 0.8f);
                Tri(a, baseCentre + (b - baseCentre) * 0.9f - Vector3.up * h * 0.45f, baseCentre + (a - baseCentre) * 0.9f - Vector3.up * h * 0.45f, col * 0.8f);
            }
        }

        /// <summary>Flat strip on the ground from a to b (width w, at the y of a and b).</summary>
        public void Strip(Vector3 a, Vector3 b, float w, Color col)
        {
            var d = b - a; d.y = 0;
            if (d.sqrMagnitude < 1e-8f) return;
            var side = Vector3.Cross(Vector3.up, d.normalized) * (w * 0.5f);
            Quad(a - side, b - side, b + side, a + side, col);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Creates a child GameObject with this mesh and the given material (no collider).</summary>
        public GameObject ToObject(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }

    /// <summary>Small deterministic float RNG for scattering props (not sim state).</summary>
    public sealed class PropRandom
    {
        private uint _s;
        public PropRandom(int seed) { _s = (uint)seed * 2654435761u + 12345u; if (_s == 0) _s = 1; }
        public float Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (_s & 0xFFFFFF) / (float)0x1000000;
        }
        public float Range(float a, float b) => a + (b - a) * Next();
    }
}
