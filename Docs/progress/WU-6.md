# WU-6: the crystal dungeon, world first

20 Nov 2026, branch `godboard/wu6-dungeon` (from `a60cdad`, the WU-5 head). Scope: the WU-6 row of `Docs/WORLD_UI_PLAN.md` section 5 and section 2.6 (CRY-*). No sim file changed; no rule changed. Everything under `Assets/World/Game` only reads state; the one command the new things send is `AcceptLead`.

## Done

- **Footprint stones** (`Interiors/FootprintStones.cs`, zone `CRY-MOVE`): four faint carved stones lying in the touch band, one per strip: turn left (arc arrow), step forward (footprint, toes up), step back (footprint, toes down), turn right. Same four moves on the hex lattice (owner Q-5): no strafe, no quarter turns. Shown underground always, and on the surface for the first 180 s (`EarlySeconds`); the setting `FootprintStones.Mode` (Auto / Always / Never, saved in PlayerPrefs `World.UI.FootprintStones`) can keep or hide them (no menu entry yet). A stone lights while its strip or key is held. Hidden in God View, menus, the map and encounters (the strips are drums there). Laid out inside the safe area (`FootprintStones.Layout`). `TouchStrips.cs` is not changed.
- **Tap a thing, Mira offers the act** (`MiraActThing`, `Expedition/ExpeditionDirector.World.cs`): every interior marker that is not a way gets one. If Mira has a lead whose goal is that marker (the sim's own queue: examine, search, take, restore), the first tap previews on the ledger ("The pedestal in the Root Chamber / A crystal rests on the stone. / her reason / Ask Mira to take it?"), Mira turns towards it, a gold ring lies round the thing; the second tap sends exactly one `AcceptLead` (the command her own "Lead the way" sends). Where she has no errand the card only reads the thing (disabled state, no question). A refusal from the sim (`CampaignApi.Check`) is shown as the refusal line. E / Enter work within 3 m (registered `IWorldAction`).
- **Companion card** (`CompanionCard`, zone `CRY-PARTY`): tap a companion (or the body lying where they fell) for name, trade, what their service does (our words), and alive / out scouting / covering the way / fallen at the place where they fell, "The party remembers." Underground a second tap on a living companion asks "Ask <name> to lead you out?" and starts "Lead me out" (the walk to the nearest open way up; no sim command). The old bubble `OnCompanionTapped` stays as the fallback and is no longer reached for companions (the card wins the tap).
- **Mira's pouch** (`MiraPouch`, `CRY-PACK`): a leather pouch on her belt; tap for the ledger "Mira's pouch": which crystals she carries, or that it is empty. No other inventory.
- **Mira's journal book** (`MiraJournalBook`, `CRY-CAMP`): a book at her hip; tap opens the journal as the map shows it (`ExpeditionJournal.Open`). **J now opens it anywhere in a campaign** (it needed the HUD before).
- **"Lead me out"**: companion cards (above), Mira's menu and every interior marker's old bubble (unchanged). Returns to the surface and the walking view.
- Every interior marker is tagged `CRY-INTERACT`; the stones `CRY-MOVE`; companions and Mira `CRY-PARTY`; pouch `CRY-PACK`; book `CRY-CAMP`. No permanent minimap or HUD meter was added.
- Shared-layer touches (kept small): `IWorldPrimary` marker (`UI/WorldPrimary.cs`) and one line in `PointerInput.TargetOf` so a prop or tag wins the tap over the person or marker it sits on; `PlayerWalker.StripOfKeys` (3 lines, public, for the stones and tests, no behaviour change); `LedgerCard` height now counts the lines of a wrapped title (the question line of a two-line title was clipped, seen in the first capture).
- **WU-5 open problem 1 (`WorldActionController` cancels previews during encounters):** checked. It does not touch interiors: it cancels only while `EncounterHost.IsRunning`, and an interior walk is not an encounter. The keeper duel (a real encounter) stages its own bodies and has nothing tappable, so nothing is lost. Left unchanged so no other encounter changes behaviour.

## Tests run (20 Nov, editor 6000.3.28f1)

- PlayMode `InteriorTests` **14 / 14** (2 new: `Band_And_Keys_StepAndTurn_BumpOnAWall_AndAShutWaySays`: strips 1 to 4 turn and step by a sixth, the key map gives the same four strips, a bump on rock, "the way is shut" said at the Ash cut; `FootprintStones_ShowUnderground_AndEarly_HideByTheSetting_AndFitTheBand`: the rule, the setting, four stones in the band inside the notch safe area at three ratios, zone tag). The save and load mid-interior and "Lead me out" tests were already there and pass.
- PlayMode `ExpeditionTests` **13 / 13** (4 new: `Pedestal_OffersMirasAct_AndTheSecondTapSendsOneCommand` (preview sends nothing, confirm sends one `AcceptLead` for the lead, a burst after sends nothing, a thing with no errand only reads), `CompanionCard_NamesTheLiving_AndTheFallenAreRemembered`, `LeadMeOut_FromACompanionCard_ReturnsToTheSurfaceAndTheHubView` (card preview, confirm, on the surface at the glade stair, walking view, sky light back), `Pouch_NamesTheCarriedCrystals_AndTheJournalBookOpensTheJournal`).
- `WorldUiTests` 6 / 6, `GodBoardTests` 10 / 10, `GodBoardVillagerTests` 7 / 7.
- **`DanceTests` 14 / 15: `MidSongPause_FreezesTime_ResumesAfterFourCount_ScoringExact` FAILS** (song time moves 1 to 4 ms during the four-count; `1e-9` tolerance). It failed the same way four times, also with the footprint stones switched off, so it is not the stones; no rhythm file changed. It passed for WU-5. Not investigated further (three editors were open on this machine; a frame-timing flake or something on the merge base). The next agent should run it on `a60cdad` to tell.
- Not run: `dotnet test DotNet` (no sim change), the full PlayMode suite, a player build, the phone.
- consoleErrors 0 from game code at each compile.

## Captures (local, not committed): `Evidence/worldui/`

At 1920 x 1080, 1024 x 768 and 844 x 390 (simulated notch; every PNG header checked against its name): `cry_corridor_{ratio}` (an interior corridor with the footprint stones), `cry_pedestal_preview_{ratio}` (at the pedestal, the preview card with "Ask Mira to take it?"), `cry_companion_card_{ratio}` (Garrick's card at the home camp, Mira's pouch and book on her), `cry_journal_{ratio}` (the journal open). Made with new `WorldUiCaptures` commands (`devhist`, `campaign [root]`, `speed`, `inside`, `before`, `tapact`, `tapmate`, `facemate`, `tappouch`, `tapbook`, `closemap`, `stones`, `cry`). The corridor shots at 1920 and 1024 were taken before the ledger fix; the pedestal shots were retaken after it.

## Not done / open

1. The stones' setting has no Esc-menu entry (WU-0 "Clear view" settings area); `FootprintStones.Mode` is the hook.
2. The companion card is shown on the surface (the camp) in the captures; the underground "lead you out" preview was tested but not captured (companions walk in the interior only with the party).
3. A companion or the pouch is a small target on Mira's body: the pick boxes are 0.6 to 0.7 m, but the 7 % screen-height rule is only met by `PickEnlarged` when the ray hits nothing, and a ray that hits Mira's own collider wins. The pouch and book are best tapped from under 4 m.
4. Doors, stairs and exits keep their old bubble ("Go up / Go down / Lead me out"); only non-way markers use the ledger. The Esc-menu save stays as is (CRY-CAMP asks for nothing new).
5. The journal is still the map's side column (small text at 844 x 390); a true in-world book page is not built.
6. Tap-to-walk underground (WU-1, `PlayerWalker`) is not touched.
7. `DanceTests.MidSongPause...` failure above.

## Next steps

Run `MidSongPause` on the merge base; add the Clear-view / stones setting to the menu; capture the underground companion preview with the party in the interior; consider making the pouch and book registered hit targets that win over Mira's body.
