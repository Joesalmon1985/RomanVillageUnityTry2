using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;
using World.Sim.Table;
using World.Sim.War;

namespace World.Game
{
    /// <summary>
    /// Packaged smoke scripts for the god board (PRD_GODBOARD 12.3): board_first_round, board_trade_discard,
    /// plague_epidemic, war_burn, board_to_historic. Run with <c>-autoplay &lt;name&gt; -novsync</c>; each logs
    /// "[AutoPlay] name OK" and exits 0, or "FAILED" and exits 1. They start their own developer start (the scene reloads and
    /// the second half goes on via <see cref="AutoPlay.ContinueAfterLoad"/>), pause the clock and step it by hand so a run
    /// takes seconds, and play through commands and the views' public methods. Situations that would need luck or many
    /// minutes (cards in hand, an AI offer, a points lead, an empty camp) are prepared by <see cref="SmokeSetup"/> in the sim.
    /// </summary>
    public static class GodBoardAutoPlay
    {
        private const string P = "AutoPlaySmoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            AutoPlayScripts.Register("board_first_round", () => Launch("board_first_round", GodBoardStart.Id, FirstRound));
            AutoPlayScripts.Register("board_trade_discard", () => Launch("board_trade_discard", GodBoardStart.Id, TradeDiscard));
            AutoPlayScripts.Register("plague_epidemic", () => Launch("plague_epidemic", GodBoardStart.Id, PlagueEpidemic));
            AutoPlayScripts.Register("war_burn", () => Launch("war_burn", GodBoardStart.Id, WarBurn));
            AutoPlayScripts.Register("board_to_historic", () => Launch("board_to_historic", GodBoardStart.EveId, ToHistoric));
        }

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static WorldState S => B.Sim.State;
        private static TableState T => S.Table;
        private static GodBoardDirector D => GodBoardDirector.Instance;
        private static GodBoardHud Hud => D.GetComponent<GodBoardHud>();

        private static AutoPlayScript Launch(string name, string startId, Func<string, AutoPlayScript> rest)
        {
            var s = new AutoPlayScript(name);
            s.Step(a => Begin(a, name, startId, rest));
            return s;
        }

        private static IEnumerator Begin(AutoPlay a, string name, string startId, Func<string, AutoPlayScript> rest)
        {
            AutoPlay.ContinueAfterLoad = rest(name);
            if (!DevStart.Begin(startId, 7UL)) { AutoPlay.ContinueAfterLoad = null; a.Fail("start " + startId + " failed: " + DevStart.LastError); yield break; }
            yield return new WaitForSecondsRealtime(120f); // the scene reloads under this script
            a.Fail("the start never reloaded the scene");
        }

        // ------------------------------------------------------------------------------ helpers

        /// <summary>Steps the clock by hand (30 ticks a frame) until the condition holds.</summary>
        private static AutoPlayScript StepUntil(this AutoPlayScript s, Func<bool> done, int maxSteps, string fail, Action each = null)
        {
            return s.Step(a => StepRoutine(a, done, maxSteps, fail, each));
        }

        private static IEnumerator StepRoutine(AutoPlay a, Func<bool> done, int maxSteps, string fail, Action each)
        {
            int n = 0;
            while (!done())
            {
                if (n >= maxSteps) { a.Fail(fail + " (after " + n + " ticks)"); yield break; }
                for (int i = 0; i < 30 && !done(); i++) { each?.Invoke(); B.Clock.StepOnce(); n++; }
                yield return null;
            }
            yield return null;
        }

        private static AutoPlayScript Tick(this AutoPlayScript s, int n) => s.Step(a => TickRoutine(n));
        private static IEnumerator TickRoutine(int n) { for (int i = 0; i < n; i++) B.Clock.StepOnce(); yield return null; yield return null; }

        private static AutoPlayScript WaitUntil(this AutoPlayScript s, Func<bool> cond, float seconds, string fail) => s.Step(a => a.DoWaitFor(cond, seconds, fail));

        private static AutoPlayScript Begun(this AutoPlayScript s)
        {
            return s.Wait(1f)
                .Check(() => T != null && D != null, "no god board in this start")
                .Call(() => B.Clock.Pause(P))
                .Check(() => T.Phase == TablePhase.NotOpened, "the table is not closed before the sacrifice");
        }

