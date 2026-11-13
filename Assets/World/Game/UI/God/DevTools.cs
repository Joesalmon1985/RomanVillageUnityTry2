using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// The F1 developer tools (REFACTOR_SPEC 2.C.2, 2.D, 8.2): God View camera and toolbar with Select, Build (plots),
    /// Road (hex edges), Camp (corners), City, Banish, Cast (any proposal, either people, from anywhere), Problems,
    /// Wares and Time (time scale, next roll, next demon). Every change goes through a sim command (the direct build
    /// commands of section 5.1, <c>CastSpell</c>, <c>Banish</c>) or through stepping the clock; nothing writes
    /// <c>WorldState</c>. AutoPlay scripts and tests call the static methods. Normal play never shows any of it.
    /// Owner: package P.
    /// </summary>
    public static class DevTools
    {
        /// <summary>Which people the build tools act for (0 your people, 1 the Ash Clan).</summary>
        public static int Faction;
        /// <summary>Building type the Build tool places.</summary>
        public static BuildingType BuildType = BuildingType.WoodcutterHut;
        /// <summary>Last command result shown by the tools.</summary>
        public static CommandResult LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Faction = PlayerIds.Human; BuildType = BuildingType.WoodcutterHut; LastResult = default; }

        private static WorldBootstrap Boot => WorldBootstrap.Instance;
        private static WorldState S => Boot != null ? Boot.Sim.State : null;

        // ---------------- commands (AutoPlay, tests, the toolbar) ----------------

        public static CommandResult Submit(Command c, string done)
        {
            var r = Boot != null ? Boot.Sim.Submit(c) : CommandResult.Reject("no_world", "There is no world.");
            LastResult = r;
            Hud.Notice(r.Ok ? done : r.Message, !r.Ok, 3f);
            return r;
        }

        public static CommandResult PlaceBuilding(BuildingType type, int plot, int faction = PlayerIds.Human) =>
            Submit(new PlaceBuilding { PlayerId = faction, Type = type, Plot = plot }, EconomyApi.Def(type)?.Name + " site placed on plot " + plot + ".");
        public static CommandResult PlaceRoad(int hexEdge, int faction = PlayerIds.Human) =>
            Submit(new PlaceRoad { PlayerId = faction, HexEdge = hexEdge }, "Road site placed on hex edge " + hexEdge + ".");
        public static CommandResult FoundCamp(int corner, int faction = PlayerIds.Human) =>
            Submit(new FoundSettlement { PlayerId = faction, Corner = corner }, "Camp site placed on corner " + corner + ".");
        public static CommandResult UpgradeCity(int corner, int faction = PlayerIds.Human) =>
            Submit(new UpgradeCity { PlayerId = faction, Corner = corner }, "City upgrade started at corner " + corner + ".");
        public static CommandResult Banish(DemonSiteKind kind, int id)
        {
            int f = PlayerIds.Human;
            if (S != null) foreach (var d in DemonApi.DemonSites(S)) if (d.Kind == kind && d.Id == id) { f = d.Faction; break; }
            return Submit(new Banish { PlayerId = f < 0 ? PlayerIds.Human : f, SiteKind = kind, SiteId = id }, "Demons banished from " + (S != null ? DemonApi.SiteName(S, kind, id) : "there") + ".");
        }
        /// <summary>Casts any open proposal (either people) from anywhere.</summary>
        public static CommandResult Cast(int proposalId)
        {
            var r = SpellCaster.CastAnywhere(proposalId);
            LastResult = r;
            Hud.Notice(r.Ok ? "Cast." : r.Message, !r.Ok, 3f);
            return r;
        }
        /// <summary>Casts the open proposal queued at a rank for a people (0 = top).</summary>
        public static CommandResult CastQueued(int faction = PlayerIds.Human, int rank = 0)
        {
            var list = FactionApi.Proposals(S, faction);
            if (rank < 0 || rank >= list.Count) { LastResult = CommandResult.Reject("unknown_proposal", "That proposal is gone."); return LastResult; }
            return Cast(list[rank].Id);
        }
        public static CommandResult Decline(int proposalId) =>
            Submit(new DeclineProposal { PlayerId = PlayerIds.Human, ProposalId = proposalId }, "Declined.");

        /// <summary>Game time runs this many times faster (Time.timeScale; the sim clock follows scaled time).</summary>
        public static void SetTimeScale(float scale) { Time.timeScale = Mathf.Clamp(scale, 0f, 16f); Hud.Notice("Time x" + Time.timeScale, false, 2f); }

        /// <summary>Steps the clock until the next dice roll lands (a forced roll; the dice stay the sim's). Returns the sum, or -1.</summary>
        public static int ForceRoll()
        {
            var s = S; if (s == null) return -1;
            int before = s.Economy.Dice.RollCount;
            int guard = s.Economy.Tuning.RollIntervalTicks + 2;
            while (s.Economy.Dice.RollCount == before && guard-- > 0) Boot.Clock.StepOnce();
            if (s.Economy.Dice.RollCount == before) return -1;
            var r = EconomyApi.LastRoll(s);
            Hud.Notice("Rolled " + r.Sum + ".", false, 2f);
            return r.Sum;
        }

        /// <summary>
        /// Steps the clock until a demon is placed (the next 7 after the grace period), at most <paramref name="maxTicks"/>.
        /// The sim has no command that places a demon directly, so this is time travel, not a spawn. True if one came.
        /// </summary>
        public static bool SkipToDemon(int maxTicks = 9000)
        {
            var s = S; if (s == null || s.Demons == null) return false;
            int before = s.Demons.Placed;
            while (s.Demons.Placed == before && maxTicks-- > 0 && !FactionApi.Outcome(s).GameOver) Boot.Clock.StepOnce();
            bool ok = s.Demons.Placed != before;
            Hud.Notice(ok ? "A demon came at tick " + s.Tick + "." : "No demon came.", !ok, 3f);
            return ok;
        }

        // Optional developer commands: if package S adds them to World.Sim (no WorldState writes here), the tools use them.
        private static Type DevType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "World.Sim") continue;
                foreach (var t in asm.GetTypes()) if (t.Name == name && typeof(Command).IsAssignableFrom(t)) return t;
            }
            return null;
        }
        private static Command MakeDev(string name, params (string field, object value)[] fields)
        {
            var t = DevType(name);
            if (t == null) return null;
            var c = (Command)Activator.CreateInstance(t);
            foreach (var (f, v) in fields)
            {
                var fi = t.GetField(f);
                if (fi == null) continue;
                fi.SetValue(c, fi.FieldType.IsEnum ? Enum.ToObject(fi.FieldType, Convert.ToInt32(v)) : Convert.ChangeType(v, fi.FieldType));
            }
            return c;
        }
        /// <summary>True when the sim offers a <c>DevAddWares</c> command (S); the Wares tool is greyed out otherwise.</summary>
        public static bool CanAddWares => DevType("DevAddWares") != null;
        /// <summary>True when the sim offers a <c>DevPlaceDemon</c> command (S).</summary>
        public static bool CanPlaceDemon => DevType("DevPlaceDemon") != null;

        /// <summary>Adds wares to a settlement's storehouse through the sim's <c>DevAddWares{Settlement, Ware, Count}</c>, if it exists.</summary>
        public static CommandResult AddWares(int settlementId, Ware ware, int count)
        {
            var c = MakeDev("DevAddWares", ("Settlement", settlementId), ("Ware", (int)ware), ("Count", count));
            if (c == null) { LastResult = CommandResult.Reject("no_dev_command", "The sim has no DevAddWares command yet."); Hud.Notice(LastResult.Message, true); return LastResult; }
            c.PlayerId = S.Economy.Settlements[settlementId].Faction;
            return Submit(c, "Added " + count + " " + EconomyApi.WareName(ware) + ".");
        }

        /// <summary>Places a demon through the sim's <c>DevPlaceDemon{SiteKind, SiteId}</c>, if it exists; else skips to the next demon.</summary>
        public static CommandResult SpawnDemon(DemonSiteKind kind, int id)
        {
            var c = MakeDev("DevPlaceDemon", ("SiteKind", (int)kind), ("SiteId", id));
            if (c != null) return Submit(c, "A demon sits on " + DemonApi.SiteName(S, kind, id) + ".");
            bool ok = SkipToDemon();
            LastResult = ok ? CommandResult.Accept() : CommandResult.Reject("no_demon", "No demon came.");
            return LastResult;
        }

        // ---------------- picking helpers ----------------

        /// <summary>Plots of the people's finished camps (ascending).</summary>
        public static List<int> PlotsOfFaction(WorldState s, int faction)
        {
            var l = new List<int>();
            foreach (var st in EconomyApi.Settlements(s))
                if (st.Faction == faction && st.State == SettlementState.Active) l.AddRange(EconomyApi.PlotsOf(s, st.Id));
            return l;
        }

        public static Vector3 PlotPos(WorldState s, int plot) =>
            new Vector3((float)PlotLayout.PlotX(s.Board.Topology, plot), BoardSpace.GroundY, (float)PlotLayout.PlotZ(s.Board.Topology, plot));

        // ---------------- installation ----------------

        private sealed class Overlay
        {
            public long Tick = -1; public string Key;
            public readonly List<GodOverlay.Spot> Spots = new List<GodOverlay.Spot>();
            public readonly List<GodOverlay.Bar> Bars = new List<GodOverlay.Bar>();
            public readonly List<int> Ids = new List<int>();
        }
        private static readonly Overlay _ov = new Overlay();

        public static void Register()
        {
            GodToolbar.Register(new GodTool { Id = GodToolbar.SelectId, Label = "Select", Order = 0, Hotkey = KeyCode.Q, PanelWidth = 0,
                TipText = () => "Click anything to see what it is (panel on the right)." });
            GodToolbar.Register(Placer("build", "Build", 10, KeyCode.B, BuildPanel, BuildRefresh, BuildClick));
            GodToolbar.Register(Placer("road", "Road", 20, KeyCode.R, FactionPanel("Road: click a green hex edge."), RoadRefresh, RoadClick));
            GodToolbar.Register(Placer("camp", "Camp", 30, KeyCode.C, FactionPanel("Camp: click a green corner."), CampRefresh, CampClick));
            GodToolbar.Register(Placer("city", "City", 40, KeyCode.Y, FactionPanel("City: click one of the people's camps (green)."), CityRefresh, CityClick));
            GodToolbar.Register(Placer("banish", "Banish", 50, KeyCode.X, (t, a) => Text(a, "Banish: click a demon site (purple)."), BanishRefresh, BanishClick));
            GodToolbar.Register(new GodTool { Id = "cast", Label = "Cast", Order = 60, Hotkey = KeyCode.P, PanelWidth = 620, DrawPanel = CastPanel,
                BadgeText = () => S != null ? FactionApi.Proposals(S, PlayerIds.Human).Count.ToString() : null });
            GodToolbar.Register(new GodTool { Id = "problems", Label = "Problems", Order = 70, Hotkey = KeyCode.O, PanelWidth = 620, DrawPanel = ProblemsPanel,
                BadgeText = () => { int n = Problems(S).Count; return n > 0 ? n.ToString() : null; } });
            GodToolbar.Register(new GodTool { Id = "wares", Label = "Wares", Order = 80, Hotkey = KeyCode.U, PanelWidth = 620, DrawPanel = WaresPanel });
            GodToolbar.Register(new GodTool { Id = "time", Label = "Time", Order = 90, Hotkey = KeyCode.T, PanelWidth = 520, DrawPanel = TimePanel });
        }

        private static GodTool Placer(string id, string label, int order, KeyCode key, Action<GodTool, Rect> panel, Action<WorldState> refresh, Action<WorldState, Vector3> click)
        {
            return new GodTool
            {
                Id = id, Label = label, Order = order, Hotkey = key, CapturesClicks = true, PanelWidth = 470, DrawPanel = panel,
                OnActivate = t => { _ov.Tick = -1; },
                OnDeactivate = t => GodOverlay.Hide(),
                OnUpdate = t =>
                {
                    var s = S; if (s == null) return;
                    string k = id + "|" + Faction + "|" + BuildType;
                    if (s.Tick != _ov.Tick || k != _ov.Key)
                    {
                        _ov.Tick = s.Tick; _ov.Key = k; _ov.Spots.Clear(); _ov.Bars.Clear(); _ov.Ids.Clear();
                        refresh(s);
                        GodOverlay.Show(_ov.Spots, _ov.Bars);
                    }
                },
                OnWorldClick = t => { if (S != null && BoardPicker.GroundPointUnderMouse(out var p)) { click(S, p); _ov.Tick = -1; } },
            };
        }

        private static Color Ok(CommandResult r) => r.Ok ? GodOverlay.Valid : GodOverlay.Invalid;

        private static void BuildRefresh(WorldState s)
        {
            foreach (int p in PlotsOfFaction(s, Faction))
                _ov.Spots.Add(new GodOverlay.Spot(p, PlotPos(s, p), Ok(EconomyApi.CheckBuilding(s, Faction, BuildType, p)), 4f));
        }
        private static void BuildClick(WorldState s, Vector3 g)
        {
            int best = -1; float bd = 6f;
            foreach (int p in PlotsOfFaction(s, Faction)) { float d = BoardSpace.Flat(PlotPos(s, p) - g); if (d < bd) { bd = d; best = p; } }
            if (best >= 0) PlaceBuilding(BuildType, best, Faction);
        }
        private static void RoadRefresh(WorldState s)
        {
            var t = s.Board.Topology;
            for (int e = 0; e < t.HexEdgeCount; e++)
            {
                if (!EconomyApi.CheckRoad(s, Faction, e).Ok) continue;
                _ov.Bars.Add(new GodOverlay.Bar(e, BoardSpace.Vertex(t, t.HexEdgeCornerA(e)), BoardSpace.Vertex(t, t.HexEdgeCornerB(e)), GodOverlay.Valid, 3f));
            }
        }
        private static void RoadClick(WorldState s, Vector3 g) { int e = BoardSpace.NearestHexEdge(g, 8f); if (e >= 0) PlaceRoad(e, Faction); }
        private static void CampRefresh(WorldState s)
        {
            var t = s.Board.Topology;
            for (int c = 0; c < t.CornerCount; c++)
                if (EconomyApi.CheckSettlement(s, Faction, c).Ok) _ov.Spots.Add(new GodOverlay.Spot(c, BoardSpace.Vertex(t, c), GodOverlay.Valid, 5f));
        }
        private static void CampClick(WorldState s, Vector3 g) { int c = BoardSpace.NearestCorner(g, 10f); if (c >= 0) FoundCamp(c, Faction); }
        private static void CityRefresh(WorldState s)
        {
            foreach (var st in EconomyApi.Settlements(s))
                if (st.Faction == Faction && st.State == SettlementState.Active)
                    _ov.Spots.Add(new GodOverlay.Spot(st.Corner, BoardSpace.Vertex(s.Board.Topology, st.Corner), Ok(EconomyApi.CheckCity(s, Faction, st.Corner)), 6f));
        }
        private static void CityClick(WorldState s, Vector3 g) { int c = BoardSpace.NearestCorner(g, 10f); if (c >= 0) UpgradeCity(c, Faction); }
        private static void BanishRefresh(WorldState s)
        {
            var purple = new Color(0.65f, 0.2f, 0.85f, 1f);
            foreach (var d in DemonApi.DemonSites(s))
                if (d.Count > 0) _ov.Spots.Add(new GodOverlay.Spot((int)d.Kind * 1000 + d.Id, new Vector3((float)d.X, 0, (float)d.Z), purple, 4f + d.Count));
        }
        private static void BanishClick(WorldState s, Vector3 g)
        {
            DemonSiteView? best = null; float bd = 12f;
            foreach (var d in DemonApi.DemonSites(s))
            {
                if (d.Count <= 0) continue;
                float dist = BoardSpace.Flat(new Vector3((float)d.X, 0, (float)d.Z) - g);
                if (dist < bd) { bd = dist; best = d; }
            }
            if (best.HasValue) Banish(best.Value.Kind, best.Value.Id);
        }

        // ---------------- panels ----------------

        private static GUIStyle _small, _head;
        private static void Styles()
        {
            if (_small != null && _small.fontSize == UiStyle.Px(19)) return;
            _small = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(19), wordWrap = true, richText = true };
            _head = new GUIStyle(_small) { fontStyle = FontStyle.Bold };
            _head.normal.textColor = UiStyle.AccentColor;
        }

        private static void Text(Rect a, string text)
        {
            Styles();
            GUI.Label(new Rect(a.x + UiStyle.Px(14), a.y + UiStyle.Px(10), a.width - UiStyle.Px(28), a.height - UiStyle.Px(20)), text, _small);
        }

        private static float FactionRow(Rect a, float y)
        {
            float bw = (a.width - UiStyle.Px(40)) / 2, bh = UiStyle.Px(40);
            if (GUI.Button(new Rect(a.x + UiStyle.Px(14), y, bw, bh), Faction == PlayerIds.Human ? "<b>Your people</b>" : "Your people", UiStyle.Button)) Faction = PlayerIds.Human;
            if (GUI.Button(new Rect(a.x + UiStyle.Px(26) + bw, y, bw, bh), Faction == PlayerIds.Rival ? "<b>Ash Clan</b>" : "Ash Clan", UiStyle.Button)) Faction = PlayerIds.Rival;
            return y + bh + UiStyle.Px(8);
        }

        private static Action<GodTool, Rect> FactionPanel(string text) => (t, a) =>
        {
            float y = FactionRow(a, a.y + UiStyle.Px(10));
            Text(new Rect(a.x, y, a.width, a.yMax - y), text + (LastResult.Ok || LastResult.Message == null ? "" : "\n\n<color=#FF9A80>" + LastResult.Message + "</color>"));
        };

        private static void BuildPanel(GodTool t, Rect a)
        {
            Styles();
            float y = FactionRow(a, a.y + UiStyle.Px(10));
            GUI.Label(new Rect(a.x + UiStyle.Px(14), y, a.width, UiStyle.Px(28)), "Click a green plot to place:", _head); y += UiStyle.Px(32);
            float bh = UiStyle.Px(34);
            foreach (var def in BuildingDefs.All)
            {
                if (!def.Placeable) continue;
                if (y + bh > a.yMax - UiStyle.Px(60)) break;
                bool on = def.Type == BuildType;
                if (GUI.Button(new Rect(a.x + UiStyle.Px(14), y, a.width - UiStyle.Px(28), bh), (on ? "<b>> " : "") + def.Name + "  <size=" + UiStyle.Px(15) + ">" + EconomyApi.CostText(def.Cost) + "</size>" + (on ? "</b>" : ""), new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(18), richText = true, alignment = TextAnchor.MiddleLeft }))
                    BuildType = def.Type;
                y += bh + UiStyle.Px(3);
            }
            if (!LastResult.Ok && LastResult.Message != null)
                GUI.Label(new Rect(a.x + UiStyle.Px(14), a.yMax - UiStyle.Px(56), a.width - UiStyle.Px(28), UiStyle.Px(50)), "<color=#FF9A80>" + LastResult.Message + "</color>", _small);
        }

        private static void CastPanel(GodTool t, Rect a)
        {
            Styles();
            var s = S; if (s == null) return;
            float y = a.y + UiStyle.Px(10), x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28);
            for (int f = 0; f < PlayerIds.Count; f++)
            {
                GUI.Label(new Rect(x, y, w, UiStyle.Px(28)), f == PlayerIds.Human ? "Your people's proposals" : "The Ash Clan's proposals", _head); y += UiStyle.Px(30);
                var list = FactionApi.Proposals(s, f);
                if (list.Count == 0) { GUI.Label(new Rect(x, y, w, UiStyle.Px(26)), "<i>none</i>", _small); y += UiStyle.Px(30); }
                foreach (var p in list)
                {
                    string line = (p.Rank + 1) + ". " + FactionApi.ProposalTitle(s, p.Id) + "  <color=#BBB0A0>#" + p.Id + " " + p.Speaker + " " + p.Score + "</color>";
                    float lh = _small.CalcHeight(new GUIContent(line), w - UiStyle.Px(190));
                    GUI.Label(new Rect(x, y, w - UiStyle.Px(190), lh), line, _small);
                    var bs = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(18), padding = new RectOffset(2, 2, 2, 2) };
                    if (GUI.Button(new Rect(x + w - UiStyle.Px(180), y, UiStyle.Px(85), UiStyle.Px(34)), "Cast", bs)) Cast(p.Id);
                    if (f == PlayerIds.Human && GUI.Button(new Rect(x + w - UiStyle.Px(90), y, UiStyle.Px(90), UiStyle.Px(34)), "Decline", bs)) Decline(p.Id);
                    y += Mathf.Max(lh, UiStyle.Px(36)) + UiStyle.Px(4);
                    GUI.Label(new Rect(x + UiStyle.Px(16), y, w - UiStyle.Px(16), UiStyle.Px(44)), "<color=#CFC6B8>" + p.Reason + "</color>", _small);
                    y += _small.CalcHeight(new GUIContent(p.Reason ?? ""), w - UiStyle.Px(16)) + UiStyle.Px(8);
                }
                y += UiStyle.Px(8);
            }
            if (!LastResult.Ok && LastResult.Message != null)
                GUI.Label(new Rect(x, Mathf.Min(y, a.yMax - UiStyle.Px(50)), w, UiStyle.Px(46)), "<color=#FF9A80>" + LastResult.Message + "</color>", _small);
        }

        /// <summary>Unfinished sites waiting for something, and stalled buildings: one sentence each.</summary>
        public static List<string> Problems(WorldState s)
        {
            var l = new List<string>();
            if (s?.Economy == null) return l;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Faction != Faction) continue;
                if (b.State == BuildingState.Site) { var w = EconomyApi.WaitingSentence(s, SiteKind.Building, b.Id); if (w != null) l.Add(w); }
                else if (b.State == BuildingState.Active && b.Reason != null) l.Add(EconomyApi.DescribeBuilding(s, b.Id).Status);
            }
            foreach (var r in s.Economy.Roads)
                if (r.Faction == Faction && r.State == RoadState.Site) { var w = EconomyApi.WaitingSentence(s, SiteKind.Road, r.Id); if (w != null) l.Add(w); }
            foreach (var st in s.Economy.Settlements)
            {
                if (st.Faction != Faction) continue;
                if (st.State == SettlementState.Site) { var w = EconomyApi.WaitingSentence(s, SiteKind.Settlement, st.Id); if (w != null) l.Add(w); }
                if (st.Upgrade != UpgradeState.None) { var w = EconomyApi.WaitingSentence(s, SiteKind.City, st.Id); if (w != null) l.Add(w); }
            }
            return l;
        }

        private static void ProblemsPanel(GodTool t, Rect a)
        {
            Styles();
            var s = S; if (s == null) return;
            float y = FactionRow(a, a.y + UiStyle.Px(10)), x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28);
            var list = Problems(s);
            if (list.Count == 0) GUI.Label(new Rect(x, y, w, UiStyle.Px(28)), "Nothing is stuck.", _small);
            foreach (var p in list)
            {
                if (y > a.yMax - UiStyle.Px(30)) break;
                float h = _small.CalcHeight(new GUIContent(p), w);
                GUI.Label(new Rect(x, y, w, h), p, _small); y += h + UiStyle.Px(6);
            }
        }

        private static void WaresPanel(GodTool t, Rect a)
        {
            Styles();
            var s = S; if (s == null) return;
            float y = a.y + UiStyle.Px(10), x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28);
            foreach (var st in EconomyApi.Settlements(s))
            {
                if (st.State != SettlementState.Active) continue;
                if (y > a.yMax - UiStyle.Px(60)) break;
                GUI.Label(new Rect(x, y, w, UiStyle.Px(28)), char.ToUpper(EconomyApi.SettlementName(s, st.Id)[0]) + EconomyApi.SettlementName(s, st.Id).Substring(1) + (st.IsCity ? " (city)" : ""), _head); y += UiStyle.Px(28);
                var parts = new List<string>();
                foreach (var ware in Wares.All) { int n = EconomyApi.StockOf(s, st.Id, ware); if (n > 0) parts.Add(EconomyApi.WareTitle(ware) + " " + n); }
                string line = parts.Count > 0 ? string.Join(", ", parts) : "<i>empty</i>";
                float h = _small.CalcHeight(new GUIContent(line), w);
                GUI.Label(new Rect(x, y, w, h), line, _small); y += h + UiStyle.Px(4);
                if (CanAddWares && GUI.Button(new Rect(x, y, UiStyle.Px(200), UiStyle.Px(32)), "+4 of each raw", UiStyle.Button))
                    foreach (var ware in Wares.Raw) AddWares(st.Id, ware, 4);
                if (CanAddWares) y += UiStyle.Px(36);
                y += UiStyle.Px(6);
            }
            if (!CanAddWares)
                GUI.Label(new Rect(x, a.yMax - UiStyle.Px(60), w, UiStyle.Px(56)), "<color=#BBB0A0>Adding wares needs a sim developer command (DevAddWares); the sim has none yet.</color>", _small);
        }

        private static void TimePanel(GodTool t, Rect a)
        {
            Styles();
            var s = S; if (s == null) return;
            float y = a.y + UiStyle.Px(10), x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), bh = UiStyle.Px(40);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(28)), "Time x" + Time.timeScale.ToString("0.##") + "   tick " + s.Tick, _head); y += UiStyle.Px(34);
            float[] scales = { 0f, 0.5f, 1f, 2f, 4f, 8f };
            float bw = (w - UiStyle.Px(5) * (scales.Length - 1)) / scales.Length;
            for (int i = 0; i < scales.Length; i++)
                if (GUI.Button(new Rect(x + i * (bw + UiStyle.Px(5)), y, bw, bh), scales[i] == 0 ? "||" : "x" + scales[i], UiStyle.Button)) SetTimeScale(scales[i]);
            y += bh + UiStyle.Px(12);
            if (GUI.Button(new Rect(x, y, w, bh), "Roll now (skip to the next roll)", UiStyle.Button)) ForceRoll();
            y += bh + UiStyle.Px(8);
            if (GUI.Button(new Rect(x, y, w, bh), CanPlaceDemon ? "Spawn a demon at the home camp" : "Next demon (skip to the next 7)", UiStyle.Button))
            {
                if (CanPlaceDemon) { var home = EconomyApi.SettlementAt(s, s.Board.StartCorners[0]); if (home != null) SpawnDemon(DemonSiteKind.Corner, home.Corner); }
                else SkipToDemon();
            }
            y += bh + UiStyle.Px(12);
            var roll = EconomyApi.LastRoll(s);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(80)), "Last roll " + (s.Economy.Dice.RollCount > 0 ? roll.Sum.ToString() : "-") + ", next in " + EconomyApi.TicksToNextRoll(s) + " ticks.\n" + FactionApi.Headline(s), _small);
        }
    }
}
