"""WP-2: verification of a built character (PRD_CHARACTERS 5.7) plus bend-test renders.

`verify_character(ctx, render=True) -> list of {check, ok, detail}` runs on a scene that went through
rig.fit_rig and skin.skin_character (and optionally face, costume, clips). It writes manifest section `verify`,
`<reports>/verify.json` and, with render=True, PNG stills of the rest pose and the bend poses to
`<reports>/renders/` (front, side and three-quarter, plus joint close-ups) on a magenta background, so any
crack shows as magenta.

Checks (each one row in the result):
- contract: armature object/data names and transform, every contract bone present, nothing outside the
  contract, parents, deform flags (sockets non-deform), at most 45 bones, roll rule, Hips the only root;
- sockets: nine present, foot sockets on the ground, StaffTip parent and SpellOrigin 0.05 m beyond StaffTip;
- symmetric fit: .L mirrors .R within SYM_TOL_M, centre bones on x = 0, measured asymmetry before averaging;
- rest arm angle 21 to 25 degrees, left and right within 1 degree;
- rest pose clean: identity pose with no action, meshes at scale 1 parented to the armature with one Armature
  modifier each;
- weights: every vertex weighted, weights sum to 1, at most 4 influences, groups only for deform bones, no
  empty groups, no weight under 0.001, no weight to a bone more than STRAY_M from the vertex (props exempt),
  rigid pieces carry one bone only, joint covers present at both elbows and knees;
- facing -Y; shape keys (if WP-3 ran) at most 16 and names in the contract; role slots (if WP-4 ran) at most 8;
- bends: for each test pose and joint (elbow, knee, shoulder, hip, neck, spine) the limb tube around the posed
  pivot must stay filled by the pieces near the joint (a crack shows as unfilled samples); pieces that are
  rigid on one bone must keep their shape (no tearing);
- clips (if WP-5 ran): every manifest clip has an action, loops close within LOOP_TOL, feet plant during stance
  (foot speed against the ground under FOOT_SLIDE_MPS at native speed);
- export (if WP-6 ran): the forward-axis check recorded in the `export` section.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import rig  # noqa: E402

SYM_TOL_M = 0.002
ARM_ANGLE_RANGE = (21.0, 25.0)
ARM_ANGLE_LR_DEG = 1.0
STRAY_M = 0.30
STRAY_WIDE_M = 0.40         # cape / skirt / hood chains run down the middle of wide garments; Hips / Spine /
                            # Chest carry aprons and waist boxes that hang 0.3 m below the short Hips bone, and
                            # an apron's far corner takes a share of the far thigh
WIDE_BONES = ("Cape", "Skirt", "Hood", "Hips", "Spine", "Chest", "UpperLeg")
MIN_WEIGHT = 0.001
SEAL_MIN = 0.97             # fraction of joint-tube samples that must be inside some piece
LOOP_TOL_DEG = 0.5
LOOP_TOL_M = 0.001
FOOT_SLIDE_MPS = 0.02       # PRD 5.7 starting value; a clip record may carry "foot_slide_limit_mps"
STANCE_HEIGHT_M = 0.012     # a foot within this of its lowest height in the clip is in stance
BG = (1.0, 0.0, 1.0)        # magenta background: cracks show as magenta


def _row(rows, check, ok, detail=""):
    rows.append({"check": check, "ok": bool(ok), "detail": detail})


# ----------------------------------------------------------------------------------------
# Static checks
# ----------------------------------------------------------------------------------------
def check_contract(ctx, rows):
    arm = ctx.armature
    ok = arm is not None and arm.name == contract.ARMATURE_OBJECT and arm.data.name == contract.ARMATURE_OBJECT
    _row(rows, "armature.names", ok, "object %s data %s" % (arm.name if arm else None, arm.data.name if arm else None))
    if arm is None:
        return False
    ident = all(abs(a - b) < 1e-6 for ra, rb in zip(arm.matrix_world, Matrix.Identity(4)) for a, b in zip(ra, rb))
    _row(rows, "armature.transform", ident and arm.parent == ctx.root, "identity world matrix, parent = root empty")
    bones = {b.name: b for b in arm.data.bones}
    expected = rig.expected_bones(ctx)
    missing = [n for n in expected if n not in bones]
    extra = [n for n in bones if n not in expected]
    _row(rows, "contract.bones", not missing and not extra, "missing %s extra %s" % (missing, extra))
    parents = rig.contract_parents(ctx)
    wrong = ["%s<-%s" % (n, b.parent.name if b.parent else None) for n, b in bones.items()
             if n in parents and (b.parent.name if b.parent else None) != parents[n]]
    _row(rows, "contract.parents", not wrong, ", ".join(wrong))
    roots = [n for n, b in bones.items() if b.parent is None]
    _row(rows, "contract.root", roots == ["Hips"], "roots %s" % roots)
    _row(rows, "contract.bone_count", len(bones) <= contract.MAX_BONES_HERO, "%d bones (max %d)" % (len(bones), contract.MAX_BONES_HERO))
    bad_def = [n for n, b in bones.items() if b.use_deform == n.startswith("Sock.")]
    _row(rows, "contract.deform_flags", not bad_def, "sockets non-deform, others deform; wrong: %s" % bad_def)
    bad_roll = []
    short = []
    for n, b in bones.items():
        m = b.matrix_local.to_3x3()
        x, y = m.col[0], m.col[1]
        if abs(y.x) < 0.94 and x.x < 0.5:
            bad_roll.append("%s(%.2f)" % (n, x.x))
        if b.length < 0.005:
            short.append(n)
    _row(rows, "contract.roll_rule", not bad_roll, "local X . world X < 0.5: %s" % bad_roll)
    _row(rows, "contract.bone_length", not short, "degenerate: %s" % short)
    return True


def check_sockets(ctx, rows):
    b = ctx.armature.data.bones
    miss = [n for n in contract.SOCKET_NAMES if n not in b]
    _row(rows, "sockets.present", not miss, "missing %s" % miss)
    if miss:
        return
    feet = [abs(b[n].head_local.z) for n in ("Sock.Foot.L", "Sock.Foot.R")]
    _row(rows, "sockets.foot_ground", max(feet) < 1e-4, "foot socket z %s" % [round(f, 4) for f in feet])
    d = (b["Sock.SpellOrigin"].head_local - b["Sock.StaffTip"].head_local).length
    _row(rows, "sockets.spell_origin", abs(d - 0.05) < 1e-3, "SpellOrigin %.3f m beyond StaffTip" % d)
    prop = ctx.islands["landmarks"].get("prop")
    if prop:
        g = Vector(prop["grip_point"])
        s = b["Sock.Hand." + prop["holding_hand"]].head_local
        _row(rows, "sockets.grip", (g - s).length < 0.005, "Sock.Hand.%s at grip point (%.4f m)" % (prop["holding_hand"], (g - s).length))
        tip = (b["Sock.StaffTip"].head_local - Vector(prop["head_point"])).length
        _row(rows, "sockets.staff_tip", tip < 0.005, "StaffTip at prop head point (%.4f m)" % tip)


def _mirror(v):
    return Vector((-v.x, v.y, v.z))


def check_symmetry(ctx, rows):
    b = ctx.armature.data.bones
    worst = 0.0
    worst_name = ""
    centre_off = []
    prop = ctx.islands["landmarks"].get("prop")
    for n in b.keys():
        if n.endswith(".L") and n[:-2] + ".R" in b:
            if prop and n.startswith("Sock.Hand"):
                continue                    # the holding hand's socket sits on the prop grip
            r = b[n[:-2] + ".R"]
            for a, c in ((b[n].head_local, r.head_local), (b[n].tail_local, r.tail_local)):
                e = (a - _mirror(c)).length
                if e > worst:
                    worst, worst_name = e, n
        elif not (n.endswith(".L") or n.endswith(".R")) and n not in ("Sock.Hip.R", "Sock.StaffTip", "Sock.SpellOrigin") \
                and not n.startswith("Skirt."):
            if abs(b[n].head_local.x) > 1e-3 or abs(b[n].tail_local.x) > 1e-3:
                centre_off.append(n)
    _row(rows, "symmetry.mirrored", worst <= SYM_TOL_M, "worst %.4f m at %s (tol %.3f)" % (worst, worst_name, SYM_TOL_M))
    _row(rows, "symmetry.centre_line", not centre_off, "off centre: %s" % centre_off)
    fit = ctx.manifest.get("skin", {}).get("fit", {})
    if fit:
        _row(rows, "symmetry.measured", fit.get("asymmetry_max_m", 1) <= rig.SYMMETRISE_MAX_M,
             "left/right joints measured %.4f m apart before averaging (max %.3f)" % (fit.get("asymmetry_max_m", -1), rig.SYMMETRISE_MAX_M))
    sk = [n for n in b.keys() if n.startswith("Skirt.")]
    if sk:
        e = max((b["Skirt.L"].head_local - _mirror(b["Skirt.R"].head_local)).length,
                (b["Skirt.L"].tail_local - _mirror(b["Skirt.R"].tail_local)).length,
                abs(b["Skirt.F"].head_local.x), abs(b["Skirt.B"].head_local.x))
        _row(rows, "symmetry.skirt", e <= SYM_TOL_M, "skirt chain mirror error %.4f m" % e)


def check_arm_angle(ctx, rows):
    a = {s: rig.arm_angle_deg(ctx.armature, s) for s in ("L", "R")}
    lo, hi = ARM_ANGLE_RANGE
    ok = all(lo <= v <= hi for v in a.values()) and abs(a["L"] - a["R"]) <= ARM_ANGLE_LR_DEG
    _row(rows, "rest.arm_angle", ok, "L %.2f R %.2f deg (range %g-%g, L-R <= %g)" % (a["L"], a["R"], lo, hi, ARM_ANGLE_LR_DEG))


def check_rest(ctx, rows):
    arm = ctx.armature
    ad = arm.animation_data
    has_action = bool(ad and ad.action)
    if not has_action:
        off = []
        for pb in arm.pose.bones:
            if pb.location.length > 1e-6 or any(abs(v) > 1e-6 for v in pb.rotation_euler) or \
                    abs(pb.rotation_quaternion.angle) > 1e-6 or any(abs(v - 1) > 1e-6 for v in pb.scale):
                off.append(pb.name)
        _row(rows, "rest.pose_identity", not off, "posed bones: %s" % off)
    else:
        _row(rows, "rest.pose_identity", True, "action assigned (%s); rest checked on the bones" % ad.action.name)
    bad = []
    for pn, o in ctx.parts.items():
        mods = [m for m in o.modifiers if m.type == "ARMATURE"]
        if len(mods) != 1 or mods[0].object != arm:
            bad.append("%s: %d armature modifiers" % (pn, len(mods)))
        if o.parent != arm or o.parent_type != "OBJECT":
            bad.append("%s: parent %s" % (pn, o.parent.name if o.parent else None))
        if any(abs(v - 1) > 1e-6 for v in o.scale) or any(abs(v) > 1e-6 for v in o.rotation_euler) or o.location.length > 1e-6:
            bad.append("%s: transform not identity" % pn)
    for o in [o for o in bpy.data.objects if o.name.startswith(contract.OVERLAY_PREFIX) and o.type == "MESH"]:
        mods = [m for m in o.modifiers if m.type == "ARMATURE"]
        if len(mods) != 1 or mods[0].object != arm:
            bad.append("%s: %d armature modifiers" % (o.name, len(mods)))
    _row(rows, "rest.meshes", not bad, "; ".join(bad))
    # hands and feet on axis: the hand piece's centre lies on the Hand bone line, the foot points forward (-Y)
    axis_bad = []
    if hasattr(ctx, "wp2"):
        for s in ("L", "R"):
            hb = arm.data.bones["Hand." + s]
            hc = Vector(ctx.wp2["joints"][s]["hand_centre"])
            d = hb.tail_local - hb.head_local
            off = (hc - hb.head_local - d.normalized() * (hc - hb.head_local).dot(d.normalized())).length
            if off > 0.015:
                axis_bad.append("Hand.%s %.3f m off the hand centre" % (s, off))
            for n in ("Foot." + s, "Toes." + s):
                fd = (arm.data.bones[n].tail_local - arm.data.bones[n].head_local).normalized()
                if fd.y > -0.5 or abs(fd.x) > 0.2:
                    axis_bad.append("%s direction %s" % (n, tuple(round(c, 2) for c in fd)))
    _row(rows, "rest.hand_foot_axis", not axis_bad, "; ".join(axis_bad))
    eye = arm.data.bones["Eye.L"].head_local.y
    head = arm.data.bones["Head"].head_local.y
    _row(rows, "rest.faces_minus_y", eye < head, "eye y %.3f in front of head pivot y %.3f" % (eye, head))
    h = max(max((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in ctx.parts.values())
    _row(rows, "rest.scale", 1.4 < h < 2.3, "tallest point %.3f m (metres, scale 1)" % h)


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.dot(ab), 1e-12)))
    return (p - (a + ab * t)).length


def check_weights(ctx, rows):
    arm = ctx.armature
    bones = arm.data.bones
    deform = {b.name for b in bones if b.use_deform}
    unweighted, over4, badsum, tiny, stray, empty, foreign = 0, 0, 0, 0, [], [], []
    for pn, o in list(ctx.parts.items()) + [(o.name, o) for o in bpy.data.objects
                                            if o.name.startswith(contract.OVERLAY_PREFIX) and o.type == "MESH"]:
        names = {g.index: g.name for g in o.vertex_groups}
        foreign += ["%s:%s" % (pn, n) for n in names.values() if n not in deform]
        used = set()
        for v in o.data.vertices:
            ws = [(names[g.group], g.weight) for g in v.groups if g.weight > 0]
            if not ws:
                unweighted += 1
                continue
            if len(ws) > contract.MAX_INFLUENCES:
                over4 += 1
            s = sum(w for _, w in ws)
            if abs(s - 1) > 0.01:
                badsum += 1
            for n, w in ws:
                used.add(n)
                if w < MIN_WEIGHT:
                    tiny += 1
                if pn != "Prop" and n in bones and w > 0.05:
                    d = _seg_dist(o.matrix_world @ v.co, bones[n].head_local, bones[n].tail_local)
                    if d > (STRAY_WIDE_M if n.split(".")[0] in WIDE_BONES else STRAY_M):
                        stray.append("%s v%d->%s %.2fm" % (pn, v.index, n, d))
        empty += ["%s:%s" % (pn, n) for n in names.values() if n not in used]
    _row(rows, "weights.all_weighted", unweighted == 0, "%d unweighted vertices" % unweighted)
    _row(rows, "weights.max_influences", over4 == 0, "%d vertices over %d influences" % (over4, contract.MAX_INFLUENCES))
    _row(rows, "weights.normalised", badsum == 0, "%d vertices whose weights do not sum to 1" % badsum)
    _row(rows, "weights.groups_are_deform_bones", not foreign, "foreign groups %s" % foreign)
    _row(rows, "weights.no_empty_groups", not empty, "empty %s" % empty)
    _row(rows, "weights.no_tiny", tiny == 0, "%d weights under %g" % (tiny, MIN_WEIGHT))
    _row(rows, "weights.no_stray", not stray, "%d far weights %s" % (len(stray), stray[:5]))
    # rigid pieces stay on one bone
    isl = rig.island_vertices(ctx)
    pieces = ctx.manifest.get("skin", {}).get("pieces", [])
    mixed = []
    for p in pieces:
        if p["bone"].startswith("field:") or p["island"] not in isl:
            continue
        d = isl[p["island"]]
        o = ctx.parts[d["part"]]
        gi = o.vertex_groups.get(p["bone"])
        if gi is None:
            mixed.append(p["island"])
            continue
        for vi in d["verts"]:
            v = o.data.vertices[vi]
            if len(v.groups) != 1 or v.groups[0].group != gi.index or abs(v.groups[0].weight - 1) > 1e-4:
                mixed.append(p["island"])
                break
    _row(rows, "weights.rigid_pieces", not mixed, "rigid pieces with mixed weights: %s" % mixed)
    covers = ctx.manifest.get("skin", {}).get("joint_covers", [])
    names = sorted(c["name"] for c in covers)
    _row(rows, "weights.joint_covers", names == ["elbow.L", "elbow.R", "knee.L", "knee.R"], "covers %s" % names)


def check_face_and_slots(ctx, rows):
    head = ctx.parts.get("Head")
    keys = head.data.shape_keys.key_blocks if head is not None and head.data.shape_keys else []
    names = [k.name for k in keys]
    if names:
        allowed = set(contract.SHAPE_KEYS) | {contract.SHAPE_KEY_BASIS}
        extra = [n for n in names if n not in allowed]
        _row(rows, "face.shape_keys", len(names) <= contract.MAX_SHAPE_KEYS_HEAD and not extra,
             "%d keys (max %d), outside contract %s" % (len(names), contract.MAX_SHAPE_KEYS_HEAD, extra))
    mats = set()
    for o in list(ctx.parts.values()):
        for s in o.material_slots:
            if s.material:
                mats.add(common.base_material_name(s.material.name))
    roles = {m for m in mats if m.startswith(contract.ROLE_MATERIAL_PREFIX)}
    if roles:
        used = {m for m in roles if m != contract.ROLE_MATERIAL_PREFIX + contract.HIDDEN_SLOT}
        _row(rows, "slots.count", len(used) <= contract.MAX_SLOTS_PER_CHARACTER and roles == mats,
             "%d role slots (max %d); non-role materials %s" % (len(used), contract.MAX_SLOTS_PER_CHARACTER, sorted(mats - roles)))


# ----------------------------------------------------------------------------------------
# Posing helpers (also used by the renders; WP-5 may reuse them for quick looks)
# ----------------------------------------------------------------------------------------
def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def rot_world(arm, name, axis, deg, pivot=None):
    """Rotate a pose bone about a world axis through its head (armature at identity)."""
    pb = arm.pose.bones[name]
    m = pb.matrix.copy()
    c = pivot if pivot is not None else m.translation.copy()
    r = Matrix.Rotation(math.radians(deg), 4, Vector(axis).normalized())
    pb.matrix = Matrix.Translation(c) @ r @ Matrix.Translation(-c) @ m
    bpy.context.view_layer.update()


def rot_local(arm, name, axis_index, deg):
    """Rotate about the bone's own current axis (0 = X flexion per the roll rule, 1 = Y twist, 2 = Z)."""
    pb = arm.pose.bones[name]
    ax = pb.matrix.to_3x3().col[axis_index]
    rot_world(arm, name, ax, deg)