        /// <summary>From the board view in setup: place the setup camp and road, let the AIs place, reach main phase.</summary>
        private static AutoPlayScript PlaceSetup(this AutoPlayScript s)
        {
            int corner = -1, edge = -1;
            return s.WaitUntil(() => D.View == BoardView.Board, 20f, "the camera did not rise to the board")
                .WaitUntil(() => D.MyTurn && D.AdviseCorner >= 0, 10f, "no setup corner offered")
                .Call(() => { corner = D.AdviseCorner; string r = D.TapAt(D.ScreenOfCorner(corner)); if (r == null || !r.StartsWith("preview")) throw new Exception("the first tap on the corner did not preview: " + r); })
                .Check(() => D.Pieces.HasGhost && T.CornerOwner[corner] != 0, "no ghost camp, or the camp was built before the second tap")
                .Wait(0.5f)
                .Call(() => { string r = D.TapAt(D.ScreenOfCorner(corner)); if (r == null || r.StartsWith("preview")) throw new Exception("the second tap on the corner sent nothing: " + r); })
                .Tick(2)
                .Check(() => T.CornerOwner[corner] == 0 && D.Pieces.CornerPiece(corner) != null, "the setup settlement did not appear")
                .WaitUntil(() => D.EdgeSpots.Count > 0, 10f, "no setup road offered")
                .Wait(0.8f) // the tap guard after the camp's command
                .Call(() => { edge = D.EdgeSpots[0]; string r = D.TapAt(D.ScreenOfEdge(edge)); if (r == null || !r.StartsWith("preview")) throw new Exception("the first tap on the edge did not preview: " + r); })
                .Wait(0.5f)
                .Call(() => { string r = D.TapAt(D.ScreenOfEdge(edge)); if (r == null || r.StartsWith("preview")) throw new Exception("the second tap on the edge sent nothing: " + r); })
                .Tick(2)
                .Check(() => T.RoadOwner[edge] == 0 && D.Pieces.RoadPiece(edge) != null, "the setup road did not appear")
                .StepUntil(() => T.Phase == TablePhase.Main, 4000, "the AI seats did not finish placing")
                .Check(() => D.Pieces.SettlementCount >= 8, "fewer than 8 settlements after setup");
        }

        /// <summary>Sacrifice (range ignored), place the setup, step to the player's first main turn.</summary>
        private static AutoPlayScript OpenAndSetUp(this AutoPlayScript s)
        {
            return s.Call(() => { if (!D.TrySacrifice(true)) throw new Exception("sacrifice refused: " + D.LastResult.Message); })
                .Tick(2)
                .PlaceSetup()
                .StepUntil(() => T.Phase == TablePhase.Main && T.Current == 0 && !T.Paused, 40000, "the player's first main turn never came");
        }

        // ------------------------------------------------------------------------------ 1

        private static AutoPlayScript FirstRound(string name)
        {
            var s = new AutoPlayScript(name);
            long frozen = 0;
            s.Begun()
             .Step(a => a.DoWalkTo(() => D.OwnTemple.AltarSpot.position, 8f, 60f))
             .Check(() => BoardSpace.Flat(D.OwnTemple.AltarSpot.position - B.Avatar.Position) <= GodBoardDirector.AltarRange, "not within altar range after the walk")
             .Call(() => { if (!D.TrySacrifice()) throw new Exception("sacrifice refused: " + D.LastResult.Message); })
             .Tick(2)
             .Check(() => T.Phase == TablePhase.Setup && !T.Paused, "the table did not open in setup")
             .PlaceSetup()
             .StepUntil(() => T.Phase == TablePhase.Main && T.Round >= 1, 4000, "no first turn started")
             .StepUntil(() => D.ProductionPulses > 0, 40000, "no production pulse")
             .Wait(0.3f)
             .Call(() => D.Leave())
             .WaitUntil(() => D.View == BoardView.Altar, 20f, "did not come back to the altar")
             .Tick(2)
             .Check(() => T.Paused, "the table is not paused after leaving")
             .Call(() => frozen = T.ActiveTicks)
             .Tick(100)
             .Check(() => T.ActiveTicks == frozen, "the paused table advanced")
             .Call(() => { if (!D.TrySacrifice(true)) throw new Exception("could not come back: " + D.LastResult.Message); })
             .Tick(2)
             .Check(() => !T.Paused, "the table did not resume")
             .WaitUntil(() => D.View == BoardView.Board, 20f, "the board did not open again")
             .Log("[AutoPlay] " + name + " OK")
             .Quit(0);
            return s;
        }

