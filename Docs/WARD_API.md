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
