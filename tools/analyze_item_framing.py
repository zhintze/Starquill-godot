#!/usr/bin/env python3
"""Compute per-category icon framing for equipment sprites.

Every item image is a full 200x200 character-sized canvas with the item drawn
where it sits on the body (hats near the top, boots at the bottom). Displayed
raw in an icon, each category lands at a different size and position. This
script measures the opaque-pixel geometry of every sprite, aggregates per
category, and emits a normalized square crop rect (Unity RawImage.uvRect,
bottom-left origin) that shows the category's items as large as possible while
keeping >=95% of items fully inside the crop.

Output: per-category stats table + generated C# for ItemIconFraming.cs.
Rerun when item art is added: python3 tools/analyze_item_framing.py
"""

import glob
import os
import re
import sys
from collections import defaultdict

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EQUIP_DIR = os.path.join(ROOT, 'Assets/Resources/Images/equipment')
WEAPON_DIR = os.path.join(ROOT, 'Assets/Resources/Images/weapons')

ALPHA_THRESHOLD = 16   # ignore near-transparent halo pixels
PADDING = 0.05         # fraction of crop size kept as margin inside the icon
COVERAGE = 0.95        # fraction of items that must fit fully inside the crop


def category_of(path):
    name = os.path.basename(path)
    m = re.match(r'(w\d{2})', name)
    if m:
        return m.group(1)
    m = re.match(r'([a-z]{2})\d', name)
    return m.group(1) if m else None


def measure(path):
    """Return (cx, cy, half_w, half_h, bbox) in normalized top-left coords, or None if empty."""
    im = Image.open(path).convert('RGBA')
    w, h = im.size
    alpha = im.getchannel('A')
    # bbox of pixels above threshold
    mask = alpha.point(lambda a: 255 if a > ALPHA_THRESHOLD else 0)
    bbox = mask.getbbox()
    if bbox is None:
        return None
    x0, y0, x1, y1 = bbox
    # alpha-weighted centroid
    px = alpha.load()
    total = cx = cy = 0
    for y in range(y0, y1):
        for x in range(x0, x1):
            a = px[x, y]
            if a > ALPHA_THRESHOLD:
                total += a
                cx += x * a
                cy += y * a
    if total == 0:
        return None
    return (cx / total / w, cy / total / h,
            (x1 - x0) / 2 / w, (y1 - y0) / 2 / h,
            (x0 / w, y0 / h, x1 / w, y1 / h))


def main():
    per_cat = defaultdict(list)
    files = glob.glob(os.path.join(EQUIP_DIR, '*', '*.png')) + \
        glob.glob(os.path.join(WEAPON_DIR, '*', '*.png'))
    for path in sorted(files):
        cat = category_of(path)
        if cat is None:
            continue
        m = measure(path)
        if m is not None:
            per_cat[cat].append(m)

    print(f"{'cat':>4} {'n':>5} {'center(x,y)':>14} {'mean half-ext':>13} "
          f"{'p95 need':>9} {'crop(cx,cy,size)':>20}")

    results = {}
    for cat in sorted(per_cat):
        ms = per_cat[cat]
        n = len(ms)
        mean_cx = sum(m[0] for m in ms) / n
        mean_cy = sum(m[1] for m in ms) / n
        mean_hw = sum(m[2] for m in ms) / n
        mean_hh = sum(m[3] for m in ms) / n

        # Required half-extent from the CATEGORY center so an item fits fully:
        # per item, the max distance from category center to any bbox edge.
        needs = []
        for _, _, _, _, (x0, y0, x1, y1) in ms:
            needs.append(max(mean_cx - x0, x1 - mean_cx, mean_cy - y0, y1 - mean_cy))
        needs.sort()
        p95 = needs[min(n - 1, int(COVERAGE * n))]

        half = min(0.5, p95 * (1 + PADDING))
        # shift center so a square crop stays inside [0,1] without shrinking
        cx = min(max(mean_cx, half), 1 - half)
        cy = min(max(mean_cy, half), 1 - half)
        size = half * 2
        results[cat] = (cx, cy, size)
        print(f"{cat:>4} {n:>5} ({mean_cx:.3f},{mean_cy:.3f}) "
              f"({mean_hw:.3f},{mean_hh:.3f}) {p95:>9.3f} "
              f"({cx:.3f},{cy:.3f},{size:.3f})")

    print("\n// ---- generated C# (top-left centers converted to Unity uvRect, bottom-left origin) ----")
    print("        private static readonly Dictionary<string, Rect> Frames = new()\n        {")
    for cat, (cx, cy, size) in results.items():
        # image cy is from top; uv v origin is bottom
        u = cx - size / 2
        v = (1 - cy) - size / 2
        print(f'            {{ "{cat}", new Rect({u:.4f}f, {v:.4f}f, {size:.4f}f, {size:.4f}f) }},')
    print("        };")


if __name__ == '__main__':
    sys.exit(main())
