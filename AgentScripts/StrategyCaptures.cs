using System.Linq;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

/// <summary>
/// Strategy UI capture states and AutoPlay runs in Play Mode. Run an entry, then capture:
///   unity command script run --file AgentScripts/StrategyCaptures.cs --entry StrategyCaptures.Run --args full_game
///   unity command capture game_view --output-path Evidence/screenshots/strat_<name>.png
/// Fast-forwarding uses the full_game driver (commands through the UI paths) and GameClock.StepOnce.
/// </summary>
public static class StrategyCaptures
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static WorldState S => B.Sim.State;
    static FullGameDriver _d;

    public static string Run(string name)
    {
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown " + name;
        B.AutoPlay.Run(s);
        return "started " + name;
    }

    public static string State()
    {
        var a = B.AutoPlay;
        var st = StrategyApi.Status(S);
        return "tick " + S.Tick + " (min " + (S.Tick / 600f).ToString("0.0") + ") " + st.Headline + " VP " + StrategyApi.Score(S, 0).Total + "-" + StrategyApi.Score(S, 1).Total
            + " breach " + st.Breaches + " draft " + st.DraftPending + " autoplay " + (a.IsRunning ? "running" : a.Failed ? "FAILED " + a.FailMessage : "idle")
            + " views: city " + StrategyViews.Instance.CityViewCount + " demons " + StrategyViews.Instance.DemonViewCount + " squads " + StrategyViews.Instance.SquadViewCount
            + " robbed " + StrategyViews.Instance.RobbedHexCount + " power " + StrategyViews.Instance.PowerSignCount + " wonder " + StrategyViews.Instance.WonderStagesShown
            + " | what now: " + (StrategyHud.Instance != null ? StrategyHud.Instance.WhatNow : "-")
            + " fps~" + (1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("0");
    }

    /// <summary>Plays the full_game driver (through the UI) until the condition holds or the budget runs out. Pauses the clock after.</summary>
    static string Until(System.Func<bool> cond, int maxTicks)
    {
        if (_d == null || _d.Sim != B.Sim) _d = new FullGameDriver(B.Sim, false, true);
        B.Clock.Pause("Capture");
        int n = 0;
        while (!cond() && n < maxTicks && !S.Strategy.GameOver) { _d.Act(); B.Clock.StepOnce(); n++; }
        return (cond() ? "reached" : "NOT reached") + " after " + n + " ticks; " + State();
    }

    public static string Resume() { B.Clock.Resume("Capture"); return "running"; }
    public static string Pause() { B.Clock.Pause("Capture"); return "paused"; }

    public static string UntilDraft() => Until(() => StrategyApi.DraftPending(S), 20000);
    public static string UntilAge(int age) => Until(() => S.Strategy.Age >= age && !StrategyApi.DraftPending(S), 30000);
    public static string UntilCities(int n) => Until(() => StrategyApi.PlayerCities(S).Count >= n, 30000);
    public static string UntilWonder(int stages) => Until(() => S.Strategy.Wonder.StagesDone >= stages, 40000);
    public static string UntilDemons() => Until(() => StrategyApi.Demons(S).Count >= 3, 30000);
    public static string UntilBarracks() => Until(() => StrategyUi.FirstBarracks(S, true) >= 0 && S.Military.Queues.Any(q => q.Orders.Count > 0), 40000);
    public static string UntilAttack() => Until(() => MilitaryApi.Squads(S, 0).Any(x => x.Engaged || (x.Order == OrderKind.Attack && x.At == S.Board.RivalOutpostVertex)), 60000);
    public static string UntilBreaches(int n) => Until(() => S.Strategy.Breaches >= n, 60000);
    /// <summary>A careless player (never moves the hero) until the breach track reaches n: demons pile up, cities get robbed.</summary>
    public static string CarelessUntilBreaches(int n)
    {
        _d = new FullGameDriver(B.Sim, false, true) { IgnoreDemons = true };
        return Until(() => S.Strategy.Breaches >= n, 60000);
    }

    public static string CarelessUntilRobbed()
    {
        _d = new FullGameDriver(B.Sim, false, true) { IgnoreDemons = true };
        return Until(() => StrategyViews.Instance.RobbedHexCount > 0 || Enumerable.Range(0, S.Board.HexTerrain.Length).Any(h => StrategyApi.IsHexRobbed(S, h)), 60000);
    }

    public static string FocusRobbed(float size = 140f)
    {
        for (int h = 0; h < S.Board.HexTerrain.Length; h++)
            if (StrategyApi.IsHexRobbed(S, h)) { var p = BoardSpace.Hex(h); return God(p.x, p.z, size) + " robbed hex " + h; }
        return "none robbed";
    }

    public static string FocusOutpost(float size = 60f) => GodAtVertex(S.Board.RivalOutpostVertex, size);

    public static string FocusWonder(float size = 40f)
    {
        int id = EconomyApi.FindBuilding(S, BuildingType.WonderSite, true);
        return id < 0 ? "no wonder" : GodAtVertex(S.Economy.Buildings[id].Vertex, size);
    }

    public static string FocusBarracks(float size = 45f)
    {
        int id = StrategyUi.FirstBarracks(S);
        if (id < 0) return "no barracks";
        B.Picker.Select(EconomyViews.Instance.Building(id));
        return GodAtVertex(S.Economy.Buildings[id].Vertex, size);
    }

    public static string FocusCity(float size = 130f)
    {
        var c = StrategyApi.PlayerCities(S).LastOrDefault();
        return GodAtVertex(c, size);
    }

    public static string HeroHover()
    {
        Tool(StrategyGodTools.HeroId);
        return GodAtVertex(S.Strategy.HeroVertex, 90f);
    }

    public static string UntilEnd() => Until(() => S.Strategy.GameOver, 60000);
    public static string Ticks(int n) => Until(() => false, n);

    public static string God(float x, float z, float size)
    {
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(new Vector3(x, 0, z), size);
        return "god at " + x + "," + z + " size " + size;
    }

    public static string GodAtVertex(int v, float size)
    {
        var p = BoardSpace.Vertex(v);
        return God(p.x, p.z, size);
    }

    public static string Tool(string id) { B.Views.SetView(ViewMode.God); StrategyWindow.Close(); GodToolbar.Instance.Activate(id); return "tool " + id; }
    public static string Window(string kind) { StrategyWindow.Open((StrategyPanels.Kind)System.Enum.Parse(typeof(StrategyPanels.Kind), kind)); return "window " + kind; }
    public static string CloseWindow() { StrategyWindow.Close(); return "closed"; }

    public static string FocusDemons(float size = 70f)
    {
        var d = StrategyApi.Demons(S).OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (d.Value == 0) return "no demons";
        return GodAtVertex(d.Key, size) + " demons " + d.Value + " at " + d.Key;
    }

    public static string FirstPersonAt(int v, float back, float yawOffset)
    {
        B.Views.SetView(ViewMode.FirstPerson);
        var p = BoardSpace.Vertex(v);
        var from = p + new Vector3(back, 0, -back);
        B.Avatar.Teleport(from + Vector3.up * 0.05f, BoardSpace.YawOf(p - from) + yawOffset, 6f);
        return "fp near " + v;
    }

    public static string FirstPersonDemons()
    {
        var d = StrategyApi.Demons(S).OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (d.Value == 0) return "no demons";
        return FirstPersonAt(d.Key, 5f, 0f);
    }

    public static string Measure()
    {
        var s = new AutoPlayScript("strat_perf");
        s.Call(() => { B.Clock.Resume("Capture"); B.Views.SetView(ViewMode.God); B.GodCam.FrameHome(); })
         .Wait(1f).MeasureFps("late game God View home", 3f)
         .Call(() => B.GodCam.SetFocus(B.Layout.VillageCentre, 300f)).Wait(1f).MeasureFps("late game God View wide", 3f)
         .Call(() => B.GodCam.FrameIsland()).Wait(1f).MeasureFps("late game whole island", 3f)
         .Call(() => { B.Views.SetView(ViewMode.FirstPerson); B.Avatar.Teleport(B.Layout.Spawn + Vector3.up * 0.05f, B.Layout.SpawnYaw, 3f); })
         .Wait(1f).MeasureFps("late game first person", 3f)
         .Log("[AutoPlay] strat_perf done " + State());
        B.AutoPlay.Run(s);
        return "measuring";
    }
}
