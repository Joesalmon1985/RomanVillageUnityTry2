# Strategy, Military and Save API

Unified design (`Docs/ECONOMY_DESIGN.md`, implemented 13 Nov 2026). Code: `Assets/World/Sim/Strategy` (`World.Sim.Strategy`), `Sim/Military` (`World.Sim.Military`), `Sim/Save` (`World.Sim.Save`). All commands go through `WorldSim.Submit` with `PlayerId = PlayerIds.Human`; every refusal has a code and a sentence. Read state freely; never write it from views.

## Flow

Prologue: rounds before the quartermaster commission (no draft, demons or rival; `FoundCity`/`BuildWonderStage` refuse `prologue`). Age I starts at the first round start after the commission (at the latest round `MaxPrologueRounds`+1 = 7) and pays `CommissionCoin` = 4 coin. Then 3 ages of 3 rounds (`Ages`, `RoundsPerAge`); every round lasts at least the harvest season (60 s, `EndRound` refuses `round_too_soon`, see ECONOMY_API). Each age starts with a draft deal for both sides and, from Age II, an **epidemic**; **each round of the age starts with one draft pick** (pause the clock with `PauseReasons.Draft` while `StrategyApi.DraftPending`: it becomes true at every round start of an age, not only at the age start). `EndRound` (economy command) runs, in order: at the last round of an age the **military comparison**, the rival's turn (plan points at the end of each age, garrison top-up, raids from Age II), the infection draw, the points check; after the last round of Age III the game ends by points (`age_end`). First side to `VictoryPoints` = 18 at a tick boundary wins; the breach track reaching `BreachLimit` = 5 loses.

## State

`WorldState.Strategy` (`StrategyState`): `Started, FirstRound, Age, RoundInAge, GameOver, Outcome (None|Victory|Defeat|Draw), OutcomeReason (points|breach|age_end|outpost), EndedTick, Demons[flag], Breaches (the breach track), Epidemics, LastOutbreaks, InfectionDeck (top = last)/InfectionDiscard/LastInfection, HeroVertex, HeroMovesLeft, Cards[id]{DefId, Age}, Decks[age-1], DraftStep (0 = no pick pending, n = pick n of the age pending, 1..`DraftPicks` = 3), DraftNextStep (added: the pick that becomes pending at the next round start; the passed hand already sits in `DraftHand` meanwhile, so show it as "next round's hand" only while `DraftPending` is false), DraftAge, Sides[player]{Hand, DraftHand, Built[{CardId, CityVertex}], Discarded, PlanVp}, Wonder{BuildingId, StagesDone, InProgressStage}, Comparisons[{Age, Tick, PlayerShields, RivalShields, Result (Tie|Win|Loss), PlayerVp, RivalVp}], Ledger, Tuning`. Cities live in `Economy.Cities` (the rival outpost is a rival city there).
`WorldState.Military` (`MilitaryState`): `Squads[id]{Owner, Kind (Field|Garrison|Raid), Alive, Units[{Id, Hp, Cooldown}], At, CurrentEdge, FromVertex, ToVertex, Progress, Order (None|Defend|Attack|Return), Target, Engaged, Occupying, BarracksId}`, `Queues[{BuildingId, Orders[{Supplied, Progress, Rations, Weapons}], HomeSquadId, Reason, ReasonWare}]`, `Outpost{Vertex, Hp, MaxHp, Destroyed, DestroyedTick, GarrisonSquadId}`, `Unpowered`, `UnitsTrained`, `ConsumedForUnits`, `Tuning`.

## Commands