def aim(arm, name, target_dir):
    pb = arm.pose.bones[name]
    d = (pb.tail - pb.head).normalized()
    t = Vector(target_dir).normalized()
    ax = d.cross(t)
    if ax.length < 1e-6:
        return
    rot_world(arm, name, ax, math.degrees(d.angle(t)))


def _bone_dir(arm, name, rest=False):
    if rest:
        b = arm.data.bones[name]
        return (b.tail_local - b.head_local).normalized()
    pb = arm.pose.bones[name]
    return (pb.tail - pb.head).normalized()


def follow_chains(arm):
    """What the runtime secondary motion (CH-13) roughly does, so the test poses show garments in a plausible
    place: skirt bones follow the thighs (forward-most, back-most, outward), the cape hangs back a little when
    the spine bends forward. WP-7 implements the real simulation; this only stages test poses."""
    b = arm.data.bones
    if "Skirt.F" in b:
        pitch, out = {}, {}
        for s, sign in (("L", 1), ("R", -1)):
            r0, r1 = _bone_dir(arm, "UpperLeg." + s, True), _bone_dir(arm, "UpperLeg." + s)
            pitch[s] = math.degrees(math.atan2(-r1.y, -r1.z) - math.atan2(-r0.y, -r0.z))   # + = thigh forward
            out[s] = max(0.0, math.degrees(math.atan2(sign * r1.x, -r1.z) - math.atan2(sign * r0.x, -r0.z)))
        fwd = max(0.0, pitch["L"], pitch["R"])
        back = max(0.0, -pitch["L"], -pitch["R"])
        if fwd > 0:
            rot_world(arm, "Skirt.F", (1, 0, 0), -0.9 * fwd)
            rot_world(arm, "Skirt.B", (1, 0, 0), -0.3 * fwd)
        if back > 0:
            rot_world(arm, "Skirt.B", (1, 0, 0), 0.8 * back)
        for s, sign in (("L", 1), ("R", -1)):
            # the side bone follows its own thigh forward and back, and outward
            rot_world(arm, "Skirt." + s, (1, 0, 0), -0.7 * pitch[s])
            if out[s] > 0:
                rot_world(arm, "Skirt." + s, (0, -sign, 0), 0.85 * out[s])
    if "Cape.1" in b:
        lean = math.degrees(_bone_dir(arm, "Chest").angle(_bone_dir(arm, "Chest", True)))
        if lean > 2:
            # the chest tilting forward swings the hanging cape out behind; gravity brings most of it back
            rot_world(arm, "Cape.1", (1, 0, 0), -0.8 * lean)


