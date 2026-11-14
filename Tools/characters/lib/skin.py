"""WP-2: skin weights (PRD_CHARACTERS 5.2 step 3), joint covers, Armature modifiers.

`skin_character(ctx)` runs after `rig.fit_rig(ctx)`:

1. Joint covers (README 4, "Joint gaps"): every elbow and knee of the 14 humans is only touching, so a rigid
   bend opens a crack. A low-poly sphere (8 x 6, flat, 48 faces) of the limb's radius is added at each elbow and
   knee pivot as an extra loose piece in the same mesh (elbows in `Body`, knees in `Legs`), with the material
   of the lower piece, weighted to the lower bone. Because it is centred on the pivot it fills the wedge that a
   bend opens. The new pieces are appended after the supplied faces, so every supplied island keeps its id;
   they are listed in the manifest (`skin.joint_covers`) and in `ctx.joint_covers`.
2. Weights. Rigid by piece (one bone per loose piece) except four soft fields:
   - torso field: torso, torso details, chest trims: Hips / Spine / Chest blended by height; below the hip line
     the front and side parts take up to LEG_SHARE of the matching upper leg (kilts, aprons and waist boxes
     move with the thigh instead of being pierced by it);
   - skirt field (dress family): the robe and its hem: Hips at the waist, then the Skirt.F/B/L/R bones by angle
     around the body; the hem uses the same field so robe and hem never separate;
   - cape field: the back cloth along Cape.1..3 by height;
   - hood field: the hood shell, Head at the face opening, Hood.1 / Hood.2 toward the back and the drape.
   Shoulder caps go to `Shoulder.x` (not the island map's UpperArm suggestion): the cap is a ball centred on
   the arm pivot, so the sleeve turns inside it and the cap stays on the torso. Face pieces follow the contract
   (iris and pupil to Eye.x, mouth and beard to Jaw, everything else on the head to Head). Props to the holding
   hand.
3. At most four influences, normalised; every vertex weighted; only deform bones get vertex groups.
4. Each part gets one Armature modifier and is parented to the armature object (identity, no bone parent).

Writes manifest section `skin`.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import rig  # noqa: E402
import islands as islands_mod  # noqa: E402

LEG_SHARE = 0.55            # share of the thigh at the bottom of a kilt / apron / waist box
LEG_SHARE_DROP = 0.22       # metres below the hip line over which that share ramps in
COVER_SEGMENTS = 8
COVER_RINGS = 6
COVER_SCALE = 1.0           # cover radius = limb radius at the joint * this

TORSO_ROLES = {"torso", "torso_detail", "cloth_front_or_trim", "cloth_shoulder_or_wrap", "hood_or_collar"}
SKIRT_ROLES = {"robe_or_skirt", "skirt_hem"}


def _smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------------------------------
# Joint covers
# ----------------------------------------------------------------------------------------
def _material_index_of(obj, vert_ids):
    """Most used material index among faces that use these vertices."""
    vs = set(vert_ids)
    count = {}
    for p in obj.data.polygons:
        if p.vertices[0] in vs:
            count[p.material_index] = count.get(p.material_index, 0) + 1
    return max(count, key=count.get) if count else 0


def add_joint_covers(ctx):
    isl = rig.island_vertices(ctx)
    cls = ctx.wp2["classes"]
    J = ctx.wp2["joints"]
    arm = ctx.armature.data
    specs = []
    for s in ("L", "R"):
        a, g, j = cls["arm"][s], cls["leg"][s], J[s]
        r_el = min(j["upper_end_radius"], j["forearm_radius"]) * COVER_SCALE
        specs.append(("elbow." + s, "Body", "LowerArm." + s, "UpperArm." + s, j["elbow"], r_el, a["lower_main"]))
        r_kn = min(j["thigh_radius"], j["shin_radius"]) * COVER_SCALE
        specs.append(("knee." + s, "Legs", "LowerLeg." + s, "UpperLeg." + s, j["knee"], r_kn, g["shin_main"]))
    covers = []
    by_part = {}
    for sp in specs:
        by_part.setdefault(sp[1], []).append(sp)
    for part, sps in by_part.items():
        obj = ctx.parts[part]
        me = obj.data
        bm = bmesh.new()
        bm.from_mesh(me)
        uv = bm.loops.layers.uv.active
        made = []
        for name, _p, bone, upper_bone, centre, radius, lower_island in sps:
            mat_index = _material_index_of(obj, isl[lower_island]["verts"])
            # pole along the mean of the two bone directions, so the rings sit around the limb
            b1 = arm.bones[upper_bone]
            b2 = arm.bones[bone]
            axis = ((b1.tail_local - b1.head_local).normalized() + (b2.tail_local - b2.head_local).normalized()).normalized()
            rot = axis.to_track_quat("Z", "X").to_matrix().to_4x4()
            mat = Matrix.Translation(Vector(centre)) @ rot
            res = bmesh.ops.create_uvsphere(bm, u_segments=COVER_SEGMENTS, v_segments=COVER_RINGS, radius=radius,
                                            matrix=mat, calc_uvs=uv is not None)
            new_verts = list(res["verts"])
            faces = {f for v in new_verts for f in v.link_faces}
            for f in faces:
                f.material_index = mat_index
                f.smooth = False
            made.append((name, bone, centre, radius, new_verts, len(faces), mat_index))
        # bmesh may reuse freed slots, so vertex indices are only known after a final index update
        bm.verts.index_update()
        made = [(n, b, c, r, sorted(v.index for v in vs), nf, mi) for n, b, c, r, vs, nf, mi in made]
        bm.to_mesh(me)
        bm.free()
        me.update()
        groups, _ = islands_mod.vertex_islands(obj)
        owner = {}
        for i, g in enumerate(groups):
            for v in g["verts"]:
                owner[v] = i
        for name, bone, centre, radius, vids, nf, mi in made:
            covers.append({"name": name, "part": part, "bone": bone, "centre": common.rv(centre),
                           "radius": common.r(radius), "vert_ids": vids, "verts": len(vids), "faces": nf,
                           "island": "%s.%02d" % (part, owner[vids[0]]),
                           "material": obj.material_slots[mi].material.name
                           if obj.material_slots and obj.material_slots[mi].material else None})
    ctx.joint_covers = covers
    return covers


# ----------------------------------------------------------------------------------------
# Weight fields
# ----------------------------------------------------------------------------------------
def _bone_z(arm, name, end="tail"):
    b = arm.bones[name]
    return (b.tail_local if end == "tail" else b.head_local).z


class Fields:
    def __init__(self, ctx):
        self.ctx = ctx
        arm = ctx.armature.data
        self.arm = arm
        lm = ctx.islands["landmarks"]
        self.hip_z = lm["hip_z"]
        self.z_hs = _bone_z(arm, "Hips")            # Hips -> Spine boundary
        self.z_sc = _bone_z(arm, "Spine")           # Spine -> Chest boundary
        self.has_skirt = "Skirt.F" in arm.bones
        self.has_cape = "Cape.1" in arm.bones
        self.has_hood = "Hood.1" in arm.bones
        isl = rig.island_vertices(ctx)
        if self.has_skirt:
            co = np.vstack([d["co"] for d in isl.values() if d["info"].get("role") in SKIRT_ROLES])
            mn, mx = co.min(axis=0), co.max(axis=0)
            self.skirt_cy = float((mn[1] + mx[1]) / 2)
            self.skirt_bottom = float(mn[2])
        if self.has_cape:
            co = np.vstack([d["co"] for d in isl.values() if d["info"].get("role") == "cloth_back"])
            self.cape_top, self.cape_bot = float(co[:, 2].max()), float(co[:, 2].min())
        if self.has_hood:
            co = np.vstack([d["co"] for d in isl.values() if d["info"].get("role") == "hood"])
            self.hood_min, self.hood_max = co.min(axis=0), co.max(axis=0)
            self.head_cy = lm["head_centre"][1]

    def torso(self, p, legs=True, centre_plate=False):
        x, y, z = p
        w = {}
        h1 = 0.05
        h2 = 0.06
        a = _smooth((z - (self.z_hs - h1)) / (2 * h1))          # 0 below, 1 above the Hips/Spine boundary
        b = _smooth((z - (self.z_sc - h2)) / (2 * h2))          # 0 below, 1 above the Spine/Chest boundary
        w["Hips"] = 1 - a
        w["Spine"] = a * (1 - b)
        w["Chest"] = a * b
        if legs and z < self.hip_z:
            share = LEG_SHARE * _smooth((self.hip_z - z) / LEG_SHARE_DROP)
            if share > 0:
                # a thin central plate (apron, tabard) takes both thighs equally so it stays flat instead of
                # shearing into a torn-looking zigzag; other pieces take the thigh on their own side
                fl = 0.5 if centre_plate else min(max(0.5 + x / 0.06, 0.0), 1.0)
                for k in list(w):
                    w[k] *= (1 - share)
                w["UpperLeg.L"] = share * fl
                w["UpperLeg.R"] = share * (1 - fl)
        return w

    def plate(self, p):
        return self.torso(p, centre_plate=True)

    def skirt(self, p):
        x, y, z = p
        if not self.has_skirt:
            return self.torso(p)
        top = self.hip_z + 0.04
        s = _smooth((top - z) / 0.18)
        w = {k: v * (1 - s) for k, v in self.torso(p, legs=False).items()}
        if s > 0:
            ang = math.atan2(y - self.skirt_cy, x)    # 0 = +X (left), pi/2 = +Y (back)
            dirs = {"Skirt.L": 0.0, "Skirt.B": math.pi / 2, "Skirt.R": math.pi, "Skirt.F": -math.pi / 2}
            lob = {}
            for n, d in dirs.items():
                c = math.cos(ang - d)
                if c > 0:
                    lob[n] = c * c
            tot = sum(lob.values()) or 1.0
            for n, v in lob.items():
                w[n] = w.get(n, 0) + s * v / tot
        return w

    def cape(self, p):
        x, y, z = p
        t = (self.cape_top - z) / max(self.cape_top - self.cape_bot, 1e-6)
        n = len(contract.CAPE_BONES)
        u = t * n - 0.5
        names = [b for b, _ in contract.CAPE_BONES]
        if u <= 0:
            return {names[0]: 1.0}
        if u >= n - 1:
            return {names[-1]: 1.0}
        k = int(math.floor(u))
        f = u - k
        return {names[k]: 1 - f, names[k + 1]: f}

    def hood(self, p):
        x, y, z = p
        back = _smooth((y - self.head_cy) / max(self.hood_max[1] - self.head_cy, 1e-6) * 1.3)
        mid_z = (self.hood_min[2] + self.hood_max[2]) / 2
        low = _smooth((mid_z - z) / max(mid_z - self.hood_min[2], 1e-6))
        w = {"Head": 1 - back}
        w["Hood.2"] = back * low
        w["Hood.1"] = back * (1 - low)
        return w


def piece_rules(ctx):
    """{island_id: bone name or 'field:<name>'} for every supplied island."""
    isl = rig.island_vertices(ctx)
    cls = ctx.wp2["classes"]
    lm = ctx.islands["landmarks"]
    arm = ctx.armature.data
    neck_bottom = arm.bones["Neck"].head_local.z
    head_bottom = lm["head_bottom_z"]
    prop = lm.get("prop")
    fields = Fields(ctx)
    rules = {}
    for s in ("L", "R"):
        a, g = cls["arm"][s], cls["leg"][s]
        if a["cap"]:
            rules[a["cap"]] = "Shoulder." + s
        for i in a["upper"]:
            rules[i] = "UpperArm." + s
        for i in a["lower"] + a["cuff"]:
            rules[i] = "LowerArm." + s
        for i in a["hand"]:
            rules[i] = "Hand." + s
        for i in g["thigh"]:
            rules[i] = "UpperLeg." + s
        for i in g["shin"] + g["boot_top"]:
            rules[i] = "LowerLeg." + s
        for i in g["foot"]:
            rules[i] = "Foot." + s
    for iid, d in isl.items():
        if iid in rules:
            continue
        info = d["info"]
        role = info.get("role")
        part = d["part"]
        cz = float((d["co"][:, 2].min() + d["co"][:, 2].max()) / 2)
        if part == "Prop":
            rules[iid] = prop["bone"] if prop else (info.get("bone") or "Hand.R")
        elif part == "Head":
            if role == "hood" and fields.has_hood:
                rules[iid] = "field:hood"
            elif role in ("eye_iris", "eye_pupil"):
                rules[iid] = "Eye." + info["side"]
            elif role in ("mouth", "beard"):
                rules[iid] = "Jaw"
            else:
                rules[iid] = "Head"
        elif role == "neck":
            rules[iid] = "Neck"
        elif role in SKIRT_ROLES and fields.has_skirt:
            rules[iid] = "field:skirt"
        elif role == "cloth_back" and fields.has_cape:
            rules[iid] = "field:cape"
        elif cz > head_bottom:
            rules[iid] = "Head"
        elif cz > neck_bottom + 0.03 and role in TORSO_ROLES and abs(float(d["co"][:, 0].mean())) < 0.12:
            rules[iid] = "Neck"
        elif role in TORSO_ROLES or role in SKIRT_ROLES or role == "cloth_back":
            mn, mx = d["co"].min(axis=0), d["co"].max(axis=0)
            size = mx - mn
            central_plate = abs(float(mn[0] + mx[0]) / 2) < 0.04 and size[1] < 0.045 and size[0] > 0.15 \
                and mn[2] < fields.hip_z - 0.05
            rules[iid] = "field:plate" if central_plate else "field:torso"
        elif info.get("bone") in arm.bones and arm.bones[info["bone"]].use_deform:
            rules[iid] = info["bone"]
        else:
            rules[iid] = "field:torso"
    ov = rig.load_overrides(ctx.key).get("islands", {})
    rules.update({k: v for k, v in ov.items() if k in rules})
    return rules, fields


# ----------------------------------------------------------------------------------------
# Main step
# ----------------------------------------------------------------------------------------
def _clean(w):
    w = {k: v for k, v in w.items() if v > 0.005}     # drop slivers (verify rejects weights under 0.001)
    if len(w) > contract.MAX_INFLUENCES:
        w = dict(sorted(w.items(), key=lambda kv: -kv[1])[:contract.MAX_INFLUENCES])
    tot = sum(w.values())
    return {k: v / tot for k, v in w.items()}


def skin_character(ctx):
    if ctx.armature is None:
        raise RuntimeError("run rig.fit_rig first")
    arm_obj = ctx.armature
    isl = rig.island_vertices(ctx)       # measured before the covers are added
    rules, fields = piece_rules(ctx)
    covers = add_joint_covers(ctx)
    weights = {pn: {} for pn in ctx.parts}
    for iid, d in isl.items():
        rule = rules[iid]
        pn = d["part"]
        for vi, p in zip(d["verts"], d["co"]):
            if rule.startswith("field:"):
                w = getattr(fields, rule.split(":", 1)[1])(p)
            else:
                w = {rule: 1.0}
            weights[pn][vi] = _clean(w)
    for c in covers:
        for vi in c["vert_ids"]:
            weights[c["part"]][vi] = {c["bone"]: 1.0}
    deform = {b.name for b in arm_obj.data.bones if b.use_deform}
    max_inf = 0
    unweighted = 0
    for pn, obj in ctx.parts.items():
        obj.vertex_groups.clear()
        used = sorted({b for w in weights[pn].values() for b in w})
        bad = [b for b in used if b not in deform]
        if bad:
            raise RuntimeError("%s: weights to non-deform or unknown bones %s" % (ctx.key, bad))
        groups = {b: obj.vertex_groups.new(name=b) for b in used}
        n = len(obj.data.vertices)
        for vi in range(n):
            w = weights[pn].get(vi)
            if not w:
                unweighted += 1
                continue
            max_inf = max(max_inf, len(w))
            for b, v in w.items():
                groups[b].add([vi], v, "REPLACE")
        for m in [m for m in obj.modifiers if m.type == "ARMATURE"]:
            obj.modifiers.remove(m)
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm_obj
        mod.use_vertex_groups = True
        mod.use_bone_envelopes = False
        mw = obj.matrix_world.copy()
        obj.parent = arm_obj
        obj.matrix_parent_inverse = arm_obj.matrix_world.inverted()
        obj.matrix_world = mw
    pieces = [{"island": iid, "bone": rules[iid]} for iid in sorted(rules)]
    pieces += [{"island": c["island"], "bone": c["bone"], "joint_cover": c["name"]} for c in covers]
    ctx.set_section("skin", {
        "max_influences": max_inf,
        "unweighted_verts": unweighted,
        "pieces": pieces,
        "fields": {"torso": ["Hips", "Spine", "Chest", "UpperLeg.L", "UpperLeg.R"],
                   "plate": ["Hips", "Spine", "Chest", "UpperLeg.L", "UpperLeg.R"],
                   "skirt": ["Hips", "Skirt.F", "Skirt.B", "Skirt.L", "Skirt.R"],
                   "cape": [b for b, _ in contract.CAPE_BONES], "hood": ["Head", "Hood.1", "Hood.2"]},
        "joint_covers": [{k: v for k, v in c.items() if k not in ("vert_ids",)} for c in covers],
        "leg_share": LEG_SHARE,
        "fit": {"asymmetry_max_m": ctx.wp2["asymmetry_max_m"], "symmetrised": ctx.wp2["symmetrised"]},
    })
    return weights
