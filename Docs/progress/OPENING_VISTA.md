# Opening vista: the game opens over the whole camp

21 Nov 2026, branch `godboard/opening-vista` (from `godboard/main` `f2c4653`). From the owner's first hands-on feedback (screenshots 8 to 12): no four step pads; the screen divided into areas that each lead to an activity; every minigame visible and findable from the opening screen, using uneven ground, buildings and people; free look while someone leads. No sim file changed, no rule changed; `Assets/World/Game` still only reads state and makes the same calls the hub's people and props make.

## What changed

1. **A new game is the god board hub.** `WorldBootstrap.CreateNewGame`: no save and no `-start` gives `GodBoardStart.Create` (what `-start dev.godboard` gives), both for a plain launch and Esc → New game. The legacy prehistoric economy (the prince's woodcutter's hut, screenshots 10 and 11) is now the developer start **`dev.legacy`** (`DevStart.LegacyId`, also in F1 → Starts). A **packaged AutoPlay run without `-start`** keeps the legacy game its scripts were written for (`first_scene` etc. need no change). `WorldBootstrap.DefaultStart` (static) decides; the PlayMode test assembly sets it to `dev.legacy` for the older tests (`Tests/PlayMode/TestDefaults.cs`, a `[SetUpFixture]`).
   The owner's 10/11 screenshots were this legacy game: the exe was launched without `-start dev.godboard`.
2. **The opening vista** (`View/Camp/OpeningVista.cs`, `VistaPlaces.cs`, `VistaTerrain.cs`; built by `HubInstaller` after the places). The game opens on a composed, elevated night view (camera about 70 m up, 34 degrees down, field of view 34) in which all six places are visible at once:
   - temple (blue dome, forecourt, keeper, builders) → the god board;
   - the council's **lookout tower** (new hub prop at the council site, the same shape the council builds when it sits, with a hide roof, pennant and fire basket; hidden while the council's own scene stands) with **five villagers waiting** at its foot → the council;
   - the dance circle (fire, drums, dancers) → rhythm;
   - the healer's lodge → the plague;
   - **Mira's expedition camp at the foot of a new rocky hill with a lit cave mouth** (two torches, a blue gleam inside), turned towards the lookout → the crystal expedition;
   - the fenced practice ring with sparring villagers → the duel.
   **Uneven ground:** the cave hill (about 10 m, a walk blocker) and 14 grassy knolls and rock outcrops on the far side of the camp (walk blockers, trees cleared round them). The hub's walkable ground itself stays level (the walk lattice and every hub test depend on it). Each place has a warm night light so it reads from the lookout.
   **The bearing and pitch** are chosen once when the vista is built, for a 16:9 frame (so phone, tablet and desktop see the same camp; the distance and centring are then fitted to the real screen): every 15 degrees at pitch 34 and 40, the fit distance where all six places' bounds sit inside the frame's margins; poses where the island's hills hide a place or the camera would sit in a hillside are skipped; the score favours the smallest place's size on screen (so the far places read) and the spread between place centres (clean regions). Seed 7 picks bearing 210 at pitch 34. The fit is redone whenever the screen size changes.
