# G1-3 proof: the god board with virtual players

Written by `GodBoardVirtualProof.VirtualPlayers_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter "FullyQualifiedName~GodBoardVirtualProof"`).
Setup: 48 seeds (3000 to 3047) per player, `GameSetup.GodBoard` (dealt board, ports), plague and war live, the rule defaults of balance F1 (infection draw every 3rd round, tie rule `NeverWhenTied`, grace 6 infection draws, rate track 1,1,2,2,2,3,3,3,4, win at 10, war lead 2, one garrison soldier). The player's seat rotates with the seed (seed mod 4), the other three seats are `TableAi`. Cap 120000 ticks. Whole run: 139 s.

Players (one command a tick through `WorldSim.Submit`, never past the engine's checks):
- **Quick**: acts at once, accepts every fair offer (TR-6 fairness, TR-4 leader rule not applied), ends its turn as soon as nothing is left to do, discards at once, banishes the demon node (most demons first) on a hex beside its own settlements whenever the cooldown allows.
- **Slow**: one action about every 3.5 s, answers offers after 9 s, ends its turn only when under a fifth of the base sand is left (so it uses most of every hourglass), no banishing.
- **Absent**: sacrifices to open the board, places its setup pieces (the setup round has no hourglass; with the AI's choice), then never acts: every turn ends on the hourglass, offers to it expire, discards are automatic.
- **AI seat**: the human seat played by `TableAi.PlayTurn` and `TableAi.Accepts`, with the AI's own turn length (30 ticks plus 10 an action), no banishing.
- **AI seat + banish** (extra): the AI seat that also banishes like Quick.
- **All-AI table** (reference, `TableHumanSeat = -1`): the G0-3 proof's setting on these seeds.

## Against PRD 12.2

| Measure | Target | Quick | Slow | Absent | AiSeat | AiSeatBanish | All-AI table |
|---|---|---|---|---|---|---|---|
| Rounds to 10 points (won games): median (min-max) | 25-40 (AI seat) | 17 (10-26, 48 won) | 18 (10-29, 48 won) | 21 (14-33, 45 won) | 18 (11-26, 48 won) | 17.5 (10-26, 48 won) | 18 (12-26, 47 won) |
| Won games in the 25-40 band | - | 2 of 48 | 5 of 48 | 13 of 45 | 1 of 48 | 2 of 48 | 2 of 47 |
| Games won by the player's seat | Quick 30-50%; Absent under 5% (25% is even) | 37.5% | 27.1% | 0.0% | 35.4% | 41.7% | - |
| Island falls (8 outbreaks) | 5-20% with no banishing; under 5% with an army brain | 0 of 48 (0.0%) | 0 of 48 (0.0%) | 3 of 48 (6.3%) | 0 of 48 (0.0%) | 0 of 48 (0.0%) | 1 of 48 (2.1%) |
| Points lost to burning per game: median / max | median under 2 | 0 / 9 | 2 / 14 | 3 / 21 | 0 / 9 | 0 / 10 | 0 / 8 |
| Games with a fall from 9+ to under 6 more than twice | none | 0 (falls in all games: 0) | 0 (falls in all games: 2) | 0 (falls in all games: 3) | 0 (falls in all games: 1) | 0 (falls in all games: 0) | 0 (falls in all games: 2) |
| Share of rounds with a war active | under 30% | 20.3% | 14.3% | 17.3% | 15.9% | 16.7% | 16.7% |
| Trades between AI seats obeying TR-4 | 100% | 384 of 384 | 401 of 401 | 334 of 334 | 417 of 417 | 444 of 444 | 791 of 791 |
| Trades with the player (of them with a leading people at the table) | - (the player's own choice; the engine closes AI offers to a leading player and AI seats refuse a leading player) | 502 (183) | 597 (233) | 0 (0) | 350 (0) | 341 (0) | - |
| AI-to-player offers per round while the player is not leading | at least 1 in 3 (0.33) | 3.02 (1015 in 336 rounds) | 3.01 (1151 in 382 rounds) | 2.93 (2470 in 844 rounds) | 3.06 (1057 in 345 rounds) | 3.05 (1056 in 346 rounds) | - |

Targets not measured by this proof: **wares ledger balance and card accounting** (covered by the ledger and table EditMode tests, not re-run per game here); **determinism** (the harness test `VirtualPlayers_SameSeedSameGame_AndEveryGameEnds` runs each virtual player twice on one seed and compares the outcome; save and load at every step is in the G0-3 save tests); **campaign** (see below).

**Campaign-free note:** these games are run without the crystal campaign. No crystal was played, restored, or granted, so the 12.2 campaign target (a direct player reaches at least 2 crystals first, a reluctant one 0, with the board played in between) is not measured here; the plague had no cure (CC-3) and no free grants (G2-3, CC-4) changed the board.

## Other figures (per game, mean)

| Measure | Quick | Slow | Absent | AiSeat | AiSeatBanish | All-AI table |
|---|---|---|---|---|---|---|
| Rounds played (all games) | 17.4 | 18.6 | 22.4 | 18.1 | 18.1 | 18.4 |
| Table ticks per round | 137 | 393 | 599 | 174 | 174 | 163 |
| Banishes by the player | 1.5 | 0.0 | 0.0 | 0.0 | 1.6 | 0.0 |
| AI offers to the player | 21.1 | 24.0 | 51.5 | 22.0 | 22.0 | 0.0 |
| Soldiers trained / demons purged (soldiers) | 6.1 | 12.8 | 16.2 | 7.6 | 7.7 | 7.8 |
| Demons purged by soldiers | 3.6 | 8.3 | 13.9 | 5.6 | 4.3 | 6.3 |
| Settlements burned | 0.75 | 2.02 | 3.04 | 0.77 | 0.94 | 0.69 |
| Trades between peoples | 18.5 | 20.8 | 7.0 | 16.0 | 16.4 | 16.5 |
| Bank and port trades | 23.7 | 25.3 | 26.7 | 26.8 | 26.6 | 26.1 |
| Outbreaks at the end | 0.2 | 0.4 | 1.0 | 0.3 | 0.4 | 0.5 |

The row "Soldiers trained / demons purged" shows trained soldiers (the next row the purges).

## Games by outcome

| Player | Won (any seat) / island fell / cap | Player wins | Player's final points: median | Rounds when the island fell: median |
|---|---|---|---|---|
| Quick | 48 / 0 / 0 | 18 of 48 | 9 | n/a |
| Slow | 48 / 0 / 0 | 13 of 48 | 8 | n/a |
| Absent | 45 / 3 / 0 | 0 of 48 | 2 | 31 |
| AiSeat | 48 / 0 / 0 | 17 of 48 | 8 | n/a |
| AiSeatBanish | 48 / 0 / 0 | 20 of 48 | 9 | n/a |
