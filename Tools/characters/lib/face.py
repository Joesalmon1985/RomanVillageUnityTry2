"""Face rig (WP-3): face differentiation, Head shape keys, face-bone fit, manifest `face` section.

Pipeline step (contract, README 2.8):  face.build_face(ctx)
  runs after rig.fit_rig and skin.skin_character (or lib/stub_rig.py while WP-2 is pending), on a scene made by
  common.load_character. It reads the committed island map (ctx.islands) and the per-character data file
  Tools/characters/cast/<key>.json ("face" block). Every human uses the same code; a character without a data
  file gets DEFAULTS (no differentiation, default expressions).

What it does, in order:
  1. refine_mouth   The supplied mouth is a straight 5-sided bar with vertices only at its two ends, so it cannot
                    curve. Four cuts along its length give it five segments (+40 tris). New vertices copy the
                    vertex-group weights of the mouth's original vertices (Jaw), so WP-2's skin stays complete.
                    New vertices are appended, so every other island keeps its vertex indices.
  2. differentiate  Moves Head pieces from data (face width, jaw width and length, chin, brow ridge, brow weight,
                    angle and height, eye size, height and spacing, nose size, mouth width, hairline, ears).
                    Base-mesh edit before any shape key exists; vertex count and order unchanged.
  3. fit_face_bones If an armature exists, moves Eye.L/R onto the moved eyes (contract: head = eye outline centre
                    0.025 m back, tail 0.06 m forward, roll rule) and moves Jaw by the mouth's displacement.
  4. author_shape_keys  Basis + the ten contract keys, procedurally from the island map:
                    Blink.L/R   eye stack (outline, sclera, iris, pupil) collapses onto a lid line: the outline
                                becomes a thin dark lash band, sclera/iris/pupil flatten and tuck behind it, skin
                                shows above. Partial weights narrow the eye from the top.
                    BrowRaise.L/R  brow up, inner end a bit more.
                    BrowFurrow  both brows: inner ends down and in, brought forward over the eye.
                    BrowWorry   both brows: inner ends up, outer ends slightly down.
                    MouthSmile / MouthFrown  corners up / down along a curve (needs step 1).
                    MouthRound  mouth narrows and grows tall.
                    MouthOpen   lower lip line drops in a lens shape.
  5. manifest       ctx.set_section("face", ...): gaze method, eye bones and limits, shape keys, expressions,
                    tuning, differentiation actually applied.

All lengths in the data are metres on the reference head (Prince, head height 0.258 m) and are scaled by the
character's head height, so one data set fits a bigger head.

No geometry is written to the repository: data files hold only numbers.
"""

import copy
import json
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import contract  # noqa: E402
import common  # noqa: E402

try:
    import bpy  # noqa: F401
    import bmesh  # noqa: F401
    import numpy as np  # noqa: F401
    from mathutils import Vector  # noqa: F401
except ImportError:      # plain python: data helpers (load_face_data, DEFAULTS) still work
    bpy = None

FACE_SCHEMA = "face/1"
REFERENCE_HEAD_HEIGHT = 0.258        # Prince head-skin height; data lengths are in this unit
EYE_PIVOT_BACK = 0.025               # contract (README 2.4)
EYE_BONE_LENGTH = 0.06
MOUTH_CUTS = 4
# Integration fix (WP-3 found thin brows vanish at a head about 53 px tall in firelight): a brow piece is never
# thinner than this (vertical extent, metres on the reference head), whatever the character's brow_thickness says.
BROW_MIN_HEIGHT = 0.022

# ---------------------------------------------------------------------------------------------------------------
# Defaults. A cast/<key>.json "face" block overrides any of these (deep merge, numbers only).
# ---------------------------------------------------------------------------------------------------------------
DEFAULT_DIFFERENTIATION = {
    # head shape
    "face_width": 1.0,          # x scale of skin, ears, hair, hood about the centre line
    "jaw_width": 1.0,           # extra x scale of the lower face, full at the chin, zero at nose height
    "chin_drop_m": 0.0,         # chin ring moves down (longer face) ; negative = shorter
    "chin_forward_m": 0.0,      # lower front of the face moves forward (-Y)
    "brow_ridge_m": 0.0,        # skin just above the eyes moves forward (heavier brow ridge)
    "cheek_width": 1.0,         # x scale of the widest skin ring (cheekbones) on top of face_width
    # brows
    "brow_thickness": 1.0,      # vertical scale of each brow piece
    "brow_length": 1.0,         # length scale about the brow centre
    "brow_angle_deg": 0.0,      # extra tilt; + = inner end down (stern), - = inner end up (soft, arched)
    "brow_height_m": 0.0,       # + = up
    "brow_forward_m": 0.0,      # + = forward (-Y)
    "brow_gap_m": 0.0,          # + = brows further apart (each moves out)
    # eyes (the whole four-disc stack moves and scales together, so iris/sclera proportions stay)
    "eye_size": 1.0,            # uniform scale of the eye stack about the outline centre
    "eye_height": 1.0,          # extra vertical scale (below 1 = narrower, heavier lidded)
    "eye_spacing_m": 0.0,       # + = each eye moves outward by this much
    "eye_height_m": 0.0,        # + = eyes (and brows) up
    # nose
    "nose_size": 1.0,           # uniform scale about the nose's back-centre
    "nose_length": 1.0,         # extra vertical scale
    "nose_width": 1.0,          # extra horizontal scale
    "nose_projection": 1.0,     # extra depth scale (how far it sticks out)
    "nose_height_m": 0.0,
    # mouth
    "mouth_width": 1.0,
    "mouth_thickness": 1.0,
    "mouth_height_m": 0.0,
    # hair
    "hairline_m": 0.0,          # + = front hair pieces move down (lower hairline), - = up (higher forehead)
    "hair_forward_m": 0.0,      # front hair pieces move forward (-Y)
    "ear_scale": 1.0,
}

