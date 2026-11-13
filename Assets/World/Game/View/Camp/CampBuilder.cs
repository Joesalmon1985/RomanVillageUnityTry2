using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Builds the home camp's dressing from <see cref="CampLayout.Dressing"/> (REFACTOR_SPEC 2.E, 8.3): hide tents, a
    /// lean-to, drying racks with hides and meat strips, carved totems, a wood pile and three villagers going about their
    /// day. Original primitive shapes only (one vertex-coloured mesh per prop, no colliders that block). Each prop is a child
    /// of "Dressing" named after its kind, carries an <see cref="IInspectable"/> on a trigger collider for the pointer, and
    /// stays inside its <see cref="CampProp.Radius"/>. V draws the store pile and hearth at the corner itself. Owner: D.
    /// </summary>
    public static class CampBuilder
    {
        public static readonly Color Hide = new Color(0.66f, 0.52f, 0.36f), HideDark = new Color(0.47f, 0.35f, 0.24f),
            Wood = new Color(0.40f, 0.29f, 0.18f), WoodLight = new Color(0.58f, 0.45f, 0.30f), Ochre = new Color(0.78f, 0.38f, 0.14f),
            Clay = new Color(0.90f, 0.87f, 0.78f), Charcoal = new Color(0.14f, 0.11f, 0.09f), Meat = new Color(0.55f, 0.16f, 0.12f),
            Fur = new Color(0.36f, 0.30f, 0.26f), Stone = new Color(0.52f, 0.50f, 0.47f);

        private static readonly Dictionary<string, (string title, string line)> Info = new Dictionary<string, (string, string)>
        {
            { "tent", ("Hide tent", "Poles and stitched hides: a family sleeps here.") },
            { "leanto", ("Lean-to", "A hide roof on two forked posts, furs underneath.") },
            { "rack", ("Drying rack", "Hides stretch and meat dries in the smoke and the wind.") },
            { "totem", ("Totem", "Carved and painted: the people's guardians.") },
            { "woodpile", ("Wood pile", "Firewood for the hearth and the dance fire.") },
        };

        public static Transform Build(Transform parent, CampLayout layout)
        {
            var root = new GameObject("Dressing").transform;
            root.SetParent(parent, false);
            var mat = MaterialFactory.CreateVertexColored(Color.white, "Camp props");
            var counts = new Dictionary<string, int>();
            int i = 0;
            foreach (var p in layout.Dressing)
            {
                counts.TryGetValue(p.Kind, out int n); counts[p.Kind] = ++n;
                if (p.Kind == "figure") { Figure(root, p, i++); continue; }
                var mb = new MeshBuilder();
                var rnd = new PropRandom(1000 + i++ * 37);
                switch (p.Kind)
                {
                    case "tent": Tent(mb, rnd); break;
                    case "leanto": LeanTo(mb, rnd); break;
                    case "rack": Rack(mb, rnd); break;
                    case "totem": Totem(mb, rnd, n); break;
                    case "woodpile": WoodPile(mb, rnd); break;
                }
                var go = mb.ToObject(Title(p.Kind) + " " + n, root, mat);
                go.transform.SetPositionAndRotation(p.Position, Quaternion.Euler(0, p.Yaw, 0));
                var bounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true; col.center = bounds.center; col.size = bounds.size;
                var info = go.AddComponent<CampPropInfo>();
                info.Kind = p.Kind;
                if (Info.TryGetValue(p.Kind, out var t)) { info.TitleText = t.title; info.Line = t.line; }
            }
            root.gameObject.AddComponent<CampLife>();
            return root;
        }

        private static string Title(string kind) => Info.TryGetValue(kind, out var t) ? t.title : kind;

        // ------------------------------------------------------------------ shapes (local: facing +Z, base at y 0)

        /// <summary>A square beam from a to b (any direction).</summary>
        public static void Beam(MeshBuilder mb, Vector3 a, Vector3 b, float w, Color col)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var f = d.normalized;
            var s = Vector3.Cross(f, Mathf.Abs(f.y) > 0.95f ? Vector3.right : Vector3.up).normalized * (w * 0.5f);
            var u = Vector3.Cross(s, f).normalized * (w * 0.5f);
            Vector3 a0 = a - s - u, a1 = a + s - u, a2 = a + s + u, a3 = a - s + u;
            Vector3 b0 = b - s - u, b1 = b + s - u, b2 = b + s + u, b3 = b - s + u;
            mb.Quad(a0, b0, b1, a1, col * 0.85f);
            mb.Quad(a1, b1, b2, a2, col);
            mb.Quad(a2, b2, b3, a3, col * 0.95f);
            mb.Quad(a3, b3, b0, a0, col * 0.9f);
            mb.Quad(b0, b3, b2, b1, col);
            mb.Quad(a0, a1, a2, a3, col);
        }

        /// <summary>Both faces of a flat quad (so hides read from either side).</summary>
        public static void Sheet(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            mb.Quad(a, b, c, d, col);
            mb.Quad(a, d, c, b, col * 0.8f);
        }

        private static void Tent(MeshBuilder mb, PropRandom rnd)
        {
            const float r = 2.0f, h = 3.4f; const int sides = 9;
            var apex = new Vector3(0, h, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + 0.5f) * Mathf.PI * 2f / sides, a1 = (i + 1.5f) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Sin(a0) * r, 0, Mathf.Cos(a0) * r);
                var p1 = new Vector3(Mathf.Sin(a1) * r, 0, Mathf.Cos(a1) * r);
                var c = Color.Lerp(Hide, HideDark, rnd.Next() * 0.6f);
                mb.Tri(p0, apex, p1, c);
                mb.Tri(p1, apex, p0, c * 0.7f);
                // Painted band of ochre and clay round the middle.
                float y0 = 0.95f, y1 = 1.25f;
                Vector3 Q(Vector3 p, float y) => p * (1f - y / h) * 1.01f + Vector3.up * y;
                mb.Quad(Q(p0, y0), Q(p0, y1), Q(p1, y1), Q(p1, y0), i % 2 == 0 ? Ochre : Clay);
            }
            // Door flap on the front (+Z), a dark opening.
            float dA = 14f * Mathf.Deg2Rad;
            var d0 = new Vector3(-Mathf.Sin(dA) * r * 1.02f, 0.02f, Mathf.Cos(dA) * r * 1.02f);
            var d1 = new Vector3(Mathf.Sin(dA) * r * 1.02f, 0.02f, Mathf.Cos(dA) * r * 1.02f);
            var dt = new Vector3(0, 1.55f, r * (1f - 1.55f / h) * 1.03f);
            mb.Tri(d0, dt, d1, Charcoal);
            // Poles crossing above the smoke hole.
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 15f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Sin(a) * r * 0.97f, 0, Mathf.Cos(a) * r * 0.97f);
                var dir = (apex - foot).normalized;
                Beam(mb, foot, apex + dir * (0.55f + 0.2f * rnd.Next()), 0.07f, Wood);
            }
        }

        private static void LeanTo(MeshBuilder mb, PropRandom rnd)
        {
            // Open side faces +Z (the hearth). Two forked posts at the front, roof sloping back to the ground.
            float hw = 1.7f, front = 0.9f, back = -1.6f, top = 1.85f;
            Beam(mb, new Vector3(-hw, 0, front), new Vector3(-hw, top + 0.15f, front), 0.12f, Wood);
            Beam(mb, new Vector3(hw, 0, front), new Vector3(hw, top + 0.15f, front), 0.12f, Wood);
            Beam(mb, new Vector3(-hw - 0.3f, top, front), new Vector3(hw + 0.3f, top, front), 0.11f, Wood);
            for (int i = 0; i < 4; i++)
            {
                float x0 = -hw + i * (2f * hw / 4f), x1 = x0 + 2f * hw / 4f;
                var c = Color.Lerp(Hide, HideDark, rnd.Next() * 0.7f);
                Sheet(mb, new Vector3(x0, 0.02f, back), new Vector3(x0, top, front), new Vector3(x1, top, front), new Vector3(x1, 0.02f, back), c);
            }
            for (int i = 0; i < 5; i++) Beam(mb, new Vector3(-hw + i * hw / 2f, 0, back - 0.1f), new Vector3(-hw + i * hw / 2f, top + 0.05f, front + 0.05f), 0.05f, Wood);
            mb.Box(new Vector3(0, 0.1f, -0.3f), new Vector3(2.6f, 0.2f, 1.3f), Fur);
            mb.Box(new Vector3(0.7f, 0.18f, -0.5f), new Vector3(0.8f, 0.16f, 0.6f), HideDark);
        }

        private static void Rack(MeshBuilder mb, PropRandom rnd)
        {
            // Long axis along local X; two A-frames and a top rail at 1.9 m.
            float hx = 1.5f, top = 1.9f;
            foreach (float x in new[] { -hx, hx })
            {
                Beam(mb, new Vector3(x, 0, -0.5f), new Vector3(x, top + 0.12f, 0), 0.08f, Wood);
                Beam(mb, new Vector3(x, 0, 0.5f), new Vector3(x, top + 0.12f, 0), 0.08f, Wood);
            }
            Beam(mb, new Vector3(-hx - 0.2f, top, 0), new Vector3(hx + 0.2f, top, 0), 0.07f, WoodLight);
            Beam(mb, new Vector3(-hx, 0.9f, 0), new Vector3(hx, 0.9f, 0), 0.05f, Wood);
            // Two stretched hides and a row of drying meat strips.
            for (int i = 0; i < 2; i++)
            {
                float x = -0.85f + i * 1.15f, w = 0.45f + 0.1f * rnd.Next();
                var c = Color.Lerp(Hide, Clay, 0.25f + 0.3f * rnd.Next());
                Sheet(mb, new Vector3(x - w, top - 1.15f, 0.02f), new Vector3(x - w, top - 0.03f, 0.02f), new Vector3(x + w, top - 0.03f, 0.02f), new Vector3(x + w, top - 1.15f, 0.02f), c);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = 0.75f + i * 0.11f - 0.2f + 0.5f;
                if (x > hx - 0.1f) break;
                mb.Box(new Vector3(x, top - 0.35f, 0), new Vector3(0.05f, 0.6f + 0.15f * rnd.Next(), 0.03f), Meat);
            }
        }

        private static void Totem(MeshBuilder mb, PropRandom rnd, int n)
        {
            // Stacked carved drums, a face, outspread wings and a beak; painted bands.
            Color[] paint = { Ochre, Clay, Charcoal, new Color(0.35f, 0.45f, 0.30f) };
            float y = 0;
            float[] heights = { 0.75f, 0.6f, 0.7f, 0.55f };
            for (int i = 0; i < heights.Length; i++)
            {
                float r = 0.32f - i * 0.02f;
                mb.Cylinder(new Vector3(0, y, 0), r, r * 0.95f, heights[i], 8, Color.Lerp(WoodLight, Wood, (i + n) % 2 == 0 ? 0.2f : 0.6f));
                mb.Cylinder(new Vector3(0, y + heights[i] - 0.1f, 0), r + 0.02f, r + 0.02f, 0.08f, 8, paint[(i + n) % paint.Length], false);
                if (i == 1 || i == 3)
                {
                    // Eyes and a mouth on the front.
                    mb.Box(new Vector3(-0.12f, y + heights[i] * 0.62f, r), new Vector3(0.12f, 0.08f, 0.06f), Clay);
                    mb.Box(new Vector3(0.12f, y + heights[i] * 0.62f, r), new Vector3(0.12f, 0.08f, 0.06f), Clay);
                    mb.Box(new Vector3(0, y + heights[i] * 0.3f, r), new Vector3(0.22f, 0.06f, 0.06f), Charcoal);
                }
                y += heights[i];
            }
            // Wings and beak on top.
            mb.Box(new Vector3(0, y - 0.25f, 0), new Vector3(1.5f, 0.18f, 0.08f), Ochre);
            mb.Box(new Vector3(-0.62f, y - 0.12f, 0), new Vector3(0.3f, 0.12f, 0.085f), Clay);
            mb.Box(new Vector3(0.62f, y - 0.12f, 0), new Vector3(0.3f, 0.12f, 0.085f), Clay);
            mb.Cone(new Vector3(0, y, 0), 0.26f, 0.35f, 6, Charcoal);
            Beam(mb, new Vector3(0, y - 0.45f, 0.25f), new Vector3(0, y - 0.55f, 0.55f), 0.1f, Ochre);
        }

        private static void WoodPile(MeshBuilder mb, PropRandom rnd)
        {
            // Logs stacked three rows high along local X, a chopping stump in front.
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    float z = (i - (3 - row) * 0.5f) * 0.24f;
                    float y = 0.12f + row * 0.2f;
                    float len = 1.0f + 0.15f * rnd.Next();
                    Beam(mb, new Vector3(-len, y, z - 0.35f), new Vector3(len, y, z - 0.35f), 0.2f, Color.Lerp(Wood, WoodLight, rnd.Next() * 0.5f));
                }
            mb.Cylinder(new Vector3(0.3f, 0, 0.75f), 0.28f, 0.26f, 0.45f, 7, WoodLight);
            Beam(mb, new Vector3(0.3f, 0.45f, 0.75f), new Vector3(0.65f, 0.75f, 0.85f), 0.05f, Wood);
            mb.Box(new Vector3(0.65f, 0.78f, 0.85f), new Vector3(0.16f, 0.1f, 0.06f), Stone);
        }

        private static void Figure(Transform root, CampProp p, int i)
        {
            Color[] tunics = { new Color(0.55f, 0.40f, 0.26f), new Color(0.48f, 0.36f, 0.28f), new Color(0.62f, 0.47f, 0.30f) };
            var a = ActorFactory.Create("camp_figure_" + i, tunics[i % tunics.Length], p.Position, p.Yaw, root);
            a.Root.name = "Villager " + (root.childCount);
        }
    }

    /// <summary>The pointer's description of a camp prop (REFACTOR_SPEC 2.D, IInspectable).</summary>
    public sealed class CampPropInfo : MonoBehaviour, IInspectable
    {
        public string Kind, TitleText, Line;
        public string Title => TitleText ?? Kind;
        public IEnumerable<string> Lines { get { if (!string.IsNullOrEmpty(Line)) yield return Line; } }
    }

    /// <summary>Small ambient life for the camp's figures: now and then one stretches, waves or claps.</summary>
    public sealed class CampLife : MonoBehaviour
    {
        private Bedsit.ICharacterActor[] _actors;
        private float _next;
        private int _turn;
        private static readonly string[] Moves = { ActorKeys.Stretch, ActorKeys.Clap, ActorKeys.Greet, ActorKeys.Wave };

        private void Start()
        {
            _actors = GetComponentsInChildren<PlaceholderActor>();
            _next = Time.time + 2f;
        }

        private void Update()
        {
            if (_actors == null || _actors.Length == 0 || Time.time < _next) return;
            _next = Time.time + 3.5f + (_turn % 3);
            var a = _actors[_turn % _actors.Length];
            if (a != null && !a.IsPlayingOneShot) a.PlayOneShot(Moves[_turn % Moves.Length]);
            _turn++;
        }
    }
}
