"""WP-1: re-run the PRD section 2 inspection as a script and write one island map per character.

  blender --background --factory-startup --disable-autoexec --python Tools/characters/inspect_cast.py -- \
      --src <extracted zip folder> --out <output folder> [--only prince,mira] [--islands-dir <dir>] [--no-blend]

--src   folder that holds Models/*.fbx and RomanVillage_DarkFantasy_Characters.blend (read only)
--out   output folder OUTSIDE the repository; receives:
          inspect/inspection.json       section 2.3 numbers for each character plus the .blend facts
          inspect/islands/<key>.json    island maps (same bytes as the committable copies)
          inspect/summary.txt           one readable line per character
--islands-dir  optional second destination for the island maps (the repo copy: Tools/characters/cast/islands)

Nothing in --src is executed. The .blend is opened with load_ui False and its text blocks are only counted.
"""

import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "lib"))

import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402

import contract  # noqa: E402
import common  # noqa: E402
from common import r, rv, log  # noqa: E402
import islands  # noqa: E402


# PRD_CHARACTERS.md section 2.3 (tris, verts, part tris, materials, height incl. props). The run fails if a number differs.
EXPECTED = {
    "prince": (2520, 1372, {"Body": 876, "Cloth": 112, "Head": 1220, "Legs": 312}, 13, 1.84),
    "mira": (2696, 1474, {"Body": 888, "Cloth": 100, "Head": 1296, "Legs": 348, "Prop": 64}, 16, 1.97),
    "juno": (2180, 1198, {"Body": 848, "Head": 1020, "Legs": 312}, 13, 1.73),
    "sol": (2768, 1502, {"Body": 924, "Cloth": 32, "Head": 1500, "Legs": 312}, 13, 1.86),
    "woodcutter": (2472, 1356, {"Body": 992, "Head": 1100, "Legs": 312, "Prop": 68}, 15, 1.91),
    "carpenter": (2316, 1268, {"Body": 992, "Head": 940, "Legs": 312, "Prop": 72}, 15, 1.66),
    "hesta": (2468, 1342, {"Body": 912, "Cloth": 28, "Head": 1180, "Legs": 348}, 13, 1.69),
    "merchant": (2424, 1330, {"Body": 1012, "Head": 1100, "Legs": 312}, 13, 1.80),
    "dancer_ochre": (2324, 1260, {"Body": 764, "Cloth": 28, "Head": 1220, "Legs": 312}, 12, 1.80),
    "dancer_ash": (2356, 1278, {"Body": 840, "Cloth": 28, "Head": 1140, "Legs": 348}, 12, 1.65),
    "dancer_moss": (2104, 1152, {"Body": 784, "Cloth": 28, "Head": 980, "Legs": 312}, 12, 1.76),
    "dancer_plum": (2524, 1378, {"Body": 840, "Cloth": 28, "Head": 1308, "Legs": 348}, 12, 1.71),
    "carrier": (2552, 1404, {"Body": 992, "Head": 1168, "Legs": 312, "Prop": 80}, 16, 1.91),
    "champion": (2684, 1468, {"Body": 1004, "Cloth": 100, "Head": 1160, "Legs": 312, "Prop": 108}, 15, 1.89),
    "stalker": (1260, 742, {"Body": 556, "Head": 408, "Legs": 296}, 7, 1.52),
    "brute": (1320, 778, {"Body": 616, "Head": 408, "Legs": 296}, 7, 2.03),
}


def check_expected(key, rec):
    t, v, pt, m, h = EXPECTED[key]
    bad = []
    if rec["tris"] != t:
        bad.append("tris %s != %s" % (rec["tris"], t))
    if rec["verts"] != v:
        bad.append("verts %s != %s" % (rec["verts"], v))
    if rec["part_tris"] != pt:
        bad.append("part tris %s != %s" % (rec["part_tris"], pt))
    if rec["materials"] != m:
        bad.append("materials %s != %s" % (rec["materials"], m))
    if abs(round(rec["height_with_props_m"], 2) - h) > 0.011:
        bad.append("height %s != %s" % (rec["height_with_props_m"], h))
    for p, info in rec["parts"].items():
        if info["non_manifold_edges"] or info["degenerate_faces"] or info["vertex_groups"] or info["shape_keys"]:
            bad.append("part %s not clean: %s" % (p, {k: info[k] for k in ("non_manifold_edges", "degenerate_faces", "vertex_groups", "shape_keys")}))
    if rec["armatures"] or rec["actions"] or rec["images"]:
        bad.append("armatures/actions/images present")
    return bad


