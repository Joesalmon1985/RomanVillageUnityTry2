"""Clip generator (WP-5). Turns readable clip data (clips/*.py, clips/style/*.json) into baked FK actions.

  build_clips(ctx_or_armature, set_names=None, style=True) -> list of clip records (contract 2.6)

How a clip is made (every clip, in this order):
  1. Data: keyed poses with frame numbers and easing (clips/*.py), or a gait cycle / start / stop / turn generator
     (lib/anim_gait.py) driven by a small parameter set. Per-character style (clips/style/<key>.json) changes the
     stand pose, amplitudes, tempo, springs and gait parameters; a clip may also carry a full per-character variant.
  2. Dense channels at 30 fps (one value per frame for every control).
  3. Layers that add the animation principles the data does not spell out:
       weight      pelvis moves over the supporting foot (before a foot lifts), free hip drops, shoulders counter-tilt
       drag        torso leans against the pelvis's velocity (the top arrives late)
       breath      chest, shoulders, arms; phase is zero at both ends of every clip so clips chain
       noise       small looping drifts so holds are never frozen, slightly different left and right
       overlap     forearm drags behind the upper arm, hand behind the forearm, head behind the chest
       springs     second-order followers per bone: overshoot and settle (follow-through)
       head level  the head keeps most of its own world orientation while the body moves under it
  4. Solve per frame: pelvis height is limited so planted feet are always reachable, legs are solved by two-bone IK
     onto world-fixed foot targets (heel and toe pivots), hands can be pinned in root space and props aim-locked,
     then cape, hood and skirt chains follow their parents.
  5. Bake: every frame is written as FK Euler XYZ keys (contract 2.6) plus the Hips location. No IK survives.

Authoring axes (all clip data): vectors are [left, forward, up] in metres for a character whose leg (hip joint to
ankle) is REF_LEG long, scaled to the rig. Bone rotations are Euler XYZ degrees in the roll-rule axes of the
contract: for the upright chain (Hips, Spine, Chest, Neck, Head) X = pitch forward, Y = turn left, Z = lean right.
"""

import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import numpy as np  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector, Quaternion, Euler  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402

FPS = contract.FPS
DT = 1.0 / FPS
REF_LEG = 0.80
CLIPS_DIR = os.path.join(common.TOOLS_DIR, "clips")
STYLE_DIR = os.path.join(CLIPS_DIR, "style")

# contact codes for the foot report
C_NONE, C_HEEL, C_FLAT, C_TOE = 0, 1, 2, 3

UPRIGHT = ["Hips", "Spine", "Chest", "Neck", "Head"]
ARM_BONES = ["UpperArm", "LowerArm", "Hand"]
# channel name -> width. Bone channels (Euler degrees) are added per rig.
VEC_CHANNELS = {
    "pelvis": 3, "weight": 1, "head_level": 1, "eyes": 2, "jaw": 1,
    "footL.pos": 3, "footL.rot": 3, "footR.pos": 3, "footR.rot": 3,
    "shoulderL": 2, "shoulderR": 2,
    "lockL": 1, "lockR": 1, "aimL": 3, "aimR": 3, "pinL": 1, "pinR": 1, "pinL.pos": 3, "pinR.pos": 3,
}
GROUPS = {
    "torso": ["Hips", "Spine", "Chest", "pelvis", "weight"],
    "head": ["Neck", "Head", "eyes", "jaw", "head_level"],
    "armL": ["UpperArm.L", "LowerArm.L", "Hand.L", "shoulderL", "lockL", "aimL", "pinL", "pinL.pos"],
    "armR": ["UpperArm.R", "LowerArm.R", "Hand.R", "shoulderR", "lockR", "aimR", "pinR", "pinR.pos"],
    "legs": ["footL.pos", "footL.rot", "footR.pos", "footR.rot"],
}


def W(v):
    """authoring [left, forward, up] -> Blender vector (left = +X, forward = -Y, up = +Z)."""
    return Vector((v[0], -v[1], v[2]))


def rad(d):
    return d * math.pi / 180.0


def euler_q(e):
    return Euler((rad(e[0]), rad(e[1]), rad(e[2])), "XYZ").to_quaternion()


def sem_rot(pitch, yaw, roll):
    """World-axis rotation with the upright-bone meaning: pitch forward, yaw left, roll (lean) right. Degrees."""
    return (Matrix.Rotation(rad(roll), 3, Vector((0, -1, 0))) @ Matrix.Rotation(rad(yaw), 3, "Z")
            @ Matrix.Rotation(rad(pitch), 3, "X"))


# ----------------------------------------------------------------------------------------
# Easing. The name describes the segment that ARRIVES at a key.
# ----------------------------------------------------------------------------------------
def _back_out(u, s=1.1):
    u = u - 1.0
    return u * u * ((s + 1) * u + s) + 1.0


EASES = {
    "smooth": lambda u: u * u * u * (u * (6 * u - 15) + 10),   # slow out of the last pose, slow into this one
    "linear": lambda u: u,
    "accel": lambda u: u ** 2.4,                                # slow start, arrives at speed (into a hit)
    "decel": lambda u: 1 - (1 - u) ** 2.4,                      # leaves at speed, settles in
    "snap": lambda u: 1 - (1 - u) ** 4.0,                       # most of the move in the first third
    "overshoot": _back_out,                                     # passes the pose by about 6 percent and returns
    "soft": lambda u: u * u * (3 - 2 * u),
    "hold": lambda u: 0.0 if u < 1.0 else 1.0,
}


def smooth(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


def smoother(u):
    u = min(1.0, max(0.0, u))
    return u * u * u * (u * (6 * u - 15) + 10)


# ----------------------------------------------------------------------------------------
# Rig: rest data and limb solving, read from any contract armature (stub or WP-2's).
# ----------------------------------------------------------------------------------------
def _frame(y, x_hint):
    y = y.normalized()
    x = x_hint - y * x_hint.dot(y)
    if x.length < 1e-6:
        x = Vector((1, 0, 0)) - y * y.x
    x.normalize()
    z = x.cross(y)
    return Matrix((x, y, z)).transposed()


class _Limb:
    pass


class Rig:
    def __init__(self, arm):
        self.arm = arm
        db = arm.data.bones
        self.names = [b.name for b in db]
        self.has = set(self.names)
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in db}
        self.rest = {b.name: b.matrix_local.copy() for b in db}
        self.rest3 = {n: m.to_3x3() for n, m in self.rest.items()}
        self.head = {n: m.translation.copy() for n, m in self.rest.items()}
        self.tail = {b.name: b.tail_local.copy() for b in db}
        self.rest_local = {}
        for n in self.names:
            p = self.parent[n]
            self.rest_local[n] = (self.rest[p].inverted() @ self.rest[n]) if p else self.rest[n].copy()
        self.rl3 = {n: m.to_3x3() for n, m in self.rest_local.items()}
        self.rlt = {n: m.translation.copy() for n, m in self.rest_local.items()}
        missing = [n for n, _ in contract.HUMAN_CORE_BONES if n not in self.has]
        if missing:
            raise RuntimeError("armature is missing contract bones: %s" % missing)
        self.limb = {}
        for s in "LR":
            self.limb["leg" + s] = self._limb("UpperLeg." + s, "LowerLeg." + s, "Foot." + s, Vector((0, -1, 0)))
            self.limb["arm" + s] = self._limb("UpperArm." + s, "LowerArm." + s, "Hand." + s, Vector((0, 1, 0)))
        self.leg_len = self.limb["legL"].l1 + self.limb["legL"].l2
        self.arm_len = self.limb["armL"].l1 + self.limb["armL"].l2
        self.scale = self.leg_len / REF_LEG
        self.foot = {s: self._foot(s) for s in "LR"}
        self.prop = {s: self._prop(s) for s in "LR"}

    def _limb(self, b1, b2, b3, pole):
        L = _Limb()
        L.b1, L.b2, L.b3 = b1, b2, b3
        a, b, c = self.head[b1], self.head[b2], self.head[b3]
        L.l1, L.l2 = (b - a).length, (c - b).length
        chain = (c - a).normalized()
        h = pole.cross(chain)
        h.normalize()
        L.F1 = _frame(b - a, h)
        L.F2 = _frame(c - b, h)
        L.rest_pole = pole
        return L

    def _foot(self, s):
        """Foot contact geometry in world axes, relative to the ground point under the ankle (rest pose)."""
        F = _Limb()
        ankle = self.head["Foot." + s]
        F.h = ankle.z
        F.ground = Vector((ankle.x, ankle.y, 0.0))
        toe_tip_y = self.tail["Toes." + s].y
        ball_y = self.head["Toes." + s].y
        sock = "Sock.Foot." + s
        centre_y = self.head[sock].y if sock in self.has else 0.5 * (ball_y + ankle.y)
        heel_y = 2.0 * centre_y - toe_tip_y            # the foot piece is symmetric about its centre
        heel_y = max(heel_y, ankle.y + 0.02)
        # rigid foot piece: it tips over a point near the toe tip, not over the ball
        toe_y = ball_y + 0.72 * (toe_tip_y - ball_y)
        F.toe = Vector((0.0, toe_y - ankle.y, 0.0))
        F.heel = Vector((0.0, heel_y - ankle.y, 0.0))
        F.centre = Vector((0.0, centre_y - ankle.y, 0.0))
        return F

    def _prop(self, s):
        """Prop axis in the hand's rest frame (grip to Sock.StaffTip), if the staff-tip socket hangs from this hand."""
        tip, hs = "Sock.StaffTip", "Sock.Hand." + s
        if tip in self.has and self.parent.get(tip) == hs:
            axis = self.head[tip] - self.head[hs]
            if axis.length > 1e-4:
                P = _Limb()
                P.axis = axis.normalized()
                P.length = axis.length
                P.grip = self.head[hs].copy()
                return P
        return None

    def solve_limb(self, name, root_pos, target, pole, parent_world3, ext=1.0):
        """Two-bone IK. Returns (q_upper, q_lower, R_upper, R_lower, mid, end, clamped)."""
        L = self.limb[name]
        d = target - root_pos
        dist = d.length
        reach = (L.l1 + L.l2) * ext
        clamped = dist > reach + 1e-6
        dc = min(max(dist, abs(L.l1 - L.l2) + 1e-4), reach)
        a = d.normalized() if dist > 1e-9 else Vector((0, 0, -1))
        x = (L.l1 * L.l1 - L.l2 * L.l2 + dc * dc) / (2 * dc)
        hgt = math.sqrt(max(L.l1 * L.l1 - x * x, 0.0))
        pd = pole - a * pole.dot(a)
        if pd.length < 1e-6:
            pd = L.rest_pole - a * L.rest_pole.dot(a)
        pd.normalize()
        mid = root_pos + a * x + pd * hgt
        end = root_pos + a * dc
        h = pd.cross(a)
        h.normalize()
        R1 = _frame(mid - root_pos, h) @ L.F1.transposed() @ self.rest3[L.b1]
        R2 = _frame(end - mid, h) @ L.F2.transposed() @ self.rest3[L.b2]
        q1 = ((parent_world3 @ self.rl3[L.b1]).inverted() @ R1).to_quaternion()
        q2 = ((R1 @ self.rl3[L.b2]).inverted() @ R2).to_quaternion()
        return q1, q2, R1, R2, mid, end, clamped


