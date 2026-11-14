"""Island map (WP-1): split each supplied part into loose pieces and describe them without vertex data.

An island = a set of vertices connected through faces. The supplied meshes are built from disconnected
primitives (a sleeve, a hand, an eye ball), so islands are the natural unit for rigid weighting.

Output (geometry-free: names, indices, counts, centres, sizes; no vertex or face data) per character:
  {
   "key", "forward", "left_sign_x", "height", "landmarks": {...}, "face": {...}, "joint_gaps": {...},
   "parts": {"Body": {"tris","verts","islands":[{id, index, tris, verts, faces, ngons, materials[], hex[],
                       centre, size, min, max, side, aspect, role, bone, nested_in, ...}]}, ...},
   "adjacency": [{a, b, bbox_gap, surface_gap, penetration, overlap}]
  }

Island ids are "<Part>.<nn>", nn = order of the island's first face in the mesh. They are stable for the
unchanged supplied FBX. skin.py gets vertex indices with vertex_islands(obj) (same order, same ids).
`role` and `bone` are SUGGESTIONS from position rules; WP-2/3/4 may override them in their own data.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import contract  # noqa: E402
import common  # noqa: E402
from common import r, rv  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils.kdtree import KDTree  # noqa: E402


# ----------------------------------------------------------------------------------------
# Splitting
# ----------------------------------------------------------------------------------------
def vertex_islands(obj):
    """Deterministic split. Returns (islands, coords) where islands is a list of dicts
    {"verts": [int], "faces": [int]} ordered by the lowest face index of each island,
    and coords is an (n,3) float array of world-space vertex positions.
    """
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    mw = np.array(obj.matrix_world, dtype=np.float64)
    co = co @ mw[:3, :3].T + mw[:3, 3]

    parent = list(range(n))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for poly in me.polygons:
        vs = poly.vertices
        a = find(vs[0])
        for v in vs[1:]:
            b = find(v)
            if a != b:
                parent[b] = a
    groups = {}
    order = []
    for poly in me.polygons:
        root = find(poly.vertices[0])
        if root not in groups:
            groups[root] = {"verts": set(), "faces": []}
            order.append(root)
        groups[root]["faces"].append(poly.index)
        groups[root]["verts"].update(poly.vertices)
    islands = []
    for root in order:
        g = groups[root]
        islands.append({"verts": sorted(g["verts"]), "faces": g["faces"]})
    return islands, co


# ----------------------------------------------------------------------------------------
# Geometry helpers
# ----------------------------------------------------------------------------------------
def _box_gap(amin, amax, bmin, bmax):
    d = np.maximum(0.0, np.maximum(amin - bmax, bmin - amax))
    return float(np.linalg.norm(d))


def _overlap_frac(amin, amax, bmin, bmax):
    lo = np.maximum(amin, bmin)
    hi = np.minimum(amax, bmax)
    d = hi - lo
    if np.any(d <= 0):
        return 0.0
    ov = float(np.prod(d))
    va = float(np.prod(np.maximum(amax - amin, 1e-9)))
    vb = float(np.prod(np.maximum(bmax - bmin, 1e-9)))
    return ov / min(va, vb)


def _contained(inner_min, inner_max, outer_min, outer_max, tol=0.002):
    return bool(np.all(inner_min >= outer_min - tol) and np.all(inner_max <= outer_max + tol))


def _bvh(isl):
    from mathutils.bvhtree import BVHTree
    from mathutils import Vector
    if "_bvh" not in isl:
        isl["_bvh"] = BVHTree.FromPolygons([Vector(p) for p in isl["_pts"]], isl["_polys"], epsilon=0.0)
    return isl["_bvh"]


_RAY_DIRS = None


def _inside(tree, p):
    """Parity test with three skew rays; majority vote. Closed convex-ish primitives only."""
    global _RAY_DIRS
    from mathutils import Vector
    if _RAY_DIRS is None:
        _RAY_DIRS = [Vector(d).normalized() for d in ((0.577, 0.301, 0.754), (-0.4, 0.83, 0.39), (0.21, -0.33, -0.93))]
    votes = 0
    for d in _RAY_DIRS:
        origin = Vector(p)
        count = 0
        for _ in range(32):
            loc, nrm, _i, _dist = tree.ray_cast(origin, d)
            if loc is None:
                break
            count += 1
            origin = loc + d * 1e-5
        votes += count % 2
    return votes >= 2


def surface_relation(a, b):
    """Surface-to-surface relation of two islands (closed primitives).
    Returns (gap, penetration): gap = smallest distance between a vertex of one piece and the surface of the other
    (0 if any vertex is inside the other piece); penetration = deepest vertex inside the other piece, measured to
    the other piece's surface (0 if none)."""
    from mathutils import Vector
    gap = 1e9
    pen = 0.0
    for src, dst in ((a, b), (b, a)):
        tree = _bvh(dst)
        dmin, dmax = _np(dst["min"]) - 1e-4, _np(dst["max"]) + 1e-4
        for p in src["_pts"]:
            v = Vector(p)
            loc, nrm, _, dist = tree.find_nearest(v)
            if loc is None:
                continue
            if np.all(p >= dmin) and np.all(p <= dmax) and _inside(tree, p):
                pen = max(pen, dist)
                gap = 0.0
            else:
                gap = min(gap, dist)
    return float(gap), float(pen)


