"""WP-1: contract self-check. Builds the development stub armature for each character and checks it against
lib/contract.py (bone names, parents, bone count, mirroring, roll rule, sockets, optional chains).

  blender --background --factory-startup --disable-autoexec --python Tools/characters/check_contract.py -- \
      --src <extracted zip folder> --out <output folder> [--only prince,mira,champion] [--save-blend]

Exit code 1 if any check fails. With --save-blend the Prince/Mira stub scenes are saved under <out>/characters/<key>/stub.blend.
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import contract  # noqa: E402
import common  # noqa: E402
import islands  # noqa: E402
import stub_rig  # noqa: E402


def check(key, src, out, save):
    ctx = common.load_character(key, src, out)
    fails = []
    if ctx.islands is None:
        return ["no island map in cast/islands"]
    if not islands.check_map_matches(ctx.islands, ctx):
        fails.append("island map does not match the imported mesh (input changed?)")
    arm = stub_rig.build_stub_armature(ctx)
    bones = {b.name: b for b in arm.data.bones}
    imap = ctx.islands
    idx = {k: v for k, v in common.read_json(os.path.join(common.CAST_DIR, "index.json"))["characters"].items()}[key]
    expected = contract.all_human_bone_names(skirt=idx["optional_chains"]["skirt"], cape=idx["optional_chains"]["cape"],
                                             hood=idx["optional_chains"]["hood"])
    for n in expected:
        if n not in bones:
            fails.append("missing bone " + n)
    for n in bones:
        if n not in expected:
            fails.append("bone outside contract " + n)
    if len(bones) > contract.MAX_BONES_HERO:
        fails.append("too many bones %d" % len(bones))
    parents = {n: p for n, p in contract.HUMAN_CORE_BONES + contract.FACE_BONES + contract.CAPE_BONES + contract.HOOD_BONES
               + contract.SKIRT_BONES + contract.SOCKETS}
    prop = imap["landmarks"].get("prop")
    if prop:
        parents["Sock.StaffTip"] = "Sock.Hand." + prop["holding_hand"]
    for n, b in bones.items():
        want = parents[n]
        got = b.parent.name if b.parent else None
        if want != got:
            fails.append("parent of %s is %s, contract says %s" % (n, got, want))
        if b.length < 0.005:
            fails.append("bone %s is degenerate (length %.4f)" % (n, b.length))
        # roll rule: local X close to world +X
        xaxis = (arm.matrix_world.to_3x3() @ b.matrix_local.to_3x3()).col[0]
        yaxis = (arm.matrix_world.to_3x3() @ b.matrix_local.to_3x3()).col[1]
        if abs(yaxis.x) < 0.94 and xaxis.x < 0.5:   # bones that run along X (Shoulder, Sock.Hip.R) have no defined roll
            fails.append("roll rule: %s local X . worldX = %.2f" % (n, xaxis.x))
    # mirroring
    for n in list(bones):
        if n.endswith(".L") and n[:-2] + ".R" in bones and not (prop and n.startswith("Sock.Hand")):
            a, b = bones[n].head_local, bones[n[:-2] + ".R"].head_local
            if abs(a.x + b.x) > 0.02 or abs(a.y - b.y) > 0.02 or abs(a.z - b.z) > 0.02:
                fails.append("not mirrored: %s head %s vs %s" % (n, tuple(round(v, 3) for v in a), tuple(round(v, 3) for v in b)))
            if bones[n].head_local.x <= 0 and not n.startswith("Sock.Hip"):
                fails.append(".L bone on the wrong side: " + n)
    # the Hips are the root; everything hangs from it
    roots = [n for n, b in bones.items() if b.parent is None]
    if roots != ["Hips"]:
        fails.append("roots: %s" % roots)
    # facing: head centre is in front of (more -Y than) torso centre
    fc = imap["face"].get("forward_check", {})
    if not fc.get("faces_minus_y"):
        fails.append("character does not face -Y")
    print("[check]", key, "bones", len(bones), "ok" if not fails else "FAIL")
    if save and key in ("prince", "mira"):
        p = ctx.path("character_dir") + "stub.blend"
        common.ensure_dir(os.path.dirname(p))
        bpy.ops.wm.save_as_mainfile(filepath=p, copy=True)
    return fails


def main():
    args = common.parse_args([("--save-blend", {"action": "store_true"})], description=__doc__)
    if not args.src or not args.out:
        raise SystemExit("--src and --out are required")
    keys = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] == "humanoid"]
    bad = {}
    for k in keys:
        f = check(k, os.path.abspath(args.src), os.path.abspath(args.out), args.save_blend)
        if f:
            bad[k] = f
    for k, f in bad.items():
        for m in f:
            print("[check] %s: %s" % (k, m))
    if bad:
        sys.exit(1)
    print("[check] all %d humanoids pass the contract self-check" % len(keys))


main()
