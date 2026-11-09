# Starquill: Implemented Systems Reference

**Last updated:** 2026-11-08
**Status:** Authoritative. This document describes what is actually built on branch `unity-idle-clicker` through Sprint 11, the balance pass, Destinations Sprint D1, the art tables (Godot art data ported to CSV) and the Character Studio art tool (last confirmed Test Runner pass: 590/592 on 2026-11-08, the 2 known old failures; the studio and extra-parts tests added since were run by direct method calls, not yet in the Test Runner). Where it conflicts with `docs/plans/2026-02-12-idle-rpg-clicker-design.md`, this document wins.

---

## 1. Overview

Unity 6 (6000.3.8f1) mobile idle RPG clicker, portrait 1080x1920, IL2CPP. A party of 4 paper-doll characters auto-battles enemy waves. The player taps Verbs (pooled party abilities) to exploit a dual-triangle advantage system. Loot drops feed a collection/equip loop that visibly changes characters.

**Assemblies:** `Core → Data → Combat / Characters / Equipment / Exploration / Display / Quests / Destinations → Managers`, with `UI` and `Services` alongside. `Starquill.Studio` (Character Studio; Editor + Windows/Linux standalone only, never in the Android game) sits on Core, Data, Display, Equipment. Tests in `EditModeTests` (592 test cases at the 2026-11-08 Test Runner pass). `Assets/Scripts/Data/Art/` (art tables: CSV reader, file name parser, converter, repo reader) is pure C# with no `UnityEngine`; it and `Assets/Tests/EditMode/Art/` also compile headless in `tools/ArtTablesTests` (`dotnet test`). The empty `Starquill.Economy` assembly was removed 2026-07-01; economy code lives in Data (`EconomyConfig`) and Equipment (`SellCalculator`, `PityTracker`).

---

## 2. Core & Data (`Assets/Scripts/Core/`, `Assets/Scripts/Data/`)

### Enums (Core)
| Enum | Values |
|---|---|
| `StatType` | STR, DEX, CON, INT, WIS, CHA (+ Physical/Mental category extension) |
| `Rarity` | Common, Uncommon, Rare, Epic, Legendary |
| `EquipmentSlot` | Head, Torso, Arms, Legs, Feet, MainHand, OffHand, Misc1-4 (11 slots) |
| `TargetMode` | Single, Cleave, AoE |
| `VerbCategory` | Physical, Mental |
| `StatusEffectType` | None, Stagger, Bleed, Weaken, Expose, Reveal, Confuse |
| `Advantage` | Weak, Neutral, Strong |

JSON parsing: `MiniJSON` + `SimpleJson` helpers (Core assembly).

### Data ScriptableObjects
- `Stats`: serializable stat block (GetStat/SetStat/Total/HighestStat/Clone/operator+)
- `SpeciesDefinition`, `SpeciesAbilityDefinition`: base stats, species ability with 6 rank thresholds, body part IDs
- `VerbDefinition`: stat type, base damage, hit count, target mode, proc chance, cooldown, rarity
- `StatusEffectDefinition`: stacking rules, overrides, cleanse relationships
- `AdvantageMatrix`: static 6x6 damage/proc matrices (`Assets/Data/Config/DefaultAdvantageMatrix.asset`)
- `EconomyConfig` (`Assets/Data/Config/DefaultEconomyConfig.asset`): all tuning knobs and formulas

### Economy formulas & knobs (EconomyConfig)
| Knob | Value |
|---|---|
| baseHP / hpGrowthRate | 50 / 0.12 (exponential enemy HP) |
| baseGold / goldGrowthRate | 5 / 0.10 |
| baseCost / costGrowthRate | 10 / 0.15 (upgrade costs) |
| offlineEfficiency / maxOfflineSeconds | 0.50 / 28800 (8h cap) |
| verbDrawCooldown / verbSlotCount / verbLockDuration | 10s / 3 / 3s |
| autoAttackDPSFraction | 0.3 |
| pity: Uncommon/Rare/Epic/Legendary | 50 / 200 / 1000 / 5000 kills |
| baseDropRate | 0.15 per kill |
| questDiscoveryRate / fragmentDropRate | 0.03 (quest offers) / 0.01 (no consumer yet) |
| ability XP: threshold/growth/rate | 100 / 1.8 / 1 per tick |
| ability gold cost: base/growth | 100 / 2.0 |
| prestigeMultiplierBase | 1.0 (stubbed; prestige is post-MVP) |

Formulas: `EnemyHP(questLevel)`, `GoldPerKill(questLevel, chaBonus, prestigeMult, boostMult)`, `UpgradeCost(currentLevel)`, `OfflineGold(goldPerSecond, elapsedSeconds, prestigeMult)`.