def _side(cx, tol=0.012):
    if abs(cx) < tol:
        return "C"
    return "L" if cx * contract.LEFT_SIGN_X > 0 else "R"


# ----------------------------------------------------------------------------------------
# Per-part descriptions
# ----------------------------------------------------------------------------------------
def describe_part(obj, part_name):
    me = obj.data
    isl_raw, co = vertex_islands(obj)
    mats = [m.name if m else "" for m in me.materials]
    poly_mat = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("material_index", poly_mat)
    out = []
    for i, g in enumerate(isl_raw):
        pts = co[g["verts"]]
        mn, mx = pts.min(axis=0), pts.max(axis=0)
        size = mx - mn
        counts = {}
        tris = 0
        ngons = 0
        for fi in g["faces"]:
            nv = len(me.polygons[fi].vertices)
            tris += nv - 2
            if nv > 4:
                ngons += 1
            mname = mats[poly_mat[fi]] if poly_mat[fi] < len(mats) else ""
            counts[mname] = counts.get(mname, 0) + 1
        mat_list = [k for k, _ in sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))]
        # closed? every edge used by exactly two faces
        edge_use = {}
        for fi in g["faces"]:
            vs = me.polygons[fi].vertices
            for a, b in zip(vs, list(vs[1:]) + [vs[0]]):
                k = (a, b) if a < b else (b, a)
                edge_use[k] = edge_use.get(k, 0) + 1
        open_edges = sum(1 for c in edge_use.values() if c != 2)
        remap = {v: k for k, v in enumerate(g["verts"])}
        polys = [tuple(remap[v] for v in me.polygons[fi].vertices) for fi in g["faces"]]
        biggest = float(size.max()) if size.max() > 0 else 1e-9
        out.append({
            "id": "%s.%02d" % (part_name, i),
            "index": i,
            "faces": len(g["faces"]),
            "tris": tris,
            "verts": len(g["verts"]),
            "ngons": ngons,
            "materials": mat_list,
            "hex": [h for h in (common.hex_from_material_name(m) for m in mat_list) if h],
            "centre": rv((mn + mx) / 2),
            "size": rv(size),
            "min": rv(mn),
            "max": rv(mx),
            "centroid": rv(pts.mean(axis=0)),
            "side": _side(float((mn[0] + mx[0]) / 2)),
            "aspect": r(float(size.min()) / biggest, 3),
            "open_edges": open_edges,
            "_pts": pts,   # stripped before writing
            "_polys": polys,
        })
    return out


# ----------------------------------------------------------------------------------------
# Classification (suggestions only)
# ----------------------------------------------------------------------------------------
def _np(v):
    return np.array(v, dtype=np.float64)


