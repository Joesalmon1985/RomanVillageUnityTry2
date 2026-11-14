"""Checks for baked clips (WP-5): loop continuity and foot planting, measured on the baked FK actions.

Nothing here trusts the generator: the armature is posed by Blender from the action's curves and the foot points
are read back from the posed bones. verify.py (WP-2) can call check_clips(arm, baked) and use the 'ok' flags.

Foot planting: while a foot is marked planted, the point it stands on (heel edge after the strike, the sole
centre while flat, the toe edge once the heel is up) must stay where it is on the ground. The ground of an in-place
clip is the frame that moves with the game's root: forward by the clip's travel so far, turned by its yaw so far.
  speed_cm_s   largest frame-to-frame speed of the standing point against the ground (PRD 5.7 limit: 2 cm/s)
  drift_mm     largest distance of the standing point from where it was planted, on whole frames (limit 3 mm)
  drift_halfframe_mm   the same sampled between the keys as well (what a player sees at any playback time)
  lift_mm      largest height of the standing point above the ground
"""

import math

import bpy
from mathutils import Matrix, Vector

import anim_core as ac
from anim_core import C_NONE, C_HEEL, C_FLAT, C_TOE, FPS

FOOT_SPEED_LIMIT_CM_S = 2.0
FOOT_DRIFT_LIMIT_MM = 3.0
LOOP_POS_TOL_M = 0.0005
LOOP_ROT_TOL_DEG = 0.05


def _angle(m0, m1):
    a = math.degrees(m0.to_quaternion().rotation_difference(m1.to_quaternion()).angle)
    return min(a, 360.0 - a)


def _pose_at(arm, frame):
    scn = bpy.context.scene
    f = int(math.floor(frame))
    scn.frame_set(f, subframe=frame - f)
    bpy.context.view_layer.update()


def _foot_points(rig, s):
    """Standing points in the Foot bone's rest frame."""
    ft = rig.foot[s]
    inv = rig.rest["Foot." + s].inverted()
    return {C_HEEL: inv @ (ft.ground + ft.heel), C_FLAT: inv @ (ft.ground + ft.centre), C_TOE: inv @ (ft.ground + ft.toe)}


def foot_report(arm, baked, rig=None):
    rig = rig or ac.Rig(arm)
    act = bpy.data.actions[baked.name]
    ac.assign_action(arm, act)
    n = baked.frames
    pts = {s: _foot_points(rig, s) for s in "LR"}
    out = {"clip": baked.name, "native_speed_mps": round(baked.native_speed, 3), "feet": {}}
    worst_speed = worst_drift = worst_lift = worst_sub = 0.0
    samples = [k * 0.5 for k in range(2 * n + 1)]
    world = {s: {} for s in "LR"}
    for t in samples:
        _pose_at(arm, t)
        i0 = min(n, int(math.floor(t)))
        i1 = min(n, i0 + 1)
        u = t - i0
        dist = baked.ground_dist[i0] * (1 - u) + baked.ground_dist[i1] * u
        yaw = math.radians(baked.ground_yaw[i0] * (1 - u) + baked.ground_yaw[i1] * u)
        Rz = Matrix.Rotation(yaw, 3, "Z")
        for s in "LR":
            M = arm.pose.bones["Foot." + s].matrix
            world[s][t] = {c: Rz @ (M @ p) + Vector((0.0, -dist, 0.0)) for c, p in pts[s].items()}
    for s in "LR":
        con = baked.contact[s]
        segs = []
        start = None
        for i in range(n + 1):
            c = int(con[i])
            if start is not None and (c != con[start]):
                segs.append((start, i - 1, int(con[start])))
                start = None
            if start is None and c != C_NONE:
                start = i
        if start is not None:
            segs.append((start, n, int(con[start])))
        f_speed = f_drift = f_lift = f_sub = 0.0
        planted = 0
        for a, b, c in segs:
            planted += b - a + 1
            p0 = world[s][float(a)][c]
            prev = p0
            for i in range(a, b + 1):
                p = world[s][float(i)][c]
                f_speed = max(f_speed, (Vector((p.x - prev.x, p.y - prev.y))).length * FPS * 100.0)
                prev = p
                f_drift = max(f_drift, Vector((p.x - p0.x, p.y - p0.y)).length * 1000.0)
                f_lift = max(f_lift, abs(p.z) * 1000.0)
                if i < b:
                    ph = world[s][i + 0.5][c]
                    f_sub = max(f_sub, Vector((ph.x - p0.x, ph.y - p0.y)).length * 1000.0)
        out["feet"][s] = {"planted_frames": planted, "segments": len(segs), "speed_cm_s": round(f_speed, 3),
                          "drift_mm": round(f_drift, 3), "drift_halfframe_mm": round(max(f_sub, f_drift), 3),
                          "lift_mm": round(f_lift, 3)}
        worst_speed, worst_drift, worst_lift = max(worst_speed, f_speed), max(worst_drift, f_drift), max(worst_lift, f_lift)
        worst_sub = max(worst_sub, f_sub, f_drift)
    out["speed_cm_s"] = round(worst_speed, 3)
    out["drift_mm"] = round(worst_drift, 3)
    out["drift_halfframe_mm"] = round(worst_sub, 3)
    out["lift_mm"] = round(worst_lift, 3)
    out["unreachable_planted_frames"] = int(getattr(baked, "clamped_planted", 0))
    out["ok"] = bool(worst_speed <= FOOT_SPEED_LIMIT_CM_S and worst_drift <= FOOT_DRIFT_LIMIT_MM)
    return out


