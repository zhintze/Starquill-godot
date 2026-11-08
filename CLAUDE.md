# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Starquill is a **mobile idle RPG clicker** built in **Unity 6 (6000.3.8f1)**, portrait 1080x1920, IL2CPP. Players assemble a party of 4 paper-doll characters, who auto-battle waves of enemies while the player fires Verbs (pooled party abilities) to exploit a dual-triangle stat advantage system (Physical: STR>DEX>CON, Mental: INT>WIS>CHA). Loot drops constantly and immediately changes character appearance via a compositing pipeline.

The original Godot open-world RPG is archived in `godot-archive/` and is not part of active development.

## Key Documents

- `docs/implemented-systems.md`: authoritative reference for what is built and how it works
- `docs/sprint-review.md`: historical sprint log (Sprints 1-11, balance pass, Destinations D1)
- `docs/roadmap.md`: remaining MVP work (Sprint 12) + post-MVP backlog
- `docs/plans/`: dated design and implementation plan docs, one pair per sprint
- `docs/plans/2026-02-12-idle-rpg-clicker-design.md`: original master design (equipment/affix sections superseded; see banner in that file)

## Architecture

### Assembly Definitions (dependency order)

```
Core → Data → Combat / Characters / Equipment / Exploration / Display → Managers
UI (references Managers and below)
```

Rules:
- Equipment references Display (ColorManager); Characters references Equipment + Display (factory chain)
- If a type is needed by both Equipment and Managers, put it in Equipment (lower in the chain). Example: `SerializedEquipment` lives in the Equipment namespace to avoid a circular dependency.
- Tests live in `Assets/Tests/EditMode/` under `EditModeTests.asmdef` (~460 test methods in 59 files; count from Test Runner, not estimates)

### Core Flow

`GameManager` (singleton MonoBehaviour, `Assets/Scripts/Managers/GameManager.cs`) owns the game loop:
- `ProcessTick()` drives auto-combat via `CombatTickProcessor`, loot drops via `ProcessLootDrops()`, and ability XP via `TickAbilityXP()`
- UI is event-driven: GameManager fires events (`OnCombatTick`, `OnGoldChanged`, `OnWaveStarted`, `OnWaveCleared`, `OnVerbActivated`, `OnLootDropped`, `OnRosterChanged`); `ExploreSceneController` and other UI controllers subscribe
- `ExploreSceneController` uses a `DeferredInitialSync` coroutine (yield null) to wait for `GameManager.Start()` ordering

### Equipment Model (post stat-redesign, 2026-02-17)

Each `EquipmentInstance` has:
- A **primary/secondary stat pair** (`PrimaryStat`/`PrimaryValue`, `SecondaryStat`/`SecondaryValue`), rolled by `EquipmentFactory.GenerateStatPair()` from prefix-weighted pools with a rarity-based budget
- One optional **AwakenedAbility**: a passive stat boost that levels via per-tick XP (`abilityBaseXPThreshold` 100, growth 1.8) or gold accelerator (`LevelUpAbility`), max level by rarity (Common 2 → Legendary 7)
- Abilities are defined in `Assets/Resources/Data/abilities.json`, loaded by `AbilityTable`, rolled per equipment type prefix

The old affix system (`AffixTable`, `RolledAffix`, `affixes.json`, `StatMods`) is deleted. Do not reintroduce it.

### Paper Doll Display

`DisplayBuilder` 4-stage pipeline → `CharacterDisplay` RenderTexture compositing (400x400, bilinear). Layer codes and color variance come from the art tables (`Assets/Data/ArtTables/`); palettes from `ColorManager`.

### Data Loading

Art data (equipment, weapons, species, part groups, palettes) is CSV in `Assets/Data/ArtTables/`, baked into `Resources/ArtTables.asset`; never add art counts by hand (variant numbers come from the image files). Game data (abilities, names, quest zones, color family overrides) stays JSON in `Assets/Resources/Data/`, parsed via MiniJSON + SimpleJson helpers in the Core assembly. `dotnet test tools/ArtTablesTests` runs the art table tests headless. ScriptableObject configs in `Assets/Data/Config/` (DefaultEconomyConfig, DefaultAdvantageMatrix). `EconomyConfig` holds all tuning knobs and economy formulas (EnemyHP, GoldPerKill, UpgradeCost, OfflineGold).

## Development Workflow

### Running Tests

Tests **cannot run headless on this machine** (Arch Linux batch-mode issue). Use Unity Editor GUI: Window > General > Test Runner > EditMode > Run All.

### Editor Tools (Tools menu)

- **Tools > Setup GameManager**: creates ScriptableObjects + GameManager + fixes EventSystem
- **Tools > Build Explore Scene**: rebuilds the ExploreScene hierarchy from scratch (`Assets/Editor/ExploreSceneBuilder.cs`). Always save the scene after.
- `Assets/Editor/ClearSave.cs`: wipes save data
- **Tools > Art Tables > Bake Now / Check Images**: rebuild `ArtTables.asset` from the CSVs; report empty and duplicate images

### Scenes

- `Assets/Scenes/ExploreScene.unity`: the game (starting scene in build settings)
- `Assets/Scenes/DisplayTest.unity`: paper-doll rendering harness

### Coplay MCP

Unity Editor integration via Coplay MCP (file-based RPC through `Temp/Coplay/MCPRequests/`). If tools disconnect, restart Claude Code. `save_scene` needs the full path (`Assets/Scenes/ExploreScene`), not just the scene name. `Packages/Coplay/` is gitignored local tooling.

## Git Conventions

- Active branch: `unity-idle-clicker`. Do not commit to `main`.
- No `Co-Authored-By` lines in commit messages.
- Do not push unless explicitly told to.

## Known Gotchas

- Unity 6 `BuildProfileContext` NullReferenceException on Linux: delete `Library/BuildProfileContext.asset`
- `System.Random` vs `UnityEngine.Random` ambiguity: qualify `UnityEngine.Random.Range()`
- If `SerializeField` references appear null at runtime, rebuild the scene (Tools > Build Explore Scene) and save; scene rebuild fixes serialization
- Input System: `activeInputHandler=0` (Input Manager only, set 2026-07-02: Both is unsupported on Android; no code uses the Input System API); EventSystem needs `StandaloneInputModule`
- `IReadOnlyList.Contains()` requires `using System.Linq;`
- RectTransform: never use `anchoredPosition` across different parent hierarchies; convert via world position
- Variance colors serialize as flat `float[]` + `int[]` keys for JsonUtility compatibility
- `ArtTables.asset` is generated; if it is missing or stale, Tools > Art Tables > Bake Now. A scripted `AssetDatabase.Refresh` only queues the auto-bake (it runs once the Editor window has focus); call `ArtTablesBaker.Bake()` directly when scripting
- Art PNGs: one folder per type (`equipment/hd01/`, `species/f01/`, `species/body/`); move files with their `.meta`
- `com.unity.purchasing` is on the 5.x line (the 4.x line produced package errors in Unity 6); it is installed but unused until Shop/IAP work begins
