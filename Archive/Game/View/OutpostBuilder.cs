using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Placeholder rival outpost at <see cref="Board.RivalOutpostVertex"/>: a log stockade (radius 9 m) with its gate
    /// towards the home village, a watchtower, two tents and a tall dark-red banner, so the larger world has a second
    /// landmark. One combined mesh, no colliders except a trigger pick volume (inspectable as "Rival outpost").
    /// Military views (garrison, raids) come later; this only marks the place.
    /// </summary>
    public static class OutpostBuilder
    {
        public const float StockadeRadius = 9f, BannerHeight = 14f;

        public static BuildingInfo Outpost { get; private set; }

        public static BuildingInfo Build(Transform parent, Board board, VillageLayout layout, Transform godOverlay)
        {
            var t = board.Topology;
            int v = board.RivalOutpostVertex;
            var c = BoardSpace.Vertex(t, v);
            var toHome = layout.Home - c; toHome.y = 0; toHome.Normalize();
            float gateYaw = BoardSpace.YawOf(toHome);

            var mb = new MeshBuilder();
            var log = new Color(0.36f, 0.25f, 0.16f);
            var rnd = new PropRandom(97);
            int logs = Mathf.RoundToInt(2f * Mathf.PI * StockadeRadius / 0.62f);
            for (int i = 0; i < logs; i++)
            {
                float yaw = i * 360f / logs;
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, gateYaw)) < 11f) continue; // gate
                var p = c + BoardSpace.DirOf(yaw) * StockadeRadius;
                float h = rnd.Range(3.0f, 3.6f);
                var col = log * rnd.Range(0.85f, 1.12f); col.a = 1;
                mb.Cylinder(p, 0.3f, 0.3f, h, 6, col, false);
                mb.Cone(p + Vector3.up * h, 0.3f, 0.5f, 6, col * 1.1f);
            }
            // Gate posts.
            foreach (float s in new[] { -12f, 12f })
                mb.Cylinder(c + BoardSpace.DirOf(gateYaw + s) * StockadeRadius, 0.4f, 0.4f, 4.4f, 6, log * 0.8f);
            mb.Box(c + toHome * StockadeRadius + Vector3.up * 4.2f, new Vector3(4.2f, 0.4f, 0.4f), log * 0.8f, gateYaw);

            // Watchtower opposite the gate.
            var tw = c - toHome * 4.5f;
            foreach (float x in new[] { -1.2f, 1.2f }) foreach (float z in new[] { -1.2f, 1.2f })
                mb.Box(tw + new Vector3(x, 3f, z), new Vector3(0.3f, 6f, 0.3f), log);
            mb.Box(tw + Vector3.up * 6.1f, new Vector3(3.4f, 0.25f, 3.4f), log * 1.1f);
            foreach (var (o, s) in new[] { (new Vector3(0, 6.7f, 1.6f), new Vector3(3.4f, 1f, 0.12f)), (new Vector3(0, 6.7f, -1.6f), new Vector3(3.4f, 1f, 0.12f)), (new Vector3(1.6f, 6.7f, 0), new Vector3(0.12f, 1f, 3.4f)), (new Vector3(-1.6f, 6.7f, 0), new Vector3(0.12f, 1f, 3.4f)) })
                mb.Box(tw + o, s, log * 0.95f);
            mb.Cone(tw + Vector3.up * 7.3f, 2.6f, 2f, 4, new Color(0.35f, 0.12f, 0.12f));

            // Tents.
            var canvas = new Color(0.62f, 0.55f, 0.45f);
            foreach (var off in new[] { new Vector3(3.5f, 0, 2.5f), new Vector3(-3.5f, 0, 2f) })
            {
                var p = c + Quaternion.Euler(0, gateYaw, 0) * off;
                mb.Cone(p, 1.8f, 2.4f, 6, canvas);
            }
            // Fire pit.
            TerrainBuilder.Disc(mb, c + Vector3.up * 0.05f, 1.1f, 8, new Color(0.25f, 0.22f, 0.2f));

            // Banner pole and flag.
            var pole = c + toHome * 2f;
            mb.Cylinder(pole, 0.14f, 0.1f, BannerHeight, 6, new Color(0.25f, 0.22f, 0.2f));
            var flagDir = BoardSpace.DirOf(gateYaw + 90f);
            var red = new Color(0.55f, 0.08f, 0.10f);
            mb.Box(pole + flagDir * 1.6f + Vector3.up * (BannerHeight - 1.3f), new Vector3(3.2f, 2.2f, 0.06f), red, gateYaw);
            mb.Box(pole + flagDir * 1.6f + Vector3.up * (BannerHeight - 1.3f), new Vector3(1.0f, 1.0f, 0.1f), new Color(0.08f, 0.07f, 0.07f), gateYaw + 45f);

            var root = new GameObject("RivalOutpost");
            root.transform.SetParent(parent, false);
            mb.ToObject("OutpostMesh", root.transform, MaterialFactory.CreateVertexColored(Color.white, "Outpost"));

            var info = new GameObject("Rival outpost").AddComponent<BuildingInfo>();
            info.transform.SetParent(root.transform, false);
            info.transform.position = c;
            info.Kind = "outpost"; info.DisplayName = "Rival outpost"; info.Vertex = v;
            info.StaticLines.Add("A rival's stockade on corner " + v + ", about " + Mathf.RoundToInt(BoardSpace.Flat(c - layout.Home)) + " m from the village.");
            info.StaticLines.Add("Its garrison and raids are simulated; the military view comes later.");
            var bc = info.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0, 3f, 0);
            bc.size = new Vector3(StockadeRadius * 2f + 1f, 6f, StockadeRadius * 2f + 1f);
            Outpost = info;

            if (godOverlay != null)
            {
                var ring = new MeshBuilder();
                TerrainBuilder.Ring(ring, c + Vector3.up * 0.15f, StockadeRadius + 6f, 2f, 32, new Color(0.85f, 0.25f, 0.22f));
                ring.ToObject("OutpostRing", godOverlay, MaterialFactory.CreateUnlit(Color.white, "OutpostRing"));
                GodViewMarker.Create(godOverlay, "Rival outpost", c, new Color(1f, 0.62f, 0.55f));
                GodViewMarker.Create(godOverlay, "Home village", layout.Home, new Color(1f, 0.92f, 0.6f));
            }
            return info;
        }
    }

    /// <summary>
    /// A screen-space name for a landmark, drawn only in God View (it lives under the God View overlay, which is
    /// inactive in first person). Constant pixel size, so it stays readable at any zoom.
    /// </summary>
    public sealed class GodViewMarker : MonoBehaviour
    {
        public string Text;
        public Color Color = Color.white;
        private GUIStyle _style;

        public static GodViewMarker Create(Transform parent, string text, Vector3 world, Color color)
        {
            var go = new GameObject("Marker " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var m = go.AddComponent<GodViewMarker>();
            m.Text = text; m.Color = color;
            return m;
        }

        private void OnGUI()
        {
            var views = ViewSwitcher.Instance;
            if (views == null || views.CurrentView != ViewMode.God || Hud.MenuOpen) return;
            var cam = views.ActiveCamera;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(transform.position);
            if (sp.z <= 0) return;
            if (_style == null) _style = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color;
            float y = Screen.height - sp.y;
            var r = new Rect(sp.x - UiStyle.Px(160), y - UiStyle.Px(52), UiStyle.Px(320), UiStyle.Px(30));
            UiStyle.ShadowLabel(r, Text, _style);
        }
    }
}