| Command | Fields | Refusals (besides `game_over`, `not_owner`) |
|---|---|---|
| `FoundCity` | `Vertex` (a hex corner; adds `CarriersPerCity` = 2 carriers; its corner joins the infection deck) | `prologue`, `bad_vertex` ("A city must stand on a hex corner."), `occupied`, `too_close` (a city or the outpost one hex edge away), `no_road`, `cannot_afford` (3 planks, 3 bricks, 2 rations; names the shortfall) |
| `Trade` | `Give, Get, Count` | `same_ware`, `bad_count`, `cannot_afford`. Rate 4:1, -1 with an active Market, -1 with Merchant Guild, best 2 |
| `Sell` / `Buy` | `Ware, Count` | `bad_count` (sell in lots of 2 for 1 coin), `cannot_afford`. Buy costs 3 coin per ware |
| `MoveHero` | `Vertex` (a `CornerNeighbour`: one hex edge) | `bad_vertex`, `no_turn_action` (6 moves per round, Ranger Post +1), `not_adjacent`. The walk clears the two intermediate flags it passes and the arrival corner |
| `DraftPick` | `CardId` | `not_drafting`, `not_in_draft`. The rival picks at the same tick; after each pick but the last of the age the hands swap and the next pick waits for the next round start (`DraftPickPerRound`; false = the old back-to-back picks) |
| `BuildCard` | `CardId, CityVertex` | `not_in_hand`, `no_city`, `city_full` (3 per city), `cannot_afford` |
| `DiscardCard` | `CardId` | `not_in_hand` (pays 3 coin) |
| `BuildWonderStage` | - | `prologue`, `wonder_complete`, `stage_in_progress`, `no_wonder`. Requests the stage goods to the wonder site; completes on delivery (stage 1: 4 planks + 4 bricks = 2 points; stage 2: 2 iron + 2 rations = 3 points) |
| `QueueUnit` | `BarracksId, Count` | `no_barracks`, `bad_count`, `queue_full` (8). Each unit requests 1 weapons + 1 rations to the barracks |
| `OrderSquad` | `SquadId, Order (Defend|Attack), Target` | `unknown_squad`, `bad_order`, `bad_vertex` |
| `CancelOrder` | `SquadId` | `unknown_squad`, `no_order` (stops at the next flag) |

## Events

Strategy: `AgeStarted{Age, Round}`, `StrategyRoundStarted{Age, RoundInAge, HeroMoves}`, `CityFounded`, `Traded`, `Sold`, `Bought`, `DemonSpawned{Vertex, Count}`, `DemonRepelled{Vertex}`, `OutbreakOccurred{Vertex, Neighbours, Breaches, Limit, Chained}`, `BreachAdvanced{Vertex, Breaches, Limit}` (one per outbreak), `EpidemicStruck{Vertex, Age, Demons, Outbreak}`, `DemonsCleared{Vertex, Count, By: hero|squad}`, `InfectionDrawn{Vertices}`, `HeroMoved`, `DraftDealt` (age start, 4 cards), `DraftPicked`, `DraftPassed` (hands swapped after a pick), `DraftResumed{Age, Step, PlayerCards}` (added: the next pick is pending at a round start), `DraftFinished` (after the third pick; one card left in each hand is discarded), `CardBuilt`, `CardDiscarded`, `WonderStageOrdered`, `WonderStageCompleted{Stage, Vp}`, `MilitaryCompared{Age, PlayerShields, RivalShields, Result, PlayerVp, RivalVp}`, `RivalScored{Vp, Total}`, `GameEnded{Outcome, Reason, PlayerVp, RivalVp}`.
Military: `UnitQueued`, `TrainingStarted{BarracksId}`, `UnitTrained`, `QueueStateChanged{BarracksId, Reason, ReasonWare}`, `PowerChanged`, `SquadOrdered`, `SquadMoved` (animate over `MilitaryTuning.TicksPerEdge` = 20 per segment), `SquadArrived`, `SquadEngaged`, `UnitHit`, `UnitDied`, `SquadDestroyed`, `OutpostDamaged`, `OutpostDestroyed`, `GarrisonReinforced`, `RaidLaunched`, `RaidOccupied`, `RaidEnded`.

## Query helpers for panels

