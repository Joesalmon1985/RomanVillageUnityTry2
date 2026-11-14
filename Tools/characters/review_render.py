"""Review renders of BUILT characters (output of build_cast.py) in a dim warm firelight set-up. Local review only.

  blender --background --factory-startup --disable-autoexec --python Tools/characters/review_render.py -- \\
      --out <build_cast --out> [--only prince,mira,champion] [--costume prehistoric,historic]
      [--what poses,faces,strips,pen,videos] [--clips Walk,Greet] [--sets idle,duel] [--duel mira,champion]

Everything is rendered from <out>/characters/<key>/<costume>/<key>@<costume>.blend: the real rig, skin, face,
costume and the clips baked on that rig (no stand-ins). Frames go to <out>/review/frames/...; a JSON shot list per
job goes to <out>/review/shots/. `review_sheets.py` (plain python, Pillow and ffmpeg) turns them into labelled
contact sheets and mp4 files under <out>/review/sheets and <out>/review/video.

  poses    rest and key poses (sampled from the character's own clips), front and three-quarter, per costume
  faces    the seven expressions plus a blink: dialogue framing, and the head at its true pixel size in a
           phone-sized frame (844 x 390) for a head 53 px and 27 px tall
  strips   every clip of every set, 8 frames each, three-quarter view
  pen      how deep overlay garments sink into the visible body in walk, run, kneel, cast and raised-arm frames
  videos   walk (start, walk, stop), turn (60 left and back), greet; with --duel a, b: the cast exchange at duel
           distance (a casts three times: b blocks, recoils light then heavy; b casts once, a blocks; a's last cast
           breaks b's ward)
"""
import json
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402

FIRE = (1.0, 0.47, 0.17)
MOON = (0.45, 0.58, 1.0)


# ----------------------------------------------------------------------------------------------------------------
# Scene
# ----------------------------------------------------------------------------------------------------------------
def open_build(out, key, costume):
    p = os.path.join(out, "characters", key, costume, "%s@%s.blend" % (key, costume))
    if not os.path.isfile(p):
        raise SystemExit("no build at %s (run build_cast.py first, without --no-blend)" % p)
    bpy.ops.wm.open_mainfile(filepath=p, load_ui=False)
    return Char.from_scene(key, costume, "")


class Char:
    def __init__(self):
        self.objs = []

    @staticmethod
    def from_scene(key, costume, suffix, objs=None):
        c = Char()
        c.key, c.costume = key, costume
        objs = objs or list(bpy.data.objects)
        c.objs = objs
        c.root = next(o for o in objs if o.type == "EMPTY" and o.parent is None and o.name.startswith(key))
        c.arm = next(o for o in objs if o.type == "ARMATURE")
        c.meshes = [o for o in objs if o.type == "MESH"]
        c.head = next((o for o in c.meshes if o.name.split(".")[0] == "Head"), None)
        man = os.path.join(OUT, "characters", key, costume, "manifest.json")
        c.manifest = common.read_json(man)
        c.actions = {}
        return c

    def action(self, base):
        """The character's own variant if it has one, else the base clip (by original name for appended files)."""
        for n in ("%s@%s" % (base, self.key), base):
            a = self.actions.get(n) if self.actions else bpy.data.actions.get(n)
            if a is not None:
                return a
        return None

    def set_action(self, act):
        ad = self.arm.animation_data or self.arm.animation_data_create()
        ad.action = act
        try:
            if act is not None and len(act.slots):
                ad.action_slot = act.slots[0]
        except AttributeError:
            pass

    def rest(self):
        self.set_action(None)
        for pb in self.arm.pose.bones:
            pb.location = (0, 0, 0)
            pb.rotation_euler = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)

    def expression(self, name, w=1.0, blink=0.0):
        if self.head is None or not self.head.data.shape_keys:
            return
        face = self.manifest.get("face", {})
        ex = face.get("expressions", {}).get(name, {"keys": {}})
        gains = face.get("tuning", {}).get("gain", {})
        ch = face.get("shape_key_channels", {})
        kb = self.head.data.shape_keys.key_blocks
        for k in contract.SHAPE_KEYS:
            if k in kb:
                kb[k].value = 0.0
        for k, v in ex.get("keys", {}).items():
            if k in kb:
                kb[k].value = min(1.0, v * w * gains.get(ch.get(k, ""), 1.0))
        if blink > 0:
            for k in ("Blink.L", "Blink.R"):
                kb[k].value = max(kb[k].value, blink)


