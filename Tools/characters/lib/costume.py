"""WP-4: costume variants (role material slots, hidden pieces, procedural overlay meshes, prop swaps).

Entry point (contract, README 2.8):   costume.apply_costume(ctx, costume_name="prehistoric")

Needs a scene that already ran load_character and the rig step (ctx.armature set; the development stub
`stub_rig.build_stub_armature` is enough). Idempotent from the original FBX: it never reads a previous output.

What it does for one character and one variant ("historic" or "prehistoric"):
  1. Maps every island of the supplied meshes (cast/islands/<key>.json, WP-1) to a role slot
     (`skin, hair, cloth_main, cloth_trim, leather, metal, eye_white, eye_dark, emissive`) with rules, then applies
     the variant's overrides. Pieces the variant switches off go to the `hidden` slot (not deleted).
  2. Creates one flat material per used slot, named `Role_<slot>` (`Role_hidden` = no draw), and sets the polygon
     material indices. Slot colours come from the variant palette; the historic palette is the polygon-weighted mean
     of the supplied colours of the pieces in the slot (the error is reported in the manifest).
  3. Builds the variant's overlay meshes `Ov_<name>` (low poly, flat shaded, from data recipes that are measured
     against the character's own pieces and landmarks), weights them to contract bones and adds the Armature modifier.
  4. Prop swaps are overlays too (`Ov_Staff`, `Ov_Club`): the supplied Prop pieces are hidden by the variant and the
     replacement is weighted rigidly to the hand bone of the socket, with the same grip and head points.
  5. Writes manifest section `costumes`: both variants as data (slots, island_slots, hidden islands, overlays, props)
     plus which one is `applied`.

Data: `cast/costumes/_defaults.json` (rules, pinned eye colours, generic prehistoric recipe) and
`cast/costumes/<key>.json` (per character; characters without a file get the generic recipe, `auto: true`).
All of it is geometry free. The pure python half of this module (everything above "Blender half") needs no bpy,
so `resolve_variant()` can be unit checked for all 14 humans without Blender.
"""

import colorsys
import json
import math
import os
import random
import re
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import contract  # noqa: E402

try:
    import bpy  # noqa: E402
    import bmesh  # noqa: E402,F401
    from mathutils import Vector, Matrix, Euler  # noqa: E402
    from mathutils.bvhtree import BVHTree  # noqa: E402
    HAVE_BPY = True
except ImportError:
    bpy = None
    HAVE_BPY = False

TOOLS_DIR = os.path.dirname(_HERE)
COSTUME_DIR = os.path.join(TOOLS_DIR, "cast", "costumes")
VARIANTS = ("historic", "prehistoric")
PART_ORDER = ("Body", "Legs", "Head", "Cloth", "Prop")


def log(*a):
    print("[costume]", *a, flush=True)


# ======================================================================================
# Pure python half: data, slot rules, variant resolution
# ======================================================================================
def read_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def hex_of(material_name):
    m = re.match(r"^RV_([0-9A-Fa-f]{6})", material_name)
    return m.group(1).upper() if m else None


def rgb255(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def to_hex(rgb):
    return "".join("%02X" % max(0, min(255, int(round(c)))) for c in rgb)


def colour_dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(rgb255(a), rgb255(b))))


def mix_hex(a, b, t):
    A, B = rgb255(a), rgb255(b)
    return to_hex([x + (y - x) * t for x, y in zip(A, B)])


def lift_hex(h, dv=0.0, ds=1.0, dh=0.0):
    r, g, b = [c / 255.0 for c in rgb255(h)]
    hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
    hh = (hh + dh) % 1.0
    ss = max(0.0, min(1.0, ss * ds))
    vv = max(0.0, min(1.0, vv + dv))
    return to_hex([c * 255 for c in colorsys.hsv_to_rgb(hh, ss, vv)])


def load_defaults():
    return read_json(os.path.join(COSTUME_DIR, "_defaults.json"))


def flat_islands(imap):
    out = []
    for pn in PART_ORDER:
        part = imap["parts"].get(pn)
        if not part:
            continue
        for i in part["islands"]:
            d = dict(i)
            d["part"] = pn
            d["hexname"] = hex_of(i["materials"][0])
            d["is_emissive"] = bool(i.get("emissive")) or "_Ember" in i["materials"][0]
            out.append(d)
    return out


def skin_hex(imap):
    return hex_of(imap["landmarks"]["skin_material"])


def base_slots(imap, defaults):
    """Rule based island -> slot map for the supplied look (before any variant override)."""
    isl = flat_islands(imap)
    skin = skin_hex(imap)
    metal = set(defaults["metal_hexes"])
    leather = set(defaults["leather_hexes"])
    skin_roles = set(defaults["skin_roles"])
    eye_dark_roles = set(defaults["eye_dark_roles"])
    hair_roles = set(defaults["hair_roles"])
    slots = {}
    cloth = []
    for i in isl:
        h, role = i["hexname"], i["role"]
        if i["is_emissive"]:
            slots[i["id"]] = "emissive"
        elif role == "eye_sclera":
            slots[i["id"]] = "eye_white"
        elif role in eye_dark_roles:
            slots[i["id"]] = "eye_dark"
        elif role in hair_roles or role == "hood":
            slots[i["id"]] = "hair"
        elif h == skin or role in skin_roles:
            slots[i["id"]] = "skin"
        elif h in metal:
            slots[i["id"]] = "metal"
        elif h in leather:
            slots[i["id"]] = "leather"
        elif role == "head_other":
            slots[i["id"]] = "hair"
        else:
            cloth.append(i)
    # cloth colours: the largest group is cloth_main, the second largest cloth_trim, the rest join the nearest.
    groups = {}
    for i in cloth:
        groups.setdefault(i["hexname"], []).append(i)
    order = sorted(groups, key=lambda h: (-sum(i["tris"] for i in groups[h]), h))
    main = order[0] if order else None
    trim = order[1] if len(order) > 1 else None
    for h in order:
        if h == main:
            s = "cloth_main"
        elif h == trim:
            s = "cloth_trim"
        else:
            s = "cloth_main" if (trim is None or colour_dist(h, main) <= colour_dist(h, trim)) else "cloth_trim"
        for i in groups[h]:
            slots[i["id"]] = s
    return slots


