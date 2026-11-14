"""WP-5 preview: bake clips on a character and render front + side frames, a contact strip and a video.

  blender --background --factory-startup --disable-autoexec --python Tools/characters/preview_clips.py -- \
      --src <extracted zip folder> --out <work folder outside the repo> --only prince,mira,champion \
      [--clips Walk,Greet | --sets idle,duel] [--seq Name=ClipA+ClipB+ClipC] [--every N | --tiles 16] [--loops 2]
      [--base] [--rig-lib <folder with rig.py and skin.py>] [--ffmpeg <ffmpeg.exe>] [--size 608x440] [--keep-frames]
      [--tag name] [--ortho 3.5 --camx 0.32]   (a wider view for clips that leave the spot, such as Fall)

Local review tool only. It reads the supplied meshes (never committed), attaches every loose piece rigidly to the
bone the island map suggests (WP-2's skin.py replaces this when it exists and --rig-lib points at it), bakes the
character's style variants (<Clip>@<key>, or the base clips with --base) and writes under <out>/preview/<key>/:
  <Clip>.mp4          front and side view, the ground moving at the clip's native speed (loops repeat --loops times)
  <Clip>_strip.png    frames of the clip, numbered (about --tiles of them, or every --every-th)
  Seq_<Name>.mp4/_strip.png   the clips of a --seq played one after another (for clips that chain)
The ground marks are fixed in the world: a foot that is planted stays on its mark.
"""
import math
import os
import shutil
import subprocess
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import islands  # noqa: E402
import stub_rig  # noqa: E402
import anim_core  # noqa: E402

FRONT_X, SIDE_X = -0.78, 0.92


def find_ffmpeg(arg):
    if arg and os.path.isfile(arg):
        return arg
    p = shutil.which("ffmpeg")
    if p:
        return p
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages")
    if os.path.isdir(root):
        for d, _dirs, files in os.walk(root):
            if "ffmpeg.exe" in files:
                return os.path.join(d, "ffmpeg.exe")
    return None


def rigid_skin(ctx, arm):
    """Preview-only skin: one bone per loose piece (island map suggestion); cape and robe get simple chain blends."""
    bones = set(b.name for b in arm.data.bones)
    hip_z = ctx.islands["landmarks"]["hip_z"]
    for part, obj in ctx.parts.items():
        isl, co = islands.vertex_islands(obj)
        meta = ctx.islands["parts"][part]["islands"]
        groups = {}

        def add(bone, idx, w):
            if bone not in bones:
                bone = "Hips"
            g = groups.get(bone) or obj.vertex_groups.new(name=bone)
            groups[bone] = g
            g.add([int(idx)], float(w), "ADD")

        for k, il in enumerate(isl):
            m = meta[k]
            bone = m.get("bone") or "Hips"
            role = m.get("role")
            for vi in il["verts"]:
                x, y, z = co[vi]
                if role in ("robe_or_skirt", "skirt_hem") and "Skirt.F" in bones:
                    t = min(1.0, max(0.0, (hip_z - 0.05 - z) / max(0.05, hip_z - 0.35)))
                    t = t * t * (3 - 2 * t)
                    ang = math.atan2(x, -y)                 # 0 = front, +90 = left
                    lobes = {"Skirt.F": 0.0, "Skirt.L": math.pi / 2, "Skirt.B": math.pi, "Skirt.R": -math.pi / 2}
                    ws = {b: max(0.0, math.cos(ang - a)) ** 2 for b, a in lobes.items()}
                    tot = sum(ws.values()) or 1.0
                    if t < 1.0:
                        add("Hips", vi, 1.0 - t)
                    for b, wv in ws.items():
                        if wv * t / tot > 1e-4:
                            add(b, vi, wv * t / tot)
                elif role == "cloth_back" and "Cape.3" in bones:
                    z1 = arm.data.bones["Cape.1"].head_local.z
                    z3 = arm.data.bones["Cape.3"].tail_local.z
                    u = min(1.0, max(0.0, (z1 - z) / max(1e-3, z1 - z3)))
                    hats = {"Cape.1": max(0.0, 1.0 - 2.0 * u), "Cape.2": max(0.0, 1.0 - abs(2.0 * u - 1.0)),
                            "Cape.3": max(0.0, 2.0 * u - 1.0)}
                    for bn, wv in hats.items():
                        if wv > 1e-4:
                            add(bn, vi, wv)
                else:
                    add(bone, vi, 1.0)
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
        obj.parent = arm
    return True


