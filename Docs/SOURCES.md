# Sources and revisions

Recorded 12 Nov 2026. Each source project stays untouched; the Unity project ports rules and data into C#.
Reference checkouts for comparison live outside this project.

| Source | Revision used | Where to read it locally | State of the local clone |
|---|---|---|---|
| RhythmWarfare | `8bf2c02a` (remote `main`, 8 Nov 2026) | `C:\Users\joesa\Documents\Cursor\MusicGame`, read with `git show 8bf2c02a:<path>` | Checked out on `feature/wizard-duel-lan` at `d9a881bc`; the pinned commit is present. Do not switch its branch. |
| DuelMasterBattle | `1b9513dd` (remote `main`, 16 Oct 2026); code change in `dc466b11` | `C:\Users\joesa\Documents\Cursor\DuelMaster\DuelMasterBattle`, read with `git show 1b9513dd:<path>` | Checked out on `feature/god-world` at `f41828ee`; the pinned commit is present. Do not switch its branch. |
| logistics-rts-sim (private) | `2385672` (28 Jun 2026), as named in the plan. Remote `main` is `42cadf0` (9 Jul 2026). | `..\_sources\logistics-rts-sim` (detached at `2385672`) | The older clone at `BoardGame\wide-rts` is at `7b6670a` (15 Jun) and lacks the hybrid work. Use `_sources`. |
| Gods-and-Wizards | `d22e32c` (remote `main`, 12 Jun 2026) | `..\_sources\gods-and-wizards` (detached at `d22e32c`) | The older clone at `BoardGame\gods-and-wizards` is at `62f15ff` (Run I) and lacks Run J. Use `_sources`. |
| RomanVillageUnity (private) | `9e4348a` plus uncommitted viewer work | `C:\Users\joesa\Documents\Cursor\RomanVillageUnity` | Seed for this project: `Packages/`, `ProjectSettings/`, `Assets/Bedsit`, `Assets/ThirdParty/OpenTS2`, `.gitignore`. |

## Source to feature

| Feature in this project | Source | What is carried forward | What is new here |
|---|---|---|---|
| Ward duel with Mira | DuelMasterBattle `WizardRTSHybrid` story duel; Godot `sim/feedback.gd`, `weave_bot.gd`; `shared_fixtures/` | Fracture / echo / fade scoring, cast limits, recovery time, candidate-filter rival, Light unlock, test vectors | 3D presentation in the village square; C# implementation |
| Opening village pacing | DuelMasterBattle `docs/OPENING_RPG_PLAYER_GUIDE.md` | Encounter order and invitation pattern | Layout on the hex map |
| One sim, two views | DuelMasterBattle `WizardRTSHybrid`; Gods-and-Wizards Run J | Snapshot-and-command separation; the 3D layer never mutates state | First-person and orthographic God View in Unity |
| Rhythm party | RhythmWarfare `content/run/run.json`, `src/run/run_engine.gd`, `src/input/pad_judge.gd` | 174 BPM, four pads, 16-beat blocks, judgement windows, 60% pass credit, mix stages, route choice | Unity DSP-clock scheduling; original fallback chart and audio |
| Roads, carriers, construction, production | logistics-rts-sim `godot_project/sim/economy/`, `sim/data/config/*.json` | Reserve-then-deliver logistics, production checks, construction requests, stall diagnostics, building and recipe data as a starting point | Graph is hex vertices and edges, not hex tiles; re-themed names; fresh C# sim (no tick parity) |
| Ware-fed military | logistics-rts-sim `sim/hybrid/`, `docs/design/04_hybrid_game_requirements.md` | Units paid for in delivered wares; delegated orders | Power plant gating; one infantry type; one rival outpost |
| Hex production, cities, trade | Gods-and-Wizards `godot_game/core/rules/` | Vertex cities, edge roads, distance rule, bank trade, round structure | Production value authorises harvest batches that workers and carriers must fulfil |
| Demons, hero, breach | Gods-and-Wizards `spread_rules.gd`, `contact_resolution_rules.gd` | Infection draw, cap of three, breach counter, clear by contact | Demons on a flag stall deliveries through it |
| Development draft | Gods-and-Wizards `draft_rules.gd`, `development_effect_engine.gd`, `data/development/development_cards_v1.json` | Pick-and-pass draft, three ages, implemented effect types | Small hand, two-player pass, discard for coin, costs retuned for this map |
| Animated characters (optional, local only) | RomanVillageUnity / UnityBedsitPOC `Bedsit.Sims2`, OpenTS2 (MPL-2.0) | `ICharacterActor` seam, `SimCharacterActor`, verified walk and social clips | Placeholder actor as the default implementation |

## Licence and provenance rules

- OpenRA, Widelands, Catan and 7 Wonders are inspirations for mechanics only. No names, data tables, art, audio, maps or exact chains from them. This follows the logistics repo's `.cursor/rules/05-ip-originality.mdc`.
- RhythmWarfare's Red Alert sound effects and OpenRA sprites, and DuelMasterBattle's `WizardRTSHybrid/external`, are third-party: do not import them.
- Commercial music track packs are never imported. Only the owner's own loops (`songs/library/` in RhythmWarfare) or audio synthesised in this project.
- Sims 2 content and anything baked or captured from it stays local and git-ignored (`config.json`, `LocalData/`, `Assets/LocalBaked/`, `Evidence/screenshots/`). A build that shows Sims characters is for the owner's machine only.
- OpenTS2 is MPL-2.0 and stays under `Assets/ThirdParty/OpenTS2` with its licence.