---

## 3. Combat (`Assets/Scripts/Combat/`)

- `CombatTickProcessor`: deterministic tick resolution; party auto-attacks (fraction of DPS), verb hits, advantage multipliers, enemy damage, wave death checks. Returns `CombatTickResult`.
- `VerbPool`: shared party verb pool; 3 draw slots, 10s rotation, tap-to-fire, verb lock (3s) after activation.
- `DamageCalculator`: dual-triangle advantage math via `AdvantageMatrix.GetMatchup()`.
- `EnemyState`: per-enemy runtime HP/stat typing.

## 4. Characters (`Assets/Scripts/Characters/`)

- `CharacterInstance`: species + stats + 11 equipment slots.
- `CharacterFactory` + `NameGenerator`: random character generation from the art tables' species rows + `names.json`.
- `CharacterRoster` / `Party`: roster of owned characters; active party of 4.
- `AutoEquipper`: best-in-slot per slot from inventory (uses `ItemComparer`), returns displaced items.

## 5. Equipment (`Assets/Scripts/Equipment/`)

### Stat model (redesigned 2026-02-17; replaces StatMods + RolledAffixes)

Every `EquipmentInstance` carries:
1. **Primary/secondary stat pair.** `EquipmentFactory.GenerateStatPair(prefix, rarity, rng)`:
   - Prefix-weighted stat pools (80% from pool, 20% any stat), secondary always differs from primary
   - Rarity point budget: Common 4-6, Uncommon 7-10, Rare 11-14, Epic 15-18, Legendary 19-22
   - Split ~65-75% primary / remainder secondary; primary always > secondary
2. **One AwakenedAbility** (optional): passive stat boost defined in `abilities.json` (14 abilities), rolled by `AbilityTable.RollAbility()` weighted per equipment type prefix.
   - Potency: `basePotency + (level-1) * potencyPerLevel`, both scaled by rarity modifier (0.8x Common → 2.0x Legendary)
   - Max level by rarity: Common 2, Uncommon 3, Rare 4, Epic 5, Legendary 7
   - Levels via per-tick XP (exponential threshold: 100 * 1.8^(level-1)) or gold purchase (`GameManager.LevelUpAbility`, cost 100 * 2.0^(level-1) * rarity multiplier)
   - All abilities are passive stat boosts for MVP; triggered abilities are data-modeled (`triggerType`/`procChance`) but not combat-resolved
3. `GetTotalStatMods()` = stat pair + ability potency on the ability's boosted stat.

### Pipeline
- `EquipmentCatalog`: loads the baked art tables (armor prefixes hd/tr/ar/lg/fe/mc; weapons w01-w07, shields w08-w09; modular weapons with per-layer variants). Each entry holds the variant numbers that exist on disk; rolls pick only from those, and `Reconstruct` maps a saved weapon part number that no longer exists to the first existing one
- `EquipmentFactory`: `CreateRandom`, `CreateRandomWeapon`, `CreateRandomLoadout(questLevel)`, `CreateStarterLoadout`, `RollRarity(questLevel)`, `Reconstruct(SerializedEquipment)`
- `LootDropper`: per-kill drop roll (baseDropRate) + rarity roll + 70% armor / 30% weapon + `PityTracker` integration
- `LootInventory`: capacity 50, add/remove/filter, events
- `SellCalculator`: rarity-based gold value scaled by questLevel
- `ItemComparer`: `ScoreItem`, `Compare`, `FindBestForSlot`
- `SerializedEquipment` / `SerializedAbility`: save format (stat pair fields + ability id/level/XP; flat float[]/int[] color serialization for JsonUtility)

## 6. Display (`Assets/Scripts/Display/`)

Paper doll rendering: `DisplayBuilder` 4-stage pipeline (species parts → equipment layers → color application → composite order) → `CharacterDisplay` RenderTexture compositing at 400x400 bilinear. `ColorManager` loads palettes from the art tables. `ImageResolver`/`ImageToken` map layer codes to sprites; `ImageResolver` takes an `ISpriteSource` (the game: `Resources`; the studio: PNGs from a repo clone) and records missing paths. `CharacterDisplay.Initialize(resolver, size)` renders at any multiple of the 200 px art canvas. `CharacterPortraitRenderer` for UI portraits. Each species part draws in one color: its `species_extra_parts.csv` rule if it has one (skin, hair, eyes, facial detail or a rolled palette color, shared within a color group), else skin (hair/eyes/facial detail fields: their own color). Extras come from that table and can be optional (`chance`). `SpeciesInstanceData` holds `PartColors` (by color group) and `OmittedExtras`.

