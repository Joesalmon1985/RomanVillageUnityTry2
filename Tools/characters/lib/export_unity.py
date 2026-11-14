"""FBX export for the Unity import step (PRD_CHARACTERS 5.6; contract 2.7, 2.8). Same settings every time.

  export_character(ctx)                       mesh + rig, no clips: <key>@<costume>.fbx in the costume folder
  export_anim_sets(arm, set_names, out_dir, variant_key=None)
                                              armature + actions only, no mesh: anim_<set>.fbx (base clips) or
                                              anim_<set>@<key>.fbx (the <Clip>@<key> variants baked on that rig)

Settings (PRD 5.6): metres, forward -Z, up Y, bake_space_transform, no leaf bones, every bone (sockets are non-deform
bones and must reach Unity), shape keys kept (no modifiers applied), flat custom normals as authored, no materials
embedded beyond the Role_* names (Unity maps materials by name), no animation in the character file.

The forward-axis check (contract: the character faces Blender -Y, Unity +Z) is done two ways and written to the
manifest `export` section: (1) the exporter's own axis matrix must send Blender -Y to FBX +Z; (2) the file is imported
back into an empty scene with the same axes and the eyes must again be in front of the head (-Y).
These files never go in the repository (meshes stay local until the owner confirms rights).
"""

import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import bpy  # noqa: E402
from bpy_extras.io_utils import axis_conversion  # noqa: E402
from mathutils import Vector  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402

COMMON_ARGS = dict(
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
    axis_forward=contract.FBX_AXIS_FORWARD, axis_up=contract.FBX_AXIS_UP, bake_space_transform=True,
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL",
    use_armature_deform_only=False, path_mode="STRIP", embed_textures=False,
)


def _select_only(objs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.hide_viewport = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def _tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def axis_matrix_check():
    m = axis_conversion(to_forward=contract.FBX_AXIS_FORWARD, to_up=contract.FBX_AXIS_UP).to_4x4()
    fwd = (m.to_3x3() @ Vector((0.0, -1.0, 0.0)))
    left = (m.to_3x3() @ Vector((1.0, 0.0, 0.0)))
    ok = fwd.z > 0.999 and left.x > 0.999
    return {"ok": bool(ok), "blender_minus_y_to_fbx": common.rv(fwd, 3), "blender_plus_x_to_fbx": common.rv(left, 3),
            "unity_note": "Unity mirrors FBX X on import: the character faces Unity +Z with .L on Unity -X (to be seen in Unity by WP-6)"}


def roundtrip_check(path):
    """Import the written file into a temporary scene and check the eyes are in front of the head (-Y) again."""
    prev = bpy.context.window.scene if bpy.context.window else bpy.context.scene
    tmp = bpy.data.scenes.new("_fbx_check")
    win = bpy.context.window
    if win is not None:
        win.scene = tmp
    before = set(bpy.data.objects)
    try:
        bpy.ops.import_scene.fbx(filepath=path, use_anim=False, axis_forward=contract.FBX_AXIS_FORWARD,
                                 axis_up=contract.FBX_AXIS_UP, ignore_leaf_bones=False)
        new = [o for o in bpy.data.objects if o not in before]
        arm = next((o for o in new if o.type == "ARMATURE"), None)
        res = {"ok": False, "detail": "no armature in the file"}
        if arm is not None:
            b = arm.data.bones
            mw = arm.matrix_world
            eye = mw @ b["Eye.L"].head_local
            head = mw @ b["Head"].head_local
            left = mw @ b["UpperArm.L"].head_local
            res = {"ok": bool(eye.y < head.y and left.x > 0), "eye_y": common.r(eye.y), "head_y": common.r(head.y),
                   "upperarm_l_x": common.r(left.x), "bones": len(b),
                   "meshes": sorted(o.name for o in new if o.type == "MESH"),
                   "shape_keys": sorted(k.name for o in new if o.type == "MESH" and o.data.shape_keys
                                        for k in o.data.shape_keys.key_blocks)}
    finally:
        for o in [o for o in bpy.data.objects if o not in before]:
            bpy.data.objects.remove(o, do_unlink=True)
        if win is not None:
            win.scene = prev
        bpy.data.scenes.remove(tmp)
    return res


def export_character(ctx, path=None):
    """Mesh + rig of the current costume. Writes manifest section `export`."""
    arm = ctx.armature
    meshes = [o for o in ctx.parts.values()] + [o for o in bpy.data.objects
                                                if o.name.startswith(contract.OVERLAY_PREFIX) and o.type == "MESH"]
    path = path or ctx.path("character_fbx")
    common.ensure_dir(os.path.dirname(path))
    # the rest pose is what Unity reads: no action, identity pose
    ad = arm.animation_data
    prev = ad.action if ad else None
    if ad:
        ad.action = None
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    head = ctx.parts.get("Head")
    if head is not None and head.data.shape_keys:
        for k in head.data.shape_keys.key_blocks:
            k.value = 0.0
    bpy.context.view_layer.update()
    _select_only([ctx.root, arm] + meshes)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "ARMATURE", "MESH"},
                             use_mesh_modifiers=False, mesh_smooth_type="OFF", use_custom_props=False,
                             use_tspace=False, bake_anim=False, **COMMON_ARGS)
    if prev is not None:
        ad.action = prev
    hidden = contract.ROLE_MATERIAL_PREFIX + contract.HIDDEN_SLOT
    per_mesh = {}
    for o in meshes:
        hid = {i for i, m in enumerate(o.data.materials) if m and m.name == hidden}
        per_mesh[o.name] = {"tris": _tris(o), "visible_tris": sum(len(p.vertices) - 2 for p in o.data.polygons
                                                                   if p.material_index not in hid),
                            "verts": len(o.data.vertices),
                            "materials": [m.name for m in o.data.materials if m],
                            "overlay": o.name.startswith(contract.OVERLAY_PREFIX)}
    section = {
        "fbx": os.path.relpath(path, ctx.out).replace("\\", "/"),
        "bytes": os.path.getsize(path),
        "costume": ctx.costume,
        "unity_target": contract.UNITY_PATHS["character_costume_fbx"].replace("<key>", ctx.key).replace(
            "<costume>", ctx.costume or "default"),
        "settings": {k: (sorted(v) if isinstance(v, set) else v) for k, v in COMMON_ARGS.items()},
        "meshes": per_mesh,
        "tris_total": sum(v["tris"] for v in per_mesh.values()),
        "tris_visible": sum(v["visible_tris"] for v in per_mesh.values()),
        "forward_axis": axis_matrix_check(),
        "roundtrip": roundtrip_check(path),
    }
    section["forward_axis"]["ok"] = bool(section["forward_axis"]["ok"] and section["roundtrip"].get("ok"))
    ctx.set_section("export", section)
    common.log(ctx.key, "exported", section["fbx"], "%d bytes" % section["bytes"])
    return section