`StrategyApi` (all `(WorldState s, ...)`):
- HUD: `Status(s)` -> `StrategyStatus{Started, Age, Ages, RoundInAge, RoundsPerAge, RoundsLeft, DraftPending, GameOver, Outcome, OutcomeReason, Headline, Breaches, BreachLimit, HeroVertex, HeroMovesLeft, NextInfection, Coin, VictoryPoints}`; `Headline(s)`; `Roman(n)`; `AgeRound(s)`.
- Score for both sides: `Score(s, player)` / `Scores(s)` -> `ScoreBreakdown{Cities, Cards, Wonder, Military, Outpost, Plan, Total}`; `ScoreText(s, player)` ("Cities 4, wonder 2, cards 1, military 2, outpost 0: 9 of 18." / rival "Plan 1, cards 2, military 1: 4 of 18."); `Comparisons(s)`.
- Cities: `CheckCity`, `CitySites` (every flag with its verdict), `LegalCitySites(s, ignoreCost)` (corners), `CityCostText`, `PlayerCities`; `Board.SuggestedCitySites`.
- Market: `TradeRate`, `SellWaresPerCoin`, `BuyPrice`, `MarketText`.
- Demons: `Demons(s)` (flag, count), `DemonsAt`, `DemonCap` (3; Ward Stones 4), `InfectionCount` (2/3/4, Beacon Tower -1), `OutbreakNeighbours(s, flag)` (who gets a demon if it outbreaks), `TakesDemons(s, flag)`, `IsHexRobbed(s, hex)`, `HeroMoveTargets`, and the forecast `Forecast(s)` -> `InfectionForecast{DeckSize, DiscardSize, Discard[], NextDraws, DemonCap, SitesAtCap[], SitesAtCapInDiscard[], Breaches, BreachLimit, Epidemics, EpidemicNextRound, Sentence}` ("3 infection cards drawn at round end; 4 in the deck, 3 in the discard; 1 site at 3 demons; breaches 1 of 5.").
- Cards: `DraftHand`, `Hand`, `BuiltCards`, `CardsAtCity`, `CitiesWithCardRoom`, `CardsByFamily(s, family)`, `DescribeCard` -> `CardView{CardId, DefId, Name, Age, Family, FamilyName, Colour, CostText, EffectText, CityVertex, Affordable, MissingText}`, `CardDefOf`, `Effects`; table `CardTable.All / Get / ForAge / ForFamily / FamilyName / FamilyColour`.
- Wonder: `Wonder(s)` -> `WonderInfo{StagesDone, StageCount, InProgressStage, BuildingId, Status, Lines ("bricks 2/4"), CanOrder, StageVp[]}`.
- `WaresSpent(s)` (strategy ledger per ware).

`MilitaryApi`: `Power(s)` -> `PowerSummary{Supply, Demand, FuelledPlants, Consumers[], Sentence}`, `IsPowered`; `DescribeBarracks(s, id)` -> `BarracksInfo{Queued, HeadSupplied, ProgressPercent, TrainingTicks, Reason, WaitingFor ("1 weapons"), Status, CostText}`; `TrainingTicks`, `UnitCostText` ("1 weapons and 1 rations per Warden"); `Squads`, `DescribeSquad` -> `SquadView`; `Outpost(s)`; `RaidedFlags`; **shields**: `Shields(s, player)` and `ShieldSummary(s)` -> `ShieldSummary{PlayerShields, PlayerSoldiers, PlayerCardShields, RivalShields, RivalGuards, RivalRaiders, RivalCardShields, NextComparisonAge, RoundsLeftInAge, WinVp, LossVp, Sentence}` ("Shields 3 (2 soldiers, 1 from cards) against the rival's 2 (2 guards at the outpost, 0 from cards): at the end of Age II you would win 3 points.").

## Cards (24, 8 per age; every family in every age)

| Age | Raw and crafted | Commerce | Civic | Military | Lore |
|---|---|---|---|---|---|
| I (1 rations or coin) | Lumber Camp: forest batches +2 (1 R); Clay Diggers: hills +2 (2 coin) | Toll Gate: 2 coin per round (2 coin) | Shrine: 1 point (1 R + 1 coin); Village Green: 1 point (1 R) | Palisade: 1 shield (2 coin); Watch Post: 1 shield (1 R) | Ranger Post: +1 hero move (2 coin) |
| II (rations + bricks) | Granary: fields and pasture +2; Charcoal Guild: charcoal kilns +1 coal per cycle | Road Guild: a road payment covers 3 more segments | Monument: 2 points; Aqueduct: 2 points (1 R + 2 B) | Drill Yard: 1 shield, training 40% faster; Stockade: 1 shield (1 R + 2 B) | Ward Stones: a site holds 4 demons before it outbreaks |
| III (rations + iron) | Master Smiths: smithies +1 weapons per cycle | Merchant Guild: market rate -1, +1 coin per round | Great Hall: 3 points (+2 coin); Census Hall: 1 point per city; Forum: 4 points (1 R + 2 iron) | Armoury: 2 shields, soldiers +1 damage; Siege Works: 1 shield, soldiers +1 damage (1 R + 1 iron) | Beacon Tower: one fewer infection draw |

