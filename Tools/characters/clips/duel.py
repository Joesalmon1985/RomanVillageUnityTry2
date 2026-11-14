"""Duel clips (WP-5): the cast in three beats, the weave loop, the hit reactions, the ward break, the endings.

Beats: CastAnticipate ends on the wound-up pose, CastRelease starts on it and ends on the thrust, CastRecover starts
on the thrust and ends on the stand pose, so the three chain without a blend. The Release event sits on the frame
where the hand (or staff, or weapon) reaches full extension, which is where the bolt should leave. The hit reactions
are started by the director when the bolt arrives, so their Impact event is on frame 0.

The base clips are for an unarmed right-handed caster. "variants" holds the two styles of PRD 7.5:
  mira      slow anticipation, crisp release, long recovery; upright and narrow; the staff does the work
  champion  quick wind-up, hard two-handed shove from the hip with a stomp, short heavy recovery; wide and low
Variant timings are fixed ("tempo": False) so the style's tempo does not stretch the duel.
"""

# ---------------------------------------------------------------- base (unarmed) ----------
WOUND = {
    "armR": {"reach": [-0.30, -0.06, 0.02], "elbow": [-0.5, -0.8, -0.2], "palm": "fwd"},
    "armL": {"reach": [0.02, 0.88, 0.0], "elbow": [0.6, -0.3, -0.7], "palm": "fwd"},
    "Hips": [0, -5, 0], "Spine": [0, -8, 0], "Chest": [-2, -22, 0], "Head": [2, 6, 0],
    "weight": 0.30, "pelvis": [0, -0.03, -0.035], "shoulderR": [3, -6],
}
THRUST = {
    "armR": {"reach": [-0.05, 0.97, 0.02], "elbow": [-0.6, -0.2, -0.7], "palm": "fwd"},
    "armL": {"reach": [0.30, 0.05, -0.40], "elbow": [0.5, -0.8, -0.2]},
    "Hips": [2, 4, 0], "Spine": [3, 6, 0], "Chest": [8, 16, 0], "Head": [-4, -4, 0],
    "weight": 0.72, "pelvis": [0, 0.07, -0.05], "shoulderR": [0, 9],
}
THRUST_OVER = {"base": "cast_thrust", "pelvis": [0, 0.09, -0.06], "Chest": [10, 19, 0], "shoulderR": [0, 12]}
GUARD = {
    "armR": {"reach": [0.14, 0.55, 0.05], "elbow": [-0.7, -0.1, -0.7], "palm": "fwd"},
    "armL": {"reach": [-0.12, 0.50, -0.10], "elbow": [0.7, -0.1, -0.7], "palm": "fwd"},
    "Chest": [6, 0, 0], "Spine": [2, 0, 0], "Head": [8, 0, 0], "pelvis": [0, 0.0, -0.05],
    "shoulderL": [6, 4], "shoulderR": [6, 4],
}
FLING = {"armL+": {"UpperArm": [-28, 0, -14], "LowerArm": [-18, 0, 0]},
         "armR+": {"UpperArm": [-24, 0, 16], "LowerArm": [-22, 0, 0]}}
KNEEL = {   # on the right knee: right foot back on its toes, left foot forward
    "pelvis": [0, 0.0, -0.41], "weight": 0.55, "footR": {"pitch": 66},
    "Chest": [16, 0, 0], "Spine": [6, 0, 0], "Head": [20, 0, 0], "Neck": [6, 0, 0],
    "armR": {"at": [-0.30, 0.10, 0.09], "elbow": [-0.7, -0.6, 0.2], "palm": "down"},
    "armL": {"at": [0.15, 0.24, 0.50], "elbow": [0.8, -0.3, 0.2], "palm": "down"},
}
KNEES = {   # on both knees, hips over the knees, slumped; the feet stay where they were, on their toes
    "pelvis": [0, 0.45, -0.46], "footL": {"pitch": 68}, "footR": {"pitch": 66},
    "Chest": [10, 0, 0], "Spine": [8, 0, 0], "Head": [24, 0, 2], "Neck": [8, 0, 0], "shoulderL": [-4, 4], "shoulderR": [-4, 4],
    "armL": {"at": [0.20, 0.60, 0.30], "elbow": [0.8, -0.5, 0.0], "palm": "down"},
    "armR": {"at": [-0.21, 0.58, 0.29], "elbow": [-0.8, -0.5, 0.0], "palm": "down"},
}
FIST_UP = {"reach": [-0.25, 0.15, 0.95], "elbow": [-0.8, -0.2, -0.5], "palm": "left"}
FIST_MID = {"reach": [-0.34, 0.22, 0.72], "elbow": [-0.8, -0.2, -0.5], "palm": "left"}
WEAVE_R = [{"reach": [-0.10, 0.62, -0.05], "palm": "fwd"}, {"reach": [0.02, 0.66, -0.20], "palm": "left"},
           {"reach": [-0.12, 0.60, -0.36], "palm": "up"}, {"reach": [-0.30, 0.62, -0.20], "palm": "fwd"}]
WEAVE_L = [{"reach": [0.24, 0.58, -0.34], "palm": "up"}, {"reach": [0.34, 0.60, -0.18], "palm": "fwd"},
           {"reach": [0.14, 0.62, -0.02], "palm": "fwd"}, {"reach": [0.02, 0.60, -0.22], "palm": "right"}]

# ---------------------------------------------------------------- Mira (staff in the left hand) ----------
M_WOUND = {
    "pinL": "off", "propL": {"aim": [0.0, -0.26, 0.97]},
    "armL": {"reach": [0.30, 0.22, 0.12], "elbow": [0.75, -0.55, -0.35]},
    "armR": {"reach": [-0.04, 0.90, 0.04], "elbow": [-0.6, -0.3, -0.7], "palm": "fwd"},
    "Hips": [0, 4, 0], "Spine": [-1, 6, 0], "Chest": [-3, 16, 0], "Head": [0, -5, 0],
    "weight": 0.62, "pelvis": [0, -0.03, -0.012], "shoulderL": [4, -5],
}
M_THRUST = {
    "pinL": "off", "propL": {"aim": [0.0, 0.80, 0.60]},
    "armL": {"reach": [0.36, 0.86, 0.02], "elbow": [0.6, -0.3, -0.7]},
    "armR": {"reach": [-0.26, 0.14, -0.36], "elbow": [-0.5, -0.8, -0.2], "palm": "left"},
    "Hips": [1, -3, 0], "Spine": [2, -5, 0], "Chest": [5, -14, 0], "Head": [-3, 2, 0],
    "weight": 0.42, "pelvis": [0, 0.05, -0.02], "shoulderL": [0, 8],
}
M_PLANT_HI = {"pinL": {"pos": [-0.05, 0.10, 0.06]}, "propL": "stand"}
M_KNEEL = {
    "pelvis": [0, 0.0, -0.40], "weight": 0.55, "footR": {"pitch": 66},
    "pinL": {"pos": [0.0, 0.24, 0.0]}, "propL": {"aim": [0.06, 0.10, 1.0]},
    "Chest": [14, 6, 0], "Spine": [6, 2, 0], "Head": [22, 0, 0], "Neck": [6, 0, 0],
    "armR": {"at": [-0.26, 0.16, 0.10], "elbow": [-0.7, -0.6, 0.2], "palm": "down"},
}