        // ------------------------------------------------------------------------------ 2

        private static AutoPlayScript TradeDiscard(string name)
        {
            var s = new AutoPlayScript(name);
            int rate = 0, wood = 0, stone = 0, clay = 0, sheep = 0, offerId = 0, owed = 0, before = 0;
            s.Begun().OpenAndSetUp()
             // Bank trade on the barter mat: the trader unrolls it, a basket gives, the wanted good is set opposite.
             .Call(() => { SmokeSetup.SetCards(S, 0, 0, 8); SmokeSetup.SetCards(S, 0, 4, 0); })
             .Wait(0.1f)
             .Call(() =>
             {
                 rate = Hud.BankRate(0); Hud.TapLedge("trader");
                 if (!Hud.MatOpen) throw new Exception("the barter mat did not open");
                 Hud.TapLedge("basket:0"); Hud.SelectBankGet(4);
                 if (Hud.BankGive != 0) throw new Exception("the basket did not set the good to give");
                 wood = T.Peoples[0].Hand[0]; stone = T.Peoples[0].Hand[4];
                 var r = Hud.DoBankTrade();
                 if (!r.Ok) throw new Exception("bank trade refused: " + r.Message);
             })
             .Tick(2)
             .Check(() => T.Peoples[0].Hand[0] == wood - rate && T.Peoples[0].Hand[4] == stone + 1, "the bank trade did not change the hand")
             .Call(() => Hud.CloseTrade())
             .Wait(0.8f)
             // An AI offer arrives as an envoy; tap once to see the bundle, again to accept. The player must not lead (TR-4): give seat 1 a city.
             .Call(() =>
             {
                 int c = TableGrants.BestCityCorner(S, 1);
                 if (c >= 0) TableGrants.GrantCity(S, 1, c, null);
                 SmokeSetup.SetCards(S, 0, 3, 3);
                 clay = T.Peoples[0].Hand[1]; sheep = T.Peoples[0].Hand[3];
                 SmokeSetup.SetCards(S, 1, 1, 3);
                 offerId = SmokeSetup.AddOffer(S, 1, 0, 1, 3);
             })
             .Wait(0.8f)
             .Check(() => Hud.IncomingOffers().Count == 1 && Hud.LedgeRects.ContainsKey("envoy:" + offerId), "the envoy is not on the ledge")
             .Call(() => { var r = Hud.TapLedge("envoy:" + offerId); if (r != WorldTapResult.Previewed) throw new Exception("the envoy tap did not preview: " + r); })
             .Check(() => T.Peoples[0].Hand[1] == clay, "the offer was accepted before the second tap")
             .Wait(0.5f)
             .Call(() => { var r = Hud.TapLedge("envoy:" + offerId); if (r != WorldTapResult.Confirmed) throw new Exception("the second envoy tap did not accept: " + r); })
             .Tick(2)
             .Check(() => T.Peoples[0].Hand[1] == clay + 1 && T.Peoples[0].Hand[3] == sheep - 1, "the accepted offer did not change the hand")
             // A 7 comes from the table's own dice: end turns, keeping 9 cards in hand, until it does.
             .StepUntil(() => T.Phase == TablePhase.Discard && T.DiscardOwed[0] > 0, 60000, "no 7 owed a discard", () =>
             {
                 if (T.Phase == TablePhase.Main && T.Current == 0 && !T.Paused) { SmokeSetup.TopUpHand(S, 0, 9); D.EndTurn(); }
             })
             .WaitUntil(() => Hud.DiscardVisible && Hud.LedgeRects.ContainsKey("sack"), 5f, "the discard sack is not on the ledge")
             // The ritual: the adviser's goods go into the sack one basket tap at a time, then the sack is tapped twice.
             .Call(() =>
             {
                 owed = Hud.DiscardOwed; before = TableRules.HandSize(T.Peoples[0].Hand);
                 var pick = TableAi.ChooseDiscard(S, 0, owed);
                 for (int g = 0; g < Wares.RawCount; g++) for (int n = 0; n < pick[g]; n++) Hud.TapLedge("basket:" + g);
                 if (Hud.DiscardChosen != owed) throw new Exception("the sack holds " + Hud.DiscardChosen + " of " + owed);
             })
             .Wait(0.8f) // the tap guard after the envoy's command (this run is much faster than a hand)
             .Call(() => { var r = Hud.TapLedge("sack"); if (r != WorldTapResult.Previewed) throw new Exception("the sack tap did not preview: " + r); })
             .Wait(0.5f)
             .Call(() => { var r = Hud.TapLedge("sack"); if (r != WorldTapResult.Confirmed) throw new Exception("the second sack tap did not give: " + r); })
             .Tick(2)
             .Check(() => TableRules.HandSize(T.Peoples[0].Hand) == before - owed && !Hud.DiscardVisible, "the discard was not completed")
             .Log("[AutoPlay] " + name + " OK")
             .Quit(0);
            return s;
        }

