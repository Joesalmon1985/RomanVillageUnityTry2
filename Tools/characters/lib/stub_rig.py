"""Development stub armature (WP-1). NOT the production rig.

Purpose: lets WP-3 (face), WP-4 (costume) and WP-5 (clips) run and test against a contract-correct armature
before WP-2 delivers `rig.fit_rig`. It places every contract bone from the committed island map only
(landmarks), with the roll rule below. No skin weights, no weighting, no IK. WP-2's `rig.fit_rig(ctx)` has
the same name/parents/roll and replaces this; code written against this stub keeps working.

Roll rule (contract): for every bone, local +X is as close as possible to world +X (character left).
Local Y runs head to tail along the bone; local Z = X cross Y. So for the vertical spine chain local Z points
forward-ish (-Y world flips sign: Z = X x Y with Y up gives Z = -world Y... see README) and flexion of a limb
is a rotation about local X.
"""

import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402


def _roll_axis(y):
    zt = Vector((1.0, 0.0, 0.0)).cross(y)
    return zt if zt.length > 1e-6 else Vector((0.0, 0.0, 1.0))


def _isl(imap, role, side=None, part=None):
    for pn, p in imap["parts"].items():
        if part and pn != part:
            continue
        for i in p["islands"]:
            if i.get("role") == role and (side is None or i["side"] == side):
                return i
    return None


def _all(imap, role, side=None):
    out = []
    for p in imap["parts"].values():
        for i in p["islands"]:
            if i.get("role") == role and (side is None or i["side"] == side):
                out.append(i)
    return out


def bone_layout(imap):
    """Return {bone_name: (head, tail)} for every contract bone this character needs (world, metres)."""
    lm = imap["landmarks"]
    V = lambda a: Vector(a)
    torso = _isl(imap, "torso")
    yc = torso["centre"][1] if torso else 0.0
    hip_z, neck_z = lm["hip_z"], lm["neck_z"]
    head_bottom, crown = lm["head_bottom_z"], lm["crown_z"]
    neck = _isl(imap, "neck")
    neck_bottom = neck["min"][2] if neck else neck_z - 0.06
    L = {}
    L["Hips"] = (V((0, yc, hip_z - 0.06)), V((0, yc, hip_z + 0.10)))
    spine_top = hip_z + 0.35 * (neck_bottom - hip_z)
    L["Spine"] = (L["Hips"][1].copy(), V((0, yc, spine_top)))
    L["Chest"] = (L["Spine"][1].copy(), V((0, yc, neck_bottom)))
    L["Neck"] = (L["Chest"][1].copy(), V((0, lm["head_centre"][1] + 0.02, head_bottom)))
    L["Head"] = (L["Neck"][1].copy(), V((0, lm["head_centre"][1], crown)))
    face = imap["face"]
    mouth = face.get("mouth")
    ear = _isl(imap, "ear", "L")
    if mouth and ear:
        L["Jaw"] = (V((0, ear["centre"][1], mouth["centre"][2] + 0.04)), V((0, mouth["centre"][1], mouth["centre"][2] - 0.02)))
    back = face["pivot_offset_back_m"] if "pivot_offset_back_m" in face else 0.025
    for s, key in (("L", "Eye.L"), ("R", "Eye.R")):
        e = face["sides"][s]["centre"]
        c = V(e) + Vector((0, back, 0))
        L[key] = (c, c + Vector((0, -0.06, 0)))
    for s in ("L", "R"):
        cap = _isl(imap, "shoulder_cap", s)
        hand = _isl(imap, "hand", s)
        sh = V(cap["centre"]) if cap else V((0.2 if s == "L" else -0.2, yc, lm["shoulder_z"][s]))
        hc = V(hand["centre"])
        wz = lm["wrist_z"][s]
        d = (hc - sh)
        d_unit = d.normalized()
        # points on the shoulder->hand line at elbow and wrist heights
        def at_z(z):
            t = (z - sh.z) / d_unit.z
            return sh + d_unit * t
        elbow = at_z(lm["elbow_z"][s])
        wrist = at_z(wz)
        tip = wrist + d_unit * (hand["size"][2] * 0.9)
        root = V((0.05 if s == "L" else -0.05, yc, sh.z))
        L["Shoulder." + s] = (root, sh)
        L["UpperArm." + s] = (sh, elbow)
        L["LowerArm." + s] = (elbow, wrist)
        L["Hand." + s] = (wrist, tip)
        thigh = _isl(imap, "thigh", s)
        foot = _isl(imap, "foot", s)
        hx, hy = thigh["centre"][0], thigh["centre"][1]
        hip = V((hx, hy, hip_z - 0.03))
        knee = V((hx, hy, lm["knee_z"][s]))
        ankle_z = lm["ankle_z"][s]
        ax = foot["centre"][0]
        ankle = V((ax, hy, max(ankle_z - 0.02, 0.05)))
        toe_base = V((ax, foot["centre"][1] - 0.02, foot["centre"][2]))
        toe_tip = V((ax, foot["min"][1], foot["centre"][2]))
        L["UpperLeg." + s] = (hip, knee)
        L["LowerLeg." + s] = (knee, ankle)
        L["Foot." + s] = (ankle, toe_base)
        L["Toes." + s] = (toe_base, toe_tip)
        gz = 0.0
        L["Sock.Foot." + s] = (V((ax, foot["centre"][1], gz)), V((ax, foot["centre"][1], gz + 0.05)))
    # sockets
    hl, hr = L["Hand.L"], L["Hand.R"]
    for s, key in (("L", "Sock.Hand.L"), ("R", "Sock.Hand.R")):
        h = L["Hand." + s]
        c = (h[0] + h[1]) / 2
        L[key] = (c, c + (h[1] - h[0]).normalized() * 0.05)
    prop = lm.get("prop")
    base = L["Sock.Hand.R"][0]
    if prop:
        g = V(prop["grip_point"])
        L["Sock.Hand." + prop["holding_hand"]] = (g, g + Vector((0, 0, -0.05)))
        head_pt = V(prop["head_point"])
    else:
        head_pt = base + Vector((0, 0, 0.45))   # placeholder position, no prop
    L["Sock.StaffTip"] = (head_pt, head_pt + Vector((0, 0, 0.05)))
    L["Sock.SpellOrigin"] = (head_pt + Vector((0, 0, 0.05)), head_pt + Vector((0, 0, 0.10)))
    L["Sock.Back"] = (V((0, yc + 0.14, spine_top)), V((0, yc + 0.19, spine_top)))
    L["Sock.Hip.R"] = (V((-0.21, yc, hip_z - 0.04)), V((-0.26, yc, hip_z - 0.04)))
    L["Sock.HeadTop"] = (V((0, lm["head_centre"][1], crown)), V((0, lm["head_centre"][1], crown + 0.05)))
    return L


