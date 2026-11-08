#!/usr/bin/env python3
"""Interactive color-pool review tool for Starquill's key color families.

Serves a local page showing every "main" palette color
(Assets/Data/ArtTables/palettes.csv) grouped by its key
family (classifier + manual overrides), and lets you select swatches and
move them between pools -- including brand-new workshop pools. Saving
writes Assets/Resources/Data/color_family_overrides.json, which the game
applies on top of the classifier (ColorManager.LoadOverridesFromJson).
Pool names that are not ColorFamily enum members exclude those colors from
every key's drop pool until the pool is promoted in code.

Usage:
    python3 tools/color_pools.py          # serve on http://localhost:8787
    python3 tools/color_pools.py --port N

The page always reflects the latest palette, classifier rules below, and
saved overrides: refresh the browser after changing any of them.
"""

import argparse
import colorsys
import json
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PALETTES = ROOT / "Assets/Data/ArtTables/palettes.csv"
OVERRIDES = ROOT / "Assets/Resources/Data/color_family_overrides.json"

# Enum families in save-contract order. KEEP IN SYNC with
# Assets/Scripts/Core/ColorFamily.cs (append-only there, append-only here).
FAMILIES = ["Red", "Orange", "Brown", "Yellow", "Green", "Blue", "Purple",
            "Neutral", "White", "Black", "Pastel"]
NOT_MINTABLE = {"Pastel"}
ACCENT = {"Red": "#D94545", "Orange": "#E68C33", "Brown": "#8C6133",
          "Yellow": "#E0C740", "Green": "#4DBF59", "Blue": "#4D85E6",
          "Purple": "#A661D9", "Neutral": "#8C8C8C", "White": "#EBEBEB",
          "Black": "#3A3A40", "Pastel": "#D9C9B8"}
CUSTOM_ACCENT = "#D9B44A"


def classify(r, g, b):
    """Python port of ColorFamilyClassifier.Classify (v4). KEEP IN SYNC with
    Assets/Scripts/Core/ColorFamily.cs -- same thresholds, same order."""
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    deg = h * 360
    if v < 0.12 or (v < 0.20 and s < 0.55):
        return "Black"
    if s < 0.15:
        return "White" if v >= 0.75 else "Neutral"
    if s < 0.35 and v >= 0.70:
        return "Pastel"
    if deg < 15 or deg >= 345:
        return "Red"
    if deg < 70:
        if v < 0.65 or (s < 0.45 and v < 0.85):
            return "Brown"
        return "Orange" if deg < 45 else "Yellow"
    if deg < 170:
        return "Green"
    if deg < 260:
        return "Blue"
    return "Purple"


def build_state():
    import csv
    with PALETTES.open(newline="", encoding="utf-8-sig") as f:
        palette = [row["hex"].lstrip("#").upper() for row in csv.DictReader(f) if row["palette"] == "main"]
    overrides = {}
    if OVERRIDES.exists():
        overrides = {k.upper(): v for k, v in json.loads(OVERRIDES.read_text()).items()}
    colors = []
    seen = set()
    for hexs in palette:
        hexs = hexs.upper()
        if hexs in seen:            # palette contains duplicates; one swatch each
            continue
        seen.add(hexs)
        r, g, b = (int(hexs[i:i + 2], 16) / 255 for i in (0, 2, 4))
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        colors.append({
            "hex": hexs,
            "h": round(h * 360, 1), "s": round(s, 3), "v": round(v, 3),
            "auto": classify(r, g, b),
            "override": overrides.get(hexs),
        })
    return colors


