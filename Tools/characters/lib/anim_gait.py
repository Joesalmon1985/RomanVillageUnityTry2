"""Gait cycle and the start, stop and turn generators (WP-5). Called by anim_core.ClipBuilder.

All locomotion is authored in place. The ground is what moves:
  cycle   the ground runs back at the clip's native speed; a stance foot rides it exactly (heel pivot at the
          strike, flat, then toe pivot), so in the game the foot is still when the root moves at native speed
  start   the ground accelerates from rest (distance profile in the clip record: travel_m); ends on a walk phase
  stop    the ground decelerates to rest; the front foot stays planted for the whole clip, the rear foot closes
  turn    the root turns in place (yaw profile in the clip record); feet are fixed in the world and step round

Parameters live in the clip data (clips/locomotion.py "gait") and can be overridden per character
(clips/style/<key>.json "gait"). Lengths are metres for a REF_LEG character and are scaled to the rig, except
"speed", which is the real native ground speed of the baked clip on this armature.
"""

import math

import numpy as np

import anim_core as ac
from anim_core import C_NONE, C_HEEL, C_FLAT, C_TOE, FPS, smooth, smoother

DEFAULT_GAIT = {
    "speed": 1.4, "duty": 0.62, "bias": -0.09, "track": 0.0, "toe": 0.0, "drop": 0.0,
    "heel_strike": 20.0, "rolldown": 0.07, "heel_off": 0.58, "toe_off": 42.0, "lift": 0.035, "swing_peak": 0.45,
    "kick": 0.0,
    "bob": 0.018, "bob_phase": 0.04, "sway": 0.022, "surge": 0.006,
    "hip_yaw": 6.0, "hip_roll": 3.5, "hip_pitch": 1.5,
    "chest_yaw": 5.0, "chest_roll": 2.0, "chest_pitch": 2.0, "chest_nod": 0.8,
    "arm_fwd": 22.0, "arm_back": 16.0, "arm_lag": 0.04, "elbow": 10.0, "elbow_dyn": 14.0, "arm_cross": 4.0,
    "arm_out": 0.0, "hand_flop": 7.0, "shoulder": 3.0, "arm_gain": {"L": 0.92, "R": 1.06},
    "head_nod": 1.0, "head_level": 0.85,
}

BODY_CHANNELS = ["pelvis", "Hips", "Spine", "Chest", "Neck", "Head", "head_level", "shoulderL", "shoulderR",
                 "UpperArm.L", "LowerArm.L", "Hand.L", "UpperArm.R", "LowerArm.R", "Hand.R"]


def gait_params(B, gait_name=None):
    """Defaults, then the gait clip's own numbers, then the style's overrides ('walk' for Walk and WalkBrisk, and per clip)."""
    g = dict(DEFAULT_GAIT)
    base = (gait_name or B.name).split("@")[0]
    src = B.cdef if gait_name is None else B.all_clips[base]
    if gait_name is not None and B.style["key"]:
        src = ac.variant_def(src, B.style["key"], B.style.get("costume"))
    g.update(src.get("gait", {}))
    sg = B.style.get("gait", {})
    if base in ("Walk", "WalkBrisk"):
        g.update(sg.get("walk", {}))          # the style's walking numbers; faster gaits keep their own unless named
    g.update(sg.get(base, {}))
    tempo = float(B.style.get("stride", B.style.get("tempo", 1.0))) if src.get("tempo", True) else 1.0
    g["frames"] = max(2, int(round(src["frames"] * tempo)))
    return g


def _hermite(p0, p1, m0, m1, u):
    u2, u3 = u * u, u * u * u
    return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1


def _stance_pitch(p, g):
    """Pitch and contact code for stance phase p in [0, duty)."""
    d = g["duty"]
    r = g["rolldown"]
    ho = g["heel_off"] * d
    if p < r:
        return -g["heel_strike"] * (1.0 - smooth(p / r)), C_HEEL
    if p < ho:
        return 0.0, C_FLAT
    u = (p - ho) / (d - ho)
    return g["toe_off"] * (u ** 1.6), C_TOE


def _swing_pitch(u, g):
    um = g["swing_peak"]
    if u < um:
        return g["toe_off"] * (1.0 - smooth(u / um)) + g["kick"] * math.sin(math.pi * u / um)
    return -g["heel_strike"] * smooth((u - um) / (1.0 - um))


