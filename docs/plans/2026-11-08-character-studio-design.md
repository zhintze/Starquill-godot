# Character Studio: Design

**Date:** 2026-11-08
**Status:** Approved (brainstorm with Zach, 2026-11-08). Phase A (art tables) implemented 2026-11-08 per `2026-11-08-character-studio-plan.md` Tasks 1-17; Phase B (studio app) not started.
**Branch:** `unity-idle-clicker`

## Goal

A standalone character generator for the artist, built from this Unity project, that exports characters as transparent PNGs for promo content and comic pages. It reuses the game's own character and equipment classes, so an exported character is exactly what the game would draw. It also becomes the artist's tool for adding new art and editing the art data, pushed to GitHub on an `art` branch.

Two parts:

1. **Art data moves to CSV.** One editable copy of the art rules, readable by the artist in a spreadsheet and by us in git diffs. The game bakes it at build time.
2. **Character Studio app** (`StarquillCharacters.exe`, Windows; Linux build for us). Bulk screen, Single screen, Library panel.

## Background: findings that shaped this design

- **A predecessor exists.** The Godot project had this tool: `godot-archive/scenes/CharacterRandomizer.tscn` + `scripts/util/character_randomizer_control.gd` (8 characters, species dropdown, Randomize, Equip Random, item search applied to all, PNG export at a scale, per-slot chance sliders). The artist's feature list is that tool plus new requests. The studio keeps its layout and button names (Jakob's Law: the artist already knows it).
- **The CSV path was dropped silently, not by decision.** In Godot, `Layer Export - *.csv` were the master and `scripts/tools/csv_to_json_converter.gd` produced JSON. The Unity migration (`docs/plans/archive/2026-02-13-paper-doll-display-design.md` §11) copied only the generated JSON. Since then the JSON in `Assets/Resources/Data/` has been hand-edited (head crop fields 2026-02-15, weapons fix 2026-07-01). The Godot CSVs are stale.
- **Hand-typed counts drifted from the art.** The game picks variants `1..amount`, so files above the typed count never appear:

  | Item | JSON | Files on disk |
  |---|---|---|
  | fe02 boots | 69 | 71 |
  | hd08 helmet with detail | 34 | 35 |
  | hd10 headband | 11 | 14 |
  | tr14 hoodless cloak | 15 | 17 |

  Weapons match disk.
- **`0000` species parts.** Face/hair/skeleton parts on disk start at `0000`, but both the Godot and Unity code pick from `0001`. The `0000` files are: exact copies of `0001` (h01-h04 every layer, f04); different, never-shown drawings (f01, f02, f03, f05, f06, s01, s02, s03); and one empty image (s04). Equipment and weapons have no `0000`.
- **Misnamed files exist.** `Artboard 1hd10-0001-094.png` (same outline as `hd10-0001-094.png`, different gray shading in 312 pixels) and `Artboard 1hd10-0004-094.png` (a *different* drawing than `hd10-0004-094.png`). Both await the artist.
- **One rule lives only in C#.** Which layers each face/hair group draws on (`f01` → 84, `h04` → 92/128/154, ...) is hardcoded in `DisplayDataRegistry.LoadLayerMappings`.
- **Runtime cost of the JSON is small** (5 files, 47 KB, parsed at startup). Baking is chosen because a build step exists anyway, not because parsing is slow.

## Decisions

| # | Decision |
|---|---|
| D1 | The studio is a Unity standalone app in this project, reusing `DisplayBuilder`, `CharacterDisplay`, `ColorManager`, `SpeciesInstanceData`, `EquipmentCatalog`, `EquipmentFactory`. Not a Python port. |
| D2 | **CSV is the only stored copy** of the art rules, in `Assets/Data/ArtTables/`. Nothing else holds the same facts. |
| D3 | The CSVs are **baked into one ScriptableObject** (`ArtTables.asset`) before every build and on CSV/art import in the Editor. The asset is generated and gitignored. The five art JSON files and their text parsers are deleted. |
| D4 | **No counts are stored.** The converter records the variant numbers that exist on disk. Gaps are fine; a missing number is never rolled. |
| D5 | `0000` is not special. One-time cleanup: delete `0000` files identical to `0001` and empty ones; renumber unique `0000` drawings to the next free number in their group. |
| D6 | Problems in art files are warnings (Console on import, build log, studio Warnings list). Problems in CSV cells fail the game build. |
| D7 | Art folders: one folder per item type / part group, named by code only, in the game and the studio alike. |
| D8 | The artist works in a clone of the repo (GitHub Desktop). The studio reads and writes the repo's art and CSVs directly and writes `.meta` files for new PNGs. |
| D9 | The artist pushes to an **`art` branch**. Zach/Claude merge it into `unity-idle-clicker` locally after checking in Unity. No pull requests. |
| D10 | The `.exe` is distributed as a GitHub Release download, never committed. |
| D11 | Export scale is chosen per export (1×/2×/4×/8× of the 200px canvas). |
| D12 | Out of scope: `abilities.json`, `names.json`, `quest_zones.json` (game data, stay JSON); `color_family_overrides.json` (owned by `tools/color_pools.py`). |

