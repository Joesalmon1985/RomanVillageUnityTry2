using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Generic inspectable for a building or prop. Later agents replace <see cref="LinesProvider"/> to show
    /// stock and stall reasons read from the sim.
    /// </summary>
    public sealed class BuildingInfo : MonoBehaviour, IInspectable
    {
        public string Kind;
        public string DisplayName;
        public int Vertex = -1;
        public Func<IEnumerable<string>> LinesProvider;
        public List<string> StaticLines = new List<string>();

        public string Title => DisplayName;
        public IEnumerable<string> Lines => LinesProvider != null ? LinesProvider() : StaticLines;
    }

    /// <summary>The town bell: ring it with E. Later milestones hook <see cref="Rung"/> to end the round.</summary>
    public sealed class TownBell : MonoBehaviour, IInteractable, IInspectable
    {
        public static event Action Rung;
        public Transform BellPivot;
        private AudioSource _audio;
        private float _swing;

        public string Prompt => "Ring the town bell";
        public string Title => "Town bell";
        public IEnumerable<string> Lines { get { yield return "Rings out across the village."; yield return "Will end the round once rounds exist."; } }

        public void Interact()
        {
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.clip = SynthClips.Bell();
                _audio.spatialBlend = 0.6f;
                _audio.minDistance = 6f; _audio.maxDistance = 120f;
            }
            _audio.Play();
            _swing = 1f;
            Rung?.Invoke();
        }

        private void Update()
        {
            if (BellPivot == null) return;
            _swing = Mathf.MoveTowards(_swing, 0f, Time.deltaTime * 0.5f);
            BellPivot.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 7f) * 22f * _swing, 0, 0);
        }
    }

    /// <summary>Builds the village square, storehouse, market stall and town bell.</summary>
    public static class VillageBuilder
    {
        public static readonly Color Stone = new Color(0.60f, 0.56f, 0.50f);
        public static readonly Color Wood = new Color(0.45f, 0.31f, 0.19f);
        public static readonly Color Plaster = new Color(0.86f, 0.80f, 0.68f);
        public static readonly Color RoofRed = new Color(0.62f, 0.28f, 0.20f);

        public static BuildingInfo Storehouse { get; private set; }
        public static BuildingInfo Market { get; private set; }
        public static TownBell Bell { get; private set; }

        public static GameObject Build(Transform parent, World.Sim.Board board, VillageLayout L)
        {
            var root = new GameObject("Village").transform;
            root.SetParent(parent, false);
            var stat = new GameObject("Static").transform;
            stat.SetParent(root, false);

            BuildSquare(stat, L);
            Storehouse = BuildStorehouse(root, stat, L, board.StorehouseVertex);
            Market = BuildMarket(root, stat, L, board.MarketVertex);
            Bell = BuildBell(root, L);
            BuildDressing(stat, L);
            StaticBatchingUtility.Combine(stat.gameObject);
            return root.gameObject;
        }

        private static void BuildSquare(Transform stat, VillageLayout L)
        {
            var mb = new MeshBuilder();
            var rnd = new PropRandom(77);
            int rings = 6, seg = 40;
            float R = VillageLayout.SquareRadius;
            for (int i = 0; i < rings; i++)
            {
                float r0 = R * i / rings, r1 = R * (i + 1) / rings;
                int s = seg;
                for (int j = 0; j < s; j++)
                {
                    float a0 = j * Mathf.PI * 2f / s, a1 = (j + 1) * Mathf.PI * 2f / s;
                    Vector3 P(float r, float a) => L.SquareCentre + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r);
                    var c = Stone * rnd.Range(0.92f, 1.04f); c.a = 1;
                    if (i == 0) mb.Tri(L.SquareCentre + Vector3.up * 0.05f, P(r1, a1), P(r1, a0), c);
                    else mb.Quad(P(r0, a0), P(r0, a1), P(r1, a1), P(r1, a0), c);
                }
            }
            // Curb ring.
            for (int j = 0; j < seg; j++)
            {
                float a = (j + 0.5f) * 360f / seg;
                var dir = Quaternion.Euler(0, -a + 90f, 0) * Vector3.forward;
                mb.Box(L.SquareCentre + dir * (R + 0.25f) + Vector3.up * 0.08f, new Vector3(R * 2f * Mathf.PI / seg + 0.1f, 0.16f, 0.5f), Stone * 0.8f, BoardSpace.YawOf(dir));
            }
            // Path from the square to the warehouse door.
            var a1v = L.SquareCentre + L.Out * (R - 0.5f); var b1v = L.WarehouseDoor + L.Out * 0.2f;
            mb.Strip(a1v + Vector3.up * 0.045f, b1v + Vector3.up * 0.045f, 4.5f, Stone * 0.85f);
            var mat = MaterialFactory.CreateVertexColored(Color.white, "Square");
            mb.ToObject("Square", stat, mat);
        }

        private static Transform Frame(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        /// <summary>Gable roof over a w x d footprint at wall height h (local to frame), ridge along X.</summary>
        private static void GableRoof(Transform frame, float w, float d, float h, float rise, Color roof, Color gable)
        {
            var mb = new MeshBuilder();
            float ov = 0.5f;
            Vector3 f0 = new Vector3(-w / 2 - ov, h, d / 2 + ov), f1 = new Vector3(w / 2 + ov, h, d / 2 + ov);
            Vector3 b0 = new Vector3(-w / 2 - ov, h, -d / 2 - ov), b1 = new Vector3(w / 2 + ov, h, -d / 2 - ov);
            Vector3 r0 = new Vector3(-w / 2 - ov, h + rise, 0), r1 = new Vector3(w / 2 + ov, h + rise, 0);
            mb.Quad(f1, r1, r0, f0, roof);
            mb.Quad(b0, r0, r1, b1, roof * 0.9f);
            mb.Quad(f0, r0, r1, f1, roof * 0.5f);
            mb.Quad(b1, r1, r0, b0, roof * 0.5f);
            mb.Tri(new Vector3(-w / 2, h, d / 2), new Vector3(-w / 2, h + rise - 0.3f, 0), new Vector3(-w / 2, h, -d / 2), gable);
            mb.Tri(new Vector3(w / 2, h, -d / 2), new Vector3(w / 2, h + rise - 0.3f, 0), new Vector3(w / 2, h, d / 2), gable);
            var go = mb.ToObject("Roof", frame, MaterialFactory.CreateVertexColored(Color.white, "Roof"));
        }

        private static BuildingInfo BuildStorehouse(Transform root, Transform stat, VillageLayout L, int vertex)
        {
            // Local frame: +Z faces the square (door side).
            var f = Frame(stat, "StorehouseShell", L.Storehouse, L.StorehouseYaw);
            float w = 11f, d = 7f, h = 4.2f, th = 0.4f;
            Signs.Box(f, "Floor", new Vector3(0, 0.06f, 0), new Vector3(w, 0.12f, d), new Color(0.5f, 0.4f, 0.3f), false);
            Signs.Box(f, "FrontL", new Vector3(-(w / 2 + 1.3f) / 2, h / 2, d / 2 - th / 2), new Vector3(w / 2 - 1.3f, h, th), Plaster);
            Signs.Box(f, "FrontR", new Vector3((w / 2 + 1.3f) / 2, h / 2, d / 2 - th / 2), new Vector3(w / 2 - 1.3f, h, th), Plaster);
            Signs.Box(f, "Lintel", new Vector3(0, (h + 3f) / 2, d / 2 - th / 2), new Vector3(2.6f, h - 3f, th), Plaster);
            Signs.Box(f, "Back", new Vector3(0, h / 2, -d / 2 + th / 2), new Vector3(w, h, th), Plaster);
            Signs.Box(f, "Left", new Vector3(-w / 2 + th / 2, h / 2, 0), new Vector3(th, h, d), Plaster);
            Signs.Box(f, "Right", new Vector3(w / 2 - th / 2, h / 2, 0), new Vector3(th, h, d), Plaster);
            foreach (float x in new[] { -w / 2, w / 2 }) foreach (float z in new[] { -d / 2, d / 2 })
                Signs.Box(f, "Beam", new Vector3(x, h / 2, z), new Vector3(0.45f, h + 0.1f, 0.45f), Wood, false);
            Signs.Box(f, "DoorFrame", new Vector3(0, 3.1f, d / 2 + 0.05f), new Vector3(2.9f, 0.25f, 0.2f), Wood, false);
            Signs.Box(f, "DoorLeaf", new Vector3(-1.75f, 1.5f, d / 2 + 0.35f), new Vector3(0.12f, 3f, 1.2f), Wood * 0.8f, false, 20f);
            GableRoof(f, w, d, h, 2.4f, RoofRed, Plaster);
            Signs.Board(f, "STOREHOUSE", new Vector3(0, 3.75f, d / 2 + 0.12f), new Vector2(3.6f, 0.62f), Wood * 0.7f, new Color(1f, 0.95f, 0.8f));
            // Interior goods.
            var rnd = new PropRandom(5);
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(rnd.Range(-4.5f, 4.5f), 0, rnd.Range(-2.8f, -0.5f));
                Signs.Box(f, "Crate", p + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.9f), Wood * rnd.Range(0.8f, 1.1f), true, rnd.Range(0, 40f));
            }
            for (int i = 0; i < 4; i++) Signs.Box(f, "Planks", new Vector3(-4.2f + i * 0.05f, 0.2f + i * 0.2f, 1.6f), new Vector3(1.0f, 0.18f, 3.2f), new Color(0.72f, 0.56f, 0.36f), false);
            Signs.Box(f, "Stone", new Vector3(4f, 0.4f, 1.6f), new Vector3(1.4f, 0.8f, 1.4f), new Color(0.6f, 0.6f, 0.58f));

            var info = new GameObject("Storehouse").AddComponent<BuildingInfo>();
            info.transform.SetParent(root, false);
            info.transform.position = L.Storehouse;
            info.Kind = "storehouse"; info.DisplayName = "Storehouse"; info.Vertex = vertex;
            info.StaticLines.Add("The village store. Wares are delivered here by carriers.");
            info.StaticLines.Add("Stock appears here once the economy is running.");
            AddPickVolume(info.gameObject, f, new Vector3(0, h / 2, 0), new Vector3(w, h + 2.4f, d));
            return info;
        }

        private static BuildingInfo BuildMarket(Transform root, Transform stat, VillageLayout L, int vertex)
        {
            var f = Frame(stat, "MarketShell", L.Market, L.MarketYaw);
            // Counter faces +Z (the square); keeper stands behind at -Z.
            Signs.Box(f, "Counter", new Vector3(0, 0.55f, 0.6f), new Vector3(4.2f, 1.1f, 0.9f), Wood);
            Signs.Box(f, "CounterTop", new Vector3(0, 1.13f, 0.65f), new Vector3(4.4f, 0.08f, 1.1f), Wood * 1.2f, false);
            foreach (float x in new[] { -2.1f, 2.1f }) foreach (float z in new[] { -1.6f, 1.0f })
                Signs.Box(f, "Post", new Vector3(x, 1.4f, z), new Vector3(0.16f, 2.8f, 0.16f), Wood * 0.85f);
            for (int i = 0; i < 7; i++)
            {
                var stripe = Signs.Box(f, "Awning", new Vector3(-2.1f + 4.2f * (i + 0.5f) / 7f, 2.95f, -0.3f), new Vector3(4.2f / 7f + 0.01f, 0.08f, 3.2f), i % 2 == 0 ? new Color(0.85f, 0.25f, 0.2f) : new Color(0.95f, 0.92f, 0.85f), false);
                stripe.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            }
            var goods = new[] { new Color(0.9f, 0.6f, 0.15f), new Color(0.75f, 0.15f, 0.12f), new Color(0.45f, 0.65f, 0.2f), new Color(0.85f, 0.8f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                Signs.Box(f, "Basket", new Vector3(-1.5f + i, 1.25f, 0.65f), new Vector3(0.7f, 0.2f, 0.6f), Wood * 0.9f, false);
                Signs.Box(f, "Goods", new Vector3(-1.5f + i, 1.4f, 0.65f), new Vector3(0.55f, 0.15f, 0.45f), goods[i], false);
            }
            Signs.Board(f, "MARKET", new Vector3(0, 3.35f, 1.05f), new Vector2(2.4f, 0.5f), Wood * 0.7f, new Color(1f, 0.95f, 0.8f));
            for (int i = 0; i < 3; i++) Signs.Box(f, "Crate", new Vector3(2.9f, 0.35f + i * 0.0f, -0.8f + i * 0.8f), new Vector3(0.7f, 0.7f, 0.7f), Wood);

            var info = new GameObject("Market").AddComponent<BuildingInfo>();
            info.transform.SetParent(root, false);
            info.transform.position = L.Market;
            info.Kind = "market"; info.DisplayName = "Market stall"; info.Vertex = vertex;
            info.StaticLines.Add("Trade wares for coin with the market keeper.");
            info.StaticLines.Add("Prices appear here once trade is running.");
            AddPickVolume(info.gameObject, f, new Vector3(0, 1.5f, -0.3f), new Vector3(4.4f, 3f, 3.2f));
            return info;
        }

        /// <summary>Trigger volume that makes a building pickable as a whole (raycasts hit triggers).</summary>
        private static void AddPickVolume(GameObject infoGo, Transform frame, Vector3 localCentre, Vector3 size)
        {
            infoGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            var bc = infoGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = localCentre;
            bc.size = size;
        }

        private static TownBell BuildBell(Transform root, VillageLayout L)
        {
            var f = new GameObject("TownBell").transform;
            f.SetParent(root, false);
            f.position = L.Bell;
            f.rotation = Quaternion.Euler(0, BoardSpace.YawOf(L.Spawn - L.Bell), 0);
            Signs.Box(f, "PostL", new Vector3(-0.8f, 1.7f, 0), new Vector3(0.22f, 3.4f, 0.22f), Wood);
            Signs.Box(f, "PostR", new Vector3(0.8f, 1.7f, 0), new Vector3(0.22f, 3.4f, 0.22f), Wood);
            Signs.Box(f, "Beam", new Vector3(0, 3.45f, 0), new Vector3(2.1f, 0.24f, 0.3f), Wood, false);
            Signs.Box(f, "Cap", new Vector3(0, 3.75f, 0), new Vector3(2.4f, 0.1f, 0.7f), RoofRed, false);
            Signs.Box(f, "Base", new Vector3(0, 0.1f, 0), new Vector3(2.2f, 0.2f, 0.9f), Stone * 0.9f, false);
            var pivot = new GameObject("BellPivot").transform;
            pivot.SetParent(f, false);
            pivot.localPosition = new Vector3(0, 3.3f, 0);
            var mb = new MeshBuilder();
            var bronze = new Color(0.80f, 0.60f, 0.25f);
            mb.Cylinder(new Vector3(0, -1.0f, 0), 0.55f, 0.32f, 0.75f, 12, bronze);
            mb.Cylinder(new Vector3(0, -0.25f, 0), 0.32f, 0.1f, 0.2f, 12, bronze * 0.9f);
            mb.ToObject("Bell", pivot, MaterialFactory.CreateVertexColored(Color.white, "Bell"));
            var bell = f.gameObject.AddComponent<TownBell>();
            bell.BellPivot = pivot;
            return bell;
        }

        private static void BuildDressing(Transform stat, VillageLayout L)
        {
            // Lamp posts around the square and benches.
            for (int i = 0; i < 6; i++)
            {
                float a = 30f + i * 60f;
                var p = L.SquareCentre + BoardSpace.DirOf(a) * (VillageLayout.SquareRadius - 1.2f);
                if ((p - L.WarehouseDoor).magnitude < 6f) continue;
                var f = Frame(stat, "Lamp", p, 0);
                Signs.Box(f, "Pole", new Vector3(0, 1.6f, 0), new Vector3(0.14f, 3.2f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
                Signs.Prim(PrimitiveType.Cube, f, "Lantern", new Vector3(0, 3.3f, 0), new Vector3(0.35f, 0.45f, 0.35f), MaterialFactory.SharedUnlit(new Color(1f, 0.85f, 0.55f)));
            }
            var benchAt = new[] { L.Home + L.Right * 10f + L.Out * 6f, L.Home - L.Right * 10f + L.Out * 7f };
            foreach (var p in benchAt)
            {
                var f = Frame(stat, "Bench", p, BoardSpace.YawOf(L.Home - p));
                Signs.Box(f, "Seat", new Vector3(0, 0.45f, 0), new Vector3(2f, 0.1f, 0.5f), Wood);
                Signs.Box(f, "LegL", new Vector3(-0.8f, 0.22f, 0), new Vector3(0.12f, 0.45f, 0.45f), Wood * 0.8f);
                Signs.Box(f, "LegR", new Vector3(0.8f, 0.22f, 0), new Vector3(0.12f, 0.45f, 0.45f), Wood * 0.8f);
            }
            // Barrels near the storehouse and the warehouse.
            var barrel = MaterialFactory.Shared(new Color(0.48f, 0.32f, 0.2f));
            foreach (var p in new[] { L.Storehouse - L.Right * 4.5f + L.Out * 4.5f, L.Storehouse - L.Right * 4.5f + L.Out * 5.4f, L.WarehouseDoor - L.Out * 1.5f - L.Right * 4.5f })
                Signs.Prim(PrimitiveType.Cylinder, stat, "Barrel", p + Vector3.up * 0.5f, new Vector3(0.8f, 0.5f, 0.8f), barrel);
        }
    }
}
