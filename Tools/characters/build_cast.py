"""Driver: build characters from the supplied FBX files (PRD_CHARACTERS 5.2; README 1 and 2.8).

  blender --background --factory-startup --disable-autoexec --python Tools/characters/build_cast.py -- \\
      --src <extracted zip folder> --out <output folder outside the repo> [--only prince,mira,champion]
      [--costume prehistoric,historic | all] [--steps rig,skin,face,costume,clips,verify,export]
      [--no-render] [--no-blend] [--no-anim]

Per character and per costume, always from the original FBX (idempotent): common.load_character, then the steps in
contract order: rig (WP-2), skin (WP-2), face (WP-3), costume (WP-4), clips (WP-5: the base clips and the character's
own <Clip>@<key> variants, baked on this character's real rig), verify (WP-2, including the clip, loop and
foot-plant checks of WP-5's anim_check), export (lib/export_unity.py: <key>@<costume>.fbx, mesh and rig, no clips).
Steps whose module is missing are skipped with a note unless asked for explicitly.

Outputs under --out (contract LAYOUT, v2):
  characters/<key>/<costume>/<key>@<costume>.fbx, <key>@<costume>.blend, manifest.json, reports/
  characters/<key>/manifest.json            the default (first) costume's manifest, plus every costume's summary
  anim/anim_<set>.fbx                       base clips (from the template character, Mira, who has every chain)
  anim/anim_<set>@<key>.fbx                 a character's style variants, baked on that character's rig
  anim/anim_manifest.json, cast_manifest.json (merged; the Unity import reads this one)

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
TEMPLATE = "mira"          # base animation files are exported from her build (she carries cape, hood and skirt chains)


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


def style_for(key):
    return [key] if os.path.isfile(os.path.join(_HERE, "clips", "style", key + ".json")) else False


def build_one(key, costume_name, args, steps, explicit):
    ctx = common.load_character(key, args.src, args.out)
    ctx.costume = costume_name
    notes = []
    results = []
    for step in steps:
        mod = _module(step)
        if mod is None:
            if explicit:
                raise RuntimeError("step %s asked for but lib/%s.py is not present" % (step, STEP_MODULES[step]))
            notes.append("skipped %s (lib/%s.py not present)" % (step, STEP_MODULES[step]))
            continue
        common.log(key, costume_name, "step", step)
        if step == "rig":
            mod.fit_rig(ctx)
        elif step == "skin":
            mod.skin_character(ctx)
        elif step == "face":
            mod.build_face(ctx)
        elif step == "costume":
            mod.apply_costume(ctx, costume_name)
        elif step == "clips":
            keep = {}
            bpy.context.scene.render.fps = contract.FPS
            mod.build_clips(ctx, style=style_for(key), keep=keep, costume=costume_name)
            ctx.baked_clips = keep
        elif step == "verify":
            results = mod.verify_character(ctx, render=not args.no_render)
        elif step == "export":
            mod.export_character(ctx)
            if "verify" in steps:
                # the forward-axis check of the written file joins the verify rows (verify runs before export)
                import verify as verify_mod
                rows = list(ctx.manifest.get("verify", []))
                verify_mod.check_export(ctx, rows)
                ctx.set_section("verify", rows)
                results = rows
    ctx.manifest.setdefault("identity", {})["build_notes"] = notes
    ctx.manifest["identity"]["costume_built"] = costume_name
    ctx.write_manifest()
    blend = ctx.path("character_blend")
    if not args.no_blend or ("export" in steps and not args.no_anim):
        common.ensure_dir(os.path.dirname(blend))
        bpy.ops.wm.save_as_mainfile(filepath=blend, copy=True, compress=False)
    anim = {}
    if "export" in steps and "clips" in steps and not args.no_anim and costume_name == contract.COSTUMES[0]:
        import export_unity
        adir = os.path.join(args.out, "anim")
        sets = list(contract.CLIP_SETS)
        if style_for(key):
            anim["variants"] = export_unity.export_anim_sets(blend, sets, adir, variant_key=key)
        if key == TEMPLATE:
            anim["base"] = export_unity.export_anim_sets(blend, sets, adir, variant_key=None)
            common.write_json(os.path.join(adir, "anim_manifest.json"), {
                "fps": contract.FPS, "template": key, "armature": "wp2", "costume": costume_name,
                "clips": [c for c in ctx.manifest.get("clips", []) if "@" not in c["name"]],
                "files": anim["base"]})
        if anim.get("variants"):
            common.write_json(os.path.join(adir, "anim_variants_%s.json" % key), {
                "key": key, "costume": costume_name,
                "clips": [c for c in ctx.manifest.get("clips", []) if "@" in c["name"]], "files": anim["variants"]})
    if args.no_blend and os.path.isfile(blend):
        os.remove(blend)
    return results, notes


def _summary_of(m):
    v = m.get("verify", [])
    ex = m.get("export", {})
    cs = m.get("costumes", {})
    applied = cs.get("applied")
    return {"verify_pass": bool(v) and all(r["ok"] for r in v), "verify_failed": [r["check"] for r in v if not r["ok"]],
            "fbx": ex.get("fbx"), "fbx_bytes": ex.get("bytes"), "tris_visible": ex.get("tris_visible"),
            "slots": cs.get("variants", {}).get(applied, {}).get("slots"),
            "overlays": cs.get("variants", {}).get(applied, {}).get("overlays"),
            "draw": cs.get("draw")}


def write_cast_manifest(out):
    """Merged index for the Unity import step: per character the shared sections (bones, sockets, skin, face,
    clips) from the default-costume build and a `builds` map with each costume's FBX, slots, overlays, verify."""
    chars = {}
    cdir = os.path.join(out, "characters")
    if os.path.isdir(cdir):
        for k in sorted(os.listdir(cdir)):
            builds = {}
            for c in contract.COSTUMES:
                p = os.path.join(cdir, k, c, "manifest.json")
                if os.path.isfile(p):
                    builds[c] = common.read_json(p)
            if not builds:
                continue
            first = builds.get(contract.COSTUMES[0]) or next(iter(builds.values()))
            entry = {s: first[s] for s in ("identity", "bones", "sockets", "skin", "face", "clips") if s in first}
            entry["costumes"] = first.get("costumes", {}).get("variants", {})
            entry["builds"] = {c: dict(_summary_of(m), verify=m.get("verify"), export=m.get("export"))
                               for c, m in builds.items()}
            chars[k] = entry
            common.write_json(os.path.join(cdir, k, "manifest.json"), dict(first, builds={
                c: _summary_of(m) for c, m in builds.items()}))
    data = {"contract_version": contract.CONTRACT_VERSION, "contract": "Tools/characters/contract.json",
            "costumes": contract.COSTUMES, "characters": chars}
    adir = os.path.join(out, "anim")
    ap = os.path.join(adir, "anim_manifest.json")
    if os.path.isfile(ap):
        data["anim"] = common.read_json(ap)
        data["anim"]["variants"] = {}
        for fn in sorted(os.listdir(adir)):
            if fn.startswith("anim_variants_") and fn.endswith(".json"):
                v = common.read_json(os.path.join(adir, fn))
                data["anim"]["variants"][v["key"]] = v
    common.write_json(os.path.join(out, contract.LAYOUT["cast_manifest"]), data)


