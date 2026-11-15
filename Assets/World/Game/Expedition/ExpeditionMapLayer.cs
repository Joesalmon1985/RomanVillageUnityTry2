using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The expedition on the Tab map (package I6, PRD 5.5; added with <see cref="MapScreen.AddLayer"/>): the four
    /// towers (ringed in their colour once lit), the sites the party has word of, where each crystal is believed to be
    /// (with "?" when that word is stale), Mira's leads (the best one named), Mira with her errand ("Mira is waiting for
    /// you here" when she waits), the scout, and the Ash runners where they were last reported with the report's age.
    /// A tap on Mira's marker sets the walk target beside her (or beside the way down when she is underground).
    /// Shows only what the party knows: beliefs and reports, never the rival's true place.
    /// </summary>
    public sealed class ExpeditionMapLayer : IMapLayer
    {
        public struct Mark { public string Kind, Label; public Vector3 World; public Color Colour; }

        private readonly ExpeditionDirector _dir;
        private readonly WorldBootstrap _boot;
        private Texture2D _disc, _ring, _gem, _tri;
        private GUIStyle _label, _bold;
        private int _stylesFor = -1;
        private readonly List<Mark> _marks = new List<Mark>();

        /// <summary>What the layer drew last (kind: tower, site, belief, lead, mira, scout, ash, companion), for tests.</summary>
        public IReadOnlyList<Mark> Marks => _marks;
        /// <summary>Taps the layer took (on Mira's marker).</summary>
        public int TapsTaken { get; private set; }
        /// <summary>World radius within which a tap counts as on Mira's marker.</summary>
        public const float TapRadius = 28f;

        public ExpeditionMapLayer(ExpeditionDirector dir, WorldBootstrap boot) { _dir = dir; _boot = boot; }

        private WorldState S => _boot.Sim.State;
        public static readonly Color MiraColour = new Color(0.72f, 0.52f, 1f), AshColour = new Color(1f, 0.38f, 0.25f), LeadColour = new Color(0.55f, 1f, 0.70f);

        private void Ensure()
        {
            if (_disc == null)
            {
                _disc = Shape(48, (x, y) => Mathf.Clamp01(24f - Mathf.Sqrt(x * x + y * y)));
                _ring = Shape(48, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(24f - d) * Mathf.Clamp01(d - 17f); });
                _gem = Shape(48, (x, y) => Mathf.Clamp01(22f - (Mathf.Abs(x) * 1.3f + Mathf.Abs(y))));
                _tri = Shape(48, (x, y) => y > -18f && Mathf.Abs(x) < (22f - y) * 0.5f ? 1f : 0f);
            }
            if (_stylesFor != Screen.height || _label == null)
            {
                _stylesFor = Screen.height;
                _label = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(17), wordWrap = false, alignment = TextAnchor.MiddleLeft };
                _bold = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
            }
        }

        private static Texture2D Shape(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alpha(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f))));
            t.Apply();
            return t;
        }

        /// <summary>The surface point of a place for the map: the marker itself, or the way down to its interior.</summary>
        public Vector3 SurfacePoint(MarkerRef r)
        {
            var s = S;
            if (!r.IsInterior) return ExpeditionSpace.World(s, r);
            if (ExpeditionSpace.TrySurfaceEntrance(s, r.Interior, out var ent)) return ExpeditionSpace.World(s, ent);
            var loc = s.Campaign.Definition.Location(r.Location);
            if (loc != null && CampaignPlaces.TryAnchorPoint(s.Campaign.Definition, loc, s.Board, out double x, out double z)) return new Vector3((float)x, 0, (float)z);
            return Vector3.zero;
        }

        /// <summary>Where Mira is on the island map: herself on the surface, the way down when she is underground.</summary>
        public Vector3 MiraPoint()
        {
            if (_dir.MiraSpace.Length == 0) return _dir.Mira.transform.position;
            var p = S.Campaign.Party;
            return SurfacePoint(p.Mode == PartyMode.Travelling && p.Leg != null ? p.Leg.To : p.At);
        }

        /// <summary>Her errand in a few words for the map.</summary>
        public string MiraLabel()
        {
            var s = S; var p = s.Campaign.Party;
            bool waiting = p.Mode == PartyMode.WaitingForPlayer || (p.Mode == PartyMode.AtThreshold && !p.PlayerPresent) || (p.Mode == PartyMode.AtThreshold && p.HoldAtThreshold);
            if (waiting) return "Mira is waiting for you here" + (_dir.MiraSpace.Length > 0 ? " (below)" : "");
            string where = CampaignApi.WhereIsMira(s);
            if (where.EndsWith(".")) where = where.Substring(0, where.Length - 1);
            return where + (_dir.MiraSpace.Length > 0 ? " (below)" : "");
        }

        private void Add(MapScreenView view, string kind, Vector3 world, Color colour, Texture2D tex, float px, string label, bool bold = false, float pulse = 0f)
        {
            _marks.Add(new Mark { Kind = kind, Label = label ?? "", World = world, Colour = colour });
            var p = view.WorldToGui(world);
            float d = UiStyle.Px(px) * (1f + pulse * Mathf.Sin(Time.unscaledTime * 5f));
            GUI.color = colour;
            GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), tex);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(label))
                UiStyle.ShadowLabel(new Rect(p.x + d / 2 + 3, p.y - UiStyle.Px(12), UiStyle.Px(520), UiStyle.Px(24)), label, bold ? _bold : _label);
        }

        public void Draw(MapScreenView view)
        {
            _marks.Clear();
            var s = S; var c = s.Campaign;
            if (c == null || _dir == null || _dir.Mira == null) return;
            if (MapScreen.Mode != MapMode.Island) return; // inside an interior the map is package I5's
            Ensure();
            var d = c.Definition;

            // Towers: always on the map (they stand on the shore, seen from far off).
            foreach (var td in d.Towers)
            {
                var loc = d.Location(td.Location);
                if (loc == null || !CampaignPlaces.TryAnchorPoint(d, loc, s.Board, out double x, out double z)) continue;
                var col = ExpeditionSites.ElementColour(d, td.Element);
                bool lit = c.Crystal(td.Element).Custody == Custody.Restored;
                var w = new Vector3((float)x, 0, (float)z);
                Add(view, "tower", w, lit ? col : Color.Lerp(col, new Color(0.3f, 0.3f, 0.3f), 0.6f), lit ? _disc : _ring, lit ? 26 : 20, LandLook.Capitalise(loc.Name) + (lit ? " (lit)" : ""));
                if (lit) Add(view, "tower_glow", w, new Color(col.r, col.g, col.b, 0.55f), _ring, 40, null, false, 0.1f);
            }

            // Where each crystal is believed to be.
            for (int i = 0; i < 4; i++)
            {
                var e = (Element)i;
                var b = CampaignApi.Belief(s, e);
                var cd = d.Crystal(e);
                if (b == null || cd == null || b.Custody == Custody.Restored || b.Custody == Custody.Carried) continue;
                var w = SurfacePoint(b.At) + new Vector3(0, 0, 9f);
                string what = b.Custody == Custody.AtSite ? "" : b.Custody == Custody.AtRivalAltar ? " (Ash altar)" : " (taken)";
                string age = ExpeditionJournal.Clock(s.Tick - b.AsOfTick);
                Add(view, "belief", w, ExpeditionSites.ElementColour(d, e), _gem, 20, LandLook.Capitalise(cd.Name) + what + (b.Stale ? " ? stale, " : ", ") + age + " ago");
            }

            // Leads: where Mira would go next.
            var leads = CampaignApi.Leads(s);
            for (int i = leads.Count - 1; i >= 0; i--)
            {
                var w = SurfacePoint(leads[i].Goal) + new Vector3(0, 0, -9f);
                bool top = i == 0;
                Add(view, "lead", w, top ? LeadColour : new Color(LeadColour.r, LeadColour.g, LeadColour.b, 0.6f), _ring, top ? 28 : 20,
                    top ? "Mira's lead: " + leads[i].Title : null, false, top ? 0.12f : 0f);
            }

            // The last word of the Ash expedition, with its age.
            var known = CampaignApi.RivalView(s);
            if (known.Reports.Count > 0)
            {
                var r = known.Reports[known.Reports.Count - 1];
                var cd = d.Crystal(r.Crystal);
                string what = r.Kind == RivalReportKind.Marching ? "Ash runners heading here" : r.Kind == RivalReportKind.Seized ? "Ash runners seized " + (cd?.Name ?? "a crystal") + " here"
                    : r.Kind == RivalReportKind.OnAltar ? "Ash altar holds " + (cd?.Name ?? "a crystal") : r.Kind == RivalReportKind.Searching ? "Ash runners searching here" : "The Ash leader waits here";
                var at = r.At.IsEmpty && cd != null ? cd.Site : r.At;
                Add(view, "ash", SurfacePoint(at) + new Vector3(9f, 0, 0), AshColour, _tri, 22, what + ", " + ExpeditionJournal.Clock(s.Tick - r.DueTick) + " ago");
            }

            // The scout.
            if (_dir.ScoutPosition(out var sp, out var who))
            {
                var m = _dir.MateOf(who);
                Add(view, "scout", sp, m != null ? m.Colour : Color.white, _disc, 14, (m != null ? m.Def.Name : "The scout") + " (scouting)");
            }

            // Mira, with her errand. The companions are small dots round her in their own colours.
            var mp = MiraPoint();
            int k = 0;
            foreach (var m in _dir.Mates)
            {
                if (!m.Alive || m.Out) continue;
                float a = k++ * 60f * Mathf.Deg2Rad;
                var gp = view.WorldToGui(mp);
                float dd = UiStyle.Px(8);
                GUI.color = m.Colour;
                GUI.DrawTexture(new Rect(gp.x + Mathf.Sin(a) * UiStyle.Px(15) - dd / 2, gp.y + Mathf.Cos(a) * UiStyle.Px(15) - dd / 2, dd, dd), _disc);
                GUI.color = Color.white;
                _marks.Add(new Mark { Kind = "companion", Label = m.Def.Name, World = mp, Colour = m.Colour });
            }
            Add(view, "mira", mp, MiraColour, _disc, 20, MiraLabel(), true, s.Campaign.Party.PlayerPresent ? 0f : 0.15f);
        }

        public bool Tap(Vector3 world)
        {
            var c = S.Campaign;
            if (c == null || _dir == null || _dir.Mira == null || MapScreen.Mode != MapMode.Island) return false;
            var mp = MiraPoint();
            if (BoardSpace.Flat(world - mp) > TapRadius) return false;
            // Walk to her (to the way down when she is below).
            if (!MapScreen.SetTarget(mp)) return false;
            TapsTaken++;
            return true;
        }
    }
}
