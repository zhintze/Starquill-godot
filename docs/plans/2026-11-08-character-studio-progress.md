# Character Studio Progress and Resume Notes

2026-11-08 (updated 2026-11-08: handoff to a fresh session for Phase A) · Read this before resuming.

Mandate: "creating a standalone character selector/generator ... that exports generated and curated characters into standalone separate images with a transparent background as a png file for use in promotional content", built on "our current object oriented system", with the art rules "only storing in one place to avoid [them getting unsynced]" and "human readable AND make for easy git diffs" (Zach, 2026-11-08).

Done when (Phase A, this handoff):
- Tasks 1-17 of the plan are committed on `unity-idle-clicker`, each with the evidence the plan asks for.
- `dotnet test tools/ArtTablesTests` passes, including `--filter Category=RepoData` with only the 3 expected warnings (2 `Artboard` files, bridge troll `hd14`).
- Zach has reported checkpoints A, B and C green (Test Runner all EditMode + play-test).
- The five art JSON files are gone and nothing reads them.

- **Plan:** `docs/plans/2026-11-08-character-studio-plan.md` · **Design:** `docs/plans/2026-11-08-character-studio-design.md` (decisions D1-D12) · **Branch:** `unity-idle-clicker` (not pushed)
- **Process:** superpowers:executing-plans, batches ending at the plan's checkpoints (A after Task 9, B after Task 14, C after Task 17). Phase B (studio app) is planned only after checkpoint C.

## Run rules for the executing session

- Stop and wait for Zach at each **CHECKPOINT**. Never run the Unity Test Runner from a script (it crashed the Editor before; see memory `current-status.md`). The headless `dotnet test` runs are yours.
- Tasks 8 and 9 move and delete files under `Assets/`: the Unity Editor must be closed. If it is open, ask Zach to close it.
- Progress watchdog: three attempts in a row on one task with no new passing test or commit means stop and report.
- Retry budget: a failing command at most twice, each after a change. `dotnet` package restore failing for network reasons is a tooling failure: report it, do not loop.
- Load-bearing fork the plan does not settle (for example, the migration dry run printing something other than the expected list, or more RepoData warnings than the 3 expected): stop and report it as a decision block with a recommended default. Do not edit art or CSV values to silence a warning.
- Subagents: none required. If you use any, follow the handoffs skill for their briefs.

## Status

| Task | State | Evidence |
|---|---|---|
| Design | done, approved by Zach | `bd340964`, fixes in `fde82549` |
| Plan | done | `fde82549` |
| 1 CsvTable + test project | done | `dotnet test tools/ArtTablesTests`: Passed 7, Failed 0 (failing build first: `Starquill.Data` not found). Deviation: `.gitignore` also got `!tools/ArtTablesTests/ArtTablesTests.csproj`, because Unity's `*.csproj` rule ignored it |
| 2 ArtFileName | done | `dotnet test tools/ArtTablesTests`: Passed 20, Failed 0 (7 + 13; failing build first: `ArtFileName` not found) |
| 3 ArtTableData + ArtCells | done | `dotnet test tools/ArtTablesTests`: Passed 41, Failed 0 (failing build first: `ArtCells` not found) |
| 4 Converter: palettes, equipment | done | `dotnet test tools/ArtTablesTests`: Passed 56, Failed 0 (failing build first: `ArtTablesConverter` not found) |
| 5 Converter: weapons, part groups | done | `dotnet test tools/ArtTablesTests`: Passed 63, Failed 0 (7 new tests failed first) |
| 6 Converter: species, skin variance | done | `dotnet test tools/ArtTablesTests`: Passed 71, Failed 0 (7 new tests failed first; `Species_FullPartToken_IsAccepted` passed before the code, it only asserts no errors) |
| 7 ArtRepo | done | `dotnet test tools/ArtTablesTests`: Passed 72, Failed 0 (failing build first: `ArtRepo` not found) |
| 8 `0000` cleanup | done | Dry run classification matched the plan (copies f04 h01-h04, empty s04, unique f01 f02 f03 f05 f06 s01-s03). Deviation: s01-s03 renumbered to `0003`, not the plan's `0004` (those groups hold 0000-0002 only; next free is 0003). PNGs 4108 -> 4098; `grep -c -- -0000-` = 0; git shows only species PNG + .meta deletes/renames |
| 9 Folder per type + paths | done; CHECKPOINT A green (Zach, 2026-11-08) | Move: 4098 PNGs before and after, 4098 `.meta`, 78 new folder `.meta`; only the 2 `Artboard` files at `find -maxdepth 2`. Coplay `check_compile_errors`: none. Test Runner: 550/552 passed; the 2 failures predate Phase A (see Open flags), Zach counted the checkpoint green without fixing them. Play-test: ExploreScene looks fine; Console has no `Sprite not found` |
| 10 CSVs + RepoData test | done | RepoData test Skipped before the tables existed. `csv` dry run: 49 / 9 / 27 / 19 / 17 skin variance / 1905 palette rows. `dotnet test --filter Category=RepoData`: Passed, warnings exactly the 3 expected (2 `Artboard`, bridge troll `hd14` on species.csv line 23). Full run: Passed 73. Folder variant counts fe02 hd08 hd10 tr14 = 71 35 14 17 |
| 11 Baker + asset + audit | done | Coplay `check_compile_errors`: none. Console: `Art tables baked: 49 equipment, 9 weapons, 27 species, 19 part groups; 0 error(s), 3 warning(s).` `ArtTables.asset` exists, `git status` silent (ignored). Check Images: 171 warnings in 6.1 s (5 empty, 166 same-pixel layers; 36 whole-variant duplicates, see Open flags). CSV edit and a new `hd01-0999-130.png` both rebaked (after the Editor window got focus) with no new warnings; the new PNG imported as Sprite, Single, 100 PPU, Bilinear, max 256, Uncompressed, no mipmaps. Test edits reverted |
| 12 Palettes from ArtTables | done | Coplay `check_compile_errors`: none, after a probe confirmed the recompile (`ColorManager.LoadPalettes` present, `LoadFromJson` gone, `ArtFixtures` compiled). `grep LoadFromJson` leaves only AbilityTable/QuestZone/NameGenerator callers. Unity tests wait for CHECKPOINT B |
| 13 Registry reads ArtTables | done | New `DisplayDataRegistryTests` first failed to compile (`Load`, `ModularPartVariants`, `Variants` missing); after the change Coplay `check_compile_errors`: none, probe confirms `Load` present. `grep ModularPartCounts\|ParseSpeciesJson\|ParseEquipmentJson Assets`: nothing |
| 14 Catalog + factory variant sets | done; CHECKPOINT B green (Zach, 2026-11-08) | Test Runner: 556 total, 554 passed, 2 failed (only the 2 known old ones). Play-test and existing save confirmed by Zach. Before that: Coplay `check_compile_errors`: none; probe confirms `EquipmentCatalog.Load` and `ArtFixtures.SixSlotCatalog`. `grep AmountPerLayer\|.Amount\|LoadEquipmentJson\|LoadWeaponsJson Assets`: nothing. `dotnet test`: 73 passed. Deviation: tests and code went in one pass, so there was no separate failing compile for this task. The three inline test catalogs matched the fixture exactly |
| 15 Delete art JSON | done | Wider grep: only `tools/color_pools.py` still read `color_palettes.json` (Task 16). 5 JSON + `.meta` removed; `migrate_art_tables.py csv` now exits with the retired message. Linux build via Coplay: Succeeded in 49 s; Editor.log shows 171 `Art images` warnings, then `Art tables baked: ...; 0 error(s), 3 warning(s).`, then `BuildPlayer: start`. With `hd01` `layer_codes` = `abc`: build Failed in 6 s with `Art tables: Error: equipment.csv line 9 ...` and `BuildFailedException: Art tables have errors`. Cell restored (git clean), rebaked: 49 equipment, 0 errors |
| 16-17 | not started | |

