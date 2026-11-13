# Play guide

WorldMVP is one small game made from four of your games. You walk round a village, duel a wizard, play a rhythm party, build a road economy and then grow it into a strategy game with cards, cities, demons and soldiers. A full game takes about 15 to 25 minutes.

## How to start

1. Open the folder `C:\Users\joesa\Documents\Cursor\RhytmDrama\WorldMVP\Builds\Windows`.
2. Double-click `WorldMVP.exe`.
3. The game opens full screen at your desktop size. Press Alt+Enter to switch to a window and back.

If there is no exe, open the project in Unity 6000.3.28f1, open `Assets/Scenes/World.unity` and press Play.

Use headphones or speakers for the rhythm party.

## Controls

The same list is in the game: press Esc, then "Controls" or "How to play".

**On foot (first person)**
- W A S D walk, Shift run, mouse to look.
- E talks to people and uses things (a yellow prompt shows what).
- Left click inspects what is under the crosshair.
- Tab switches to God View and back. You stay exactly where you stood.

**God View (overhead map)**
- W A S D or hold the middle mouse button and drag to move the map. Mouse wheel zooms.
- H goes back to the village. M shows the whole island.
- Left click selects. Right click puts the tool back to Inspect.
- Tools on the bottom bar, with their keys: B Build, R Road (X switches to removing roads), V Inspect, P Problems, G Wares, T Market, C Cards, F City, J Hero, N Army, O Wonder, L Go here, and End Round.
- **Go here (L)** is fast travel: click open ground and you are standing there, back on foot. The island is about 950 m across, so use it.

**Anywhere**
- F5 saves. F9 loads. There is one save slot.
- Esc opens the pause menu (or closes a dialogue or panel). The pause menu has Save, Load, New game and Quit. Load, New game and Quit ask you to click twice.

**Ward duel with Mira**
- 1 2 3 4 add essences to your guess. Backspace removes one.
- Enter locks your own Ward at the start. Then Enter or Space casts a guess.
- At the end: R for a rematch, Enter or Esc to go back.

**Rhythm party with DJ Sol**
- D F J K are the four pads. Press when a note reaches the line. Hold the long notes.
- In the last block, Left arrow picks the bar and Right arrow picks the balcony.
- [ and ] move the timing by 10 ms if the notes feel early or late.
- At the end: R to try again, Enter or Esc to go back.
- The music cannot pause. During the song, Esc once shows a warning; Esc again leaves the party.

**The bell**
- The bell in the square starts the next round, like the End Round button. It only works after the harvest season: 60 seconds after the round began. The button shows the seconds left.

## Your first 15 minutes

1. **Look around.** You start in the village square. Mira stands to your left. The warehouse door glows ahead and you can hear music.
2. **Duel Mira (2 to 4 minutes).** Walk up to her and press E, then pick the duel. First set your own Ward: three essences with keys 1 to 4, then Enter. Then guess hers. After each guess you see three numbers: fractures (right essence in the right place), echoes (right essence, wrong place) and fades (wrong). She guesses yours at the same time. Break her Ward first to win. Win or lose, you return to where you stood. After three finished duels a fourth essence (Light) joins.
3. **The warehouse party (2 minutes).** Walk into the warehouse and talk to DJ Sol behind the decks. A count-in plays, then notes fall on four lanes. Hit them with D F J K. There are four blocks. Pass three to win the crowd. In the last block pick the bar (Left) or the balcony (Right); if you pass that block, that area opens. Walk into it.
4. **Bram's commission (4 to 6 minutes).** Walk back to the square and find Quartermaster Bram by the storehouse. He wants six buildings: forester, woodcutter, sawmill, clay pit, charcoal kiln and brick kiln, all joined to the storehouse by roads, then 18 planks and 18 bricks in the storehouse. Press Tab. Use Build (B) to place a building on a glowing spot and Road (R) to join it up. Carriers walk materials along your roads; follow one on foot if you like. The line under the top bar tells you the next step. When the goods are in, talk to Bram again.
5. **Rounds.** Ring the bell or press End Round. Each round draws numbers; hexes with those numbers give a batch of goods for your buildings to harvest. The first round that starts after the commission is done begins Age I (after six rounds it begins anyway). There are 3 ages of 3 rounds.
6. **The draft.** Every round of an age starts with one card pick. The game pauses and the draft opens (in God View; on foot, talk to Scribe Pell or press Tab). Take one card. Build it later at one of your cities from the Cards tool, or discard it for coin.
7. **Cities.** Use City (F). Glowing corners are legal: green means you can afford it now. A city needs a road to its corner and cannot sit next to another city.
8. **Demons and the hero.** Demons appear on flags at the start of each round. They block roads and buildings next to them. Too many on one place breaks out: that is a breach. Use Hero (J) to walk the hero onto them; it clears demons on the way.
9. **Soldiers.** Build a barracks, give it power and supplies, and queue soldiers in Army (N). Send a squad to attack the rival outpost to the north-west, or keep it home to stop raids. At the end of each age your shields are compared with the rival's.
10. **How it ends.** First to 18 points wins. 5 breaches lose. Otherwise, after the last age, the most points wins. The end screen says why and shows both scores. New game starts a fresh island.

The top bar always shows the age, the round, the points (you against the rival), breaches, coin and power. The line under it says what to do next.

## What is rough

- Nobody has played it by hand yet. Expect places where you do not know what to do next.
- The characters are simple placeholder figures.
- Nobody has listened to the audio on this build. The rhythm timing may feel early or late on your machine; use [ and ].
- The draft opens by itself only in God View.
- Late in the game, demon labels can stack up round the village in God View.
- Saving is not possible during the duel or the party. Loading takes you back to the saved moment; the duel or party you were in is not saved.
- Go here only works on open ground inside the board; buildings, rocks and mountain peaks refuse it.
- The window starts full screen. Alt+Enter gives a window.

## Sending feedback

Write down the time and what happened for anything that confused you. That is the most useful feedback. For each part (duel, party, commission, strategy, save) say:
- what you expected and did not get,
- where you stopped knowing what to do,
- anything that felt mistimed (rhythm: early or late, and roughly by how much) or unfair (duel),
- anything you could not read,
- the three things you would change first.

If something breaks, send the log file too: `C:\Users\joesa\AppData\LocalLow\Joe Salmon\WorldMVP\Player.log`. Your save is next to it (`worldmvp_save.json`).