def _swing_lift(u, g, sc):
    return g["lift"] * sc * math.sin(math.pi * u ** 0.8) ** 2


def foot_cycle(p, g, sc):
    """One foot at its own phase p (0 = heel strike). Returns (fwd, up, pitch, contact) in root space."""
    T = g["frames"] / FPS
    v = g["speed"]
    Lc = v * T
    d = g["duty"]
    strike = d * Lc / 2.0 + g["bias"] * sc
    if p < d:
        pitch, c = _stance_pitch(p, g)
        return strike - Lc * p, 0.0, pitch, c
    u = (p - d) / (1.0 - d)
    m = -v * (1.0 - d) * T
    fwd = _hermite(strike - Lc * d, strike, m, m, u)
    return fwd, _swing_lift(u, g, sc), _swing_pitch(u, g), C_NONE


def cycle_state(B, g, phi):
    """Every channel of the gait at phase phi (0 = left heel strike). Values are absolute channel values."""
    sc = B.sc
    st = B.stand_ch
    two = 2.0 * math.pi
    out = {}
    contact = {}
    for s, off, sg in (("L", 0.0, 1.0), ("R", 0.5, -1.0)):
        fwd, up, pitch, c = foot_cycle((phi + off) % 1.0, g, sc)
        out["foot%s.pos" % s] = np.array([sg * g["track"] * sc, fwd, up])
        out["foot%s.rot" % s] = np.array([sg * g["toe"], pitch, 0.0])
        contact[s] = c
    # pelvis: lowest in double support (walk) or mid-stance (run), over the stance foot, hip of the swing leg forward
    bob = -g["bob"] * sc * math.cos(2 * two * (phi - g["bob_phase"]))
    sway = g["sway"] * sc * math.sin(two * (phi - 0.05))
    surge = g["surge"] * sc * math.sin(2 * two * (phi - 0.02))
    out["pelvis"] = np.array(st["pelvis"]) + np.array([sway, surge, bob - g["drop"] * sc])
    yaw = -g["hip_yaw"] * math.cos(two * phi)
    roll = g["hip_roll"] * math.sin(two * (phi - 0.07))
    out["Hips"] = np.array(st["Hips"]) + np.array([g["hip_pitch"], yaw, roll])
    # spine and chest wind the other way, a little late
    cy = math.cos(two * (phi - 0.035))
    sr = math.sin(two * (phi - 0.10))
    out["Spine"] = np.array(st["Spine"]) + np.array([0.3 * g["chest_pitch"], 0.5 * g["hip_yaw"] * cy,
                                                    -0.5 * g["hip_roll"] * sr])
    nod = g["chest_nod"] * math.cos(2 * two * (phi - 0.09))
    out["Chest"] = np.array(st["Chest"]) + np.array([0.7 * g["chest_pitch"] + nod,
                                                    (0.5 * g["hip_yaw"] + g["chest_yaw"]) * math.cos(two * (phi - 0.06)),
                                                    -(0.5 * g["hip_roll"] + g["chest_roll"]) * sr])
    hn = g["head_nod"] * math.cos(2 * two * (phi - 0.16))
    out["Neck"] = np.array(st["Neck"]) + np.array([-0.4 * g["chest_pitch"] + 0.4 * hn, 0.0, 0.0])
    out["Head"] = np.array(st["Head"]) + np.array([-0.3 * g["chest_pitch"] + 0.6 * hn, 0.0, 0.0])
    out["head_level"] = np.array([g["head_level"]])
    # arms swing against the legs: right arm forward on the left heel strike
    pr = B.style.get("prop") or {}
    carry_hand = pr.get("hand") if pr.get("walk") == "carry" else None
    for s, off, sg in (("R", 0.0, -1.0), ("L", 0.5, 1.0)):
        gain = g["arm_gain"].get(s, 1.0)
        carry = s == carry_hand
        if carry:
            # a hand that carries a weapon swings much less and is held a little out, so the weapon never passes
            # through the leg (integration fix: the champion's sword and club swept 57 cm through his thigh)
            gain *= pr.get("swing", 0.3)
        lag = g["arm_lag"] + (0.012 if s == "L" else 0.0)
        sw = math.cos(two * (phi - off - lag))
        mid = 0.5 * (g["arm_fwd"] + g["arm_back"]) * gain
        bias = 0.5 * (g["arm_fwd"] - g["arm_back"]) * gain
        ua = np.array(st["UpperArm." + s], dtype=float)
        ua[0] += -(bias + mid * sw)
        ua[2] += sg * ((0.0 if carry else g["arm_cross"] * max(0.0, sw)) - g["arm_out"] - (pr.get("out_deg", 8.0) if carry else 0.0))
        la = np.array(st["LowerArm." + s], dtype=float)
        la[0] += -(g["elbow"] + g["elbow_dyn"] * (0.5 + 0.5 * math.cos(two * (phi - off - lag - 0.07)))) * gain
        if carry:
            la[0] += -pr.get("elbow", 6.0)
        ha = np.array(st["Hand." + s], dtype=float)
        ha[0] += g["hand_flop"] * math.sin(two * (phi - off - lag - 0.12))
        out["UpperArm." + s], out["LowerArm." + s], out["Hand." + s] = ua, la, ha
        out["shoulder" + s] = np.array(st["shoulder" + s]) + np.array([0.0, g["shoulder"] * sw])
    return out, contact


