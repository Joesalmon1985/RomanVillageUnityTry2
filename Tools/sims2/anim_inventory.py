#!/usr/bin/env python3
"""Inventory every Sims 2 ANIM resource (type 0xFB00791E) by name, read-only.

Reads each *.package (DBPF 1.x) index, and for each ANIM entry decompresses (QFS/RefPack) only the first few
hundred bytes to recover the resource name. Never writes to the game folder.

Output (git-ignored, EA metadata kept local): <out>/anim_inventory.csv  (name, prefix, packages)
Usage:
  python -I Tools/sims2/anim_inventory.py --game-dir <.../game> --out LocalData/m0
  python -I Tools/sims2/anim_inventory.py --game-dir <.../game> --out LocalData/m0 --grep "walk|carry"
"""
import argparse, csv, os, re, struct, sys, time
from collections import defaultdict

TYPE_ANIM = 0xFB00791E
NAME_RE = re.compile(rb"[A-Za-z0-9_\-\.]{3,120}_anim", re.I)
HEAD_BYTES = 1024      # compressed bytes read per resource
NAME_WINDOW = 400      # decompressed bytes searched for the name


def read_index(f):
    f.seek(0)
    h = f.read(96)
    if len(h) < 96 or h[:4] != b"DBPF":
        return None
    major, minor = struct.unpack_from("<II", h, 4)
    if major != 1:
        return None
    count, off, size = struct.unpack_from("<III", h, 36)
    idx_minor = struct.unpack_from("<I", h, 60)[0]
    if count == 0 or size == 0:
        return []
    f.seek(off)
    raw = f.read(size)
    ent = size // count
    if ent not in (20, 24):
        ent = 24 if idx_minor == 2 else 20
    out = []
    for i in range(count):
        e = raw[i * ent:(i + 1) * ent]
        if len(e) < ent:
            break
        if ent == 20:
            t, g, inst, o, s = struct.unpack("<5I", e)
        else:
            t, g, inst_hi, inst, o, s = struct.unpack("<6I", e)
        out.append((t, g, inst, o, s))
    return out


def qfs_prefix(data, limit):
    """Decompress the start of a RefPack stream (after the 4-byte size prefix); stop once `limit` bytes exist."""
    flags = data[4]
    p = 6
    p += 4 if flags & 0x80 else 3
    if flags & 0x01:
        p += 4 if flags & 0x80 else 3
    out = bytearray()
    n = len(data)
    while p < n and len(out) < limit:
        cc = data[p]; p += 1
        if cc < 0x80:
            if p >= n: break
            b1 = data[p]; p += 1
            plain = cc & 3
            copy = ((cc & 0x1C) >> 2) + 3
            offs = ((cc & 0x60) << 3) + b1 + 1
        elif cc < 0xC0:
            if p + 1 >= n: break
            b1, b2 = data[p], data[p + 1]; p += 2
            plain = (b1 >> 6) & 3
            copy = (cc & 0x3F) + 4
            offs = ((b1 & 0x3F) << 8) + b2 + 1
        elif cc < 0xE0:
            if p + 2 >= n: break
            b1, b2, b3 = data[p], data[p + 1], data[p + 2]; p += 3
            plain = cc & 3
            copy = ((cc & 0x0C) << 6) + b3 + 5
            offs = ((cc & 0x10) << 12) + (b1 << 8) + b2 + 1
        elif cc < 0xFC:
            plain = ((cc & 0x1F) << 2) + 4
            copy = 0
            offs = 0
        else:
            plain = cc & 3
            out += data[p:p + plain]
            break
        out += data[p:p + plain]; p += plain
        if copy:
            start = len(out) - offs
            if start < 0:
                break
            for _ in range(copy):
                out.append(out[start]); start += 1
    return bytes(out)


def entry_name(f, off, size):
    f.seek(off)
    d = f.read(min(size, HEAD_BYTES))
    if len(d) >= 6 and d[4:6] == b"\x10\xfb" or (len(d) >= 6 and d[5] == 0xFB and d[4] in (0x10, 0x11, 0x90, 0x91)):
        body = qfs_prefix(d, NAME_WINDOW)
    else:
        body = d[:NAME_WINDOW]
    m = NAME_RE.search(body)
    return m.group(0).decode("ascii") if m else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game-dir", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--grep", help="regex; print matching names (case-insensitive) after the scan")
    a = ap.parse_args()

    t0 = time.time()
    names = defaultdict(set)      # name -> set(package relative path)
    unnamed = 0
    pkgs = 0
    for root, _, files in os.walk(a.game_dir):
        for fn in files:
            if not fn.lower().endswith(".package"):
                continue
            path = os.path.join(root, fn)
            try:
                with open(path, "rb") as f:
                    idx = read_index(f)
                    if not idx:
                        continue
                    anims = [e for e in idx if e[0] == TYPE_ANIM]
                    if not anims:
                        continue
                    pkgs += 1
                    rel = os.path.relpath(path, a.game_dir).replace("\\", "/")
                    for _, _, _, off, size in anims:
                        n = entry_name(f, off, size)
                        if n:
                            names[n].add(rel)
                        else:
                            unnamed += 1
            except OSError as ex:
                print("skip", path, ex, file=sys.stderr)

    os.makedirs(a.out, exist_ok=True)
    csv_path = os.path.join(a.out, "anim_inventory.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["name", "prefix", "packages"])
        for n in sorted(names, key=str.lower):
            w.writerow([n, n.split("-")[0], ";".join(sorted(names[n]))])

    print(f"packages with ANIM: {pkgs}; unique named ANIMs: {len(names)}; unnamed: {unnamed}; {time.time()-t0:.1f}s")
    print(f"wrote {csv_path}")
    pre = defaultdict(int)
    for n in names:
        pre[n.split("-")[0]] += 1
    print("by prefix:", ", ".join(f"{k}={v}" for k, v in sorted(pre.items(), key=lambda kv: -kv[1])[:15]))

    known = ["a-pose-neutral-stand_anim", "a-idle-neutral-armsSide-wtC-breatheLoop_anim", "a-react-wave-fullBody_anim",
             "a-clapSmall-loop-standing_anim", "a-soc-greet-nod_anim", "o-door-open-CW_anim"]
    print("known-name check:", {k: (k in names) for k in known})

    if a.grep:
        rx = re.compile(a.grep, re.I)
        hits = sorted((n for n in names if rx.search(n)), key=str.lower)
        print(f"\n{len(hits)} names match /{a.grep}/")
        for n in hits[:400]:
            print(" ", n)


if __name__ == "__main__":
    main()
