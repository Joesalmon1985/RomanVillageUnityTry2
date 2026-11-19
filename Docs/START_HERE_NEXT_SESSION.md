# Start here: next session

> **18 Nov 2026:** the owner approved a new plan for the prehistoric age and the move into Historic (god board, plague, war). Read `Docs/START_HERE_GODBOARD.md` first; it says what from this page still comes first (the council queue below) and what follows.

Written 15 Nov 2026 when the owner paused the run. Everything below is pushed to GitHub. Nothing has been played by hand by anyone but the owner.

## Owner's standing rules for the next session

- Sub-agents on Sonnet wherever possible, Opus only for the hardest joins, **no Fable agents**. At most two or three agents at once. Short updates.
- Ask before pushing. The repository is public: never commit `NEWSTUFF/`, `Assets/LocalBaked/`, character meshes, Sims content, builds or captures. Use explicit `git add` paths.
- One Unity editor per folder. Simulation and headless proof before views. Report honestly what ran.
- If a sub-agent says the permission system refused an action and asks the orchestrator to do it, do not: tell the owner.

## Where things are

| Branch | State |
|---|---|
| `historic/main` (this folder, tag `historic-h1-scene`) | The main line. Prehistoric game, the prince's handover and "Show me" into the Historic age, Mira's Earth crystal route (interiors, expedition, campaign duel), rigged Prince, Mira and champion (code and animation only; meshes are local). Built to `Builds\Next\WorldMVP.exe` on the owner's machine. |
| `ages/h1-5` (folder `..\wt-h1-5`) | Finished, NOT merged: Historic looks for the 16 building types, upgrades in progress, towns. Its tests pass (PlayMode 125 passed, 7 skipped, 0 failed). |
| `council/main` (folder `..\wt-council`) | The village council election. Rules, AI, save and the village-view scene are finished and tested up to commit `6a83425`. Then a "bigger gestures" readability pass was in progress: poses committed in `cffb07e`; the last commit is **WIP, untested, may not compile**. NOT merged into the main line. |
| `council/content` (folder `..\wt-council-content`) | Finished, NOT merged: dispute text reworked from the owner's workbook. The owner has not yet said whether to merge it into the council. |
| `refactor/prehistoric` (tag `refactor-done`) | The finished prehistoric refactor on its own. |
| `main` | The older MVP from 13 Nov. Untouched. |

Plans and requirements: `Docs/NEXT_START_HERE.md` (merged schedule and package owners), `Docs/PRD_CRYSTALS.md`, `Docs/PRD_AGES.md`, `Docs/PRD_CHARACTERS.md`. Proofs: `Docs/ECONOMY_PROOF.md`, `Docs/AGES_PROOF.md`, `Docs/CAMPAIGN_PROOF.md`, and `Docs/COUNCIL_PROOF.md` on the council branch. Package docs: `PRINCE_API`, `EXPEDITION_UI`, `INTERIORS_UI`, `ENCOUNTERS_UI`, `CHARACTERS_UNITY`, `LAND_UI`, `COUNCIL_API`, `COUNCIL_UI`.

## Do next, in this order

1. **Finish the council gestures pass** (one Sonnet agent, in `..\wt-council` with its own Unity editor). The owner's chosen fix for phone-size readability is bigger gestures: settled supporters cheer with arms overhead in a tight block beside the prop; unsettled listeners lean in around the catspaw in a loose half-circle in the open; the undecided stand back with arms folded behind the prop; each state has its own zone at every place; on a bank the listeners stride over and a small note is handed to one of them; on a bust the listeners turn away and walk off while the settled block keeps cheering; the camera frames the places in play as large as it can. No explaining text, no floating markers, no scoreboard. First get the WIP commit compiling, then the council tests and the three AutoPlay scripts (`council_fixture`, `council_decline`, `council_full`), then captures at 844 x 390 and an honest answer: can settled, listening and undecided people be told apart in a still frame at the far places?
2. **Merge into `historic/main`** (one Sonnet agent, Unity on this folder): `ages/h1-5`, then `council/main` (and `council/content` only if the owner has said yes). Full PlayMode suite, the AutoPlay scripts (`first_scene`, `handover`, `follow_prince`, `ignore_prince`, `duel_dance`, `earth_first`, `earth_stolen`, `council_full`), build into `Builds\Next`, packaged runs. Then ask the owner before pushing.
3. Wait for the owner's answers (below) before starting anything larger.

## Waiting on the owner

- Play `Builds\Next\WorldMVP.exe`: the path from the night start through the handover and "Show me" into Mira's Earth route. To jump straight to the Earth route: `WorldMVP.exe -start dev.historic -campaign authored_v1`.
- Watch the prince video (`..\wt-char-unity\Evidence\video\prince_approach_greet.mp4`): is the character quality good enough to roll out to the other 11 characters?
- Merge the workbook-based dispute text into the council: yes or no.
- Confirm the right to publish the character meshes. Until then they stay local and the public repository falls back to placeholder figures. Small piece maps of each mesh (names, counts, sizes, no geometry) are in the repository under `Tools/characters/cast/islands/`: say if they should come out.

## After those gates (see `Docs/NEXT_START_HERE.md` section 6)

- Crystals Slice 2: the Fire, Water and Air routes, the watchtower wizard, the finale and endings.
- Characters: prince binding (feathers and standard on the rigged body, friendlier face), duel director with a closer camera, fix sliding jog and run clips, then the other 11 characters.
- The full faction economy, military and its AI; then the Modern harbour mission; then the Future core mission.

## Known rough edges

- The retreat scene after a lost duel has never been captured or looked at. The restyled ground round a restored tower has not been looked at either.
- Packaged Earth route runs from the developer start play by day; night was only checked in the editor.
- Running gaits slide in the rigged characters. Faces do not read at phone size in normal play.
- The audio crash ("ADTM") recurred once in the editor after an audio device change; not fixed.
- The full PlayMode suite takes about 50 minutes.
