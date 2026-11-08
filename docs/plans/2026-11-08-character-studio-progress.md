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
| 3-17 | not started | |

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

Plan Task 3 (art table data model and cell parsing).

## Open flags

None.