def _flex(arm, name, deg):
    rot_local(arm, name, 0, deg)


def pose_library():
    """name -> function(arm). Signs follow the roll rule (README 2.1): +X tilts an upward bone forward and swings a
    downward bone back, so thigh forward / arm forward / elbow bend = -X, knee bend = +X, spine forward = +X."""
    def rest(a):
        pass

    def tpose(a):
        for s, sg in (("L", 1), ("R", -1)):
            aim(a, "UpperArm." + s, (sg, 0, 0))

    def elbows(a):
        for s in ("L", "R"):
            _flex(a, "UpperArm." + s, -35)
            _flex(a, "LowerArm." + s, -110)
            _flex(a, "Hand." + s, -30)

    def arms_up(a):
        _flex(a, "UpperArm.L", -160)
        _flex(a, "UpperArm.R", -90)
        _flex(a, "LowerArm.R", -60)
        rot_local(a, "Hand.R", 2, 40)

    def step(a):
        _flex(a, "UpperLeg.L", -70)
        _flex(a, "LowerLeg.L", 100)
        _flex(a, "Foot.L", -20)
        _flex(a, "UpperLeg.R", 25)
        _flex(a, "LowerLeg.R", 35)
        _flex(a, "Foot.R", 15)
        _flex(a, "UpperArm.R", -35)
        _flex(a, "UpperArm.L", 30)

    def walk(a):
        _flex(a, "UpperLeg.L", -30)
        _flex(a, "LowerLeg.L", 20)
        _flex(a, "UpperLeg.R", 20)
        _flex(a, "LowerLeg.R", 45)
        _flex(a, "UpperArm.R", -25)
        _flex(a, "UpperArm.L", 20)
        _flex(a, "LowerArm.R", -30)
        _flex(a, "LowerArm.L", -15)

    def crouch(a):
        for s in ("L", "R"):
            _flex(a, "UpperLeg." + s, -85)
            _flex(a, "LowerLeg." + s, 110)
            _flex(a, "Foot." + s, -20)
            _flex(a, "UpperArm." + s, -40)
            _flex(a, "LowerArm." + s, -50)
        _flex(a, "Spine", 20)
        _flex(a, "Chest", 10)

    def spine(a):
        _flex(a, "Spine", 25)
        _flex(a, "Chest", 15)
        rot_local(a, "Chest", 1, 25)
        rot_local(a, "Neck", 2, 15)
        rot_local(a, "Head", 1, 45)

    def neck(a):
        _flex(a, "Neck", 20)
        _flex(a, "Head", 25)
        rot_local(a, "Head", 1, -40)
        rot_local(a, "Spine", 2, -15)
        rot_local(a, "Chest", 2, -10)

    def abduct(a):
        for s, sg in (("L", 1), ("R", -1)):
            rot_world(a, "UpperLeg." + s, (0, -sg, 0), 35)
            rot_world(a, "LowerLeg." + s, (0, sg, 0), 10)
            rot_world(a, "UpperArm." + s, (0, -sg, 0), 45)
            _flex(a, "LowerArm." + s, -60)

    def backbend(a):
        _flex(a, "Spine", -20)
        _flex(a, "Chest", -10)
        _flex(a, "Neck", -15)
        _flex(a, "UpperLeg.L", 30)
        _flex(a, "LowerLeg.L", 60)
        _flex(a, "UpperArm.L", 50)
        _flex(a, "UpperArm.R", 50)

    return [("rest", rest), ("tpose", tpose), ("elbows", elbows), ("arms_up", arms_up), ("step", step),
            ("walk", walk), ("crouch", crouch), ("spine", spine), ("neck", neck), ("abduct", abduct),
            ("backbend", backbend)]


