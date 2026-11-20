# G0-3 proof: all-AI god board games with the plague and the war

Written by `GodBoardJoinProof.AiOnlyGodBoard_PlagueAndWar_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter "FullyQualifiedName~GodBoardJoinProof"`).
Setup: 40 seeds (3000 to 3039), `GameSetup.GodBoard` (dealt board, ports), AI-only table (`TableHumanSeat = -1`, 4 seats on `TableAi`), plague and war wired as in a real game, nobody banishing (no player). Balance F1 defaults: infection every 3rd round, war tie rule `NeverWhenTied`. Cap 120000 ticks.

## Against PRD 12.2

| Measure | Target | Value |
|---|---|---|
| Rounds to 10 points (games won): min / median / max | 25-40 | 12 / 19 / 26 |
| Won games in the 25-40 band | - | 2 of 39 |
| Island falls (8 outbreaks) | under 5% with an army brain active | 1 of 40 (2.5%) |
| Points lost to burning per game: median / max | median under 2 | 0 / 8 |
| Games where a people fell from 9+ to under 6 more than twice | none | 0 (falls in all games: 2) |
| Share of rounds with any war active | under 30% | 15.0% (111 of 739 rounds) |
| Share of table ticks with any war active | - | 8.5% |
| Trades between peoples obeying TR-4 | 100% | 673 of 673 (100.0%) |

## Other figures (per game, mean)

| Measure | Value |
|---|---|
| Games won / island fell / hit the cap | 39 / 1 / 0 |
| Rounds when the island fell: min / median / max | 25 / 25 / 25 |
| Wins by people 0 / 1 / 2 / 3 | 8 / 8 / 7 / 16 |
| Soldiers trained | 7.9 |
| Demons purged by soldiers | 6.5 |
| Duels / soldiers died | 5.3 / 2.6 |
| Settlements burned (of them cities) / home camps looted | 0.68 (0.45) / 0.30 |
| Outbreaks / epidemics | 0.5 / 1.1 |
| Blocked hex productions | 8.0 |
| Trades between peoples / bank and port | 16.8 / 26.0 |
| Builds: roads / settlements / cities | 22.5 / 10.3 / 12.4 |

## Per seed

