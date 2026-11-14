"""WP-2 driver: build characters from the supplied FBX files (PRD_CHARACTERS 5.2).

  blender --background --factory-startup --disable-autoexec --python Tools/characters/build_cast.py -- \
      --src <extracted zip folder> --out <output folder outside the repo> [--only prince,mira,champion]
      [--steps rig,skin,face,costume,clips,verify,export] [--costume prehistoric] [--no-render] [--no-blend]

Per character, always from the original FBX (idempotent): common.load_character, then the steps in contract
order. Steps owned by other packages are imported lazily: `face` (lib/face.py, WP-3), `costume`
(lib/costume.py, WP-4), `clips` (lib/anim_core.py, WP-5), `export` (lib/export_unity.py, WP-6). When --steps is
not given, a step whose module is not present yet is skipped with a note; when it is asked for explicitly and
missing, the run fails.

Outputs (contract LAYOUT): <out>/characters/<key>/<key>.blend, manifest.json, reports/verify.json and the bend-test
renders in reports/renders/, and <out>/cast_manifest.json (merged index of every character manifest found under
<out>/characters plus <out>/anim/anim_manifest.json if present).

Exit code 1 if any character fails a verify check or a step raises.
"""
import importlib
import os
import sys
import traceback

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import contract  # noqa: E402
import common  # noqa: E402

ALL_STEPS = ["rig", "skin", "face", "costume", "clips", "verify", "export"]
STEP_MODULES = {"rig": "rig", "skin": "skin", "face": "face", "costume": "costume", "clips": "anim_core",
                "verify": "verify", "export": "export_unity"}


def _module(step):
    name = STEP_MODULES[step]
    if not os.path.isfile(os.path.join(_HERE, "lib", name + ".py")):
        return None
    return importlib.import_module(name)


def _repo_root():
    return os.path.dirname(os.path.dirname(_HERE))


def _inside(path, root):
    path, root = os.path.normcase(os.path.abspath(path)), os.path.normcase(os.path.abspath(root))
    return path == root or path.startswith(root + os.sep)


def build_one(key, args, steps, explicit):
    ctx = common.load_character(key, args.src, args.out)
    notes = []
    results = []
    for step in steps:
        mod = _module(step)
        if mod is None:
            if explicit:
                raise RuntimeError("step %s asked for but lib/%s.py is not present" % (step, STEP_MODULES[step]))
            notes.append("skipped %s (lib/%s.py not present)" % (step, STEP_MODULES[step]))
            continue
        common.log(key, "step", step)
        if step == "rig":
            mod.fit_rig(ctx)
        elif step == "skin":
            mod.skin_character(ctx)
        elif step == "face":
            mod.build_face(ctx)
        elif step == "costume":
            mod.apply_costume(ctx, args.costume)
        elif step == "clips":
            mod.build_clips(ctx)
        elif step == "verify":
            results = mod.verify_character(ctx, render=not args.no_render)
        elif step == "export":
            mod.export_character(ctx)
    ctx.manifest.setdefault("identity", {})["build_notes"] = notes
    ctx.write_manifest()
    if not args.no_blend:
        p = ctx.path("character_blend")
        common.ensure_dir(os.path.dirname(p))
        bpy.ops.wm.save_as_mainfile(filepath=p, copy=True, compress=False)
    return results, notes


def write_cast_manifest(out):
    chars = {}
    cdir = os.path.join(out, "characters")
    if os.path.isdir(cdir):
        for k in sorted(os.listdir(cdir)):
            p = os.path.join(cdir, k, "manifest.json")
            if os.path.isfile(p):
                chars[k] = common.read_json(p)
    data = {"contract_version": contract.CONTRACT_VERSION, "characters": chars}
    ap = os.path.join(out, *contract.LAYOUT["anim_manifest"].split("/"))
    if os.path.isfile(ap):
        data["anim"] = common.read_json(ap)
    common.write_json(os.path.join(out, contract.LAYOUT["cast_manifest"]), data)


def main():
    args = common.parse_args([
        ("--steps", {"default": "", "help": "comma separated subset of " + ",".join(ALL_STEPS)}),
        ("--costume", {"default": "prehistoric"}),
        ("--no-render", {"action": "store_true", "help": "skip the bend-test renders in verify"}),
        ("--no-blend", {"action": "store_true", "help": "do not save <key>.blend"}),
    ], description=__doc__)
    if not args.src or not args.out:
        raise SystemExit("--src and --out are required")
    args.src, args.out = os.path.abspath(args.src), os.path.abspath(args.out)
    if _inside(args.out, _repo_root()):
        raise SystemExit("--out must be outside the repository (%s)" % _repo_root())
    explicit = bool(args.steps.strip())
    steps = [s.strip() for s in args.steps.split(",") if s.strip()] if explicit else list(ALL_STEPS)
    unknown = [s for s in steps if s not in ALL_STEPS]
    if unknown:
        raise SystemExit("unknown step(s) %s" % unknown)
    steps = [s for s in ALL_STEPS if s in steps]      # contract order
    keys = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] == "humanoid"]
    skipped = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] != "humanoid"]
    summary = {}
    for k in keys:
        try:
            results, notes = build_one(k, args, steps, explicit)
            fails = [r for r in results if not r["ok"]]
            summary[k] = "PASS" if not fails else "FAIL (%d): %s" % (len(fails), "; ".join(
                "%s: %s" % (r["check"], r["detail"]) for r in fails[:6]))
            if "verify" not in steps:
                summary[k] = "built (verify not run)"
        except Exception as e:  # noqa: BLE001
            traceback.print_exc()
            summary[k] = "ERROR: %s" % e
    write_cast_manifest(args.out)
    for k in skipped:
        common.log(k, "skipped: demon rig is WP-13")
    for k, s in summary.items():
        common.log("RESULT", k, s)
    if any(not s.startswith(("PASS", "built")) for s in summary.values()):
        sys.exit(1)


main()
