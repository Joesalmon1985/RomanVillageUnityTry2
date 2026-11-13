# Progress

What was built and what actually ran, per milestone. Newest last.

## Milestone 0: skeleton and shared contracts (13 Nov 2026)

Built:
- Assemblies: `World.Sim` (no engine references, Newtonsoft only), `World.Game` (refs World.Sim, Bedsit.Core), `World.Sim.Tests` (EditMode), `World.Game.Tests` (PlayMode).
- Sim core: `Rng` (SplitMix64), `Ware`, partial `WorldState` and `PlayerState`, `Command`/`CommandResult`/`ICommandHandler`, `SimEvent`/`EventBuffer`, `ISimSystem` with `SystemOrder` slots, `WorldSim` (Submit, Step at 10 ticks/s, DrainEvents), `Reasons` (closed list and sentences), `WorldSimFactory` with empty Economy, Strategy and Military module stubs; Ward and Rhythm namespace placeholders.
- Board: `HexCoord`, `Terrain`, `BoardTopology` (radius R, int ids, adjacency, exact pointy-top geometry at 20 m per edge, BFS paths with an edge filter), `Board`, `BoardGenerator.CreateMvpBoard(seed)`.
- Game: `Assets/Scenes/World.unity` (one `WorldBootstrap`), `GameClock` with pause reasons, sun, camera at the home city, 400 m ground plane, `MaterialFactory` with two own shaders kept in builds through Resources materials, `UiStyle`. World.unity is the only build scene; `AgentScripts/BuildWindows.cs` targets `Builds/Windows/WorldMVP.exe`.
- Fast harness: `DotNet/WorldMVP.sln` runs the EditMode test sources under .NET 10.
- Contracts for parallel agents: `Docs/CONTRACTS.md`.

Ran:
- `dotnet test DotNet`: 43 passed, 0 failed.
- Unity recompile: completed, 0 compile errors; `unity command console status`: 0 errors (1 warning: Unity's Input Manager deprecation notice).
- `unity command test run --mode editor`: 43 passed, 0 failed (same sources as the .NET run).
- `unity command test run --mode playmode --async-tests`: 1 passed (`WorldBootTests.WorldScene_Boots_AndSimTicks`: scene loads, one bootstrap, sim ticks, pause reasons hold the clock).
- Play Mode capture `Evidence/screenshots/m0_boot.png` (git-ignored), inspected: green ground to the horizon under the default sky, debug overlay showing the tick advancing.

Not run: no Windows player build (not part of M0); nothing beyond the M0 gate.
- Ward duel rules (M2, feat/ward): scorer, Mira rival, time-stepped session, WardProgress in WorldState; 71 EditMode tests pass under `dotnet test DotNet`; API in `Docs/WARD_API.md`. Not run in Unity.