# ---------------------------------------------------------------- champion (weapon in the right hand) ----------
C_WOUND = {
    "pelvis": [0, -0.05, -0.07], "weight": 0.24,
    "Hips": [0, -8, 0], "Spine": [2, -10, 0], "Chest": [4, -22, 0], "Head": [4, 14, 0],
    "armR": {"reach": [-0.50, -0.30, -0.60], "elbow": [-0.6, -0.7, 0.2]}, "propR": {"aim": [-0.1, -0.75, -0.65]},
    "armL": {"reach": [-0.28, 0.46, -0.46], "elbow": [0.7, -0.2, -0.6], "palm": "right"},
    "shoulderR": [2, -8], "shoulderL": [2, 8],
}
C_THRUST = {
    "pelvis": [0, 0.12, -0.10], "weight": 0.80, "footL": {"pos": [0.0, 0.24, 0.0]},
    "Hips": [4, 6, 0], "Spine": [5, 8, 0], "Chest": [10, 18, 0], "Neck": [5, 0, 0], "Head": [-10, -8, 0],
    "armR": {"reach": [-0.10, 0.96, -0.12], "elbow": [-0.7, -0.2, -0.6]}, "propR": {"aim": [0.0, 0.95, 0.3]},
    "armL": {"reach": [0.22, 0.93, -0.20], "elbow": [0.7, -0.2, -0.6], "palm": "fwd"},
    "shoulderR": [0, 12], "shoulderL": [0, 10],
}
C_GUARD = {
    "pelvis": [0, 0.02, -0.06], "Chest": [8, 10, 0], "Spine": [3, 3, 0], "Head": [6, -4, 0],
    "armR": {"reach": [0.22, 0.56, -0.06], "elbow": [-0.8, -0.2, -0.5]}, "propR": {"aim": [0.75, 0.1, 0.65]},
    "armL": {"reach": [0.15, 0.46, -0.46], "elbow": [0.8, -0.3, -0.4], "palm": "right"},
    "shoulderL": [6, 4], "shoulderR": [6, 4],
}
C_KNEES = {
    "pelvis": [0, 0.54, -0.42], "footL": {"pitch": 68}, "footR": {"pitch": 66},
    "Chest": [16, 0, 0], "Spine": [8, 0, 0], "Head": [20, 0, 0], "Neck": [6, 0, 0],
    "armL": {"at": [0.22, 0.20, 0.10], "elbow": [0.8, -0.5, 0.2], "palm": "down"},
    "armR": {"at": [-0.46, 0.10, 0.14], "elbow": [-0.8, -0.5, 0.2]}, "propR": {"aim": [-0.5, 0.85, 0.0]},
}

POSES = {
    "cast_wound": WOUND, "cast_thrust": THRUST, "cast_thrust_over": THRUST_OVER, "guard": GUARD, "fling": FLING,
    "kneel": KNEEL, "knees": KNEES,
    "m_wound": M_WOUND, "m_thrust": M_THRUST, "m_plant_hi": M_PLANT_HI, "m_kneel": M_KNEEL,
    "c_wound": C_WOUND, "c_thrust": C_THRUST, "c_guard": C_GUARD, "c_knees": C_KNEES,
}

