# Species Extra Parts Table: Design and Plan

2026-11-08 · Approved by Zach ("the new table is important and we should add it as this will become a growing problem if not fixed right now. proceed").

**Goal:** one table, `Assets/Data/ArtTables/species_extra_parts.csv`, that lists every species-specific extra drawing (wings, tails, horns, halo, hooves, fur tufts) and every species part with its own color, keyed by **part**, not by layer number. It replaces the `other_body_parts` column of `species.csv` and the whole of `species_skin_variance.csv`.

**Why:** the old rule (Godot `SkinVariance`, ported as `species_skin_variance.csv`) colors *everything on a layer* for a species. A layer means different things per species (134 is horns on a satyr and a halo on an angel), an extra could only be skin-colored or layer-colored, nothing could be optional, and the Unity port silently dropped one rule (rhinofolk's horn is drawn as facial detail, which the Unity `DisplayBuilder` never let a layer rule touch).

## Format

```
species,part,color,color_group,chance
satyr,0005-004,skinvar_satyr_1,fur,
satyr,legs,skinvar_satyr_1,fur,
satyr,0005-010,skinvar_satyr_2,horns,
angel,0014-134,skin,,
centaur,legs,skinvar_centaur_1,,
```

| Column | Meaning |
|---|---|
| `species` | a `species.csv` name |
| `part` | an extra drawing: a body image (`0014-134`), a part group (`x01`) or a full part token; **or** a `species.csv` part column (`legs`, `mouth`, `facial_detail`, ...) to give that part its own color |
| `color` | `skin`, `hair`, `eyes`, `facial_detail`, or a palette name from `palettes.csv` |
| `color_group` | rows of one species with the same group share one rolled color (they must name the same color); blank: the row rolls its own |
| `chance` | extras only: blank = always drawn; a number 0-1 = how often it is drawn |

## Decisions

| # | Decision | Why |
|---|---|---|
| E1 | One row per extra part and per own-colored part, file order kept. | Rules per species and per part; one-line diffs. |
| E2 | A `species.csv` with anything in `other_body_parts` is an **error** that points to the new file; the column is removed. `species_skin_variance.csv` is deleted. | No silent data loss in an old clone (D6: table problems fail the build). |
| E3 | Random rolls keep their old order: one roll per palette color group, in first-row order, right after the four body colors; chance rolls come last and only for chance < 1. The migration writes rows in the old variance set order. | `CharacterInstance` rebuilds appearance from the character id: saved characters keep their exact look. |
| E4 | A part with no row keeps today's color (extras: skin; hair/eyes/facial detail fields: their color; other fields: skin). A row's color overrides it for every layer of that part. | Rhinofolk's horn gets its palette back (Godot behavior); nothing else changes. |
| E5 | The studio: one color row per palette color group (named after its parts, "Extra part" for extras), a Show toggle for optional extras, presets store colors by group. The Library panel gets an **Extra parts** tab (key = species + part). | The artist edits it like the other tables. |

## Tasks

| Task | Files | Check |
|---|---|---|
| X1 Data layer | `ArtTableData.cs` (`SpeciesExtraPartRow`; `ArtSources.ExtraPartsCsv`), `ArtTablesConverter.cs` (`ConvertExtraParts`, `other_body_parts` guard), `ArtRepo.cs` | `dotnet test tools/ArtTablesTests` |
| X2 Data migration | new `tools/migrate_species_extra_parts.py` (one-time), `species.csv`, `species_extra_parts.csv`, delete `species_skin_variance.csv` | the script proves every old layer rule maps to whole parts; converter: 0 errors |
| X3 Display | `DisplayDataRegistry.cs` (`SpeciesPartRule`), `SpeciesInstanceData.cs` (`PartColors`, `OmittedExtras`), `DisplayBuilder.cs` (color per part) | EditMode tests; baseline: 540 bodies (27 species x 20 ids) render identically except rhinofolk's horn |
| X4 Studio | `BodyParts`, `SingleScreen`, `ThumbnailRenderer`, `StudioCharacter`, `Presets`, Library (`LibraryEditor`, `LibraryScreen`, `RowFormView`) | studio tests by reflection; play run |
| X5 Docs, bake, Linux build | `docs/implemented-systems.md`, progress doc; `ArtTablesBaker.Bake()` | build succeeds |
