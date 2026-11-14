"""WP-2: template armature fitted per character (PRD_CHARACTERS 5.2 step 2, 5.3, 5.3a).

`fit_rig(ctx)` builds the production armature for one loaded human (see common.load_character):

- every contract bone (HUMAN_CORE_BONES + FACE_BONES + sockets) with the contract parents, plus the optional
  cape / hood / skirt chains where cast/index.json says the character has them;
- joints are measured from the vertices of the supplied pieces (not only from the island-map boxes): the
  shoulder pivot is the shoulder-cap centre, the elbow and knee are the mid-points between the end rings of the
  upper and lower pieces, the wrist is the forearm's far ring, and so on;
- left and right are measured separately, the difference is recorded in the manifest (`bones_fit.asymmetry_m`)
  and, when it is under SYMMETRISE_MAX_M, the two sides are averaged so the fit is exactly mirrored;
- roll rule (README 2.1): local +X as close as possible to world +X, local Y head to tail, Z = X cross Y;
- rest pose = the supplied A-pose, armature object at the origin with identity transform, child of the root empty.

Writes manifest sections `bones` and `sockets`, and `ctx.wp2` (measured joints and the piece classification that
skin.py uses). Per-character overrides, if ever needed, go in cast/rig/<key>.json (WP-2 owned):
  {"bones": {"<Bone>": {"head": [x,y,z], "tail": [x,y,z]}}, "islands": {"<island id>": "<Bone or field name>"}}
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import islands as islands_mod  # noqa: E402

SYMMETRISE_MAX_M = 0.005      # sides measured further apart than this are left as measured (verify then fails)
BAND = 0.18                   # fraction of a piece's length used as its "end ring"
NOT_MIRRORED = ("foot_min", "foot_max")   # box corners: min of one side mirrors to max of the other
RIG_OVERRIDES_DIR = os.path.join(common.CAST_DIR, "rig")


# ----------------------------------------------------------------------------------------
# Island access
# ----------------------------------------------------------------------------------------
def island_vertices(ctx):
    """{island_id: {"part", "verts": [int], "co": (n,3) world coords, "info": island-map entry}} for every part.
    Cached on ctx (computed on the unmodified supplied meshes, before skin.py adds joint covers)."""
    cache = getattr(ctx, "_wp2_islands", None)
    if cache is not None:
        return cache
    out = {}
    imap = ctx.islands
    for pn, obj in ctx.parts.items():
        groups, co = islands_mod.vertex_islands(obj)
        infos = imap["parts"][pn]["islands"] if imap and pn in imap["parts"] else []
        for i, g in enumerate(groups):
            iid = "%s.%02d" % (pn, i)
            info = infos[i] if i < len(infos) else {"id": iid, "role": None, "bone": None, "side": "C"}
            if info.get("id") != iid:
                raise RuntimeError("%s: island map order differs from the mesh (%s vs %s)" % (ctx.key, info.get("id"), iid))
            out[iid] = {"part": pn, "verts": g["verts"], "co": co[g["verts"]], "info": info}
    ctx._wp2_islands = out
    return out


def chains_for(key):
    idx = common.read_json(os.path.join(common.CAST_DIR, "index.json"))["characters"][key]
    return idx["optional_chains"]


def load_overrides(key):
    p = os.path.join(RIG_OVERRIDES_DIR, key + ".json")
    return common.read_json(p) if os.path.isfile(p) else {}


def _side_of(info):
    return info.get("side", "C")


def classify(ctx):
    """Group the body pieces into the roles the fit and the skin need. Returns a dict:
    arm[s] = {cap, upper: [ids], upper_main, lower: [ids], lower_main, cuff: [ids], hand: [ids]}
    leg[s] = {thigh: [ids], shin: [ids] (top first), shin_main, foot: [ids]}
    """
    isl = island_vertices(ctx)
    arm = {}
    leg = {}
    for s in ("L", "R"):
        side = [i for i, d in isl.items() if _side_of(d["info"]) == s and d["part"] in ("Body", "Legs")]
        role = lambda r: [i for i in side if isl[i]["info"].get("role") == r]  # noqa: E731
        caps = role("shoulder_cap")
        uppers = role("upper_arm_piece")
        if not caps:
            # the champion's cap is labelled upper_arm_piece: the round piece (most verts) whose centre is highest
            cands = sorted(uppers, key=lambda i: (-len(isl[i]["verts"]), -isl[i]["info"]["centre"][2]))
            cands = [i for i in cands if len(isl[i]["verts"]) >= 30]
            if cands:
                caps = [cands[0]]
        cap = caps[0] if caps else None
        uppers = [i for i in uppers if i != cap]
        # the main sleeve: the upper piece with the longest vertical extent
        upper_main = max(uppers, key=lambda i: isl[i]["info"]["size"][2]) if uppers else None
        lowers = role("lower_arm_piece")
        lower_main = max(lowers, key=lambda i: (isl[i]["info"]["size"][2], len(isl[i]["verts"]) == 16)) if lowers else None
        # prefer the skin/sleeve forearm (16 verts) over a gauntlet when both are long
        sixteen = [i for i in lowers if len(isl[i]["verts"]) == 16]
        if sixteen:
            lower_main = max(sixteen, key=lambda i: isl[i]["info"]["size"][2])
        arm[s] = {"cap": cap, "upper": uppers, "upper_main": upper_main, "lower": lowers, "lower_main": lower_main,
                  "cuff": role("cuff"), "hand": role("hand") + role("thumb"), "hand_main": (role("hand") or [None])[0]}
        thighs = role("thigh")
        shins = sorted(role("shin_or_boot"), key=lambda i: -isl[i]["info"]["centre"][2])
        leg[s] = {"thigh": thighs, "thigh_main": thighs[0] if thighs else None, "shin": shins,
                  "shin_main": shins[0] if shins else None, "boot_top": role("cuff_or_boot_top"), "foot": role("foot")}
    return {"arm": arm, "leg": leg}


# ----------------------------------------------------------------------------------------
# Small geometry helpers (numpy on world coordinates)
# ----------------------------------------------------------------------------------------
def _ring(co, axis, end, band=BAND):
    """Centre and mean radius of the vertices in the end band of `co` along unit `axis` ('min' or 'max')."""
    t = co @ axis
    lo, hi = t.min(), t.max()
    span = max(hi - lo, 1e-6)
    sel = co[t <= lo + band * span] if end == "min" else co[t >= hi - band * span]
    c = sel.mean(axis=0)
    d = sel - c
    d = d - np.outer(d @ axis, axis)
    rad = float(np.sqrt((d ** 2).sum(axis=1)).mean()) if len(sel) else 0.0
    return c, rad


def _bbox_centre(co):
    return (co.min(axis=0) + co.max(axis=0)) / 2.0


def _V(a):
    return Vector((float(a[0]), float(a[1]), float(a[2])))


def _roll_axis(y):
    zt = Vector((1.0, 0.0, 0.0)).cross(y)
    return zt if zt.length > 1e-6 else Vector((0.0, 0.0, 1.0))


# ----------------------------------------------------------------------------------------
# Measuring
# ----------------------------------------------------------------------------------------
def measure(ctx):
    """Measure joints from the pieces. Returns (joints, cls) where joints holds world points per side."""
    isl = island_vertices(ctx)
    cls = classify(ctx)
    lm = ctx.islands["landmarks"]
    J = {"L": {}, "R": {}}
    for s in ("L", "R"):
        a = cls["arm"][s]
        cap = isl[a["cap"]]["co"]
        sh = _bbox_centre(cap)
        hand = isl[a["hand_main"]]["co"]
        hc = _bbox_centre(hand)
        d = hc - sh
        d = d / np.linalg.norm(d)
        up = isl[a["upper_main"]]["co"]
        lo = isl[a["lower_main"]]["co"]
        # upper piece: far end along d is the elbow side
        u_end, u_rad = _ring(up, d, "max")
        l_start, l_rad = _ring(lo, d, "min")
        elbow = (u_end + l_start) / 2.0
        d2 = hc - elbow
        d2 = d2 / np.linalg.norm(d2)
        wrist, w_rad = _ring(lo, d2, "max")
        tip, _ = _ring(hand, d2, "max", band=0.12)
        # keep the hand bone on the line through the hand's centre
        hand_dir = (hc - wrist)
        hand_dir = hand_dir / np.linalg.norm(hand_dir)
        tip = wrist + hand_dir * float((tip - wrist) @ hand_dir)
        J[s].update({"shoulder": sh, "elbow": elbow, "wrist": wrist, "hand_tip": tip, "hand_centre": hc,
                     "upper_end_radius": u_rad, "forearm_radius": l_rad, "wrist_radius": w_rad,
                     "cap_radius": float(np.abs(cap - sh).max(axis=0)[[0, 1, 2]].mean())})
        g = cls["leg"][s]
        th = isl[g["thigh_main"]]["co"]
        sn = isl[g["shin_main"]]["co"]
        z = np.array([0.0, 0.0, 1.0])
        t_top, t_rad_top = _ring(th, z, "max")
        t_bot, t_rad = _ring(th, z, "min")
        s_top, s_rad = _ring(sn, z, "max")
        knee = (t_bot + s_top) / 2.0
        hip = t_top.copy()
        hip[2] -= 0.5 * t_rad_top            # pivot half a thigh radius below the thigh top (stays inside the waist)
        lowest = isl[g["shin"][-1]]["co"]
        ankle = _bbox_centre(lowest)
        ankle[2] = lm["ankle_z"][s]
        foot = isl[g["foot"][0]]["co"]
        fmin, fmax = foot.min(axis=0), foot.max(axis=0)
        fc = _bbox_centre(foot)
        toe_base = np.array([fc[0], fc[1] - 0.02, fc[2]])
        toe_tip = np.array([fc[0], fmin[1], fc[2]])
        J[s].update({"hip": hip, "knee": knee, "ankle": ankle, "toe_base": toe_base, "toe_tip": toe_tip,
                     "foot_centre": fc, "foot_min": fmin, "foot_max": fmax, "thigh_radius": t_rad, "shin_radius": s_rad})
    return J, cls


def _mirror(p):
    return np.array([-p[0], p[1], p[2]])


def symmetrise(J):
    """Average left and mirrored right. Returns the max asymmetry found (metres) per joint."""
    asym = {}
    for k, vl in J["L"].items():
        vr = J["R"][k]
        if k in NOT_MIRRORED:
            continue
        if isinstance(vl, np.ndarray) and vl.shape == (3,):
            diff = float(np.linalg.norm(vl - _mirror(vr)))
            asym[k] = round(diff, 4)
    worst = max(asym.values()) if asym else 0.0
    if worst <= SYMMETRISE_MAX_M:
        for k, vl in list(J["L"].items()):
            vr = J["R"][k]
            if k in NOT_MIRRORED:
                continue
            if isinstance(vl, np.ndarray) and vl.shape == (3,):
                m = (vl + _mirror(vr)) / 2.0
                J["L"][k] = m
                J["R"][k] = _mirror(m)
            elif isinstance(vl, float):
                m = (vl + vr) / 2.0
                J["L"][k] = J["R"][k] = m
    return asym, worst


# ----------------------------------------------------------------------------------------
# Layout
# ----------------------------------------------------------------------------------------
def bone_layout(ctx, J, cls):
    """{bone: (head Vector, tail Vector)} for every bone this character gets (world, metres)."""
    imap = ctx.islands
    lm = imap["landmarks"]
    isl = island_vertices(ctx)
    torso = [d for d in isl.values() if d["info"].get("role") == "torso"]
    yc = float(_bbox_centre(torso[0]["co"])[1]) if torso else 0.0
    neck = [d for d in isl.values() if d["info"].get("role") == "neck"]
    hip_z = lm["hip_z"]
    neck_bottom = float(neck[0]["co"][:, 2].min()) if neck else lm["neck_z"] - 0.06
    head_bottom, crown = lm["head_bottom_z"], lm["crown_z"]
    hc = lm["head_centre"]
    L = {}
    L["Hips"] = (Vector((0, yc, hip_z - 0.06)), Vector((0, yc, hip_z + 0.10)))
    spine_top = hip_z + 0.10 + 0.45 * (neck_bottom - hip_z - 0.10)
    L["Spine"] = (L["Hips"][1].copy(), Vector((0, yc, spine_top)))
    L["Chest"] = (L["Spine"][1].copy(), Vector((0, yc, neck_bottom)))
    neck_y = float(_bbox_centre(neck[0]["co"])[1]) if neck else yc
    L["Neck"] = (Vector((0, neck_y, neck_bottom)), Vector((0, (neck_y + hc[1]) / 2.0, head_bottom)))
    L["Head"] = (L["Neck"][1].copy(), Vector((0, hc[1], crown)))
    face = imap["face"]
    mouth = face.get("mouth")
    ears = [isl[e]["info"] for e in face.get("ears", []) if e in isl]
    ear_y = sum(e["centre"][1] for e in ears) / len(ears) if ears else hc[1]
    if mouth:
        mz = mouth["centre"][2]
        L["Jaw"] = (Vector((0, ear_y, mz + 0.04)), Vector((0, mouth["centre"][1], mz - 0.02)))
    else:
        L["Jaw"] = (Vector((0, ear_y, head_bottom + 0.06)), Vector((0, hc[1] - 0.1, head_bottom + 0.02)))
    back = face.get("pivot_offset_back_m", 0.025)
    for s in ("L", "R"):
        e = face["sides"][s]["centre"]
        c = Vector(e) + Vector((0, back, 0))
        L["Eye." + s] = (c, c + Vector((0, -0.06, 0)))
    for s in ("L", "R"):
        j = J[s]
        sign = 1.0 if s == "L" else -1.0
        sh = _V(j["shoulder"])
        L["Shoulder." + s] = (Vector((sign * 0.05, yc, sh.z)), sh)
        L["UpperArm." + s] = (sh, _V(j["elbow"]))
        L["LowerArm." + s] = (_V(j["elbow"]), _V(j["wrist"]))
        L["Hand." + s] = (_V(j["wrist"]), _V(j["hand_tip"]))
        L["UpperLeg." + s] = (_V(j["hip"]), _V(j["knee"]))
        L["LowerLeg." + s] = (_V(j["knee"]), _V(j["ankle"]))
        L["Foot." + s] = (_V(j["ankle"]), _V(j["toe_base"]))
        L["Toes." + s] = (_V(j["toe_base"]), _V(j["toe_tip"]))
        fc = j["foot_centre"]
        L["Sock.Foot." + s] = (Vector((fc[0], fc[1], 0.0)), Vector((fc[0], fc[1], 0.05)))
        hcn = _V(j["hand_centre"])
        hd = (L["Hand." + s][1] - L["Hand." + s][0]).normalized()
        L["Sock.Hand." + s] = (hcn, hcn + hd * 0.05)
    prop = lm.get("prop")
    placeholder = prop is None
    if prop:
        g = Vector(prop["grip_point"])
        head_pt = Vector(prop["head_point"])
        ax = (head_pt - g).normalized()
        L["Sock.Hand." + prop["holding_hand"]] = (g, g + ax * 0.05)
        L["Sock.StaffTip"] = (head_pt, head_pt + ax * 0.05)
        L["Sock.SpellOrigin"] = (head_pt + ax * 0.05, head_pt + ax * 0.10)
    else:
        base = L["Sock.Hand.R"][0]
        head_pt = base + Vector((0, 0, 0.45))
        L["Sock.StaffTip"] = (head_pt, head_pt + Vector((0, 0, 0.05)))
        L["Sock.SpellOrigin"] = (head_pt + Vector((0, 0, 0.05)), head_pt + Vector((0, 0, 0.10)))
    torso_max_y = float(torso[0]["co"][:, 1].max()) if torso else yc + 0.12
    back_z = spine_top + 0.5 * (neck_bottom - spine_top)
    L["Sock.Back"] = (Vector((0, yc + 0.14, back_z)), Vector((0, yc + 0.19, back_z)))
    L["Sock.Hip.R"] = (Vector((-0.21, yc, hip_z - 0.04)), Vector((-0.26, yc, hip_z - 0.04)))
    L["Sock.HeadTop"] = (Vector((0, hc[1], crown)), Vector((0, hc[1], crown + 0.05)))

    # optional chains
    ch = chains_for(ctx.key)
    if ch.get("cape"):
        backs = [d for d in isl.values() if d["info"].get("role") == "cloth_back"]
        co = np.vstack([d["co"] for d in backs])
        top, bot = float(co[:, 2].max()), float(co[:, 2].min())
        y = float(co[:, 1].mean())
        n = len(contract.CAPE_BONES)
        for k, (name, _p) in enumerate(contract.CAPE_BONES):
            z0 = top - (top - bot) * k / n
            z1 = top - (top - bot) * (k + 1) / n
            L[name] = (Vector((0, y, z0)), Vector((0, y, z1)))
    if ch.get("hood"):
        hoods = [d for d in isl.values() if d["info"].get("role") == "hood"]
        co = np.vstack([d["co"] for d in hoods])
        mn, mx = co.min(axis=0), co.max(axis=0)
        # Hood.1: from the crown down the back of the hood; Hood.2: the lower back part (the drape)
        top = Vector((0, hc[1] + 0.02, float(mx[2]) - 0.03))
        mid = Vector((0, float(mx[1]) - 0.05, float((mn[2] + mx[2]) / 2)))
        low = Vector((0, float(mx[1]) - 0.02, float(mn[2]) + 0.01))
        L["Hood.1"] = (top, mid)
        L["Hood.2"] = (mid.copy(), low)
    if ch.get("skirt"):
        robes = [d for d in isl.values() if d["info"].get("role") in ("robe_or_skirt", "skirt_hem")]
        co = np.vstack([d["co"] for d in robes])
        bottom = float(co[:, 2].min())
        mn, mx = co.min(axis=0), co.max(axis=0)
        cy = float((mn[1] + mx[1]) / 2)
        rx, ry = float(mx[0] - mn[0]) / 2, float(mx[1] - mn[1]) / 2
        top_z = hip_z
        # each skirt bone hangs from the hip ring down the middle of its quarter of the robe
        for name, (dx, dy) in {"Skirt.F": (0.0, -1.0), "Skirt.B": (0.0, 1.0), "Skirt.L": (1.0, 0.0), "Skirt.R": (-1.0, 0.0)}.items():
            hx, hy = dx * rx * 0.55, cy + dy * ry * 0.55
            tx, ty = dx * rx * 0.9, cy + dy * ry * 0.9
            L[name] = (Vector((hx, hy, top_z)), Vector((tx, ty, bottom)))
    # overrides
    ov = load_overrides(ctx.key).get("bones", {})
    for name, d in ov.items():
        if name in L:
            h, t = L[name]
            L[name] = (Vector(d.get("head", h)), Vector(d.get("tail", t)))
    return L, placeholder


def contract_parents(ctx):
    parents = {n: p for n, p in contract.HUMAN_CORE_BONES + contract.FACE_BONES + contract.CAPE_BONES
               + contract.HOOD_BONES + contract.SKIRT_BONES + contract.SOCKETS}
    prop = ctx.islands["landmarks"].get("prop")
    if prop:
        parents["Sock.StaffTip"] = "Sock.Hand." + prop["holding_hand"]
    return parents


def expected_bones(ctx):
    ch = chains_for(ctx.key)
    return contract.all_human_bone_names(skirt=ch["skirt"], cape=ch["cape"], hood=ch["hood"])


# ----------------------------------------------------------------------------------------
# Build
# ----------------------------------------------------------------------------------------
def _remove_existing():
    for o in list(bpy.data.objects):
        if o.type == "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.armatures):
        bpy.data.armatures.remove(a)


def fit_rig(ctx):
    """Build the fitted armature. Sets ctx.armature and the manifest sections `bones` and `sockets`."""
    if ctx.info["rig"] != "humanoid":
        raise RuntimeError("%s is not a humanoid (demons are WP-13)" % ctx.key)
    if ctx.islands is None:
        raise RuntimeError("no island map for %s" % ctx.key)
    if not islands_mod.check_map_matches(ctx.islands, ctx):
        raise RuntimeError("%s: the committed island map does not match the imported mesh" % ctx.key)
    # objects must be clean: scale 1, rotation 0 (checked in the PRD); fail loudly otherwise
    for o in [ctx.root] + list(ctx.parts.values()):
        if any(abs(v - 1) > 1e-6 for v in o.scale) or any(abs(v) > 1e-6 for v in o.rotation_euler):
            raise RuntimeError("%s: object %s is not at scale 1 / rotation 0" % (ctx.key, o.name))
    J, cls = measure(ctx)
    asym, worst = symmetrise(J)
    layout, placeholder = bone_layout(ctx, J, cls)
    names = expected_bones(ctx)
    missing = [n for n in names if n not in layout]
    if missing:
        raise RuntimeError("%s: no layout for %s" % (ctx.key, missing))
    parents = contract_parents(ctx)

    _remove_existing()
    arm = bpy.data.armatures.new(contract.ARMATURE_OBJECT)
    obj = bpy.data.objects.new(contract.ARMATURE_OBJECT, arm)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = ctx.root
    obj.matrix_parent_inverse.identity()
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)
    arm.display_type = "STICK"
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for name in names:
        h, t = layout[name]
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = h, t
        if (t - h).length < 1e-3:
            eb.tail = h + Vector((0, 0, 0.02))
        eb.align_roll(_roll_axis((eb.tail - eb.head).normalized()))
        eb.use_deform = not name.startswith("Sock.")
        eb.use_connect = False
        made[name] = eb
    for name, eb in made.items():
        p = parents[name]
        if p is not None:
            eb.parent = made[p]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in obj.pose.bones:
        pb.rotation_mode = "XYZ"           # clip data are Euler XYZ in bone-local axes (README 2.6)
    ctx.armature = obj

    bones = []
    for n in names:
        b = arm.bones[n]
        bones.append({"name": n, "parent": b.parent.name if b.parent else None, "deform": bool(b.use_deform),
                      "head": common.rv(b.head_local), "tail": common.rv(b.tail_local), "roll_rule": "x_to_world_x"})
    socks = {}
    for n in contract.SOCKET_NAMES:
        b = arm.bones[n]
        socks[n] = {"parent": b.parent.name, "head": common.rv(b.head_local), "tail": common.rv(b.tail_local),
                    "placeholder": bool(placeholder and n in ("Sock.StaffTip", "Sock.SpellOrigin"))}
    ctx.set_section("bones", bones)
    ctx.set_section("sockets", socks)
    ctx.wp2 = {"joints": J, "classes": cls, "asymmetry_m": asym, "asymmetry_max_m": round(worst, 4),
               "symmetrised": worst <= SYMMETRISE_MAX_M, "layout": layout}
    return obj


def arm_angle_deg(arm_obj, side):
    """Rest arm angle from vertical, shoulder pivot to hand centre (Sock.Hand is at the hand centre or the grip)."""
    b = arm_obj.data.bones
    sh = b["UpperArm." + side].head_local
    hand = (b["Hand." + side].head_local + b["Hand." + side].tail_local) / 2
    d = hand - sh
    return math.degrees(math.atan2(math.hypot(d.x, d.y), -d.z))
