using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Table;

/// <summary>G1-6 play-mode driver for captures (unity command script run --file AgentScripts/GodBoardG16.cs --entry GodBoardG16.X).</summary>
public static class GodBoardG16
{
    private static WorldBootstrap B => WorldBootstrap.Instance;
    private static GodBoardDirector D => GodBoardDirector.Instance;
    private static GodBoardHud H => D.GetComponent<GodBoardHud>();

    public static string Prep()
    {
        GodBoardDirector.RiseSeconds = 0.05f;
        if (DayNight.Instance != null) DayNight.Instance.SetHour(11);
        return D.TrySacrifice(true) ? "sacrificed" : "refused";
    }

    public static string Place()
    {
        B.Clock.Pause("g16");
        D.TapAt(D.AdviseCorner >= 0 ? D.ScreenOfCorner(D.AdviseCorner) : Vector2.zero);
        for (int i = 0; i < 3; i++) B.Clock.StepOnce();
        if (D.EdgeSpots.Count > 0) D.TapAt(D.ScreenOfEdge(D.EdgeSpots[0]));
        var t = D.T; int g = 0;
        while (!(t.Phase == TablePhase.Main && t.Current == 0) && g++ < 30000) B.Clock.StepOnce();
        return "phase=" + t.Phase + " cur=" + t.Current + " steps=" + g;
    }

    public static string Trade()
    {
        var hand = D.T.Peoples[0].Hand; hand[0] = 6; hand[1] = 2; hand[2] = 3; hand[3] = 1; hand[4] = 0;
        H.OpenTrade(); H.SelectBankGive(0); H.SelectBankGet(4);
        return "trade";
    }

    public static string OfferTab() { H.SetTab(1); H.AdjustOffer(true, 0, 1); H.AdjustOffer(true, 0, 1); H.AdjustOffer(false, 4, 1); return "offer"; }

    public static string Card()
    {
        H.CloseTrade();
        var t = D.T;
        for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == 1 && t.CornerLevel[c] == 1) { t.CornerLevel[c] = 2; break; }
        t.Peoples[1].Hand[1] = 3;
        var o = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, TicksLeft = 110, Status = OfferStatus.Open };
        o.Give[1] = 1; o.Get[3] = 1; t.Offers.Add(o);
        var o2 = new TradeOffer { Id = t.NextOfferId++, From = 2, To = -1, TicksLeft = 60, Status = OfferStatus.Open };
        o2.Give[2] = 1; o2.Get[0] = 2; t.Offers.Add(o2);
        t.Peoples[2].Hand[2] = 2;
        return "card";
    }

    public static string Discard()
    {
        var t = D.T; var hand = t.Peoples[0].Hand;
        t.Offers.Clear();
        hand[0] = 4; hand[1] = 3; hand[2] = 2; hand[3] = 1; hand[4] = 0;
        t.Phase = TablePhase.Discard; t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 5; t.DiscardTicksLeft = 100;
        H.AdjustDiscard(0, 1); H.AdjustDiscard(0, 1); H.AdjustDiscard(1, 1);
        return "discard";
    }

    public static string Over()
    {
        TableEngine eng = null;
        foreach (var s in B.Sim.Systems) if (s is TableEngine te) eng = te;
        eng.IslandFalls(B.Sim.State, new EventBuffer());
        return "over";
    }

    public static string ShotTrade() { return Shot("g16_trade_panel"); }
    public static string ShotOffer() { return Shot("g16_offer_tab"); }
    public static string ShotCard() { return Shot("g16_offer_card"); }
    public static string ShotDiscard() { return Shot("g16_discard_picker"); }
    public static string ShotOver() { return Shot("g16_game_over"); }
    public static string Info() { var t = D.T; return "view=" + D.View + " phase=" + t.Phase + " cur=" + t.Current + " adv=" + D.AdviseCorner + " corners=" + D.CornerSpots.Count + " setupCorner=" + t.SetupCorner; }

    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot"; }
}