def _classify_head(isl, H):
    """Label head islands. Returns dict with face summary. Mutates isl dicts (role)."""
    fwd = -1  # forward = -Y
    skin_cands = [i for i in isl if abs(i["centre"][0]) < 0.01 and i["size"][0] > 0.15 and i["size"][2] > 0.15 and i["size"][1] > 0.15]
    # head skin: the largest-volume centred island with the smallest face count among big centred ones is not reliable;
    # use the island whose centre is lowest among big centred islands (hair sits above the skull).
    head = None
    if skin_cands:
        head = min(skin_cands, key=lambda i: (i["centre"][2], i["index"]))
    if head is None:
        head = max(isl, key=lambda i: i["size"][0] * i["size"][1] * i["size"][2])
    head["role"] = "head_skin"
    hc = _np(head["centre"])
    hs = _np(head["size"])
    skin_mat = head["materials"][0] if head["materials"] else None

    def front(i):  # distance in front of head centre
        return (hc[1] - i["centre"][1]) * -fwd * -1 if False else (hc[1] - i["centre"][1])

    rest = [i for i in isl if i is not head]
    # eye stacks: group islands by (round(|x|,2), round(z,2)) pairs that are in front of the head centre
    cands = []
    for i in rest:
        if i["side"] == "C":
            continue
        if front(i) < 0.35 * hs[1]:      # must be well in front of the head centre
            continue
        if i["size"][0] > 0.09 or i["size"][2] > 0.09:  # too big for an eye layer
            continue
        cands.append(i)
    stacks = {}
    clusters = []   # [side, x, z, [islands]]
    for i in cands:
        for c in clusters:
            if c[0] == i["side"] and abs(c[1] - i["centre"][0]) < 0.012 and abs(c[2] - i["centre"][2]) < 0.012:
                c[3].append(i)
                break
        else:
            clusters.append([i["side"], i["centre"][0], i["centre"][2], [i]])
    for c in clusters:
        stacks[(c[0], round(c[1], 3), round(c[2], 3))] = c[3]
    eye_stacks = {k: v for k, v in stacks.items() if len(v) >= 3}
    face = {"eyes": {}, "brows": [], "mouth": None, "nose": None, "ears": [], "hair": [], "beard": [], "other": []}
    eye_ids = set()
    for k, v in sorted(eye_stacks.items()):
        side = k[0]
        v = sorted(v, key=lambda i: (-i["size"][0] * i["size"][2], i["index"]))  # largest first = back/outline
        names = ["outline", "sclera", "iris", "pupil"]
        # drop thin plates (brows, highlights) that happen to sit in the stack
        layers = []
        for n, i in zip(names, v):
            i["role"] = "eye_" + n
            layers.append(i)
            eye_ids.add(i["id"])
        face["eyes"][side] = layers
    eye_z = None
    if face["eyes"]:
        eye_z = float(np.mean([L[0]["centre"][2] for L in face["eyes"].values()]))
    for i in rest:
        if i["id"] in eye_ids:
            continue
        c = _np(i["centre"])
        sz = _np(i["size"])
        mats = i["materials"]
        is_skin = skin_mat is not None and mats and mats[0] == skin_mat
        # brows: mirrored thin boxes above the eyes, in front
        if eye_z is not None and i["side"] != "C" and c[2] > eye_z + 0.01 and c[2] < eye_z + 0.07 and front(i) > 0.35 * hs[1] and sz[2] < 0.04:
            i["role"] = "brow"
            face["brows"].append(i)
        elif i["side"] != "C" and is_skin and abs(c[0]) > 0.4 * hs[0] and front(i) < 0.2 * hs[1]:
            i["role"] = "ear"
            face["ears"].append(i)
        elif i["side"] == "C" and eye_z is not None and c[2] < eye_z - 0.05 and front(i) > 0.35 * hs[1] and sz[2] < 0.03 and not is_skin:
            i["role"] = "mouth"
            face["mouth"] = i
        elif i["side"] == "C" and is_skin and front(i) > 0.35 * hs[1]:
            i["role"] = "nose"
            face["nose"] = i
        elif eye_z is not None and c[2] < eye_z - 0.03 and not is_skin and front(i) > 0.1 * hs[1]:
            i["role"] = "beard"
            face["beard"].append(i)
        elif not is_skin and sz[0] > 1.15 * hs[0] and sz[2] > 0.9 * hs[2]:
            i["role"] = "hood"
            face.setdefault("hood", []).append(i)
        elif c[2] >= (eye_z if eye_z is not None else hc[2]) - 0.01 and not is_skin:
            i["role"] = "hair"
            face["hair"].append(i)
        else:
            i["role"] = "head_other"
            face["other"].append(i)
    return head, face, eye_z


