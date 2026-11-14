# Start here: the next work (ages, crystals, characters)

Written 14 Nov 2026 on branch `docs/next-ages`. Read this page first. It is the single entry point for a new session or an orchestrator. The three PRDs hold the detail; this page holds the order, the rules and the open questions.

## 1. What the game is now

WorldMVP started as a Roman village with four games joined into one. The owner then agreed a **prehistoric refactor**: a camp at night. Dice add young stock to hexes, camps have plots, main roads carry goods, the prince asks and the player casts a free spell to start each build. The player is a viewpoint who walks in 60 degree turns and 5 m steps. The rhythm game is a dance circle. Mira's Ward duel is unchanged. The refactor is specified in `Docs/REFACTOR_SPEC.md` and is not finished: the integration branch does not yet compile for sure, and two packages are unmerged.

**`Docs/RESUME.md` covers finishing the refactor.** Do that first. Everything below starts from a finished refactor (git tag `refactor-done`, put on the branch by the last refactor package).

## 2. The four-age plan

One island, four ages, one seeded board and one simulation. **Prehistoric** (built) grows into **Historic**: a Roman-styled village, the prince running the camp on his own, a full original faction tree with soldiers and upgrades, and the crystal campaign led by Mira. Then **Modern**: a harbour town under blockade, a points-and-credits economy and a full RTS building roster, with one mission (break the blockade). Then **Future**: one industrial district with belts, power, turrets and five waves. The ages are joined by two time jumps that carry a small, authored set of things across. The characters pass runs beside all of it: real rigged people in place of capsule placeholders.

## 3. The owner's decisions, in brief

- Finish the refactor first. Then three characters and the Earth route together. Modern after Earth, then Future.
- One island, four ages, built forward from the prehistoric refactor.
- The prince asks and carries out builds. Once the first supply chain delivers, he chooses and builds alone. The player can still advise him.
- Mira leads the crystal expedition. The player follows her, advises on riddles, and casts for her in lethal duels. The duel against Mira stays as optional, safe practice.
- In the campaign an Ash Clan overrun costs a camp and gives the rival time. It does not end the game. In standalone skirmish it stays fatal.
- Each lethal duel loss costs one of Mira's six companions. A lethal loss with none left ends the run.
- The Ash Clan is the opponent and its expedition races Mira. The Lord of Chaos is kept for the last fight.
- The economy grows age by age to full production chains and military. Every building can be upgraded to Historic with goods, then to Modern with credits.
- First crystal slice is Earth: descend into the quarry, find the crystal, or follow the theft trail if the rival got there first. Show a win, a loss that kills one companion, and the restoration. Then the other three routes, then the generator.
- Mira can scout and answer riddles while the player is away. At a lethal fight she waits or withdraws. The player can find her and rejoin.
- Dungeons use the same step movement in small connected interiors, with a clear way back up.
- Modern: original names and looks, a full roster by function. Each faction unlocks it alone at 10 victory points. Building needs delivered goods and credits. Production queues also need physical goods. One complete faction first; the rest is tracked in a coverage table.
- The harbour mission can start once Earth plays end to end, and has its own test start.
- Characters: prove Mira, the prince and one opponent in real gameplay footage before rolling out. Use them from the prehistoric opening in camp clothes, then reuse faces and rigs for Historic. Check the Sims 2 probe; if it fails, carry on and flag it.
- The release target is an offline landscape iPhone. For now, test in a phone-sized Windows window.
- Computer players follow the technique of the Widelands computer player (economy and military) and the OpenRA bots (RTS), written fresh.
- Start-here notes go in `Docs/`; the owner's local material stays out of the repo.

## 4. The limits

| Limit | What it means |
|---|---|
| Original lore, faction and roster | Every name, place, riddle, line, building, unit and data table is ours. Nothing from any other game, even renamed. No names or backstory from the 1990 source game; its archives are never read. |
| AI from technique, not code | Study published designs of the Widelands computer player and the OpenRA modular bot for ideas. Write fresh C# with our data. Both are GPL: no code or tables copied. |
| Nothing from NEWSTUFF | The owner's answers file, the characters ZIP, the Amiga archives, Sims 2 files and comparison footage are never committed, copied or pasted into the repo. |
| Meshes stay local | Character meshes, prefabs, materials and edited `.blend` files stay out of git until the owner confirms in writing that they may be published (`Docs/PROVENANCE_CHARACTERS.md`). Tools, code, clip data and animation-only files may be committed. |
| Public repo | Never commit `config.json`, `LocalData/`, `Evidence/screenshots/`, `*.package` or anything from Sims 2. Check `git status` before each commit. |