**Art tables (2026-11-08).** The art rules live only in six CSVs in `Assets/Data/ArtTables/`: `equipment.csv`, `weapons.csv`, `species.csv`, `species_parts.csv` (part group layers, formerly hardcoded), `species_extra_parts.csv`, `palettes.csv` (one row per color). `species_extra_parts.csv` (2026-11-08, replaced `species.csv` `other_body_parts` and `species_skin_variance.csv`) has one row per species extra drawing (wings, tails, horns, halo) and per species part with its own color: `species,part,color,color_group,chance`; `color` is skin, hair, eyes, facial_detail or a palette; rows sharing a `color_group` roll one color; `chance` (extras only, blank = always) makes an extra optional. Rules are keyed by part, not layer; random rolls keep the old order so characters rebuilt from their id keep their look (plan `docs/plans/2026-11-08-species-extra-parts-plan.md`). `ArtTablesBaker` (Editor) converts them plus a scan of `Assets/Resources/Images` into `Assets/Resources/ArtTables.asset` (generated, gitignored) on Editor load if missing, whenever a CSV or art PNG changes (runs on the Editor's next update, which on Linux waits for window focus), before every build (table errors fail the build), and from Tools > Art Tables > Bake Now. `DisplayDataRegistry`, `EquipmentCatalog` and `ColorManager` read the asset. Variant numbers come from the files on disk, gaps allowed; nothing stores a count. A variant counts only when every layer of its item or part group has a file; the bake warns about missing layers, misnamed files, folders with no row and rows with no images. Images sit in one folder per type: `equipment/hd01/hd01-0001-130.png`, `weapons/w01/w01-164-0001.png`, `species/body/0001-016.png`, `species/f01/f01-0001-084.png`. `ArtImageAudit` (Tools > Art Tables > Check Images, and before builds) reports empty images and pixel-identical layers within a type.

## 7. Exploration (`Assets/Scripts/Exploration/`)

`ExplorationManager` / `ExplorationState` (Exploring / InQuest / QuestRetreat / InDungeon): ambient explore mode driving wave spawning; discovery rolls feed the quest system (§7b). Travel, fragments, and quest discovery freeze during dungeon runs. Fragments remain unconsumed (Locations, Sprint D2).

## 7b. Quests (`Assets/Scripts/Quests/`, added Sprint 9)

`QuestZoneTable` (loads `quest_zones.json`, 2 zones with dominant stat types + dialogue) → deterministic `QuestGenerator` (tier ladder 1-3 N / 4 E / 5-7 N / 8 E / 9-10 H / 11 Boss; typed waves, HP ramp, mini-boss/boss waves) → `QuestLog` state machine (Idle/Offered/Active/Retreated, guarded transitions, retreat = half-gold penalty, boss completion advances zone). GameManager orchestrates discovery offers, quest-wave spawning, rewards (tier gold multiplier + rarity-floor loot rolls + key payouts; full-inventory overflow goes to the reward mailbox, never lost), questLevel progression (+1, boss +2), and save persistence (specs regenerate on load).

## 7c. Destinations (`Assets/Scripts/Destinations/`, added Sprint D1)

Keys + dungeons per `docs/plans/2026-07-02-destinations-design.md` (Locations are Sprint D2):
- **Keys are instances** (`KeyInstance`): up to one modifier of each kind — color family (10 HSV buckets over the "main" palette via `ColorFamilyClassifier` in Core + family-filtered `ColorManager`; White/Black split from Neutral post-D1, enum append-only for save compat), equipment slot (`KeySlot`, 7), class archetype (`ClassArchetype`, 15 two-stat builds, ids are save-format contract) — plus difficulty D1-6. `KeyPouch` (soft cap 30: over it, drop rate halves), `KeyRoller` (drops: kind 40/30/30, difficulty 85/13/2), `KeyFusion` (different kinds combine, same variant merges as upgrade, cross-variant invalid; cost = fusionBaseCost × questLevel × D²).
- **Dungeons are timed wave rushes**: `DungeonGenerator` (deterministic from key + runCounter + questLevel; duration 150s + 20s/D, enemy ×(1+0.6·(D−1)), rarity floor D1 Unc → D5+ Legendary, mini-boss every 5th wave) + `DungeonRun` (countdown, waves cleared, bonus rolls per 3 waves over par, cap +3). All drops inside a dungeon carry the key's modifiers (`DropModifiers` → forced primary/secondary stats, color family, slot bias); end-of-run curated rolls via `DungeonRewardRoller` (floored, legendary-weighted at D4). Runs are not persisted: pause/quit banks cleared-wave rewards immediately.
- Key sources: active kills (`keyDropRate` 0.004), quest tiers (Elite/Hard 1 key, Boss 1 D2 key, Hard +25% extra). Keys sell for keySellBase × questLevel × D.

## 8. Managers (`Assets/Scripts/Managers/`)

`GameManager` (scene singleton): owns Party, VerbPool, ExplorationManager, LootInventory, gold, questLevel.
- `ProcessTick()`: combat tick → gold → loot drops → ability XP
- Events: `OnCombatTick`, `OnGoldChanged`, `OnWaveStarted`, `OnWaveCleared`, `OnVerbActivated`, `OnLootDropped`, `OnRosterChanged`, `OnKeysChanged`, `OnDungeonStarted`, `OnDungeonEnded`
- Player actions: `OnVerbTapped(slot)`, `SellItem`, `EquipItemFromInventory`, `AutoEquipCharacter`, `LevelUpAbility`, `BuildPartyFromRoster`, `StartDungeon(key)`, `FuseKeys(a,b)` (atomic: validates rules + gold before consuming), `SellKey`
- Save on pause/quit via `SaveManager`/`SaveData` (local JSON: roster, equipment, inventory, gold, quest level, pity counters)

## 9. UI (`Assets/Scripts/UI/`, 47 files)

Design system: `UiTheme` tokens (type floor 28px, touch >= 120px) + `UiFactory` primitives (Banner, LadderRow, ProgressBar, Image-based pips/chips — no Unicode glyphs in TMP) + `BottomSheet` (own Canvas, stacked sheets sort above open ones) + `ItemCardBuilder` v2, established in the 2026-07-01 mobile UI redesign.

- **Explore screen:** `ExploreSceneController`, `TopBarDisplay`, path indicator bar (travel/quest dual mode), `VerbBarDisplay` + `VerbCardAnimator`, `EnemyDisplayController`, `DamageNumberSpawner`, `GoldCounterAnimator`, `PartyPortraitStrip`, `LootToastFeed`, `QuestBannerDisplay`, `DungeonHudDisplay`, parallax background
- **Sheets:** `QuestOfferSheet`, `QuestCompletionSheet`, `DungeonCompletionSheet`, `ItemDetailSheet`, `KeyDetailSheet`, `KeyFusionSheet`, offline-earnings Welcome-back sheet
- **Party screen:** `PartyScreenController` (stat column + Train button left, doll center, portrait strip right), `RosterGridDisplay`, `CharacterFocusDisplay`, `EquipmentSlotsDisplay`, `EquipmentDrawer`, `ActionLoadoutDisplay`
- **Loot screen:** `LootScreenController` (sort tabs, mailbox notice card), `ItemDisplayData`, `ComparisonData`, `ItemIconFraming`
- **Quests screen:** `QuestsScreenController` (QUEST section: current-quest card + zone ladder; DESTINATIONS section: key pouch + fragment bar + Locations tease), `QuestPresenter`, `KeyPresenter` (pure presenters, unit-tested)
- **Shop screen:** `ShopScreenController` + `ShopPresenter` + `TavernPresenter` (BOOSTS / TAVERN / CHEST / PREMIUM). Tavern: 6 generated recruits on a 6h wall-clock rotation (`TavernStock` in Characters, persisted in save so stock never re-rolls mid-slot; price ≈ `tavernCostMinutes` of income), two-line rows with bare-headed roster-style portraits (`EquipmentDisplayMapper.ToDisplayList(bareHead)` — display-only, shared by all portrait call sites), `TavernRecruitSheet` full-body doll + total stats + verbs before buying; recruits auto-join an open active-party slot, else bench (roster cap 20)
- **Infrastructure:** `ScreenManager` + `BottomNavDisplay`, `NumberFormatter`, `StatTypeColors`, `SafeAreaAdapter`
- Canvas + TextMeshPro, CanvasScaler 1080x1920 match height. Surfaces are still flat placeholder blocks: art direction lands in Full UI Pass 2 (Sprint 12).

## 10. Editor & Local Tooling (`Assets/Editor/`, `tools/`)

- `tools/color_pools.py`: local review server for key color families — shows every "main" palette color per pool, supports selecting swatches and moving them between pools (including new workshop pools), saves to `Assets/Resources/Data/color_family_overrides.json` which `ColorManager` applies over the classifier at load. Its classifier port must stay in sync with `ColorFamily.cs` (both files carry the note). Workshop pool names outside the enum exclude those colors from key drop pools.
- `SetupGameManager` (Tools menu): creates config assets + GameManager + EventSystem fix
- `ExploreSceneBuilder` (Tools menu): full scene rebuild; always save scene after
- `ClearSave`: wipes local save
- `ArtTablesBaker` (Tools > Art Tables): Bake Now, Check Images. New PNGs under `Assets/Resources/Images/` get the paper-doll sprite import settings automatically (Sprite, 100 PPU, Bilinear, max 256, uncompressed, no mipmaps); this replaced the manual `SpriteImportFixer`.
- `tools/ArtTablesTests`: `dotnet test tools/ArtTablesTests` runs the art table tests headless; `--filter Category=RepoData` converts the real CSVs and images.
- `tools/migrate_art_tables.py`: one-time migration (run 2026-11-08): `0000` part cleanup, one folder per type, art JSON to CSV. Kept for history; its `csv` step refuses to run now that the JSON is gone.
- `tools/migrate_species_extra_parts.py`: one-time migration (run 2026-11-08) of `other_body_parts` + `species_skin_variance.csv` into `species_extra_parts.csv`. Kept for history.
- `CharacterStudioTools` (Tools > Character Studio): Create Scene, Build Linux, Build Windows (`Builds/CharacterStudio/`, gitignored; Mono, LZ4HC, 1600x900 window). It switches player settings for the build and restores them, puts the Editor back on its previous platform, and reopens an open scene whose unsaved flag is false so the build does not stop on a save prompt.

## 10b. Character Studio (`Assets/Scripts/Studio/`, `Assets/Scenes/CharacterStudio.unity`)

A standalone app for the artist (Windows `.exe`, Linux build; not in the game's scene list) that rolls and edits characters with the game's own classes and exports transparent PNGs. Design: `docs/plans/2026-11-08-character-studio-design.md`; plans and decisions P1-P34: `docs/plans/2026-11-08-character-studio-phase-b-plan.md`; status: `docs/plans/2026-11-08-character-studio-progress.md`.

- **Data:** reads the artist's repo clone directly (`StudioData`: the art table converter in memory, no baked asset; `DiskSpriteSource`: PNGs with the game's sprite settings). The studio render matches the game's render (`StudioPixelMatchTests`).
- **Bulk screen:** grid of 8-64 random characters (`StudioRoller`, Godot randomizer chances and per-type weights), species window, search (`ItemSearch`: token, file name, description word) and forced items (`ForcedItems`, Godot slot limits, hand rules), per-card species/skin/Select/Lock/Reroll/Edit, random settings window, export of selected or all at 1x-8x with trim (`StudioRenderer`, `PixelOps`, `ExportNamer`).
- **Single screen:** large preview with backgrounds, Body tab (species, palette colors, hair group/variant, face parts and extras with thumbnails, part color groups, optional extras) and Gear tab (type, variant thumbnails, colors, weapon parts, row reroll and lock), undo/redo (`EditHistory`).
- **Presets:** character, cast and random settings as readable JSON in `presets/` (`Presets`, `PresetStore`); loading checks every choice against the current art and lists problems in Warnings.
- **Library panel:** row forms for item types, weapons, species, part groups and extra parts (`LibraryEditor`: the converter refuses changes that add errors; asks before overwriting a file changed on disk), remove with its folder, Rescan with `.meta` writing (`MetaWriter`), Check images.
- **UI:** code-built uGUI (`StudioUi`), palette-only color pickers, `PickList` dropdowns (tall, fast scrolling, searchable).

---

## 11. Not Yet Implemented

**MVP remainder (Sprint 12):**

| System | Notes |
|---|---|
| Tutorial / FTUE | tap verbs, equip loot, accept a quest |
| Animation/juice pass | verb activation, loot drops, quest completion, transitions |
| Full UI Pass 2 | art-direct the flat placeholder surfaces; unify newest screens; re-run heuristics |
| Final balance pass | play-test-driven knob tuning (`tools/balance_sim.py`); destinations knobs are untuned guesses |
| Real ad/IAP backends | services are mocked: needs Unity Ads game id + 3 placements, Google Play + Purchasing 5.x device wiring, store metadata |
| Bug sweep + Android device build | performance check, icon/splash |

**Post-MVP:**

| System | Notes |
|---|---|
| Destinations Sprints D2-D3 | Locations (fragment consumer, procedural one-time visits, dialogue trees) + content/key art (`docs/plans/2026-07-02-destinations-design.md` §4, §8) |
| Prestige | `prestigeMultiplier` stubbed at 1.0 throughout |
| Verb drops/collection | design exists (`docs/verb-stat-system-design-doc.md`); verbs currently come with the character |
| Cloud save / analytics / Remote Config | not started |

The remaining-work roadmap lives in `docs/roadmap.md`.