PAGE = """<!doctype html>
<html><head><meta charset="utf-8"><title>Starquill Color Pools</title>
<style>
  :root { --bg:#17181c; --panel:#1e2025; --line:#2b2e35; --text:#e8e6e1;
          --dim:#98958d; --gold:#d9b44a; }
  * { box-sizing:border-box; }
  body { background:var(--bg); color:var(--text); margin:0;
         font-family:"Avenir Next","Segoe UI",system-ui,sans-serif;
         padding:40px 24px 140px; }
  .wrap { max-width:1060px; margin:0 auto; }
  .eyebrow { text-transform:uppercase; letter-spacing:.14em; font-size:12px;
             color:var(--gold); margin:0 0 8px; }
  h1 { font-size:26px; font-weight:600; margin:0 0 6px; }
  .sub { color:var(--dim); font-size:14px; max-width:70ch; line-height:1.55; margin:0 0 30px; }
  .fam { background:var(--panel); border:1px solid var(--line); border-radius:6px;
         padding:16px 18px 18px; margin-bottom:16px; }
  .fam header { display:flex; align-items:center; gap:10px; margin-bottom:12px; flex-wrap:wrap; }
  .chip { width:13px; height:13px; border-radius:3px; flex:none;
          box-shadow:0 0 0 1px rgba(255,255,255,.14); }
  h2 { font-size:15px; font-weight:600; margin:0; }
  .tag { font-size:10px; text-transform:uppercase; letter-spacing:.09em;
         color:var(--gold); border:1px solid var(--gold); border-radius:3px; padding:1px 6px; }
  .count { font-family:ui-monospace,Consolas,monospace; font-size:12px; color:var(--dim);
           font-variant-numeric:tabular-nums; margin-left:auto; }
  .grid { display:flex; flex-wrap:wrap; gap:3px; }
  .sw { width:26px; height:26px; border-radius:3px; position:relative; cursor:pointer;
        box-shadow:inset 0 0 0 1px rgba(255,255,255,.07); }
  .sw.sel { outline:3px solid var(--gold); outline-offset:1px; z-index:1; }
  .sw.ovr::after { content:""; position:absolute; right:2px; top:2px; width:6px; height:6px;
                   border-radius:50%; background:var(--gold);
                   box-shadow:0 0 0 1px rgba(0,0,0,.5); }
  .bar { position:fixed; left:0; right:0; bottom:0; background:#121317;
         border-top:1px solid var(--line); padding:12px 24px; z-index:10; }
  .bar .inner { max-width:1060px; margin:0 auto; display:flex; align-items:center;
                gap:8px; flex-wrap:wrap; }
  .bar .lbl { font-size:13px; color:var(--dim); margin-right:4px;
              font-variant-numeric:tabular-nums; min-width:130px; }
  button { background:var(--panel); color:var(--text); border:1px solid var(--line);
           border-radius:4px; padding:7px 12px; font-size:13px; cursor:pointer;
           font-family:inherit; }
  button:hover:not(:disabled) { border-color:var(--gold); }
  button:disabled { opacity:.35; cursor:default; }
  button.pool { padding-left:26px; position:relative; }
  button.pool::before { content:""; position:absolute; left:8px; top:50%;
        transform:translateY(-50%); width:11px; height:11px; border-radius:2px;
        background:var(--pc, #888); }
  button.save { background:var(--gold); color:#17181c; font-weight:600; border:none; }
  button.save.dirty { box-shadow:0 0 0 2px rgba(217,180,74,.4); }
  input[type=text] { background:var(--bg); border:1px solid var(--line); color:var(--text);
         border-radius:4px; padding:7px 10px; font-size:13px; width:130px; font-family:inherit; }
  input[type=text]:focus { outline:none; border-color:var(--gold); }
  .toast { position:fixed; bottom:76px; right:24px; background:var(--panel);
           border:1px solid var(--gold); border-radius:4px; padding:10px 16px;
           font-size:13px; opacity:0; transition:opacity .2s; pointer-events:none; }
  .toast.show { opacity:1; }
</style></head><body>
<div class="wrap">
  <p class="eyebrow">Starquill · local tool · tools/color_pools.py</p>
  <h1>Color pool review</h1>
  <p class="sub">Click swatches to select (click again to deselect), then send them to a pool
     with the bar below. Gold-dotted swatches carry a manual override; "Auto" returns the
     selection to the classifier. Pools outside the ColorFamily enum are workshop pools:
     saved, but excluded from key drops until promoted in code. Save writes
     <code>color_family_overrides.json</code>; reload the page to pick up palette or
     classifier changes.</p>
  <div id="pools"></div>
</div>
<div class="bar"><div class="inner">
  <span class="lbl" id="selCount">0 selected</span>
  <span id="poolBtns"></span>
  <input type="text" id="newPool" placeholder="new pool name" maxlength="20">
  <button id="mkPool">Create &amp; move</button>
  <button id="autoBtn">Auto</button>
  <button id="clearBtn">Clear</button>
  <button class="save" id="saveBtn">Save</button>
</div></div>
<div class="toast" id="toast"></div>
<script>
const COLORS = __DATA__;
const FAMILIES = __FAMILIES__;
const NOT_MINTABLE = __NOT_MINTABLE__;
const ACCENT = __ACCENT__;
const CUSTOM_ACCENT = "__CUSTOM_ACCENT__";

const sel = new Set();
let dirty = false;

function pool(c) { return c.override || c.auto; }
function accent(p) { return ACCENT[p] || CUSTOM_ACCENT; }

function poolNames() {
  const custom = new Set();
  COLORS.forEach(c => { const p = pool(c); if (!FAMILIES.includes(p)) custom.add(p); });
  return FAMILIES.concat([...custom].sort());
}

function sortKey(c, p) {
  const achro = ["White","Neutral","Black","Pastel"].includes(p);
  return achro ? [c.v, c.h] : [(p === "Red" ? (c.h - 345 + 360) % 360 : c.h), c.v];
}

function render() {
  const host = document.getElementById("pools");
  host.innerHTML = "";
  for (const p of poolNames()) {
    const members = COLORS.filter(c => pool(c) === p)
      .sort((a, b) => { const ka = sortKey(a, p), kb = sortKey(b, p);
                        return ka[0] - kb[0] || ka[1] - kb[1]; });
    if (!members.length && !FAMILIES.includes(p)) continue;
    const sec = document.createElement("section");
    sec.className = "fam";
    const known = FAMILIES.includes(p);
    const tag = !known ? '<span class="tag">workshop · not in game</span>'
              : NOT_MINTABLE.includes(p) ? '<span class="tag">not mintable</span>' : "";
    sec.innerHTML = `<header><span class="chip" style="background:${accent(p)}"></span>
      <h2>${p}${known && !NOT_MINTABLE.includes(p) ? " Key" : ""}</h2>${tag}
      <span class="count">${members.length}</span></header>`;
    const grid = document.createElement("div");
    grid.className = "grid";
    for (const c of members) {
      const d = document.createElement("div");
      d.className = "sw" + (sel.has(c.hex) ? " sel" : "") + (c.override ? " ovr" : "");
      d.style.background = "#" + c.hex;
      d.title = `#${c.hex}  H ${c.h}°  S ${c.s}  V ${c.v}  auto: ${c.auto}` +
                (c.override ? `  override: ${c.override}` : "");
      d.onclick = () => { sel.has(c.hex) ? sel.delete(c.hex) : sel.add(c.hex); render(); };
      grid.appendChild(d);
    }
    sec.appendChild(grid);
    host.appendChild(sec);
  }
  document.getElementById("selCount").textContent = sel.size + " selected";
  renderBar();
}

function renderBar() {
  const host = document.getElementById("poolBtns");
  host.innerHTML = "";
  for (const p of poolNames()) {
    const b = document.createElement("button");
    b.className = "pool";
    b.style.setProperty("--pc", accent(p));
    b.textContent = p;
    b.disabled = sel.size === 0;
    b.onclick = () => moveSelection(p);
    host.appendChild(b);
  }
  document.getElementById("autoBtn").disabled = sel.size === 0;
  document.getElementById("mkPool").disabled = sel.size === 0;
  document.getElementById("saveBtn").classList.toggle("dirty", dirty);
}

function moveSelection(p) {
  COLORS.forEach(c => {
    if (!sel.has(c.hex)) return;
    c.override = (p === null || p === c.auto) ? null : p;
  });
  sel.clear(); dirty = true; render();
}

document.getElementById("autoBtn").onclick = () => moveSelection(null);
document.getElementById("clearBtn").onclick = () => { sel.clear(); render(); };
document.getElementById("mkPool").onclick = () => {
  const name = document.getElementById("newPool").value.trim()
    .replace(/[^A-Za-z0-9_]/g, "");
  if (!name) { toast("Pool name needs letters or digits"); return; }
  document.getElementById("newPool").value = "";
  moveSelection(name);
};
document.getElementById("saveBtn").onclick = async () => {
  const overrides = {};
  COLORS.forEach(c => { if (c.override) overrides[c.hex] = c.override; });
  const res = await fetch("/save", { method: "POST", body: JSON.stringify(overrides) });
  if (res.ok) { dirty = false; renderBar(); toast("Saved " + Object.keys(overrides).length + " overrides"); }
  else toast("Save failed: " + res.status);
};
window.addEventListener("beforeunload", e => { if (dirty) e.preventDefault(); });

let toastTimer;
function toast(msg) {
  const t = document.getElementById("toast");
  t.textContent = msg; t.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove("show"), 2200);
}

render();
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path not in ("/", "/index.html"):
            self.send_error(404)
            return
        page = (PAGE
                .replace("__DATA__", json.dumps(build_state()))
                .replace("__FAMILIES__", json.dumps(FAMILIES))
                .replace("__NOT_MINTABLE__", json.dumps(sorted(NOT_MINTABLE)))
                .replace("__ACCENT__", json.dumps(ACCENT))
                .replace("__CUSTOM_ACCENT__", CUSTOM_ACCENT))
        body = page.encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path != "/save":
            self.send_error(404)
            return
        length = int(self.headers.get("Content-Length", 0))
        try:
            overrides = json.loads(self.rfile.read(length))
            assert isinstance(overrides, dict)
            clean = {k.upper(): str(v) for k, v in sorted(overrides.items())}
        except (json.JSONDecodeError, AssertionError):
            self.send_error(400, "invalid overrides payload")
            return
        OVERRIDES.write_text(json.dumps(clean, indent=2) + "\n")
        self.send_response(200)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, fmt, *args):  # quiet
        pass


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--port", type=int, default=8787)
    ap.add_argument("--no-browser", action="store_true")
    args = ap.parse_args()
    server = HTTPServer(("127.0.0.1", args.port), Handler)
    url = f"http://localhost:{args.port}/"
    print(f"Color pool review: {url}  (Ctrl+C to stop)")
    if not args.no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
