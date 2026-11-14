"""WP-5 driver: bake the clip sets, check them and write the animation files.

  blender --background --factory-startup --disable-autoexec --python Tools/characters/build_clips.py -- \
      --out <work folder outside the repo> [--template mira] [--sets idle,locomotion,...] [--style all|none|k1,k2]
      [--src <extracted zip folder> --rig-lib <folder with WP-2's rig.py>] [--no-fbx] [--strict]

Without --src the clips are baked on the development stub armature of the template character, built from the
committed island map only (no mesh is read). With --src and --rig-lib they are baked on WP-2's fitted rig.
The template is Mira by default because her armature carries every optional chain (cape, hood, skirt), so the cape,
hood and skirt curves exist in the files; characters without those bones ignore them.

Writes, under --out:
  anim/anim_<set>.fbx          armature and actions only, no mesh (contract 2.6); base clips and <Clip>@<key> variants
  anim/anim_manifest.json      clip records: name, set, length_s, frames, fps, loop, additive, native_speed_mps, events
                               plus optional fields: base, style, travel_m, exit_phase / enter_phase, gait, turn_deg,
                               turn_frames, hold_frames
  anim/reports/foot_report.json, loop_report.json, summary.txt
Exit code 1 with --strict if a loop or foot check fails.
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
import stub_rig  # noqa: E402
import anim_core  # noqa: E402
import anim_check  # noqa: E402


def make_armature(key, src, out, rig_lib):
    """Returns (ctx, armature, kind)."""
    if src and rig_lib:
        sys.path.insert(0, rig_lib)
        try:
            import rig as wp2_rig
            ctx = common.load_character(key, src, out)
            wp2_rig.fit_rig(ctx)
            return ctx, ctx.armature, "wp2"
        except Exception as e:      # noqa: BLE001
            common.log("WP-2 rig not usable (%s); using the stub" % e)
    common.reset_scene()
    ctx = common.BuildContext(key, src, out)
    ctx.root = bpy.data.objects.new(key, None)
    bpy.context.scene.collection.objects.link(ctx.root)
    arm = stub_rig.build_stub_armature(ctx)
    return ctx, arm, "stub"


def export_anim_fbx(arm, path):
    """Fallback exporter with the contract's settings (WP-6's export_unity.export_anim_sets replaces it)."""
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    common.ensure_dir(os.path.dirname(path))
    # the exporter only bakes "all actions" for objects that already have animation data
    acts = sorted(bpy.data.actions, key=lambda a: a.name)
    if acts:
        anim_core.assign_action(arm, acts[0])
    scn = bpy.context.scene
    scn.frame_start = 0
    scn.frame_end = int(max(a.frame_end for a in acts)) if acts else 1
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
        axis_forward=contract.FBX_AXIS_FORWARD, axis_up=contract.FBX_AXIS_UP, bake_space_transform=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        use_armature_deform_only=False)


def main():
    args = common.parse_args([
        ("--template", {"default": "mira"}), ("--sets", {"default": ""}), ("--style", {"default": "all"}),
        ("--rig-lib", {"default": ""}), ("--no-fbx", {"action": "store_true"}), ("--strict", {"action": "store_true"}),
    ], description="WP-5 clip build")
    if not args.out:
        raise SystemExit("--out is required")
    repo = os.path.abspath(os.path.join(_HERE, "..", ".."))
    if os.path.abspath(args.out).startswith(repo + os.sep):
        raise SystemExit("--out must be outside the repository")
    sets = [s for s in args.sets.split(",") if s] or list(contract.CLIP_SETS)
    style = True if args.style == "all" else (False if args.style == "none" else args.style.split(","))
    anim_dir = common.ensure_dir(os.path.join(args.out, "anim"))
    rep_dir = common.ensure_dir(os.path.join(anim_dir, "reports"))
    # pass 1: every set on one armature, so the checks can compare clips of different sets
    ctx, arm, kind = make_armature(args.template, args.src, args.out, args.rig_lib)
    bpy.context.scene.render.fps = contract.FPS
    keep = {}
    records = anim_core.build_clips(arm, set_names=sets, style=style, keep=keep)
    rep = anim_check.check_clips(arm, keep)
    foot, loops, chains = rep["foot"], rep["loop"], rep["chain"]
    # pass 2: one file per set (a fresh armature per set, so a file holds only its own actions)
    if not args.no_fbx:
        for set_name in sets:
            ctx, arm, kind = make_armature(args.template, args.src, args.out, args.rig_lib)
            bpy.context.scene.render.fps = contract.FPS
            anim_core.build_clips(arm, set_names=[set_name], style=style)
            try:
                import export_unity
                export_unity.export_anim_sets(ctx, [set_name])
            except ImportError:
                export_anim_fbx(arm, os.path.join(anim_dir, "%s%s.fbx" % (contract.ANIM_FBX_PREFIX, set_name)))
            common.log("exported set", set_name)
    manifest = {"fps": contract.FPS, "template": args.template, "armature": kind,
                "foot_speed_limit_cm_s": anim_check.FOOT_SPEED_LIMIT_CM_S, "clips": records}
    common.write_json(os.path.join(anim_dir, "anim_manifest.json"), manifest)
    common.write_json(os.path.join(rep_dir, "foot_report.json"), foot)
    common.write_json(os.path.join(rep_dir, "loop_report.json"), {"loops": loops, "chains": chains})
    lines = ["clip | frames | s | loop | add | native m/s | events | foot speed cm/s | drift mm | drift between keys mm | loop pos m / rot deg"]
    fr = {f["clip"]: f for f in foot}
    lr = {l["clip"]: l for l in loops}
    bad = []
    for r in records:
        f = fr[r["name"]]
        l = lr.get(r["name"])
        ev = " ".join("%s@%d" % (e["name"], e["frame"]) for e in r["events"]) or "-"
        lines.append("%s | %d | %.3f | %s | %s | %.2f | %s | %.3f | %.3f | %.3f | %s" % (
            r["name"], r["frames"], r["length_s"], "loop" if r["loop"] else "-", "add" if r["additive"] else "-",
            r["native_speed_mps"], ev, f["speed_cm_s"], f["drift_mm"], f["drift_halfframe_mm"],
            ("%.5f / %.3f" % (l["pos_m"], l["rot_deg"])) if l else "-"))
        if not f["ok"] or (l and not l["ok"]):
            bad.append(r["name"])
    lines.append("")
    lines.append("chains (last frame of a against first frame of b): pos m, rot deg")
    for c in chains:
        lines.append("%s -> %s | %.5f (%s) | %.3f (%s)" % (c["from"], c["to"], c["pos_m"], c["worst_pos"], c["rot_deg"], c["worst_rot"]))
    lines.append("")
    for nmb in bad:
        lines.append("FAIL %s: %s" % (nmb, fr[nmb]))
    lines.append("FAILED: " + (", ".join(bad) if bad else "none"))
    with open(os.path.join(rep_dir, "summary.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    if bad and args.strict:
        sys.exit(1)


if __name__ == "__main__":
    main()
