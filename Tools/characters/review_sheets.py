"""Contact sheets and videos from review_render.py output (plain python with Pillow, plus ffmpeg). Local only.

  python -I Tools/characters/review_sheets.py --out <build_cast --out> [--ffmpeg <ffmpeg.exe>]

Reads <out>/review/shots/*.json and writes
  <out>/review/sheets/<key>_poses.png          rest and key poses, front / three-quarter / side, both costumes
  <out>/review/sheets/<key>_<costume>_faces.png   expressions at dialogue size
  <out>/review/sheets/<key>_<costume>_phone.png   the head at its TRUE pixel size in an 844 x 390 frame (53 px and
                                                   27 px heads), enlarged 4x with nearest neighbour, plus one full frame
  <out>/review/sheets/<key>_<costume>_<set>.png   a strip per clip (8 frames), one sheet per clip set
  <out>/review/video/<name>.mp4                 every rendered sequence, 30 fps H.264
"""
import argparse
import glob
import json
import os
import shutil
import subprocess

from PIL import Image, ImageDraw, ImageFont

BG = (22, 22, 26)
FG = (230, 230, 230)


def font(size):
    for name in ("arial.ttf", "segoeui.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def text(img, xy, s, size=16, fill=FG):
    ImageDraw.Draw(img).text(xy, s, font=font(size), fill=fill)


def grid(rows, row_labels, col_labels, title, lw=150, cw=None, ch=None):
    """rows: list of lists of PIL images (same size per sheet)."""
    cw = cw or max(im.size[0] for r in rows for im in r if im)
    ch = ch or max(im.size[1] for r in rows for im in r if im)
    ncol = max(len(r) for r in rows)
    top = 56
    sheet = Image.new("RGB", (lw + ncol * cw, top + len(rows) * ch), BG)
    text(sheet, (8, 6), title, 20)
    for j, c in enumerate(col_labels):
        text(sheet, (lw + j * cw + 4, 32), c, 14)
    for i, r in enumerate(rows):
        text(sheet, (6, top + i * ch + 6), row_labels[i], 14)
        for j, im in enumerate(r):
            if im is not None:
                sheet.paste(im, (lw + j * cw, top + i * ch))
    return sheet


def load(p):
    return Image.open(p).convert("RGB")


def find_ffmpeg(arg):
    if arg and os.path.isfile(arg):
        return arg
    p = shutil.which("ffmpeg")
    if p:
        return p
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages")
    for d, _dirs, files in os.walk(root):
        if "ffmpeg.exe" in files:
            return os.path.join(d, "ffmpeg.exe")
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--ffmpeg", default="")
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    rv = os.path.join(a.out, "review")
    sd = os.path.join(rv, "sheets")
    os.makedirs(sd, exist_ok=True)
    shots = {}
    for p in sorted(glob.glob(os.path.join(rv, "shots", "*.json"))):
        shots[os.path.basename(p)[:-5]] = json.load(open(p, encoding="utf-8"))
    only = [k for k in a.only.split(",") if k]
    made = []
    # poses: both costumes side by side
    keys = sorted({v["key"] for n, v in shots.items() if n.startswith("poses_")})
    for key in keys:
        if only and key not in only:
            continue
        cos = [v for n, v in sorted(shots.items()) if n.startswith("poses_%s_" % key)]
        cos.sort(key=lambda v: 0 if v["costume"] == "prehistoric" else 1)
        names = [r["pose"] for r in cos[0]["rows"]]
        rows, labels = [], []
        for nm in names:
            row = []
            for v in cos:
                r = next((r for r in v["rows"] if r["pose"] == nm), None)
                row += [load(f) for f in r["files"]] if r else [None] * 3
            rows.append(row)
            labels.append(nm)
        cols = ["%s %s" % (v["costume"], w) for v in cos for w in ("front", "3/4", "side")]
        out = os.path.join(sd, "%s_poses.png" % key)
        g = grid(rows, labels, cols, "%s: rest and key poses (real rig, skin, costume; dim firelight)" % key, lw=110)
        g.save(out)
        made.append(out)
    # faces
    for n, v in sorted(shots.items()):
        if not n.startswith("faces_") or (only and v["key"] not in only):
            continue
        ims = [load(r["files"]["dialogue"]) for r in v["rows"]]
        g = grid([ims[:4], ims[4:]], ["", ""], [r["state"] for r in v["rows"][:4]], "%s / %s: expressions at dialogue distance (2.5 m, firelight)" % (v["key"], v["costume"]), lw=10)
        d = ImageDraw.Draw(g)
        for k, r in enumerate(v["rows"]):
            text(g, (10 + (k % 4) * ims[0].size[0] + 6, 56 + (k // 4) * ims[0].size[1] + 6), r["state"], 16)
        out = os.path.join(sd, "%s_%s_faces.png" % (v["key"], v["costume"]))
        g.save(out)
        made.append(out)
        # phone crops at true size, enlarged 4x nearest
        cells, labels = [], []
        for px in (53, 27):
            row = []
            for r in [r for r in v["phone"] if r["px"] == px]:
                im = load(r["file"])
                W, H = im.size
                cx, cy = r["centre"][0] * W, (1 - r["centre"][1]) * H
                box = int(px * 2.4)
                crop = im.crop((int(cx - box / 2), int(cy - box / 2), int(cx + box / 2), int(cy + box / 2)))
                row.append(crop.resize((box * 4, box * 4), Image.NEAREST))
            cells.append(row)
            labels.append("head %d px" % px)
        cw = max(im.size[0] for r in cells for im in r)
        chh = max(im.size[1] for r in cells for im in r)
        g = grid(cells, labels, [r["state"] for r in v["phone"] if r["px"] == 53],
                 "%s / %s: phone size (844 x 390 frame), true pixels shown 4x" % (v["key"], v["costume"]), lw=100, cw=cw, ch=chh)
        out = os.path.join(sd, "%s_%s_phone.png" % (v["key"], v["costume"]))
        g.save(out)
        made.append(out)
        full = next(r for r in v["phone"] if r["px"] == 53 and r["state"] == "neutral")
        load(full["file"]).save(os.path.join(sd, "%s_%s_phone_frame53.png" % (v["key"], v["costume"])))
    # strips
    for n, v in sorted(shots.items()):
        if not n.startswith("strips_") or (only and v["key"] not in only):
            continue
        by_set = {}
        for r in v["rows"]:
            by_set.setdefault(r["set"], []).append(r)
        for set_name, rows in by_set.items():
            ims = [[load(f) for f in r["files"]] for r in rows]
            labels = ["%s\n%d f" % (r["clip"], r["length"]) for r in rows]
            g = grid(ims, labels, ["f %s" % f for f in rows[0]["frames"]] if len(rows) == 1 else ["0", "1/7", "2/7", "3/7", "4/7", "5/7", "6/7", "end"],
                     "%s / %s: %s clips, 8 frames each (three-quarter view)" % (v["key"], v["costume"], set_name), lw=150)
            out = os.path.join(sd, "%s_%s_%s.png" % (v["key"], v["costume"], set_name))
            g.save(out)
            made.append(out)
    # videos
    ff = find_ffmpeg(a.ffmpeg)
    vd = os.path.join(rv, "video")
    os.makedirs(vd, exist_ok=True)
    for n, v in sorted(shots.items()):
        if not n.startswith("video_") or not ff:
            continue
        out = os.path.join(vd, v["name"] + ".mp4")
        cmd = [ff, "-y", "-loglevel", "error", "-framerate", str(v.get("fps", 30)), "-i", os.path.join(v["dir"], "%05d.png"),
               "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", out]
        subprocess.run(cmd, check=True)
        made.append(out)
    for m in made:
        print(m)


if __name__ == "__main__":
    main()
