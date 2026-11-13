# WorldMVP: shared village world (Unity 6000.3.28f1)

One Unity game that joins four of the owner's games: a first-person village with a Tab God View, a Ward duel with Mira, a 174 BPM rhythm party in the warehouse, and one connected road, hex, draft and military economy.

**Status: MVP built and packaged (13 Nov 2026): `Builds/Windows/WorldMVP.exe` (rebuild with `AgentScripts/BuildWindows.cs`), how to play in `Docs/PLAY_GUIDE.md`, what ran in `Docs/PROGRESS.md`. Shared sim contracts for parallel work: `Docs/CONTRACTS.md`. Do not change the plan without the owner.**

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