## 5. The three PRDs

| PRD | In one line | Reading path |
|---|---|---|
| `Docs/PRD_CRYSTALS.md` | The four-crystal campaign in the Historic age: Mira's expedition, the Ash race, six companions, interiors, data packs, a generator and validator. Three slices: Earth; the other routes, finale and endings; generator. | Owner (20 min): sections 1, 4, 5.1 to 5.6, 7, 11, 12. Builders: 2, 3, 5.7 to 5.12, 6, 8, 9, 10, appendices A to D. |
| `Docs/PRD_AGES.md` | The later ages: handover and Historic growth, the full Valley faction (43 wares, 40 buildings, soldiers, upgrades), points and credits, the Modern roster and harbour mission, the Future district, and both AIs. Slices H1, H2, M1, F1, C1. | Owner (10 min): sections 1 to 4. Then 5 to 9 for the rules, 10 to 12 for the build and proof, 13 to 15 for risks and decisions. Builders: appendix A. |
| `Docs/PRD_CHARACTERS.md` | Real characters: one Blender pipeline, a shared rig and face rig, prehistoric and Historic costumes, authored clips, a duel presentation, and the owner's before and after video. Three slices: first three characters; the rest of the cast and crowd; Historic costumes. | Owner: sections 1, 4, 7.5, 8, 11, 12. Builders: 2, 3, 5, 6, 7, 9, 10. |

Each PRD ends with a "Reconciliation, 14 Nov 2026" section listing the cross-PRD fixes. Where a PRD and this page differ, the PRD wins for detail and this page wins for order.

## 6. Merged schedule

Waves run in the owner's priority order. Packages in one wave can run in parallel when their dependencies are met. A later wave starts when what it depends on is merged or tagged, not when the whole earlier wave ends.

