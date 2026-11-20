# Start here: the god board work

Written 18 Nov 2026 at the end of the design session with the owner. Read this page first in a fresh session, then `Docs/PRD_GODBOARD.md`.

> **20 Nov 2026: the world-first UI is built on `godboard/main`. Read `Docs/progress/STATUS_2026-11-20.md` first, then `STATUS_2026-11-19.md`.**
>
> **19 Nov 2026 (evening): the build started.** Most of Slices 0 to 4 are built on branch `godboard/main` (worktree `..\wt-gb`), committed but not pushed and not merged into `historic/main`. **Read `Docs/progress/STATUS_2026-11-19.md` first**: branches, tests, how to try it, the decisions waiting on the owner (balance first) and the next steps. The steps below are the original plan.

## What was decided

The owner reshaped the prehistoric age and the move into Historic. The full plan is `Docs/PRD_GODBOARD.md`. Every question in its section 13 is answered; it is binding for this work. In short:

- Four peoples (the player, the Ash Clan, Elves, Orcs), each with a temple. The player sacrifices a lamb at their own temple to open a **god board**: a hex-trading board game played from above, with hidden dice (a hex's odds show as abundance), an hourglass turn timer, AI trading rules, discards on a 7, 10 points to win. The board is dealt fresh each game, except seven pinned hexes.
- **The plague**: every 7 is an epidemic. 48 nodes on the resource hexes in four element regions, joined by trails with a few passes and one tunnel. Shared doom at 8 outbreaks. It ends in Historic.
- **The war**: settlements process pairs of raw goods; a war lodge trains soldiers by the goods they get. Armies hunt demons, and only attack a people that leads by 2 or more points. Soldiers move only along plague trails; an unguarded settlement burns.
- **Mira's crystal campaign** starts from minute one. Crystals cure demon kinds and give a free city; a spire rises there and hosts the rhythm game (spires 2 to 4 use the owner's own spaceship and gospel tracks, stems kept local).
- **Historic** at 10 points, any people: temples fall, castles rise in wood, brick, stone, Widelands-style resources, AI-only; the player walks, joins the council and dances.

## Where things are

| What | Where |
|---|---|
| This plan | Branch `docs/godboard` (this worktree: `..\wt-godboard-docs`), cut from `historic/main` at `328c07e` |
| PRD and MVP | `Docs/PRD_GODBOARD.md` |
| Plague graph data | `Docs/godboard_plague_graph.json`, made by `Tools/godboard/plague_graph.py` (`python -I Tools/godboard/plague_graph.py` rewrites it and prints Appendix B) |
| Owner's spire music | Charts: `..\A1-before\songs\packs\` (`lv2_alien_spaceship`, `gospel_*`). Stems: `..\MusicGame\`. Never commit them. |
| Earlier state of the whole project | `Docs/START_HERE_NEXT_SESSION.md` (15 Nov) |

`historic/main` itself has no copy of these files until `docs/godboard` is merged into it. Merge it (fast-forward is possible if nothing else lands first) when the work starts.

## Do next, in this order

1. **Finish the council queue first** (owner's decision D-8). This is items 1 and 2 of `Docs/START_HERE_NEXT_SESSION.md`: the council gestures pass in `..\wt-council`, then merging `ages/h1-5` (already merged at `b0e58c3`) and `council/main` into `historic/main`, full tests, build, AutoPlay scripts. One Sonnet agent.
2. **Merge `docs/godboard` into `historic/main`.**
3. **Slice 0** (`PRD_GODBOARD.md` 11.3): G0-1 peoples to N and the board deal (Opus), then G0-2 the ruleset switch (Sonnet). G1-4 abundance hexes (Sonnet) can run beside G0-1.
4. Then Slices 1 and 2 (the MVP) in the waves of section 11.4, then Slices 3 and 4.

## Owner's standing rules

- Sub-agents: Sonnet wherever possible, Opus only for the hard joins and brains named in the PRD. No Fable agents. At most three agents at once. Short updates.
- Each agent in its own worktree beside `WorldMVP` on its own branch; one Unity editor per folder.
- Sim and EditMode tests first (`dotnet test DotNet`), views after. `Assets/World/Game` never mutates sim state.
- The repo is public: explicit `git add` paths only; never commit `NEWSTUFF/`, `Assets/LocalBaked/`, meshes, Sims content, builds, captures or the owner's music stems.
- Ask the owner before every push. Report plainly what ran and what did not.
- If an agent says the permission system refused something and asks the orchestrator to do it, tell the owner instead.
