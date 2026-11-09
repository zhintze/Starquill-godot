#!/usr/bin/env python3
"""One-time migration (2026-11-08, docs/plans/2026-11-08-species-extra-parts-plan.md):
species.csv other_body_parts + species_skin_variance.csv -> species_extra_parts.csv.

Each old layer rule (species, layers, palette) becomes one row per species part
drawn on those layers; rules covering several parts share a color_group, so they
still roll one color. Rows keep the old rule order (saved characters keep their
look, plan E3). Extras no rule covered become skin-colored rows. Reports any rule
that colored only some layers of a part, and layers with nothing on them.
Run from the repo root: python3 tools/migrate_species_extra_parts.py
"""
import csv, io, os, re, sys

T = "Assets/Data/ArtTables"
FIELDS = ["back_arm", "legs", "body", "head", "front_arm", "eyes", "nose", "mouth", "ears", "facial_hair", "facial_detail"]

def read(name):
    with open(os.path.join(T, name), newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))

def write(name, header, rows):
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    for r in rows: w.writerow([r[h] for h in header])
    with open(os.path.join(T, name), "w", newline="", encoding="utf-8") as f:
        f.write(buf.getvalue())

groups = {r["group"]: [int(x) for x in r["layers"].split()] for r in read("species_parts.csv")}

def layers(token):
    if re.fullmatch(r"\d{4}-\d{3}", token): return [int(token[-3:])]
    if re.fullmatch(r"[a-z]\d{2}", token): return groups.get(token, [])
    m = re.fullmatch(r"[a-z]\d{2}-\d{4}-(\d{3})", token)
    return [int(m.group(1))] if m else []

species_rows = read("species.csv")
with open(os.path.join(T, "species.csv"), newline="", encoding="utf-8") as f:
    species_header = next(csv.reader(f))
variance = read("species_skin_variance.csv")
out, report = [], []

for s in species_rows:
    name = s["name"]
    extras = s["other_body_parts"].split()
    parts = [(f, s[f]) for f in FIELDS if s[f]] + [(e, e) for e in extras]
    used = set()
    for v in [v for v in variance if v["species"] == name]:
        rule = [int(x) for x in v["layers"].split()]
        hit = [(p, tok) for p, tok in parts if set(layers(tok)) & set(rule)]
        for p, tok in hit:
            partial = set(layers(tok)) - set(rule)
            if partial: report.append(f"PARTIAL {name}: rule {rule} colors {p} ({tok}) but not its layers {sorted(partial)}")
        for hg in s["hair_groups"].split():
            if set(layers(hg)) & set(rule): report.append(f"HAIR {name}: rule {rule} touches hair group {hg} (hair never took layer rules)")
        covered = set(l for _, tok in hit for l in layers(tok))
        for l in sorted(set(rule) - covered): report.append(f"EMPTY {name}: rule layer {l} has no part (dropped)")
        group = v["palette"].replace("skinvar_", "") if len(hit) > 1 else ""
        for p, tok in hit:
            if p in used: report.append(f"TWICE {name}: {p} in two rules; first kept"); continue
            used.add(p)
            out.append({"species": name, "part": p, "color": v["palette"], "color_group": group, "chance": ""})
    for e in extras:
        if e not in used:
            out.append({"species": name, "part": e, "color": "skin", "color_group": "", "chance": ""})

write("species_extra_parts.csv", ["species", "part", "color", "color_group", "chance"], out)
new_header = [h for h in species_header if h != "other_body_parts"]
write("species.csv", new_header, species_rows)
os.remove(os.path.join(T, "species_skin_variance.csv"))
if os.path.exists(os.path.join(T, "species_skin_variance.csv.meta")):
    os.remove(os.path.join(T, "species_skin_variance.csv.meta"))
print(f"{len(out)} rows written")
print("\n".join(report) if report else "every rule maps to whole parts")