def optional_layout(imap, key):
    """Cape, hood and skirt chains where the character has the garment (positions only)."""
    lm = imap["landmarks"]
    L = {}
    idx = contract.CAST_BY_KEY[key]
    backs = _all(imap, "cloth_back")
    if backs:
        top = max(i["max"][2] for i in backs)
        bot = min(i["min"][2] for i in backs)
        y = max(i["centre"][1] for i in backs)
        n = len(contract.CAPE_BONES)
        for k, (name, _p) in enumerate(contract.CAPE_BONES):
            z0 = top - (top - bot) * k / n
            z1 = top - (top - bot) * (k + 1) / n
            L[name] = (Vector((0, y, z0)), Vector((0, y, z1)))
    hoods = _all(imap, "hood")
    if hoods:
        h = hoods[0]
        L["Hood.1"] = (Vector((0, h["centre"][1], h["max"][2] - 0.02)), Vector((0, h["max"][1], h["centre"][2])))
        L["Hood.2"] = (L["Hood.1"][1].copy(), Vector((0, h["max"][1] + 0.02, h["min"][2])))
    skirts = _all(imap, "robe_or_skirt")
    legs_dress = imap["parts"]["Legs"]["tris"] == 348
    if skirts or legs_dress:
        bottom = min([i["min"][2] for i in skirts] + [0.12])
        hz = lm["hip_z"]
        for name, (x, y) in {"Skirt.F": (0.0, -0.12), "Skirt.B": (0.0, 0.14), "Skirt.L": (0.2, 0.0), "Skirt.R": (-0.2, 0.0)}.items():
            L[name] = (Vector((x, y, hz)), Vector((x, y, bottom)))
    return L


def build_stub_armature(ctx):
    """Create the stub armature for a loaded character (see common.load_character). Returns the armature object."""
    imap = ctx.islands
    if imap is None:
        raise RuntimeError("island map for %s not found in cast/islands" % ctx.key)
    layout = bone_layout(imap)
    layout.update(optional_layout(imap, ctx.key))
    arm = bpy.data.armatures.new(contract.ARMATURE_OBJECT)
    obj = bpy.data.objects.new(contract.ARMATURE_OBJECT, arm)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = ctx.root
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    parents = {n: p for n, p in contract.HUMAN_CORE_BONES + contract.FACE_BONES + contract.CAPE_BONES + contract.HOOD_BONES
               + contract.SKIRT_BONES + contract.SOCKETS}
    prop = imap["landmarks"].get("prop")
    if prop:   # contract: StaffTip hangs from the socket of the hand that holds the prop
        parents["Sock.StaffTip"] = "Sock.Hand." + prop["holding_hand"]
    made = {}
    for name in [n for n in parents if n in layout]:
        h, t = layout[name]
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = h, t
        if (t - h).length < 1e-4:
            eb.tail = h + Vector((0, 0, 0.02))
        eb.align_roll(_roll_axis((eb.tail - eb.head).normalized()))
        eb.use_deform = not name.startswith("Sock.")
        made[name] = eb
    for name, eb in made.items():
        p = parents[name]
        if p in made:
            eb.parent = made[p]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    ctx.armature = obj
    return obj
