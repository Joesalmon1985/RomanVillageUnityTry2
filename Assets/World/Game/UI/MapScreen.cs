using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The Tab map (REFACTOR_SPEC 2.D, appendix B): a read-only top-down view of the whole island: hexes with their
    /// terrain and numbers, camps of both peoples (cities larger, sites hollow), main roads and road sites, demons,
    /// your people's open proposals (the top one is where the prince wants you), the prince and the player. The world
    /// keeps running. Tapping a spot sets an auto-walk target that starts when the map closes (no teleport). Reads state
    /// only and submits no command. Owner: package P.
    /// </summary>
    public static class MapScreen
    {
        public static bool IsOpen { get; private set; }
        /// <summary>The walk target picked on the map (starts when the map closes), or null.</summary>
        public static Cell? Target { get; private set; }

        public static event System.Action<bool> Toggled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; Target = null; Toggled = null; }

        public static void Open()
        {
            if (IsOpen) return;
            if (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God) return;
            IsOpen = true;
            Toggled?.Invoke(true);
        }

        /// <summary>Closes the map and starts the auto-walk to the picked target, if any.</summary>
        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            var w = PlayerWalker.Instance;
            if (Target.HasValue && w != null)
            {
                var t = Target.Value;
                Target = null;
                if (t != w.Cell && !w.WalkTo(t)) SpeechBubbles.Say(null, "There is no way there.", 3f);
            }
            Toggled?.Invoke(false);
        }

        public static void Toggle() { if (IsOpen) Close(); else Open(); }

        /// <summary>Picks the walk target at a world point (the nearest free cell). False when nothing walkable is near.</summary>
        public static bool SetTarget(Vector3 world)
        {
            var c = WalkGrid.NearestFree(WalkLattice.ToCell(world), 4);
            if (!c.HasValue) return false;
            Target = c;
            return true;
        }

        public static void ClearTarget() => Target = null;
    }

    /// <summary>Draws the <see cref="MapScreen"/> and reads Tab and taps on it. Added by <see cref="PlayerInstaller"/>.</summary>
    public sealed class MapScreenView : MonoBehaviour
    {
        public static readonly Color YourColour = new Color(0.25f, 0.55f, 1f);
        public static readonly Color RivalColour = new Color(0.85f, 0.25f, 0.15f);
        private const int TexSize = 640;
        private Texture2D _base, _disc, _ring, _arrow;
        private float _extent; // half side of the mapped square, metres
        private Rect _rect;
        private GUIStyle _num, _label, _title, _small;
        private int _stylesFor = -1;

        public static MapScreenView Instance { get; private set; }
        /// <summary>Screen rect (GUI coordinates) of the map while open.</summary>
        public Rect MapRect => _rect;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static bool CanToggle =>
            !Hud.MenuOpen && (EncounterHost.Instance == null || !EncounterHost.Instance.IsRunning)
            && (ViewSwitcher.Instance == null || ViewSwitcher.Instance.CurrentView != ViewMode.God);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && (MapScreen.IsOpen || CanToggle)) MapScreen.Toggle();
            if (!MapScreen.IsOpen) return;
            if (!CanToggle) { MapScreen.Close(); return; }
            if (Input.GetMouseButtonDown(0)) TapScreen(Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) TapScreen(Input.GetTouch(i).position);
            if (Input.GetMouseButtonDown(1)) MapScreen.ClearTarget();
        }

        /// <summary>A tap on the open map at a screen point (pixels, origin bottom-left): sets the walk target. False if off the map.</summary>
        public bool TapScreen(Vector2 screen)
        {
            if (!MapScreen.IsOpen || _rect.width <= 0) return false;
            var gui = new Vector2(screen.x, Screen.height - screen.y);
            if (!_rect.Contains(gui)) return false;
            return MapScreen.SetTarget(GuiToWorld(gui));
        }

        public Vector3 GuiToWorld(Vector2 gui)
        {
            float u = (gui.x - _rect.x) / _rect.width, v = 1f - (gui.y - _rect.y) / _rect.height;
            return new Vector3((u * 2f - 1f) * _extent, BoardSpace.GroundY, (v * 2f - 1f) * _extent);
        }

        public Vector2 WorldToGui(Vector3 w)
        {
            float u = (w.x / _extent + 1f) * 0.5f, v = (w.z / _extent + 1f) * 0.5f;
            return new Vector2(_rect.x + u * _rect.width, _rect.y + (1f - v) * _rect.height);
        }

        private void EnsureTextures()
        {
            if (_base != null) return;
            var b = WorldBootstrap.Instance;
            if (b == null || b.Sim == null) return;
            var board = b.Sim.State.Board;
            var t = board.Topology;
            _extent = BoardSpace.BoardRadius + 45f;
            _base = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave, name = "MapBase" };
            var ids = new int[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    double wx = ((x + 0.5) / TexSize * 2 - 1) * _extent, wz = ((y + 0.5) / TexSize * 2 - 1) * _extent;
                    ids[y * TexSize + x] = t.HexAt(wx, wz);
                }
            var sea = new Color(0.16f, 0.32f, 0.45f, 1f);
            var rim = new Color(0.42f, 0.50f, 0.30f, 1f);
            var px = new Color[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    int i = y * TexSize + x;
                    int h = ids[i];
                    Color c;
                    if (h < 0)
                    {
                        float wx = ((x + 0.5f) / TexSize * 2 - 1) * _extent, wz = ((y + 0.5f) / TexSize * 2 - 1) * _extent;
                        c = Mathf.Sqrt(wx * wx + wz * wz) < BoardSpace.BoardRadius + 25f ? rim : sea;
                    }
                    else
                    {
                        c = Muted(TerrainBuilder.TerrainColor(board.HexTerrain[h]));
                        bool border = (x + 1 < TexSize && ids[i + 1] != h) || (y + 1 < TexSize && ids[i + TexSize] != h)
                                      || (x > 0 && ids[i - 1] != h) || (y > 0 && ids[i - TexSize] != h);
                        if (border) c = Color.Lerp(c, new Color(0.12f, 0.10f, 0.08f), 0.55f);
                    }
                    px[i] = c;
                }
            _base.SetPixels(px);
            _base.Apply();
            _disc = Disc(64, false);
            _ring = Disc(64, true);
            _arrow = Arrow(64);
        }

        private static Color Muted(Color c) { Color.RGBToHSV(c, out float h, out float s, out float v); var m = Color.HSVToRGB(h, s * 0.8f, Mathf.Clamp01(v * 0.95f + 0.05f)); m.a = 1f; return m; }

        private static Texture2D Disc(int n, bool ring)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = Mathf.Clamp01(r - d);
                    if (ring) a *= Mathf.Clamp01(d - (r - n * 0.16f));
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        private static Texture2D Arrow(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Pointing up (towards +y in the texture = top of the GUI rect after the flip).
                    float fy = (y + 0.5f) / n, fx = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f;
                    float edge = fy; // width grows towards the bottom
                    float a = fx < (1f - edge) && fy > 0.08f ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (_stylesFor == Screen.height && _num != null) return;
            _stylesFor = Screen.height;
            _num = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = UiStyle.Px(15) };
            _label = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(17), wordWrap = false, alignment = TextAnchor.MiddleLeft };
            _title = new GUIStyle(UiStyle.Label) { fontSize = UiStyle.Px(26), fontStyle = FontStyle.Bold, wordWrap = false };
            _title.normal.textColor = UiStyle.AccentColor;
            _small = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(17), wordWrap = true };
        }

        private void OnGUI()
        {
            if (!MapScreen.IsOpen) { _rect = Rect.zero; return; }
            var b = WorldBootstrap.Instance;
            if (b == null || b.Sim == null) return;
            EnsureTextures(); EnsureStyles();
            GUI.depth = -15;
            var s = b.Sim.State;
            var t = s.Board.Topology;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), UiStyle.SolidTexture(new Color(0.05f, 0.06f, 0.08f, 0.94f)));
            float size = Mathf.Min(Screen.height - UiStyle.Px(110), Screen.width - UiStyle.Px(420));
            size = Mathf.Max(size, Mathf.Min(Screen.width, Screen.height) * 0.6f);
            _rect = new Rect(UiStyle.Px(30), (Screen.height - size) / 2 + UiStyle.Px(20), size, size);
            GUI.DrawTexture(_rect, _base);
            float mpp = size / (2f * _extent); // GUI px per metre

            // Main roads and road sites.
            foreach (var r in s.Economy.Roads)
            {
                if (r.State == RoadState.Cancelled) continue;
                var a = WorldToGui(BoardSpace.Vertex(t, t.HexEdgeCornerA(r.HexEdge)));
                var c = WorldToGui(BoardSpace.Vertex(t, t.HexEdgeCornerB(r.HexEdge)));
                var col = r.Faction == PlayerIds.Human ? YourColour : RivalColour;
                if (r.State != RoadState.Built) col = new Color(col.r, col.g, col.b, 0.45f);
                Line(a, c, Mathf.Max(2f, UiStyle.Px(r.State == RoadState.Built ? 5 : 3)), col);
            }
            // Hex numbers.
            for (int h = 0; h < t.HexCount; h++)
            {
                int n = s.Board.HexNumber[h];
                if (n <= 0) continue;
                var p = WorldToGui(BoardSpace.Hex(t, h));
                float d = UiStyle.Px(28);
                GUI.color = new Color(0.97f, 0.93f, 0.80f, 0.95f);
                GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), _disc);
                GUI.color = Color.white;
                _num.normal.textColor = n == 6 || n == 8 ? new Color(0.75f, 0.1f, 0.05f) : new Color(0.15f, 0.12f, 0.08f);
                GUI.Label(new Rect(p.x - d / 2, p.y - d / 2, d, d), n.ToString(), _num);
            }
            // Camps.
            foreach (var st in s.Economy.Settlements)
            {
                if (st.State == SettlementState.Cancelled) continue;
                var p = WorldToGui(BoardSpace.Vertex(t, st.Corner));
                var col = st.Faction == PlayerIds.Human ? YourColour : RivalColour;
                float d = UiStyle.Px(st.IsCity ? 30 : 22);
                GUI.color = st.State == SettlementState.Active ? col : new Color(col.r, col.g, col.b, 0.9f);
                GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), st.State == SettlementState.Active ? _disc : _ring);
                if (st.IsCity) { GUI.color = Color.white; GUI.DrawTexture(new Rect(p.x - d * 0.75f, p.y - d * 0.75f, d * 1.5f, d * 1.5f), _ring); }
                GUI.color = Color.white;
            }
            // Demons.
            foreach (var dv in DemonApi.DemonSites(s))
            {
                if (dv.Count <= 0) continue;
                var p = WorldToGui(new Vector3((float)dv.X, 0, (float)dv.Z));
                float d = UiStyle.Px(26);
                GUI.color = new Color(0.45f, 0.05f, 0.55f, 0.95f);
                GUI.DrawTexture(new Rect(p.x - d / 2 + UiStyle.Px(10), p.y - d / 2 - UiStyle.Px(10), d, d), _disc);
                GUI.color = Color.white;
                _num.normal.textColor = Color.white;
                GUI.Label(new Rect(p.x - d / 2 + UiStyle.Px(10), p.y - d / 2 - UiStyle.Px(10), d, d), dv.Count.ToString(), _num);
            }
            // Your proposals; the top one is where the prince wants you.
            var props = FactionApi.Proposals(s, PlayerIds.Human);
            for (int i = props.Count - 1; i >= 0; i--)
            {
                if (!FactionApi.ProposalTarget(s, props[i].Id, out double x, out double z, out _)) continue;
                var p = WorldToGui(new Vector3((float)x, 0, (float)z));
                bool top = i == 0;
                float d = UiStyle.Px(top ? 34 : 22) * (top ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f) : 1f);
                GUI.color = top ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.85f, 0.2f, 0.6f);
                GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), _ring);
                GUI.color = Color.white;
                if (top) UiStyle.ShadowLabel(new Rect(p.x + d / 2 + 2, p.y - UiStyle.Px(13), UiStyle.Px(420), UiStyle.Px(26)), "The prince wants you here: " + FactionApi.ProposalTitle(s, props[i].Id), _label);
            }
            // The prince.
            var prince = PrinceDirector.Instance != null ? PrinceDirector.Instance.Prince : null;
            if (prince != null) Marker(prince.transform.position, new Color(1f, 0.75f, 0.2f), "Prince");
            // Walk target and player.
            var w = PlayerWalker.Instance;
            var target = MapScreen.Target ?? (w != null ? w.AutoGoal : null);
            if (target.HasValue)
            {
                var p = WorldToGui(WalkLattice.ToWorld(target.Value));
                float d = UiStyle.Px(20);
                GUI.color = Color.white; GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), _ring);
                UiStyle.ShadowLabel(new Rect(p.x + d / 2 + 2, p.y - UiStyle.Px(12), UiStyle.Px(200), UiStyle.Px(24)), MapScreen.Target.HasValue ? "Walk here" : "Walking here", _label);
            }
            if (w != null)
            {
                var p = WorldToGui(w.Position);
                float d = UiStyle.Px(30);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(w.Yaw, p);
                GUI.color = new Color(1f, 0.95f, 0.3f);
                GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), _arrow);
                GUI.matrix = m;
                GUI.color = Color.white;
                UiStyle.ShadowLabel(new Rect(p.x + d / 2, p.y + UiStyle.Px(2), UiStyle.Px(80), UiStyle.Px(24)), "You", _label);
            }
            GUI.color = Color.white;
            DrawSide(s, new Rect(_rect.xMax + UiStyle.Px(30), _rect.y, Screen.width - _rect.xMax - UiStyle.Px(60), _rect.height), mpp);
        }

        private void Marker(Vector3 world, Color c, string name)
        {
            var p = WorldToGui(world);
            float d = UiStyle.Px(16);
            GUI.color = c; GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), _disc); GUI.color = Color.white;
            UiStyle.ShadowLabel(new Rect(p.x + d / 2 + 2, p.y - UiStyle.Px(12), UiStyle.Px(160), UiStyle.Px(24)), name, _label);
        }

        private void DrawSide(WorldState s, Rect r, float mpp)
        {
            if (r.width < UiStyle.Px(160)) return;
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, UiStyle.Px(36)), "The island", _title); y += UiStyle.Px(44);
            Key(r.x, ref y, r.width, _disc, YourColour, "Your camps (city: ringed)");
            Key(r.x, ref y, r.width, _disc, RivalColour, "Camps of the Ash Clan");
            Key(r.x, ref y, r.width, _ring, Color.white, "Camp sites, walk target");
            Key(r.x, ref y, r.width, _disc, new Color(0.45f, 0.05f, 0.55f), "Demons (with their count)");
            Key(r.x, ref y, r.width, _ring, new Color(1f, 0.85f, 0.2f), "Proposals of your people");
            Key(r.x, ref y, r.width, _arrow, new Color(1f, 0.95f, 0.3f), "You");
            y += UiStyle.Px(14);
            string text = "Numbered hexes yield when the dice show their number (6 and 8 most often, in red).\n\n" +
                          "Tap a spot to walk there when the map closes. Right click clears it. Tab closes the map.\n\n" +
                          FactionApi.Headline(s);
            GUI.Label(new Rect(r.x, y, r.width, r.yMax - y), text, _small);
        }

        private void Key(float x, ref float y, float w, Texture tex, Color c, string text)
        {
            float d = UiStyle.Px(20);
            GUI.color = c; GUI.DrawTexture(new Rect(x, y + UiStyle.Px(3), d, d), tex); GUI.color = Color.white;
            GUI.Label(new Rect(x + d + UiStyle.Px(10), y, w - d - UiStyle.Px(10), UiStyle.Px(26)), text, _label);
            y += UiStyle.Px(30);
        }

        private static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var m = GUI.matrix;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2, len, width), UiStyle.SolidTexture(c));
            GUI.matrix = m;
        }
    }
}