CLIPS = {
    # ------------------------------------------------------------------ cast
    "CastAnticipate": {
        "frames": 9,
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 2, "set": {"pelvis": [0, 0.01, -0.01], "Chest": [2, 3, 0]}, "only": ["torso"]},
                 {"f": 9, "pose": "cast_wound", "ease": "decel"}],
        "variants": {
            "mira": {"replace": True, "frames": 12, "tempo": False, "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"pelvis": [0, 0.008, -0.012], "Chest": [2, -3, 0], "pinL": {"pos": [-0.05, 0.10, 0.0]}},
                 "only": ["torso", "pinL", "pinL.pos"]},
                {"f": 10, "pose": "m_wound", "set": {"pelvis": [0, -0.026, -0.010], "Chest": [-2.5, 14.5, 0]}, "ease": "smooth"},
                {"f": 12, "pose": "m_wound", "ease": "soft"}],
                "offsets": {"armR": -1}},
            "champion": {"replace": True, "frames": 7, "tempo": False, "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 7, "pose": "c_wound", "ease": "decel"}],
                "offsets": {}},
        },
    },
    "CastRelease": {
        "frames": 8,
        "events": [{"name": "Release", "f": 2}],
        "keys": [{"f": 0, "pose": "cast_wound"},
                 {"f": 2, "pose": "cast_thrust_over", "ease": "accel"},
                 {"f": 5, "pose": "cast_thrust", "ease": "soft"},
                 {"f": 8, "pose": "cast_thrust"}],
        "layers": {"overlap": 0.5},
        "variants": {
            "mira": {"replace": True, "frames": 6, "tempo": False, "events": [{"name": "Release", "f": 2}], "keys": [
                {"f": 0, "pose": "m_wound"},
                {"f": 2, "pose": "m_thrust", "set": {"pelvis": [0, 0.06, -0.024]}, "ease": "accel"},
                {"f": 4, "pose": "m_thrust", "ease": "soft"},
                {"f": 6, "pose": "m_thrust"}],
                "layers": {"overlap": 0.4}},
            "champion": {"replace": True, "frames": 8, "tempo": False, "events": [{"name": "Release", "f": 3}], "keys": [
                {"f": 0, "pose": "c_wound"},
                {"f": 3, "pose": "c_thrust", "set": {"pelvis": [0, 0.15, -0.12], "Chest": [13, 21, 0]}, "ease": "accel"},
                {"f": 6, "pose": "c_thrust", "ease": "soft"},
                {"f": 8, "pose": "c_thrust"}],
                "steps": [{"foot": "L", "f0": 0, "f1": 3, "to": [0.0, 0.24], "lift": 0.06, "shift": 2, "w_support": 0.24,
                           "settle": 2, "land": 0.0}],
                "layers": {"overlap": 0.8}},
        },
    },
    "CastRecover": {
        "frames": 15,
        "keys": [{"f": 0, "pose": "cast_thrust"},
                 {"f": 7, "set": {"armR+": {"UpperArm": [-8, 0, 0], "LowerArm": [-14, 0, 0]}, "armL": "hang",
                                  "Chest": [1, -3, 0], "Spine": [0, -1, 0], "Hips": [0, 0, 0], "pelvis": [0, 0.0, -0.022],
                                  "weight": 0.48, "Head": [1, 0, 0], "shoulderR": [0, 0]}, "ease": "soft"},
                 {"f": 15, "pose": "stand"}],
        "variants": {
            "mira": {"replace": True, "frames": 20, "tempo": False, "keys": [
                {"f": 0, "pose": "m_thrust"},
                {"f": 7, "set": {"pinL": "off", "propL": {"aim": [0.0, 0.10, 1.0]},
                                 "armL": {"reach": [0.30, 0.62, -0.20], "elbow": [0.7, -0.4, -0.6]},
                                 "armR": {"reach": [-0.30, 0.30, -0.62], "elbow": [-0.6, -0.6, -0.4]},
                                 "Hips": [0, 0, 0], "Spine": [0, -1, 0], "Chest": [1, -3, 0], "Head": [0, 0, 0],
                                 "weight": 0.5, "pelvis": [0, 0.01, -0.006], "shoulderL": [0, 2]}, "ease": "soft"},
                {"f": 14, "pose": "m_plant_hi", "set": {"armR": "hang", "Chest": [0, 0, 0], "Spine": [0, 0, 0],
                                                        "pelvis": [0, 0, 0.004], "shoulderL": [2, 0]}, "ease": "soft"},
                {"f": 17, "set": {"pinL": "stand", "pelvis": [0, 0, -0.01], "Head": [3, 0, 0], "shoulderL": [0, 0]}, "ease": "accel"},
                {"f": 20, "pose": "stand"}]},
            "champion": {"replace": True, "frames": 11, "tempo": False, "keys": [
                {"f": 0, "pose": "c_thrust"},
                {"f": 5, "set": {"pelvis": [0, 0.02, -0.06], "weight": 0.5, "Hips": [1, 0, 0], "Spine": [2, 2, 0],
                                 "Chest": [4, 4, 0], "Neck": [0, 0, 0], "Head": [-2, 0, 0],
                                 "armR": {"reach": [-0.36, 0.36, -0.70], "elbow": [-0.7, -0.5, -0.4]}, "propR": "stand",
                                 "armL": {"reach": [0.32, 0.30, -0.66], "elbow": [0.7, -0.5, -0.4]},
                                 "shoulderL": [0, 0], "shoulderR": [0, 0]}, "ease": "soft"},
                {"f": 8, "set": {"pelvis": [0, 0, -0.04], "Chest": [3, 0, 0], "Spine": [1, 0, 0], "armR": "hang", "armL": "hang"},
                 "ease": "soft"},
                {"f": 11, "pose": "stand"}],
                "steps": [{"foot": "L", "f0": 2, "f1": 7, "to": [0.0, 0.0], "lift": 0.05, "shift": 2, "w_support": 0.2,
                           "settle": 3}]},
        },
    },
    # 2 s loop: the hands compose the weave in front of the chest (the right hand round one way, the left the other).
    # The loop never drops to the stand pose: it is entered and left by a cross-fade.
    "CastWeave_Loop": {
        "frames": 60, "loop": True,
        "keys": [
            {"f": 0, "set": {"armR": WEAVE_R[0], "armL": WEAVE_L[0], "Chest": [2, 3, 0], "Head": [4, 0, 0], "pelvis": [0, 0.01, -0.01]}},
            {"f": 15, "set": {"armR": WEAVE_R[1], "armL": WEAVE_L[1], "Chest": [2, -2, 0], "Head": [5, 2, 0], "pelvis": [0, 0.01, -0.016]},
             "ease": "soft"},
            {"f": 30, "set": {"armR": WEAVE_R[2], "armL": WEAVE_L[2], "Chest": [3, -4, 0], "Head": [6, 0, 0], "pelvis": [0, 0.01, -0.01]},
             "ease": "soft"},
            {"f": 45, "set": {"armR": WEAVE_R[3], "armL": WEAVE_L[3], "Chest": [2, 1, 0], "Head": [4, -2, 0], "pelvis": [0, 0.01, -0.016]},
             "ease": "soft"},
            {"f": 60, "set": {"armR": WEAVE_R[0], "armL": WEAVE_L[0], "Chest": [2, 3, 0], "Head": [4, 0, 0], "pelvis": [0, 0.01, -0.01]},
             "ease": "soft"},
        ],
        "offsets": {"armL": 3, "head": 2},
        "layers": {"noise": 0.5},
        "variants": {
            # Mira: the staff stays planted; only the right hand draws, wide and slow in front of her, and the head follows it.
            "mira": {"replace": True, "frames": 60, "loop": True, "tempo": False, "keys": [
                {"f": 0, "set": {"armR": {"reach": [0.10, 0.74, 0.10], "palm": "fwd"}, "Chest": [1, 5, 0], "Head": [-3, 3, 0]}},
                {"f": 15, "set": {"armR": {"reach": [0.36, 0.66, -0.12], "palm": "left"}, "Chest": [1, 9, 0], "Head": [1, 7, 0]},
                 "ease": "soft"},
                {"f": 30, "set": {"armR": {"reach": [0.12, 0.70, -0.40], "palm": "up"}, "Chest": [3, 5, 0], "Head": [5, 3, 0]},
                 "ease": "soft"},
                {"f": 45, "set": {"armR": {"reach": [-0.16, 0.70, -0.14], "palm": "fwd"}, "Chest": [1, 1, 0], "Head": [1, -2, 0]},
                 "ease": "soft"},
                {"f": 60, "set": {"armR": {"reach": [0.10, 0.74, 0.10], "palm": "fwd"}, "Chest": [1, 5, 0], "Head": [-3, 3, 0]},
                 "ease": "soft"}],
                "offsets": {"head": 2},
                "layers": {"noise": 0.4}},
            # Champion: low, rocking on the wide base; the weapon hand grinds a slow circle at the hip, the off hand guards.
            "champion": {"replace": True, "frames": 60, "loop": True, "tempo": False, "keys": [
                {"f": 0, "set": {"pelvis": [0, -0.02, -0.05], "weight": 0.36, "Chest": [5, -8, 0], "Head": [0, 6, 0],
                                 "armR": {"reach": [-0.50, 0.10, -0.66], "elbow": [-0.7, -0.6, 0.0]}, "propR": {"aim": [-0.2, -0.3, -0.9]},
                                 "armL": {"reach": [0.10, 0.70, -0.20], "elbow": [0.7, -0.3, -0.6], "palm": "fwd"}}},
                {"f": 20, "set": {"pelvis": [0, 0.03, -0.06], "weight": 0.60, "Chest": [7, 2, 0], "Head": [-2, -1, 0],
                                  "armR": {"reach": [-0.42, 0.34, -0.60], "elbow": [-0.7, -0.6, 0.0]}, "propR": {"aim": [0.1, 0.35, -0.9]},
                                  "armL": {"reach": [0.20, 0.62, -0.30], "elbow": [0.7, -0.3, -0.6], "palm": "fwd"}}, "ease": "soft"},
                {"f": 40, "set": {"pelvis": [0, -0.01, -0.045], "weight": 0.42, "Chest": [5, -6, 0], "Head": [0, 4, 0],
                                  "armR": {"reach": [-0.54, 0.16, -0.58], "elbow": [-0.7, -0.6, 0.0]}, "propR": {"aim": [-0.25, -0.1, -0.95]},
                                  "armL": {"reach": [0.12, 0.72, -0.16], "elbow": [0.7, -0.3, -0.6], "palm": "fwd"}}, "ease": "soft"},
                {"f": 60, "set": {"pelvis": [0, -0.02, -0.05], "weight": 0.36, "Chest": [5, -8, 0], "Head": [0, 6, 0],
                                  "armR": {"reach": [-0.50, 0.10, -0.66], "elbow": [-0.7, -0.6, 0.0]}, "propR": {"aim": [-0.2, -0.3, -0.9]},
                                  "armL": {"reach": [0.10, 0.70, -0.20], "elbow": [0.7, -0.3, -0.6], "palm": "fwd"}}, "ease": "soft"}],
                "layers": {"noise": 0.5}},
        },
    },
    # ------------------------------------------------------------------ reactions (Impact on frame 0)
    "Block": {
        "frames": 15, "events": [{"name": "Impact", "f": 0}],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 2, "pose": "guard", "ease": "snap"},
                 {"f": 4, "pose": "guard", "set": {"pelvis": [0, -0.035, -0.045], "Chest": [2, 0, 0], "Head": [10, 0, 0]}},
                 {"f": 8, "pose": "guard", "set": {"pelvis": [0, 0.012, -0.05]}, "ease": "soft"},
                 {"f": 15, "pose": "stand"}],
        "variants": {
            # Mira: the staff comes forward as a barrier, the free palm opens beside it; she gives a little and re-plants.
            "mira": {"replace": True, "frames": 16, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 2, "set": {"pinL": "off", "propL": {"aim": [0.0, 0.12, 1.0]},
                                 "armL": {"reach": [-0.26, 0.80, -0.44], "elbow": [0.7, -0.4, -0.6]},
                                 "armR": {"reach": [0.06, 0.66, -0.10], "elbow": [-0.7, -0.3, -0.6], "palm": "fwd"},
                                 "Chest": [5, 0, 0], "Head": [5, 0, 0], "pelvis": [0, 0.0, -0.03]}, "ease": "snap"},
                {"f": 4, "set": {"pelvis": [0, -0.03, -0.028], "Chest": [1, 0, 0], "Head": [7, 0, 0]}, "only": ["torso", "head"]},
                {"f": 8, "set": {"pinL": "off", "propL": {"aim": [0.0, 0.12, 1.0]},
                                 "armL": {"reach": [-0.26, 0.80, -0.44], "elbow": [0.7, -0.4, -0.6]},
                                 "armR": {"reach": [0.06, 0.66, -0.10], "elbow": [-0.7, -0.3, -0.6], "palm": "fwd"},
                                 "pelvis": [0, 0.008, -0.03], "Chest": [4, 0, 0], "Head": [3, 0, 0]}, "ease": "soft"},
                {"f": 13, "pose": "m_plant_hi", "set": {"armR": "hang", "Chest": [0, 0, 0], "pelvis": [0, 0, 0]}, "ease": "soft"},
                {"f": 16, "pose": "stand"}]},
            "champion": {"replace": True, "frames": 15, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 2, "pose": "c_guard", "ease": "snap"},
                {"f": 4, "pose": "c_guard", "set": {"pelvis": [0, -0.03, -0.05], "Chest": [3, 8, 0]}},
                {"f": 9, "pose": "c_guard", "set": {"pelvis": [0, 0.04, -0.055], "Chest": [9, 11, 0]}, "ease": "soft"},
                {"f": 15, "pose": "stand"}]},
        },
    },
    "RecoilLight": {
        "frames": 18, "events": [{"name": "Impact", "f": 0}],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 2, "pose": "fling", "set": {"Chest": [-14, 0, 0], "Spine": [-6, 0, 0], "Head": [8, 0, 0], "Neck": [3, 0, 0],
                                                   "head_level": 0.15, "pelvis": [0, -0.05, -0.012], "shoulderL": [6, 0], "shoulderR": [6, 0]},
                  "ease": "snap"},
                 {"f": 5, "set": {"Head": [-8, 0, 0], "Neck": [-3, 0, 0]}, "only": ["head"], "ease": "soft"},
                 {"f": 9, "set": {"Chest": [4, 0, 0], "Spine": [1.5, 0, 0], "Head": [2, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0.012, -0.025],
                                  "armL": "hang", "armR": "hang", "shoulderL": [0, 0], "shoulderR": [0, 0]}, "ease": "soft"},
                 {"f": 18, "pose": "stand"}],
        "variants": {
            # Mira: gives at the chest, a half step back, the planted staff holds her; step back in.
            "mira": {"replace": True, "frames": 18, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 2, "set": {"Chest": [-15, 5, 0], "Spine": [-6, 2, 0], "Head": [8, 0, 0], "Neck": [3, 0, 0], "head_level": 0.15,
                                 "pelvis": [0, -0.03, -0.01],
                                 "armR+": {"UpperArm": [-22, 0, 10], "LowerArm": [-16, 0, 0]}, "shoulderR": [5, 0]}, "ease": "snap"},
                {"f": 5, "set": {"Head": [-6, 0, 0], "Neck": [-2, 0, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 9, "set": {"Chest": [3, 0, 0], "Spine": [1, 0, 0], "Head": [2, 0, 0], "Neck": [0, 0, 0], "armR": "hang",
                                 "shoulderR": [0, 0], "pelvis": [0, 0, -0.012]}, "ease": "soft"},
                {"f": 18, "pose": "stand"}],
                "steps": [{"foot": "R", "f0": 1, "f1": 6, "to": [0.0, -0.17], "lift": 0.03, "shift": 2, "w_support": 0.25, "settle": 3},
                          {"foot": "R", "f0": 10, "f1": 15, "to": [0.0, 0.0], "lift": 0.03, "shift": 3, "w_support": 0.2, "settle": 3}]},
            # Champion: rocks back on his heels, plants, shakes it off.
            "champion": {"replace": True, "frames": 19, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"Chest": [-15, 0, 0], "Spine": [-7, 0, 0], "Head": [9, 0, 0], "head_level": 0.15, "pelvis": [0, -0.09, 0.0],
                                 "footL": {"pitch": -15}, "armL+": {"UpperArm": [-16, 0, -8], "LowerArm": [-10, 0, 0]},
                                 "shoulderL": [6, 0], "shoulderR": [6, 0]}, "ease": "decel"},
                {"f": 8, "set": {"Chest": [6, 0, 0], "Spine": [2, 0, 0], "Head": [-6, 0, 0], "pelvis": [0, 0.025, -0.055],
                                 "footL": {"pitch": 0}, "armL": "hang", "shoulderL": [2, 0], "shoulderR": [2, 0]}, "ease": "accel"},
                {"f": 11, "set": {"Head": [-4, -10, 0], "Neck": [0, -3, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 14, "set": {"Head": [-2, 8, 0], "Neck": [0, 2, 0], "pelvis": [0, 0.01, -0.03]}, "only": ["head", "pelvis"], "ease": "soft"},
                {"f": 19, "pose": "stand"}]},
        },
    },
    "RecoilHeavy": {
        "frames": 27, "events": [{"name": "Impact", "f": 0}],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 3, "pose": "fling", "set": {"Chest": [-24, 0, 0], "Spine": [-10, 0, 0], "Head": [10, 0, 0], "Neck": [4, 0, 0],
                                                   "head_level": 0.15, "pelvis": [0, -0.04, -0.02], "shoulderL": [8, 0], "shoulderR": [8, 0]},
                  "ease": "snap"},
                 {"f": 6, "set": {"Head": [-12, 0, 0], "Neck": [-4, 0, 0]}, "only": ["head"], "ease": "soft"},
                 {"f": 11, "set": {"Chest": [10, 0, 0], "Spine": [4, 0, 0], "Head": [4, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0.0, -0.07],
                                   "armL+": {"UpperArm": [-6, 0, -10], "LowerArm": [-20, 0, 0]},
                                   "armR+": {"UpperArm": [-6, 0, 10], "LowerArm": [-20, 0, 0]},
                                   "shoulderL": [0, 0], "shoulderR": [0, 0]}, "ease": "soft"},
                 {"f": 20, "set": {"Chest": [3, 0, 0], "Spine": [1, 0, 0], "Head": [-2, 0, 0], "pelvis": [0, 0.0, -0.03],
                                   "armL": "hang", "armR": "hang"}, "ease": "soft"},
                 {"f": 27, "pose": "stand"}],
        "steps": [{"foot": "R", "f0": 1, "f1": 7, "to": [0.0, -0.30], "lift": 0.04, "shift": 2, "w_support": 0.3, "settle": 4},
                  {"foot": "R", "f0": 14, "f1": 21, "to": [0.0, 0.0], "lift": 0.045, "shift": 4, "w_support": 0.15, "settle": 5}],
        "variants": {
            # Mira: a full step back, the staff comes up and is planted again behind the first spot, she leans on it,
            # then steps in and plants it where it was.
            "mira": {"replace": True, "frames": 28, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"Chest": [-25, 6, 0], "Spine": [-10, 2, 0], "Head": [10, 0, 0], "Neck": [4, 0, 0], "head_level": 0.15,
                                 "pinL": {"pos": [-0.03, 0.04, 0.10]}, "propL": {"aim": [0.05, -0.16, 0.98]},
                                 "armR+": {"UpperArm": [-24, 0, 14], "LowerArm": [-20, 0, 0]}, "shoulderR": [6, 0]}, "ease": "snap"},
                {"f": 6, "set": {"Head": [-9, 0, 0], "Neck": [-3, 0, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 9, "set": {"pinL": {"pos": [-0.02, -0.07, 0.0]}, "propL": {"aim": [0.0, 0.06, 1.0]}, "Chest": [7, 0, 0],
                                 "Spine": [3, 0, 0], "Head": [4, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0, -0.05],
                                 "armR+": {"UpperArm": [-4, 0, 4], "LowerArm": [-16, 0, 0]}, "shoulderR": [0, 0]}, "ease": "soft"},
                {"f": 15, "set": {"pinL": {"pos": [-0.02, -0.07, 0.0]}, "Chest": [5, 0, 0], "pelvis": [0, 0, -0.04], "Head": [0, 0, 0]}},
                {"f": 20, "set": {"pinL": {"pos": [-0.04, 0.03, 0.07]}, "propL": "stand", "Chest": [2, 0, 0], "Spine": [1, 0, 0],
                                  "pelvis": [0, 0, -0.012], "armR": "hang"}, "ease": "soft"},
                {"f": 24, "set": {"pinL": "stand", "pelvis": [0, 0, -0.012], "Head": [2, 0, 0]}, "ease": "accel"},
                {"f": 28, "pose": "stand"}],
                "steps": [{"foot": "R", "f0": 1, "f1": 7, "to": [0.0, -0.30], "lift": 0.035, "shift": 2, "w_support": 0.3, "settle": 4},
                          {"foot": "R", "f0": 15, "f1": 22, "to": [0.0, 0.0], "lift": 0.035, "shift": 4, "w_support": 0.15, "settle": 5}]},
            # Champion: a step back off the rear foot, low, then a stamp back into his base and a shake.
            "champion": {"replace": True, "frames": 28, "tempo": False, "events": [{"name": "Impact", "f": 0}], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"Chest": [-24, 0, 0], "Spine": [-10, 0, 0], "Head": [10, 0, 0], "head_level": 0.15, "pelvis": [0, -0.05, 0.0],
                                 "armL+": {"UpperArm": [-26, 0, -12], "LowerArm": [-12, 0, 0]}, "shoulderL": [8, 0], "shoulderR": [8, 0]},
                 "ease": "snap"},
                {"f": 6, "set": {"Head": [-12, 0, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 10, "set": {"Chest": [9, 0, 0], "Spine": [4, 0, 0], "Head": [-4, 0, 0], "pelvis": [0, 0.0, -0.08],
                                  "armL+": {"UpperArm": [-4, 0, -8], "LowerArm": [-18, 0, 0]}, "shoulderL": [3, 0], "shoulderR": [3, 0]},
                 "ease": "soft"},
                {"f": 19, "set": {"Chest": [5, 0, 0], "Spine": [2, 0, 0], "Head": [-5, 0, 0], "pelvis": [0, 0.02, -0.06], "armL": "hang",
                                  "shoulderL": [0, 0], "shoulderR": [0, 0]}, "ease": "soft"},
                {"f": 22, "set": {"Head": [-4, -10, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 25, "set": {"Head": [-2, 7, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 28, "pose": "stand"}],
                "steps": [{"foot": "R", "f0": 1, "f1": 7, "to": [0.0, -0.26], "lift": 0.04, "shift": 2, "w_support": 0.3, "settle": 4},
                          {"foot": "R", "f0": 12, "f1": 18, "to": [0.0, 0.0], "lift": 0.08, "shift": 3, "w_support": 0.1, "settle": 4,
                           "land": 0.0}]},
        },
    },
    # 1.4 s: thrown back, a long step to catch it, the legs give, down on one knee, hold (frames 30 to 42).
    "WardBreak": {
        "frames": 42, "events": [{"name": "Impact", "f": 0}], "hold": [30, 42],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 3, "pose": "fling", "set": {"Chest": [-22, 0, 0], "Spine": [-9, 0, 0], "Head": [12, 0, 0], "Neck": [5, 0, 0],
                                                   "pelvis": [0, -0.03, -0.01], "shoulderL": [9, 0], "shoulderR": [9, 0]}, "ease": "snap"},
                 {"f": 6, "set": {"Head": [-16, 0, 0], "Neck": [-6, 0, 0],
                                  "armL": {"reach": [0.70, 0.35, 0.25], "palm": "fwd"}, "armR": {"reach": [-0.72, 0.30, 0.15], "palm": "fwd"}},
                  "only": ["head", "armL", "armR"], "ease": "soft"},
                 {"f": 12, "set": {"Chest": [6, 0, 0], "Spine": [3, 0, 0], "Head": [6, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0.0, -0.12],
                                   "weight": 0.5, "footR": {"pitch": 20}, "shoulderL": [0, 0], "shoulderR": [0, 0],
                                   "armL+": {"UpperArm": [-10, 0, -14], "LowerArm": [-24, 0, 0]},
                                   "armR+": {"UpperArm": [-10, 0, 14], "LowerArm": [-24, 0, 0]}}, "ease": "soft"},
                 {"f": 21, "pose": "kneel", "set": {"pelvis": [0, 0.0, -0.44], "Chest": [22, 0, 0]}, "ease": "accel"},
                 {"f": 26, "pose": "kneel", "ease": "soft"},
                 {"f": 34, "pose": "kneel", "set": {"Head": [6, 0, 0], "Neck": [1, 0, 0], "Chest": [13, 0, 0]}, "ease": "smooth"},
                 {"f": 42, "pose": "kneel", "set": {"Head": [5, 0, 0], "Neck": [1, 0, 0], "Chest": [13, 0, 0]}}],
        "steps": [{"foot": "R", "f0": 1, "f1": 8, "to": [0.0, -0.56], "lift": 0.05, "shift": 2, "w_support": 0.3, "settle": 5, "land": 12.0}],
        "layers": {"breath": 0, "noise": 0.4},
        "variants": {
            # Mira: the staff slips from its place as she is thrown back; she goes down on one knee and catches herself on it.
            "mira": {"replace": True, "frames": 42, "tempo": False, "events": [{"name": "Impact", "f": 0}], "hold": [30, 42], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"Chest": [-20, 6, 0], "Spine": [-8, 2, 0], "Head": [11, 0, 0], "Neck": [4, 0, 0],
                                 "pelvis": [0, -0.03, -0.01], "pinL": "off", "propL": {"aim": [0.30, -0.30, 0.90]},
                                 "armL": {"reach": [0.66, 0.34, -0.30], "elbow": [0.6, -0.6, -0.4]},
                                 "armR+": {"UpperArm": [-30, 0, 20], "LowerArm": [-24, 0, 0]}, "shoulderR": [8, 0], "shoulderL": [6, 0]},
                 "ease": "snap"},
                {"f": 6, "set": {"Head": [-14, 0, 0], "Neck": [-5, 0, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 9, "set": {"propL": {"aim": [0.42, -0.10, 0.90]}, "armL": {"reach": [0.74, 0.40, -0.44], "elbow": [0.6, -0.6, -0.4]}},
                 "only": ["armL"], "ease": "soft"},
                {"f": 13, "set": {"Chest": [6, 2, 0], "Spine": [3, 0, 0], "Head": [6, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0, -0.12],
                                  "footR": {"pitch": 20}, "armR+": {"UpperArm": [-8, 0, 10], "LowerArm": [-22, 0, 0]},
                                  "shoulderR": [0, 0], "shoulderL": [0, 0]}, "skip": ["armL"], "ease": "soft"},
                {"f": 21, "pose": "m_kneel", "set": {"pelvis": [0, 0, -0.43], "Chest": [19, 6, 0]}, "ease": "accel"},
                {"f": 26, "pose": "m_kneel", "ease": "soft"},
                {"f": 35, "pose": "m_kneel", "set": {"Head": [4, -3, 0], "Neck": [0, 0, 0], "Chest": [11, 5, 0]}, "ease": "smooth"},
                {"f": 42, "pose": "m_kneel", "set": {"Head": [3, -3, 0], "Neck": [0, 0, 0], "Chest": [11, 5, 0]}}],
                "steps": [{"foot": "R", "f0": 1, "f1": 8, "to": [0.0, -0.54], "lift": 0.04, "shift": 2, "w_support": 0.3,
                           "settle": 5, "land": 12.0}],
                "layers": {"breath": 0, "noise": 0.3}},
            # Champion: staggers two steps, falls forward onto both knees, the weapon hand on the ground; looks up, stunned.
            "champion": {"replace": True, "frames": 44, "tempo": False, "events": [{"name": "Impact", "f": 0}], "hold": [34, 44], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 3, "set": {"Chest": [-22, 0, 0], "Spine": [-9, 0, 0], "Head": [12, 0, 0], "pelvis": [0, -0.03, 0.0],
                                 "armL": {"reach": [0.74, 0.30, 0.20], "palm": "fwd"}, "propR": {"aim": [-0.5, 0.3, -0.8]},
                                 "armR+": {"UpperArm": [-22, 0, 22], "LowerArm": [-10, 0, 0]}, "shoulderL": [9, 0], "shoulderR": [9, 0]},
                 "ease": "snap"},
                {"f": 6, "set": {"Head": [-16, 0, 0]}, "only": ["head"], "ease": "soft"},
                {"f": 11, "set": {"Chest": [-8, 0, 0], "Spine": [-3, 0, 0], "Head": [-6, 0, 0], "pelvis": [0, 0.0, -0.04],
                                  "armL": {"reach": [0.66, 0.42, 0.0], "palm": "fwd"}, "shoulderL": [5, 0], "shoulderR": [5, 0]},
                 "ease": "soft"},
                {"f": 17, "set": {"Chest": [8, 0, 0], "Spine": [4, 0, 0], "Head": [6, 0, 0], "pelvis": [0, 0.22, -0.14],
                                  "footL": {"pitch": 24}, "footR": {"pitch": 22}, "shoulderL": [0, 0], "shoulderR": [0, 0],
                                  "armL+": {"UpperArm": [-14, 0, -12], "LowerArm": [-26, 0, 0]}}, "ease": "soft"},
                {"f": 25, "pose": "c_knees", "set": {"pelvis": [0, 0.57, -0.45], "Chest": [22, 0, 0]}, "ease": "accel"},
                {"f": 30, "pose": "c_knees", "ease": "soft"},
                {"f": 38, "pose": "c_knees", "set": {"Head": [-6, 0, 0], "Neck": [-2, 0, 0], "Chest": [12, 0, 0]}, "ease": "smooth"},
                {"f": 44, "pose": "c_knees", "set": {"Head": [-5, 0, 0], "Neck": [-2, 0, 0], "Chest": [12, 0, 0]}}],
                "steps": [{"foot": "R", "f0": 1, "f1": 6, "to": [0.0, -0.30], "lift": 0.05, "shift": 2, "w_support": 0.3, "settle": 2,
                           "land": 0.0},
                          {"foot": "L", "f0": 7, "f1": 13, "to": [0.0, -0.40], "lift": 0.05, "shift": 2, "w_support": 0.3, "settle": 4,
                           "land": 0.0}],
                "layers": {"breath": 0, "noise": 0.4}},
        },
    },
    # 2.5 s: gathers, punches a fist up, a second smaller pump, settles tall (hold frames 58 to 75).
    "Victory": {
        "frames": 75, "hold": [58, 75],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 8, "set": {"pelvis": [0, 0, -0.05], "Chest": [6, 0, 0], "Head": [4, 0, 0],
                                  "armR+": {"UpperArm": [4, 0, 0], "LowerArm": [-30, 0, 0]}, "armL+": {"LowerArm": [-18, 0, 0]}}},
                 {"f": 17, "set": {"armR": FIST_UP, "shoulderR": [10, -3], "pelvis": [0, 0, 0.012], "Chest": [-8, 0, -3],
                                   "Spine": [-3, 0, 0], "Head": [-10, 0, 0], "armL+": {"UpperArm": [6, 0, 6], "LowerArm": [-40, 0, 0]},
                                   "weight": 0.45}, "ease": "decel"},
                 {"f": 26, "set": {"armR": FIST_MID, "shoulderR": [6, -2], "pelvis": [0, 0, -0.02], "Chest": [-3, 0, -2], "Head": [-4, 0, 0]},
                  "ease": "soft"},
                 {"f": 33, "set": {"armR": FIST_UP, "shoulderR": [9, -3], "pelvis": [0, 0, 0.008], "Chest": [-7, 0, -3], "Head": [-9, 0, 0]},
                  "ease": "decel"},
                 {"f": 44, "set": {"armR": FIST_UP, "shoulderR": [8, -3], "Chest": [-6, 0, -2], "Head": [-7, 0, 0]}},
                 {"f": 58, "set": {"armR": "hang", "shoulderR": [0, -3], "shoulderL": [0, -3], "armL": "hang", "pelvis": [0, 0, 0.004],
                                   "Chest": [-5, 0, 0], "Spine": [-1, 0, 0], "Head": [-5, 0, 0], "weight": 0.5}, "ease": "smooth"},
                 {"f": 75, "set": {"shoulderR": [0, -3], "shoulderL": [0, -3], "pelvis": [0, 0, 0.004], "Chest": [-5, 0, 0],
                                   "Spine": [-1, 0, 0], "Head": [-5, 0, 0]}}],
        "offsets": {"head": 2},
        "variants": {
            # Mira: no jump. A breath, the staff lifted and set down once, firmly; chin up; the free hand to the brooch.
            "mira": {"replace": True, "frames": 75, "tempo": False, "hold": [56, 75], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 10, "set": {"Chest": [-3, 0, 0], "Head": [-3, 0, 0], "pelvis": [0, 0, 0.006], "shoulderL": [3, 0], "shoulderR": [3, 0]}},
                {"f": 22, "set": {"pinL": {"pos": [-0.05, 0.10, 0.24]}, "Chest": [-5, 0, 0], "Head": [-8, 4, 0], "pelvis": [0, 0, 0.012],
                                  "shoulderL": [8, 0], "eyes": [5, -4]}, "ease": "decel"},
                {"f": 27, "set": {"pinL": {"pos": [-0.05, 0.10, 0.26]}}, "only": ["armL"]},
                {"f": 31, "set": {"pinL": {"pos": [-0.05, 0.10, 0.0]}, "Chest": [2, 0, 0], "Head": [5, 0, 0], "pelvis": [0, 0, -0.018],
                                  "shoulderL": [0, 0], "eyes": [0, 0]}, "ease": "accel"},
                {"f": 37, "set": {"Chest": [-2, 0, 0], "Head": [-2, 0, 0], "pelvis": [0, 0, 0.0]}, "ease": "soft"},
                {"f": 48, "set": {"armR": {"reach": [0.26, 0.40, -0.24], "elbow": [-0.8, -0.3, -0.5], "palm": "back"},
                                  "Chest": [-4, 0, 0], "Head": [-5, 0, 0], "shoulderR": [2, 4]}, "ease": "smooth"},
                {"f": 56, "set": {"armR": {"reach": [0.26, 0.40, -0.24], "elbow": [-0.8, -0.3, -0.5], "palm": "back"},
                                  "Chest": [-4, 0, 0], "Head": [-6, 0, 0], "shoulderR": [2, 4], "pelvis": [0, 0, 0.004]}},
                {"f": 75, "set": {"armR": {"reach": [0.26, 0.40, -0.24], "elbow": [-0.8, -0.3, -0.5], "palm": "back"},
                                  "Chest": [-4, 0, 0], "Head": [-6, 0, 0], "shoulderR": [2, 4], "pelvis": [0, 0, 0.004]}}]},
            # Champion: crouches, then both arms go up with the weapon overhead and the head back; three pumps, each smaller;
            # ends with the weapon over the shoulder and a fist on the hip.
            "champion": {"replace": True, "frames": 78, "tempo": False, "hold": [62, 78], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 8, "set": {"pelvis": [0, 0, -0.07], "Chest": [10, 0, 0], "Spine": [4, 0, 0], "Head": [6, 0, 0],
                                 "armL+": {"LowerArm": [-30, 0, 0]}, "armR+": {"LowerArm": [-24, 0, 0]}}},
                {"f": 17, "set": {"pelvis": [0, 0, 0.012], "Chest": [-12, 0, 0], "Spine": [-5, 0, 0], "Head": [-14, 0, 0], "jaw": 4,
                                  "armR": {"reach": [-0.36, 0.10, 0.94], "elbow": [-0.8, -0.3, -0.4]}, "propR": {"aim": [-0.15, -0.1, 1.0]},
                                  "armL": {"reach": [0.56, 0.20, 0.56], "elbow": [0.8, -0.3, -0.5], "palm": "right"},
                                  "shoulderL": [8, -3], "shoulderR": [10, -3]}, "ease": "decel"},
                {"f": 24, "set": {"pelvis": [0, 0, -0.035], "Chest": [-5, 0, 0], "Head": [-6, 0, 0],
                                  "armR": {"reach": [-0.42, 0.20, 0.72], "elbow": [-0.8, -0.3, -0.4]},
                                  "armL": {"reach": [0.56, 0.26, 0.34], "elbow": [0.8, -0.3, -0.5], "palm": "right"}}, "ease": "soft"},
                {"f": 30, "set": {"pelvis": [0, 0, 0.006], "Chest": [-11, 0, 0], "Head": [-13, 0, 0],
                                  "armR": {"reach": [-0.36, 0.10, 0.94], "elbow": [-0.8, -0.3, -0.4]},
                                  "armL": {"reach": [0.56, 0.20, 0.56], "elbow": [0.8, -0.3, -0.5], "palm": "right"}}, "ease": "decel"},
                {"f": 37, "set": {"pelvis": [0, 0, -0.025], "Chest": [-6, 0, 0], "Head": [-7, 0, 0],
                                  "armR": {"reach": [-0.42, 0.20, 0.76], "elbow": [-0.8, -0.3, -0.4]},
                                  "armL": {"reach": [0.56, 0.26, 0.38], "elbow": [0.8, -0.3, -0.5], "palm": "right"}}, "ease": "soft"},
                {"f": 43, "set": {"pelvis": [0, 0, 0.0], "Chest": [-10, 0, 0], "Head": [-11, 0, 0], "jaw": 3,
                                  "armR": {"reach": [-0.36, 0.10, 0.92], "elbow": [-0.8, -0.3, -0.4]},
                                  "armL": {"reach": [0.56, 0.20, 0.52], "elbow": [0.8, -0.3, -0.5], "palm": "right"}}, "ease": "decel"},
                {"f": 62, "set": {"pelvis": [0, 0, 0.0], "Chest": [-5, 0, 0], "Spine": [-2, 0, 0], "Head": [-4, 0, 0], "jaw": 0,
                                  "armR": {"reach": [-0.30, 0.30, -0.06], "elbow": [-0.8, -0.3, -0.5]}, "propR": {"aim": [-0.15, -0.55, 0.82]},
                                  "armL": {"reach": [0.44, -0.04, -0.72], "elbow": [0.9, -0.4, 0.0], "palm": "right"},
                                  "shoulderL": [0, -3], "shoulderR": [3, -3]}, "ease": "smooth"},
                {"f": 78, "set": {"pelvis": [0, 0, 0.0], "Chest": [-5, 0, 0], "Spine": [-2, 0, 0], "Head": [-4, 0, 0],
                                  "armR": {"reach": [-0.30, 0.30, -0.06], "elbow": [-0.8, -0.3, -0.5]}, "propR": {"aim": [-0.15, -0.55, 0.82]},
                                  "armL": {"reach": [0.44, -0.04, -0.72], "elbow": [0.9, -0.4, 0.0], "palm": "right"},
                                  "shoulderL": [0, -3], "shoulderR": [3, -3]}}]},
        },
    },
    # 2.5 s: the head and shoulders go first, the knees give, down onto both knees, sits back, hold (frames 54 to 75).
    "Defeat": {
        "frames": 75, "hold": [54, 75],
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 12, "set": {"Head": [14, 0, 2], "Neck": [5, 0, 0], "Chest": [6, 0, 0], "shoulderL": [-4, 4], "shoulderR": [-4, 4],
                                   "pelvis": [0, 0, -0.03]}, "ease": "smooth"},
                 {"f": 26, "set": {"pelvis": [0, 0.20, -0.20], "footL": {"pitch": 34}, "footR": {"pitch": 32}, "Chest": [3, 0, 0],
                                   "Spine": [3, 0, 0], "Head": [18, 0, 2], "Neck": [6, 0, 0]}, "ease": "accel"},
                 {"f": 37, "pose": "knees", "set": {"pelvis": [0, 0.52, -0.45], "Chest": [20, 0, 0]}, "ease": "accel"},
                 {"f": 43, "pose": "knees", "set": {"pelvis": [0, 0.46, -0.44], "Chest": [5, 0, 0], "Head": [14, 0, 2]}, "ease": "soft"},
                 {"f": 54, "pose": "knees", "ease": "smooth"},
                 {"f": 75, "pose": "knees"}],
        "offsets": {"head": 2, "armL": 2, "armR": 3},
        "layers": {"noise": 0.3},
        "variants": {
            # Mira: stays on her feet a moment, sags onto the staff, then down on one knee, head bowed, both hands on the staff.
            "mira": {"replace": True, "frames": 75, "tempo": False, "hold": [56, 75], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 14, "set": {"Head": [14, 3, 0], "Neck": [5, 0, 0], "Chest": [5, 4, 0], "shoulderL": [-2, 3], "shoulderR": [-4, 4],
                                  "pelvis": [0, 0, -0.025]}, "ease": "smooth"},
                {"f": 30, "set": {"pelvis": [0, 0, -0.13], "Chest": [9, 6, 0], "Spine": [4, 2, 0], "Head": [18, 3, 0], "Neck": [6, 0, 0],
                                  "footR": {"pitch": 18}}, "ease": "smooth"},
                {"f": 46, "pose": "m_kneel", "set": {"pelvis": [0, 0, -0.42], "pinL": {"pos": [-0.02, 0.20, 0.0]}}, "ease": "accel"},
                {"f": 56, "pose": "m_kneel", "set": {"pinL": {"pos": [-0.02, 0.20, 0.0]}, "Head": [26, 2, 0],
                                                     "armR": {"at": [0.30, 0.34, 0.74], "elbow": [-0.7, -0.5, -0.4], "palm": "left"}},
                 "ease": "smooth"},
                {"f": 75, "pose": "m_kneel", "set": {"pinL": {"pos": [-0.02, 0.20, 0.0]}, "Head": [26, 2, 0],
                                                     "armR": {"at": [0.30, 0.34, 0.74], "elbow": [-0.7, -0.5, -0.4], "palm": "left"}}}],
                "steps": [{"foot": "R", "f0": 31, "f1": 39, "to": [0.0, -0.50], "lift": 0.03, "shift": 5, "w_support": 0.25,
                           "settle": 6, "land": 14.0}],
                "layers": {"noise": 0.3}},
            # Champion: drops straight onto both knees, heavy, the weapon falls forward onto the ground, head hangs.
            "champion": {"replace": True, "frames": 75, "tempo": False, "hold": [52, 75], "keys": [
                {"f": 0, "pose": "stand"},
                {"f": 10, "set": {"Head": [12, 0, 0], "Neck": [4, 0, 0], "Chest": [5, 0, 0], "shoulderL": [-5, 4], "shoulderR": [-5, 4],
                                  "pelvis": [0, 0, -0.03], "propR": {"aim": [0.0, 0.25, -0.97]}}, "ease": "smooth"},
                {"f": 22, "set": {"pelvis": [0, 0.20, -0.20], "footL": {"pitch": 34}, "footR": {"pitch": 32}, "Chest": [2, 0, 0],
                                  "Spine": [3, 0, 0], "Head": [16, 0, 0]}, "ease": "accel"},
                {"f": 31, "pose": "c_knees", "set": {"pelvis": [0, 0.50, -0.44], "Chest": [22, 0, 0], "Head": [10, 0, 0],
                                                     "armR": {"at": [-0.44, 0.34, 0.26], "elbow": [-0.8, -0.5, 0.2]},
                                                     "propR": {"aim": [-0.25, 0.92, -0.25]},
                                                     "armL": {"at": [0.20, 0.50, 0.44], "elbow": [0.8, -0.5, 0.0], "palm": "down"}},
                 "ease": "accel"},
                {"f": 38, "pose": "c_knees", "set": {"pelvis": [0, 0.42, -0.46], "Chest": [8, 0, 0], "Head": [20, 0, 0],
                                                     "armR": {"at": [-0.44, 0.30, 0.24], "elbow": [-0.8, -0.5, 0.2]},
                                                     "propR": {"aim": [-0.25, 0.92, -0.25]},
                                                     "armL": {"at": [0.20, 0.46, 0.44], "elbow": [0.8, -0.5, 0.0], "palm": "down"}},
                 "ease": "soft"},
                {"f": 52, "pose": "c_knees", "set": {"pelvis": [0, 0.44, -0.47], "Chest": [18, 0, 0], "Spine": [10, 0, 0], "Head": [28, 0, 3],
                                                     "armR": {"at": [-0.44, 0.30, 0.20], "elbow": [-0.8, -0.5, 0.2]},
                                                     "propR": {"aim": [-0.25, 0.94, -0.2]},
                                                     "armL": {"at": [0.20, 0.46, 0.44], "elbow": [0.8, -0.5, 0.0], "palm": "down"}},
                 "ease": "smooth"},
                {"f": 75, "pose": "c_knees", "set": {"pelvis": [0, 0.44, -0.47], "Chest": [18, 0, 0], "Spine": [10, 0, 0], "Head": [28, 0, 3],
                                                     "armR": {"at": [-0.44, 0.30, 0.20], "elbow": [-0.8, -0.5, 0.2]},
                                                     "propR": {"aim": [-0.25, 0.94, -0.2]},
                                                     "armL": {"at": [0.20, 0.46, 0.44], "elbow": [0.8, -0.5, 0.0], "palm": "down"}}}],
                "layers": {"noise": 0.3}},
        },
    },
    # 0.8 s: starts back (shoulders up, arms out, head away), peers forward, settles. Also the old 'surprise' key.
    "Surprise": {
        "frames": 24,
        "keys": [{"f": 0, "pose": "stand"},
                 {"f": 3, "set": {"Chest": [-8, 0, 0], "Spine": [-3, 0, 0], "Head": [-7, 0, 0], "Neck": [-2, 0, 0],
                                  "pelvis": [0, -0.02, 0.012], "shoulderL": [9, -2], "shoulderR": [9, -2],
                                  "armL+": {"UpperArm": [-6, 0, -16], "LowerArm": [-34, 0, 0]},
                                  "armR+": {"UpperArm": [-6, 0, 18], "LowerArm": [-38, 0, 0]}}, "ease": "snap"},
                 {"f": 9, "set": {"Chest": [-5, 0, 0], "Spine": [-2, 0, 0], "Head": [-4, 0, 0], "pelvis": [0, -0.016, -0.012],
                                  "shoulderL": [6, -1], "shoulderR": [6, -1],
                                  "armL+": {"UpperArm": [-4, 0, -12], "LowerArm": [-30, 0, 0]},
                                  "armR+": {"UpperArm": [-4, 0, 13], "LowerArm": [-32, 0, 0]}}, "ease": "soft"},
                 {"f": 16, "set": {"Chest": [3, 0, 0], "Spine": [1, 0, 0], "Head": [3, 0, 0], "Neck": [0, 0, 0], "pelvis": [0, 0.006, -0.006],
                                   "shoulderL": [1, 0], "shoulderR": [1, 0],
                                   "armL+": {"UpperArm": [0, 0, -3], "LowerArm": [-8, 0, 0]},
                                   "armR+": {"UpperArm": [0, 0, 3], "LowerArm": [-9, 0, 0]}}, "ease": "smooth"},
                 {"f": 24, "pose": "stand"}],
        "offsets": {"armL": 1, "armR": 2},
        "layers": {"noise": 0, "breath": 0},
    },
}