def _classify_body(body, legs, cloth, prop, H):
    """Rule-based role and bone suggestions for Body, Legs, Cloth, Prop. Returns landmarks dict."""
    lm = {}
    allb = body + legs
    # skin material: the neck piece = highest centred small Body island below the head
    neck = None
    cands = [i for i in body if i["side"] == "C" and i["size"][0] < 0.2 and 0.76 * H < i["centre"][2] < 0.93 * H]
    if cands:
        neck = max(cands, key=lambda i: (i["centre"][2], -i["index"]))
    skin_mat = neck["materials"][0] if neck else None
    lm["skin_material"] = skin_mat

    # ---- legs ----
    leg_side = {"L": [], "R": []}
    for i in legs:
        if i["side"] in leg_side:
            leg_side[i["side"]].append(i)
        else:
            i["role"] = "skirt_hem"
            i["bone"] = "Hips"
    hip_z = None
    knee = {}
    ankle = {}
    for s, lst in leg_side.items():
        if not lst:
            continue
        lst.sort(key=lambda i: -i["centre"][2])
        thigh = lst[0]
        foot = min(lst, key=lambda i: (i["centre"][2], i["index"]))
        thigh["role"] = "thigh"
        hip_z = max(hip_z or 0.0, thigh["max"][2])
        calf_cands = [i for i in lst if i is not thigh and i is not foot]
        calf = calf_cands[0] if calf_cands else None
        knee_z = (thigh["min"][2] + (calf["max"][2] if calf else thigh["min"][2])) / 2 if calf else thigh["min"][2]
        knee[s] = knee_z
        ankle[s] = foot["max"][2]
        foot["role"] = "foot"
        for i in lst:
            if i is thigh:
                i["bone"] = "UpperLeg." + s
            elif i is foot:
                i["bone"] = "Foot." + s
                i["role"] = "foot"
            else:
                i.setdefault("role", "shin_or_boot")
                i["bone"] = "UpperLeg." + s if i["centre"][2] > knee_z else "LowerLeg." + s
                if i["role"] == "shin_or_boot" and i["size"][2] < 0.06 and i["aspect"] < 0.5:
                    i["role"] = "cuff_or_boot_top"
    lm["hip_z"] = r(hip_z or 0.0)
    lm["knee_z"] = {s: r(v) for s, v in knee.items()}
    lm["ankle_z"] = {s: r(v) for s, v in ankle.items()}
    hz = hip_z or 0.5 * H

    # ---- arms and torso ----
    hz0 = hip_z or 0.5 * H
    centred = [i for i in body if i["side"] == "C"]
    torso = max([i for i in centred if i["centre"][2] > hz0], key=lambda i: i["size"][0] * i["size"][2] * i["size"][1], default=None)
    for i in centred:   # long garments: a centred piece that reaches far below the hips is a robe/skirt
        if i is not torso and i["min"][2] < 0.7 * hz0 and i["size"][0] > 0.3:
            i["role"] = "robe_or_skirt"
            i["bone"] = "Hips"
    if neck:
        neck["role"] = "neck"
        neck["bone"] = "Neck"
    if torso:
        torso["role"] = "torso"
        torso["bone"] = "Chest"
        half = torso["size"][0] / 2
    else:
        half = 0.2
    neck_z = neck["centre"][2] if neck else 0.85 * H
    arms = {"L": [], "R": []}
    for i in body:
        if i is neck or i is torso or i.get("role") == "robe_or_skirt":
            continue
        if i["side"] in arms and abs(i["centre"][0]) > 0.4 * half and i["centre"][2] > 0.45 * H and i["centre"][2] < neck_z + 0.05 and abs(i["centre"][0]) > 0.165:
            arms[i["side"]].append(i)
        elif i["side"] == "C" or abs(i["centre"][0]) <= 0.165:
            # centred or small off-centre details: belts, pouches, buckles, waist, skirt tops
            z = i["centre"][2]
            i["role"] = "torso_detail"
            i["bone"] = "Hips" if z < hz else ("Spine" if z < hz + 0.28 * (neck_z - hz) else "Chest")
        else:
            i["role"] = "body_other"
            i["bone"] = "Chest"
    shoulder = {}
    elbow = {}
    wrist = {}
    for s, lst in arms.items():
        if not lst:
            continue
        lst.sort(key=lambda i: (-i["centre"][2], i["index"]))
        # hand: lowest island with all sizes >= 0.05 (a sphere), thumb = small islands next to it
        big = [i for i in lst if min(i["size"]) >= 0.05]
        hand = min(big, key=lambda i: (i["centre"][2], i["index"])) if big else lst[-1]
        hand["role"] = "hand"
        hand["bone"] = "Hand." + s
        shoulder_cap = lst[0]
        top_z = max(i["max"][2] for i in lst)
        wrist_z = hand["max"][2]
        shoulder_z = shoulder_cap["centre"][2]
        elbow_z = (shoulder_z + wrist_z) / 2
        shoulder[s] = shoulder_z
        elbow[s] = elbow_z
        wrist[s] = wrist_z
        for i in lst:
            if i is hand:
                continue
            near_hand = (abs(i["centre"][0] - hand["centre"][0]) < 0.06 and abs(i["centre"][2] - hand["centre"][2]) < 0.08 and i["size"][2] < 0.09)
            is_skin_piece = bool(skin_mat) and bool(i["materials"]) and i["materials"][0] == skin_mat
            if near_hand and is_skin_piece:
                i["role"] = "thumb"
                i["bone"] = "Hand." + s
            elif near_hand:
                i["role"] = "cuff"
                i["bone"] = "LowerArm." + s
            elif i is shoulder_cap and i["tris"] >= 40:
                i["role"] = "shoulder_cap"
                i["bone"] = "UpperArm." + s
            elif i["centre"][2] >= elbow_z:
                i["role"] = "upper_arm_piece"
                i["bone"] = "UpperArm." + s
            else:
                i["role"] = "lower_arm_piece"
                i["bone"] = "LowerArm." + s
        lm.setdefault("arm_top_z", {})[s] = r(top_z)
    lm["shoulder_z"] = {s: r(v) for s, v in shoulder.items()}
    lm["elbow_z"] = {s: r(v) for s, v in elbow.items()}
    lm["wrist_z"] = {s: r(v) for s, v in wrist.items()}
    # arm angle off vertical: shoulder cap centre to hand centre
    ang = {}
    for s in ("L", "R"):
        caps = [i for i in body if i.get("role") == "shoulder_cap" and i["side"] == s]
        hands = [i for i in body if i.get("role") == "hand" and i["side"] == s]
        if hands:
            h = hands[0]
            src = caps[0] if caps else None
            if src is None:
                ups = [i for i in body if i.get("role") == "upper_arm_piece" and i["side"] == s]
                src = ups[0] if ups else None
            if src:
                dx = abs(h["centre"][0] - src["centre"][0])
                dz = src["centre"][2] - h["centre"][2]
                ang[s] = r(math.degrees(math.atan2(dx, dz)), 1)
    lm["arm_angle_deg_from_vertical"] = ang
    lm["neck_z"] = r(neck_z)
    # ---- cloth (cape, hood, scarf, mantle) ----
    torso_cy = torso["centre"][1] if torso else 0.0
    for i in cloth:
        c = i["centre"]
        i["role"] = "cloth_piece"
        if c[1] > torso_cy + 0.06 and c[2] < neck_z + 0.02:
            i["role"] = "cloth_back"
            i["bone"] = "Cape.1"
        elif c[2] > neck_z + 0.02:
            i["role"] = "hood_or_collar"
            i["bone"] = "Hood.1"
        elif c[1] < torso_cy - 0.05 and i["size"][1] < 0.06:
            i["role"] = "cloth_front_or_trim"
            i["bone"] = "Chest"
        else:
            i["role"] = "cloth_shoulder_or_wrap"
            i["bone"] = "Chest"
    # ---- prop ----
    hands = [i for i in body if i.get("role") == "hand"]
    prop_info = None
    if prop:
        allpts = np.concatenate([p["_pts"] for p in prop], axis=0)
        mn, mx = allpts.min(axis=0), allpts.max(axis=0)
        centre = (mn + mx) / 2
        best = None
        for h in hands:
            d = float(np.linalg.norm(_np(h["centre"]) - centre))
            hp = h["_pts"]
            dd = float(np.min(np.linalg.norm(allpts[:, None, :] - hp.mean(axis=0)[None, None, :], axis=2)))
            if best is None or dd < best[0]:
                best = (dd, h)
        side = best[1]["side"] if best else "R"
        hand_c = _np(best[1]["centre"]) if best else centre
        # principal axis of the prop (PCA over all prop vertices), grip = hand centre projected onto the axis segment
        cen = allpts.mean(axis=0)
        _u, _s, vt = np.linalg.svd(allpts - cen, full_matrices=False)
        axis = vt[0]
        if axis[2] < 0:
            axis = -axis
        t = (allpts - cen) @ axis
        t_lo, t_hi = float(t.min()), float(t.max())
        th = float((hand_c - cen) @ axis)
        th = min(max(th, t_lo), t_hi)
        grip = cen + axis * th
        glow = [p for p in prop if p.get("emissive")]
        if glow:
            head_pt = _np(glow[0]["centre"])
            head_kind = "emissive_piece"
        else:
            head_pt = cen + axis * (t_hi if abs(t_hi - th) >= abs(t_lo - th) else t_lo)
            head_kind = "far_end_from_grip"
        tail_pt = cen + axis * (t_lo if np.linalg.norm((cen + axis * t_lo) - head_pt) >= np.linalg.norm((cen + axis * t_hi) - head_pt) else t_hi)
        topi = int(np.argmax(allpts[:, 2]))
        botI = int(np.argmin(allpts[:, 2]))
        prop_info = {
            "holding_hand": side,
            "bone": "Hand." + side,
            "axis": rv(axis),
            "grip_point": rv(grip), "head_point": rv(head_pt), "head_point_rule": head_kind, "tail_point": rv(tail_pt),
            "top_point": rv(allpts[topi]), "bottom_point": rv(allpts[botI]),
            "bounds_min": rv(mn), "bounds_max": rv(mx),
            "length": r(float(t_hi - t_lo)),
            "grip_to_head_m": r(float(np.linalg.norm(head_pt - grip))),
            "pieces": [p["id"] for p in prop],
        }
        for p in prop:
            p["role"] = "prop_piece"
            p["bone"] = "Hand." + side
    lm["prop"] = prop_info
    return lm


