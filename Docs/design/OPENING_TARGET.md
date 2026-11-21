# Opening screen target layout (owner approved, 21 Nov 2026)

The opening vista should put each place inside its own block of a 16:9 landscape screen. These are the tap regions: a tap anywhere picks the place whose block it falls in. The blocks are never drawn in the game.

```
+----------------+-----------------------+------------------+   top row: far away, smaller
| YELLOW         | BLUE                  | PINK             |
| Temple:        | Council longhouse     | Music camp:      |
| board game     | (largest, centre)     | rhythm           |
+------------------+------------------+---------------------+   bottom row: near, bigger
| RED              | TEAL             | GREEN               |
| Duel ring        | Healer's lodge:  | Crystal cave:       |
|                  | plague           | dungeon             |
+------------------+------------------+---------------------+
          (player start / campfire in the near foreground band)
```

Approximate fractions of the screen (x from left, y from top):

| Colour | Place | x | y | What must be visible (from the owner's art prompts) |
|---|---|---|---|---|
| Yellow | Temple (god board) | 0.00 to 0.29 | 0.00 to 0.44 | Squat stone-and-timber temple, stone forecourt, granary bins, timber and bricks, temple keeper, builders, a trader |
| Blue | Council | 0.29 to 0.69 | 0.00 to 0.44 | The biggest longhouse, dominating the centre; chief presiding, clusters of arguing villagers, steward outside |
| Pink | Rhythm (Sol's drums) | 0.69 to 1.00 | 0.00 to 0.44 | Hide-and-timber shelter, large drums, torches, dance circle, performers, bandleader |
| Red | Duel ring | 0.00 to 0.32 | 0.44 to 1.00 | Fenced practice ring, weapon and shield rack, sparring pair, referee, small audience |
| Teal | Healer's lodge (plague) | 0.32 to 0.63 | 0.44 to 1.00 | Thatched hut, herb rack, cot, healer tending a coughing villager, courier arriving |
| Green | Crystal cave (Mira's dungeon) | 0.63 to 1.00 | 0.44 to 1.00 | Low stone opening in ground-level boulders, crystal light spilling out, scout with lantern |

Ground: the owner chose (21 Nov) to **keep the low hills** and the rocky cave hill, even though the art prompts say "completely flat". The walkable hub ground stays level.

The owner's full art prompts are in the git-ignored folder `wt-gb\UI\` (never commit that folder: the repo is public and the prompts name commercial games).
