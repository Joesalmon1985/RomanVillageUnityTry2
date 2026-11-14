"""Face preview renders for WP-3 (local output only; never commit the images).

  blender --background --factory-startup --disable-autoexec --python Tools/characters/face_preview.py -- \
      --src <extracted folder> --out <dir outside the repo> --only prince,mira,champion [--states a,b] [--views a,b]

Per character: loads the supplied FBX, fits an armature (WP-2's rig.fit_rig + skin.skin_character when they exist
in lib/, otherwise lib/stub_rig.py plus a preview-only rigid skin of the Head), runs face.build_face, then renders
every state (neutral, the six other expressions, blink, look_away) from every view:

  close_front / close_3q        dialogue close-ups, 640 x 640, neutral studio light and dim warm firelight
  phone_dialogue                844 x 390, camera 1.8 m away, 60 deg vertical field of view (first-person talk)
  phone_pushin                  844 x 390, 3.2 m, 35 deg (PRD 7.2 ward-break push-in)
  phone_wide                    844 x 390, 6 m, 40 deg (PRD 7.2 duel camera)

Gaze follows the PRD 5.4 share rule toward the camera ("the person addressed"): head takes 40 % of the turn, the
eyes take the rest within +-14 deg yaw, +-5 deg pitch.

Writes <out>/renders/<key>/<state>__<view>__<light>.png, <out>/renders/<key>/shots.json (head pixel boxes for the
contact sheet), <out>/characters/<key>/manifest.json (face section) and <out>/characters/<key>/<key>_face.blend.
Make contact sheets afterwards with:  python -I Tools/characters/face_contact_sheet.py --out <same out>
"""

import json
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402

import common  # noqa: E402
import contract  # noqa: E402
import face as facelib  # noqa: E402

STATES = ["neutral", "attentive", "concentrating", "surprised", "pleased", "pained", "determined", "blink",
          "look_away"]
VIEWS = {
    # name: (yaw from the front toward the character's left, distance m, vertical fov deg or None for lens,
    #        resolution, lights)
    "close_front": (0.0, 1.0, None, (640, 640), ("neutral", "fire")),
    "close_3q": (40.0, 1.0, None, (640, 640), ("neutral", "fire")),
    "phone_dialogue": (20.0, 1.8, 60.0, (844, 390), ("fire", "neutral")),
    "phone_pushin": (35.0, 3.2, 35.0, (844, 390), ("fire",)),
    "phone_wide": (30.0, 6.0, 40.0, (844, 390), ("fire",)),
}


def outside_repo(path):
    repo = os.path.abspath(os.path.join(_HERE, "..", ".."))
    p = os.path.abspath(path)
    return not (p == repo or p.startswith(repo + os.sep))


# ----------------------------------------------------------------------------------------------------------------
def rig_character(ctx):
    """Production rig when WP-2 has delivered it, otherwise the stub plus a preview-only Head skin."""
    try:
        import rig  # noqa: F401
        import skin  # noqa: F401
        rig.fit_rig(ctx)
        skin.skin_character(ctx)
        return "wp2"
    except ImportError:
        pass
    import stub_rig
    arm = stub_rig.build_stub_armature(ctx)
    head = ctx.parts["Head"]
    import islands as _islands
    groups, _ = _islands.vertex_islands(head)
    mapped = ctx.islands["parts"]["Head"]["islands"]
    for g, m in zip(groups, mapped):
        bone = m.get("bone") or "Head"
        if bone not in arm.data.bones:
            bone = "Head"
        vg = head.vertex_groups.get(bone) or head.vertex_groups.new(name=bone)
        vg.add(g["verts"], 1.0, "REPLACE")
    mod = head.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return "stub"


