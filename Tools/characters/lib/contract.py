"""Naming contract for the character pipeline (PRD_CHARACTERS.md section 5.3, resolved by WP-1).

Pure Python: no bpy import, so Blender scripts, plain python and tests can all read it.
`Tools/characters/contract.json` is generated from this file (`python -I Tools/characters/lib/contract.py`)
so the Unity side (WP-6, WP-7) can read the same names without parsing Python.

This file is the single source of truth for names. Nothing else in the pipeline may invent a bone, part,
shape key, socket, action or slot name. Changing a name here is a contract change: tell the owner.
See Tools/characters/README.md, section "Contract", for the prose and the reasons.
"""

import json
import os

CONTRACT_VERSION = 1

# --------------------------------------------------------------------------------------
# Side convention (measured, see README "Resolved unknowns"):
# in the supplied files every character faces -Y in Blender (front view looks along +Y).
# Character left (".L") is +X in Blender. Blender -Y forward exports (forward -Z, up Y,
# bake_space_transform) to a character that faces +Z in Unity, with ".L" on Unity -X.
# --------------------------------------------------------------------------------------
AUTHORING_FORWARD = "-Y"
AUTHORING_UP = "Z"
LEFT_SIGN_X = 1  # character left is +X in Blender
FBX_AXIS_FORWARD = "-Z"
FBX_AXIS_UP = "Y"
UNITY_FORWARD = "+Z"

# --------------------------------------------------------------------------------------
# Cast: key -> data. Keys are the names used on every command line (--only mira,prince,champion)
# and in every file name. Numbers and fbx file names are the supplied ones.
# `tier`: hero characters get the face rig. All 16 are hero for the intake; crowd tier is WP-15.
# `rig`: "humanoid" (Humanoid avatar, template armature) or "demon" (Generic, shared Demon armature).
# --------------------------------------------------------------------------------------
CAST = [
    # key, number, fbx, display, rig, slice
    ("prince", 1, "01_Prince.fbx", "Prince", "humanoid", 1),
    ("mira", 2, "02_Mira.fbx", "Mira", "humanoid", 1),
    ("juno", 3, "03_Juno.fbx", "Juno", "humanoid", 1),
    ("sol", 4, "04_DJ_Sol.fbx", "DJ Sol", "humanoid", 2),
    ("woodcutter", 5, "05_Woodcutter.fbx", "Woodcutter", "humanoid", 2),
    ("carpenter", 6, "06_Carpenter.fbx", "Carpenter", "humanoid", 2),
    ("hesta", 7, "07_Hesta_Market_Keeper.fbx", "Hesta", "humanoid", 2),
    ("merchant", 8, "08_Village_Merchant.fbx", "Village Merchant", "humanoid", 2),
    ("dancer_ochre", 9, "09_Dancer_Ochre.fbx", "Dancer Ochre", "humanoid", 2),
    ("dancer_ash", 10, "10_Dancer_Ash.fbx", "Dancer Ash", "humanoid", 2),
    ("dancer_moss", 11, "11_Dancer_Moss.fbx", "Dancer Moss", "humanoid", 2),
    ("dancer_plum", 12, "12_Dancer_Plum.fbx", "Dancer Plum", "humanoid", 2),
    ("carrier", 13, "13_Timber_Carrier.fbx", "Timber Carrier", "humanoid", 2),
    ("champion", 14, "14_Road_Hero.fbx", "Road Hero (Ash champion)", "humanoid", 1),
    ("stalker", 15, "15_Shadow_Stalker.fbx", "Shadow Stalker", "demon", 2),
    ("brute", 16, "16_Shadow_Brute.fbx", "Shadow Brute", "demon", 2),
]
CAST_KEYS = [c[0] for c in CAST]
CAST_BY_KEY = {c[0]: {"key": c[0], "number": c[1], "fbx": c[2], "display": c[3], "rig": c[4], "slice": c[5]} for c in CAST}
FIRST_THREE = ["prince", "mira", "champion"]

# FBX object-name suffixes in the supplied files: "<NN>_<Name>_<Part>"; the part is the last token.
SOURCE_PART_SUFFIXES = ["Body", "Cloth", "Head", "Legs", "Prop"]