def selector_match(i, sel):
    """Selector grammar: 'Body.07' | 'role:cuff' | 'hex:682937' | 'bone:Hips' | 'part:Prop' | 'side:L' | 'slot:..' joined with '&'."""
    for term in sel.split("&"):
        term = term.strip()
        if ":" not in term:
            ok = i["id"] == term
        else:
            k, v = term.split(":", 1)
            if k == "role":
                ok = i["role"] == v
            elif k == "hex":
                ok = i["hexname"] == v.upper()
            elif k == "bone":
                ok = i.get("bone") == v
            elif k == "part":
                ok = i["part"] == v
            elif k == "side":
                ok = i["side"] == v
            elif k == "emissive":
                ok = i["is_emissive"] == (v == "1")
            else:
                raise ValueError("bad selector term " + term)
        if not ok:
            return False
    return True


def select_ids(imap, sel):
    return [i["id"] for i in flat_islands(imap) if selector_match(i, sel)]


def _cond(imap, cond):
    """'has_role:robe_or_skirt', 'no_role:...', 'has_prop', 'no_prop'; list = all must hold."""
    if cond is None:
        return True
    if isinstance(cond, list):
        return all(_cond(imap, c) for c in cond)
    roles = {i["role"] for i in flat_islands(imap)}
    if cond == "has_prop":
        return bool(imap["landmarks"].get("prop"))
    if cond == "no_prop":
        return not imap["landmarks"].get("prop")
    k, v = cond.split(":", 1)
    return (v in roles) if k == "has_role" else (v not in roles)


def load_costume_data(key, imap=None):
    """Per character file if present, else the generic recipe (auto: true)."""
    defaults = load_defaults()
    path = os.path.join(COSTUME_DIR, key + ".json")
    if os.path.isfile(path):
        d = read_json(path)
        d["auto"] = False
        return d
    d = json.loads(json.dumps(defaults["auto_character"]))
    d["key"] = key
    d["auto"] = True
    return d


def _historic_means(imap, slots):
    """Polygon weighted mean of the supplied colours per slot."""
    acc = {}
    for i in flat_islands(imap):
        s = slots.get(i["id"])
        if s is None or s == "hidden" or i["hexname"] is None:
            continue
        acc.setdefault(s, []).append((i["tris"], rgb255(i["hexname"])))
    out = {}
    for s, lst in acc.items():
        tot = float(sum(w for w, _ in lst))
        out[s] = to_hex([sum(w * c[k] for w, c in lst) / tot for k in range(3)])
    return out


def _apply_merges(slots, merges):
    for src, dst in merges.items():
        for k, v in list(slots.items()):
            if v == src:
                slots[k] = dst
    return slots


def _fit_slot_budget(slots, defaults):
    merged = {}
    for src, dst in defaults["fallback_merge_order"]:
        used = {v for v in slots.values() if v != contract.HIDDEN_SLOT}
        if len(used) <= contract.MAX_SLOTS_PER_CHARACTER:
            break
        if src in used:
            _apply_merges(slots, {src: dst})
            merged[src] = dst
    return merged


def auto_palette(imap, hist, defaults):
    """Prehistoric palette for a character without a data file: hide, dyed fur in the identity colour, bone."""
    a = defaults["auto_palette"]
    main = hist.get("cloth_main", a["hide"])
    trim = hist.get("cloth_trim", main)
    ident = lift_hex(trim, dv=a["dye_lift_v"], ds=a["dye_sat"])
    hide = mix_hex(lift_hex(main, dv=0.08, ds=0.6), a["hide"], a["hide_mix"])
    return {"cloth_main": hide, "cloth_trim": ident, "leather": a["leather"], "metal": a["bone"],
            "hair": lift_hex(hist.get("hair", "362B32"), dv=-0.04)}   # brows share this slot: keep them dark against skin


