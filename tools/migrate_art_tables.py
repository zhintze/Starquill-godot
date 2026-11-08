#!/usr/bin/env python3
"""One-time art migration for the Character Studio design (2026-11-08).

  python3 tools/migrate_art_tables.py cleanup [--apply]  # 0000 species parts
  python3 tools/migrate_art_tables.py move    [--apply]  # one folder per type
  python3 tools/migrate_art_tables.py csv     [--apply]  # JSON -> Assets/Data/ArtTables/*.csv

Without --apply it only prints what it would do. Close the Unity Editor
first: files move with their .meta so Unity keeps every asset ID.
Run the steps in order: cleanup, move, csv.
"""
import argparse
import csv
import io
import json
import os
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
IMG = ROOT / "Assets/Resources/Images"
DATA = ROOT / "Assets/Resources/Data"
TABLES = ROOT / "Assets/Data/ArtTables"

PART_RX = re.compile(r"^([a-z]\d{2})-(\d{4})-(\d{3})\.png$")


def move_with_meta(src: Path, dst: Path, apply: bool):
    print(f"  move {src.relative_to(IMG)} -> {dst.relative_to(IMG)}")
    if apply:
        dst.parent.mkdir(parents=True, exist_ok=True)
        src.rename(dst)
        meta = src.with_name(src.name + ".meta")
        if meta.exists():
            meta.rename(dst.with_name(dst.name + ".meta"))


def delete_with_meta(p: Path, apply: bool):
    print(f"  delete {p.relative_to(IMG)}")
    if apply:
        p.unlink()
        meta = p.with_name(p.name + ".meta")
        if meta.exists():
            meta.unlink()


def same_pixels(a: Path, b: Path) -> bool:
    A = Image.open(a).convert("RGBA")
    B = Image.open(b).convert("RGBA")
    return A.size == B.size and A.tobytes() == B.tobytes()


def is_empty(p: Path) -> bool:
    return Image.open(p).convert("RGBA").getchannel("A").getbbox() is None


def cmd_cleanup(apply: bool):
    """0000 is not special after this: copies of 0001 and empty images are
    deleted; unique drawings are renumbered to the next free number."""
    folder = IMG / "species"
    groups = {}  # group -> {number: {layer: path}}
    for f in sorted(folder.iterdir()):
        m = PART_RX.match(f.name)
        if m:
            groups.setdefault(m[1], {}).setdefault(int(m[2]), {})[int(m[3])] = f

    for group, numbers in sorted(groups.items()):
        zero = numbers.get(0)
        if not zero:
            continue
        one = numbers.get(1, {})
        if one and set(zero) == set(one) and all(same_pixels(zero[L], one[L]) for L in zero):
            print(f"{group}: 0000 is a copy of 0001")
            for p in zero.values():
                delete_with_meta(p, apply)
        elif all(is_empty(p) for p in zero.values()):
            print(f"{group}: 0000 is empty")
            for p in zero.values():
                delete_with_meta(p, apply)
        else:
            new = max(numbers) + 1
            print(f"{group}: 0000 is a unique drawing, renumbered to {new:04d}")
            for layer, p in zero.items():
                move_with_meta(p, p.with_name(f"{group}-{new:04d}-{layer:03d}.png"), apply)


FLAT_RULES = [
    ("equipment", re.compile(r"^([a-z]{2}\d{2})-\d{4}-\d{3}\.png$"), lambda m: m[1]),
    ("weapons", re.compile(r"^(w\d{2})-\d{1,3}-\d{4}\.png$"), lambda m: m[1]),
    ("species", re.compile(r"^\d{4}-\d{3}\.png$"), lambda m: "body"),
    ("species", PART_RX, lambda m: m[1]),
]


def cmd_move(apply: bool):
    """Flat folders -> one folder per item type / part group (+ species/body)."""
    left = []
    for root in ("equipment", "weapons", "species"):
        for f in sorted((IMG / root).iterdir()):
            if not f.is_file() or f.suffix != ".png":
                continue
            for rule_root, rx, folder in FLAT_RULES:
                m = rx.match(f.name) if rule_root == root else None
                if m:
                    move_with_meta(f, IMG / root / folder(m) / f.name, apply)
                    break
            else:
                left.append(f.relative_to(IMG))
    for p in left:
        print(f"  left in place (misnamed, artist decides): {p}")


def ints(xs):
    return " ".join(str(x) for x in xs)


def num(v):
    return f"{v:g}"


def slug(name):
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


# Copied from DisplayDataRegistry.LoadLayerMappings (moves into species_parts.csv).
LAYER_MAPPINGS = {
    "f01": [84], "f02": [86], "f03": [85], "f04": [100], "f05": [122], "f06": [87],
    "h01": [92], "h02": [92, 128], "h03": [92, 154], "h04": [92, 128, 154],
    "d02": [86], "d04": [100], "d05": [122], "d06": [87], "g02": [86],
    "s01": [82], "s02": [84], "s03": [86], "s04": [87],
}