def mat(name, rgb, emit=0.0, rough=0.95):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*rgb, 1)
        b.inputs["Emission Strength"].default_value = emit
    return m


def setup_stage(size=(640, 480), samples=16, ground=True, fire_at=None):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    try:
        sc.eevee.taa_render_samples = samples
        sc.eevee.use_shadows = True
    except AttributeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast" if "AgX - Medium High Contrast" in [
        i.identifier for i in sc.view_settings.bl_rna.properties["look"].enum_items] else "None"
    sc.render.fps = contract.FPS
    w = sc.world or bpy.data.worlds.new("Night")
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.012, 0.016, 0.032, 1)
    bg.inputs[1].default_value = 1.0
    for o in [o for o in bpy.data.objects if o.name.startswith("RV_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    if ground:
        me = bpy.data.meshes.new("RV_Ground")
        n = 40
        r = 20.0
        verts, faces = [], []
        for i in range(n + 1):
            for j in range(n + 1):
                verts.append((-r + 2 * r * i / n, -r + 2 * r * j / n, 0.0))
        for i in range(n):
            for j in range(n):
                a = i * (n + 1) + j
                faces.append((a, a + n + 1, a + n + 2, a + 1))
        me.from_pydata(verts, [], faces)
        g = bpy.data.objects.new("RV_Ground", me)
        sc.collection.objects.link(g)
        m1 = mat("RV_Earth", (0.075, 0.058, 0.045))
        m2 = mat("RV_Earth2", (0.06, 0.047, 0.037))
        me.materials.append(m1)
        me.materials.append(m2)
        for k, p in enumerate(me.polygons):
            i, j = divmod(k, n)
            p.material_index = (i + j) % 2
    fire = Vector(fire_at or (-1.4, -1.6, 0.35))
    add_light("RV_Fire", "POINT", fire, 420.0, FIRE, size=0.25)
    add_light("RV_Fire2", "POINT", fire + Vector((0.25, 0.2, 0.25)), 120.0, (1.0, 0.6, 0.25), size=0.3)
    moon = add_light("RV_Moon", "SUN", (3, 4, 6), 0.5, MOON)
    moon.rotation_euler = (Vector((0, 0, 0)) - Vector((3, 4, 6))).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.objects.get("RV_Cam")
    if cam is None:
        cd = bpy.data.cameras.new("RV_Cam")
        cam = bpy.data.objects.new("RV_Cam", cd)
        sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def add_light(name, kind, loc, energy, colour, size=0.2):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = colour
    if kind == "POINT":
        ld.shadow_soft_size = size
    else:
        ld.angle = math.radians(5)
    ob = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    return ob


def aim_cam(cam, eye, target, lens=50.0, ortho=None):
    cam.location = Vector(eye)
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.type = "PERSP"
        cam.data.lens = lens
    cam.data.clip_start = 0.05
    cam.data.clip_end = 100


def view_dir(az_deg):
    """Camera direction for an azimuth: 0 = in front of a character facing -Y, + = toward its left (+X)."""
    a = math.radians(az_deg)
    return Vector((math.sin(a), -math.cos(a), 0.0))


def render(path):
    common.ensure_dir(os.path.dirname(path))
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def frame(f):
    fi = int(math.floor(f))
    bpy.context.scene.frame_set(fi, subframe=f - fi)


def shot_log(name, data):
    common.write_json(os.path.join(OUT, "review", "shots", name + ".json"), data)


# ----------------------------------------------------------------------------------------------------------------
# Poses sheet
# ----------------------------------------------------------------------------------------------------------------
KEY_POSES = [("rest", None, 0), ("walk", "Walk", 0.25), ("run", "Run", 0.3), ("greet", "Greet", 23 / 60.0),
             ("cast_wound", "CastAnticipate", 1.0), ("cast_release", "CastRelease", 0.55),
             ("raised_arm", "Victory", 17 / 75.0), ("kneel", "WardBreak", 1.0), ("kneel_take", "KneelTake", 0.33)]


def sample(c, base, u):
    a = c.action(base)
    if a is None:
        return False
    c.set_action(a)
    frame(a.frame_start + u * (a.frame_end - a.frame_start))
    return True


def job_poses(c):
    cam = setup_stage((420, 560), samples=24)
    shots = []
    for name, clip, u in KEY_POSES:
        if clip is None:
            c.rest()
            bpy.context.scene.frame_set(0)
        elif not sample(c, clip, u):
            continue
        c.expression("neutral")
        row = []
        for az in (0, 35, 90):
            d = view_dir(az)
            tgt = Vector((0, 0, 0.92))
            aim_cam(cam, tgt + d * 7.0 + Vector((0, 0, 0.25)), tgt, ortho=2.25)
            p = os.path.join(OUT, "review", "frames", "poses", c.key, c.costume, "%s_%d.png" % (name, az))
            row.append(render(p))
        shots.append({"pose": name, "clip": clip, "views": ["front", "3q", "side"], "files": row})
    shot_log("poses_%s_%s" % (c.key, c.costume), {"key": c.key, "costume": c.costume, "rows": shots})


# ----------------------------------------------------------------------------------------------------------------
# Faces
# ----------------------------------------------------------------------------------------------------------------
STATES = [("neutral", 0), ("attentive", 0), ("concentrating", 0), ("surprised", 0), ("pleased", 0), ("pained", 0),
          ("determined", 0), ("blink", 1.0)]


def head_point(c):
    b = c.arm.data.bones
    h = c.arm.matrix_world @ b["Head"].head_local
    t = c.arm.matrix_world @ b["Sock.HeadTop"].head_local
    return (h + t) / 2, (t - h).length


def job_faces(c):
    c.rest()
    c.set_action(c.action("Idle_A"))
    bpy.context.scene.frame_set(0)
    hc, hh = head_point(c)
    cam = setup_stage((360, 400), samples=32, fire_at=(-1.2, -2.0, 1.15))
    rows = []
    for st, blink in STATES:
        c.expression("neutral" if st == "blink" else st, blink=blink)
        files = {}
        # dialogue framing, three-quarter from the fire side, about 2.5 m
        d = view_dir(-28)
        aim_cam(cam, hc + d * 2.5 + Vector((0, 0, 0.02)), hc + Vector((0, 0, -0.02)), lens=135)
        files["dialogue"] = render(os.path.join(OUT, "review", "frames", "faces", c.key, c.costume, "%s_dialogue.png" % st))
        rows.append({"state": st, "files": files})
    # phone framing: an 844 x 390 frame with the head at a known pixel height (true size, no filtering)
    phone = []
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 844, 390
    for px in (53, 27):
        for st in ("neutral", "concentrating", "surprised", "determined"):
            c.expression(st)
            # perspective camera, 40 degree vertical field of view; distance chosen for a head px tall
            vfov = math.radians(40.0)
            dist = (hh * 1.35) / (2 * math.tan(vfov / 2) * px / 390.0)
            d = view_dir(-20)
            cam.data.type = "PERSP"
            cam.data.sensor_fit = "VERTICAL"
            cam.data.angle_y = vfov
            cam.location = hc + d * dist + Vector((0, 0, 0.3))
            cam.rotation_euler = (hc - cam.location).to_track_quat("-Z", "Y").to_euler()
            p = render(os.path.join(OUT, "review", "frames", "faces", c.key, c.costume, "phone%d_%s.png" % (px, st)))
            # where the head is in the frame, for the crop
            from bpy_extras.object_utils import world_to_camera_view
            v = world_to_camera_view(sc, cam, hc)
            phone.append({"px": px, "state": st, "file": p, "centre": [v.x, v.y], "head_px": px})
    c.expression("neutral")
    sc.render.resolution_x, sc.render.resolution_y = 360, 400
    shot_log("faces_%s_%s" % (c.key, c.costume), {"key": c.key, "costume": c.costume, "rows": rows, "phone": phone})


# ----------------------------------------------------------------------------------------------------------------
# Clip strips
# ----------------------------------------------------------------------------------------------------------------
def job_strips(c, sets, only_clips):
    cam = setup_stage((240, 320), samples=12)
    out = []
    for set_name in sets:
        for base in contract.CLIP_SETS[set_name]:
            if only_clips and base not in only_clips:
                continue
            a = c.action(base)
            if a is None:
                continue
            c.set_action(a)
            n = int(a.frame_end)
            fr = [round(n * k / 7.0, 2) for k in range(8)]
            files = []
            gd = list(a.get("ground_m", [])) if a.get("ground_m") is not None else []
            gy = list(a.get("ground_yaw_deg", [])) if a.get("ground_yaw_deg") is not None else []
            for f in fr:
                frame(f)
                # move the character along its root curve so travel and turns read in the strip
                i = min(int(f), len(gd) - 1) if gd else 0
                dist = gd[i] if gd and a.get("native_speed_mps", 0) > 0 and not a.use_cyclic else 0.0
                yaw = gy[i] if gy else 0.0
                c.root.location = (0, -dist, 0)
                c.root.rotation_euler = (0, 0, math.radians(yaw))
                bpy.context.view_layer.update()
                tgt = Vector((0, -dist * 0.5 if dist else 0.0, 0.9))
                d = view_dir(32)
                aim_cam(cam, tgt + d * 7.0 + Vector((0, 0, 0.6)), tgt, ortho=2.5 if base not in ("Fall",) else 3.4)
                files.append(render(os.path.join(OUT, "review", "frames", "strips", c.key, c.costume, a.name.replace("@", "_at_"),
                                                 "f%05.1f.png" % f)))
            c.root.location = (0, 0, 0)
            c.root.rotation_euler = (0, 0, 0)
            out.append({"set": set_name, "clip": a.name, "frames": fr, "files": files, "length": n})
    shot_log("strips_%s_%s" % (c.key, c.costume), {"key": c.key, "costume": c.costume, "rows": out})


# ----------------------------------------------------------------------------------------------------------------
# Overlay penetration in real poses (integration issue 1)
# ----------------------------------------------------------------------------------------------------------------
PEN_FRAMES = [("Walk", [0.0, 0.25, 0.5, 0.75]), ("Run", [0.0, 0.25, 0.5, 0.75]), ("WardBreak", [0.6, 1.0]),
              ("KneelTake", [0.6, 1.0]), ("CastAnticipate", [1.0]), ("CastRelease", [0.4, 1.0]),
              ("Victory", [0.23, 0.45]), ("Greet", [0.38]), ("Turn60L", [0.4]), ("Defeat", [1.0])]


def body_tree(c):
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for o in c.meshes:
        if o.name.startswith(contract.OVERLAY_PREFIX):
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        hidden = {i for i, m in enumerate(o.data.materials) if m and m.name.endswith("_" + contract.HIDDEN_SLOT)}
        base = len(verts)
        verts += [tuple(ev.matrix_world @ v.co) for v in me.vertices]
        me.calc_loop_triangles()
        for t in me.loop_triangles:
            if me.polygons[t.polygon_index].material_index in hidden:
                continue
            polys.append(tuple(base + i for i in t.vertices))
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys)


def job_pen(c):
    dg = bpy.context.evaluated_depsgraph_get()
    rep = {}
    ovs = [o for o in c.meshes if o.name.startswith(contract.OVERLAY_PREFIX) and "costume_prop" not in o.keys()]
    for base, us in PEN_FRAMES:
        a = c.action(base)
        if a is None:
            continue
        c.set_action(a)
        for u in us:
            f = a.frame_start + u * (a.frame_end - a.frame_start)
            frame(f)
            dg = bpy.context.evaluated_depsgraph_get()
            tree = body_tree(c)
            for o in ovs:
                ev = o.evaluated_get(dg)
                me = ev.to_mesh()
                worst, n30, n15 = 0.0, 0, 0
                wp = None
                for v in me.vertices:
                    p = ev.matrix_world @ v.co
                    loc, nor, idx, dist = tree.find_nearest(p)
                    # inside a piece, by the nearest face's normal; no body piece is thicker than 24 cm, so a
                    # deeper "inside" is a point between two pieces, not in one
                    if loc is not None and (p - loc).dot(nor) < 0 and dist < 0.12:
                        n15 += dist > 0.015
                        n30 += dist > 0.03
                        if dist > worst:
                            worst, wp = dist, [round(x, 3) for x in p]
                ev.to_mesh_clear()
                r = rep.setdefault(o.name, {})
                r["%s@%.2f" % (a.name, u)] = {"max_cm": round(worst * 100, 1), "verts_over_15mm": n15,
                                              "verts_over_30mm": n30, "of": len(o.data.vertices), "worst_at": wp}
    summary = {o: max(v.values(), key=lambda x: x["max_cm"]) for o, v in rep.items()}
    common.write_json(os.path.join(OUT, "review", "pen", "%s_%s.json" % (c.key, c.costume)),
                      {"key": c.key, "costume": c.costume, "per_frame": rep, "worst": summary})
    for o, v in rep.items():
        w = max(v.items(), key=lambda kv: kv[1]["max_cm"])
        print("[pen] %s %s %-14s worst %5.1f cm at %s (%d verts > 3 cm)" % (c.key, c.costume, o, w[1]["max_cm"], w[0],
                                                                          w[1]["verts_over_30mm"]))


# ----------------------------------------------------------------------------------------------------------------
# Sequencer: clips played one after another with root motion, for the videos
# ----------------------------------------------------------------------------------------------------------------
class Seq:
    """A list of (action, from_frame, to_frame, expression) segments with root motion; pose(t) poses the character
    at global frame t. The root moves along each clip's ground curve (in-place clips) and turns by its yaw."""

    def __init__(self, c, origin=(0, 0, 0), yaw=0.0):
        self.c = c
        self.segs = []
        self.origin = Vector(origin)
        self.yaw0 = yaw
        self.total = 0.0

    def add(self, base, f0=None, f1=None, expr="neutral", loops=1):
        a = self.c.action(base) if isinstance(base, str) else base
        if a is None:
            raise KeyError("no action %s for %s" % (base, self.c.key))
        f0 = a.frame_start if f0 is None else f0
        f1 = a.frame_end if f1 is None else f1
        if loops > 1:
            f1 = f0 + (a.frame_end - a.frame_start) * loops - (a.frame_start - f0 if f0 < a.frame_start else 0)
        self.segs.append({"a": a, "f0": float(f0), "f1": float(f1), "t0": self.total, "expr": expr})
        self.total += float(f1) - float(f0)
        return self

    def _ground(self, a, f):
        """Distance travelled and yaw turned by clip a from its frame 0 to local frame f (f may exceed one cycle)."""
        n = a.frame_end - a.frame_start
        gm = list(a.get("ground_m", []))
        gy = list(a.get("ground_yaw_deg", []))
        v = float(a.get("native_speed_mps", 0.0))
        if a.use_cyclic:
            return v * f / contract.FPS, 0.0
        if not gm:
            return 0.0, 0.0
        f = max(0.0, min(f, n))
        i = int(math.floor(f))
        j = min(i + 1, len(gm) - 1)
        u = f - i
        d = gm[min(i, len(gm) - 1)] * (1 - u) + gm[j] * u
        y = (gy[min(i, len(gy) - 1)] * (1 - u) + gy[j] * u) if gy else 0.0
        return d, y

    def pose(self, t):
        pos = Vector(self.origin)
        yaw = self.yaw0
        cur = None
        for s in self.segs:
            if t >= s["t0"] + (s["f1"] - s["f0"]) and s is not self.segs[-1]:
                d0, y0 = self._ground(s["a"], s["f0"])
                d1, y1 = self._ground(s["a"], s["f1"])
                fwd = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Vector((0, -1, 0))
                pos += fwd * (d1 - d0)
                yaw += y1 - y0
                continue
            cur = s
            break
        s = cur
        lf = s["f0"] + (t - s["t0"])
        lf = min(lf, s["f1"])
        d0, y0 = self._ground(s["a"], s["f0"])
        d1, y1 = self._ground(s["a"], lf)
        a = s["a"]
        n = a.frame_end - a.frame_start
        play = a.frame_start + ((lf - a.frame_start) % n if a.use_cyclic else lf - a.frame_start)
        if a.use_cyclic and abs(play - a.frame_end) < 1e-6:
            play = a.frame_start
        self.c.set_action(a)
        fwd = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Vector((0, -1, 0))
        self.c.root.location = pos + fwd * (d1 - d0)
        # an in-place clip's own yaw is in the pose only for turn clips; the root takes the ground yaw
        self.c.root.rotation_euler = (0, 0, math.radians(yaw + (y1 - y0)))
        frame(play)
        return s


def blink_at(t, seed):
    """A blink every 2.5 to 6 s (deterministic), 6 frames long."""
    import random
    r = random.Random(seed)
    k = 0.0
    while k < t + 10:
        k += r.uniform(75, 170)
        if 0 <= t - k < 6:
            u = (t - k) / 6.0
            return math.sin(math.pi * min(1.0, u * 1.6)) if u < 0.62 else max(0.0, 1 - (u - 0.62) / 0.38)
    return 0.0


def follow_fire(c, offset=(-1.5, -1.7, 0.35)):
    """For single-character videos: the fire keeps its place relative to the character (as if it walked past a row
    of fires), so the walk is not a silhouette half way through. Returns an `extra` callback."""
    f1, f2 = bpy.data.objects["RV_Fire"], bpy.data.objects["RV_Fire2"]

    def cb(t):
        base = c.root.location.copy()
        f1.location = base + Vector(offset)
        f2.location = base + Vector(offset) + Vector((0.25, 0.2, 0.25))
    return cb


def render_seq(name, seqs, cams, n_frames, every=1, extra=None):
    """seqs: list of Seq; cams: function(t) -> (eye, target, lens). Writes frames and a shot json."""
    sc = bpy.context.scene
    cam = sc.camera
    files = []
    for t in range(0, int(n_frames), every):
        for k, s in enumerate(seqs):
            seg = s.pose(float(t))
            s.c.expression(seg["expr"], blink=blink_at(t, k * 17 + 3))
        if extra:
            extra(t)
        bpy.context.view_layer.update()
        eye, tgt, lens = cams(t)
        aim_cam(cam, eye, tgt, lens=lens)
        files.append(render(os.path.join(OUT, "review", "frames", "video", name, "%05d.png" % t)))
    shot_log("video_" + name, {"name": name, "frames": len(files), "fps": contract.FPS // every, "dir":
                               os.path.join(OUT, "review", "frames", "video", name)})


def job_videos(c):
    setup_stage((960, 540), samples=12)
    sc = bpy.context.scene
    # 1. walk: idle, start, walk (two cycles from the start's exit phase), stop on the left heel strike, settle
    st = c.action("StartL")
    wk = c.action("Walk")
    s = Seq(c)
    s.add("Idle_A", 0, 24)
    s.add(st)
    n = wk.frame_end
    exit_ph = 0.12
    rec = {r["name"]: r for r in c.manifest.get("clips", [])}
    if st.name in rec and "exit_phase" in rec[st.name]:
        exit_ph = rec[st.name]["exit_phase"]
    s.add(wk, exit_ph * n, 2 * n)
    s.add("StopL")
    s.add("Idle_A", 0, 40)
    total = s.total

    def cam_walk(t):
        p = c.root.location.copy() + Vector((0, 0, 0.85))
        return p + view_dir(55) * 5.0 + Vector((0, 0, 0.6)), p, 35
    render_seq("%s_%s_walk" % (c.key, c.costume), [s], cam_walk, total, extra=follow_fire(c))
    # 2. turn: 60 left, hold, 60 right back, settle
    s = Seq(c)
    s.add("Idle_A", 0, 20).add("Turn60L").add("Idle_A", 0, 24).add("Turn60R").add("Idle_A", 0, 30)

    def cam_turn(t):
        p = Vector((0, 0, 0.9))
        return p + view_dir(20) * 6.0 + Vector((0, 0, 1.0)), p, 40
    c.root.location = (0, 0, 0)
    render_seq("%s_%s_turn" % (c.key, c.costume), [s], cam_turn, s.total, extra=follow_fire(c))
    # 3. greet at dialogue distance
    s = Seq(c)
    s.add("Idle_A", 0, 18, "neutral").add("Greet", expr="pleased").add("Nod", expr="attentive").add("Idle_A", 0, 30, "attentive")
    hc = Vector((0, 0, 1.45))

    def cam_greet(t):
        return Vector((0, 0, 1.35)) + view_dir(-25) * 3.8 + Vector((0, 0, 0.1)), Vector((0, 0, 1.25)), 42
    c.root.location = (0, 0, 0)
    render_seq("%s_%s_greet" % (c.key, c.costume), [s], cam_greet, s.total, extra=follow_fire(c, (-1.3, -2.0, 1.0)))


# ----------------------------------------------------------------------------------------------------------------
# Duel exchange: two characters facing each other at duel distance
# ----------------------------------------------------------------------------------------------------------------
def append_char(key, costume):
    p = os.path.join(OUT, "characters", key, costume, "%s@%s.blend" % (key, costume))
    with bpy.data.libraries.load(p, link=False) as (src, dst):
        dst.objects = list(src.objects)
        dst.actions = list(src.actions)
        names = list(src.actions)
    objs = [o for o in dst.objects if o is not None]
    for o in objs:
        bpy.context.scene.collection.objects.link(o)
    c = Char.from_scene(key, costume, "", objs)
    c.actions = {n: a for n, a in zip(names, dst.actions) if a is not None}
    return c


def make_bolt():
    me = bpy.data.meshes.new("RV_Bolt")
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.07)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("RV_Bolt", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat("RV_BoltMat", (0.75, 0.45, 1.0), emit=18.0))
    lt = add_light("RV_BoltLight", "POINT", (0, 0, -5), 0.0, (0.75, 0.5, 1.0), size=0.1)
    return o, lt


