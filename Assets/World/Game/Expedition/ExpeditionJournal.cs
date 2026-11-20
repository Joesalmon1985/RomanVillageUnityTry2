using System.Collections.Generic;
using System.Text;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// Mira's journal (package I6, PRD 5.5 "Journal and rejoining"): the side text of the Tab map while a campaign
    /// runs, and one line on the HUD strip's side when the HUD is on (H). It says where Mira is and what she is doing,
    /// what the party believes of each crystal and how old that word is (a stale belief gets the sim's "where to look"
    /// sentence), where to look next (her best lead and its reason), who of the six is alive and where the others
    /// fell, and the latest entries. A tap on its title (or J) unfolds the whole journal over the map's legend. Asking
    /// Mira "What do we know?" opens it. Reads <see cref="CampaignApi"/> only.
    /// </summary>
    public sealed class ExpeditionJournal : MonoBehaviour
    {
        private ExpeditionDirector _dir;
        private WorldBootstrap _boot;
        private GUIStyle _title, _head, _text, _hud;
        private int _stylesFor = -1;
        private Vector2 _scroll;
        private float _openAt = -1f;

        /// <summary>True when the whole journal is unfolded over the map's side column.</summary>
        public bool Expanded { get; set; }
        /// <summary>Screen rect of the panel drawn last frame (GUI coordinates), or an empty rect.</summary>
        public Rect PanelRect { get; private set; }
        /// <summary>The HUD line drawn last frame, or null (HUD off, map open, a duel).</summary>
        public string HudLineShown { get; private set; }

        private WorldState S => _boot.Sim.State;

        public static ExpeditionJournal Create(ExpeditionDirector dir, WorldBootstrap boot, Transform parent)
        {
            var go = new GameObject("Expedition journal");
            go.transform.SetParent(parent, false);
            var j = go.AddComponent<ExpeditionJournal>();
            j._dir = dir; j._boot = boot;
            return j;
        }

        /// <summary>Opens the map with the journal unfolded, once Mira has finished her sentence.</summary>
        public void OpenSoon() { _openAt = Time.unscaledTime + 0.2f; }

        /// <summary>Opens the map with the journal unfolded now.</summary>
        public void Open()
        {
            Expanded = true;
            MapScreen.Open();
        }

        private void Update()
        {
            if (_openAt >= 0f && Time.unscaledTime >= _openAt && _dir != null && _dir.ScriptPending == 0)
            {
                _openAt = -1f;
                Open();
            }
            if (MapScreen.IsOpen && Input.GetKeyDown(KeyCode.J)) Expanded = !Expanded;
            else if (!MapScreen.IsOpen && Input.GetKeyDown(KeyCode.J) && !Hud.MenuOpen && (EncounterHost.Instance == null || !EncounterHost.Instance.IsRunning)) Open();
            if (!MapScreen.IsOpen && Expanded && _openAt < 0f) Expanded = false;
        }

        // ---------------- the text (also read by tests) ----------------

        public static string Clock(long ticks)
        {
            long s = System.Math.Max(0, ticks) / WorldSim.TicksPerSecond;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        private static string Cap(string s) => LandLook.Capitalise(s);

        /// <summary>One sentence for what the party believes of a crystal, with the age of that word and, when stale, where to look.</summary>
        public static string BeliefSentence(WorldState s, Element e)
        {
            var c = s.Campaign; var d = c.Definition;
            var b = CampaignApi.Belief(s, e);
            var cd = d.Crystal(e);
            if (b == null || cd == null) return "";
            string place = CampaignTokens.PlaceName(d, b.At);
            if (place.Length == 0) place = "an unknown place";
            string what;
            switch (b.Custody)
            {
                case Custody.AtSite: what = "believed on its pedestal at " + place; break;
                case Custody.RivalTransit: what = "taken by the Ash Clan from " + place; break;
                case Custody.AtRivalAltar: what = "on an Ash altar at " + place; break;
                case Custody.Carried: what = "in Mira's hands"; break;
                default: what = "set in " + (d.Location(d.Tower(cd.Tower)?.Location ?? "")?.Name ?? "its tower"); break;
            }
            string text = Cap(cd.Name) + ": " + what + ".";
            if (b.Custody == Custody.Carried || b.Custody == Custody.Restored) return text;
            text += " Word is " + Clock(s.Tick - b.AsOfTick) + " old.";
            if (b.Stale) text += " Stale. " + b.WhereToLook;
            return text;
        }

        /// <summary>Where to look next: Mira's best lead and its reason.</summary>
        public static string NextSentence(WorldState s)
        {
            var top = CampaignApi.TopLead(s);
            if (top == null) return ExpeditionLines.Get(s, ExpeditionLines.MiraNothing, "Nothing more to chase just now.");
            string place = CampaignTokens.PlaceName(s.Campaign.Definition, top.Goal);
            return top.Title + (place.Length > 0 ? " (" + place + ")" : "") + ". " + top.Reason;
        }

        /// <summary>Where Mira is, and whether you are with her.</summary>
        public static string MiraSentence(WorldState s)
        {
            var p = s.Campaign.Party;
            string where = CampaignApi.WhereIsMira(s);
            if (p.PlayerPresent) return where + " You are with her.";
            return where + " " + ExpeditionLines.Get(s, ExpeditionLines.CueHowToRejoin, "Find her and tap her to rejoin.");
        }

        /// <summary>The roster: who is with her, and where the others fell.</summary>
        public static List<string> RosterLines(WorldState s)
        {
            var list = new List<string>();
            var rec = CampaignApi.Record(s);
            foreach (var k in CampaignApi.Companions(s))
            {
                if (k.Alive) { list.Add(k.Name + ", " + LowerFirst(k.Role) + "."); continue; }
                var f = rec != null ? rec.Fallen.Find(x => x.Id == k.Id) : null;
                list.Add(k.Name + " fell" + (f != null && !string.IsNullOrEmpty(f.Place) ? " at " + f.Place : "") + ".");
            }
            return list;
        }

        private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        /// <summary>The demons' pace as the restoration changed it (one line for the journal).</summary>
        public static string DemonSentence(WorldState s)
        {
            var c = s.Campaign;
            int restored = c.RestoredOrder.Count;
            if (restored == 0) return "";
            if (restored >= 4) return "All four towers burn. The demons are gone.";
            int now = s.Demons != null && s.Demons.Tuning != null ? s.Demons.Tuning.SevensPerDraw : 0;
            int onAltars = 0;
            foreach (var st in c.Crystals) if (st.Custody == Custody.AtRivalAltar) onAltars++;
            string lit = restored + " of four towers lit.";
            if (now > c.BaseSevensPerDraw) return lit + " The demons come less often: " + Sevens(now) + " (it was " + Sevens(c.BaseSevensPerDraw) + ").";
            if (now < c.BaseSevensPerDraw) return lit + " But " + onAltars + " on Ash altars: the demons come more often, " + Sevens(now) + " (it was " + Sevens(c.BaseSevensPerDraw) + ").";
            return lit + (onAltars > 0 ? " But " + onAltars + " on Ash altars: the demons come as often as before." : " The demons come as often as before.");
        }

        private static string Sevens(int n) => n <= 1 ? "on every seven rolled" : "once in every " + n + " sevens rolled";

        /// <summary>The whole compact journal text (rich text), top to bottom.</summary>
        public static string CompactText(WorldState s, int entries)
        {
            var c = s.Campaign;
            var sb = new StringBuilder();
            sb.Append(MiraSentence(s)).Append("\n\n");
            sb.Append("<b>The crystals</b>\n");
            for (int i = 0; i < 4; i++)
            {
                string line = BeliefSentence(s, (Element)i);
                if (line.Length > 0) sb.Append(line).Append('\n');
            }
            string demons = DemonSentence(s);
            if (demons.Length > 0) sb.Append(demons).Append('\n');
            sb.Append("\n<b>Where next</b>\n").Append(NextSentence(s)).Append("\n\n");
            sb.Append("<b>The six</b>\n");
            foreach (var l in RosterLines(s)) sb.Append(l).Append('\n');
            var journal = CampaignApi.Journal(s);
            if (journal.Count > 0 && entries > 0)
            {
                sb.Append("\n<b>Latest</b>\n");
                var latest = Distinct(journal);
                for (int i = Mathf.Max(0, latest.Count - entries); i < latest.Count; i++)
                    sb.Append("<color=#BBB0A0>").Append(Clock(latest[i].Tick - c.StartTick)).Append("</color>  ").Append(latest[i].Text).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// The journal without a deed told twice: the runtime and the pack both record some deeds ("Mira took the
        /// Deepstone from the Ash altar" and the pack's "Mira beat Dolm ... and took the Deepstone from the Ash altar"),
        /// on the same tick or a few apart. Of two near-doubles within <see cref="NearTicks"/> the fuller sentence is kept.
        /// </summary>
        public static List<JournalEntry> Distinct(IReadOnlyList<JournalEntry> journal)
        {
            var list = new List<JournalEntry>();
            var words = new List<HashSet<string>>();
            foreach (var j in journal)
            {
                var w = Words(j.Text);
                int twin = -1;
                for (int k = list.Count - 1; k >= 0 && j.Tick - list[k].Tick <= NearTicks; k--)
                    if (list[k].Text == j.Text || NearDouble(words[k], w)) { twin = k; break; }
                if (twin < 0) { list.Add(j); words.Add(w); }
                else if ((j.Text ?? "").Length > (list[twin].Text ?? "").Length) { list[twin] = j; words[twin] = w; }
            }
            return list;
        }

        /// <summary>Two entries this close (ticks) may be one deed told twice.</summary>
        public const int NearTicks = 30;

        private static readonly HashSet<string> s_common = new HashSet<string> { "mira", "the", "and", "from", "with", "that", "this", "they", "their", "into", "were", "have" };

        private static HashSet<string> Words(string text)
        {
            var set = new HashSet<string>();
            foreach (var raw in (text ?? "").ToLowerInvariant().Split(' ', ',', '.', ':', ';', '!', '?'))
                if (raw.Length >= 4 && !s_common.Contains(raw)) set.Add(raw);
            return set;
        }

        /// <summary>Most of the shorter sentence's words are in the other one.</summary>
        private static bool NearDouble(HashSet<string> a, HashSet<string> b)
        {
            var small = a.Count <= b.Count ? a : b; var big = small == a ? b : a;
            if (small.Count < 3) return false;
            int shared = 0;
            foreach (var x in small) if (big.Contains(x)) shared++;
            return shared * 10 >= small.Count * 8;
        }

        /// <summary>The line for the HUD: where Mira is, and the newest thing in the journal.</summary>
        public static string HudLine(WorldState s)
        {
            var journal = CampaignApi.Journal(s);
            string last = journal.Count > 0 ? journal[journal.Count - 1].Text : "";
            return CampaignApi.WhereIsMira(s) + (last.Length > 0 ? "  " + last : "");
        }

        // ---------------- drawing ----------------

        private void EnsureStyles()
        {
            if (_stylesFor == Screen.height && _text != null) return;
            _stylesFor = Screen.height;
            _title = new GUIStyle(UiStyle.Label) { fontSize = UiStyle.Px(26), fontStyle = FontStyle.Bold, wordWrap = false };
            _title.normal.textColor = UiStyle.AccentColor;
            _head = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(16), wordWrap = false };
            _head.normal.textColor = new Color(0.75f, 0.70f, 0.62f);
            _text = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(17), wordWrap = true, richText = true };
            _hud = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(19), wordWrap = true, richText = true };
        }

        private void OnGUI()
        {
            PanelRect = Rect.zero; HudLineShown = null;
            if (_boot == null || _boot.Sim == null || S.Campaign == null) return;
            EnsureStyles();
            if (MapScreen.IsOpen) { DrawOnMap(); return; }
            DrawHud();
        }

        private void DrawHud()
        {
            if (!Hud.Visible || Hud.MenuOpen) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            if (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God) return;
            GUI.depth = -5;
            string line = HudLine(S);
            float w = Mathf.Min(UiStyle.Px(620), Screen.width * 0.42f);
            float h = _hud.CalcHeight(new GUIContent(line), w - UiStyle.Px(28)) + UiStyle.Px(16);
            var r = new Rect(Screen.width - w - UiStyle.Px(20), UiStyle.Px(16), w, h);
            UiStyle.DrawPanel(r);
            GUI.Label(new Rect(r.x + UiStyle.Px(14), r.y + UiStyle.Px(8), w - UiStyle.Px(28), h), "<color=#F2B84D><b>Mira</b></color>  " + line.Substring(line.StartsWith("Mira ") ? 5 : 0), _hud);
            HudLineShown = line;
        }

        private void DrawOnMap()
        {
            var view = MapScreenView.Instance;
            if (view == null || view.MapRect.width <= 0) return;
            GUI.depth = -16; // over the map's own side text
            var map = view.MapRect;
            float x = map.xMax + UiStyle.Px(30), w = Screen.width - map.xMax - UiStyle.Px(60);
            if (w < UiStyle.Px(160)) return;
            // Folded: under the map's key (its six legend rows stay readable). Unfolded: the whole column.
            float top = Expanded ? map.y : map.y + UiStyle.Px(44 + 6 * 30 + 10);
            var r = new Rect(x - UiStyle.Px(12), top - UiStyle.Px(6), w + UiStyle.Px(24), map.yMax - top + UiStyle.Px(6));
            GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(0.05f, 0.06f, 0.08f, 1f)));
            PanelRect = r;
            float y = top;
            var titleRect = new Rect(x, y, w, UiStyle.Px(34));
            GUI.Label(titleRect, "Mira's journal", _title);
            GUI.Label(new Rect(x, y + UiStyle.Px(32), w, UiStyle.Px(22)), Expanded ? "tap here (or J) to fold it away" : "tap here (or J) for the whole journal", _head);
            var e = Event.current;
            if (e.type == EventType.MouseDown && new Rect(x, y, w, UiStyle.Px(56)).Contains(e.mousePosition)) { Expanded = !Expanded; e.Use(); }
            y += UiStyle.Px(60);
            var s = S;
            var body = new Rect(x, y, w, map.yMax - y);
            if (!Expanded)
            {
                // As many of the latest entries as fit.
                string text = CompactText(s, 0);
                for (int n = 5; n >= 0; n--)
                {
                    text = CompactText(s, n);
                    if (_text.CalcHeight(new GUIContent(text), body.width) <= body.height) break;
                }
                GUI.Label(body, text, _text);
                return;
            }
            string all = CompactText(s, 0) + "\n<b>The journal</b>\n" + FullEntries(s);
            float h = _text.CalcHeight(new GUIContent(all), body.width - UiStyle.Px(22));
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0, 0, body.width - UiStyle.Px(22), h));
            GUI.Label(new Rect(0, 0, body.width - UiStyle.Px(22), h), all, _text);
            GUI.EndScrollView();
        }

        /// <summary>Every journal entry, newest first, with its time since the expedition began.</summary>
        public static string FullEntries(WorldState s)
        {
            var c = s.Campaign;
            var journal = Distinct(CampaignApi.Journal(s));
            var sb = new StringBuilder();
            for (int i = journal.Count - 1; i >= 0; i--)
                sb.Append("<color=#BBB0A0>").Append(Clock(journal[i].Tick - c.StartTick)).Append("</color>  ").Append(journal[i].Text).Append('\n');
            return sb.ToString();
        }
    }
}
