# AutoPlay smoke scripts for the god board (20 Nov 2026)

Branch `godboard/autoplay`. Code: `Assets/World/Game/Board/GodBoard/GodBoardAutoPlay.cs` (five scripts), `Assets/World/Sim/GodBoard/SmokeSetup.cs` (sim-side set-up helpers, so Game code writes no state), a `ProductionPulses` counter on `GodBoardDirector`. Commands: `Docs/PLAY_GUIDE.md`, "God board smoke runs".

## Packaged results (Builds/Windows, 844x390, -novsync)

| Script | Result | Seconds |
|---|---|---|
| board_first_round | OK | 14 |
| board_trade_discard | OK | 9 |
| plague_epidemic | OK | 12 |
| war_burn | OK | 6 |
| board_to_historic | OK | 10 |
| first_scene | OK | about 180 |

No exceptions in the logs; editor console errors 0.

## Done
All five scripts, documentation, packaged runs.

## Caveats (not done / honest limits)
- Scripts pause the clock and step it by hand, so real-time pacing (AI turn speed, hourglass) is not exercised.
- Prepared through `SmokeSetup`: hand cards (bank trade, keeping 9 cards so a 7 forces a discard), the AI offer card, two free camps for people 1 (points lead, war), two extra soldiers per other people (a lone home garrison never leaves, WA-13). The 7s, the epidemic, the war march and the burning are the game's own.
- `board_first_round` walks to the altar; the others sacrifice with range ignored.
- `war_burn` runs with the table still closed (the war runs anyway).
- Not covered: earth_first, earth_stolen, historic_walk (PRD list), touch input, a real-time unpaused run.

## Next steps
Add an unpaused real-time run; fold the scripts into the integration build checklist.