def fill_cycle(B):
    g = gait_params(B)
    B.is_gait = True
    B.native_speed = g["speed"]
    n = B.n
    for i in range(B.F):
        st, contact = cycle_state(B, g, i / n)
        for c, v in st.items():
            B.ch[c][i] = v
        for s in "LR":
            B.contact[s][i] = contact[s]
        B.ground_dist[i] = g["speed"] * i / FPS
    B.events = [{"name": "FootL", "frame": 0}, {"name": "FootR", "frame": int(round(n * 0.5))}]
    carry_prop(B, g)


def carry_prop(B, g):
    """Walking with a planted staff (style 'prop': {'hand': 'L', 'walk': 'staff'}): the staff's foot rides the ground
    like a foot, once per cycle with the opposite leg, and the hand is pinned so the arm follows the staff.
    Walking with a carried weapon (style 'prop': {'hand': 'R', 'walk': 'carry', 'aim': [left, fwd, up], ...}): the arm
    swing is cut (cycle_state) and the weapon is locked to a resting direction, low and a little out from the leg,
    rocking slightly with the stride."""
    pr = B.style.get("prop")
    if not pr or not B.is_gait:
        return
    s = pr["hand"]
    P = B.rig.prop[s]
    if P is None:
        return
    if pr.get("walk") == "carry":
        aim0 = np.array(pr.get("aim", [-0.30, 0.10, -0.95]), dtype=float)
        if s == "L":
            aim0[0] = -aim0[0] if pr.get("aim_side_auto", True) and aim0[0] < 0 else aim0[0]
        rock = pr.get("rock", 0.06)
        for i in range(B.F):
            ph = 2.0 * math.pi * (i / B.n)
            a = aim0 + np.array([0.0, rock * math.cos(ph - 0.6), 0.0])
            B.ch["aim" + s][i] = list(a / np.linalg.norm(a))
            B.ch["lock" + s][i] = pr.get("lock", 1.0)
        return
    if pr.get("walk") != "staff":
        return
    if g.get("prop") == "carry":
        # too fast to plant a staff: it is carried off the ground, leaning forward, rocking a little with the stride
        aim0 = pr.get("carry_aim", [0.0, 0.34, 0.94])
        sgn = 1.0 if s == "L" else -1.0
        ua, la = B.ch["UpperArm." + s], B.ch["LowerArm." + s]
        st = B.stand_ch
        for i in range(B.F):
            ph = 2.0 * math.pi * (i / B.n)
            B.ch["pin" + s][i] = 0.0
            B.ch["lock" + s][i] = 1.0
            B.ch["aim" + s][i] = [aim0[0] + 0.03 * sgn * math.sin(ph), aim0[1] + 0.07 * math.sin(2 * ph + 0.6), aim0[2]]
            ua[i] = np.array(st["UpperArm." + s]) + np.array([-16.0 + 6.0 * math.sin(ph + 0.4), 0.0, -sgn * 6.0])
            la[i] = np.array(st["LowerArm." + s]) + np.array([-40.0 + 5.0 * math.sin(2 * ph), 0.0, 0.0])
        return
    rig = B.rig
    n = B.n
    sc = B.sc
    T = n / FPS
    v = g["speed"]
    Lc = v * T
    wrist0 = rig.head["Hand." + s]
    grip_off = P.grip - wrist0                         # grip relative to the wrist, rest orientation
    below = (P.grip.z - 0.03) / max(0.2, P.axis.z)     # grip to the staff's foot, along the axis
    off = 0.5 if s == "L" else 0.0                     # plants with the opposite foot's strike
    duty = pr.get("duty", 0.5)
    reach = pr.get("reach", 0.26) * sc
    hand_fwd = pr.get("hand_fwd", 0.10) * sc
    hand_in = pr.get("hand_in", 0.03) * sc
    sgn = 1.0 if s == "L" else -1.0
    for i in range(B.F):
        p = (i / n + off + pr.get("phase", 0.0)) % 1.0
        if p < duty:
            tip_f = reach - Lc * p
            tip_u = 0.0
        else:
            u = (p - duty) / (1.0 - duty)
            m = -v * (1.0 - duty) * T
            tip_f = _hermite(reach - Lc * duty, reach, m, m, u)
            tip_u = pr.get("lift", 0.07) * sc * math.sin(math.pi * u) ** 1.5
        # where the grip wants to be: near its rest place, riding a little with the staff
        gr = np.array([P.grip.x - sgn * hand_in, -(P.grip.y) + hand_fwd + 0.18 * (tip_f - hand_fwd), P.grip.z])
        gr[2] += float(B.ch["pelvis"][i][2]) * 0.6
        tip = np.array([P.grip.x - sgn * hand_in * 0.5 + pr.get("tip_out", 0.02) * sgn, tip_f, 0.03 + tip_u])
        d = gr - tip
        d = d / np.linalg.norm(d)
        grip = tip + d * below
        aim = [d[0], d[1], d[2]]                        # staff head direction (grip to head) = tip to grip
        B.ch["aim" + s][i] = aim
        B.ch["lock" + s][i] = 1.0
        # the wrist that puts the grip there: rest offset rotated with the staff
        D = ac.W([P.axis.x, -P.axis.y, P.axis.z]).rotation_difference(ac.W(aim)).to_matrix()
        wr = ac.W(grip) - D @ grip_off
        B.ch["pin" + s][i] = 1.0
        B.ch["pin%s.pos" % s][i] = [wr.x - wrist0.x, -(wr.y - wrist0.y), wr.z - wrist0.z]