## Changes since approval (2026-11-08)

Built in Phase A and Phase B Stages B1-B5. Decisions made while building are P1-P34 in `2026-11-08-character-studio-phase-b-plan.md` and E1-E5 in `2026-11-08-species-extra-parts-plan.md`. Where they differ from this document:

- **Species extra parts** are a seventh concept with their own table, `species_extra_parts.csv`, keyed by part (not layer): extras such as wings, tails, horns and the angel halo, plus species parts with their own palette. Replaces `species.csv` `other_body_parts` and `species_skin_variance.csv`.
- **Colors are palette only** (Zach): every picker offers just the palette the game rolls from (species skin palette; hair/eye/facial detail palette or `main`; `main` for gear; a part's own palette). No free color picker.
- **Dropdowns** are the studio's own `PickList`: tall, fast-scrolling, searchable.
- **Species size** stays off in the game and the studio (`docs/roadmap.md` backlog).
- The Library panel also edits `species_extra_parts.csv` (key = species + part).

## 1. Art data

### Folder layout (game and studio)

```
Assets/Resources/Images/
  equipment/<item_type>/<item_type>-<NNNN>-<LLL>.png     e.g. equipment/hd01/hd01-0001-130.png
  weapons/<item_type>/<item_type>-<LLL>-<NNNN>.png       e.g. weapons/w01/w01-164-0001.png
  species/body/<NNNN>-<LLL>.png                          static body parts, e.g. 0001-016.png
  species/<group>/<group>-<NNNN>-<LLL>.png               face/hair/skeleton groups, e.g. species/f01/f01-0001-084.png
Assets/Data/ArtTables/
  equipment.csv  weapons.csv  species.csv  species_parts.csv  species_extra_parts.csv  palettes.csv
```

`ImageToken` path builders gain the folder segment. A one-time script moves every PNG together with its `.meta` (GUIDs survive).

### Tables

One row per thing. Lists inside a cell are space-separated (`10 134 130`). Blank means none/default. Column order is fixed; writers preserve row order so an edit changes one line.

| Table | Columns |
|---|---|
| `equipment.csv` | `item_type, description, layer_codes, hidden_layers, color_variance_layers` (slot comes from the prefix via `EquipmentCatalog.SlotForPrefix`; not stored twice) |
| `weapons.csv` | `item_type, description, hand_type, modular, layer_codes, hidden_layers, color_variance_layers` |
| `species.csv` | `name, x_scale, y_scale, head_y_offset, head_zoom, back_arm, legs, body, head, front_arm, eyes, nose, mouth, ears, facial_hair, facial_detail, hair_groups, skin_palette, hair_palette, eye_palette, facial_detail_palette, item_restrictions` |
| `species_parts.csv` | `group, layers, description` (replaces the hardcoded `LoadLayerMappings`) |
| `species_extra_parts.csv` | `species, part, color, color_group, chance` (2026-11-08; replaced `other_body_parts` and the first port's `species_skin_variance.csv`, which kept Godot's layer-keyed `skinVariance_sets`; see `2026-11-08-species-extra-parts-plan.md`) |
| `palettes.csv` | `palette, hex` (one row per color; row order is palette order). Species with inline skin hex lists get named palettes (e.g. `skin_naga`). |

### Converter and bake

- Plain C# in a low assembly (no `UnityEngine` file access in the core logic, so it is testable): `CsvTable` reader/writer, `ArtTablesConverter` (CSV text + list of file paths → `ArtTables` data + warnings).
- `ArtTables` holds, per item type / weapon part / part group, the **set of variant numbers found on disk**. A variant of a multi-layer item counts only when every layer file exists.
- Game side: `EquipmentCatalog`, `DisplayDataRegistry`, `ColorManager` load from `ArtTables`. `EquipmentFactory.rng.Next(1, Amount+1)` and `DisplayDataRegistry.PickModularImageNum` become picks from the variant set. Hair-group weighting uses the set size.
- Editor: an `AssetPostprocessor` re-bakes when a CSV or art PNG changes and applies sprite import settings (100 PPU, center pivot, bilinear, no compression) to new PNGs, replacing the manual Fix Sprite menu. An `IPreprocessBuildWithReport` hook bakes before every build and fails on CSV errors.
- Studio side: the same converter runs in memory on launch and on Rescan.

### Warnings

Each names the file/row and the problem:

- incomplete variant (missing layer file)
- file name that matches no pattern
- pixel-identical variants in one group; empty image
- folder with no CSV row; CSV row with no folder
- bad CSV cell (error: fails game build; studio skips the row)
- preset references art that no longer exists

### One-time migration (in order)

1. Generate the CSVs from the **current JSON** (newer than the Godot CSVs) plus the hardcoded layer mappings.
2. `0000` cleanup per D5.
3. Resolve the two `Artboard 1hd10-*` files with the artist.
4. Move images into per-type folders with their `.meta` files.
5. Switch loaders to `ArtTables`; delete the five JSON files; gitignore the generated asset.

Result: the 8 drawings hidden by stale counts and the 8 unique `0000` drawings start appearing in game loot and characters (intended; a small loot-pool change).

## 2. Character Studio app

- `Assets/Scenes/CharacterStudio.unity` + `Assets/Scripts/Studio/` (`Starquill.Studio` assembly → Display, Equipment, Characters). Never included in the game build.
- Separate build profiles: Windows (Mono) and Linux standalone. **Requires adding "Windows Build Support (Mono)" to 6000.3.8f1 in Unity Hub.**
- `ImageResolver` gains a disk source (`Texture2D.LoadImage` + `Sprite.Create` with the import settings above). The game keeps `Resources.Load`.
- First launch asks for the repo clone folder and remembers it.

### Bulk screen (ported from the Godot randomizer)

- Top bar: **Count** (8/16/32/64, grid scrolls), **Species** (multi-select; each character rolls from the checked ones), **Randomize All** (body parts, colors, gear), **Equip Random** (strip and reroll gear only), **Settings**, **Export**.
- **Search**: by `hd01-0001`, a full file name, or a description word. The pick becomes a **Forced items** chip applied to every character, replacing that slot, surviving Equip Random; ✕ removes it. Slot-limit and two-handed warnings as in Godot.
- Cards: checkerboard preview, **Lock** (ignored by Randomize All / Equip Random), reroll this card, species dropdown, skin color dropdown, **Edit** (opens in Single).
- **Settings**: chance per slot (incl. main hand, off hand), facial hair chance, facial detail chance, weight per item type within each slot. Saved as named presets.

### Single screen

- Large preview with background toggle (checkerboard / white / dark); Body and Gear tabs.
- **Body**: species, skin color (species palette + free picker), hair group/variant/color, eye color, each face part with a variant picker and None where allowed.
- **Gear**: per slot (Head, Torso, Arms, Legs, Feet, Main Hand, Off Hand, Misc 1-4): item type dropdown filtered to the slot, thumbnail variant picker built from existing numbers only, base and second-color pickers (game swatches + free picker), weapon part pickers, row reroll, Lock, ✕. A note when an item hides a body part.
- Undo/redo (Ctrl+Z / Ctrl+Y) across all changes including rerolls.

### Presets

JSON files in a `presets/` folder next to the exe (studio-only data, not art rules): **Character**, **Cast** (whole Bulk grid incl. locks), **Random settings**. They store choices only (item type, variant number, hex colors). Missing art loads as an empty slot plus a warning.

### Export

All or checked characters into a chosen folder; scale 1×/2×/4×/8×; optional trim to the character with padding; transparent background. Names: `<preset or species>_<NN>_<date>.png`.

### Library panel

- **New item type**: pick slot; next free code suggested; description, layer codes, hidden layers, second-color layers; creates folder + CSV row; **Open folder**.
- **New species / face or hair group**: same pattern for `species.csv`, `species_parts.csv`.
- **Edit** any row via a form (one-line diff).
- **Remove**: deletes folder (with `.meta` files) and CSV row; git is the undo.
- **Rescan**: rereads CSVs and folders, rebuilds `ArtTables` in memory, refreshes every dropdown.
- New PNGs get a `.meta` from a template with the game's sprite settings and a new GUID.
- Warnings list; red errors must be cleared before pushing.

### Error handling

- Missing image at draw time: that layer draws nothing, warning added, no crash.
- CSV locked by Excel on save: "close equipment.csv in Excel"; the edit is kept.
- CSV changed on disk since the last Rescan: ask before overwriting.
- Export folder not writable: message, nothing lost.

## 3. Artist workflow

1. Install GitHub Desktop; clone `zhintze/Starquill`; switch to the `art` branch.
2. Download the studio from the latest GitHub Release; first launch points it at the clone.
3. Add art (Library panel or drop files into the type folder), Rescan, check Warnings (no red).
4. Commit and push to `art` in GitHub Desktop.
5. Zach/Claude merge `art` into `unity-idle-clicker` after checking in Unity.

The artist README (`docs/character-studio-readme.md`) is written after testing: plain words, screenshots, covering setup, both screens, adding a set, warnings, commit/push.

## 4. Testing

- EditMode: CSV read/write (quoting, lists, one-line diffs on edit); converter output; file scan cases (gap, stray file, incomplete variant, duplicate, empty); factory never rolls a missing number over thousands of rolls; new image paths; preset round-trip including missing art.
- The 12 test files that feed JSON text to loaders switch to CSV text through the converter.
- All existing tests (~460) green in the Test Runner GUI (no headless runs on this machine).
- Pixel match: a character rendered by the game's `CharacterDisplay` equals the studio export at 2× of the same character.
- Linux studio build tested here; Windows `.exe` smoke-tested by the artist.

## Open items

- Which `hd10-0001` and `hd10-0004` headband drawings are correct, the `Artboard` copies or the named files (artist).
- Confirm the GitHub login of the artist (`rungsglas` is the other collaborator).