def resolve_variant(key, imap, variant_name, data=None):
    """Pure data: slots, island_slots, hidden islands, overlays and props of one variant. No bpy."""
    defaults = load_defaults()
    data = data or load_costume_data(key, imap)
    if variant_name not in data["variants"]:
        raise KeyError("character %s has no costume variant %r" % (key, variant_name))
    var = data["variants"][variant_name]
    hist_slots = base_slots(imap, defaults)
    merges = dict(data.get("merge", {}))
    _apply_merges(hist_slots, merges)
    merged = dict(merges)
    merged.update(_fit_slot_budget(hist_slots, defaults))
    hist_colours = _historic_means(imap, hist_slots)
    slots = dict(hist_slots)
    if variant_name != "historic":
        for sel, slot in var.get("slot_overrides", {}).items():
            for iid in select_ids(imap, sel):
                slots[iid] = slot
        for entry in var.get("conditional", []):
            if not _cond(imap, entry.get("when")):
                continue
            for sel, slot in entry.get("slot_overrides", {}).items():
                for iid in select_ids(imap, sel):
                    slots[iid] = slot
        for sel in var.get("hidden", []):
            for iid in select_ids(imap, sel):
                slots[iid] = contract.HIDDEN_SLOT
        for entry in var.get("conditional", []):
            if _cond(imap, entry.get("when")):
                for sel in entry.get("hidden", []):
                    for iid in select_ids(imap, sel):
                        slots[iid] = contract.HIDDEN_SLOT
        _apply_merges(slots, var.get("merge", {}))
    used = sorted({v for v in slots.values() if v != contract.HIDDEN_SLOT}, key=contract.ROLE_SLOTS.index)
    colours = {}
    pal = dict(var.get("palette", {}))
    if variant_name != "historic" and data.get("auto"):
        ap = auto_palette(imap, hist_colours, defaults)
        ap.update(pal)
        pal = ap
    for s in used:
        c = pal.get(s)
        if c in (None, "supplied", "keep"):
            c = hist_colours.get(s) or hist_colours.get(merged_target(merged, s)) or "808080"
        colours[s] = c.upper()
    colours["skin"] = pal.get("skin", skin_hex(imap)) if "skin" in used else None
    if colours.get("skin") in ("keep", "supplied"):
        colours["skin"] = skin_hex(imap)
    if colours["skin"] is None:
        del colours["skin"]
    for s, c in defaults["eye_colours"].items():
        if s in used and s not in var.get("palette", {}):
            colours[s] = c
    hidden = sorted(k for k, v in slots.items() if v == contract.HIDDEN_SLOT)
    overlays, props = [], {}
    if variant_name != "historic":
        for o in var.get("overlays", []):
            if _cond(imap, o.get("when")):
                overlays.append(o)
                if o.get("prop"):
                    props[o["prop"]["socket"]] = o["prop"]["key"]
    # error of the slot colours against the supplied pieces (meaningful for the historic palette)
    err = {}
    for i in flat_islands(imap):
        s = slots.get(i["id"])
        if s in (None, "hidden") or i["hexname"] is None or s not in colours:
            continue
        d = colour_dist(i["hexname"], colours[s])
        e = err.setdefault(s, {"max": 0.0, "mean_w": 0.0, "w": 0})
        e["max"] = max(e["max"], d)
        e["mean_w"] += d * i["tris"]
        e["w"] += i["tris"]
    colour_error = {s: {"max_rgb": round(e["max"], 1), "mean_rgb": round(e["mean_w"] / e["w"], 1)} for s, e in sorted(err.items())}
    return {"name": variant_name, "description": var.get("description", ""), "slots": colours,
            "slot_count": len(used), "island_slots": dict(sorted(slots.items())), "hidden_islands": hidden,
            "overlay_recipes": overlays, "overlays": [contract.OVERLAY_PREFIX + o["name"] for o in overlays],
            "props": props, "merged": merged, "colour_error": colour_error}


def merged_target(merged, s):
    return merged.get(s, s)


# ======================================================================================
# Blender half
# ======================================================================================
_NAMES = {}


