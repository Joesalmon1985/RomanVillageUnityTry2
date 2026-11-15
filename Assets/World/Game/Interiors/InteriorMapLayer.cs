using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The interior map (package I5, PRD_CRYSTALS 5.10 "The map"): on Tab inside an interior, the cells explored so
    /// far as a hex plan with their walls, the ways out in green (always shown, explored or not), stairs down, doors
    /// shut or open, the markers discovered (hidden until seen, or "?" while the lamp-bearer lives), where Mira and the
    /// party are, and the player. The side panel names the interior and offers "Lead me out". On the island map the
    /// open ways down are marked at their places. An <see cref="IMapLayer"/> added by <see cref="InteriorDirector"/>.
    /// </summary>
    public sealed class InteriorMapLayer : IMapLayer
    {
        public static readonly InteriorMapLayer Instance = new InteriorMapLayer();

        public static readonly Color WayOutColour = new Color(0.35f, 0.92f, 0.45f);
        public static readonly Color WayDownColour = new Color(0.45f, 0.65f, 1f);
        public static readonly Color PartyColour = new Color(1f, 0.62f, 0.25f);
        private const int TexSize = 256;

        private Texture2D _tex;
        private string _texFor;
        private int _texVersion = -1;
        private Rect _leadRect;

        /// <summary>The interior map's middle and half side (metres) for an interior.</summary>
        public static void Frame(InteriorModel m, out Vector2 centre, out float extent)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int j = 0; j < Mathf.Max(1, m.Height); j++)
                for (int i = 0; i < Mathf.Max(1, m.Width); i++)
                {
                    var p = WalkLattice.ToWorld(m.ToCell(i, j));
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
            // Fit the floor (not the whole block) so a small interior is drawn large.
            if (m.Floor.Count > 0)
            {
                minX = minZ = float.MaxValue; maxX = maxZ = float.MinValue;
                foreach (var c in m.Floor)
                {
                    var p = WalkLattice.ToWorld(c);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
            }
            centre = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            extent = Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f + 9f;
        }

        /// <summary>Whether the map shows a marker: ways out always; others once their cell was seen.</summary>
        public static bool Discovered(InteriorModel m, MarkerDef md)
        {
            var link = Interiors.LinkAt(m.Ref(md.Id));
            if (link != null && link.Up) return true;
            foreach (var c in Interiors.Explored(m.Id)) if (c.I == md.I && c.J == md.J) return true;
            return false;
        }

        /// <summary>True while the companion with the lamp service lives (undiscovered markers show as "?").</summary>
        public static bool LampLives(WorldState s)
        {
            if (s?.Campaign == null) return false;
            foreach (var c in CampaignApi.Companions(s)) if (c.Alive && c.Service == CompanionServices.Lamp) return true;
            return false;
        }

        public void Draw(MapScreenView view)
        {
            var b = WorldBootstrap.Instance;
            var s = b != null && b.Sim != null ? b.Sim.State : null;
            var m = Interiors.Get(MapScreen.InteriorShown);
            if (m == null) { DrawIslandWays(view); return; }
            EnsureTexture(m);
            Frame(m, out var centre, out float extent);
            var tl = view.WorldToGui(new Vector3(centre.x - extent, 0, centre.y + extent));
            var br = view.WorldToGui(new Vector3(centre.x + extent, 0, centre.y - extent));
            GUI.DrawTexture(new Rect(tl.x, tl.y, br.x - tl.x, br.y - tl.y), _tex);

            float d = UiStyle.Px(22);
            bool lamp = LampLives(s);
            foreach (var md in m.Markers)
            {
                var at = m.World(md.I, md.J);
                var link = Interiors.LinkAt(m.Ref(md.Id));
                bool seen = Discovered(m, md);
                if (!seen)
                {
                    if (lamp) view.DrawLabel(at, "?", -UiStyle.Px(5));
                    continue;
                }
                if (link != null)
                {
                    bool open = link.IsOpen;
                    var col = link.Up ? WayOutColour : WayDownColour;
                    if (!open) col = new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f, 0.9f);
                    float pulse = link.Up && open ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
                    view.DrawDisc(at, d * 1.15f * pulse, col);
                    view.DrawRing(at, d * 1.7f * pulse, col);
                    view.DrawLabel(at, LandLook.Capitalise(link.Up ? (link.To.IsInterior ? "up: " : "way out: ") + link.Name : "down: " + link.Name) + (open ? "" : " (shut)"), d);
                    continue;
                }
                switch (md.Kind)
                {
                    case MarkerKind.Door:
                        bool o = Interiors.Knows(md.OpenWhen);
                        view.DrawDisc(at, d, o ? new Color(0.65f, 0.48f, 0.30f) : new Color(0.45f, 0.25f, 0.12f));
                        view.DrawLabel(at, LandLook.Capitalise(Named(md)) + (o ? " (open)" : " (barred)"), d * 0.7f);
                        break;
                    case MarkerKind.Pedestal:
                    case MarkerKind.Altar:
                        view.DrawRing(at, d * 1.2f, InteriorProps.ElementColour(InteriorDirector.Instance?.Definition, md.Crystal));
                        view.DrawLabel(at, LandLook.Capitalise(Named(md)), d * 0.7f);
                        break;
                    case MarkerKind.Threshold:
                        view.DrawDisc(at, d * 0.8f, new Color(0.85f, 0.3f, 0.2f));
                        view.DrawLabel(at, LandLook.Capitalise(Named(md)), d * 0.6f);
                        break;
                    case MarkerKind.Inscription:
                        view.DrawDisc(at, d * 0.7f, new Color(0.95f, 0.9f, 0.75f));
                        view.DrawLabel(at, LandLook.Capitalise(Named(md)), d * 0.6f);
                        break;
                    default:
                        view.DrawDisc(at, d * 0.5f, new Color(0.7f, 0.7f, 0.68f));
                        break;
                }
            }
            // Mira and the party (from the campaign's state; along their leg's cells when they walk here).
            if (s != null && Interiors.PartyWorld(s, LandViews.Instance != null ? LandViews.Instance.TickFraction : 0f, out var party, out _, out var partyIn) && partyIn == m.Id)
            {
                view.DrawDisc(party, UiStyle.Px(20), PartyColour);
                view.DrawLabel(party, "Mira and the party", UiStyle.Px(12));
            }
        }

        private static string Named(MarkerDef md) => string.IsNullOrEmpty(md.Name) ? md.Id : md.Name;

        /// <summary>On the island map: the open ways down at their places.</summary>
        private static void DrawIslandWays(MapScreenView view)
        {
            foreach (var l in Interiors.Links)
            {
                if (!l.OnSurface || !l.IsOpen) continue;
                var at = WalkLattice.ToWorld(l.Cell);
                view.DrawRing(at, UiStyle.Px(18), WayDownColour);
                view.DrawLabel(at, LandLook.Capitalise(l.Name), UiStyle.Px(10));
            }
        }

        public bool Tap(Vector3 world) => false; // a tap walks there (MapScreen's own target)

        /// <summary>The side panel of the interior map.</summary>
        public void DrawSide(MapScreenView view, InteriorModel m, Rect r)
        {
            _leadRect = Rect.zero;
            if (r.width < UiStyle.Px(160)) return;
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, UiStyle.Px(36)), LandLook.Capitalise(m.Name), view.TitleStyle); y += UiStyle.Px(44);
            Key(view, r.x, ref y, r.width, view.DiscTexture, WayOutColour, "The way out, and stairs up");
            Key(view, r.x, ref y, r.width, view.DiscTexture, WayDownColour, "Stairs down");
            Key(view, r.x, ref y, r.width, view.DiscTexture, PartyColour, "Mira and the party");
            Key(view, r.x, ref y, r.width, view.DiscTexture, new Color(0.95f, 0.9f, 0.75f), "Marks on the walls");
            Key(view, r.x, ref y, r.width, view.RingTexture, Color.white, "Walk target");
            y += UiStyle.Px(16);
            // "Lead me out": the game walks you to the nearest open way up.
            var w = PlayerWalker.Instance;
            var d = InteriorDirector.Instance;
            int steps = 0;
            bool can = w != null && d != null && d.FindWayOut(w.Cell, out _, out _, out steps);
            _leadRect = new Rect(r.x, y, Mathf.Min(r.width, UiStyle.Px(420)), UiStyle.Px(44));
            GUI.DrawTexture(_leadRect, UiStyle.SolidTexture(can ? new Color(0.16f, 0.32f, 0.18f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.8f)));
            UiStyle.ShadowLabel(new Rect(_leadRect.x + UiStyle.Px(12), _leadRect.y + UiStyle.Px(8), _leadRect.width, _leadRect.height), can ? "▸ Lead me out (" + steps + " steps)" : "No open way out from here", view.LabelStyle);
            y += UiStyle.Px(58);
            string text = "Tap a spot to walk there when the map closes. Stepping onto a stair or a way out takes you through it.\n\n" +
                          "Only what you have seen is drawn. Ways out are always marked.";
            GUI.Label(new Rect(r.x, y, r.width, r.yMax - y), text, view.SmallStyle);
        }

        private static void Key(MapScreenView view, float x, ref float y, float w, Texture tex, Color c, string text)
        {
            float d = UiStyle.Px(20);
            GUI.color = c; GUI.DrawTexture(new Rect(x, y + UiStyle.Px(3), d, d), tex); GUI.color = Color.white;
            GUI.Label(new Rect(x + d + UiStyle.Px(10), y, w - d - UiStyle.Px(10), UiStyle.Px(26)), text, view.LabelStyle);
            y += UiStyle.Px(30);
        }

        /// <summary>A tap on the side panel (GUI coordinates): "Lead me out" closes the map and starts the walk.</summary>
        public bool TapSide(Vector2 gui)
        {
            if (_leadRect.width <= 0 || !_leadRect.Contains(gui)) return false;
            MapScreen.ClearTarget();
            MapScreen.Close();
            Interiors.LeadOut();
            return true;
        }

        /// <summary>The rect of "Lead me out" on the side panel (tests), zero when not drawn.</summary>
        public Rect LeadOutRect => _leadRect;

        // ---------------- the plan ----------------

        private void EnsureTexture(InteriorModel m)
        {
            int version = Interiors.Explored(m.Id).Count * 31 + DoorState(m);
            if (_tex != null && _texFor == m.Id && _texVersion == version) return;
            if (_tex == null) _tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave, name = "InteriorMap" };
            _texFor = m.Id; _texVersion = version;
            Frame(m, out var centre, out float extent);
            var seen = new HashSet<Cell>();
            foreach (var c in Interiors.Explored(m.Id)) seen.Add(m.ToCell(c.I, c.J));
            var floor = new Color(0.62f, 0.55f, 0.44f, 1f);
            var wall = new Color(0.15f, 0.12f, 0.10f, 1f);
            var none = new Color(0, 0, 0, 0);
            var px = new Color[TexSize * TexSize];
            float cellR = WalkLattice.Step * 0.5f;
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float wx = centre.x + ((x + 0.5f) / TexSize * 2f - 1f) * extent;
                    float wz = centre.y + ((y + 0.5f) / TexSize * 2f - 1f) * extent;
                    var p = new Vector3(wx, 0, wz);
                    var c = WalkLattice.ToCell(p);
                    Color col = none;
                    if (seen.Contains(c))
                    {
                        float dc = BoardSpace.Flat(p - WalkLattice.ToWorld(c));
                        col = floor * (dc > cellR * 0.9f ? 0.8f : 1f);
                        col.a = 1f;
                        if (WalkGrid.IsBlocked(c)) col = new Color(0.45f, 0.3f, 0.18f, 1f);
                    }
                    else if (!m.Floor.Contains(c))
                    {
                        // Rock that borders a seen cell: the wall line.
                        for (int h = 0; h < 6; h++)
                            if (seen.Contains(c + WalkLattice.Dir(h)))
                            {
                                var n = c + WalkLattice.Dir(h);
                                float dn = BoardSpace.Flat(p - WalkLattice.ToWorld(n));
                                if (dn < WalkLattice.Step * 0.9f) { col = wall; break; }
                            }
                    }
                    px[y * TexSize + x] = col;
                }
            _tex.SetPixels(px);
            _tex.Apply();
        }

        private static int DoorState(InteriorModel m)
        {
            int k = 0, bit = 1;
            foreach (var md in m.Markers)
                if (md.Kind == MarkerKind.Door) { if (Interiors.Knows(md.OpenWhen)) k += bit; bit <<= 1; }
            return k * 7919;
        }
    }
}
