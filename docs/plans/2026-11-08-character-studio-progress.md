# Character Studio Progress and Resume Notes

2026-11-08 (updated 2026-11-08: Stage B1 done, CHECKPOINT D green; running B2-B5 without stops; one Linux build at the end for all remaining checkpoints) · Read this before resuming.

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
| B1 1-13 | done, CHECKPOINT D green | `e2615d67`..`74524558`; Test Runner 590/592 (the 2 known old failures), results file 2026-11-08 16:25; 36/36 studio tests; `StudioPixelMatchTests`: opaque 43246, differ(>2) 4 (0.00%), max diff 6; Zach: "characterstudio worked" (2026-11-08) |
| B1 T1-2 asmdef, sprite source | done | `e2615d67`; compile clean; probe: `ImageResolver(ISpriteSource)`, `MissingPaths`, `CharacterDisplay.Initialize(ImageResolver,Int32)`, `ImageResolverTests` present |
| B1 T3 PixelOps | done | `29679a15`; compile clean; probe: `PixelOps` 5 methods, `PixelOpsTests` 5 tests |
| B1 T4 DiskSpriteSource | done | `a39243c7`; compile clean; probe: `DiskSpriteSource(String)`, `FilePathFor`, `Load`, `Dispose`; 3 tests |
| B1 T5 StudioData | done | `547bc9fd`; compile clean; smoke call: load 137 ms, 27 species, 49 armor types, 9 weapons, 3 issues, 0 errors |
| B1 T6 StudioCharacter | done | `a4cc405b`; compile clean; probe: `StudioCharacter` (`ToDisplayList`, `SpeciesView`), `StudioItem`; 3 tests |
| B1 T7 RandomSettings, StudioRoller | done | `d668cf35`; compile clean; smoke call: 300 rolls at chance 1, avg 10.0 items, 0 two-handed with off hand; 8 tests |
| B1 T8 StudioRenderer, ExportNamer | done | `e340014d`; compile clean; smoke call (human, seed 42): 2x full export 400x400 RGBA with transparent corner, trimmed 271x358; 0 missing sprites; edge pixels dark from the art's own outlines (source art edge mean rgb 1,1,1), no fringe |
| B1 T9 pixel match test | done | `f6075d3e`; compile clean; same check run as a smoke call: opaque 43246, differ(>2) 4 (0.00%), max diff 6. Zach's Test Runner run is the record |
| B1 T10 StudioSettings | done | `5269cfc4`; compile clean; probe: `Load`, `Save`, `SnapScale`, `ResolvedExportFolder`; 3 tests (7 cases) |
| B1 T11 StudioUi | done | `99ee8c70`; compile clean; probe: `StudioUi` 15 helpers |
| B1 T12 StudioApp, picker, Bulk, Warnings | done | `fc54fd8c`; compile clean; probe: `StudioApp`, `RepoPickerView`, `WarningsView`, `BulkScreen`. Deviation: `StudioApp.renderer` renamed `studioRenderer` (CS0108: hid `Component.renderer`); reflection confirms the rename compiled |
| B1 T13 scene, build menu, builds | done | `d9216355`. Scene created, build list still only ExploreScene. Editor smoke (play + scripted clicks): 8 cards drawn, Lock survives Randomize All and Equip Random, Reroll, Count 64 builds 64 cards, Export wrote 8 trimmed RGBA PNGs with transparent corners; no studio errors in the log. Builds: Linux Succeeded 91 MB on disk (report 89 MB), 350 s; Windows Succeeded 96 MB on disk (report 94 MB), 235 s, 38 MB zipped. Deviations: (1) `CreateScene` first calls `SaveCurrentModifiedScenesIfUserWantsTo` (as written it silently dropped unsaved scene work); (2) `Build` sets the scripting backend only when it is not already Mono, and calls `AssetDatabase.SaveAssets()` after restoring: the first Linux build had left the studio's product name, window size, mode and a `Standalone: 0` line in `ProjectSettings.asset`; restored and verified identical to the pre-build diff, and the Windows build with the fix left it unchanged |
| B2 plan (Tasks 14-23) | approved by Zach | `b5e9e4b5`; decisions P8-P16. Zach (2026-11-08): "all looks good continue all the way through the tasks until complete, then a linux build for full testing of all remaining checkpoints" |
| B2 T14 StudioUi helpers, layout fixes | done | `603045a5`; compile clean (all of T14-22 compiled together, one refresh); probe: `Popup`, `Slider`, `Swatch`, `Spacer`, `ArrowSprite`, `DotSprite`; no new CS warnings |
| B2 T15 ItemSearch, ForcedItem | done | `7d7fecb7`; smoke call: "hat" 15 of 204, file name `hd01-NNNN-130.png` 1, "gloves" 16 = ar01's 16 variants |
| B2 T16 roller additions | done | `d3f57d73`; probe `MakeItem`, `ChangeSpecies`, `Reset` |
| B2 T17 ForcedItems | done | `a5066d55`; smoke call: w04 then w08 refused ("w04-0001 is two-handed, so the off hand cannot hold w08-0001."); 100 rolls at chance 1 + forced hd01/w04: 0 two-handed with off hand; 28 without the hat = species that restrict hd01 (7 of 27) |
| B2 T18 settings: excluded species | done | `dfa247d2`; probe `excludedSpecies`, `DefaultExportFolder` |
| B2 T19-21 Species, Settings, Swatch popups | done | `090452ce`, `b43cf098`, `14a075bc`; probes |
| B2 T22 Bulk screen complete | done | `71b2b9ab`; probe ctor |
| B1 fix: build target | done | `c4075fc4`. The B1 Windows build left the Editor on StandaloneWindows64; on the next refresh the IAP package opened a modal "Required Dependency Missing" (install Microsoft GDK?) that froze Coplay. Answered "Don't install" (no change), switched the Editor back to StandaloneLinux64 (the Linux build's 85 ms "prepare assets" shows it was on Standalone before), and `Build` now switches back to the previous target after building |
| Species extra parts table | done | `b25eb0a6` plan, `fa85bd6d` code + data. dotnet ArtTablesTests 84 pass; art/display/studio test methods by reflection 242 pass (RepoArtTables needs NUnit's TestContext, passes under dotnet). Baseline 540 bodies: rolls identical; 128 renders differ, all explained: 20 rhinofolk (horn follows its palette) + 108 white rolls now drawn white (old white-means-none shortcut; before/after checked by eye). Game ExploreScene starts with no exception. Studio: satyr shows two shared color rows; Library Extra parts tab 29 rows, key fields locked. Linux rebuilt 19:56: 90 MB; the build paused on Unity's "save modified scenes (CharacterStudio)?" dialog (the scene's camera flag set by scripted displays, already committed); answered Don't Save; ProjectSettings restored to the session-start state |
| Zach feedback 2026-11-08: species part colors | done | `d8d4b378`: Body tab gets one color row per species_skin_variance.csv set (17 sets in 16 species), named after the parts drawn on its layers (Legs, Mouth, Facial detail, Other part for fixed extra drawings such as wings), palette = that set's hex list only, one pick colors all the set's layers. Play: centaur Legs color 4 swatches, 233320 -> F3D9C2 on the preview. Studio tests by reflection 117 pass. Presets already saved these (`skinVariance`) |
| Zach feedback 2026-11-08 Linux rebuild | done | after species part colors: 90 MB, 168 s; ProjectSettings unchanged |
| Zach feedback 2026-11-08 (after E-G) | done; Linux rebuild below | `9c3be0e3` palette-only color pickers; `823835a5` PickList dropdowns + dark scrollbars; `71b9b9dc` species.csv bridge troll `hd14` -> `hd13` (re-baked ArtTables.asset; table warnings 3 -> 2, the 2 Artboard files). Checked in play: card species list 27 rows in a 688 px panel with search; "elf" leaves dark/divine/oak elf, Enter picks dark elf; on an unchanged djinn card the Single skin picker shows 298 swatches (the shared `human` skin palette), hair and head color 1,027 (`main`), no sliders or hex field. Studio tests by reflection 116 pass |
| B5 T40 Linux build | done; CHECKPOINTS E-G waiting for Zach | Tools build Succeeded: `Builds/CharacterStudio/Linux/StarquillCharacters.x86_64`, 91 MB on disk (report 90 MB), 148 s; `git diff ProjectSettings/` identical to the session-start diff; headless start (`-batchmode -nographics`, 15 s) logged no exception |
| B5 T38-39 Library screen, smoke run | done | `63869de8`; compile clean; studio test methods by reflection 116 pass. Play run on a temporary copy of the tables + images (studio repo setting pointed at it, restored after, copy deleted): New item type form suggested hd14; saved -> equipment.csv one added line `hd14,smoke hat,130,,`, folder + folder .meta; layer codes `abc` refused with the converter's error, form stayed open; a copied PNG + Rescan wrote 1 .meta identical to hd01's apart from the guid; Back reloaded the art and kept the 8-card grid; search found hd14-0001; Remove (confirm) restored equipment.csv byte-identical and deleted the folder |
| B5 T36-37 library edits, meta writer | done | `2475a785` `5b9da933`; compile clean. Studio test methods called directly by reflection (scratchpad `RunStudioTests.cs`, not the Unity Test Runner): 116 cases pass, 0 fail (pixel match skipped: needs TestContext; covered at CHECKPOINT D) |
| B4 T34-35 preset UI, smoke run | done | `7281206f`; compile clean (one CS0165 fixed before commit; `PresetFiles` made lazy: `Application.dataPath` is not allowed in a MonoBehaviour constructor). Play run: Bulk Presets saved `smoke cast` (32 characters); Randomize All; a hand-edited copy (Torso tr07 #9999) loaded back: card 1 species restored, 32 cards, status "1 warning (see Warnings)", Warnings 3 -> 4; Settings > Presets saved `smoke settings`; Single > Presets saved `smoke hero`. Files readable JSON (floats print with full precision, e.g. 0.8999999761581421: JsonUtility). Smoke presets deleted |
| B4 T32-33 preset model and files | done | `d8bb5d89` `2b96540e`; compile clean; smoke call: 200 random characters (all species, chance 1) through JSON and back: identical JSON every time, 0 warnings (first run found 61 differing: loading rolled a fresh body whose other hair group leaked in; fixed before commit) |
| B3 T28-31 pickers, Single screen, Edit, smoke run | done | `57d5c8cd` `5132f051` `ac1f2522`; compile clean, probes, no new CS warnings. Play run: Edit on card 1 opens Single (Bulk hidden); Body tab rows Species, Skin, Hair (h01 0046 + color), Eye color, Detail color, Eyes, Nose, Mouth, Facial detail (fixed: Show toggle); Gear tab: Head type hd06 -> hd01, Undo restores hd06, Redo hd01; variant picker for hd01: 62 cells all with thumbnails, picked 0062 and it stuck; color picker (main palette 1,027 swatches) set the head color 131C40; Export this wrote dark-elf_01 PNG (deleted); Back returns to Bulk. Modular weapon row shows part pickers 164/166/168. Note: in the Editor's portrait Game view (1080x1920) the right panel is narrow and rows squeeze; set the Game view to 16:9 for the studio. Dropdown captions now cut with ... instead of wrapping |
| B3 T24-27 copy/history, slot reroll, body parts, thumbnails | done | `9ee40822` `c4699906` `94612f85` `d50a4dfc`; compile clean, probes; smoke call: human editable parts Ears f04, Eyes f01, Nose f02, Mouth f03, Facial hair f05 (None ok), Facial detail f06 (None ok); stone golem: fixed facial detail only; hd07 on human with hair h01 hides Facial hair (h01 draws on 92 only; h02-h04 also use 128/154); thumbnails item 103 px, eyes 44, hair 96, weapon 115, checked by eye |
| B2 T23 Editor smoke run | done; Linux build deferred to the end of B5 (Zach's instruction) | fix `a0b990cd` (popups get their own canvas, sort order 100, so they cover the search list, order 50). Play + scripted clicks: 32 cards (saved count); `Species (27/27)`, `Export all (32)`; search hat: 15 buttons + "Showing 15 of 204"; pick: chip `hd01-0001  x`, list hides; w04 added, then w08 refused in red ("w04-0001 is two-handed, so the off hand cannot hold w08-0001."); locked card kept its species through Randomize All; card 2 dropdown -> stone golem; Species None -> `Species (0/27)` + red message, Randomize All refused; All restores; Settings popup 68 sliders, Reset reopens; chip x removes with status; Skin picker sets the swatch (djinn: 298 swatches, its skin column names a big palette); Select 2 -> `Export selected (2)`, 2 PNGs written (deleted). Captures: labels on one line, dropdown arrows drawn. Log: no studio errors |

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

**CHECKPOINTS E-G (Zach), one pass on the Linux build and the Test Runner:**
- Test Runner > EditMode > Run All: expect about 673 tests (592 at CHECKPOINT D plus 81 new studio cases), all green except the 2 known old failures. The studio cases were also called directly by reflection on 2026-11-08: 116 pass (pixel match needs the runner).
- Run `Builds/CharacterStudio/Linux/StarquillCharacters.x86_64` (first launch: repo picker, paste `/home/keroppi/Development/Starquill`). In the Editor instead, set the Game view to a 16:9 size first (the game's portrait view squeezes the studio).
- **E, Bulk:** Species window (uncheck some; Randomize All uses only checked; restart remembers); search `hd01-0001`, a pasted file name, `hat`; chips and their x; Equip Random keeps forced items; `w04` then `w08` shows a red two-handed message; card species and Skin; Settings sliders (Head 0 gives no hats); Select a few, Export selected; type an export folder.
- **F, Single:** Edit on a card; Body tab (species, skin, hair group/variant/color, eye color, face parts with thumbnails, None for facial hair/detail); Gear tab (type dropdowns, variant thumbnails, colors, weapon parts, row Reroll, row Lock survives Reroll gear, x); Ctrl+Z / Ctrl+Y; backgrounds; Export this; Back updates the card.
- **G, Presets and Library:** save and load a cast, a character, random settings; hand-edit a preset to a missing variant and see the warning. Library: New item type (suggested code, slot, start from a copy), a bad layer code is refused, Rescan after dropping a PNG in the new folder writes its .meta, Back keeps the grid, Remove (confirm). Try it on a scratch branch or `git restore` afterwards: the Library edits this repo's CSVs and image folders.
- Then: B6 (GitHub Release of the Windows build, artist README) needs Zach's go; the Windows build was not rebuilt in this run.

## Open flags

- Two EditMode tests fail since 2026-07-02, unrelated to this work; Zach said note them and do not fix now. Expect exactly these 2 at CHECKPOINT D:
  - `ColorFamilyTests.Classify_KnownColors(0.82, 0.71, 0.55, Brown)` gets Pastel (stale test case after `144845e1`).
  - `TavernPresenterTests.RecruitLabel_ShowsPrice` expects `RECRUIT · 2.5Kg`, gets `RECRUIT · 2.5K` (`TavernPresenter.RecruitLabel` drops the `g`: real UI bug).
- Art problems for the artist, left untouched: 5 empty images (`ar02-0010-034`, one each in tr14, s04, w05, w06) and 36 whole-variant duplicates, including three `0000` drawings renumbered in Phase A Task 8 (`f02 0221` = `0055`, `f05 0067` = `0002`, `f06 0054` = `0002`). Full list: Tools > Art Tables > Check Images. The two `Artboard 1hd10-*` files still await the artist.
- Player builds carry all art uncompressed (626 MB in a Linux build; the import setting is Uncompressed). This affects the Android game build too; outside Phase B. The studio uses LZ4HC (P6): record its build sizes at CHECKPOINT D.
- Studio layout, seen in the Editor smoke capture (portrait Game view, 1080x1920): top-bar labels wrap ("Cou/nt", "Sca/le", "Tri" for Trim) and the dropdown arrows draw as plain white squares (`DefaultControls.Resources` has no sprites). Plan says layout polish waits for Zach's checkpoint feedback; not changed.
- The builds include a `Starquill Characters_BurstDebugInformation_DoNotShip/` folder; leave it out of the B6 zip.
- ExploreScene shows as unsaved after Coplay scripts that create objects in the open scene; checked twice, its content was byte-identical to the file (no real change).
- `Library/CharacterStudio/studio-settings.json` exists from the Editor smoke run (repo root = this checkout); the smoke-run export PNGs were deleted.
- B1's Windows build had left the Editor on StandaloneWindows64, which made the IAP package open a modal "install Microsoft GDK?" dialog that froze Coplay (answered Don't install; fixed in `c4075fc4`). If it appears again, Don't install is safe.
- The Editor's Game view is the game's portrait 1080x1920; the studio is laid out for 16:9 (the Single screen's right panel squeezes in portrait). The builds open at 1600x900.
- Palettes are large: the main palette has 1,027 colors and some species skin palettes ~300, so those pickers are long scroll lists. Grouping by color family could come later.
- Preset JSON prints floats with full precision (`0.8999999761581421`): JsonUtility; harmless.
- Excel's file lock cannot be reproduced on Linux; the "close it in Excel" message is untested until the artist tries it on Windows.
- Species extra parts (2026-11-08): white in a part palette now draws white (it used to fall back to skin): goblin/ogre/cyclops ogre/bridge troll tusks are always white now, and rolls of #FFFFFF for devil/djinn/vampire/war orc horns and teeth and fairy wings. Rhinofolk horns follow their ivory palette. The angel halo (0014-134) and half-demon horns still use skin color: give them a palette row in species_extra_parts.csv if they should not.
- The ExploreScene smoke start (2026-11-08) loaded the local save and showed the offline-earnings panel (not claimed); stopping play may have saved the game's last-seen time.
- A build started from a script hangs if an open scene has unsaved changes: Unity asks to save them first. From the menu that is a normal prompt.
