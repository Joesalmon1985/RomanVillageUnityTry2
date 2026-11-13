# Build plan for the day run

Companion to `PRD.md` (what and why), `TEST_PLAN.md` (how it is checked) and `SOURCES.md` (where rules come from).
This file is the working brief for the long build run: architecture, order of work, gates, and what to cut first.

## 1. Fixed technical decisions

| Area | Decision | Reason |
|---|---|---|
| Engine | Unity 6000.3.28f1, built-in render pipeline, legacy Input Manager, Windows keyboard and mouse | Inherited from the seed, already compiles with 0 errors; Windows build module is installed |
| Project | `RhytmDrama\WorldMVP`, own git repo, one startup scene `Assets/Scenes/World.unity` | Owner's choice |
| Scene content | Built in code at startup from data by a single `WorldBootstrap` object; no hand-authored scene hierarchy | Reviewable as text, rebuildable, and the pattern the seed already uses |
| Simulation | New pure C# assembly with no `UnityEngine` reference; integer and fixed-step only; own seeded RNG | Testable in EditMode in seconds; deterministic saves and replays |
| UI | IMGUI (`OnGUI`) with one shared style helper, scaled to screen height | No assets or layout files needed; fastest to make legible. Recorded as debt to replace after the MVP |
| Materials | One helper that creates materials from an always-included shader | Default primitive material is stripped from player builds and renders magenta (seen in RomanVillageUnity) |
| Characters | `ICharacterActor` with `PlaceholderActor` (capsule body, head, coloured tunic, bob and arm swing) as default; `SimCharacterActor` used only when `config.json` resolves a Sims 2 install and a launch flag asks for it | Owner's choice: placeholders by default, Sims optional and local-only |
| Audio | Rhythm stems scheduled with `AudioSource.PlayScheduled` against `AudioSettings.dspTime`; fallback drums and bass synthesised into `AudioClip`s at startup | Sample-accurate sync; no third-party audio |
| Save | One JSON file under `Application.persistentDataPath` via Newtonsoft (already a package) | Sim state is plain data |

## 2. Assemblies and folders

```
Assets/
  World/
    Sim/                 World.Sim.asmdef  (noEngineReferences)
      Core/              Rng, Ids, FixedClock, Command, Event, WorldState, WorldSim (tick + command gate)
      Board/             HexCoord, VertexId, EdgeId, BoardTopology, BoardGenerator, Terrain
      Economy/           Wares, Stores, Recipes, Buildings, Construction, Roads, Carriers, Jobs, Diagnostics
      Strategy/          Rounds, ProductionDraw, Batches, Cities, Market, Rival, Demons, Hero, Draft, Cards, Wonder, Score
      Military/          Power, Barracks, Squads, Orders, Combat, Outpost
      Encounters/        WardDuel (feedback, rival, session), RhythmPhrase (chart, judge, blocks, mix, route)
      Save/              SaveModel, SaveCodec
      Data/              static default data tables (also exported to JSON for tuning)
    Game/                World.Game.asmdef (references World.Sim, Bedsit.Core)
      Boot/              WorldBootstrap, GameClock (drives sim ticks, pause reasons)
      View/              TerrainBuilder, BuildingViews, RoadViews, CarrierViews, SquadViews, DemonViews, EventAnimator
      Player/            FirstPersonAvatar, GodCamera, ViewSwitcher, Picker, Interactor
      Actors/            PlaceholderActor, ActorFactory, SimsActorAdapter (compiled, off by default)
      Encounters/        EncounterHost, WardDuelScene, RhythmPartyScene, StemPlayer, SynthAudio
      UI/                Hud, Dialogue, InspectPanel, GodPanels (build, roads, priority, military, market, draft, round), Diagnostics, Guide
    Tests/
      EditMode/          World.Sim.Tests.asmdef
      PlayMode/          World.Game.Tests.asmdef
  StreamingAssets/world/ tuning JSON overrides (duel, rhythm, economy, cards, map seed)
AgentScripts/            builders and probes run with `unity command script run` (outside Assets)
Docs/                    PRD, BUILD_PLAN, TEST_PLAN, SOURCES, PLAY_GUIDE (written in milestone 5)
```