def job_duel(a_key, b_key, costume):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    A = append_char(a_key, costume)
    B = append_char(b_key, costume)
    D = 5.5
    # A at the origin facing +Y (toward B); B at y = D facing -Y (toward A)
    sa, sb = Seq(A, (0, 0, 0), 180.0), Seq(B, (0, D, 0), 0.0)
    fire = (2.1, D * 0.3, 0.35)                                     # a camp fire beside the pair, nearer the caster
    setup_stage((960, 540), samples=12, fire_at=fire)
    bolt, blight = make_bolt()
    flight = 18                                                     # 0.6 s
    beats = []                                                      # (release frame, from, to, reaction)

    def rec_of(c, name):
        a = c.action(name)
        for r in c.manifest.get("clips", []):
            if r["name"] == a.name:
                return r
        return {"events": []}

    def release_frame(c, name):
        for e in rec_of(c, name).get("events", []):
            if e["name"] == "Release":
                return e["frame"]
        return 2

    def cast(caster, other, s_c, s_o, reaction, c_expr, o_expr, gap_after=20):
        """caster: anticipate, release, recover. other: idle until impact, then the reaction."""
        t0 = max(s_c.total, s_o.total)
        if s_c.total < t0:
            s_c.add("Idle_A", 0, t0 - s_c.total, "determined")
        s_c.add("CastAnticipate", expr=c_expr)
        rel = s_c.total + release_frame(caster, "CastRelease")
        s_c.add("CastRelease", expr=c_expr).add("CastRecover", expr=c_expr)
        impact = rel + flight
        s_o.add("Idle_A", 0, impact - s_o.total, "attentive")
        s_o.add(reaction, expr=o_expr)
        beats.append((rel, caster, other, reaction))
        return impact

    sa.add("Idle_A", 0, 30, "concentrating")
    sb.add("Idle_A", 0, 30, "determined")
    cast(A, B, sa, sb, "Block", "concentrating", "determined")
    cast(A, B, sa, sb, "RecoilLight", "concentrating", "pained")
    cast(B, A, sb, sa, "Block", "determined", "concentrating")
    cast(A, B, sa, sb, "RecoilHeavy", "concentrating", "pained")
    cast(A, B, sa, sb, "WardBreak", "determined", "surprised")
    end = max(sa.total, sb.total) + 50                              # the break holds 1.5 s and more
    if sa.total < end:
        sa.add("Idle_A", 0, end - sa.total, "determined")
    # B holds the last frame of the ward break (the last segment of a Seq holds its end frame)
    sb.total = end
    starts = {}

    def extra(t):
        bolt.location = (0, 0, -5)
        blight.data.energy = 0.0
        for k, (rel, cst, oth, _r) in enumerate(beats):
            if rel <= t < rel + flight:
                u = (t - rel) / flight
                if k not in starts:          # the bolt leaves the spell-origin socket at the release frame
                    bpy.context.view_layer.update()
                    starts[k] = cst.root.matrix_world @ (cst.arm.matrix_local @ cst.arm.pose.bones["Sock.SpellOrigin"].head)
                m0 = starts[k]
                tgt = oth.root.location + Vector((0, 0, 1.2))
                bolt.location = m0.lerp(tgt, u)
                blight.location = bolt.location
                blight.data.energy = 60.0

    def cams(t):
        # behind and beside A, about 2 m up: A left of centre in three-quarter back view, B's face in the right half
        eye = Vector((1.25, -2.4, 1.95))
        tgt = Vector((-0.15, D * 0.7, 1.0))
        return eye, tgt, 40

    total = max(sa.total, sb.total)
    render_seq("duel_%s_%s_%s" % (a_key, b_key, costume), [sa, sb], cams, total, extra=extra)

    def cams_side(t):
        return Vector((8.6, D * 0.5, 1.6)), Vector((0, D * 0.5, 0.95)), 32
    render_seq("duel_%s_%s_%s_side" % (a_key, b_key, costume), [sa, sb], cams_side, total, extra=extra)
    shot_log("duel_beats_%s_%s_%s" % (a_key, b_key, costume),
             {"beats": [{"release": r, "from": c.key, "to": o.key, "reaction": x} for r, c, o, x in beats],
              "frames": total})