# Amplitudes of the shape keys at weight 1, metres on the reference head. Shared by all humans; a character may
# override a few in its data ("shape_key_amplitude").
DEFAULT_AMPLITUDE = {
    "blink_lid_line": 0.30,          # lid line, fraction of the outline half-height below the eye centre
    "blink_lash_band": 0.22,         # closed outline height as a fraction of its open height
    "blink_tuck_m": 0.010,           # sclera/iris/pupil move back behind the lash band at full blink
    "brow_raise_m": 0.0125,
    "brow_raise_inner_extra": 0.30,  # inner end rises this much more (fraction)
    "brow_furrow_down_m": 0.0095,
    "brow_furrow_in_m": 0.0045,
    "brow_furrow_forward_m": 0.011,
    "brow_worry_up_m": 0.011,
    "brow_worry_outer_down_m": 0.003,
    "brow_worry_in_m": 0.002,
    "mouth_smile_up_m": 0.0085,
    "mouth_smile_out_m": 0.004,
    "mouth_frown_down_m": 0.0075,
    "mouth_round_width": 0.50,       # x scale at weight 1
    "mouth_round_height": 2.6,       # z scale at weight 1
    "mouth_open_m": 0.018,           # lower lip line drop at the centre
    "mouth_forward_m": 0.003,        # open/round mouths come forward so they never sink into the skin
}

# Expression presets (contract.EXPRESSIONS). Values: shape-key weights 0..1, `jaw_deg` (Jaw opening, degrees),
# `gaze` ("target" = eyes on the person addressed, "down", "away", "free"), `blink_rate` multiplier.
DEFAULT_EXPRESSIONS = {
    "neutral": {"keys": {}, "jaw_deg": 0.0, "gaze": "target", "blink_rate": 1.0},
    "attentive": {"keys": {"BrowRaise.L": 0.7, "BrowRaise.R": 0.7, "BrowWorry": 0.15, "MouthSmile": 0.25},
                  "jaw_deg": 0.0, "gaze": "target", "blink_rate": 0.8},
    "concentrating": {"keys": {"BrowFurrow": 0.85, "Blink.L": 0.4, "Blink.R": 0.4, "MouthFrown": 0.1},
                      "jaw_deg": 0.0, "gaze": "target", "blink_rate": 0.5},
    "surprised": {"keys": {"BrowRaise.L": 1.0, "BrowRaise.R": 1.0, "BrowWorry": 0.25, "MouthRound": 0.6,
                           "MouthOpen": 0.75}, "jaw_deg": 3.0, "gaze": "target", "blink_rate": 0.2},
    "pleased": {"keys": {"MouthSmile": 0.9, "BrowRaise.L": 0.25, "BrowRaise.R": 0.25, "Blink.L": 0.15,
                         "Blink.R": 0.15}, "jaw_deg": 0.0, "gaze": "target", "blink_rate": 1.0},
    "pained": {"keys": {"BrowWorry": 0.9, "BrowFurrow": 0.3, "Blink.L": 0.45, "Blink.R": 0.45, "MouthFrown": 0.6,
                        "MouthOpen": 0.25}, "jaw_deg": 1.5, "gaze": "down", "blink_rate": 1.4},
    "determined": {"keys": {"BrowFurrow": 0.5, "MouthFrown": 0.6},
                   "jaw_deg": 0.0, "gaze": "target", "blink_rate": 0.6},
}

