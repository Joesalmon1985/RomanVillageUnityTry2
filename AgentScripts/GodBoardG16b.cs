using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Table;

/// <summary>G1-6b capture driver (unity command script run --file AgentScripts/GodBoardG16b.cs --entry GodBoardG16b.X). Use with GodBoardG16 (Prep, Place, Trade, Card, Discard, Over).</summary>
public static class GodBoardG16b
{
    private static GodBoardDirector D => GodBoardDirector.Instance;
    private static GodBoardHud H => D.GetComponent<GodBoardHud>();

    public static string Hand()
    {
        var hand = D.T.Peoples[0].Hand; hand[0] = 4; hand[1] = 3; hand[2] = 2; hand[3] = 1; hand[4] = 0;
        return "hand";
    }

    public static string Rich()
    {
        var hand = D.T.Peoples[0].Hand; hand[0] = 3; hand[1] = 3; hand[2] = 3; hand[3] = 2; hand[4] = 3;
        return "rich";
    }

    public static string BuildRoad() { if (D.BuildMode != BuildKind.Road) D.SetBuildMode(BuildKind.Road); return "mode=" + D.BuildMode; }
    public static string BuildCamp() { if (D.BuildMode != BuildKind.Settlement) D.SetBuildMode(BuildKind.Settlement); return "mode=" + D.BuildMode; }
    public static string ClearMode() { if (D.BuildMode != BuildKind.None) D.SetBuildMode(D.BuildMode); return "mode=" + D.BuildMode; }
    public static string Epidemic() { D.ShowMoment("epidemic", "The gods are angry.", "Sickness runs through the camps.", 30f); return "moment"; }
    public static string Crystal() { D.ShowMoment("crystal", "A crystal returns.", "A free city rises for us.", 30f); return "moment"; }
    public static string Burned() { D.ShowMoment("burned", "A camp burns.", "Our people flee the smoke.", 30f); return "moment"; }
    public static string Prince() { D.ShowMoment("prince", "The prince advises", "Build a road toward the wood.", 30f); return "moment"; }
    public static string NoMoment() { D.ShowMoment("", "", "", 0f); return "none"; }
    public static string LogOpen() { H.SetLogOpen(true); return "log open"; }
    public static string LogClose() { H.SetLogOpen(false); return "log closed"; }
    public static string TradePeoples() { H.OpenTrade(); H.SetTab(1); H.AdjustOffer(true, 0, 1); H.AdjustOffer(true, 0, 1); H.AdjustOffer(false, 4, 1); H.SetOfferTo(1); return "peoples"; }
    public static string Texts() { return string.Join("|", H.Texts); }
    public static string Info() { var t = D.T; return "view=" + D.View + " phase=" + t.Phase + " cur=" + t.Current + " mode=" + D.BuildMode + " corners=" + D.CornerSpots.Count + " edges=" + D.EdgeSpots.Count + " upg=" + D.UpgradeSpots.Count + " feed=" + D.FeedTotal; }
    public static string Shot_turn() { return Shot("g16b_turn"); }
    public static string Shot_build_road() { return Shot("g16b_build_road"); }
    public static string Shot_build_camp() { return Shot("g16b_build_camp"); }
    public static string Shot_trade_bank() { return Shot("g16b_trade_bank"); }
    public static string Shot_trade_peoples() { return Shot("g16b_trade_peoples"); }
    public static string Shot_offer_card() { return Shot("g16b_offer_card"); }
    public static string Shot_discard() { return Shot("g16b_discard"); }
    public static string Shot_epidemic() { return Shot("g16b_epidemic"); }
    public static string Shot_crystal() { return Shot("g16b_crystal"); }
    public static string Shot_burned() { return Shot("g16b_burned"); }
    public static string Shot_prince() { return Shot("g16b_prince"); }
    public static string Shot_game_over() { return Shot("g16b_game_over"); }
    public static string Shot_log_open() { return Shot("g16b_log_open"); }
    public static string Shot_log_closed() { return Shot("g16b_log_closed"); }
    /// <summary>Sends villagers for every hex round the player's start corner, and steps them partway (the table clock may be paused).</summary>
    public static string Walk()
    {
        var b = D.S.Board;
        var hexes = new System.Collections.Generic.List<int>(b.Topology.VertexHexes(b.StartCorners[0]));
        int n = D.Collectors.OnProduced(new Produced { Hexes = hexes.ToArray() });
        D.Collectors.Step(1.3f);
        return "sent=" + n + " active=" + D.Collectors.Active.Count;
    }
    public static string Shot_walkers() { return Shot("g16b_villagers_trail"); }
    public static string Shot_ports() { return Shot("g16b_port_tokens"); }
    public static string Shot(string name) { ScreenCapture.CaptureScreenshot("Evidence/godboard/" + name + ".png"); return "shot"; }
}
