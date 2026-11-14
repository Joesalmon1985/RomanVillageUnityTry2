"""Shared helpers for every Blender script in Tools/characters (WP-1).

Run inside Blender 4.5 headless:
  blender --background --factory-startup --disable-autoexec --python <script> -- <args>

Rules:
- Paths are always arguments. Nothing is read from the extracted folder except geometry (FBX/.blend import).
- No script from the input folder is ever executed (--disable-autoexec, and .blend text blocks are never run).
- Output is deterministic: same inputs, same bytes (sorted keys, rounded floats, no timestamps in data).
"""

import json
import os
import re
import sys

try:
    import bpy  # noqa: F401
    HAVE_BPY = True
except ImportError:  # plain python (tests, contract export)
    bpy = None
    HAVE_BPY = False

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import contract  # noqa: E402

TOOLS_DIR = os.path.dirname(_HERE)                 # .../Tools/characters
CAST_DIR = os.path.join(TOOLS_DIR, "cast")
ISLANDS_REPO_DIR = os.path.join(CAST_DIR, "islands")


def log(*a):
    print("[chars]", *a, flush=True)


def script_args(argv=None):
    """Arguments after the first '--' (Blender passes everything before it to itself)."""
    argv = sys.argv if argv is None else argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def parse_args(spec, argv=None, description=""):
    """Tiny argparse wrapper. spec: list of (flag, kwargs). Always accepts --src, --out, --only."""
    import argparse
    p = argparse.ArgumentParser(description=description, prog=os.path.basename(sys.argv[0]) if sys.argv else "script")
    p.add_argument("--src", required=False, help="folder that holds Models/*.fbx and the .blend (the extracted zip folder)")
    p.add_argument("--out", required=False, help="output folder, outside the repository")
    p.add_argument("--only", default="", help="comma separated cast keys; empty = all")
    for flag, kw in spec:
        p.add_argument(flag, **kw)
    return p.parse_args(script_args(argv))


def selected_keys(only):
    keys = [k.strip().lower() for k in (only or "").split(",") if k.strip()]
    if not keys:
        return list(contract.CAST_KEYS)
    unknown = [k for k in keys if k not in contract.CAST_BY_KEY]
    if unknown:
        raise SystemExit("unknown cast key(s): %s (known: %s)" % (",".join(unknown), ",".join(contract.CAST_KEYS)))
    return keys


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)
    return path


def r(v, n=4):
    """Round a float for deterministic JSON. Normalises -0.0."""
    x = round(float(v), n)
    return 0.0 if x == 0 else x


def rv(v, n=4):
    return [r(c, n) for c in v]


def write_json(path, data):
    ensure_dir(os.path.dirname(path))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=1, sort_keys=True, ensure_ascii=True)
        f.write("\n")


def read_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def hex_from_material_name(name):
    """'RV_9C856A', 'RV_A05BE4_Ember.001' -> '9C856A'. None if the name is not an RV_ hex name."""
    m = re.match(r"^RV_([0-9A-Fa-f]{6})", name)
    return m.group(1).upper() if m else None


def base_material_name(name):
    """Strip a Blender duplicate suffix: 'RV_A05BE4_Ember.001' -> 'RV_A05BE4_Ember'."""
    return re.sub(r"\.\d{3}$", "", name)