# ----------------------------------------------------------------------------------------------------------------
def main():
    global OUT
    args = common.parse_args([
        ("--costume", {"default": "prehistoric"}), ("--what", {"default": "poses,faces,strips,pen"}),
        ("--clips", {"default": ""}), ("--sets", {"default": ""}), ("--duel", {"default": ""}),
    ])
    OUT = os.path.abspath(args.out)
    what = set(w for w in args.what.split(",") if w)
    keys = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] == "humanoid"] if args.only else []
    costumes = contract.COSTUMES if args.costume == "all" else args.costume.split(",")
    sets = [s for s in args.sets.split(",") if s] or list(contract.CLIP_SETS)
    only_clips = [c for c in args.clips.split(",") if c]
    for key in keys:
        for cos in costumes:
            for w in ("poses", "faces", "strips", "pen", "videos"):
                if w not in what:
                    continue
                c = open_build(OUT, key, cos)
                common.log("review", key, cos, w)
                if w == "poses":
                    job_poses(c)
                elif w == "faces":
                    job_faces(c)
                elif w == "strips":
                    job_strips(c, sets, only_clips)
                elif w == "pen":
                    job_pen(c)
                elif w == "videos":
                    job_videos(c)
    if args.duel:
        a, b = args.duel.split(",")
        for cos in costumes:
            job_duel(a, b, cos)


OUT = None
main()
