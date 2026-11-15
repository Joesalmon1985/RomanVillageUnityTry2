"""Campaign beat clips (WP-5, PRD 6.2 "Campaign beats"): KneelTake, Cover, Fall, Point.

KneelTake and Cover end on a hold pose (hold_frames in the clip record), not on the stand pose: the game holds the
last frame or blends out. Fall ends lying down. Point returns to the stand pose.
The pedestal for KneelTake is assumed 0.35 m in front of the character with its top 0.80 m high (the crystal sits
there); the PropHit event marks the frame the hands close on it, so the game can attach the crystal to the hand socket.
"""

PED_REACH_R = {"at": [-0.10, 0.36, 0.86], "elbow": [-0.6, -0.5, -0.6], "palm": "left"}
PED_REACH_L = {"at": [0.10, 0.36, 0.86], "elbow": [0.6, -0.5, -0.6], "palm": "right"}
PED_GRASP_R = {"at": [-0.055, 0.35, 0.82], "elbow": [-0.6, -0.5, -0.6], "palm": "left"}
PED_GRASP_L = {"at": [0.055, 0.35, 0.82], "elbow": [0.6, -0.5, -0.6], "palm": "right"}
PED_LIFT_R = {"at": [-0.055, 0.30, 0.98], "elbow": [-0.7, -0.4, -0.6], "palm": "left"}
PED_LIFT_L = {"at": [0.055, 0.30, 0.98], "elbow": [0.7, -0.4, -0.6], "palm": "right"}
CHEST_R = {"reach": [0.36, 0.46, -0.22], "elbow": [-0.7, -0.4, -0.6], "palm": "left"}
CHEST_L = {"reach": [-0.36, 0.46, -0.22], "elbow": [0.7, -0.4, -0.6], "palm": "right"}
SHOW_R = {"reach": [0.36, 0.60, 0.40], "elbow": [-0.7, -0.3, -0.6], "palm": "left"}
SHOW_L = {"reach": [-0.36, 0.60, 0.40], "elbow": [0.7, -0.3, -0.6], "palm": "right"}

KT_KNEEL = {"pelvis": [0, 0.04, -0.41], "weight": 0.74, "footR": {"pitch": 66},
            # a weapon in the right hand hangs down and out of the way while both hands take the crystal
            "propR": {"aim": [-0.62, 0.10, -0.78]},
            "Chest": [12, 0, 0], "Spine": [5, 0, 0], "Head": [10, 0, 0], "Neck": [3, 0, 0]}

POINT_FAR = {"reach": [-0.20, 0.95, 0.04], "elbow": [-0.5, -0.4, -0.7], "palm": "left", "space": "root"}
POINT_FAR_JAB = {"reach": [-0.20, 0.97, 0.08], "elbow": [-0.5, -0.4, -0.7], "palm": "left", "space": "root"}
POINT_UP = {"reach": [-0.24, 0.42, -0.22], "elbow": [-0.8, -0.4, -0.5], "palm": "left"}

COVER = {   # bladed guard: left side and left forearm to the front, weapon hand low behind, weight on bent legs
    "pelvis": [0, 0.0, -0.10], "weight": 0.58, "Hips": [3, -16, 0], "Spine": [3, -4, 0], "Chest": [5, -6, 0],
    "Head": [-4, 22, 0], "Neck": [0, 6, 0],
    "armL": {"reach": [-0.34, 0.58, -0.06], "elbow": [0.85, -0.1, -0.5], "palm": "back"},
    "armR": {"reach": [-0.42, -0.10, -0.66], "elbow": [-0.6, -0.7, 0.1]},
    "shoulderL": [5, 8], "shoulderR": [2, -6],
}

