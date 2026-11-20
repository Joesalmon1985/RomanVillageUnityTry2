# Play guide

WorldMVP is now a small prehistoric island. Two peoples, yours and the Ash Clan, each start with one camp. You do not command anything: you are a viewpoint that walks the island, and the prince and his villagers come to you with what they want to build. Nothing is built for your people until you walk to the spot and press the ground there. That press is a spell. It is free. If you ignore the prince, the Ash Clan outgrows you and overruns your camp. A game takes about 13 to 20 minutes and starts at night.

## How to start

1. Open the folder `C:\Users\joesa\Documents\Cursor\RhytmDrama\WorldMVP\Builds\Windows`.
2. Double-click `WorldMVP.exe`.
3. The game opens full screen at your desktop size. Alt+Enter switches to a window and back.

If there is no exe, open the project in Unity 6000.3.28f1, open `Assets/Scenes/World.unity` and press Play.

Use headphones or speakers for the dance.

## Controls

The same list is in the game: press Esc, then "Controls" or "How to play".

**Walking** (Dungeon Master style: you move one 5 m step at a time and turn in sixths of a circle)

| Action | Keys |
|---|---|
| Step forward | W, Up arrow, Numpad 8 |
| Step back | S, Down arrow, Numpad 2 or 5 |
| Turn left | A, Left arrow, Numpad 4 or 7 |
| Turn right | D, Right arrow, Numpad 6 or 9 |

Hold a key to keep going. You cannot walk into a camp's hearth, a building, a tent, the dance fire or a mountain crag; you bump and stay. There is no mouse look.

**Pointer.** The mouse cursor is always free.

- Click a person to talk to them, or to get "Follow" (carriers, a villager who is walking).
- Click a thing (a hex, a building, a carrier, a camp) and a small label says what it is.
- Click the glowing ring on the ground to cast, when you stand within 15 m of it. Further away a bubble says "Walk closer to cast."
- Right click stops following or auto-walking. So does any movement key.

**Speech bubbles.** People talk in bubbles over their heads. Replies are the lines under a bubble: click one, or press 1, 2 or 3. If someone asks you something from outside your view, a box at the screen edge says who is calling and which way to turn.

**Other keys**

| Key | What |
|---|---|
| Tab | The map of the island. Click a spot, close the map, and you walk there. |
| H | The HUD: game time, the last roll, both peoples' strength, the stock of your nearest camp, what your people want built. Off at the start. |
| Esc | Pause menu: guide, save, load, new game, quit. |
| F5 / F9 | Save / load (one slot). |
| F1 | Developer tools (an overhead camera and build tools for testing). Not needed to play. |

## What happens

**The dice.** Two dice roll by themselves every 10 seconds. Every hex has a number. When its number comes up and a camp stands on one of its corners, young stock appears there: saplings in forest, seedlings in fields, lambs on pasture, clay in the hills, stone in the mountains. Young stock takes about 75 seconds to grow. Only a collecting building of a camp next to that hex can gather it. 6 and 8 come up most often (they are red on the map).

**Camps.** A camp sits on a hex corner and has its own store. Carriers walk goods between its buildings and its store on short paths. Main roads run along hex edges and join camps; porters carry loads along them. A camp can later become a city.

**The prince.** He walks up to you at the start and asks you to come and see a woodcutter's hut. After that he, Osric the sawyer and Wenna the merchant come to you with one proposal at a time, the best first. Your answers:

- **Show me** (or "Lead the way"): the villager walks to the spot and you follow automatically. At the spot they say why, and a gold ring glows on the ground. Click inside the ring to cast.
- **Not this**: that proposal is dropped for a minute and the next best is offered.
- **Later**: they come back to it in a minute.
- You can also just walk to the ring yourself. The villager sees you going and meets you there.
- If you do not answer, they ask again after about 10 seconds while you stand near, and less often if you walk off. After three unanswered questions they go quiet for a while. Click the villager to hear the proposal again at once.

Casting only starts the work. Carriers still have to bring the goods, and the building takes time to go up.

**Demons.** After the first few minutes a roll of 7 can put a demon on one of your camps or roads. A camp with a demon stops gathering; a road with a demon is closed. The prince proposes a banish; walk there and press the blue ring.

**Winning and losing.** Each camp counts 1, each city 2, each fed warrior 1. If one people is 3 or more stronger than the other, a war camp appears and a 90 second warning runs; if the lead still holds at the end, that people overruns the other and the game ends. No warning starts in the first 8 minutes. At 20 minutes the stronger people wins. Press H to see both strengths.

## Mira's duel

Mira stands by the home camp. Click her and choose "Practice duel". You each hide a Ward of three essences and take turns guessing the other's. After each guess you learn how many parts are right and in the right place (fractures), right but in the wrong place (echoes), and wrong (fades). Break her Ward before she breaks yours. Keys are shown on the duel's own panels: 1 to 4 pick essences, Backspace removes one, Enter locks your Ward and casts a guess. The world waits while you duel.

## The dance

Sol the drummer stands at the dance circle (the fire with four drums, in the fields beside the home camp). Click him and choose "Dance at the fire". You stand facing the fire with four drums in front of you: D, F, J and K from left to right. An ember flies from the fire to a drum, and a ring closes on its skin; strike the drum as the ember lands. Hold when a stream of embers keeps coming. There are four blocks. Sol calls each block as kept or missed in his bubble; the fire jumps when you keep one and sinks when you miss. In the last block, choose an ending: Left arrow (or click left of the fire) for the fire leap, Right arrow (or click right of it) for the spirit call. Press H during the dance if you want the score strip and key hints. The world waits while you dance.