PART_FIELDS = [("backArm", "back arm"), ("legs", "legs"), ("body", "body"), ("head", "head"),
               ("frontArm", "front arm"), ("eyes", "eyes"), ("nose", "nose"), ("mouth", "mouth"),
               ("ears", "ears"), ("facialHair", "facial hair"), ("facialDetail", "facial detail")]


def write_csv(path: Path, header, rows, apply: bool):
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    w.writerows(rows)
    print(f"  {path.relative_to(ROOT)}: {len(rows)} rows")
    if apply:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(buf.getvalue(), encoding="utf-8", newline="")


def cmd_csv(apply: bool):
    eq = json.loads((DATA / "equipment.json").read_text())
    wp = json.loads((DATA / "weapons.json").read_text())
    sp = json.loads((DATA / "species.json").read_text())
    palettes = {k: list(v) for k, v in json.loads((DATA / "color_palettes.json").read_text()).items()}

    write_csv(TABLES / "equipment.csv",
              ["item_type", "description", "layer_codes", "hidden_layers", "color_variance_layers"],
              [[e["item_type"], e["description"], ints(e["layer_codes"]), ints(e["hidden_layers"]),
                ints(e["layer_color_variance"])] for e in eq], apply)

    write_csv(TABLES / "weapons.csv",
              ["item_type", "description", "hand_type", "modular", "layer_codes", "hidden_layers", "color_variance_layers"],
              [[w["item_type"], w["description"], w.get("hand_type", "one_handed"), "y" if w["modular"] else "n",
                ints(w["layer_codes"]), ints(w["hidden_layers"]), ints(w["layer_color_variance"])] for w in wp], apply)

    def palette_for(values, prefix, species):
        if not values:
            return ""
        if len(values) == 1 and values[0] in palettes:
            return values[0]
        name = f"{prefix}_{slug(species)}"
        palettes[name] = values
        return name

    species_rows, variance_rows, used_by = [], [], {}
    for s in sp:
        n = s["name"]
        for field, label in PART_FIELDS:
            tok = s.get(field, "")
            if tok and re.match(r"^[a-z]\d{2}", tok):
                used_by.setdefault(tok[:3], set()).add((label, n))
        for h in s.get("hair", []):
            used_by.setdefault(h[:3], set()).add(("hair", n))
        species_rows.append([
            n, num(s.get("x_scale", 1)), num(s.get("y_scale", 1)),
            num(s.get("head_y_offset", 0.35)), num(s.get("head_zoom", 0.5)),
            s.get("backArm", ""), s.get("legs", ""), s.get("body", ""), s.get("head", ""), s.get("frontArm", ""),
            s.get("eyes", ""), s.get("nose", ""), s.get("mouth", ""), s.get("ears", ""),
            s.get("facialHair", ""), s.get("facialDetail", ""),
            " ".join(s.get("hair", [])), " ".join(s.get("otherBodyParts", [])),
            palette_for(s.get("skin_color", []), "skin", n),
            palette_for(s.get("hair_color", []), "hair", n),
            palette_for(s.get("eyes_color", []), "eyes", n),
            palette_for(s.get("facialDetail_color", []), "facial_detail", n),
            " ".join(s.get("itemRestrictions", [])),
        ])
        for idx, vs in enumerate(s.get("skinVariance_sets", []), 1):
            pname = f"skinvar_{slug(n)}_{idx}"
            palettes[pname] = vs["hex_colors"]
            variance_rows.append([n, ints(vs["indices"]), pname])

    write_csv(TABLES / "species.csv",
              ["name", "x_scale", "y_scale", "head_y_offset", "head_zoom", "back_arm", "legs", "body", "head",
               "front_arm", "eyes", "nose", "mouth", "ears", "facial_hair", "facial_detail", "hair_groups",
               "other_body_parts", "skin_palette", "hair_palette", "eye_palette", "facial_detail_palette",
               "item_restrictions"], species_rows, apply)

    def describe(group):
        uses = used_by.get(group, set())
        labels = sorted({label for label, _ in uses})
        names = sorted({n for _, n in uses})
        who = ", ".join(names[:3]) + (f" +{len(names) - 3} more" if len(names) > 3 else "")
        return f"{' / '.join(labels) or 'unused'} ({who})" if names else "unused"

    write_csv(TABLES / "species_parts.csv", ["group", "description", "layers"],
              [[g, describe(g), ints(layers)] for g, layers in LAYER_MAPPINGS.items()], apply)

    write_csv(TABLES / "species_skin_variance.csv", ["species", "layers", "palette"], variance_rows, apply)

    write_csv(TABLES / "palettes.csv", ["palette", "hex"],
              [[name, "#" + h.lstrip("#").upper()] for name, colors in palettes.items() for h in colors], apply)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("step", choices=["cleanup", "move", "csv"])
    ap.add_argument("--apply", action="store_true", help="make the changes (default: print only)")
    a = ap.parse_args()
    {"cleanup": cmd_cleanup, "move": cmd_move, "csv": cmd_csv}[a.step](a.apply)
    if not a.apply:
        print("\n(dry run; add --apply to make these changes)")


if __name__ == "__main__":
    sys.exit(main())
