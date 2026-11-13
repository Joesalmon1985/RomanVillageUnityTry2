# Strategy UI (draft, cities, demons, hero, military, endings)

How the strategy and military layers (`Docs/STRATEGY_API.md`) are shown and played in God View and first person. Written 13 Nov 2026 (main, after the economy rebalance merge). Read `Docs/ECONOMY_UI.md` first: this layer uses its toolbar, commands, overlay and toasts.

Code: `Assets/World/Game/Strategy` (namespace `World.Game`). Installed by one line in `WorldBootstrap.Awake` after the economy: `StrategyInstaller.Install(this)`. Rules kept: views read state and the sim's helpers (`StrategyApi`, `MilitaryApi`, `EconomyApi`); every action is a command through `StrategyUi` -> `GodCommands.Submit` (refusals come back as the sim's sentence in a toast); no number, name, cost or count is written in the Game layer (all read from state, tuning or helpers).

## Playing a game

1. Prologue: finish Quartermaster Bram's commission (Build and Road tools, `ECONOMY_UI.md`). The strip's "What now?" line follows Bram's next step.
2. End the round (End Round button or the bell, once the harvest season is over: both show the countdown). Age I begins with a draft.
3. Each round of an age starts with one draft pick: the clock pauses (`PauseReasons.Draft`) and the draft opens as a modal in God View (in first person: talk to Scribe Pell or press Tab). Take a card; build it from the Cards panel at one of your cities, or discard it for coin.
4. Grow: cities (City tool), the wonder (Wonder tool), soldiers (Army tool), trade (Market tool or Hesta). Keep demons off your roads with the hero (Hero tool). End rounds when the batches are used.
5. The game ends at the points target, the breach limit, or after the last age. The end screen gives the reason and both score breakdowns; New game reloads the scene with a fresh seed.

## HUD (`StrategyHud`, both views)

- **Strip** under the objective (God View full width, first person the same, narrower minimum): age and round (with the economy round and the numbers drawn), points you : rival of the target (hover: both `ScoreText` breakdowns; click: keeps the breakdown open), the breach track as boxes up to the limit (last two red), coin, power supply/demand (red when short), next infection draw. It sizes itself to its content and drops the infection count, then the round detail, on narrow screens.
- **What now?** line under it, rebuilt twice a second from state, in priority order: game over (headline); a pick is waiting; the commission's next step (`EconomyFirstPerson.NextStep`, the sim's sentences); a demon site at the cap; a demon on a building's flag or its road (`demon_on_route`); a stalled barracks (`DescribeBarracks(..).Status`); a card you can build now; a city you can found now; a wonder stage you can order; the first economy problem (`EconomyText.Problems`, sentence from the sim); a shield deficit before the comparison (`ShieldSummary.Sentence`); otherwise "end the round when the batches are used". Buttons on the line: "Open draft" while a pick waits and the modal is closed, "Results" after the game ends.
- **Objective** (top centre) becomes the headline once Age I starts ("Age II, round 1 of 3. First to 18 points wins; 5 breaches lose.").
- **Toasts and banners** (via `EconomyHud.Toast/Banner`): round start (hero moves), infection draws (sites named), demons repelled or cleared, outbreaks (site, the neighbours it spreads to, breach n of limit), rival picks and rival cards built (with the effect), rival plan points, soldier trained, barracks stalls (sentence), fights, losses, outpost hits (throttled), raids. Big banners: age start, epidemic, the age-end comparison with its result, wonder stage done, outpost destroyed, game over. Command results (card built, city founded, ...) are the command toasts.
- **End screen**: Victory / Defeat / Draw, `StrategyApi.Headline` (why), game minute and round, a table of cities, cards, wonder, military, outpost, rival plan and total for both sides, the comparisons, breaches, soldiers, outpost. Buttons: **New game (fresh seed)** (`WorldBootstrap.NewGame(seed)` reloads the scene; `WorldBootstrap.NextSeed` is read in `Awake`, so every view, panel and NPC is rebuilt from the new state) and **Keep looking around** (closes; "Results" reopens it).

## Panels (`StrategyPanels`, shared by God View tools and `StrategyWindow`)

`StrategyWindow.Open(kind)` shows any panel centred (first person NPCs, the draft modal); it frees the cursor with an avatar lock; Esc or Close shuts it. The God View tools draw the same bodies in the left column.