# ----------------------------------------------------------------------------------------
# Transitions
# ----------------------------------------------------------------------------------------
_GAIT_CACHE = {}


def gait_post(B, gait_name):
    """The gait clip for the same rig and style, with every layer applied (channels per frame)."""
    key = (id(B.rig), B.style["key"], B.style.get("costume"), gait_name)
    if key not in _GAIT_CACHE:
        cdef = B.all_clips[gait_name]
        if B.style["key"]:
            cdef = ac.variant_def(cdef, B.style["key"], B.style.get("costume"))
        G = ac.ClipBuilder(B.rig, B.style, gait_name, cdef, B.poses, B.all_clips)
        G.prepare()
        _GAIT_CACHE[key] = G
    return _GAIT_CACHE[key]


def _gait_at(G, c, frame):
    """Channel c of the looping gait builder at a fractional frame."""
    n = G.n
    f = frame % n
    i0 = int(math.floor(f)) % n
    i1 = (i0 + 1) % n
    u = f - math.floor(f)
    return G.ch[c][i0] * (1 - u) + G.ch[c][i1] * u


def _blend_body(B, G, frame_of, beta):
    """Cross-blend the body channels of the transition toward the gait (beta[i] = share of the gait)."""
    for c in BODY_CHANNELS + ["lockL", "lockR", "aimL", "aimR", "pinL", "pinR", "pinL.pos", "pinR.pos"]:
        if c not in B.ch or c not in G.ch:
            continue
        for i in range(B.F):
            if beta[i] > 0.0:
                B.ch[c][i] = B.ch[c][i] * (1 - beta[i]) + _gait_at(G, c, frame_of(i)) * beta[i]


def _lateral_from_weight(B, w):
    """Pelvis side shift and hip drop from a weight track (root space, feet at their stance width)."""
    L = B.style.get("layers", {})
    half = 0.5 * (B.stance["L"].base.x - B.stance["R"].base.x)
    B.ch["pelvis"][:, 0] += L.get("shift_lat", 0.7) * (w - 0.5) * 2.0 * half
    tilt = (w - 0.5) * 2.0 * L.get("hip_drop", 4.0)
    B.ch["Hips"][:, 2] += tilt
    B.ch["Spine"][:, 2] += -0.45 * tilt
    B.ch["Chest"][:, 2] += -0.75 * tilt