def _nested(parts_isl):
    """Mark islands whose bounding box lies fully inside another island's box (hidden/under-sleeve pieces)."""
    flat = [(pn, i) for pn, lst in parts_isl.items() for i in lst]
    for pn, a in flat:
        amin, amax = _np(a["min"]), _np(a["max"])
        host = None
        for pn2, b in flat:
            if b is a:
                continue
            bmin, bmax = _np(b["min"]), _np(b["max"])
            if _contained(amin, amax, bmin, bmax):
                vol_a = float(np.prod(np.maximum(amax - amin, 1e-9)))
                vol_b = float(np.prod(np.maximum(bmax - bmin, 1e-9)))
                if vol_b > vol_a * 1.05 and (host is None or vol_b < host[1]):
                    host = (b["id"], vol_b)
        if host:
            a["nested_in"] = host[0]


def _adjacency(parts_isl, max_gap=0.03):
    flat = [i for lst in parts_isl.values() for i in lst]
    rows = []
    for a_idx in range(len(flat)):
        a = flat[a_idx]
        amin, amax = _np(a["min"]), _np(a["max"])
        for b_idx in range(a_idx + 1, len(flat)):
            b = flat[b_idx]
            bmin, bmax = _np(b["min"]), _np(b["max"])
            gap = _box_gap(amin, amax, bmin, bmax)
            if gap > max_gap:
                continue
            ov = _overlap_frac(amin, amax, bmin, bmax)
            sg, pen = surface_relation(a, b)
            rows.append({"a": a["id"], "b": b["id"], "bbox_gap": r(gap, 4), "surface_gap": r(sg, 4), "penetration": r(pen, 4), "overlap": r(ov, 3)})
    return rows