def mesh_health(obj):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    degen = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    bm.free()
    # flat shading check: corner normals vs polygon normals
    me.calc_loop_triangles()
    flat_loops = 0
    total_loops = len(me.loops)
    cn = np.empty(total_loops * 3, dtype=np.float64)
    try:
        me.corner_normals.foreach_get("vector", cn)
        cn = cn.reshape(-1, 3)
        pn = np.empty(len(me.polygons) * 3, dtype=np.float64)
        me.polygons.foreach_get("normal", pn)
        pn = pn.reshape(-1, 3)
        ls = np.empty(len(me.polygons), dtype=np.int32)
        lt = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get("loop_start", ls)
        me.polygons.foreach_get("loop_total", lt)
        poly_of_loop = np.repeat(np.arange(len(me.polygons)), lt)
        dots = np.einsum("ij,ij->i", cn, pn[poly_of_loop])
        flat_loops = int(np.sum(dots > 0.9999))
    except Exception as e:  # pragma: no cover
        flat_loops = -1
    smooth_polys = sum(1 for p in me.polygons if p.use_smooth)
    uv_layers = [u.name for u in me.uv_layers]
    uv_range = None
    if me.uv_layers:
        uv = np.empty(len(me.loops) * 2, dtype=np.float64)
        me.uv_layers[0].uv.foreach_get("vector", uv)
        uv = uv.reshape(-1, 2)
        uv_range = [rv(uv.min(axis=0)), rv(uv.max(axis=0))]
    return {
        "non_manifold_edges": nonman, "degenerate_faces": degen,
        "custom_normals": bool(me.has_custom_normals),
        "flat_corner_normal_fraction": r(flat_loops / total_loops, 4) if total_loops and flat_loops >= 0 else None,
        "smooth_flagged_polygons": smooth_polys, "polygons": len(me.polygons),
        "uv_layers": uv_layers, "uv_range": uv_range,
        "vertex_groups": len(obj.vertex_groups), "shape_keys": (len(me.shape_keys.key_blocks) if me.shape_keys else 0),
        "modifiers": [m.type for m in obj.modifiers],
        "scale": rv(obj.scale), "rotation_euler": rv(obj.rotation_euler),
    }


