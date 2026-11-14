"""Contact sheets for the WP-3 face previews (plain python with Pillow; run with `python -I`).

  python -I Tools/characters/face_contact_sheet.py --out <same --out as face_preview.py> [--only prince,mira]

Reads <out>/renders/<key>/shots.json and writes:
  <out>/sheets/<key>_closeups.png   rows = states, columns = close_front/close_3q x neutral/fire (dialogue framing)
  <out>/sheets/<key>_phone.png      rows = states, columns = phone shots: the head region at its TRUE pixel size
                                    on an 844 x 390 frame, enlarged with nearest-neighbour so single pixels show
  <out>/sheets/<key>_phone_frames.png  full 844 x 390 frames for neutral and surprised (what a phone shows)
  <out>/sheets/faces_compare.png    the characters side by side (neutral and each expression, front, both lights)
Local output only; never commit.
"""

import argparse
import json
import os

from PIL import Image, ImageDraw, ImageFont

STATES = ["neutral", "attentive", "concentrating", "surprised", "pleased", "pained", "determined", "blink",
          "look_away"]


def font(size):
    for name in ("arial.ttf", "DejaVuSans.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def label(img, text, size=18, fill=(235, 235, 235)):
    d = ImageDraw.Draw(img)
    d.text((6, 4), text, font=font(size), fill=fill)


def grid(cells, rows, cols, cw, ch, row_labels, col_labels, title):
    lw, th, top = 150, 40, 30
    sheet = Image.new("RGB", (lw + cols * cw, th + top + rows * ch), (24, 24, 28))
    d = ImageDraw.Draw(sheet)
    d.text((8, 6), title, font=font(22), fill=(255, 255, 255))
    for c, t in enumerate(col_labels):
        d.text((lw + c * cw + 6, th + 4), t, font=font(15), fill=(200, 200, 200))
    for r, t in enumerate(row_labels):
        d.text((8, th + top + r * ch + ch // 2 - 10), t, font=font(18), fill=(230, 230, 230))
    for (r, c), im in cells.items():
        sheet.paste(im, (lw + c * cw, th + top + r * ch))
    return sheet


def crop_head(path, box, scale, pad=1.6):
    im = Image.open(path).convert("RGB")
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    h = max(y1 - y0, x1 - x0) * pad
    l, t = int(round(cx - h / 2)), int(round(cy - h / 2))
    size = int(round(h))
    c = im.crop((l, t, l + size, t + size))
    return c.resize((size * scale, size * scale), Image.NEAREST), size


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    rroot = os.path.join(a.out, "renders")
    keys = [k for k in (a.only.split(",") if a.only else sorted(os.listdir(rroot)))
            if os.path.isfile(os.path.join(rroot, k, "shots.json"))]
    sdir = os.path.join(a.out, "sheets")
    os.makedirs(sdir, exist_ok=True)
    for key in keys:
        shots = json.load(open(os.path.join(rroot, key, "shots.json"), encoding="utf-8"))["shots"]
        by = {(s["state"], s["view"], s["light"]): s for s in shots}
        states = [s for s in STATES if any(k[0] == s for k in by)]
        # close-ups
        cols = [(v, l) for v in ("close_front", "close_3q") for l in ("neutral", "fire") if any(k[1:] == (v, l) for k in by)]
        cw = 320
        cells = {}
        for r, st in enumerate(states):
            for c, (v, l) in enumerate(cols):
                s = by.get((st, v, l))
                if s:
                    cells[(r, c)] = Image.open(os.path.join(rroot, key, s["file"])).convert("RGB").resize((cw, cw), Image.LANCZOS)
        if cells:
            g = grid(cells, len(states), len(cols), cw, cw, states, ["%s / %s light" % c for c in cols],
                     "%s - dialogue close-ups (eyes on camera = the person addressed)" % key)
            g.save(os.path.join(sdir, key + "_closeups.png"))
        # phone crops at true pixel size, enlarged
        pcols = [(v, l) for v in ("phone_dialogue", "phone_pushin", "phone_wide") for l in ("fire", "neutral")
                 if any(k[1:] == (v, l) for k in by)]
        cells, sizes = {}, {}
        ch = 260
        for r, st in enumerate(states):
            for c, (v, l) in enumerate(pcols):
                s = by.get((st, v, l))
                if not s:
                    continue
                hpx = s["head_box_px"][3] - s["head_box_px"][1]
                scale = max(1, int(round(ch / (hpx * 1.6))))
                im, size = crop_head(os.path.join(rroot, key, s["file"]), s["head_box_px"], scale)
                sizes[(v, l)] = round(hpx)
                canvas = Image.new("RGB", (ch, ch), (24, 24, 28))
                canvas.paste(im.crop((0, 0, min(ch, im.width), min(ch, im.height))), (0, 0))
                cells[(r, c)] = canvas
        if cells:
            g = grid(cells, len(states), len(pcols), ch, ch, states,
                     ["%s / %s (head %s px)" % (v.replace("phone_", ""), l, sizes.get((v, l), "?")) for v, l in pcols],
                     "%s - phone 844x390: head region at true pixels, nearest-neighbour enlarged" % key)
            g.save(os.path.join(sdir, key + "_phone.png"))
        # full frames
        frames = [by.get((st, "phone_dialogue", "fire")) for st in ("neutral", "surprised", "concentrating")]
        frames = [f for f in frames if f]
        if frames:
            sheet = Image.new("RGB", (844, 390 * len(frames)), (0, 0, 0))
            for i, f in enumerate(frames):
                im = Image.open(os.path.join(rroot, key, f["file"])).convert("RGB")
                label(im, "%s  %s  (full 844x390 frame)" % (key, f["state"]))
                sheet.paste(im, (0, 390 * i))
            sheet.save(os.path.join(sdir, key + "_phone_frames.png"))
    # comparison sheet across characters
    if len(keys) > 1:
        cw = 260
        cells = {}
        cols = [(k, l) for k in keys for l in ("neutral", "fire")]
        for c, (k, l) in enumerate(cols):
            shots = json.load(open(os.path.join(rroot, k, "shots.json"), encoding="utf-8"))["shots"]
            by = {(s["state"], s["view"], s["light"]): s for s in shots}
            for r, st in enumerate(STATES):
                s = by.get((st, "close_front", l))
                if s:
                    cells[(r, c)] = Image.open(os.path.join(rroot, k, s["file"])).convert("RGB").resize((cw, cw), Image.LANCZOS)
        g = grid(cells, len(STATES), len(cols), cw, cw, STATES, ["%s / %s" % c for c in cols],
                 "faces side by side (front close-up)")
        g.save(os.path.join(sdir, "faces_compare.png"))
        cells = {}
        for c, k in enumerate(keys):
            shots = json.load(open(os.path.join(rroot, k, "shots.json"), encoding="utf-8"))["shots"]
            by = {(s["state"], s["view"], s["light"]): s for s in shots}
            for r, st in enumerate(STATES):
                s = by.get((st, "close_3q", "neutral"))
                if s:
                    cells[(r, c)] = Image.open(os.path.join(rroot, k, s["file"])).convert("RGB").resize((cw, cw), Image.LANCZOS)
        if cells:
            g = grid(cells, len(STATES), len(keys), cw, cw, STATES, keys, "three-quarter, neutral light")
            g.save(os.path.join(sdir, "faces_compare_3q.png"))
    print("sheets in", sdir)


if __name__ == "__main__":
    main()
