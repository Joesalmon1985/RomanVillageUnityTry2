# Testing plan for the owner: the hub and every minigame

A hands-on checklist to try each minigame and its world-first UI. About 60–90 minutes for everything; each section stands alone. For each one: how to get there, what you should see, what to try, and what to tell us.

Written 20 Nov 2026 for branch `godboard/main`. Background: `Docs/WORLD_UI_PLAN.md` (what each place is meant to show), `Docs/progress/STATUS_2026-11-20.md` (what is built), `Docs/progress/DECISIONS_2026-11-20.md` (the choices made for you).

---

## 0. Setup (5 minutes)

**Run the game**
- Easiest: the packaged build `..\wt-gb\Builds\Windows\WorldMVP.exe` (rebuilt at the end of this session).
- **Since 21 Nov 2026 a plain launch (double-click, no arguments) and Esc → New game start the god board hub** and open on the vista over the camp. `-start dev.godboard -seed 7` still gives the same hub on the fixed seed. The old prehistoric economy (the prince's woodcutter's hut) is only the developer start `-start dev.legacy`.
- For a phone-shaped window, start it from a command prompt in that folder:
  `.\WorldMVP.exe -seed 7 -screen-fullscreen 0 -screen-width 844 -screen-height 390` (in PowerShell the `.\` is needed; in the old Command Prompt it can be left off)
- Or in Unity: open the folder, open `Assets/Scenes/World.unity`, press Play.

**Your music at the spires (optional)**
- Put a file `config.json` next to the `Assets` folder (or next to the exe) containing:
  `{ "rhythmPacksDir": "C:\\Users\\joesa\\Documents\\Cursor\\A1-before\\songs\\packs" }`
- Without it every spire plays the warehouse track.

**The touch grammar everywhere**
- Tap (click) a person or thing once: it **previews** (a ring, a short card: "Sacrifice a lamb?").
- Tap it again: it **does it**. Tap anywhere else: cancel.
- The game opens on **the vista**: the whole camp from above at night. Tap a part of the screen once: the place drawn there lights up and a card names its game. Tap it again: you go there and its game starts. Esc (or a tap on the sky) brings you back to the vista.
- Tap plain ground: you walk there (a footprint ring marks the spot). Drag sideways: you look round (also while someone leads you or talks to you). There are no step buttons on screen any more.
- Keys still work: arrows/WASD to step and turn, E to use the nearest thing (twice to confirm), Esc for the menu.

**Clear view**
- Esc menu → "Clear view" (or H on the board) brings back the old on-screen panels (board HUD, plague buttons, duel panel) for comparison or if a world object is hard to read. One switch covers all of them.

**Developer helpers** (only where a step says so)
- F1 opens developer tools: "Starts" (jump to a start), "Council" (make the council available), "Campaign", "Zones" (outlines of your wireframe zones over the scene — check they line up, then switch them off).

**While you test, note:** anything you cannot find, anything you tap that does nothing, any number you need but cannot read, anything that looks wrong at the phone size.

---

## 1. The hub at night: the opening vista (10 minutes)

**Get there:** just start the game (or `-seed 7` for the fixed island). You open at night looking down over the home camp.

**You should see, all at once:** the temple (blue dome), the council's lookout tower with villagers waiting, the dance circle with its fire and drums, the healer's thatched lodge, Mira's expedition camp at the foot of a hill with a lit cave mouth, and the fenced practice ring with sparring villagers. Rolling knolls behind. No buttons, labels or outlines. The prince welcomes you and each place lights up in turn as he names it.

**Try**
1. Tap anywhere on the screen once: the place in that part of the screen lights up and a card says what it is. Tap a different part: the other place lights instead. Nothing starts.
2. Tap the same part again: the camera glides down to the place and its game starts (the temple's board rises; the dance begins; Mira or the healer talk to you; the referee calls Mira over; the tower opens the council, or the steward says no dispute is called yet).
3. Leave the game (Esc in the game, or "Leave"), then press Esc again or tap the sky: back to the vista.
4. From a place, tap the ground to walk around; drag to look. While the prince or anyone leads you, a drag still turns your view.
5. On the keyboard: A / D in the vista light the places left to right, E or Enter goes, W steps down into the walking view.

**Tell us:** Is each place obvious from the vista? Do the screen parts feel right (does a tap pick the place you meant)? Is the glide down too slow or fast?

---

## 2. The hex board game at the temple (20 minutes)

**Get there:** from the hub, walk to the temple. Tap the altar (or the doorway, or the lamb pen) once to preview "Sacrifice a lamb?", again to confirm. The camera rises to the board.

**You should see:** the island from above, with no numbers or dice anywhere. Rich-looking hexes (thick forest, big mountains) produce more often. Along the bottom: a torch-lit stone ledge with:
- five baskets (your hand: logs, bricks, sheaves, fleeces, stones) that fill as you gain cards;
- the builder's bench with a road plank, a hut and a walled hut, and your spare pieces stacked behind;
- four banner stakes (one per people) with point stones; the longest-road cord on its holder;
- the trader, the chronicler (the event log), the prince (hints), a demon-ward totem, the lamb stone (go back), and a big hourglass.

**Try**
1. **Setup:** tap a glowing spot for your second camp: a ghost hut appears. Tap again to build. Same for the road.
2. **Exact numbers:** tap a basket, a banner stake, the chronicler. Each opens a tablet with exact counts (your cards, everyone's points, costs, what you can afford).
3. **Build:** tap the hut on the bench to choose that build mode; only those spots light up. Build one with the two taps.
4. **Bank trade:** tap the trader; the barter mat unrolls; put 4 of one good on it and choose what you want; confirm.
5. **Offers:** when an AI people's envoy walks onto the ledge with a bundle, tap them to see the offer, then accept or wave them off.
6. **End turn:** tap the hourglass once (it tips), again to end the turn.
7. **A 7:** when the gods are angry (dark sky, thunder), if you hold more than 7 cards, put the right number into the sack before its small hourglass runs out.
8. **Leave and return:** tap the lamb stone (twice). You are back at the altar; the turn is frozen. Sacrifice again: it resumes where it stopped.
9. Watch the island during turns: villagers walk out to the hexes that produced and carry goods home.
10. Press H (or Esc → Clear view) to compare the same information in the old panels, then switch back.

**Tell us:** Does the ledge read as objects on a ledge? Can you always find your hand, your points and the build costs? Is the AI's trading sensible? How long does a game feel?

**Known:** offers to other peoples still open the old trade window; jetties on the island are not tappable (their rates show on the trader's tablet); the barter mat hangs as a cloth over the bottom of the island at phone size; the win is at 10 points with the new balance (decision 1), so games are short.

---

## 3. The plague at the healer's lodge (10 minutes)

**Get there:** in a god board game, play a few rounds until demons appear (or wait for "the gods are angry"), then return to the world and walk to the healer's lodge.

**You should see** in the lodge yard: a carved map table of the island with 48 pebbles, trails, the four regions and demon figurines; a warning post with 8 lanterns (one lit per outbreak); a rate stick with a peg; four element vessels (cured, gone, or strengthened by the Ash Clan); a runner who comes in on each outbreak. No screen buttons.

**Try**
1. Ask the healer "How bad is it?": exact outbreaks, rate and demons by kind. Nothing about epidemics or cards left.
2. Walk to a shrine out in the world that has demons (they stand on the cairn). Tap it once (preview, the demons flinch), again to banish one. The charm in your hand dims while it cools.
3. Tap a shrine too far away: it should say why it refuses.
4. On the board, tap the demon-ward totem to show the plague overlay; tap again to hide it.

**Tell us:** Can you tell how dangerous things are from the lodge alone? Is banishing on foot worth doing? Is the table readable?

**P2b:** tap the table (or a pebble): the camera leans over it, the island fills the left of the picture, the ledger sits in a column on the right; walking or Esc stands you up where you were. Pebbles are enlarged tap targets (the nearest centre wins); figurines of mixed kinds stand apart; the tunnel is a violet-lipped groove.

**Known:** with the new balance (infections every third round) the plague is much milder than before.

---

## 4. The council at the lookout tower (15 minutes)

**Get there:** walk to the steward at the foot of the lookout tower. If he says "No dispute is called yet", press F1 → Council → make the village eligible, then tap the steward's bench (twice) to hear the dispute.

**You should see:** the camp's eleven gathering places from the tower, villagers standing as supporters, coloured rings under settled and listening people, your three representatives, and the proposals as notes. No dice, no sums, no numbers on places.

**Try**
1. Tap a proposal note once: ghost representatives appear at **both** places it would move, the listeners there turn. Tap a different note: the preview moves. Tap the same note again: both move together.
2. Tap the recorder's tablet: exact standings per candidate and place (temporary, kept, claimed).
3. Push your luck, then bank: tap the recorder's seal once (preview), again (keep the progress). Try another round until you bust and see only the unkept progress lost.
4. Leave by the tower ladder (twice), come back via the steward: your standing is as you left it.

**Tell us:** Is it clear which two places a proposal moves? Can you tell temporary from kept from claimed? Is the "same or different" listening game clear?

**P2b:** a proposal that moves two far-apart places now keeps both in the picture (the camera pulls back rather than pushes), each marked with a pale pillar and a larger ghost; the notes are cream tablets about a tenth of the picture's height. At 844 x 390 the two places are still small people on the ground; the pillars and rings are what you read.

**Known:** the people at a far place are small on a phone.

---

## 5. Rhythm at the dance circle and the spires (10 minutes)

**Get there:** tap Sol or a drum at the dance circle (twice). After the first crystal spire has risen, Sol offers to lead you to the spires instead; tap a spire's stage there.

**You should see:** four drums with a performer behind each, torches for the blocks of the song (gold = kept, ember = missed), embers flying from the fire to the drums.

**Try**
1. Hit the drums by tapping the drums themselves, by the touch strips at the bottom, and by D F J K. Try two at once and a held note.
2. Tap the log seat mid-song: it pauses. Tap it again to resume: four clicks, then the music continues exactly where it stopped.
3. Tap Sol after a block: his tally gives exact Perfect / Great / OK / Miss counts.
4. After a song, tap the tuning bell: tap along to eight clicks to calibrate. Play again and see if timing feels better.
5. Leave by the log seat (twice).
6. With `config.json` set: at spire 2 you hear the alien spaceship track; spires 3 and 4 play two gospel tracks.

**Tell us:** Do the hit windows feel fair? Is the pause and four-count right? Can you read how you are doing without numbers on screen?

**Known:** the tuning bell only exists during a dance; props are simple.

---

## 6. The crystal dungeon with Mira (15 minutes)

**Get there:** tap Mira at her expedition camp → "Set out for the crystals". Follow her to the Stonemark Glade and down the stair. (Faster: F1 → Campaign.)

**You should see:** underground, four footprint stones at the bottom for step forward, step back, turn left, turn right; Mira and her six companions; the pouch and journal on Mira's belt. No minimap.

**Try**
1. Step and turn with the stones, the touch band and the keys. Walk into a wall (bump), try a shut way (it says so).
2. Tap a pedestal or marker: Mira offers to act (preview); tap again and she does it.
3. Tap a companion: their card (name, role, service, alive or fallen).
4. Tap Mira's pouch (the crystals she carries) and journal (what you know, leads, where to look).
5. "Lead me out" (from a companion): you should come back to **the hub spot you set out from** (decision 11).

**Tell us:** Is moving and acting comfortable with taps alone? Is it clear what Mira can do for you? Does the race against the Ash Clan feel tense?

**Known:** the pouch and journal are small targets on Mira's body (best from close); doors and stairs still use speech bubbles; the journal text is small at phone size.

---

## 7. The Ward duel at the ring (10 minutes)

**Get there:** walk to the fenced duel ring; tap the referee or the ring (twice) → practice duel; Mira walks over.

**You should see:** essence stones at your feet to build a weave, orbs in your hand as you shape it, bead rails for casts left, tally sticks for the history (without ever revealing where the hidden Ward is), the referee, and a gate to leave.

**Try**
1. Tap stones to add essences; tap an orb to take it back.
2. Tap your palm once (preview the cast), again to cast. Watch the beads and tally sticks change.
3. Win or lose; tap Mira at the result for a rematch; leave by the gate (twice). You should be back at the ring where you stood.

**Tell us:** Can you play the duel without the old panel? Is it clear whose spell is being shaped?

**P2b:** the practice duel uses the wide over-the-shoulder camera (both wizards, posts and the stones row in frame); the referee's card is a strip along the top edge; the referee raises a hand at the start and points at the winner.

**Known:** the opponent's beads and tally stick are small on a phone (tap them to read).

---

## 8. The switch into Historic (10 minutes)

**Get there:** F1 → Starts → `dev.godboard.eve` (one moment before someone reaches 10 points), or `-start dev.godboard.eve`. Or play a board game to 10 points.

**You should see:** "The age has turned"; the board closes; temples collapse in dust; castles start at each people's best settlement with scaffolding, rising wood → brick → stone; forests become individual trees, woodcutters fell them, foresters plant saplings; carriers walk between buildings. The plague is gone.

**Try:** walk around for 10 minutes; check the council and the spires still work; look at a forest being cut and replanted.

**Tell us:** Does Historic feel like a new age? Is anything missing for the player to do?

**Known:** Historic is AI-only for now; carriers walk in straight lines; castles take about 13 minutes.

---

## 9. Phone size and screen shapes (5 minutes)

Run the exe at 844 × 390 (section 0), then at 1024 × 768 and 1920 × 1080. In each, glance at the hub, the board ledge and one minigame.

**Tell us:** anything cut off, too small to tap, or hidden behind something.

---

## 10. Optional: the automatic smoke runs

From a command prompt in `Builds\Windows`, e.g. `.\WorldMVP.exe -autoplay world_ui_tour -novsync -screen-fullscreen 0 -screen-width 844 -screen-height 390`. Each prints `[AutoPlay] … OK` in its log and closes. The full list is in `Docs/PLAY_GUIDE.md` ("God board smoke runs").

---

## Feedback template (copy per section)

- Place / game:
- Screen size:
- Could not find:
- Tapped and nothing happened:
- Needed a number I could not read:
- Looked wrong:
- Felt good:
- Change I want:
