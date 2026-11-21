# TUTORIALS: a first-time tutorial for every minigame

21 Nov 2026, branch `godboard/tutorials` (from `godboard/main` f2c4653). Owner's request: "give each game a tutorial which helps the player do their first turn and gameplay loop of each."

## What it is

A small shared system in `Assets/World/Game/Tutorial/`. Each game has a list of steps. A step has a **tutor** (a person already in that place), a short **line** in a speech bubble, a **ring** on what to tap now, and a **condition read from the game** that ends it. The tutorials only talk and highlight: they send no command, change no rule, and never block the game (the AI seats keep playing, the clock keeps its own rules).

- **Tutor bubble**: a paper-coloured speech bubble with the tutor's name, `n / N`, the line and a **Skip** reply. It hangs over the tutor's head when the tutor is on screen, and sits at the top of the safe area otherwise (always on the board, where the tutor is on the ledge). It is placed to stay inside the safe area and off the ring and the ledger card. A tap on the bubble never reaches the world (`SpeechBubbles.HandleTap` and the board's `Over` know it).
- **Ring**: a pulsing rounded gold ring (with a second ring that grows and fades) round the screen bounds of the target, never smaller than 9 % of the screen height. When the target is out of view, a short soft gold pulse shows at the screen edge on its side and the bubble adds "Out of view: turn towards the glow at the edge." (no arrow, no overlay).
- **Out of order**: the director advances while the current step's condition already holds, so doing things early moves the tutorial on without a word. Conditions are read from the sim's events and state (`Built`, `Traded`, `TurnEnded`, `CouncilBanked`, ...), from the ledger tablets that were opened (`LedgerCard.Changed`, so the duel's and the dance's private action machines count too) and from the games' own public state.
- **Tips** are said when something happens, outside the order, and never block: an envoy's offer, a 7 with the discard sack (`The gods are angry ...`), the epidemic, a council bust. A 7 is never forced.
- **Pause and resume**: away from its place a tutorial waits where it is; another game's may start meanwhile and the first resumes when the player is back. A new world (new game, load) starts every unfinished tutorial over.
- **Remembered**: "done" is stored per game in `PlayerPrefs` (`WorldMVP.Tutorial.Done.<game>`). Automated runs (tests, `-autoplay`, `-runTests`) use a memory store so nothing is written to the registry.
- **Replay**: Esc menu, page "Tutorials" (a system menu): one "Show me how again" for each game. The tutorial then begins when the player is at that place, and shows **every** step, including those whose condition already holds (each is read for four seconds and goes on). Skip is remembered as done.

## The steps

| Game (id) | Tutor | Steps (condition) | Tips |
|---|---|---|---|
| Hex board (`board`) | temple keeper at the altar, the prince on the ledge | 1 sacrifice at the altar (table opened) 2 camp ghost (ghost shown) 3 camp confirm (setup camp built) 4 road ghost 5 road confirm 6 read the baskets (the baskets' tablet opened) 7 read the build costs (the bench's tablet opened) 8 wait for the turn (it is the player's main turn) 9 build or trade (a `Built` or `Traded` by the player) 10 end the turn with the hourglass (the player's `TurnEnded` in the main phase) 11 leave by the lamb stone (`TablePaused`). Outro by the keeper. | envoy offer; a 7 and the discard sack; the epidemic |
| Plague (`plague`) | the healer | 1 ask "How bad is it?" (her dialogue option) 2 read the warning post's lanterns 3 the rate stick 4 a vessel 5 walk to a shrine with demons and tap once (preview) 6 tap again (banish sent) 7 watch the charm cool. If no demon stands yet, step 5 says so and waits. | none |
| Council (`council`) | the recorder | 1 press on (the candidate's hand) 2 preview a proposal note (both places light) 3 commit it 4 read the recorder's tablet 5 push once more 6 bank with the seal (preview, then confirm). | a bust (pending or taken) |
| Rhythm (`rhythm`) | Sol | 1 hit the drums (a judged hit) 2 a block's torch keeps or dims 3 pause on the log seat and resume (four-count) 4 read Sol's tally 5 tune with the bell (between phrases). | none |
| Crystal dungeon (`dungeon`) | Mira | 1 step with the forward footprint stone 2 turn 3 tap a marker where Mira has an errand (her offer) 4 tap again (she acts) 5 tap a companion (card) 6 open the journal on Mira's hip 7 "Lead me out" (companion card, second tap). Starts when the player is underground. | none |
| Ward duel (`duel`) | the referee | 1 fill the hand with stones 2 take an orb back 3 lock the secret Ward with the palm (preview, confirm) 4 build and cast a weave (preview, confirm) 5 read the beads 6 read the tally stick 7 leave by the gate. Practice and friendly duels only (a lethal duel keeps its stakes card). | none |

## Files

- `Tutorial/TutorialCore.cs`: `TutorialStore`, `TutorialStep`, `TutorialTip`, `Tutorial`, `TutorialTargets` (turn a world object, a world point or a ledge item into a screen rect).
- `Tutorial/TutorialDirector.cs`: the runner (created after the scene loads, survives scene reloads): loop, tips, replay, drawing of ring, edge glow and bubble.
- `Tutorial/BoardTutorial.cs`, `PlagueTutorial.cs`, `CouncilTutorial.cs`, `RhythmTutorial.cs`, `DungeonTutorial.cs`, `DuelTutorial.cs`: one per game.
- `Tutorial/TutorialMenu.cs`: the Esc menu page.
- Small edits elsewhere: `UI/Hud.cs` (third menu tab "Tutorials"), `UI/SpeechBubbles.cs` and `Board/GodBoard/GodBoardHud*.cs` (a tap on the bubble is not a world tap), `View/Camp/HealersLodge.cs` (a counter `HowBadAsked`). Not touched: the hub layout, `Player/*`, `Boot/*`, the prince's lines.

## Tests

`Assets/World/Tests/PlayMode/TutorialTests.cs` (PlayMode, fixture `World.Game.Tests.TutorialTests`) and `TutorialTestSetup` (a `SetUpFixture`: tutorials are **off** and memory-only in every other fixture of the assembly; `TutorialTests` turns them on and scales the reading times down so a playthrough takes seconds). One test per game plays the tutorial through with the same taps and commands the game's own fixtures use, checks that each step comes up in order with a line, a named tutor and a target that lies on the screen, that Skip ends it and is remembered, that "Show me how again" starts it over, and that finishing is remembered.

### Results (21 Nov 2026, editor 6000.3.28f1, final code)

- `TutorialTests` **8 / 8** PlayMode: one per game (board, plague, council, rhythm, dungeon, duel) plus the Esc menu page (every game listed, "Show me how again" starts the board tutorial) and the council's bust tip. The pictures were taken from the same tests (capture mode), 8 / 8 as well.
- Regression, all with the tutorials off by `TutorialTestSetup`: `GodBoardTests` 10 / 10, `GodBoardLedgeTests` 21 / 21, `PlagueViewTests` 10 / 10, `CouncilTests` **41 / 41** (the long one, once), `DanceTests` 17 / 17, `ExpeditionTests` 14 / 14, `InteriorTests` 16 / 16, `WardDuelTests` 6 / 6, `HubTests` 19 / 19, `WorldTourTests` 2 / 2, `WorldUiTests` 6 / 6. EditMode `World.Game.EditTests` 25 / 25 (includes the player-build compile test).
- Rebuilt player (`BuildWindows.Run`, 102 MB, 0 errors), run with `-batchmode` (no window, so no window-size registry key was written): `board_first_round` OK and `world_ui_tour` OK, exit 0, with the tutorials **on** in both (the log shows `[Tutorial] board begins`, its steps, then council, rhythm, duel and dungeon beginning during the tour; logs `Builds/logs/tut_*.log`, not committed). `ProjectSettings/ProjectSettings.asset` line-ending change from the build reverted.
- Console: 0 errors from game code.

### Captures (local only, never committed)

`Evidence/tutorials/<game>_<step>_844x390.png`, one per step of every game (51 files), taken at 844 x 390 with the simulated notch safe area, by `TutorialTests` in capture mode. Looked at: the ring is on the target (or an edge glow when it is out of view) and the bubble sits clear of it in the ones checked: `board_sacrifice`, `board_settle-preview`, `board_road-preview`, `board_baskets`, `board_end-turn`, `board_leave`, `board_outro`, `plague_lanterns`, `plague_banish-confirm`, `plague_vessels`, `plague_charm`, `council_preview`, `council_tablet`, `council_bust`, `rhythm_hit`, `rhythm_pause`, `rhythm_torch`, `duel_stones`, `duel_take-back`, `duel_cast`, `duel_gate`, `dungeon_step`, `dungeon_marker`, `menu_tutorials`.

## How to see it

- Player: start any god board game. At the altar the temple keeper begins. The other tutorials begin where the game does (the healer's yard, the council, the dance, the dungeon, the duel ring).
- Again: Esc, "Tutorials", "Show me how again". (Esc belongs to the god board while it is open: leave by the lamb stone first.)
- Editor tests with pictures: write the folder into `Temp/tutorial_capture.txt` and run `TutorialTests`; each step is saved as a PNG at 844 x 390 with the phone's notch (local only, never committed).

## Open problems

1. **The tutor is a name on the bubble, not always a figure you can see.** On the god board the prince stands on the ledge but the bubble sits at the top of the screen (so it never hides the island's spots or the ledge); in the dungeon Mira is often out of view. The name and the speaking order carry it; there is no pointer from the bubble to the tutor when the tutor is off screen.
2. **Off-screen targets** are shown by an edge glow and one line of words, not by an arrow (the brief forbids arrows). In the dark yard of the lodge this is a soft gold strip at the screen edge; it is easy to miss on a bright screen.
3. **The board's first turn is real time.** The AI turns and the hourglass keep running while the tutorial talks (no rule or pause was added); a slow reader can lose their first turn to the hourglass. The steps then move on by themselves (a turn that ended counts as "built or traded" and "ended the turn").
4. **The plague's banish step can wait a long time** (infection is slow: every third round): it speaks only in the healer's yard, and is silent elsewhere, until a demon stands on a shrine. The healer's tutorial may therefore not finish in one visit; it resumes where it stopped.
5. **Replay shows every step**, including the ones whose condition already holds, each for four seconds; so a replay of the board does not make you place a second camp, it tells you about the step and moves on.
6. **The ledger tablet and the tutor's bubble can overlap** when a tablet from an earlier step is still open and the screen is small (the bubble draws on top and avoids the tablet where there is room). Tapping elsewhere closes the tablet.
7. **Not tested by hand:** a real finger on a phone (taps were driven through the same entry points as the other fixtures), audio, and the PlayerPrefs store in a built player (the tests and the batchmode runs use the memory store).
8. The tutorial reads time with `Time.unscaledTime`; `TutorialDirector.TimeScale` exists only so tests can run fast.