def _joint_gaps(parts_isl, adjacency):
    """Skeletal joints only (Body, Legs, Head pieces; cloth and props excluded).
    Pairs of adjacent islands whose suggested bones are chain neighbours, rolled up per joint:
      coverage_m = deepest interpenetration of the best pair (how far a piece reaches into its neighbour)
      gap_m      = smallest open distance between pieces when none interpenetrate
      status     = covered (>= 2 cm of reach), touching (reach under 2 cm or gap under 5 mm), open (gap >= 5 mm)
    A rigid bend of angle A at a 'touching' joint opens a crack of roughly radius * sin(A); 'covered' joints
    hide bends up to roughly asin(coverage / radius)."""
    by_id = {i["id"]: i for lst in parts_isl.values() for i in lst}
    chain = {("UpperArm", "LowerArm"): "elbow", ("LowerArm", "Hand"): "wrist", ("Chest", "UpperArm"): "shoulder",
             ("Hips", "UpperLeg"): "hip", ("Chest", "UpperLeg"): "hip", ("Spine", "UpperLeg"): "hip", ("UpperLeg", "LowerLeg"): "knee", ("LowerLeg", "Foot"): "ankle",
             ("Chest", "Neck"): "neck_base", ("Neck", "Head"): "neck_head", ("Spine", "Chest"): "spine_chest",
             ("Hips", "Spine"): "hips_spine"}

    def base(b):
        return (b or "").split(".")[0]

    def sidec(b):
        t = (b or "").split(".")
        return t[1] if len(t) > 1 and t[1] in ("L", "R") else ""

    pairs = []
    per = {}
    for row in adjacency:
        a, b = by_id[row["a"]], by_id[row["b"]]
        if a["id"].split(".")[0] in ("Cloth", "Prop") or b["id"].split(".")[0] in ("Cloth", "Prop"):
            continue
        ba, bb = a.get("bone"), b.get("bone")
        if not ba or not bb or ba == bb:
            continue
        sa, sb = sidec(ba), sidec(bb)
        if sa and sb and sa != sb:
            continue
        name = chain.get((base(ba), base(bb))) or chain.get((base(bb), base(ba)))
        if not name:
            continue
        side = sa or sb
        jn = name + ("." + side if side else "")
        pairs.append({"a": a["id"], "b": b["id"], "joint": jn, "bones": [ba, bb], "bbox_gap": row["bbox_gap"],
                      "surface_gap": row["surface_gap"], "penetration": row["penetration"], "overlap": row["overlap"]})
        j = per.setdefault(jn, {"coverage_m": 0.0, "gap_m": 1e9, "pairs": 0})
        j["pairs"] += 1
        j["coverage_m"] = max(j["coverage_m"], row["penetration"])
        if row["penetration"] <= 0:
            j["gap_m"] = min(j["gap_m"], row["surface_gap"])
    joints = {}
    for jn, j in sorted(per.items()):
        cov, gap = j["coverage_m"], j["gap_m"]
        if cov >= 0.02:
            status = "covered"
        elif cov > 0 or gap < 0.005:
            status = "touching"
        else:
            status = "open"
        joints[jn] = {"status": status, "coverage_m": r(cov, 4), "gap_m": (r(gap, 4) if cov <= 0 else 0.0), "pairs": j["pairs"]}
    expected = ["elbow.L", "elbow.R", "wrist.L", "wrist.R", "shoulder.L", "shoulder.R", "hip.L", "hip.R", "knee.L", "knee.R",
                "ankle.L", "ankle.R"]
    return {"joints": joints, "pairs": pairs,
            "not_covered": sorted(k for k, v in joints.items() if v["status"] != "covered"),
            "missing_joints": [k for k in expected if k not in joints]}