3. **Screen regions instead of buttons.** No rectangles, labels or outlines are drawn. Every frame each place's screen box is its projected bounds (a ring at the ground plus the top, from the vista camera). `RegionAt(point)`: inside one box → that place; inside several → the nearest centre among them; outside every box → the nearest box. So the whole screen is divided, at any ratio, much like the owner's 9.png (left = ring, centre-left = Mira's hill, centre = tower, bottom centre = temple, top = lodge, right = dance circle at seed 7).
   - **First tap** on a region: the place lights up (a pulsing ring and a brighter light) and the shared `LedgerCard` names the activity ("The temple / Offer a lamb at the altar and the whole island becomes the gods' board. / Play the gods' board game at the temple?"). The vista places are `IWorldAction`s on the WU-0 machine (tap guard, double-tap guard, timeout), not registered with `WorldActions`, so the walking view never sees them.
   - **Second tap** (same region, or the card, or E): the camera glides down (1.4 s arc) to the player standing at the place, facing it, and **the place's entry runs at once**: the offering (the board rises), the steward's bench (the council opens, or the steward's honest "no dispute is called yet" card), Sol's seat (the dance starts; after spire 1 "Follow Sol to the spires"), the healer's conversation, Mira's conversation by the cave mouth ("Set out for the crystals / Practice duel / Maybe later"), the ring (Mira walks over, the duel starts). **Why automatic:** the vista's first tap already was the preview with the honest card, and the second tap the confirm; a third tap at the place would make every entry three taps. Where an entry cannot run, its own refusal card shows on arrival.
   - **Back to the vista:** Esc (a phone's back key) in the walking view, or a tap on the sky. Esc in the vista opens the menu. A one-time notice after the first arrival says so.
   - **Roaming:** from any place, tap the ground to walk (unchanged); W / up arrow in the vista steps down into the walking view where the player stands. A / D in the vista light the places left to right; E or Enter goes.
   - A drag in the vista swings the view up to 35 degrees round the camp (never into a hillside); the regions follow.
   - An encounter or the god board that takes the camera while the vista is up (a duel whose opponent arrives late) makes the vista give way at once.
4. **No step pads in the hub.** `TouchStrips.StripsMove` is true only underground: on the surface the strips are not moves, a press in the bottom band is an ordinary tap or drag, and the footprint stones are never drawn on the surface (any setting). The rhythm drums' band and the dungeon's footprint stones are unchanged. The banish charm in the hand is hidden while the vista is up.
5. **Free look everywhere.** Why the owner could not turn while building the woodcutter's hut: while the player follows a leader (`PlayerWalker.Follow`), the walker turns itself to face the leader every frame it stands still, and a drag either stopped the lead or was undone at once; a locked walker ignored turns. Now a drag while the walker is locked or led turns only the head (`PlayerWalker.LookOffset`, any angle, the lead goes on); when the walker is free and the finger is up, the look folds into the body (it turns to the nearest sixth from where it was looking). When free, a drag turns in sixths as before (WU-1b).
6. **First moments.** In a god board game with the vista, the prince does not walk to the player or lead to Mira: once the vista is up he welcomes ("Welcome home. I'm the prince. Everything we do is down there: tap a place to see it, tap again to go.") and each place lights for a beat with one line ("Our temple. Offer a lamb and play the gods' board.", "The lookout tower...", "Sol's drums...", "The healer's lodge...", "Mira's camp by the cave...", "The practice ring..."). The first tap in the vista ends the welcome. No build pitches in god board games (unchanged since G1-7; checked by a test on the default start). The old god board guide still runs where the vista is off (tests, AutoPlay).

## Tests (editor 6000.3.28f1, PlayMode)

New `OpeningVistaTests` **5 / 5**:
- `NoArgumentStart_IsTheGodBoardHub_OpeningOnTheVista_WithAWelcome` (plain scene load: no dev start, GodBoard ruleset, six places, walker held, camera up, the welcome points from the temple on, no first scene, no pitch);
- `AllSixPlaces_AreOnScreen_AndEachRegionIsItsOwn_AtThreeRatios` (844x390, 1024x768, 1920x1080 via `OpeningVista.ScreenOverride`: every centre inside the simulated notch safe area, every box on screen, each centre is its own region, every one of 800 sample points has a region and every place a fair share);
- `EachRegion_FirstTapPreviews_SecondTapGoesThere_AndBackToTheVista` (all six through `PointerInput.TapAt`: preview, lit, still up; confirm, glide, arrival at the place, the entry ran (board opened, dance, council or honest card, bubbles, Mira coming); back to the vista; then a late duel takes the camera from the vista);
- `NoStepPads_InTheHub_TheBandTaps_AndStripsDoNotMove`;
- `Drag_TurnsTheView_WhileThePrinceLeads_AndWhileLocked`.

Regression (each fixture run separately after the changes): `HubTests` 19/19, `WorldUiTests` 6/6, `WorldTourTests` 2/2, `GodBoardTests` 10/10, `PrinceTests` 13/13, `DanceTests` 17/17, `ExpeditionTests` 14/14, `InteriorTests` 16/16, `PlayerTests` 16/16, `GodBoardLedgeTests` 21/21, `CouncilTests` 40/41 then the one failure re-run alone 1/1 green (`Wu4_GroundRings_..._PhoneSize` checks a place is in the picture at the *game view's* width; my captures had left the game view at 1024x768; at 1920x1080 it passes). After the last tweaks (bottom margin, deferred hint) `OpeningVistaTests` 5/5, `HubTests` 19/19, `WorldTourTests` 2/2 again.
Updated tests: `PlayerTests.TouchStrips_MapToTheFourMoves` (strips do not move on the surface), `InteriorTests.FootprintStones_...` (never on the surface), `InteriorTests.Band_And_Keys_...` (the shut-way step uses the forward move), `GodBoardLedgeTests.FootprintStones_AreHidden_...` (none on the surface even with Always).
Not run: the full PlayMode suite, EditMode, `dotnet test` (no sim change).

## Packaged build (`BuildWindows.Run`, 102 MB, 0 errors; 844x390 windowed, `-novsync`)

| Run | Result | Seconds |
|---|---|---|
| `world_ui_tour` | OK | 38 |
| `board_first_round` | OK | 21 |
| `first_scene -timescale 2` (no `-start`: a packaged AutoPlay run keeps the legacy game) | OK | 78 |
| plain launch, **no arguments** | log: `[World] new game: god board hub`, `[Vista] open over the camp: bearing 210, pitch 34, 6 places, screen 844x390 (start)`, `[Boot] playable ... prince True, villagers 24`; no exceptions | 25 (closed by hand) |

The plain launch opened at 844x390 windowed only because Unity remembered the window size of the smoke runs.

## Captures (local only, `Evidence/vista/`, not committed)

- The vista: `final_vista_1920.png`, `final_vista_1024.png` (simulated notch), `final_vista_844.png` (simulated notch); the region maps (one colour per place, dots at the centres) `final_regions_1920/1024/844.png`.
- The welcome at 844x390: `welcome1_844n.png` (greeting and the temple), `welcome2_844n.png` (the tower lit), `welcome3_844n.png` (the lodge).
- Previews: `final_preview_duel_1920.png`, `final_preview_cave_844.png`, `pv_temple_1920.png`.
- Arrivals: `final_arrive_duel_1920.png` (at the ring, Mira on her way), `final_arrive_cave_844.png` (Mira's conversation by the cave mouth), `final_arrive_dance_844.png` (the dance begins), `arrive_HUB-COUNCIL_1920.png` (up the tower), `arrive_HUB-HEALER_1920.png`, `arrive_temple_1920.png` / `later_temple_1920.png` (the board rising and up).
- Tuning sets (older code): `p38_*.png` (eight bearings), `pose_*.png`.

## What the owner should look at

1. Start the exe with no arguments: does every place read from the vista at your screen size? Is anything too small on the phone shape?
2. Tap around: does each part of the screen pick the place you expected (9.png's areas)? Is the glide down quick enough?
3. The automatic start on arrival (second tap = go and begin): right, or would you rather confirm once more at the place?
4. The uneven ground is scenery round a level camp (hill and cave, knolls). Is that enough, or should the camp itself sit on terraces (a bigger change to the walk lattice)?

## Open problems

- The hub's walkable ground is still level; the hill and knolls are walk blockers you cannot climb.
- The vista's bearing suits seed 7 well; other seeds choose their own by the same score, captured only for seed 7.
- Phones: at 844x390 the far places are about 45 px tall; the regions are large, but the art is small.
- The windowed smoke runs make Unity remember an 844x390 window (registry `HKCU\Software\Joe Salmon\WorldMVP`); not cleared (no registry writes). Delete that key for a full-screen double-click.
- `Temp/owner_shots/` was emptied when the Unity editor opened the worktree (Unity clears `Temp/`); 12.webp could not be viewed.
- Another agent (`wt-tut`) adds per-game tutorials: none of their director hooks were touched here; the prince's opening lines are in `PrinceLines` (`Vista*`).
