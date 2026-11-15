# Characters in Unity (WP-6 import, WP-7 actor)

The Unity half of `Docs/PRD_CHARACTERS.md`. Written 15 Nov 2026 on branch `char/unity`. The art is local until the owner confirms the rights (`Docs/PROVENANCE_CHARACTERS.md`); everything here builds, boots and passes its tests without it, with the placeholder figures.

## 1. Getting the art into the project (this machine only)

```
python -I Tools/characters/sync_unity_local.py --src WorldMVP\NEWSTUFF\characters_work\integrated
unity command script run --file Assets/Editor/Characters/CharacterBuilder.cs --entry World.Characters.Editor.CharacterBuilder.Run
```

| What | Where | Git |
|---|---|---|
| Character FBX (`<key>@<costume>.fbx`), `cast_manifest.json` | `Assets/LocalBaked/Characters/Models/`, `.../cast_manifest.json` | ignored (the sync refuses to copy unless `git check-ignore` says so) |
| Role-slot materials, merged meshes, prefabs | `Assets/LocalBaked/Characters/Materials/`, `Meshes/`, `Resources/Characters/<key>@<costume>.prefab` | ignored |
| Animation-only FBX (skeleton and clips, no mesh; the sync refuses a file with a Geometry node), `anim_manifest.json`, `anim_variants_<key>.json`, `Masks/UpperBody.mask` | `Assets/Characters/Animation/` | tracked |

A player build made on this machine includes the prefabs (they sit in a `Resources` folder); a clone of the public repository has no `Assets/LocalBaked` and every actor is a placeholder.

## 2. Import (WP-6, `Assets/Editor/Characters/`)

- `CharacterImport` (AssetPostprocessor, both folders): normals imported as authored (flat), no tangents, blend shapes on (models), read/write off, no mesh compression, `optimizeGameObjects` off (sockets, eyes, jaw and cloth chains stay transforms), no cameras or lights. Part meshes are renamed `Mesh.Body`, `Mesh.Head`, ... on import: the mesh `Head` would clash with the bone `Head`, which Humanoid refuses. Clip settings from the manifests on every import: loop flag, no loop pose, every root channel baked into the pose with its original values (clips are in place; the game moves the root), additive reference pose frame 0 for additive clips, events (`OnActorEvent(name)`, no receiver required) at the manifest frames.
- `CharacterBuilder`: Humanoid avatars from `contract.json` `humanoid_map` (24 bones, explicit, no auto-mapping) for every human model and every animation file. An animation-only FBX has no rest pose of its own (its default pose is a take's first frame), so its avatar skeleton is taken from the rest pose of the rig it was baked on (`anim_<set>@<key>` from that character, base sets from the template, Mira); without this the retargeted walk is broken. Per character and costume: one `World/Lit` material per role slot from the manifest colour (`_SelfLit` 1 on `emissive`), `Role_hidden` mapped to a draw-nothing material (`World/ActorHidden`), then ONE merged skinned mesh with one submesh per visible slot (hidden pieces dropped, the ten face blend shapes carried over), an `Animator` (no controller: the actor drives a PlayableGraph), a capsule collider and `RiggedCharacter` (sockets, face data, the resolved clip set: the character's own `<Clip>@<key>` variant when it exists, else the base clip).
- Report of a run: `Library/CharacterBuild.txt`.
- Measured: the character faces +Z with `.L` on -X (no extra rotation needed), scale 1 (head top 1.84 m for the prince), feet on y = 0. All 28 builds (14 humans x 2 costumes) import as Humanoid. Over the 3.2k hero budget: `champion@prehistoric` 3521 and `sol@prehistoric` 3416 visible triangles (art data). Draw calls per hero: 9 (one per slot). Bones 36 to 42.

Tests: `Assets/Editor/Characters/Tests/CharacterImportTests.cs` (EditMode; ignored without art): bones, sockets, Humanoid avatar, blend shapes, slot materials and colours, forward axis, scale, triangle counts, clip list, variants, loop and additive flags, lengths, events, root curves, no mesh in animation files, and the Walk clip's planted-foot speed at native speed (prince 0.053, Mira 0.017, champion 0.037 m/s mean; limit 0.08).

## 3. Runtime (WP-7, `Assets/World/Game/Actors/`)

