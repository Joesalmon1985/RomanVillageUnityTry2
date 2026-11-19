# G0-3 proof: all-AI god board games with the plague and the war

Written by `GodBoardJoinProof.AiOnlyGodBoard_PlagueAndWar_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter "FullyQualifiedName~GodBoardJoinProof"`).
Setup: 40 seeds (3000 to 3039), `GameSetup.GodBoard` (dealt board, ports), AI-only table (`TableHumanSeat = -1`, 4 seats on `TableAi`), plague and war wired as in a real game, nobody banishing (no player). War tie rule: `OnlyWhenNotEveryoneTied` (default). Cap 120000 ticks.

## Against PRD 12.2

| Measure | Target | Value |
|---|---|---|
| Rounds to 10 points (games won): min / median / max | 25-40 | 12 / 15 / 17 |
| Won games in the 25-40 band | - | 0 of 9 |
| Island falls (8 outbreaks) | under 5% with an army brain active | 31 of 40 (77.5%) |
| Points lost to burning per game: median / max | median under 2 | 0 / 4 |
| Games where a people fell from 9+ to under 6 more than twice | none | 0 (falls in all games: 0) |
| Share of rounds with any war active | under 30% | 56.5% (329 of 582 rounds) |
| Share of table ticks with any war active | - | 40.9% |
| Trades between peoples obeying TR-4 | 100% | 341 of 341 (100.0%) |

## Other figures (per game, mean)

| Measure | Value |
|---|---|
| Games won / island fell / hit the cap | 9 / 31 / 0 |
| Rounds when the island fell: min / median / max | 8 / 15 / 22 |
| Wins by people 0 / 1 / 2 / 3 | 2 / 2 / 3 / 2 |
| Soldiers trained | 3.6 |
| Demons purged by soldiers | 14.5 |
| Duels / soldiers died | 4.6 / 2.8 |
| Settlements burned (of them cities) / home camps looted | 0.15 (0.15) / 0.05 |
| Outbreaks / epidemics | 6.9 / 5.1 |
| Blocked hex productions | 28.5 |
| Trades between peoples / bank and port | 8.5 / 15.7 |
| Builds: roads / settlements / cities | 9.8 / 4.1 / 7.4 |

## Per seed

