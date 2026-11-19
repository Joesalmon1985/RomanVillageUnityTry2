using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// G1-6: the trade panel (bank and port trades shown as pictures of goods, offers to one people or all), the offer cards
    /// for AI offers to the player, the discard picker for a 7 (TU-10), the epidemic banner and the end panels (AgeEnded,
    /// GameLost). IMGUI like the rest of <see cref="GodBoardHud"/>, laid out in design pixels for 844 x 390. The view only
    /// reads the table and submits commands (<see cref="GodBoardDirector.Send"/>). No dice, no odds, no roll values.
    /// The public methods are what the buttons call, so tests can drive them without a pointer.
    /// </summary>
    public sealed partial class GodBoardHud
    {
        private bool _under;

        // ---------------------------------------------------------------- state

        public bool TradeOpen { get; private set; }
        /// <summary>0 = bank or port, 1 = offer to the peoples.</summary>
        public int TradeTab { get; private set; }
        public int BankGive { get; private set; } = -1;
        public int BankGet { get; private set; } = -1;
        /// <summary>The people an offer goes to, or -1 for everyone.</summary>
        public int OfferTo { get; private set; } = -1;
        public readonly int[] OfferGive = new int[Wares.RawCount];
        public readonly int[] OfferGet = new int[Wares.RawCount];
        public readonly int[] DiscardPick = new int[Wares.RawCount];
        public string TradeMessage { get; private set; } = "";
        private bool _pickActive;

        private static readonly Color Panel = new Color(0.10f, 0.09f, 0.08f, 0.96f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
        private static readonly Color Soft = new Color(0.9f, 0.86f, 0.75f);

        // ---------------------------------------------------------------- reading the table

        private TableState Tb => _d != null ? _d.T : null;

        /// <summary>True when the player must discard now (TU-10).</summary>
        public bool DiscardVisible
        {
            get
            {
                var t = Tb;
                return t != null && _d.View == BoardView.Board && t.Phase == TablePhase.Discard && !t.Paused && _d.Human < t.DiscardOwed.Length && t.DiscardOwed[_d.Human] > 0;
            }
        }

        public int DiscardOwed => DiscardVisible ? Tb.DiscardOwed[_d.Human] : 0;
        public int DiscardChosen { get { int n = 0; foreach (int c in DiscardPick) n += c; return n; } }

        /// <summary>True when the table has ended (an age ended or the island fell): the end panel shows and the table takes no input.</summary>
        public bool GameOverShown { get { var t = Tb; return t != null && _d.View == BoardView.Board && t.Phase == TablePhase.Ended; } }

        /// <summary>Open offers addressed to the player (or to everyone) that the player may still answer, soonest to expire first.</summary>
        public List<TradeOffer> IncomingOffers()
        {
            var list = new List<TradeOffer>();
            var t = Tb;
            if (t == null || t.Paused || t.Phase != TablePhase.Main) return list;
            int me = _d.Human;
            foreach (var o in t.Offers)
                if (o.Status == OfferStatus.Open && o.From != me && (o.To == me || o.To < 0) && !o.DeclinedBy.Contains(me)) list.Add(o);
            list.Sort((a, b) => a.TicksLeft.CompareTo(b.TicksLeft));
            return list;
        }

        public TradeOffer OwnOpenOffer()
        {
            var t = Tb;
            if (t == null) return null;
            foreach (var o in t.Offers) if (o.Status == OfferStatus.Open && o.From == _d.Human) return o;
            return null;
        }

        public int BankRate(int good) => TableRules.TradeRate(_d.S, _d.Human, (Ware)good);

        private bool CanAct
        {
            get { var t = Tb; return t != null && t.Phase == TablePhase.Main && t.Current == _d.Human && !t.Paused; }
        }

        // ---------------------------------------------------------------- actions (what the buttons call)

        public void OpenTrade() { TradeOpen = true; TradeMessage = ""; }
        public void CloseTrade() { TradeOpen = false; TradeMessage = ""; }
        public void SetTab(int tab) { TradeTab = tab; TradeMessage = ""; }
        public void SelectBankGive(int g) { BankGive = g; if (BankGet == g) BankGet = -1; TradeMessage = ""; }
        public void SelectBankGet(int g) { BankGet = g; if (BankGive == g) BankGive = -1; TradeMessage = ""; }
        public void SetOfferTo(int p) { OfferTo = p; }

        public CommandResult DoBankTrade()
        {
            if (BankGive < 0 || BankGet < 0 || BankGive == BankGet) { TradeMessage = "Choose the good to give and the good to get."; return CommandResult.Reject("bad_trade", TradeMessage); }
            var r = _d.Send(new BankTrade { Give = (Ware)BankGive, Get = (Ware)BankGet });
            TradeMessage = r.Ok ? "Traded." : Sentence(r);
            return r;
        }

        public void AdjustOffer(bool give, int good, int delta)
        {
            var arr = give ? OfferGive : OfferGet;
            int max = give ? Tb.Peoples[_d.Human].Hand[good] : 9;
            arr[good] = Mathf.Clamp(arr[good] + delta, 0, Mathf.Max(0, max));
            TradeMessage = "";
        }

        public CommandResult DoOffer()
        {
            int gives = 0, gets = 0;
            for (int g = 0; g < Wares.RawCount; g++) { gives += OfferGive[g]; gets += OfferGet[g]; }
            if (gives <= 0 || gets <= 0) { TradeMessage = "Choose what to give and what to get."; return CommandResult.Reject("bad_trade", TradeMessage); }
            var r = _d.Send(new OfferTrade { To = OfferTo, Give = (int[])OfferGive.Clone(), Get = (int[])OfferGet.Clone() });
            TradeMessage = r.Ok ? "Offer made. We wait for an answer." : Sentence(r);
            if (r.Ok) { System.Array.Clear(OfferGive, 0, OfferGive.Length); System.Array.Clear(OfferGet, 0, OfferGet.Length); }
            return r;
        }

        public CommandResult AnswerOffer(int offerId, bool accept)
        {
            var r = _d.Send(new AnswerOffer { OfferId = offerId, Accept = accept });
            return r;
        }

        private static string Sentence(CommandResult r) => string.IsNullOrEmpty(r.Message) ? TableRefusals.Sentence(r.Reason) : r.Message;

        private void EnsurePick()
        {
            if (_pickActive) return;
            System.Array.Clear(DiscardPick, 0, DiscardPick.Length);
            _pickActive = true;
        }

        public void AdjustDiscard(int good, int delta)
        {
            EnsurePick();
            int hand = Tb.Peoples[_d.Human].Hand[good];
            int room = DiscardOwed - DiscardChosen;
            int next = DiscardPick[good] + delta;
            if (delta > 0 && room <= 0) return;
            DiscardPick[good] = Mathf.Clamp(next, 0, hand);
        }

        /// <summary>Fills the picker with the adviser's choice (the same the default chooser makes when time runs out).</summary>
        public void SuggestDiscard()
        {
            EnsurePick();
            var c = TableAi.ChooseDiscard(_d.S, _d.Human, DiscardOwed);
            if (c != null && c.Length == Wares.RawCount) for (int g = 0; g < Wares.RawCount; g++) DiscardPick[g] = c[g];
        }

        public CommandResult DoDiscard()
        {
            EnsurePick();
            var r = _d.Send(new DiscardCards { Cards = (int[])DiscardPick.Clone() });
            if (r.Ok) { System.Array.Clear(DiscardPick, 0, DiscardPick.Length); _pickActive = false; }
            return r;
        }

        // ---------------------------------------------------------------- drawing helpers

        private void GoodsRow(Rect at, int[] counts, float size)
        {
            // icons with counts, in a row: [icon]x2 [icon]x1
            float x = at.x;
            for (int g = 0; g < Wares.RawCount; g++)
            {
                if (counts[g] <= 0) continue;
                GUI.DrawTexture(new Rect(x, at.y + (at.height - 16f * _u) * 0.5f, 16f * _u, 16f * _u), IconOf(g));
                Text(new Rect(x + 17f * _u, at.y, 22f * _u, at.height), "x" + counts[g], size, UiStyle.TextColor, true);
                x += 40f * _u;
            }
            if (x == at.x) Text(at, "nothing", size, Soft);
        }

        private void IconsFor(Rect at, int good, int count)
        {
            float x = at.x;
            for (int k = 0; k < count; k++)
            {
                GUI.DrawTexture(new Rect(x, at.y, at.height, at.height), IconOf(good));
                x += at.height * 0.8f;
            }
        }

        private void Window(Rect r, string title, bool closeButton, out bool closed)
        {
            closed = false;
            Fill(r, Panel);
            Frame(r, Gold, Mathf.Max(2f, 2f * _u));
            Text(new Rect(r.x + 10f * _u, r.y + 2f * _u, r.width - 80f * _u, 26f * _u), title, 14f, Gold, true);
            if (closeButton && Button(new Rect(r.xMax - 64f * _u, r.y + 3f * _u, 58f * _u, 26f * _u), "Close", true, 11f, true)) closed = true;
        }

        // ---------------------------------------------------------------- trade panel

        private void TradeModal(WorldState s, TableState t, float W)
        {
            const float PW = 400f, PH = 262f;
            float px = Mathf.Max(6f, (W - 238f - PW) * 0.5f), py = 52f;
            var r = R(px, py, PW, PH);
            Window(r, "Trade", true, out bool closed);
            if (closed) { CloseTrade(); return; }
            bool turn = CanAct;
            if (Button(R(px + 10f, py + 32f, 120f, 28f), "Bank or port", true, 11f, true)) SetTab(0);
            if (Button(R(px + 134f, py + 32f, 120f, 28f), "Offer to peoples", true, 11f, true)) SetTab(1);
            Frame(R(px + 10f + TradeTab * 124f, py + 32f, 120f, 28f), Gold, Mathf.Max(2f, 2f * _u));
            if (!turn) Text(R(px + 260f, py + 32f, 136f, 28f), "Wait for your turn.", 11f, new Color(1f, 0.7f, 0.55f), true);
            if (TradeTab == 0) BankTab(s, t, px, py, turn); else OfferTab(s, t, px, py, turn);
            Text(R(px + 10f, py + 196f, PW - 20f, 18f), TradeMessage, 11f, TradeMessage == "Traded." || TradeMessage.StartsWith("Offer made") ? new Color(0.6f, 0.9f, 0.5f) : new Color(1f, 0.7f, 0.55f), true);
        }

        private void BankTab(WorldState s, TableState t, float px, float py, bool turn)
        {
            var hand = t.Peoples[_d.Human].Hand;
            Text(R(px + 10f, py + 66f, 60f, 44f), "You give", 11f, Soft, true);
            Text(R(px + 10f, py + 114f, 60f, 44f), "You get", 11f, Soft, true);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = px + 72f + g * 64f;
                int rate = BankRate(g);
                bool afford = hand[g] >= rate;
                var rg = R(x, py + 66f, 60f, 44f);
                Fill(rg, new Color(GoodColours[g].r * 0.3f, GoodColours[g].g * 0.3f, GoodColours[g].b * 0.3f, 0.95f));
                GUI.DrawTexture(new Rect(rg.x + 3f * _u, rg.y + 3f * _u, 22f * _u, 22f * _u), IconOf(g));
                Text(new Rect(rg.x + 28f * _u, rg.y + 2f * _u, 30f * _u, 26f * _u), hand[g].ToString(), 15f, UiStyle.TextColor, true, TextAnchor.MiddleCenter);
                Text(new Rect(rg.x, rg.y + 28f * _u, rg.width, 14f * _u), rate + " for 1", 10f, afford ? new Color(0.8f, 0.95f, 0.6f) : new Color(0.75f, 0.6f, 0.55f), true, TextAnchor.MiddleCenter);
                if (BankGive == g) Frame(rg, Gold, Mathf.Max(2f, 2f * _u));
                if (GUI.Button(rg, GUIContent.none, GUIStyle.none) && (!_under)) SelectBankGive(g);

                var rt = R(x, py + 114f, 60f, 44f);
                Fill(rt, new Color(GoodColours[g].r * 0.3f, GoodColours[g].g * 0.3f, GoodColours[g].b * 0.3f, 0.95f));
                GUI.DrawTexture(new Rect(rt.x + 19f * _u, rt.y + 6f * _u, 22f * _u, 22f * _u), IconOf(g));
                Text(new Rect(rt.x, rt.y + 28f * _u, rt.width, 14f * _u), GoodNames[g], 10f, Soft, false, TextAnchor.MiddleCenter);
                if (BankGet == g) Frame(rt, Gold, Mathf.Max(2f, 2f * _u));
                if (GUI.Button(rt, GUIContent.none, GUIStyle.none) && (!_under)) SelectBankGet(g);
            }
            // The deal as pictures: the goods given, an arrow, the good received.
            var line = R(px + 72f, py + 164f, 300f, 24f);
            if (BankGive >= 0 && BankGet >= 0 && BankGive != BankGet)
            {
                int rate = BankRate(BankGive);
                IconsFor(new Rect(line.x, line.y, 0f, 22f * _u), BankGive, rate);
                float ax = line.x + (rate - 1) * 22f * _u * 0.8f + 28f * _u;
                Text(new Rect(ax, line.y, 30f * _u, line.height), "->", 14f, Gold, true, TextAnchor.MiddleCenter);
                IconsFor(new Rect(ax + 34f * _u, line.y, 0f, 22f * _u), BankGet, 1);
                string portNote = rate < 4 ? "  (port rate)" : "";
                Text(new Rect(ax + 64f * _u, line.y, 140f * _u, line.height), portNote, 10f, Soft);
            }
            else Text(line, "Pick a good to give and a good to get.", 10f, Soft);
            bool can = turn && BankGive >= 0 && BankGet >= 0 && BankGive != BankGet && hand[BankGive] >= BankRate(BankGive);
            if (Button(R(px + 140f, py + 220f, 120f, 34f), "Trade", can, 13f, true)) DoBankTrade();
        }

        private void OfferTab(WorldState s, TableState t, float px, float py, bool turn)
        {
            var hand = t.Peoples[_d.Human].Hand;
            Text(R(px + 10f, py + 64f, 40f, 26f), "To", 11f, Soft, true);
            float bx = px + 40f;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                if (p == _d.Human) continue;
                if (Button(R(bx, py + 64f, 82f, 26f), _d.PeopleShort(p), true, 10f, true)) SetOfferTo(p);
                if (OfferTo == p) Frame(R(bx, py + 64f, 82f, 26f), Gold, Mathf.Max(2f, 2f * _u));
                bx += 86f;
            }
            if (Button(R(bx, py + 64f, 60f, 26f), "All", true, 10f, true)) SetOfferTo(-1);
            if (OfferTo == -1) Frame(R(bx, py + 64f, 60f, 26f), Gold, Mathf.Max(2f, 2f * _u));
            CounterRow(px, py + 94f, "You give", OfferGive, true, hand);
            CounterRow(px, py + 144f, "You get", OfferGet, false, hand);
            bool any = false, any2 = false;
            for (int g = 0; g < Wares.RawCount; g++) { any |= OfferGive[g] > 0; any2 |= OfferGet[g] > 0; }
            if (Button(R(px + 140f, py + 220f, 120f, 34f), "Make the offer", turn && any && any2, 12f, true)) DoOffer();
        }

        private void CounterRow(float px, float y, string label, int[] counts, bool give, int[] hand)
        {
            Text(R(px + 10f, y, 60f, 46f), label, 11f, Soft, true);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = px + 72f + g * 64f;
                var rg = R(x, y, 60f, 46f);
                Fill(rg, new Color(GoodColours[g].r * 0.3f, GoodColours[g].g * 0.3f, GoodColours[g].b * 0.3f, 0.95f));
                GUI.DrawTexture(new Rect(rg.x + 5f * _u, rg.y + 1f * _u, 18f * _u, 18f * _u), IconOf(g));
                Text(new Rect(rg.x + 26f * _u, rg.y, 32f * _u, 20f * _u), counts[g].ToString(), 14f, counts[g] > 0 ? Gold : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
                if (Button(new Rect(rg.x, rg.y + 19f * _u, 30f * _u, 27f * _u), "-", counts[g] > 0, 13f, true)) AdjustOffer(give, g, -1);
                if (Button(new Rect(rg.x + 30f * _u, rg.y + 19f * _u, 30f * _u, 27f * _u), "+", give ? counts[g] < hand[g] : counts[g] < 9, 13f, true)) AdjustOffer(give, g, 1);
            }
        }

        // ---------------------------------------------------------------- offer cards (AI offers to the player)

        private void OfferCards(WorldState s, TableState t)
        {
            var offers = IncomingOffers();
            var own = OwnOpenOffer();
            const float CW = 226f, CH = 78f;
            float y = 390f - 6f - CH;
            if (_u > 0f) { }
            int shown = Mathf.Min(2, offers.Count);
            for (int i = 0; i < shown; i++)
            {
                var o = offers[i];
                var r = RRB(6f, y, CW, CH);
                Fill(r, new Color(0.10f, 0.09f, 0.08f, 0.94f));
                People pe = null; foreach (var q in s.Peoples) if (q.Id == o.From) pe = q;
                FillNoHit(new Rect(r.x, r.y, 6f * _u, r.height), pe != null ? TempleView.PeopleColour(pe) : Color.white);
                Frame(r, Gold, Mathf.Max(1.5f, 1.5f * _u));
                float frac = Mathf.Clamp01(o.TicksLeft / (float)t.Tuning.OfferTicks);
                FillNoHit(new Rect(r.x + 8f * _u, r.y + 3f * _u, (r.width - 12f * _u) * frac, 4f * _u), frac > 0.3f ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.9f, 0.3f, 0.2f));
                Text(new Rect(r.x + 10f * _u, r.y + 8f * _u, r.width - 50f * _u, 16f * _u), _d.PeopleShort(o.From) + " offer a trade", 11f, UiStyle.TextColor, true);
                Text(new Rect(r.xMax - 44f * _u, r.y + 8f * _u, 40f * _u, 16f * _u), Mathf.CeilToInt(o.TicksLeft / (float)WorldSim.TicksPerSecond) + " s", 10f, Soft, false, TextAnchor.MiddleRight);
                Text(new Rect(r.x + 10f * _u, r.y + 24f * _u, 44f * _u, 16f * _u), "You get", 10f, Soft);
                GoodsRow(new Rect(r.x + 56f * _u, r.y + 24f * _u, 170f * _u, 16f * _u), o.Give, 10f);
                Text(new Rect(r.x + 10f * _u, r.y + 40f * _u, 44f * _u, 16f * _u), "You give", 10f, Soft);
                GoodsRow(new Rect(r.x + 56f * _u, r.y + 40f * _u, 170f * _u, 16f * _u), o.Get, 10f);
                bool afford = TableRules.CanPay(t.Peoples[_d.Human].Hand, o.Get);
                if (Button(new Rect(r.x + 10f * _u, r.y + 50f * _u, 98f * _u, 26f * _u), afford ? "Accept" : "Cannot pay", afford && !GameOverShown, 11f)) AnswerOffer(o.Id, true);
                if (Button(new Rect(r.x + 114f * _u, r.y + 50f * _u, 98f * _u, 26f * _u), "Decline", !GameOverShown, 11f)) AnswerOffer(o.Id, false);
                y -= CH + 4f;
            }
            if (offers.Count > shown)
                Text(RRB(6f, y + 2f, CW, 14f), "+" + (offers.Count - shown) + " more offers", 10f, Soft, false, TextAnchor.MiddleRight);
            if (own != null)
            {
                float yy = shown > 0 ? y - (offers.Count > shown ? 14f : 0f) : 390f - 6f - 18f;
                var r = RRB(6f, yy, CW, 16f);
                FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.7f));
                Text(new Rect(r.x + 4f * _u, r.y, r.width, r.height), "Your offer is open: " + Mathf.CeilToInt(own.TicksLeft / (float)WorldSim.TicksPerSecond) + " s", 10f, Soft);
            }
        }

        /// <summary>A rect anchored to the right edge with its top at design y.</summary>
        private Rect RRB(float fromRight, float y, float w, float h) => RR(fromRight, y, w, h);

        // ---------------------------------------------------------------- discards (TU-10)

        private void DiscardModal(WorldState s, TableState t, float W)
        {
            EnsurePick();
            var hand = t.Peoples[_d.Human].Hand;
            int owed = DiscardOwed, chosen = DiscardChosen;
            const float PW = 400f, PH = 214f;
            float px = Mathf.Max(6f, (W - PW) * 0.5f), py = 70f;
            var full = new Rect(0, 0, Screen.width, Screen.height);
            Fill(full, new Color(0f, 0f, 0f, 0.45f));
            var r = R(px, py, PW, PH);
            Window(r, "Sickness. Give up " + owed + " cards.", false, out _);
            // Time limit: a bar that shrinks; when it ends the adviser chooses.
            float frac = Mathf.Clamp01(t.DiscardTicksLeft / (float)t.Tuning.DiscardTicks);
            var bar = R(px + 10f, py + 32f, PW - 20f, 10f);
            FillNoHit(bar, new Color(0.2f, 0.18f, 0.15f, 1f));
            FillNoHit(new Rect(bar.x, bar.y, bar.width * frac, bar.height), frac > 0.3f ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.9f, 0.3f, 0.2f));
            Text(R(px + 10f, py + 44f, PW - 20f, 16f), Mathf.CeilToInt(t.DiscardTicksLeft / (float)WorldSim.TicksPerSecond) + " s left, then the adviser chooses for us.", 10f, Soft);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = px + 40f + g * 64f;
                var rg = R(x, py + 66f, 60f, 56f);
                Fill(rg, new Color(GoodColours[g].r * 0.3f, GoodColours[g].g * 0.3f, GoodColours[g].b * 0.3f, 0.95f));
                GUI.DrawTexture(new Rect(rg.x + 5f * _u, rg.y + 2f * _u, 20f * _u, 20f * _u), IconOf(g));
                Text(new Rect(rg.x + 26f * _u, rg.y, 32f * _u, 24f * _u), DiscardPick[g] + " / " + hand[g], 10f, DiscardPick[g] > 0 ? Gold : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
                if (Button(new Rect(rg.x, rg.y + 27f * _u, 30f * _u, 28f * _u), "-", DiscardPick[g] > 0, 13f, true)) AdjustDiscard(g, -1);
                if (Button(new Rect(rg.x + 30f * _u, rg.y + 27f * _u, 30f * _u, 28f * _u), "+", chosen < owed && DiscardPick[g] < hand[g], 13f, true)) AdjustDiscard(g, 1);
            }
            Text(R(px + 10f, py + 130f, PW - 20f, 18f), "Chosen " + chosen + " of " + owed + ".", 12f, chosen == owed ? new Color(0.6f, 0.9f, 0.5f) : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            if (Button(R(px + 40f, py + 160f, 150f, 36f), "Choose for me", true, 12f, true)) SuggestDiscard();
            if (Button(R(px + 210f, py + 160f, 150f, 36f), "Give them up", chosen == owed, 13f, true)) DoDiscard();
        }

        // ---------------------------------------------------------------- the epidemic moment

        private void EpidemicBanner(float W)
        {
            if (_d.EpidemicAt < -50f) return;
            float age = Time.unscaledTime - _d.EpidemicAt;
            const float Total = 3.2f;
            if (age < 0f || age > Total) return;
            float a = age < 0.4f ? age / 0.4f : Mathf.Clamp01((Total - age) / (Total - 0.4f));
            FillNoHit(new Rect(0, 0, Screen.width, Screen.height), new Color(0.12f, 0.02f, 0.02f, 0.5f * a));
            var band = R(0f, 150f, W, 56f);
            FillNoHit(band, new Color(0.05f, 0.02f, 0.02f, 0.75f * a));
            Text(band, "The gods are angry.", 26f, new Color(1f, 0.78f, 0.7f, a), true, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- game over

        private void GameOverModal(WorldState s, TableState t, float W)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f));
            const float PW = 360f, PH = 224f;
            float px = (W - PW) * 0.5f, py = 80f;
            var r = R(px, py, PW, PH);
            Fill(r, Panel);
            Frame(r, Gold, Mathf.Max(2f, 2f * _u));
            bool lost = t.IslandFell;
            Text(R(px, py + 10f, PW, 34f), lost ? "The island falls." : "The age has turned.", 24f, lost ? new Color(1f, 0.7f, 0.6f) : Gold, true, TextAnchor.MiddleCenter);
            string sub;
            if (lost) sub = "The sickness has taken the land. Nobody wins.";
            else if (t.Winner == _d.Human) sub = "You won: our people reached " + PointsOf(t.Winner) + " points.";
            else sub = _d.PeopleShort(t.Winner) + " won: they reached " + PointsOf(t.Winner) + " points.";
            Text(R(px, py + 46f, PW, 20f), sub, 12f, UiStyle.TextColor, false, TextAnchor.MiddleCenter);
            var order = new List<int>();
            for (int p = 0; p < t.PeopleCount; p++) order.Add(p);
            order.Sort((a, b) => PointsOf(b).CompareTo(PointsOf(a)));
            float y = py + 72f;
            foreach (int p in order)
            {
                People pe = null; foreach (var q in s.Peoples) if (q.Id == p) pe = q;
                FillNoHit(R(px + 60f, y + 3f, 8f, 14f), pe != null ? TempleView.PeopleColour(pe) : Color.white);
                Text(R(px + 76f, y, 140f, 20f), _d.PeopleShort(p), 12f, p == t.Winner ? Gold : UiStyle.TextColor, p == t.Winner);
                Text(R(px + 220f, y, 80f, 20f), PointsOf(p) + " pts", 12f, Soft, false, TextAnchor.MiddleRight);
                y += 20f;
            }
            if (Button(R(px + (PW - 170f) * 0.5f, py + PH - 44f, 170f, 36f), "Return to the world", true, 13f, true)) _d.Leave();
        }

        private int PointsOf(int p) => p >= 0 && p < _d.Points.Length ? _d.Points[p] : 0;
    }
}