def _srgb_to_lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def slot_material(slot, hexv):
    """One flat material per slot, named Role_<slot>. Unity maps by name; the hex is also a custom property."""
    name = contract.ROLE_MATERIAL_PREFIX + slot
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if slot == contract.HIDDEN_SLOT:
        sh = nt.nodes.new("ShaderNodeBsdfTransparent")
        nt.links.new(sh.outputs[0], out.inputs[0])
        mat.diffuse_color = (0, 0, 0, 0)
        mat["hex"] = "000000"
        return mat
    rgb = [_srgb_to_lin(c) for c in rgb255(hexv)]
    sh = nt.nodes.new("ShaderNodeBsdfPrincipled")
    sh.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    sh.inputs["Roughness"].default_value = 0.92
    if "Specular IOR Level" in sh.inputs:
        sh.inputs["Specular IOR Level"].default_value = 0.1
    if slot == "emissive":
        sh.inputs["Emission Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        sh.inputs["Emission Strength"].default_value = 1.4
    nt.links.new(sh.outputs[0], out.inputs[0])
    mat.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
    mat["hex"] = hexv
    return mat


class Geo:
    """Measurements of the loaded character that overlay recipes use (all world space, metres)."""

    def __init__(self, ctx):
        import numpy as np
        import islands as islands_mod
        self.np = np
        self.ctx = ctx
        self.imap = ctx.islands
        lm = self.imap["landmarks"]
        mean = lambda d: (d["L"] + d["R"]) / 2.0 if isinstance(d, dict) else d
        self.names = {
            "hip_z": lm["hip_z"], "knee_z": mean(lm["knee_z"]), "ankle_z": mean(lm["ankle_z"]),
            "shoulder_z": mean(lm["shoulder_z"]), "neck_z": lm["neck_z"], "elbow_z": mean(lm["elbow_z"]),
            "wrist_z": mean(lm["wrist_z"]), "crown_z": lm["crown_z"], "head_bottom_z": lm["head_bottom_z"],
            "head_cz": lm["head_centre"][2], "head_cy": lm["head_centre"][1], "height": self.imap["height"],
            "zero": 0.0,
        }
        # per island vertex data
        self.isl_verts = {}     # id -> (np array of coords, np array of edges (m,2) indices into coords)
        self.part_isl = {}
        self.imap_by_id = {i["id"]: i for i in flat_islands(self.imap)}
        for pn, obj in ctx.parts.items():
            isls, co = islands_mod.vertex_islands(obj)
            me = obj.data
            ne = len(me.edges)
            ev = np.empty(ne * 2, dtype=np.int64)
            me.edges.foreach_get("vertices", ev)
            ev = ev.reshape(ne, 2)
            vi = np.full(len(me.vertices), -1, dtype=np.int64)
            for n, isl in enumerate(isls):
                vi[isl["verts"]] = n
            self.part_isl[pn] = (isls, co, ev, vi)
            for n, isl in enumerate(isls):
                iid = "%s.%02d" % (pn, n)
                verts = np.array(isl["verts"], dtype=np.int64)
                remap = {int(v): k for k, v in enumerate(verts)}
                e = ev[vi[ev[:, 0]] == n]
                e2 = np.array([[remap[int(a)], remap[int(b)]] for a, b in e], dtype=np.int64) if len(e) else np.zeros((0, 2), dtype=np.int64)
                self.isl_verts[iid] = (co[verts], e2)
        self.segs = {}
        arm = ctx.armature
        if arm is not None:
            M = arm.matrix_world
            for b in arm.data.bones:
                self.segs[b.name] = (M @ b.head_local, M @ b.tail_local)
        self._head_bvh = None

    # ---- values
    def val(self, e):
        if isinstance(e, (int, float)):
            return float(e)
        m = re.match(r"^\s*([A-Za-z_]+)\s*((?:[+-]\s*\d*\.?\d+\s*)*)$", e)
        if not m:
            raise ValueError("bad expression %r" % e)
        base = self.names[m.group(1)]
        add = sum(float(x.replace(" ", "")) for x in re.findall(r"[+-]\s*\d*\.?\d+", m.group(2)))
        return base + add

    def point(self, p):
        return Vector([self.val(c) for c in p])

    def seg(self, bone):
        return self.segs[bone]

    # ---- sections
    def fit_ids(self, fit):
        ids = []
        for sel in fit:
            ids += select_ids(self.imap, sel)
        return ids

    def section(self, fit, z, _depth=0):
        """Bounding box (xmin, xmax, ymin, ymax) of the plane z cut through the listed pieces (selectors)."""
        np = self.np
        pts = []
        for iid in self.fit_ids(fit):
            co, e = self.isl_verts[iid]
            if len(e) == 0:
                continue
            a, b = co[e[:, 0]], co[e[:, 1]]
            lo, hi = np.minimum(a[:, 2], b[:, 2]), np.maximum(a[:, 2], b[:, 2])
            m = (lo <= z) & (hi >= z) & (hi - lo > 1e-9)
            if not m.any():
                continue
            a, b = a[m], b[m]
            t = ((z - a[:, 2]) / (b[:, 2] - a[:, 2]))[:, None]
            pts.append(a[:, :2] + (b[:, :2] - a[:, :2]) * t)
        if not pts:
            # outside the pieces' height range: clamp to the nearest height that is covered (1 mm inside)
            zs = [self.isl_verts[i][0][:, 2] for i in self.fit_ids(fit)]
            if not zs or _depth > 0:
                return None
            lo = min(float(a.min()) for a in zs) + 0.001
            hi = max(float(a.max()) for a in zs) - 0.001
            return self.section(fit, min(max(z, lo), hi), _depth + 1)
        p = np.concatenate(pts)
        return float(p[:, 0].min()), float(p[:, 0].max()), float(p[:, 1].min()), float(p[:, 1].max())

    def head_surface_y(self, x, z):
        """Front surface y of the Head mesh at (x, z) by ray cast from the front (None if missed)."""
        head = self.ctx.parts["Head"]
        if self._head_bvh is None:
            ids = [i for i in self.isl_verts if i.startswith("Head.")]
            me = head.data
            verts = [head.matrix_world @ v.co for v in me.vertices]
            polys = [tuple(p.vertices) for p in me.polygons]
            self._head_bvh = BVHTree.FromPolygons([tuple(v) for v in verts], polys)
        hit = self._head_bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
        return None if hit[0] is None else hit[0].y


# ---------------------------------------------------------------------------------------
class MeshBuilder:
    """Collects vertices, faces, per-face slot and per-vertex weight spec."""

    def __init__(self):
        self.v, self.f, self.fs, self.vw = [], [], [], []
        self.wspec = None

    def vert(self, p):
        self.v.append((float(p[0]), float(p[1]), float(p[2])))
        self.vw.append(self.wspec)
        return len(self.v) - 1

    def face(self, idx, slot, ref=None):
        """Add a polygon; if `ref` (a point that is inside the shape) is given the winding is flipped to face away from it."""
        if ref is not None and len(idx) >= 3:
            p = [Vector(self.v[i]) for i in idx]
            n = (p[1] - p[0]).cross(p[2] - p[1])
            c = sum(p, Vector()) / len(p)
            if n.length > 1e-12 and n.dot(c - Vector(ref)) < 0:
                idx = list(reversed(idx))
        self.f.append(tuple(idx))
        self.fs.append(slot)


def _rng(name):
    return random.Random(sum(ord(c) * (k + 1) for k, c in enumerate(name)))


def _ring_point(cx, cy, rx, ry, theta_deg, boxy):
    t = math.radians(theta_deg)
    s, c = math.sin(t), math.cos(t)
    e = 2.0 / boxy
    return (cx + rx * math.copysign(abs(s) ** e, s), cy - ry * math.copysign(abs(c) ** e, c))


def _build_rings(mb, ring_pts_list, closed, slot, ref_fn, teeth_last=0):
    """ring_pts_list: list of rings, each a list of 3D points; quads between consecutive rings."""
    idx = [[mb.vert(p) for p in ring] for ring in ring_pts_list]
    for r in range(len(idx) - 1):
        a, b = idx[r], idx[r + 1]
        n = len(a)
        span = n if closed else n - 1
        for j in range(span):
            j2 = (j + 1) % n
            mb.face([a[j], b[j], b[j2], a[j2]], slot, ref_fn(r, j))
    return idx


def part_shell(g, mb, p, name):
    rng = _rng(name + p.get("tag", ""))
    rings = p["rings"]
    sides = p.get("sides", 12)
    arc = p.get("arc")
    boxy = p.get("boxy", 3.0)
    teeth = p.get("teeth", 0)
    depth = p.get("tooth_depth", 0.04)
    jit = p.get("jitter", 0.0)
    slot = p["slot"]
    if arc:
        a0, a1 = arc
        angles = [a0 + (a1 - a0) * j / sides for j in range(sides + 1)]
        closed = False
    else:
        angles = [360.0 * j / sides for j in range(sides)]
        closed = True
    all_pts, centres = [], []
    for ri, r in enumerate(rings):
        z = g.val(r["z"])
        if "fit" in r:
            ext = g.section(r["fit"], z)
            if ext is None:
                raise RuntimeError("%s: no section of %s at z=%.3f" % (name, r["fit"], z))
            cx, cy = (ext[0] + ext[1]) / 2, (ext[2] + ext[3]) / 2
            rx, ry = (ext[1] - ext[0]) / 2, (ext[3] - ext[2]) / 2
        else:
            cx, cy, rx, ry = r.get("cx", 0.0), r.get("cy", 0.0), r["rx"], r["ry"]
        m = r.get("margin", 0.0)
        rx = max(rx + m, r.get("min_rx", 0.0)) * r.get("sx", 1.0)
        ry = max(ry + m, r.get("min_ry", 0.0)) * r.get("sy", 1.0)
        cx += r.get("dx", 0.0)
        cy += r.get("dy", 0.0)
        last = ri == len(rings) - 1
        pts = []
        for j, ang in enumerate(angles):
            x, y = _ring_point(cx, cy, rx, ry, ang, boxy)
            zz = z + r.get("z_sin", 0.0) * math.sin(math.radians(ang)) + r.get("z_cos", 0.0) * (-math.cos(math.radians(ang)))
            if teeth and last and (j % 2 == 1):
                zz -= depth * (1.0 + rng.uniform(-jit, jit) * 4)
            elif jit:
                zz += rng.uniform(-jit, jit)
                x += rng.uniform(-jit, jit) * 0.5
                y += rng.uniform(-jit, jit) * 0.5
            pts.append((x, y, zz))
        all_pts.append(pts)
        centres.append((cx, cy, z - 0.05))
    # flat plates have no inside; use the ring centre axis as the winding reference
    _build_rings(mb, all_pts, closed, slot, lambda r, j: (centres[r][0], centres[r][1], (all_pts[r][j][2] + all_pts[r + 1][j][2]) / 2))
    if p.get("cap_top") and closed:
        top = [mb.vert(q) for q in all_pts[0]]
        mb.face(top, slot, (centres[0][0], centres[0][1], all_pts[0][0][2] - 0.5))


def _bone_frame(g, bone):
    h, t = g.seg(bone)
    axis = (t - h)
    ln = axis.length
    axis.normalize()
    u = Vector((1, 0, 0)) - axis * axis.x
    if u.length < 1e-4:
        u = Vector((0, -1, 0)) - axis * axis.y
    u.normalize()
    v = axis.cross(u)
    return h, axis, ln, u, v


def part_band(g, mb, p, name):
    rng = _rng(name + p.get("tag", ""))
    sides = p.get("sides", 8)
    boxy = p.get("boxy", 2.4)
    teeth = p.get("teeth", 0)
    depth = p.get("tooth_depth", 0.03)
    slot = p["slot"]
    for side in p.get("mirror", ["L", "R"]):
        bone = p["bone"].replace(".L", "." + side).replace(".R", "." + side)
        h, axis, ln, u, v = _bone_frame(g, bone)
        saved_spec = mb.wspec
        if p.get("side_rigid", True):   # a band belongs to the limb it wraps (inner verts are equidistant from both limbs)
            mb.wspec = {"bones": [bone], "rigid": True}
        rings = []
        centres = []
        for ri, r in enumerate(p["rings"]):
            c = h + axis * (ln * r["t"]) + u * r.get("du", 0.0) + v * r.get("dv", 0.0)
            pts = []
            last = ri == (0 if p.get("teeth_at") == "first" else len(p["rings"]) - 1)
            for j in range(sides):
                ang = 360.0 * j / sides
                x, y = _ring_point(0, 0, r["r"] * r.get("su", 1.0), r["r"] * r.get("sv", 1.0), ang, boxy)
                q = c + u * x + v * y
                if teeth and last and j % 2 == 1:
                    q = q + axis * (depth * (-1.0 if p.get("teeth_at") == "first" else 1.0))
                pts.append(tuple(q))
            rings.append(pts)
            centres.append(c)
        _build_rings(mb, rings, True, slot, lambda r, j: (centres[r] + centres[r + 1]) / 2)
        mb.wspec = saved_spec


def _box_verts(size, top_scale=(1.0, 1.0)):
    sx, sy, sz = [s / 2.0 for s in size]
    tx, ty = top_scale
    return [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
            (-sx * tx, -sy * ty, sz), (sx * tx, -sy * ty, sz), (sx * tx, sy * ty, sz), (-sx * tx, sy * ty, sz)]


def part_box(g, mb, p, name):
    """A (tapered) box: centre, size, euler degrees (xyz), optional top_scale; mirror X optional."""
    for mx in ([1, -1] if p.get("mirror_x") else [1]):
        c = g.point(p["centre"])
        if p.get("on"):
            on = p["on"]
            ext = g.section(on["fit"], c.z)
            if ext is not None:
                x, y = _ring_point((ext[0] + ext[1]) / 2, (ext[2] + ext[3]) / 2, (ext[1] - ext[0]) / 2 + on.get("margin", 0.01),
                                   (ext[3] - ext[2]) / 2 + on.get("margin", 0.01), on["theta"], on.get("boxy", 3.0))
                c.x, c.y = x, y
        c.x *= mx
        rot = Euler([math.radians(a) for a in p.get("rot", [0, 0, 0])], "XYZ")
        if mx < 0:
            rot = Euler((rot.x, -rot.y, -rot.z), "XYZ")
        R = rot.to_matrix()
        vs = [R @ Vector(q) + c for q in _box_verts(p["size"], tuple(p.get("top_scale", (1.0, 1.0))))]
        ids = [mb.vert(q) for q in vs]
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        for f in faces:
            mb.face([ids[i] for i in f], p["slot"], c)


def part_lathe(g, mb, p, name):
    """Solid of revolution along p0 -> p1. profile: list of [t, radius]; sides; twist (deg over length); squash [a,b]."""
    p0, p1 = g.point(p["p0"]), g.point(p["p1"])
    axis = p1 - p0
    ln = axis.length
    axis.normalize()
    u = Vector((1, 0, 0)) - axis * axis.x
    if u.length < 1e-4:
        u = Vector((0, 1, 0))
    u.normalize()
    v = axis.cross(u)
    n = p.get("sides", 6)
    sq = p.get("squash", [1.0, 1.0])
    twist = p.get("twist", 0.0)
    rows = []
    for t, r in p["profile"]:
        c = p0 + axis * (ln * t)
        pts = []
        for j in range(n):
            ang = 2 * math.pi * j / n + math.radians(twist * t) + math.radians(p.get("phase", 0.0))
            pts.append(tuple(c + u * (r * sq[0] * math.cos(ang)) + v * (r * sq[1] * math.sin(ang))))
        rows.append((c, pts))
    ids = [[mb.vert(q) for q in pts] for _, pts in rows]
    for ri in range(len(ids) - 1):
        a, b = ids[ri], ids[ri + 1]
        ref = (rows[ri][0] + rows[ri + 1][0]) / 2
        for j in range(n):
            j2 = (j + 1) % n
            quad = [a[j], b[j], b[j2], a[j2]]
            if len(set(quad)) == 4:
                mb.face(quad, p["slot"], ref)
    for ri in (0, len(ids) - 1):
        r = p["profile"][ri][1]
        if r > 1e-5:
            mb.face(list(ids[ri]), p["slot"], rows[len(rows) // 2][0])


def part_prongs(g, mb, p, name):
    """Thin tilted spikes around an axis point (staff cradle, tusks): count boxes."""
    c = g.point(p["centre"])
    n = p.get("count", 4)
    for k in range(n):
        ang = 2 * math.pi * k / n + math.radians(p.get("phase", 0.0))
        out = Vector((math.cos(ang), math.sin(ang), 0)) * p.get("radius", 0.04)
        tilt = math.radians(p.get("tilt", 12.0))
        # rotate about the tangent so the tip leans outward
        R = Euler((0, 0, ang + math.pi / 2), "XYZ").to_matrix() @ Euler((-tilt if p.get("lean_out", True) else tilt, 0, 0), "XYZ").to_matrix()
        vs = [R @ Vector(q) + c + out for q in _box_verts(p["size"], tuple(p.get("top_scale", (0.3, 0.3))))]
        ids = [mb.vert(q) for q in vs]
        for f in [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
            mb.face([ids[i] for i in f], p["slot"], c + out)


def part_necklace(g, mb, p, name):
    """Beads or fangs around the neck: ring (rx, ry) at z, front dropped by `drop`; each bead a small tapered box pointing down."""
    rng = _rng(name + "neck")
    n = p.get("count", 12)
    cx, cy = p.get("cx", 0.0), p.get("cy", 0.0)
    for k in range(n):
        ang = 360.0 * k / n
        x, y = _ring_point(cx, cy, p["rx"], p["ry"], ang, 2.0)
        front = max(0.0, math.cos(math.radians(ang + 180.0)))
        z = g.val(p["z"]) - p.get("drop", 0.06) * front ** 1.5
        y -= p.get("push", 0.0) * front ** 1.5
        size = list(p.get("bead", [0.022, 0.018, 0.04]))
        if abs(((ang + 180) % 360) - 180) < 1:   # pendant at the front
            size = list(p.get("pendant", [0.035, 0.02, 0.075]))
        j = rng.uniform(-0.1, 0.1)
        c = Vector((x, y, z))
        R = Euler((0, 0, math.radians(-ang)), "XYZ").to_matrix()
        vs = [R @ Vector(q) + c for q in _box_verts(size, (0.35, 0.5))]
        # point tips downward: flip so the taper is at the bottom
        vs = [Vector((q.x, q.y, 2 * c.z - q.z)) for q in vs]
        ids = [mb.vert(q) for q in vs]
        for f in [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]:
            mb.face([ids[i] for i in f], p["slot"], c)


def part_sash(g, mb, p, name):
    """A diagonal strap on the torso surface from (theta, z) to (theta, z) with width, following the fitted torso."""
    steps = p.get("steps", 6)
    a, b = p["from"], p["to"]
    boxy = p.get("boxy", 3.0)
    left, right = [], []
    for k in range(steps + 1):
        t = k / steps
        z = g.val(a["z"]) + (g.val(b["z"]) - g.val(a["z"])) * t
        th = a["theta"] + (b["theta"] - a["theta"]) * t
        ext = g.section(p["fit"], z)
        cx, cy = (ext[0] + ext[1]) / 2, (ext[2] + ext[3]) / 2
        rx, ry = (ext[1] - ext[0]) / 2 + p.get("margin", 0.02), (ext[3] - ext[2]) / 2 + p.get("margin", 0.02)
        x, y = _ring_point(cx, cy, rx, ry, th, boxy)
        w = p.get("width", 0.05) / 2
        left.append((x, y, z + w))
        right.append((x, y, z - w))
    li = [mb.vert(q) for q in left]
    ri = [mb.vert(q) for q in right]
    cxy = (0.0, p.get("cy", 0.0), (g.val(a["z"]) + g.val(b["z"])) / 2)
    for k in range(steps):
        mb.face([li[k], ri[k], ri[k + 1], li[k + 1]], p["slot"], (cxy[0], cxy[1], (left[k][2] + right[k][2]) / 2))


def part_stripes(g, mb, p, name):
    """Face paint: thin flat bars on the front of the head surface (ray cast), mirrored left/right."""
    for s in p["stripes"]:
        for mx in ([1, -1] if p.get("mirror", True) else [1]):
            x0, z0 = s["x"] * mx, g.val(s["z"])
            ang = math.radians(s.get("angle", 0.0)) * mx
            ln, w = s.get("len", 0.05), s.get("w", 0.012)
            dx, dz = math.sin(ang) * ln / 2, math.cos(ang) * ln / 2
            pts = []
            for (px, pz) in [(x0 - dx, z0 - dz), (x0 + dx, z0 + dz)]:
                y = g.head_surface_y(px, pz)
                if y is None:
                    y = -0.11
                pts.append((px, pz, y))
            ox = math.cos(ang) * w / 2 * mx
            oz = -math.sin(ang) * w / 2
            quad = []
            for (px, pz, y) in (pts[0], pts[1]):
                for sgn in (1, -1):
                    qx, qz = px + ox * sgn, pz + oz * sgn
                    y2 = g.head_surface_y(qx, qz)
                    quad.append((qx, (y2 if y2 is not None else y) - p.get("lift", 0.004), qz))
            i = [mb.vert(q) for q in quad]
            mb.face([i[0], i[2], i[3], i[1]], p["slot"], (x0, 0.0, z0))


def part_patches(g, mb, p, name):
    """Hide patches / seams: small quads laid on the fitted surface (random but deterministic positions)."""
    rng = _rng(name + p.get("tag", "") + "patch")
    boxy = p.get("boxy", 3.0)
    n = p.get("count", 8)
    z0, z1 = g.val(p["z0"]), g.val(p["z1"])
    for k in range(n):
        th = rng.uniform(*p["theta"])
        zc = rng.uniform(z0, z1)
        w = rng.uniform(*p["w"])
        h = rng.uniform(*p["h"])
        corners = []
        for (dth, dz) in ((-1, 1), (-1, -1), (1, -1), (1, 1)):
            z = zc + dz * h / 2 + rng.uniform(-0.01, 0.01) * 0
            ext = g.section(p["fit"], z)
            cx, cy = (ext[0] + ext[1]) / 2, (ext[2] + ext[3]) / 2
            rx, ry = (ext[1] - ext[0]) / 2 + p.get("margin", 0.008), (ext[3] - ext[2]) / 2 + p.get("margin", 0.008)
            ang = th + dth * math.degrees(w / 2 / max(rx, ry))
            x, y = _ring_point(cx, cy, rx, ry, ang, boxy)
            corners.append((x, y, z))
        ids = [mb.vert(q) for q in corners]
        ext = g.section(p["fit"], zc)
        mb.face(ids, p["slot"], ((ext[0] + ext[1]) / 2, (ext[2] + ext[3]) / 2, zc))


PART_BUILDERS = {"shell": part_shell, "band": part_band, "box": part_box, "lathe": part_lathe, "prongs": part_prongs,
                 "necklace": part_necklace, "sash": part_sash, "stripes": part_stripes, "patches": part_patches}


def build_overlay_mesh(g, recipe):
    mb = MeshBuilder()
    parts = recipe.get("parts") or [recipe]
    for k, p in enumerate(parts):
        q = dict(p)
        q.setdefault("tag", str(k))
        q.setdefault("slot", recipe.get("slot"))
        mb.wspec = q.get("weights") or recipe.get("weights") or {"bones": ["Chest"], "rigid": True}
        PART_BUILDERS[q["type"]](g, mb, q, recipe["name"])
    return mb


def _closest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return a + ab * t


def vertex_weights(g, pos, spec):
    """spec: {"bones":[...], "rigid":bool, "falloff":p, "eps":m, "bias":{bone:factor}} -> [(bone, w)] (<= 4)."""
    bones = [b for b in spec["bones"] if b in g.segs]
    if not bones:
        raise RuntimeError("none of the bones %s exist on the armature" % spec["bones"])
    if spec.get("rigid") or len(bones) == 1:
        return [(bones[0], 1.0)]
    pw = spec.get("falloff", 3.0)
    eps = spec.get("eps", 0.03)
    bias = spec.get("bias", {})
    ws = []
    for b in bones:
        a, c = g.segs[b]
        d = (pos - _closest_on_segment(pos, a, c)).length
        ws.append((b, bias.get(b, 1.0) / ((d + eps) ** pw)))
    ws.sort(key=lambda x: -x[1])
    ws = ws[:contract.MAX_INFLUENCES]
    tot = sum(w for _, w in ws)
    ws = [(b, w / tot) for b, w in ws if w / tot > 0.01]
    tot = sum(w for _, w in ws)
    return [(b, w / tot) for b, w in ws]


def create_overlay_object(ctx, g, recipe, colours):
    name = contract.OVERLAY_PREFIX + recipe["name"]
    mb = build_overlay_mesh(g, recipe)
    me = bpy.data.meshes.new(name)
    me.from_pydata(mb.v, [], mb.f)
    used = sorted({s for s in mb.fs}, key=contract.ROLE_SLOTS.index)
    for s in used:
        if s not in colours:
            raise RuntimeError("overlay %s uses slot %s which the variant does not define" % (name, s))
        me.materials.append(slot_material(s, colours[s]))
    for pi, poly in enumerate(me.polygons):
        poly.material_index = used.index(mb.fs[pi])
        poly.use_smooth = False
    me.update()
    me.validate()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = ctx.root
    wcache = {}
    for vi, pos in enumerate(mb.v):
        spec = mb.vw[vi]
        key = json.dumps(spec, sort_keys=True)
        ws = vertex_weights(g, Vector(pos), spec)
        for b, w in ws:
            vg = obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)
            vg.add([vi], w, "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = ctx.armature
    obj["costume_overlay"] = recipe["name"]
    return obj, len(me.polygons), sum(len(p.vertices) - 2 for p in me.polygons)


# ---------------------------------------------------------------------------------------
def _assign_materials(ctx, g, resolved):
    """Replace the supplied RV_ materials of the base parts by Role_ slot materials."""
    slots = resolved["island_slots"]
    colours = resolved["slots"]
    summary = {}
    for pn, obj in ctx.parts.items():
        isls, co, ev, vi = g.part_isl[pn]
        me = obj.data
        known = [i for i in flat_islands(ctx.islands) if i["part"] == pn]
        used_slots = set()
        face_slot = {}
        for n, isl in enumerate(isls):
            iid = "%s.%02d" % (pn, n)
            if iid in slots:
                s = slots[iid]
            else:
                # island added after the map (WP-2 joint covers): take the slot of the nearest mapped island
                c = co[isl["verts"]].mean(axis=0)
                best = min(known, key=lambda k: (k["centre"][0] - c[0]) ** 2 + (k["centre"][1] - c[1]) ** 2 + (k["centre"][2] - c[2]) ** 2)
                s = slots[best["id"]]
            used_slots.add(s)
            for fi in isl["faces"]:
                face_slot[fi] = s
        order = [s for s in contract.ROLE_SLOTS if s in used_slots] + ([contract.HIDDEN_SLOT] if contract.HIDDEN_SLOT in used_slots else [])
        me.materials.clear()
        for s in order:
            me.materials.append(slot_material(s, colours.get(s, "000000")))
        for fi, poly in enumerate(me.polygons):
            poly.material_index = order.index(face_slot[fi])
        me.update()
        summary[pn] = order
    return summary


def _recipe_tris(obj):
    me = obj.data
    return sum(len(p.vertices) - 2 for p in me.polygons)


def remove_previous_overlays(ctx):
    for o in list(bpy.data.objects):
        if o.name.startswith(contract.OVERLAY_PREFIX):
            bpy.data.objects.remove(o, do_unlink=True)


def apply_costume(ctx, costume_name="prehistoric"):
    """Contract entry point. See module docstring."""
    if not HAVE_BPY:
        raise RuntimeError("apply_costume needs Blender")
    if ctx.armature is None:
        raise RuntimeError("apply_costume needs ctx.armature (run the rig step or stub_rig first)")
    import islands as islands_mod
    imap = ctx.islands
    if imap is None:
        raise RuntimeError("no island map for " + ctx.key)
    data = load_costume_data(ctx.key, imap)
    resolved = {n: resolve_variant(ctx.key, imap, n, data) for n in VARIANTS if n in data["variants"]}
    if costume_name not in resolved:
        raise KeyError("unknown costume %r (have %s)" % (costume_name, ",".join(resolved)))
    cur = resolved[costume_name]
    g = Geo(ctx)
    remove_previous_overlays(ctx)
    mats = _assign_materials(ctx, g, cur)
    overlays_made = {}
    for recipe in cur["overlay_recipes"]:
        obj, nf, nt = create_overlay_object(ctx, g, recipe, cur["slots"])
        overlays_made[obj.name] = {"tris": nt, "slots": [m.name for m in obj.data.materials]}
        ctx.overlay_objects = getattr(ctx, "overlay_objects", [])
        ctx.overlay_objects.append(obj)
    # triangle count of what is drawn
    vis = 0
    for pn, obj in ctx.parts.items():
        hidden_idx = [i for i, m in enumerate(obj.data.materials) if m.name == contract.ROLE_MATERIAL_PREFIX + contract.HIDDEN_SLOT]
        vis += sum(len(p.vertices) - 2 for p in obj.data.polygons if p.material_index not in hidden_idx)
    overlay_tris = sum(v["tris"] for v in overlays_made.values())
    section = {"applied": costume_name, "auto": bool(data.get("auto")), "variants": {}}
    for n, r in resolved.items():
        section["variants"][n] = {
            "description": r["description"], "slots": r["slots"], "slot_count": r["slot_count"],
            "island_slots": r["island_slots"], "hidden_islands": r["hidden_islands"], "overlays": r["overlays"],
            "props": r["props"], "merged_slots": r["merged"], "colour_error": r["colour_error"],
        }
    section["draw"] = {"visible_base_tris": vis, "overlay_tris": overlay_tris, "total_tris": vis + overlay_tris,
                       "overlays": overlays_made, "slot_materials": mats}
    ctx.set_section("costumes", section)
    log("%s/%s: slots=%d hidden=%d overlays=%d tris=%d (+%d overlay)" % (
        ctx.key, costume_name, cur["slot_count"], len(cur["hidden_islands"]), len(cur["overlays"]), vis + overlay_tris, overlay_tris))
    return section
