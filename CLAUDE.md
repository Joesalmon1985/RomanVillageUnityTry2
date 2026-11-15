# WorldMVP: shared village world (Unity 6000.3.28f1)

One Unity game that joins four of the owner's games. On `main`: a first-person village with a Tab God View, a Ward duel with Mira, a 174 BPM rhythm party in the warehouse, and one connected road, hex, draft and military economy. On `refactor/prehistoric`: the same games recast as a prehistoric island (see the status below).

**Status: on branch `refactor/prehistoric` the prehistoric refactor is finished and packaged (14 Nov 2026, tag `refactor-done`): a small island with dice on a clock, growing stock, camps with plots, main roads, the prince and spells, Dungeon Master movement, a dance circle, Mira's duel, a night start with a day-night cycle, save and load. `Docs/REFACTOR_SPEC.md` is binding on that branch and overrides `CONTRACTS.md`, `ECONOMY_DESIGN.md` and the older API notes where they differ. `main` still holds the older MVP (`189466f`); merging the refactor into it is the owner's decision. Nobody has played either by hand yet. What comes next is in `Docs/NEXT_START_HERE.md`; do not start it or change the plan without the owner. On `historic/main` (tag `earth-slice-1`, 15 Nov) the crystal campaign's Earth route is joined and packaged (interiors, Mira's expedition, the campaign duel); reach it with F1 "Campaign" or `-start dev.historic -campaign authored_v1` (`Docs/PLAY_GUIDE.md`).**

**Paused on 15 Nov 2026: read `Docs/START_HERE_NEXT_SESSION.md` first. It gives the branches, what is finished, what is unfinished, the next steps in order, what is waiting on the owner, and the owner's rules for sub-agents (Sonnet where possible, no Fable agents).**

Before doing anything, read `Docs/HANDOFF.md`. It gives the state, reading order, machine paths, Unity commands, source file map and rules.

- Requirements: `Docs/PRD.md`
- Architecture, milestones, cut order: `Docs/BUILD_PLAN.md`
- Gates, test vectors, owner play script: `Docs/TEST_PLAN.md`
- Source repos, revisions, licences: `Docs/SOURCES.md`

Rules that always apply:

- This repo is public. Never commit `config.json`, `LocalData/`, `Evidence/screenshots/`, `*.package` or anything derived from Sims 2. Check `git status` before each commit.
- No third-party art or audio from the source repos. Original names and data; reference games are inspiration for mechanics only.
- `Assets/World/Game` never mutates simulation state: it reads snapshots, sends commands and animates events.
- Simulation code and its EditMode tests come before views in every milestone.
- Report which tests and playthroughs actually ran; say plainly what did not.
