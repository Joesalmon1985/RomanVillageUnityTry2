# G1-3 proof: the god board with virtual players

Written by `GodBoardVirtualProof.VirtualPlayers_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter "FullyQualifiedName~GodBoardVirtualProof"`).
Setup: 48 seeds (3000 to 3047) per player, `GameSetup.GodBoard` (dealt board, ports), plague and war live, all rule defaults unchanged (tie rule `OnlyWhenNotEveryoneTied`, grace 6, rate track 1,1,2,2,2,3,3,3,4, win at 10, war lead 2, one garrison soldier). The player's seat rotates with the seed (seed mod 4), the other three seats are `TableAi`. Cap 120000 ticks. Whole run: 35 s.

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
| Rounds to 10 points (won games): median (min-max) | 25-40 (AI seat) | 15 (10-17, 16 won) | 16.5 (10-22, 14 won) | 21 (16-22, 4 won) | 16 (12-21, 15 won) | 16 (10-19, 17 won) | 15 (12-17, 11 won) |
| Won games in the 25-40 band | - | 0 of 16 | 0 of 14 | 0 of 4 | 0 of 15 | 0 of 17 | 0 of 11 |
| Games won by the player's seat | Quick 30-50%; Absent under 5% (25% is even) | 12.5% | 10.4% | 0.0% | 4.2% | 8.3% | - |
| Island falls (8 outbreaks) | 5-20% with no banishing; under 5% with an army brain | 32 of 48 (66.7%) | 34 of 48 (70.8%) | 44 of 48 (91.7%) | 33 of 48 (68.8%) | 31 of 48 (64.6%) | 37 of 48 (77.1%) |
| Points lost to burning per game: median / max | median under 2 | 0 / 4 | 3 / 18 | 6 / 15 | 0 / 4 | 0 / 6 | 0 / 8 |
| Games with a fall from 9+ to under 6 more than twice | none | 0 (falls in all games: 0) | 0 (falls in all games: 1) | 0 (falls in all games: 0) | 0 (falls in all games: 0) | 0 (falls in all games: 0) | 0 (falls in all games: 0) |
| Share of rounds with a war active | under 30% | 56.5% | 57.3% | 44.2% | 54.5% | 57.6% | 55.6% |
| Trades between AI seats obeying TR-4 | 100% | 182 of 182 | 300 of 300 | 207 of 207 | 204 of 204 | 226 of 226 | 404 of 404 |
| Trades with the player (of them with a leading people at the table) | - (the player's own choice; the engine closes AI offers to a leading player and AI seats refuse a leading player) | 273 (101) | 384 (137) | 0 (0) | 190 (0) | 203 (0) | - |
| AI-to-player offers per round while the player is not leading | at least 1 in 3 (0.33) | 2.99 (637 in 213 rounds) | 3.04 (825 in 271 rounds) | 2.78 (1447 in 520 rounds) | 2.98 (739 in 248 rounds) | 2.97 (704 in 237 rounds) | - |

Targets not measured by this proof: **wares ledger balance and card accounting** (covered by the ledger and table EditMode tests, not re-run per game here); **determinism** (the harness test `VirtualPlayers_SameSeedSameGame_AndEveryGameEnds` runs each virtual player twice on one seed and compares the outcome; save and load at every step is in the G0-3 save tests); **campaign** (see below).

**Campaign-free note:** these games are run without the crystal campaign. No crystal was played, restored, or granted, so the 12.2 campaign target (a direct player reaches at least 2 crystals first, a reluctant one 0, with the board played in between) is not measured here; the plague had no cure (CC-3) and no free grants (G2-3, CC-4) changed the board.

## Other figures (per game, mean)

| Measure | Quick | Slow | Absent | AiSeat | AiSeatBanish | All-AI table |
|---|---|---|---|---|---|---|
| Rounds played (all games) | 14.1 | 15.5 | 16.4 | 14.6 | 14.6 | 14.4 |
| Table ticks per round | 117 | 355 | 519 | 152 | 153 | 142 |
| Banishes by the player | 3.2 | 0.0 | 0.0 | 0.0 | 3.9 | 0.0 |
| AI offers to the player | 13.3 | 17.2 | 30.1 | 15.4 | 14.7 | 0.0 |
| Soldiers trained / demons purged (soldiers) | 2.8 | 7.7 | 7.6 | 3.9 | 4.2 | 3.6 |
| Demons purged by soldiers | 9.4 | 27.1 | 29.4 | 15.9 | 13.1 | 14.2 |
| Settlements burned | 0.06 | 1.90 | 3.04 | 0.15 | 0.25 | 0.21 |
| Trades between peoples | 9.5 | 14.3 | 4.3 | 8.2 | 8.9 | 8.4 |
| Bank and port trades | 15.0 | 16.2 | 11.4 | 16.3 | 16.5 | 15.6 |
| Outbreaks at the end | 6.3 | 6.8 | 7.7 | 6.8 | 6.4 | 7.0 |

The row "Soldiers trained / demons purged" shows trained soldiers (the next row the purges).

## Games by outcome

| Player | Won (any seat) / island fell / cap | Player wins | Player's final points: median | Rounds when the island fell: median |
|---|---|---|---|---|
| Quick | 16 / 32 / 0 | 6 of 48 | 5 | 14 |
| Slow | 14 / 34 / 0 | 5 of 48 | 5.5 | 15 |
| Absent | 4 / 44 / 0 | 0 of 48 | 2 | 16 |
| AiSeat | 15 / 33 / 0 | 2 of 48 | 5 | 14 |
| AiSeatBanish | 17 / 31 / 0 | 4 of 48 | 5.5 | 14 |
