# Polish P3: people animation follow-ups (branch godboard/polish-people-2)

Written 20 Nov 2026, on top of P1 (`Docs/progress/POLISH_P1.md`). View work only: `Assets/World/Game` reads the sim and never writes it; no rule, tuning or sim file changed. Everything committed works with the placeholder bodies.

## What changed

**Elbows** (`PlaceholderActor`): each arm is an upper arm on the shoulder pivot and a forearm on an elbow pivot (`ElbowL/R`, hand and arm band on the forearm). `Forearm(right)` gives the pivot for hand props; `ElbowBend(right)` is the test probe. The elbow is relaxed (10 degrees) when standing, bends as the arm swings forward when walking and is pumped near 75 when running; every pose and one-shot sets it: carry in the arms (58, holding the load at the forearms), shoulder carry (115, the hand steadies the load), strike (cocked 95 behind the head, straight through the blow), block (100, forearms up), gesture, talk beats, wave and beckon (the forearm waves), hammer (cocked at the top, straight on the blow), clap, surprise, cheer, fidgets (scratching the head, hands on hips, rubbing the hands), the council poses (folded arms, hand to ear or chin) and pointing (straight). Seated people rest the forearms. `Offer` keeps the arm straight, so the council's handed note (parented to `Arm(true)` at the hand, P2b's code) still sits in the hand.

**Knees on mesh figures** (`MeshFigure`): the leg mesh is split at half the hip height into a thigh and a shin on a knee pivot (`MeshFigure.Split`, cached per source mesh; exact for the box legs both callers use, so `CollectWalkers` and `HistoricDirector` did not change their meshes). The knee bends while its leg swings forward (36 degrees walking, 72 running) and a worker crouches a little. Probe `KneeBend`.

