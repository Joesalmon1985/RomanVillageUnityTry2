"""Copy the built character art into the Unity project (WP-6).

Usage (plain Python, no Blender; run from anywhere):

    python -I Tools/characters/sync_unity_local.py --src <build out folder> [--project <unity project>] [--only k1,k2] [--dry-run]

`--src` is the `build_cast.py --out` folder (on this machine
`WorldMVP\\NEWSTUFF\\characters_work\\integrated`). It copies:

| From | To | Git |
|---|---|---|
| `characters/<key>/<costume>/<key>@<costume>.fbx` | `Assets/LocalBaked/Characters/Models/` | ignored (meshes, rights not confirmed) |
| `cast_manifest.json` | `Assets/LocalBaked/Characters/cast_manifest.json` | ignored |
| `anim/anim_<set>.fbx`, `anim/anim_<set>@<key>.fbx`, `anim_manifest.json`, `anim_variants_<key>.json` | `Assets/Characters/Animation/` | tracked (skeleton and clips, no mesh) |

Before copying anything it asks git whether the local folder is ignored (`git check-ignore`) and refuses if it
is not, and it refuses any animation file that contains a mesh (FBX `Geometry` node). Files whose bytes are
unchanged are not rewritten, so Unity does not re-import them.
"""
import argparse
import filecmp
import json
import os
import shutil
import subprocess
import sys

LOCAL_REL = os.path.join("Assets", "LocalBaked", "Characters")
ANIM_REL = os.path.join("Assets", "Characters", "Animation")


def die(msg):
    print("sync: ERROR " + msg)
    sys.exit(1)


def git_ignored(project, rel_path):
    r = subprocess.run(["git", "-C", project, "check-ignore", "-q", rel_path.replace("\\", "/")])
    return r.returncode == 0


def fbx_has_mesh(path):
    """True if a binary or ASCII FBX holds mesh geometry (a Geometry node of type Mesh)."""
    with open(path, "rb") as f:
        data = f.read()
    if data.startswith(b"Kaydara FBX Binary"):
        # Binary FBX stores object names as "<name>\x00\x01<class>"; a mesh has class Geometry.
        return b"\x00\x01Geometry" in data
    return b"Geometry::" in data


def copy(src, dst, dry, stats):
    if os.path.exists(dst) and filecmp.cmp(src, dst, shallow=False):
        stats["same"] += 1
        return
    stats["copied"] += 1
    print("  copy " + os.path.relpath(src) + " -> " + dst)
    if not dry:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--project", default=os.path.normpath(os.path.join(here, "..", "..")))
    ap.add_argument("--only", default="")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    src = os.path.abspath(a.src)
    project = os.path.abspath(a.project)
    manifest_path = os.path.join(src, "cast_manifest.json")
    if not os.path.isfile(manifest_path):
        die("no cast_manifest.json in " + src)
    if os.path.commonpath([src, project]) == project:
        die("--src must be outside the project")

    # Gate: the local folder must be ignored by git before anything lands in it.
    probe = os.path.join(LOCAL_REL, "Models", "probe@prehistoric.fbx")
    if not git_ignored(project, probe) or not git_ignored(project, os.path.join(LOCAL_REL, "cast_manifest.json")):
        die(LOCAL_REL + " is not git-ignored in " + project + "; refusing to copy meshes")

    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)
    only = [k for k in a.only.split(",") if k]
    stats = {"copied": 0, "same": 0}

    local = os.path.join(project, LOCAL_REL)
    copy(manifest_path, os.path.join(local, "cast_manifest.json"), a.dry_run, stats)
    n_models = 0
    for key, ch in sorted(manifest.get("characters", {}).items()):
        if only and key not in only:
            continue
        for costume, build in sorted(ch.get("builds", {}).items()):
            rel = build.get("fbx")
            if not rel:
                continue
            fbx = os.path.join(src, rel)
            if not os.path.isfile(fbx):
                print("  missing " + fbx)
                continue
            copy(fbx, os.path.join(local, "Models", os.path.basename(fbx)), a.dry_run, stats)
            n_models += 1

    anim_src = os.path.join(src, "anim")
    anim_dst = os.path.join(project, ANIM_REL)
    n_anim = 0
    for name in sorted(os.listdir(anim_src)):
        p = os.path.join(anim_src, name)
        if name.endswith(".fbx"):
            if fbx_has_mesh(p):
                die(name + " contains a mesh; animation files must be skeleton and clips only")
            copy(p, os.path.join(anim_dst, name), a.dry_run, stats)
            n_anim += 1
        elif name == "anim_manifest.json" or (name.startswith("anim_variants_") and name.endswith(".json")):
            copy(p, os.path.join(anim_dst, name), a.dry_run, stats)

    print("sync: %d character models, %d animation files; %d copied, %d unchanged%s" % (
        n_models, n_anim, stats["copied"], stats["same"], " (dry run)" if a.dry_run else ""))


if __name__ == "__main__":
    main()
