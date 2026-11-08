# Character Studio Progress and Resume Notes

2026-11-08 (updated 2026-11-08: Phase A done; handoff to a fresh session for Phase B Stage B1) · Read this before resuming.

Mandate: "creating a standalone character selector/generator ... that exports generated and curated characters into standalone separate images with a transparent background as a png file for use in promotional content", built on "our current object oriented system", with the art rules "only storing in one place to avoid [them getting unsynced]" and "human readable AND make for easy git diffs" (Zach, 2026-11-08).

Done when (Stage B1, this handoff):
- Phase B plan Tasks 1-13 are committed on `unity-idle-clicker`, each with the evidence its steps ask for (compile check plus a reflection probe for new types; commit hash).
- `Tools > Character Studio > Build Linux` and `Build Windows` both report `Succeeded`, with sizes recorded here.
- Zach has reported CHECKPOINT D: Test Runner all EditMode green except the 2 known old failures (Open flags), the `StudioPixelMatchTests` numbers, and the studio tried in the Editor and as the Linux build.

- **Plan:** `docs/plans/2026-11-08-character-studio-phase-b-plan.md` (Stage B1 in full, B2-B6 outlined; planning decisions P1-P7 at its top) · **Design:** `docs/plans/2026-11-08-character-studio-design.md` (D1-D12) · **Phase A plan:** `docs/plans/2026-11-08-character-studio-plan.md` · **Branch:** `unity-idle-clicker` (not pushed)
- **Process:** superpowers:executing-plans; one batch, Tasks 1-13, ending at CHECKPOINT D. B2's detailed tasks are written only after CHECKPOINT D.

## Run rules for the executing session

- Stop at **CHECKPOINT D** and wait for Zach. Never run the Unity Test Runner from a script (it crashed the Editor before). The Editor must be open with Coplay connected; if Coplay reports "Unity Editor is not running", ask Zach to open it.
- Compile checks: write an `AssetDatabase.Refresh()` script in the scratchpad (never under `Assets/`), run it with Coplay `execute_script`, then `check_compile_errors`, then a reflection probe that the new type or member exists. A bare "No compile errors" can be stale.
- Coplay `play_game` / `capture_ui_canvas` / `stop_game` and the build menu via `execute_script` are allowed (Task 13). Build result lines land in `~/.config/unity3d/Editor.log`, not the Coplay log buffer.
- Progress watchdog: three attempts in a row on one task with no new compile-clean probe or commit means stop and report.
- Retry budget: a failing command at most twice, each after a change. Coplay disconnects and Editor hangs are tooling failures: report them, do not loop.
- API mismatches between the plan's code and the installed Unity/uGUI/TMP (a renamed member, an obsolete warning) are fixed in place and listed as deviations in the Status row. A **load-bearing fork** the plan does not settle (for example the pixel-match test failing, a studio build too large to hand the artist, or a needed change to a game class beyond `ImageResolver`/`CharacterDisplay`): stop and report it as a decision block with a recommended default.
- Subagents: none required. If you use any, follow the handoffs skill for their briefs.

## Status

| Task | State | Evidence |
|---|---|---|
| Design | done, approved by Zach | `bd340964`, fixes in `fde82549` |
| Phase A (Tasks 1-17) | done, checkpoints A, B, C green | 18 commits `1756e7fa`..`f88cb6c2`; Test Runner 554/556 (2 known old failures), results file 2026-11-08 14:50; `dotnet test tools/ArtTablesTests` 73 passed, RepoData 3 expected warnings |
| Phase B plan | done | `08b23989` |
| B1 1-13 | in progress (rows below) | Editor opened by Zach 2026-11-08; Coplay connected |
| B1 T1-2 asmdef, sprite source | done | `e2615d67`; compile clean; probe: `ImageResolver(ISpriteSource)`, `MissingPaths`, `CharacterDisplay.Initialize(ImageResolver,Int32)`, `ImageResolverTests` present |
| B1 T3 PixelOps | done | `29679a15`; compile clean; probe: `PixelOps` 5 methods, `PixelOpsTests` 5 tests |
| B1 T4 DiskSpriteSource | done | `a39243c7`; compile clean; probe: `DiskSpriteSource(String)`, `FilePathFor`, `Load`, `Dispose`; 3 tests |
| B1 T5 StudioData | done | `547bc9fd`; compile clean; smoke call: load 137 ms, 27 species, 49 armor types, 9 weapons, 3 issues, 0 errors |
| B1 T6 StudioCharacter | done | `a4cc405b`; compile clean; probe: `StudioCharacter` (`ToDisplayList`, `SpeciesView`), `StudioItem`; 3 tests |
| B1 T7 RandomSettings, StudioRoller | done | `d668cf35`; compile clean; smoke call: 300 rolls at chance 1, avg 10.0 items, 0 two-handed with off hand; 8 tests |
| B1 T8 StudioRenderer, ExportNamer | done | `e340014d`; compile clean; smoke call (human, seed 42): 2x full export 400x400 RGBA with transparent corner, trimmed 271x358; 0 missing sprites; edge pixels dark from the art's own outlines (source art edge mean rgb 1,1,1), no fringe |
| B1 T9 pixel match test | done | `f6075d3e`; compile clean; same check run as a smoke call: opaque 43246, differ(>2) 4 (0.00%), max diff 6. Zach's Test Runner run is the record |
| B1 T10 StudioSettings | done | `5269cfc4`; compile clean; probe: `Load`, `Save`, `SnapScale`, `ResolvedExportFolder`; 3 tests (7 cases) |

