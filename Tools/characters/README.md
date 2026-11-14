# Character pipeline (Blender 4.5, headless)

Tooling for `Docs/PRD_CHARACTERS.md`. This folder holds scripts, contracts and geometry-free data only. The supplied meshes, every generated .blend, FBX with a mesh, render or capture stays local (see `Docs/PROVENANCE_CHARACTERS.md`).

Status: WP-1 (intake, island maps, contract) and WP-17 (provenance note) are done. WP-2 to WP-5 start from section 2 below.

## 1. Running things

All scripts take paths as arguments, run with Blender 4.5 headless, and never execute anything from the input folder:

```
"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" --background --factory-startup --disable-autoexec --python <script> -- <args>
```

| Script | Args | Does |
|---|---|---|
| `Tools/characters/inspect_cast.py` | `--src <extracted zip folder> --out <dir outside the repo> [--only k1,k2] [--islands-dir <dir>] [--no-blend]` | Re-runs the PRD section 2 inspection for all 16 characters, writes `<out>/inspect/inspection.json`, `summary.txt`, `index.json`, `islands/<key>.json`. Exits 1 if any number differs from PRD 2.3. `--islands-dir Tools/characters/cast/islands` also refreshes the committed island maps (and `cast/index.json` when all 16 run). |
| `Tools/characters/check_contract.py` | `--src ... --out ... [--only ...] [--save-blend]` | Builds the development stub armature for every human and checks it against the contract. Exits 1 on failure. |
| `python -I Tools/characters/lib/contract.py` | none | Re-generates `Tools/characters/contract.json` from `lib/contract.py`. Plain python, no Blender. |