| Panel | Shows | Actions |
|---|---|---|
| Draft | age, pick n of the picks, one line on pick-and-pass, each card's name, family (colour), age, effect, cost, whether you could build it now; what the rival has taken and built; your hand; next round's hand while no pick waits (`DraftNextStep`) | Take (DraftPick); build / discard from hand |
| Cards | hand (cost, effect, shortfall), city choice when more than one city has room, built cards per city with effects | Build at a city, Discard for coin |
| Market | `MarketText`, coin, a give/get picker with stock, the stock after the trade; every ware with stock | Trade rate:1, Sell (lot size from the sim), Buy 1 |
| Wonder | `Wonder(s).Status`, every stage's cost and points and state, delivered goods | Build stage n (refusal as tooltip); plan a wonder site |
| Army | power sentence; barracks status sentence, queue, supplied / waiting for, power, progress bar, unit cost and training time; shields sentence and past comparisons; outpost status; each squad (units, health, where, order text) | Queue a soldier; per squad Attack outpost, Defend home, Order to a flag (then click a flag), Cancel; raids named |
| Cities | the city rule and cost, the hovered corner's verdict, the suggested sites with verdicts | Show, Found here |
| Hero | moves left, where, the infection forecast sentence and discard pile, demons by count (cap flagged) | Show |

## God View tools (`StrategyGodTools`)

In the toolbar gaps: **Market [T]** (62), **Cards [C]** (65, badge "(draft)" or hand size), **City [F]** (70), **Hero [J]** (72, badge moves left), **Army [N]** (75, badge soldiers or a red "!" when the barracks has no power), **Wonder [O]** (80). The toolbar now fits 12 tools: labels drop the hotkey hint, then shrink, rather than clip.

- **City** (`CityTool`, captures clicks): `LegalCitySites(s, true)` corners glow (green: legal now; amber: only the cost is missing), refreshed twice a second; hover: `CheckCity` verdict as a tooltip (refusal sentence in red); click founds (`TryFound`).
- **Hero** (`HeroTool`, captures clicks): `HeroMoveTargets` glow; hover tells how many demons the walk clears; a click on another corner shows the sim's refusal; click moves (`TryMove`). Recomputed only when the hero's corner or moves change.
- **Army** (`ArmyTool`): while a squad order is armed ("Order to a flag"), the tool captures clicks and highlights the flag under the mouse; right click cancels.

## World views (`StrategyViews`)

Reconciled after every tick, keyed by id, `RebuildFromState()` from state alone; no per-frame board scans.

| View | Shows | Inspect |
|---|---|---|
| `CityView` (player cities besides home) | `StrategyModels.City`: octagonal wall with towers and a gate, a keep with a blue banner, four houses (blue roofs; distinct from the home square); God View territory ring of `TerritorySegments` x segment length; tag "City (cards n/max)", "under demons" | points, cards here, demons (robber), hexes |
| `DemonView` (per flag with demons) | one creature per demon up to the cap + 1: hunched, horned, spiked back, long clawed arms, flickering red eyes; breathes and twitches, turns to watch you in first person; pulsing purple disc scaled with God View zoom; tag "n demons" ("FULL" in red at the cap) | where, cap, who it outbreaks to, how to clear |
| Robbed hex | dark outline and a star of dark strips over the hex; God View tag "Robbed: no batches" | - |
| `HeroView` | placeholder figure in gold with a staff, glowing orb, banner and cape; gold ring; walks the hex edge's four flags after `HeroMoved`; tag "Hero (n moves)" | moves, where, how to move |
| `SquadView` (player squads, rival garrison and raiders) | one soldier per unit (helmet, shield, spear; blue for you, red for the rival; guards with tall shields), two abreast, walking `From -> To` by `(Progress + tick fraction) / TicksPerEdge`, jabbing spears and leaning in while engaged, disc flashes on `UnitHit`; garrison in front of the outpost; tag "Squad 3: 4, 38/40 - attack" | units, health, order text, target |
| Outpost | flames grow with damage; destroyed: the stockade is flattened and charred with rubble; tag "Outpost hp/max, n guards"; inspect lines: status, rival plan, its built cards, rival score | |
| `PowerSign` (smelter, barracks, power plant) | lightning bolt above the building: glowing yellow powered / fuelled, blinking red without power, grey when idle or not needed | - |
| Wonder stages | stage 1 raises four columns and an architrave on the wonder site's platform, stage 2 an obelisk with a gilded tip | - |
| Bursts | an expanding ring at an outbreak (purple, with flashing lines to the neighbours it spread to), an epidemic (red), a clearing (gold) | - |

