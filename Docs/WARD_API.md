# Ward API (namespace `World.Sim.Encounters.Ward`)

Pure C#, integer milliseconds, deterministic. The session is independent of `WorldSim`; the UI owns it and drives it with real time.

- `Essence` { Flame, Frost, Vine, Light }; `EssenceInfo.DisplayName(e)`, `EssenceInfo.Symbol(e)`. Weaves and Wards are `int[]` of `(int)Essence`, length `WardTuning.Loci` (3).
- `WardTuning` plain class with defaults (Loci, MaxCasts, PlayerRecoveryMs, IdleAutoCastMs, Mira* timings, MiraMistakeRatePercent, LightUnlockAfterDuels).
- `WardFormat.For(state.Ward.CompletedDuelsVsMira, tuning)` -> `Essence[]` pool (Light from the 4th duel).
- `new WardDuelSession(tuning, pool, seed)`; seed e.g. from `state.Rng` plus the duel count.
- `SetPlayerWard(int[])` -> bool (false if wrong length or essence outside pool). Phase Setup -> Active, the clock starts.
- `Advance(int dtMs)`: call each frame with elapsed ms. Runs Mira, the 60 s idle auto-cast (random weave, spends a cast) and end detection.
- `CastResult PlayerCast(int[] weave)`: `Ok`, `Fractures/Echoes/Fades`, `Broken`, `MsUntilNextCast`; refused with `Reason` (`recovering`, `no_casts`, `bad_weave`, `not_active`) and a `Message` sentence.
- Read-only: `Phase` (Setup/Active/Complete), `Outcome` (None, Victory, Defeat, Stalemate, Clash, Forfeited), `PlayerCastsLeft`, `RivalCastsLeft`, `MsUntilPlayerMayCast`, `ElapsedMs`, `PlayerHistory`, `RivalHistory` (`WardCastEntry`: Weave, Fractures, Echoes, Fades; show totals only, never positions).
- `DrainEvents()` -> `PlayerCastEvent` (Entry, Auto), `RivalCastEvent` (Entry), `DuelEndedEvent` (Outcome); each has `AtMs`.
- `Forfeit()`: leaving mid-duel; Outcome Forfeited.
- `RevealedPlayerWard` / `RevealedRivalWard`: null until Phase is Complete (the rival Ward is never readable earlier).
- `state.Ward` (`WardProgress`: CompletedDuelsVsMira, Wins, Losses, Draws): call `state.Ward.Record(session.Outcome)` once when the duel ends. Forfeits are ignored. Saved with `WorldState`.

Outcome rules: both Wards broken in the same `Advance` step = Clash (draw); one break = Victory/Defeat; both sides out of casts = Stalemate. When the player runs out first Mira keeps casting until she breaks (Defeat) or runs out (Stalemate). A `PlayerCast` that breaks the Ward ends the duel at once (Victory).

```csharp
var pool = WardFormat.For(state.Ward.CompletedDuelsVsMira, tuning);
var duel = new WardDuelSession(tuning, pool, seed);
duel.SetPlayerWard(chosen);                       // from the Ward-setup screen
// Update():
duel.Advance((int)(Time.unscaledDeltaTime * 1000));
if (castPressed) { var r = duel.PlayerCast(weave); if (!r.Ok) ShowToast(r.Message); }
foreach (var ev in duel.DrainEvents()) Animate(ev);
if (duel.Phase == WardPhase.Complete) { state.Ward.Record(duel.Outcome); Show(duel.RevealedPlayerWard, duel.RevealedRivalWard); }
```

Rival (measured, 200 seeds, 3 loci): pool of 3 breaks 200/200 within ten casts, 2.83 casts on average; pool of 4 breaks 200/200, 3.28 average. Mira is fast; raise `MiraMistakeRatePercent` or her think time to soften her.


## Opponent profiles (PRD_CRYSTALS I3)

Opponents are data: `WardOpponentProfile` (first-cast delay, think time, `MistakeRatePercent` = random casts that ignore feedback, optional `MaxCasts`, optional `Pool`, plus presentation hints `CastTempo`, `CastAnimMs`, `StyleNote` that the simulation never reads). The table is `StreamingAssets/world/ward_opponents.json` (`{ "Profiles": [...] }`), parsed with `WardOpponents.Parse(json)`; `WardOpponents.BuiltIn()` is the same table in code (a test pins equality). `opponents.Spec(id, seed, completedDuelsVsMira)` returns a `WardDuelSpec`; `WardDuelSession.Create(spec)` builds the session. An unknown id falls back to `keeper`. `practice_mira` has no pool, so the caller keeps the 3-then-4 `WardFormat`; the session for it is cast-for-cast identical to `new WardDuelSession(WardTuning.Practice(), pool, seed)` (tested).

| Id | Name | Pool | First cast | Think | Mistakes | Tempo |
|---|---|---|---|---|---|---|
| `practice_mira` | Mira | 3, then 4 (caller) | 3 to 6 s | 6 to 10 s | 0% | measured |
| `keeper` | generic keeper | 3 | 4 to 7 s | 7 to 11 s | 15% | slow |
| `keeper_earth` / `_fire` / `_water` / `_air` | Dolm / Brask / Nual / Pike | 3 | 4 to 7 s | 7 to 11 s | 15% | slow / brisk / measured / brisk |
| `rival_hesk` | Hesk Varr | 3 | 3.5 to 6.5 s | 6.5 to 10.5 s | 5% | measured |
| `friendly_orrin` | Orrin | 3 | 5 to 8 s | 9 to 13 s | 25% | slow |
| `finale_unweaver` | the Unweaver, in Hesk Varr | 4 (with Light) | 3 to 6 s | 5.5 to 9.5 s | 0% | hasty |

Measured win rates (victories only; 300 seeds 7000 to 7299 per cell; `WardOpponentTests.Measure_PrintsWinRateTable`). Scripted players, all candidate-filter deduction: first-timer = `WardBalanceTests.FirstTimer` (a cast every 5 to 7 s, 15% random casts); competent = every 3.5 to 5 s, 5% random; expert = every 2.2 to 3.2 s, 0% random.

| Profile | First-timer win | lose | draw | Competent win | Expert win |
|---|---|---|---|---|---|
| `practice_mira` | 54% | 46% | 0% | 81% | 97% |
| `keeper` | 71% | 29% | 0% | 92% | 97% |
| `keeper_earth` | 71% | 29% | 0% | 92% | 97% |
| `keeper_fire` | 71% | 29% | 0% | 92% | 97% |
| `keeper_water` | 71% | 29% | 0% | 92% | 97% |
| `keeper_air` | 71% | 29% | 0% | 92% | 97% |
| `rival_hesk` | 64% | 36% | 0% | 88% | 97% |
| `friendly_orrin` | 82% | 18% | 0% | 94% | 98% |
| `finale_unweaver` | 57% | 43% | 0% | 85% | 97% |

The keepers sit at 71% for the first-timer (target 65 to 75) and the finale at 57% (target a little over half). The PRD starting think times (8 to 12 s keepers, 6 to 10 s finale) measured 75% and 60%, so each think range was shortened by 1 s (finale 0.5 s) to centre on the target; Hesk Varr sits between. Stalemates and clashes are about 0% for every profile.