## Decision map

Design-level decisions D1-D12 are in the design doc; planning decisions P1-P7 are in the Phase B plan. Not repeated here.

### Handoff

### Decision: execute in the main checkout, not a git worktree
- Serves: Done when (CHECKPOINT D in the Editor Zach uses)
- Beat the runner-up because: the Editor and Test Runner Zach uses have this checkout's `Library/` imported; a worktree means a multi-minute reimport of ~4,100 sprites and a second Editor project per checkpoint.
- Deferred: worktree per batch: only if parallel work lands on `unity-idle-clicker`.
- Ruled out: none.
- Evidence: Phase A ran this way through three checkpoints.

### Decision: Phase B lands in stages; only B1 is detailed
- Serves: Done when (B1); mandate ("exports ... png ... for promotional content" is B1's core)
- Beat the runner-up because: B2-B6 build on B1's real classes (`StudioCharacter`, `StudioRoller`, `StudioRenderer`, `StudioUi`); detailing them now would mean guessing APIs, the reason Phase B waited for Phase A.
- Deferred: detailing B2 (full Bulk screen) now: after CHECKPOINT D. Splitting B1 further: B1 is the smallest slice that gives the artist exported PNGs.
- Ruled out: none.
- Evidence: `docs/plans/2026-11-08-character-studio-phase-b-plan.md` "Scope of this document".

### Decision: species size stays off in the game and the studio
- Serves: D1 (the studio draws exactly what the game draws)
- Beat the runner-up because: Zach decided (2026-11-08) to keep it disabled while he takes the options to the artist.
- Deferred: (a) shared tall canvas where characters stand side by side, fit-to-frame in portraits and cards; (b) scale everywhere on one shared canvas; (c) special rules for a few species with most differences reduced sharply (Zach's suggestion). All in the `docs/roadmap.md` backlog item.
- Ruled out: scale only below 1: arbitrary, the 1.1-1.4 species stay wrong.
- Evidence: `7477c365`; Phase B plan P3.

## Digest

- **Established:**
  - Art data is six CSVs in `Assets/Data/ArtTables/`, converted by `ArtTablesConverter` (pure C#, `Assets/Scripts/Data/Art/`); the game reads the baked `Resources/ArtTables.asset`, the studio converts in memory from the repo clone. 27 species, 49 equipment types, 9 weapon types, 19 part groups.
  - All art PNGs are 200×200 except `species/body/0026-037.png` (201×201); `CharacterDisplay` at texture `200 × k` is the canvas at `k×`; the game renders at 400.
  - The auto-rebake after a CSV/PNG change runs on the Editor's next update, which on this Linux box waits for window focus; scripted, call `ArtTablesBaker.Bake()` directly.
  - The Coplay log buffer does not show logs written during a player build; read `~/.config/unity3d/Editor.log`.
  - The Task 15 Linux test build (all art in `Resources`) took 49 s and was 719 MB, 626 MB of it `resources.assets.resS`.
- **Ruled out:** Python port of the renderer (D1); CSV plus committed JSON (D2); hand-typed variant counts (D4); UI Toolkit and native file dialogs for the studio (P1, P2).
- **Open threads:**
  - which `hd10-0001` / `hd10-0004` headband drawings are right (artist, design Open items)
  - whether `rungsglas` is the artist's GitHub login (design Open items)
  - species size options (artist; decision above)
  - save file protection (backlog, `docs/roadmap.md`)

## Next step

Phase B plan Task 1 (studio assembly), then Tasks 2-13 in order; Task 2 is the only one that touches game code (`ImageResolver`, `CharacterDisplay`), so a mistake there shows in the game too: compile-check it before moving on.

## Open flags

- Two EditMode tests fail since 2026-07-02, unrelated to this work; Zach said note them and do not fix now. Expect exactly these 2 at CHECKPOINT D:
  - `ColorFamilyTests.Classify_KnownColors(0.82, 0.71, 0.55, Brown)` gets Pastel (stale test case after `144845e1`).
  - `TavernPresenterTests.RecruitLabel_ShowsPrice` expects `RECRUIT · 2.5Kg`, gets `RECRUIT · 2.5K` (`TavernPresenter.RecruitLabel` drops the `g`: real UI bug).
- Art problems for the artist, left untouched: 5 empty images (`ar02-0010-034`, one each in tr14, s04, w05, w06) and 36 whole-variant duplicates, including three `0000` drawings renumbered in Phase A Task 8 (`f02 0221` = `0055`, `f05 0067` = `0002`, `f06 0054` = `0002`). Full list: Tools > Art Tables > Check Images. The two `Artboard 1hd10-*` files still await the artist.
- Player builds carry all art uncompressed (626 MB in a Linux build; the import setting is Uncompressed). This affects the Android game build too; outside Phase B. The studio uses LZ4HC (P6): record its build sizes at CHECKPOINT D.
