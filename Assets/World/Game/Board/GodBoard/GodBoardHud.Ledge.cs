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
            if (MatOpen && !GameOverShown)
            {
                // the unrolled barter mat: what you give (moved from the basket) on the top row, the five goods to get below,
                // the deal stone and the peoples' peg board at the right end
                float pad = 8f * _lu, cell = row + gap;
                float my1 = _mat.y + pad, my2 = _mat.yMax - pad - row;
                float mx = _mat.x + pad + 4f * _lu;
                _ledgeRects["mat-give"] = new Rect(mx, my1, 5f * cell - gap, row);
                for (int g = 0; g < Wares.RawCount; g++) _ledgeRects["mat-get:" + g] = new Rect(mx + g * cell, my2, row, row);
                float rx = _mat.xMax - pad - 4f * _lu - row;
                _ledgeRects["mat-deal"] = new Rect(rx, my2, row, row);
                _ledgeRects["mat-peoples"] = new Rect(rx, my1, row, row);
            }
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
                case "totem": return GodBoardLedger.Totem(TotemOn);
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
                    TotemToggle();
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
                case "mat-peoples":
                    SetTab(1); WorldActions.Cancel();
                    return WorldTapResult.Inspected;
                case "mat-give":
                {
                    var o = Obj("mat-give", WorldZones.HexTrade);
                    o.GuiRect = rect;
                    o.Content = () => { var c = GodBoardLedger.Trader(_d.FreshModel()); c.Lines.Insert(0, MatDealLine(_d.FreshModel())); c.Title = "The barter mat"; return c; };
                    o.CheckFn = null; o.CommitFn = null; o.PreviewFn = null; o.Question = null;
                    return WorldActions.Inspect(NoteOf(o));
                }
                case "mat-deal":
                {
                    var o = Obj("mat-deal", WorldZones.HexTrade);
                    o.GuiRect = rect;
                    o.Content = () => { var m = _d.FreshModel(); var c = GodBoardLedger.Trader(m); c.Title = "The deal stone"; c.Lines.Insert(0, MatDealLine(m)); return c; };
                    o.Question = "Make this trade?";
                    o.CheckFn = () =>
                    {
                        var m = _d.Model;
                        if (!m.CanAct) return WorldActionCheck.Refuse("Wait for your turn.");
                        if (BankGive < 0 || BankGet < 0 || BankGive == BankGet) return WorldActionCheck.Refuse("Tap a basket to give, then a good on the mat to get.");
                        if (m.Hand[BankGive] < m.BankRate[BankGive]) return WorldActionCheck.Refuse("You need " + m.BankRate[BankGive] + " " + GodBoardLedger.Plural1(m.BankRate[BankGive], "good", "goods") + " of that kind.");
                        return WorldActionCheck.Allowed;
                    };
                    o.PreviewFn = null;
                    o.CommitFn = () => DoBankTrade().Ok;
                    return WorldActions.Tap(o);
                }
            }
            if (id.StartsWith("mat-get:"))
            {
                if (!MatOpen) return WorldTapResult.Ignored;
                SelectBankGet(int.Parse(id.Substring(8)));
                WorldActions.Cancel();
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

        private string MatDealLine(GodBoardViewModel vm)
        {
            if (!vm.CanAct) return "Wait for your turn.";
            if (!string.IsNullOrEmpty(TradeMessage)) return TradeMessage;
            if (BankGive >= 0 && BankGet >= 0 && BankGive != BankGet)
            {
                var cost = new int[Wares.RawCount]; cost[BankGive] = vm.BankRate[BankGive];
                var gain = new int[Wares.RawCount]; gain[BankGet] = 1;
                return "Give " + GodBoardLedger.Goods(cost) + " for " + GodBoardLedger.Goods(gain) + ".";
            }
            return "Tap a basket to give, then a good on the mat to get. The bank takes " + vm.BankRateBase + ".";
        }

        /// <summary>The demon-ward totem works the plague overlay through WU-3's public hooks (the old round-icon hooks as a fallback).</summary>
        public static bool TotemAvailable => PlagueViews.Instance != null || PlagueToggle != null;
        public static bool TotemOn => PlagueViews.Instance != null ? PlagueViews.OverlayOn : PlagueIsOn != null && PlagueIsOn();
        private static void TotemToggle()
        {
            if (PlagueViews.Instance != null) PlagueViews.ToggleOverlayFromTotem();
            else if (PlagueToggle != null) PlagueToggle();
        }

        private static int[] MissingFor(int[] hand, int[] cost)
        {
            var m = new int[Wares.RawCount];
            for (int g = 0; g < Wares.RawCount; g++) m[g] = Mathf.Max(0, cost[g] - hand[g]);
            return m;
        }


        // ---------------------------------------------------------------- the 3D ledge (WU-2b): no IMGUI drawing in the world-first view

        private AltarLedgeRig _rig;
        private int _ledgeRectAt = -1, _ledgeRectN;

        /// <summary>The 3D ledge rig (its camera, models and hit boxes); null until the board first shows.</summary>
        public AltarLedgeRig Rig => _rig;
        /// <summary>The barter mat's rect (GUI pixels) from the last layout; it hangs above the ledge when the trader unrolls it.</summary>
        public Rect MatRect => _mat;
        /// <summary>Times the old IMGUI ledge was drawn (stays 0: the world-first view draws the ledge in 3D only).</summary>
        public int LedgeGuiDraws { get; private set; }
        /// <summary>Feed lines the chronicler holds that the player has not read.</summary>
        public int UnreadFeed => _d != null ? Mathf.Max(0, _d.FeedTotal - _logSeen) : 0;
        public Color PeopleColour(int p) => PeopleCol(p);

        /// <summary>The builder has just refused and this good's basket is one of those lacking (the basket glows red).</summary>
        public bool Lacks(int g)
        {
            if (_refusedKind == BuildKind.None || Time.unscaledTime >= _refusedUntil || _d == null) return false;
            return _d.Model.KindOf(_refusedKind).Missing[g] > 0;
        }

        /// <summary>The builder's head-shake as a sideways offset (units of the bench's height) for the refused sample.</summary>
        public float ShakeOffset(BuildKind k) => Time.unscaledTime < _shakeUntil && _refusedKind == k ? Mathf.Sin(Time.unscaledTime * 50f) * 0.05f : 0f;

        private bool WorldFirstBoard => _d != null && _d.View == BoardView.Board && _d.T != null && !GodBoardSettings.ClearView;

        /// <summary>Called from OnGUI in the world-first view: the ledge band and the mat block board presses (they are not drawn here).</summary>
        private void LedgeBlocks()
        {
            LayoutLedge();
            _ledgeRectAt = _rects.Count;
            _rects.Add(new Rect(0f, _band.y, Screen.width, Screen.height - _band.y));
            if (MatOpen && !GameOverShown) _rects.Add(_mat);
            _ledgeRectN = _rects.Count - _ledgeRectAt;
        }

        private void Update()
        {
            if (!WorldFirstBoard || _rig == null || !_rig.Shown) return;
            Vector2 pos; bool pressed;
            if (Input.touchCount > 0) { var tc = Input.GetTouch(0); pos = tc.position; pressed = Input.touchCount == 1 && tc.phase == TouchPhase.Began; }
            else { pos = Input.mousePosition; pressed = Input.GetMouseButtonDown(0); }
            if (!pressed) return;
            var g = new Vector2(pos.x, Screen.height - pos.y);
            if (LedgerCard.IsOpen && LedgerCard.LastRect.Contains(g)) return;   // the tablet takes its own taps
            for (int i = 0; i < _rects.Count; i++) if ((i < _ledgeRectAt || i >= _ledgeRectAt + _ledgeRectN) && _rects[i].Contains(g)) return;   // a modal or card drawn over the ledge
            string id = _rig.Pick(pos);
            if (id != null) TapLedge(id);
        }

        private void LateUpdate()
        {
            bool show = WorldFirstBoard;
            if (_rig == null) { if (!show) return; _rig = AltarLedgeRig.Create(this, _d); }
            if (show) LayoutLedge();
            _rig.Sync(show);
        }
    }
}
