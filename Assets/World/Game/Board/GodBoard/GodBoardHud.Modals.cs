using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// G1-6b: the trade and discard modals (rows of goods cards, a strong title band, round cancel and confirm buttons), the
    /// offer cards above the hourglass, and the end panel. The logic they call is in <c>GodBoardHud.Panels.cs</c>.
    /// </summary>
    public sealed partial class GodBoardHud
    {
        private static readonly Color Band = new Color(0.46f, 0.32f, 0.12f, 1f);
        private static readonly Color Ok = new Color(0.22f, 0.50f, 0.22f, 1f);
        private static readonly Color No = new Color(0.55f, 0.20f, 0.16f, 1f);

        private void Dim(float a) => Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, a));

        /// <summary>A modal window: the panel, a title band and the title. Returns the band rect.</summary>
        private Rect ModalWindow(float px, float py, float pw, float ph, string title)
        {
            var r = R(px, py, pw, ph);
            Fill(r, Panel);
            Frame(r, Gold, Mathf.Max(2f, 2f * _u));
            var band = new Rect(r.x, r.y, r.width, 30f * _u);
            FillNoHit(band, Band);
            Text(new Rect(band.x + 12f * _u, band.y, 200f * _u, band.height), title, 15f, UiStyle.TextColor, true);
            return band;
        }

        private void IconsFor(Rect at, int good, int count, float step)
        {
            float x = at.x;
            for (int k = 0; k < count; k++)
            {
                GUI.DrawTexture(new Rect(x, at.y, at.height, at.height), IconOf(good));
                x += step;
            }
        }

        private void GoodsRow(Rect at, int[] counts, float size)
        {
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

        // ---------------------------------------------------------------- trade

        private void TradeModal(WorldState s, TableState t, float W)
        {
            const float PW = 440f, PH = 276f;
            float px = (W - PW) * 0.5f, py = 54f;
            Dim(0.4f);
            var band = ModalWindow(px, py, PW, PH, "Trade");
            bool turn = CanAct;
            // the switch: Peoples / Bank or port
            for (int i = 0; i < 2; i++)
            {
                var sw = R(px + 150f + i * 128f, py + 4f, 122f, 22f);
                FillNoHit(sw, TradeTab == (i == 0 ? 1 : 0) ? new Color(0.95f, 0.78f, 0.3f) : new Color(0.25f, 0.18f, 0.08f));
                Text(sw, i == 0 ? "Peoples" : "Bank or port", 11f, TradeTab == (i == 0 ? 1 : 0) ? new Color(0.15f, 0.1f, 0.04f) : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
                if (Tap(sw, true, true)) SetTab(i == 0 ? 1 : 0);
                Texts.Add("[" + (i == 0 ? "Peoples" : "Bank or port") + "]");
            }
            if (TradeTab == 0) BankTab(s, t, px, py, turn); else OfferTab(s, t, px, py, turn);
            string msg = !turn ? "Wait for your turn." : TradeMessage;
            Text(R(px + 70f, py + PH - 20f, PW - 140f, 16f), msg, 11f, msg == "Traded." || msg.StartsWith("Offer made") ? new Color(0.6f, 0.9f, 0.5f) : new Color(1f, 0.7f, 0.55f), true, TextAnchor.MiddleCenter);
            if (RoundButton(R(px + 12f, py + PH - 52f, 40f, 40f), GCross, "Close trade", No, true, false, true)) CloseTrade();
            bool can;
            if (TradeTab == 0) can = turn && BankGive >= 0 && BankGet >= 0 && BankGive != BankGet && t.Peoples[_d.Human].Hand[BankGive] >= BankRate(BankGive);
            else { bool a = false, b = false; for (int g = 0; g < Wares.RawCount; g++) { a |= OfferGive[g] > 0; b |= OfferGet[g] > 0; } can = turn && a && b; }
            if (RoundButton(R(px + PW - 52f, py + PH - 52f, 40f, 40f), GCheck, TradeTab == 0 ? "Trade" : "Make the offer", Ok, can, false, true))
            { if (TradeTab == 0) DoBankTrade(); else DoOffer(); }
        }

        private void BankTab(WorldState s, TableState t, float px, float py, bool turn)
        {
            var hand = t.Peoples[_d.Human].Hand;
            Text(R(px + 10f, py + 62f, 60f, 16f), "Give", 12f, Soft, true);
            Text(R(px + 10f, py + 142f, 60f, 16f), "Get", 12f, Soft, true);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = px + 80f + g * 68f;
                int rate = BankRate(g);
                bool afford = hand[g] >= rate;
                var rg = R(x, py + 42f, 56f, 64f);
                GoodsCard(rg, g, hand[g], !afford, BankGive == g);
                if (Tap(rg, true, false)) SelectBankGive(g);
                // the rate as a picture: this many of the good
                float step = Mathf.Min(11f, 52f / Mathf.Max(1, rate));
                var pics = R(x + 2f, py + 108f, 0f, 11f);
                IconsFor(new Rect(pics.x, pics.y, 11f * _u, 11f * _u), g, rate, step * _u);
                var rt = R(x, py + 122f, 56f, 64f);
                GoodsCard(rt, g, 0, false, BankGet == g, false);
                if (Tap(rt, true, false)) SelectBankGet(g);
            }
            // the deal as pictures
            var line = R(px + 80f, py + 194f, 330f, 22f);
            if (BankGive >= 0 && BankGet >= 0 && BankGive != BankGet)
            {
                int rate = BankRate(BankGive);
                float cx = (line.x + line.xMax) * 0.5f - (rate * 13f + 40f) * _u * 0.5f;
                IconsFor(new Rect(cx, line.y, 22f * _u, 22f * _u), BankGive, rate, 13f * _u);
                float ax = cx + rate * 13f * _u + 10f * _u;
                GlyphAt(new Rect(ax, line.y + 2f * _u, 20f * _u, 20f * _u), GSwap, Gold);
                IconsFor(new Rect(ax + 28f * _u, line.y, 22f * _u, 22f * _u), BankGet, 1, 0f);
            }
        }

        private void OfferTab(WorldState s, TableState t, float px, float py, bool turn)
        {
            var hand = t.Peoples[_d.Human].Hand;
            Text(R(px + 10f, py + 62f, 60f, 16f), "Give", 12f, Soft, true);
            Text(R(px + 10f, py + 142f, 60f, 16f), "Get", 12f, Soft, true);
            for (int row = 0; row < 2; row++)
            {
                bool give = row == 0;
                var arr = give ? OfferGive : OfferGet;
                for (int g = 0; g < Wares.RawCount; g++)
                {
                    float x = px + 80f + g * 68f, y = py + 42f + row * 80f;
                    var rg = R(x, y, 56f, 64f);
                    bool can = give ? arr[g] < hand[g] : arr[g] < 9;
                    GoodsCard(rg, g, arr[g], give && hand[g] == 0, arr[g] > 0, true, arr[g] > 0 ? new Color(0.62f, 0.45f, 0.08f) : (Color?)null);
                    if (Tap(rg, can, false)) AdjustOffer(give, g, 1);
                    if (arr[g] > 0)
                    {
                        var mr = R(x + 18f, y + 66f, 20f, 20f);
                        if (RoundButton(mr, GMinus, (give ? "Less give " : "Less get ") + GoodNames[g], new Color(0.3f, 0.22f, 0.12f), true, false, true)) AdjustOffer(give, g, -1);
                    }
                }
            }
            // who the offer goes to: people emblems, then "everyone"
            float ex = px + 80f;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                if (p == _d.Human) continue;
                var e = R(ex, py + 218f, 28f, 28f);
                if (OfferTo == p) Disc(new Rect(e.x - 3f * _u, e.y - 3f * _u, e.width + 6f * _u, e.height + 6f * _u), Gold);
                Disc(e, PeopleCol(p));
                string nm = _d.PeopleShort(p);
                Text(e, nm.Substring(0, 1), 13f, Color.white, true, TextAnchor.MiddleCenter);
                if (Tap(e, true, true)) SetOfferTo(p);
                Texts.Add("[To " + nm + "]");
                ex += 36f;
            }
            var all = R(ex, py + 218f, 40f, 28f);
            FillNoHit(all, OfferTo == -1 ? Gold : new Color(0.3f, 0.24f, 0.14f));
            Text(all, "All", 12f, OfferTo == -1 ? new Color(0.15f, 0.1f, 0.04f) : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            if (Tap(all, true, true)) SetOfferTo(-1);
            Texts.Add("[To all]");
        }

        // ---------------------------------------------------------------- offer cards (AI offers to the player)

        private void OfferCards(WorldState s, TableState t)
        {
            var offers = IncomingOffers();
            var own = OwnOpenOffer();
            const float CW = 218f, CH = 74f;
            float bottom = 8f + 70f + 8f;
            int shown = Mathf.Min(2, offers.Count);
            for (int i = 0; i < shown; i++)
            {
                var o = offers[i];
                var r = RRB(8f, bottom, CW, CH);
                Fill(r, new Color(0.10f, 0.09f, 0.08f, 0.95f));
                var pc = PeopleCol(o.From);
                FillNoHit(new Rect(r.x, r.y, 6f * _u, r.height), pc);
                Frame(r, Gold, Mathf.Max(1.5f, 1.5f * _u));
                var em = Sub(r, _u, 12f, 5f, 18f, 18f);
                Disc(em, pc);
                string nm = _d.PeopleShort(o.From);
                Text(em, nm.Substring(0, 1), 10f, Color.white, true, TextAnchor.MiddleCenter);
                Text(Sub(r, _u, 34f, 4f, 110f, 20f), nm + " offer", 11f, UiStyle.TextColor, true);
                Text(Sub(r, _u, 12f, 26f, 40f, 18f), "You get", 10f, Soft);
                GoodsRow(Sub(r, _u, 54f, 26f, 100f, 18f), o.Give, 10f);
                Text(Sub(r, _u, 12f, 46f, 40f, 18f), "You give", 10f, Soft);
                GoodsRow(Sub(r, _u, 54f, 46f, 100f, 18f), o.Get, 10f);
                bool afford = TableRules.CanPay(t.Peoples[_d.Human].Hand, o.Get);
                if (RoundButton(Sub(r, _u, 158f, 20f, 30f, 30f), GCheck, afford ? "Accept" : "Cannot pay", Ok, afford && !GameOverShown)) AnswerOffer(o.Id, true);
                if (RoundButton(Sub(r, _u, 188f, 20f, 26f, 26f), GCross, "Decline", No, !GameOverShown)) AnswerOffer(o.Id, false);
                // the expiry as a draining edge along the bottom
                float frac = Mathf.Clamp01(o.TicksLeft / (float)t.Tuning.OfferTicks);
                FillNoHit(new Rect(r.x + 6f * _u, r.yMax - 5f * _u, r.width - 6f * _u, 5f * _u), new Color(0.2f, 0.18f, 0.15f));
                FillNoHit(new Rect(r.x + 6f * _u, r.yMax - 5f * _u, (r.width - 6f * _u) * frac, 5f * _u), frac > 0.3f ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.9f, 0.3f, 0.2f));
                bottom += CH + 4f;
            }
            if (offers.Count > shown)
            {
                Text(RRB(8f, bottom, CW, 14f), "+" + (offers.Count - shown) + " more offers", 10f, Soft, false, TextAnchor.MiddleRight);
                bottom += 14f;
            }
            if (own != null)
            {
                var r = RRB(8f, bottom, CW, 16f);
                FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.7f));
                float frac = Mathf.Clamp01(own.TicksLeft / (float)t.Tuning.OfferTicks);
                FillNoHit(new Rect(r.x, r.yMax - 3f * _u, r.width * frac, 3f * _u), Gold);
                Text(new Rect(r.x + 4f * _u, r.y, r.width, r.height), "Your offer is open.", 10f, Soft);
            }
        }

        // ---------------------------------------------------------------- discards (TU-10)

        private void DiscardModal(WorldState s, TableState t, float W)
        {
            EnsurePick();
            var hand = t.Peoples[_d.Human].Hand;
            int owed = DiscardOwed, chosen = DiscardChosen;
            const float PW = 400f, PH = 226f;
            float px = (W - PW) * 0.5f, py = 74f;
            Dim(0.5f);
            var band = ModalWindow(px, py, PW, PH, "Sickness: give up " + owed + (owed == 1 ? " card" : " cards"));
            // the timer: a bar along the bottom of the title band
            float frac = Mathf.Clamp01(t.DiscardTicksLeft / (float)t.Tuning.DiscardTicks);
            var bar = new Rect(band.x, band.yMax - 6f * _u, band.width, 6f * _u);
            FillNoHit(bar, new Color(0.2f, 0.14f, 0.06f));
            FillNoHit(new Rect(bar.x, bar.y, bar.width * frac, bar.height), frac > 0.3f ? new Color(0.98f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.2f));
            Text(R(px + 200f, py + 2f, 190f, 20f), Mathf.CeilToInt(t.DiscardTicksLeft / (float)WorldSim.TicksPerSecond) + " s, then the adviser chooses", 10f, new Color(1f, 0.95f, 0.8f), false, TextAnchor.MiddleRight);
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float x = px + 30f + g * 70f;
                var rg = R(x, py + 52f, 58f, 70f);
                bool canAdd = chosen < owed && DiscardPick[g] < hand[g];
                GoodsCard(rg, g, DiscardPick[g] > 0 ? DiscardPick[g] : hand[g], hand[g] == 0, DiscardPick[g] > 0, true, DiscardPick[g] > 0 ? new Color(0.62f, 0.45f, 0.08f) : (Color?)null);
                if (Tap(rg, canAdd, true)) AdjustDiscard(g, 1);
                if (DiscardPick[g] > 0 && RoundButton(R(x + 19f, py + 126f, 20f, 20f), GMinus, "Less " + GoodNames[g], new Color(0.3f, 0.22f, 0.12f), true, false, true)) AdjustDiscard(g, -1);
            }
            Text(R(px + 10f, py + 154f, PW - 20f, 18f), "Chosen " + chosen + " of " + owed + ".", 12f, chosen == owed ? new Color(0.6f, 0.9f, 0.5f) : UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            if (Button(R(px + 14f, py + PH - 46f, 140f, 34f), "Choose for me", true, 12f, true)) SuggestDiscard();
            if (RoundButton(R(px + PW - 54f, py + PH - 52f, 42f, 42f), GCheck, "Give them up", Ok, chosen == owed, false, true)) DoDiscard();
        }

        // ---------------------------------------------------------------- game over

        private void GameOverModal(WorldState s, TableState t, float W)
        {
            Dim(0.62f);
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
                var em = R(px + 56f, y + 1f, 18f, 18f);
                Disc(em, PeopleCol(p));
                Text(R(px + 82f, y, 140f, 20f), _d.PeopleShort(p), 12f, p == t.Winner ? Gold : UiStyle.TextColor, p == t.Winner);
                Text(R(px + 220f, y, 80f, 20f), PointsOf(p) + " pts", 12f, Soft, false, TextAnchor.MiddleRight);
                y += 22f;
            }
            if (Button(R(px + (PW - 170f) * 0.5f, py + PH - 44f, 170f, 36f), "Return to the world", true, 13f, true)) _d.Leave();
        }
    }
}