| Type | What |
|---|---|
| `ActorFactory` | Moved out of `PlaceholderActor.cs`, both signatures unchanged. Asks `CastRegistry.PrefabFor(id)`; a prefab gives a `RiggedActor`, anything else the `PlaceholderActor` as before (nothing logged). |
| `CastRegistry` | Actor id to cast key (`prince`, `mira`, `ash_chief` and `rival_leader` -> `champion`, `sawyer` -> carpenter, `merchant`, `builder` -> woodcutter, `host` -> sol, `market_keeper` -> hesta, `ward_player` -> juno, `dancer0..3`, `camp_figure_*` by hash, `keeper_*` -> champion). `Reviewed` = prince, Mira, champion: the only keys used by default. `EnableAllRoles` (command line `-allCast`) uses every built body; `ForcePlaceholders` (`-placeholder-actors`) uses none. `CostumeFor(key)` (default prehistoric) is the hook for the costume switch; `LoadPrefab(key, costume)`, `HasArt(key)`. |
| `RiggedActor` | `ICharacterActor` + `IExpressiveActor` + `IMotionShaper`. Keys: `idle`; `wave`/`greet` -> Greet; `beckon` -> Beckon; `cheer` -> Victory; `surprise` -> Surprise; `clap` -> Talk_Emphasis; `stretch` -> Idle_Look; `hammer` -> Nod; `cast` -> CastAnticipate, CastRelease, CastRecover. |
| `IExpressiveActor` | `SetGaze(Transform)`, `SetGaze(Vector3?)`, `GazePoint`, `SetExpression(name, w)`, `ResetExpression()`, `SetSpeedHint`, `GroundSpeed`, `Socket(name)`, `Attach(prop, socket, pos, rot)`, `DetachAll(socket)`, `PlayBeat(clip)`, `PointAt(Vector3?)`, `SetTalking(bool?)`, `ForceIdle()`, `ResetPresentation()`, `SetYaw`, event `BeatEvent(name, beatId)`. |
| `ActorFace` | Blink (manifest timing, double blinks), expressions with channel gains, talk ticker (mouth key and jaw), gaze: eyes take what they can (+-12, +-5 degrees) at once, head (share 0.4, lag 0.15 s, limit 55/25) and chest (share 0.15) follow. |
| `ActorCloth` | Cape (trails with speed, swings out of turns), hood (lags the head), skirt panels (follow the thighs 68 percent forward, 26 sideways). Humanoid clips carry no curves for these bones, so this is what moves them. |
| `Npc` | `PointAt` and `PlaceAt` reach rigged actors through `IExpressiveActor`; `UpdateWalk` multiplies its speed by `IMotionShaper.SpeedScale` (1 for the placeholder). |

How it moves (all from the manifest): a PlayableGraph evaluated in `LateUpdate` after `Npc` has moved the root. Gaits Walk 1.4, WalkBrisk 2.2, Jog 3.6, Run 7.0 m/s blend by measured ground speed; their phase advances by ground covered divided by the blended stride, so the planted foot stays put at any speed. A walk from standing: a turn clip first if the path turns 40 degrees or more, then the start clip's weight shift (the walker waits through its zero-distance frames, about 0.1 s), the start clip's own distance curve, then 9 m/s^2 up to the walker's speed. Near the goal: 9 m/s^2 down to walking pace and the stop clip (StopL or StopR by foot phase) timed by its distance curve, landing exactly on the goal. Standing turns of 40 degrees or more play Turn60L/R with the root following the clip's yaw curve (scaled to the angle); smaller turns and turns while walking rotate smoothly. Gestures play full-body when standing and on the upper body (mask) while walking; the talk loop (prince and Mira Talk_B, the champion Talk_Emphasis) plays on the upper body while one of the NPC's speech bubbles shows. Automatic gaze: the player while speaking, greeting or standing close in front; the pointed spot at the start of a point. Automatic expression from the gesture (pleased on a greeting, determined on a point or beckon, concentrating or determined on a cast, surprised, pained) and attentive while talking. Night: each slot material gets the lighting package's Person values (`LightShapes.SelfLit/Rim`), with more self-light on eye whites (0.6) and irises (0.4) so eyes read in firelight; the glow slot stays self-lit. The prince's carried light comes from `LightDecorator` as before (it follows the `Npc`). LOD: beyond 25 m from the camera no face, gaze or cloth and the graph every second frame; beyond 60 m or off screen every fourth; never during an encounter (`RiggedActor.LodEnabled` off for tests and captures).

## 4. For the later packages

- **WP-8 prince binding**: `Npc.Actor as IExpressiveActor` gives `PlayBeat("Beckon" | "PointGround" | "LeadLook" | "Greet" | "Nod")`, `PointAt(point)` holds the point (loops the clip's hold frames) until `PointAt(null)`, `SetGaze`, `SetExpression`. `ActorKeys.Beckon` and `Greet` already map. `PrinceVillagers` decorations (circlet, feathers, sun standard) apply only to the placeholder; the rigged prince has his own circlet overlay.
- **WP-9 duel director**: `PlayBeat` for every duel clip (CastAnticipate, CastRelease, CastRecover, CastWeave_Loop, Block, RecoilLight, RecoilHeavy, WardBreak, Victory, Defeat, Surprise), each start increments the beat id carried by `BeatEvent` (`Release` on CastRelease frame 2, `Impact` on the reactions); events fire once per crossing from the manifest times (not from Unity's animation events). `Socket("Sock.SpellOrigin")`, `ForceIdle`, `ResetPresentation`. Duel actors are never LOD-reduced while an encounter runs.
- **WP-15 crowd**: nothing crowd-specific yet; `CastRegistry.KeyFor("camp_figure_N")` picks a body; budgets in section 2.
- **Costume switch (WP-18)**: replace `CastRegistry.CostumeFor`, then call `RiggedActor.SwapCostume(costume)` on live actors: same rig and clips, position, facing and runtime props kept. Both costumes of all 14 humans are built locally.