def _eye_report(face, head, eye_z):
    rep = {"found": bool(face["eyes"])}
    if not face["eyes"]:
        return rep
    sides = {}
    for s, layers in face["eyes"].items():
        outer = layers[0]
        sz = _np(outer["size"])
        ratio = float(sz.min() / sz.max())
        shape = "round" if ratio >= 0.7 else ("disc" if ratio >= 0.3 else "plate")
        scl = layers[1] if len(layers) > 1 else outer
        iris = layers[2] if len(layers) > 2 else None
        pupil = layers[3] if len(layers) > 3 else None
        front_of = lambda i: i["min"][1]  # forward is -Y: the front-most point is min Y
        proud = None
        if iris:
            proud = r(front_of(scl) - front_of(iris) if front_of(iris) < front_of(scl) else 0.0, 4)
        sides[s] = {
            "layers": [{"id": L["id"], "role": L["role"], "size": L["size"], "centre": L["centre"], "material": L["materials"][0]} for L in layers],
            "outer_size": outer["size"], "depth_over_width": r(float(sz[1] / max(sz[0], sz[2])), 3),
            "min_over_max": r(ratio, 3), "shape": shape,
            "iris_proud_of_sclera_m": proud,
            "centre": outer["centre"],
        }
    L, R = sides.get("L"), sides.get("R")
    shapes = {v["shape"] for v in sides.values()}
    if len(shapes) == 1:
        shape = shapes.pop()
    else:
        shape = sorted(shapes)[0]
    spacing = None
    if L and R:
        spacing = r(abs(L["centre"][0] - R["centre"][0]), 4)
    pivot_back = 0.025
    rep.update({"sides": sides, "shape": shape, "eye_spacing": spacing,
                "pivot_offset_back_m": pivot_back,
                "gaze_method": "bone" if shape in ("round", "disc") else "shapekey",
                "carried_by_eye_bone": ["iris", "pupil"],
                "eye_z": r(eye_z)})
    return rep


# ----------------------------------------------------------------------------------------
# Entry points
# ----------------------------------------------------------------------------------------
def build_island_map(ctx_or_objs, key):
    """ctx_or_objs: BuildContext (parts keyed by contract names) or {"Body": obj, ...}.
    Returns the geometry-free island map dict. Parts must be un-renamed or renamed; both work."""
    objs = ctx_or_objs.parts if hasattr(ctx_or_objs, "parts") else ctx_or_objs
    info = contract.CAST_BY_KEY[key]
    described = {p: describe_part(o, p) for p, o in objs.items()}
    # character height = highest point of Body/Legs/Head (props excluded)
    top = max(i["max"][2] for p in ("Body", "Legs", "Head") for i in described[p])
    H_body = top
    for lst in described.values():
        for i in lst:
            if any("_Ember" in m for m in i["materials"]):
                i["emissive"] = True
    if info["rig"] == "demon":
        return _demon_map(described, key, info, H_body)
    head_skin, face, eye_z = _classify_head(described["Head"], H_body)
    body = described["Body"]
    legs = described["Legs"]
    cloth = described.get("Cloth", [])
    prop = described.get("Prop", [])
    lm = _classify_body(body, legs, cloth, prop, H_body)
    # head bones
    for i in described["Head"]:
        role = i.get("role", "")
        if role in ("eye_iris", "eye_pupil"):
            i["bone"] = "Eye." + i["side"]
        elif role == "hood":
            i["bone"] = "Hood.1"
        elif role in ("mouth", "beard"):
            i["bone"] = "Jaw"
        elif role in ("eye_outline", "eye_sclera"):
            i["bone"] = "Head"
        else:
            i["bone"] = "Head"
    lm["head_bottom_z"] = r(head_skin["min"][2])
    lm["head_top_z_skin"] = r(head_skin["max"][2])
    lm["crown_z"] = r(max(i["max"][2] for i in described["Head"]))
    lm["head_centre"] = head_skin["centre"]
    lm["head_size"] = head_skin["size"]
    forward_checks = {}
    if face["eyes"]:
        eye_y = float(np.mean([L[0]["centre"][1] for L in face["eyes"].values()]))
        forward_checks = {"eye_y": r(eye_y), "head_centre_y": head_skin["centre"][1],
                          "faces_minus_y": bool(eye_y < head_skin["centre"][1])}
    face_report = _eye_report(face, head_skin, eye_z) if face["eyes"] else {"found": False}
    torso_c = [i for i in described["Body"] if i.get("role") == "torso"]
    if torso_c:
        forward_checks["head_centre_y_minus_torso_y"] = r(head_skin["centre"][1] - torso_c[0]["centre"][1])
    face_report["forward_check"] = forward_checks
    face_report["brows"] = [{"id": i["id"], "size": i["size"], "centre": i["centre"], "side": i["side"]} for i in face["brows"]]
    face_report["mouth"] = ({"id": face["mouth"]["id"], "size": face["mouth"]["size"], "centre": face["mouth"]["centre"]} if face["mouth"] else None)
    face_report["nose"] = ({"id": face["nose"]["id"], "size": face["nose"]["size"], "centre": face["nose"]["centre"]} if face["nose"] else None)
    face_report["ears"] = [i["id"] for i in face["ears"]]
    face_report["hair"] = [i["id"] for i in face["hair"]]
    face_report["hood"] = [i["id"] for i in face.get("hood", [])]
    face_report["beard"] = [i["id"] for i in face["beard"]]
    face_report["head_skin"] = head_skin["id"]
    face_report["unclassified"] = [i["id"] for i in face["other"]]
    face_report["head_islands"] = len(described["Head"])
    # nesting, adjacency, joint gaps
    _nested(described)
    adjacency = _adjacency(described)
    joint = _joint_gaps(described, adjacency)
    # sleeve overlap: arm pieces nested in another arm/shoulder piece, or overlapping neighbours
    sleeve = {"nested_pieces": [i["id"] + "<" + i["nested_in"] for p in ("Body",) for i in described[p] if i.get("nested_in") and (i.get("bone") or "").startswith(("UpperArm", "LowerArm", "Hand"))],
              "overlapping_arm_pairs": sum(1 for j in joint["pairs"] if j["bones"][0].startswith(("UpperArm", "LowerArm", "Hand")) and j["bones"][1].startswith(("UpperArm", "LowerArm", "Hand")) and j["overlap"] > 0)}
    parts_out = {}
    for p, lst in described.items():
        tri = sum(i["tris"] for i in lst)
        vts = sum(i["verts"] for i in lst)
        clean = []
        for i in lst:
            d = {k: v for k, v in i.items() if not k.startswith("_")}
            clean.append(d)
        parts_out[p] = {"tris": tri, "verts": vts, "island_count": len(lst), "islands": clean}
    out = {
        "schema": 1,
        "key": key,
        "number": info["number"],
        "display": info["display"],
        "source_fbx": info["fbx"],
        "forward": contract.AUTHORING_FORWARD,
        "left_sign_x": contract.LEFT_SIGN_X,
        "height": r(H_body),
        "height_with_props": r(max([H_body] + [i["max"][2] for i in prop])),
        "landmarks": lm,
        "face": face_report,
        "sleeve_overlap": sleeve,
        "joint_gaps": joint,
        "parts": parts_out,
        "adjacency": adjacency,
    }
    return out


