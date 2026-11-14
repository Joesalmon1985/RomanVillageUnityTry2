"""Social clips (WP-5): greet, nod, shake, talk, listen.

Body and head only. The mouth is not keyed: talking drives the MouthOpen shape key at runtime and the jaw stays
shut here. Head turns are kept moderate so the runtime gaze layer (eyes first, head 40 percent, chest 15 percent)
has room to add its own look on top. Eyes in these clips are small leads only.
"""

# Raised open hand beside the head: wrist a little outside and in front of the shoulder, at chin height.
GREET_UP = {"reach": [-0.58, 0.46, 0.40], "elbow": [-0.70, -0.25, -0.65], "palm": "fwd"}
GREET_TOP = {"reach": [-0.62, 0.42, 0.50], "elbow": [-0.70, -0.25, -0.65], "palm": "fwd", "hand": [0, 0, -8]}
GREET_WAVE = {"reach": [-0.54, 0.46, 0.42], "elbow": [-0.70, -0.25, -0.65], "palm": "fwd", "hand": [0, 0, 12]}
# On the way up and down the hand passes in front of the chest with the elbow bent (not out through a T-pose).
GREET_MID = {"reach": [-0.30, 0.56, -0.22], "elbow": [-0.75, -0.45, -0.5], "palm": "left"}
# Conversational hand: forearm forward at waist height, palm turned up and in.
TALK_R = {"reach": [-0.16, 0.62, -0.50], "elbow": [-0.25, -0.6, -0.75], "palm": [0.5, 0, 0.85]}
TALK_R_HI = {"reach": [-0.20, 0.70, -0.30], "elbow": [-0.25, -0.6, -0.75], "palm": [0.4, 0.2, 0.9]}
TALK_R_OUT = {"reach": [-0.38, 0.58, -0.42], "elbow": [-0.35, -0.6, -0.7], "palm": [0.2, 0.3, 0.9]}
TALK_L = {"reach": [0.18, 0.55, -0.56], "elbow": [0.25, -0.6, -0.75], "palm": [-0.5, 0, 0.85]}
TALK_L_OUT = {"reach": [0.36, 0.52, -0.46], "elbow": [0.35, -0.6, -0.7], "palm": [-0.2, 0.3, 0.9]}
CHOP_UP = {"reach": [-0.20, 0.55, -0.12], "elbow": [-0.7, -0.4, -0.6], "palm": "left"}
CHOP_DN = {"reach": [-0.16, 0.74, -0.52], "elbow": [-0.6, -0.5, -0.6], "palm": "left"}

