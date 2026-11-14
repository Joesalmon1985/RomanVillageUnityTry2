"""Locomotion clips (WP-5): gait cycles and the start, stop and turn generators. See lib/anim_gait.py.

Gait numbers (defaults in anim_gait.DEFAULT_GAIT; a style file can override any of them per character):
  speed        native ground speed in m/s (the root moves this fast when the clip plays at speed 1)
  duty         share of the cycle a foot is on the ground (walk > 0.5: double support; run < 0.5: flight)
  bias         shifts the whole stride forward (+) or back (-) under the hips, metres
  heel_strike  toes-up angle at contact; rolldown: share of the cycle to get the foot flat
  heel_off     share of the stance after which the heel starts to rise; toe_off: heel-up angle when the foot leaves
  lift, kick   swing clearance and the extra heel kick behind (run)
  bob, sway, surge, drop      pelvis up/down, side to side, fore/aft, and a constant crouch
  hip_yaw, hip_roll, hip_pitch, chest_*   rotations; the chest winds against the hips a little late
  arm_*        swing forward/back, elbow bend (base + on the forward swing), swing across the body, held away
Frames per cycle and speed give the stride: Walk 30 f at 1.4 m/s = 0.70 m per step.
"""

CLIPS = {
    "Walk": {
        "type": "gait", "frames": 30,
        "gait": {"speed": 1.4, "arm_fwd": 20.0, "arm_back": 24.0, "elbow": 2.0, "elbow_dyn": 14.0},
    },
    "WalkBrisk": {
        "type": "gait", "frames": 20,
        "gait": {"speed": 2.2, "duty": 0.58, "bias": -0.08, "heel_strike": 22.0, "toe_off": 48.0, "lift": 0.05,
                 "bob": 0.022, "sway": 0.018, "hip_yaw": 8.0, "hip_pitch": 3.0, "chest_yaw": 7.0, "chest_pitch": 4.5,
                 "arm_fwd": 30.0, "arm_back": 28.0, "elbow": 18.0, "elbow_dyn": 26.0, "arm_cross": 6.0,
                 "hand_flop": 9.0, "shoulder": 4.5, "head_nod": 1.4},
    },
    # native 3.6 m/s: plays at 0.83x for 3 m/s and 1.4x for 5 m/s
    "Jog": {
        "type": "gait", "frames": 16,
        "gait": {"speed": 3.6, "duty": 0.34, "bias": -0.05, "heel_strike": 6.0, "rolldown": 0.04, "heel_off": 0.45,
                 "toe_off": 52.0, "lift": 0.10, "kick": 30.0, "swing_peak": 0.6,
                 "bob": 0.032, "bob_phase": 0.20, "sway": 0.010, "surge": 0.01, "drop": 0.045,
                 "hip_yaw": 9.0, "hip_roll": 3.0, "hip_pitch": 6.0, "chest_yaw": 9.0, "chest_roll": 1.5,
                 "chest_pitch": 9.0, "chest_nod": 1.5,
                 "arm_fwd": 34.0, "arm_back": 34.0, "elbow": 62.0, "elbow_dyn": 22.0, "arm_cross": 9.0,
                 "hand_flop": 5.0, "shoulder": 6.0, "head_nod": 1.5, "head_level": 0.9, "prop": "carry"},
    },
    # native 7 m/s (the expedition party's speed): long flight, deep lean, arms pumping
    "Run": {
        "type": "gait", "frames": 14,
        "gait": {"speed": 7.0, "duty": 0.25, "bias": -0.07, "heel_strike": 2.0, "rolldown": 0.03, "heel_off": 0.40,
                 "toe_off": 58.0, "lift": 0.16, "kick": 48.0, "swing_peak": 0.62,
                 "bob": 0.042, "bob_phase": 0.15, "sway": 0.006, "surge": 0.012, "drop": 0.05,
                 "hip_yaw": 12.0, "hip_roll": 3.0, "hip_pitch": 10.0, "chest_yaw": 12.0, "chest_roll": 1.5,
                 "chest_pitch": 13.0, "chest_nod": 2.0,
                 "arm_fwd": 46.0, "arm_back": 44.0, "elbow": 78.0, "elbow_dyn": 18.0, "arm_cross": 10.0,
                 "hand_flop": 4.0, "shoulder": 8.0, "head_nod": 1.5, "head_level": 0.92, "prop": "carry"},
    },
    # Start: the pelvis moves over the standing foot first (frames 0 to 5), then the other foot leaves.
    # Ends on the Walk at the standing foot's toe-off (exit_phase in the clip record).
    "StartL": {
        "type": "start", "frames": 18, "gait_clip": "Walk", "push_frame": 3, "lift_frame": 5,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 4, "set": {"pelvis": [0, -0.012, -0.016], "Spine": [1.5, 0, 0], "Chest": [2.5, -3, 0],
                             "Head": [-2, 0, 0], "armR+": {"UpperArm": [5, 0, 0]}, "armL+": {"UpperArm": [-3, 0, 0]}}},
            {"f": 9, "set": {"pelvis": [0, 0.03, -0.02], "Spine": [2, 0, 0], "Chest": [5, 2, 0], "Head": [-3, 0, 0]},
             "ease": "soft"},
            {"f": 18, "pose": "stand"},
        ],
    },
    "StartR": {"mirror_of": "StartL"},
    # Stop: enters on the Walk at a heel strike. The front foot stays planted, the rear foot closes, the body
    # carries on over the feet, dips, comes back and settles.
    "StopL": {
        "type": "stop", "frames": 20, "gait_clip": "Walk", "glide_frames": 12, "land_frame": 11,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 7, "set": {"pelvis": [0, 0.04, -0.03], "Spine": [3.5, 0, 0], "Chest": [8, 0, 0], "Head": [-5, 0, 0],
                             "armL+": {"UpperArm": [-12, 0, 0], "LowerArm": [-10, 0, 0]},
                             "armR+": {"UpperArm": [-9, 0, 0], "LowerArm": [-12, 0, 0]}}, "ease": "soft"},
            {"f": 12, "set": {"pelvis": [0, 0.055, -0.036], "Chest": [4, 0, 0]}, "ease": "soft"},
            {"f": 16, "set": {"pelvis": [0, -0.012, 0.005], "Spine": [-1.0, 0, 0], "Chest": [-3.5, 0, 0], "Head": [2, 0, 0],
                              "armL+": {"UpperArm": [5, 0, 0]}, "armR+": {"UpperArm": [4, 0, 0]}}, "ease": "soft"},
            {"f": 20, "pose": "stand"},
        ],
        "offsets": {"head": 1, "armL": 2, "armR": 3},
    },
    "StopR": {"mirror_of": "StopL"},
    # Turn 60 degrees to the left on the spot: look first, weight onto the outside foot, inside foot steps round,
    # weight across, outside foot closes, the chest and arms arrive last.
    "Turn60L": {
        "type": "turn", "frames": 17, "turn_deg": 60.0, "turn_from": 2, "turn_to": 13,
        "turn_steps": [{"foot": "L", "f0": 3, "f1": 8}, {"foot": "R", "f0": 9, "f1": 14}],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 3, "set": {"eyes": [10, 0], "Head": [0, 16, 2], "Neck": [0, 8, 0], "Chest": [0, 6, 0],
                             "Spine": [0, 3, 0], "pelvis": [0, 0, -0.014]}},
            {"f": 8, "set": {"eyes": [4, 0], "Head": [0, 12, 0], "Neck": [0, 5, 0], "Chest": [1.5, 3, 0],
                             "Spine": [0, 1, 0], "pelvis": [0, 0, -0.02],
                             "armR+": {"UpperArm": [0, 0, 5]}, "armL+": {"UpperArm": [0, 0, -3]}}, "ease": "soft"},
            {"f": 13, "set": {"eyes": [0, 0], "Head": [0, -2, 0], "Neck": [0, 0, 0], "Chest": [0, -4, 0],
                              "Spine": [0, -1.5, 0], "pelvis": [0, 0, -0.012],
                              "armR+": {"UpperArm": [0, 0, 3]}}, "ease": "soft"},
            {"f": 17, "pose": "stand"},
        ],
    },
    "Turn60R": {"mirror_of": "Turn60L"},
    # Additive, for heading changes under 40 degrees: a look, a small shift onto the inside foot and a settle.
    "TurnSmallL": {
        "frames": 12,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 3, "set": {"eyes": [8, 0], "Head": [0, 9, 0], "Neck": [0, 4, 0], "weight": 0.58, "pelvis": [0, 0, -0.008]}},
            {"f": 7, "set": {"eyes": [0, 0], "Head": [0, 3, 0], "Chest": [0, 4, 0], "Spine": [0, 2, 0], "weight": 0.6,
                             "footR": {"pitch": 10}}, "ease": "soft"},
            {"f": 12, "pose": "stand"},
        ],
        "layers": {"noise": 0, "breath": 0},
    },
    "TurnSmallR": {"mirror_of": "TurnSmallL"},
}