Labels: IMGUI tags at `GUI.depth` 9, culled by distance in first person, placed without overlap; God View building names (`EconomyViews`) are decluttered: zoomed out, only the selected, hovered or stalled buildings keep their tag.

## First person (`StrategyFirstPerson`)

- **Hesta** (market keeper): `MarketText`, your coin; "Open the market".
- **Captain Orla**: barracks status sentence, shields sentence, outpost status; "Barracks and squads".
- **Scribe Pell**: while a pick waits, "Pick a card" (the draft panel) and "Show my cards"; otherwise the headline, both score lines, the infection forecast and rounds left, "Show my cards", "The wonder".
- **The bell** ends the round (economy); its prompt shows the harvest-season countdown.

## AutoPlay, tests, captures

- `full_game` (`FullGameDriver`, a port of the headless competent/completionist `StrategyPlayer` in the sim tests): every command goes through the UI path a player uses (`SubmitThroughUi`: Build/Road/City/Hero tools, the End Round toolbar action, the panel actions, the inspect buttons). State-driven (no fixed ticks). Steps `GameClock.StepOnce` 10 ticks per frame with the normal clock paused; fails on any console error or no ending within the budget; logs `[AutoPlay] full_game OK: <Outcome> (<reason>) at game minute m, score a-b`. `-autoplay full_game` in a player.
- PlayMode `StrategyViewTests` (6): draft pick through the panel path unblocks End Round (clock paused for the draft, modal open in God View); founding a city shows its view and ring and adds the city points, the City tool highlights exactly the legal corners; a demon on a building's flag shows a view, stops the building (red badge, Problems), the Hero tool walks there and clears it; switching the plant off shows no power on the barracks (sign and sentence) and switching it on clears it; a squad ordered to attack walks and the fight resolves; the game ends with the end screen and a new game rebuilds every view from state.
- `AgentScripts/StrategyCaptures.cs`: `Run(name)`, `State()`, `UntilDraft/UntilAge/UntilCities/UntilWonder/UntilDemons/UntilBarracks/UntilAttack/UntilBreaches/UntilEnd/Ticks` (play with the driver, then pause), `God`, `GodAtVertex`, `Tool`, `Window`, `FocusDemons`, `FirstPersonAt`, `FirstPersonDemons`, `Measure`.

## Extension points

- New panel: add a `StrategyPanels.Kind` and a body `static void X(Flow f, WorldState s)`, then register a tool with `StrategyGodTools.PanelTool(id, label, order, key, kind, width, tip, badge)` and/or open it with `StrategyWindow.Open(kind)` from an NPC option.
- New action: a one-line wrapper in `StrategyUi` around `GodCommands.Submit(command, okText)`; add its case to `FullGameDriver.SubmitThroughUi` if the driver uses it.
- New world view: follow `StrategyViews.SyncAll` (dictionary by id, create / update / remove, nothing kept from events except one-shot effects).
- New hint: add a rule to `StrategyHud.ComputeWhatNow` in priority order, using the sim's sentence where one exists.
- `Flow` (`StrategyUi.cs`): tiny scrolled IMGUI layout (`Title`, `Head`, `Text`, `Buttons`, `Button`, `Box`, `Line`) sized from the previous frame.

## Needed from the sim (not public today)

- `EconomyLogic.CommissionTypes` is still internal (`EconomyLook.CommissionTypes` mirrors it).
- No helper names a flag in words: `StrategyUi.SiteName` builds "the sawmill", "your city by the forest and hills" from buildings, cities and hex terrain.
- No public "next hero step towards a flag": the driver and the test port `HeroRoute` from the sim tests.
- The rival's built cards have no city (they sit "at the outpost"); `DescribeCard(..).CityVertex` is the outpost corner or -1.

## Known limits

- One draft modal at a time; the draft only opens itself in God View (first person shows the line and Scribe Pell).
- Squads at one flag are spread on a small circle; a large fight at the outpost can overlap the stockade.
- The robbed-hex marker is a strip pattern, not a real grey-out of the tile.
- Soldiers and demons are primitive meshes without skeletal animation (the bob, jab and twitch are transforms).