def inspect_character(key, src, out, islands_dirs):
    ctx = common.load_character(key, src, out, rename=True)
    objs = [o for o in bpy.data.objects]
    parts = ctx.parts
    mats = {}
    for p, o in parts.items():
        for m in o.data.materials:
            if m:
                mats[m.name] = True
    all_mat_names = sorted(mats)
    base_names = sorted({common.base_material_name(n) for n in all_mat_names})
    part_info = {}
    allmin, allmax = np.array([1e9] * 3), np.array([-1e9] * 3)
    height_parts = 0.0
    for p, o in sorted(parts.items()):
        me = o.data
        me.calc_loop_triangles()
        pts = np.empty(len(me.vertices) * 3)
        me.vertices.foreach_get("co", pts)
        pts = pts.reshape(-1, 3)
        allmin = np.minimum(allmin, pts.min(axis=0))
        allmax = np.maximum(allmax, pts.max(axis=0))
        h = mesh_health(o)
        h.update({"tris": len(me.loop_triangles), "verts": len(me.vertices),
                  "materials": [m.name if m else None for m in me.materials],
                  "bounds_min": rv(pts.min(axis=0)), "bounds_max": rv(pts.max(axis=0))})
        part_info[p] = h
    tris = sum(v["tris"] for v in part_info.values())
    verts = sum(v["verts"] for v in part_info.values())
    # island map
    imap = islands.build_island_map(ctx, key)
    for p in part_info:
        part_info[p]["islands"] = imap["parts"][p]["island_count"]
    leg_tris = part_info["Legs"]["tris"]
    info = contract.CAST_BY_KEY[key]
    if info["rig"] == "demon":
        family = "n/a"
    else:
        family = "dress" if leg_tris == 348 else ("trousers" if leg_tris == 312 else "other:%d" % leg_tris)
    rec = {
        "key": key, "number": info["number"], "display": info["display"], "source_fbx": info["fbx"],
        "root": {"name_in_file": ctx.root.name, "location": rv(ctx.root.location), "scale": rv(ctx.root.scale),
                 "rotation_euler": rv(ctx.root.rotation_euler)},
        "tris": tris, "verts": verts,
        "parts": part_info,
        "part_tris": {p: v["tris"] for p, v in sorted(part_info.items())},
        "materials": len(all_mat_names), "material_names": all_mat_names,
        "materials_with_duplicate_suffix": [n for n in all_mat_names if n != common.base_material_name(n)],
        "distinct_base_materials": len(base_names),
        "height_m": r(imap["height"]), "height_with_props_m": r(imap["height_with_props"]),
        "bounds_min": rv(allmin), "bounds_max": rv(allmax),
        "leg_family": family,
        "armatures": sum(1 for o in objs if o.type == "ARMATURE"),
        "actions": len(bpy.data.actions), "images": len(bpy.data.images),
        "objects": len(objs), "unit_scale": bpy.context.scene.unit_settings.scale_length,
        "unit_system": bpy.context.scene.unit_settings.system,
        "forward_check": imap["face"].get("forward_check"),
        "eye_shape": imap["face"].get("shape"),
        "gaze_method": imap["face"].get("gaze_method"),
    }
    for d in islands_dirs:
        common.write_json(os.path.join(d, key + ".json"), imap)
    return rec, imap


def inspect_blend(src):
    """Facts from the supplied .blend, read only. Text blocks are counted, never run."""
    path = common.blend_path(src)
    common.reset_scene()
    bpy.ops.wm.open_mainfile(filepath=path, load_ui=False, use_scripts=False)
    objs = bpy.data.objects
    empties = [o for o in objs if o.type == "EMPTY"]
    meshes = [o for o in objs if o.type == "MESH"]
    emissive = []
    for m in bpy.data.materials:
        if m.use_nodes and m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == "BSDF_PRINCIPLED":
                    strength = n.inputs.get("Emission Strength")
                    color = n.inputs.get("Emission Color")
                    if strength is not None and strength.default_value > 0 and color is not None and sum(color.default_value[:3]) > 0:
                        emissive.append({"material": m.name, "strength": r(strength.default_value, 3)})
    props = {}
    for o in empties:
        props[o.name] = {k: str(o[k]) for k in sorted(o.keys()) if not k.startswith("_")}
    xs = [o.location.x for o in empties]
    return {
        "blender_version_saved_with": "%d.%d.%d" % tuple(bpy.data.version),
        "objects": len(objs), "empties": len(empties), "meshes": len(meshes),
        "collections": len(bpy.data.collections), "collection_names": sorted(c.name for c in bpy.data.collections),
        "text_blocks": len(bpy.data.texts), "libraries": len(bpy.data.libraries),
        "images": [i.name for i in bpy.data.images], "cameras": len(bpy.data.cameras), "lights": len(bpy.data.lights),
        "armatures": len(bpy.data.armatures), "actions": len(bpy.data.actions),
        "materials": len(bpy.data.materials),
        "emissive_materials": sorted(emissive, key=lambda e: e["material"]),
        "empty_custom_properties": props,
        "empty_x_range": [r(min(xs)), r(max(xs))] if xs else None,
    }