def fill_transition(B, kind):
    if kind == "turn":
        return fill_turn(B)
    cdef = B.cdef
    gname = cdef.get("gait_clip", "Walk")
    g = gait_params(B, gname)
    G = gait_post(B, gname)
    sc = B.sc
    n = B.n
    Ng = g["frames"]
    T = Ng / FPS
    v = g["speed"]
    Lc = v * T
    d = g["duty"]
    strike = d * Lc / 2.0 + g["bias"] * sc
    B.skip_weight = True
    s1 = "R" if B.mirror else "L"            # start: the foot that steps first; stop: the planted front foot
    s2 = "L" if s1 == "R" else "R"
    sg1 = 1.0 if s1 == "L" else -1.0
    phi0 = 0.0 if s1 == "L" else 0.5         # gait phase of s1's heel strike
    pos1, rot1 = B.ch["foot%s.pos" % s1], B.ch["foot%s.rot" % s1]
    pos2, rot2 = B.ch["foot%s.pos" % s2], B.ch["foot%s.rot" % s2]
    w_on2 = 0.1 if s1 == "L" else 0.9        # weight value that puts the body over s2
    w_on1 = 1.0 - w_on2
    Gd = np.zeros(B.F)
    w = np.full(B.F, 0.5)
    if kind == "start":
        # s1 steps first; the clip ends on the gait at phase phi_end after s1's strike (s2's toe-off by default)
        phi_end = cdef.get("phase_end", d - 0.5)
        t_push = B.fr(cdef.get("push_frame", 3))
        i_lo = B.fr(cdef.get("lift_frame", 5))
        f_hs = n - phi_end * Ng                                  # frame of s1's heel strike
        D = Lc * d - strike                                      # ground travel that puts s2 on its toe-off
        Tp = (n - t_push) / FPS

        def Gat(f):
            u = min(1.0, max(0.0, (f - t_push) / (n - t_push)))
            return (3 * D - v * Tp) * u * u + (v * Tp - 2 * D) * u * u * u

        def dG(f):
            u = min(1.0, max(0.0, (f - t_push) / (n - t_push)))
            return (2 * (3 * D - v * Tp) * u + 3 * (v * Tp - 2 * D) * u * u) / (n - t_push)   # per frame

        land = strike - Lc * phi_end + (D - Gat(f_hs))            # root-space forward of s1 at its strike
        for i in range(B.F):
            Gd[i] = Gat(i)
            # s2: planted the whole clip, heel coming up as in the gait
            p2 = phi_end + 0.5 - (n - i) / Ng
            pit, c = (0.0, C_FLAT)
            if p2 >= g["heel_off"] * d:
                pit, c = _stance_pitch(min(p2, d - 1e-6), g)
            pos2[i] = [0.0, -Gd[i], 0.0]
            rot2[i] = [0.0, pit, 0.0]
            B.contact[s2][i] = c
            if i <= i_lo:
                pos1[i] = [0.0, -Gd[i], 0.0]
                rot1[i] = [0.0, 0.0, 0.0]
                B.contact[s1][i] = C_FLAT
            elif i < f_hs:
                u = (i - i_lo) / (f_hs - i_lo)
                span = f_hs - i_lo
                fwd = _hermite(-Gat(i_lo), land, -dG(i_lo) * span, -v / FPS * span, u)
                up = (g["lift"] * 1.25) * sc * math.sin(math.pi * u ** 0.8) ** 2
                um = 0.5
                pit = 16.0 * math.sin(math.pi * u / um) if u < um else -g["heel_strike"] * smooth((u - um) / (1 - um))
                pos1[i] = [sg1 * g["track"] * sc * smooth(u), fwd, up]
                rot1[i] = [sg1 * g["toe"] * smooth(u), pit, 0.0]
                B.contact[s1][i] = C_NONE
            else:
                pit, c = _stance_pitch((i - f_hs) / Ng, g)
                pos1[i] = [sg1 * g["track"] * sc, land - (Gd[i] - Gat(f_hs)), 0.0]
                rot1[i] = [sg1 * g["toe"], pit, 0.0]
                B.contact[s1][i] = c
            # weight: over s2 before s1 lifts (the pelvis starts on frame 0, the foot leaves on i_lo)
            w[i] = 0.5 + (w_on2 - 0.5) * smoother(i / max(1.0, i_lo - 0.5)) if i <= i_lo else w_on2
        _lateral_from_weight(B, w)
        frame_of = lambda i: (phi0 + phi_end) * Ng - (n - i)
        beta = np.array([smoother((i - (i_lo - 1)) / max(1.0, (f_hs - 1) - (i_lo - 1))) for i in range(B.F)])
        _blend_body(B, G, frame_of, beta)
        B.events = [{"name": "Foot" + s1, "frame": int(round(f_hs))}]
        B.meta["travel_m"] = round(float(D), 4)
        B.meta["exit_phase"] = round(float((phi0 + phi_end) % 1.0), 4)

        def post(B_):
            # exact hand-over: the last frames take the gait's finished channels (springs and all)
            be = np.array([smoother((i - (n - 4)) / 4.0) for i in range(B.F)])
            _blend_body(B, G, frame_of, be)
        B.post_layers = post
    else:
        # stop: starts on the gait at s1's heel strike; s1 never leaves the ground, s2 closes beside it
        n_g = B.fr(cdef.get("glide_frames", 12))
        i_to = int(round((d - 0.5) * Ng))
        i_land = B.fr(cdef.get("land_frame", 11))
        D = strike                                              # s1 ends under the hip
        Tp = n_g / FPS

        def Gat(f):
            u = min(1.0, max(0.0, f / n_g))
            return v * Tp * u + (3 * D - 2 * v * Tp) * u * u + (v * Tp - 2 * D) * u * u * u

        def dG(f):
            u = min(1.0, max(0.0, f / n_g))
            return (v * Tp + 2 * (3 * D - 2 * v * Tp) * u + 3 * (v * Tp - 2 * D) * u * u) / n_g

        rear0 = strike - Lc * 0.5
        p_to, _ = _stance_pitch(min(0.5 + i_to / Ng, d - 1e-6), g)
        for i in range(B.F):
            Gd[i] = Gat(i)
            pit, c = _stance_pitch(i / Ng, g) if i / Ng < g["rolldown"] else (0.0, C_FLAT)
            pos1[i] = [sg1 * g["track"] * sc, strike - Gd[i], 0.0]
            rot1[i] = [sg1 * g["toe"], pit, 0.0]
            B.contact[s1][i] = c
            if i <= i_to:
                pit, c = _stance_pitch(min(0.5 + i / Ng, d - 1e-6), g)
                pos2[i] = [-sg1 * g["track"] * sc, rear0 - Gd[i], 0.0]
                rot2[i] = [-sg1 * g["toe"], pit, 0.0]
                B.contact[s2][i] = c
            elif i < i_land:
                u = (i - i_to) / (i_land - i_to)
                span = i_land - i_to
                fwd = _hermite(rear0 - Gat(i_to), D - Gat(i_land), -dG(i_to) * span, -dG(i_land) * span, u)
                up = 0.8 * g["lift"] * sc * math.sin(math.pi * u ** 0.8) ** 2
                pit = p_to * (1.0 - smooth(u / 0.55)) if u < 0.55 else 7.0 * math.sin(math.pi * (u - 0.55) / 0.45)
                pos2[i] = [-sg1 * g["track"] * sc * (1 - smooth(u)), fwd, up]
                rot2[i] = [-sg1 * g["toe"] * (1 - smooth(u)), pit, 0.0]
                B.contact[s2][i] = C_NONE
            else:
                pos2[i] = [0.0, D - Gd[i], 0.0]
                rot2[i] = [0.0, 0.0, 0.0]
                B.contact[s2][i] = C_FLAT
            # weight: on the planted front foot while the other closes, then to the middle with a small overshoot
            if i <= i_land:
                w[i] = 0.5 + (w_on1 - 0.5) * smoother(i / max(1.0, i_to + 2.0))
            else:
                u = min(1.0, (i - i_land) / max(1.0, (n - i_land) * 0.8))
                w[i] = 0.5 + (w_on1 - 0.5) * ((1.0 - smoother(u)) - 0.12 * math.sin(math.pi * u))
        _lateral_from_weight(B, w)
        frame_of = lambda i: phi0 * Ng + float(i)
        beta = np.array([1.0 - smoother(i / 5.0) for i in range(B.F)])
        _blend_body(B, G, frame_of, beta)
        B.events = [{"name": "Foot" + s2, "frame": int(i_land)}]
        B.meta["travel_m"] = round(float(D), 4)
        B.meta["enter_phase"] = round(float(phi0), 4)

        def post(B_):
            be = np.array([1.0 - smoother(i / 3.0) for i in range(B.F)])
            _blend_body(B, G, frame_of, be)
        B.post_layers = post
    B.native_speed = v
    B.meta["gait"] = gname
    B.ground_dist[:] = Gd
    B.pelvis_mode = "local"