def colour_materials():
    for m in bpy.data.materials:
        hx = common.hex_from_material_name(m.name)
        if hx:
            c = [int(hx[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
            c = [min(1.0, (v ** 2.2) * 1.35 + 0.02) for v in c]      # sRGB -> linear, lifted a little for the preview
            m.diffuse_color = (c[0], c[1], c[2], 1.0)


def make_stage(char_objs):
    """Collection with the character and its ground marks; a second, turned instance gives the side view."""
    scn = bpy.context.scene
    col = bpy.data.collections.new("CHAR")
    scn.collection.children.link(col)
    for o in char_objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)

    def box(name, sx, sy, sz, colr):
        me = bpy.data.meshes.new(name)
        v = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz), (-sx, -sy, 0), (sx, -sy, 0), (sx, sy, 0), (-sx, sy, 0)]
        f = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        me.from_pydata(v, [], f)
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = colr
        me.materials.append(mat)
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        return o

    floor = box("Floor", 0.52, 1.0, 0.02, (0.16, 0.17, 0.2, 1))
    floor.location.z = -0.004
    pivot = bpy.data.objects.new("MarkPivot", None)
    col.objects.link(pivot)
    marks = []
    for k in range(8):
        m = box("Mark%d" % k, 0.50, 0.018, 0.004, (0.75, 0.62, 0.3, 1) if k % 2 == 0 else (0.45, 0.48, 0.55, 1))
        m.parent = pivot
        marks.append(m)
    dots = []
    for k in range(8):
        d = box("Dot%d" % k, 0.03, 0.03, 0.004, (0.9, 0.3, 0.25, 1))
        d.parent = pivot
        dots.append(d)
    holder = bpy.data.objects.new("FrontHolder", None)
    scn.collection.objects.link(holder)
    holder.instance_type = "COLLECTION"
    holder.instance_collection = col
    holder.location = (FRONT_X, 0, 0)
    side = bpy.data.objects.new("SideHolder", None)
    scn.collection.objects.link(side)
    side.instance_type = "COLLECTION"
    side.instance_collection = col
    side.location = (SIDE_X, 0, 0)
    side.rotation_euler = (0, 0, math.radians(90))
    # hide the originals from the render by excluding the source collection from the view layer
    for lc in bpy.context.view_layer.layer_collection.children:
        if lc.collection == col:
            lc.exclude = True
    return marks, dots, pivot


def place_marks(marks, dots, pivot, dist, yaw):
    """Ground marks every 0.25 m, fixed in the world, drawn inside a 2.2 m window under the character."""
    win = 2.0
    pivot.rotation_euler = (0, 0, math.radians(-yaw))
    for k, m in enumerate(marks):
        y = ((k * 0.25 + dist + win / 2) % win) - win / 2          # the ground runs back (+Y) as dist grows
        m.location = (0, y, 0)
        dots[k].location = (0.40 if k % 2 == 0 else -0.40, ((k * 0.25 + 0.125 + dist + win / 2) % win) - win / 2, 0)


def setup_render(size, ortho=3.5, camx=0.32):
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    sh = scn.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_shadows = True
    sh.shadow_intensity = 0.35
    sh.show_cavity = True
    sh.show_object_outline = True
    scn.display.light_direction = (0.35, 0.45, 0.82)
    scn.display.render_aa = "5"
    scn.render.resolution_x, scn.render.resolution_y = size
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"
    scn.render.film_transparent = False
    scn.render.fps = contract.FPS
    scn.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("W")
    world.color = (0.035, 0.04, 0.055)
    scn.world = world
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = ortho
    cam = bpy.data.objects.new("Cam", cam_d)
    scn.collection.objects.link(cam)
    cam.location = (camx, -12.0, 1.22 * ortho / 3.5 + 12.0 * math.tan(math.radians(4.0)))
    cam.rotation_euler = (math.radians(86.0), 0, 0)
    scn.camera = cam


def render_clip(arm, baked, marks, dots, pivot, frames_dir):
    scn = bpy.context.scene
    act = bpy.data.actions[baked.name]
    anim_core.assign_action(arm, act)
    common.ensure_dir(frames_dir)
    for f in range(baked.frames + 1):
        scn.frame_set(f)
        place_marks(marks, dots, pivot, float(baked.ground_dist[f]), float(baked.ground_yaw[f]))
        scn.render.filepath = os.path.join(frames_dir, "f_%04d.png" % f)
        bpy.ops.render.render(write_still=True)


def encode(ffmpeg, frames_dir, n_frames, out_base, label, every, loop, loops, cols):
    font = "C\\:/Windows/Fonts/consola.ttf"
    txt = "drawtext=fontfile='%s':text='%s  f%%{frame_num}':start_number=0:x=8:y=8:fontsize=16:fontcolor=white:box=1:boxcolor=black@0.5" % (font, label)
    pat = os.path.join(frames_dir, "f_%04d.png")
    last = n_frames if not loop else n_frames - 1          # a loop's last frame repeats the first
    vf = txt
    cmd = [ffmpeg, "-y", "-loglevel", "error", "-framerate", "30", "-start_number", "0"]
    if loop and loops > 1:
        cmd += ["-stream_loop", str(loops - 1)]
    cmd += ["-i", pat, "-frames:v", str((last + 1) * (loops if loop else 1)), "-vf", vf, "-c:v", "libx264",
            "-pix_fmt", "yuv420p", "-crf", "20", out_base + ".mp4"]
    subprocess.run(cmd, check=False)
    picks = list(range(0, n_frames + 1, every))
    if picks[-1] != n_frames:
        picks.append(n_frames)
    sel = "+".join("eq(n\\,%d)" % p for p in picks)
    rows = int(math.ceil(len(picks) / float(cols)))
    vf2 = "%s,select='%s',scale=iw*0.79:-1,tile=%dx%d:padding=2" % (txt, sel, cols, rows)
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-start_number", "0", "-i", pat, "-vf", vf2, "-fps_mode", "vfr", "-frames:v", "1",
                    out_base + "_strip.png"], check=False)