# ----------------------------------------------------------------------------------------------------------------
def setup_world_and_lights(hc):
    scn = bpy.context.scene
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scn.render.engine = eng
            break
        except TypeError:
            continue
    try:
        scn.eevee.taa_render_samples = 64
    except AttributeError:
        pass
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "None"
    world = bpy.data.worlds.new("World")
    scn.world = world
    world.use_nodes = True
    lights = {"neutral": [], "fire": []}

    def sun(name, src_dir, strength, colour, group):
        d = bpy.data.lights.new(name, "SUN")
        d.energy = strength
        d.color = colour
        d.angle = math.radians(8)
        o = bpy.data.objects.new(name, d)
        o.rotation_euler = Vector(src_dir).normalized().to_track_quat("Z", "Y").to_euler()
        bpy.context.scene.collection.objects.link(o)
        lights[group].append(o)

    def point(name, loc, power, colour, radius, group):
        d = bpy.data.lights.new(name, "POINT")
        d.energy = power
        d.color = colour
        d.shadow_soft_size = radius
        o = bpy.data.objects.new(name, d)
        o.location = loc
        bpy.context.scene.collection.objects.link(o)
        lights[group].append(o)

    sun("KeyNeutral", (-0.6, -1.0, 0.9), 3.0, (1.0, 0.98, 0.95), "neutral")
    sun("FillNeutral", (0.9, -0.6, 0.2), 0.9, (0.95, 0.97, 1.0), "neutral")
    # night camp: fire on the ground 1.6 m in front, a little to the character's left; faint cool moon from behind
    point("Fire", hc + Vector((0.5, -2.2, -1.2)), 380.0, (1.0, 0.47, 0.16), 0.25, "fire")
    point("FireBounce", hc + Vector((-0.6, -1.2, -1.0)), 25.0, (1.0, 0.55, 0.25), 0.6, "fire")
    sun("Moon", (0.4, 1.0, 0.8), 0.18, (0.55, 0.65, 1.0), "fire")
    return world, lights


def set_light(world, lights, which):
    bg = world.node_tree.nodes["Background"]
    if which == "neutral":
        bg.inputs[0].default_value = (0.30, 0.31, 0.33, 1.0)
        bg.inputs[1].default_value = 1.0
    else:
        bg.inputs[0].default_value = (0.010, 0.013, 0.028, 1.0)
        bg.inputs[1].default_value = 1.0
    for g, objs in lights.items():
        for o in objs:
            o.hide_render = (g != which)


def make_camera(name, hc, yaw_deg, dist, vfov, res):
    cd = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(cam)
    a = math.radians(yaw_deg)
    # camera position: in front of the face (-Y), swung toward the character's left (+X)
    up = 0.02 if vfov is None else (0.05 if dist < 2.5 else 0.25)
    cam.location = hc + Vector((math.sin(a) * dist, -math.cos(a) * dist, up))
    aim = hc.copy()
    if vfov is not None and res[0] > res[1]:
        aim = hc + Vector((0.0, 0.0, -0.05 * dist))      # head in the upper part of the frame, body below
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()
    if vfov is None:
        cd.lens = 85.0
        cd.sensor_fit = "AUTO"
    else:
        cd.sensor_fit = "VERTICAL"
        cd.angle_y = math.radians(vfov)
    cd.clip_start = 0.05
    return cam


# ----------------------------------------------------------------------------------------------------------------
def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)


def aim_eyes(arm, target, limits, yaw_bias_deg=None, pitch_bias_deg=0.0):
    bpy.context.view_layer.update()
    out = {}
    for name in ("Eye.L", "Eye.R"):
        pb = arm.pose.bones[name]
        pb.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    for name in ("Eye.L", "Eye.R"):
        pb = arm.pose.bones[name]
        m = arm.matrix_world @ pb.matrix
        d = m.inverted().to_3x3() @ (target - m.translation)
        if yaw_bias_deg is not None:
            yaw, pitch = math.radians(yaw_bias_deg), math.radians(pitch_bias_deg)
        else:
            yaw = math.atan2(-d.x, d.y)
            pitch = math.atan2(d.z, math.hypot(d.x, d.y))
        ly, lp = math.radians(limits["eye_yaw_limit_deg"]), math.radians(limits["eye_pitch_limit_deg"])
        yaw = max(-ly, min(ly, yaw))
        pitch = max(-lp, min(lp, pitch))
        pb.rotation_euler = (pitch, 0.0, yaw)
        out[name] = (round(math.degrees(yaw), 1), round(math.degrees(pitch), 1))
    return out