def fill_turn(B):
    """Turn in place by cdef['turn_deg'] to the left (to the right when the clip is a mirror). The root's yaw
    follows a smooth profile; each foot is fixed in the world while planted and steps round to its new stance."""
    cdef = B.cdef
    n = B.n
    sc = B.sc
    sgn = -1.0 if B.mirror else 1.0
    ang = sgn * float(cdef.get("turn_deg", 60.0))
    a, b = B.fr(cdef.get("turn_from", 2)), B.fr(cdef.get("turn_to", cdef["frames"] - 3))
    th = np.array([ang * smoother((i - a) / float(b - a)) for i in range(B.F)])
    B.ground_yaw[:] = th
    steps = cdef.get("turn_steps", [{"foot": "L", "f0": 3, "f1": 8}, {"foot": "R", "f0": 9, "f1": 14}])
    lift = cdef.get("lift", 0.05) * sc
    w = B.ch["weight"][:, 0]
    pts = [(0.0, 0.5)]
    for stp in steps:
        s = B._side_name(stp["foot"])
        base = B.stance[s].base
        g0 = np.array([base.x, base.y])
        cR, sR = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        g1 = np.array([cR * g0[0] - sR * g0[1], sR * g0[0] + cR * g0[1]])
        f0, f1 = B.fr(stp["f0"]), B.fr(stp["f1"])
        pos, rot = B.ch["foot%s.pos" % s], B.ch["foot%s.rot" % s]
        for i in range(B.F):
            if i <= f0:
                pg, yg, up, pit, c = g0, 0.0, 0.0, 0.0, C_FLAT
            elif i < f1:
                u = (i - f0) / float(f1 - f0)
                e = smooth(u)
                pg = g0 + (g1 - g0) * e
                yg = ang * e
                up = lift * math.sin(math.pi * u ** 0.85) ** 1.4
                pit = 10.0 * math.sin(math.pi * min(1.0, u / 0.5)) if u < 0.5 else 6.0 * smooth((u - 0.5) / 0.5)
                c = C_NONE
            else:
                k = min(1.0, (i - f1) / 2.0)
                pg, yg, up, pit, c = g1, ang, 0.0, 6.0 * (1.0 - smooth(k)), (C_FLAT if k >= 1.0 else C_TOE)
            t = math.radians(-th[i])
            ct, stn = math.cos(t), math.sin(t)
            px, py = ct * pg[0] - stn * pg[1], stn * pg[0] + ct * pg[1]
            pos[i] = [px - base.x, -(py - base.y), up]
            rot[i] = [yg - th[i], pit, 0.0]
            B.contact[s][i] = c
        sup = cdef.get("turn_weight", 0.26)          # share of the weight left on the foot that is in the air
        sup = sup if s == "L" else 1.0 - sup
        shift = stp.get("shift", 3)
        pts.append((max(pts[-1][0] + 0.5, f0 - shift), pts[-1][1]))
        pts.append((float(f0), sup))
        pts.append((float(f1), sup))
        B.events.append({"name": "Foot" + s, "frame": int(f1)})
    pts.append((float(n), 0.5))
    for i in range(B.F):
        for k in range(1, len(pts)):
            if i <= pts[k][0]:
                f0_, v0 = pts[k - 1]
                f1_, v1 = pts[k]
                u = 0.0 if f1_ <= f0_ else (i - f0_) / (f1_ - f0_)
                w[i] = v0 + (v1 - v0) * smoother(u)
                break
    B.meta["turn_deg"] = ang
    B.meta["turn_frames"] = [int(a), int(b)]
    B.pelvis_mode = "local"
