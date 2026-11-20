# WU-2: the hex board game, the altar ledge

20 Nov 2026, branch `godboard/wu2-ledge` (from `34afb86`). Scope: the WU-2 row of `Docs/WORLD_UI_PLAN.md` section 5.

## Done

- `GodBoardViewModel` (hand, costs, affordability, pieces left, legal spot counts, discard need and timer, points, cards, roads, soldiers, war pairs, bank and port rates, offers, sand). Plain class built from sim state; the ledge and the Clear view HUD both read `GodBoardDirector.Model`. `GodBoardLedger` builds every tablet text (`LedgerText.Violation` clean).
- The altar ledge (`GodBoardHud.Ledge.cs`): carved stone band inside the safe area (stone bleeds to the screen edge), board camera fitted above it. Objects: five baskets (fullness shows the hand, no digits; tap = ledger with exact counts, costs, affordability; on the mat a basket picks the good to give; in a discard it puts a good in the sack), builder's bench (3 samples, spare ticks, shakes head and dims lacking baskets on refusal), four banner stakes (point stones, longest-road cord, war helmet, raised active stake; tap = exact standings), trader + barter mat (bank rate; the jetty count and rates are in the trader text), envoys (tap = bundle and patience, tap again = accept; x = wave off; patience bar), discard sack (knots, timer bar, clear button), turn hourglass (tap tips, again ends turn), chronicler, prince (second tap toggles hints), demon-ward totem (calls the existing `GodBoardHud.PlagueToggle` hook), lamb stone (preview, confirm, `Leave`). Every object has a proxy tagged `HEX-STORE/BUILD/AWARDS/TURN/TRADE/RAIDER/END/FEED/DEV/PLAGUE/LEAVE`; `WorldHitTarget.ScreenBounds` reads them so the F1 outline works.
- Ghost-piece preview: `BoardSpotAction` + `GodBoardPieces.ShowGhost`. First tap on a legal spot = ghost + ledger, second tap or the builder = one command, elsewhere/Esc = cancel. `TapAtNow` is the old one-tap path (tests, captures).
- Action guard in `GodBoardDirector.Submit`: end turn once per turn; same build/trade/answer within 0.5 s is refused (`DuplicatesBlocked`).
- Clear view: `GodBoardSettings.ClearView` (PlayerPrefs, off by default), Esc-menu button, H key on the board; draws the G1-6b HUD from the same model.
- Small WU-0 edits: `IScreenAnchored` for the ledger card, `LedgerCard` depth in front of the ledge, `WorldActionController` no longer cancels previews on the board and watches the board camera.

## Tests (editor 6000.3.28f1)

- EditMode `World.Game.EditTests`: 24/24 (19 WU-0 + 5 new view model/ledger tests).
- PlayMode: `GodBoardLedgeTests` 12/12 (preview then confirm sends once; cancel leaves nothing; illegal spot never offered; builder confirms a ghost and speaks refusals; ledge in safe area, objects at least MinSide, no overlaps, zones; bank and port trade via mat plus duplicate guard; envoy accept and wave off; sack before and after the timer; end turn two taps and once per turn; leaving freezes sand; Clear view same numbers), `GodBoardTests` 10/10, `GodBoardVillagerTests` 7/7, `WorldUiTests` 6/6, `PlagueViewTests` 4/4, `WarViewTests` 5/5, `HistoricViewTests` 6/6. Not run: full PlayMode suite, `dotnet test`.
- AutoPlay (rebuilt player, 844x390): `board_first_round`, `board_trade_discard`, `plague_epidemic`, `war_burn`, `board_to_historic` all OK. The scripts now use previews and the ledge, and wait out the 0.6 s tap guard (the run is faster than a hand).
- Console: 0 errors from game code.

## Captures (local, `Evidence/worldui/`, 1920x1080, 1024x768, 844x390 with notch; sizes verified)

`normal_`, `buildpreview_`, `mat_`, `envoy_`, `discard_`, `epidemic_`, `gameover_`, `clearview_` + `{size}.png`; `ledge_outline_844x390.png`. Driver commands added to `AgentScripts/WorldUiCaptures.cs` (open1, open2, hand, preview, mat, envoy, discard, fill, epidemic, gameover, clearview, tapledge, ledge).

## Not done / open

- The ledge is IMGUI (same layer as the HUD), not a second 3D camera; objects are drawn shapes with stable ids, rects and zone proxies.
- Jetties on the island are not tappable (rates are on the trader's tablet and the mat). No counter-offers (sim has none).
- The epidemic capture shows the moment card only, not the storm sky. The Clear view HUD's people panels still overrun the notch (pre-existing G1-6b).
- Totem uses the existing `PlagueHud` toggle hooks; switch to WU-3's public toggle when it lands.
- The peoples tab of the mat opens the old trade window (offers to peoples).
