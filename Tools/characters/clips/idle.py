"""Idle clips (WP-5). All loop; frame 0 is the character's stand pose so one-shots chain into them.

Breathing, small drifts, arm overlap and the hip drop on the unloaded side come from the generator layers; the keys
here only say where the weight is, where the head looks and what the hands do.
"""

CLIPS = {
    # 6 s. Weight goes to the left leg, rests, crosses to the right, rests, comes back through the middle.
    "Idle_A": {
        "frames": 180, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 34, "set": {"weight": 0.70, "Head": [0, -3, 1], "Chest": [0, -1.5, 0]}, "ease": "decel"},
            {"f": 82, "set": {"weight": 0.68, "Head": [1, 2, 0]}},
            {"f": 118, "set": {"weight": 0.29, "Head": [0, 4, -1.5], "Chest": [0, 2, 0]}},
            {"f": 152, "set": {"weight": 0.31, "Head": [-1, 1, -1]}},
            {"f": 180, "set": {"weight": 0.50}, "ease": "accel"},
        ],
        "offsets": {"head": 5},
    },
    # 4.5 s. A shoulder roll and a small re-plant of the right foot: the body unlocks and settles again.
    "Idle_B": {
        "frames": 136, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 26, "set": {"weight": 0.70, "shoulderL": [5, -3], "shoulderR": [6, -4], "Chest": [-3, 0, 0],
                              "Head": [-4, 0, 2], "pelvis": [0, 0, 0.004]}, "ease": "decel"},
            {"f": 44, "set": {"weight": 0.72, "shoulderL": [-2, 2], "shoulderR": [-2, 3], "Chest": [1.5, 0, 0],
                              "Head": [2, -3, 0], "pelvis": [0, 0, -0.006]}},
            {"f": 70, "set": {"weight": 0.55, "shoulderL": [0, 0], "shoulderR": [0, 0], "Chest": [0, 3, 0], "Head": [0, 5, -1]}},
            {"f": 104, "set": {"weight": 0.60, "Chest": [0, -1, 0], "Head": [1, -2, 1]}},
            {"f": 136, "set": {"weight": 0.50}, "ease": "accel"},
        ],
        "steps": [{"foot": "R", "f0": 30, "f1": 37, "to": [-0.035, 0.03], "yaw": -5, "lift": 0.025, "shift": 5,
                   "w_support": 0.2, "settle": 8, "event": False},
                  {"foot": "R", "f0": 108, "f1": 115, "to": [0.0, 0.0], "yaw": 0, "lift": 0.02, "shift": 6,
                   "w_support": 0.25, "settle": 10, "event": False}],
        "offsets": {"head": 4, "armL": 3, "armR": 4},
    },
    # 5 s. Eyes go first, the head follows, the chest a little; a long look left, a shorter one right, settle.
    "Idle_Look": {
        "frames": 150, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 10, "set": {"eyes": [12, 0]}, "only": ["eyes"]},
            {"f": 24, "set": {"eyes": [8, 0], "Head": [-1, 24, -2], "Neck": [0, 10, 0], "Chest": [0, 6, 0],
                              "Spine": [0, 2, 0], "weight": 0.60}, "ease": "decel"},
            {"f": 60, "set": {"eyes": [6, 1], "Head": [1, 22, -1], "Neck": [0, 9, 0], "Chest": [0, 5, 0],
                              "Spine": [0, 2, 0], "weight": 0.61}},
            {"f": 68, "set": {"eyes": [-10, 0]}, "only": ["eyes"]},
            {"f": 84, "set": {"eyes": [-6, 0], "Head": [0, -15, 1.5], "Neck": [0, -6, 0], "Chest": [0, -4, 0],
                              "Spine": [0, -1, 0], "weight": 0.44}},
            {"f": 112, "set": {"eyes": [-4, 0], "Head": [1, -13, 1], "Neck": [0, -5, 0], "Chest": [0, -3, 0],
                               "Spine": [0, -1, 0], "weight": 0.43}},
            {"f": 120, "set": {"eyes": [0, 0]}, "only": ["eyes"]},
            {"f": 136, "set": {"Head": [2, 0, 0], "Neck": [0, 0, 0], "Chest": [0, 0, 0], "Spine": [0, 0, 0], "weight": 0.49}},
            {"f": 150, "set": {"weight": 0.50}},
        ],
        "offsets": {"torso": 3},
        "layers": {"noise": 0.6},
    },
    # 6 s. The prop hand stays where it is (planted staff, or a weapon held still); the off hand and the body breathe.
    # On a character with a planted staff the hand is pinned in the style's stand pose, so the staff never slides.
    "Idle_Staff": {
        "frames": 180, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 40, "set": {"weight": 0.61, "Head": [0, 3, 0]}, "ease": "decel"},
            {"f": 70, "set": {"weight": 0.62, "Head": [-5, 9, -2], "Neck": [-2, 4, 0], "eyes": [6, -3]}},
            {"f": 100, "set": {"weight": 0.60, "Head": [-4, 8, -2], "Neck": [-2, 4, 0], "eyes": [5, -3]}},
            {"f": 126, "set": {"weight": 0.42, "Head": [1, -2, 0], "Neck": [0, 0, 0], "eyes": [0, 0],
                               "armR+": {"LowerArm": [-8, 0, 0]}}},
            {"f": 156, "set": {"weight": 0.41, "Head": [0, 0, 1]}},
            {"f": 180, "set": {"weight": 0.50}, "ease": "accel"},
        ],
        "offsets": {"head": 4},
    },
}