# --------------------------------------------------------------------------------------
# Objects in a build scene. One character per Blender scene (reset_scene between characters).
# --------------------------------------------------------------------------------------
ROOT_OBJECT_PREFIX = ""          # root empty is named exactly <key>, e.g. "mira"
ARMATURE_OBJECT = "Armature"     # armature object AND armature data name (data: "Armature")
PART_NAMES = ["Body", "Legs", "Head", "Cloth", "Prop"]  # mesh object names, no key prefix
# Overlay meshes made by WP-4 are named "Ov_<name>" (e.g. Ov_Mantle). Hidden Historic pieces are
# not deleted: they are hidden by a vertex-group-free mechanism (object hide flag + manifest list).
OVERLAY_PREFIX = "Ov_"

# --------------------------------------------------------------------------------------
# Bones. Parents are fixed. Every human gets every bone in HUMAN_CORE_BONES and FACE_BONES and
# every socket. Optional chains exist only where the character has the garment.
# Rest pose = the supplied A-pose; bone DIRECTIONS are snapped to the template (see README).
# --------------------------------------------------------------------------------------
HUMAN_CORE_BONES = [
    # (name, parent)
    ("Hips", None),
    ("Spine", "Hips"),
    ("Chest", "Spine"),
    ("Neck", "Chest"),
    ("Head", "Neck"),
    ("Shoulder.L", "Chest"), ("Shoulder.R", "Chest"),
    ("UpperArm.L", "Shoulder.L"), ("UpperArm.R", "Shoulder.R"),
    ("LowerArm.L", "UpperArm.L"), ("LowerArm.R", "UpperArm.R"),
    ("Hand.L", "LowerArm.L"), ("Hand.R", "LowerArm.R"),
    ("UpperLeg.L", "Hips"), ("UpperLeg.R", "Hips"),
    ("LowerLeg.L", "UpperLeg.L"), ("LowerLeg.R", "UpperLeg.R"),
    ("Foot.L", "LowerLeg.L"), ("Foot.R", "LowerLeg.R"),
    ("Toes.L", "Foot.L"), ("Toes.R", "Foot.R"),
]
# Face bones. Brows, lids and mouth corners are SHAPE KEYS, not bones (decision, README). Only
# the jaw and the two eyes are bones. Eye bones carry iris + pupil only (README "Gaze").
FACE_BONES = [("Jaw", "Head"), ("Eye.L", "Head"), ("Eye.R", "Head")]

# Optional deform chains (extra transforms in Unity, not in the Humanoid map).
CAPE_BONES = [("Cape.1", "Chest"), ("Cape.2", "Cape.1"), ("Cape.3", "Cape.2")]   # single centre chain
HOOD_BONES = [("Hood.1", "Head"), ("Hood.2", "Hood.1")]
SKIRT_BONES = [("Skirt.F", "Hips"), ("Skirt.B", "Hips"), ("Skirt.L", "Hips"), ("Skirt.R", "Hips")]

# Sockets: non-deform bones, no weights, created on every human. Name -> parent.
# Sock.StaffTip and Sock.SpellOrigin exist on every human; on characters without a staff they sit
# 0.45 m above Sock.Hand.R along the hand's up direction (placeholder position, manifest says so).
SOCKETS = [
    ("Sock.Hand.R", "Hand.R"),
    ("Sock.Hand.L", "Hand.L"),
    ("Sock.StaffTip", "Sock.Hand.R"),
    ("Sock.SpellOrigin", "Sock.StaffTip"),
    ("Sock.Back", "Chest"),
    ("Sock.Hip.R", "Hips"),
    ("Sock.HeadTop", "Head"),
    ("Sock.Foot.L", "Foot.L"),
    ("Sock.Foot.R", "Foot.R"),
]
SOCKET_NAMES = [s[0] for s in SOCKETS]

MAX_BONES_HERO = 45
MAX_SHAPE_KEYS_HEAD = 16
MAX_INFLUENCES = 4

