# Starquill Tools

## color_pools.py: key color family review

Local, interactive review of the dungeon-key color families. Shows every
color in the "main" palette grouped by the pool the game would drop it
from, and lets you manually move colors between pools: the same view as
the classifier, plus your corrections on top.

### Quick start

```bash
python3 tools/color_pools.py            # serves http://localhost:8787 and opens the browser
python3 tools/color_pools.py --port 9000
python3 tools/color_pools.py --no-browser
```

No dependencies beyond the Python standard library. Stop with Ctrl+C.

The page reads the palette (`Assets/Data/ArtTables/palettes.csv`), the classifier rules,
and the saved overrides fresh on every load: after changing any of them,
refresh the browser.

### Using the page

| Action | How |
|---|---|
| Select colors | Click swatches; click again to deselect. Selection count shows in the bottom bar |
| Move selection to a pool | Click that pool's button in the bottom bar |
| Create a new pool | Type a name, click **Create & move** (letters/digits/underscore only) |
| Undo a manual move | Select the swatches, click **Auto**: returns them to the classifier's family |
| Persist | **Save**: writes `Assets/Resources/Data/color_family_overrides.json` |

Reading the swatches:

- **Gold corner dot**: the color carries a manual override.
- **Hover tooltip**: hex, HSV, the classifier's family, and the override if any.
- **"not mintable" tag**: a real enum family that KeyRoller never mints as a
  key (currently Pastel: the unsorted pale pool).
- **"workshop · not in game" tag**: a pool you created that has no
  ColorFamily enum member yet (see below).

### What Save changes in the game

`ColorManager` loads the overrides file at startup and applies it on top of
`ColorFamilyClassifier` when building key drop pools:

- Color moved to a **real family** (Red, Brown, Black, ...): that color key's
  dungeons now drop it; its old family no longer does.
- Color moved to a **workshop pool** (any name outside the enum): excluded
  from every key's drop pool. It still appears in ordinary unkeyed loot.
  This is the "we don't know where this belongs" state.

The overrides file is checked into the repo: curation is versioned, and
overrides are keyed by hex, so they survive classifier retunes (manual
calls always win over the classifier).

### Promoting a workshop pool to a real key color

When a workshop pool (say `Ivory`) is coherent enough to become a key:

1. Append `Ivory` to the `ColorFamily` enum in
   `Assets/Scripts/Core/ColorFamily.cs`. **Append only, never reorder**:
   ordinals are the save-format contract.
2. Add it to `KeyRoller.MintableFamilies`
   (`Assets/Scripts/Destinations/KeyRoller.cs`) if it should drop as a key.
3. Add an accent color case in `KeyPresenter.AccentColor`
   (`Assets/Scripts/UI/KeyPresenter.cs`).
4. Add it to `FAMILIES` (and `ACCENT`) in this tool.
5. Existing overrides pointing at `Ivory` start applying in-game
   automatically: `Enum.TryParse` now resolves the name.

### Keep-in-sync rule

The tool contains a Python port of `ColorFamilyClassifier.Classify`. If you
change thresholds or rules in `Assets/Scripts/Core/ColorFamily.cs`, mirror
the change in `classify()` here (both carry a KEEP IN SYNC comment). Drift
symptom: the tool shows a color in one pool while the game drops it from
another.

### Related tests (Unity Test Runner, EditMode)

`ColorFamilyTests` (classifier), `ColorManagerFamilyTests` /
`ColorManagerOverrideTests` (pool building + overrides),
`KeyRollerTests.Roll_NeverMintsPastelKeys` (mintability). Tests cannot run
headless on this machine: use Window > General > Test Runner.

---

## Other tools

- `balance_sim.py`: economy pacing simulation (quest-level wall tuning; see
  `docs/balance-analysis.md`).
- `analyze_item_framing.py`: measures alpha-weighted sprite framing per item
  category to regenerate `ItemIconFraming` crops. Rerun when art changes.
- `migrate_art_tables.py`: one-time, already run 2026-11; kept for history.
  It moved the art PNGs into one folder per type and turned the art JSON into
  the CSVs in `Assets/Data/ArtTables/`. Its `csv` step now refuses to run.
- `ArtTablesTests/`: `dotnet test tools/ArtTablesTests` runs the art table
  tests headless (no Unity needed); `--filter Category=RepoData` checks the
  real CSVs and images in this repo.
