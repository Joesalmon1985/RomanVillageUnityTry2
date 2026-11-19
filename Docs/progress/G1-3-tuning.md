# G1-3 tuning options study (proof settings only; no rule default changed)

Written by `GodBoardVirtualProof.TuningStudy_WriteOptions`. Every row is the **AI seat** (the human seat played by the table AI, seat rotating with the seed) with three `TableAi` peoples, 48 seeds (3000 to 3047), dealt board, plague and war live, no banishing. Variants are set only inside the proof through `Knobs` (static knobs in `PlagueRules`, `WarData`, and `TableTuning.WinPoints` on the fresh state), restored after every game. Run time 439 s.

Targets: island falls under about 10%, rounds to win 25-40, war active under about 30% of rounds.

| Variant | Won / fell | Rounds to win: median (min-max) | Won in 25-40 | Island falls | Rounds at war | Burned points median / max | Trained / purged | Player wins |
|---|---|---|---|---|---|---|---|---|
| Default (the rules as they stand) | 15 / 33 | 16 (12-21, 15 won) | 0 of 15 | 68.8% | 54.5% | 0 / 4 | 3.9 / 15.9 | 4.2% |
| S1 plague grace 6 to 10 rounds | 26 / 22 | 17 (11-25, 26 won) | 1 of 26 | 45.8% | 59.6% | 0 / 5 | 6.3 / 18.9 | 16.7% |
| S2 rate track 1,1,1,2,2,2,3,3,4 | 15 / 33 | 16 (12-22, 15 won) | 0 of 15 | 68.8% | 54.6% | 0 / 4 | 4.0 / 16.4 | 6.3% |
| S3 infection every 2nd round | 34 / 14 | 18 (10-25, 34 won) | 1 of 34 | 29.2% | 66.9% | 2 / 14 | 7.7 / 15.2 | 22.9% |
| S4 win at 12 points | 6 / 42 | 16.5 (13-20, 6 won) | 0 of 6 | 87.5% | 55.7% | 0 / 4 | 4.4 / 18.1 | 2.1% |
| S5 win at 13 points | 3 / 45 | 19 (14-21, 3 won) | 0 of 3 | 93.8% | 56.3% | 0 / 4 | 4.7 / 19.1 | 0.0% |
| S6 war lead 3 | 16 / 32 | 16 (10-21, 16 won) | 0 of 16 | 66.7% | 48.9% | 0 / 2 | 3.9 / 16.2 | 6.3% |
| S7 two soldiers per settlement | 6 / 42 | 15.5 (13-18, 6 won) | 0 of 6 | 87.5% | 59.8% | 2 / 14 | 4.6 / 22.2 | 6.3% |
| S8 WA-11 literal tie rule | 13 / 35 | 16 (12-21, 13 won) | 0 of 13 | 72.9% | 86.4% | 0 / 4 | 3.8 / 15.0 | 2.1% |
| S9 epidemic places 2 demons | 21 / 27 | 16 (11-24, 21 won) | 0 of 21 | 56.3% | 58.1% | 0 / 4 | 5.0 / 18.8 | 8.3% |
| S10 island falls at 10 outbreaks | 15 / 33 | 16 (12-21, 15 won) | 0 of 15 | 68.8% | 55.0% | 0 / 4 | 4.1 / 17.2 | 4.2% |
| S11 rate track advances every 2nd epidemic | 15 / 33 | 16 (12-22, 15 won) | 0 of 15 | 68.8% | 54.5% | 0 / 4 | 4.0 / 16.6 | 6.3% |
| S13 infection every 3rd round | 48 / 0 | 20 (11-31, 48 won) | 12 of 48 | 0.0% ok | 68.2% | 4 / 20 | 9.9 / 9.9 | 33.3% |
| S14 war lead 4 | 16 / 32 | 16 (10-21, 16 won) | 0 of 16 | 66.7% | 47.4% | 0 / 2 | 3.9 / 16.4 | 6.3% |
| S15 tied leaders never fight (new tie value) | 12 / 36 | 16 (12-19, 12 won) | 0 of 12 | 75.0% | 11.1% ok | 0 / 4 | 4.0 / 18.1 | 6.3% |
| S16 win at 14 points | 3 / 45 | 20 (14-22, 3 won) | 0 of 3 | 93.8% | 56.4% | 0 / 4 | 4.7 / 19.4 | 0.0% |
| D1 S13 + win 12 | 44 / 4 | 23 (16-32, 44 won) | 16 of 44 | 8.3% ok | 71.9% | 7 / 20 | 14.1 / 18.5 | 25.0% |
| D2 S13 + win 13 | 41 / 7 | 25 (18-34, 41 won) | 22 of 41 | 14.6% | 73.5% | 8 / 32 | 16.5 / 23.6 | 31.3% |
| D3 S13 + win 14 | 29 / 19 | 26 (21-33, 29 won) | 18 of 29 | 39.6% | 73.7% | 8.5 / 33 | 20.4 / 35.6 | 25.0% |
| D4 S13 + win 13 + S15 (ties never fight) | 45 / 3 | 23 (16-30, 45 won) | 15 of 45 | 6.3% ok | 22.0% ok | 4 / 26 | 13.9 / 16.6 | 37.5% |
| D5 S13 + win 13 + S15 + lead 3 | 46 / 2 | 22 (15-29, 46 won) | 8 of 46 | 4.2% ok | 11.1% ok | 0 / 14 | 12.6 / 13.0 | 31.3% |
| D6 S13 + win 14 + S15 + lead 3 | 44 / 4 | 23 (16-29, 44 won) | 16 of 44 | 8.3% ok | 13.2% ok | 0 / 14 | 14.5 / 16.2 | 27.1% |
| D7 every 2nd round + win 13 + S15 + lead 3 | 33 / 15 | 21 (16-29, 33 won) | 6 of 33 | 31.3% | 11.0% ok | 0 / 9 | 11.5 / 27.7 | 14.6% |
| D8 D7 + grace 10 | 47 / 1 | 22 (16-30, 47 won) | 13 of 47 | 2.1% ok | 11.0% ok | 0.5 / 11 | 13.1 / 13.9 | 29.2% |
| D9 S15 + lead 3 only (war change alone) | 15 / 33 | 16 (10-19, 15 won) | 0 of 15 | 68.8% | 4.0% ok | 0 / 1 | 3.9 / 17.3 | 10.4% |
| F1 S13 + S15 (infect every 3rd round, tied leaders never fight) | 48 / 0 | 18 (11-26, 48 won) | 1 of 48 | 0.0% ok | 15.9% ok | 0 / 9 | 7.6 / 5.6 | 35.4% |
| F2 F1 + win 12 | 46 / 2 | 20 (14-27, 46 won) | 6 of 46 | 4.2% ok | 20.4% ok | 2 / 11 | 11.1 / 10.8 | 33.3% |
| E1 D6 + grace 10 | 43 / 5 | 23 (16-33, 43 won) | 17 of 43 | 10.4% | 11.8% ok | 0 / 14 | 16.4 / 11.8 | 29.2% |
| E2 S13 + win 14 + S15 (war lead stays 2) | 37 / 11 | 23 (16-35, 37 won) | 15 of 37 | 22.9% | 22.3% ok | 4.5 / 26 | 16.9 / 24.5 | 33.3% |
| E3 every 2nd round + grace 10 + win 14 + S15 + lead 3 | 37 / 11 | 23 (18-31, 37 won) | 16 of 37 | 22.9% | 11.2% ok | 1 / 15 | 16.0 / 23.4 | 22.9% |
| E4 S13 + win 15 + S15 + lead 3 | 25 / 23 | 23 (16-38, 25 won) | 10 of 25 | 47.9% | 11.7% ok | 2 / 16 | 20.4 / 35.0 | 14.6% |
| E5 every 2nd round + rate every 2nd epidemic + grace 10 + win 14 + S15 + lead 3 | 38 / 10 | 23 (18-32, 38 won) | 16 of 38 | 20.8% | 11.1% ok | 1 / 15 | 16.1 / 22.9 | 22.9% |

