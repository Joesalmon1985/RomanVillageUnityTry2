"""Clip data format and shared poses (WP-5). The generator that reads this is lib/anim_core.py.

Files: every clips/*.py may define CLIPS (clip name -> definition) and POSES (pose name -> pose). Clip names must be
in contract.CLIP_SETS. clips/style/<key>.json holds a character's style (stand pose, amplitude, tempo, stride,
springs, gait overrides, prop handling); clips/style/base.json is the template every style overrides.

A pose lists only what differs from the character's stand pose:
  Hips, Spine, Chest, Neck, Head   Euler degrees ADDED to the stand pose: [pitch forward, turn left, lean right]
  pelvis                           metres ADDED to the stand pose: [left, forward, up]
  weight                           0 = all on the right foot, 1 = all on the left (the pelvis and hips follow)
  armL / armR                      "hang", or {"reach": [left, forward, up] in arm lengths from the shoulder} or
                                   {"at": [left, forward, up] metres in root space}; optional "elbow" (direction the
                                   elbow points), "palm" ("fwd", "back", "up", "down", "left", "right" or a direction),
                                   "hand" (extra wrist Euler), "space": "chest" (default) or "root"
  armL+ / armR+                    small Euler offsets per bone on the hanging arm: {"UpperArm": [x, y, z], ...}
  footL / footR                    {"pos": [left, forward, up], "yaw", "pitch" (+ heel up, - toes up), "roll"};
                                   a foot never slides between keys: positions change only through "steps"
  propL / propR                    {"aim": direction of the prop's head, "lock": 0..1} or "stand"
  pinL / pinR                      {"pos": [left, forward, up] offset of the hand from its rest place, held in root
                                   space while the body moves}, "stand" or "off"
  eyes                             [look left, look down] degrees; jaw: degrees open (kept at 4 or less)
  shoulderL / shoulderR            [raise, forward] degrees; head_level: 0..1 (1 = head ignores the torso's lean)
  base                             name of a pose to start from. A pose name ending in "~" is its left/right mirror.

A clip definition:
  frames        length at 30 fps (a style's tempo scales it unless "tempo": False)
  keys          [{"f": frame, "pose": name or [names], "set": {inline pose}, "ease": name, "only"/"skip": [groups]}]
                eases describe the arrival: smooth, soft, linear, accel (into a hit), decel (settle in), snap,
                overshoot, hold. Groups: torso, head, armL, armR, legs, or a channel name.
                Frame 0 and the last frame key every channel (anything not mentioned is the stand pose).
  offsets       {"head": -2, "armR": 3}: shift a group's keys by frames (overlap by hand)
  steps         [{"foot": "L", "f0": lift, "f1": plant, "to": [left, forward], "yaw", "lift", "shift": frames the weight
                moves off the foot before it lifts, "w_support": share left on the stepping foot, "settle", "land"}]
  events        [{"name": "Release", "f": frame}]; steps and gaits add FootL / FootR themselves
  hold          [from, to]: frames the game may hold or loop (written to the clip record as hold_frames)
  layers        per clip scale of the generator layers: breath, noise, overlap, drag, hip_drop, shift_lat, shift_fwd
  springs       per clip spring overrides {"Head": [omega, damping]}
  type          "gait", "start", "stop", "turn" (lib/anim_gait.py) or keyed (default); "mirror_of": clip name
  variants      {"mira": {...}}: merged over the definition for <Clip>@mira; with "replace": True it stands alone
  only_for      [cast keys]: no <Clip>@<key> for anyone else

To add or change a clip: edit the data here, add a new name to contract.CLIP_SETS (a contract change), then
  blender ... --python Tools/characters/build_clips.py -- --out <work folder>          (bake, check, export)
  blender ... --python Tools/characters/preview_clips.py -- --src <zip folder> --out <work folder> --only prince --clips <Name>
and read <out>/anim/reports/summary.txt and the strip image before trusting it.
"""

POSES = {
    "stand": {},
}
