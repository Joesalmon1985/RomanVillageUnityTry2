#if UNITY_EDITOR
// Editor only (WORLD_UI_PLAN P-9, WU-0): this whole file is compiled out of player builds, so the zone outline and
// its labels can never appear in the game. Tests check the type is absent from player script assemblies.
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The debug wireframe outline (F1 tool "Zones", off by default): draws each <see cref="WorldZone"/>-tagged
    /// object's screen bounds and the wireframe's rectangle for its zone, in outline with the zone id, plus the safe
    /// area. The wireframe shown is the one of the tagged objects on screen ("auto"), or one picked in the panel.
    /// </summary>
    public static class ZoneOutlineTool
    {
        public const string ToolId = "zones";
        /// <summary>Off by default; the F1 panel toggles it.</summary>
        public static bool Enabled;
        /// <summary>Wireframe to draw (null = auto from the tagged objects in view).</summary>
        public static string Wireframe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Enabled = false; Wireframe = null; }

        public static void Register(WorldBootstrap boot)
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Zones", Order = 99, PanelWidth = 420, DrawPanel = Panel,
                TipText = () => "Zones: editor-only outline of the world-first UI zones (never in builds).",
            });
            if (boot != null && boot.GetComponent<ZoneOutlineOverlay>() == null) boot.gameObject.AddComponent<ZoneOutlineOverlay>();
        }

        private static void Panel(GodTool t, Rect a)
        {
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(38), gap = UiStyle.Px(6);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(56)), "Outlines tagged world objects and the layout zones. Editor only.", UiStyle.Small);
            y += UiStyle.Px(60);
            if (GUI.Button(new Rect(x, y, w, bh), Enabled ? "Hide zone outline" : "Show zone outline", UiStyle.Button)) Enabled = !Enabled;
            y += bh + gap;
            float bw = (w - gap * 3) / 4f;
            int i = 0;
            foreach (var wf in new[] { null, WorldZones.Hub, WorldZones.Hex, WorldZones.Plague, WorldZones.Council, WorldZones.Rhythm, WorldZones.Crystal, WorldZones.Duel })
            {
                var r = new Rect(x + (i % 4) * (bw + gap), y + (i / 4) * (bh + gap), bw, bh);
                string label = (wf ?? "auto") + (Wireframe == wf ? " *" : "");
                if (GUI.Button(r, label, UiStyle.Button)) Wireframe = wf;
                i++;
            }
        }
    }

    /// <summary>Draws the outline while <see cref="ZoneOutlineTool.Enabled"/>.</summary>
    public sealed class ZoneOutlineOverlay : MonoBehaviour
    {
        private static readonly Color Obj = new Color(0.3f, 1f, 0.85f, 1f);
        private static readonly Color Zone = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color Safe = new Color(1f, 0.4f, 0.4f, 1f);
        private Texture2D _px;
        private GUIStyle _label;

        /// <summary>Rects drawn last frame (tests): tagged objects and wireframe zones.</summary>
        public static int ObjectsDrawn { get; private set; }
        public static int ZonesDrawn { get; private set; }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            ObjectsDrawn = 0; ZonesDrawn = 0;
            if (!ZoneOutlineTool.Enabled) return;
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, UiStyle.Px(18)), fontStyle = FontStyle.Bold, wordWrap = false };
            GUI.depth = -20;
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            var safe = ScreenSafeArea.Current();
            Box(safe, Safe, 1f);
            string wf = ZoneOutlineTool.Wireframe;
            foreach (var z in WorldZone.All)
            {
                if (z == null || !z.isActiveAndEnabled) continue;
                if (!WorldHitTarget.ScreenBounds(z, cam, out var r)) continue;
                if (r.xMax < 0 || r.yMax < 0 || r.xMin > Screen.width || r.yMin > Screen.height) continue;
                Box(r, Obj, 2f);
                Label(new Vector2(r.x + 3, r.y + 2), z.ZoneId, Obj);
                ObjectsDrawn++;
                if (wf == null) wf = WorldZone.WireframeOf(z.ZoneId);
            }
            if (wf == null) wf = WorldZones.Hub;
            foreach (var kv in WorldZones.Rects)
            {
                if (WorldZone.WireframeOf(kv.Key) != wf) continue;
                var r = WorldZones.OnScreen(kv.Value, safe);
                Dashed(r, Zone);
                Label(new Vector2(r.x + 4, r.yMax - _label.fontSize - 6), kv.Key, Zone);
                ZonesDrawn++;
            }
        }

        private void Label(Vector2 at, string text, Color c)
        {
            var size = _label.CalcSize(new GUIContent(text));
            var prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(at.x - 2, at.y, size.x + 4, size.y), _px);
            GUI.color = prev;
            _label.normal.textColor = c;
            GUI.Label(new Rect(at.x, at.y, size.x, size.y), text, _label);
        }

        private void Box(Rect r, Color c, float w)
        {
            var prev = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), _px);
            GUI.color = prev;
        }

        private void Dashed(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c;
            const float dash = 10f, gap = 6f, w = 2f;
            for (float x = r.x; x < r.xMax; x += dash + gap)
            {
                float l = Mathf.Min(dash, r.xMax - x);
                GUI.DrawTexture(new Rect(x, r.y, l, w), _px); GUI.DrawTexture(new Rect(x, r.yMax - w, l, w), _px);
            }
            for (float y = r.y; y < r.yMax; y += dash + gap)
            {
                float l = Mathf.Min(dash, r.yMax - y);
                GUI.DrawTexture(new Rect(r.x, y, w, l), _px); GUI.DrawTexture(new Rect(r.xMax - w, y, w, l), _px);
            }
            GUI.color = prev;
        }
    }
}
#endif
