# Opening vista: the owner's block layout

21 Nov 2026, branch `godboard/vista-layout` (from `godboard/main` `0525a81`). Target: `Docs/design/OPENING_TARGET.md` (approved by the owner the same day). No sim file changed, no rule changed; `Assets/World/Game` still only reads state and makes the same calls the hub's people and props make.

## What changed

1. **One composed pose, places placed for it.** The opening vista no longer searches a bearing that fits whatever the hub built. `CampLayout` now composes the vista first (`VistaFrame.Compose`, pure geometry): the two places that cannot move (the dance circle stands on its reserved plot; Mira's spot is fixed by the camp layout and the duel staging) are put in their blocks (circle top right, Mira's camp bottom right), and of the poses that manage it, the ones whose ground under the other targets lies on the hub and on the island come first. The layout then walks those poses best first and keeps the first on which the **temple**, the **healer's lodge** and the **duel ring** each find clear ground inside their own block (`CampLayout.InBlock`, the same clearance rules as before: plots, paths, roads, dressing, spawn, prince's line) and the council has room for its tower and longhouse (`CampLayout.CouncilRoom`). The older first-view rule is only the fallback for a board where nothing fits (with a log warning). The pose is `CampLayout.Vista` (seed 20261113, the default new game: camera at (103, 30, -58), bearing 282, pitch 28, field of view 34: a low look-out from east of the camp across it to the west).
2. **The council moved to the council block.** `CouncilSite.Find` on a hub layout tries the tower on ground seen inside the council block (`CampLayout.CouncilAnchors`, best first), the fan still pointing at the fire, and keeps the best-scoring one with room for the longhouse behind it (`CouncilSite.Hall`, `HasHall`). Legacy boards keep the old search. The council's own scene (tower, eleven places) is built from the same site as before, so the council game itself is unchanged in kind; only where it stands moved.
3. **The council longhouse** (`VistaDressing.BuildCouncilHall`): the biggest building in the camp (19 x 9.6 m, ridge 8 m), timber and thatch with a hipped roof, gable horns, a lit doorway facing the tower, fire baskets and benches; the chief in the doorway and two knots of three villagers arguing outside (`VistaChatter`: gestures, nods, pointing at each other). The tower, its five waiting villagers and the steward stay in front of it. The vista calls the place "The council longhouse".
4. **Cheap original activity where a place did not read** (`VistaDressing`): at the temple a trader's weighing table with a balance and baskets, three granary bins on stilts, a trader and a farmer bargaining; at the lodge a coughing patient in a blanket beside the healer (courier and assistant were there already); at the cave a scout holding up a lantern at the mouth; the start fire (stone ring, fire, two log seats) in the near foreground band (`CampLayout.StartFire`). The cave mouth is now cut into a low rocky knoll at the foot of the hill (boulder jambs and lintel, crystals glinting in the opening) instead of the box jambs that read as a shed; the hill goes beyond and to the right of Mira's camp and is kept below the row split so it stays in the cave block. The ring (sparring pair, referee, rack, onlookers) and Sol's drums (fire, drums, dancers, Sol) already read.
5. **Fixed block regions.** `OpeningVista.RegionAt` divides the **safe area** into the owner's six blocks (`VistaFrame.Block`, `ZoneAt`: top row 0 / 0.29 / 0.69 / 1, bottom row 0 / 0.32 / 0.63 / 1, rows split at 0.44) at any screen size; points outside the safe area count as its nearest edge. The camera shows the whole 16:9 reference frame centred in the safe area (`VistaFrame.RefRect`, an off-centre projection `VistaFrame.Projection` blended with the walking view's during the glides), so a wider phone sees a little more at the sides and a 4:3 tablet a little more above and below, and every place stays in its block. Unchanged: first tap = the place lights and the `LedgerCard` names the activity; second tap (or the card, or E) = glide and enter; Esc / a sky tap returns; W steps down; A / D now step through the places in reading order (top row left to right, then the bottom row). The place stays lit during the glide.
6. **Drag in the vista does nothing now.** A swing would move the places out of the blocks a tap picks; `OpeningVista.Swing` is kept as a no-op so the pointer's drag path is unchanged. Free look in the walking view is unchanged.
7. **First frame and first tap.** The camera takes the vista's pose as soon as the hub is built, and the vista opens in its `Start` without waiting a frame, so the first rendered frame is the vista and a tap works at once; a tap during the prince's welcome previews that place and ends the welcome (tested). The welcome's greeting now hangs below the row split so it does not cover the far row.