## Finalists against every virtual player (same seeds, 48 games each)

Cells: rounds to win (median) / island falls / war share of rounds / player wins. Targets: rounds 25-40 (AI seat), falls under 10%, war under 30%, Quick wins 30-50%, Absent under 5%.

| Option set | Quick | Slow | Absent | AI seat |
|---|---|---|---|---|
| Default (the rules as they stand) | 15 / 66.7% / 56.5% / 12.5% | 16.5 / 70.8% / 57.3% / 10.4% | 21 / 91.7% / 44.2% / 0.0% | 16 / 68.8% / 54.5% / 4.2% |
| F1 infect every 3rd round + tied leaders never fight | 17 / 0.0% / 20.3% / 37.5% | 18 / 0.0% / 14.3% / 27.1% | 21 / 6.3% / 17.3% / 0.0% | 18 / 0.0% / 15.9% / 35.4% |
| F3 F1 + win 13 | 22 / 8.3% / 25.9% / 31.3% | 24 / 2.1% / 22.6% / 25.0% | 28.5 / 20.8% / 22.8% / 0.0% | 23 / 6.3% / 22.0% / 37.5% |
| D5 F3 + war lead 3 | 22 / 2.1% / 13.2% / 31.3% | 22 / 0.0% / 10.8% / 35.4% | 25 / 4.2% / 9.9% / 0.0% | 22 / 4.2% / 11.1% / 31.3% |
| D8 infect every 2nd + grace 10 + win 13 + ties never + lead 3 | 21.5 / 0.0% / 12.2% / 35.4% | 21 / 2.1% / 10.6% / 22.9% | 25 / 6.3% / 11.2% / 0.0% | 22 / 2.1% / 11.0% / 29.2% |