Rule: `World.Game` never writes to `WorldState`. It reads snapshots, sends `Command`s, and animates `Event`s.

## 3. Simulation model

**Board.** Pointy-top axial hexes, radius 2 (19 hexes, 54 vertices, 72 edges). A vertex is a *flag*: it can hold one of a city, a building, an outpost, or nothing. An edge can hold a road. World scale: 20 m per edge (map about 170 m across; crossing on foot takes about 35 s at 5 m/s). Terrain per hex: forest (timber), hills (stone), fields (grain), mountain (ore), meadow (none). Each resource hex carries a production number 1 to 4. **Superseded 13 Nov 2026 (ECONOMY_DESIGN section 10, Map row):** radius 4 (61 hexes), 60 m hex edges split into three 20 m road segments; cities on hex corners, buildings on any flag (corners and the two intermediate points per hex edge), territory 80 m; Pasture terrain added. See CONTRACTS (Board) and MAP_MIGRATION.

**Clocks.** `GameClock` advances the sim at 10 ticks per second while no pause reason is set. Pause reasons are a set: `Encounter`, `Menu`, `Draft`. Encounters own their clocks (duel: unscaled time; rhythm: DSP time).

**Commands.** All player, NPC-sign and rival actions go through `WorldSim.Submit(Command)`, which returns accepted or a reason code and a player-facing sentence. Commands apply at the next tick boundary. Initial set: `PlaceRoad`, `RemoveRoad`, `PlaceBuilding`, `CancelSite`, `SetPriority`, `ToggleBuilding`, `FoundCity`, `Trade`, `EndRound`, `MoveHero`, `DraftPick`, `BuildCard`, `DiscardCard`, `BuildWonderStage`, `QueueUnit`, `OrderSquad(Defend|Attack)`, `CancelOrder`.

**Tick order.** rounds and batches, harvest workers, production, construction, job creation, carrier assignment and movement, power, barracks queue, squad orders and movement, combat, demons and blocking, score and end checks, diagnostics. Each system appends `Event`s (for example `BatchAuthorised`, `WarePickedUp`, `WareDelivered`, `SiteCompleted`, `JobStalled(reason)`, `UnitTrained`, `SquadEngaged`, `DemonSpawned`, `BreachAdvanced`, `CardBuilt`, `RoundStarted`).

**Logistics invariants (tested).**
- A production draw authorises a batch exactly once per matching hex per round.
- A ware is always in exactly one place: a store, a flag stack, or a carrier. Reserved wares are not spendable.
- Recipe inputs are consumed once per cycle; a delivery credits its destination once, including across save and load.
- Every stalled job, site, building and queue exposes one reason from a closed list: `no_road`, `no_carrier`, `waiting_ware`, `demon_on_route`, `output_full`, `input_missing`, `no_power`, `no_batch`, `invalid_site`, `no_turn_action`, `disabled`.

**Where the four inspirations meet.**
- Hex draw (Catan-like) gates *raw* output: a woodcutter can only fell against an authorised timber batch.
- Roads and carriers (Widelands-like) move every ware, including military supplies and wonder materials. Nothing is paid from a global pool except coin.
- Ore, refinery, power and the barracks queue (RTS-like) consume carried wares; power shortage stalls the queue.
- Draft cards and the wonder (7 Wonders-like) spend carried wares or coin and change later batches, trade rates or training time.
- Demons (Gods-and-Wizards) sit on flags and block deliveries through them until the hero or a squad clears them.

Note for the port: the source hybrid sim deducts unit costs straight from the headquarters store. This project follows the source's stated requirement instead (wares delivered to the barracks flag), because the plan asks for a visible delivery holding up the squad.

## 4. Milestones, gates and time budget