| Seed | End | Rounds | Winner | Points | Outbreaks | Rounds at war | Burned (points) | Trained | Purged | Died |
|---|---|---|---|---|---|---|---|---|---|---|
| 3000 | won | 15 | 3 | 8, 5, 9, 10 | 0 | 5 | 0 (0) | 2 | 3 | 1 |
| 3001 | won | 14 | 2 | 3, 9, 10, 5 | 0 | 1 | 0 (0) | 5 | 1 | 0 |
| 3002 | won | 17 | 1 | 9, 10, 6, 9 | 0 | 2 | 0 (0) | 8 | 4 | 1 |
| 3003 | won | 19 | 2 | 5, 9, 10, 8 | 0 | 4 | 3 (4) | 9 | 4 | 6 |
| 3004 | won | 24 | 3 | 9, 9, 9, 10 | 0 | 9 | 2 (3) | 10 | 8 | 8 |
| 3005 | island | 25 | - | 9, 7, 8, 8 | 8 | 0 | 0 (0) | 11 | 41 | 0 |
| 3006 | won | 19 | 3 | 6, 9, 7, 10 | 0 | 7 | 0 (0) | 11 | 5 | 2 |
| 3007 | won | 17 | 0 | 11, 5, 5, 7 | 0 | 1 | 0 (0) | 6 | 4 | 0 |
| 3008 | won | 20 | 3 | 4, 9, 9, 10 | 0 | 2 | 0 (0) | 9 | 3 | 0 |
| 3009 | won | 21 | 1 | 8, 10, 5, 6 | 0 | 8 | 1 (2) | 10 | 6 | 12 |
| 3010 | won | 22 | 2 | 7, 8, 10, 8 | 2 | 1 | 0 (0) | 13 | 9 | 0 |
| 3011 | won | 12 | 2 | 9, 5, 10, 6 | 0 | 2 | 0 (0) | 4 | 2 | 0 |
| 3012 | won | 15 | 0 | 10, 9, 5, 5 | 0 | 1 | 0 (0) | 2 | 4 | 0 |
| 3013 | won | 14 | 3 | 5, 8, 8, 10 | 0 | 1 | 0 (0) | 9 | 4 | 0 |
| 3014 | won | 17 | 2 | 4, 7, 10, 5 | 0 | 1 | 0 (0) | 3 | 5 | 0 |
| 3015 | won | 26 | 3 | 8, 9, 8, 10 | 4 | 6 | 5 (8) | 12 | 28 | 10 |
| 3016 | won | 19 | 3 | 9, 8, 8, 10 | 0 | 1 | 0 (0) | 9 | 5 | 0 |
| 3017 | won | 14 | 3 | 6, 8, 6, 10 | 0 | 2 | 0 (0) | 4 | 3 | 0 |
| 3018 | won | 15 | 1 | 5, 10, 7, 9 | 0 | 4 | 1 (1) | 5 | 2 | 2 |
| 3019 | won | 19 | 3 | 7, 8, 8, 11 | 0 | 1 | 0 (0) | 9 | 4 | 0 |
| 3020 | won | 14 | 2 | 4, 3, 11, 8 | 0 | 1 | 0 (0) | 5 | 2 | 0 |
| 3021 | won | 22 | 3 | 8, 8, 6, 10 | 1 | 2 | 0 (0) | 9 | 9 | 0 |
| 3022 | won | 19 | 3 | 7, 8, 8, 10 | 0 | 3 | 0 (0) | 9 | 4 | 1 |
| 3023 | won | 15 | 1 | 6, 10, 8, 9 | 0 | 1 | 0 (0) | 9 | 4 | 0 |
| 3024 | won | 15 | 0 | 11, 6, 7, 8 | 0 | 1 | 0 (0) | 7 | 3 | 0 |
| 3025 | won | 18 | 0 | 10, 9, 7, 8 | 0 | 0 | 0 (0) | 10 | 1 | 0 |
| 3026 | won | 24 | 0 | 10, 9, 7, 7 | 1 | 2 | 1 (2) | 11 | 18 | 3 |
| 3027 | won | 21 | 3 | 8, 8, 6, 10 | 0 | 4 | 2 (4) | 9 | 3 | 3 |
| 3028 | won | 14 | 1 | 5, 10, 4, 7 | 0 | 5 | 1 (2) | 4 | 2 | 11 |
| 3029 | won | 26 | 3 | 6, 4, 9, 10 | 0 | 2 | 0 (0) | 7 | 9 | 2 |
| 3030 | won | 22 | 0 | 10, 6, 9, 6 | 2 | 3 | 1 (2) | 10 | 11 | 5 |
| 3031 | won | 22 | 3 | 9, 7, 4, 10 | 0 | 7 | 4 (8) | 9 | 13 | 15 |
| 3032 | won | 20 | 2 | 8, 8, 10, 9 | 0 | 6 | 3 (5) | 14 | 5 | 15 |
| 3033 | won | 14 | 0 | 10, 9, 8, 7 | 0 | 0 | 0 (0) | 8 | 2 | 0 |
| 3034 | won | 19 | 3 | 8, 6, 9, 10 | 0 | 1 | 0 (0) | 9 | 5 | 0 |
| 3035 | won | 16 | 1 | 7, 10, 7, 8 | 0 | 2 | 0 (0) | 9 | 3 | 0 |
| 3036 | won | 23 | 1 | 7, 10, 7, 7 | 1 | 3 | 0 (0) | 8 | 12 | 0 |
| 3037 | won | 16 | 3 | 2, 6, 6, 10 | 0 | 4 | 3 (4) | 3 | 3 | 6 |
| 3038 | won | 15 | 0 | 10, 8, 6, 8 | 0 | 1 | 0 (0) | 3 | 1 | 0 |
| 3039 | won | 20 | 1 | 9, 11, 4, 9 | 0 | 4 | 0 (0) | 10 | 4 | 0 |

## Variants (same seeds; proof settings only, the first row is the balance F1 default)

| Variant | Won / island fell / cap | Median rounds to 10 (won) | Island falls | Rounds at war | Points burned per game: median / max | Trained / purged per game | TR-4 |
|---|---|---|---|---|---|---|---|
| Default (above) | 39 / 1 / 0 | 19 | 2.5% | 15.0% | 0 / 8 | 7.9 / 6.5 | 673 of 673 |
| No war (war stopped from the start; plague only, as in G0-2) | 39 / 1 / 0 | 18 | 2.5% | 0.0% | 0 / 0 | 0.0 / 0.0 | 609 of 609 |
| WA-11 literal tie rule (everyone tied = at war) | 37 / 3 / 0 | 20 | 7.5% | 90.6% | 3 / 15 | 8.6 / 6.6 | 775 of 775 |
| Before balance F1 (infection every round, tied leaders fight unless everyone is tied) | 9 / 31 / 0 | 15 | 77.5% | 56.5% | 0 / 4 | 3.6 / 14.5 | 341 of 341 |
| Player-paced rounds (AiTurnTicks 150: about 600 ticks a round) | 40 / 0 / 0 | 19 | 0.0% | 11.8% | 1 / 9 | 13.9 / 8.2 | 685 of 685 |
