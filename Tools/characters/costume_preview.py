"""WP-4: build, check and render costume variants. Local review tool; writes only under --out (outside the repo).

  blender --background --factory-startup --disable-autoexec --python Tools/characters/costume_preview.py -- \
      --src <extracted zip folder> --out <output folder> [--only prince,mira,champion] [--variants historic,prehistoric] \
      [--poses rest,walk,armsup,reach] [--views front,side,threeq,back] [--lights neutral,fire] [--render] [--size 480]

For every character and variant: load the supplied FBX, build the development stub armature (or call the production
`rig.fit_rig` once WP-2 has delivered), skin the supplied pieces rigidly by their island bone suggestions (stand-in for
WP-2's `skin.py`; overlays are weighted by costume.py itself), apply the costume, pose the rig, measure how far overlay
vertices sink into the body (penetration report), and optionally render stills (Cycles, CPU) in a neutral and a dim warm
firelight set-up. Sheets are written as <out>/wp4/<key>/<variant>_<pose>_<light>.png (views side by side).
"""
import json
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector, Euler  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import islands as islands_mod  # noqa: E402
import costume  # noqa: E402

try:
    import rig as rig_mod  # WP-2, when merged
except ImportError:
    rig_mod = None
import stub_rig  # noqa: E402

POSES = {
    "rest": {},
    # a walk stride: left leg back, right leg forward, opposite arm swing (positive X on a down-pointing bone swings back)
    "walk": {"UpperLeg.L": (28, 0, 0), "LowerLeg.L": (38, 0, 0), "UpperLeg.R": (-24, 0, 0), "LowerLeg.R": (6, 0, 0),
             "UpperArm.L": (-26, 0, 0), "LowerArm.L": (-18, 0, 0), "UpperArm.R": (26, 0, 0), "LowerArm.R": (-8, 0, 0),
             "Hips": (0, 0, 4), "Chest": (0, 0, -6)},
    # both arms raised sideways to about 135 degrees (rotation about the bone's local Z = world Y for these bones)
    "armsup": {"UpperArm.L": (0, 0, -110), "UpperArm.R": (0, 0, 110)},
    # arms reaching forward (cast / beckon)
    "reach": {"UpperArm.L": (-95, 0, 0), "UpperArm.R": (-95, 0, 0), "LowerArm.L": (-10, 0, 0), "LowerArm.R": (-10, 0, 0)},
}
VIEWS = {"front": 0.0, "threeq": 40.0, "side": 90.0, "back": 180.0}   # azimuth: 0 camera in front (-Y), +90 camera on character left (+X)


# ----------------------------------------------------------------------------------------
def fallback_skin(ctx):
    """Rigid weights per island from the island map's suggested bone (stand-in until WP-2's skin.py exists)."""
    arm = ctx.armature
    bones = {b.name for b in arm.data.bones}
    by_id = {i["id"]: i for i in costume.flat_islands(ctx.islands)}
    for pn, obj in ctx.parts.items():
        isls, co = islands_mod.vertex_islands(obj)
        for n, isl in enumerate(isls):
            info = by_id.get("%s.%02d" % (pn, n))
            bone = info["bone"] if info and info.get("bone") in bones else "Hips"
            vg = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            vg.add(isl["verts"], 1.0, "REPLACE")
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm


def set_pose(arm, spec):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
    for b, (x, y, z) in spec.items():
        if b in arm.pose.bones:
            arm.pose.bones[b].rotation_euler = Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ")
    bpy.context.view_layer.update()