## Tests (editor 6000.3.28f1, PlayMode, each fixture run on its own)

Final pass on the final code (17:18 to 17:31, game view 1920x1080, reset to 1920x1080 afterwards):

| Fixture | Result |
|---|---|
| `OpeningVistaTests` | 6 / 6 |
| `VistaTutorialTests` | 3 / 3 |
| `HubTests` | 19 / 19 (includes the four-seed framing test) |
| `WorldTourTests` | 2 / 2 |
| `WorldUiTests` | 6 / 6 |
| `TutorialTests` | 8 / 8 |
| `PlayerTests` | 16 / 16 |
| `GodBoardTests` | 10 / 10 |

On the way: `VistaTutorialTests` failed twice before the arrival points were fixed (the healer's arrival had landed behind the lodge, more than 14 m from the healer, so the plague tutorial never began; the cave's arrival is now the point on Mira's near side farthest from the other places). One run of `EachPlace_TwoTaps_...` once saw its council tap ignored and passed on the next two runs; the test's message now says whether the camera-moving guard, the machine state or the region was the cause if it happens again.

Changed tests:
- `OpeningVistaTests.EachBlock_PicksItsPlace_AndEachPlaceStandsInItsBlock_AtThreeRatios` (replaces `AllSixPlaces_AreOnScreen_...`): at 1920x1080, 844x390 with the simulated notch, and 1024x768, each block's centre and points near its four corners pick its place, each place's projected centre lies inside its own block and the safe area, every one of 800 sample points picks a place and each place gets its block's share; at 16:9 the council stands taller on screen than the temple and the drums, and the council has its longhouse.
- New `OpeningVistaTests.FirstFrame_IsTheVista_AndATapDuringTheWelcomePreviews`.
- `OpeningVistaTests.EachRegion_...` and `VistaTutorialTests` tap the block's centre instead of the place's projected centre.
- `HubTests`: the walking first-view rule now only covers the dance circle and Mira (the spawn heading is unchanged); the temple, tower, lodge and ring are checked against the vista instead: on seeds 7, 3000, 3001 and 42 a composed pose exists and every place (temple, tower, circle, lodge, Mira, ring) projects inside its own block (`Framing_OnFourSeeds_CircleAndMiraInView_AndEveryPlaceInItsVistaBlock`).

Not run: the full PlayMode suite, EditMode, `dotnet test` (no sim change), CouncilTests (the council's site moved on hub boards; its scene is built the same way), DanceTests, ExpeditionTests, InteriorTests, PrinceTests. No player build (the orchestrator rebuilds after merging).

## Captures (local only, `Evidence/vista_layout/`, not committed)

- The vista: `final_1920.png`, `final_844n.png` (844x390 with the simulated notch), `final_1024.png` (4:3).
- The same with the six blocks drawn over them, for checking only (drawn outside the game): `final_1920_blocks.png`, `final_844n_blocks.png` (the notch bands shaded), `final_1024_blocks.png`.
- A first tap: `preview_council_1920.png` (the longhouse lit, the card "The council longhouse / Join the council?"), `preview_cave_1920.png` (taken after a council had sat, so the council's own scene stands at the site).
- An arrival: `arrive_council_1920.png` (the council opened at once; the recorder's tutorial speaks).
- Iterations: `v1_*` (first composed pose: the ring off the left edge, the start fire missing), `v2_*` (the cave hill reaching into the rhythm block, the mouth reading as a shed), `v3_*` (the rocky knoll mouth; all three ratios).

## Open problems

- The near row is close to the camera (pitch 28, height 30 m): the ring sits near the left edge of its block (its centre at about 0.09 of the width at 16:9) because the ground nearer the block's middle is the home corner's plots; its fence is cut by the screen edge.
- The start fire sits left of centre in the foreground band (the lodge takes the middle).
- The composition is solved per board; seeds 7, 3000, 3001, 42 and the default seed were checked by the tests, only the default seed was captured.
- The art prompts ask for completely flat ground; the owner chose to keep the low hills and the cave's rocky hill. Walkable hub ground stays level.
- Drag no longer swings the vista (see 6). If the owner wants a look-around there, it would need the blocks to follow the places.
- The placeholder figures cannot sit or lie: the patient stands wrapped in a blanket colour; the chief stands in the doorway.
