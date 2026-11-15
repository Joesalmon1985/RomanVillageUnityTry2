"""The prince's clips (WP-5): beckon, look back while leading, point at the ground.

The prince faces the player when he beckons; the plot is to his left, so the sweep and the look go that way.
"""

REACH_OUT = {"reach": [-0.20, 0.92, -0.18], "elbow": [-0.6, -0.3, -0.7], "palm": "up"}
SWEEP_IN = {"reach": [-0.20, 0.34, 0.20], "elbow": [-0.75, -0.25, -0.6], "palm": "back"}
SWEEP_FLICK = {"reach": [-0.14, 0.24, 0.30], "elbow": [-0.75, -0.25, -0.6], "palm": "back", "hand": [-14, 0, 0]}
# Pointing at the ground a couple of steps ahead and a little to the right.
POINT_ARM = {"reach": [-0.26, 0.80, -0.52], "elbow": [-0.5, -0.5, -0.5], "palm": "left", "space": "root"}
POINT_JAB = {"reach": [-0.27, 0.84, -0.60], "elbow": [-0.5, -0.5, -0.5], "palm": "left", "space": "root"}
POINT_READY = {"reach": [-0.20, 0.40, -0.30], "elbow": [-0.8, -0.4, -0.5], "palm": "left"}

CLIPS = {
    # 1.6 s. Reach out to the player palm up, sweep the hand back to the shoulder while the body opens toward the
    # plot (eyes and head go there first), a small second flick, then come back.
    "Beckon": {
        "frames": 48, "only_for": ["prince"],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 4, "set": {"armR+": {"UpperArm": [6, 0, -4], "LowerArm": [5, 0, 0]}, "Chest": [1, -3, 0],
                             "pelvis": [0, -0.008, -0.008], "Head": [-2, 0, 0]}},
            {"f": 12, "set": {"armR": REACH_OUT, "shoulderR": [2, 8], "Chest": [4, 5, 0], "Spine": [1.5, 2, 0],
                              "pelvis": [0, 0.02, -0.004], "Head": [-2, 0, 2], "weight": 0.45}, "ease": "decel"},
            {"f": 15, "set": {"eyes": [12, 0]}, "only": ["eyes"]},
            {"f": 25, "set": {"armR": SWEEP_IN, "shoulderR": [6, -2], "Chest": [-2, 16, -2], "Spine": [0, 7, 0],
                              "Hips": [0, 5, 0], "pelvis": [0.01, 0, 0.004], "Head": [-3, 30, -3], "Neck": [0, 12, 0],
                              "eyes": [10, 0], "weight": 0.66}, "ease": "soft"},
            {"f": 30, "set": {"armR": SWEEP_FLICK, "shoulderR": [7, -3], "Head": [-5, 34, -3], "Neck": [0, 13, 0],
                              "Chest": [-3, 18, -2]}, "ease": "soft"},
            {"f": 36, "set": {"armR": SWEEP_IN, "shoulderR": [5, -2], "Head": [-2, 26, -2], "Neck": [0, 10, 0],
                              "Chest": [-1, 14, -1], "Spine": [0, 6, 0], "Hips": [0, 4, 0], "eyes": [4, 0], "weight": 0.64},
             "ease": "soft"},
            {"f": 48, "pose": "stand"},
        ],
        "offsets": {"head": -2},
        "layers": {"noise": 0.3},
    },
    # 1.2 s, additive on Walk. A glance back over the left shoulder: eyes, head, then the chest; the left arm
    # opens back a little ("this way"); return in the same order.
    "LeadLook": {
        "frames": 36, "only_for": ["prince"],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 5, "set": {"eyes": [13, 0], "Head": [0, 16, 0], "Neck": [0, 6, 0]}, "only": ["head"]},
            {"f": 13, "set": {"eyes": [12, 0], "Head": [3, 40, -5], "Neck": [0, 24, 0], "Chest": [0, 16, 0],
                              "Spine": [0, 7, 0], "shoulderL": [0, -6], "armL+": {"UpperArm": [10, 0, -9], "LowerArm": [8, 0, 0]}},
             "ease": "decel"},
            {"f": 23, "set": {"eyes": [10, 0], "Head": [2, 38, -4], "Neck": [0, 23, 0], "Chest": [0, 15, 0],
                              "Spine": [0, 7, 0], "shoulderL": [0, -5], "armL+": {"UpperArm": [8, 0, -8], "LowerArm": [6, 0, 0]}}},
            {"f": 27, "set": {"eyes": [0, 0]}, "only": ["eyes"]},
            {"f": 36, "pose": "stand"},
        ],
        "offsets": {"torso": 2, "armL": 3},
        "layers": {"noise": 0, "breath": 0},
    },
    # 2.4 s. Look down at the spot, lift the hand, point with a lean and a step of weight onto the right foot,
    # hold for a second (hold frames 22 to 52: a small jab, then a look up at the player), release.
    "PointGround": {
        "frames": 72, "only_for": ["prince"], "hold": [22, 52],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 4, "set": {"eyes": [-9, 5]}, "only": ["eyes"]},
            {"f": 8, "set": {"armR": POINT_READY, "shoulderR": [4, 0], "Head": [10, -10, 0], "Neck": [4, -4, 0],
                             "Chest": [-1, 2, 0], "pelvis": [0, -0.006, 0.004], "weight": 0.55}, "ease": "soft"},
            {"f": 17, "set": {"armR": POINT_JAB, "shoulderR": [0, 9], "Head": [17, -14, 0], "Neck": [6, -5, 0],
                              "Chest": [9, -9, 1], "Spine": [4, -3, 0], "Hips": [2, -2, 0], "pelvis": [-0.01, 0.04, -0.03],
                              "weight": 0.30, "eyes": [-6, 5]}, "ease": "decel"},
            {"f": 22, "set": {"armR": POINT_ARM, "shoulderR": [0, 8], "Head": [16, -13, 0], "Neck": [6, -5, 0],
                              "Chest": [8, -8, 1], "Spine": [3.5, -3, 0], "Hips": [2, -2, 0], "pelvis": [-0.01, 0.035, -0.026],
                              "weight": 0.31}, "ease": "soft"},
            {"f": 30, "set": {"armR": POINT_JAB, "Head": [18, -13, 0]}, "ease": "accel", "only": ["armR", "head"]},
            {"f": 34, "set": {"armR": POINT_ARM, "Head": [15, -12, 0], "eyes": [-4, 4]}, "ease": "soft", "only": ["armR", "head"]},
            {"f": 37, "set": {"eyes": [3, -4]}, "only": ["eyes"]},
            {"f": 43, "set": {"Head": [-2, 3, 2], "Neck": [0, 1, 0], "eyes": [0, -2]}, "ease": "soft", "only": ["head"]},
            {"f": 52, "set": {"armR": POINT_ARM, "shoulderR": [0, 8], "Head": [-1, 3, 2], "Neck": [0, 1, 0], "eyes": [0, 0],
                              "Chest": [8, -8, 1], "Spine": [3.5, -3, 0], "Hips": [2, -2, 0], "pelvis": [-0.01, 0.035, -0.026],
                              "weight": 0.31}},
            {"f": 61, "set": {"armR": POINT_READY, "shoulderR": [2, 0], "Chest": [1, -1, 0], "Spine": [0.5, 0, 0],
                              "Hips": [0, 0, 0], "pelvis": [0, 0, 0.004], "weight": 0.48, "Head": [-2, 0, 0]}, "ease": "smooth"},
            {"f": 72, "pose": "stand"},
        ],
        "layers": {"noise": 0.4},
    },
}