        // ------------------------------------------------------------------------------ 3

        private static bool FeedHas(string word)
        {
            foreach (var f in D.Feed) if (f.Text != null && f.Text.Contains(word)) return true;
            return false;
        }

        private static AutoPlayScript PlagueEpidemic(string name)
        {
            var s = new AutoPlayScript(name);
            int node = 0, demons0 = 0, before = 0;
            s.Begun().OpenAndSetUp()
             .StepUntil(() => PlagueViews.Instance.EpidemicsSeen > 0, 80000, "no epidemic (7) came", () =>
             {
                 if (T.Phase == TablePhase.Main && T.Current == 0 && !T.Paused) D.EndTurn();
                 if (T.Phase == TablePhase.Discard && T.DiscardOwed[0] > 0) { Hud.SuggestDiscard(); Hud.DoDiscard(); }
             })
             .Wait(0.5f)
             .Check(() => PlagueApi.TotalDemons(S) > 0, "the epidemic placed no demons")
             .Check(() => D.EpidemicAt > 0f && FeedHas("angry"), "the board did not show the epidemic in the feed")
             .WaitUntil(() => PlagueViews.Instance.StormLevel > 0.5f, 10f, "the sky did not darken")
             .Call(() => PlagueViews.Instance.SetOverlay(true))
             .Wait(0.3f)
             .Check(() => PlagueViews.Instance.Overlay && PlagueViews.Instance.OverlayNodeCount == 48, "the overlay did not show 48 nodes")
             .Check(() => PlagueViews.Instance.OverlayRoot.activeSelf, "the overlay layer is not shown on the board")
             .Call(() => D.Leave())
             .WaitUntil(() => D.View == BoardView.Altar, 20f, "did not leave the board")
             .Call(() =>
             {
                 for (int n = 1; n <= PlagueGraphData.NodeCount; n++) if (PlagueApi.DemonsAt(S, n) > 0) { node = n; break; }
                 if (node == 0) throw new Exception("no demon on the island");
                 demons0 = PlagueApi.TotalDemons(S); before = PlagueApi.DemonsAt(S, node);
                 var at = PlagueViews.Instance.NodePosition(node);
                 B.Avatar.Teleport(at + new Vector3(8f, 0, 0), 270f, 8f);
             })
             .Wait(0.3f)
             .Check(() => PlagueViews.Instance.NodeNearPlayer == node, "the player is not beside the demon")
             .Call(() => { var r = PlagueViews.Instance.TryBanishNear(); if (!r.Ok) throw new Exception("banish refused: " + r.Message); })
             .Tick(2)
             .Check(() => PlagueApi.DemonsAt(S, node) == before - 1 && PlagueApi.TotalDemons(S) == demons0 - 1, "the banish did not remove a demon")
             .Log("[AutoPlay] " + name + " OK")
             .Quit(0);
            return s;
        }