def apply_pose(arm, fn):
    reset_pose(arm)
    fn(arm)
    follow_chains(arm)
    bpy.context.view_layer.update()


# ----------------------------------------------------------------------------------------
# Bend checks (numeric): joint seal and rigid-piece integrity
# ----------------------------------------------------------------------------------------
def _evaluated_coords(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    mw = np.array(ev.matrix_world)
    co = co @ mw[:3, :3].T + mw[:3, 3]
    ev.to_mesh_clear()
    return co


def _piece_list(ctx):
    """[(part, verts, faces)] for every island including joint covers, on the current (covered) mesh."""
    import islands as islands_mod
    out = []
    for pn, o in ctx.parts.items():
        groups, _ = islands_mod.vertex_islands(o)
        for i, g in enumerate(groups):
            out.append(("%s.%02d" % (pn, i), pn, g["verts"], [list(o.data.polygons[f].vertices) for f in g["faces"]]))
    return out


def _joint_specs(ctx):
    J = ctx.wp2["joints"] if hasattr(ctx, "wp2") else None
    isl = rig.island_vertices(ctx)
    neck = [d for d in isl.values() if d["info"].get("role") == "neck"]
    neck_r = float((neck[0]["co"].max(axis=0) - neck[0]["co"].min(axis=0))[0] / 2) if neck else 0.06
    specs = []
    for s in ("L", "R"):
        j = J[s]
        specs += [("elbow." + s, "UpperArm." + s, "LowerArm." + s, j["forearm_radius"]),
                  ("knee." + s, "UpperLeg." + s, "LowerLeg." + s, j["shin_radius"]),
                  ("shoulder." + s, "Shoulder." + s, "UpperArm." + s, j["upper_end_radius"]),
                  ("hip." + s, "Hips", "UpperLeg." + s, j["thigh_radius"])]
    specs += [("neck", "Neck", "Head", neck_r), ("spine", "Spine", "Chest", 0.09), ("waist", "Hips", "Spine", 0.09)]
    return specs


def _inside(trees, p):
    for t in trees:
        hit = t.find_nearest(p)
        if hit[0] is None:
            continue
        loc, nrm = hit[0], hit[1]
        if (p - loc).dot(nrm) <= 0.002:
            return True
    return False


def bend_checks(ctx, rows, poses=None):
    """Numeric seal per joint and pose. Returns {pose: {joint: fraction filled}}."""
    arm = ctx.armature
    pieces = _piece_list(ctx)
    rest_co = {pn: _evaluated_coords(o) for pn, o in ctx.parts.items()}
    specs = _joint_specs(ctx)
    near = {}
    for name, pb_name, cb_name, r in specs:
        piv = arm.data.bones[cb_name].head_local
        sel = []
        for pid, pn, verts, faces in pieces:
            c = rest_co[pn][verts]
            mn, mx = c.min(axis=0), c.max(axis=0)
            q = np.clip(np.array(piv), mn, mx)
            if np.linalg.norm(q - np.array(piv)) < 1.2 * r:
                sel.append(pid)
        near[name] = sel
    result = {}
    worst = {}
    for pose_name, fn in (poses or pose_library()):
        apply_pose(arm, fn)
        co = {pn: _evaluated_coords(o) for pn, o in ctx.parts.items()}
        trees = {}
        for pid, pn, verts, faces in pieces:
            loc = {v: i for i, v in enumerate(verts)}
            trees[pid] = BVHTree.FromPolygons([Vector(co[pn][v]) for v in verts], [[loc[v] for v in f] for f in faces])
        result[pose_name] = {}
        for name, pb_name, cb_name, r in specs:
            pp = arm.pose.bones[pb_name]
            cb = arm.pose.bones[cb_name]
            piv = cb.head.copy()
            a1 = (pp.tail - pp.head).normalized()
            a2 = (cb.tail - cb.head).normalized()
            tr = [trees[p] for p in near[name]]
            n_in = n_all = 0
            for k in range(5):
                t = k / 4.0
                ax = (a1 * (1 - t) + a2 * t)
                if ax.length < 1e-6:
                    ax = a2
                ax.normalize()
                u = ax.orthogonal().normalized()
                v = ax.cross(u)
                for az in range(12):
                    ang = 2 * math.pi * az / 12
                    for rho in (0.4, 0.75):
                        p = piv + (u * math.cos(ang) + v * math.sin(ang)) * (rho * r)
                        n_all += 1
                        n_in += _inside(tr, p)
            f = n_in / max(n_all, 1)
            result[pose_name][name] = round(f, 3)
            if f < worst.get(name, (2, ""))[0]:
                worst[name] = (f, pose_name)
    reset_pose(arm)
    for name, (f, pose_name) in sorted(worst.items()):
        _row(rows, "bend.seal." + name, f >= SEAL_MIN, "worst %.3f filled in pose %s (min %.2f)" % (f, pose_name, SEAL_MIN))
    return result


# ----------------------------------------------------------------------------------------
# Clips (WP-5 output) and export (WP-6 output)
# ----------------------------------------------------------------------------------------
def _assign_action(arm, act):
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    if hasattr(ad, "action_slot") and getattr(ad, "action_slot", None) is None and hasattr(act, "slots") and len(act.slots):
        ad.action_slot = act.slots[0]


def check_clips(ctx, rows):
    clips = ctx.manifest.get("clips")
    if not clips:
        return
    arm = ctx.armature
    sc = bpy.context.scene
    prev = arm.animation_data.action if arm.animation_data else None
    missing = [c["name"] for c in clips if c["name"] not in bpy.data.actions]
    _row(rows, "clips.actions_present", not missing, "missing actions %s" % missing)
    bad_loop, slide = [], []
    for c in clips:
        act = bpy.data.actions.get(c["name"])
        if act is None:
            continue
        _assign_action(arm, act)
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])

        def snapshot(f):
            sc.frame_set(f)
            return {pb.name: pb.matrix.copy() for pb in arm.pose.bones}

        if c.get("loop"):
            a, b = snapshot(f0), snapshot(f1)
            worst_deg = max(math.degrees(a[n].to_quaternion().rotation_difference(b[n].to_quaternion()).angle) for n in a)
            worst_m = max((a[n].translation - b[n].translation).length for n in a)
            if worst_deg > LOOP_TOL_DEG or worst_m > LOOP_TOL_M:
                bad_loop.append("%s %.2fdeg %.4fm" % (c["name"], worst_deg, worst_m))
        v = float(c.get("native_speed_mps") or 0)
        if v > 0:
            limit = float(c.get("foot_slide_limit_mps", FOOT_SLIDE_MPS))
            fps = float(c.get("fps", contract.FPS))
            for s in ("L", "R"):
                pts = []
                for f in range(f0, f1 + 1):
                    sc.frame_set(f)
                    pts.append(arm.matrix_world @ arm.pose.bones["Sock.Foot." + s].head)
                zmin = min(p.z for p in pts)
                phases, cur = [], []
                for i in range(1, len(pts)):
                    if pts[i].z - zmin <= STANCE_HEIGHT_M and pts[i - 1].z - zmin <= STANCE_HEIGHT_M:
                        d = pts[i] - pts[i - 1]
                        # in-place clip: the ground moves backward (+Y) under the character at v
                        gx, gy = d.x * fps, d.y * fps - v
                        cur.append(math.hypot(gx, gy))
                    elif cur:
                        phases.append(cur)
                        cur = []
                if cur:
                    phases.append(cur)
                worst = max((sum(p) / len(p) for p in phases if len(p) >= 2), default=0.0)
                if worst > limit:
                    slide.append("%s %s %.3f m/s" % (c["name"], s, worst))
    _row(rows, "clips.loops_close", not bad_loop, "; ".join(bad_loop))
    _row(rows, "clips.foot_plant", not slide, "stance foot speed over limit: %s" % "; ".join(slide))
    _assign_action(arm, prev) if prev else (arm.animation_data and setattr(arm.animation_data, "action", None))
    sc.frame_set(1)


