# M0 findings: what the Sims 2 animation library gives the village

Date: 12 Nov 2026. Method: `Tools/sims2/anim_inventory.py` reads every `*.package` (DBPF 1.x) index in the owner's Sims 2 Legacy Collection
install, decompresses (RefPack) only the first bytes of each `ANIM` resource (type 0xFB00791E) to recover its name, and writes
`LocalData/m0/anim_inventory.csv` (git-ignored). Read-only on the game folder; about 5 s for the whole install.

**Result: 40 packages, 25,275 uniquely named animations.** Sanity check: all six clip names used by UnityBedsitPOC (pose, idle, wave,
clap, greet-nod, door-open) are found. Prefix key: `a` adult solo, `a2a` adult pair, `a2o` adult with object, `o2a` the object's side,
`c` child, `o` object only. Names below are *candidates by name only*: nothing here has been played in Unity yet (see "Still unverified").

## What each village role needs

| Need | Verdict | Candidate clips |
|---|---|---|
| Walk / run | **Plenty** | `a-male-walk-alt0-normal`, `a-female-walk-alt0-normal`, `...-run-alt0-normal`, `walkShort`/`runShort` variants; mood walks for free personality: `alt4-happy`, `alt5-depressed`, `alt6-angry`, `alt7-skip`, `alt8-sneak`, `alt2-sexy`, `youngAdult`. Generic `a-walk-alt8-lope`, `a-run-alt9-lope`. |
| Carry (log, plank, sapling) | **Good** | Overlay-style poses: `a-generic-standing-carry`, `a2o-brick-carry`, `a2o-marketBasket-carry`, `a2o-hydroponicPlant-carry` (+ `grasp`, `put`), `a2o-trashBag-carry`, `a-reaper-scythe-carryPose` (tool carried). Original engine layers these over walking. |
| Forester | **Excellent** | `a2o-garden-plant`, `-water`, `-fertilize`, `-tend-start/loop1/loop2/stop`, `-harvest-*`; shovel `a2o-diggingForStuff-dig-start/loop/end`; `a2o-leafPile-rake-*`. |
| Woodcutter | **Gap** | No axe-chop. Only `a2o-axeThrowing-*` (throw, gather axes) and a food-chopping loop. Stand-ins to try: `a2o-diggingForStuff-dig-loop`, `a2o-counter-chop-loopNormal/Expert`. Likely best: author 1-2 short chop clips on the Sims skeleton. |
| Sawmill | **Stand-in likely OK** | Bench work: `a2o-counter-chop-loop*` (repetitive cutting), `a2o-toyBench-build-low/med/highSkill-loop`, `a2o-hobby-trainTable-build-loop1..3`. Needs a bench prop at matching height. |
| Hand a ware between two workers | **Excellent** | `a2a-giveGift-giver/givee-neutral/good`, `a2a-giveGiftHeavy-giver-give(-02)/givee-accept(-02)` (heavy fits logs), `a2a-giveGiftLight-*`, `a2a-soc-tossFootball-*` (throw/catch). |
| Meet and interact | **Excellent** | `a2a-soc-shakeHands-leadIn/shaker/shakee`, `a2a-greetManly/HighFive/Bohemian/Technology-*`, `a2a-soc-hug*`, `a2a-soc-gossip-*` (start/loop/finish), `a2a-soc-cheerUp-*`, `a-soc-talkTalk-*`, `a2a-soc-wave-wavee`. |
| Idle variety | **Plenty** | `a-idle-neutral-armsHips-*` (headScratch, rubNeck, footBrush, nailCheck, noseRub), `a-idle-knit-standing-loopLookAround`, etc. |

So the Sims 2 library covers walking, carrying, planting and every social/handoff moment. The only real gap is the axe (and a proper saw).

## M0b: verified in Unity (12 Nov 2026)