# ----------------------------------------------------------------------------------------
# Data loading: clip modules, pose library, styles
# ----------------------------------------------------------------------------------------
def _load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def load_clip_data():
    """Returns (clips, poses): every CLIPS and POSES dict from clips/*.py merged."""
    clips, poses = {}, {}
    for fn in sorted(os.listdir(CLIPS_DIR)):
        if fn.endswith(".py") and not fn.startswith("_"):
            mod = _load_module(os.path.join(CLIPS_DIR, fn), "wp5_clips_" + fn[:-3])
            poses.update(getattr(mod, "POSES", {}))
            for k, v in getattr(mod, "CLIPS", {}).items():
                if k in clips:
                    raise RuntimeError("clip %s defined twice" % k)
                clips[k] = v
    return clips, poses


def _deep_merge(a, b):
    out = dict(a)
    for k, v in b.items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k] = _deep_merge(out[k], v)
        else:
            out[k] = v
    return out


def load_style(key=None):
    base = common.read_json(os.path.join(STYLE_DIR, "base.json"))
    if key is None or key == "base":
        base["key"] = None
        return base
    path = os.path.join(STYLE_DIR, key + ".json")
    st = _deep_merge(base, common.read_json(path))
    st["key"] = key
    return st


def style_keys():
    return sorted(fn[:-5] for fn in os.listdir(STYLE_DIR) if fn.endswith(".json") and fn != "base.json")


def mirror_pose(p):
    """Left/right mirror of a pose dict (authoring data)."""
    def swap(k):
        if k.endswith(".L"):
            return k[:-2] + ".R"
        if k.endswith(".R"):
            return k[:-2] + ".L"
        for a, b in (("armL", "armR"), ("footL", "footR"), ("shoulderL", "shoulderR"), ("propL", "propR"),
                     ("pinL", "pinR"), ("armL+", "armR+")):
            if k == a:
                return b
            if k == b:
                return a
        return k

    def flip_vec(v):
        return [-v[0], v[1], v[2]]

    def flip_eul(v):
        return [v[0], -v[1], -v[2]]

    out = {}
    for k, v in p.items():
        nk = swap(k)
        if k == "weight":
            out[nk] = 1.0 - v
        elif k == "pelvis":
            out[nk] = flip_vec(v)
        elif k == "eyes":
            out[nk] = [-v[0], v[1]]
        elif k in ("armL", "armR") and isinstance(v, dict):
            d = dict(v)
            for kk in ("reach", "elbow", "at"):
                if kk in d:
                    d[kk] = flip_vec(d[kk])
            if isinstance(d.get("palm"), (list, tuple)):
                d["palm"] = flip_vec(d["palm"])
            elif d.get("palm") in ("left", "right"):
                d["palm"] = "right" if d["palm"] == "left" else "left"
            if "hand" in d:
                d["hand"] = flip_eul(d["hand"])
            out[nk] = d
        elif k in ("armL+", "armR+"):
            out[nk] = {b: flip_eul(e) for b, e in v.items()}
        elif k in ("footL", "footR"):
            d = dict(v)
            if "pos" in d:
                d["pos"] = flip_vec(d["pos"])
            for kk in ("yaw", "roll"):
                if kk in d:
                    d[kk] = -d[kk]
            out[nk] = d
        elif k in ("propL", "propR", "pinL", "pinR") and isinstance(v, dict):
            d = dict(v)
            for kk in ("aim", "pos"):
                if kk in d:
                    d[kk] = flip_vec(d[kk])
            out[nk] = d
        elif k in ("shoulderL", "shoulderR", "jaw", "head_level", "base"):
            out[nk] = v
        elif isinstance(v, (list, tuple)) and len(v) == 3:
            out[nk] = flip_eul(v)
        else:
            out[nk] = v
    return out


PALM_WORDS = {"fwd": [0, 1, 0], "back": [0, -1, 0], "up": [0, 0, 1], "down": [0, 0, -1],
              "left": [1, 0, 0], "right": [-1, 0, 0]}


# ----------------------------------------------------------------------------------------
# Builder: one clip on one rig with one style
# ----------------------------------------------------------------------------------------
class Baked:
    """Result of one clip: per-bone Euler arrays (radians), Hips location, and the facts the reports need."""
    pass