# Unity Humanoid mapping. The importer (WP-6) sets HumanDescription from this table; it does not rely
# on Unity auto-mapping. .L is character left = Unity LeftXxx.
HUMANOID_MAP = {
    "Hips": "Hips", "Spine": "Spine", "Chest": "Chest", "Neck": "Neck", "Head": "Head",
    "Jaw": "Jaw", "Eye.L": "LeftEye", "Eye.R": "RightEye",
    "Shoulder.L": "LeftShoulder", "Shoulder.R": "RightShoulder",
    "UpperArm.L": "LeftUpperArm", "UpperArm.R": "RightUpperArm",
    "LowerArm.L": "LeftLowerArm", "LowerArm.R": "RightLowerArm",
    "Hand.L": "LeftHand", "Hand.R": "RightHand",
    "UpperLeg.L": "LeftUpperLeg", "UpperLeg.R": "RightUpperLeg",
    "LowerLeg.L": "LeftLowerLeg", "LowerLeg.R": "RightLowerLeg",
    "Foot.L": "LeftFoot", "Foot.R": "RightFoot",
    "Toes.L": "LeftToes", "Toes.R": "RightToes",
}

# Demon armature (WP-13 owns the fit; names are fixed here so one clip set plays on both demons).
# Generic rig, root bone "Hips" is NOT used: root is "Root".
DEMON_BONES = [
    ("Root", None), ("Hips", "Root"), ("Spine", "Hips"), ("Chest", "Spine"), ("Neck", "Chest"),
    ("Head", "Neck"), ("Jaw", "Head"),
    ("UpperArm.L", "Chest"), ("LowerArm.L", "UpperArm.L"), ("Hand.L", "LowerArm.L"),
    ("UpperArm.R", "Chest"), ("LowerArm.R", "UpperArm.R"), ("Hand.R", "LowerArm.R"),
    ("UpperLeg.L", "Hips"), ("LowerLeg.L", "UpperLeg.L"), ("Foot.L", "LowerLeg.L"),
    ("UpperLeg.R", "Hips"), ("LowerLeg.R", "UpperLeg.R"), ("Foot.R", "LowerLeg.R"),
]
# Optional demon extras where pieces allow: Horn.L, Horn.R, Claw.L, Claw.R, Claw.Foot.L, Claw.Foot.R
DEMON_OPTIONAL = ["Horn.L", "Horn.R", "Claw.L", "Claw.R", "Claw.Foot.L", "Claw.Foot.R"]
DEMON_SOCKETS = [("Sock.Hand.R", "Hand.R"), ("Sock.Hand.L", "Hand.L"), ("Sock.HeadTop", "Head"),
                 ("Sock.Foot.L", "Foot.L"), ("Sock.Foot.R", "Foot.R")]

# --------------------------------------------------------------------------------------
# Shape keys on the Head mesh (WP-3 creates; the runtime FaceController, WP-7, writes them).
# Exact names. Weights 0..1. Basis key is "Basis". 10 keys + Basis = 11 <= 16.
# No gaze keys: all 14 humans have disc eyes that use Eye.L/R bones (README "Gaze"). A future plate-eyed character
# would need GazeLeft/Right/Up/Down added here (a contract change).
# --------------------------------------------------------------------------------------
SHAPE_KEY_BASIS = "Basis"
SHAPE_KEYS = [
    "Blink.L", "Blink.R",
    "BrowRaise.L", "BrowRaise.R",
    "BrowFurrow",      # both brows, inner ends down and together
    "BrowWorry",       # both brows, inner ends up
    "MouthSmile", "MouthFrown", "MouthRound", "MouthOpen",
]
# Expression presets (data in cast/<key>.json "face.expressions"; names fixed here).
EXPRESSIONS = ["neutral", "attentive", "concentrating", "surprised", "pleased", "pained", "determined"]

# --------------------------------------------------------------------------------------
# Role material slots (WP-4). Material names in the exported FBX and in the manifest.
# The supplied RV_xxxxxx materials exist only until WP-4 remaps them. A character uses only the
# slots it needs; at most 8 slots per hero character (budget: one draw call per slot).
# --------------------------------------------------------------------------------------
ROLE_SLOTS = ["skin", "hair", "cloth_main", "cloth_trim", "leather", "metal", "eye_white", "eye_dark", "emissive"]
ROLE_MATERIAL_PREFIX = "Role_"   # Role_skin, Role_hair, ...
# sclera -> eye_white; iris, pupil, eye outline -> eye_dark; brows and beard -> hair; glow -> emissive.
MAX_SLOTS_PER_CHARACTER = 8
# Pieces a costume hides are not deleted: they are assigned to this extra material slot, which the Unity
# importer maps to a no-draw material (so a live costume swap can show or hide them). Not counted in the 8.
HIDDEN_SLOT = "hidden"