CLIPS = {
    # 2 s. Chin lifts as the eyes meet the other person, the arm dips before it rises (anticipation), palm up
    # beside the head, a nod while the hand holds, then the elbow leads the hand down and the arm swings past and settles.
    "Greet": {
        "frames": 60,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 4, "set": {"Head": [-3, 0, 0], "eyes": [0, -2], "weight": 0.56}, "only": ["head", "weight"]},
            {"f": 7, "set": {"armR+": {"UpperArm": [7, 0, -3], "LowerArm": [6, 0, 0]}, "shoulderR": [-3, -2],
                             "Chest": [1.5, -4, 1.5], "Spine": [0.5, -1.5, 0], "pelvis": [0, 0, -0.008]}},
            {"f": 12, "set": {"armR": GREET_MID, "shoulderR": [3, 2]}, "only": ["armR"], "ease": "soft"},
            {"f": 18, "set": {"armR": GREET_TOP, "shoulderR": [7, 0], "Chest": [-3, 3, -2], "Spine": [-1, 1, -1],
                              "pelvis": [0, 0.005, 0.006], "weight": 0.60, "Head": [-4, 0, -3]}, "ease": "decel"},
            {"f": 23, "set": {"armR": GREET_UP, "shoulderR": [5, 0], "Head": [-2, 0, -3]}, "ease": "soft"},
            {"f": 29, "set": {"armR": GREET_WAVE, "Head": [12, 0, -2], "Neck": [5, 0, 0], "Chest": [5, 3, -2], "Spine": [2, 1, -1],
                              "pelvis": [0, 0.012, -0.012], "eyes": [0, -4]}, "ease": "soft"},
            {"f": 36, "set": {"armR": GREET_UP, "shoulderR": [5, 0], "Head": [-2, 0, -2], "Neck": [0, 0, 0], "eyes": [0, 0],
                              "Chest": [-2, 3, -2], "Spine": [-1, 1, -1], "weight": 0.60, "pelvis": [0, 0.005, 0.004]},
             "ease": "soft"},
            {"f": 41, "set": {"armR": GREET_UP, "shoulderR": [4, 0], "Head": [-1, 0, -1]}},
            {"f": 47, "set": {"armR": GREET_MID, "shoulderR": [2, 1]}, "only": ["armR"], "ease": "soft"},
            {"f": 53, "set": {"armR+": {"UpperArm": [5, 0, 0], "LowerArm": [-6, 0, 0]}, "shoulderR": [-1, 0],
                              "Chest": [1, -1, 0], "Spine": [0, 0, 0], "pelvis": [0, 0, -0.005], "weight": 0.52,
                              "Head": [1, 0, 0]}, "ease": "smooth"},
            {"f": 60, "pose": "stand"},
        ],
        "offsets": {"head": -1},
        "layers": {"noise": 0.4},
    },
    # Additive. A small lift, the nod, and a slight overshoot back.
    "Nod": {
        "frames": 24,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 4, "set": {"Head": [-3.5, 0, 0], "Neck": [-1, 0, 0]}, "ease": "soft"},
            {"f": 10, "set": {"Head": [12, 0, 0], "Neck": [5, 0, 0], "Chest": [1.5, 0, 0]}, "ease": "soft"},
            {"f": 16, "set": {"Head": [-2, 0, 0], "Neck": [-0.5, 0, 0], "Chest": [0, 0, 0]}, "ease": "soft"},
            {"f": 24, "pose": "stand"},
        ],
        "layers": {"noise": 0, "breath": 0},
    },
    # Additive. Three swings, each smaller; the neck goes first, the head trails it.
    "ShakeHead": {
        "frames": 26,
        "keys": [
            {"f": 0, "pose": "stand"},
            {"f": 5, "set": {"Head": [1, 15, 1], "Neck": [0, 6, 0]}, "ease": "soft"},
            {"f": 11, "set": {"Head": [2, -14, -1], "Neck": [0, -6, 0]}, "ease": "soft"},
            {"f": 17, "set": {"Head": [1, 8, 0.5], "Neck": [0, 3, 0]}, "ease": "soft"},
            {"f": 26, "pose": "stand"},
        ],
        "offsets": {"Head": 1},
        "layers": {"noise": 0, "breath": 0},
    },
    # 3.2 s loop. One hand comes up and makes two small beats, the other joins lower, both drop.
    "Talk_A": {
        "frames": 96, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 16, "set": {"armR": TALK_R, "Head": [2, -4, 2], "Chest": [2, -3, 0], "weight": 0.44}, "ease": "decel"},
            {"f": 24, "set": {"armR": TALK_R_HI, "Head": [-2, -2, 1], "Chest": [0, -2, 0]}, "ease": "soft"},
            {"f": 30, "set": {"armR": TALK_R, "Head": [4, -1, 0], "Chest": [2.5, -1, 0]}, "ease": "accel"},
            {"f": 40, "set": {"armR": TALK_R_HI, "armL": TALK_L, "Head": [-1, 4, -2], "Chest": [0, 2, 0], "weight": 0.52},
             "ease": "soft"},
            {"f": 48, "set": {"armR": TALK_R_OUT, "armL": TALK_L_OUT, "Head": [3, 3, -2], "Chest": [2, 1, 0],
                              "shoulderL": [3, 0], "shoulderR": [3, 0]}, "ease": "soft"},
            {"f": 66, "set": {"armR": TALK_R_OUT, "armL": TALK_L_OUT, "Head": [0, 0, -1], "Chest": [0, 0, 0],
                              "shoulderL": [1, 0], "shoulderR": [1, 0], "weight": 0.56}},
            {"f": 84, "set": {"armR": "hang", "armL": "hang", "shoulderL": [0, 0], "shoulderR": [0, 0], "Head": [1, 0, 0]}},
            {"f": 96, "set": {"weight": 0.50}},
        ],
        "offsets": {"armL": 4, "head": -2},
        "layers": {"noise": 0.7},
    },
    # 3.6 s loop. The other hand leads, with a shrug in the middle.
    "Talk_B": {
        "frames": 108, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 18, "set": {"armL": TALK_L, "Head": [1, 5, -3], "Chest": [1, 3, 0], "weight": 0.58}, "ease": "decel"},
            {"f": 30, "set": {"armL": TALK_L_OUT, "Head": [-1, 2, -2], "Chest": [0, 2, 0]}, "ease": "soft"},
            {"f": 44, "set": {"armL": TALK_L_OUT, "armR": TALK_R_OUT, "shoulderL": [8, 0], "shoulderR": [8, 0],
                              "Head": [-3, 0, 4], "Chest": [-1.5, 0, 0], "pelvis": [0, 0, 0.004]}, "ease": "soft"},
            {"f": 56, "set": {"armL": TALK_L, "armR": TALK_R, "shoulderL": [0, 0], "shoulderR": [0, 0],
                              "Head": [3, -3, 1], "Chest": [1.5, -2, 0], "pelvis": [0, 0, -0.004], "weight": 0.46},
             "ease": "soft"},
            {"f": 74, "set": {"armL": "hang", "armR": TALK_R_HI, "Head": [0, -4, 0], "Chest": [0, -2, 0]}},
            {"f": 82, "set": {"armR": TALK_R, "Head": [4, -3, 0]}, "ease": "accel"},
            {"f": 98, "set": {"armR": "hang", "Head": [0, 0, 0], "Chest": [0, 0, 0]}},
            {"f": 108, "set": {"weight": 0.50}},
        ],
        "offsets": {"armR": 3, "head": -2},
        "layers": {"noise": 0.7},
    },
    # 3 s loop. Leans in, the hand comes down twice with a head accent on each, opens both hands, settles.
    "Talk_Emphasis": {
        "frames": 90, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 12, "set": {"armR": CHOP_UP, "Chest": [1, -3, 0], "Head": [-4, -2, 0], "pelvis": [0, 0.01, 0]}, "ease": "decel"},
            {"f": 18, "set": {"armR": CHOP_DN, "Chest": [6, -2, 0], "Spine": [2, 0, 0], "Head": [6, -1, 0], "pelvis": [0, 0.025, -0.012]},
             "ease": "accel"},
            {"f": 27, "set": {"armR": CHOP_UP, "Chest": [3, -2, 0], "Spine": [1, 0, 0], "Head": [-3, -1, 0], "pelvis": [0, 0.02, -0.004]},
             "ease": "soft"},
            {"f": 33, "set": {"armR": CHOP_DN, "Chest": [7, -1, 0], "Spine": [2.5, 0, 0], "Head": [7, 0, 0], "pelvis": [0, 0.03, -0.014]},
             "ease": "accel"},
            {"f": 48, "set": {"armR": TALK_R_OUT, "armL": TALK_L_OUT, "Chest": [-1, 0, 0], "Spine": [0, 0, 0],
                              "Head": [-3, 0, 2], "pelvis": [0, 0.005, 0.004], "shoulderL": [4, 0], "shoulderR": [4, 0]},
             "ease": "soft"},
            {"f": 66, "set": {"armR": TALK_R_OUT, "armL": TALK_L_OUT, "Head": [0, 0, 1], "shoulderL": [1, 0], "shoulderR": [1, 0]}},
            {"f": 82, "set": {"armR": "hang", "armL": "hang", "shoulderL": [0, 0], "shoulderR": [0, 0], "Chest": [0, 0, 0],
                              "Head": [1, 0, 0], "pelvis": [0, 0, 0]}},
            {"f": 90, "set": {"weight": 0.50}},
        ],
        "offsets": {"armL": 3, "head": 1},
        "layers": {"noise": 0.6},
    },
    # 4 s loop. Weight on one leg, head tilted toward the speaker, two small nods, a shift, one more nod.
    "Listen": {
        "frames": 120, "loop": True,
        "keys": [
            {"f": 0, "set": {"weight": 0.50}},
            {"f": 22, "set": {"weight": 0.34, "Head": [1, 0, 5], "Chest": [1, 0, 0]}, "ease": "decel"},
            {"f": 38, "set": {"weight": 0.35, "Head": [0, 0, 5]}},
            {"f": 43, "set": {"Head": [7, 0, 5], "Neck": [2, 0, 0]}, "ease": "soft", "only": ["head"]},
            {"f": 48, "set": {"Head": [0, 0, 5], "Neck": [0, 0, 0]}, "ease": "soft", "only": ["head"]},
            {"f": 53, "set": {"Head": [5, 0, 4], "Neck": [1.5, 0, 0]}, "ease": "soft", "only": ["head"]},
            {"f": 60, "set": {"Head": [0, 0, 4], "Neck": [0, 0, 0], "weight": 0.36}, "ease": "soft"},
            {"f": 84, "set": {"weight": 0.58, "Head": [-1, 2, -3], "Chest": [0, 1, 0]}},
            {"f": 98, "set": {"Head": [-1, 2, -3]}, "only": ["head"]},
            {"f": 103, "set": {"Head": [6, 2, -3], "Neck": [2, 0, 0]}, "ease": "soft", "only": ["head"]},
            {"f": 110, "set": {"Head": [0, 1, -1], "Neck": [0, 0, 0]}, "ease": "soft", "only": ["head"]},
            {"f": 120, "set": {"weight": 0.50}},
        ],
        "layers": {"noise": 0.8},
    },
}