**Naming.** Branch and worktree folder (under `C:\Users\joesa\Documents\Cursor\RhytmDrama\`) follow the lane. Crystals: branch `cry/<id>`, folder `wt-cry-<id>`, merged into `feat/crystals`. Ages: branch `ages/<id>`, folder `wt-<id>`, merged into `ages/h1` (the existing lane, `wt-h1`). Characters: branch `char/wp<N>`, folder `wt-char<N>`, merged into `char/main` (`wt-char`). Ids are lower case (`cry/i0`, `ages/h1-0`, `char/wp6`). `wt-char`, `wt-char2` to `wt-char5` and `wt-h1` already exist: look at what they hold before starting new ones. The branch column below shows only the branch. Tags: `refactor-done`, `ages-H1-0`, `cry-I0`, `cry-I7`, `cry-J0`, `cry-G0`, `ages-H2-0`, `ages-M1-0`, `ages-F1-0`, `char-gate-A10`.

**Needs.** U = Unity editor, B = Blender, ff = ffmpeg, none = neither. Tier is as the PRD set it (`*` = this page's suggestion, because no PRD covers the refactor).

### Wave 0: finish the refactor (priority 1)

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| R-1 | Join and compile the integration branch, finish the join list in `RESUME.md` | fable* | U | `refactor/prehistoric` (WorldMVP) | none |
| R-2 | Finish the WIP on `pre/n` (prince, sawyer, merchant, first scene) and merge it | opus* | U | `pre/n` (wt-n) | finish: none; merge: R-1 |
| R-3 | Finish the WIP on `pre/l` (night, lights) and merge it | opus* | U | `pre/l` (wt-l) | finish: none; merge: R-1 |
| R-4 | Package F-1: save and load, play guide, remove the unused graph, Windows build, packaged runs, give the owner the exe, tag `refactor-done` | fable* | U | `refactor/prehistoric` | R-1, R-2, R-3 |
| WP-1 | Characters: intake and island map (already committed on `char/main`, `0f2e15f`; verify) | sonnet | B | `char/main` | none |
| WP-10 | Sims 2 reference captures, local only, in a worktree with its own `config.json` | sonnet | U | `char/wp10` | none |
| WP-17 | Provenance note with a line for the owner to sign | sonnet | none | `char/wp17` | none |

### Wave 1: after `refactor-done` (Blender work needs no game code and may already be running)

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| H1-0 | Ages interfaces: eras, history, landmarks, ledger skeleton, grades, outer plot ids, save v5 with migration, `SystemOrder`, the `CampaignState` shell. Tag `ages-H1-0` | opus | none | `ages/h1-0` | `refactor-done` |
| WP-2 | Rig template, fit and skin | opus | B | `char/wp2` | WP-1 |
| WP-3 | Face rig | opus | B | `char/wp3` | WP-1 |
| WP-4 | Prehistoric costume | opus | B | `char/wp4` | WP-1 (reads WP-2's skinning) |
| WP-5 | Core clips and walk generator (including `Run`) | opus | B | `char/wp5` | WP-1; WP-2 for real fits |

### Wave 2: after `ages-H1-0`

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| I0 | Crystals interface: campaign types, commands, events, `CampaignApi`, `Lattice.cs`, encounter request, `DuelStageSpec`, installers. Tag `cry-I0` | opus | U (one compile check) | `cry/i0` | `ages-H1-0` |
| H1-1 | Handover, autonomy, advice and explain, campaign overrun, the Historic step | opus | none | `ages/h1-1` | H1-0 |
| H1-2 | Grade 1 upgrades of the 16 types, City to Town, outer plots, cycle-bonus field | sonnet | none | `ages/h1-2` | H1-0 |
| WP-6 | Scripted Unity import, prefab builder, Animator controller | opus | U (B for export test) | `char/wp6` | first outputs of WP-2 and WP-5 |

### Wave 3: after `cry-I0` and H1-1/H1-2 (Unity editors: one per folder; if the owner's machine limits editors, take I7, I5, I6, H1-4 first)

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| I1 | Campaign runtime: custody, rival clock, facts, leads, Mira's brain, attempts, restoration | opus | none | `cry/i1` | I0 |
| I2 | Format, loader, validator v1, authored Earth pack, skeleton pack, fixtures | opus | none | `cry/i2` | I0 |
| I3 | Opponent profiles and measured win rates | sonnet | none | `cry/i3` | I0 |
| I5 | Interiors in the game: walk grid regions, stairs, interior map, "Lead me out", `IMapLayer` | opus | U | `cry/i5` | I0 |
| I6 | Expedition director: Mira's loop, companions, journal, map layer, site and tower views | fable | U | `cry/i6` | I0 (then I1, I2 to run for real) |
| I7 | Campaign duel: Mira as caster, lethal, friendly and practice modes, surrender, phone panel. Tag `cry-I7` | opus | U | `cry/i7` | I0, I3 |
| H1-3 | Ages proof section H | sonnet | none | `ages/h1-3` | H1-1, H1-2 |
| H1-4 | Prince handover scene, invitation, advice replies | opus | U | `ages/h1-4` | H1-0; H1-1 to finish |
| H1-5 | Grade visuals for the 16 types and the town | sonnet | U | `ages/h1-5` | H1-0; H1-2 to finish |
| WP-7 | Runtime actor and factory (`CastActor`, registry, face and gaze) | opus | U | `char/wp7` | WP-6 |
| WP-8 | Prince first-scene binding | sonnet | U | `char/wp8` | WP-7, `refactor-done` |

### Wave 4: first characters and Earth, proven in the real game

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| I4 | Campaign proof (sections A to D, F) | opus | none | `cry/i4` | I1, I2 |
| I8 | Integration: checkpoint, developer start, F1 tool, AutoPlay `earth_first`, `earth_stolen`, phone captures | fable | U (integration folder) | `cry/i8` | I1 to I7 |
| WP-9 | Duel director and stage, `ward_champion` preset | opus | U | `char/wp9` | WP-7, WP-5, `cry-I7` |
| WP-11 | Captures, before and after video, score sheet | fable | U, ff | `char/wp11` | WP-9 |

**Owner gates after Wave 4:** G2 (watch the first character video) and G3 (play Earth), see section 8.

### Wave 5: crystals Slice 2 and the character roll-out (after G2 and G3); H2 sim in the background

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| J0 | Crystals interface additions: finale, endings, scout, reports, boons, overrun hook, site registry. Tag `cry-J0` | opus | U (one compile check) | `cry/j0` | Slice 1 merged |
| J1 | Runtime part two: full rival plan, scouting, finale binding, endings, boons | opus | none | `cry/j1` | J0 |
| J2a | Fire pack | opus | none | `cry/j2a` | J0 |
| J2b | Water pack | opus | none | `cry/j2b` | J0 |
| J2c | Air pack | opus | none | `cry/j2c` | J0 |
| J2d | Finale and endings pack | opus | none | `cry/j2d` | J0 |
| J3 | Proof part two (E, G) and remaining fixtures | opus | none | `cry/j3` | J1, J2a to J2d |
| J4 | Fire and Water set pieces | opus | U | `cry/j4` | J0 |
| J5 | Air set pieces | opus | U | `cry/j5` | J0 |
| J6 | Finale, endings, island change | fable | U | `cry/j6` | J0 |
| J7 | Slice 2 integration: full-campaign AutoPlay, overrun link, phone pass | fable | U (integration folder) | `cry/j7` | J1 to J6 |
| WP-12 | Role clips (tools, carrying, drums, dance, warriors) | opus | B | `char/wp12` | WP-5, G2 |
| WP-13 | Demon rig and clips | opus | B | `char/wp13` | WP-1 |
| WP-14 | Roll-out to all humans | sonnet | B | `char/wp14` | G2, WP-4 |
| H2-0 | Ages interfaces for the full faction, military and fog. Tag `ages-H2-0` | opus | none | `ages/h2-0` | H1 merged |
| H2-1 | Economy tree: wares, buildings, recipes, veins, tools, credits income | opus | none | `ages/h2-1` | H2-0 |
| H2-2 | Military: soldiers, training, garrisons, attacks, capture, fog | opus | none | `ages/h2-2` | H2-0 |
| H2-3 | Points ledger | sonnet | none | `ages/h2-3` | H2-0 |
| H2-4 | Faction AI (needs-based, scheduler, personalities) | fable | none | `ages/h2-4` | H2-0; H2-1, H2-2 to finish |
| H2-5 | Proof sections A, F, V, D and the coverage report | opus | none | `ages/h2-5` | H2-1 to H2-4 |

H2 sim packages share no files with the crystals packages. Start them as soon as H1 has merged if there is room. Crystals packages win ties.

### Wave 6: crystals Slice 3, Historic views, rest of characters' Slice 2

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| G0 | Generator interface. Tag `cry-G0` | opus | none | `cry/g0` | Slice 2 merged |
| G1 | Generator and the first template | fable | none | `cry/g1` | G0 |
| G2 | Validator hardening (speed, sentences, all fixtures) | opus | none | `cry/g2` | G0 |
| G3 | Command-line tool and outline writer | sonnet | none | `cry/g3` | G0 |
| G4 | "A different telling" and developer start by seed | opus | U | `cry/g4` | G0 |
| G5 | Two generated examples, proof section H, play both | fable | U (integration folder) | `cry/g5` | G1 to G4 |
| H2-6 | Historic views: 24 buildings by grade, soldiers, fog on the map | opus | U | `ages/h2-6` | H2-0; H2-1, H2-2 to finish; `cry-I5` merged |
| H2-7 | Military advice, map taps, HUD points and credits | sonnet | U | `ages/h2-7` | H2-0, H1-4, `cry-I5` merged |
| WP-15 | Crowd tier in `LandFigure` | opus | U | `char/wp15` | WP-14 |
| WP-16 | Camp role binding (Sol, dancers, camp figures) | sonnet | U | `char/wp16` | WP-14, WP-12 |

### Wave 7: Modern M1 (after G3 was passed and H2 is merged); characters' Historic costumes

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| M1-0 | Modern interfaces: lattice, units, structures, orders, power, scenario shapes. Tag `ages-M1-0` | opus | none | `ages/m1-0` | H2 merged |
| M1-1 | Units: pathing, orders, combat, unit fog, engineer | opus | none | `ages/m1-1` | M1-0 |
| M1-2 | Base: unlock, mixed costs, power, queues, harvester, grade 2 | opus | none | `ages/m1-2` | M1-0 |
| M1-3 | RTS AI modules | fable | none | `ages/m1-3` | M1-0; M1-1, M1-2 to finish |
| M1-4 | Historic to Modern transfer, harbour mission, starts | opus | none | `ages/m1-4` | M1-0 |
| M1-5 | Modern proof | opus | none | `ages/m1-5` | M1-1 to M1-4 |
| M1-6 | RTS views | opus | U | `ages/m1-6` | M1-0 |
| M1-7 | The harbour in play: commander, quartermaster, map orders, AutoPlay | opus | U | `ages/m1-7` | M1-0, M1-4 |
| WP-18 | Historic costumes and the age-switch hook | opus | B, U | `char/wp18` | G2, `ages-H1-0` |
| WP-19 | Companions, wizard, smith, ferryman faces | opus | B | `char/wp19` | WP-3, G5 gate |

### Wave 8: Future F1

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| F1-0 | Future interfaces. Tag `ages-F1-0` | opus | none | `ages/f1-0` | M1 merged |
| F1-1 | Industry sim: belts, power, turrets, assembler | opus | none | `ages/f1-1` | F1-0 |
| F1-2 | Future mission: transition, district, waves, core | opus | none | `ages/f1-2` | F1-0 |
| F1-3 | Future proof | sonnet | none | `ages/f1-3` | F1-1, F1-2 |
| F1-4 | Industry views and play | opus | U | `ages/f1-4` | F1-0 |

### Wave 9: campaign proof C1

| Id | Purpose | Tier | Needs | Branch | Depends on |
|---|---|---|---|---|---|
| C1-1 | Chain proof, save matrix, seeded variations | fable | none | `ages/c1-1` | F1 merged; crystal ending (or its fixture, reported as such) |
| C1-2 | AutoPlay `chain_short`, packaged runs, Windows build | sonnet | U | `ages/c1-2` | C1-1 |
| C1-3 | Refresh this page, coverage and progress notes | sonnet | none | `ages/c1-3` | C1-1, C1-2 |
| WP-20 | Final character quality pass on all slices | fable | U, ff | `char/wp20` | WP-18, WP-19 |

## 7. Rules for running agents

- **One Unity editor per folder.** Every package that needs Unity gets its own git worktree under `C:\Users\joesa\Documents\Cursor\RhytmDrama\`. Close or reuse editors left open (WorldMVP, wt-n, wt-l were left open on 13 Nov).
- **Sim and tests first.** Write simulation code and its EditMode tests before any view. Run `dotnet test DotNet` green before any merge.
- **Headless proof before views.** Each slice has a generated proof. Views come after it.
- **One owner per file.** Each package edits only the files its PRD lists. Three exceptions: registration files (`Sim/Core/WorldSimFactory.cs`, `Boot/WorldBootstrap.cs`, `Game/UI/Hud.cs`) take one-line additions from any package, and a conflict there is fixed by keeping both lines; handover files change owner at a named tag (the Ward encounter files at `cry-I7`, `Lattice.cs` at `cry-I0`, the `CampaignState` shell at `ages-H1-0`); `Docs/PROGRESS.md` and `Docs/PLAY_GUIDE.md` are append-only, one dated section per package. `Game` never writes simulation state.
- **Commit with explicit paths** (`git add <paths>`, never `git add -A`). Check `git status` for NEWSTUFF, `config.json`, `LocalData/`, Sims files and character meshes first.
- **Never push** unless the owner asks.
- **Report honestly.** Every hand-off says which tests and playthroughs actually ran, and what did not.
- **Tags and starts.** Game-code packages start from `refactor-done` or a later lane tag. Blender-only characters packages may start from `docs/next-ages`.
- **Models.** Use the tier the PRD gave. Where none is given, use the strongest available.

## 8. Owner gates and open decisions

### Gates: where the owner must look before work continues

| Gate | After | What the owner does | Blocks |
|---|---|---|---|
| G0 | R-4 | Plays the refactor exe: dance readability, the prince, spells, night look. Nothing has been played by hand yet. | Nothing hard; faults are fixed on the refactor lane and merged forward. |
| G1 | H1 merged | Plays Prehistoric to "Show me": the handover, the prince building alone, "What are you doing?", advice. | Crystals Slice 1 on the real handover (the standalone start works meanwhile). |
| G2 | WP-11 | Watches the before and after video of Mira, the prince and the champion. Answers: are faces and weight shift good enough to roll out? | WP-12, WP-14 and everything after. |
| G3 | I8 | Plays Earth end to end (the script in `PRD_CRYSTALS` 9.3), win and loss, with placeholder companions. | Crystals Slice 2, and Modern M1. |
| G4 | any time | Reads and signs the provenance note, or refuses. | Committing any mesh, prefab or edited `.blend`. |
| G5 | start of character Slice 3 | Approves the lineup image of the six companions and the wizard. | WP-19. |
| G6 | H2 merged | Watches the prince grow a full economy, train soldiers and defend. | M1. |
| G7 | J7 | Plays the whole campaign to both endings. | Crystals Slice 3. |
| G8 | G5 | Reads two generated outlines and plays them. | Closing the crystals work. |
| G9 | M1-7 | Plays the harbour mission, both approaches. | F1. |
| G10 | F1-4 | Plays the core mission. | C1. |
| G11 | C1-2 | Plays the packaged chain. | Release work. |

### Open decisions (work proceeds on the default unless the owner says otherwise)

**Story and names**

| Decision | Default |
|---|---|
| Names: Oskerry, the Unweaver, the four crystals and towers, six companions, Hesk Varr, keepers, Orrin | As written in `PRD_CRYSTALS` section 4. They are data. |
| Names of Commander Ilse Teague, Quartermaster Tobin Hale, Engineer Rhea Okonkwo-Dahl, the Valley people, and every ware, building and unit | Working names in `PRD_AGES`. |
| Is "the Lord of Chaos" kept as a title? | Yes, what the peoples call the Unweaver. |
| Is the Road Hero model the Ash champion and later the rival leader? | Yes. The owner names him. |
| Same people across the jumps | Mira recurs; the prince is succeeded by his heir; the engineer is a descendant; the prince and companions appear in records and the memorial. |

**Campaign rules**

| Decision | Default |
|---|---|
| Can reloading undo a companion's death? | No. One checkpoint at every commit and resolution. |
| Game closed during a lethal duel | The duel restarts with a new seed, no penalty. |
| How much may Mira do alone? | Everything except lethal duels and restorations. |
| Keeper and final difficulty | A first-timer wins about 7 in 10 keeper duels and a little over half the finales. Tuned after the first playthrough. |
| Campaign length and race pace | 30 to 50 minutes. The Ash expedition reaches its first crystal at 8 minutes, then needs about 7 per crystal. |
| Does terminal defeat end the whole game? | Yes. "Begin again" starts a new campaign. |
| Does restoring a crystal cost the faction anything? | No. The games link by time, overruns and demons only. |
| May your people wipe out the Ash Clan during the expedition? | No. An Ash camp can fall (the expedition stops 90 s), but the Ash home stands. |
| Generated campaigns in normal play? | Yes, as "A different telling" after the first ending. |
| **New:** do crystals pay victory points? | Yes: 1 to the player per restored crystal, 1 to the Ash Clan per crystal first set on its altar. |
| **New:** after the restored ending, is the step to Modern offered at once? | Yes, as "Onward, thirty winters on"; "Begin again" is the other reply. |
| A later optional Mira duel outside the campaign | Non-lethal, no new permanent deaths. |

**Ages and economy**

| Decision | Default |
|---|---|
| Can a faction open the Modern roster in Historic if it reaches 10 points? | Yes, points only. Historic AI personalities do not build roster structures until Modern. |
| Does the handover happen in the 20-minute skirmish too? | Yes (autonomy only, no age step). |
| Is the step to Historic automatic or by invitation? | By invitation: the prince asks, the player says "Show me", asked again every 3 minutes. |
| Do real attacks replace the abstract overrun from Historic on? | Yes. Strength stays as a HUD and AI measure. |
| Credits before Modern | The Trading post earns credits from Historic on. |
| What carries across the jumps | Ownership of settlements and roads, half the stock, buildings with grades. Soldiers and Modern structures do not. |
| Future scope | Defend one district. More sectors are finished-game scope. |
| The Ash Clan's later names | Ash Clan, Ash Directorate (Modern), Ash engines (Future). |

**Characters and publication**

| Decision | Default |
|---|---|
| May the meshes and derivatives be published? | No until the owner says so. |
| Who is the player's body in the practice duel? | Juno. |
| Keep each character's identity colour in prehistoric dress? | Yes, as dyed hide and fur. |
| Face style | Keep the blocky lineup style, differentiate by parameters. |
| Quality gate at the first video | Accept a stylised look, with the simpler face fallback after two failed iterations. |
| Six companions and the wizard | New heads and hair on the existing bodies; lineup approval at the start of Slice 3. |
| **New:** the four altar keepers wear the champion's body in other palettes until new faces exist | Yes. |
| Phone test windows | 844 x 390 and 932 x 430. |

## 9. Unresolved cross-PRD points (from the reconciliation)

Everything the three PRDs disagreed on is fixed in the PRDs, and each fix is in that PRD's changelog. Nothing is left that blocks work. These points need the owner or an owner-level choice, and each has a default already applied:

1. **Crystal points** (default yes). Added to both crystals and ages; the owner has not seen it.
2. **"Onward" after the restored ending** (default yes). Both PRDs wanted to end the campaign with their own bubble.
3. **Keepers share the champion's body** until Slice 3 faces exist (default yes). Four keepers plus the leader will look related.
4. **Handover in skirmish** (`PRD_AGES` 14.2). It lets the prince run the camp in the 20-minute game; the owner's answer speaks of the campaign. Default yes.
5. **Engineering detail, no owner needed:** after a restored ending, the island stays in campaign mode (overrun not fatal). After terminal defeat the game is over. `PRD_AGES` H1-1 should state this in its tests.
6. **Not checked in code:** a few Unity-side names the PRDs assume (`CampLayout.Clearings`, hooks in `Game/UI/Hud.cs`, `Npc` option replacement) were read from docs, not opened in the editor. The first Unity package that touches each should confirm it and log a change.

## 10. Unity base, 14 Nov

Branch `historic/main`, tag `historic-base` = `refactor-done` + the sim tag `sim-H1-cry-S1` (Historic step, ledger, grades, towns, handover and autonomy, save v5; the crystal campaign's format, packs, runtime and Ward profiles) + the Unity half of crystals I0 + the shells below. Unity packages of Wave 3 and the character packages branch from `historic-base`, each in its own worktree, and edit only their own files. A shell is a compiling file with the names of the PRD appendices and empty bodies marked "I0 STUB" or "Shell"; its owner replaces the bodies. The three registration files (`Sim/Core/WorldSimFactory.cs`, `Boot/WorldBootstrap.cs`, `Game/UI/Hud.cs`) still take one-line additions from anyone.

| Package | Owns (only these) | Entry points already in the base |
|---|---|---|
| I5 interiors | `Game/Interiors/**`; `Game/UI/MapScreen.cs` and `Game/UI/MapLayers.cs`; the region edits in `Game/Player/WalkGrid.cs` and `PlayerWalker.cs`; `Tests/PlayMode/InteriorTests.cs`; `Docs/INTERIORS_UI.md` | `InteriorsInstaller.Install(boot)` (called by `WorldBootstrap` after the prince); `Interiors` (`Current`, `Changed`, `Enter`, `Exit`, `LeadOut`, `Build`, `RebuildFromState`, internal `SetCurrent`) and `InteriorSpace` (`Origin`, `ToWorld`, `TryLocal`) in `Game/Interiors/Interiors.cs`; `MapScreen.Mode`, `MapScreen.AddLayer/RemoveLayer/Layers`, `IMapLayer`, `MapMode` (the map draws layers before the player's arrow and offers them taps first). The `WalkGrid` additions of appendix B (`AddOpenRegion`, `RemoveOpenRegions`, `SetCellBlocked`) are not stubbed: I5 adds them. |
| I6 expedition | `Game/Expedition/**` (from `cry-J0` `Sites/**` go to J4, J5, J6); `Tests/PlayMode/ExpeditionTests.cs`; `Docs/EXPEDITION_UI.md` | `ExpeditionInstaller.Install(boot)` (after interiors, before the F1 tools); `ExpeditionDirector` shell (`Instance`, `Mira`, `Companions`, `Phase`, `Said`, `RestoreScene`). Mira's dialogue is replaced at run time through `Npc.Options` / `Npc.DialogueBuilder` (both public). Site clearings: `LandViews.AddClearing(x, z, radius)` / `ClearExtraClearings()` (static, applied at the next tick), instead of `CampLayout.Clearings`, which is per instance and cannot take additions. Map content through `MapScreen.AddLayer`. The duel is started with `EncounterDirectory.TryStart(EncounterDirectory.WardCampaign, npc, request)` or through `CampaignDuel.Start`. |
| I7 campaign duel | `Game/Encounters/WardDuelEncounter.cs`, `WardDuelStage.cs`, `EncounterInstaller.cs` (these three pass to WP-9 at `cry-I7`), `EncounterUi.cs`, `WardBot.cs`, `CampaignDuel.cs`; `Tests/PlayMode/CampaignDuelTests.cs` | `CampaignDuel` shell (`Create` is the factory registered under `ward_campaign`, today the practice duel; `Start(EncounterCommitted)` returns false; `Resolved` and internal `RaiseResolved`). `EncounterContext.Request` and `WardDuelRequest` (`Game/Encounters/EncounterRequest.cs`, with `From(EncounterCommitted, EncounterKind)`), `DuelStageSpec` (`Game/Encounters/DuelStageSpec.cs`, with `VolunteerActorId`, `VolunteerPos`). These two files and the `Request` field stay I0's: additive changes only, logged in `CAMPAIGN_API.md`. |
| I8 integration | `Game/Boot/SaveSystem.cs`, new `Boot/CampaignStart.cs`, `Boot/CampaignScripts.cs`, `Game/UI/God/CampaignDevTool.cs`, `Game/UI/GuideText.cs`, the tests named in `PRD_CRYSTALS` 8.1 | Nothing new: `SaveSystem` already writes save version 5 and reads version 4 (as a Prehistoric skirmish). `DevStart` (below) builds `dev.historic`; `CampaignStart` then calls `StartCampaign` on it. |
| H1-4 prince scene | `Game/Prince/**` except `PrinceActorBinding.cs` (WP-8) and, once H2-7 starts, `Advice/`; `Tests/PlayMode/HandoverPlayTests.cs` | `PrinceInstaller.Install` and `PrinceDirector` are already H1-4's. The base only taught them the new proposal kinds: `KindOf` gives "upgrade" and "town" (and "collector" for any later unknown kind), `Args` fills them, and `PrinceLines` has two placeholder banks `prince/body/upgrade` and `prince/body/town`. H1-4 replaces those lines and adds the handover scene, the invitation (re-asked every `ErasTuning.InviteRepeatTicks`) and the advice replies; it listens for `PrinceHandover`, `ActionAnnounced`, `ChapterReady`. |
| H1-5 grade visuals | `Game/View/Land/Grades/**`; `Tests/PlayMode/GradeViewTests.cs` | `GradeViewsInstaller.Install(boot)` in `Game/View/Land/Grades/GradeViewsInstaller.cs`, called right after `LandViewsInstaller` (before the lighting). Hook without editing the land views: `LandViews.Synced`, `LandViews.Buildings`, `LandViews.Settlements`, read `Building.Grade`, `Upgrading`, `Settlement.IsTown`. The ground of a town's outer plots and their paths is already cleared by `LandViews`. |
| WP-6 import | `Assets/Editor/Characters/**` (folder does not exist yet; editor code there compiles into the default editor assembly and sees `World.Game`), `Assets/Characters/Animation/**`, Animator controllers, `lib/export_unity.py` | No shell needed: nothing shared. |
| WP-7 actor and factory | `Game/Actors/**` except `CostumeSwitch.cs` (WP-18) and `Modern/**` (M1-7); `Tests/PlayMode/ActorTests.cs` | `ActorFactory` is still inside `Game/Actors/PlaceholderActor.cs`, which WP-7 owns and moves out; every character is created through it, so no other file changes. |

Also in the base, for everyone:

- **Developer starts:** `Game/Boot/DevStart.cs` (`Begin(id, seed)`, `Current`, `TakePending`, F1 tool "Starts", hotkey G; command line `-start dev.historic [-seed n]`). It rebuilds the scene on `ScenarioStarts.Create`. A `dev.historic` world has no campaign views yet (I5, I6). Owner: the ages lane, additive.
- **Lattice:** `Tests/PlayMode/HistoricBaseTests.cs` pins sim `Lattice` = game `WalkLattice` (cells, centres, rounding, distance). From `cry-I0`, `Lattice.cs` belongs to `PRD_AGES` M1-0 and M1-1.
- **Long tests:** the three proofs carry a `Timeout` attribute (Unity stops a test at 180 s by default): `ProofReport` 20 min, `HandoverProof` 20 min, `CampaignProof` 60 min. The two heavy ones (`HandoverProof`, `CampaignProof`, together about 9 minutes under .NET and longer in Unity) are also `[Category("HeavyProof")]`: a routine Unity EditMode run may leave them out, `dotnet test DotNet` always runs them. New long EditMode tests do the same.
- **The base also merged** `sim-H1-cry-S1-tuned` (the campaign race tuning, 450 dotnet tests).