DEFAULT_TUNING = {
    # runtime multipliers per channel (FaceController scales preset weights by these, then clamps to 0..1)
    "gain": {"blink": 1.0, "brow": 1.0, "mouth": 1.0, "jaw": 1.0},
    # blink behaviour (PRD quality table: blink every 2.5 to 6 s)
    "blink": {"interval_s": [2.5, 6.0], "double_chance": 0.15, "close_s": 0.06, "hold_s": 0.03, "open_s": 0.10},
    # gaze share rule (PRD 5.4): eyes take what they can, head ~40 %, chest ~15 %, eyes arrive first
    "gaze": {"eye_yaw_limit_deg": 12.0, "eye_pitch_limit_deg": 5.0, "head_share": 0.40, "chest_share": 0.15,
             "head_yaw_limit_deg": 55.0, "head_pitch_limit_deg": 25.0, "eye_lead_s": 0.06, "head_lag_s": 0.15,
             "saccade_s": 0.05},
    # talking: a jaw-and-mouth ticker while a speech bubble shows
    "talk": {"key": "MouthOpen", "weight": [0.15, 0.55], "jaw_deg": [0.0, 2.0], "rate_hz": 7.0},
}

DEFAULTS = {
    "differentiation": DEFAULT_DIFFERENTIATION,
    "shape_key_amplitude": DEFAULT_AMPLITUDE,
    "expressions": DEFAULT_EXPRESSIONS,
    "tuning": DEFAULT_TUNING,
    "island_roles": {},       # optional per-character role overrides, e.g. {"Head.19": "hair"}
}

KEY_CHANNEL = {"Blink.L": "blink", "Blink.R": "blink", "BrowRaise.L": "brow", "BrowRaise.R": "brow",
               "BrowFurrow": "brow", "BrowWorry": "brow", "MouthSmile": "mouth", "MouthFrown": "mouth",
               "MouthRound": "mouth", "MouthOpen": "mouth"}