def penetration(ctx, tol=0.008):
    """How deep overlay vertices sit inside the visible body pieces (evaluated pose). Metres."""
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for pn, obj in ctx.parts.items():
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        hidden = {i for i, m in enumerate(obj.data.materials) if m and m.name.endswith("_" + contract.HIDDEN_SLOT)}
        base = len(verts)
        verts += [tuple(ev.matrix_world @ v.co) for v in me.vertices]
        me.calc_loop_triangles()
        for t in me.loop_triangles:
            if me.polygons[t.polygon_index].material_index in hidden:
                continue
            polys.append(tuple(base + i for i in t.vertices))
        ev.to_mesh_clear()
    tree = BVHTree.FromPolygons(verts, polys)
    out = {}
    for obj in getattr(ctx, "overlay_objects", []):
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        depths = []
        worst = None
        for v in me.vertices:
            p = ev.matrix_world @ v.co
            loc, nor, idx, dist = tree.find_nearest(p)
            if loc is not None and (p - loc).dot(nor) < 0 and dist > tol:
                depths.append(dist)
                if worst is None or dist > worst[0]:
                    worst = (dist, v.index, [round(c, 3) for c in p])
        ev.to_mesh_clear()
        out[obj.name] = {"verts": len(me.vertices) if False else int(len(obj.data.vertices)), "inside_over_8mm": len(depths),
                         "inside_over_30mm": sum(1 for d in depths if d > 0.03), "max_depth_m": round(max(depths), 4) if depths else 0.0, "worst": worst[1:] if worst else None}
    return out