class ClipBuilder:
    def __init__(self, rig, style, name, cdef, poses, all_clips=None):
        self.rig = rig
        self.style = style
        self.name = name
        self.poses = poses
        self.all_clips = all_clips or {}
        self.mirror = False
        if cdef.get("mirror_of"):                         # same data as the source clip, left and right swapped
            src = self.all_clips[cdef["mirror_of"]]
            if style["key"]:
                src = variant_def(src, style["key"])
            merged = dict(src)
            merged.update({k: v for k, v in cdef.items() if k != "mirror_of"})
            cdef = merged
            self.mirror = True
        self.cdef = cdef
        self.loop = bool(cdef.get("loop", name.split("@")[0] in contract.LOOP_CLIPS))
        tempo = float(style.get("tempo", 1.0)) if cdef.get("tempo", True) else 1.0
        if cdef.get("type") == "gait":
            # a gait keeps its speed, so its length is the stride: 'stride' < 1 gives shorter, quicker steps
            tempo = float(style.get("stride", tempo))
        self.tempo = tempo
        self.n = int(cdef["frames"]) if tempo == 1.0 else max(2, int(round(cdef["frames"] * tempo)))
        self.F = self.n if self.loop else self.n + 1
        self.sc = rig.scale
        self.amp = float(style.get("amplitude", 1.0)) * float(cdef.get("amplitude", 1.0))
        st = style["stand"]
        self.stand = st
        self.stance = {}
        for s, sg in (("L", 1.0), ("R", -1.0)):
            S = _Limb()
            S.base = rig.foot[s].ground + W([sg * st.get("stance_width", 0.0) * self.sc,
                                             sg * 0.5 * st.get("stagger", 0.0) * self.sc, 0.0])
            S.yaw = sg * st.get("toe_out", 0.0)
            self.stance[s] = S
        self.ch = {}
        self.contact = {s: np.full(self.F, C_FLAT, dtype=np.int32) for s in "LR"}
        self.ground_dist = np.zeros(self.F)     # metres the ground has moved back under the root (in place clips)
        self.ground_yaw = np.zeros(self.F)      # degrees the root has turned left (turn clips)
        self.native_speed = 0.0
        self.events = []
        self.is_gait = False
        self.skip_weight = False
        self.pelvis_mode = None
        self.post_layers = None
        self.meta = {}
        self.arm_keys_q = {}

    # ---- stand pose --------------------------------------------------------------------
    def stand_values(self):
        """Channel values of the style's stand pose (absolute)."""
        st = self.stand
        v = {}
        for b in UPRIGHT:
            v[b] = list(st.get("bones", {}).get(b, [0, 0, 0]))
        p = st.get("pelvis", [0, 0, 0])
        v["pelvis"] = [p[0] * self.sc, p[1] * self.sc, p[2] * self.sc]
        v["weight"] = [st.get("weight", 0.5)]
        v["head_level"] = [st.get("head_level", 0.6)]
        v["eyes"] = [0, 0]
        v["jaw"] = [0]
        for s in "LR":
            v["foot%s.pos" % s] = [0, 0, 0]
            v["foot%s.rot" % s] = [0, 0, 0]
            sh = st.get("shoulders", [0, 0])
            v["shoulder" + s] = list(sh)
            pin = st.get("pin", {}).get(s)
            v["pin" + s] = [pin.get("w", 1.0)] if pin else [0]
            v["pin%s.pos" % s] = [c * self.sc for c in pin.get("pos", [0, 0, 0])] if pin else [0, 0, 0]
            lock = st.get("lock", {}).get(s, 0.0)
            v["lock" + s] = [lock]
            P = self.rig.prop[s]
            aim = st.get("aim", {}).get(s)
            if aim is None:
                aim = [P.axis.x, -P.axis.y, P.axis.z] if P else [0, 0, 1]
            v["aim" + s] = list(aim)
            arm = st.get("arms", {}).get(s) or st.get("arms", {}).get("both")
            vals = self.resolve_arm(s, arm, v) if arm is not None else None
            for i, b in enumerate(ARM_BONES):
                val = list(vals[i]) if vals else [0, 0, 0]
                v["%s.%s" % (b, s)] = self._q_to_e(val) if self._is_q(val) else val
        return v

    # ---- arm resolution (IK at the key) -----------------------------------------------------
    def _torso_world(self, vals):
        """Chest world rotation and shoulder joint positions for a set of torso channel values."""
        rig = self.rig
        R = rig.rest3["Hips"] @ euler_q(vals.get("Hips", [0, 0, 0])).to_matrix()
        p = rig.head["Hips"] + W(vals.get("pelvis", [0, 0, 0]))
        for b in ("Spine", "Chest"):
            p = p + R @ rig.rlt[b]
            R = R @ rig.rl3[b] @ euler_q(vals.get(b, [0, 0, 0])).to_matrix()
        return R, p

    def resolve_arm(self, s, spec, vals):
        """spec -> ([upper], [lower], [hand]) Euler degrees. spec is {'fk': {...}} or {'reach': ...}."""
        rig = self.rig
        sg = 1.0 if s == "L" else -1.0
        if "fk" in spec:
            fk = spec["fk"]
            return [list(fk.get(b, [0, 0, 0])) for b in ARM_BONES]
        L = rig.limb["arm" + s]
        S0 = rig.head[L.b1]
        if "at" in spec:                      # wrist position in root space, metres for a REF_LEG character
            reach = W(spec["at"]) * self.sc - S0
        else:
            reach = W(spec["reach"]) * rig.arm_len
        pole = W(spec.get("elbow", [sg * 0.55, -0.55, -0.65]))
        Rc0 = rig.rest3["Chest"]
        conv = None
        if spec.get("space", "chest") == "root" or "at" in spec:
            Rc, pc = self._torso_world(vals)
            Sw = pc + Rc @ rig.rlt["Shoulder." + s] + Rc @ rig.rl3["Shoulder." + s] @ rig.rlt[L.b1]
            conv = Rc0 @ Rc.transposed()
            target = S0 + conv @ (S0 + reach - Sw)
            pole = conv @ pole
        else:
            target = S0 + reach
        q1, q2, R1, R2, mid, end, _ = rig.solve_limb("arm" + s, S0, target, pole, rig.rest3["Shoulder." + s],
                                                     ext=0.985)
        Rh = R2 @ rig.rl3[L.b3]
        twist = 0.0
        palm = spec.get("palm")
        if palm is not None:
            if isinstance(palm, str):
                palm = PALM_WORDS[palm]
            nd = W(palm)
            if conv is not None:
                nd = conv @ nd
            yh = Rh.col[1].normalized()
            n0 = Rh @ (rig.rest3[L.b3].transposed() @ Vector((-sg, 0, 0)))
            n0 = (n0 - yh * n0.dot(yh)).normalized()
            ndp = nd - yh * nd.dot(yh)
            if ndp.length > 1e-4:
                ndp.normalize()
                twist = math.atan2(n0.cross(ndp).dot(yh), n0.dot(ndp))
                twist = max(-2.6, min(2.6, twist))
        share = 0.5
        q2 = q2 @ Quaternion(Vector((0, 1, 0)), twist * share)
        qh = Quaternion(Vector((0, 1, 0)), twist * (1 - share))
        if "hand" in spec:
            qh = qh @ euler_q(spec["hand"])
        return [[q1], [q2], [qh]]

    # ---- pose -> channel values ----------------------------------------------------------
    def expand_pose(self, pose):
        """Follow 'base' inheritance and 'mirror' markers; returns a flat pose dict."""
        if isinstance(pose, str):
            if pose.endswith("~"):                       # "name~" = mirrored pose
                return mirror_pose(self.expand_pose(pose[:-1]))
            if pose not in self.poses:
                raise KeyError("unknown pose %r in clip %s" % (pose, self.name))
            pose = self.poses[pose]
        out = {}
        base = pose.get("base")
        if base:
            out.update(self.expand_pose(base))
        for k, v in pose.items():
            if k != "base":
                if isinstance(v, dict) and isinstance(out.get(k), dict) and k.startswith(("foot", "prop", "pin")):
                    d = dict(out[k])
                    d.update(v)
                    out[k] = d
                else:
                    out[k] = v
        return out

    def pose_values(self, pose, stand):
        """Flat pose -> {channel: value}. Upright bones and pelvis are deltas on the stand pose (scaled by the
        style amplitude); arms, feet and the rest are absolute."""
        sc, amp = self.sc, self.amp
        v = {}
        for k, val in pose.items():
            if k in UPRIGHT:
                v[k] = [stand[k][i] + val[i] * amp for i in range(3)]
            elif k == "pelvis":
                v[k] = [stand[k][i] + val[i] * sc * amp for i in range(3)]
            elif k in ("weight", "head_level", "jaw"):
                v[k] = [float(val)]
            elif k == "eyes":
                v[k] = list(val)
            elif k in ("shoulderL", "shoulderR"):
                v[k] = [stand[k][0] + val[0], stand[k][1] + val[1]]
            elif k in ("footL", "footR"):
                s = k[-1]
                if "pos" in val:
                    v["foot%s.pos" % s] = [c * sc for c in val["pos"]]
                if any(x in val for x in ("yaw", "pitch", "roll")):
                    v["foot%s.rot" % s] = [val.get("yaw", 0.0), val.get("pitch", 0.0), val.get("roll", 0.0)]
            elif k in ("propL", "propR"):
                s = k[-1]
                if val == "stand":
                    v["lock" + s] = stand["lock" + s]
                    v["aim" + s] = stand["aim" + s]
                else:
                    if "aim" in val:
                        v["aim" + s] = list(val["aim"])
                    v["lock" + s] = [val.get("lock", 1.0)]
            elif k in ("pinL", "pinR"):
                s = k[-1]
                if val == "stand":
                    v["pin" + s] = stand["pin" + s]
                    v["pin%s.pos" % s] = stand["pin%s.pos" % s]
                elif val in (None, "off"):
                    v["pin" + s] = [0.0]
                else:
                    v["pin" + s] = [val.get("w", 1.0)]
                    v["pin%s.pos" % s] = [c * sc for c in val.get("pos", [0, 0, 0])]
            elif k in ("armL", "armR"):
                s = k[-1]
                if val in ("hang", "stand"):
                    for b in ARM_BONES:
                        v["%s.%s" % (b, s)] = stand["%s.%s" % (b, s)]
                else:
                    torso = dict(stand)
                    torso.update({kk: vv for kk, vv in v.items() if kk in ("Hips", "Spine", "Chest", "pelvis")})
                    for kk in ("Hips", "Spine", "Chest"):
                        if kk in pose:
                            torso[kk] = [stand[kk][i] + pose[kk][i] * amp for i in range(3)]
                    if "pelvis" in pose:
                        torso["pelvis"] = [stand["pelvis"][i] + pose["pelvis"][i] * sc * amp for i in range(3)]
                    res = self.resolve_arm(s, val, torso)
                    for i, b in enumerate(ARM_BONES):
                        v["%s.%s" % (b, s)] = res[i]
            elif k in ("armL+", "armR+"):                 # small offsets on the stand arm (Euler degrees per bone)
                s = k[3]
                for b in ARM_BONES:
                    if b in val:
                        base = stand["%s.%s" % (b, s)]
                        v["%s.%s" % (b, s)] = [base[i] + val[b][i] for i in range(3)]
            elif "." in k and k.split(".")[0] in ARM_BONES + ["Shoulder"]:
                v[k] = list(val)
            elif k in ("Jaw",):
                v["jaw"] = [val[0]]
            else:
                raise KeyError("pose key %r not understood (clip %s)" % (k, self.name))
        return v

    # ---- dense channels from keys ---------------------------------------------------------
    def alloc(self, stand):
        for k, val in stand.items():
            self.ch[k] = np.tile(np.array(val, dtype=np.float64), (self.F, 1))

    def build_keyed(self):
        cdef = self.cdef
        stand = self.stand_values()
        self.stand_ch = stand
        self.alloc(stand)
        self.events = [{"name": self._side_name(ev["name"]), "frame": int(self.fr(ev["f"]))}
                       for ev in cdef.get("events", [])]
        keys = cdef.get("keys")
        if not keys:
            return
        n = self.n
        tracks = {}          # channel -> list of (frame, value, ease)
        offsets = cdef.get("offsets", {})
        ch_offset = {}
        for g, off in offsets.items():
            for c in self._group(g):
                ch_offset[c] = off
        for key in keys:
            f = float(self.fr(key["f"]))
            pose = {}
            if "pose" in key:
                names = key["pose"] if isinstance(key["pose"], list) else [key["pose"]]
                for nm in names:
                    pose.update(self.expand_pose(nm))
            if "set" in key:
                pose.update(self.expand_pose(key["set"]))
            if self.mirror:
                pose = mirror_pose(pose)
            vals = self.pose_values(pose, stand)
            if key.get("all") or key["f"] == 0 or key["f"] == cdef["frames"]:
                full = dict(stand)
                full.update(vals)
                vals = full
            only = key.get("only")
            if only:
                allowed = set()
                for g in only:
                    allowed.update(self._group(g))
                vals = {k: v for k, v in vals.items() if k in allowed}
            skip = key.get("skip")
            if skip:
                denied = set()
                for g in skip:
                    denied.update(self._group(g))
                vals = {k: v for k, v in vals.items() if k not in denied}
            ease = key.get("ease", "smooth")
            eases = key.get("eases", {})
            for c, val in vals.items():
                ff = f
                if 0 < f < n and c in ch_offset:
                    ff = min(n - 1.0, max(1.0, f + ch_offset[c]))
                e = ease
                for g, ge in eases.items():
                    if c in self._group(g):
                        e = ge
                if c.endswith(".pos") and c.startswith("foot"):
                    e = "hold"                      # a foot only moves through a step, never by sliding between keys
                tracks.setdefault(c, []).append((ff, val, e))
        for c, tr in tracks.items():
            tr.sort(key=lambda t: t[0])
            # quaternion keys (IK arms) -> Euler, continuous with the previous key
            prev = None
            vals = []
            for (f, val, e) in tr:
                if self._is_q(val):
                    eu = val[0].to_euler("XYZ", Euler([rad(a) for a in prev], "XYZ")) if prev is not None \
                        else val[0].to_euler("XYZ")
                    val = [math.degrees(a) for a in eu]
                elif prev is not None and c in self.ch and len(val) == 3 and c.split(".")[0] in ARM_BONES:
                    val = [val[i] - 360.0 * round((val[i] - prev[i]) / 360.0) for i in range(3)]
                prev = val
                vals.append((f, np.array(val, dtype=np.float64), e))
            arr = self.ch[c]
            for i in range(self.F):
                arr[i] = self._sample(vals, float(i))

    def fr(self, f):
        """Authored frame -> frame of this (tempo-scaled) clip. The last authored frame maps to the last frame."""
        if f >= self.cdef["frames"]:
            return self.n
        if f <= 0:
            return 0
        return int(min(self.n - 1, max(1, round(f * self.tempo))))

    def _side_name(self, name):
        """Swap L and R in a group, channel, foot or event name when this clip is a mirror of another."""
        if not self.mirror:
            return name
        table = {"armL": "armR", "armR": "armL", "FootL": "FootR", "FootR": "FootL", "L": "R", "R": "L",
                 "shoulderL": "shoulderR", "shoulderR": "shoulderL"}
        if name in table:
            return table[name]
        if name.endswith(".L"):
            return name[:-1] + "R"
        if name.endswith(".R"):
            return name[:-1] + "L"
        return name

    def _group(self, g):
        g = self._side_name(g)
        return GROUPS.get(g, [g])

    @staticmethod
    def _is_q(v):
        return isinstance(v, list) and len(v) == 1 and isinstance(v[0], Quaternion)

    @staticmethod
    def _q_to_e(v):
        return [math.degrees(a) for a in v[0].to_euler("XYZ")]

    @staticmethod
    def _sample(tr, t):
        if t <= tr[0][0]:
            return tr[0][1]
        if t >= tr[-1][0]:
            return tr[-1][1]
        for i in range(1, len(tr)):
            if t <= tr[i][0]:
                f0, v0, _ = tr[i - 1]
                f1, v1, e = tr[i]
                if f1 - f0 < 1e-9:
                    return v1
                u = EASES[e]((t - f0) / (f1 - f0))
                return v0 + (v1 - v0) * u
        return tr[-1][1]

    # ---- steps (keyed clips): lift, arc, plant, with the weight shift before the lift ------------
    def apply_steps(self):
        steps = self.cdef.get("steps", [])
        if not steps:
            return
        sc = self.sc
        w = self.ch["weight"][:, 0]
        for stp in steps:
            s = self._side_name(stp["foot"])
            msg = -1.0 if self.mirror else 1.0
            f0 = self.fr(stp["f0"])
            f1 = max(f0 + 1, self.fr(stp["f1"]))
            pos = self.ch["foot%s.pos" % s]
            rot = self.ch["foot%s.rot" % s]
            p0 = pos[max(f0 - 1, 0)].copy()
            r0 = rot[max(f0 - 1, 0)].copy()
            to = stp.get("to", [0.0, 0.0])
            p1 = np.array([msg * to[0] * sc, to[1] * sc, 0.0])
            yaw1 = msg * stp["yaw"] if "yaw" in stp else r0[0]
            lift = stp.get("lift", 0.06) * sc
            fwd = (p1[1] - p0[1])
            push = stp.get("push", 18.0 if fwd >= -0.02 else -10.0)      # toes down at lift-off (heel first if back)
            land = stp.get("land", -12.0 if fwd > 0.05 else (10.0 if fwd < -0.05 else 0.0))
            dur = max(1, f1 - f0)
            keyed_pitch = rot[:, 1].copy()
            keyed_yaw = rot[:, 0].copy()
            for i in range(f0, self.F):
                u = (i - f0) / dur
                if u < 1.0:
                    e = smooth(u) * 0.75 + u * 0.25
                    pos[i, :2] = p0[:2] + (p1[:2] - p0[:2]) * e
                    pos[i, 2] = lift * math.sin(math.pi * min(1.0, u * 1.08)) ** 1.3
                    pitch = push * math.sin(math.pi * min(1.0, u / 0.45)) if u < 0.45 else land * smooth((u - 0.45) / 0.5)
                    rot[i, 0] = r0[0] + (yaw1 - r0[0]) * smooth(u)
                    rot[i, 1] = pitch
                    self.contact[s][i] = C_NONE
                else:
                    k = min(1.0, (i - f1) / 2.0)           # from the landing angle to the keyed angle over two frames
                    pos[i] = p1
                    rot[i, 0] = yaw1 if "yaw" in stp else keyed_yaw[i]
                    rot[i, 1] = land * (1.0 - smooth(k)) + keyed_pitch[i] * smooth(k)
                    self.contact[s][i] = C_FLAT
            # weight: onto the other foot before the lift (the pelvis leads the foot), released after the plant
            shift = max(2, int(round(stp.get("shift", 4) * self.tempo)))
            settle = max(2, int(round(stp.get("settle", 5) * self.tempo)))
            w_sup = stp.get("w_support", 0.04)                 # share of the weight left on the stepping foot
            w_sup = w_sup if s == "L" else 1.0 - w_sup
            for i in range(max(0, f0 - shift), min(self.F, f1 + settle + 1)):
                if i < f0:
                    env = smoother((i - (f0 - shift)) / float(shift))
                elif i <= f1:
                    env = 1.0
                else:
                    env = 1.0 - smoother((i - f1) / float(settle))
                w[i] = w[i] + (w_sup - w[i]) * env
            if stp.get("event", True):
                self.events.append({"name": "Foot" + s, "frame": int(min(self.n, f1))})

    # ---- layers ----------------------------------------------------------------------------
    def _shift(self, arr, k):
        """arr delayed by k frames (value at t-k), loop aware."""
        if k == 0:
            return arr.copy()
        if self.loop:
            return np.roll(arr, k, axis=0)
        out = np.empty_like(arr)
        out[k:] = arr[:-k]
        out[:k] = arr[0]
        return out

    def _lag(self, x, k):
        """1-D track delayed by k frames; a one-shot catches up over its last frames so it ends on its keyed pose."""
        y = self._shift(x[:, None], k)[:, 0]
        if not self.loop:
            m = min(len(x) - 1, k + 2)
            for j in range(m):
                a = smooth((j + 1) / m)
                i = len(x) - m + j
                y[i] = y[i] * (1 - a) + x[i] * a
        return y

    def _deriv(self, arr):
        if self.loop:
            return (np.roll(arr, -1, axis=0) - np.roll(arr, 1, axis=0)) * (0.5 * FPS)
        d = np.gradient(arr, axis=0) * FPS
        return d

    def _window(self, k=6):
        """1 inside the clip, eased to 0 at both ends of a one-shot (so it starts and ends on its keyed pose)."""
        if self.loop:
            return np.ones(self.F)
        w = np.ones(self.F)
        k = min(k, max(1, self.F // 3))
        for i in range(k):
            a = smooth(i / k)
            w[i] = min(w[i], a)
            w[self.F - 1 - i] = min(w[self.F - 1 - i], a)
        return w

    def foot_origin(self, s, i):
        return self.stance[s].base + W(self.ch["foot%s.pos" % s][i])

    def layer_weight(self):
        L = self.style.get("layers", {})
        cl = self.cdef.get("layers", {})
        k_lat = cl.get("shift_lat", L.get("shift_lat", 0.7))
        k_fwd = cl.get("shift_fwd", L.get("shift_fwd", 0.85))
        k_roll = cl.get("hip_drop", L.get("hip_drop", 4.0))
        if self.is_gait or self.skip_weight:
            return
        w = self.ch["weight"][:, 0]
        w0 = self.stand.get("weight", 0.5)
        c0 = self.stance["R"].base + (self.stance["L"].base - self.stance["R"].base) * w0
        pel = self.ch["pelvis"]
        for i in range(self.F):
            oL, oR = self.foot_origin("L", i), self.foot_origin("R", i)
            # a lifted foot does not pull the pelvis: use where it will be only through the weight value
            c = oR + (oL - oR) * w[i]
            d = c - c0
            pel[i, 0] += d.x * k_lat
            pel[i, 1] += -d.y * k_fwd
        tilt = (w - w0) * 2.0 * k_roll
        self.ch["Hips"][:, 2] += tilt
        self.ch["Hips"][:, 1] += -(w - w0) * 2.0 * L.get("hip_yaw", 1.5)
        self.ch["Spine"][:, 2] += -0.45 * self._lag(tilt, 1)
        self.ch["Chest"][:, 2] += -0.75 * self._lag(tilt, 2)
        # the unloaded foot rolls a little onto its toe when most of the weight has left it
        for s, sign in (("L", -1.0), ("R", 1.0)):
            free = np.clip(((w - 0.5) * sign * 2.0 - 0.25) / 0.6, 0.0, 1.0)
            rot = self.ch["foot%s.rot" % s]
            planted = self.contact[s] == C_FLAT
            add = L.get("free_heel", 5.0) * free * free * (3 - 2 * free)
            rot[:, 1] += np.where(planted & (rot[:, 1] == 0.0), add, 0.0)

    def layer_drag(self):
        L = self.style.get("layers", {})
        lag = self.cdef.get("layers", {}).get("drag", L.get("drag", 0.07))
        if lag <= 0:
            return
        v = self._deriv(self.ch["pelvis"])
        h = 0.5 * self.sc
        win = self._window(3)                      # a one-shot starts and ends exactly on its keyed pose
        roll = np.degrees(np.arctan(lag * v[:, 0] / h)) * win
        pitch = -np.degrees(np.arctan(lag * v[:, 1] / h)) * win
        self.ch["Spine"][:, 2] += 0.4 * roll
        self.ch["Chest"][:, 2] += 0.6 * roll
        self.ch["Spine"][:, 0] += 0.4 * pitch
        self.ch["Chest"][:, 0] += 0.6 * pitch

    def layer_breath(self):
        L = self.style.get("layers", {})
        amp = L.get("breath", 1.0) * self.cdef.get("layers", {}).get("breath", 1.0)
        T = self.n * DT
        if amp <= 0 or T < 1.6:
            return
        period = L.get("breath_period", 3.3)
        nb = max(1, int(round(T / period)))
        t = np.arange(self.F) / self.n
        ph = nb * t + 0.035 * np.sin(2 * math.pi * t) * (1 if nb > 1 else 0)
        ph = ph - 0.07 * np.sin(2 * math.pi * ph)              # inhale a little quicker than exhale
        b = 0.5 - 0.5 * np.cos(2 * math.pi * ph)
        b2 = self._shift(b[:, None], 3)[:, 0] if self.loop else b
        self.ch["Chest"][:, 0] += -1.5 * amp * b
        self.ch["Spine"][:, 0] += 0.55 * amp * b
        self.ch["Neck"][:, 0] += 0.6 * amp * b2
        self.ch["pelvis"][:, 2] += -0.0025 * amp * b * self.sc
        for s, sg in (("L", 1.0), ("R", -1.0)):
            self.ch["shoulder" + s][:, 0] += 1.8 * amp * b * (1.0 if s == "L" else 0.88)
            self.ch["UpperArm." + s][:, 2] += -sg * 0.9 * amp * b2

    def layer_noise(self):
        L = self.style.get("layers", {})
        amp = L.get("noise", 1.0) * self.cdef.get("layers", {}).get("noise", 1.0)
        T = self.n * DT
        if amp <= 0 or T < 1.2:
            return
        t = np.arange(self.F) / self.n
        win = self._window(8)
        seed = sum(ord(c) for c in self.name.split("@")[0])

        def wave(k, ph, m=1.0):
            # zero at frame 0 (so a loop still starts on the stand pose); the phase only picks the direction and size
            k = max(1, int(round(k * T / 4.0)))
            sgn = 1.0 if ((seed * 0.137 + ph) % 1.0) < 0.5 else -1.0
            return m * sgn * (0.75 + 0.5 * ((seed * 0.071 + ph * 3.0) % 1.0)) * np.sin(2 * math.pi * k * t) * win

        self.ch["Head"][:, 1] += amp * (1.1 * wave(1, 0.11) + 0.5 * wave(3, 0.62))
        self.ch["Head"][:, 0] += amp * (0.6 * wave(2, 0.35))
        self.ch["Head"][:, 2] += amp * (0.5 * wave(1, 0.80))
        self.ch["Chest"][:, 1] += amp * (0.6 * wave(1, 0.47))
        self.ch["Spine"][:, 2] += amp * (0.3 * wave(2, 0.21))
        self.ch["weight"][:, 0] += amp * 0.012 * wave(2, 0.05)
        self.ch["UpperArm.L"][:, 0] += amp * (0.7 * wave(1, 0.30) + 0.3 * wave(3, 0.9))
        self.ch["UpperArm.R"][:, 0] += amp * (0.6 * wave(2, 0.74))
        self.ch["LowerArm.L"][:, 0] += amp * (0.8 * wave(2, 0.55))
        self.ch["LowerArm.R"][:, 0] += amp * (0.9 * wave(1, 0.15))

    def layer_overlap(self):
        L = self.style.get("layers", {})
        g = L.get("overlap", 1.0) * self.cdef.get("layers", {}).get("overlap", 1.0)
        if g <= 0:
            return
        win = self._window(4)[:, None]
        for s in "LR":
            ua, la, ha = (self.ch["%s.%s" % (b, s)] for b in ARM_BONES)
            # the drag is capped: on a big fast move it must not straighten the elbow
            d = np.clip((self._shift(ua, 2) - ua) * win, -30.0, 30.0)
            la[:, 0] += np.clip(0.30 * g * d[:, 0], -7.0, 7.0)
            la[:, 2] += np.clip(0.15 * g * d[:, 2], -4.0, 4.0)
            tot = ua + la
            d2 = (self._shift(tot, 2) - tot) * win
            ha[:, 0] += np.clip(0.40 * g * d2[:, 0], -14.0, 14.0)
            ha[:, 2] += np.clip(0.40 * g * d2[:, 2], -14.0, 14.0)
        tor = self.ch["Hips"] + self.ch["Spine"] + self.ch["Chest"]
        d = (self._shift(tor, 3) - tor) * win
        self.ch["Neck"][:, :] += np.clip(0.15 * g * d, -4.0, 4.0)
        self.ch["Head"][:, :] += np.clip(0.20 * g * d, -6.0, 6.0)

    def spring(self, x, omega, zeta, sub=4):
        F = len(x)
        y = np.empty_like(x)
        s = x[0].copy()
        v = np.zeros_like(s)
        h = DT / sub
        reps = 3 if self.loop else 1
        for _ in range(reps):
            for i in range(F):
                prev = x[i - 1] if (i > 0 or self.loop) else x[0]
                for k in range(sub):
                    tgt = prev + (x[i] - prev) * ((k + 1) / sub)
                    a = omega * omega * (tgt - s) - 2.0 * zeta * omega * v
                    v = v + a * h
                    s = s + v * h
                y[i] = s
        if not self.loop:
            K = min(5, F - 1)
            for j in range(K):
                a = smooth((j + 1) / K)
                y[F - K + j] = y[F - K + j] * (1 - a) + x[F - K + j] * a
        return y

    def layer_springs(self):
        sp = dict(self.style.get("springs", {}))
        sp.update(self.cdef.get("springs", {}))
        for name, par in sp.items():
            if not par:
                continue
            targets = [name] if name in self.ch else [name + ".L", name + ".R"]
            for c in targets:
                if c in self.ch:
                    self.ch[c] = self.spring(self.ch[c], par[0], par[1])

    # ---- geometry per frame -----------------------------------------------------------------
    def foot_world(self, s, i):
        ft = self.rig.foot[s]
        st = self.stance[s]
        rot = self.ch["foot%s.rot" % s][i]
        O = self.foot_origin(s, i)
        Rz = Matrix.Rotation(rad(st.yaw + rot[0]), 3, "Z")
        Rx = Matrix.Rotation(rad(rot[1]), 3, "X")
        Rr = Matrix.Rotation(rad(rot[2]), 3, Vector((0, -1, 0)))
        P = ft.toe if rot[1] >= 0 else ft.heel
        a0 = Vector((0, 0, ft.h))
        Rpr = Rx @ Rr
        ankle = O + Rz @ (P + Rpr @ (a0 - P))
        return ankle, Rz @ Rpr

    def hips_world(self, i):
        rig = self.rig
        R = rig.rest3["Hips"] @ euler_q(self.ch["Hips"][i]).to_matrix()
        p = rig.head["Hips"] + W(self.ch["pelvis"][i])
        return R, p

    def limit_pelvis_height(self):
        """Lower the pelvis where a planted foot would be out of reach. A gait keeps its bob shape (whole curve
        moves down); other clips get a smooth local dip."""
        rig = self.rig
        ext = self.cdef.get("leg_ext", self.style.get("leg_ext", 0.996))
        R_leg = rig.leg_len * ext
        excess = np.zeros(self.F)
        for i in range(self.F):
            R, p = self.hips_world(i)
            for s in "LR":
                if self.contact[s][i] == C_NONE:
                    continue
                hip = p + R @ rig.rlt["UpperLeg." + s]
                ankle, _ = self.foot_world(s, i)
                dx, dy = hip.x - ankle.x, hip.y - ankle.y
                hd2 = dx * dx + dy * dy
                if hd2 >= R_leg * R_leg:
                    need = hip.z - ankle.z            # cannot be reached at any height: drop as far as sensible
                    excess[i] = max(excess[i], min(need, 0.25 * rig.leg_len))
                    continue
                zmax = ankle.z + math.sqrt(R_leg * R_leg - hd2)
                excess[i] = max(excess[i], hip.z - zmax)
        if excess.max() <= 0:
            return
        if self.is_gait and self.pelvis_mode != "local":
            self.ch["pelvis"][:, 2] -= excess.max()
            return
        e = excess.copy()
        for _ in range(3):                                    # dilate, then smooth, never below the need
            if self.loop:
                d = np.maximum(np.maximum(np.roll(e, 1), np.roll(e, -1)), e)
            else:
                d = np.maximum(np.maximum(np.concatenate(([e[0]], e[:-1])), np.concatenate((e[1:], [e[-1]]))), e)
            ker = np.array([0.25, 0.5, 0.25])
            pad = np.concatenate(([d[-1] if self.loop else d[0]], d, [d[0] if self.loop else d[-1]]))
            e = np.maximum(np.convolve(pad, ker, mode="valid"), excess)
        if not self.loop:                                     # the first and last frames keep their keyed height
            e[0], e[-1] = excess[0], excess[-1]
        self.ch["pelvis"][:, 2] -= e

    def solve_frame(self, i, store):
        rig = self.rig
        ch = self.ch
        q = {}
        R_h, p_h = self.hips_world(i)
        q["Hips"] = euler_q(ch["Hips"][i])
        loc = rig.rest3["Hips"].transposed() @ W(ch["pelvis"][i])
        w3 = {"Hips": R_h}
        wp = {"Hips": p_h}

        def fk(name, qq):
            p = rig.parent[name]
            wp[name] = wp[p] + w3[p] @ rig.rlt[name]
            w3[name] = w3[p] @ rig.rl3[name] @ qq.to_matrix()
            q[name] = qq

        fk("Spine", euler_q(ch["Spine"][i]))
        fk("Chest", euler_q(ch["Chest"][i]))
        # head levelling: undo part of what the torso did, half at the neck and half at the head
        hl = float(ch["head_level"][i][0])
        Rd = (w3["Chest"] @ rig.rest3["Chest"].transposed()).to_quaternion()
        C = Quaternion().slerp(Rd.inverted(), 0.5 * hl).to_matrix()
        for b in ("Neck", "Head"):
            p = rig.parent[b]
            base = w3[p] @ rig.rl3[b]
            Rn = C @ (base @ euler_q(ch[b][i]).to_matrix())
            fk(b, (base.inverted() @ Rn).to_quaternion())
        if "Jaw" in rig.has:
            fk("Jaw", euler_q([float(ch["jaw"][i][0]), 0, 0]))
        ey = ch["eyes"][i]
        for b in ("Eye.L", "Eye.R"):
            if b in rig.has:
                D = Matrix.Rotation(rad(ey[0]), 3, "Z") @ Matrix.Rotation(rad(ey[1]), 3, "X")
                fk(b, (rig.rest3[b].transposed() @ D @ rig.rest3[b]).to_quaternion())
        # arms
        for s, sg in (("L", 1.0), ("R", -1.0)):
            sh = ch["shoulder" + s][i]
            D = Matrix.Rotation(rad(sh[0]), 3, Vector((0, -sg, 0))) @ Matrix.Rotation(rad(-sg * sh[1]), 3, "Z")
            b = "Shoulder." + s
            fk(b, (rig.rest3[b].transposed() @ D @ rig.rest3[b]).to_quaternion())
            names = ["%s.%s" % (x, s) for x in ARM_BONES]
            eul = [ch[nm][i] for nm in names]
            qs = [euler_q(e) for e in eul]
            pin = float(ch["pin" + s][i][0])
            if pin > 1e-4:
                p_sh = wp[b] + w3[b] @ rig.rlt[names[0]]
                target = rig.head[names[2]] + W(ch["pin%s.pos" % s][i])
                pole = w3["Chest"] @ rig.rest3["Chest"].transposed() @ W([sg * 0.6, -0.55, -0.55])
                q1, q2, _, _, _, _, _ = rig.solve_limb("arm" + s, p_sh, target, pole, w3[b], ext=0.99)
                qs[0] = qs[0].slerp(q1, pin)
                qs[1] = qs[1].slerp(q2, pin)
            fk(names[0], qs[0])
            fk(names[1], qs[1])
            lock = float(ch["lock" + s][i][0])
            if lock > 1e-4:
                P = rig.prop[s]
                axis0 = P.axis if P else Vector((0, 0, 1))
                aim = W(ch["aim" + s][i])
                if aim.length < 1e-6:
                    aim = axis0.copy()
                D = axis0.rotation_difference(aim.normalized()).to_matrix()
                base = w3[names[1]] @ rig.rl3[names[2]]
                ql = (base.inverted() @ (D @ rig.rest3[names[2]])).to_quaternion()
                qs[2] = qs[2].slerp(ql, lock)
            fk(names[2], qs[2])
        # legs
        clamped = {}
        for s in "LR":
            L = rig.limb["leg" + s]
            hip = p_h + R_h @ rig.rlt[L.b1]
            ankle, Rf = self.foot_world(s, i)
            rot = ch["foot%s.rot" % s][i]
            yaw_k = 0.65 * (self.stance[s].yaw + rot[0]) + 0.35 * float(ch["Hips"][i][1])
            pole = Matrix.Rotation(rad(yaw_k), 3, "Z") @ Vector((0, -1, 0))
            q1, q2, R1, R2, mid, end, cl = rig.solve_limb("leg" + s, hip, ankle, pole, R_h, ext=1.0)
            clamped[s] = cl
            q[L.b1], q[L.b2] = q1, q2
            w3[L.b1], w3[L.b2] = R1, R2
            wp[L.b1], wp[L.b2] = hip, mid
            base = R2 @ rig.rl3[L.b3]
            fk(L.b3, (base.inverted() @ (Rf @ rig.rest3[L.b3])).to_quaternion())
            wp[L.b3] = end
            if "Toes." + s in rig.has:
                fk("Toes." + s, Quaternion())
        store["w3"].append(w3)
        store["wp"].append(wp)
        store["clamped"].append(clamped)
        return q, loc

    # ---- chains: cape, hood, skirt follow their parents ----------------------------------------
    def chains(self, store):
        rig = self.rig
        F = self.F
        out = {}
        cs = self.style.get("chains", {})
        w3, wp = store["w3"], store["wp"]
        if "Cape.1" in rig.has:
            par = cs.get("cape", {})
            pos = np.array([list(wp[i]["Chest"] + w3[i]["Chest"] @ rig.rlt["Cape.1"]) for i in range(F)])
            v = self._deriv(pos)
            lean = np.array([math.degrees(math.atan2(-w3[i]["Chest"].col[1].y, w3[i]["Chest"].col[1].z)) for i in range(F)])
            side = np.array([math.degrees(math.atan2(w3[i]["Chest"].col[1].x, w3[i]["Chest"].col[1].z)) for i in range(F)])
            yawv = self._deriv(np.array([[math.degrees(math.atan2(-w3[i]["Chest"].col[0].y, w3[i]["Chest"].col[0].x))]
                                         for i in range(F)]))[:, 0]
            # air drag from the ground running under an in-place clip (zero when the clip stands still)
            gd = self.ground_dist[:F]
            if self.is_gait and self.pelvis_mode != "local":
                gv = np.full(F, self.native_speed)
            else:
                gv = np.gradient(gd) * FPS
            wind = gv * par.get("wind", 0.55)
            win = self._window(3)       # velocity terms fade at the ends of a one-shot, so clips chain on the pose
            tx = (par.get("drag", 11.0) * (-v[:, 1]) + par.get("lift", 6.0) * np.abs(v[:, 2]) + 0.02 * np.abs(yawv)) * win
            tx = tx + par.get("drag", 11.0) * wind + np.maximum(0.0, -lean) * 1.0
            tx = np.clip(tx, -2.0, par.get("max", 48.0))
            tz = np.clip(par.get("side", 9.0) * v[:, 0] * win - 0.6 * side, -14.0, 14.0)
            tgt = np.stack([tx, np.zeros(F), tz], axis=1)
            om, ze = par.get("spring", [9.0, 0.32])
            share = par.get("share", [0.5, 0.3, 0.3])
            for k, b in enumerate(("Cape.1", "Cape.2", "Cape.3")):
                if b in rig.has:
                    y = self.spring(self._shift(tgt, 2 * k), om * (1.0 - 0.12 * k), ze)
                    y[:, 0] = np.maximum(y[:, 0], -2.0)
                    out[b] = np.radians(y * share[k])
                    if k > 0:
                        # leaning forward (a bow, a kneel, lying face down): below the shoulders the cloth hangs
                        # down over the back instead of standing out in line with the chest
                        drape = np.clip(par.get("drape", 0.45) * np.maximum(0.0, lean), 0.0, 38.0)
                        out[b][:, 0] -= np.radians(self.spring(drape[:, None], om, 0.7)[:, 0])
        if "Hood.1" in rig.has:
            par = cs.get("hood", {})
            pit = np.array([[math.degrees(math.atan2(-w3[i]["Head"].col[1].y, w3[i]["Head"].col[1].z)),
                             0.0,
                             math.degrees(math.atan2(w3[i]["Head"].col[1].x, w3[i]["Head"].col[1].z))] for i in range(F)])
            d = self._deriv(pit)
            tgt = np.clip(-par.get("drag", 0.035) * d, -5.0, 5.0) * self._window(3)[:, None]
            y = self.spring(tgt, *par.get("spring", [12.0, 0.4]))
            out["Hood.1"] = np.radians(y * 0.6)
            if "Hood.2" in rig.has:
                out["Hood.2"] = np.radians(self._shift(y, 2) * 0.8)
        if "Skirt.F" in rig.has:
            par = cs.get("skirt", {})
            f = np.zeros(F)
            b = np.zeros(F)
            lat = {"L": np.zeros(F), "R": np.zeros(F)}
            for i in range(F):
                Rh = w3[i]["Hips"]
                down = -Rh.col[1]
                fwd = Rh.col[2]
                left = Rh.col[0]
                for s in "LR":
                    hip = wp[i]["UpperLeg." + s]
                    for pt in (wp[i]["LowerLeg." + s], wp[i]["Foot." + s]):
                        d = pt - hip
                        a = math.degrees(math.atan2(d.dot(fwd), max(1e-4, d.dot(down))))
                        f[i] = max(f[i], a)
                        b[i] = max(b[i], -a)
                    d = wp[i]["Foot." + s] - hip
                    sgn = 1.0 if s == "L" else -1.0
                    lat[s][i] = max(0.0, sgn * math.degrees(math.atan2(d.dot(left), max(1e-4, d.dot(down)))) - 2.0)
            k = par.get("follow", 0.9)
            om, ze = par.get("spring", [16.0, 0.55])
            mx = par.get("max", 40.0)
            tr = {"Skirt.F": np.stack([-np.clip(k * f, 0, mx), np.zeros(F), np.zeros(F)], axis=1),
                  "Skirt.B": np.stack([np.clip(k * b, 0, mx), np.zeros(F), np.zeros(F)], axis=1),
                  "Skirt.L": np.stack([np.zeros(F), np.zeros(F), -np.clip(k * lat["L"], 0, mx)], axis=1),
                  "Skirt.R": np.stack([np.zeros(F), np.zeros(F), np.clip(k * lat["R"], 0, mx)], axis=1)}
            for nm, tgt in tr.items():
                if nm in rig.has:
                    out[nm] = np.radians(self.spring(tgt, om, ze))
        return out

    # ---- the whole clip ---------------------------------------------------------------------------
    def prepare(self):
        """Everything up to the per-frame solve: dense channels with all layers applied."""
        import anim_gait
        kind = self.cdef.get("type", "keyed")
        self.build_keyed()
        if kind == "gait":
            anim_gait.fill_cycle(self)
        elif kind in ("start", "stop", "turn"):
            anim_gait.fill_transition(self, kind)
        self.apply_steps()
        self.layer_weight()
        self.layer_drag()
        self.layer_breath()
        self.layer_noise()
        self.layer_overlap()
        self.layer_springs()
        if self.post_layers:
            self.post_layers(self)
        # a planted foot that is pitched stands on its toe or its heel (this is what the foot report measures)
        for s in "LR":
            pit = self.ch["foot%s.rot" % s][:, 1]
            c = self.contact[s]
            c[(c != C_NONE) & (pit > 0.5)] = C_TOE
            c[(c != C_NONE) & (pit < -0.5)] = C_HEEL
            c[(c != C_NONE) & (np.abs(pit) <= 0.5)] = C_FLAT
        self.limit_pelvis_height()
        if "hold" in self.cdef:
            self.meta["hold_frames"] = [int(self.fr(self.cdef["hold"][0])), int(self.fr(self.cdef["hold"][1]))]
        return self

    def build(self):
        self.prepare()
        store = {"w3": [], "wp": [], "clamped": []}
        rig = self.rig
        eul = {}
        prev = {}
        loc = np.zeros((self.F, 3))
        for i in range(self.F):
            q, l = self.solve_frame(i, store)
            loc[i] = l
            for b, qq in q.items():
                e = qq.to_euler("XYZ", prev[b]) if b in prev else qq.to_euler("XYZ")
                prev[b] = e
                eul.setdefault(b, np.zeros((self.F, 3)))[i] = e
        for b, arr in self.chains(store).items():
            eul[b] = arr
        if self.loop:                                   # close the loop: frame n repeats frame 0
            loc = np.concatenate([loc, loc[:1]], axis=0)
            for b in eul:
                first = eul[b][:1].copy()
                last = eul[b][-1]
                first = first - 2 * math.pi * np.round((first - last) / (2 * math.pi))
                eul[b] = np.concatenate([eul[b], first], axis=0)
            for s in "LR":
                self.contact[s] = np.concatenate([self.contact[s], self.contact[s][:1]])
            self.ground_dist = np.concatenate([self.ground_dist, [self.ground_dist[0] + self.native_speed * self.n * DT]])
            self.ground_yaw = np.concatenate([self.ground_yaw, self.ground_yaw[:1]])
        B = Baked()
        B.name = self.name
        B.frames = self.n
        B.loop = self.loop
        B.euler = eul
        B.loc = loc
        B.events = sorted(self.events, key=lambda e: (e["frame"], e["name"]))
        B.native_speed = self.native_speed
        B.contact = self.contact
        B.ground_dist = self.ground_dist
        B.ground_yaw = self.ground_yaw
        B.clamped_planted = sum(1 for i, c in enumerate(store["clamped"]) for s in "LR"
                                if c[s] and self.contact[s][i] != C_NONE)
        B.meta = dict(self.meta)
        B.meta.update(self.cdef.get("meta", {}))
        return B


# ----------------------------------------------------------------------------------------
# Writing actions
# ----------------------------------------------------------------------------------------
def _new_action(name, arm):
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    return act


def _fcurve_factory(act, arm):
    """Blender 4.4+ layered actions with a fallback to the legacy API."""
    try:
        slot = act.slots.new(id_type="OBJECT", name="Armature")
        layer = act.layers.new("Layer")
        strip = layer.strips.new(type="KEYFRAME")
        bag = strip.channelbag(slot, ensure=True)

        def make(path, index, group):
            fc = bag.fcurves.new(path, index=index)
            g = bag.groups.get(group) or bag.groups.new(group)
            fc.group = g
            return fc
        return make
    except (AttributeError, TypeError):
        def make(path, index, group):
            return act.fcurves.new(path, index=index, action_group=group)
        return make


def write_action(arm, baked):
    act = _new_action(baked.name, arm)
    make = _fcurve_factory(act, arm)
    nF = baked.loc.shape[0]
    frames = np.arange(nF, dtype=np.float64)

    def put(path, index, group, values):
        fc = make(path, index, group)
        fc.keyframe_points.add(nF)
        co = np.empty(nF * 2)
        co[0::2] = frames
        co[1::2] = values
        fc.keyframe_points.foreach_set("co", co)
        fc.keyframe_points.foreach_set("interpolation", [1] * nF)      # LINEAR
        fc.update()

    for i in range(3):
        put('pose.bones["Hips"].location', i, "Hips", baked.loc[:, i])
    for b in sorted(baked.euler):
        if b not in arm.pose.bones:
            continue
        arm.pose.bones[b].rotation_mode = "XYZ"
        for i in range(3):
            put('pose.bones["%s"].rotation_euler' % b, i, b, baked.euler[b][:, i])
    act.use_frame_range = True
    act.frame_start = 0
    act.frame_end = baked.frames
    try:
        act.use_cyclic = bool(baked.loop)
    except AttributeError:
        pass
    return act


def assign_action(arm, act):
    """Make `act` the armature's active action (handles action slots on Blender 4.4+)."""
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    try:
        if act is not None and arm.animation_data.action_slot is None and len(act.slots):
            arm.animation_data.action_slot = act.slots[0]
    except AttributeError:
        pass
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"


# ----------------------------------------------------------------------------------------
# Public API
# ----------------------------------------------------------------------------------------
def clip_set_of(name):
    base = name.split("@")[0]
    for s, names in contract.CLIP_SETS.items():
        if base in names:
            return s
    return None


def variant_def(cdef, key):
    """Clip definition for a character: the base definition with its 'variants'[key] merged over it."""
    v = cdef.get("variants", {}).get(key)
    if v is None:
        return cdef
    if v.get("replace"):
        out = {k: val for k, val in v.items() if k != "replace"}
        for k in ("type", "set"):
            if k in cdef and k not in out:
                out[k] = cdef[k]
        return out
    return _deep_merge({k: val for k, val in cdef.items() if k != "variants"}, v)


def wants_variant(name, cdef, style):
    """A character gets <Clip>@<key> when the clip has its own variant data or the style lists the clip."""
    key = style["key"]
    if "only_for" in cdef and key not in cdef["only_for"]:
        return False
    if key in cdef.get("variants", {}):
        return True
    want = style.get("variants", [])
    return name in want or clip_set_of(name) in want


def build_one(rig, style, name, cdef, poses, all_clips):
    key = style["key"]
    action_name = name if key is None else "%s@%s" % (name, key)
    cd = cdef if key is None else variant_def(cdef, key)
    b = ClipBuilder(rig, style, action_name, cd, poses, all_clips)
    baked = b.build()
    baked.base = name
    baked.style_key = key
    return baked


def record(baked):
    """Manifest record (contract 2.6) plus optional extra fields (additive: travel_m, turn_deg, style, base)."""
    name = baked.name
    base = baked.base
    rec = {
        "name": name, "set": clip_set_of(base), "length_s": common.r(baked.frames / FPS, 4), "frames": baked.frames,
        "fps": FPS, "loop": bool(baked.loop), "additive": base in contract.ADDITIVE_CLIPS,
        "native_speed_mps": common.r(baked.native_speed, 3),
        "events": [{"name": e["name"], "frame": int(e["frame"])} for e in baked.events],
    }
    if baked.style_key:
        rec["base"] = base
        rec["style"] = baked.style_key
    for k, v in baked.meta.items():
        rec[k] = v
    return rec


def build_clips(ctx_or_armature, set_names=None, style=True, only=None, keep=None):
    """Create one action per clip on the armature (contract 2.8).

    ctx_or_armature  a BuildContext (uses ctx.armature, writes manifest section 'clips') or an armature object
    set_names        list of clip sets (contract.CLIP_SETS keys); None = all
    style            True: base clips plus <Clip>@<key> variants for every style file;
                     False: base clips only; a list of cast keys: base clips plus those characters' variants
    only             optional list of base clip names to build (development)
    keep             optional dict that receives {action name: Baked} (for reports and previews)
    Returns the list of clip records. The same armature works for every character: lengths are read from it.
    """
    ctx = None
    arm = ctx_or_armature
    if not isinstance(ctx_or_armature, bpy.types.Object):
        ctx = ctx_or_armature
        arm = ctx.armature
    if arm is None or arm.type != "ARMATURE":
        raise RuntimeError("build_clips needs an armature (run rig.fit_rig or stub_rig.build_stub_armature first)")
    rig = Rig(arm)
    clips, poses = load_clip_data()
    sets = list(contract.CLIP_SETS) if set_names is None else list(set_names)
    names = [c for s in sets for c in contract.CLIP_SETS[s]]
    missing = [c for c in names if c not in clips]
    if only:
        names = [c for c in names if c in only]
    if style is True:
        keys = style_keys()
    elif not style:
        keys = []
    else:
        keys = list(style)
    styles = [load_style(None)] + [load_style(k) for k in keys]
    records = []
    for name in names:
        if name not in clips:
            continue
        cdef = clips[name]
        for st in styles:
            if st["key"] is not None and not wants_variant(name, cdef, st):
                continue
            if st["key"] is None and cdef.get("base") is False:
                continue
            baked = build_one(rig, st, name, cdef, poses, clips)
            write_action(arm, baked)
            records.append(record(baked))
            if keep is not None:
                keep[baked.name] = baked
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    if missing and not only:
        common.log("clips in the contract with no data yet:", ", ".join(missing))
    if ctx is not None:
        ctx.set_section("clips", records)
    return records