# --------------------------------------------------------------------------------------
# Animation. Clip names are exactly the Blender action names and the Unity clip names (Unity
# importer strips any "Armature|" prefix up to the last "|"). 30 fps. In place (no root motion).
# One animation-only FBX per clip set. Per-character style variants are separate actions named
# "<Clip>@<key>" in the same file (e.g. CastAnticipate@mira); the Unity override controller maps them.
# --------------------------------------------------------------------------------------
FPS = 30
CLIP_SETS = {
    "idle": ["Idle_A", "Idle_B", "Idle_Look", "Idle_Staff"],
    "locomotion": ["Walk", "WalkBrisk", "Jog", "StartL", "StartR", "StopL", "StopR",
                   "Turn60L", "Turn60R", "TurnSmallL", "TurnSmallR"],
    "social": ["Greet", "Nod", "ShakeHead", "Talk_A", "Talk_B", "Talk_Emphasis", "Listen"],
    "prince": ["Beckon", "LeadLook", "PointGround"],
    "duel": ["CastAnticipate", "CastRelease", "CastRecover", "CastWeave_Loop", "Block",
             "RecoilLight", "RecoilHeavy", "WardBreak", "Victory", "Defeat", "Surprise"],
}
ALL_CLIPS = [c for names in CLIP_SETS.values() for c in names]
# Clips whose additive/overlay use is intended (Unity sets additive reference pose; manifest flag "additive").
ADDITIVE_CLIPS = ["Nod", "ShakeHead", "LeadLook", "TurnSmallL", "TurnSmallR"]
# Clips that loop (manifest flag "loop"). Others are one-shots. Hold loops at the end are separate keys in the data.
LOOP_CLIPS = ["Idle_A", "Idle_B", "Idle_Look", "Idle_Staff", "Walk", "WalkBrisk", "Jog",
              "Talk_A", "Talk_B", "Talk_Emphasis", "Listen", "CastWeave_Loop"]
EVENT_NAMES = ["Release", "Impact", "FootL", "FootR", "PropHit", "BeatHit"]
ANIM_FBX_PREFIX = "anim_"        # anim_idle.fbx, anim_locomotion.fbx ...

# --------------------------------------------------------------------------------------
# File layout. <out> is the --out argument (outside the repo until the owner confirms rights).
# --------------------------------------------------------------------------------------
LAYOUT = {
    "character_dir": "characters/<key>/",
    "character_blend": "characters/<key>/<key>.blend",
    "character_fbx": "characters/<key>/<key>.fbx",               # mesh + rig, no clips
    "character_manifest": "characters/<key>/manifest.json",
    "character_reports": "characters/<key>/reports/",             # verify output, foot report, renders
    "anim_dir": "anim/",
    "anim_fbx": "anim/anim_<set>.fbx",                            # skeleton + actions, no mesh
    "anim_manifest": "anim/anim_manifest.json",                   # clip name, set, length, loop, native_speed, events
    "cast_manifest": "cast_manifest.json",                        # merged index of all character + anim manifests
    "inspect_dir": "inspect/",
    "islands_dir": "inspect/islands/",
}
# Where WP-6 copies things in the Unity project:
UNITY_PATHS = {
    "character_fbx": "Assets/Characters/Resources/Characters/Models/<key>.fbx",
    "prefab": "Assets/Characters/Resources/Characters/<key>.prefab",   # Resources.Load("Characters/<key>")
    "anim_fbx": "Assets/Characters/Animation/anim_<set>.fbx",
}

# --------------------------------------------------------------------------------------
# Manifest (per character), assembled by build_cast.py from sections each step writes.
# --------------------------------------------------------------------------------------
MANIFEST_SECTIONS = {
    "identity": "WP-1/common: key, number, display, rig, source fbx file name (no path)",
    "meshes": "WP-2 export: per part {name, tris, verts, materials[], hidden}",
    "bones": "WP-2: list of {name, parent, deform, head, tail} in rest pose (metres, Blender axes)",
    "sockets": "WP-2: {name: {parent, head, placeholder: bool}}",
    "skin": "WP-2: max_influences, unweighted_verts (must be 0), pieces[{island, bone}]",
    "face": "WP-3: gaze_method, shape_keys[], eye_bones[], expressions{}, tuning{}",
    "costumes": "WP-4: {name: {slots{slot: hex}, overlays[], hidden_islands[], props{socket: propKey}}}",
    "clips": "WP-5: list of {name, set, length_s, frames, fps, loop, additive, native_speed_mps, events[{name,frame}]}",
    "verify": "WP-2: list of {check, ok, detail}",
    "export": "WP-2/WP-6: forward_axis check, fbx file name, triangle counts",
}