# ----------------------------------------------------------------------------------------
def setup_render(size, samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x = size
    sc.render.resolution_y = int(size * 4 / 3)
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    return sc


def make_camera(az_deg, ortho=2.35):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("PrevCam")
    if cam is None:
        cd = bpy.data.cameras.new("PrevCam")
        cd.type = "ORTHO"
        cam = bpy.data.objects.new("PrevCam", cd)
        sc.collection.objects.link(cam)
    cam.data.ortho_scale = ortho
    a = math.radians(az_deg)
    target = Vector((0, 0, 0.98))
    d = Vector((math.sin(a), -math.cos(a), 0.0))
    cam.location = target + d * 9.0
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam, d


def clear_lights():
    for o in list(bpy.data.objects):
        if o.type == "LIGHT":
            bpy.data.objects.remove(o, do_unlink=True)


def add_light(kind, loc, energy, colour, size=0.2, rot=None):
    ld = bpy.data.lights.new(kind, kind)
    ld.energy = energy
    ld.color = colour
    if kind == "POINT":
        ld.shadow_soft_size = size
    if kind == "SUN":
        ld.angle = math.radians(8)
    ob = bpy.data.objects.new(kind, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    if rot is not None:
        ob.rotation_euler = rot
    return ob


def setup_lights(mode, d):
    sc = bpy.context.scene
    clear_lights()
    right = Vector((d.y * -1.0, d.x, 0.0)) if False else Vector((math.cos(math.atan2(d.x, -d.y)), math.sin(math.atan2(d.x, -d.y)), 0))
    world = sc.world or bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    if mode == "neutral":
        bg.inputs[0].default_value = (0.42, 0.45, 0.50, 1)
        bg.inputs[1].default_value = 0.9
        sc.view_settings.view_transform = "Standard"
        key = Vector((0, 0, 0.98)) + d * 4 + right * -2.5 + Vector((0, 0, 3.0))
        add_light("SUN", key, 2.6, (1.0, 0.97, 0.92), rot=(Vector((0, 0, 0)) - key).to_track_quat("-Z", "Y").to_euler())
        fill = Vector((0, 0, 0.98)) + d * 4 + right * 3.0 + Vector((0, 0, 1.0))
        add_light("SUN", fill, 0.9, (0.85, 0.9, 1.0), rot=(Vector((0, 0, 0)) - fill).to_track_quat("-Z", "Y").to_euler())
    elif mode == "mixed":   # camp fire plus moon: both lights, as in the game at night
        bg.inputs[0].default_value = (0.03, 0.04, 0.085, 1)
        bg.inputs[1].default_value = 1.0
        sc.view_settings.view_transform = "AgX"
        pos = Vector((0, 0, 0.62)) + d * 1.5 + right * -1.35
        add_light("POINT", pos, 260.0, (1.0, 0.5, 0.2), size=0.18)
        moon = Vector((0, 0, 0.98)) + d * 2 + right * 3.0 + Vector((0, 0, 4.0))
        add_light("SUN", moon, 1.3, (0.55, 0.66, 1.0), rot=(Vector((0, 0, 0)) - moon).to_track_quat("-Z", "Y").to_euler())
    else:   # fire: dim cool moon ambient, one warm point light low and to the side, faint cool rim from behind
        bg.inputs[0].default_value = (0.020, 0.028, 0.055, 1)
        bg.inputs[1].default_value = 1.0
        sc.view_settings.view_transform = "AgX"
        pos = Vector((0, 0, 0.62)) + d * 1.5 + right * -1.35
        add_light("POINT", pos, 380.0, (1.0, 0.46, 0.16), size=0.18)
        rim = Vector((0, 0, 0.98)) - d * 4 + right * 2.0 + Vector((0, 0, 3.0))
        add_light("SUN", rim, 0.55, (0.45, 0.6, 1.0), rot=(Vector((0, 0, 0)) - rim).to_track_quat("-Z", "Y").to_euler())


def ensure_ground():
    if bpy.data.objects.get("PrevGround"):
        return
    bpy.ops.mesh.primitive_circle_add(vertices=48, radius=3.0, fill_type="NGON", location=(0, 0, -0.002))
    g = bpy.context.active_object
    g.name = "PrevGround"
    m = bpy.data.materials.new("PrevGroundMat")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.11, 0.09, 0.07, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    g.data.materials.append(m)


def render_view(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sheet(paths, out):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p)
        w, h = im.size
        a = np.empty(w * h * 4, dtype=np.float32)
        im.pixels.foreach_get(a)
        imgs.append(a.reshape(h, w, 4))
        bpy.data.images.remove(im)
    row = np.concatenate(imgs, axis=1)
    h, w = row.shape[:2]
    im = bpy.data.images.new("sheet", w, h, alpha=False)
    im.pixels.foreach_set(row.ravel())
    im.filepath_raw = out
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def run():
    args = common.parse_args([
        ("--variants", dict(default="historic,prehistoric")),
        ("--poses", dict(default="rest")),
        ("--views", dict(default="front,threeq,side,back")),
        ("--lights", dict(default="neutral,fire")),
        ("--render", dict(action="store_true")),
        ("--size", dict(type=int, default=420)),
        ("--samples", dict(type=int, default=40)),
        ("--tag", dict(default="")),
    ])
    keys = common.selected_keys(args.only)
    outdir = os.path.join(args.out, "wp4")
    report = {}
    for key in keys:
        if contract.CAST_BY_KEY[key]["rig"] != "humanoid":
            continue
        for variant in args.variants.split(","):
            ctx = common.load_character(key, args.src, args.out)
            if rig_mod is not None and hasattr(rig_mod, "fit_rig"):
                rig_mod.fit_rig(ctx)
            else:
                stub_rig.build_stub_armature(ctx)
            fallback_skin(ctx)
            costume.apply_costume(ctx, variant)
            d = os.path.join(outdir, key)
            os.makedirs(d, exist_ok=True)
            pen = {}
            for pose in args.poses.split(","):
                set_pose(ctx.armature, POSES[pose])
                pen[pose] = penetration(ctx)
                if args.render:
                    ensure_ground()
                    setup_render(args.size, args.samples)
                    for light in args.lights.split(","):
                        paths = []
                        for view in args.views.split(","):
                            cam, dvec = make_camera(VIEWS[view])
                            setup_lights(light, dvec)
                            p = os.path.join(d, "_tmp_%s_%s_%s_%s.png" % (variant, pose, light, view))
                            render_view(p)
                            paths.append(p)
                        sheet(paths, os.path.join(d, "%s%s_%s_%s.png" % (variant, args.tag, pose, light)))
                        for p in paths:
                            os.remove(p)
            report.setdefault(key, {})[variant] = {"penetration": pen, "draw": ctx.manifest["costumes"]["draw"]["total_tris"],
                                                   "overlay_tris": ctx.manifest["costumes"]["draw"]["overlay_tris"]}
            ctx.out = args.out
            common.write_json(os.path.join(d, "manifest_costumes_%s.json" % variant), ctx.manifest["costumes"])
            for pose, items in pen.items():
                for name, r in items.items():
                    print("[pen] %s %s %s %s verts=%d >8mm=%d >30mm=%d max=%.1fcm worst=%s" % (
                        key, variant, pose, name, r["verts"], r["inside_over_8mm"], r["inside_over_30mm"], r["max_depth_m"] * 100, r["worst"]))
    common.write_json(os.path.join(outdir, "preview_report%s.json" % args.tag), report)


if __name__ == "__main__":
    run()