def main():
    args = common.parse_args([("--islands-dir", {"default": "", "help": "second destination for island maps (repo copy)"}),
                              ("--no-blend", {"action": "store_true", "help": "skip the .blend facts"})],
                             description=__doc__)
    if not args.src or not args.out:
        raise SystemExit("--src and --out are required")
    src = os.path.abspath(args.src)
    out = os.path.abspath(args.out)
    repo_root = os.path.abspath(os.path.join(common.TOOLS_DIR, "..", ".."))
    if os.path.commonpath([out, repo_root]) == repo_root:
        raise SystemExit("refusing to write outside-the-repo data into the repository: --out must not be inside %s" % repo_root)
    keys = common.selected_keys(args.only)
    ins_dir = common.ensure_dir(os.path.join(out, "inspect"))
    isl_dir = common.ensure_dir(os.path.join(ins_dir, "islands"))
    dirs = [isl_dir] + ([os.path.abspath(args.islands_dir)] if args.islands_dir else [])
    result = {"characters": {}, "contract_version": contract.CONTRACT_VERSION}
    lines = []
    failures = []
    for key in keys:
        log("inspect", key)
        rec, imap = inspect_character(key, src, out, dirs)
        result["characters"][key] = rec
        rec["matches_prd_section_2_3"] = not check_expected(key, rec)
        for msg in check_expected(key, rec):
            failures.append("%s: %s" % (key, msg))
        lm = imap["landmarks"]
        nc = len(imap["joint_gaps"]["not_covered"]) if "joint_gaps" in imap else -1
        lines.append("%-12s tris %5d verts %5d parts %s mats %2d h %.2f leg %-8s eye %-6s gaze %-8s fwd_ok %s arm %s not_covered %d" % (
            key, rec["tris"], rec["verts"], rec["part_tris"], rec["materials"], rec["height_m"], rec["leg_family"],
            rec["eye_shape"], rec["gaze_method"], (rec["forward_check"] or {}).get("faces_minus_y"),
            lm.get("arm_angle_deg_from_vertical"), nc))
    # cast index (geometry-free): one small record per character for the later packages
    index = {}
    for key in keys:
        rec = result["characters"][key]
        imap = common.read_json(os.path.join(isl_dir, key + ".json"))
        roles = {i.get("role") for p in imap["parts"].values() for i in p["islands"]}
        lm = imap["landmarks"]
        prop = lm.get("prop") if isinstance(lm, dict) else None
        info = contract.CAST_BY_KEY[key]
        index[key] = {
            "number": info["number"], "display": info["display"], "rig": info["rig"], "slice": info["slice"],
            "source_fbx": info["fbx"], "leg_family": rec["leg_family"], "height_m": rec["height_m"],
            "height_with_props_m": rec["height_with_props_m"], "tris": rec["tris"],
            "parts": sorted(rec["parts"].keys()),
            "optional_chains": {"skirt": rec["leg_family"] == "dress", "cape": "cloth_back" in roles,
                                "hood": bool(roles & {"hood", "hood_or_collar"})},
            "prop": ({"hand": prop["holding_hand"], "bone": prop["bone"], "head_point": prop["head_point"],
                      "grip_point": prop["grip_point"]} if prop else None),
            "eye_shape": rec["eye_shape"], "gaze_method": rec["gaze_method"],
            "island_map": "cast/islands/%s.json" % key,
        }
    common.write_json(os.path.join(ins_dir, "index.json"), index)
    if args.islands_dir and len(keys) == len(contract.CAST_KEYS):
        common.write_json(os.path.join(os.path.dirname(os.path.abspath(args.islands_dir)), "index.json"),
                          {"schema": 1, "contract_version": contract.CONTRACT_VERSION, "characters": index})
    if not args.no_blend:
        log("inspect blend")
        result["blend"] = inspect_blend(src)
    common.write_json(os.path.join(ins_dir, "inspection.json"), result)
    with open(os.path.join(ins_dir, "summary.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    log("done", len(keys), "characters ->", out)
    if failures:
        for f in failures:
            log("MISMATCH with PRD section 2.3:", f)
        sys.exit(1)
    log("all numbers match PRD section 2.3")


main()