def loop_report(arm, baked):
    """First and last frame of a loop must be the same pose (bone heads and orientations, armature space)."""
    act = bpy.data.actions[baked.name]
    ac.assign_action(arm, act)
    _pose_at(arm, 0.0)
    a = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
    _pose_at(arm, float(baked.frames))
    pos = rot = 0.0
    worst = ""
    for pb in arm.pose.bones:
        m0, m1 = a[pb.name], pb.matrix
        d = (m0.translation - m1.translation).length
        ang = _angle(m0, m1)
        if d > pos or ang > rot:
            worst = pb.name
        pos, rot = max(pos, d), max(rot, ang)
    # velocity across the seam: the step from the last frame to frame 1 against the step before the seam
    _pose_at(arm, 1.0)
    b1 = {pb.name: pb.matrix.translation.copy() for pb in arm.pose.bones}
    _pose_at(arm, float(baked.frames - 1))
    seam = 0.0
    for pb in arm.pose.bones:
        v_in = a[pb.name].translation - pb.matrix.translation
        v_out = b1[pb.name] - a[pb.name].translation
        seam = max(seam, (v_out - v_in).length * FPS * FPS)       # m/s^2 across the seam
    return {"clip": baked.name, "pos_m": round(pos, 6), "rot_deg": round(rot, 4), "worst_bone": worst,
            "seam_accel_mps2": round(seam, 2), "ok": bool(pos <= LOOP_POS_TOL_M and rot <= LOOP_ROT_TOL_DEG)}


def chain_report(arm, a_baked, b_baked):
    """Last frame of clip a against first frame of clip b (for clips that are meant to follow each other)."""
    ac.assign_action(arm, bpy.data.actions[a_baked.name])
    _pose_at(arm, float(a_baked.frames))
    a = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
    ac.assign_action(arm, bpy.data.actions[b_baked.name])
    _pose_at(arm, 0.0)
    pos = rot = 0.0
    wp = wr = ""
    for pb in arm.pose.bones:
        d = (a[pb.name].translation - pb.matrix.translation).length
        ang = _angle(a[pb.name], pb.matrix)
        if d > pos:
            pos, wp = d, pb.name
        if ang > rot:
            rot, wr = ang, pb.name
    return {"from": a_baked.name, "to": b_baked.name, "pos_m": round(pos, 5), "rot_deg": round(rot, 3),
            "worst_pos": wp, "worst_rot": wr}


def check_clips(arm, keep):
    """keep: {action name: Baked} from anim_core.build_clips(..., keep=...). Returns {'foot': [...], 'loop': [...],
    'chain': [...], 'ok': bool}."""
    rig = ac.Rig(arm)
    foot, loops, chains = [], [], []
    for name in sorted(keep):
        b = keep[name]
        foot.append(foot_report(arm, b, rig))
        if b.loop:
            loops.append(loop_report(arm, b))
    def nm(base, key):
        return base + "@" + key if key else base
    keys = sorted(set(b.style_key for b in keep.values()), key=lambda k: k or "")
    for key in keys:
        for a, b in (("CastAnticipate", "CastRelease"), ("CastRelease", "CastRecover")):
            if nm(a, key) in keep and nm(b, key) in keep:
                chains.append(chain_report(arm, keep[nm(a, key)], keep[nm(b, key)]))
        for a, b in (("CastRecover", "Idle_A"), ("Greet", "Idle_A"), ("StopL", "Idle_A"), ("StopR", "Idle_A"),
                     ("Turn60L", "Idle_A"), ("Turn60R", "Idle_A"), ("Block", "Idle_A"), ("RecoilLight", "Idle_A"),
                     ("RecoilHeavy", "Idle_A"), ("Surprise", "Idle_A"), ("Beckon", "Idle_A"), ("PointGround", "Idle_A")):
            if nm(a, key) in keep and nm(b, key) in keep:
                chains.append(chain_report(arm, keep[nm(a, key)], keep[nm(b, key)]))
    ok = all(f["ok"] for f in foot) and all(l["ok"] for l in loops)
    return {"foot": foot, "loop": loops, "chain": chains, "ok": ok}
