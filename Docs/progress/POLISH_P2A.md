# POLISH P2a: board ledge, rhythm props, crystal dungeon

20 Nov 2026, branch `godboard/polish-games-a` (from `2f033f4`). Presentation only: no sim file, no rule, no command changed. `Assets/World/Game` still never mutates sim state. Not touched: `Actors/*`, `CollectWalkers`, envoy animation, `Historic/*`, `War/*`, `Encounters/Duel*`, `Plague/LodgeTable*`, `Council/*`.

## Board (`Board/GodBoard/*`, `UI/LedgerCard.cs`)

- **Mat lies on the ledge.** Tapping the trader no longer hangs a cloth over the island. The ledge slides up by one strip (0.3 s, `GodBoardHud.MatRise`) and the barter mat lies along that strip at the top of the stone; the board camera already fits the island above the band, so it refits shorter and nothing is covered. Left to right on the cloth: the goods to give (moved from the basket), the five goods to get, the deal stone, then the peoples. The strip folds away and the ledge slides back when the trader is tapped again.
- **Offers to the peoples are on the mat** (`mat-people:P`, `mat-people:all`): a people's emblem (banner in their colour) per other people plus one for all. Tap a basket to give and a good on the mat to get, then an emblem: the tablet previews "Make this offer to X?" ("You give 1 log and ask 1 stone."), the second tap sends one `OfferTrade` (1 for 1, `To` = that people or -1). The sim's own refusals (for example "will not trade with the leader") are shown as before. The old IMGUI trade window is no longer reachable in the world-first view (Clear view keeps the old HUD).
- **Jetties are tappable.** A tap near a jetty (planks, boat or token, enlarged to the shared tap size) opens a tablet "A jetty for sheaves" with the exact rate in words and as a picture row of goods (chips of the good, an arrow, one four-colour "any other" chip; new `LedgerContent.PicGood/PicCount`, `ILedgerPicture`) and whether you hold a camp there. It only tells; it sends nothing. Legal build spots win over a jetty. (`GodBoardDirector.PortAt / PortInfo / ScreenOfPort`, `GodBoardHud.TapJetty`.)
- **Tablets never open over the ledge.** `LedgerCard.FloorTop` (the ledge's top edge while it is up) makes the tablet of any ledge object open above that edge, centred on the object; if too tall it widens first.
- **Footprint stones are hidden during the camera rise** to the board, while it is up and while it falls back (`FootprintStones.ShouldShow`; WU-8 open problem 3).
- Fix found on the way: mat items now stand at their own slab depth (they were half buried when the ledge moved).

## Rhythm (`View/Camp/DanceStageProps.cs`, `StandingBell.cs`, `RhythmPartyEncounter.cs`)

- **Stage lit at night.** Performers, torch poles, bell and seat were unlit flat shapes; they are now lit materials, with a warm stage light on the dancer's side so the performers' fronts and the drums read, a point light on each torch (embers when unlit, gold when kept, dull red when missed) and a faint ember on every unlit torch so the four torches are found in the dark.
- **Paused label vs tally.** `RhythmPartyEncounter.PausedRectClearOfLedger` moves the "Paused" panel above, below or beside the open ledger tablet (Sol's tally) so it is never behind it at 844x390.
- **Standing tuning bell** (`StandingBell`, built next to the home `DanceCircle` by `CampInstaller` and every spire's circle by `SpireDirector`): a post, beam, swinging bell and a lantern. Tap: tablet "The tuning bell ... Tune your drum timing?"; second tap starts the eight clicks (dsp clock); tap along with a click, a touch, D F J K or Space; the median offset is kept in the same PlayerPrefs key as the dance's own bell and `[ ]`. World taps are off while tuning (restored after). It hides while a dance runs (the encounter's own bell stands in the same place) and when its circle is retired. (The home circle retires when the first spire rises, so there the spire's bell is the one.)

## Dungeon (`Interiors/InteriorProps.cs`, `Expedition/*`)

- **Stairs and exits use the ledger.** New `InteriorWay` (`IWorldAction`, primary target): first tap previews "Go down?" / "Go up?" on the tablet (a shut way says "Shut fast"), second tap walks to the way (same walk as before; no sim command). The old speech-bubble choice is no longer reached for ways. Doors and other markers already had the `MiraActThing` preview/confirm from WU-6.
- **Pouch and journal easier to tap.** Their pick volumes are 1.3 m and 1.2 m (were 0.7 and 0.6), and tapping Mira's menu offers "Show me your pouch." when a reply slot is free (the bubble shows three; her journal stays "What do we know?").
- **Journal as a book.** When the journal is unfolded it is a two-page book over the map inside the safe area: left page status, crystals and where next; right page all entries; body text `Screen.height * 0.042` clamped 15 to 30 px (16 px at 390); a drag scrolls a page; a Close button of at least the tap size. Folded, the old side column is unchanged.

## Tests

See the table at the end (filled from the actual runs).

New tests: `GodBoardLedgeTests` (mat lies on the ledge and the ledge slides; mat offers to peoples preview then send the right `OfferTrade`, also to all; jetty tap shows its rate as picture chips and sends nothing; ledge tablets open above the ledge; footprint stones hidden during the rise), `DanceTests` (standing bell tapped outside a dance tunes and keeps the offset, hides in a dance; Paused label clear of the tally card, props lit), `InteriorTests` (stair preview then confirm, no bubble), `ExpeditionTests` (book view, larger pouch and book volumes; Mira's menu list updated).
