# PRD and MVP: characters and animation pass

Status: draft for the owner, 14 Nov 2026. Branch `docs/next-ages` (from `refactor/prehistoric` at `32d11ef`). Unity 6000.3.28f1.
Binding inputs: the owner's answers D14 to D17 and E18 to E20, and the limits in section G of `OWNER_ANSWERS.md` (local, not in the repo). Where they differ from the owner's visual-polish brief, the answers win.
Nothing in this document is game code. Every number in section 2 was measured from the supplied files on 14 Nov 2026.

## 1. Summary

The game is a prehistoric camp at night with firelight. Today every person is a capsule-and-box placeholder built in code (`PlaceholderActor`). The owner supplied 16 editable low-poly base meshes (14 humans, 2 demons) from a ChatGPT concept session. They are static: no skeleton, no skin weights, no shape keys, no clips.

This pass gives the game real characters:

1. One scripted Blender pipeline (kept in `Tools/characters/`) that rigs a character, adds a face rig, adds sockets, builds costume variants and authors original clips from keyed poses and procedural cycles.
2. A Unity side that imports them with scripts (no hand clicking), builds prefabs and an Animator, and plugs into the existing `ActorFactory` and `ICharacterActor` so every call site keeps working. The placeholder stays as the fallback.
3. A proof in the real game with three characters: Mira, the Prince and one duel opponent (the Road Hero model, as the Ash Clan champion). The owner sees the Prince walk up and greet, Mira cast, the opponent react and a Ward break, in actual night lighting, with a before and after video.
4. Only after that proof, the rest of the cast and crowd roles, then Historic dark-fantasy costumes on the same faces and rigs.

Key facts that shape the plan:

- The supplied costumes are already the Historic dark-fantasy look. The new work is the prehistoric costume set, not the Historic one.
- All 14 humans share one body scale, one A-pose, and two leg families (trousers, 312 triangles; long dress, 348 triangles). That makes one shared humanoid armature realistic.
- The faces share one base head topology (eight of them are identical in face count). Features differ mainly by hair, beard and colour. Distinct face shapes are therefore real work in this pass, not a given.
- Provenance of the meshes is unconfirmed for a public repository. The plan keeps meshes local until the owner confirms, and commits tools, code and original animation-only data first.
- Honest quality: scripted rigging and procedural clips will give a readable, stylised result. They will not match hand-animated faces or weight shift. Section 11 says what level is realistic and where the owner's eye is needed.

## 2. Asset inspection findings

### 2.1 How it was inspected