def main():
    args = common.parse_args([
        ("--steps", {"default": "", "help": "comma separated subset of " + ",".join(ALL_STEPS)}),
        ("--costume", {"default": "prehistoric", "help": "comma separated, or 'all' (%s)" % ",".join(contract.COSTUMES)}),
        ("--no-render", {"action": "store_true", "help": "skip the bend-test renders in verify"}),
        ("--no-blend", {"action": "store_true", "help": "do not keep <key>@<costume>.blend"}),
        ("--no-anim", {"action": "store_true", "help": "do not export the animation files"}),
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
    costumes = list(contract.COSTUMES) if args.costume.strip() == "all" else \
        [c.strip() for c in args.costume.split(",") if c.strip()]
    bad = [c for c in costumes if c not in contract.COSTUMES]
    if bad:
        raise SystemExit("unknown costume(s) %s (known: %s)" % (bad, contract.COSTUMES))
    keys = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] == "humanoid"]
    skipped = [k for k in common.selected_keys(args.only) if contract.CAST_BY_KEY[k]["rig"] != "humanoid"]
    summary = {}
    for k in keys:
        for c in costumes:
            try:
                results, notes = build_one(k, c, args, steps, explicit)
                fails = [r for r in results if not r["ok"]]
                s = "PASS (%d checks)" % len(results) if not fails else "FAIL (%d): %s" % (len(fails), "; ".join(
                    "%s: %s" % (r["check"], r["detail"]) for r in fails[:6]))
                if "verify" not in steps:
                    s = "built (verify not run)"
                summary["%s/%s" % (k, c)] = s
            except Exception as e:  # noqa: BLE001
                traceback.print_exc()
                summary["%s/%s" % (k, c)] = "ERROR: %s" % e
    write_cast_manifest(args.out)
    for k in skipped:
        common.log(k, "skipped: demon rig is WP-13")
    for k, s in summary.items():
        common.log("RESULT", k, s)
    common.write_json(os.path.join(args.out, "build_summary.json"), summary)
    if any(not s.startswith(("PASS", "built")) for s in summary.values()):
        sys.exit(1)


main()
