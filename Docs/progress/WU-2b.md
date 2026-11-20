# WU-2b: the altar ledge as real 3D objects

20 Nov 2026, branch `godboard/wu2b-ledge-3d` (from `77463e6`, `godboard/main` with WU-3 merged in at `0351d27`). Follows `Docs/progress/WU-2.md`; plan: `Docs/WORLD_UI_PLAN.md` 2.2.

## Done

- **3D ledge** (`Board/GodBoard/AltarLedgeRig.cs`, `LedgeItem.cs`): a carved stone slab (blocks with seams, a rim along the back edge) seen by its own camera (`Ledge Camera`, layer 30 only, depth-only clear, depth 40, so it is layered in front of the island; the board camera and every other camera drop layer 30). The rig sits 20 km below the island. Two torches stand on the rim at the screen edges; two flickering warm point lights and a warm fill light only light layer 30. The slab is fitted each frame so its back edge meets the band top from `GodBoardHud.LayoutLedge` (safe area kept; the stone bleeds to the screen edges).
- **Objects, all built from primitives (original art)** and standing inside their WU-2 layout rects, with a small yaw jitter so they read as things set down by hand: five wicker **offering baskets** whose pieces (logs with light end rings, clay bricks, sheaves, fleeces, stones) follow the hand up to 9; the **builder's bench** (three bench sections with a road plank, a hut and a walled hut in the player's colour, spare-piece stacks at the end; dark when unaffordable, gold under the active mode, the sample shakes on a refusal and the lacking baskets glow red); four **banner stakes** in people colours (raised and waving for the people whose turn it is, a cairn of point stones, the gold longest-road cord, a war helmet); the **trader** with a rolled mat at his feet; the **barter mat** (unrolls in 0.35 s above the ledge; the goods to give leave the basket and lie on the mat, the five goods to get sit on plates, a deal stone with clasped hands, a peg board for offers to the peoples); **envoys** in their people colour who walk in from the side, hold out a bundle showing what they give, a bowl at their feet shows what they ask (dark when you cannot pay), and turn away as patience runs out; a rope barrier and raised palm to wave each one off; the **discard sack** (grows as goods go in, knots on its cord tied gold per good, goods peeking from its mouth, a small real hourglass beside it for the time limit) and an upturned bowl to tip it out; the turn **hourglass** (wooden caps and posts, glass bulbs, sand that drains with the turn and a falling stream; one tap tips it on its side and lifts a bell, the second ends the turn); the **chronicler** with a scroll (a candle burns while there are unread lines); the **prince** (crown dark when hints are off); the **demon-ward totem** (eyes glow while the plague overlay is on); the **lamb stone** (a lamb on an altar block). No digits on any object.
- **Picking**: every object has a camera-facing `BoxCollider` covering exactly its layout rect (so at least `WorldHitTarget.MinSide`); `GodBoardHud.Update` sends a press through `AltarLedgeRig.Pick` (a `Physics.Raycast` on layer 30) into the unchanged `TapLedge` and the shared `WorldActions` preview/confirm flow. Presses on IMGUI cards or the ledger tablet are not passed through. New tap ids for the mat: `mat-give` (ledger: the deal and the rates), `mat-get:G` (the good to get), `mat-deal` (two-step: preview "Make this trade?", then `BankTrade` once), `mat-peoples` (the old offers window, as before).
- **No IMGUI ledge in the world-first view**: the old drawing code is gone; OnGUI only reserves the band and the mat rect so board presses there are ignored. Clear view (H key, Esc menu) hides the rig and draws the G1-6b HUD as before. WU-2 logic is unchanged (view model, ledger texts, action guard, commands, `TapLedge`).
- **One Clear view setting** (after merging WU-3): `PlagueHud.ClearView` now reads and writes `GodBoardSettings.ClearView` (minimal edit in `Plague/PlagueHud.cs`). The totem calls `PlagueViews.ToggleOverlayFromTotem()` / reads `PlagueViews.OverlayOn` (falls back to the old `GodBoardHud.PlagueToggle` hooks if the plague module is absent).
- **Board readability at night** (`GodBoardNightLight.cs`, on the director): while the god's view is up, a cool moonlight key light (not on the ledge layer) and an ambient lift (max with the day-night ambient, scaled by `DayNight.Night`) light the island. It runs before the plague's storm darkening, so an epidemic still darkens the board. `DayNight` re-applies its own ambient every frame, so the walking-world night is unchanged once you are back at the altar.
- AutoPlay `board_trade_discard` now picks the good to get by tapping it on the mat (`TapLedge("mat-get:4")`).

## Tests (editor 6000.3.28f1)

- PlayMode `GodBoardLedgeTests` 16/16 (the 12 WU-2 tests updated where a control moved, plus: the default view draws no IMGUI ledge and every ledge rect is a modelled 3D object on layer 30 with a collider, the ledge camera sees only the ledge and is in front of the board camera; at 844x390 with the notch every collider's screen bounds is at least `MinSide` and a raycast at each rect's centre picks it; the baskets' visible goods follow the hand and the exact count is on the tablet; one Clear view setting shows the board HUD and the Plague button together and the totem toggles the overlay).
- PlayMode `GodBoardTests` 10/10, `GodBoardVillagerTests` 7/7, `WorldUiTests` 6/6, `PlagueViewTests` 9/9, `WarViewTests` 5/5, `HistoricViewTests` 6/6.
- EditMode `World.Game.EditTests` 24/24.
- Not run: the full PlayMode suite, `World.Sim.Tests`.
- Console: 0 errors from game code.

## AutoPlay

Rebuilt player (`AgentScripts/BuildWindows.cs`, 101 MB, 0 errors), windowed 844x390: `board_first_round`, `board_trade_discard`, `plague_epidemic`, `war_burn`, `board_to_historic` all `[AutoPlay] ... OK`, exit 0 (logs `Builds/logs/wu2b_*.log`). `ProjectSettings/ProjectSettings.asset` line-ending change from the build reverted.

## Captures (local only, `Evidence/worldui/`, never committed)

`wu2b_normal_`, `wu2b_buildpreview_`, `wu2b_mat_`, `wu2b_envoy_`, `wu2b_discard_`, `wu2b_endturn_`, `wu2b_clearview_` + `1920x1080`, `1024x768`, `844x390` (`.png`), all with the simulated notch, every PNG size checked by the driver's `check`. Earlier iterations: `v1_` to `v6_`. Driven by `AgentScripts/WorldUiCaptures.cs` (unchanged; its `tapledge` command reaches the new `mat-get:G`, `mat-deal`, `mat-give`, `mat-peoples` ids).

## Not done / open

- The mat is a cloth hung above the back edge of the ledge, over the island's lower edge, not lying on the stone: there is no room on the ledge at 844x390. It reads as a cloth banner with plates; a lying rug would need a deeper ledge.
- Offers to the peoples (the peg board) still open the old IMGUI trade window; counter-offers do not exist in the sim.
- The ledger tablet can unfold over ledge objects when there is no room above (WU-0 placement).
- Jetties on the island are still not tappable (rates are on the trader's and the mat's tablets).
- Front-row figures (envoys) overlap the bottoms of the baskets behind them at 844x390; it reads as depth, not a bug, but a second look by the owner is welcome.
- Ambient lift is applied by frame order (before the plague's LateUpdate); if another system sets the ambient later in the frame the lift is lost for that frame.
