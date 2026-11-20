using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// Where the council village stands: a lookout tower near the home fire and eleven dispute places spread in a fan in
    /// front of it (the storehouse by the fire, the others on the clearest open ground at 20 to 50 m), found with the
    /// camp layout's obstruction test. All positions are world metres at normal size.
    /// </summary>
    public sealed class CouncilSite
    {
        /// <summary>The foot of the lookout tower (ground).</summary>
        public Vector3 Position;
        /// <summary>Direction of the fan, degrees about Y (0 = +Z).</summary>
        public float Yaw;
        public float Scale = 1f;
        public string Why;
        public Vector3 Hearth;
        /// <summary>World centre of the place of each track (index track - 2), where the objective stands.</summary>
        public readonly Vector3[] Loc = new Vector3[11];
        public readonly float[] Radius = new float[11];
        public int Failures;

        public Vector3 ToWorld(Vector3 local) => Position + Quaternion.Euler(0, Yaw, 0) * local;
        public static Vector3 Dir(float yaw) => Quaternion.Euler(0, yaw, 0) * Vector3.forward;

        /// <summary>Radius of the cluster of a place with n people.</summary>
        public static float RadiusFor(int people) => 2.3f + 0.24f * people;

        /// <summary>Slots (lateral x, forward z in metres from the tower, in the fan's frame) of each track.</summary>
        public static readonly Vector2[] Slots =
        {
            new Vector2(-10, 56),  // 2 crossing (water's edge)
            new Vector2(0, 40),   // 3 boundary
            new Vector2(-22, 43), // 4 forest edge
            new Vector2(-25, 33), // 5 timber workyard
            new Vector2(-21, 23), // 6 herbs, healer's shelter
            new Vector2(0, 28),   // 7 grain: the storehouse beside the fire (placed from the hearth)
            new Vector2(21, 23),  // 8 tools, quarry face
            new Vector2(25, 33),  // 9 shares, mill stone
            new Vector2(22, 43),  // 10 water, spring
            new Vector2(0, 49),   // 11 witness, the track
            new Vector2(10, 56),  // 12 deed, keeper's post
        };

        /// <summary>
        /// Like <see cref="CampLayout.Obstruction"/> but ignoring hex stock patches and the unbuilt road lines (trees,
        /// crops and rocks are cleared with <c>LandViews.AddClearing</c>): keeps off plots, corners, the home camp's paths.
        /// </summary>
        public static string Blocked(Board board, CampLayout layout, Vector3 p, float radius, bool ignoreHome = false)
        {
            var t = board.Topology;
            p.y = 0;
            int home = board.StartCorners != null && board.StartCorners.Length > 0 ? board.StartCorners[0] : 0;
            int reserved = board.ReservedPlots != null && board.ReservedPlots.Length > 0 ? board.ReservedPlots[0] : -1;
            if (reserved >= 0)
            {
                var c = new Vector3((float)PlotLayout.PlotX(t, reserved), 0, (float)PlotLayout.PlotZ(t, reserved));
                if ((p - c).magnitude < PlotLayout.DanceCircleRadius + radius + 2f) return "dance circle";
            }
            for (int c = 0; c < t.CornerCount; c++)
            {
                var cp = BoardSpace.Vertex(t, c); cp.y = 0;
                float d = (p - cp).magnitude;
                if (d > 200f) continue;
                if (!(ignoreHome && c == home) && d < (c == home ? CampLayout.HomeCornerClear : CampLayout.CornerClear) + radius) return "corner " + c;
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                for (int k = 0; k < PlotLayout.PlotsPerCorner; k++)
                {
                    int plot = PlotLayout.PlotId(c, k / 3, k % 3);
                    if (plot == reserved) continue;
                    var pc = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
                    if ((p - pc).magnitude < PlotLayout.PlotRadius + CampLayout.PlotMargin + radius) return "plot " + plot;
                }
            }
            foreach (var pr in layout.Dressing) { var d2 = pr.Position - p; d2.y = 0; if (d2.magnitude < pr.Radius + radius + 1f) return "camp prop"; }
            foreach (var pl in layout.Places) { var d3 = pl.Position - p; d3.y = 0; if (d3.magnitude < pl.Radius + radius + 1f) return "hub place"; }
            return null;
        }

        static bool OkTerrain(Board board, Vector3 w)
        {
            int hx = BoardSpace.HexAt(w);
            if (hx < 0) return false;
            var tr = board.HexTerrain[hx];
            return tr != World.Sim.Terrain.Hills && tr != World.Sim.Terrain.Mountain;
        }

        static readonly List<Vector2> Offsets = BuildOffsets();
        static List<Vector2> BuildOffsets()
        {
            var l = new List<Vector2>();
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) l.Add(new Vector2(x * 3f, z * 3f));
            l.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            return l;
        }

        /// <summary>Finds the tower and the eleven places: tries a ring of fan directions and keeps the clearest.</summary>
        public static CouncilSite Find(WorldState s)
        {
            var board = s.Board;
            var layout = new CampLayout(board);
            var hearth = layout.Hearth; hearth.y = 0;
            CouncilSite best = null; float bestScore = float.MaxValue;
            for (int k = 0; k < 12; k++)
            {
                float yaw = k * 30f;
                var site = Try(board, layout, hearth, yaw, out float score);
                if (score < bestScore) { bestScore = score; best = site; }
            }
            best.Why = "tower " + (best.Position - hearth).magnitude.ToString("0") + " m from the fire, fan yaw " + best.Yaw.ToString("0") + ", " + best.Failures + " places on relaxed ground";
            return best;
        }

        static CouncilSite Try(Board board, CampLayout layout, Vector3 hearth, float yaw, out float score)
        {
            var site = new CouncilSite { Yaw = yaw, Hearth = hearth };
            var dir = Dir(yaw);
            score = 0f;
            // the tower: about 18 m behind the fire, on clear ground
            Vector3 tower = hearth - dir * 18f; bool towerOk = false;
            for (float d = 18f; d <= 26f && !towerOk; d += 2f)
            {
                var c = hearth - dir * d;
                if (Blocked(board, layout, c, 6f) == null && OkTerrain(board, c)) { tower = c; towerOk = true; }
            }
            if (!towerOk) score += 60f;
            site.Position = tower;
            for (int t = 2; t <= 12; t++)
            {
                int i = t - 2;
                float R = RadiusFor(CouncilRules.Length(t));
                site.Radius[i] = R;
                var nominal = t == 7 ? hearth + dir * 10f : tower + Quaternion.Euler(0, yaw, 0) * new Vector3(Slots[i].x, 0, Slots[i].y);
                nominal.y = 0;
                if (t == 7) { site.Loc[i] = nominal; continue; }
                bool found = false;
                foreach (var o in Offsets)
                {
                    var w = nominal + new Vector3(o.x, 0, o.y);
                    if (Blocked(board, layout, w, R * 0.7f + 1f) != null || !OkTerrain(board, w)) continue;
                    if ((w - tower).magnitude < 14f + R * 0.5f) continue;
                    if (t == 6 && layout.TryPlace("HUB-HEALER", out var lodge) && (w - lodge.Position).magnitude < 20f) continue; // the healer's lodge stays away from the herbs place
                    site.Loc[i] = w; score += o.magnitude; found = true; break;
                }
                if (!found) { site.Loc[i] = nominal; score += 40f; site.Failures++; }
            }
            // places must not overlap each other
            for (int a = 0; a < 11; a++)
                for (int b = a + 1; b < 11; b++)
                {
                    float need = site.Radius[a] + site.Radius[b] + 1f;
                    float d = (site.Loc[a] - site.Loc[b]).magnitude;
                    if (d < need) score += (need - d) * 8f;
                }
            return site;
        }
    }
}