Unity 6000.3.28f1 project seeded from UnityBedsitPOC (OpenTS2 + `Bedsit.Core`/`Bedsit.Sims2`); compiles with 0 errors. `Assets/RomanVillage/Probe/ClipProbe.cs`
(scene `Assets/Scenes/ClipProbe.unity`) played **56 candidate clips** on a live, dressed Pleasantview Sim: **56 OK, 0 failed**. Per-clip data is in the
git-ignored `LocalData/m0/clip_probe.csv`; frames in `Evidence/screenshots/` (git-ignored). Candidate list: `Tools/sims2/probe_candidates.txt`.

1. **Expansion packs must be mounted.** The bedsit project only mounts `Base`; 28 of 56 clips (all gardening, digging, axe, bench, `greetManly/HighFive`, `tossFootball`, lope walk) live in
   `EP*/SP*/TSData/Res/3D` and resolved as "missing" until those folders were mounted (`Sims2Runtime.MountDirectory` per `ProductFlags`). The baker must do the same.
2. **Walk and run cycles are authored in place.** `root_trans` translation is ~0 over the cycle, so **we** must move the character; Sims 2's routing engine did that in the original game.
   Gait cycles: male walk 0.86 s, female walk 0.91 s, run 0.66 s. Provisional ground-speed estimates from foot travel (range / cycle): male walk 0.57, female walk 0.68, run 1.14 m/s
   (units = Unity units; heuristic measured while the clip was cross-fading in, so **treat as low and recalibrate with a foot-lock test in M3**). `walkShort` is exactly half the stride of `walk`
   at the same cycle time, so it is a shorter-step variant for tight routes, not a different gait.
3. **Male and female clips both play on the same skeleton** (`a-male-*` on a female Sim): one `auskel` rig serves every adult worker.
4. **Carry clips are static arm poses** (duration 0, 4-14 channels, ~5 deg change on their own): they are overlays that need a prop and an upper-body mask over a walk. Not yet layered over walking.
5. **Sawyer stand-in works:** `a2o-counter-chop-loopNormal/Expert` (2.5 s / 1.5 s) bend the Sim ~100 deg to work at counter height with the root fixed. Needs a bench prop at matching height.
6. **Woodcutter gap confirmed:** `a2o-axeThrowing-throw` (4.4 s) is a two-handed throw wind-up, not a chop. Plan: author 1-2 chop clips on `auskel`, using that two-handed grip as the base.
7. **Forester clips are long and segmentable:** `garden-plant` 15.2 s, `-water` 7.7 s, `-fertilize` 10.7 s; `-tend` and `-harvest` have start / loop (1.9 / 3.5 s) / stop segments; `-tend-start/stop` include a 0.3 m step in/out.
   `diggingForStuff` start 2.3 s / loop 3.7 s / end 3.0 s with 0.1 m root moves.
8. **Paired clips have matched lengths:** `giveGiftHeavy` giver/givee 4.53 s / 4.53 s, `shakeHands` shaker/shakee 3.16 s / 3.16 s, `greetManly` 4.79 s / 4.79 s, `tossFootball` 2.58 s / 2.58 s. Starting both at the same instant keeps them in sync. `shakeHands-leadIn` has a 0.21 m root step.
   `giveGift-*-neutral` is 11.6 s (too long for a handoff); use `giveGiftHeavy` for logs. Relative start offsets between the two Sims are not yet known.

## Still open
- **Baked path.** Everything above ran on the *live* OpenTS2 actor. The baker (not yet ported to this project) must be extended to mount expansions and bake these clips; baked IK is frozen at the original object's position, so props must match.
- **Layered carry over walk** (upper-body mixing mask) and attaching a prop to a hand bone.
- **Pair alignment:** per-pair relative offset/facing between the two Sims.
- **Calibrated walking speed** (foot-lock test) and per-clip stride for walkShort.
- **Gait weights/blending** between walk and run, and turning in place.

## Next
M1 (C# economy sim with parity tests against the GDScript sim) can start in parallel with a short "M0c": port the baker, bake the clips above, and build the locomotion + carry layer with a prop.
