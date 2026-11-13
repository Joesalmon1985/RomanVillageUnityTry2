# Economy design (prehistoric village)

The economy design is now `Docs/REFACTOR_SPEC.md` sections 2 to 6 (binding): dice and growing hex stock, camps with plots and primitive collectors, main roads, per-camp storehouses with shipments, workshops and warriors, the prince's proposals and spells, the rival and the overrun, minimal demons. It is not repeated here, so there is one source.

What the implementation adds or changes is recorded in three places:

- The spec's API change log (appendix A.5): every added name and every rule detail the code had to settle.
- `Docs/ECONOMY_API.md`: how the code behaves where the spec leaves room (logistics order, routes, brain scores as tuned) and what the game layer can call.
- `Docs/PROGRESS.md` (S entry): the tuning log with the numbers tried and why the values differ from the spec's starting values. The proof with the resulting numbers is `Docs/ECONOMY_PROOF.md`.

The previous unified design (rounds, draft, wonder, military, breach track) is in history (`Docs/ECONOMY_DESIGN.md` on `main` at `189466f`).