## Saving

F5 saves and F9 loads, or use the pause menu. There is one slot. You cannot save during the duel or the dance. A loaded game comes back at the same game time, so the time of day is the same too.

## How to play the Earth route (the crystal campaign, first slice)

A normal launch is the island game above. The crystal campaign is reached by a developer start for now (the prince's handover scene that leads into it comes later).

**Start it.** Either:

- In the game press F1, open the tool "Campaign" (key K in the God View) and press "Start the campaign (authored_v1)". The world is rebuilt on the developer start `dev.historic` (a few seconds) and the campaign begins. Press F1 again to walk.
- Or start the exe from a command prompt: `WorldMVP.exe -start dev.historic -campaign authored_v1`.

**What to expect.** Mira comes to you with six companions and tells why the four crystals matter (about 45 seconds; click her to skip to her question). Answer "Lead the way" and follow her: to the boundary stone in the Stonemark Glade, down the stair into the Old Quarry, along the survey marks to the Root Chamber, where the Deepstone (an amber and moss crystal cluster) rests on its pedestal. She carries it out and to the Rooted Tower; answer "Set it" and the tower lights. Underground, Tab shows the cave map with the party and the way out, and any party member offers "Lead me out". Tap Mira at any time for "What do we know?" (the journal, also on the Tab map; J opens it) or "Go on without me".

If the Ash Clan's runners get there first (they set out when the campaign starts; the march takes about 3:30 to 5:30), the pedestal is bare: Mira follows their tally into the Tally Vault, where Dolm the Tally-Keeper guards the crystal. At his threshold she asks whether to fight. The duel is Mira's Ward against Dolm's; if her Ward breaks, the companion named at the threshold (Garrick first) stays to hold the way out and does not come back, and the party retreats to the survey gallery to try again. Win, and the barred door opens onto the Ash cut, a shorter way up.

**Developer buttons** (F1, "Campaign", while a campaign runs): send the Ash expedition ahead one step (twice puts the Deepstone on their altar), move Mira on to her next marker at once, kill or revive a companion. The game saves a checkpoint to the one slot the moment a duel ends; F5 and F9 work as usual outside a duel.

**What is rough here.** Only the Earth crystal is written; the other three sites are placeholders. Companions, Mira and the keeper are block figures. The duel is played with the duel panel's buttons (or keys 1 to 4, Enter). Nobody has played this route by hand yet: it has been driven by scripts that tap the replies.

## What is rough

- Characters are simple block figures. There is no music outside the dance.
- Nights are dark away from fires and your lantern; building sites are hard to read at night from a distance.
- The first ring is behind you when the game starts. Answer the prince and he leads you to it.
- Walking between spots is 50 to 100 m each time. The Tab map can walk you there.
- The Ash Clan is weak against a player who answers quickly; a slow player can still lose.
- The dance has no lanes: most notes are on the two outer drums.

## God board smoke runs

Packaged AutoPlay scripts for the god board (PRD_GODBOARD 12.3). Build with `AgentScripts/BuildWindows.cs`, then run each; it logs `[AutoPlay] <name> OK` and exits 0, or `FAILED` and exits 1. Each takes 6 to 20 seconds (it pauses the clock and steps it by hand).

```bash
Builds/Windows/WorldMVP.exe -autoplay <name> -novsync -screen-fullscreen 0 -screen-width 844 -screen-height 390 -logFile Builds/logs/<name>.log
```

| Script | What it checks |
|---|---|
| `board_first_round` | dev.godboard, walk to the altar, sacrifice, setup camp and road (each by a ghost preview, then a confirming tap), AI seats place, first turn, a production pulse, leave (table pauses and stays frozen), come back (resumes) |
| `board_trade_discard` | WU-2 ledge: the trader unrolls the barter mat and a bank trade changes the hand; an AI offer arrives as an envoy (tap to see, tap again to accept); a 7 from the table's own dice forces a discard that is done by the sack (basket taps, then two taps on the sack) |
| `plague_epidemic` | a 7 brings an epidemic (feed line, sky, demons), the overlay shows 48 nodes, a banish on foot removes a demon |
| `war_burn` | a soldier reaches an unguarded camp, it burns, a ruin shows, the owner's table points drop |
| `board_to_historic` | dev.godboard.eve: the switch plays, temples fall, castle scaffolds start, trees render, the player walks |
| `world_ui_tour` | WU-8: from the hub of dev.godboard, enter and leave all six destinations in a row (temple to god board, council, rhythm, healer's lodge, duel ring, Mira's crystal expedition: down the glade stair, Lead me out, back on the hub cell) and check the same hub cell and heading each time, the sim state kept, and no wireframe type in the player |
| `phone_frametime` | WU-8: 60 s of frames on dev.godboard seed 7 (use the phone-sized window); logs average fps and ms and the worst frame |

Set-up that would need luck or minutes (cards in hand, an AI offer, a points lead, extra soldiers) is prepared by `World.Sim.Table.SmokeSetup` in the sim; everything else goes through commands and the views' public methods. `first_scene` is the older smoke run.