def main():
    args = common.parse_args([
        ("--clips", {"default": ""}), ("--sets", {"default": ""}), ("--seq", {"action": "append", "default": []}),
        ("--every", {"type": int, "default": 0}), ("--tiles", {"type": int, "default": 16}), ("--loops", {"type": int, "default": 2}),
        ("--base", {"action": "store_true"}), ("--rig-lib", {"default": ""}), ("--ffmpeg", {"default": ""}),
        ("--size", {"default": "608x440"}), ("--keep-frames", {"action": "store_true"}), ("--cols", {"type": int, "default": 4}),
        ("--tag", {"default": ""}), ("--ortho", {"type": float, "default": 3.5}), ("--camx", {"type": float, "default": 0.32}),
    ], description="WP-5 clip preview")
    if not args.src or not args.out:
        raise SystemExit("--src and --out are required")
    ffmpeg = find_ffmpeg(args.ffmpeg)
    size = tuple(int(v) for v in args.size.split("x"))
    sets = [s for s in args.sets.split(",") if s] or None
    only = [c for c in args.clips.split(",") if c] or None
    if only and not sets:
        sets = None
    for key in common.selected_keys(args.only):
        ctx = common.load_character(key, args.src, args.out)
        arm = None
        if args.rig_lib:
            sys.path.insert(0, args.rig_lib)
            try:
                import rig as wp2_rig
                import skin as wp2_skin
                wp2_rig.fit_rig(ctx)
                wp2_skin.skin_character(ctx)
                arm = ctx.armature
                common.log("using WP-2 rig and skin from", args.rig_lib)
            except Exception as e:      # noqa: BLE001
                common.log("WP-2 rig not usable (%s); falling back to the stub" % e)
                ctx = common.load_character(key, args.src, args.out)
                arm = None
        if arm is None:
            arm = stub_rig.build_stub_armature(ctx)
            rigid_skin(ctx, arm)
        colour_materials()
        keep = {}
        style = False if args.base else [key]
        anim_core.build_clips(arm, set_names=sets, style=style, only=only, keep=keep)
        setup_render(size, args.ortho, args.camx)
        objs = [ctx.root, arm] + list(ctx.parts.values())
        marks, dots, pivot = make_stage(objs)
        outdir = common.ensure_dir(os.path.join(args.out, "preview" + (("_" + args.tag) if args.tag else ""), key))
        suffix = "" if args.base else "@" + key
        todo = []
        for name, b in keep.items():
            if args.base and "@" in name:
                continue
            if not args.base and not name.endswith(suffix):
                # a character without its own variant of this clip plays the base clip
                if (name + suffix) in keep:
                    continue
            todo.append(name)
        for name in todo:
            b = keep[name]
            fdir = os.path.join(outdir, "_frames", name.replace("@", "_"))
            render_clip(arm, b, marks, dots, pivot, fdir)
            if ffmpeg:
                every = args.every or max(1, int(math.ceil((b.frames + 1) / float(args.tiles))))
                encode(ffmpeg, fdir, b.frames, os.path.join(outdir, name.split("@")[0]), name, every, b.loop,
                       args.loops, args.cols)
            common.log("preview", key, name, "frames", b.frames)
        for spec in args.seq:
            label, _, chain = spec.partition("=")
            fdir = os.path.join(outdir, "_frames", "seq_" + label)
            common.ensure_dir(fdir)
            k = 0
            for cn in chain.split("+"):
                nm = cn + suffix if (cn + suffix) in keep else cn
                src = os.path.join(outdir, "_frames", nm.replace("@", "_"))
                b = keep[nm]
                for f in range(b.frames + (0 if cn != chain.split("+")[-1] else 1)):
                    shutil.copyfile(os.path.join(src, "f_%04d.png" % f), os.path.join(fdir, "f_%04d.png" % k))
                    k += 1
            if ffmpeg:
                every = args.every or max(1, int(math.ceil(k / float(args.tiles))))
                encode(ffmpeg, fdir, k - 1, os.path.join(outdir, "Seq_" + label), label, every, False, 1, args.cols)
        if not args.keep_frames:
            shutil.rmtree(os.path.join(outdir, "_frames"), ignore_errors=True)


if __name__ == "__main__":
    main()