def pose_state(ctx, face, state, cam, hc):
    arm = ctx.armature
    reset_pose(arm)
    g = face["tuning"]["gaze"]
    expr = state if state in contract.EXPRESSIONS else "neutral"
    extra = {"Blink.L": 1.0, "Blink.R": 1.0} if state == "blink" else None
    facelib.apply_expression(ctx, expr, face, extra_keys=extra)
    gaze = face["expressions"][expr].get("gaze", "target")
    head = arm.pose.bones["Head"]
    to_cam = cam.location - hc
    total_yaw = math.atan2(to_cam.x, -to_cam.y)          # + = toward the character's left
    head_yaw = g["head_share"] * total_yaw
    head_pitch = 0.0
    if gaze == "down":
        head_pitch = math.radians(8.0)
    if state == "look_away":
        head_yaw = 0.0
    head.rotation_euler = (head_pitch, head_yaw, 0.0)
    if state == "look_away":
        return aim_eyes(arm, cam.location, g, yaw_bias_deg=g["eye_yaw_limit_deg"], pitch_bias_deg=0.0)
    if gaze == "down":
        return aim_eyes(arm, cam.location, g, yaw_bias_deg=0.0, pitch_bias_deg=g["eye_pitch_limit_deg"])
    return aim_eyes(arm, cam.location, g)


def head_box_px(cam, hc, hs, res):
    scn = bpy.context.scene
    pts = []
    for dx in (-0.5, 0.5):
        for dz in (-0.5, 0.5):
            p = hc + Vector((dx * hs.x, -0.5 * hs.y, dz * hs.z))
            c = world_to_camera_view(scn, cam, p)
            pts.append((c.x * res[0], (1.0 - c.y) * res[1]))
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return [round(min(xs), 1), round(min(ys), 1), round(max(xs), 1), round(max(ys), 1)]


# ----------------------------------------------------------------------------------------------------------------
def run_character(key, a):
    ctx = common.load_character(key, a.src, a.out)
    rig_kind = rig_character(ctx)
    face = facelib.load_face_data(key)
    section = facelib.build_face(ctx, face)
    ctx.manifest["face_preview"] = {"rig": rig_kind}   # local marker, not a contract section
    common.write_json(ctx.path("character_manifest"), ctx.manifest)
    common.ensure_dir(ctx.path("character_dir"))
    hc = Vector(ctx.islands["landmarks"]["head_centre"])
    hs = Vector(ctx.islands["landmarks"]["head_size"])
    world, lights = setup_world_and_lights(hc)
    scn = bpy.context.scene
    rdir = common.ensure_dir(os.path.join(a.out, "renders", key))
    shots = {"key": key, "rig": rig_kind, "head_size_m": [round(v, 4) for v in hs], "shots": []}
    states = [s for s in (a.states.split(",") if a.states else STATES)]
    views = [v for v in (a.views.split(",") if a.views else VIEWS.keys())]
    cams = {v: make_camera("Cam_" + v, hc, *VIEWS[v][:4]) for v in views}
    for v in views:
        yaw, dist, vfov, res, lights_for = VIEWS[v]
        scn.camera = cams[v]
        scn.render.resolution_x, scn.render.resolution_y = res
        scn.render.resolution_percentage = 100
        box = head_box_px(cams[v], hc, hs, res)
        for state in states:
            eyes = pose_state(ctx, face, state, cams[v], hc)
            for lt in lights_for:
                set_light(world, lights, lt)
                fn = "%s__%s__%s.png" % (state, v, lt)
                scn.render.filepath = os.path.join(rdir, fn)
                bpy.ops.render.render(write_still=True)
                shots["shots"].append({"file": fn, "state": state, "view": v, "light": lt, "res": list(res),
                                       "head_box_px": box, "eyes_deg": eyes})
        common.log(key, v, "done")
    common.write_json(os.path.join(rdir, "shots.json"), shots)
    reset_pose(ctx.armature)
    for k in contract.SHAPE_KEYS:
        ctx.parts["Head"].data.shape_keys.key_blocks[k].value = 0.0
    if not a.no_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ctx.path("character_dir"), key + "_face.blend"),
                                    compress=True)
    return section


def main():
    a = common.parse_args([("--states", {"default": ""}), ("--views", {"default": ""}),
                           ("--no-blend", {"action": "store_true"})],
                          description="WP-3 face preview renders")
    if not a.src or not a.out:
        raise SystemExit("--src and --out are required")
    if not outside_repo(a.out):
        raise SystemExit("--out must be outside the repository")
    keys = common.selected_keys(a.only or ",".join(contract.FIRST_THREE))
    for key in keys:
        if contract.CAST_BY_KEY[key]["rig"] != "humanoid":
            common.log("skip", key, "(demon)")
            continue
        sec = run_character(key, a)
        common.log(key, "shape keys", ",".join(sec["shape_keys"]), "mouth", sec["mouth_refine"].get("verts_added"))


if __name__ == "__main__":
    main()
