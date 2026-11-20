# WU-7: the Ward duel, world first

20 Nov 2026, branch `godboard/wu7-duel` (from `a780993`, `godboard/main` with WU-2b merged). Scope: the WU-7 row of `Docs/WORLD_UI_PLAN.md` section 5 and 2.7. No sim file changed; no rule changed.

## Done

- **`DuelWorldProps`** (`Assets/World/Game/Encounters/DuelWorldProps.cs`), built by `WardDuelEncounter.Begin` for every duel mode (practice, friendly, lethal) and removed with it. Objects (all primitives, unlit, original):
  - **Essence stones** on a cord along the bottom of the frame, one per essence of the duel's pool (Flame a tall point, Frost a crystal, Vine wide and low, Light a flat star: shapes as well as colours). Tap a stone to add its essence to the hand.
  - **The hand**: three sockets (the weave being shaped, one orb each) and a palm. Tap an orb in a socket to take it back (that orb; later ones close up). Tap the palm: the first tap previews ("Lock this Ward?" in setup, "Cast this weave?" when active, on the WU-0 ledger card), the second tap (or a tap on the card) confirms and sends the one command (`LockWard` / `Cast`). An incomplete hand, an out-of-casts hand and a recovering hand are refused in words on the card ("The hand is still recovering."); the palm glow returns as recovery runs and pulses in the last 15 s before the idle auto-cast. The orbs circling the caster's own hand (`WardDuelStage.SetGathering`) now show in every mode.
  - **Beads** (casts left) on a rail across the top of a post beside each wizard (lit = left, dark = spent); tap for the count in words.
  - **Tally sticks**, one per side on the same post: a row per cast of three pegs coloured fracture / echo / fade, always sorted by kind, so no position can be read. Tap a stick: the ledger lists each cast (weave names and the three totals), the totals "In all" and the legend. Never a locus (CR-26).
  - **Referee** (placeholder actor): tap for the state in words and counts (casts left, the hand state, the rules legend for a first-timer). At the end he walks to the middle and the ledger opens by itself with the result and what to tap.
  - **Wards revealed in the air** at the end (three gems over each Ward, in the revealed order); hidden again on a rematch.
  - **Gate** (left end of the bottom row): leaves by preview then confirm. Practice and friendly: "Leave the duel?" (harmless, as before). Lethal: the card says "Garrick will pay for this. Leave?" (`CampaignDuel.SurrenderLine`) and the confirm is the existing `Surrender()`. At the result it is "Go back to the ring?" and the confirm is `Return()`. Tapping **Mira (or the player's wizard)** at the result previews and confirms the rematch (refused in words in a lethal duel).
- **Placement and size**: the rig is fixed to the view (camera space), laid out from the safe area and the screen height each frame; every stone, socket, palm and the gate is at least `WorldHitTarget.MinSide` (enlarged hit rects, nearest centre wins). Taps arrive as screen points through `WardDuelEncounter.TapAt` / `InjectTap` (mouse and touch polled in `Tick`); keys are unchanged (1 to 4, Backspace, Enter, Space, R, Esc) and call the same methods.
- **WU-0 interaction**: a private `WorldActionMachine` in the props (as WU-5 did for the log seat), because the shared `WorldActionController` cancels the shared machine while an encounter runs. The ledger shows through `LedgerCard.Show`; a tap on a card that only tells closes it. `WorldActionController` and `WorldActions` are untouched, so nothing else changes.
- **Clear view** (`DuelLayout` kept): `GodBoardSettings.ClearView` is the one setting. On: the props are hidden, world taps do nothing and `DrawCompact` / `DrawDesktop` draw the panel exactly as before. Off (default): the screen shows only the lethal **stakes card** (needed for correctness) and a one-line toast (idle auto-cast notice, a full hand). H toggles the setting in the duel (as on the board).
- **Zone tags**: `DUEL-STATE` (both posts, `WorldZone`), `DUEL-END` (referee, `WorldZone`; the gate is named `DUEL-END gate`), `DUEL-WEAVE` (stones, sockets and the palm are named `DUEL-WEAVE ...`; they live in camera space so they carry the name, not a `WorldZone`), DUEL-STAGE is the existing stage.
- Return to the hub: `EncounterHost` restores the walker pose as before; the ring errand (Mira walks home) is untouched. `View/Camp/DuelRing.cs` and `Player/*` were not edited.

## Tests (editor 6000.3.28f1, Unity pipeline test runner)

- **`WardDuelTests` 5/5** (2 new): `Stones_BuildTakeBackAndCast_RecoveryBlocksASecondCast_BeadsAndTallyMatchTheSession` (Ward built from stones, an orb taken back, preview then confirm to lock; an incomplete weave is refused; cast by preview and confirm; recovery refuses a second cast in words and nothing is cast; beads equal the casts left on both sides; tally pegs equal the `PlayerHistory` totals; rows sorted; ledger text clean of AB-1 words; nothing in the air) and `Duel_NoLocusInTheWorldBeforeTheEnd_ResultRevealsBothWardsInTheAir_RematchAndGateReturn` (no place word in any ledger, no Ward in the air before the end, both Wards in the air after, the referee speaks, a tap on the caster rematches, the gate leaves and the avatar pose is restored).
- **`CampaignDuelTests` 13/13** (3 new): friendly duel by stones with tally and beads equal to both histories and every stone on screen at the hit size; **lethal**: stakes card first (no rig, no world tap), then stones and the gate: the preview names the cost, the confirm is one surrender, one resolve, one volunteer lost, no rematch, the gate returns; Clear view draws `DuelLayout` and hides the stones, then the one setting switches them back. The earlier lethal tests (victory, defeat, confirmed leave, exit and reload, no-leak, practice as request, staging) are unchanged and pass. `Layout_LiveDuel_UsesTheSafeArea` now sets Clear view first (it reads `LastLayout`).
- **`HubTests` 10/10** (1 new): `Duel_GateLeavesByTwoTaps_AndTheHubViewIsBackAtTheRing` (from the ring: Mira walks over, the stones stand, the gate previews then leaves, Mira walks home, the player position, yaw and pitch are as before, no props left).
- Regression: `WorldUiTests` 6/6, `DanceTests` 15/15 (one run failed `MidSongPause...` by 4 ms on a song-time equality while three other editors were busy; re-run alone it passed), `RhythmPartyTests` 4/4. Not run: the full PlayMode suite, `dotnet test DotNet` (no sim change), EditMode.
- Console: 0 errors from game code (clean batch compile and clean editor compile).

## AutoPlay (rebuilt player, `BuildWindows.Run`, 101 MB, 0 errors; 844x390 windowed, `-novsync`)

`duel_friendly` OK (two duels), `duel_keeper_win` OK, `duel_keeper_loss` OK (including the reload), `duel_dance` OK (the duel, then the dance with 20 perfect). All exit 0, logs in `Builds/logs/wu7_*.log`. These scripts and `WardBot` play through the encounter's public methods, not through taps; the taps are covered by the PlayMode tests. The line-ending-only change to `ProjectSettings/ProjectSettings.asset` from the build was reverted.

## Captures (local only, `Evidence/worldui/`, never committed)

`wu7_{setup,preview,active,cast,break,result,clearview}_{1920x1080,1024x768,844x390}.png`, simulated notch at each size, every PNG header checked by the driver `check` (all exact). Driven by new `duelstart` / `duel ...` / `shotafter` commands in `AgentScripts/WorldUiCaptures.cs` (a practice duel from the camp at night).

## Not done / open

- The ledger tablet (setup, preview, result) sits over the stage on a phone because the referee, its anchor, stands in the middle; a card anchored at a screen corner for these would be clearer.
- Stones, hand and gate are fixed to the view (like the god board ledge), not objects standing on the ground; the beads and tally sticks are real objects at the wizards. At 844x390 the opponent beads and stick are small (about 6 px beads); a tap on the stick gives the exact words.
- The "break" capture shows the opponent Ward gone and the revealed gems; a capture at the exact shatter frame was not taken (the `shotafter 0.8` capture lands after the shards settle).
- The captures start the practice duel from the camp (capture driver); from the duel ring the code path is the same (`HubTests` covers the start and the return, not a capture there).
- Referee, caster and opponent bodies are the existing placeholder actors; the referee has no animation beyond walking to the middle.
- The AutoPlay scripts do not tap the stones; an AutoPlay variant through taps would be a small addition to `CampaignDuelScripts`.
- `DuelRing.StartDuel` was not touched (WU-1b may wrap it as a two-tap entry); nothing here depends on its shape.

## Next steps

1. Owner look at the captures; tune the bottom row (spacing, palm size) and the post distance (`DuelWorldProps.PostSide`).
2. Move the referee card to a fixed corner anchor on phones; consider a bigger bead rail for the opponent.
3. WU-8: fold a duel-by-taps step into the tour.
