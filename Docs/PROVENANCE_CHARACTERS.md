# Provenance of the supplied character meshes

Status: NOT CONFIRMED. The owner has not yet confirmed the right to publish these meshes or anything derived from them. Until that line is signed below, no mesh-bearing file goes into this public repository.

Written 14 Nov 2026 (WP-17 of `Docs/PRD_CHARACTERS.md`, section 2.7). Facts were measured from the supplied files on that date; nothing here is legal advice.

## 1. What the files say

Package: a zip the owner supplied, extracted to the local folder `WorldMVP\NEWSTUFF\characters_extracted\` (never committed). Contents: `Character_Lineup.jpg` (1760 x 1066 render of all 16), `README.md`, `RomanVillage_DarkFantasy_Characters.blend`, and `Models/01_Prince.fbx` to `16_Shadow_Brute.fbx`.

- The README calls the models "original" and "based on the six nighttime concept scenes". The owner says the zip came from their ChatGPT concept-art session.
- FBX metadata: creator string "Blender (stable FBX IO) - 4.3.2 - 5.12.5"; an author field reading "Blender Foundation" (the exporter's default text, not a person or studio).
- The .blend was saved by Blender 4.3 (4.3.34). It has no author, licence, copyright or URL data, no text blocks, no linked libraries, no lights or cameras, and one unused image named `Render Result`.
- The lineup image has no EXIF, XMP or content-credential data.
- No third-party names, trademarks, brand names or licence text anywhere in the 16 FBX files, the .blend, the README or the image metadata.
- No texture images. Flat colours only, materials named by hex colour.
- Zip entry timestamps are 13 Nov 2026, 23:40 to 23:41.

## 2. What is not known

The files cannot answer these:

- Which tool generated the meshes, and from which prompts or reference images.
- Whether any reference image used in that session contained third-party art.
- The current terms of the service the owner used, and what they grant over generated output. Those terms, and the law on protecting AI-assisted work, change and differ by country. The owner should read them as they stand when deciding.
- Whether the owner is content to publish derivatives under this repository's licence.

## 3. What may and may not be committed

| Commit to the public repo now | Keep local until the owner confirms rights |
|---|---|
| Blender scripts in `Tools/characters/` (they take the zip folder as an argument and contain no mesh data); clip data files; the cast manifest and island maps (names, indices, counts, centres and sizes; no vertex or face data); Unity import and prefab-builder code; Animator controllers and avatar masks; `ActorFactory` changes with the placeholder fallback; tests that skip when art is absent; the PRD; this note. Animation-only FBX files (a skeleton and clips, no mesh) are original work and can go in. | The zip, `characters_extracted/`, the supplied .blend, any edited .blend, any FBX with a mesh, prefabs and materials built from the meshes, lineup images, captures and videos. |

Rules that hold regardless of this note (from `CLAUDE.md` and the owner's answers): the `NEWSTUFF` folder, the zip, Amiga archives, Sims files and Sims footage never go in the repo.

For agents, before every commit that touches characters:

1. `git status` and read the list. Stop if any path is an FBX with a mesh, a .blend, a lineup or capture image, a prefab or material built from the meshes, or anything under `NEWSTUFF`, `characters_work`, `Evidence/` or `LocalData/`.
2. Island maps and manifests may be committed only if they contain no vertex or face data (centres, sizes and counts are fine).
3. Generated working files go to a folder outside the repository (on this machine `WorldMVP\NEWSTUFF\characters_work\`). Scripts refuse an output folder inside the repository.
4. The game must build and boot with no character art. Characters load by name from `Resources` with no hard asset references, and a missing prefab falls back to the placeholder actor with nothing logged at error level (CH-14).

## 4. Once the owner confirms

After the owner signs below, the following may be committed together with this note: the runtime FBX files, prefabs, materials, and the edited .blend sources (`Art/Characters/Source/`, `Assets/Characters/Resources/Characters/`). Do this in one commit that names this document, and update the status line at the top of this file.

## 5. Owner confirmation

The owner fills this in. An agent must not fill it in or infer it.

```
I have read section 2 of this note and the terms of the service that produced the meshes.
I confirm I have the right to publish the supplied meshes and derivatives of them
(rigged, re-costumed, re-coloured, animated) in this public repository under its licence.

Owner: ______________________   Date: ____________

Scope (tick): [ ] all 16 characters   [ ] only: ______________________
Notes / conditions: ______________________________________________
```