def as_dict():
    return {
        "version": CONTRACT_VERSION,
        "authoring": {"forward": AUTHORING_FORWARD, "up": AUTHORING_UP, "left_sign_x": LEFT_SIGN_X,
                      "fbx_axis_forward": FBX_AXIS_FORWARD, "fbx_axis_up": FBX_AXIS_UP, "unity_forward": UNITY_FORWARD},
        "cast": [CAST_BY_KEY[k] for k in CAST_KEYS],
        "first_three": FIRST_THREE,
        "objects": {"armature": ARMATURE_OBJECT, "parts": PART_NAMES, "overlay_prefix": OVERLAY_PREFIX},
        "human_bones": [{"name": n, "parent": p} for n, p in HUMAN_CORE_BONES + FACE_BONES],
        "optional_bones": {
            "cape": [{"name": n, "parent": p} for n, p in CAPE_BONES],
            "hood": [{"name": n, "parent": p} for n, p in HOOD_BONES],
            "skirt": [{"name": n, "parent": p} for n, p in SKIRT_BONES],
        },
        "sockets": [{"name": n, "parent": p} for n, p in SOCKETS],
        "humanoid_map": HUMANOID_MAP,
        "demon_bones": [{"name": n, "parent": p} for n, p in DEMON_BONES],
        "demon_optional_bones": DEMON_OPTIONAL,
        "demon_sockets": [{"name": n, "parent": p} for n, p in DEMON_SOCKETS],
        "limits": {"max_bones": MAX_BONES_HERO, "max_shape_keys": MAX_SHAPE_KEYS_HEAD, "max_influences": MAX_INFLUENCES,
                   "max_slots": MAX_SLOTS_PER_CHARACTER},
        "shape_keys": {"basis": SHAPE_KEY_BASIS, "names": SHAPE_KEYS},
        "expressions": EXPRESSIONS,
        "role_slots": {"names": ROLE_SLOTS, "material_prefix": ROLE_MATERIAL_PREFIX, "hidden": HIDDEN_SLOT,
                       "max_per_character": MAX_SLOTS_PER_CHARACTER},
        "animation": {"fps": FPS, "clip_sets": CLIP_SETS, "loop": LOOP_CLIPS, "additive": ADDITIVE_CLIPS,
                      "events": EVENT_NAMES, "fbx_prefix": ANIM_FBX_PREFIX, "variant_separator": "@"},
        "layout": LAYOUT,
        "unity_paths": UNITY_PATHS,
        "manifest_sections": MANIFEST_SECTIONS,
    }


def all_human_bone_names(skirt=False, cape=False, hood=False):
    names = [n for n, _ in HUMAN_CORE_BONES + FACE_BONES]
    if cape:
        names += [n for n, _ in CAPE_BONES]
    if hood:
        names += [n for n, _ in HOOD_BONES]
    if skirt:
        names += [n for n, _ in SKIRT_BONES]
    return names + SOCKET_NAMES


def validate():
    """Self-check used by the tests and by `python lib/contract.py`."""
    names = [n for n, _ in HUMAN_CORE_BONES + FACE_BONES + CAPE_BONES + HOOD_BONES + SKIRT_BONES + SOCKETS]
    assert len(names) == len(set(names)), "duplicate bone names"
    known = set(names)
    for n, p in HUMAN_CORE_BONES + FACE_BONES + CAPE_BONES + HOOD_BONES + SKIRT_BONES + SOCKETS:
        assert p is None or p in known, "unknown parent %s for %s" % (p, n)
    assert len(all_human_bone_names(True, True, True)) <= MAX_BONES_HERO, "too many bones"
    assert len(SHAPE_KEYS) + 1 <= MAX_SHAPE_KEYS_HEAD
    for k, d in HUMANOID_MAP.items():
        assert k in known, k
    assert len(set(ALL_CLIPS)) == len(ALL_CLIPS)
    return True


if __name__ == "__main__":
    validate()
    out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "contract.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(as_dict(), f, indent=2, sort_keys=False)
        f.write("\n")
    print("wrote", out, "bones max", len(all_human_bone_names(True, True, True)))
