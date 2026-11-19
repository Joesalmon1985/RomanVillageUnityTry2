# God board UI: layout brief (G1-6b)

Written 19 Nov 2026 from reference screenshots the owner shared of a commercial digital hex-trading board game, and from our own captures of the current god board (G1-5/G1-6). The screenshots are not in the repo and must not be copied: we take **layout patterns only**. All art, names, icons and wording stay ours (`CLAUDE.md`: reference games are inspiration for mechanics only). The rules that differ from that game still hold: no dice, no numbers or odds on hexes (AB-1), no development cards, no robber.

Target: landscape phone, 844 × 390, touch first, mouse too.

## What the reference does well, and what we take

| Pattern in the reference | Why it works | Our version |
|---|---|---|
| The board is large and centred; every control hugs a screen edge | The board is the game; nothing permanent covers it | Board fills the middle. Only edges carry UI. Today's right-hand column of five text buttons is removed (see below). |
| One panel per player across the top: face, name, a big points badge in the player's colour, small counters underneath (cards in hand, other counts) | You read the race at a glance | Four people panels across the top in each people's banner colour: emblem, name, **large points badge**, small icons for cards held, road length, soldiers. Longest road shown as a marker on its holder. The active people's panel is lifted and ringed; the ring is also the hourglass (it drains round the badge). |
| The player's hand is a fan of picture cards at the bottom centre, each with a count badge | Your resources are the thing you look at most | Five goods cards (wood, clay, grain, sheep, stone) as a fan at the bottom centre, picture plus count badge; empty goods dimmed. Cards lift when a trade or discard picks them. |
| A build-costs card sits at the bottom left, drawn as pictures | You never have to remember costs | A costs card at the bottom left: road, settlement, city as rows of goods pictures. Rows you can afford light up; **tapping a row enters that build mode** and lights the legal spots on the board (replaces hunting for spots). |
| Spare pieces are shown as little stacks beside the table | Piece limits are visible without text | Small stacks of road / settlement / city pieces in the player's colour beside the costs card, one stack per kind, shrinking as they are used. Replaces "Roads left 14 / Camps left …" text. |
| One big round action button at the bottom right | One obvious "what next" | One big round **hourglass button** at the bottom right: it shows the sand (HG) and tapping it ends the turn (HG-4). No separate "End turn" text button. |
| Trade is a modal: two rows of the five resource cards (top: what you give, bottom: what you get), counts on each card, a Player / Bank switch, round cancel and confirm buttons | Fast, symmetric, readable | Same shape in our art: give row and get row of the five goods cards, tap a card to add one, a small minus to remove; a switch for **Peoples / Bank or port** (the port rate shown as a picture, e.g. three wood → one stone); round ✗ and ✓. Offering to one people or all is a row of people emblems above the ✓. |
| A choice modal with a strong title band and big targets (e.g. pick a player) | Clear forced decisions | The same style for **discards on a 7** (pick the cards, timer bar in the title band) and for any "choose a people" step. |
| A collapsible log panel with tabs at the bottom left | History without clutter | The event feed moves from the top left to a collapsible log tab at the bottom left (closed by default; a small badge counts unread lines). Short toasts still appear briefly at the top centre for big moments (epidemic, crystal reward, war). |
| A card that pops up large when played, with its picture and text | Events feel like events | Big moment cards for: "The gods are angry." (epidemic), a crystal's free city, the prince's advice, a settlement burned. No numbers. |
| Harbours as round tokens on the coast with a resource picture | Trade rates are on the board, not in a menu | Port tokens in the top view at each jetty: the good's picture for 2:1, a ring of goods for 3:1. |
| Small round icon buttons (settings, etc.) | Secondary actions stay out of the way | Small round icons along one edge for: return to the world, hint (prince), plague overlay, zoom in/out, settings. |

## What we do NOT take

- Number tokens on hexes, dice, odds pips: hidden by our rules (AB-1). Abundance shows production.
- Development cards, the robber, stealing.
- Any art, portrait, card frame, icon, colour scheme, font or name from the reference.

## Layout at 844 × 390

```
+-----------------------------------------------------------------------------+
| [People 1][People 2][People 3][People 4]                    (o)(o)(o)(o)(o) |  top: people panels; right: icon buttons
|                                                                             |
|                                                                             |
|                         BOARD (centre, unobstructed)                        |
|                                                                             |
| [log tab]                                                     [offer cards] |
| [costs card][stacks]        [ hand: 5 goods cards fan ]          ( HOURGLASS)|
+-----------------------------------------------------------------------------+
```

- Modals (trade, discard, game over) open over the board centre and dim it.
- Offer cards stack above the hourglass on the right, newest on top, each with accept ✓ / decline ✗ and its expiry as a draining edge.

## Done when

- Everything above works by touch and mouse at 844 × 390, with captures of: a normal turn, build mode from the costs card, the trade modal, an offer card, the discard picker, an epidemic moment card, game over.
- No numbers or odds appear anywhere outside F1 tools.
- Existing PlayMode tests (`GodBoardTests`, `GodBoardVillagerTests`, `PlagueViewTests`) still pass, updated only where a control moved.
