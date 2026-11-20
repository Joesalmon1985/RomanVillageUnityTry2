# WU-3: the plague at the healer's lodge

20 Nov 2026, branch `godboard/wu3-lodge` (from `9fa6bff`). Scope: the WU-3 row of `Docs/WORLD_UI_PLAN.md` section 5, inspection only (owner's answer Q-4). `Assets/World/Game` reads state and sends only `Banish{Node}`; no rule changed.

## Done

- **Lodge props** (`Plague/LodgeTable.cs`, `LodgeTable.Build` called by `HealersLodge.Build`). The lodge is a closed hut, so the "interior" stands in its yard in front of the door (frame local, so they follow the hub layout): a carved **map table** (island hexes tinted by region, 73 trails: cream cords, gold crossings, the tunnel a dark groove, 48 element pebbles, demon figurines by kind per node; each pebble is a tap target with its own ledger: region, demons by kind, whether its hex is blocked), a **warning post** (8 lanterns, one lit per outbreak, a post light that brightens), a **rate stick** (nine notches, peg at the current step, bar width = rate), four **vessels** (empty / cured clear light / eradicated stone lid / strengthened dark and cracked), the **runner's satchel** (last draw and the previous eight), the **banish charm's peg** (ready or exact seconds), a **round-draw sign**. Props show only while the plague runs (hidden when `Ended` or Historic; after IslandFell they stay, all lanterns lit). An `Infected`/`Epidemic` event drops the figurines onto the table.
- **Ledgers** by `LodgeInspect` (IInteractable that calls `WorldActions.Inspect`): words and counts only; no deck, epidemic or card count anywhere.
- **Healer**: lines changed ("Read my table, the post and the vessels"), two options "How bad is it?" (exact outbreaks of 8, rate; opens the post's ledger) and "Is my charm ready?" (seconds; opens the peg's ledger).
- **Banish at the shrines** (`NodeView` is now an `IWorldAction`): first tap previews (gold ring pulsing at the shrine, demons flinch, ledger "Banish a fire demon from here?"), second tap sends `Banish{Node}` once. Refused with a reason that is both on the ledger and spoken in a bubble: too far ("Walk closer to cast."), no demons, cooling ("still smouldering: ready in N seconds"), plague over / island fallen (disabled). Shrines register for the enlarged hit target only while they hold demons, so ground taps near empty shrines still walk. B still works (`TryBanishNear`).
- **Charm in the world** (`BanishCharm`, held at the lower right of the first-person view): glows gold and pulses when ready, a dull cooling ember (scaled) while the 300-tick cooldown runs. The preview ring at a shrine shows the same gold or ember.
- **Screen buttons removed**: the "Plague" button, the "Banish [B]" button/bar and the counter panel are drawn only with the **Clear view** setting; the board's round Plague icon (`GodBoardHud.PlagueToggle`) is offered only then. There was no Clear view setting yet, so `PlagueHud.ClearView` (PlayerPrefs `world.clearview`, default off) is the switch for the settings menu to flip.
- **Toggle for the board totem**: `PlagueViews.ToggleOverlayFromTotem()` (returns the new state) and `PlagueViews.OverlayOn`. V unchanged.
- Zones tagged: `PLG-MAP`, `PLG-DANGER` (post and stick), `PLG-CURES` (4 vessels), `PLG-DECK`, `PLG-ACTIONS` (charm peg), `PLG-PHASE` (sign), `PLG-CITY` (each of the 48 shrines).
- `AgentScripts/WU3Drive.cs`: capture driver (`Temp/wu3_cmd.txt`: `at outside|table|post|vessels|...`, `infect`, `outbreaks N`, `cure/erad/strong E`, `storm`, `runner`, `ledger ...`), used with `WorldUiCaptures` (`godboard`, `size`, `shot`, `check`).

## Tests actually run (editor 6000.3.28f1, 20 Nov)

- `PlagueViewTests` **9 / 9** (4 old, 5 new: table = overlay demons per node and kind after infection and after a banish; lanterns = outbreaks, peg = rate step, vessels follow cure / eradicate / Ash altar; AB-1 and no "remain / cards / deck / epidemics" across every lodge text (60+ strings), zones tagged, a prop tap opens a ledger; shrine tap far = refused with "closer", near = preview, confirm once, burst swallowed, cooldown, charm cools, cooling refusal; no screen buttons by default, the totem toggle).
- `HubTests` 9 / 9 (one assertion now counts only `HUB-` zones under the hub root), `WorldUiTests` 6 / 6, `GodBoardTests` 10 / 10, `GodBoardVillagerTests` 7 / 7.
- Player build `AgentScripts/BuildWindows.cs` succeeded (0 errors); AutoPlay `plague_epidemic` **OK** (844 x 390, log `Builds/logs/plague_epidemic.log`, no exceptions). No control it uses moved.
- Not run: the full PlayMode suite, EditMode (`WorldActionTests` build-define test), `dotnet test`. Not tested: the Clear view buttons drawn with the setting on (IMGUI), the held charm on a real phone view, shrine taps with a real pointer (tests call `WorldActions.Tap`).

## Captures (local, not committed): `Evidence/worldui/`

`lodge_outside_*`, `lodge_table_close_*`, `lodge_node_ledger_*`, `lodge_after_outbreak_*`, `lodge_island_falls_*` at `1920x1080`, `1024x768`, `844x390` (notch safe area simulated; every PNG checked to its exact size). The "outside" and "table" sets are at 1 outbreak with Earth cured and Water strengthened; "after outbreak" has 4 lanterns lit; "island falls" is 8 outbreaks plus the storm (the runner is not in the shots: the clock was paused).

## Open / next

- The table's island fills about two thirds of the table; the hex plates and figurines overlap at nodes with 3 demons of mixed kinds. The tunnel groove is hard to see at distance.
- The camera pitch is fixed (8 degrees), so the table top reads at a shallow angle from standing distance; a close approach (about 2 m) is needed for the pebbles.
- Pick targets on the table pebbles and jars are small (below 7 % of the height at phone size); the props are registered only as whole objects for the enlarged hit.
- No horn sound from the struck region's shrine yet (the plan's round-draw sign mentions pebbles only; the shrine pops as before).
- The settings menu must expose `PlagueHud.ClearView`; the god board totem package must call `PlagueViews.ToggleOverlayFromTotem()`.
- Held charm covers the lower right of the view; check with the touch controls overlay.