## Rules in brief

- **Infection.** Deck: the flags within `InfectionRadius` = 2 segments of home (home itself joins in Age II; each founded city adds its corner). Each round end draws 2/3/4 cards by age. A drawn corner always takes a demon; an intermediate flag only while it holds a building or site, a road end, a city or demons (otherwise the card is discarded and the next drawn). A demon on the hero's flag is repelled.
- **Outbreak.** A site that would gain a demon beyond `DemonCap` (3) outbreaks instead: the breach track advances by one and each connected neighbouring site gains a demon; a full neighbour outbreaks in turn; each site outbreaks at most once per draw. *Connected neighbouring site*: walk from the site along each of its road segments (built or not) in a straight line to the first flag that can hold demons; a corner has up to 3 neighbours, an intermediate flag 2; the outpost is never one.
- **Epidemic** at each age change (II and III): the bottom card whose site can hold demons is drawn and its site filled to 3 (an outbreak if it already held demons); the discard pile is shuffled and put on top of the deck.
- **Effects.** A demon (or a raid) on a flag blocks carriers through it (`demon_on_route`) and stops the building there (no harvest, recipe, construction, fuel, training or wonder work). A hex whose every touching player city holds a demon gets no batches and its harvesters stop (the robber).
- **Answers.** The hero (6 moves per round, clears what it walks over), squads (clear the flag they arrive at; `OrderSquad` Defend/Attack to any flag), lore cards.
- **Batches** at your cities grow by 2 per age (`AgeBatchBonus`) plus raw-card bonuses.
- **Military comparison.** At the end of each age: your shields = your living soldiers in field squads + military card shields; the rival's = its living garrison guards (a flat 2) + its built military cards. Win +1/+3/+5 by age, lose -1, tie 0; mirrored for the rival; recorded in `Comparisons` and `MilitaryCompared`.
- **Soldiers.** Training (150 ticks; Drill Yard -40%) starts when 1 weapons + 1 rations are in the barracks store and runs only while powered and not under a demon; units form one squad per barracks (max 4). Squads walk the flag graph (20 ticks per segment); combat on fixed ticks (HP 10, damage 3, cooldown 10; guards HP 8, damage 2). Outpost (60 HP) destroyed: +2 points, the rival's plan and raids stop.
- **Rival** (deterministic, visible rules): plan +1 point at the end of each age (`RivalPlanRounds` 3, 6, 9); garrison of 2 (`GarrisonBase` 2, `GarrisonPerAge` 0), topped up at its turn; from Age II a raid of 1 walks to your road flag nearest the outpost, holds it (blocking it) for a round, then goes home; drafts the card worth most to it (shields and points, the larger first; a card it can no longer build counts only as denial) and **builds** (free, at the outpost, `CardBuilt` with `PlayerId` 1) the point and shield cards it drafts: at most `RivalBuildsPerAge` = 2 per age, of them `RivalShieldCardsPerAge` = 1 shield card, `RivalMaxBuilt` = 9 in all. Its built civic cards score (`Score(s, 1).Cards`), its military cards add shields. The rest of its picks only deny you.

## Save (`World.Sim.Save.SaveCodec`)

`ToJson(WorldState, PlayerPose pose = null, extras = null)`; `FromJson(json)` -> `SaveModel{Version, Tick, Player, Extras, State}` (throws `FormatException` for empty, broken, older or newer saves; `CurrentVersion` = 3, `OldestVersion` = 3: version 3 adds `EconomyState.RoundStartedTick`, `StrategyState.DraftNextStep`, the new tuning fields and the 24-card decks); `CreateSim(state)` registers Economy, Strategy and Military (systems, handlers, hooks) without InitState; `Load(json, out model)`; `StateJson(state)`. Save between ticks: queued commands are not saved. All new state (comparisons, epidemics, outbreak lists, bonuses, mine credit, blocked flags, carrier idle time) is plain data and round-trips.

**Balance:** `Docs/ECONOMY_PROOF.md` (sections E and F) lists every scripted game, its timeline and the measured verdicts.