PRONE = {   # face down: the hips have gone forward over the knees and on to the ground, the feet stay on their toes
    "pelvis": [0, 0.98, -0.775], "footL": {"pitch": 84}, "footR": {"pitch": 80},
    "Hips": [84, 0, 0], "Spine": [3, 0, 0], "Chest": [2, 0, 0], "Neck": [-6, 0, 0], "Head": [-14, 48, 0], "head_level": 0.0,
    "armR": {"at": [-0.34, 1.80, 0.07], "elbow": [-0.9, 0.2, 0.3], "palm": "down"},
    "armL": {"at": [0.40, 1.32, 0.07], "elbow": [0.9, -0.3, 0.3], "palm": "down"},
    # whatever is in the hands goes down with the body and lies flat, pointing ahead
    "pinL": "off", "pinR": "off", "propL": {"aim": [0.12, 0.99, 0.04]}, "propR": {"aim": [-0.12, 0.99, 0.04]},
}
KNEEL2 = {"pelvis": [0, 0.47, -0.45], "footL": {"pitch": 68}, "footR": {"pitch": 66}, "pinL": "off", "pinR": "off",
          "propL": {"aim": [0.25, 0.45, 0.85], "lock": 1.0}, "propR": {"aim": [-0.25, 0.45, -0.85], "lock": 1.0},
          "Chest": [8, 0, 0], "Spine": [5, 0, 0], "Head": [16, 0, 3], "Neck": [5, 0, 0]}

POSES = {"kt_kneel": KT_KNEEL, "cover": COVER, "prone": PRONE, "kneel2": KNEEL2}