| Seed | End | Rounds | Winner | Points | Outbreaks | Rounds at war | Burned (points) | Trained | Purged | Died |
|---|---|---|---|---|---|---|---|---|---|---|
| 3000 | won | 15 | 3 | 7, 5, 8, 10 | 3 | 11 | 0 (0) | 2 | 10 | 6 |
| 3001 | island | 13 | - | 4, 7, 8, 5 | 8 | 6 | 0 (0) | 4 | 17 | 2 |
| 3002 | won | 15 | 0 | 10, 8, 6, 8 | 1 | 11 | 1 (2) | 7 | 10 | 4 |
| 3003 | won | 15 | 1 | 3, 10, 4, 6 | 5 | 11 | 2 (4) | 4 | 10 | 8 |
| 3004 | island | 15 | - | 4, 4, 9, 4 | 8 | 9 | 0 (0) | 1 | 10 | 4 |
| 3005 | island | 8 | - | 2, 2, 2, 2 | 8 | 0 | 0 (0) | 0 | 0 | 0 |
| 3006 | island | 14 | - | 2, 3, 4, 4 | 8 | 6 | 0 (0) | 3 | 9 | 1 |
| 3007 | island | 17 | - | 3, 3, 5, 5 | 8 | 10 | 0 (0) | 4 | 20 | 1 |
| 3008 | island | 15 | - | 3, 2, 3, 2 | 8 | 3 | 0 (0) | 3 | 6 | 0 |
| 3009 | island | 9 | - | 2, 2, 3, 2 | 8 | 0 | 0 (0) | 0 | 0 | 0 |
| 3010 | island | 16 | - | 3, 5, 6, 5 | 8 | 9 | 0 (0) | 5 | 19 | 4 |
| 3011 | island | 21 | - | 4, 6, 8, 7 | 8 | 12 | 1 (2) | 8 | 38 | 7 |
| 3012 | island | 11 | - | 7, 4, 4, 3 | 8 | 7 | 0 (0) | 1 | 8 | 0 |
| 3013 | won | 15 | 3 | 6, 9, 6, 10 | 4 | 10 | 0 (0) | 9 | 24 | 6 |
| 3014 | island | 12 | - | 3, 4, 4, 3 | 8 | 6 | 0 (0) | 0 | 4 | 2 |
| 3015 | island | 15 | - | 4, 4, 5, 4 | 8 | 6 | 1 (2) | 4 | 11 | 4 |
| 3016 | island | 22 | - | 9, 9, 8, 8 | 8 | 14 | 0 (0) | 10 | 51 | 7 |
| 3017 | island | 17 | - | 5, 7, 9, 7 | 8 | 12 | 0 (0) | 5 | 24 | 4 |
| 3018 | island | 10 | - | 2, 3, 5, 3 | 8 | 7 | 0 (0) | 1 | 1 | 3 |
| 3019 | island | 16 | - | 5, 3, 7, 7 | 8 | 10 | 0 (0) | 6 | 14 | 1 |
| 3020 | won | 12 | 2 | 3, 3, 11, 7 | 0 | 8 | 0 (0) | 3 | 7 | 4 |
| 3021 | island | 16 | - | 3, 2, 4, 4 | 8 | 8 | 0 (0) | 0 | 11 | 1 |
| 3022 | island | 10 | - | 3, 3, 3, 3 | 8 | 3 | 0 (0) | 1 | 3 | 1 |
| 3023 | won | 14 | 1 | 7, 10, 8, 6 | 4 | 8 | 0 (0) | 7 | 27 | 1 |
| 3024 | won | 17 | 0 | 11, 5, 7, 7 | 4 | 12 | 0 (0) | 8 | 26 | 5 |
| 3025 | won | 16 | 2 | 7, 6, 10, 7 | 2 | 10 | 0 (0) | 8 | 16 | 4 |
| 3026 | island | 11 | - | 3, 3, 3, 2 | 8 | 5 | 0 (0) | 1 | 3 | 0 |
| 3027 | island | 12 | - | 4, 4, 2, 4 | 8 | 7 | 0 (0) | 1 | 12 | 0 |
| 3028 | island | 14 | - | 4, 5, 4, 5 | 8 | 9 | 0 (0) | 2 | 7 | 1 |
| 3029 | island | 15 | - | 3, 2, 2, 3 | 8 | 5 | 0 (0) | 1 | 3 | 0 |
| 3030 | island | 17 | - | 4, 4, 6, 4 | 8 | 8 | 0 (0) | 3 | 26 | 2 |
| 3031 | island | 16 | - | 2, 4, 2, 6 | 8 | 11 | 0 (0) | 3 | 11 | 2 |
| 3032 | won | 15 | 2 | 7, 4, 10, 6 | 5 | 10 | 0 (0) | 5 | 27 | 2 |
| 3033 | island | 18 | - | 6, 6, 6, 4 | 8 | 14 | 0 (0) | 7 | 28 | 4 |
| 3034 | island | 20 | - | 8, 5, 9, 9 | 8 | 14 | 0 (0) | 6 | 42 | 5 |
| 3035 | island | 13 | - | 4, 6, 5, 5 | 8 | 8 | 0 (0) | 3 | 11 | 2 |
| 3036 | island | 14 | - | 3, 3, 3, 4 | 8 | 4 | 0 (0) | 2 | 8 | 1 |
| 3037 | island | 12 | - | 4, 4, 2, 3 | 8 | 3 | 0 (0) | 2 | 6 | 0 |
| 3038 | island | 16 | - | 5, 8, 6, 4 | 8 | 12 | 1 (2) | 4 | 12 | 9 |
| 3039 | island | 13 | - | 4, 4, 2, 4 | 8 | 10 | 0 (0) | 1 | 6 | 4 |

## Variants (same seeds; proof settings only, no rule changed)

| Variant | Won / island fell / cap | Median rounds to 10 (won) | Island falls | Rounds at war | Points burned per game: median / max | Trained / purged per game | TR-4 |
|---|---|---|---|---|---|---|---|
| Default (above) | 9 / 31 / 0 | 15 | 77.5% | 56.5% | 0 / 4 | 3.6 / 14.5 | 341 of 341 |
| No war (war stopped from the start; plague only, as in G0-2) | 2 / 38 / 0 | 13.5 | 95.0% | 0.0% | 0 / 0 | 0.0 / 0.0 | 270 of 270 |
| WA-11 literal tie rule (everyone tied = at war) | 10 / 30 / 0 | 15 | 75.0% | 87.7% | 0 / 4 | 3.6 / 14.5 | 345 of 345 |
| Player-paced rounds (AiTurnTicks 150: about 600 ticks a round) | 5 / 35 / 0 | 18 | 87.5% | 57.5% | 6.5 / 20 | 8.9 / 32.0 | 507 of 508 |