        // ------------------------------------------------------------------------------ 4

        private static int _diag;
        private static string WarDiag()
        {
            var sb = new System.Text.StringBuilder("phase=" + T.Phase + " war01=" + WarApi.AtWar(S, 0, 1) + " pts=" + string.Join(",", S.War.Points) + " settlements=" + S.War.Settlements.Count + " soldiers=" + S.War.Soldiers.Count);
            foreach (var x in S.War.Soldiers) sb.Append(" [" + x.People + " node" + x.Node + " corner" + x.Corner + " ord" + x.Order + " tgt" + x.OrderTarget + " path" + x.Path.Count + " mv" + x.Moving + "]");
            return sb.ToString();
        }

        private static bool RuinShown()
        {
            foreach (var r in S.War.Ruins)
            {
                var site = WarViews.Instance.SiteOf(r.Corner);
                if (site != null && (site.State == SiteState.Burning || site.State == SiteState.Ruin)) return true;
            }
            return false;
        }

        private static AutoPlayScript WarBurn(string name)
        {
            var s = new AutoPlayScript(name);
            int pts0 = 0; const int victim = 1;
            var corners = new List<int>();
            s.Begun()
             // People 1 takes two free camps (the crystal-grant path), leads by 2 and is at war with the table.
             .Call(() => { corners = SmokeSetup.GrantSettlements(S, victim, 2); if (corners.Count < 2) throw new Exception("could not grant camps"); })
             .Tick(3)
             .Check(() => WarApi.AtWar(S, 0, victim), "the lead did not start a war")
             .Call(() => { foreach (int c in corners) SmokeSetup.RemoveGarrison(S, victim, c); for (int p = 0; p < S.Peoples.Count; p++) if (p != victim) { SmokeSetup.AddSoldier(S, p); SmokeSetup.AddSoldier(S, p); } pts0 = TableRules.Points(S, victim); })
             .StepUntil(() => S.War.Burned > 0, 60000, "no camp burned", () => { if (++_diag % 3000 == 1) Debug.Log("[AutoPlay] war_burn diag " + WarDiag()); })
             .Tick(2)
             .Check(() => S.War.Ruins.Count > 0, "no ruin appeared")
             .Check(() => TableRules.Points(S, victim) < pts0, "the table's points did not drop")
             .WaitUntil(RuinShown, 10f, "the ruin is not shown")
             .Log("[AutoPlay] " + name + " OK")
             .Quit(0);
            return s;
        }

        // ------------------------------------------------------------------------------ 5

        private static AutoPlayScript ToHistoric(string name)
        {
            var s = new AutoPlayScript(name);
            Vector3 p0 = default;
            s.Wait(1f)
             .Check(() => D != null && HistoricDirector.Instance != null, "no Historic director")
             .WaitUntil(() => S.Historic != null, 40f, "the switch to Historic never happened")
             .WaitUntil(() => HistoricDirector.Instance.TemplesDown, 40f, "the temples did not fall")
             .Wait(1f)
             .Call(() => HistoricDirector.Instance.SyncNow())
             .Check(() => { for (int p = 0; p < S.Historic.Peoples.Count; p++) if (!HistoricDirector.Instance.ScaffoldShown(p)) return false; return true; }, "the castles did not start")
             .Check(() => HistoricDirector.Instance.TreeMeshHexes > 0, "no trees render")
             .WaitUntil(() => B.Avatar.CanMove, 20f, "the player cannot move")
             .Call(() => p0 = B.Avatar.Position)
             .Step(a => TryWalk(a))
             .Check(() => BoardSpace.Flat(B.Avatar.Position - p0) > 0.5f, "the player did not walk")
             .Log("[AutoPlay] " + name + " OK")
             .Quit(0);
            return s;
        }

        private static IEnumerator TryWalk(AutoPlay a)
        {
            var w = B.Avatar; var p0 = w.Position;
            for (int h = 0; h < 6; h++)
            {
                yield return a.DoMoves(LatticeMove.Forward, 2);
                if (BoardSpace.Flat(w.Position - p0) > 0.5f) yield break;
                yield return a.DoMoves(LatticeMove.TurnRight, 1);
            }
        }
    }
}
