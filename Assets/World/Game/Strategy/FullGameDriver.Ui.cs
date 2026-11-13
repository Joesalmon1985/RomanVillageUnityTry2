using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    public sealed partial class FullGameDriver
    {
        /// <summary>Player heuristic (not sim tuning): switch the brick kiln off above this many bricks once the smelter runs.</summary>
        public int BrickKilnOffAt = 12;
        /// <summary>Player heuristic: switch it back on below this many.</summary>
        public int BrickKilnOnBelow = 6;

        public int UiActions { get; private set; }
        public int Refusals { get; private set; }
        public readonly Dictionary<string, int> ByCommand = new Dictionary<string, int>();

        /// <summary>
        /// Sends a command through the UI path a player's click takes: the Build and Road tools, the City and Hero tools,
        /// the End Round toolbar action, the draft / cards / market / wonder / army panel actions, the inspect buttons.
        /// </summary>
        public CommandResult SubmitThroughUi(Command c)
        {
            CommandResult r;
            var bar = GodToolbar.Instance;
            switch (c)
            {
                case PlaceBuilding pb:
                    var bt = (BuildTool)bar.Activate(BuildTool.ToolId);
                    bt.SelectType(pb.Type);
                    r = bt.TryPlace(pb.Vertex);
                    break;
                case PlaceRoad pr: r = ((RoadTool)bar.Activate(RoadTool.ToolId)).Place(pr.Edge); break;
                case EndRound _:
                    bar.Activate(EconomyGodTools.EndRoundId);
                    r = GodCommands.LastCommand is EndRound ? GodCommands.LastResult : CommandResult.Reject("no_end_round", "End Round did not run.");
                    break;
                case FoundCity fc: r = ((CityTool)bar.Activate(StrategyGodTools.CityId)).TryFound(fc.Vertex); break;
                case MoveHero mh: r = ((HeroTool)bar.Activate(StrategyGodTools.HeroId)).TryMove(mh.Vertex); break;
                case DraftPick dp: r = StrategyUi.Pick(dp.CardId); break;
                case BuildCard bc: bar.Activate(StrategyGodTools.CardsId); r = StrategyUi.BuildCard(bc.CardId, bc.CityVertex); break;
                case DiscardCard dc: bar.Activate(StrategyGodTools.CardsId); r = StrategyUi.DiscardCard(dc.CardId); break;
                case Trade t: bar.Activate(StrategyGodTools.MarketId); r = StrategyUi.Trade(t.Give, t.Get, t.Count); break;
                case Buy b: bar.Activate(StrategyGodTools.MarketId); r = StrategyUi.Buy(b.Ware, b.Count); break;
                case Sell sl: bar.Activate(StrategyGodTools.MarketId); r = StrategyUi.Sell(sl.Ware, sl.Count); break;
                case BuildWonderStage _: bar.Activate(StrategyGodTools.WonderId); r = StrategyUi.OrderWonderStage(); break;
                case QueueUnit q: bar.Activate(StrategyGodTools.ArmyId); r = StrategyUi.QueueUnit(q.BarracksId, q.Count); break;
                case OrderSquad o: bar.Activate(StrategyGodTools.ArmyId); r = StrategyUi.OrderSquad(o.SquadId, o.Order, o.Target); break;
                case CancelOrder co: bar.Activate(StrategyGodTools.ArmyId); r = StrategyUi.CancelOrder(co.SquadId); break;
                case SetPriority sp: r = GodCommands.SetPriority(sp.BuildingId, sp.Priority); break;
                case ToggleBuilding tb: r = GodCommands.Toggle(tb.BuildingId, tb.Enabled); break;
                default: r = GodCommands.Submit(c, c.GetType().Name + " sent."); break;
            }
            UiActions++;
            string k = c.GetType().Name;
            ByCommand[k] = ByCommand.TryGetValue(k, out int n) ? n + 1 : 1;
            if (!r.Ok && !(c is EndRound)) Refusals++;
            return r;
        }

        // ---------------- planning helpers (same rules as EconomyTestKit) ----------------

        private static class Kit
        {
            public static int HomeHex(WorldState s, World.Sim.Terrain t) => CommissionDriver.HomeHex(s, t);
            public static Func<int, bool> Touches(WorldState s, int hex) => CommissionDriver.Touches(s, hex);
            public static int[] PathToNetwork(WorldState s, int v) => CommissionDriver.PathToNetwork(s, v);
            public static List<int> RoadsToConnect(WorldState s, int v) => CommissionDriver.RoadsToConnect(s, v);

            /// <summary>Valid flag for the type: fewest new roads, then closest to the storehouse (or near the anchor).</summary>
            public static int ChooseVertex(WorldState s, BuildingType type, Func<int, bool> filter = null, int anchor = -1)
            {
                int best = -1, bestCost = int.MaxValue;
                var topo = s.Board.Topology;
                for (int v = 0; v < topo.VertexCount; v++)
                {
                    if (!EconomyApi.CheckPlacement(s, PlayerIds.Human, type, v).Ok) continue;
                    if (filter != null && !filter(v)) continue;
                    var p = PathToNetwork(s, v);
                    if (p == null) continue;
                    int cost = 0;
                    foreach (int e in topo.PathEdges(p)) if (!s.Economy.Roads[e]) cost++;
                    if (anchor >= 0) cost = cost * 15 + 10 * topo.Distance(v, anchor) + topo.Distance(v, s.Board.StorehouseVertex);
                    else cost = cost * 100 + topo.Distance(v, s.Board.StorehouseVertex);
                    if (cost < bestCost) { best = v; bestCost = cost; }
                }
                return best;
            }
        }

        // ---------------- AutoPlay ----------------

        public static FullGameDriver Last { get; private set; }

        /// <summary>
        /// Plays from the current state to an ending through the UI paths, advancing the clock ticksPerFrame ticks per
        /// frame with GameClock.StepOnce (the normal clock is paused meanwhile). Fails on console errors or no ending.
        /// </summary>
        public static IEnumerator Drive(AutoPlay a, int ticksPerFrame, int tickBudget, bool completionist = true)
        {
            var boot = WorldBootstrap.Instance;
            var d = new FullGameDriver(boot.Sim, false, completionist);
            Last = d;
            if (boot.Views.CurrentView != ViewMode.God) boot.Views.SetView(ViewMode.God);
            boot.GodCam.SetFocus(boot.Layout.VillageCentre, 160f);
            int errors = 0;
            string firstError = null;
            void OnLog(string msg, string stack, LogType t)
            {
                if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; firstError ??= msg; }
            }
            Application.logMessageReceived += OnLog;
            const string pause = "AutoPlay";
            boot.Clock.Pause(pause);
            long start = boot.Sim.State.Tick;
            try
            {
                while (!boot.Sim.State.Strategy.GameOver)
                {
                    for (int k = 0; k < ticksPerFrame && !boot.Sim.State.Strategy.GameOver; k++)
                    {
                        d.Act();
                        boot.Clock.StepOnce();
                    }
                    if (errors > 0) { a.Fail("console error during the game: " + firstError); yield break; }
                    if (boot.Sim.State.Tick - start > tickBudget) { a.Fail("no ending after " + tickBudget + " ticks (" + StrategyApi.Headline(boot.Sim.State) + ")"); yield break; }
                    yield return null;
                }
            }
            finally
            {
                boot.Clock.Resume(pause);
                Application.logMessageReceived -= OnLog;
            }
            var s = boot.Sim.State;
            Debug.Log("[AutoPlay] full_game ended at tick " + s.Tick + " (game minute " + (s.Tick / (60f * WorldSim.TicksPerSecond)).ToString("0.0") + "): " + StrategyApi.Headline(s)
                + " Rounds " + s.Economy.Round + ", cities " + StrategyApi.PlayerCities(s).Count + ", cards " + s.Strategy.Sides[PlayerIds.Human].Built.Count
                + ", wonder " + s.Strategy.Wonder.StagesDone + ", soldiers " + s.Military.UnitsTrained + ", outpost " + (s.Military.Outpost.Destroyed ? "destroyed" : "standing")
                + ", breaches " + s.Strategy.Breaches + "; " + d.UiActions + " UI actions, " + d.Refusals + " refusals.");
        }

        /// <summary>The registered "full_game" script: prologue to an ending in God View through the tools and panels.</summary>
        public static AutoPlayScript Script(int ticksPerFrame = 10)
        {
            var s = new AutoPlayScript("full_game");
            s.Wait(0.5f)
             .Check(() => !WorldBootstrap.Instance.Sim.State.Strategy.Started, "the game has already started")
             .Step(a => Drive(a, ticksPerFrame, 60000))
             .Wait(0.5f)
             .Check(() => WorldBootstrap.Instance.Sim.State.Strategy.Outcome != GameOutcome.None, "no outcome")
             .Check(() => StrategyHud.Instance != null && StrategyHud.Instance.EndScreenOpen, "the end screen did not open")
             .Call(() =>
             {
                 var st = WorldBootstrap.Instance.Sim.State;
                 Debug.Log("[AutoPlay] full_game OK: " + st.Strategy.Outcome + " (" + st.Strategy.OutcomeReason + ") at game minute "
                     + ((st.Strategy.EndedTick >= 0 ? st.Strategy.EndedTick : st.Tick) / (60f * WorldSim.TicksPerSecond)).ToString("0.0") + ", score "
                     + StrategyApi.Score(st, PlayerIds.Human).Total + "-" + StrategyApi.Score(st, PlayerIds.Rival).Total);
             })
             .Quit(0);
            return s;
        }
    }
}