# ----------------------------------------------------------------------------------------
# Scene handling
# ----------------------------------------------------------------------------------------
def reset_scene():
    """Empty factory scene (no default cube, camera, light)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def fbx_path(src, key):
    return os.path.join(src, "Models", contract.CAST_BY_KEY[key]["fbx"])


def blend_path(src):
    return os.path.join(src, "RomanVillage_DarkFantasy_Characters.blend")


def part_of(obj_name):
    """'02_Mira_Body' -> 'Body'. Returns None for the root empty."""
    last = obj_name.rsplit("_", 1)[-1]
    # Blender may add .001 suffixes on collision
    last = re.sub(r"\.\d{3}$", "", last)
    return last if last in contract.SOURCE_PART_SUFFIXES else None


class BuildContext:
    """What every pipeline step receives. One character per Blender scene.

    Attributes:
      key, info          cast key and its contract entry
      src, out           input folder and output folder (strings)
      root               root empty (named <key> after load_character)
      parts              {"Body": obj, "Legs": obj, "Head": obj, "Cloth": obj?, "Prop": obj?}  (contract part names)
      armature           armature object or None (created by rig.py)
      manifest           dict of sections (contract.MANIFEST_SECTIONS); steps set their own section only
      islands            geometry-free island map (dict) loaded from cast/islands/<key>.json, or None
    """

    def __init__(self, key, src, out):
        self.key = key
        self.info = contract.CAST_BY_KEY[key]
        self.src = src
        self.out = out
        self.root = None
        self.parts = {}
        self.armature = None
        self.manifest = {"identity": {"key": key, "number": self.info["number"], "display": self.info["display"],
                                      "rig": self.info["rig"], "source_fbx": self.info["fbx"],
                                      "contract_version": contract.CONTRACT_VERSION}}
        self.islands = load_islands(key)
        self.costume = None      # set by build_cast.py: outputs then go to the per-costume folder (contract v2)

    _COSTUME_PATHS = {"character_blend": "character_costume_blend", "character_fbx": "character_costume_fbx",
                      "character_manifest": "character_costume_manifest", "character_reports": "character_costume_reports"}

    def path(self, layout_key, **kw):
        if self.costume and layout_key in self._COSTUME_PATHS:
            layout_key = self._COSTUME_PATHS[layout_key]
            kw.setdefault("costume", self.costume)
        rel = contract.LAYOUT[layout_key].replace("<key>", self.key)
        for k, v in kw.items():
            rel = rel.replace("<%s>" % k, v)
        return os.path.join(self.out, *rel.split("/"))

    def set_section(self, name, data):
        if name not in contract.MANIFEST_SECTIONS:
            raise KeyError("unknown manifest section %r" % name)
        self.manifest[name] = data

    def write_manifest(self):
        write_json(self.path("character_manifest"), self.manifest)


def import_fbx(filepath):
    """Import one FBX with the supplied files' own axes (forward -Z, up Y => Blender -Y facing, Z up)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=filepath, use_anim=False)
    return [o for o in bpy.data.objects if o not in before]


def load_character(key, src, out, rename=True):
    """Reset the scene, import the supplied FBX, rename objects to the contract names.

    After this call: root empty is named <key>, parts are named Body/Legs/Head/Cloth/Prop, materials keep
    their RV_xxxxxx names (WP-4 remaps them to Role_* slots).
    """
    reset_scene()
    objs = import_fbx(fbx_path(src, key))
    ctx = BuildContext(key, src, out)
    roots = [o for o in objs if o.type == "EMPTY" and o.parent is None]
    if len(roots) != 1:
        raise RuntimeError("%s: expected one root empty, found %d" % (key, len(roots)))
    ctx.root = roots[0]
    for o in objs:
        if o.type == "MESH":
            p = part_of(o.name)
            if p is None:
                raise RuntimeError("%s: unexpected mesh object name %r" % (key, o.name))
            ctx.parts[p] = o
    if rename:
        ctx.root.name = key
        for p, o in ctx.parts.items():
            o.name = p
            o.data.name = p
    return ctx


# ----------------------------------------------------------------------------------------
# Island map access (geometry-free JSON committed in cast/islands/<key>.json)
# ----------------------------------------------------------------------------------------
def load_islands(key, directory=None):
    path = os.path.join(directory or ISLANDS_REPO_DIR, key + ".json")
    if os.path.isfile(path):
        return read_json(path)
    return None


def island_by_id(islands, island_id):
    for part in islands["parts"].values():
        for isl in part["islands"]:
            if isl["id"] == island_id:
                return isl
    raise KeyError(island_id)


def islands_with_bone(islands, bone):
    """All islands whose suggested bone is `bone` (a starting point for skin.py)."""
    out = []
    for part in islands["parts"].values():
        for isl in part["islands"]:
            if isl.get("bone") == bone:
                out.append(isl)
    return out