# ---------------------------------------------------------------------------------------------------------------
# Data
# ---------------------------------------------------------------------------------------------------------------
def _merge(base, over):
    out = copy.deepcopy(base)
    for k, v in (over or {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            # expressions replace whole presets (a preset is a unit), everything else merges
            out[k] = _merge(out[k], v) if k not in DEFAULT_EXPRESSIONS else copy.deepcopy(v)
        else:
            out[k] = copy.deepcopy(v)
    return out


def face_data_path(key, cast_dir=None):
    return os.path.join(cast_dir or common.CAST_DIR, key + ".json")


def load_face_data(key, cast_dir=None):
    """DEFAULTS merged with cast/<key>.json "face". Validates names against the contract."""
    path = face_data_path(key, cast_dir)
    raw = common.read_json(path) if os.path.isfile(path) else {}
    face = _merge(DEFAULTS, raw.get("face", {}))
    unknown = set(face["differentiation"]) - set(DEFAULT_DIFFERENTIATION)
    if unknown:
        raise ValueError("%s: unknown differentiation parameter(s) %s" % (key, sorted(unknown)))
    unknown = set(face["shape_key_amplitude"]) - set(DEFAULT_AMPLITUDE)
    if unknown:
        raise ValueError("%s: unknown amplitude(s) %s" % (key, sorted(unknown)))
    for name, ex in face["expressions"].items():
        if name not in contract.EXPRESSIONS:
            raise ValueError("%s: expression %r not in contract.EXPRESSIONS" % (key, name))
        for k, w in ex.get("keys", {}).items():
            if k not in contract.SHAPE_KEYS:
                raise ValueError("%s: expression %s uses unknown shape key %r" % (key, name, k))
            if not 0.0 <= float(w) <= 1.0:
                raise ValueError("%s: expression %s key %s weight %s outside 0..1" % (key, name, k, w))
    missing = [e for e in contract.EXPRESSIONS if e not in face["expressions"]]
    if missing:
        raise ValueError("%s: expressions missing %s" % (key, missing))
    face["_source"] = os.path.relpath(path, common.TOOLS_DIR).replace("\\", "/") if raw else None
    face["_raw"] = raw
    return face


# ---------------------------------------------------------------------------------------------------------------
# Head pieces
# ---------------------------------------------------------------------------------------------------------------
class HeadPieces:
    """Vertex index sets per role on the Head mesh, matched to the island map by island order."""

    def __init__(self, ctx, face):
        import islands as _islands
        self.obj = ctx.parts["Head"]
        mw = self.obj.matrix_world
        if any(abs(mw[i][j] - (1.0 if i == j else 0.0)) > 1e-6 for i in range(4) for j in range(4)):
            raise RuntimeError("Head object transform must be identity (load_character keeps scale 1, rot 0)")
        imap = ctx.islands
        if imap is None:
            raise RuntimeError("no island map for %s" % ctx.key)
        mapped = imap["parts"]["Head"]["islands"]
        groups, _co = _islands.vertex_islands(self.obj)
        if len(groups) != len(mapped):
            raise RuntimeError("%s: Head has %d islands, island map has %d (input changed?)"
                               % (ctx.key, len(groups), len(mapped)))
        self.by_id = {}
        overrides = face.get("island_roles", {})
        for g, m in zip(groups, mapped):
            role = overrides.get(m["id"], m["role"])
            self.by_id[m["id"]] = {"verts": list(g["verts"]), "faces": list(g["faces"]), "role": role,
                                   "side": m["side"], "map": m}
        self.imap = imap
        lm = imap["landmarks"]
        self.head_centre = Vector(lm["head_centre"])
        self.head_size = Vector(lm["head_size"])
        self.unit = self.head_size.z / REFERENCE_HEAD_HEIGHT

    def ids(self, role, side=None):
        return [i for i, d in sorted(self.by_id.items()) if d["role"] == role and (side is None or d["side"] == side)]

    def verts(self, role, side=None):
        out = []
        for i in self.ids(role, side):
            out += self.by_id[i]["verts"]
        return out

    def one(self, role, side=None):
        ids = self.ids(role, side)
        if len(ids) != 1:
            raise RuntimeError("expected one %s island (side %s), found %s" % (role, side, ids))
        return ids[0]

    def eye_stack(self, side):
        return [self.one(r, side) for r in ("eye_outline", "eye_sclera", "eye_iris", "eye_pupil")]


def _co(me):
    a = np.empty(len(me.vertices) * 3, dtype=np.float64)
    me.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def _set_co(me, co):
    me.vertices.foreach_set("co", co.reshape(-1).astype(np.float32))
    me.update()


def _bbox(co, idx):
    p = co[idx]
    return p.min(axis=0), p.max(axis=0), (p.min(axis=0) + p.max(axis=0)) / 2.0


# ---------------------------------------------------------------------------------------------------------------
# 1. Mouth refinement
# ---------------------------------------------------------------------------------------------------------------
def refine_mouth(ctx, pieces, cuts=MOUTH_CUTS):
    """Cut the mouth bar along its length so smile and frown can curve. Returns a report dict."""
    obj = pieces.obj
    me = obj.data
    if me.shape_keys:
        raise RuntimeError("Head already has shape keys; build_face runs once on a freshly loaded scene")
    mid = pieces.one("mouth")
    mverts = set(pieces.by_id[mid]["verts"])
    n_before, f_before = len(me.vertices), len(me.polygons)
    # vertex-group weights of the original mouth verts (WP-2 puts the whole mouth on Jaw)
    groups = {}
    for v in sorted(mverts):
        for g in me.vertices[v].groups:
            groups.setdefault(g.group, []).append(g.weight)
    mean_w = {gi: sum(ws) / len(ws) for gi, ws in groups.items()}
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    long_edges = []
    width = max(abs(bm.verts[v].co.x) for v in mverts) * 2
    for e in bm.edges:
        a, b = e.verts
        if a.index in mverts and b.index in mverts and abs(a.co.x - b.co.x) > 0.5 * width:
            long_edges.append(e)
    if not long_edges:
        bm.free()
        return {"refined": False, "reason": "no long mouth edges"}
    bmesh.ops.subdivide_edges(bm, edges=long_edges, cuts=cuts, use_grid_fill=True)
    bm.to_mesh(me)
    bm.free()
    me.update()
    new = list(range(n_before, len(me.vertices)))
    for gi, w in mean_w.items():
        obj.vertex_groups[gi].add(new, w, "REPLACE")
    pieces.by_id[mid]["verts"] = sorted(mverts) + new
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    return {"refined": True, "cuts": cuts, "verts_added": len(new), "faces_added": len(me.polygons) - f_before,
            "head_tris_after": tris, "weights_copied_from_groups": sorted(obj.vertex_groups[g].name for g in mean_w)}


# ---------------------------------------------------------------------------------------------------------------
# 2. Differentiation
# ---------------------------------------------------------------------------------------------------------------
def differentiate(ctx, pieces, P):
    """Apply differentiation parameters P to the base Head mesh. Returns {island_id: displacement of centre}."""
    me = pieces.obj.data
    co = _co(me)
    orig = co.copy()
    u = pieces.unit
    hc = pieces.head_centre
    skin = pieces.verts("head_skin")
    smin, smax, _ = _bbox(orig, skin)
    nose_id = pieces.one("nose")
    _, _, nose_c = _bbox(orig, pieces.by_id[nose_id]["verts"])
    eye_z = pieces.imap["face"]["eye_z"]

    fw = P["face_width"]
    # --- skin: face width, cheek width, jaw width, chin, brow ridge
    zs = orig[skin, 2]
    rings = sorted(set(np.round(zs, 4)))
    widest = max(rings, key=lambda z: np.abs(orig[skin][np.isclose(zs, z, atol=1e-4), 0]).max())
    front_y = smin[1]
    depth = smax[1] - smin[1]
    for v in skin:
        x, y, z = orig[v]
        sx = fw
        if abs(z - widest) < 1e-4:
            sx *= P["cheek_width"]
        top, bot = nose_c[2], smin[2]
        wj = min(1.0, max(0.0, (top - z) / (top - bot)))
        sx *= 1.0 + (P["jaw_width"] - 1.0) * wj
        co[v, 0] = x * sx
        front = max(0.0, 1.0 - (y - front_y) / (0.5 * depth))       # 1 at the face plane, 0 at mid-depth
        if z < top:
            co[v, 2] -= P["chin_drop_m"] * u * wj
            co[v, 1] -= P["chin_forward_m"] * u * wj * front
        # brow ridge: the skin ring nearest above the eyes
        if z > eye_z and abs(z - min([r for r in rings if r > eye_z] or [z])) < 1e-4:
            co[v, 1] -= P["brow_ridge_m"] * u * front

    def move(ids, fn):
        for i in ids:
            vs = pieces.by_id[i]["verts"]
            mn, mx, c = _bbox(orig, vs)
            for v in vs:
                co[v] = fn(orig[v], c, mn, mx, pieces.by_id[i])

    # --- ears: move with the face width, scale
    def ear(p, c, mn, mx, d):
        nc = np.array([c[0] * fw, c[1], c[2]])
        return nc + (p - c) * P["ear_scale"]
    move(pieces.ids("ear"), ear)

    # --- hair, hood, other head pieces: x scale about the centre line; front hair pieces take the hairline
    def hair(p, c, mn, mx, d):
        q = np.array([p[0] * fw, p[1], p[2]])
        if c[1] < hc.y - 0.01 * u:            # in front of the head centre = frames the face
            q[2] -= P["hairline_m"] * u
            q[1] -= P["hair_forward_m"] * u
        return q
    move(pieces.ids("hair") + pieces.ids("beard") + pieces.ids("hood") + pieces.ids("head_other"), hair)

    # --- eyes: whole stack
    eye_shift = {}
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        stack = pieces.eye_stack(side)
        _, _, oc = _bbox(orig, pieces.by_id[stack[0]]["verts"])
        shift = np.array([sgn * P["eye_spacing_m"] * u, 0.0, P["eye_height_m"] * u])
        eye_shift[side] = shift
        for i in stack:
            for v in pieces.by_id[i]["verts"]:
                rel = (orig[v] - oc) * P["eye_size"]
                rel[2] *= P["eye_height"]
                co[v] = oc + rel + shift

    # --- brows
    for i in pieces.ids("brow"):
        d = pieces.by_id[i]
        side = d["side"]
        sgn = 1.0 if side == "L" else -1.0
        vs = d["verts"]
        mn, mx, c = _bbox(orig, vs)
        ang = math.radians(P["brow_angle_deg"])
        thick = max(P["brow_thickness"], BROW_MIN_HEIGHT * pieces.unit / max(1e-6, float(mx[2] - mn[2])))
        d["brow_thickness_applied"] = thick
        for v in vs:
            rel = orig[v] - c
            rel[0] *= P["brow_length"]
            rel[2] *= thick
            # tilt: inner end (towards x = 0) down for + angle
            inward = -sgn * rel[0]                         # + at the inner end
            rel[2] -= math.tan(ang) * inward
            q = c + rel
            q += eye_shift[side]
            q[0] += sgn * P["brow_gap_m"] * u
            q[2] += P["brow_height_m"] * u
            q[1] -= (P["brow_forward_m"] + P["brow_ridge_m"]) * u
            co[v] = q

    # --- nose: scale about its back centre
    def nose(p, c, mn, mx, d):
        back = np.array([c[0], mx[1], c[2]])
        rel = (p - back) * P["nose_size"]
        rel *= np.array([P["nose_width"], P["nose_projection"], P["nose_length"]])
        return back + rel + np.array([0.0, 0.0, P["nose_height_m"] * u])
    move([nose_id], nose)

    # --- mouth
    mid = pieces.one("mouth")
    def mouth(p, c, mn, mx, d):
        rel = p - c
        rel[0] *= P["mouth_width"]
        rel[2] *= P["mouth_thickness"]
        q = c + rel
        q[2] += P["mouth_height_m"] * u
        # keep the mouth proud of the (maybe moved) skin: follow the chin's forward move
        q[1] -= P["chin_forward_m"] * u * 0.5
        return q
    move([mid], mouth)

    _set_co(me, co)
    disp = {}
    for i, d in pieces.by_id.items():
        _, _, c0 = _bbox(orig, d["verts"])
        _, _, c1 = _bbox(co, d["verts"])
        disp[i] = (c1 - c0)
    return disp


def fit_mouth_to_skin(ctx, pieces, clearance_m=0.0015):
    """The supplied mouth is straight but the face is curved, so its middle sinks into the skin (it reads as two
    dashes). Bring every cross-section of the (refined) mouth forward until its front is `clearance_m` proud of the
    head skin. Base-mesh edit, before shape keys. Returns the largest forward move."""
    from mathutils.bvhtree import BVHTree
    me = pieces.obj.data
    co = _co(me)
    skin = set(pieces.verts("head_skin"))
    polys = [list(p.vertices) for p in me.polygons if p.vertices[0] in skin]
    tree = BVHTree.FromPolygons([Vector(c) for c in co], polys)
    mvs = pieces.by_id[pieces.one("mouth")]["verts"]
    sections = {}
    for v in mvs:
        sections.setdefault(round(co[v, 0], 4), []).append(v)
    u = pieces.unit
    worst = 0.0
    for x, vs in sections.items():
        zc = float(np.mean(co[vs, 2]))
        front = float(co[vs, 1].min())
        hit = tree.ray_cast(Vector((x, -0.5, zc)), Vector((0.0, 1.0, 0.0)))
        if hit[0] is None:
            continue
        want = hit[0].y - clearance_m * u
        if front > want:
            co[vs, 1] -= (front - want)
            worst = max(worst, front - want)
    _set_co(me, co)
    return common.r(worst, 4)


# ---------------------------------------------------------------------------------------------------------------
# 3. Face bones follow the moved pieces
# ---------------------------------------------------------------------------------------------------------------
def _roll_axis(y):
    zt = Vector((1.0, 0.0, 0.0)).cross(y)
    return zt if zt.length > 1e-6 else Vector((0.0, 0.0, 1.0))


def fit_face_bones(ctx, pieces, disp):
    arm = ctx.armature
    if arm is None:
        return {"moved": False, "reason": "no armature"}
    me = pieces.obj.data
    co = _co(me)
    targets = {}
    for side in ("L", "R"):
        oid = pieces.eye_stack(side)[0]
        _, _, c = _bbox(co, pieces.by_id[oid]["verts"])
        h = Vector(c) + Vector((0.0, EYE_PIVOT_BACK, 0.0))
        targets["Eye." + side] = ("abs", h, h + Vector((0.0, -EYE_BONE_LENGTH, 0.0)))
    mdisp = Vector(disp[pieces.one("mouth")])
    targets["Jaw"] = ("delta", mdisp, None)
    prev_active = bpy.context.view_layer.objects.active
    prev_mode = arm.mode
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    out = {}
    for name, (kind, a, b) in targets.items():
        eb = arm.data.edit_bones.get(name)
        if eb is None:
            continue
        mw_inv = arm.matrix_world.inverted()
        if kind == "abs":
            eb.head, eb.tail = mw_inv @ a, mw_inv @ b
        else:
            eb.head = eb.head + a
            eb.tail = eb.tail + a
        eb.align_roll(_roll_axis((eb.tail - eb.head).normalized()))
        out[name] = {"head": common.rv(arm.matrix_world @ eb.head), "tail": common.rv(arm.matrix_world @ eb.tail)}
    bpy.ops.object.mode_set(mode=prev_mode if prev_mode in ("OBJECT", "POSE") else "OBJECT")
    bpy.context.view_layer.objects.active = prev_active
    return {"moved": True, "bones": out}


# ---------------------------------------------------------------------------------------------------------------
# 4. Shape keys
# ---------------------------------------------------------------------------------------------------------------
def author_shape_keys(ctx, pieces, A):
    obj = pieces.obj
    me = obj.data
    base = _co(me)
    u = pieces.unit
    obj.shape_key_add(name=contract.SHAPE_KEY_BASIS, from_mix=False)
    targets = {}

    # ---- blink
    for side in ("L", "R"):
        stack = pieces.eye_stack(side)
        omn, omx, oc = _bbox(base, pieces.by_id[stack[0]]["verts"])
        half = (omx[2] - omn[2]) / 2.0
        lid = oc[2] - A["blink_lid_line"] * half
        t = base.copy()
        for k, i in enumerate(stack):
            vs = pieces.by_id[i]["verts"]
            mn, mx, c = _bbox(base, vs)
            for v in vs:
                p = base[v]
                if k == 0:     # outline -> lash band centred on the lid line
                    t[v, 2] = lid + (p[2] - c[2]) * A["blink_lash_band"]
                else:          # sclera, iris, pupil -> flattened onto the lid line, tucked behind the band
                    t[v, 2] = lid + (p[2] - c[2]) * 0.04
                    t[v, 1] = p[1] + A["blink_tuck_m"] * u
        targets["Blink." + side] = t

    # ---- brows
    brows = {}
    for i in pieces.ids("brow"):
        d = pieces.by_id[i]
        mn, mx, c = _bbox(base, d["verts"])
        brows[d["side"]] = (d["verts"], mn, mx, c)

    def inner_t(p, mn, mx, side):
        # 0 at the outer end, 1 at the inner end
        span = mx[0] - mn[0]
        return (mx[0] - p[0]) / span if side == "L" else (p[0] - mn[0]) / span

    for side in ("L", "R"):
        vs, mn, mx, c = brows[side]
        t = base.copy()
        for v in vs:
            it = inner_t(base[v], mn, mx, side)
            t[v, 2] += A["brow_raise_m"] * u * (1.0 + A["brow_raise_inner_extra"] * (it - 0.5))
            t[v, 1] -= 0.001 * u
        targets["BrowRaise." + side] = t

    t = base.copy()
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        vs, mn, mx, c = brows[side]
        for v in vs:
            it = inner_t(base[v], mn, mx, side)
            t[v, 2] -= A["brow_furrow_down_m"] * u * (0.35 + 0.65 * it)
            t[v, 0] -= sgn * A["brow_furrow_in_m"] * u * it
            t[v, 1] -= A["brow_furrow_forward_m"] * u * (0.5 + 0.5 * it)
    targets["BrowFurrow"] = t

    t = base.copy()
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        vs, mn, mx, c = brows[side]
        for v in vs:
            it = inner_t(base[v], mn, mx, side)
            t[v, 2] += A["brow_worry_up_m"] * u * it - A["brow_worry_outer_down_m"] * u * (1.0 - it)
            t[v, 0] -= sgn * A["brow_worry_in_m"] * u * it
            t[v, 1] -= 0.002 * u * it
    targets["BrowWorry"] = t

    # ---- mouth
    mvs = pieces.by_id[pieces.one("mouth")]["verts"]
    mn, mx, mc = _bbox(base, mvs)
    hw = (mx[0] - mn[0]) / 2.0
    t_s, t_f, t_r, t_o = base.copy(), base.copy(), base.copy(), base.copy()
    for v in mvs:
        p = base[v]
        xn = (p[0] - mc[0]) / hw                       # -1 .. 1
        curve = xn * xn
        t_s[v, 2] += A["mouth_smile_up_m"] * u * curve - 0.0008 * u * (1 - curve)
        t_s[v, 0] += A["mouth_smile_out_m"] * u * xn
        t_s[v, 1] += 0.002 * u * curve                 # corners wrap back a little
        t_f[v, 2] -= A["mouth_frown_down_m"] * u * curve - 0.0012 * u * (1 - curve)
        t_f[v, 1] += 0.0015 * u * curve
        t_r[v, 0] = mc[0] + (p[0] - mc[0]) * A["mouth_round_width"]
        t_r[v, 2] = mc[2] + (p[2] - mc[2]) * A["mouth_round_height"]
        t_r[v, 1] -= A["mouth_forward_m"] * u
        lens = math.sqrt(max(0.0, 1.0 - xn * xn))
        if p[2] < mc[2] - 1e-6:
            t_o[v, 2] -= A["mouth_open_m"] * u * lens
        elif p[2] > mc[2] + 1e-6:
            t_o[v, 2] += 0.0015 * u * lens
        t_o[v, 1] -= A["mouth_forward_m"] * u * lens
    targets.update({"MouthSmile": t_s, "MouthFrown": t_f, "MouthRound": t_r, "MouthOpen": t_o})

    report = {}
    for name in contract.SHAPE_KEYS:
        kb = obj.shape_key_add(name=name, from_mix=False)
        kb.slider_min, kb.slider_max = 0.0, 1.0
        kb.data.foreach_set("co", targets[name].reshape(-1).astype(np.float32))
        delta = np.linalg.norm(targets[name] - base, axis=1)
        report[name] = {"verts_moved": int((delta > 1e-6).sum()), "max_move_m": common.r(float(delta.max()), 4)}
    me.update()
    return report


# ---------------------------------------------------------------------------------------------------------------
# Driver step
# ---------------------------------------------------------------------------------------------------------------
def _num(d):
    return {k: (common.r(v, 5) if isinstance(v, float) else v) for k, v in d.items()}


def build_face(ctx, face=None):
    """Contract step (README 2.8). Adds Head shape keys, differentiates the face, fits face bones, writes the
    manifest `face` section. Returns the section dict."""
    if ctx.info["rig"] != "humanoid":
        raise RuntimeError("build_face is for humans; demons are WP-13")
    face = face or load_face_data(ctx.key)
    pieces = HeadPieces(ctx, face)
    mouth_report = refine_mouth(ctx, pieces)
    disp = differentiate(ctx, pieces, face["differentiation"])
    mouth_report["fit_forward_max_m"] = fit_mouth_to_skin(ctx, pieces)
    if pieces.obj.vertex_groups:
        mouth_report["head_verts_without_weights"] = sum(1 for v in pieces.obj.data.vertices if not v.groups)
    bones_report = fit_face_bones(ctx, pieces, disp)
    keys_report = author_shape_keys(ctx, pieces, face["shape_key_amplitude"])
    nk = len(pieces.obj.data.shape_keys.key_blocks)
    if nk > contract.MAX_SHAPE_KEYS_HEAD:
        raise RuntimeError("too many shape keys: %d" % nk)
    eye_ids = {s: pieces.eye_stack(s) for s in ("L", "R")}
    section = {
        "schema": FACE_SCHEMA,
        "data_file": face["_source"],
        "gaze_method": "bone",
        "eye_bones": ["Eye.L", "Eye.R"],
        "jaw_bone": "Jaw",
        "eye_bone_carries": {s: [eye_ids[s][2], eye_ids[s][3]] for s in ("L", "R")},
        "eye_bone_axes": {"yaw": "local Z, + turns the gaze to the character's right (-X)",
                          "pitch": "local X, + turns the gaze down"},
        "jaw_bone_axes": {"open": "local X, + opens (chin down)"},
        "shape_key_basis": contract.SHAPE_KEY_BASIS,
        "shape_keys": list(contract.SHAPE_KEYS),
        "shape_key_channels": KEY_CHANNEL,
        "shape_key_report": keys_report,
        "expressions": face["expressions"],
        "tuning": face["tuning"],
        "differentiation": _num(face["differentiation"]),
        "head_unit": common.r(pieces.unit, 4),
        "brow_thickness_applied": {i: common.r(pieces.by_id[i].get("brow_thickness_applied", 1.0), 3)
                                   for i in pieces.ids("brow")},
        "mouth_refine": mouth_report,
        "face_bones": bones_report,
    }
    ctx.set_section("face", section)
    return section


# ---------------------------------------------------------------------------------------------------------------
# Helpers for previews, clips and tests
# ---------------------------------------------------------------------------------------------------------------
def apply_expression(ctx, name, face=None, weight=1.0, extra_keys=None):
    """Set the Head shape keys (and Jaw rotation if an armature exists) to a preset, scaled by tuning gains."""
    face = face or load_face_data(ctx.key)
    ex = face["expressions"][name]
    gains = face["tuning"]["gain"]
    kbs = ctx.parts["Head"].data.shape_keys.key_blocks
    for k in contract.SHAPE_KEYS:
        kbs[k].value = 0.0
    for k, w in ex.get("keys", {}).items():
        kbs[k].value = min(1.0, max(0.0, w * weight * gains.get(KEY_CHANNEL[k], 1.0)))
    for k, w in (extra_keys or {}).items():
        kbs[k].value = w
    if ctx.armature is not None and "Jaw" in ctx.armature.pose.bones:
        pb = ctx.armature.pose.bones["Jaw"]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (math.radians(ex.get("jaw_deg", 0.0) * weight * gains.get("jaw", 1.0)), 0.0, 0.0)


def expression_table(face):
    """Plain dict {expression: {shape key: weight}} with gains applied (for the clip package and Unity)."""
    gains = face["tuning"]["gain"]
    out = {}
    for name, ex in face["expressions"].items():
        out[name] = {k: common.r(min(1.0, w * gains.get(KEY_CHANNEL[k], 1.0)), 3) for k, w in ex["keys"].items()}
    return out


def validate_cast_files(cast_dir=None):
    """Plain-python check of every cast/<key>.json face block (names, ranges). Returns {key: expression table}."""
    out = {}
    for key in contract.CAST_KEYS:
        if contract.CAST_BY_KEY[key]["rig"] != "humanoid":
            continue
        face = load_face_data(key, cast_dir)
        out[key] = {"data_file": face["_source"], "expressions": expression_table(face)}
    return out


if __name__ == "__main__":
    # python -I Tools/characters/lib/face.py   -> validates the face data of all 14 humans (defaults where absent)
    res = validate_cast_files()
    for k, v in res.items():
        print("%-13s %s" % (k, v["data_file"] or "(defaults)"))
    print("ok: %d humans, %d expressions each, %d shape keys" % (len(res), len(contract.EXPRESSIONS),
                                                                 len(contract.SHAPE_KEYS)))