- The zip was extracted into a new empty local folder `WorldMVP\NEWSTUFF\characters_extracted\` (never committed). The archive was treated as data: scripts were kept in a scratch folder outside it and run with Python `-I`.
- Blender 4.5.10 LTS ran headless (`--background --factory-startup --disable-autoexec`). One script opened the .blend, another imported each of the 16 FBX files and recorded object names, counts, materials, modifiers, scale, bounds, loose-part counts, UVs, normals and edge health. No script from the archive was run (the .blend holds no text blocks and no scripts).
- Unity was not opened. Nothing about Unity import is verified yet.

### 2.2 Files in the zip

| File | Size (bytes) | Note |
|---|---|---|
| `Character_Lineup.jpg` | 285,195 | 1760 x 1066 render of all 16 meshes. No EXIF, XMP or content-credential data. |
| `README.md` | 1,920 | Says: sixteen original low-poly models from six nighttime concept scenes; static base models with separate head, body, leg, clothing and prop groups; no armatures, skin weights, clips or colliders; solid materials, no external textures; each 1,260 to 2,700 triangles. |
| `RomanVillage_DarkFantasy_Characters.blend` | 5,068,340 | Editable cast, 16 collections. Saved by a Blender 4.3 build; opens cleanly in 4.5.10. |
| `Models/01_Prince.fbx` to `16_Shadow_Brute.fbx` | 55,804 to 104,604 each | One FBX per character. |

The README's claims about no armature, no skin weights, separate groups and triangle range all matched the inspection.

### 2.3 Per-character table

All 16 FBX files: unit system metric, scale 1.0, every object has scale 1 and rotation 0 after import, root is an empty at floor level (z = 0). Armatures 0, vertex groups 0, shape keys 0, actions 0, images 0. Each mesh has exactly one UV layer; UVs span 0 to 1 and carry no art (there are no textures). Non-manifold edges 0 and degenerate faces 0 on every part. Height is the highest point of any part, including hair and props.

| # | Character | Tris | Verts | Parts (tris) | Materials | Height m | Leg family |
|---|---|---|---|---|---|---|---|
| 01 | Prince | 2,520 | 1,372 | Body 876, Cloth 112, Head 1,220, Legs 312 | 13 | 1.84 | trousers |
| 02 | Mira | 2,696 | 1,474 | Body 888, Cloth 100, Head 1,296, Legs 348, Prop 64 | 16 | 1.97 incl. staff (head top about 1.87) | dress |
| 03 | Juno | 2,180 | 1,198 | Body 848, Head 1,020, Legs 312 | 13 | 1.73 | trousers |
| 04 | DJ Sol | 2,768 | 1,502 | Body 924, Cloth 32, Head 1,500, Legs 312 | 13 | 1.86 | trousers |
| 05 | Woodcutter | 2,472 | 1,356 | Body 992, Head 1,100, Legs 312, Prop 68 | 15 | 1.91 | trousers |
| 06 | Carpenter | 2,316 | 1,268 | Body 992, Head 940, Legs 312, Prop 72 | 15 | 1.66 | trousers |
| 07 | Hesta (market keeper) | 2,468 | 1,342 | Body 912, Cloth 28, Head 1,180, Legs 348 | 13 | 1.69 | dress |
| 08 | Village Merchant | 2,424 | 1,330 | Body 1,012, Head 1,100, Legs 312 | 13 | 1.80 | trousers |
| 09 | Dancer Ochre | 2,324 | 1,260 | Body 764, Cloth 28, Head 1,220, Legs 312 | 12 | 1.80 | trousers |
| 10 | Dancer Ash | 2,356 | 1,278 | Body 840, Cloth 28, Head 1,140, Legs 348 | 12 | 1.65 | dress |
| 11 | Dancer Moss | 2,104 | 1,152 | Body 784, Cloth 28, Head 980, Legs 312 | 12 | 1.76 | trousers |
| 12 | Dancer Plum | 2,524 | 1,378 | Body 840, Cloth 28, Head 1,308, Legs 348 | 12 | 1.71 | dress |
| 13 | Timber Carrier | 2,552 | 1,404 | Body 992, Head 1,168, Legs 312, Prop 80 | 16 | 1.91 | trousers |
| 14 | Road Hero | 2,684 | 1,468 | Body 1,004, Cloth 100, Head 1,160, Legs 312, Prop 108 | 15 | 1.89 | trousers |
| 15 | Shadow Stalker | 1,260 | 742 | Body 556, Head 408, Legs 296 | 7 | 1.52 | n/a |
| 16 | Shadow Brute | 1,320 | 778 | Body 616, Head 408, Legs 296 | 7 | 2.03 | n/a |

The leg family is read from the leg triangle count (312 or 348) and the lineup (the 348 group are the four long garments). Heights need a tape measure in Unity before use; they are Blender bounds.

### 2.4 What each character looks like (from `Character_Lineup.jpg`)

| # | Description |
|---|---|
| 01 Prince | Short dark tousled hair, clean-shaven, stern neutral face. Tan tunic, maroon cape over the shoulders, maroon cuffs and boot tops, belt, dark trousers. |
| 02 Mira | Purple hooded robe to the floor, long dark hair under the hood, belt, round amethyst brooch at the chest, purple cuffs. Carries a tall wooden staff topped by a glowing amethyst crystal. Glow materials are flagged emissive in the .blend. |
| 03 Juno | Shorter, short dark hair. Tan sleeves, red apron, red cuffs and boot tops. |
| 04 DJ Sol | Dark curly hair, teal ear pieces, teal vest over a tan top, teal cuffs and boot tops, brown skin. |
| 05 Woodcutter | Tall, dark hair, full beard, brown leather apron over an olive tunic, axe held low at the left hand. |
| 06 Carpenter | Bald, white beard, brown apron, small hammer held low at the right hand. Shortest of the men. |
| 07 Hesta | Green long tunic with a tan scarf, dark hair, belt pouch. |
| 08 Village Merchant | Grey hair and beard, grey-lilac apron over an olive-tan tunic, belt pouch. |
| 09 Dancer Ochre | Dark short hair, ochre top with sash, orange cuffs. |
| 10 Dancer Ash | Dark hair, pale grey-beige long dress with lilac sash, brown skin. |
| 11 Dancer Moss | Auburn hair, green tunic, green cuffs. |
| 12 Dancer Plum | Dark hair with a braid, plum long tunic with pink sash. |
| 13 Timber Carrier | Dark hair, beard, tan apron, belt pouch, a lantern with glowing glass hanging from the right hand (emissive material). |
| 14 Road Hero | Dark swept hair, blue tunic, blue cape with a shoulder plate, blue cuffs, large steel-grey sword held point down at the left. |
| 15 Shadow Stalker | Small purple-grey horned demon, orange glowing eyes, fangs, clawed hands and feet, hunched, arms slightly out. |
| 16 Shadow Brute | Large grey-purple demon, big horns and shoulder spikes, orange eyes, fangs, layered chest plates, long clawed arms. |

Observed on the lineup: the human faces look alike (one eye, brow, nose and mouth treatment) and are told apart by hair, beard and colour. This matches the topology data: heads 01, 05, 06, 08, 09, 10, 11 and 14 have the same quad and n-gon counts, so they come from one base head.

### 2.5 Findings that matter for rigging

| Topic | Fact |
|---|---|
| Armature, weights, shape keys | None in any file. Everything must be added. |
| Separate parts | Yes, as separate objects per character: Body, Legs, Head, plus Cloth (only where a cape, hood or scarf exists) and Prop (only where a held item exists). Hair, beard, eyes, brows and mouth are not separate objects: they are loose pieces inside the Head mesh. |
| Loose pieces inside a part | Heads have 12 to 28 loose pieces, bodies 17 to 31, legs 10 to 16. Heads carry 7 to 9 materials, which suggests separate pieces for skin, brow, eye white, iris, pupil, hair and mouth. That is an inference. Whether eyes are round volumes (can rotate) or flat plates (need to slide) has not been checked and decides the gaze method. |
| Weighting method that fits | Rigid weighting per loose piece (each piece to one bone) is possible because the pieces are disconnected. Whether arm pieces sit under puffed sleeve pieces, and where joint gaps show, is unverified. The first Blender work package produces an island map to settle this. |
| Rest pose | A-pose. For the Prince and Mira the hand tip is 0.43 m out from the centre line and 0.98 m high, with the shoulder at 0.29 m out; that puts the arm roughly 17 degrees off vertical. Other humans' hand tips lie 0.39 to 0.45 m out. Unity Humanoid accepts an A-pose. |
| Hands and fingers | The hands are blocky; no finger bones are planned. Humanoid works without them. |
| Long garments | Mira (02), Hesta (07), Dancer Ash (10) and Dancer Plum (12) have a leg part that is dress-like and a body part that extends below the hips (Mira's body part starts at 0.105 m). A skirt cannot follow two straight leg bones; it needs a limited-swing skirt or a few skirt bones. |
| Normals | Every FBX mesh carries custom normals and imports with smooth flags; the .blend itself has flat faces. The lineup looks faceted. Whether the FBX normals are flat has not been checked; Unity import must be set to Import Normals and then looked at. |
| Faces | N-gons exist (2 to 48 per part). Unity triangulates on import. No stray geometry found. |
| Emissive | Four materials are emissive in the .blend: Mira's amethyst (two copies, strengths 0.9 and 0.35), the carrier's lantern glass (1.25), the demons' eyes (2.6). The FBX materials carry no emission data that Unity will honour; set emission in Unity materials, as the README says. |
| Materials | Flat colours only, named by hex colour (`RV_9C856A` and so on). 82 distinct names across the 16 FBX files; the same colour name recurs across characters. Duplicate names with a `.001` suffix exist on Mira. |
| Facing and forward axis | Not verified. In Blender, Mira's hood extends to +Y, so the face looks toward -Y. Which way it faces in Unity depends on the export axis chosen; the pipeline fixes this and tests it. |
| Scale | One unit equals one metre; humans 1.65 to 1.91 m. The Unity project uses metres (5 m lattice step). |
| .blend extras | 78 objects (16 empties, 62 meshes), lineup positions applied (x from -4.62 to 4.62, two rows at y 0 and 2.1), no lights, no cameras, an unused "Render Result" image. Each empty carries two custom properties: `art_style` and `rig_status` ("Static model with separate mesh groups"). |
| Broken | Nothing broken. Open items are the ones above (normals, eyes, joint gaps, duplicate material names). |

### 2.6 The Sims 2 comparison reference

- `WorldMVP\config.json` exists and is git-ignored (`.gitignore` line `/config.json`). The two folder paths it holds (the game folder and the user folder) both exist on disk as directories. It also lists 17 content-pack ids. I did not list or copy any game content and I am not printing the file.
- `config.json` lives in the `WorldMVP` folder, not in this worktree. Any other worktree that runs the probe needs its own local copy.
- On this branch the probe is still in the tree: `Assets/RomanVillage/Probe/ClipProbe.cs`, `ClipViewer.cs`, `probe_candidates.txt`, and the scenes `Assets/Scenes/ClipProbe.unity` and `ClipViewer.unity`. OpenTS2 is under `Assets/ThirdParty/OpenTS2`; `Assets/Bedsit/Sims2/SimCharacterActor.cs` is the optional actor.
- Not verified: that the probe still compiles and mounts the install on this branch (Unity was not opened). `Docs/M0_ANIMATION_FINDINGS.md` records 56 general clips verified on 12 Nov 2026 and says nothing about combat or face clips. Those need separate checking.
- Verdict: reachable on disk, yes. Runtime check pending.

### 2.7 Provenance

What the files say:

- The README says the models are "original" and "based on the six nighttime concept scenes". The owner says the zip came from their ChatGPT concept-art session.
- FBX metadata: creator string "Blender (stable FBX IO) - 4.3.2 - 5.12.5"; an author field reading "Blender Foundation" (the exporter's default text, not a person or studio).
- .blend: saved by Blender 4.3. No author, licence, copyright or URL data. No embedded text blocks, no linked libraries, no image textures.
- No third-party names, trademarks, brand names or licence text anywhere in the 16 FBX files, the .blend, the README or the lineup image metadata.
- Zip entry timestamps are 13 Nov 2026, 23:40 to 23:41.

Known: the files contain no texture images, no embedded scripts and no stated third-party licence. The geometry is simple low-poly shapes.
Unknown, and not answerable from the files:

- Which tool generated the meshes, and from what prompts or reference images. The concept scenes were not inspected.
- Whether any reference image used in that session contained third-party art.
- The current terms of the service the owner used, and what they grant over generated output. Those terms and the law on protecting AI-assisted work change, and the position differs by country. The owner should read the terms as they stand when deciding.
- Whether the owner is content to publish derivatives under the repository's licence.

Recommendation:

| Commit to the public repo now | Keep local until the owner confirms rights |
|---|---|
| Blender scripts in `Tools/characters/` (they take the zip folder as an argument and contain no mesh data), clip data files, the cast manifest (names, bone lists, clip lists, no geometry), Unity import and prefab-builder code, Animator controllers and avatar masks, `ActorFactory` changes with the placeholder fallback, tests that skip when art is absent, this PRD, a provenance note. Animation-only FBX files (a skeleton and clips, no mesh) are original work and can go in. | The zip, `characters_extracted/`, the supplied .blend, any edited .blend, any FBX with a mesh, prefabs and materials built from the meshes, lineup images, captures and videos. |

After the owner confirms in writing (a line in `Docs/PROVENANCE_CHARACTERS.md`, which a later work package writes), commit: the runtime FBX files, prefabs, materials, and the edited .blend sources, with that note. Until then the game must build and boot with no character art, so loading is by name from a `Resources` folder with no hard asset references (CH-14).

Rule from the owner's answers that stays in force: NEWSTUFF, the zip, Amiga archives, Sims files and Sims footage never go in the repo.

## 3. Requirements

Priority: M = needed for Slice 1, S2 = Slice 2, S3 = Slice 3.

### Cast and costume

| Id | Requirement | Slice |
|---|---|---|
| CH-1 | Mira (02), the Prince (01) and the Ash champion (14) are the first three characters. | M |
| CH-2 | Every named character keeps its identity from the lineup: hair, build, palette family, silhouette. Face shapes are differentiated (brow, eye spacing and size, nose, mouth width, hairline) while the base topology stays shared. | M |
| CH-3 | The prehistoric costume set is the default from the first scene: hide wrap, fur mantle, kilt or apron, thong belt, bare arms where the model allows, fur boots, camp-appropriate props. | M |
| CH-4 | The Historic dark-fantasy costume (the supplied look) is a swappable variant on the same body, face and rig. | S3 |
| CH-5 | Costume swaps use role-based material slots (about eight) plus optional overlay meshes on the same skeleton. A costume is data (palette, overlay list, prop list), not a new rig. | M |
| CH-6 | Props sit on named sockets and stay attached through every clip. | M |
| CH-7 | Style stays flat-shaded low poly. Colours are chosen to read under warm firelight and cool moonlight (dark values lifted, no pure black). | M |

### Rig and face

| Id | Requirement | Slice |
|---|---|---|
| CH-8 | One shared humanoid armature template, fitted per character, with identical bone names, rest pose and sockets. | M |
| CH-9 | Humanoid avatar for all 14 humans; Generic rig for the two demons, sharing one demon armature. | M/S2 |
| CH-10 | Face controls: blink, gaze (eyes plus head lead), brows (raise, furrow, worry), jaw open, mouth shapes (smile, frown, round), and named expressions built from them. Cues are shared; per-character tuning scales them. | M |
| CH-11 | Gaze targets the person being addressed, with eye, head and chest taking share of the turn, clamped to a natural range. | M |
| CH-12 | Sockets: hand.R, hand.L, staff tip, spell origin, back, hip.R, head top (for carried loads), plus a ground-contact point per foot. | M |
| CH-13 | Secondary motion for capes, hoods, skirts and staffs: a few bones, simulated in code, with a cap on cost. | M (Mira, Prince) |

### Integration

| Id | Requirement | Slice |
|---|---|---|
| CH-14 | New actors load by name from a `Resources` folder. A missing prefab means the placeholder is used and nothing logs an error. The public repo builds and boots without any character art. | M |
| CH-15 | `ActorFactory.Create` keeps both signatures. Every current call site (`Npc.Spawn`, `CampBuilder`, `DanceCircle`, `WardDuelStage`) works unchanged. | M |
| CH-16 | The new actor implements `ICharacterActor` with the existing logical keys (`idle`, `wave`, `greet`, `clap`, `cheer`, `surprise`, `stretch`, `cast`). A second interface, in `World.Game`, adds the new abilities (gaze, expression, speed, props, duel beats). `Bedsit.ICharacterActor` and `SimCharacterActor` are not changed. | M |
| CH-17 | A character's `colour` argument still tints its cloth slot so people stay tellable apart. | M |
| CH-18 | The same duel presentation runs with a second compatible opponent prefab and no encounter code specific to either. | M |
| CH-19 | A crowd tier with fewer bones, no face, one material, cheaper update. | S2 |

### Animation

| Id | Requirement | Slice |
|---|---|---|
| CH-20 | Clips are authored once on the template armature and play on every humanoid through Humanoid retargeting. Per-character style comes from a small override layer, not new clips. | M |
| CH-21 | Locomotion is a blend tree on speed with start, stop and 60-degree turn clips that match the 60-degree lattice headings. Feet plant: during stance, foot speed against the ground stays under a stated limit. | M |
| CH-22 | Upper-body gesture layer (greet, talk, listen, beckon, point, carry, tool) never fights the locomotion layer. | M |
| CH-23 | Duel layer with distinct anticipation, release, impact, recovery beats; Mira and the champion have different casting styles. | M |
| CH-24 | Everything is scripted and repeatable: rerunning the pipeline on the same inputs gives the same outputs. | M |
| CH-25 | Clips for tool work, carrying, drumming, dancing, warriors and demon threat. | S2 |

### Ward Break

| Id | Requirement | Slice |
|---|---|---|
| CH-26 | Animation and effects are driven by accepted simulation events only. A rejected cast produces no release, no bolt, no reaction. | M |
| CH-27 | Each accepted cast produces exactly one release, one projectile, one impact reaction. | M |
| CH-28 | A final Ward break stays readable for at least 1.5 s before the result screen covers it. This delays the screen, never the simulation. | M |
| CH-29 | Overlapping casts, an interrupt, a rematch and an exit mid-animation leave no stale bolts, shards, lights, props out of place, or doubled outcomes. | M |
| CH-30 | Hidden information stays hidden: reactions use only the aggregate fracture count, never a locus. Return-camera behaviour and the casualty logic are untouched. | M |
| CH-31 | Practice duel against Mira stays accessible and non-lethal, with her new body. | M |

### Quality and delivery

| Id | Requirement | Slice |
|---|---|---|
| CH-32 | Quality bar from the brief (faces, movement, interaction, duel), scored on repeatable captures, section 10. | M |
| CH-33 | Faces readable at 844 x 390 in the night lighting (section 7.4 gives the pixel targets). | M |
| CH-34 | A before and after video the owner can watch, captured from Unity on this machine. | M |
| CH-35 | Sims 2 side-by-side as a local-only reference if the probe runs; otherwise flagged for later. Nothing from Sims is traced, retargeted or committed. | M |
| CH-36 | Performance: characters stay inside the budget in section 5.8 with the real night lights. | M/S2 |
| CH-37 | Concise instructions for adding a character and reusing a clip are delivered with the scripts. | M |

## 4. Cast and costume plan

### 4.1 Who is who

| # | Model | Identity in this game | Slice |
|---|---|---|---|
| 01 | Prince | The prince. Walks up, greets, leads, points at the ground, reacts to casts. Later runs the camp himself. | 1 |
| 02 | Mira | Mira. Caster in Ward Break; the player casts through her. Practice opponent in the optional duel. Later leads the crystal expedition. | 1 |
| 14 | Road Hero | The Ash champion: one original duel opponent. Later the rival expedition's leader, so his face is reused. Working title; the owner names him. | 1 |
| 04 | DJ Sol | Sol the drummer at the dance circle (the host spawned by `CampInstaller`). | 2 |
| 06 | Carpenter | The sawyer who pitches wood improvements. | 2 |
| 08 | Village Merchant | The merchant who pitches main roads. | 2 |
| 05 | Woodcutter | Woodcutter worker and builder body. | 2 |
| 13 | Timber Carrier | Carrier and porter body (head load or back load). | 2 |
| 09 to 12 | Dancers Ochre, Ash, Moss, Plum | The four dancers at the circle; also camp villagers. | 2 |
| 03 | Juno | Apprentice caster in the practice duel (the player's side), and a camp villager. Juno left the game with the warehouse; this gives her a use. | 1 (practice duel) / 2 |
| 07 | Hesta | Camp elder and gatherer. | 2 |
| 15 | Shadow Stalker | Small demon on settlement corners and road edges. | 2 |
| 16 | Shadow Brute | Large demon, outbreak and boss-scale threat. | 2 |

Roles with no named model today, and how they are covered:

| Role | Cover | Slice |
|---|---|---|
| Villagers, builders, carriers, warriors (the `LandFigure` crowd) | Crowd tier built from the same bodies and heads with palette and costume variants. Warriors carry a spear. | 2 |
| Ash Clan warriors | Same crowd bodies in an ash-grey and charcoal palette, face paint, a different prop. | 2 |
| Six companions | New heads and hair on existing bodies, made with the same scripts, not copies of the named cast. | 3 |
| Wizard | An old head built from the beard and bald variants, with a hood. | 3 |
| Rival leader | The champion (14) in rival costume, or a new head if the owner prefers. | 3 |

### 4.2 Costume per age

How it works. A costume is data that says: palette (colour per role slot), overlay meshes to show, original pieces to hide, props per socket.

- Shared: the skeleton, the face rig, the sockets, and the body, head and legs meshes.
- Role slots (about eight, remapped from the 12 to 16 flat-colour materials by a script, reviewed per character): skin, hair, cloth main, cloth trim, leather or fur, metal or bone, eye, emissive.
- Prehistoric (default): recolour cloth slots to hide-and-ochre families, hide the Historic pieces that cannot work bare (the island map decides which), add overlay meshes skinned to the same skeleton (fur mantle, kilt or wrap, thong belt, fur boots, headband or bone necklace), swap props.
- Historic (the supplied look): the original palette and pieces, no overlays.
- The robust fallback if hiding pieces leaves holes: keep the original tunic, recolour it to hide brown, and cover it with overlays. This always works. The cleaner look (bare arms) is the target; the fallback is the guaranteed floor.

Prehistoric look, per first-three character (flat-shaded, tuned for orange firelight and cool shadow):

| Character | Prehistoric version | Props (socket) | Silhouette notes |
|---|---|---|---|
| Prince | Tan hide tunic cut to a short wrap, maroon-dyed fur mantle on one shoulder (the old cape's colour), maroon cuffs kept as cord bindings, thong belt, fur boots. | Bone-and-cord torch or nothing in hand; a flint knife on hip. | Mantle gives the widest shoulder line of the three; reads as "leader" at night. |
| Mira | Plum-dyed long hide robe with a rough hem (keeps the long shape and the staff), hood of soft hide, no cape, amethyst brooch kept, hair under the hood. | Carved wood staff with the amethyst crystal (staff socket; emissive stays). | Tallest vertical line in the camp: staff plus hood. Crystal glow is her light source. |
| Ash champion | Grey-blue fur cloak over a charcoal hide wrap, shoulder plate becomes a bone pauldron, grey face paint stripes, wide stance in every clip. | Stone-headed war club (replaces the sword); ash-wood shield optional. | Broadest, lowest, heaviest outline against Mira's tall, narrow one. |

Historic dark-fantasy version: the supplied costume. Slice 3 turns it into a variant, and adds role-specific Historic pieces for roles that never had one.

Facial identity: the faces and rig come first. The prehistoric and Historic costumes both use the same head, so a person is recognisable across ages (the owner's answer D16).

Face differentiation work (CH-2), done on the head pieces by script from parameters (not by hand-sculpting): brow thickness and angle, eye size and spacing, nose length, mouth width, jaw width, hairline shape. Parameters live in per-character data so the owner can adjust numbers.

## 5. Pipeline

### 5.1 Repo layout

| Path | Contents | Tracked |
|---|---|---|
| `Tools/characters/` | Blender scripts, clip data, README for adding a character | yes |
| `Tools/characters/lib/` | `common.py`, `islands.py`, `rig.py`, `skin.py`, `face.py`, `costume.py`, `anim_core.py`, `export_unity.py`, `verify.py` | yes |
| `Tools/characters/cast/` | One small data file per character: parameters, sockets, face tuning, costume sets | yes |
| `Tools/characters/clips/` | Pose and cycle data for each clip | yes |
| `Tools/characters/build_cast.py` | Driver | yes |
| `Assets/Characters/Resources/Characters/` | Runtime FBX, prefabs, materials, controllers | clips and controllers yes; meshes after the owner confirms |
| `Assets/Characters/Animation/` | Animation-only FBX, avatar masks, controllers | yes |
| `Assets/Editor/Characters/` | Import postprocessor, prefab builder | yes |
| `Assets/World/Game/Actors/` | Runtime actor code | yes |
| `Art/Characters/Source/` | Edited .blend files | after the owner confirms |
| `Evidence/raw/characters/`, `Evidence/screenshots/characters/` | Captures and video (already git-ignored) | no |

### 5.2 How a run works

One command, no clicking:

`blender.exe --background --factory-startup --disable-autoexec --python Tools/characters/build_cast.py -- --src <extracted zip folder> --out <output folder> --only mira,prince,champion`

Blender 4.5 is installed at `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe`. Paths are always arguments. The scripts never run from inside the extracted folder and never trust anything in it beyond reading geometry.

Steps in order, each a function that can run alone:

1. Import the FBX of the character. Record an island map (loose pieces with material, centre, size, count).
2. Fit the template armature: measure landmarks from the pieces (shoulder, elbow, wrist, hip, knee, ankle, neck, crown) and place bones. Bone names follow section 5.3.
3. Weight: each loose piece goes to one bone by rule (upper arm pieces to upper arm and so on). Soft blends at elbows, knees and the neck by a distance falloff. Cloth, hoods, capes and skirts get chains with limited swing. Output: at most four influences per vertex, every vertex weighted (checked).
4. Face: classify head pieces (eye, brow, mouth, nose, hair, beard, ears) by material and position, then build shape keys and bones (section 5.4). Apply the per-character differentiation parameters.
5. Costume: build the palette slots, hide or show pieces, build overlay meshes and weight them with the same code, place props on sockets.
6. Clips: build actions on the template armature from data (section 5.5) and bake.
7. Verify (section 5.7), write `cast_manifest.json` (bones, sockets, shape keys, clips with length, loop flag, native speed, events, triangle counts).
8. Export (section 5.6).

The driver is idempotent: it starts from the original FBX and the data files every time, never from the last output.

### 5.3 Shared armature (naming contract)

Unity-Humanoid-mappable chain: `Hips`, `Spine`, `Chest`, `Neck`, `Head`, `Jaw`, `Eye.L`, `Eye.R`, `Shoulder.L/R`, `UpperArm.L/R`, `LowerArm.L/R`, `Hand.L/R`, `UpperLeg.L/R`, `LowerLeg.L/R`, `Foot.L/R`, `Toes.L/R`.

Not in the Humanoid map (kept as extra transforms): brow bones `Brow.L/R` (inner and outer), lid bones `Lid.Up.L/R`, `Lid.Lo.L/R` (if lids are bones; otherwise shape keys), cloak chain `Cape.1..3`, hood `Hood.1..2`, skirt `Skirt.F`, `Skirt.B`, `Skirt.L`, `Skirt.R`.

Sockets (empty bones, no weights): `Sock.Hand.R`, `Sock.Hand.L`, `Sock.StaffTip`, `Sock.SpellOrigin` (child of the staff or of `Hand.R`), `Sock.Back`, `Sock.Hip.R`, `Sock.HeadTop`, `Sock.Foot.L`, `Sock.Foot.R` (ground contact).

Budget per hero character: at most 45 bones including face and sockets; at most 16 shape keys on the head.

### 5.3a Contract resolutions (WP-1, 14 Nov 2026)

The exact contract (bone, part, shape-key, socket, slot, clip and file names, step signatures, file ownership) is `Tools/characters/lib/contract.py`, explained in `Tools/characters/README.md` section 2. Where it differs from the text above, the contract wins:

- Brow and lid bones are dropped: brows, lids and mouth are shape keys (`Blink.L/R`, `BrowRaise.L/R`, `BrowFurrow`, `BrowWorry`, `MouthSmile`, `MouthFrown`, `MouthRound`, `MouthOpen`). Face bones are only `Jaw`, `Eye.L`, `Eye.R`. All 14 humans have flattened-ellipsoid ("disc") eyes: gaze is eye bones carrying iris and pupil, horizontal +-14 degrees, vertical +-5 (the iris fills the sclera's height); no gaze shape keys.
- Role slots are nine names plus a `hidden` slot for pieces a costume hides; one character uses at most 8.
- `Sock.StaffTip` hangs from the socket of the hand that holds the prop (Mira holds her staff in her left hand).
- Left is Blender +X; every supplied character faces Blender -Y; arm rest angle is 21 to 25 degrees (centre to centre), symmetric.
- Elbows and knees are only touching on all 14 humans, so WP-2 adds joint covers there; shoulders, wrists, hips and ankles are covered.
- FBX normals are flat in all 16 meshes.
- `cast/<key>.json` is face data (WP-3); island maps are `cast/islands/<key>.json` and `cast/index.json` (WP-1).

Demons share `Demon` armature: root, hips, spine, chest, neck, head, jaw, two arms of three bones, two legs of three bones, and a few horn and claw tips if the pieces allow. Generic type, shared bone names so one clip set plays on the Stalker and the Brute.

### 5.4 Face rig

| Channel | Method | Notes |
|---|---|---|
| Blink | Lids as bones or a shape key that moves or scales the eye pieces. Random interval, short double blink sometimes. | Needed on every hero face. |
| Gaze | `Eye.L/R` bones if the eye pieces are round volumes. If they are flat plates, shape keys slide iris and pupil in the plane. The island map decides, per character. | Head and chest lead; see below. |
| Brows | `Brow.*` bones or shape keys: raise, furrow, worry (inner up). | Brows are the cheapest strong signal at a distance. |
| Jaw | `Jaw` bone, or a mouth-piece shape key if the mouth is a small plate. | Talk ticker while a speech bubble shows. |
| Mouth | Shape keys: smile, frown, round (surprise). | Small plates: translate and scale corner pieces. |
| Expressions | Presets mixing the channels: neutral, attentive, concentrating, surprised, pleased, pained, determined. | Per-character multiplier on each channel. |

Runtime: a `FaceController` in `World.Game` writes the shape-key weights and bone offsets in `LateUpdate` with smoothing. A gaze controller uses the Animation Rigging package (already in `Packages/manifest.json`, version 1.3.1) for head and eye aim, with a share rule: eyes take what they can, the head about 40 percent, the chest about 15 percent, all clamped, with a short lag so the eyes arrive first. Expression presets are a data asset per character (`FaceTuning`).

Hero tier only. The crowd tier has no face rig.

### 5.5 Clip authoring in Blender

- Clips are written as data: a list of key poses per bone (Euler angles in degrees, or a name from a pose library), times in frames at 30 fps, easing per segment, and events (name, frame).
- Cycles (idle breathing, walk, jog, dance, tool work) come from small generators with parameters (stride length, step time, hip sway, arm swing, foot lift). The walk generator solves the legs with simple two-bone inverse kinematics against a ground plane so the foot stays fixed during stance, then bakes to forward-kinematics keys. Stride and cycle time set the clip's native ground speed, recorded in the manifest.
- Weight shift: before every step the hips move over the supporting foot (anticipation), the chest counter-rotates, the head stays level and a little behind. Stops end with the planted foot settling and a short overshoot. These are authored as parameters in the generator, not left to the Animator.
- Overlap: arms and head lag the hips by a few frames in every clip (offset curves in the generator).
- Style per character: a small data set of offsets (stance width, shoulder height, head carry, tempo scale) applied as an additive override when baking. The Prince is relaxed and open, Mira upright and economical, the champion wide and heavy.
- All clips are authored in place (no root motion); Unity moves the root. The Sims library worked the same way (`M0_ANIMATION_FINDINGS.md`).
- Nothing is copied, traced or retargeted from Sims clips. They are looked at as a reference only.

### 5.6 Export and Unity import

Blender export (one call in `export_unity.py`, same settings every time): FBX, meters, forward -Z and up Y, `bake_space_transform` on, only deform bones plus extras listed in the manifest, no leaf bones, shape keys on, vertex colours off, mesh smoothing from edges, one file per character (mesh and rig, no clips) and one animation-only file per clip set (skeleton and actions, no mesh). The facing axis is tested: after import, a script checks that the character's forward points along +Z.

Unity import is a `ModelImporter` postprocessor in `Assets/Editor/Characters/` that reads `cast_manifest.json`:

- Humanoid avatar built from each character model for the 14 humans; the animation-only files import as Humanoid and play on every avatar.
- Generic for the two demons, with the root bone set.
- Import normals as authored, blend shapes on, tangents off, read/write off, no mesh compression, optimise game objects off for hero characters (sockets must stay reachable).
- Clips: loop flags, loop-pose, root bake flags and events from the manifest; no hand clicking.
- Materials: not taken from the FBX. Role slots map to project materials made through `MaterialFactory` (the project's own shader family `WorldLit`), with emission set for the amethyst, lantern glass and demon eyes. Whether `WorldLit` can take a palette per role is to be confirmed by the import work package; the fallback is eight submesh materials.

Animator controller (one shared asset, with per-character override controllers):

| Layer | Contents |
|---|---|
| Locomotion (base) | Idle; blend tree on `Speed` (idle, walk, brisk, jog); start-step and stop clips chosen by foot phase; `Turn60L` and `Turn60R`; speed multiplier set from the actual root speed over the clip's native speed so feet plant. |
| Gesture (upper body mask, override) | Greet, nod, talk A/B/emphasis, listen, beckon, point at ground, carry, tool strike. |
| Duel (full body, override, triggers) | `CastAnticipate`, `CastRelease`, `CastRecover`, `Block`, `RecoilLight`, `RecoilHeavy`, `WardBreak`, `Victory`, `Defeat`, `Surprise`. |
| Aim | Not an Animator layer: Animation Rigging constraints for head, eyes and hand-to-prop snap. |

Animation events (names only; routed by an `ActorAnimationEvents` component to a C# event on the actor): `Release`, `Impact`, `FootL`, `FootR`, `PropHit`, `BeatHit`. Unity fires events from both clips during a cross-fade, so every consumer de-duplicates by beat id (CH-27) and also has a timer fallback if an event never fires (culling, a hitch).

### 5.7 Verification scripts

`verify.py` runs in Blender after the build and fails the run on any of:

- an unweighted vertex, more than four influences, a bone outside the naming contract, more than 45 bones or 16 shape keys;
- a missing socket;
- rest pose arm angle out of the stated range or left and right not mirrored;
- a clip missing from the manifest, or a loop whose first and last pose differ beyond a tolerance;
- foot sliding: during stance phases, foot speed against the ground above a limit (a number in the data, starting at 2 cm per second at native speed);
- forward axis wrong after export.

An EditMode test in Unity (skipped when art is absent) checks the same manifest facts against the imported prefab: avatar valid and Humanoid, every clip present and `length` close to the manifest, every socket reachable by name, every shape key name present, and that sampling the walk clip at native speed leaves the foot bone world speed under the same limit during stance.

### 5.8 Plugging into the game

Current seams (read from the code):

- `ICharacterActor` (`Assets/Bedsit/Core/ICharacterActor.cs`): `ActorId`, `Root`, `CurrentKey`, `IsPlayingOneShot`, `OneShotFinished`, `PlayIdle`, `PlayOneShot(key)`, `FaceTowards(point)`, `SetVisible`. Shared with the optional Sims actor, so it is left alone.
- `ActorFactory.Create(id, colour, [position, yaw], [parent])` in `PlaceholderActor.cs`. It always returns a `PlaceholderActor`. Callers: `Npc.Spawn` (ids from `NpcIds`, the prince, sawyer, merchant), `CampBuilder` (`camp_figure_N`), `DanceCircle` (`dancerN`), `WardDuelStage` (`ward_player`). Two places test `is PlaceholderActor` to call `SetYaw`; both already have an `else` branch.
- `PlaceholderActor` infers walking from how fast its root moves, turns at 300 degrees per second, and plays timed one-shots with the key `cast`.
- `LandFigure` (`View/Land`) is the separate crowd path: a shared mesh per kind, `Pose` (Stand, Walk, Work, Slump) animated by its owner view, hidden within 1.1 m of the eye.

Plan:

1. A `CastRegistry` (data) maps an actor id or id pattern to a cast entry: character key, costume set for the current age, crowd or hero tier. Examples: `mira` to Mira; the prince's id to the Prince; `host` to Sol; `camp_figure_*` to a villager pool chosen by hash; `dancer*` to the four dancers; `ward_player` to the role "apprentice" (Juno).
2. `ActorFactory.Create` asks the registry. If the entry exists and `Resources.Load` finds the prefab, it instantiates a `CastActor`. Otherwise it returns the `PlaceholderActor` exactly as today. Both signatures keep working. A command-line or AutoPlay flag `--placeholder-actors` forces the placeholder, which is how the "before" footage is made.
3. `CastActor` implements `ICharacterActor` and a new `IExpressiveActor` in `World.Game`: `SetGaze(Transform or point)`, `SetExpression(name, weight)`, `SetSpeedHint`, `Attach(propKey, socket)`, `PlayBeat(name)`, `ForceIdle()`, `ResetPresentation()`, and events `BeatEvent(name, beatId)`. The logical keys map as: `idle` to Idle; `wave` and `greet` to Greet; `cheer` to Victory-lite; `clap`, `surprise`, `stretch` to their gesture clips; `cast` to the full anticipate, release, recover chain (so old callers still get a cast).
4. Speed for walking is still inferred from root motion (as the placeholder does), so `Npc.UpdateWalk`, the player-follow code and the lattice code need no change. The `Npc` field `WalkSpeed` is 3 m/s today; the walk and jog clips cover 0 to about 3.2 m/s.
5. `FaceTowards` uses a lead chain (eyes, head, chest, then feet with a turn clip beyond 40 degrees) so a turn does not snap.
6. Crowd tier (Slice 2): `LandFigure.Create` gains an optional crowd prefab from the registry. If absent it builds the primitive person as today. `Animate(time)` becomes parameter writes (`Speed`, `Work`, `Slump`) on the crowd animator. `Place` keeps its eye-distance hide.

Performance budget (targets to be measured on this machine, not promises):

| Item | Target |
|---|---|
| Hero tier | At most 3.2k triangles with overlays, at most 8 draw calls (one per role slot), at most 45 bones, at most 16 shape keys. |
| Crowd tier | At most 1.4k triangles, 1 material, at most 18 bones, no face, no shape keys, no secondary-motion bones. |
| Animator cost | Hero actors within 25 m update every frame. Crowd within 25 m every frame, 25 to 60 m every second or fourth frame, beyond 60 m culled (`CullCompletely`). Duel actors always animate. |
| Lights | No per-actor lights. At night the camp fire and the existing duel light do the work. Only the cast flash and the bolt light (already in `WardDuelStage`) are added. Quality levels in `ProjectSettings/QualitySettings.asset` set `pixelLightCount` from 0 to 3, so firelight on characters must be tested at the shipped quality level. |
| Frame rate | No regression below the existing target in `REFACTOR_SPEC.md` 8.4: above 120 fps at 1920 x 1080 in the editor, 20 minutes of prompt play. Report device performance only if a device is tested (not this milestone). |
| Memory | Hero meshes read/write off, shared materials, shared controller. |

## 6. Clips

All at 30 fps, authored in place. Native speed is in the manifest. "Core" means authored once on the template and shared by every humanoid.

### 6.1 First three

| Group | Clip | Length (s) | Notes |
|---|---|---|---|
| Idle | `Idle_A` (breathing, weight shift every 4 to 6 s) | 6, loop | Core, with per-character style. |
| Idle | `Idle_B`, `Idle_Look` (glance and settle) | 4 to 5, loop | Variety; the actor picks at random. |
| Idle | Mira `Idle_Staff` (staff planted, off hand relaxed) | 6, loop | Style variant. |
| Locomotion | `Walk` | native 1.4 m/s, cycle about 1.0 | Foot planted; hips sway, chest counter-rotates. |
| Locomotion | `WalkBrisk`, `Jog` | native 2.2 and 3.0 m/s | Blend tree with `Walk`; `Npc.WalkSpeed` of 3 m/s plays `Jog`. |
| Locomotion | `StartL`, `StartR` | 0.5 | Weight shift, then first step. |
| Locomotion | `StopL`, `StopR` | 0.6 | Planted foot settles, small overshoot; chosen by foot phase. |
| Locomotion | `Turn60L`, `Turn60R` | 0.5 | One lattice heading. `TurnSmallL/R` additive for less than 40 degrees. |
| Social | `Greet` (palm raise and nod) | 2.0 | Eyes go first, then head, then hand. |
| Social | `Nod`, `ShakeHead` | 0.8 | Additive. |
| Social | `Talk_A`, `Talk_B`, `Talk_Emphasis` | 3 to 4, loop | Upper body; jaw ticker from code. |
| Social | `Listen` | 4, loop | Small nods, head tilt, weight shift. |
| Prince | `Beckon` (sweep of the arm: come with me) | 1.6 | Looks at the viewpoint, then toward the plot. |
| Prince | `LeadLook` (glance back while walking) | 1.2 | Additive on `Walk`. |
| Prince | `PointGround` (point at the plot, hold, release) | 2.4 | Hold loop 1 s; the pitch waits for the cast. |
| Duel | `CastAnticipate`, `CastRelease`, `CastRecover` | 0.3, 0.25, 0.5 | Release event on its first frame. Mira: staff raise, crystal flare, controlled. Champion: two-handed shove from the hip, wide base, faster. |
| Duel | `CastWeave_Loop` (hands composing) | 2, loop | While the weave is being built. |
| Duel | `Block` | 0.5 | Ward holds with no fracture. |
| Duel | `RecoilLight`, `RecoilHeavy` | 0.6, 0.9 | One or two fractures; scaled. |
| Duel | `WardBreak` (stagger back, drop, hold) | 1.4 | The reaction that must read. |
| Duel | `Victory`, `Defeat` | 2.5, 2.5 | Both settle into a looped hold. |
| Duel | `Surprise` | 0.8 | Also the old `surprise` key. |

### 6.2 Rest of the cast

| Group | Clips | Who |
|---|---|---|
| Tool work | `Chop` (stone axe, 1.4 loop, `PropHit` event), `Saw`/`Adze` (1.5 loop), `Hammer`, `Dig`, `Plant`, `Gather` | Woodcutter, sawyer, builders, foresters |
| Carrying | `CarryHead_Idle`, `CarryHead_Walk`, `CarryBack_Walk`, `Hand_Off` (give and take) | Carriers, porters |
| Trade | `ShowGoods`, `Count` | Merchant, Hesta |
| Drumming | `Drum_L`, `Drum_R`, `Drum_Both`, `Drum_Accent` with `BeatHit` events | Sol |
| Dancing | `Dance_Sway`, `Dance_Stomp`, `Dance_Spin`, `Dance_Leap`, `Cheer`, `Clap` | Dancers |
| Warrior | `Spear_Idle`, `Spear_Advance`, `Spear_Thrust`, `Guard`, `Hit_Light`, `Fall` | Warriors, Ash Clan |
| Demon (Generic) | `Demon_Idle` (crouched sway), `Stalk`, `Lunge`, `Threat` (rear up, show fangs), `Banished` (collapse, eyes dim) | Stalker and Brute; Brute has a slower, heavier set from the same data with different parameters |
| Dialogue | The first-three social clips above | All named cast |

## 7. Duel staging

### 7.1 Who is on stage

- Campaign-style test duel (the test setup the owner's brief allows if the campaign duel is not on this branch): Mira is the caster, the player casts through her; the Ash champion is the opponent. Registered by `EncounterInstaller` as a new encounter id `ward_champion`, reusing `WardDuelSession`, `EncounterHost` and the Ward UI.
- Practice duel (existing, kept): Mira is the opponent; Juno is the player's apprentice body in place of the generic `ward_player`. No penalty.
- Both read the same `DuelStageSpec` (caster actor id, opponent actor id, positions). The second compatible opponent (swap test, CH-18) is the Woodcutter or the Merchant dressed as a clan fighter, with no encounter code change.

### 7.2 Camera and frame

The duel camera is moved by `EncounterHost` and restored on exit (unchanged). Staging puts Mira and the opponent about 5.5 m apart, facing each other, as `WardDuelStage` does today. The camera floats behind and slightly beside Mira, about 2 m up, at a narrower field of view than the walking view, so Mira is left of centre in three-quarter back view and the opponent's face is visible in the right half.

Why a narrower view: at 844 x 390 with a 70 degree vertical field of view and the opponent 6 m away, one metre is about 46 pixels and a 0.31 m head is about 14 pixels. At 40 degrees it is about 89 pixels per metre and 27 pixels. During the ward-break push-in (camera to about 3.2 m at 35 degrees) it is about 193 pixels per metre and 60 pixels. These are computed from the stated angles; head height 0.31 m is from the Prince mesh bounds.

### 7.3 Event-driven beats

Source of truth: `WardDuelSession.DrainEvents()` (`PlayerCastEvent`, `RivalCastEvent`, `DuelEndedEvent`, each with `AtMs`). `WardDuelEncounter.Cast()` already returns without a session event when a cast is refused, so nothing animates on a rejection.

A new `DuelDirector` (presentation only, in `Encounters/`) takes each accepted cast event and creates one beat with a unique id (side, `AtMs`, running index):

1. Anticipation. The caster plays `CastAnticipate`. Mira raises the staff and the crystal brightens; the champion drops into his base.
2. Release. The `Release` animation event, once per beat id, spawns the bolt at the `Sock.SpellOrigin` socket. If the event has not fired 0.2 s after the planned time, a timer fires it (still once).
3. Flight. The existing 0.6 s bolt flight and glow, unchanged in look.
4. Impact. On arrival, once: the target Ward gets `Hit(fractures, broken)` as today, and the target actor plays `Block` (0 fractures), `RecoilLight` (1), `RecoilHeavy` (2) or `WardBreak` (broken).
5. Recovery. The caster plays `CastRecover` and returns to idle or `CastWeave_Loop`.

The reaction uses only the aggregate fracture count that is already shown on screen. It never reads a locus or the hidden Ward.

### 7.4 Rules the director must keep

| Case | Behaviour |
|---|---|
| Rejected cast | No event from the session, so no beat. Refusal toast only (unchanged). |
| Auto-cast (idle timer) | Same as an accepted cast. |
| Two casts in flight (one each side, or a player cast during the opponent's recovery) | Each beat has its own id and its own actor. A second beat on the same actor queues behind the first; the player's recovery time and Mira's think time make this rare. Bolts and impacts never merge. |
| Final break | The reaction hold is at least 1.5 s after the impact. The result screen waits for the director (a gate in `WardDuelEncounter`); the session has already resolved, so determinism is untouched. |
| Presentation timing | Uses unscaled time like the existing stage, so it is independent of the simulation clock, and it freezes with the pause menu like the duel clock. |
| Rematch | `ResetWards()` plus `DuelDirector.Reset()`: an epoch counter makes every pending callback ignore itself, destroys bolts and shards, calls `ResetPresentation()` and `ForceIdle()` on both actors. |
| Interrupt or exit mid-animation | Forfeit or Return destroys the stage and director, which cancels all pending beats. The persistent Mira (a camp NPC) gets `ResetPresentation()` (gaze, expression, props, layers) and faces her home yaw. The opponent prefab is destroyed with the stage. |
| Camera | Restored by `EncounterHost` as today; the push-in is part of the stage and is reverted on reset. |
| Casualty logic and return-camera | Untouched. |

### 7.5 Mira and the champion read differently

| | Mira | Ash champion |
|---|---|---|
| Stance | Upright, narrow, staff planted | Wide, low, weight forward |
| Rhythm | Slow anticipation, crisp release, long recovery | Quick wind-up, hard shove, short recovery |
| Release visual | Crystal flare, thin bolt | Fist-and-club thrust, thicker bolt |
| Hit reaction | Step back and re-plant the staff | Rock back, plant, shake it off |
| Break reaction | Staff slips, drops to one knee | Staggers two steps, falls to both knees |
| Face | Concentrating, brows down, mouth closed | Determined, jaw set; surprised on a break |

## 8. MVP slices

### Slice 1: Mira, the Prince and the champion proven in the real game

What the owner will see:

1. A short video (about 60 to 90 s, section 10.3) with a before half (placeholders) and an after half. It shows: the Prince walking up through the night camp and stopping with weight settling; a greeting with eye contact; a beckon, a walk with a glance back, a point at the ground; a close-up of the three faces blinking and looking at the person they address; Mira anticipating, releasing and recovering; the champion's different cast; the champion reacting; a Ward breaking with a visible reaction before the result.
2. The same in the game: the normal first scene (the prince approaches and greets), the practice duel with Mira in her new body, and `ward_champion` with Mira casting through the player's choices.
3. One lineup image of the three in prehistoric costume and, as a clearly marked second image, in Historic costume if it exists by then (it is not required).
4. A one-page score sheet against the quality table, with captures.

Acceptance checks:

- A1. `build_cast.py` for the three characters is rerunnable and passes `verify.py`.
- A2. Unity imports Mira and the Prince as Humanoid; the same `Walk` and `Greet` play on both with working Avatars; the champion plays the same duel presentation.
- A3. Foot-plant test passes at native speed, and in play at `Npc.WalkSpeed`.
- A4. The first scene runs (`first_scene` AutoPlay, when the prince branch is merged) with the new Prince: he reaches the player, greets, leads and points. Other `ActorFactory` users (camp figures, dancers, Sol, Mira at camp) still work as placeholders or new actors.
- A5. A win, a loss, a rematch and an early exit through normal controls. Section 7.4 holds on each, checked by PlayMode tests (section 10.2).
- A6. Second compatible opponent prefab swaps in with no code change.
- A7. Faces pass the pixel targets at 844 x 390 and 932 x 430.
- A8. Game builds and boots with no character art present (placeholder fallback), and with the Sims install unavailable.
- A9. Existing EditMode and PlayMode tests, including the Ward and encounter tests, stay green. Report what ran and what did not.
- A10. The owner watches the video and answers the gate question: are the faces and weight shift good enough to roll out?

### Slice 2: the rest of the cast and crowd roles

What the owner will see: Sol drumming at the circle on the beat; four distinct dancers; the sawyer, merchant and woodcutter working with props; carriers with loads that stay on; builders, warriors and Ash Clan figures in the land views; both demons with their own threat stances; a camp full of people with no frame drop; a lineup image of all 16 in prehistoric dress.

Acceptance checks:

- B1. `build_cast.py` runs on all 14 humans and both demons and passes `verify.py`.
- B2. Roles in section 4.1 each play their clip in the real game, spot checked with captures.
- B3. Crowd tier in `LandFigure`; a measured frame-time comparison with the placeholder crowd at 20 minutes of prompt play; no regression below the 120 fps target.
- B4. Dance clips fire `BeatHit` on the beat within one frame at 30 fps.
- B5. Demon clips play on both demons from one clip set.
- B6. The `cast_all`, `duel_dance`, `first_scene` and `prompt_game` AutoPlay scripts still pass.

### Slice 3: Historic costumes

What the owner will see: the same people and faces in the supplied dark-fantasy costumes, switchable per age; Mira, the champion as rival leader, six companions and the wizard as new faces from the same scripts; a before and after of one character across ages.

Acceptance checks:

- C1. The age switch (driven by the ages work, `PRD_AGES.md`) swaps palette and overlays on a live actor with no new rig and no hitch.
- C2. Each Historic costume equals the supplied look within a stated colour tolerance (checked by capture).
- C3. Six companions are visibly different from each other and from the cast (lineup image).
- C4. Existing animation checks (A2, A3, A7) re-run on Historic costumes.
- C5. After the owner confirms rights, the committed set matches section 2.7.

## 9. Work packages

Rules for parallel agents: each package owns the files listed and no other package edits them. One Unity editor per folder (per worktree). Blender packages need no editor and can run side by side. Model tiers: sonnet for mechanical or well-specified work, opus for substantial implementation, fable for the hardest judgement work (looking at captures and iterating on quality).

| Id | Package | Model | Needs | Owns (files and folders) | Depends on | Purpose and acceptance |
|---|---|---|---|---|---|---|
| WP-1 | Intake and island map | sonnet | Blender | `Tools/characters/lib/common.py`, `islands.py`, `inspect_cast.py`, `cast/*.json` (island maps), `Tools/characters/README.md` | none | Re-run the section 2 inspection as a script; write one island map per character (piece, material, centre, size; eye shape: round or flat; sleeve overlap; joint gaps). Accept: reproduces section 2.3 numbers; island maps for all 16. |
| WP-2 | Rig template, fit and skin | opus | Blender | `lib/rig.py`, `lib/skin.py`, `lib/verify.py`, `build_cast.py` | WP-1 | Template armature with the naming contract (5.3), fit per character, rigid-piece weights, skirt and cape chains. Accept: first three pass `verify.py`; A-pose to a T-pose test bend shows no broken joints (render). |
| WP-3 | Face rig | opus | Blender | `lib/face.py`, face parameters in `cast/<name>.json` | WP-1 (contract names from section 5.3 let it start before WP-2 finishes) | Blink, gaze, brows, jaw, mouth, expressions, plus differentiation parameters. Accept: shape keys and bones per 5.4 on the three; a still image per expression per character. |
| WP-4 | Prehistoric costume | opus | Blender | `lib/costume.py`, `cast/costumes/*.json` | WP-1; skinning calls into WP-2's `skin.py` (read-only use) | Role slots, hide or show pieces, overlay meshes (mantle, wrap, kilt, boots), prop swaps. Accept: three costumes per 4.2, rendered under a firelight setup. |
| WP-5 | Core clips and walk generator | opus | Blender | `lib/anim_core.py`, `clips/*.py` | contract names (5.3); WP-2 to bake on real fits | Clips in 6.1 for Idle, Locomotion, Social, Prince, Duel. Accept: `verify.py` loop and foot-slide checks pass; manifest lists lengths, native speeds, events. |
| WP-6 | Export and Unity import | opus | Unity (Blender for export test) | `lib/export_unity.py`, `Assets/Editor/Characters/**`, `Assets/Characters/Animation/**`, Animator controllers and masks | WP-2, WP-5 first outputs | Scripted import (5.6), prefab builder, controller with layers and events, avatar checks. Accept: A2 and the EditMode manifest test. |
| WP-7 | Runtime actor and factory | opus | Unity (same editor as WP-6, run in sequence, or separate worktree) | `Assets/World/Game/Actors/**` (`CastActor`, `CastRegistry`, `FaceController`, `GazeController`, `IExpressiveActor`, `ActorFactory` moved out of `PlaceholderActor.cs`), `Tests/PlayMode/ActorTests.cs` | WP-6 prefab and controller | Implements 5.8 items 1 to 5; placeholder fallback; `--placeholder-actors` flag. Accept: all current call sites unchanged and passing; tests run with and without art. Coordinate with the owner of `Actors/**` on the prince branch (merge that first). |
| WP-8 | Prince first scene binding | sonnet | Unity | `Assets/World/Game/Prince/PrinceActorBinding.cs` (new file only) | WP-7; prince branch merged | Greet, beckon, lead-look and point-ground triggered by the existing pitch loop events. Accept: A4. |
| WP-9 | Duel director and stage | opus | Unity (own editor) | `Encounters/DuelDirector.cs`, `Encounters/WardDuelStage.cs`, `Encounters/WardDuelEncounter.cs` (gate and stage spec only), `EncounterInstaller.cs` (`ward_champion`), `Tests/PlayMode/WardPresentationTests.cs` | WP-7, WP-5 duel clips | Section 7. Accept: A5, A6; the tests in 10.2. |
| WP-10 | Sims reference captures (local) | sonnet | Unity (own worktree copy with a local `config.json`) | Output only, in `Evidence/screenshots/characters/ref/`; no tracked files | none | Run `ClipViewer` or `ClipProbe` at fixed framing; capture neutral face, blinking, a walk into a stop, a greeting; audition a cast-like gesture and a hit clip and record clip ids. If it does not run, record why and stop. |
| WP-11 | Capture, video and review | fable | Unity plus ffmpeg | `AgentScripts/CharacterCaptures.cs`, `Evidence/raw/characters/**` | WP-9 | Section 10: shots, video, score sheet, and the written list of what to fix. Iterates with WP-3, WP-5 and WP-9 owners on faces and weight shift until A7 and the quality table hold. |
| WP-12 | Role clips | opus | Blender | `clips/roles/*.py` | WP-5, gate A10 | Clips in 6.2 for humans. Accept: B2, B4 (beat events). |
| WP-13 | Demon rig and clips | opus | Blender | `lib/demon.py`, `clips/demons/*.py`, `cast/demon_*.json` | WP-1 | Section 5.3 demon armature and the demon clips. Accept: B5. |
| WP-14 | Roll-out to all humans | sonnet | Blender | `cast/<name>.json` for the other 11 humans; `Assets/Characters/Resources/Characters/**` (generated) | gate A10, WP-4 | Run `build_cast.py` for all; fix per-character data. Accept: B1. |
| WP-15 | Crowd tier and `LandFigure` | opus | Unity | `Assets/World/Game/View/Land/LandFigure.cs`, `LandLook` crowd additions, crowd prefab builder in `Assets/Editor/Characters/Crowd*.cs` | WP-14 | Section 5.8 item 6, budget table. Accept: B3 with a frame-time comparison. |
| WP-16 | Camp role binding | sonnet | Unity | `View/Camp/CampInstaller.cs`, `DanceCircle.cs`, `CampBuilder.cs` (actor binding only) | WP-14, WP-12 | Sol, dancers, camp figures use registry entries and beat events. Accept: B2, B4, B6. |
| WP-17 | Provenance note and confirmation | sonnet | none | `Docs/PROVENANCE_CHARACTERS.md` | none | Writes the note from section 2.7 with a line for the owner to sign off; commit rules for art. |
| WP-18 | Historic costumes and ages hook | opus | Blender and Unity (own worktree) | `cast/costumes/historic_*.json`, `Assets/World/Game/Actors/CostumeSwitch.cs`, tests | gate A10, `PRD_AGES.md` hook | Slice 3 C1, C2, C4. |
| WP-19 | Companions, wizard, rival leader faces | opus | Blender | `cast/companion_*.json`, `cast/wizard.json` | WP-3 | C3. |
| WP-20 | Final quality pass | fable | Unity plus ffmpeg | captures only | WP-18, WP-19 | Re-run the score sheet on all slices; list gaps. |

Order and parallelism:

1. Start together: WP-1, WP-10, WP-17 (none depend on each other).
2. After WP-1: WP-2, WP-3, WP-4 in parallel (they share only the naming contract in 5.3 and read each other's functions; none edits another's files). WP-5 starts as soon as the contract is fixed and bakes on real fits when WP-2 delivers.
3. When WP-2 and WP-5 give first FBX: WP-6, then WP-7 (same editor in sequence). WP-8 once the prince branch is merged.
4. WP-9 after WP-7 and the duel clips.
5. WP-11 runs against WP-9, loops with the owners of WP-3, WP-5, WP-9 until the checks hold, then the owner watches the video (gate A10).
6. After the gate: WP-12, WP-13, WP-14 in parallel; then WP-15 and WP-16 (Unity, separate folders; two editors on two worktrees, or in sequence).
7. After the Slice 2 acceptance: WP-18, WP-19, then WP-20.

The three PRDs touch some of the same files. `View/Camp/**` and `Encounters/**` are also named by other work; the agent running WP-9 or WP-16 checks with whoever owns those paths at that time.

## 10. Test and capture plan

### 10.1 Blender and manifest tests

`verify.py` (5.7) is the first gate. It prints one line per check and exits non-zero on failure, so a script can run it for all characters. A numeric foot-plant report is saved with each build.

### 10.2 Unity tests

EditMode (skip with an ignore message when no art is present):

- manifest equals imported prefab (bones, sockets, shape keys, clip names, lengths);
- Humanoid avatar valid for each human, Generic valid for each demon;
- walk clip foot speed during stance at native speed under the limit;
- `ActorFactory.Create` returns the placeholder when the registry is empty or the prefab is missing, and logs nothing at error level.

PlayMode (`WardPresentationTests`, `ActorTests`):

- a rejected cast gives zero beats, zero bolts, zero releases;
- an accepted cast gives exactly one release, one bolt, one impact reaction; repeat for the Auto cast;
- two overlapping casts (one each side) each give one of each, with no shared state;
- a final break holds 1.5 s before the result screen shows, and the session outcome is unchanged;
- rematch in flight: no bolts, shards, lights, or beats remain; both actors idle;
- exit mid-animation (forfeit, Return, Esc): the stage is destroyed, Mira is reset to her camp pose and props, no exceptions logged;
- win, loss, rematch and early exit through normal controls (AutoPlay `WardBot` for the scripted player, then a manual pass by the agent where possible);
- the second opponent prefab swaps in.

Existing tests that must stay green: `WardBalanceTests`, `WardDuelTests`, `RhythmPartyTests`, `PlayerTests`, `DanceTests`, `IntegrationTests`, and `dotnet test DotNet`.

### 10.3 Captures and video

Shots are scripted, not hand-framed (pattern: `AgentScripts/EncounterCaptures.cs`, `GameViewSize.cs`, and the `unity:unity-pipeline` and `unity:unity-cli` skills for driving the editor without clicking). Each shot has a fixed camera, a fixed time of night, a fixed seed and a frame number.

| Shot | What | Framing |
|---|---|---|
| S1 | Prince approach and stop, wide gameplay camera | Walking view, 70 degree field of view, 1280 x 720 |
| S2 | Prince greeting, close-up face | Dialogue distance (about 2.5 m) |
| S3 | Prince beckon, walk, point at ground | Gameplay camera |
| S4 | Face line-up: three characters, neutral, blink, address, surprise | Fixed close-up |
| S5 | Mira cast: anticipation, release, recovery | Duel camera |
| S6 | Champion cast and reaction | Duel camera |
| S7 | Ward break and reaction, with the 1.5 s hold | Duel camera and push-in |
| S8 | Same shots at 844 x 390 and 932 x 430 | Phone-sized window via `GameViewSize.cs` |
| S9 | Same face shots under moonlight only, firelight only, and mixed | Fixed lights |

Shots are taken from the real night scene with the real lights (not a studio setup). Output: `Evidence/screenshots/characters/<date>/` (already git-ignored). Sims reference captures, if the probe runs: `Evidence/screenshots/characters/ref/`, local only.

Video, how it is made on this machine: in Play Mode set `Time.captureFramerate = 30`, write a numbered PNG sequence each frame from the game view for a scripted run (AutoPlay script `characters_s1`), then encode with `ffmpeg` (installed under the owner's WinGet packages) to H.264 mp4. Fixed frame rate means no dropped frames and the same run produces the same video. The "before" run uses `--placeholder-actors` on the same seed and script. A stacked or side-by-side file is made with ffmpeg `hstack`. Videos go to `Evidence/raw/characters/` (git-ignored). Footage containing Sims content stays in the local reference folder and is never joined to the public set.

### 10.4 Quality table and who scores

The comparison table from the brief, with checks that can be read off the captures:

| Check | Pass looks like | Read off |
|---|---|---|
| Faces | Silhouettes differ at a glance; eyes find the addressed person within 0.3 s; blink every 2.5 to 6 s; brows and mouth show attention, concentration and surprise at dialogue distance and at 844 x 390 | S2, S4, S8, S9 |
| Movement | Weight shifts before a step; feet do not slide (numeric report); head, shoulders and hips lag in order; direction change mid-walk blends with no pop | S1, S3, foot report |
| Interaction | Hand reaches the prop and stays; characters look at each other; gestures settle back to idle | S2, S3, props check |
| Duel | Anticipation, release, impact and recovery are four visible beats; the two casters differ; the effects leave the reaction visible | S5 to S7 |

Scores: 0 fails, 1 weak, 2 passes, per check, per character, side by side with the "before" run and, if available, the Sims 2 shot. The scoring agent (WP-11, a fable-tier agent) writes the reasons and the fix list. The owner is the final judge of faces and weight shift.

## 11. Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Scripted rigging and procedural clips do not reach hand-animated quality on faces and weight shift | Faces look mechanical; walks look stiff | Realistic level below. Gate at the owner's video. Fallback below. |
| Faces are tiny flat plates; gaze and mouth are hard to read | Eye contact lost at phone size | Method chosen per character from the island map; narrow duel field of view and push-in; brows and head lead carry most of the signal. |
| Blocky bodies with separate loose pieces show gaps when joints bend | Visible cracks at elbows, knees | Overlap and cover pieces added by script; bend range limited per bone; T-pose bend render in `verify.py` acceptance. |
| Long robes and skirts clip through legs | Mira's robe pokes through when walking | Skirt chain with limited swing, leg swing clamp for dress characters, test in walk and turn clips. |
| Costume hiding leaves holes under the removed tunic | Prehistoric look broken | Overlay fallback (4.2) is the floor. |
| Normals import as smooth | Look loses flat shading | Import normals as authored; check on first import; recompute flat if needed. |
| Mesh rights unconfirmed | Cannot publish art | Section 2.7: local meshes, committed tooling, fallback to placeholders. |
| Animation events fire twice during cross-fades or not at all when culled | Double bolts or no bolt | De-duplication by beat id, timer fallback, duel actors never culled (CH-27). |
| Firelight does not light faces; low `pixelLightCount` levels | Faces read as silhouettes at night | Test at shipped quality level; small face-lift on eye whites and skin values; optional per-conversation soft fill light, no shadows, off by default. |
| Crowd cost with real-time lights | Frame rate regression | Crowd tier, animation LOD, cull; measured with the 20-minute prompt run. |
| Merge conflicts with other branches editing `Actors/**`, `Encounters/**`, `View/Camp/**`, `View/Land/**` | Lost work | Package ownership table; merge the prince branch (`pre/n`) before WP-7 and WP-8; coordinate with other PRD agents. |
| Sims probe does not run on this branch | No side-by-side | Continue with original rigs; flag for later (owner answer D17). |
| Humanoid retarget changes poses on very different proportions | Clips look off on the Merchant or Carpenter | Test the shortest and tallest characters early; per-character style override. |
| Game tuning treats the walk speed as 3 m/s | Jog everywhere looks hurried | Walk speed is a tuning value; clips cover 0 to 3.2 m/s; the owner can lower it. |

What level is realistic, plainly:

- Good: readable silhouettes; a walk with planted feet, hip sway and a proper stop; clear greeting, beckon and point; a duel with four distinct beats and two different casting styles; blink, gaze with head lead, brow changes and a jaw ticker that make a face look attentive.
- Not realistic from scripts alone: subtle acting, believable weight on heavy actions, convincing speech lip shapes, cloth that always looks right, and the flow of a Sims 2 mocap-based library (its inventory is 25,275 named animations).
- Fallback if the face check fails twice: ship blink, gaze and brows only (no mouth shapes), larger brows, and stronger head and body language; mark the face check as known weak.
- Fallback if a clip fails: fewer, stronger poses with longer holds beat many weak clips; the placeholder remains for any character not ready.
- Where the owner's eye is needed: the three faces (do they read as the people in the lineup, and do they feel alive), the walk and stop feel, the weight of the champion's cast, and the final Ward break. Four short looks, each with a reference clip.

## 12. Open decisions for the owner

| # | Decision | Recommended default (work proceeds on this) |
|---|---|---|
| 1 | Do you confirm the right to publish the meshes and derivatives in the public repo? | No until you say so. Commit tooling, clips, code and controllers; keep meshes, prefabs and edited .blend local. |
| 2 | Is the Road Hero the Ash champion and the later rival leader? | Yes. You name him. |
| 3 | Who is the player's body in the practice duel against Mira? | Juno. |
| 4 | Do the prehistoric costumes keep each character's identity colour (Prince maroon, Mira plum, champion blue-grey)? | Yes, as dyed hide and fur. |
| 5 | Faces: keep the blocky lineup style with more difference between people, or move toward rounder faces? | Keep the lineup style, differentiate by parameters. |
| 6 | Quality gate at the first video: accept a stylised look, with the simpler face fallback if two iterations still fail? | Yes. |
| 7 | Phone-sized test windows. | 844 x 390 and 932 x 430. |
| 8 | Who are the six companions and the wizard? | New heads and hair on the existing bodies; you approve a lineup image at the start of Slice 3. |

## 13. Choices I made that you might want to overrule

1. The Road Hero is the first opponent, because he is the broadest, the tallest of the men with a sword and cape silhouette that contrasts with Mira's narrow staff silhouette, and he can later serve as the rival leader.
2. The duel test setup is a new encounter id `ward_champion`, so the existing practice duel and its tests stay as they are.
3. Meshes stay local until you confirm rights; tooling and animation-only data are committed. This delays public commits of art but never breaks the build.
4. Prehistoric costume is the first-class work. The supplied look is treated as Historic, so Slice 3 is mostly variant plumbing and new faces.
5. One shared template armature with rigid weighting by loose piece, because the meshes are disconnected pieces, not smooth skins. This gives a chunky, toy-like motion that suits the style but limits softness.
6. Clips are authored once on the template and shared through Humanoid, with a small style offset per character, not unique clips per person.
7. Face rig uses shape keys for brows, mouth and (if the eyes are flat) iris slide, and bones for jaw and round eyes; the method is picked per character from the island map.
8. Gaze uses the Animation Rigging package that is already in the project; no new package is added.
9. Flat-colour materials are remapped to about eight role slots so costume variants are palette swaps. This is a deliberate reduction of the 12 to 16 flat colours each model has.
10. The `ICharacterActor` interface is not changed. A second interface carries the new abilities, so `SimCharacterActor` and the Sims layer are untouched.
11. Art loads by name from a `Resources` folder so the repo builds with no art. This costs a little build-size discipline.
12. Demons use a shared Generic rig rather than Humanoid, as the brief allows.
13. The duel camera uses a narrower field of view than the walking view and a short push-in on a ward break, so faces are readable at phone size. This changes the look of the duel compared with today's wide over-the-shoulder shot.
14. Video is a numbered PNG sequence at a fixed 30 fps, encoded with ffmpeg, rather than real-time screen capture. This is repeatable but needs a scripted run.
15. Performance numbers in section 5.8 are targets I chose; they are not measured yet.