**Historic carriers walk** (`HistoricDirector`, `MeshFigure.MaxGaitSpeed`): the sim's trip timing stays authoritative (the carrier is where `Progress / Ticks` puts it; only the 10 Hz steps are smoothed). A trip is a hex step of about 104 m in 7 s (30 + 40 ticks per hex), so the ground speed is 10 to 25 m/s, which no gait can show. The legs are therefore capped at a brisk walk (`CarrierGaitCap` 2.2 figure units per second, under `Gait.RunFrom` 3.6): the stride and cadence are a walk while the root covers the trip's ground. Routes are no longer straight: each trip follows a gentle curve (quadratic, bowed 14 percent of its length, 3 to 24 m) to the side with more room from the buildings between its ends (the ends' own buildings excluded; open country bows by job id so streams spread to both sides), plus the job's lane. Woodcutters and foresters get the same cap. Probes `CarrierGaitSpeed`, `CarrierRouteBulge`. Not done: routing along the board's roads (the road graph is not used by the view; a curved route was the fallback the brief allowed).

**Soldiers die** (`SoldierView`, shader `World/LitVertexColor`): stagger back a step with the arms thrown out (0 to 0.55 s), the knees give, fall onto the face or the back by id (accelerating, with a small bounce, to 1.15 s), lie still (to 2.3 s), then dissolve in blocks to 3.4 s; the body never shrinks or sinks. The dissolve is a `WORLD_DISSOLVE` local keyword on `World/LitVertexColor` (blocky noise in each part's own space, a dark ashen edge, `clip`); only the dying soldier's parts switch to the keyword material (a clone of `LandLook.Lit`), with `_Dissolve` set through a property block, so terrain and everything else draw as before. The shader now has `addshadow` so a dissolving body's shadow dissolves too.

**The healer's lodge works** (`View/Camp/HealersLodge.cs`): new placeholder one-shots `grind` (both hands on the pestle in small circles), `tend` (stooped, smoothing the bedding) and `fetch` (reach up to the rack on the toes, take a bundle down to the chest). The healer walks to the mortar and grinds (four strokes), walks to the cot and tends it twice, and back. The assistant sorts the baskets by the door (tend and nod), walks to the herb rack, fetches a bundle (a small herb bundle prop in her hand), carries it to the mortar table, sets it down and goes back. Both pause while the player is greeting them or the healer's dialogue is open, and the healer stops while the runner brings news. Asked "How bad is it?" the healer stops, turns to the map table and points at it for about 3 s, then gestures and goes back to work ("Is my charm ready?" turns her to the charm peg). `HealerWork`, `AssistantWork`, `HealerShowed`, `Bundle` for tests.

**Rigged bodies** (`RiggedActor.FallbackClips`): the keys the rigged actor did not know now map to clips of the PRD set, first one present wins, else nothing (false, as before): strike = CastRelease, Talk_Emphasis, Nod; block = Block, Surprise; gesture = Talk_Emphasis, Greet; nod = Nod; clapHigh = Victory, Talk_Emphasis (was unmapped since package N); grind = Nod, Talk_Emphasis; tend = PointGround, Nod; fetch = Idle_Look, Nod. A test checks every `ActorKeys` key plays on the placeholder and has rigged clips or a fallback.

**Duel ring framing**: the ring was not hidden by its own fence: the player's war lodge (`WarSites.LodgeView`, a 16 m long house) was placed on top of the duel ring. `WarViews.PlaceNear` now keeps every war building off the hub's places (`HubInstaller.Layout.Places`, radius plus footprint plus 1 m). `PeopleCaptures` gained `ringview DIST HEIGHT [DEG]` (a spectator's spot square to the sparring line, above the fence), `soldier` / `soldierdie`, `ask` (the healer shows the table), `ringnear` (what stands on the ring) and more `info`. `collect_side` is now taken at 5 figure units to the side, 1.3 up (it was taken from inside the figure's own back).

## Tests

New in `PeopleAnimationTests` (PlayMode, now 11): elbows bend in the swing, an arms carry, a strike (cocked, then straight), a block and a gesture; mesh figures have knees (thigh and shin halves) that bend in the stride and straighten standing; a carrier moving 15 m/s shows a walking gait under the run threshold with a walking stride; a soldier's death staggers, falls flat, lies on the ground, does not fade while lying, never changes the body's scale and ends dissolved with the dissolve keyword; every one-shot key plays on the placeholder and has rigged clips. New in `HubTests`: the healer grinds and tends, the assistant fetches from the racks and carries a bundle, and asked, the healer faces the map table and then goes back to work.

PlayMode, one fixture at a time (async), on the final code: `PeopleAnimationTests` 11/11, `HubTests` 19/19, `WarViewTests` 5/5, `HistoricViewTests` 6/6, `PlagueViewTests` 9/9, `GodBoardVillagerTests` 7/7, `WorldUiTests` 6/6, `GodBoardTests` 10/10. consoleErrors 0 from game code after every compile. Not run: the full PlayMode suite, EditMode (no sim file changed), `ActorTests` with local art.

## Frame time (packaged, 844 x 390, `-autoplay phone_frametime`, dev.godboard seed 7, 60 s, vSync off, desktop)

Both builds made in this worktree with `AgentScripts/BuildWindows.cs`: Base = `godboard/main` at `4581c93` (checked out detached for the build, then back), P3 = this branch. Run alternately, Base first. Logs `Builds/logs/p3ab_*.log` (local).

| Build | Runs (avg ms) | Median | Worst frame |
|---|---|---|---|
| Base `4581c93` | 2.44, 2.97, 2.74 | 2.74 | 19.6 to 19.8 ms |
| P3 | 2.39, 2.61, 2.25 | 2.39 | 17.8 to 20.7 ms |

Run to run noise is about +-0.3 ms (other editors were running for other agents), as large as the difference; P3 is not slower (two more transforms per placeholder, two per mesh figure; the dissolve keyword costs only on a dying soldier).

## Captures (local, not committed) `Evidence/polish/`

844 x 390 with the simulated notch: `walk_0..7.png` + `walk_sheet.png` (villagers walking, elbows bending; one runner pumping), `carrier_0..7.png` + `carrier_sheet.png` (a Historic carrier walking with a load, onto a road), `death_0..9.png` + `death_sheet.png` (a soldier staggers, falls, lies, dissolves, gone; 0.36 s apart), `healer_0..7.png` + `healer_sheet.png` (healer at the cot, the assistant at the herb rack, the healer walking to the mortar), `grind_0..5.png` + `grind_sheet.png` (grinding), `healer_ask_844.png` (pointing at the map table), `ring_844.png`, `ring_844_0..5.png` + `ring_sheet.png` (sparring framed from outside the fence), `collect_side_0..3.png` + `collect_side_sheet.png`. 1920 x 1080: `walk_1920.png`, `carrier_1920.png`, `death_stagger_1920.png`, `death_1920.png`, `healer_1920.png` (grinding, the assistant sorting behind), `ring_1920.png`.

## Needs checking with local art (cannot be seen here)

- The rigged fallbacks above: that CastRelease reads as a staff blow, Block as a guard, Nod for grinding and PointGround for tending look acceptable, and that the queued cast clips are not left half played when a strike interrupts.
- The lodge loop with a rigged healer: `FaceTowards` and `PointAt` go through `IExpressiveActor`; the herb bundle falls back to a fixed spot on the assistant's root when she is rigged (no `Forearm`), so it should move to the hand socket there.
- The council's handed note on a rigged body is untouched (P2b).