## Decision map

Design-level decisions D1-D12 are in the design doc's Decisions table; they are not repeated here.

### Handoff
### Decision: execute in the main checkout, not a git worktree
- Serves: Done when, Tasks 8-9 and the checkpoints
- Beat the runner-up because: Zach plays and runs the Test Runner in the Editor that has this checkout's `Library/` imported; a worktree would need a full multi-minute reimport of ~4,100 sprites and a second Editor project for every checkpoint.
- Deferred: worktree per task batch: only worth it if parallel work lands on `unity-idle-clicker` during Phase A.
- Ruled out: none.
- Evidence: superpowers:executing-plans suggests a worktree; this overrides it for a Unity project.

## Digest

- **Established:** the JSON art counts are stale (fe02 69/71, hd08 34/35, hd10 11/14, tr14 15/17 files); every multi-layer item and part group has all layers for every variant; `0000` parts split into copies (f04, h01-h04), empty (s04), unique (f01 f02 f03 f05 f06 s01 s02 s03), verified with full RGBA comparison; both `Artboard 1hd10-*` files differ from their namesakes; 27 species, 49 equipment types, 9 weapon types, 19 part groups.
- **Ruled out:** Python port of the renderer (D1: must reuse the game's classes); CSV plus committed JSON (two stores drift); hand-typed variant counts (D4).
- **Open threads:**
  - which `hd10-0001` / `hd10-0004` headband drawings are right (artist, design Open items)
  - whether `rungsglas` is the artist's GitHub login (design Open items)
  - Windows Build Support (Mono) must be installed before Phase B (Zach)
  - save file protection is a separate backlog item (`docs/roadmap.md`)

## Next step

Plan Task 16 (Python tools read the CSVs).

## Open flags

- Task 8: the plan expected s01-s03 `0000` to become `0004`; the script's next-free rule gave `0003`. The JSON count for s01-s03 is 3, so the game already asked for a missing `0003`; it now finds the renumbered drawing, which means those three drawings become visible in play before Task 14 (the plan said renumbered drawings stay hidden until then).
- Two EditMode tests fail, both since 2026-07-02 and unrelated to Phase A; Zach said note them and do not fix now (2026-11-08). Expect 2 failures at checkpoints B and C:
  - `ColorFamilyTests.Classify_KnownColors(0.82, 0.71, 0.55, Brown)` gets Pastel: the Pastel rule from `144845e1` catches this tan; the test case is stale (the same file lists `#C7A68B` tan as Pastel).
  - `TavernPresenterTests.RecruitLabel_ShowsPrice` expects `RECRUIT · 2.5Kg`, gets `RECRUIT · 2.5K`: `TavernPresenter.RecruitLabel` drops the `g` every other price label shows (real UI bug).
- Check Images (Task 11): 5 empty images (`ar02-0010-034`, one each in tr14, s04, w05, w06) and 36 whole-variant duplicates (every layer identical), e.g. tr03 0107-0111 = 0007-0011, f02 0211-0220 = 0052-0061, h01 x5. Three of them are `0000` drawings renumbered in Task 8, which only compared `0000` with `0001`: f02 0221 = 0055, f05 0067 = 0002, f06 0054 = 0002. Art left untouched (artist decides). Full list: rerun Tools > Art Tables > Check Images.
- The auto-rebake runs on the Editor's next update, which on this Linux box waits until the Editor window has focus. A scripted `AssetDatabase.Refresh` from Coplay queues it but does not run it; call `ArtTablesBaker.Bake()` directly when scripting.