def _demon_map(described, key, info, H):
    """Demons: pieces are described but NOT classified (WP-13 owns the demon rig). Geometry-free facts only."""
    body = described["Body"]
    torso = max(body, key=lambda i: i["size"][0] * i["size"][1] * i["size"][2])
    head = described["Head"]
    hb_min = _np([min(i["min"][k] for i in head) for k in range(3)])
    hb_max = _np([max(i["max"][k] for i in head) for k in range(3)])
    head_c = (hb_min + hb_max) / 2
    for lst in described.values():
        for i in lst:
            i["role"] = None
            i["bone"] = None
    _nested(described)
    adjacency = _adjacency(described)
    glow = [i["id"] for lst in described.values() for i in lst if i.get("emissive")]
    parts_out = {}
    for p, lst in described.items():
        parts_out[p] = {"tris": sum(i["tris"] for i in lst), "verts": sum(i["verts"] for i in lst), "island_count": len(lst),
                        "islands": [{k: v for k, v in i.items() if not k.startswith("_")} for i in lst]}
    return {
        "schema": 1, "key": key, "number": info["number"], "display": info["display"], "source_fbx": info["fbx"],
        "forward": contract.AUTHORING_FORWARD, "left_sign_x": contract.LEFT_SIGN_X,
        "height": r(H), "height_with_props": r(H),
        "classified": False,
        "note": "demon pieces are not classified; roles and bones are null. WP-13 classifies them.",
        "landmarks": {"head_centre": rv(head_c), "head_size": rv(hb_max - hb_min), "torso_island": torso["id"],
                      "tail_or_back_pieces": [i["id"] for i in body if i["centre"][1] > torso["centre"][1] + 0.15]},
        "face": {"found": False, "emissive_islands": glow,
                 "forward_check": {"head_centre_y": r(head_c[1]), "torso_centre_y": torso["centre"][1],
                                   "faces_minus_y": bool(head_c[1] < torso["centre"][1])}},
        "parts": parts_out, "adjacency": adjacency,
    }


def signature(obj):
    """Cheap fingerprint of the split order: [(faces, verts)] per island. Used to detect a changed input."""
    isl, _ = vertex_islands(obj)
    return [(len(g["faces"]), len(g["verts"])) for g in isl]


def check_map_matches(island_map, ctx):
    """True if the committed island map still describes this scene's parts (same island count and sizes)."""
    for pn, o in ctx.parts.items():
        sig = signature(o)
        want = [(i["faces"], i["verts"]) for i in island_map["parts"][pn]["islands"]]
        if sig != want:
            return False
    return True