Budget assumes roughly 20 working hours of agent time. Each milestone ends with its gate from `TEST_PLAN.md`, a commit, and a line in `Docs/PROGRESS.md` saying what ran.

| # | Milestone | Budget | Contents | Gate |
|---|---|---|---|---|
| 0 | Skeleton | 1 h | Asmdefs, `WorldBootstrap`, empty scene registered in build settings, test runners green, material and UI helpers | Play shows ground and sky; 1 EditMode and 1 PlayMode test pass |
| 1 | Walkable village | 2.5 h | Board topology, terrain mesh, square, warehouse shell, market, storehouse, first-person avatar, God camera, Tab switch, picker, three NPCs with greetings and accept/leave dialogue | G1 |
| 2 | Opening encounters | 4.5 h | Ward duel sim and scene; rhythm phrase sim, DSP stem player, synthesised fallback, bar and balcony route; pause and return-to-spot | G2 |
| 3 | Road economy | 5 h | Stores, recipes, construction, roads, carriers, jobs, batches, diagnostics, God View build and road tools, quartermaster commission, demonstration route | G3 |
| 4 | Strategy and military | 4.5 h | Rounds, cities, market, rival, demons, hero, draft and cards, wonder, power, barracks, squad orders, outpost, victory and loss | G4 |
| 5 | Save and package | 2 h | Save slot, control guide, `PLAY_GUIDE.md`, Windows build, packaged-build smoke run | G5 |
| - | Buffer | 0.5 h | | |

Owner's priority: milestones 1 to 3 must be solid. If the budget runs out, milestone 4 lands in this order and stops where time ends, with the stop point written in `PROGRESS.md`:

1. Rounds, second city, market trade (strategy core)
2. Demons, hero, breach loss
3. Draft with six cards, wonder, victory by points
4. Power, ore, refinery, barracks queue
5. Squad orders and outpost attack
6. Save in flight, Windows build (do the build even if items 4 and 5 are thin)

Cut list inside milestones 1 to 3, used only if a gate is at risk: decorative props, carrier arm animation, more than one rhythm chart, Light essence unlock ceremony (keep the rule), building-specific meshes (fall back to labelled blocks).

## 5. Working method for the run

- Write `World.Sim` code and its EditMode tests first in every milestone; views come after the rules pass.
- Loop: edit files, `unity command script recompile`, poll `recompile_status`, `unity command test run --mode editor --filter <fixture>`, then `unity command console status` (0 errors required).
- Visual checks: enter Play Mode, drive with a `-selftest` style autoplayer (`AutoPlay` component reading a script of inputs and commands), capture with `unity command capture game_view` into `Evidence/`, and look at the image before claiming a gate.
- Bulk scene or asset generation goes in `AgentScripts/*.cs` run with `unity command script run`.
- Keep the editor ticking: `unity command editor set_auto_tick --enabled true`.
- Never commit: `config.json`, `LocalData/`, `Evidence/screenshots/`, anything Sims-derived. Commit after each gate on `main`.
- Record honestly in `PROGRESS.md` which tests and playthroughs actually ran, and which were not run.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Rhythm timing feels off in the editor (audio latency, frame time) | Schedule on the DSP clock, judge against DSP time sampled at input, expose a calibration offset in the pause menu, test the boundary cases in EditMode with injected times |
| Carrier and job logic hides double-delivery bugs | Conservation test over thousands of ticks with random commands; save and load in the middle |
| IMGUI God View becomes unreadable | One panel at a time, large type, reason sentences instead of codes; screenshot review at each gate |
| Six strategic rounds give too little raw output for 10 planks and 10 stone | Seeded starter draws; batch size is a tuning value; EditMode test plays the commission headless with a scripted player |
| Scope: milestone 4 is as large as 1 to 3 together | Priority order above; every system has a minimal data-driven version first |
| Sims adapter breaks the default build | Adapter is behind a flag and a null check; default path never touches OpenTS2 content |