def export_anim_sets(blend_path, set_names, out_dir, variant_key=None):
    """One FBX per clip set: the armature and only the actions of that set (base clips, or only <Clip>@<variant_key>).
    The FBX exporter writes every action in the file that binds to the armature, so for each set the saved build
    .blend is reopened and every other action is deleted before the export (the .blend on disk is not changed).
    Call it last: it replaces the open scene. Returns {set: {"fbx", "bytes", "actions"}}."""
    import anim_core
    out = {}
    for set_name in set_names:
        names = set(contract.CLIP_SETS[set_name])

        def wanted(a):
            base, _, key = a.name.partition("@")
            if base not in names:
                return False
            return (key == variant_key) if variant_key else (key == "")
        bpy.ops.wm.open_mainfile(filepath=blend_path, load_ui=False)
        keep = sorted([a for a in bpy.data.actions if wanted(a)], key=lambda a: a.name)
        if not keep:
            continue
        for a in [a for a in bpy.data.actions if a not in keep]:
            bpy.data.actions.remove(a)
        arm = bpy.data.objects[contract.ARMATURE_OBJECT]
        anim_core.assign_action(arm, keep[0])
        scn = bpy.context.scene
        scn.render.fps = contract.FPS
        scn.frame_start = 0
        scn.frame_end = int(max(a.frame_end for a in keep))
        _select_only([arm])
        fn = "%s%s%s.fbx" % (contract.ANIM_FBX_PREFIX, set_name, ("@" + variant_key) if variant_key else "")
        path = os.path.join(out_dir, fn)
        common.ensure_dir(out_dir)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE"},
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                                 bake_anim_step=1.0, bake_anim_simplify_factor=0.0, **COMMON_ARGS)
        out[set_name] = {"fbx": fn, "bytes": os.path.getsize(path), "actions": [a.name for a in keep]}
        common.log("anim export", fn, len(keep), "actions")
    return out
