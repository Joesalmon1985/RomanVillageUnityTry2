using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>One object on the altar ledge as a world action: its screen rect (for the ledger to unfold from), its words, and, when it does something, its two-step preview and command.</summary>
    public sealed class LedgeObject : IWorldAction, IScreenAnchored
    {
        public readonly string Id, Zone;
        public Rect GuiRect;
        public Func<LedgerContent> Content;
        public string Question;
        public Func<WorldActionCheck> CheckFn;
        public Action<bool> PreviewFn;
        public Func<bool> CommitFn;
        public GameObject Proxy;

        public LedgeObject(string id, string zone) { Id = id; Zone = zone; }

        public string Title { get { var c = Content != null ? Content() : null; return c != null ? c.Title : Id; } }
        public IEnumerable<string> Lines { get { var c = Content != null ? Content() : null; return c != null ? c.Lines : new List<string>(); } }
        public string PreviewLine => Question;
        public WorldActionCheck Check() => CheckFn != null ? CheckFn() : WorldActionCheck.Allowed;
        public void ShowPreview(bool on) { PreviewFn?.Invoke(on); }
        public bool Commit() => CommitFn != null && CommitFn();
        public bool TryGetScreenRect(out Rect gui) { gui = GuiRect; return GuiRect.width > 0f; }
    }

    /// <summary>An inspect-only view of a ledge object: a tap opens its ledger tablet and sends nothing (an <see cref="IWorldAction"/> would preview).</summary>
    public sealed class LedgeNote : IInspectable, IScreenAnchored
    {
        public readonly LedgeObject Src;
        public LedgeNote(LedgeObject src) { Src = src; }
        public string Title => Src.Title;
        public IEnumerable<string> Lines => Src.Lines;
        public bool TryGetScreenRect(out Rect gui) => Src.TryGetScreenRect(out gui);
    }

    /// <summary>Sits on the proxy object of a ledge object so zone tools (the F1 outline) and tests can read its screen rect.</summary>
    public sealed class LedgeProxy : MonoBehaviour
    {
        public LedgeObject Object;
        public Func<Rect> RectFn;
        public bool TryGetScreenRect(out Rect gui) { gui = RectFn != null ? RectFn() : default; return gui.width > 0f; }
    }

    /// <summary>
    /// The altar ledge (WU-2, WORLD_UI_PLAN 2.2): a carved stone band at the bottom of the frame, inside the safe area, holding the
    /// edge functions as objects: five offering baskets (the hand), the builder's bench (build modes, spare pieces), four banner
    /// stakes (points, longest road, war), the trader and the barter mat, envoys (AI offers), the discard sack, the turn hourglass,
    /// the chronicler, the prince, the demon-ward totem and the lamb stone. Drawn in the same IMGUI layer as the Clear view HUD
    /// from the same <see cref="GodBoardViewModel"/>; every object carries its <c>HEX-*</c> zone id and goes through the shared
    /// <see cref="WorldActions"/> machine (tap once to preview, again to confirm; the ledger tablet gives exact counts and words).
    /// No digits sit on the objects: fullness, stacks and stones show the state, the tablet gives the number. No dice, no odds.
    /// </summary>
    public sealed partial class GodBoardHud
    {
        private readonly Dictionary<string, LedgeObject> _ledge = new Dictionary<string, LedgeObject>();
        private readonly Dictionary<string, Rect> _ledgeRects = new Dictionary<string, Rect>();
        private Transform _ledgeRoot;
        private bool _tipped;
        private float _shakeUntil, _refusedUntil;
        private BuildKind _refusedKind = BuildKind.None;
        private Rect _band, _mat;
        private float _lu = 1f;

        /// <summary>The ledge band's rect in GUI pixels (top-left origin) from the last layout.</summary>
        public Rect LedgeBand => _band;
        public Rect LedgeRect(string id) => _ledgeRects.TryGetValue(id, out var r) ? r : default;
        public IReadOnlyDictionary<string, Rect> LedgeRects => _ledgeRects;
        public IReadOnlyDictionary<string, LedgeObject> LedgeObjects => _ledge;
        /// <summary>The barter mat is unrolled (the trader was tapped) and showing the bank rows.</summary>
        public bool MatOpen => TradeOpen && TradeTab == 0;
        public bool HourglassTipped => _tipped;

        private static readonly Color StoneDark = new Color(0.19f, 0.17f, 0.15f, 1f);
        private static readonly Color StoneMid = new Color(0.30f, 0.27f, 0.23f, 1f);
        private static readonly Color StoneLit = new Color(0.47f, 0.42f, 0.34f, 1f);
        private static readonly Color Wicker = new Color(0.55f, 0.38f, 0.18f, 1f);

        // ---------------------------------------------------------------- identity of the objects

        private LedgeObject Obj(string id, string zone)
        {
            if (_ledge.TryGetValue(id, out var o)) return o;
            o = new LedgeObject(id, zone);
            if (_ledgeRoot == null) { _ledgeRoot = new GameObject("AltarLedge").transform; _ledgeRoot.SetParent(transform, false); }
            o.Proxy = new GameObject("Ledge " + id);
            o.Proxy.transform.SetParent(_ledgeRoot, false);
            WorldZone.Tag(o.Proxy, zone);
            var lp = o.Proxy.AddComponent<LedgeProxy>(); lp.Object = o;
            string key = id;
            lp.RectFn = () => o.GuiRect.width > 0f ? o.GuiRect : (key == "baskets" ? LedgeRect("basket:2") : key == "bench" ? LedgeRect("bench:1") : key == "stakes" || key == "stakes-turn" ? LedgeRect("stake:1") : LedgeRect(key));
            _ledge[id] = o;
            return o;
        }

        /// <summary>Lays out the ledge for this screen and the current state: fills <see cref="LedgeRects"/> and the band.</summary>
        public void LayoutLedge()
        {
            _u = Mathf.Max(0.6f, Screen.height / 390f);
            var safe = ScreenSafeArea.Current();
            float wd = safe.width / _u;
            float k = Mathf.Clamp(wd / 620f, 0.65f, 1f);
            _lu = _u * k;
            float row = 44f * _lu, gap = 4f * _lu;
            float bandH = 2f * row + 3f * gap + 4f * _lu;
            _band = new Rect(safe.x, safe.yMax - bandH, safe.width, bandH);
            _ledgeRects.Clear();
            Obj("baskets", WorldZones.HexStore); Obj("bench", WorldZones.HexBuild); Obj("stakes", WorldZones.HexAwards); Obj("stakes-turn", WorldZones.HexTurn);
            Obj("trader", WorldZones.HexTrade); Obj("hourglass", WorldZones.HexEnd); Obj("chronicler", WorldZones.HexFeed); Obj("prince", WorldZones.HexDev);
            Obj("totem", WorldZones.HexPlague); Obj("lamb", WorldZones.HexLeave); Obj("sack", WorldZones.HexRaider);
            float y1 = _band.y + 4f * _lu + gap, y2 = y1 + row + gap;
            float x = _band.x + 6f * _lu;
            for (int g = 0; g < Wares.RawCount; g++) { _ledgeRects["basket:" + g] = new Rect(x, y1, row, row); x += row; }
            x += 8f * _lu;
            for (int i = 0; i < 3; i++) { _ledgeRects["bench:" + i] = new Rect(x, y1, row, row); x += row; }
            x += 8f * _lu;
            var vm = _d.Model;
            for (int p = 0; p < vm.PeopleCount && p < 4; p++) { _ledgeRects["stake:" + p] = new Rect(x, y1, row, row); x += row; }
            _ledgeRects["hourglass"] = new Rect(_band.xMax - 6f * _lu - 64f * _lu, _band.y + 4f * _lu + gap, 64f * _lu, 2f * row + gap);
            // second row: trader, sack, envoys on the left; chronicler, prince, totem, lamb on the right
            float x2 = _band.x + 6f * _lu;
            _ledgeRects["trader"] = new Rect(x2, y2, row, row); x2 += row + gap;
            if (vm.MustDiscard)
            {
                _ledgeRects["sack"] = new Rect(x2, y2, row, row); x2 += row + gap;
                _ledgeRects["sack-clear"] = new Rect(x2, y2, row, row); x2 += row + gap;
            }
            int shown = Mathf.Min(2, vm.Incoming.Count);
            float ew = 120f * _lu;
            for (int i = 0; i < shown; i++)
            {
                int id = vm.Incoming[i].Id;
                _ledgeRects["envoy:" + id] = new Rect(x2, y2, ew - row, row);
                _ledgeRects["wave:" + id] = new Rect(x2 + ew - row, y2, row, row);
                x2 += ew + gap;
            }
            float xr = _ledgeRects["hourglass"].x - 8f * _lu - 4f * (row + gap) + gap;
            string[] right = { "chronicler", "prince", "totem", "lamb" };
            for (int i = 0; i < 4; i++) _ledgeRects[right[i]] = new Rect(xr + i * (row + gap), y2, row, row);
            float mw = Mathf.Min(430f * _lu, safe.width - 12f * _lu), mh = 132f * _lu;
            _mat = new Rect(safe.center.x - mw / 2f, _band.y - mh - 6f * _lu, mw, mh);
        }

        // ---------------------------------------------------------------- the taps (what the objects do)

        private void RefusedBuild(BuildKind k)
        {
            _refusedKind = k; _refusedUntil = Time.unscaledTime + 3f; _shakeUntil = Time.unscaledTime + 0.6f;
        }

        private LedgerContent LedgerOf(string what)
        {
            var vm = _d.FreshModel();
            switch (what)
            {
                case "baskets": return GodBoardLedger.Baskets(vm);
                case "bench": return GodBoardLedger.Bench(vm);
                case "stakes": return GodBoardLedger.Standings(vm);
                case "trader": return GodBoardLedger.Trader(vm);
                case "hourglass": return GodBoardLedger.Hourglass(vm);
                case "chronicler": { var l = new List<string>(); foreach (var f in _d.Feed) l.Add(f.Text); return GodBoardLedger.Chronicler(l); }
                case "prince": return GodBoardLedger.Prince(GodBoardDirector.AdviserOn, _d.LastHintLine);
                case "totem": return GodBoardLedger.Totem(PlagueIsOn != null && PlagueIsOn());
                case "sack": return GodBoardLedger.Sack(vm, DiscardChosen);
            }
            return new LedgerContent { Title = what };
        }

        private readonly Dictionary<string, LedgeNote> _notes = new Dictionary<string, LedgeNote>();

        private LedgeNote NoteOf(LedgeObject o)
        {
            if (!_notes.TryGetValue(o.Id, out var n)) { n = new LedgeNote(o); _notes[o.Id] = n; }
            return n;
        }

        private LedgeNote InspectObj(string id, string zone, string what, Rect rect)
        {
            var o = Obj(id, zone);
            o.GuiRect = rect;
            o.Content = () => LedgerOf(what);
            o.CheckFn = null; o.CommitFn = null; o.PreviewFn = null; o.Question = null;
            return NoteOf(o);
        }

        /// <summary>A tap on a ledge object by id (the pointer calls this for a press on its rect; tests and AutoPlay too).</summary>
        public WorldTapResult TapLedge(string id)
        {
            if (_d == null || _d.View != BoardView.Board || GameOverShown) return WorldTapResult.Ignored;
            if (_ledgeRects.Count == 0) LayoutLedge();
            var vm = _d.FreshModel();
            var rect = LedgeRect(id);
            if (id.StartsWith("basket:"))
            {
                int g = int.Parse(id.Substring(7));
                if (vm.MustDiscard) { EnsurePick(); AdjustDiscard(g, 1); WorldActions.Cancel(); return WorldTapResult.Ignored; }
                if (MatOpen && vm.CanAct) { SelectBankGive(g); return WorldTapResult.Ignored; }
                return WorldActions.Inspect(InspectObj("baskets", WorldZones.HexStore, "baskets", rect));
            }
            if (id.StartsWith("bench:"))
            {
                int k = int.Parse(id.Substring(6));
                var kind = GodBoardViewModel.Kinds[k];
                // The builder also confirms an open ghost piece (tap the spot again or the builder).
                if (WorldActions.State == WorldActionState.Preview && WorldActions.Machine.Current is BoardSpotAction open) return WorldActions.Tap(open);
                var kv = vm.Kind[k];
                bool setup = vm.Phase == TablePhase.Setup;
                var o = Obj("bench", WorldZones.HexBuild);
                o.GuiRect = rect;
                o.Content = () => { var c = GodBoardLedger.Bench(_d.FreshModel()); c.Title = kv.Name + ": the builder's bench"; return c; };
                o.PreviewFn = null; o.CommitFn = null;
                if (kv.Affordable)
                {
                    TradeOpen = false;
                    _d.SetBuildMode(kind);
                    return WorldActions.Inspect(NoteOf(o));
                }
                RefusedBuild(kind);
                string why = setup ? "Place your first camps and roads on the island." : GodBoardLedger.BuildRefusal(vm, kind);
                o.CheckFn = () => WorldActionCheck.Refuse(why);
                o.Question = null;
                var res = WorldActions.Tap(o);
                o.CheckFn = null;
                return res;
            }
            if (id.StartsWith("stake:")) return WorldActions.Inspect(InspectObj("stakes", WorldZones.HexAwards, "stakes", rect));
            switch (id)
            {
                case "trader":
                    if (TradeOpen) CloseTrade(); else { OpenTrade(); TradeTab = 0; }
                    WorldActions.Cancel();
                    return WorldTapResult.Inspected;
                case "chronicler": return WorldActions.Inspect(InspectObj("chronicler", WorldZones.HexFeed, "chronicler", rect));
                case "prince":
                {
                    bool again = LedgerCard.IsOpen && WorldActions.Machine.Current is LedgeNote lo && lo.Src.Id == "prince";
                    if (again) GodBoardDirector.AdviserOn = !GodBoardDirector.AdviserOn;
                    return WorldActions.Inspect(InspectObj("prince", WorldZones.HexDev, "prince", rect));
                }
                case "totem":
                {
                    if (PlagueToggle != null) PlagueToggle();
                    return WorldActions.Inspect(InspectObj("totem", WorldZones.HexPlague, "totem", rect));
                }
                case "hourglass":
                {
                    var o = Obj("hourglass", WorldZones.HexEnd);
                    o.GuiRect = rect;
                    o.Content = () => LedgerOf("hourglass");
                    o.Question = "End your turn?";
                    o.CheckFn = () =>
                    {
                        var m = _d.Model;
                        if (m.CanAct) return WorldActionCheck.Allowed;
                        if (m.Phase == TablePhase.Setup) return WorldActionCheck.Refuse(m.Current == m.Human ? "Place your camp and road first." : "The peoples are placing their camps.");
                        return WorldActionCheck.Refuse(m.Paused ? "The board is waiting." : "It is not your turn.");
                    };
                    o.PreviewFn = on => _tipped = on;
                    o.CommitFn = () => { var r = _d.EndTurn(); return r.Ok; };
                    return WorldActions.Tap(o);
                }
                case "lamb":
                {
                    var o = Obj("lamb", WorldZones.HexLeave);
                    o.GuiRect = rect;
                    o.Content = () => new LedgerContent { Title = "The lamb stone", Lines = { "The table waits and the sand freezes while you are away." } };
                    o.Question = "Return to the world?";
                    o.CheckFn = null; o.PreviewFn = null;
                    o.CommitFn = () => { _d.Leave(); return true; };
                    return WorldActions.Tap(o);
                }
                case "sack":
                {
                    var o = Obj("sack", WorldZones.HexRaider);
                    o.GuiRect = rect;
                    o.Content = () => LedgerOf("sack");
                    o.Question = "Give the sack?";
                    o.CheckFn = () =>
                    {
                        var m = _d.Model;
                        if (!m.MustDiscard) return WorldActionCheck.Refuse("Nothing is owed.");
                        int left = m.DiscardOwed - DiscardChosen;
                        return left == 0 ? WorldActionCheck.Allowed : WorldActionCheck.Refuse("Put " + left + " more " + GodBoardLedger.Plural1(left, "good", "goods") + " in the sack: tap a basket for each.");
                    };
                    o.PreviewFn = null;
                    o.CommitFn = () => DoDiscard().Ok;
                    return WorldActions.Tap(o);
                }
                case "sack-clear":
                    Array.Clear(DiscardPick, 0, DiscardPick.Length);
                    return WorldTapResult.Ignored;
            }
            if (id.StartsWith("envoy:"))
            {
                int oid = int.Parse(id.Substring(6));
                TradeOffer offer = null;
                foreach (var x in vm.Incoming) if (x.Id == oid) offer = x;
                if (offer == null) return WorldTapResult.Ignored;
                var o = Obj("envoy:" + oid, WorldZones.HexTrade);
                o.GuiRect = rect;
                o.Content = () => { var m = _d.FreshModel(); TradeOffer cur = null; foreach (var x in m.Incoming) if (x.Id == oid) cur = x; return cur != null ? GodBoardLedger.Envoy(m, cur) : new LedgerContent { Title = "The envoy has gone." }; };
                o.Question = "Accept this offer?";
                o.CheckFn = () =>
                {
                    var m = _d.Model; TradeOffer cur = null; foreach (var x in m.Incoming) if (x.Id == oid) cur = x;
                    if (cur == null) return WorldActionCheck.Refuse("The envoy has gone.");
                    return TableRules.CanPay(m.Hand, cur.Get) ? WorldActionCheck.Allowed : WorldActionCheck.Refuse("You cannot pay that: you lack " + GodBoardLedger.Goods(MissingFor(m.Hand, cur.Get)) + ".");
                };
                o.PreviewFn = null;
                o.CommitFn = () => AnswerOffer(oid, true).Ok;
                return WorldActions.Tap(o);
            }
            if (id.StartsWith("wave:"))
            {
                int oid = int.Parse(id.Substring(5));
                WorldActions.Cancel();
                AnswerOffer(oid, false);
                return WorldTapResult.Confirmed;
            }
            return WorldTapResult.Ignored;
        }

        private static int[] MissingFor(int[] hand, int[] cost)
        {
            var m = new int[Wares.RawCount];
            for (int g = 0; g < Wares.RawCount; g++) m[g] = Mathf.Max(0, cost[g] - hand[g]);
            return m;
        }

        // ---------------------------------------------------------------- drawing

        private void LedgeTap(string id, Rect r)
        {
            _rects.Add(r);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !_under) TapLedge(id);
            Texts.Add("[" + id + "]");
        }

        private void Tile(Rect r, Color tint)
        {
            FillNoHit(new Rect(r.x + 1f, r.y + 2f * _lu, r.width, r.height), new Color(0f, 0f, 0f, 0.4f));
            FillNoHit(r, tint);
            FillNoHit(new Rect(r.x, r.y, r.width, 2f * _lu), new Color(1f, 0.95f, 0.85f, 0.18f));
            FillNoHit(new Rect(r.x, r.yMax - 2f * _lu, r.width, 2f * _lu), new Color(0f, 0f, 0f, 0.25f));
        }

        private Rect In(Rect r, float dx, float dy, float w, float h) => new Rect(r.x + dx * _lu, r.y + dy * _lu, w * _lu, h * _lu);

        private void Ledge(WorldState s, TableState t, float W)
        {
            LayoutLedge();
            var vm = _d.Model;
            // the carved band
            _rects.Add(_band);
            var bleed = new Rect(0f, _band.y, Screen.width, Screen.height - _band.y);
            _rects.Add(bleed);
            FillNoHit(bleed, StoneDark);
            FillNoHit(new Rect(0f, _band.y, Screen.width, 4f * _lu), StoneLit);
            FillNoHit(new Rect(_band.x, _band.y, _band.width, 4f * _lu), StoneLit);
            FillNoHit(new Rect(_band.x, _band.y + 4f * _lu, _band.width, 2f * _lu), new Color(0.1f, 0.09f, 0.08f));
            for (float cx = _band.x + 22f * _lu; cx < _band.xMax; cx += 44f * _lu) FillNoHit(new Rect(cx, _band.y + 8f * _lu, _lu, _band.height - 12f * _lu), new Color(0f, 0f, 0f, 0.16f));
            Frame(_band, new Color(0.62f, 0.52f, 0.36f, 0.55f), Mathf.Max(1f, _lu));

            for (int g = 0; g < Wares.RawCount; g++) DrawBasket(g, vm);
            for (int k = 0; k < 3; k++) DrawBench(k, vm);
            for (int p = 0; p < vm.PeopleCount && p < 4; p++) DrawStake(p, vm);
            DrawTrader(vm);
            if (vm.MustDiscard) DrawSack(vm);
            DrawEnvoys(vm);
            DrawSimple("chronicler", GScroll, new Color(0.92f, 0.86f, 0.72f), StoneMid, null);
            DrawPrince();
            DrawTotem();
            DrawSimple("lamb", GHouse, new Color(0.96f, 0.93f, 0.85f), StoneMid, IconOf(3));
            DrawHourglass(vm);
            if (MatOpen && !GameOverShown) DrawMat(vm);
        }

        private void DrawBasket(int g, GodBoardViewModel vm)
        {
            var r = LedgeRect("basket:" + g);
            Tile(r, StoneMid);
            int n = vm.Hand[g];
            // wicker basket: three narrowing rows
            var body = In(r, 5, 22, 34, 17);
            FillNoHit(body, Wicker);
            for (int i = 0; i < 3; i++) FillNoHit(new Rect(body.x, body.y + (3f + i * 5f) * _lu, body.width, _lu), new Color(0.3f, 0.2f, 0.08f, 0.9f));
            FillNoHit(In(r, 3, 20, 38, 4), new Color(0.40f, 0.27f, 0.11f));
            // the goods in the basket: up to six pictures, fuller as the hand grows
            int show = Mathf.Min(6, n);
            for (int i = 0; i < show; i++)
            {
                float px = 5f + (i % 3) * 11f, py = 6f - (i / 3) * 0f + (i / 3) * 1f;
                var ir = In(r, px, 3f + (i / 3) * 7f, 15, 15);
                GUI.DrawTexture(ir, IconOf(g));
            }
            if (n == 0) FillNoHit(r, new Color(0.04f, 0.03f, 0.03f, 0.5f));
            else if (n > 6) FillNoHit(In(r, 4, 39, 36, 2), GoodColours[g]);
            bool lacks = _refusedKind != BuildKind.None && Time.unscaledTime < _refusedUntil && vm.KindOf(_refusedKind).Missing[g] > 0;
            bool picked = (MatOpen && BankGive == g) || DiscardPick[g] > 0;
            if (lacks) Frame(r, new Color(0.95f, 0.3f, 0.2f), Mathf.Max(2f, 2f * _lu));
            else if (picked) Frame(r, Gold, Mathf.Max(2f, 2f * _lu));
            LedgeTap("basket:" + g, r);
        }

        private void DrawBench(int k, GodBoardViewModel vm)
        {
            var r = LedgeRect("bench:" + k);
            var kv = vm.Kind[k];
            bool ok = kv.Affordable;
            bool active = _d.BuildMode == kv.Kind;
            float shake = Time.unscaledTime < _shakeUntil && _refusedKind == kv.Kind ? Mathf.Sin(Time.unscaledTime * 50f) * 2f * _lu : 0f;
            var rr = new Rect(r.x + shake, r.y, r.width, r.height);
            Tile(rr, ok ? new Color(0.36f, 0.29f, 0.14f) : StoneMid);
            FillNoHit(In(rr, 2, 30, 40, 5), new Color(0.50f, 0.36f, 0.18f)); // the plank
            var col = PeopleCol(vm.Human);
            var pc = ok ? col : col * 0.55f; pc.a = 1f;
            int glyph = k == 0 ? GRoad : k == 1 ? GHouse : GCity;
            GlyphAt(In(rr, 7, 1, 30, 30), glyph, pc);
            // the spare pieces stacked behind: one tick per piece left
            int left = Mathf.Min(kv.Left, kv.Max);
            float tw = Mathf.Max(1f, 36f / Mathf.Max(1, kv.Max));
            for (int i = 0; i < left; i++) FillNoHit(In(rr, 4f + i * tw, 36, Mathf.Max(1f, tw - 0.6f), 6), Color.Lerp(col, Color.black, 0.15f));
            if (active) Frame(rr, Gold, Mathf.Max(2f, 2f * _lu));
            else if (ok) Frame(rr, new Color(0.9f, 0.75f, 0.3f, 0.8f), Mathf.Max(1f, _lu));
            if (!ok) FillNoHit(rr, new Color(0.05f, 0.04f, 0.04f, 0.38f));
            LedgeTap("bench:" + k, r);
        }

        private void DrawStake(int p, GodBoardViewModel vm)
        {
            var r = LedgeRect("stake:" + p);
            bool current = vm.Current == p && !vm.Paused && vm.Phase != TablePhase.NotOpened && vm.Phase != TablePhase.Ended;
            Tile(r, StoneMid);
            var col = PeopleCol(p);
            float lift = current ? -4f : 0f;
            FillNoHit(In(r, 8, 6 + lift, 3, 36 - lift), new Color(0.36f, 0.25f, 0.12f)); // the pole
            var banner = In(r, 11, 7 + lift, 16, 13);
            FillNoHit(banner, col);
            FillNoHit(new Rect(banner.x, banner.yMax - 2f * _lu, banner.width, 2f * _lu), Color.Lerp(col, Color.black, 0.35f));
            string nm = _d.PeopleShort(p);
            Text(banner, nm.Length > 0 ? nm.Substring(0, 1) : "?", 9f * (_lu / _u), Color.white, true, TextAnchor.MiddleCenter);
            if (vm.LongestHolder == p) FillNoHit(In(r, 8, 23 + lift, 22, 2), Gold); // the longest-road cord
            // point stones: one small stone per point, two columns
            int pts = Mathf.Min(vm.Points[p], 12);
            for (int i = 0; i < pts; i++)
            {
                var st = In(r, 29f + (i % 2) * 7f, 38f - (i / 2) * 6f, 5, 5);
                Disc(st, new Color(0.88f, 0.84f, 0.74f));
            }
            bool war = false;
            foreach (var pr in vm.WarPairs) if (pr[0] == p || pr[1] == p) war = true;
            if (war) GlyphAt(In(r, 27, 2, 14, 14), GHelmet, new Color(0.9f, 0.3f, 0.2f));
            if (current) Frame(r, Gold, Mathf.Max(2f, 2f * _lu));
            if (current && p == vm.Human && vm.CanAct) Arc(new Vector2(r.xMax - 6f * _lu, r.y + 6f * _lu), 4f * _lu, 2f * _lu, vm.SandFraction, Gold);
            LedgeTap("stake:" + p, r);
        }

        private void DrawTrader(GodBoardViewModel vm)
        {
            var r = LedgeRect("trader");
            Tile(r, MatOpen ? new Color(0.36f, 0.29f, 0.14f) : StoneMid);
            Disc(In(r, 12, 5, 20, 20), new Color(0.80f, 0.62f, 0.45f)); // the trader's face
            FillNoHit(In(r, 8, 4, 28, 6), new Color(0.55f, 0.20f, 0.14f)); // hat
            GlyphAt(In(r, 11, 22, 22, 20), GSwap, Gold);
            if (MatOpen) Frame(r, Gold, Mathf.Max(2f, 2f * _lu));
            LedgeTap("trader", r);
        }

        private void DrawSack(GodBoardViewModel vm)
        {
            var r = LedgeRect("sack");
            Tile(r, new Color(0.34f, 0.2f, 0.1f));
            Disc(In(r, 6, 14, 32, 28), new Color(0.66f, 0.5f, 0.28f));
            FillNoHit(In(r, 15, 8, 14, 8), new Color(0.58f, 0.43f, 0.22f));
            // knots: one per good owed, filled as goods go in
            int owed = Mathf.Min(vm.DiscardOwed, 12);
            for (int i = 0; i < owed; i++)
            {
                var kn = In(r, 4f + (i % 6) * 6.2f, 2f + (i / 6) * 6f, 4.6f, 4.6f);
                Disc(kn, i < DiscardChosen ? Gold : new Color(0.15f, 0.1f, 0.06f));
            }
            // the small timer hourglass: a bar along the foot
            float frac = Mathf.Clamp01(vm.DiscardTicksLeft / (float)Mathf.Max(1, vm.DiscardTicksMax));
            FillNoHit(In(r, 4, 39, 36, 3), new Color(0.2f, 0.14f, 0.06f));
            FillNoHit(In(r, 4, 39, 36f * frac, 3), frac > 0.3f ? Gold : new Color(0.9f, 0.3f, 0.2f));
            if (DiscardChosen == vm.DiscardOwed) Frame(r, new Color(0.6f, 0.9f, 0.5f), Mathf.Max(2f, 2f * _lu));
            LedgeTap("sack", r);
            var c = LedgeRect("sack-clear");
            Tile(c, StoneMid);
            GlyphAt(In(c, 8, 8, 28, 28), GMinus, new Color(0.9f, 0.8f, 0.7f));
            LedgeTap("sack-clear", c);
        }

        private void DrawEnvoys(GodBoardViewModel vm)
        {
            int shown = Mathf.Min(2, vm.Incoming.Count);
            for (int i = 0; i < shown; i++)
            {
                var o = vm.Incoming[i];
                var r = LedgeRect("envoy:" + o.Id);
                var w = LedgeRect("wave:" + o.Id);
                Tile(r, new Color(0.28f, 0.24f, 0.19f));
                var col = PeopleCol(o.From);
                FillNoHit(new Rect(r.x, r.y, 5f * _lu, r.height), col);
                Disc(In(r, 8, 3, 14, 14), col); // the envoy
                string nm = _d.PeopleShort(o.From);
                Text(In(r, 8, 3, 14, 14), nm.Substring(0, 1), 8f * (_lu / _u), Color.white, true, TextAnchor.MiddleCenter);
                // the bundle held out: the goods you get
                float bx = 24f; int shownGoods = 0;
                for (int g = 0; g < Wares.RawCount && shownGoods < 3; g++)
                    if (o.Give[g] > 0) { GUI.DrawTexture(In(r, bx, 3, 14, 14), IconOf(g)); bx += 13f; shownGoods++; }
                GlyphAt(In(r, 4, 21, 14, 14), GSwap, Gold);
                bx = 24f; shownGoods = 0;
                for (int g = 0; g < Wares.RawCount && shownGoods < 3; g++)
                    if (o.Get[g] > 0) { GUI.DrawTexture(In(r, bx, 22, 14, 14), IconOf(g)); bx += 13f; shownGoods++; }
                // patience: the envoy turns to go as the edge drains
                float frac = Mathf.Clamp01(o.TicksLeft / (float)vm.OfferTicks);
                FillNoHit(In(r, 0, 40, r.width / _lu, 4), new Color(0.2f, 0.18f, 0.15f));
                FillNoHit(In(r, 0, 40, r.width / _lu * frac, 4), frac > 0.3f ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.9f, 0.3f, 0.2f));
                if (!TableRules.CanPay(vm.Hand, o.Get)) FillNoHit(r, new Color(0.05f, 0.04f, 0.04f, 0.3f));
                LedgeTap("envoy:" + o.Id, r);
                Tile(w, new Color(0.30f, 0.16f, 0.12f));
                GlyphAt(In(w, 8, 8, 28, 28), GCross, new Color(1f, 0.85f, 0.8f));
                LedgeTap("wave:" + o.Id, w);
            }
            if (vm.Incoming.Count > shown) Text(new Rect(_band.x + 6f * _lu, _band.y - 14f * _lu, 160f * _lu, 14f * _lu), "+" + (vm.Incoming.Count - shown) + " more envoys wait", 9f * (_lu / _u), Soft);
        }

        private void DrawSimple(string id, int glyph, Color ink, Color tint, Texture2D pic)
        {
            var r = LedgeRect(id);
            Tile(r, tint);
            if (pic != null) { GUI.DrawTexture(In(r, 6, 3, 32, 32), pic); GlyphAt(In(r, 22, 24, 20, 20), glyph, ink); }
            else GlyphAt(In(r, 7, 7, 30, 30), glyph, ink);
            if (id == "chronicler")
            {
                int unread = Mathf.Max(0, _d.FeedTotal - _logSeen);
                if (unread > 0) Disc(In(r, 33, 2, 9, 9), new Color(0.85f, 0.25f, 0.18f));
            }
            LedgeTap(id, r);
        }

        private void DrawPrince()
        {
            var r = LedgeRect("prince");
            Tile(r, StoneMid);
            Disc(In(r, 12, 12, 20, 20), new Color(0.82f, 0.66f, 0.5f));
            FillNoHit(In(r, 10, 6, 24, 8), GodBoardDirector.AdviserOn ? Gold : new Color(0.5f, 0.45f, 0.35f)); // the crown, dark when hints are off
            GlyphAt(In(r, 16, 28, 12, 12), GBulb, GodBoardDirector.AdviserOn ? Gold : new Color(0.5f, 0.45f, 0.35f));
            LedgeTap("prince", r);
        }

        private void DrawTotem()
        {
            var r = LedgeRect("totem");
            bool on = PlagueIsOn != null && PlagueIsOn();
            Tile(r, on ? new Color(0.36f, 0.12f, 0.12f) : StoneMid);
            FillNoHit(In(r, 17, 6, 10, 34), new Color(0.40f, 0.28f, 0.15f));
            GlyphAt(In(r, 8, 6, 28, 28), GDroplet, on ? new Color(1f, 0.4f, 0.3f) : new Color(0.7f, 0.45f, 0.4f));
            if (PlagueToggle == null) FillNoHit(r, new Color(0.05f, 0.04f, 0.04f, 0.45f));
            if (on) Frame(r, new Color(1f, 0.4f, 0.3f), Mathf.Max(2f, 2f * _lu));
            LedgeTap("totem", r);
        }

        private void DrawHourglass(GodBoardViewModel vm)
        {
            var r = LedgeRect("hourglass");
            bool main = vm.CanAct;
            Tile(r, main ? new Color(0.42f, 0.28f, 0.10f) : StoneMid);
            float frac = vm.SandFraction;
            var c = r.center;
            Arc(c, Mathf.Min(r.width, r.height) * 0.5f - 3f * _lu, 3f * _lu, 1f, new Color(0.25f, 0.22f, 0.16f));
            if (main) Arc(c, Mathf.Min(r.width, r.height) * 0.5f - 3f * _lu, 3f * _lu, frac, frac > 0.3f ? Gold : new Color(0.95f, 0.3f, 0.2f));
            // the glass: tips to its side when you have previewed ending the turn
            var old = GUI.matrix;
            if (_tipped) GUIUtility.RotateAroundPivot(90f, c);
            float side = Mathf.Min(r.width, r.height) * 0.6f;
            var glass = new Rect(c.x - side / 2f, c.y - side / 2f, side, side);
            GlyphAt(glass, GHourglass, main ? new Color(1f, 0.97f, 0.88f) : new Color(0.75f, 0.72f, 0.66f));
            float k = side / 32f, gx = glass.x, gy = glass.yMax;
            var sand = main ? new Color(0.98f, 0.82f, 0.35f) : new Color(0.5f, 0.45f, 0.35f);
            float top = main ? frac : 0f;
            FillNoHit(new Rect(gx + 11f * k, gy - (17f + 8f * top) * k, 10f * k, 8f * top * k), sand);
            FillNoHit(new Rect(gx + 10f * k, gy - (6f + 8f * (1f - top)) * k, 12f * k, 8f * (1f - top) * k), sand);
            GUI.matrix = old;
            if (_tipped) { Disc(new Rect(r.xMax - 20f * _lu, r.y + 4f * _lu, 14f * _lu, 14f * _lu), Gold); GlyphAt(new Rect(r.xMax - 18f * _lu, r.y + 6f * _lu, 10f * _lu, 10f * _lu), GBulb, new Color(0.3f, 0.2f, 0.05f)); }
            LedgeTap("hourglass", r);
        }

        // ---------------------------------------------------------------- the barter mat (bank and ports)

        private void DrawMat(GodBoardViewModel vm)
        {
            var m = _mat;
            _rects.Add(m);
            FillNoHit(new Rect(m.x - 2f * _lu, m.y - 2f * _lu, m.width + 4f * _lu, m.height + 4f * _lu), new Color(0.96f, 0.92f, 0.82f, 0.9f));
            FillNoHit(m, new Color(0.34f, 0.22f, 0.12f, 0.97f));
            for (float yy = m.y + 8f * _lu; yy < m.yMax; yy += 10f * _lu) FillNoHit(new Rect(m.x, yy, m.width, _lu), new Color(0f, 0f, 0f, 0.14f));
            Text(In(m, 8, 1, 120, 14), "The barter mat", 10f * (_lu / _u), new Color(1f, 0.92f, 0.7f), true);
            // switch to offers to the peoples
            var sw = In(m, m.width / _lu - 128f, 2, 92, 14);
            FillNoHit(sw, new Color(0.25f, 0.18f, 0.08f));
            Text(sw, "To peoples", 9f * (_lu / _u), UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            if (GUI.Button(sw, GUIContent.none, GUIStyle.none)) SetTab(1);
            Texts.Add("[To peoples]");
            var hand = vm.Hand;
            float cw = 38f, ch = 46f;
            float x0 = m.width / _lu * 0.5f - (5 * (cw + 6f)) * 0.5f + 20f;
            Text(In(m, 6, 20, 30, 14), "Give", 9f * (_lu / _u), Soft, true);
            Text(In(m, 6, 20 + ch + 14, 30, 14), "Get", 9f * (_lu / _u), Soft, true);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = x0 + g * (cw + 6f);
                var rg = In(m, x, 16, cw, ch - 4f);
                bool afford = hand[g] >= vm.BankRate[g];
                GoodsCard(rg, g, hand[g], !afford, BankGive == g);
                if (Tap(rg, true, false)) SelectBankGive(g);
                var rt = In(m, x, 16 + ch + 12f, cw, ch - 4f);
                GoodsCard(rt, g, 0, false, BankGet == g, false);
                if (Tap(rt, true, false)) SelectBankGet(g);
            }
            string deal;
            if (BankGive >= 0 && BankGet >= 0 && BankGive != BankGet)
            {
                int rate = vm.BankRate[BankGive];
                var cost = new int[Wares.RawCount]; cost[BankGive] = rate;
                var gain = new int[Wares.RawCount]; gain[BankGet] = 1;
                deal = "Give " + GodBoardLedger.Goods(cost) + " for " + GodBoardLedger.Goods(gain) + ".";
            }
            else deal = "Pick a good to give, then a good to get. The bank takes " + vm.BankRateBase + ".";
            string msg = !vm.CanAct ? "Wait for your turn." : (string.IsNullOrEmpty(TradeMessage) ? deal : TradeMessage);
            Text(In(m, 6, m.height / _lu - 16f, m.width / _lu - 100f, 14), msg, 9f * (_lu / _u), msg == "Traded." ? new Color(0.6f, 0.9f, 0.5f) : Soft, false);
            bool can = vm.CanAct && BankGive >= 0 && BankGet >= 0 && BankGive != BankGet && hand[BankGive] >= vm.BankRate[BankGive];
            if (RoundButton(In(m, m.width / _lu - 84f, m.height / _lu - 40f, 36, 36), GCheck, "Trade", Ok, can, false, true)) DoBankTrade();
            if (RoundButton(In(m, m.width / _lu - 44f, m.height / _lu - 40f, 36, 36), GCross, "Close the mat", No, true, false, true)) CloseTrade();
        }
    }
}