`--src` is the folder that holds `Models/*.fbx` and the `.blend` (on this machine `WorldMVP\NEWSTUFF\characters_extracted\RomanVillage_DarkFantasy_Characters`). `--out` must be outside the repository (`inspect_cast.py` refuses otherwise). Local work folder: `WorldMVP\NEWSTUFF\characters_work\`.

Re-running is deterministic: same inputs give byte-identical JSON (checked).

## 2. Contract (authoritative; WP-2, WP-3, WP-4, WP-5 build to this)

The single source of truth is `Tools/characters/lib/contract.py` (pure Python, no `bpy`). `Tools/characters/contract.json` is generated from it for the Unity side. If this prose and `contract.py` disagree, `contract.py` wins; fix the prose. Changing a name is a contract change and needs the owner. Nobody invents a bone, part, shape key, socket, slot or clip name that is not in `contract.py`.

### 2.1 Conventions (measured, not assumed)

- Units: metres, Blender Z up. Scale 1, rotation 0, origin at floor level (z = 0) for every object after import.
- Authoring forward is Blender -Y (all 16 supplied characters face -Y: eye centre is in front of the head centre; checked in every island map `face.forward_check.faces_minus_y`).
- Sides: `.L` is the character's left and is Blender +X; `.R` is -X. Checked against the props: the Woodcutter's axe (viewer's left in the lineup) is in the character's right hand, `x < 0`.
- Export: forward -Z, up Y. Blender's own axis conversion (`axis_conversion(to_forward='-Z', to_up='Y')`) maps Blender -Y (front) to FBX +Z and character left (+X) to FBX +X; Unity's importer then mirrors X, so the character faces +Z in Unity with `.L` on Unity -X. This is derived from the exporter maths. It is NOT yet seen in Unity. WP-6 must confirm with its forward-axis test (PRD 5.6) and report; if wrong, only the export axis arguments change.
- Bone roll rule (so clip data means the same on every character): for every bone, local +X is as close as possible to world +X (character left); local Y runs head to tail along the bone; local Z = X cross Y. Consequences: a rotation about local X is flexion/extension. Positive X rotation tilts an upward-pointing bone forward; on a downward-pointing leg bone it swings the foot backward. Bones that run along world X (`Shoulder.*`, `Sock.Hip.R`) have no defined roll and are exempt.
- All 14 humans share one A-pose. Rest arm angle (shoulder-cap centre to hand centre, from vertical) is 21.1 to 25.0 degrees on every character and symmetric left to right (the PRD's "about 17" used the hand tip, not the centre). `verify.py` should allow 21 to 25 and require mirrored left/right within 1 degree.

### 2.2 Objects in a build scene

One character per Blender scene (`common.reset_scene()` between characters; `common.load_character(key, src, out)` does it).

| Object | Name | Notes |
|---|---|---|
| Root empty | `<key>` (e.g. `mira`) | at the origin |
| Armature object and data | `Armature` | child of the root |
| Meshes | `Body`, `Legs`, `Head`, `Cloth` (only if the character has one), `Prop` (only if held item) | renamed from `02_Mira_Body` etc. by `load_character`. Mesh data has the same name. |
| Overlay meshes (WP-4) | `Ov_<name>` (e.g. `Ov_Mantle`, `Ov_Kilt`, `Ov_Boots`) | skinned to the same armature, one object each, toggled by SetActive |

Cast keys (command lines, file names): `prince, mira, juno, sol, woodcutter, carpenter, hesta, merchant, dancer_ochre, dancer_ash, dancer_moss, dancer_plum, carrier, champion` (Road Hero), `stalker, brute`. First three: `prince, mira, champion`.

### 2.3 Bones (humans)

Every human gets every row marked "all". Parents are fixed. Hips is the single root of the deform hierarchy (there is no separate Root bone on humans). Bone budget: at most 45 including sockets; the largest possible set is 42 (checked).

| Bones | Parent | Present |
|---|---|---|
| `Hips` | none | all |
| `Spine` | Hips | all |
| `Chest` | Spine | all |
| `Neck` | Chest | all |
| `Head` | Neck | all |
| `Jaw`, `Eye.L`, `Eye.R` | Head | all |
| `Shoulder.L/R` | Chest | all |
| `UpperArm.L/R` | Shoulder.x | all |
| `LowerArm.L/R` | UpperArm.x | all |
| `Hand.L/R` | LowerArm.x | all |
| `UpperLeg.L/R` | Hips | all |
| `LowerLeg.L/R` | UpperLeg.x | all |
| `Foot.L/R` | LowerLeg.x | all |
| `Toes.L/R` | Foot.x | all |
| `Cape.1`, `Cape.2`, `Cape.3` | Chest, Cape.1, Cape.2 | only if `cast/index.json` `optional_chains.cape` (Prince, Mira, champion). One centre chain. |
| `Hood.1`, `Hood.2` | Head, Hood.1 | only if `optional_chains.hood` (Mira) |
| `Skirt.F`, `Skirt.B`, `Skirt.L`, `Skirt.R` | Hips | only if `optional_chains.skirt` (dress family: Mira, Hesta, Dancer Ash, Dancer Plum) |

Sockets (non-deform bones, no weights, no vertex groups, every human has all nine):

| Socket | Parent | Position |
|---|---|---|
| `Sock.Hand.R`, `Sock.Hand.L` | Hand.R, Hand.L | hand centre; if the character holds a prop in that hand, at the prop's grip point |
| `Sock.StaffTip` | the socket of the hand that holds the prop (`Sock.Hand.L` for Mira, carpenter, carrier; `Sock.Hand.R` for champion and woodcutter); `Sock.Hand.R` when there is no prop | the prop's head point (staff crystal, blade tip, axe head, lantern). With no prop: 0.45 m above `Sock.Hand.R`, flagged `placeholder` in the manifest. |
| `Sock.SpellOrigin` | Sock.StaffTip | 0.05 m beyond StaffTip |
| `Sock.Back` | Chest | behind the spine, 0.14 m back |
| `Sock.Hip.R` | Hips | right hip, 0.21 m out |
| `Sock.HeadTop` | Head | crown |
| `Sock.Foot.L`, `Sock.Foot.R` | Foot.L, Foot.R | ground contact, z = 0 under the foot centre |

The Unity importer (WP-6) sets the Humanoid map explicitly from `contract.json` `humanoid_map` (it does not rely on Unity auto-mapping). Jaw, Eye.L, Eye.R, Shoulder, Toes are mapped too.

Demons: a separate Generic armature (names in `contract.DEMON_BONES`, root `Root`) owned by WP-13. WP-1 does not classify demon pieces (`classified: false` in their island maps).

### 2.4 Face (WP-3)

- Bones: only `Jaw`, `Eye.L`, `Eye.R`. Brows, lids and mouth are shape keys. The PRD's `Brow.*` and `Lid.*` bones are dropped from the contract (rigid pieces: a shape key is the same cost and imports reliably).
- Shape keys on the `Head` mesh, exact names, weights 0 to 1, Basis key `Basis`: `Blink.L`, `Blink.R`, `BrowRaise.L`, `BrowRaise.R`, `BrowFurrow`, `BrowWorry`, `MouthSmile`, `MouthFrown`, `MouthRound`, `MouthOpen`. Ten keys plus Basis, limit 16. Eyes need no gaze keys (see Gaze).
- Expression presets (names fixed): `neutral, attentive, concentrating, surprised, pleased, pained, determined`, stored as data in `cast/<key>.json` under `face.expressions`.
- Weighting: `Eye.L/R` carry the iris and pupil islands only (island roles `eye_iris`, `eye_pupil`); the outline and sclera stay on `Head`. `Jaw` carries the mouth island and any beard islands (roles `mouth`, `beard`). Everything else on the head is `Head`; the hood (role `hood`, Mira) is `Hood.1`. These are the `bone` suggestions in the island maps; WP-2's `skin.py` applies them, WP-3 only adds shape keys and the manifest `face` section.
- Eye bone positions (WP-2 `rig.py`): bone head = eye outline centre moved 0.025 m back (+Y); tail 0.06 m forward. Limits for the runtime: horizontal +-14 degrees, vertical +-5 degrees (see Gaze).
- Shape keys on a rigid piece are translations and scales only; keep brow tilt small.

### 2.5 Material slots (WP-4)

Role slots (material names `Role_<slot>` in the exported FBX and the manifest): `skin, hair, cloth_main, cloth_trim, leather, metal, eye_white, eye_dark, emissive`. Mapping: sclera to `eye_white`; iris, pupil and eye outline to `eye_dark`; brows and beard to `hair`; glow pieces to `emissive`. A character uses only the slots it needs and at most 8 (`verify.py` checks the count, `hidden` excluded). Pieces a costume hides are not deleted: they are assigned to an extra slot `Role_hidden`, which the Unity importer maps to a no-draw material, so a live costume swap can show and hide them. Until WP-4 remaps, materials keep their supplied `RV_xxxxxx` names; emissive ones carry the suffix `_Ember` (`RV_A05BE4_Ember` Mira, `RV_F0B350_Ember` carrier lantern, `RV_FF832F_Ember` demon eyes). The island maps flag emissive islands.

### 2.6 Clips and animation files (WP-5)

- 30 fps, in place, no root motion. One action per clip; action name = clip name = Unity clip name (the importer strips any `Armature|` prefix, up to the last `|`).
- Clip names and sets (`contract.CLIP_SETS`): `idle` (Idle_A, Idle_B, Idle_Look, Idle_Staff), `locomotion` (Walk, WalkBrisk, Jog, StartL, StartR, StopL, StopR, Turn60L, Turn60R, TurnSmallL, TurnSmallR), `social` (Greet, Nod, ShakeHead, Talk_A, Talk_B, Talk_Emphasis, Listen), `prince` (Beckon, LeadLook, PointGround), `duel` (CastAnticipate, CastRelease, CastRecover, CastWeave_Loop, Block, RecoilLight, RecoilHeavy, WardBreak, Victory, Defeat, Surprise). Role clips (PRD 6.2) are added to new sets by WP-12/WP-13 in the same file.
- Per-character style variants are extra actions in the same file named `<Clip>@<key>` (for example `CastAnticipate@mira`, `CastAnticipate@champion`, `Idle_Staff@mira`). The Unity override controller maps them; characters without a variant use the base clip.
- Files: `<out>/anim/anim_<set>.fbx` (armature and actions, no mesh), plus `<out>/anim/anim_manifest.json`. Copied by WP-6 to `Assets/Characters/Animation/anim_<set>.fbx`.
- Clip record in the manifest: `{name, set, length_s, frames, fps, loop, additive, native_speed_mps, events:[{name, frame}]}`. Loop clips: `contract.LOOP_CLIPS`; additive clips: `contract.ADDITIVE_CLIPS`. Event names: `Release, Impact, FootL, FootR, PropHit, BeatHit`. Events live in the manifest only (FBX cannot carry them from Blender); WP-6 writes them onto the imported clips.
- Rotations in clip data are Euler degrees, bone rotation mode XYZ, in the bone-local axes of the roll rule in 2.1.

### 2.7 File layout and manifest

All under `--out` (outside the repo until the owner confirms rights):

```
<out>/inspect/...                         WP-1 outputs
<out>/characters/<key>/<key>.blend        edited source
<out>/characters/<key>/<key>.fbx          mesh + rig, no clips
<out>/characters/<key>/manifest.json      per-character manifest
<out>/characters/<key>/reports/           verify output, foot report, renders
<out>/anim/anim_<set>.fbx, anim_manifest.json
<out>/cast_manifest.json                  merged index of all manifests (build_cast.py rewrites it)
```

Unity targets (WP-6): `Assets/Characters/Resources/Characters/Models/<key>.fbx`, prefab `Assets/Characters/Resources/Characters/<key>.prefab` (loaded with `Resources.Load("Characters/<key>")`), `Assets/Characters/Animation/anim_<set>.fbx`.

Per-character manifest sections (`contract.MANIFEST_SECTIONS`); each step writes only its own section via `ctx.set_section(name, data)`:
`identity` (common), `meshes`, `bones`, `sockets`, `skin`, `verify`, `export` (WP-2/WP-6), `face` (WP-3), `costumes` (WP-4), `clips` (WP-5). Manifests contain names and numbers only, no geometry.

### 2.8 Script entry points (who owns what)

Steps run in this order per character. Signatures are fixed so each package can be developed and tested alone:

| Step | Function | Owner | Writes |
|---|---|---|---|
| load | `common.load_character(key, src, out) -> BuildContext` | WP-1 | scene, `ctx.parts`, `ctx.islands` |
| rig | `rig.fit_rig(ctx)` | WP-2 | `ctx.armature`, sections `bones`, `sockets` |
| skin | `skin.skin_character(ctx)` | WP-2 | vertex groups, Armature modifiers, section `skin` |
| face | `face.build_face(ctx)` | WP-3 | Head shape keys, section `face` |
| costume | `costume.apply_costume(ctx, costume_name)` (default `"prehistoric"`) | WP-4 | slots, hidden pieces, `Ov_*` meshes, props, section `costumes` |
| clips | `anim_core.build_clips(ctx_or_armature, set_names=None, style=True)` | WP-5 | actions on the armature, section `clips` |
| verify | `verify.verify_character(ctx) -> list of {check, ok, detail}` | WP-2 | section `verify` |
| export | `export_unity.export_character(ctx)`, `export_unity.export_anim_sets(ctx, set_names)` | WP-6 | FBX files |
| driver | `build_cast.py -- --src --out --only [--steps rig,skin,face,costume,clips,verify,export] [--costume prehistoric]` | WP-2 | runs the above, writes manifest |

Rules: a step runs on a scene that already ran the steps above it; a step never edits another package's files or another package's manifest section; every step is idempotent from the original FBX (the driver always starts from `load_character`). `BuildContext` has `key, info, src, out, root, parts, armature, manifest, islands` and `ctx.path("character_fbx")` etc.

Development stub: until WP-2 delivers, `lib/stub_rig.py` `build_stub_armature(ctx)` builds a contract-correct armature (positions from the island map, roll rule applied, no weights). WP-3, WP-4 and WP-5 may call it to test. It is not the production rig and carries no weights; `rig.fit_rig` replaces it with the same names, parents and roll rule.

Ownership of `cast/` files (so nobody edits the same file):
- `cast/islands/<key>.json` and `cast/index.json`: WP-1, generated, do not hand-edit.
- `cast/<key>.json`: WP-3 (face parameters and expressions). Created by WP-3; WP-14 adds the rest.
- `cast/costumes/*.json`: WP-4.
- `cast/rig/<key>.json`: WP-2 (optional per-character fit overrides).
- `clips/style/<key>.json`: WP-5 (style offsets).

## 3. Island maps (`cast/islands/<key>.json`)

Geometry-free: names, indices, counts, centres, sizes, no vertex or face data. Island = vertices connected through faces. Ids are `<Part>.<nn>` (nn = order of the island's first face); stable for the unchanged supplied FBX. `islands.vertex_islands(obj)` returns the vertex indices in the same order, so `skin.py` can weight by id; `islands.check_map_matches(map, ctx)` tells you if the mesh no longer matches the committed map.

Fields per island: `id, index, faces, tris, verts, ngons, materials (RV names, most used first), hex, centre, size, min, max, centroid, side (L/R/C), aspect, open_edges, role, bone, nested_in, emissive`. `role` and `bone` are position-rule SUGGESTIONS (torso, neck, shoulder_cap, upper_arm_piece, lower_arm_piece, cuff, hand, thumb, thigh, shin_or_boot, cuff_or_boot_top, foot, robe_or_skirt, skirt_hem, torso_detail, cloth_back, cloth_shoulder_or_wrap, cloth_front_or_trim, hood_or_collar, prop_piece; head: head_skin, ear, eye_outline, eye_sclera, eye_iris, eye_pupil, brow, nose, mouth, hair, beard, hood, head_other). WP-2 and WP-3 may override them in their own data.

Top-level fields: `height` (body, without props), `height_with_props`, `landmarks` (shoulder_z, elbow_z, wrist_z, hip_z, knee_z, ankle_z, neck_z, head_bottom_z, crown_z, head_centre, head_size, arm angle, skin_material, `prop` with holding hand, axis, grip point, head point, length), `face` (eye layers and shape, eye spacing, brows, mouth, nose, ears, hair, beard, hood, forward check), `joint_gaps`, `sleeve_overlap`, `adjacency` (islands closer than 3 cm with surface gap, penetration and bbox overlap).

## 4. Resolved unknowns (from the inspection of 14 Nov 2026)

| Unknown (PRD 2.5, 5.4) | Answer |
|---|---|
| Eye shape, round or flat | Neither. All 14 humans have the identical construction: four stacked flattened ellipsoids per eye (dark outline 0.054 to 0.061 m wide, 0.026 to 0.029 deep, 0.033 to 0.037 high, depth/width 0.47; cream sclera; iris; pupil), iris proud of the sclera by 4.6 to 5.2 mm, eye spacing 0.084 to 0.096 m. Class `disc`. |
| Gaze method | `Eye.L/R` bones carrying iris and pupil only, pivot 0.025 m behind the outline centre. The iris is as tall as the sclera (0.022 m), so there is almost no vertical room: horizontal +-14 degrees (iris slides about 11 mm of the 29 mm free sclera width), vertical +-5 degrees. Looking up or down is carried by the head. No gaze shape keys are needed for any human. Demon eyes are single emissive ellipsoids (WP-13). |
| Joint gaps | Skeletal joints (cloth and props excluded), from interpenetration depth: covered (reach 2 cm or more) at shoulder, wrist, hip, ankle, neck. Touching only at every elbow and every knee on all 14 humans (elbow reach 0 to 2 mm, knee gap 1 to 4 mm). A rigid bend there opens a crack. WP-2 must add a joint cover at each elbow and knee: a small sphere or cylinder cap of the limb's radius (about 0.055 m at the elbow, 0.07 m at the knee), weighted to the lower bone, as extra pieces in the same mesh. `joint_gaps.joints` in each map has the per-joint numbers. Hips to spine waist boxes are touching or open on some characters, harmless (belts and waist boxes). |
| Sleeve overlap | Shoulder caps overlap both torso and upper arm (covered). No arm piece is fully hidden inside another (`nested_pieces` empty on all). Upper arm to forearm just touches (see elbow). Cuffs and hands sit 2 to 3 cm into the forearm. |
| Forward axis | Blender -Y for all 16. Unity +Z after export forward -Z, up Y (maths in 2.1, to be confirmed in Unity by WP-6). |
| Flat normals | Yes. In all 16 meshes every corner normal equals its face normal (fraction 1.0), although polygons carry the smooth flag. Unity must import normals as authored (Import Normals), which keeps the faceted look. |
| Long garments | Dress family (leg part 348 tris): Mira, Hesta, Dancer Ash, Dancer Plum. In all four, the body part holds a robe piece `Body.02` (Mira: z 0.105 to 0.974) around the thighs and shins, and the legs sit inside it (`nested_in`). Role `robe_or_skirt`, suggested bone `Hips` plus the `Skirt.*` chain. Mira's hem ring is `Legs.00` (`skirt_hem`). |
| Mira's hood and staff | Hood: the big hair-coloured shell `Head.19` (role `hood`) plus trim `Head.20/21`, and cape pieces `Cloth.00` to `Cloth.03`. Staff: held in her LEFT hand (character left, +X), grip z 0.965, crystal (emissive piece `Prop.02`) 0.89 m above the grip, tip z 1.855, staff top 1.967. |
| Props | Mira staff L, Carpenter hammer L, Timber Carrier lantern L; Road Hero (champion) sword R (blade down, tip z 0.234), Woodcutter axe R. Details in `landmarks.prop`. |
| Left/right symmetry | Every human is symmetric in rest pose (arms 21.1 to 25.0 degrees, mirrored). |
| Duplicate material names | Only Mira: `RV_A05BE4_Ember` and `RV_A05BE4_Ember.001` (two emission strengths, 0.9 and 0.35). |
| .blend | Saved by Blender 4.3.34; 78 objects, 16 empties, 62 meshes, 17 collections (16 characters and `Collection`), no text blocks, no libraries, no lights or cameras, image `Render Result` only. All numbers in PRD 2.3 reproduce (script exits 1 otherwise). |

Not resolved here: whether the FBX normals survive Unity import as faceted (WP-6 checks on first import), whether the forward axis comes out +Z in Unity (WP-6), and whether `WorldLit` takes a palette per role (WP-6).

## 5. What changed from the PRD (also noted in PRD section 5.3a)

1. Brow and lid bones removed; brows, lids, mouth corners are shape keys. Only `Jaw`, `Eye.L`, `Eye.R` are face bones.
2. Gaze shape keys removed from the list (no human needs them). Gaze is eye bones for iris and pupil only, with the limits above.
3. Role slots: nine names plus `hidden` (the PRD said about eight). One character still uses at most 8.
4. `Sock.StaffTip` hangs from the socket of the hand that holds the prop, not always the right hand (Mira holds her staff in her left).
5. Added files beyond the PRD list: `lib/contract.py`, `contract.json`, `lib/stub_rig.py`, `check_contract.py`, `cast/index.json`, `cast/islands/`.
6. `cast/<key>.json` is face data only (WP-3). Island maps live in `cast/islands/`, so the PRD's two uses of `cast/*.json` do not collide.
7. Per-character clip style is shipped as `<Clip>@<key>` variants in the shared animation files, mapped by the Unity override controller, not baked into per-character files.
8. Rest arm angle is 21 to 25 degrees by centre-to-centre measure; the PRD's 17 was a hand-tip measure.

## 6. Adding a character (short, CH-37)

1. Put the new mesh FBX under `<src>/Models/` and add a row to `CAST` in `lib/contract.py` (key, number, fbx name, display, rig, slice); run `python -I Tools/characters/lib/contract.py`.
2. Run `inspect_cast.py ... --only <key> --islands-dir Tools/characters/cast/islands`; read `face.found`, `joint_gaps.not_covered`, `landmarks.prop` in the new map.
3. Run `check_contract.py --only <key>`.
4. Add `cast/<key>.json` (face tuning, WP-3), a costume file (WP-4) and a style file (WP-5), then run `build_cast.py --only <key>` (WP-2 driver).