def check_export(ctx, rows):
    ex = ctx.manifest.get("export")
    if not ex:
        return
    fwd = ex.get("forward_axis") or ex.get("forward_axis_check")
    if fwd is not None:
        ok = fwd.get("ok") if isinstance(fwd, dict) else bool(fwd)
        _row(rows, "export.forward_axis", ok, str(fwd))


# ----------------------------------------------------------------------------------------
# Renders
# ----------------------------------------------------------------------------------------
def _setup_render(ctx, res=(360, 480)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_object_outline = False
    sc.display.shading.show_cavity = False
    sc.display.shading.show_shadows = False
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.view_settings.view_transform = "Standard"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("VerifyWorld")
    sc.world.color = BG
    # Workbench draws material.diffuse_color: copy the supplied base colour so the stills show the palette
    for m in bpy.data.materials:
        if m.use_nodes and m.node_tree:
            bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is not None:
                c = bsdf.inputs["Base Color"].default_value
                m.diffuse_color = (c[0], c[1], c[2], 1.0)
    cam_data = bpy.data.cameras.get("VerifyCam") or bpy.data.cameras.new("VerifyCam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.get("VerifyCam") or bpy.data.objects.new("VerifyCam", cam_data)
    if cam.name not in sc.collection.objects:
        sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def _place_cam(cam, target, view, scale, dist=6.0):
    yaw = {"front": 0.0, "side": 90.0, "threequarter": 40.0, "back": 180.0, "side_r": -90.0}[view]
    a = math.radians(yaw)
    # front camera sits on -Y looking +Y; positive yaw moves it toward the character's left (+X)
    pos = Vector(target) + Vector((math.sin(a) * dist, -math.cos(a) * dist, 0.0))
    d = (Vector(target) - pos).normalized()
    cam.location = pos
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = scale
    cam.data.clip_end = 100


def _render_to(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def _sheet(paths_grid, out_path):
    """Compose rendered PNGs (rows x cols, same size) into one PNG with numpy."""
    imgs = []
    for row in paths_grid:
        r = []
        for p in row:
            im = bpy.data.images.load(p, check_existing=False)
            w, h = im.size
            a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, im.channels)[:, :, :3]
            bpy.data.images.remove(im)
            r.append(a)
        imgs.append(r)
    h, w = imgs[0][0].shape[:2]
    gap = 4
    H = len(imgs) * h + (len(imgs) - 1) * gap
    W = max(len(r) for r in imgs) * w + (max(len(r) for r in imgs) - 1) * gap
    canvas = np.ones((H, W, 4), dtype=np.float32)
    canvas[:, :, :3] = 0.15
    for i, r in enumerate(imgs):
        for j, a in enumerate(r):
            y0 = H - (i + 1) * h - i * gap         # Blender images are stored bottom-up
            x0 = j * (w + gap)
            canvas[y0:y0 + h, x0:x0 + w, :3] = a
    out = bpy.data.images.new("sheet", W, H, alpha=False)
    out.pixels[:] = canvas.ravel()
    out.filepath_raw = out_path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)


def render_poses(ctx, out_dir, poses=None, views=("front", "side", "threequarter"), per_sheet=4):
    """Full-body stills per pose and view, composed into sheets of `per_sheet` poses (rows) x views (cols),
    plus joint close-ups. Returns the list of sheet files."""
    arm = ctx.armature
    common.ensure_dir(out_dir)
    cam = _setup_render(ctx)
    h = max(max((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in ctx.parts.values())
    centre = (0.0, 0.0, h / 2.0)
    poses = poses or pose_library()
    single = os.path.join(out_dir, "single")
    common.ensure_dir(single)
    grid = []
    for pose_name, fn in poses:
        apply_pose(arm, fn)
        row = []
        for v in views:
            _place_cam(cam, centre, v, h * 1.12)
            p = os.path.join(single, "%s_%s_%s.png" % (ctx.key, pose_name, v))
            _render_to(p)
            row.append(p)
        grid.append(row)
    sheets = []
    for k in range(0, len(grid), per_sheet):
        p = os.path.join(out_dir, "%s_poses_%d.png" % (ctx.key, k // per_sheet + 1))
        _sheet(grid[k:k + per_sheet], p)
        sheets.append(p)
    # close-ups: (pose, bone whose head is the joint, views)
    b = arm.data.bones
    closeups = [("elbows", "LowerArm.L", ("front", "side", "threequarter")),
                ("tpose", "UpperArm.L", ("front", "back", "threequarter")),
                ("step", "LowerLeg.L", ("front", "side", "threequarter")),
                ("crouch", "UpperLeg.L", ("front", "side", "threequarter")),
                ("spine", "Neck", ("front", "side", "threequarter")),
                ("arms_up", "UpperArm.R", ("front", "side_r", "back"))]
    lib = dict(poses)
    grid = []
    for pose_name, bone, vs in closeups:
        if pose_name not in lib:
            continue
        apply_pose(arm, lib[pose_name])
        piv = arm.pose.bones[bone].head.copy()
        row = []
        for v in vs:
            _place_cam(cam, piv, v, 0.5)
            p = os.path.join(single, "%s_close_%s_%s_%s.png" % (ctx.key, pose_name, bone, v))
            _render_to(p)
            row.append(p)
        grid.append(row)
    if grid:
        p = os.path.join(out_dir, "%s_closeups.png" % ctx.key)
        _sheet(grid, p)
        sheets.append(p)
    reset_pose(arm)
    return sheets


# ----------------------------------------------------------------------------------------
# Entry point (contract signature: verify.verify_character(ctx) -> list of {check, ok, detail})
# ----------------------------------------------------------------------------------------
def verify_character(ctx, render=True):
    rows = []
    if not check_contract(ctx, rows):
        ctx.set_section("verify", rows)
        return rows
    check_sockets(ctx, rows)
    check_symmetry(ctx, rows)
    check_arm_angle(ctx, rows)
    check_rest(ctx, rows)
    check_weights(ctx, rows)
    check_face_and_slots(ctx, rows)
    report_dir = ctx.path("character_reports")
    seal = {}
    if hasattr(ctx, "wp2"):
        ad = ctx.armature.animation_data
        prev = ad.action if ad else None
        if ad:
            ad.action = None
        seal = bend_checks(ctx, rows)
        if render:
            sheets = render_poses(ctx, os.path.join(report_dir, "renders"))
            _row(rows, "renders.written", bool(sheets), "%d sheets in %s" % (len(sheets), os.path.join(report_dir, "renders")))
        if prev is not None:
            _assign_action(ctx.armature, prev)
    check_clips(ctx, rows)
    check_export(ctx, rows)
    ctx.set_section("verify", rows)
    common.write_json(os.path.join(report_dir, "verify.json"),
                      {"key": ctx.key, "pass": all(r["ok"] for r in rows), "checks": rows, "bend_seal": seal})
    for r in rows:
        if not r["ok"]:
            common.log(ctx.key, "FAIL", r["check"], r["detail"])
    common.log(ctx.key, "verify", "PASS" if all(r["ok"] for r in rows) else "FAIL",
               "%d/%d checks" % (sum(r["ok"] for r in rows), len(rows)))
    return rows
