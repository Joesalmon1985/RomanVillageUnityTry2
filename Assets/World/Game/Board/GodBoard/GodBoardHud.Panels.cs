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

    }
}