CLIPS = {
    # 3.2 s. A look down at the pedestal, a step back onto the right knee, both hands in, close on the crystal (PropHit),
    # lift it clear, stand with it at the chest, raise it to the eyes, bring it back to the chest (hold frames 86 to 96).
    "KneelTake": {
        "frames": 96, "hold": [86, 96], "events": [{"name": "PropHit", "f": 31}],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 5, "set": {"eyes": [0, 5], "Head": [9, 0, 0], "Neck": [3, 0, 0]}, "only": ["head"]},
            {"f": 12, "set": {"pelvis": [0, 0.0, -0.14], "Chest": [8, 0, 0], "Spine": [3, 0, 0], "Head": [12, 0, 0], "footR": {"pitch": 20},
                              "armR+": {"UpperArm": [-14, 0, 0], "LowerArm": [-30, 0, 0]},
                              "armL+": {"UpperArm": [-12, 0, 0], "LowerArm": [-26, 0, 0]}}, "ease": "soft"},
            {"f": 22, "pose": "kt_kneel", "set": {"pelvis": [0, 0.04, -0.43], "armR": PED_REACH_R, "armL": PED_REACH_L}, "ease": "soft"},
            {"f": 26, "pose": "kt_kneel", "set": {"armR": PED_REACH_R, "armL": PED_REACH_L}, "ease": "soft"},
            {"f": 31, "pose": "kt_kneel", "set": {"armR": PED_GRASP_R, "armL": PED_GRASP_L, "Chest": [14, 0, 0], "Head": [12, 0, 0]},
             "ease": "soft"},
            {"f": 37, "pose": "kt_kneel", "set": {"armR": PED_GRASP_R, "armL": PED_GRASP_L, "Chest": [14, 0, 0], "Head": [12, 0, 0]}},
            {"f": 46, "pose": "kt_kneel", "set": {"armR": PED_LIFT_R, "armL": PED_LIFT_L, "Chest": [8, 0, 0], "Head": [4, 0, 0],
                                                  "pelvis": [0, 0.03, -0.40]}, "ease": "smooth"},
            {"f": 64, "set": {"propR": "stand", "armR": CHEST_R, "armL": CHEST_L, "pelvis": [0, 0, 0.004], "weight": 0.5, "Chest": [1, 0, 0],
                              "Spine": [0, 0, 0], "Head": [8, 0, 0], "Neck": [2, 0, 0], "footR": {"pitch": 0}, "eyes": [0, 4]},
             "ease": "smooth"},
            {"f": 76, "set": {"armR": SHOW_R, "armL": SHOW_L, "Chest": [-3, 0, 0], "Head": [-5, 0, 0], "Neck": [-1, 0, 0],
                              "eyes": [0, -3], "shoulderL": [4, 0], "shoulderR": [4, 0]}, "ease": "smooth"},
            {"f": 86, "set": {"armR": CHEST_R, "armL": CHEST_L, "Chest": [0, 0, 0], "Head": [4, 0, 0], "eyes": [0, 2],
                              "shoulderL": [0, 0], "shoulderR": [0, 0]}, "ease": "smooth"},
            {"f": 96, "set": {"armR": CHEST_R, "armL": CHEST_L, "Chest": [0, 0, 0], "Head": [3, 0, 0], "eyes": [0, 2]}},
        ],
        "steps": [{"foot": "R", "f0": 12, "f1": 20, "to": [0.0, -0.52], "lift": 0.04, "shift": 5, "w_support": 0.2, "settle": 4, "land": 12.0},
                  {"foot": "R", "f0": 52, "f1": 61, "to": [0.0, 0.0], "lift": 0.05, "shift": 5, "w_support": 0.15, "settle": 5}],
        "layers": {"noise": 0.3},
    },
    # 1.6 s. A step forward and across into a low, bladed guard: forearm up in front, weapon hand low behind, eyes over
    # the forearm. The catch after the step dips and comes back. Hold frames 20 to 48.
    "Cover": {
        "frames": 48, "hold": [20, 48],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 3, "set": {"eyes": [8, 0], "Head": [0, 10, 0], "pelvis": [0, -0.01, -0.02]}, "only": ["head", "pelvis"]},
            {"f": 12, "pose": "cover", "set": {"pelvis": [0, 0.02, -0.15], "Chest": [9, -6, 0]}, "ease": "decel"},
            {"f": 20, "pose": "cover", "ease": "soft"},
            {"f": 32, "pose": "cover", "set": {"Head": [-4, 14, 0], "pelvis": [0, 0.0, -0.09], "eyes": [-6, 0]}, "ease": "smooth"},
            {"f": 48, "pose": "cover"},
        ],
        "steps": [{"foot": "L", "f0": 4, "f1": 11, "to": [-0.05, 0.30], "yaw": 6, "lift": 0.06, "shift": 3, "w_support": 0.15,
                   "settle": 3, "land": 0.0},
                  {"foot": "R", "f0": 13, "f1": 18, "to": [0.03, -0.05], "yaw": -24, "lift": 0.03, "shift": 2, "w_support": 0.2,
                   "settle": 4, "land": 0.0, "event": False}],
        "offsets": {"armL": -1, "armR": 2},
        "layers": {"noise": 0.5},
    },
    # 2.2 s. Struck: the chest gives and the head goes back, the knees fold, a moment upright on the knees, then the body
    # tips forward, lands (frame 46), rebounds a little and lies still. Hold frames 54 to 66.
    "Fall": {
        "frames": 66, "hold": [54, 66],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 3, "set": {"Chest": [-16, 0, 0], "Spine": [-6, 0, 0], "Head": [8, 0, 0], "head_level": 0.1, "pelvis": [0, -0.04, -0.01],
                             "armL+": {"UpperArm": [-22, 0, -14], "LowerArm": [-16, 0, 0]},
                             "armR+": {"UpperArm": [-20, 0, 16], "LowerArm": [-18, 0, 0]}}, "ease": "snap"},
            {"f": 7, "set": {"Head": [-12, 0, 0]}, "only": ["head"], "ease": "soft"},
            {"f": 14, "set": {"pelvis": [0, 0.18, -0.20], "footL": {"pitch": 34}, "footR": {"pitch": 32}, "Chest": [2, 0, 0],
                              "Spine": [2, 0, 0], "Head": [10, 0, 2], "head_level": 0.3,
                              "armL+": {"UpperArm": [0, 0, -8], "LowerArm": [-10, 0, 0]},
                              "armR+": {"UpperArm": [0, 0, 8], "LowerArm": [-10, 0, 0]}}, "ease": "accel"},
            {"f": 21, "pose": "kneel2", "set": {"pelvis": [0, 0.50, -0.46], "Chest": [14, 0, 0], "armL": "hang", "armR": "hang"}, "ease": "accel"},
            {"f": 27, "pose": "kneel2", "set": {"Chest": [3, 0, 0], "Head": [12, 0, 4]}, "ease": "soft"},
            {"f": 34, "pose": "kneel2", "set": {"pelvis": [0, 0.52, -0.46], "Hips": [10, 0, 0], "Chest": [8, 0, 0], "Head": [18, 0, 5]},
             "ease": "smooth"},
            {"f": 46, "pose": "prone", "set": {"pelvis": [0, 0.98, -0.79]}, "ease": "accel"},
            {"f": 49, "pose": "prone", "set": {"pelvis": [0, 0.985, -0.755], "Hips": [80, 0, 0], "Head": [-10, 40, 0]}, "ease": "soft"},
            {"f": 54, "pose": "prone", "ease": "soft"},
            {"f": 66, "pose": "prone"},
        ],
        "offsets": {"head": 1, "armL": 1, "armR": 2},
        "layers": {"breath": 0, "noise": 0},
    },
    # 2 s. A look, the hand comes up past the chest and out level toward a place ahead and to the right, a small
    # second push, a look back at the listener while the arm holds (hold frames 18 to 42), release.
    "Point": {
        "frames": 60, "hold": [18, 42],
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 3, "set": {"eyes": [-10, 0], "Head": [0, -8, 0]}, "only": ["head"]},
            {"f": 8, "set": {"armR": POINT_UP, "shoulderR": [4, 0], "Chest": [-1, 3, 0], "pelvis": [0, -0.006, 0.004],
                             "Head": [-1, -12, 0], "Neck": [0, -4, 0]}, "ease": "soft"},
            {"f": 16, "set": {"armR": POINT_FAR_JAB, "shoulderR": [3, 10], "Chest": [3, -10, 0], "Spine": [1, -4, 0], "Hips": [0, -3, 0],
                              "pelvis": [0, 0.03, -0.012], "weight": 0.36, "Head": [-2, -14, 0], "Neck": [0, -5, 0], "eyes": [-6, 0]},
             "ease": "decel"},
            {"f": 20, "set": {"armR": POINT_FAR, "shoulderR": [2, 9], "Chest": [2.5, -9, 0], "pelvis": [0, 0.026, -0.01]}, "ease": "soft"},
            {"f": 27, "set": {"armR": POINT_FAR_JAB}, "only": ["armR"], "ease": "accel"},
            {"f": 31, "set": {"armR": POINT_FAR, "eyes": [4, 0]}, "only": ["armR", "eyes"], "ease": "soft"},
            {"f": 37, "set": {"Head": [1, 4, 2], "Neck": [0, 1, 0], "eyes": [0, 0]}, "only": ["head"], "ease": "soft"},
            {"f": 42, "set": {"armR": POINT_FAR, "shoulderR": [2, 9], "Chest": [2.5, -9, 0], "Spine": [1, -4, 0], "Hips": [0, -3, 0],
                              "pelvis": [0, 0.026, -0.01], "weight": 0.36, "Head": [1, 4, 2], "Neck": [0, 1, 0]}},
            {"f": 51, "set": {"armR": POINT_UP, "shoulderR": [2, 0], "Chest": [0, -1, 0], "Spine": [0, 0, 0], "Hips": [0, 0, 0],
                              "pelvis": [0, 0, 0.003], "weight": 0.49, "Head": [-1, 0, 0]}, "ease": "smooth"},
            {"f": 60, "pose": "stand"},
        ],
        "layers": {"noise": 0.4},
    },
}
