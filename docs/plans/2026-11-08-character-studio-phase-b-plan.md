# Character Studio Phase B Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A standalone Character Studio app (Windows `.exe` for the artist, Linux build for us), built from this Unity project, that rolls and edits characters with the game's own classes and exports them as transparent PNGs.

**Architecture:** A `Starquill.Studio` assembly (Editor + Windows/Linux standalone only, never Android) and a `CharacterStudio` scene that is not in the game's scene list. The studio reads the artist's repo clone directly: `ArtRepo` + `ArtTablesConverter` (Phase A) feed `DisplayDataRegistry` and `EquipmentCatalog`, and a disk sprite source loads PNGs from the clone instead of `Resources`. Rendering is the game's `CharacterDisplay` + `DisplayBuilder`; the UI is uGUI built in code from Unity's standard control factories.

**Tech Stack:** Unity 6 (6000.3.8f1), C# 9, uGUI 2.0 + TextMeshPro, NUnit (Unity Test Runner, EditMode), Mono scripting backend for the standalone builds.

**Design of record:** `docs/plans/2026-11-08-character-studio-design.md` §2-§4 (D1, D8, D10, D11). **Phase A** (art tables) is done: `docs/plans/2026-11-08-character-studio-plan.md`.

---

## Scope of this document

Phase B is large, so it lands in stages, each ending at a checkpoint where Zach runs the Test Runner and tries the studio:

| Stage | Delivers | Detail here |
|---|---|---|
| **B1 Foundation** | assembly, disk sprites, data load from a repo folder, character model + roller, render + PNG export, minimal Bulk screen (Count, Randomize All, Equip Random, Lock, reroll, Export), repo picker, Warnings list, scene + Linux/Windows builds | **full (Tasks 1-13, CHECKPOINT D)** |
| B2 Bulk screen complete | species multi-select, search + Forced items chips, card species/skin, Settings window, selected-only export, export folder | **full (Tasks 14-23, CHECKPOINT E)**, written after CHECKPOINT D |
| B3 Single screen | Body and Gear tabs, variant thumbnails, color pickers, undo/redo | tasks 24-31 (P17: code in commits) |
| B4 Presets | Character, Cast, Random settings JSON | outline |
| B5 Library panel | new item type / species / part group, edit row, remove, Rescan, `.meta` writer, Excel lock | outline |
| B6 Release | GitHub Release packaging, artist README | outline |

Each later stage gets its detailed tasks after the previous checkpoint, written against the real code: B2 (Tasks 14-23) was written after CHECKPOINT D.

## Decisions made while planning (2026-11-08)

| # | Decision | Why |
|---|---|---|
| P1 | uGUI built in code with `DefaultControls` / `TMP_DefaultControls`, flat colored styling | The game's `UiFactory` has no dropdowns, text fields or scroll grids; UI Toolkit needs a theme asset and a second UI system. The standard factories build complete dropdown/input/scroll hierarchies at runtime. |
| P2 | No native file dialogs: the repo folder and export folder are typed or pasted into a text field, with an **Open folder** button | Unity has no runtime file dialog; a plugin adds a dependency for a once-per-install step. |
| P3 | Species size (`x_scale`/`y_scale`) is **not** applied | The game never draws it (backlog item in `docs/roadmap.md`, 2026-11-08); the studio must match the game (D1). |
| P4 | Facial hair / facial detail chances hide the part by passing the renderer a copy of the species data with that field blanked | Works for both group tokens (`f05`) and static tokens (`0006-087`); the game's `DisplayBuilder` stays untouched. |
| P5 | Studio settings and exports live in `Library/CharacterStudio/` in the Editor and next to the exe in a build | `Library/` is gitignored; next-to-exe matches the design's `presets/` folder. |
| P6 | Studio builds use `BuildOptions.CompressWithLz4HC` | Unity packs every `Resources` sprite into any build (the uncompressed art is 626 MB in a player build). The studio loads art from disk and cannot exclude `Resources`; compression is the cheap fix. Measure the size at CHECKPOINT D. |
| P7 | The studio's random gear follows the Godot randomizer: chance per slot (Head 0.9, Torso 1, Arms 0.8, Legs 1, Feet 0.7, Main hand 0.6, Off hand 0.3 when the main hand is empty or one-handed, each Misc slot 0.3 from `mc` items), facial hair 0.7, facial detail 0.8, weight 1 per item type, species `item_restrictions` never rolled | Jakob's Law: the artist's existing tool (`godot-archive/scripts/core/starquill_data.gd:30-46`, `equipment_factory.gd:121-200`). |

## Ground rules for the executor

- Branch `unity-idle-clicker`. Never commit to `main`. Do not push. No `Co-Authored-By` lines.
- **Unity tests are never run from a script.** Zach runs Window > General > Test Runner > EditMode > Run All at the checkpoint. The only headless tests are `dotnet test tools/ArtTablesTests` (Phase A code).
- Compile checks: after writing files, run an `AssetDatabase.Refresh()` through Coplay `execute_script` (script file in the scratchpad, never under `Assets/`), then `check_compile_errors`. "No compile errors" can be stale: confirm a new type exists with a reflection probe before trusting it (Phase A lesson).
- Commit the `.meta` files Unity creates for new files and folders.
- Studio code must not reference `Starquill.UI`, `Starquill.Managers` or `GameManager`: the studio never starts the game loop.
- Every new class gets an XML-free `///` summary line in the style of the existing code.

## Facts the executor needs

- Sprite paths: `ImageToken` builders return paths like `Images/equipment/hd01/hd01-0001-130` (no extension), relative to `Assets/Resources`.
- All art PNGs are 200×200 (one is 201×201: `species/body/0026-037.png`). `CharacterDisplay`'s camera is orthographic size 1 at 100 PPU, so a texture of `200 × k` pixels is the canvas at `k×`. The game renders at 400 (2×).
- Unity's `Sprites-Default` shader writes **premultiplied** color into the render texture; PNG export must un-premultiply.
- Removing a group from `SpeciesInstanceData.ModularImageNums` makes `DisplayBuilder` skip that part (`DisplayBuilder.cs:106-111`).
- `EquipmentInstance` is immutable; the studio keeps its own `StudioItem` per slot and converts to `EquipmentDisplayInfo` for drawing.
- `SpeciesDisplayData.ItemRestrictions` lists item types the species cannot wear; `DisplayBuilder` filters them at draw time.
- Shields are `w08`/`w09` (one-handed, non-modular). Two-handed: `w04 w05 w07`.
- Built-in render pipeline, Gamma color space, Input Manager only (EventSystem needs `StandaloneInputModule`).
- `TMP_DefaultControls` and `DefaultControls` exist in `com.unity.ugui` 2.0 (`Library/PackageCache/com.unity.ugui@*/Runtime`).

---

## Task 1: Studio assembly

**Files:**
- Create: `Assets/Scripts/Studio/Starquill.Studio.asmdef`
- Modify: `Assets/Tests/EditMode/EditModeTests.asmdef` (add the reference)
- Create: `Assets/Tests/EditMode/Studio/` (folder for Tasks 3-10 tests)

**Step 1: Create the asmdef**

```json
{
    "name": "Starquill.Studio",
    "rootNamespace": "Starquill.Studio",
    "references": [
        "Starquill.Core",
        "Starquill.Data",
        "Starquill.Display",
        "Starquill.Equipment",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [
        "Editor",
        "LinuxStandalone64",
        "WindowsStandalone64"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```
`includePlatforms` keeps the studio out of the Android game build. `autoReferenced` lets `Assets/Editor` (Task 13) see `StudioApp`.

**Step 2: Reference it from the tests**

In `EditModeTests.asmdef` add `"Starquill.Studio"` to `references` (after `"Starquill.UI"`).

**Step 3: Verify**

The assembly compiles once it has a file (Task 3). Nothing to run yet. No commit until Task 2 (an asmdef with no scripts is fine to commit with Task 2).

---

## Task 2: Sprite source behind `ImageResolver`

**Files:**
- Create: `Assets/Scripts/Display/ISpriteSource.cs`
- Modify: `Assets/Scripts/Display/ImageResolver.cs`
- Modify: `Assets/Scripts/Display/CharacterDisplay.cs` (Initialize overload)
- Create: `Assets/Tests/EditMode/Display/ImageResolverTests.cs`

**Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Tests.Display
{
    public class ImageResolverTests
    {
        private sealed class CountingSource : ISpriteSource
        {
            public int Calls;
            public Sprite Result;
            public Sprite Load(string spritePath) { Calls++; return Result; }
        }

        [Test]
        public void Resolve_AsksTheSourceOnceThenCaches()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            var source = new CountingSource { Result = sprite };
            var resolver = new ImageResolver(source);

            Assert.AreSame(sprite, resolver.Resolve("Images/a"));
            Assert.AreSame(sprite, resolver.Resolve("Images/a"));
            Assert.AreEqual(1, source.Calls);

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Resolve_MissingSpriteIsRecorded()
        {
            var resolver = new ImageResolver(new CountingSource());

            Assert.IsNull(resolver.Resolve("Images/equipment/hd01/hd01-0999-130"));

            CollectionAssert.Contains(resolver.MissingPaths, "Images/equipment/hd01/hd01-0999-130");
        }

        [Test]
        public void ClearCache_ForgetsMissingPaths()
        {
            var resolver = new ImageResolver(new CountingSource());
            resolver.Resolve("Images/x");

            resolver.ClearCache();

            Assert.AreEqual(0, resolver.MissingPaths.Count);
        }
    }
}
```

**Step 2: Compile-check to see it fail**

Refresh + `check_compile_errors`: errors for `ISpriteSource`, the `ImageResolver(ISpriteSource)` constructor and `MissingPaths`.

**Step 3: Implement**

`Assets/Scripts/Display/ISpriteSource.cs`:
```csharp
using UnityEngine;

namespace Starquill.Display
{
    /// Where ImageResolver gets sprites. The game loads from Resources;
    /// the Character Studio loads PNGs from the artist's repo clone.
    public interface ISpriteSource
    {
        /// Null when the sprite does not exist.
        Sprite Load(string spritePath);
    }

    public sealed class ResourcesSpriteSource : ISpriteSource
    {
        public Sprite Load(string spritePath) => Resources.Load<Sprite>(spritePath);
    }
}
```

`ImageResolver.cs` becomes:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Starquill.Display
{
    public class ImageResolver
    {
        private readonly Dictionary<string, Sprite> cache = new();
        private readonly HashSet<string> missing = new();
        private readonly ISpriteSource source;
        private int missCount;

        public ImageResolver() : this(new ResourcesSpriteSource()) { }

        public ImageResolver(ISpriteSource source)
        {
            this.source = source ?? new ResourcesSpriteSource();
        }

        /// Sprite paths that were asked for and not found (studio Warnings list).
        public IReadOnlyCollection<string> MissingPaths => missing;

        public Sprite Resolve(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
                return null;

            if (cache.TryGetValue(spritePath, out var cached))
                return cached;

            var sprite = source.Load(spritePath);
            if (sprite == null)
            {
                missing.Add(spritePath);
                missCount++;
                if (missCount <= 10)
                    Debug.LogWarning($"ImageResolver: Sprite not found at '{spritePath}'");
                else if (missCount == 11)
                    Debug.LogWarning("ImageResolver: Suppressing further missing sprite warnings");
            }

            cache[spritePath] = sprite;
            return sprite;
        }

        public void ClearCache()
        {
            cache.Clear();
            missing.Clear();
            missCount = 0;
        }

        public int CachedCount => cache.Count;
    }
}
```

`CharacterDisplay.cs`: add below the existing `Initialize`:
```csharp
        /// size: render texture width and height in pixels (200 = the 1x canvas).
        public void Initialize(ImageResolver resolver, int size)
        {
            textureSize = size;
            Initialize(resolver);
        }
```

**Step 4: Compile**

Refresh + `check_compile_errors`: none. Probe that `ImageResolver` has a constructor taking `ISpriteSource`.

**Step 5: Commit**

```bash
git add Assets/Scripts/Display/ISpriteSource.cs Assets/Scripts/Display/ISpriteSource.cs.meta Assets/Scripts/Display/ImageResolver.cs Assets/Scripts/Display/CharacterDisplay.cs Assets/Tests/EditMode/Display/ImageResolverTests.cs Assets/Tests/EditMode/Display/ImageResolverTests.cs.meta Assets/Scripts/Studio Assets/Scripts/Studio.meta Assets/Tests/EditMode/EditModeTests.asmdef
git commit -m "feat: sprite source behind ImageResolver; CharacterDisplay texture size; studio assembly"
```

---

## Task 3: Pixel operations for export

**Files:**
- Create: `Assets/Scripts/Studio/PixelOps.cs`
- Create: `Assets/Tests/EditMode/Studio/PixelOpsTests.cs`

**Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class PixelOpsTests
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [Test]
        public void Unpremultiply_RestoresStraightColor()
        {
            var px = new[] { new Color32(64, 32, 0, 128), new Color32(10, 20, 30, 255), new Color32(9, 9, 9, 0) };

            PixelOps.Unpremultiply(px);

            Assert.AreEqual(new Color32(128, 64, 0, 128), px[0]);
            Assert.AreEqual(new Color32(10, 20, 30, 255), px[1]);
            Assert.AreEqual(Clear, px[2]);
        }

        [Test]
        public void AlphaBounds_FindsTheOpaqueBox_OrNullWhenEmpty()
        {
            var px = new Color32[4 * 4];
            Assert.IsNull(PixelOps.AlphaBounds(px, 4, 4));

            px[2 * 4 + 1] = new Color32(255, 0, 0, 255); // x=1, y=2
            px[3 * 4 + 2] = new Color32(255, 0, 0, 10);  // x=2, y=3

            Assert.AreEqual(new RectInt(1, 2, 2, 2), PixelOps.AlphaBounds(px, 4, 4));
        }

        [Test]
        public void Crop_KeepsOnlyTheRect()
        {
            var px = new Color32[3 * 2];
            px[1 * 3 + 2] = new Color32(1, 2, 3, 4); // x=2, y=1

            var cropped = PixelOps.Crop(px, 3, new RectInt(2, 1, 1, 1));

            Assert.AreEqual(1, cropped.Length);
            Assert.AreEqual(new Color32(1, 2, 3, 4), cropped[0]);
        }

        [Test]
        public void Pad_AddsClearBorder()
        {
            var px = new[] { new Color32(5, 5, 5, 255) };

            var padded = PixelOps.Pad(px, 1, 1, 2, out int w, out int h);

            Assert.AreEqual(5, w);
            Assert.AreEqual(5, h);
            Assert.AreEqual(new Color32(5, 5, 5, 255), padded[2 * 5 + 2]);
            Assert.AreEqual(Clear, padded[0]);
        }

        [Test]
        public void BleedTransparentEdges_GivesClearNeighboursTheOpaqueColor_OnePass()
        {
            var px = new[] { new Color32(200, 0, 0, 255), Clear, Clear };

            PixelOps.BleedTransparentEdges(px, 3, 1);

            Assert.AreEqual(new Color32(200, 0, 0, 0), px[1]);
            Assert.AreEqual(Clear, px[2]);
            Assert.AreEqual(new Color32(200, 0, 0, 255), px[0]);
        }
    }
}
```

**Step 2: Compile-check to see it fail** (`PixelOps` not found).

**Step 3: Implement**

`Assets/Scripts/Studio/PixelOps.cs`:
```csharp
using System;
using UnityEngine;

namespace Starquill.Studio
{
    /// Pixel helpers for PNG export. Arrays are Unity order: row 0 is the
    /// bottom row, index = y * width + x.
    public static class PixelOps
    {
        /// Unity's sprite shader writes premultiplied color; PNG wants straight alpha.
        public static void Unpremultiply(Color32[] px)
        {
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 255) continue;
                if (c.a == 0) { px[i] = new Color32(0, 0, 0, 0); continue; }
                px[i] = new Color32(Div(c.r, c.a), Div(c.g, c.a), Div(c.b, c.a), c.a);
            }
        }

        private static byte Div(byte v, byte a) => (byte)Math.Min(255, (v * 255 + a / 2) / a);

        /// Smallest rect holding every pixel with alpha > 0; null if none.
        public static RectInt? AlphaBounds(Color32[] px, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX < 0) return null;
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public static Color32[] Crop(Color32[] px, int w, RectInt r)
        {
            var result = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                Array.Copy(px, (r.y + y) * w + r.x, result, y * r.width, r.width);
            return result;
        }

        public static Color32[] Pad(Color32[] px, int w, int h, int pad, out int newW, out int newH)
        {
            newW = w + pad * 2;
            newH = h + pad * 2;
            var result = new Color32[newW * newH];
            for (int y = 0; y < h; y++)
                Array.Copy(px, y * w, result, (y + pad) * newW + pad, w);
            return result;
        }

        /// One pass: each fully transparent pixel takes the average color of its
        /// opaque 4-neighbours (alpha stays 0). Approximates the importer's
        /// "alpha is transparency", so bilinear edges do not pick up black.
        public static void BleedTransparentEdges(Color32[] px, int w, int h)
        {
            var src = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (src[i].a != 0) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    void Add(int xx, int yy)
                    {
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) return;
                        var c = src[yy * w + xx];
                        if (c.a == 0) return;
                        r += c.r; g += c.g; b += c.b; n++;
                    }
                    Add(x - 1, y); Add(x + 1, y); Add(x, y - 1); Add(x, y + 1);
                    if (n > 0) px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                }
        }
    }
}
```

**Step 4: Compile** (none; probe `Starquill.Studio.PixelOps`).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/PixelOps.cs* Assets/Tests/EditMode/Studio.meta Assets/Tests/EditMode/Studio/PixelOpsTests.cs*
git commit -m "feat(studio): pixel helpers for PNG export (unpremultiply, trim, pad, edge bleed)"
```

---

## Task 4: Disk sprite source

**Files:**
- Create: `Assets/Scripts/Studio/DiskSpriteSource.cs`
- Create: `Assets/Tests/EditMode/Studio/DiskSpriteSourceTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.IO;
using NUnit.Framework;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class DiskSpriteSourceTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "studio_" + Path.GetRandomFileName());
            string dir = Path.Combine(root, "Assets", "Resources", "Images", "equipment", "hd01");
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[16]);
            tex.SetPixel(1, 1, Color.red);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "hd01-0001-130.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void FilePathFor_MapsSpritePathIntoTheRepo()
        {
            string expected = Path.Combine(root, "Assets", "Resources", "Images", "equipment", "hd01", "hd01-0001-130.png");
            Assert.AreEqual(expected, DiskSpriteSource.FilePathFor(root, "Images/equipment/hd01/hd01-0001-130"));
        }

        [Test]
        public void Load_ReadsPngAsSpriteAt100PixelsPerUnit()
        {
            using var source = new DiskSpriteSource(root);

            var sprite = source.Load("Images/equipment/hd01/hd01-0001-130");

            Assert.IsNotNull(sprite);
            Assert.AreEqual(4, (int)sprite.rect.width);
            Assert.AreEqual(100f, sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Bilinear, sprite.texture.filterMode);
        }

        [Test]
        public void Load_MissingFileIsNull()
        {
            using var source = new DiskSpriteSource(root);
            Assert.IsNull(source.Load("Images/equipment/hd01/hd01-0002-130"));
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/DiskSpriteSource.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Starquill.Display;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Starquill.Studio
{
    /// Loads art PNGs from a repo clone (<repo>/Assets/Resources/<spritePath>.png)
    /// with the game's sprite import settings: 100 PPU, center pivot, bilinear,
    /// clamp, full-rect mesh. Dispose frees every texture it created.
    public sealed class DiskSpriteSource : ISpriteSource, IDisposable
    {
        private readonly string repoRoot;
        private readonly List<Object> created = new();

        public DiskSpriteSource(string repoRoot) { this.repoRoot = repoRoot; }

        public static string FilePathFor(string repoRoot, string spritePath) =>
            Path.Combine(repoRoot, "Assets", "Resources", spritePath.Replace('/', Path.DirectorySeparatorChar) + ".png");

        public Sprite Load(string spritePath)
        {
            string file = FilePathFor(repoRoot, spritePath);
            if (!File.Exists(file)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(file)))
            {
                Kill(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = tex.GetPixels32();
            PixelOps.BleedTransparentEdges(px, tex.width, tex.height);
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            created.Add(tex);
            created.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var o in created) Kill(o);
            created.Clear();
        }

        internal static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
```

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/DiskSpriteSource.cs* Assets/Tests/EditMode/Studio/DiskSpriteSourceTests.cs*
git commit -m "feat(studio): load art sprites from a repo clone on disk"
```

---

## Task 5: Studio data from a repo folder

**Files:**
- Create: `Assets/Scripts/Studio/StudioData.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioTestRepo.cs` (shared loaded data for tests)
- Create: `Assets/Tests/EditMode/Studio/StudioDataTests.cs`

**Step 1: Write the failing tests**

`StudioTestRepo.cs`:
```csharp
using System.IO;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    /// The real repo's art, loaded once for all studio tests.
    public static class StudioTestRepo
    {
        public static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static StudioData data;
        public static StudioData Data => data ??= StudioData.Load(Root);
    }
}
```

`StudioDataTests.cs`:
```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioDataTests
    {
        [Test]
        public void Load_RealRepo_FillsRegistryAndCatalog()
        {
            var d = StudioTestRepo.Data;

            Assert.AreEqual(27, d.SpeciesNames.Count);
            Assert.AreEqual(49, d.Catalog.AllEntries.Count);
            Assert.AreEqual(9, d.Catalog.WeaponEntries.Count);
            Assert.IsTrue(d.Registry.Species.ContainsKey("human"));
            Assert.IsFalse(d.Issues.Any(i => i.level == ArtIssueLevel.Error), string.Join("\n", d.Issues));
        }

        [Test]
        public void SpeciesNames_AreSorted()
        {
            var names = StudioTestRepo.Data.SpeciesNames;
            CollectionAssert.AreEqual(names.OrderBy(n => n, System.StringComparer.Ordinal).ToList(), names);
        }

        [Test]
        public void LooksLikeRepo_NeedsTablesAndImages()
        {
            Assert.IsTrue(StudioData.LooksLikeRepo(StudioTestRepo.Root));
            Assert.IsFalse(StudioData.LooksLikeRepo(Path.GetTempPath()));
            Assert.IsFalse(StudioData.LooksLikeRepo(""));
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/StudioData.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Data.Art;
using Starquill.Display;
using Starquill.Equipment;

namespace Starquill.Studio
{
    /// The art tables of one repo clone, converted in memory (no baked asset),
    /// loaded into the game's own registry and catalog.
    public sealed class StudioData
    {
        public string RepoRoot { get; }
        public DisplayDataRegistry Registry { get; } = new DisplayDataRegistry();
        public EquipmentCatalog Catalog { get; } = new EquipmentCatalog();
        public List<ArtIssue> Issues { get; } = new List<ArtIssue>();
        public List<string> SpeciesNames { get; } = new List<string>();

        private StudioData(string repoRoot) { RepoRoot = repoRoot; }

        public static bool LooksLikeRepo(string root) =>
            !string.IsNullOrWhiteSpace(root)
            && Directory.Exists(Path.Combine(root, ArtRepo.TablesDir))
            && Directory.Exists(Path.Combine(root, ArtRepo.ImagesDir));

        public static StudioData Load(string repoRoot)
        {
            var d = new StudioData(repoRoot);
            d.Reload();
            return d;
        }

        /// Rereads CSVs and image folders (the Library panel's Rescan, B5).
        public void Reload()
        {
            var result = ArtTablesConverter.Convert(ArtRepo.ReadSources(RepoRoot));
            Issues.Clear();
            Issues.AddRange(result.Issues);
            Registry.Load(result.Data);
            Catalog.Load(result.Data);
            SpeciesNames.Clear();
            SpeciesNames.AddRange(Registry.Species.Keys.OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
```

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioData.cs* Assets/Tests/EditMode/Studio/StudioTestRepo.cs* Assets/Tests/EditMode/Studio/StudioDataTests.cs*
git commit -m "feat(studio): load art tables from a repo folder into the game's registry and catalog"
```

---

## Task 6: Studio character model

**Files:**
- Create: `Assets/Scripts/Studio/StudioCharacter.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioCharacterTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioCharacterTests
    {
        [Test]
        public void ToDisplayList_SlotOrderAndOffhandFlag()
        {
            var c = new StudioCharacter { SpeciesName = "human" };
            c.Gear[EquipmentSlot.OffHand] = new StudioItem { ItemType = "w08", ItemNum = 1 };
            c.Gear[EquipmentSlot.Head] = new StudioItem { ItemType = "hd01", ItemNum = 3, BaseColor = Color.red };

            var list = c.ToDisplayList();

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("hd01", list[0].ItemType);
            Assert.AreEqual(3, list[0].ItemNum);
            Assert.AreEqual(Color.red, list[0].BaseColor);
            Assert.IsFalse(list[0].IsOffhand);
            Assert.AreEqual("w08", list[1].ItemType);
            Assert.IsTrue(list[1].IsOffhand);
        }

        [Test]
        public void ToDisplayList_CopiesVarianceColors()
        {
            var c = new StudioCharacter { SpeciesName = "human" };
            var item = new StudioItem { ItemType = "hd08", ItemNum = 1 };
            item.VarianceColors[160] = Color.blue;
            c.Gear[EquipmentSlot.Head] = item;

            var info = c.ToDisplayList()[0];
            item.VarianceColors[160] = Color.green;

            Assert.AreEqual(Color.blue, info.VarianceColors[160]);
        }

        [Test]
        public void SpeciesView_BlanksHiddenFacialParts()
        {
            var species = new SpeciesDisplayData { Name = "human", FacialHair = "f05", FacialDetail = "0006-087", Eyes = "f01" };
            var c = new StudioCharacter { SpeciesName = "human", HideFacialHair = true };

            var view = c.SpeciesView(species);

            Assert.AreEqual("", view.FacialHair);
            Assert.AreEqual("0006-087", view.FacialDetail);
            Assert.AreEqual("f01", view.Eyes);
            Assert.AreEqual("f05", species.FacialHair, "the registry's data is never changed");
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/StudioCharacter.cs`:
```csharp
using System;
using System.Collections.Generic;
using Starquill.Core;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Studio
{
    /// One gear choice: what the game's EquipmentDisplayInfo needs, editable.
    public sealed class StudioItem
    {
        public string ItemType;
        public int ItemNum;
        public Color BaseColor = Color.white;
        public Dictionary<int, Color> VarianceColors = new Dictionary<int, Color>();
        public int[] LayerVariants; // modular weapons only, parallel to layer codes
    }

    /// A character on the studio's grid: species appearance (the game's
    /// SpeciesInstanceData), gear per slot, and studio-only flags.
    public sealed class StudioCharacter
    {
        public string SpeciesName;
        public SpeciesInstanceData Body;
        public readonly Dictionary<EquipmentSlot, StudioItem> Gear = new Dictionary<EquipmentSlot, StudioItem>();
        public bool HideFacialHair, HideFacialDetail;
        public bool Locked;

        public List<EquipmentDisplayInfo> ToDisplayList()
        {
            var list = new List<EquipmentDisplayInfo>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!Gear.TryGetValue(slot, out var item) || item == null) continue;
                list.Add(new EquipmentDisplayInfo
                {
                    ItemType = item.ItemType,
                    ItemNum = item.ItemNum,
                    BaseColor = item.BaseColor,
                    VarianceColors = new Dictionary<int, Color>(item.VarianceColors),
                    LayerVariants = item.LayerVariants,
                    IsOffhand = slot == EquipmentSlot.OffHand
                });
            }
            return list;
        }

        /// The species data to draw with: a copy with hidden facial parts blanked.
        public SpeciesDisplayData SpeciesView(SpeciesDisplayData s)
        {
            if (!HideFacialHair && !HideFacialDetail) return s;
            return new SpeciesDisplayData
            {
                Name = s.Name,
                BackArm = s.BackArm, Body = s.Body, Ears = s.Ears, Eyes = s.Eyes,
                FacialDetail = HideFacialDetail ? "" : s.FacialDetail,
                FacialHair = HideFacialHair ? "" : s.FacialHair,
                FrontArm = s.FrontArm, Head = s.Head, Legs = s.Legs, Mouth = s.Mouth, Nose = s.Nose,
                Hair = s.Hair, OtherBodyParts = s.OtherBodyParts, ItemRestrictions = s.ItemRestrictions,
                SkinColor = s.SkinColor, HairColor = s.HairColor, EyesColor = s.EyesColor,
                FacialDetailColor = s.FacialDetailColor, SkinVarianceSets = s.SkinVarianceSets,
                XScale = s.XScale, YScale = s.YScale, HeadYOffset = s.HeadYOffset, HeadZoom = s.HeadZoom
            };
        }
    }
}
```
If `SpeciesDisplayData` gains a field later, `SpeciesView` must copy it; the field list mirrors `DisplayDataRegistry.cs:7-23`.

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioCharacter.cs* Assets/Tests/EditMode/Studio/StudioCharacterTests.cs*
git commit -m "feat(studio): character model with per-slot gear and hideable facial parts"
```

---

## Task 7: Random settings and roller

**Files:**
- Create: `Assets/Scripts/Studio/RandomSettings.cs`
- Create: `Assets/Scripts/Studio/StudioRoller.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioRollerTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioRollerTests
    {
        private static StudioData D => StudioTestRepo.Data;
        private static StudioRoller Roller => new StudioRoller(D);

        private static RandomSettings Never()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 0f;
            return s;
        }

        private static RandomSettings Always()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            s.FacialHair = s.FacialDetail = 1f;
            return s;
        }

        [Test]
        public void ChancesZero_NoGear()
        {
            var c = Roller.RollCharacter(new[] { "human" }, Never(), new System.Random(1));
            Assert.AreEqual(0, c.Gear.Count);
            Assert.AreEqual("human", c.SpeciesName);
            Assert.IsNotNull(c.Body);
        }

        [Test]
        public void ChancesOne_FillsArmorMainHandAndMisc()
        {
            var c = Roller.RollCharacter(new[] { "human" }, Always(), new System.Random(2));
            foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Torso, EquipmentSlot.Arms, EquipmentSlot.Legs,
                         EquipmentSlot.Feet, EquipmentSlot.MainHand, EquipmentSlot.Misc1, EquipmentSlot.Misc2,
                         EquipmentSlot.Misc3, EquipmentSlot.Misc4 })
                Assert.IsTrue(c.Gear.ContainsKey(slot), slot.ToString());
            StringAssert.StartsWith("mc", c.Gear[EquipmentSlot.Misc1].ItemType);
        }

        [Test]
        public void TwoHandedMainHand_NeverHasOffHand_ShieldsNeverInMainHand()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 500; i++)
            {
                var c = Roller.RollCharacter(new[] { "human" }, Always(), rng);
                var main = D.Catalog.GetWeapon(c.Gear[EquipmentSlot.MainHand].ItemType);
                Assert.IsFalse(main.ItemType == "w08" || main.ItemType == "w09");
                if (main.HandType == "two_handed") Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.OffHand));
                else Assert.IsTrue(c.Gear.ContainsKey(EquipmentSlot.OffHand));
            }
        }

        [Test]
        public void EveryRolledLayer_HasItsImageOnDisk()
        {
            var rng = new System.Random(4);
            for (int i = 0; i < 300; i++)
            {
                var c = Roller.RollCharacter(D.SpeciesNames, Always(), rng);
                foreach (var kv in c.Gear)
                {
                    var item = kv.Value;
                    var weapon = D.Catalog.GetWeapon(item.ItemType);
                    var codes = weapon != null ? weapon.LayerCodes : D.Catalog.GetEntry(item.ItemType).LayerCodes;
                    for (int l = 0; l < codes.Length; l++)
                    {
                        string path = weapon != null
                            ? ImageToken.BuildWeaponSpritePath(item.ItemType, codes[l], item.LayerVariants != null ? item.LayerVariants[l] : item.ItemNum)
                            : ImageToken.BuildEquipmentSpritePath(item.ItemType, item.ItemNum, codes[l]);
                        Assert.IsTrue(File.Exists(DiskSpriteSource.FilePathFor(StudioTestRepo.Root, path)), path);
                    }
                }
            }
        }

        [Test]
        public void ItemRestrictions_AreNeverRolled()
        {
            var restricted = D.Registry.Species["stone golem"].ItemRestrictions;
            var rng = new System.Random(5);
            for (int i = 0; i < 300; i++)
            {
                var c = Roller.RollCharacter(new[] { "stone golem" }, Always(), rng);
                foreach (var item in c.Gear.Values) CollectionAssert.DoesNotContain(restricted, item.ItemType);
            }
        }

        [Test]
        public void TypeWeightZero_IsNeverPicked()
        {
            var s = Always();
            foreach (var e in D.Catalog.GetByPrefix("hd").Where(e => e.ItemType != "hd03")) s.TypeWeights[e.ItemType] = 0f;
            var rng = new System.Random(6);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual("hd03", Roller.RollCharacter(new[] { "human" }, s, rng).Gear[EquipmentSlot.Head].ItemType);
        }

        [Test]
        public void FacialChances_HideOrShowTheParts()
        {
            var s = Always();
            s.FacialHair = 0f;
            var c = Roller.RollCharacter(new[] { "human" }, s, new System.Random(7));
            Assert.IsTrue(c.HideFacialHair);
            Assert.IsFalse(c.HideFacialDetail);
        }

        [Test]
        public void Gear_UsesOnlyTheChosenSpecies()
        {
            var rng = new System.Random(8);
            var picks = Enumerable.Range(0, 200).Select(_ => Roller.RollCharacter(new[] { "dwarf", "naga" }, Never(), rng).SpeciesName).Distinct().OrderBy(x => x);
            CollectionAssert.AreEqual(new[] { "dwarf", "naga" }, picks);
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/RandomSettings.cs`:
```csharp
using System;
using System.Collections.Generic;
using Starquill.Core;

namespace Starquill.Studio
{
    /// Chances and weights for random characters. Defaults copy the Godot
    /// randomizer (godot-archive/scripts/core/starquill_data.gd:30-46).
    public sealed class RandomSettings
    {
        public float Head = 0.9f, Torso = 1f, Arms = 0.8f, Legs = 1f, Feet = 0.7f;
        public float MainHand = 0.6f;
        /// Only when the main hand is empty or one-handed.
        public float OffHand = 0.3f;
        /// Each of Misc 1-4, filled from mc items.
        public float MiscEach = 0.3f;
        public float FacialHair = 0.7f, FacialDetail = 0.8f;
        /// Item type -> weight within its slot; missing means 1, 0 means never.
        public Dictionary<string, float> TypeWeights = new Dictionary<string, float>();

        public float WeightOf(string itemType) =>
            TypeWeights.TryGetValue(itemType, out var w) ? Math.Max(0f, w) : 1f;

        public float ChanceFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return Head;
                case EquipmentSlot.Torso: return Torso;
                case EquipmentSlot.Arms: return Arms;
                case EquipmentSlot.Legs: return Legs;
                case EquipmentSlot.Feet: return Feet;
                case EquipmentSlot.MainHand: return MainHand;
                case EquipmentSlot.OffHand: return OffHand;
                default: return MiscEach;
            }
        }
    }
}
```

`Assets/Scripts/Studio/StudioRoller.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Starquill.Core;
using Starquill.Display;
using Starquill.Equipment;

namespace Starquill.Studio
{
    /// Random bodies and gear for studio characters, from the game's registry,
    /// catalog and palettes. Variant numbers come only from files on disk.
    public sealed class StudioRoller
    {
        private static readonly (EquipmentSlot slot, string prefix)[] ArmorSlots =
        {
            (EquipmentSlot.Head, "hd"), (EquipmentSlot.Torso, "tr"), (EquipmentSlot.Arms, "ar"),
            (EquipmentSlot.Legs, "lg"), (EquipmentSlot.Feet, "fe")
        };

        private static readonly EquipmentSlot[] MiscSlots =
            { EquipmentSlot.Misc1, EquipmentSlot.Misc2, EquipmentSlot.Misc3, EquipmentSlot.Misc4 };

        private readonly StudioData data;

        public StudioRoller(StudioData data) { this.data = data; }

        public StudioCharacter RollCharacter(IReadOnlyList<string> species, RandomSettings s, Random rng)
        {
            var c = new StudioCharacter { SpeciesName = species[rng.Next(species.Count)] };
            RollBody(c, s, rng);
            RollGear(c, s, rng);
            return c;
        }

        /// New appearance for the character's current species.
        public void RollBody(StudioCharacter c, RandomSettings s, Random rng)
        {
            var species = data.Registry.Species[c.SpeciesName];
            c.Body = SpeciesInstanceData.CreateFrom(species, data.Registry, rng);
            c.HideFacialHair = !string.IsNullOrEmpty(species.FacialHair) && rng.NextDouble() >= s.FacialHair;
            c.HideFacialDetail = !string.IsNullOrEmpty(species.FacialDetail) && rng.NextDouble() >= s.FacialDetail;
        }

        /// Strips and rerolls gear only.
        public void RollGear(StudioCharacter c, RandomSettings s, Random rng)
        {
            c.Gear.Clear();
            var restricted = new HashSet<string>(data.Registry.Species[c.SpeciesName].ItemRestrictions ?? Array.Empty<string>());

            foreach (var (slot, prefix) in ArmorSlots)
                if (rng.NextDouble() < s.ChanceFor(slot))
                    PutArmor(c, slot, prefix, restricted, s, rng);

            WeaponEntry main = null;
            if (rng.NextDouble() < s.MainHand)
            {
                main = PickWeighted(data.Catalog.WeaponEntries
                    .Where(w => !IsShield(w.ItemType) && !restricted.Contains(w.ItemType)).ToList(), s, rng);
                if (main != null) c.Gear[EquipmentSlot.MainHand] = MakeWeapon(main, rng);
            }

            bool twoHanded = main != null && main.HandType == "two_handed";
            if (!twoHanded && rng.NextDouble() < s.OffHand)
            {
                var off = PickWeighted(data.Catalog.WeaponEntries
                    .Where(w => w.HandType == "one_handed" && !restricted.Contains(w.ItemType)).ToList(), s, rng);
                if (off != null) c.Gear[EquipmentSlot.OffHand] = MakeWeapon(off, rng);
            }

            foreach (var slot in MiscSlots)
                if (rng.NextDouble() < s.MiscEach)
                    PutArmor(c, slot, "mc", restricted, s, rng);
        }

        private void PutArmor(StudioCharacter c, EquipmentSlot slot, string prefix, HashSet<string> restricted, RandomSettings s, Random rng)
        {
            var entry = PickWeighted(data.Catalog.GetByPrefix(prefix).Where(e => !restricted.Contains(e.ItemType)).ToList(), s, rng);
            if (entry != null) c.Gear[slot] = MakeArmor(entry, rng);
        }

        public StudioItem MakeArmor(CatalogEntry entry, Random rng)
        {
            var item = new StudioItem
            {
                ItemType = entry.ItemType,
                ItemNum = entry.Variants[rng.Next(entry.Variants.Length)],
                BaseColor = data.Registry.Colors.GetRandomColor("main", rng)
            };
            AddVariance(item, entry.LayerColorVariance, rng);
            return item;
        }

        public StudioItem MakeWeapon(WeaponEntry entry, Random rng)
        {
            var item = new StudioItem { ItemType = entry.ItemType, BaseColor = data.Registry.Colors.GetRandomColor("main", rng) };
            if (entry.Modular)
            {
                item.LayerVariants = entry.VariantsPerLayer.Select(v => v[rng.Next(v.Length)]).ToArray();
                item.ItemNum = item.LayerVariants[0];
            }
            else
            {
                var shared = entry.VariantsPerLayer[0];
                item.ItemNum = shared[rng.Next(shared.Length)];
            }
            AddVariance(item, entry.LayerColorVariance, rng);
            return item;
        }

        private void AddVariance(StudioItem item, int[] layers, Random rng)
        {
            if (layers == null) return;
            foreach (int layer in layers)
                item.VarianceColors[layer] = data.Registry.Colors.GetRandomColor("main", rng);
        }

        private static bool IsShield(string itemType) => itemType == "w08" || itemType == "w09";

        private static T PickWeighted<T>(List<T> items, RandomSettings s, Random rng) where T : class
        {
            float total = 0f;
            foreach (var it in items) total += s.WeightOf(TypeOf(it));
            if (total <= 0f) return null;
            double roll = rng.NextDouble() * total;
            foreach (var it in items)
            {
                roll -= s.WeightOf(TypeOf(it));
                if (roll < 0) return it;
            }
            return items.LastOrDefault(it => s.WeightOf(TypeOf(it)) > 0f);
        }

        private static string TypeOf(object it) =>
            it is CatalogEntry e ? e.ItemType : ((WeaponEntry)it).ItemType;
    }
}
```

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/RandomSettings.cs* Assets/Scripts/Studio/StudioRoller.cs* Assets/Tests/EditMode/Studio/StudioRollerTests.cs*
git commit -m "feat(studio): random characters with per-slot chances, type weights and species restrictions"
```

---

## Task 8: Renderer, PNG export, file names

**Files:**
- Create: `Assets/Scripts/Studio/StudioRenderer.cs`
- Create: `Assets/Scripts/Studio/ExportNamer.cs`
- Create: `Assets/Tests/EditMode/Studio/ExportNamerTests.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioRendererTests.cs`

**Step 1: Write the failing tests**

`ExportNamerTests.cs`:
```csharp
using System;
using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ExportNamerTests
    {
        [Test]
        public void FileName_SlugIndexDate()
        {
            Assert.AreEqual("stone-golem_03_2026-11-08.png", ExportNamer.FileName("Stone Golem", 3, new DateTime(2026, 11, 8)));
        }

        [Test]
        public void UniquePath_NeverOverwrites()
        {
            string dir = Path.Combine(Path.GetTempPath(), "names_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                Assert.AreEqual(Path.Combine(dir, "a.png"), ExportNamer.UniquePath(dir, "a.png"));
                File.WriteAllText(Path.Combine(dir, "a.png"), "");
                File.WriteAllText(Path.Combine(dir, "a-2.png"), "");
                Assert.AreEqual(Path.Combine(dir, "a-3.png"), ExportNamer.UniquePath(dir, "a.png"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
```

`StudioRendererTests.cs`:
```csharp
using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioRendererTests
    {
        [Test]
        public void ExportPng_ScaleAndTrim()
        {
            var data = StudioTestRepo.Data;
            using var source = new DiskSpriteSource(StudioTestRepo.Root);
            using var renderer = new StudioRenderer(data, new ImageResolver(source));
            var c = new StudioRoller(data).RollCharacter(new[] { "human" }, new RandomSettings(), new System.Random(11));

            var full = new Texture2D(2, 2);
            full.LoadImage(renderer.ExportPng(c, 2, trim: false, padding: 0));
            Assert.AreEqual(400, full.width);
            Assert.AreEqual(400, full.height);
            Assert.AreEqual(0, full.GetPixel(0, 0).a, "background is transparent");

            var trimmed = new Texture2D(2, 2);
            trimmed.LoadImage(renderer.ExportPng(c, 2, trim: true, padding: 8));
            Assert.Less(trimmed.width, 400);
            Assert.AreEqual(0, trimmed.GetPixel(0, 0).a, "padding is transparent");
            Assert.Greater(trimmed.GetPixel(trimmed.width / 2, trimmed.height / 2).a, 0, "the character is in the middle");

            Object.DestroyImmediate(full);
            Object.DestroyImmediate(trimmed);
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/ExportNamer.cs`:
```csharp
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Starquill.Studio
{
    /// Export file names: <name>_<NN>_<yyyy-MM-dd>.png, never overwriting.
    public static class ExportNamer
    {
        public static string Slug(string s) =>
            Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        public static string FileName(string name, int index, DateTime date) =>
            $"{Slug(name)}_{index:D2}_{date:yyyy-MM-dd}.png";

        public static string UniquePath(string folder, string fileName)
        {
            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path)) return path;
            string stem = Path.GetFileNameWithoutExtension(fileName), ext = Path.GetExtension(fileName);
            for (int n = 2; ; n++)
            {
                path = Path.Combine(folder, $"{stem}-{n}{ext}");
                if (!File.Exists(path)) return path;
            }
        }
    }
}
```

`Assets/Scripts/Studio/StudioRenderer.cs`:
```csharp
using System;
using System.Collections.Generic;
using Starquill.Display;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Starquill.Studio
{
    /// Draws studio characters with the game's CharacterDisplay and
    /// DisplayBuilder. Previews get their own display; exports share one
    /// display per size.
    public sealed class StudioRenderer : IDisposable
    {
        public const int CanvasPixels = 200; // the art canvas at 1x

        private readonly StudioData data;
        private readonly ImageResolver resolver;
        private readonly DisplayBuilder builder;
        private readonly Transform parent;
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly Dictionary<int, CharacterDisplay> exportDisplays = new Dictionary<int, CharacterDisplay>();

        public StudioRenderer(StudioData data, ImageResolver resolver, Transform parent = null)
        {
            this.data = data;
            this.resolver = resolver;
            this.parent = parent;
            builder = new DisplayBuilder(data.Registry);
        }

        public ImageResolver Resolver => resolver;

        public CharacterDisplay CreatePreview(int size = 400)
        {
            var go = new GameObject("StudioPreview");
            if (parent != null) go.transform.SetParent(parent, false);
            owned.Add(go);
            var display = go.AddComponent<CharacterDisplay>();
            display.Initialize(resolver, size);
            return display;
        }

        public void Draw(CharacterDisplay display, StudioCharacter c)
        {
            var species = data.Registry.Species[c.SpeciesName];
            display.RebuildFromData(c.Body, c.SpeciesView(species), c.ToDisplayList(), builder);
        }

        /// Premultiplied render, as the game draws it.
        public RenderTexture Render(StudioCharacter c, int size)
        {
            if (!exportDisplays.TryGetValue(size, out var display))
                exportDisplays[size] = display = CreatePreview(size);
            Draw(display, c);
            return display.Texture;
        }

        public static Color32[] ReadPixels(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            var px = tex.GetPixels32();
            DiskSpriteSource.Kill(tex);
            return px;
        }

        /// Transparent PNG at scale x the 200 px canvas; trim cuts to the
        /// character and adds padding (in 1x pixels, multiplied by scale).
        public byte[] ExportPng(StudioCharacter c, int scale, bool trim, int padding)
        {
            int size = CanvasPixels * scale;
            var px = ReadPixels(Render(c, size));
            PixelOps.Unpremultiply(px);
            int w = size, h = size;
            if (trim)
            {
                var bounds = PixelOps.AlphaBounds(px, w, h);
                if (bounds.HasValue)
                {
                    px = PixelOps.Crop(px, w, bounds.Value);
                    w = bounds.Value.width;
                    h = bounds.Value.height;
                    px = PixelOps.Pad(px, w, h, padding * scale, out w, out h);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            var png = tex.EncodeToPNG();
            DiskSpriteSource.Kill(tex);
            return png;
        }

        public void Dispose()
        {
            foreach (var go in owned) DiskSpriteSource.Kill(go);
            owned.Clear();
            exportDisplays.Clear();
        }
    }
}
```

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioRenderer.cs* Assets/Scripts/Studio/ExportNamer.cs* Assets/Tests/EditMode/Studio/ExportNamerTests.cs* Assets/Tests/EditMode/Studio/StudioRendererTests.cs*
git commit -m "feat(studio): render with the game's CharacterDisplay; transparent PNG export with scale and trim"
```

---

## Task 9: Pixel match against the game's render

**Files:**
- Create: `Assets/Tests/EditMode/Studio/StudioPixelMatchTests.cs`

The design's §4 check: a character drawn by the game's path (Resources sprites) equals the studio's render (disk sprites) at the same size.

**Step 1: Write the test**

```csharp
using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioPixelMatchTests
    {
        [Test]
        public void StudioRender_MatchesTheGameRender()
        {
            var data = StudioTestRepo.Data;
            var s = new RandomSettings { FacialHair = 1f, FacialDetail = 1f };
            var c = new StudioRoller(data).RollCharacter(new[] { "human" }, s, new System.Random(42));
            var species = data.Registry.Species["human"];

            var gameGo = new GameObject("GameRender");
            var game = gameGo.AddComponent<CharacterDisplay>();
            game.Initialize(new ImageResolver(), 400);
            game.RebuildFromData(c.Body, species, c.ToDisplayList(), new DisplayBuilder(data.Registry));
            var gamePx = StudioRenderer.ReadPixels(game.Texture);

            using var source = new DiskSpriteSource(StudioTestRepo.Root);
            using var studio = new StudioRenderer(data, new ImageResolver(source));
            var studioPx = StudioRenderer.ReadPixels(studio.Render(c, 400));

            int opaque = 0, differ = 0, maxDiff = 0;
            for (int i = 0; i < gamePx.Length; i++)
            {
                if (gamePx[i].a > 0) opaque++;
                int d = Mathf.Max(Mathf.Abs(gamePx[i].r - studioPx[i].r), Mathf.Abs(gamePx[i].g - studioPx[i].g),
                                  Mathf.Abs(gamePx[i].b - studioPx[i].b), Mathf.Abs(gamePx[i].a - studioPx[i].a));
                if (d > 2) differ++;
                if (d > maxDiff) maxDiff = d;
            }
            TestContext.WriteLine($"opaque {opaque}, differ(>2) {differ} ({100f * differ / gamePx.Length:0.00}%), max diff {maxDiff}");
            Object.DestroyImmediate(gameGo);

            Assert.Greater(opaque, 1000, "the game render drew a character");
            Assert.LessOrEqual(differ, gamePx.Length / 100, "more than 1% of pixels differ");
        }
    }
}
```

**Step 2: Compile** (none; it runs at the checkpoint).

If Zach's run fails this test, stop and report the printed numbers as a decision block (likely cause: edge texels where the importer's color bleed differs from `PixelOps.BleedTransparentEdges`). Do not loosen the threshold without Zach.

**Step 3: Commit**

```bash
git add Assets/Tests/EditMode/Studio/StudioPixelMatchTests.cs*
git commit -m "test(studio): studio render matches the game's render of the same character"
```

---

## Task 10: Studio settings file

**Files:**
- Create: `Assets/Scripts/Studio/StudioSettings.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioSettingsTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioSettingsTests
    {
        [Test]
        public void SaveLoad_RoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), "studio_" + Path.GetRandomFileName(), "studio-settings.json");
            try
            {
                new StudioSettings { repoRoot = "/repo", exportScale = 4, trim = false, count = 32 }.Save(path);
                var s = StudioSettings.Load(path);
                Assert.AreEqual("/repo", s.repoRoot);
                Assert.AreEqual(4, s.exportScale);
                Assert.IsFalse(s.trim);
                Assert.AreEqual(32, s.count);
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void Load_MissingOrBrokenFile_GivesDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "broken_" + Path.GetRandomFileName() + ".json");
            Assert.AreEqual(2, StudioSettings.Load(path).exportScale);
            File.WriteAllText(path, "{ not json");
            try { Assert.AreEqual(8, StudioSettings.Load(path).count); }
            finally { File.Delete(path); }
        }

        [TestCase(0, 1)] [TestCase(3, 2)] [TestCase(5, 4)] [TestCase(8, 8)] [TestCase(99, 8)]
        public void SnapScale_ToAllowedValues(int input, int expected)
        {
            Assert.AreEqual(expected, StudioSettings.SnapScale(input));
        }
    }
}
```
(`SnapScale` rounds down to the nearest of 1, 2, 4, 8; below 1 gives 1.)

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/StudioSettings.cs`:
```csharp
using System;
using System.IO;
using UnityEngine;

namespace Starquill.Studio
{
    /// Per-install studio choices. In the Editor: Library/CharacterStudio/;
    /// in a build: next to the exe (where presets/ also goes).
    [Serializable]
    public class StudioSettings
    {
        public static readonly int[] Scales = { 1, 2, 4, 8 };
        public static readonly int[] Counts = { 8, 16, 32, 64 };

        public string repoRoot = "";
        public string exportFolder = "";
        public int exportScale = 2;
        public bool trim = true;
        public int padding = 8;
        public int count = 8;

        public static string AppFolder => Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "CharacterStudio")
            : Directory.GetParent(Application.dataPath).FullName;

        public static string DefaultPath => Path.Combine(AppFolder, "studio-settings.json");

        public string ResolvedExportFolder =>
            string.IsNullOrWhiteSpace(exportFolder) ? Path.Combine(AppFolder, "exports") : exportFolder;

        public static int SnapScale(int scale)
        {
            int result = 1;
            foreach (int s in Scales) if (scale >= s) result = s;
            return result;
        }

        public static StudioSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                    return JsonUtility.FromJson<StudioSettings>(File.ReadAllText(path)) ?? new StudioSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Studio settings unreadable, using defaults: {e.Message}");
            }
            return new StudioSettings();
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}
```

**Step 4: Compile** (none).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioSettings.cs* Assets/Tests/EditMode/Studio/StudioSettingsTests.cs*
git commit -m "feat(studio): settings file (repo folder, export folder, scale, trim, count)"
```

---

## Task 11: UI helpers

**Files:**
- Create: `Assets/Scripts/Studio/UI/StudioUi.cs`

No tests: thin wrappers over Unity's control factories, checked by eye at the checkpoint.

**Step 1: Implement**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Code-built uGUI for the studio: flat colors, desktop sizes
    /// (1920x1080 reference). Wraps DefaultControls / TMP_DefaultControls.
    public static class StudioUi
    {
        public static readonly Color Background = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color PanelColor = new Color(0.19f, 0.19f, 0.22f);
        public static readonly Color ControlColor = new Color(0.30f, 0.30f, 0.35f);
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.95f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.94f);
        public static readonly Color ErrorColor = new Color(0.95f, 0.40f, 0.40f);

        private static readonly TMP_DefaultControls.Resources TmpRes = new TMP_DefaultControls.Resources();
        private static readonly DefaultControls.Resources UiRes = new DefaultControls.Resources();

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float height, float spacing = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var row = go.GetComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            Size(go, -1, height);
            return row;
        }

        public static VerticalLayoutGroup Column(Transform parent, string name, float spacing = 6f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            return col;
        }

        public static LayoutElement Size(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            return le;
        }

        public static TMP_Text Label(Transform parent, string text, float fontSize = 20f, float width = -1f)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes);
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            Size(go, width, fontSize + 12f);
            return tmp;
        }

        public static Button Button(Transform parent, string label, Action onClick, float width = 150f)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 20f;
            text.color = TextColor;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Size(go, width, 40f);
            return button;
        }

        public static TMP_Dropdown Dropdown(Transform parent, IList<string> options, int value, Action<int> onChanged, float width = 160f)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpRes);
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var dd = go.GetComponent<TMP_Dropdown>();
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Count - 1));
            dd.captionText.color = TextColor;
            dd.captionText.fontSize = 20f;
            dd.itemText.fontSize = 20f;
            dd.onValueChanged.AddListener(i => onChanged(i));
            Size(go, width, 40f);
            return dd;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged, float width = 120f)
        {
            var go = DefaultControls.CreateToggle(UiRes);
            go.transform.SetParent(parent, false);
            var toggle = go.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(value);
            ((Image)toggle.targetGraphic).color = ControlColor;
            toggle.graphic.color = Accent;
            var text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = TextColor;
            text.fontSize = 18;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            Size(go, width, 30f);
            return toggle;
        }

        public static TMP_InputField Input(Transform parent, string value, Action<string> onEndEdit, float width = 700f)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpRes);
            go.transform.SetParent(parent, false);
            var input = go.GetComponent<TMP_InputField>();
            input.text = value ?? "";
            input.textComponent.fontSize = 20f;
            input.onEndEdit.AddListener(v => onEndEdit(v));
            Size(go, width, 40f);
            return input;
        }

        /// Vertical scroll area whose content is a grid of fixed-size cells.
        public static RectTransform ScrollGrid(Transform parent, Vector2 cell, float spacing)
        {
            var go = DefaultControls.CreateScrollView(UiRes);
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(12, 12, 12, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// Vertical scroll list (Warnings).
        public static RectTransform ScrollList(Transform parent)
        {
            var go = DefaultControls.CreateScrollView(UiRes);
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var col = content.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(10, 10, 10, 10);
            col.spacing = 4f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static Texture2D checker;

        /// 2x2 light/dark gray tile; use with RawImage.uvRect to repeat it.
        public static Texture2D Checkerboard()
        {
            if (checker != null) return checker;
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var a = new Color32(200, 200, 200, 255);
            var b = new Color32(150, 150, 150, 255);
            checker.SetPixels32(new[] { a, b, b, a });
            checker.Apply();
            return checker;
        }
    }
}
```

**Step 2: Compile** (none; probe `Starquill.Studio.UI.StudioUi`).

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI.meta Assets/Scripts/Studio/UI/StudioUi.cs*
git commit -m "feat(studio): code-built uGUI helpers"
```

---

## Task 12: Studio app, repo picker, Bulk screen (minimal), Warnings

**Files:**
- Create: `Assets/Scripts/Studio/StudioApp.cs`
- Create: `Assets/Scripts/Studio/UI/RepoPickerView.cs`
- Create: `Assets/Scripts/Studio/UI/BulkScreen.cs`
- Create: `Assets/Scripts/Studio/UI/WarningsView.cs`

No unit tests: logic lives in Tasks 3-10; these classes wire it to buttons. Checked at CHECKPOINT D.

**Step 1: `StudioApp.cs`**

```csharp
using System.IO;
using Starquill.Display;
using Starquill.Studio.UI;
using UnityEngine;

namespace Starquill.Studio
{
    /// Entry point of the CharacterStudio scene. Builds every screen in code.
    public sealed class StudioApp : MonoBehaviour
    {
        private StudioSettings settings;
        private Canvas canvas;
        private DiskSpriteSource sprites;
        private StudioRenderer renderer;

        public StudioSettings Settings => settings;
        public StudioData Data { get; private set; }
        public StudioRenderer Renderer => renderer;

        private void Start()
        {
            settings = StudioSettings.Load(StudioSettings.DefaultPath);
            if (Application.isEditor && !StudioData.LooksLikeRepo(settings.repoRoot))
                settings.repoRoot = Directory.GetParent(Application.dataPath).FullName;

            StudioUi.EnsureEventSystem();
            canvas = StudioUi.CreateCanvas("StudioCanvas");
            var bg = StudioUi.Panel(canvas.transform, "Background", StudioUi.Background);
            StudioUi.Stretch(bg);

            if (StudioData.LooksLikeRepo(settings.repoRoot)) OpenRepo(settings.repoRoot);
            else ShowRepoPicker();
        }

        public void ShowRepoPicker()
        {
            ClearScreens();
            new RepoPickerView(canvas.transform, settings.repoRoot, OpenRepo);
        }

        private void OpenRepo(string root)
        {
            ClearScreens();
            renderer?.Dispose();
            sprites?.Dispose();

            settings.repoRoot = root;
            SaveSettings();
            Data = StudioData.Load(root);
            sprites = new DiskSpriteSource(root);
            renderer = new StudioRenderer(Data, new ImageResolver(sprites), transform);
            new BulkScreen(canvas.transform, this);
        }

        public void SaveSettings()
        {
            try { settings.Save(StudioSettings.DefaultPath); }
            catch (IOException e) { Debug.LogWarning("Could not save studio settings: " + e.Message); }
        }

        private void ClearScreens()
        {
            for (int i = canvas.transform.childCount - 1; i >= 1; i--) // keep Background
                Destroy(canvas.transform.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            renderer?.Dispose();
            sprites?.Dispose();
        }
    }
}
```

**Step 2: `RepoPickerView.cs`**

```csharp
using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// First launch: where is the Starquill clone? (P2: typed or pasted path.)
    public sealed class RepoPickerView
    {
        public RepoPickerView(Transform parent, string current, Action<string> onChosen)
        {
            var panel = StudioUi.Panel(parent, "RepoPicker", StudioUi.PanelColor);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900, 300);
            var col = StudioUi.Column(panel, "Column", 14f);
            StudioUi.Stretch((RectTransform)col.transform, 30, 30, 30, 30);

            StudioUi.Label(col.transform, "Where is your Starquill folder?", 28f);
            StudioUi.Label(col.transform, "Paste the folder GitHub Desktop cloned (it holds Assets/Data/ArtTables).", 18f);
            TMP_Text error = null;
            var input = StudioUi.Input(col.transform, current, _ => { });
            var row = StudioUi.Row(col.transform, "Buttons", 44f);
            StudioUi.Button(row.transform, "Use this folder", () =>
            {
                string path = input.text.Trim().Trim('"');
                if (StudioData.LooksLikeRepo(path)) onChosen(Path.GetFullPath(path));
                else error.text = "That folder has no Assets/Data/ArtTables and Assets/Resources/Images. Pick the top Starquill folder.";
            }, 220f);
            error = StudioUi.Label(col.transform, "", 18f);
            error.color = StudioUi.ErrorColor;
        }
    }
}
```

**Step 3: `WarningsView.cs`**

```csharp
using System.Linq;
using Starquill.Data.Art;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Overlay listing art table problems and images that failed to load.
    public sealed class WarningsView
    {
        public WarningsView(Transform parent, StudioApp app)
        {
            var panel = StudioUi.Panel(parent, "Warnings", StudioUi.PanelColor);
            StudioUi.Stretch(panel, 200, 100, 200, 100);
            var col = StudioUi.Column(panel, "Column", 10f);
            StudioUi.Stretch((RectTransform)col.transform, 20, 20, 20, 20);

            var header = StudioUi.Row(col.transform, "Header", 44f);
            var issues = app.Data.Issues;
            int errors = issues.Count(i => i.level == ArtIssueLevel.Error);
            StudioUi.Label(header.transform, $"Warnings: {issues.Count - errors}   Errors: {errors}", 24f, 600f);
            StudioUi.Button(header.transform, "Close", () => Object.Destroy(panel.gameObject), 120f);

            var list = StudioUi.ScrollList(col.transform);
            StudioUi.Size(list.parent.parent.gameObject, -1, 780f).flexibleHeight = 1;
            foreach (var issue in issues)
            {
                var label = StudioUi.Label(list, issue.ToString(), 17f);
                if (issue.level == ArtIssueLevel.Error) label.color = StudioUi.ErrorColor;
                label.textWrappingMode = TextWrappingModes.Normal;
            }
            foreach (var path in app.Renderer.Resolver.MissingPaths.OrderBy(p => p))
                StudioUi.Label(list, $"Warning: {path}.png: image not found while drawing", 17f).textWrappingMode = TextWrappingModes.Normal;
            if (issues.Count == 0 && app.Renderer.Resolver.MissingPaths.Count == 0)
                StudioUi.Label(list, "No problems found.", 18f);
        }
    }
}
```

**Step 4: `BulkScreen.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// The grid of random characters (ported from the Godot randomizer).
    /// B1: Count, Randomize All, Equip Random, Lock, reroll, Export.
    public sealed class BulkScreen
    {
        private sealed class Card
        {
            public GameObject Root;
            public CharacterDisplay Display;
            public RawImage Image;
            public TMP_Text Species;
            public Toggle Lock;
        }

        private readonly StudioApp app;
        private readonly StudioRoller roller;
        private readonly RandomSettings random = new RandomSettings();
        private readonly System.Random rng = new System.Random(Environment.TickCount);
        private readonly List<StudioCharacter> characters = new List<StudioCharacter>();
        private readonly List<Card> cards = new List<Card>();
        private readonly RectTransform grid;
        private readonly TMP_Text status;
        private readonly Button warnings;

        public BulkScreen(Transform parent, StudioApp app)
        {
            this.app = app;
            roller = new StudioRoller(app.Data);
            var s = app.Settings;

            var root = StudioUi.Panel(parent, "Bulk", StudioUi.Background);
            StudioUi.Stretch(root);

            var top = StudioUi.Row(root, "TopBar", 56f);
            var topRt = (RectTransform)top.transform;
            topRt.anchorMin = new Vector2(0, 1);
            topRt.anchorMax = new Vector2(1, 1);
            topRt.pivot = new Vector2(0.5f, 1);
            topRt.sizeDelta = new Vector2(-24, 56);
            topRt.anchoredPosition = new Vector2(0, -8);

            StudioUi.Label(top.transform, "Count", 20f, 60f);
            StudioUi.Dropdown(top.transform, StudioSettings.Counts.Select(c => c.ToString()).ToList(),
                Array.IndexOf(StudioSettings.Counts, s.count), i => { s.count = StudioSettings.Counts[i]; app.SaveSettings(); SetCount(s.count); }, 90f);
            StudioUi.Button(top.transform, "Randomize All", RandomizeAll, 170f);
            StudioUi.Button(top.transform, "Equip Random", EquipRandom, 170f);
            StudioUi.Label(top.transform, "Scale", 20f, 56f);
            StudioUi.Dropdown(top.transform, StudioSettings.Scales.Select(x => x + "x").ToList(),
                Array.IndexOf(StudioSettings.Scales, StudioSettings.SnapScale(s.exportScale)),
                i => { s.exportScale = StudioSettings.Scales[i]; app.SaveSettings(); }, 80f);
            StudioUi.Toggle(top.transform, "Trim", s.trim, v => { s.trim = v; app.SaveSettings(); }, 90f);
            StudioUi.Button(top.transform, "Export", Export, 120f);
            StudioUi.Button(top.transform, "Open folder", OpenExportFolder, 150f);
            warnings = StudioUi.Button(top.transform, "Warnings", () => new WarningsView(parent, app), 170f);
            StudioUi.Button(top.transform, "Repo folder", app.ShowRepoPicker, 150f);

            status = StudioUi.Label(root, "", 18f);
            var statusRt = (RectTransform)status.transform;
            statusRt.anchorMin = new Vector2(0, 0);
            statusRt.anchorMax = new Vector2(1, 0);
            statusRt.pivot = new Vector2(0.5f, 0);
            statusRt.sizeDelta = new Vector2(-24, 30);
            statusRt.anchoredPosition = new Vector2(0, 6);

            grid = StudioUi.ScrollGrid(root, new Vector2(220, 300), 12f);
            StudioUi.Stretch((RectTransform)grid.parent.parent, 0, 72, 0, 40); // the ScrollView

            SetCount(s.count);
            UpdateWarningsButton();
        }

        private void SetCount(int count)
        {
            while (characters.Count < count)
            {
                characters.Add(roller.RollCharacter(app.Data.SpeciesNames, random, rng));
                cards.Add(CreateCard(cards.Count));
                Redraw(cards.Count - 1);
            }
            while (characters.Count > count)
            {
                int last = characters.Count - 1;
                UnityEngine.Object.Destroy(cards[last].Root);
                UnityEngine.Object.Destroy(cards[last].Display.gameObject);
                cards.RemoveAt(last);
                characters.RemoveAt(last);
            }
        }

        private Card CreateCard(int index)
        {
            var card = new Card();
            var panel = StudioUi.Panel(grid, $"Card{index + 1}", StudioUi.PanelColor);
            card.Root = panel.gameObject;
            var col = StudioUi.Column(panel, "Column", 4f);
            StudioUi.Stretch((RectTransform)col.transform, 6, 6, 6, 6);

            var checker = new GameObject("Checker", typeof(RectTransform), typeof(RawImage));
            checker.transform.SetParent(col.transform, false);
            var checkerImage = checker.GetComponent<RawImage>();
            checkerImage.texture = StudioUi.Checkerboard();
            checkerImage.uvRect = new Rect(0, 0, 13, 13);
            StudioUi.Size(checker, 208, 208);

            var doll = new GameObject("Character", typeof(RectTransform), typeof(RawImage));
            doll.transform.SetParent(checker.transform, false);
            StudioUi.Stretch((RectTransform)doll.transform);
            card.Image = doll.GetComponent<RawImage>();
            card.Display = app.Renderer.CreatePreview();
            card.Image.texture = card.Display.Texture;

            card.Species = StudioUi.Label(col.transform, "", 18f);
            var row = StudioUi.Row(col.transform, "Buttons", 34f, 6f);
            card.Lock = StudioUi.Toggle(row.transform, "Lock", false, v => characters[index].Locked = v, 90f);
            StudioUi.Button(row.transform, "Reroll", () => Reroll(index), 100f);
            return card;
        }

        private void Redraw(int i)
        {
            app.Renderer.Draw(cards[i].Display, characters[i]);
            cards[i].Species.text = characters[i].SpeciesName;
            cards[i].Lock.SetIsOnWithoutNotify(characters[i].Locked);
            UpdateWarningsButton();
        }

        private void RandomizeAll()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                characters[i] = roller.RollCharacter(app.Data.SpeciesNames, random, rng);
                Redraw(i);
            }
        }

        private void EquipRandom()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                roller.RollGear(characters[i], random, rng);
                Redraw(i);
            }
        }

        private void Reroll(int i)
        {
            bool locked = characters[i].Locked;
            characters[i] = roller.RollCharacter(app.Data.SpeciesNames, random, rng);
            characters[i].Locked = locked;
            Redraw(i);
        }

        private void Export()
        {
            var s = app.Settings;
            string folder = s.ResolvedExportFolder;
            try
            {
                Directory.CreateDirectory(folder);
                var today = DateTime.Now;
                for (int i = 0; i < characters.Count; i++)
                {
                    var png = app.Renderer.ExportPng(characters[i], StudioSettings.SnapScale(s.exportScale), s.trim, s.padding);
                    File.WriteAllBytes(ExportNamer.UniquePath(folder, ExportNamer.FileName(characters[i].SpeciesName, i + 1, today)), png);
                }
                status.color = StudioUi.TextColor;
                status.text = $"Exported {characters.Count} PNGs to {folder}";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                status.color = StudioUi.ErrorColor;
                status.text = $"Could not write to {folder}: {e.Message}. Nothing was lost; pick another folder.";
            }
            UpdateWarningsButton();
        }

        private void OpenExportFolder()
        {
            string folder = app.Settings.ResolvedExportFolder;
            Directory.CreateDirectory(folder);
            Application.OpenURL(new Uri(folder).AbsoluteUri);
        }

        private void UpdateWarningsButton()
        {
            int count = app.Data.Issues.Count + app.Renderer.Resolver.MissingPaths.Count;
            warnings.GetComponentInChildren<TMP_Text>().text = $"Warnings ({count})";
        }
    }
}
```
B1 has no export folder field: exports go to `ResolvedExportFolder` (`Library/CharacterStudio/exports` in the Editor, `exports/` next to the exe). B2 adds the folder field.

**Step 5: Compile**

Refresh + `check_compile_errors`: none. Probe `Starquill.Studio.StudioApp`.

**Step 6: Commit**

```bash
git add Assets/Scripts/Studio/StudioApp.cs* Assets/Scripts/Studio/UI/RepoPickerView.cs* Assets/Scripts/Studio/UI/BulkScreen.cs* Assets/Scripts/Studio/UI/WarningsView.cs*
git commit -m "feat(studio): app shell with repo picker, minimal Bulk screen and Warnings list"
```

---

## Task 13: Scene, build menu, Linux and Windows builds

**Files:**
- Create: `Assets/Editor/CharacterStudioTools.cs`
- Create (by the menu): `Assets/Scenes/CharacterStudio.unity`
- Modify: `.gitignore`

**Step 1: Editor tools**

```csharp
using Starquill.Studio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// Tools > Character Studio: create the studio scene and build the app.
/// The scene is never added to the game's build scene list.
public static class CharacterStudioTools
{
    public const string ScenePath = "Assets/Scenes/CharacterStudio.unity";
    private const string LinuxPath = "Builds/CharacterStudio/Linux/StarquillCharacters.x86_64";
    private const string WindowsPath = "Builds/CharacterStudio/Windows/StarquillCharacters.exe";

    [MenuItem("Tools/Character Studio/Create Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.13f, 0.13f, 0.15f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        new GameObject("StudioApp", typeof(StudioApp));

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Character Studio scene saved: " + ScenePath);
    }

    [MenuItem("Tools/Character Studio/Build Linux")]
    public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, LinuxPath);

    [MenuItem("Tools/Character Studio/Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

    public static BuildReport Build(BuildTarget target, string path)
    {
        var named = NamedBuildTarget.Standalone;
        string productName = PlayerSettings.productName;
        var backend = PlayerSettings.GetScriptingBackend(named);
        var mode = PlayerSettings.fullScreenMode;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        bool resizable = PlayerSettings.resizableWindow;
        try
        {
            PlayerSettings.productName = "Starquill Characters";
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.CompressWithLz4HC
            });
            var sum = report.summary;
            Debug.Log($"Character Studio build {sum.result}: {path} ({sum.totalSize / (1024 * 1024)} MB, {sum.totalTime.TotalSeconds:0} s)");
            return report;
        }
        finally
        {
            PlayerSettings.productName = productName;
            PlayerSettings.SetScriptingBackend(named, backend);
            PlayerSettings.fullScreenMode = mode;
            PlayerSettings.defaultScreenWidth = width;
            PlayerSettings.defaultScreenHeight = height;
            PlayerSettings.resizableWindow = resizable;
        }
    }
}
```
Append to `.gitignore` under `# Builds`: `/Builds/`.

**Step 2: Create the scene**

Run Tools > Character Studio > Create Scene (Coplay `execute_script` calling `CharacterStudioTools.CreateScene()` is fine). Check `ProjectSettings/EditorBuildSettings.asset` still lists only `ExploreScene`.

**Step 3: Smoke-test in the Editor (executor)**

Open `CharacterStudio.unity`, Play (Coplay `play_game`), `capture_ui_canvas` or a Game view screenshot: 8 cards with characters, top bar visible. `get_unity_logs`: no exceptions. Stop. Fix compile/runtime errors before the checkpoint; layout polish waits for Zach's feedback.

**Step 4: Builds**

Run `CharacterStudioTools.BuildLinux()` then `BuildWindows()` via Coplay (each takes about a minute; the Phase A test build took 49 s). Read the result line from `~/.config/unity3d/Editor.log` (`Character Studio build Succeeded: ... MB`). Record both sizes. Confirm `git status` shows no `ProjectSettings` change from the build (the settings are restored), and no `Builds/` files.

**Step 5: Commit**

```bash
git add Assets/Editor/CharacterStudioTools.cs* Assets/Scenes/CharacterStudio.unity* .gitignore
git commit -m "feat(studio): CharacterStudio scene and Linux/Windows build menu"
```

**CHECKPOINT D (Zach):**
- Test Runner > EditMode > Run All: all green except the 2 known old failures. Read `StudioPixelMatchTests` output (opaque / differ / max diff) and report it.
- Open `Assets/Scenes/CharacterStudio.unity`, Play: Count 8 → 64, Randomize All, Equip Random, Lock a card and check it survives both, Reroll, Export at 2× with Trim on, Open folder: PNGs have a transparent background and no dark fringe.
- Run `Builds/CharacterStudio/Linux/StarquillCharacters.x86_64`: the repo picker appears (no settings next to the exe yet); paste the repo path; the grid fills; Export writes next to the exe.
- Note the two build sizes (P6). If the Windows zip is too big to hand the artist, decide before B6.
- Then: detailed tasks for B2.

---

## Stage B2: Bulk screen complete (Tasks 14-23, CHECKPOINT E)

Written 2026-11-08 after CHECKPOINT D, against B1's committed code (`e2615d67`..`74524558`). Design of record: `docs/plans/2026-11-08-character-studio-design.md` §2 Bulk screen.

### Decisions made while planning B2 (2026-11-08)

| # | Decision | Why |
|---|---|---|
| P8 | Forced weapons are allowed: one main hand, one off hand, shields (`w08`/`w09`) only in the off hand. A combination that cannot fit (two-handed plus anything in the off hand, a third weapon, a second shield) is refused with a message saying why. | The approved B2 outline. Godot blocked weapons from search altogether (`character_randomizer_control.gd:530-536`), which leaves the artist no way to show one sword across a cast. |
| P9 | Forced items go on **unlocked** cards only: when added, after every roll, after a card's species change. Removing a chip takes the item off unlocked cards and leaves that slot empty until the next roll; other gear stays. | Godot rerolled every card's whole outfit on each add/remove (`:636-640`); keeping the rest of the outfit is less surprising. Lock means "leave this card alone" (design §2). |
| P10 | When a forced off-hand item meets a randomly rolled two-handed main hand, the main hand is emptied; a forced two-handed main hand empties a random off hand. | A forced item always wins over a random one. |
| P11 | Slot limits follow Godot (`:556-575`): the first forced item of a slot type takes its slot, later ones go to Misc 1-4; the four Misc slots are shared by every overflow and every forced `mc` item. So at most 5 per slot type and 4 `mc`, as in Godot, and never more than fits. | Jakob's Law: the artist's existing tool. |
| P12 | Search matches an item token (`hd01-0001`, partial too), a full file name or pasted path (`hd01-0001-130.png`, `w01-164-0001.png`), or a word in the item description (`hat`). It lists at most 15 results and says "Showing 15 of N". | Design §2 Search; Godot capped results at 20 in a smaller list. |
| P13 | Species selection is stored in `studio-settings.json` as **excluded** species, so a species added later (B5) starts checked. Random settings (chances, weights) stay in memory until B4 adds Random settings presets. | Exclusions need no migration when species are added; B4 owns preset files. |
| P14 | The card **Edit** button arrives with the Single screen (B3). | Nothing to open yet. |
| P15 | Export moves to a bottom bar with an **export folder** field (typed or pasted, P2) and a **Select** checkbox per card: Export writes the selected cards, or all when none is selected; the button says which ("Export selected (3)" / "Export all (8)"). | Design §2 Export "All or checked characters into a chosen folder"; Nielsen: visibility of system status. |
| P16 | The CHECKPOINT D layout flags are fixed here: labels never wrap (auto width), toggle labels overflow instead of wrapping, and the dropdown arrow and check marks use small sprites drawn in code. | Seen in the CHECKPOINT D capture ("Cou/nt", "Sca/le", "Tri", white squares). |

### Facts the executor needs (B2)

- B1 code is as written in Tasks 1-13 except `StudioApp`'s renderer field is `studioRenderer` (public property `Renderer` unchanged).
- Species `item_restrictions` are in `species.csv` column 23; stone golem cannot wear `hd01`-`hd13` and most `tr` types.
- Armor types with second-color layers: `ar05 hd02 hd04 hd06 hd08`. Weapons `w01`-`w07` are modular; `w08`/`w09` (shields) are not; two-handed: `w04 w05 w07`.
- Non-modular weapons store the same number list on every layer, so "numbers usable on every layer" (the intersection) works for both kinds.
- `ColorManager.ResolveColorField` never returns an empty array (falls back to a default palette).
- uGUI `DefaultControls.CreateSlider` builds `Background`, `Fill Area/Fill`, `Handle Slide Area/Handle`.
- A nested `Canvas` with `overrideSorting = true` draws its children above later siblings of the parent canvas; the search results list needs it because the card grid is created after the top bar.
- Coplay `execute_script` gives up after 60 s, but a build keeps running in the Editor; read the result line from `~/.config/unity3d/Editor.log`.
- The CharacterStudio scene is opened with `EditorSceneManager.OpenScene`; if ExploreScene shows as unsaved after scripts, compare a `SaveScene(..., saveAsCopy: true)` copy with the file before discarding (B1 found it byte-identical twice).

---

## Task 14: UI helpers, B2 additions and layout fixes

**Files:**
- Modify (replace whole file): `Assets/Scripts/Studio/UI/StudioUi.cs`

No tests (thin wrappers, checked by eye at CHECKPOINT E). Existing callers (`RepoPickerView`, `WarningsView`, `BulkScreen`) keep compiling: every B1 signature is unchanged; `Input` gains an optional `placeholder`.

**Step 1: Replace `StudioUi.cs`**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Code-built uGUI for the studio: flat colors, desktop sizes
    /// (1920x1080 reference). Wraps DefaultControls / TMP_DefaultControls;
    /// the dropdown arrow and check mark are drawn in code (no sprite assets).
    public static class StudioUi
    {
        public static readonly Color Background = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color PanelColor = new Color(0.19f, 0.19f, 0.22f);
        public static readonly Color ControlColor = new Color(0.30f, 0.30f, 0.35f);
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.95f);
        public static readonly Color ChipColor = new Color(0.25f, 0.43f, 0.66f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.94f);
        public static readonly Color ErrorColor = new Color(0.95f, 0.40f, 0.40f);

        private static Sprite arrow, dot;
        private static Texture2D checker;

        private static TMP_DefaultControls.Resources TmpRes() =>
            new TMP_DefaultControls.Resources { dropdown = ArrowSprite(), checkmark = DotSprite() };

        private static DefaultControls.Resources UiRes() =>
            new DefaultControls.Resources { dropdown = ArrowSprite(), checkmark = DotSprite() };

        /// Down-pointing triangle for dropdowns.
        public static Sprite ArrowSprite()
        {
            if (arrow != null) return arrow;
            return arrow = MakeSprite(32, (x, y) =>
            {
                if (y < 9 || y > 22) return false;
                float halfWidth = (y - 9) * 12f / 13f;
                return Mathf.Abs(x + 0.5f - 16f) <= halfWidth;
            });
        }

        /// Filled circle for check marks.
        public static Sprite DotSprite()
        {
            if (dot != null) return dot;
            return dot = MakeSprite(32, (x, y) => (new Vector2(x + 0.5f, y + 0.5f) - new Vector2(16f, 16f)).sqrMagnitude <= 100f);
        }

        private static Sprite MakeSprite(int size, Func<int, int, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(inside(x, y) ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float height, float spacing = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var row = go.GetComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            Size(go, -1, height);
            return row;
        }

        public static VerticalLayoutGroup Column(Transform parent, string name, float spacing = 6f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            return col;
        }

        /// Takes the free width in a Row, pushing later items to the right.
        public static void Spacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        public static LayoutElement Size(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            return le;
        }

        /// One line, never wrapped. width -1: as wide as the text.
        public static TMP_Text Label(Transform parent, string text, float fontSize = 20f, float width = -1f)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes());
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            Size(go, width, fontSize + 12f);
            return tmp;
        }

        public static Button Button(Transform parent, string label, Action onClick, float width = 150f)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes());
            go.name = label;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 20f;
            text.color = TextColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Size(go, width, 40f);
            return button;
        }

        public static TMP_Dropdown Dropdown(Transform parent, IList<string> options, int value, Action<int> onChanged, float width = 160f)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var dd = go.GetComponent<TMP_Dropdown>();
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Count - 1));
            dd.captionText.color = TextColor;
            dd.captionText.fontSize = 20f;
            dd.itemText.fontSize = 20f;
            dd.onValueChanged.AddListener(i => onChanged(i));
            Size(go, width, 40f);
            return dd;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged, float width = 120f)
        {
            var go = DefaultControls.CreateToggle(UiRes());
            go.transform.SetParent(parent, false);
            var toggle = go.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(value);
            ((Image)toggle.targetGraphic).color = ControlColor;
            toggle.graphic.color = Accent;
            var text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = TextColor;
            text.fontSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            Size(go, width, 30f);
            return toggle;
        }

        public static TMP_InputField Input(Transform parent, string value, Action<string> onEndEdit, float width = 700f, string placeholder = null)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpRes());
            go.transform.SetParent(parent, false);
            var input = go.GetComponent<TMP_InputField>();
            input.text = value ?? "";
            input.textComponent.fontSize = 20f;
            if (placeholder != null && input.placeholder is TMP_Text hint)
            {
                hint.text = placeholder;
                hint.fontSize = 18f;
                hint.textWrappingMode = TextWrappingModes.NoWrap;
            }
            input.onEndEdit.AddListener(v => onEndEdit(v));
            Size(go, width, 40f);
            return input;
        }

        /// Row: name, slider, value. The value text follows the slider.
        public static Slider Slider(Transform parent, string label, float min, float max, float value, Action<float> onChanged, string format = "0.00")
        {
            var row = Row(parent, "Slider " + label, 30f, 10f);
            Label(row.transform, label, 18f, 330f);
            var go = DefaultControls.CreateSlider(UiRes());
            go.transform.SetParent(row.transform, false);
            go.transform.Find("Background").GetComponent<Image>().color = ControlColor;
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = TextColor;
            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            Size(go, 300f, 20f);
            var valueText = Label(row.transform, value.ToString(format), 18f, 60f);
            slider.onValueChanged.AddListener(v => { valueText.text = v.ToString(format); onChanged(v); });
            return slider;
        }

        /// Clickable color square.
        public static Image Swatch(Transform parent, Color color, float size, Action onClick)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            Size(go, size, size);
            return image;
        }

        /// Modal window over the whole canvas: a dim backdrop (a click on it
        /// closes), a centered panel with a title and Close, and a scrolling
        /// list. Returns the list; close() removes the window.
        public static RectTransform Popup(Transform canvasRoot, string title, Vector2 size, out Action close)
        {
            var root = new GameObject("Popup: " + title, typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)root.transform);
            Action doClose = () => { if (root != null) UnityEngine.Object.Destroy(root); };
            close = doClose;

            // The backdrop and the window are siblings, so clicks inside the
            // window never reach the backdrop's Button.
            var backdrop = Panel(root.transform, "Backdrop", new Color(0f, 0f, 0f, 0.55f));
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => doClose());

            var window = Panel(root.transform, "Window", PanelColor);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = size;
            var col = Column(window, "Column", 10f);
            Stretch((RectTransform)col.transform, 20, 20, 20, 20);

            var header = Row(col.transform, "Header", 44f);
            Label(header.transform, title, 26f);
            Spacer(header.transform);
            Button(header.transform, "Close", () => doClose(), 120f);

            var list = ScrollList(col.transform);
            Size(list.parent.parent.gameObject, -1, size.y - 104f).flexibleHeight = 1;
            return list;
        }

        /// Vertical scroll area whose content is a grid of fixed-size cells.
        public static RectTransform ScrollGrid(Transform parent, Vector2 cell, float spacing)
        {
            var go = DefaultControls.CreateScrollView(UiRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(12, 12, 12, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// Vertical scroll list (Warnings, popups).
        public static RectTransform ScrollList(Transform parent)
        {
            var go = DefaultControls.CreateScrollView(UiRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var col = content.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(10, 10, 10, 10);
            col.spacing = 4f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// 2x2 light/dark gray tile; use with RawImage.uvRect to repeat it.
        public static Texture2D Checkerboard()
        {
            if (checker != null) return checker;
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var a = new Color32(200, 200, 200, 255);
            var b = new Color32(150, 150, 150, 255);
            checker.SetPixels32(new[] { a, b, b, a });
            checker.Apply();
            return checker;
        }
    }
}
```

**Step 2: Compile** (Refresh + `check_compile_errors`: none). Probe `StudioUi` has `Popup`, `Slider`, `Swatch`, `Spacer`, `ArrowSprite`, `DotSprite`. Check the Console for new warnings in `Assets/Scripts/Studio` (B1 found CS0108 only that way).

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI/StudioUi.cs
git commit -m "feat(studio): popup, slider and swatch helpers; no-wrap labels; drawn dropdown arrow and check mark"
```

---

## Task 15: Item search

**Files:**
- Create: `Assets/Scripts/Studio/ForcedItem.cs`
- Create: `Assets/Scripts/Studio/ItemSearch.cs`
- Create: `Assets/Tests/EditMode/Studio/ItemSearchTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ItemSearchTests
    {
        private static StudioData D => StudioTestRepo.Data;

        [Test]
        public void Token_FindsThatItemFirst()
        {
            int n = ItemSearch.NumbersFor(D, "hd01")[0];
            string token = $"hd01-{n:D4}";

            var r = ItemSearch.Find(D, token);

            Assert.AreEqual(token, r.Items[0].Token);
        }

        [Test]
        public void EquipmentFileName_OrPastedPath_FindsExactlyThatItem()
        {
            int n = ItemSearch.NumbersFor(D, "hd01")[0];
            int layer = D.Catalog.GetEntry("hd01").LayerCodes[0];
            string file = Path.GetFileName(ImageToken.BuildEquipmentSpritePath("hd01", n, layer)) + ".png";

            foreach (var q in new[] { file, file.ToUpperInvariant(),
                         @"C:\Users\artist\Starquill\Assets\Resources\Images\equipment\hd01\" + file })
            {
                var r = ItemSearch.Find(D, q);
                Assert.AreEqual(1, r.Total, q);
                Assert.AreEqual(new ForcedItem("hd01", n), r.Items[0], q);
            }
        }

        [Test]
        public void WeaponFileName_FindsTheWeapon()
        {
            var w = D.Catalog.GetWeapon("w01");
            int n = ItemSearch.NumbersFor(D, "w01")[0];
            string file = Path.GetFileName(ImageToken.BuildWeaponSpritePath("w01", w.LayerCodes[0], n)) + ".png";

            var r = ItemSearch.Find(D, file);

            Assert.AreEqual(1, r.Total, file);
            Assert.AreEqual(new ForcedItem("w01", n), r.Items[0]);
        }

        [Test]
        public void DescriptionWord_FindsEveryVariantOfThatType()
        {
            var r = ItemSearch.Find(D, "gloves", 1000);

            Assert.AreEqual(ItemSearch.NumbersFor(D, "ar01").Length, r.Total);
            Assert.IsTrue(r.Items.All(i => i.ItemType == "ar01"));
        }

        [Test]
        public void ResultsStopAtMax_TotalCountsAll()
        {
            var r = ItemSearch.Find(D, "hd", 10);

            Assert.AreEqual(10, r.Items.Count);
            Assert.Greater(r.Total, 10);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("zzzz-nothing")]
        [TestCase("hd01-9999-130.png")]
        public void NoMatch_IsEmpty(string q)
        {
            var r = ItemSearch.Find(D, q);

            Assert.AreEqual(0, r.Total);
            Assert.AreEqual(0, r.Items.Count);
        }

        [Test]
        public void NumbersFor_ModularWeapon_EveryLayerHasTheImage()
        {
            var w = D.Catalog.WeaponEntries.First(e => e.Modular);
            var nums = ItemSearch.NumbersFor(D, w.ItemType);

            Assert.IsNotEmpty(nums);
            foreach (int n in nums)
                foreach (int layer in w.LayerCodes)
                    Assert.IsTrue(File.Exists(DiskSpriteSource.FilePathFor(StudioTestRepo.Root,
                        ImageToken.BuildWeaponSpritePath(w.ItemType, layer, n))), $"{w.ItemType} layer {layer} #{n}");
        }

        [Test]
        public void NumbersFor_UnknownType_IsEmpty()
        {
            Assert.IsEmpty(ItemSearch.NumbersFor(D, "zz99"));
        }
    }
}
```

**Step 2: Compile-check to see it fail** (`ItemSearch`, `ForcedItem` not found).

**Step 3: Implement**

`Assets/Scripts/Studio/ForcedItem.cs`:
```csharp
using System;

namespace Starquill.Studio
{
    /// One item variant picked in search, e.g. hd01-0001.
    public sealed class ForcedItem : IEquatable<ForcedItem>
    {
        public readonly string ItemType;
        public readonly int ItemNum;

        public ForcedItem(string itemType, int itemNum)
        {
            ItemType = itemType;
            ItemNum = itemNum;
        }

        public string Token => $"{ItemType}-{ItemNum:D4}";
        public bool IsWeapon => ItemType.StartsWith("w", StringComparison.Ordinal);
        /// Slot code: hd, tr, ar, lg, fe, mc (weapons: w0).
        public string Prefix => ItemType.Substring(0, 2);

        /// True when the slot holds this exact variant.
        public bool Matches(StudioItem item) => item != null && item.ItemType == ItemType && item.ItemNum == ItemNum;

        public bool Equals(ForcedItem other) => other != null && other.ItemType == ItemType && other.ItemNum == ItemNum;
        public override bool Equals(object obj) => Equals(obj as ForcedItem);
        public override int GetHashCode() => (ItemType, ItemNum).GetHashCode();
        public override string ToString() => Token;
    }
}
```

`Assets/Scripts/Studio/ItemSearch.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Starquill.Studio
{
    /// The Bulk screen's search box: an item token (hd01-0001, partial too),
    /// a file name or pasted path, or a word from the item description.
    /// Only variants whose every layer image exists are listed.
    public static class ItemSearch
    {
        public sealed class Result
        {
            public readonly List<ForcedItem> Items = new List<ForcedItem>();
            /// Matches before the max cut.
            public int Total;
        }

        private static readonly Regex EquipmentFile = new Regex(@"^([a-z]{2}\d{2})-(\d{4})-\d{3}(\.png)?$");
        private static readonly Regex WeaponFile = new Regex(@"^(w\d{2})-\d{1,3}-(\d{4})(\.png)?$");

        /// Variant numbers that exist on every layer of the type, ascending;
        /// empty for an unknown type.
        public static int[] NumbersFor(StudioData d, string itemType)
        {
            var weapon = d.Catalog.GetWeapon(itemType);
            if (weapon != null)
            {
                IEnumerable<int> common = weapon.VariantsPerLayer[0];
                foreach (var layer in weapon.VariantsPerLayer.Skip(1)) common = common.Intersect(layer);
                return common.OrderBy(n => n).ToArray();
            }
            var entry = d.Catalog.GetEntry(itemType);
            return entry != null ? entry.Variants.OrderBy(n => n).ToArray() : Array.Empty<int>();
        }

        public static string Description(StudioData d, string itemType) =>
            d.Catalog.GetEntry(itemType)?.Description ?? d.Catalog.GetWeapon(itemType)?.Description ?? "";

        private static IEnumerable<string> AllTypes(StudioData d) =>
            d.Catalog.AllEntries.Select(e => e.ItemType)
                .Concat(d.Catalog.WeaponEntries.Select(w => w.ItemType))
                .OrderBy(t => t, StringComparer.Ordinal);

        public static Result Find(StudioData d, string query, int max = 15)
        {
            var r = new Result();
            string q = (query ?? "").Trim().Trim('"').ToLowerInvariant();
            int slash = Math.Max(q.LastIndexOf('/'), q.LastIndexOf('\\'));
            if (slash >= 0) q = q.Substring(slash + 1); // a pasted path: keep the file name
            if (q.Length == 0) return r;

            var file = EquipmentFile.Match(q);
            if (!file.Success) file = WeaponFile.Match(q);
            if (file.Success)
            {
                string type = file.Groups[1].Value;
                int num = int.Parse(file.Groups[2].Value);
                if (NumbersFor(d, type).Contains(num))
                {
                    r.Items.Add(new ForcedItem(type, num));
                    r.Total = 1;
                }
                return r;
            }

            foreach (var type in AllTypes(d))
            {
                bool descriptionHit = Description(d, type).ToLowerInvariant().Contains(q);
                foreach (int n in NumbersFor(d, type))
                {
                    var item = new ForcedItem(type, n);
                    if (!descriptionHit && !item.Token.Contains(q)) continue;
                    r.Total++;
                    if (r.Items.Count < max) r.Items.Add(item);
                }
            }
            return r;
        }
    }
}
```

**Step 4: Compile** (none; probe `ItemSearch.Find`, `ItemSearch.NumbersFor`, `ForcedItem`). Smoke call: `ItemSearch.Find(StudioData.Load(root), "hat").Total` is greater than 0 and `Find(..., "hd01-0001-130.png").Total` is 1 if `hd01` has variant 1 (print both).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/ForcedItem.cs* Assets/Scripts/Studio/ItemSearch.cs* Assets/Tests/EditMode/Studio/ItemSearchTests.cs*
git commit -m "feat(studio): item search by token, file name or description word"
```

---

## Task 16: Roller additions (exact item, species change, settings reset)

**Files:**
- Modify: `Assets/Scripts/Studio/StudioRoller.cs`
- Modify: `Assets/Scripts/Studio/RandomSettings.cs`
- Create: `Assets/Tests/EditMode/Studio/StudioRollerItemTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Linq;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioRollerItemTests
    {
        private static StudioData D => StudioTestRepo.Data;

        [Test]
        public void MakeItem_Armor_UsesTheNumberAndRollsSecondColors()
        {
            var entry = D.Catalog.GetEntry("hd08"); // second-color layer 160
            int n = ItemSearch.NumbersFor(D, "hd08").Last();

            var item = new StudioRoller(D).MakeItem("hd08", n, new System.Random(1));

            Assert.AreEqual("hd08", item.ItemType);
            Assert.AreEqual(n, item.ItemNum);
            Assert.IsNull(item.LayerVariants);
            CollectionAssert.AreEquivalent(entry.LayerColorVariance, item.VarianceColors.Keys);
        }

        [Test]
        public void MakeItem_ModularWeapon_EveryLayerUsesTheNumber()
        {
            var w = D.Catalog.WeaponEntries.First(e => e.Modular);
            int n = ItemSearch.NumbersFor(D, w.ItemType)[0];

            var item = new StudioRoller(D).MakeItem(w.ItemType, n, new System.Random(2));

            Assert.AreEqual(n, item.ItemNum);
            Assert.AreEqual(w.LayerCodes.Length, item.LayerVariants.Length);
            Assert.IsTrue(item.LayerVariants.All(v => v == n));
        }

        [Test]
        public void MakeItem_UnknownType_IsNull()
        {
            Assert.IsNull(new StudioRoller(D).MakeItem("zz99", 1, new System.Random(3)));
        }

        [Test]
        public void ChangeSpecies_NewBody_KeepsWearableGear_DropsRestricted()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            var roller = new StudioRoller(D);
            var restricted = D.Registry.Species["stone golem"].ItemRestrictions ?? new string[0];
            var rng = new System.Random(4);

            for (int i = 0; i < 50; i++)
            {
                var c = roller.RollCharacter(new[] { "human" }, s, rng);
                var keep = c.Gear.Where(kv => !restricted.Contains(kv.Value.ItemType)).ToDictionary(kv => kv.Key, kv => kv.Value);

                roller.ChangeSpecies(c, "stone golem", s, rng);

                Assert.AreEqual("stone golem", c.SpeciesName);
                Assert.AreEqual("stone golem", c.Body.SpeciesName);
                CollectionAssert.AreEquivalent(keep, c.Gear);
            }
        }

        [Test]
        public void RandomSettings_Reset_RestoresDefaults()
        {
            var s = new RandomSettings { Head = 0f, MiscEach = 1f, FacialHair = 0f };
            s.TypeWeights["hd01"] = 0f;

            s.Reset();

            var d = new RandomSettings();
            Assert.AreEqual(d.Head, s.Head);
            Assert.AreEqual(d.MiscEach, s.MiscEach);
            Assert.AreEqual(d.FacialHair, s.FacialHair);
            Assert.AreEqual(0, s.TypeWeights.Count);
        }
    }
}
```

**Step 2: Compile-check to see it fail** (`MakeItem`, `ChangeSpecies`, `Reset` not found).

**Step 3: Implement**

In `StudioRoller.cs`:

1. Add a helper after the constructor and use it in `RollGear` (replace the line that builds `restricted` from `data.Registry.Species[c.SpeciesName].ItemRestrictions`):
```csharp
        private HashSet<string> Restricted(string species) =>
            new HashSet<string>(data.Registry.Species[species].ItemRestrictions ?? Array.Empty<string>());
```
`RollGear` then starts:
```csharp
            c.Gear.Clear();
            var restricted = Restricted(c.SpeciesName);
```

2. Add after `RollGear`:
```csharp
        /// New species and a new body for it; gear the new species can wear stays.
        public void ChangeSpecies(StudioCharacter c, string species, RandomSettings s, Random rng)
        {
            c.SpeciesName = species;
            RollBody(c, s, rng);
            var restricted = Restricted(species);
            foreach (var slot in c.Gear.Keys.ToList())
                if (restricted.Contains(c.Gear[slot].ItemType)) c.Gear.Remove(slot);
        }

        /// This exact variant with random colors; null for an unknown type.
        /// A modular weapon uses the number on every layer.
        public StudioItem MakeItem(string itemType, int itemNum, Random rng)
        {
            var weapon = data.Catalog.GetWeapon(itemType);
            var entry = weapon == null ? data.Catalog.GetEntry(itemType) : null;
            if (weapon == null && entry == null) return null;

            var item = new StudioItem
            {
                ItemType = itemType,
                ItemNum = itemNum,
                BaseColor = data.Registry.Colors.GetRandomColor("main", rng)
            };
            if (weapon != null && weapon.Modular)
                item.LayerVariants = weapon.LayerCodes.Select(_ => itemNum).ToArray();
            AddVariance(item, weapon != null ? weapon.LayerColorVariance : entry.LayerColorVariance, rng);
            return item;
        }
```

3. Make the shield check shareable (ForcedItems uses it): change `private static bool IsShield` to `internal static bool IsShield`.

In `RandomSettings.cs`, add after `ChanceFor`:
```csharp
        /// Back to the defaults above; every weight back to 1.
        public void Reset()
        {
            var d = new RandomSettings();
            Head = d.Head; Torso = d.Torso; Arms = d.Arms; Legs = d.Legs; Feet = d.Feet;
            MainHand = d.MainHand; OffHand = d.OffHand; MiscEach = d.MiscEach;
            FacialHair = d.FacialHair; FacialDetail = d.FacialDetail;
            TypeWeights.Clear();
        }
```

**Step 4: Compile** (none; probe `MakeItem`, `ChangeSpecies`, `Reset`). B1's `StudioRollerTests` must still compile unchanged.

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioRoller.cs Assets/Scripts/Studio/RandomSettings.cs Assets/Tests/EditMode/Studio/StudioRollerItemTests.cs*
git commit -m "feat(studio): exact-variant items, species change that keeps wearable gear, settings reset"
```

---

## Task 17: Forced items

**Files:**
- Create: `Assets/Scripts/Studio/ForcedItems.cs`
- Create: `Assets/Tests/EditMode/Studio/ForcedItemsTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ForcedItemsTests
    {
        private static StudioData D => StudioTestRepo.Data;
        private static StudioRoller Roller => new StudioRoller(D);

        private static ForcedItem First(string type) => new ForcedItem(type, ItemSearch.NumbersFor(D, type)[0]);

        private static ForcedItem[] Several(string prefix, int count) =>
            D.Catalog.GetByPrefix(prefix)
                .SelectMany(e => ItemSearch.NumbersFor(D, e.ItemType).Select(n => new ForcedItem(e.ItemType, n)))
                .Take(count).ToArray();

        private static StudioCharacter Bare(string species = "human")
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 0f;
            return Roller.RollCharacter(new[] { species }, s, new System.Random(1));
        }

        [Test]
        public void Duplicate_IsRefused()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("hd01"), out _));

            Assert.IsFalse(f.TryAdd(First("hd01"), out var msg));
            StringAssert.Contains("already", msg);
        }

        [Test]
        public void NumberWithoutArt_IsRefused()
        {
            Assert.IsFalse(new ForcedItems(D).TryAdd(new ForcedItem("hd01", 9999), out var msg));
            StringAssert.Contains("no art", msg);
        }

        [Test]
        public void FiveOfOneSlotType_Fit_TheSixthIsRefused()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 6);
            for (int i = 0; i < 5; i++) Assert.IsTrue(f.TryAdd(hats[i], out var m), m);

            Assert.IsFalse(f.TryAdd(hats[5], out var msg));
            StringAssert.Contains("Misc", msg);
        }

        [Test]
        public void MiscSlotsAreSharedByOverflowAndMiscItems()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            var misc = Several("mc", 4);
            Assert.IsTrue(f.TryAdd(hats[0], out _));
            Assert.IsTrue(f.TryAdd(hats[1], out _)); // takes Misc 1
            for (int i = 0; i < 3; i++) Assert.IsTrue(f.TryAdd(misc[i], out _));

            Assert.IsFalse(f.TryAdd(misc[3], out _));
        }

        [TestCase("w04", "w01")] // two-handed main, then a one-handed off hand
        [TestCase("w01", "w04")] // two-handed cannot go in the off hand
        [TestCase("w08", "w04")] // shield in the off hand, then two-handed
        public void HandConflicts_AreRefused(string first, string second)
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First(first), out _));

            Assert.IsFalse(f.TryAdd(First(second), out var msg));
            StringAssert.Contains("two-handed", msg);
        }

        [Test]
        public void OneHandedAndShield_Fit_AThirdWeaponIsRefused()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("w01"), out _));
            Assert.IsTrue(f.TryAdd(First("w08"), out _));

            Assert.IsFalse(f.TryAdd(First("w02"), out var msg));
            StringAssert.Contains("Both hands", msg);
        }

        [Test]
        public void Apply_FirstOfAPrefixTakesItsSlot_LaterOnesGoToMisc()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            f.TryAdd(hats[0], out _);
            f.TryAdd(hats[1], out _);
            var c = Bare();

            f.Apply(c, Roller, new System.Random(2));

            Assert.IsTrue(hats[0].Matches(c.Gear[EquipmentSlot.Head]));
            Assert.IsTrue(hats[1].Matches(c.Gear[EquipmentSlot.Misc1]));
            Assert.AreEqual(2, c.Gear.Count);
        }

        [Test]
        public void Apply_KeepsOtherGear_AndASecondApplyChangesNothing()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("hd01"), out _);
            var c = Bare();
            var boots = Roller.MakeItem("fe01", ItemSearch.NumbersFor(D, "fe01")[0], new System.Random(3));
            c.Gear[EquipmentSlot.Feet] = boots;

            f.Apply(c, Roller, new System.Random(4));
            var placed = c.Gear[EquipmentSlot.Head];
            f.Apply(c, Roller, new System.Random(5));

            Assert.AreSame(boots, c.Gear[EquipmentSlot.Feet]);
            Assert.AreSame(placed, c.Gear[EquipmentSlot.Head], "the item and its colors stay");
        }

        [Test]
        public void Apply_ForcedTwoHanded_EmptiesTheOffHand()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("w04"), out _);
            var c = Bare();
            c.Gear[EquipmentSlot.OffHand] = Roller.MakeItem("w08", ItemSearch.NumbersFor(D, "w08")[0], new System.Random(6));

            f.Apply(c, Roller, new System.Random(7));

            Assert.AreEqual("w04", c.Gear[EquipmentSlot.MainHand].ItemType);
            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.OffHand));
        }

        [Test]
        public void Apply_ForcedShield_EmptiesARandomTwoHandedMainHand()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("w08"), out _);
            var c = Bare();
            c.Gear[EquipmentSlot.MainHand] = Roller.MakeItem("w04", ItemSearch.NumbersFor(D, "w04")[0], new System.Random(8));

            f.Apply(c, Roller, new System.Random(9));

            Assert.AreEqual("w08", c.Gear[EquipmentSlot.OffHand].ItemType);
            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.MainHand));
        }

        [Test]
        public void Apply_SkipsItemsTheSpeciesCannotWear()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("hd01"), out _)); // stone golem cannot wear hd01
            var c = Bare("stone golem");

            f.Apply(c, Roller, new System.Random(10));

            Assert.IsFalse(c.Gear.Values.Any(i => i.ItemType == "hd01"));
        }

        [Test]
        public void RemoveThenStripAndApply_MovesTheNextItemIntoTheSlot()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            f.TryAdd(hats[0], out _);
            f.TryAdd(hats[1], out _);
            var c = Bare();
            f.Apply(c, Roller, new System.Random(11));

            var before = f.Items.ToList();
            Assert.IsTrue(f.Remove(hats[0]));
            ForcedItems.Strip(c, before);
            f.Apply(c, Roller, new System.Random(12));

            Assert.IsTrue(hats[1].Matches(c.Gear[EquipmentSlot.Head]));
            Assert.AreEqual(1, c.Gear.Count);
        }
    }
}
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement**

`Assets/Scripts/Studio/ForcedItems.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Starquill.Core;

namespace Starquill.Studio
{
    /// The Bulk screen's Forced items: variants put on every unlocked
    /// character after each roll (P9-P11). The first item of a slot type
    /// takes its slot, later ones go to Misc 1-4; weapons: one main hand,
    /// one off hand, shields only in the off hand, two-handed leaves the
    /// off hand empty.
    public sealed class ForcedItems
    {
        private static readonly EquipmentSlot[] MiscSlots =
            { EquipmentSlot.Misc1, EquipmentSlot.Misc2, EquipmentSlot.Misc3, EquipmentSlot.Misc4 };

        private readonly StudioData data;
        private readonly List<ForcedItem> items = new List<ForcedItem>();

        public ForcedItems(StudioData data) { this.data = data; }

        public IReadOnlyList<ForcedItem> Items => items;

        /// False, with the reason, when the item cannot be added.
        public bool TryAdd(ForcedItem item, out string message)
        {
            message = null;
            if (items.Contains(item))
            {
                message = $"{item} is already forced.";
                return false;
            }
            if (!ItemSearch.NumbersFor(data, item.ItemType).Contains(item.ItemNum))
            {
                message = $"{item} has no art (an image for one of its layers is missing).";
                return false;
            }

            var next = new List<ForcedItem>(items) { item };
            if (item.IsWeapon)
            {
                if (!PlanHands(next.Where(i => i.IsWeapon), out _, out _, out message)) return false;
            }
            else if (MiscNeeded(next) > MiscSlots.Length)
            {
                message = $"Misc 1-4 are full, so {item} has no slot. Remove a forced item first.";
                return false;
            }
            items.Add(item);
            return true;
        }

        public bool Remove(ForcedItem item) => items.Remove(item);

        /// Puts every forced item the species can wear on c, replacing what
        /// is in those slots. An item already in its slot stays as it is.
        public void Apply(StudioCharacter c, StudioRoller roller, Random rng)
        {
            if (items.Count == 0) return;
            var restricted = new HashSet<string>(data.Registry.Species[c.SpeciesName].ItemRestrictions ?? Array.Empty<string>());
            var usable = items.Where(i => !restricted.Contains(i.ItemType)).ToList();
            var taken = new HashSet<EquipmentSlot>();

            PlanHands(usable.Where(i => i.IsWeapon), out var main, out var off, out _);
            if (main != null) Put(c, EquipmentSlot.MainHand, main, taken, roller, rng);
            if (off != null) Put(c, EquipmentSlot.OffHand, off, taken, roller, rng);

            var prefixes = new HashSet<string>();
            foreach (var item in usable.Where(i => !i.IsWeapon))
            {
                EquipmentSlot slot;
                if (item.Prefix != "mc" && prefixes.Add(item.Prefix))
                {
                    slot = NaturalSlot(item.Prefix);
                }
                else
                {
                    int free = Array.FindIndex(MiscSlots, s => !taken.Contains(s));
                    if (free < 0) continue;
                    slot = MiscSlots[free];
                }
                Put(c, slot, item, taken, roller, rng);
            }

            if (c.Gear.TryGetValue(EquipmentSlot.MainHand, out var held) && IsTwoHanded(held.ItemType)
                && c.Gear.ContainsKey(EquipmentSlot.OffHand))
                c.Gear.Remove(taken.Contains(EquipmentSlot.OffHand) ? EquipmentSlot.MainHand : EquipmentSlot.OffHand);
        }

        /// Empties every slot of c that holds one of these items.
        public static void Strip(StudioCharacter c, IEnumerable<ForcedItem> list)
        {
            var set = list.ToList();
            foreach (var slot in c.Gear.Keys.ToList())
                if (set.Any(i => i.Matches(c.Gear[slot]))) c.Gear.Remove(slot);
        }

        private static void Put(StudioCharacter c, EquipmentSlot slot, ForcedItem item, HashSet<EquipmentSlot> taken,
            StudioRoller roller, Random rng)
        {
            taken.Add(slot);
            if (c.Gear.TryGetValue(slot, out var current) && item.Matches(current)) return;
            c.Gear[slot] = roller.MakeItem(item.ItemType, item.ItemNum, rng);
        }

        private bool PlanHands(IEnumerable<ForcedItem> weapons, out ForcedItem main, out ForcedItem off, out string problem)
        {
            main = off = null;
            problem = null;
            foreach (var w in weapons)
            {
                if (StudioRoller.IsShield(w.ItemType))
                {
                    if (off != null) { problem = $"The off hand already holds {off}."; return false; }
                    off = w;
                }
                else if (main == null)
                {
                    main = w;
                }
                else if (off != null)
                {
                    problem = "Both hands are already forced. Remove a weapon first.";
                    return false;
                }
                else if (IsTwoHanded(w.ItemType))
                {
                    problem = $"{w} is two-handed and can only go in the main hand, which holds {main}.";
                    return false;
                }
                else
                {
                    off = w;
                }
            }
            if (main != null && off != null && IsTwoHanded(main.ItemType))
            {
                problem = $"{main} is two-handed, so the off hand cannot hold {off}.";
                return false;
            }
            return true;
        }

        private bool IsTwoHanded(string itemType) => data.Catalog.GetWeapon(itemType)?.HandType == "two_handed";

        /// Misc slots the armor list needs: every item after the first of its
        /// slot type, plus every mc item.
        private static int MiscNeeded(IEnumerable<ForcedItem> list) =>
            list.Where(i => !i.IsWeapon).GroupBy(i => i.Prefix).Sum(g => g.Key == "mc" ? g.Count() : g.Count() - 1);

        private static EquipmentSlot NaturalSlot(string prefix)
        {
            switch (prefix)
            {
                case "hd": return EquipmentSlot.Head;
                case "tr": return EquipmentSlot.Torso;
                case "ar": return EquipmentSlot.Arms;
                case "lg": return EquipmentSlot.Legs;
                case "fe": return EquipmentSlot.Feet;
                default: return EquipmentSlot.Misc1;
            }
        }
    }
}
```

**Step 4: Compile** (none; probe `ForcedItems` with `TryAdd`, `Remove`, `Apply`, `Strip`, `Items`). Smoke call: add `hd01-<first>` and `w04-<first>`, apply to 100 rolled characters at chance 1, count characters that end with two-handed main plus off hand (expect 0).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/ForcedItems.cs* Assets/Tests/EditMode/Studio/ForcedItemsTests.cs*
git commit -m "feat(studio): forced items with Godot slot limits and hand rules"
```

---

## Task 18: Settings: excluded species and default export folder

**Files:**
- Modify: `Assets/Scripts/Studio/StudioSettings.cs`
- Modify: `Assets/Tests/EditMode/Studio/StudioSettingsTests.cs`

**Step 1: Write the failing tests** (add to `StudioSettingsTests`)

```csharp
        [Test]
        public void ExcludedSpecies_DefaultsEmpty_AndRoundTrips()
        {
            Assert.IsEmpty(new StudioSettings().excludedSpecies);
            string path = Path.Combine(Path.GetTempPath(), "studio_" + Path.GetRandomFileName(), "studio-settings.json");
            try
            {
                var s = new StudioSettings();
                s.excludedSpecies.Add("naga");
                s.Save(path);
                CollectionAssert.AreEqual(new[] { "naga" }, StudioSettings.Load(path).excludedSpecies);
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void Load_B1FileWithoutExcludedSpecies_GivesAnEmptyList()
        {
            string path = Path.Combine(Path.GetTempPath(), "b1_" + Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, "{ \"repoRoot\": \"/r\", \"count\": 16 }");
            try
            {
                var s = StudioSettings.Load(path);
                Assert.AreEqual(16, s.count);
                Assert.IsNotNull(s.excludedSpecies);
                Assert.IsEmpty(s.excludedSpecies);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ResolvedExportFolder_EmptyMeansDefault()
        {
            Assert.AreEqual(StudioSettings.DefaultExportFolder, new StudioSettings().ResolvedExportFolder);
            Assert.AreEqual("/x", new StudioSettings { exportFolder = "/x" }.ResolvedExportFolder);
        }
```

**Step 2: Compile-check to see it fail.**

**Step 3: Implement** in `StudioSettings.cs`:

- Add `using System.Collections.Generic;`.
- Add the field after `count`:
```csharp
        /// Species left out of random rolls (unchecked in the Species window).
        /// Stored as exclusions so species added later start checked (P13).
        public List<string> excludedSpecies = new List<string>();
```
- Replace `ResolvedExportFolder` with:
```csharp
        public static string DefaultExportFolder => Path.Combine(AppFolder, "exports");

        public string ResolvedExportFolder =>
            string.IsNullOrWhiteSpace(exportFolder) ? DefaultExportFolder : exportFolder;
```
- In `Load`, replace the `return JsonUtility.FromJson...` line with:
```csharp
                {
                    var s = JsonUtility.FromJson<StudioSettings>(File.ReadAllText(path)) ?? new StudioSettings();
                    s.excludedSpecies ??= new List<string>();
                    return s;
                }
```
(the `if (File.Exists(path))` now guards that block).

**Step 4: Compile** (none; probe `excludedSpecies`, `DefaultExportFolder`).

**Step 5: Commit**

```bash
git add Assets/Scripts/Studio/StudioSettings.cs Assets/Tests/EditMode/Studio/StudioSettingsTests.cs
git commit -m "feat(studio): remember unchecked species; default export folder"
```

---

## Task 19: Species window

**Files:**
- Create: `Assets/Scripts/Studio/UI/SpeciesPickerView.cs`

No unit tests (wiring; checked at CHECKPOINT E).

**Step 1: Implement**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup with a checkbox per species. Unchecked names go into
    /// excluded (StudioSettings.excludedSpecies); onChanged saves.
    public sealed class SpeciesPickerView
    {
        public SpeciesPickerView(Transform canvasRoot, IReadOnlyList<string> names, List<string> excluded, Action onChanged)
        {
            var list = StudioUi.Popup(canvasRoot, "Species", new Vector2(560, 860), out _);
            var toggles = new List<Toggle>();

            var buttons = StudioUi.Row(list, "AllNone", 44f);
            StudioUi.Button(buttons.transform, "All", () => SetAll(true), 120f);
            StudioUi.Button(buttons.transform, "None", () => SetAll(false), 120f);
            StudioUi.Label(list, "New and rerolled characters pick from the checked species.", 17f);

            foreach (var name in names)
            {
                string n = name;
                toggles.Add(StudioUi.Toggle(list, n, !excluded.Contains(n), on =>
                {
                    if (on) excluded.Remove(n);
                    else if (!excluded.Contains(n)) excluded.Add(n);
                    onChanged();
                }, 400f));
            }

            void SetAll(bool on)
            {
                excluded.Clear();
                if (!on) excluded.AddRange(names);
                foreach (var t in toggles) t.SetIsOnWithoutNotify(on);
                onChanged();
            }
        }
    }
}
```

**Step 2: Compile** (none; probe the type).

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI/SpeciesPickerView.cs*
git commit -m "feat(studio): species window (check which species random characters use)"
```

---

## Task 20: Random settings window

**Files:**
- Create: `Assets/Scripts/Studio/UI/SettingsWindow.cs`

**Step 1: Implement**

```csharp
using System;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Popup with the RandomSettings sliders: chance per slot, facial
    /// chances, weight per item type grouped by slot. Edits the Bulk
    /// screen's settings object directly; the next roll uses them.
    public sealed class SettingsWindow
    {
        private static readonly (string prefix, string title)[] Groups =
        {
            ("hd", "Head"), ("tr", "Torso"), ("ar", "Arms"), ("lg", "Legs"), ("fe", "Feet"), ("mc", "Misc")
        };

        public SettingsWindow(Transform canvasRoot, StudioData data, RandomSettings s)
        {
            var list = StudioUi.Popup(canvasRoot, "Random settings", new Vector2(820, 920), out var close);

            var top = StudioUi.Row(list, "Top", 44f);
            StudioUi.Button(top.transform, "Reset to defaults", () =>
            {
                s.Reset();
                close();
                new SettingsWindow(canvasRoot, data, s);
            }, 230f);
            StudioUi.Label(top.transform, "Changes apply to the next Randomize All, Equip Random or Reroll.", 17f);

            Header(list, "Chance per slot (0 = never, 1 = always)");
            Chance(list, "Head", s.Head, v => s.Head = v);
            Chance(list, "Torso", s.Torso, v => s.Torso = v);
            Chance(list, "Arms", s.Arms, v => s.Arms = v);
            Chance(list, "Legs", s.Legs, v => s.Legs = v);
            Chance(list, "Feet", s.Feet, v => s.Feet = v);
            Chance(list, "Main hand", s.MainHand, v => s.MainHand = v);
            Chance(list, "Off hand", s.OffHand, v => s.OffHand = v);
            Note(list, "Off hand rolls only when the main hand is empty or one-handed.");
            Chance(list, "Misc, each of 1-4", s.MiscEach, v => s.MiscEach = v);

            Header(list, "Face");
            Chance(list, "Facial hair", s.FacialHair, v => s.FacialHair = v);
            Chance(list, "Facial detail", s.FacialDetail, v => s.FacialDetail = v);

            Header(list, "Weight per item type within its slot (0 = never, 1 = normal)");
            foreach (var (prefix, title) in Groups)
            {
                Note(list, title);
                foreach (var e in data.Catalog.GetByPrefix(prefix)) Weight(list, e.ItemType, e.Description, s);
            }
            Note(list, "Weapons");
            foreach (var w in data.Catalog.WeaponEntries) Weight(list, w.ItemType, w.Description, s);
        }

        private static void Header(Transform list, string text) => StudioUi.Label(list, text, 22f).color = StudioUi.Accent;

        private static void Note(Transform list, string text) => StudioUi.Label(list, text, 17f);

        private static void Chance(Transform list, string label, float value, Action<float> set) =>
            StudioUi.Slider(list, label, 0f, 1f, value, set, "0.00");

        private static void Weight(Transform list, string itemType, string description, RandomSettings s) =>
            StudioUi.Slider(list, $"{itemType}  {description}", 0f, 3f, s.WeightOf(itemType), v => s.TypeWeights[itemType] = v, "0.0");
    }
}
```

**Step 2: Compile** (none; probe the type).

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI/SettingsWindow.cs*
git commit -m "feat(studio): random settings window (slot chances, facial chances, item type weights)"
```

---

## Task 21: Skin color picker

**Files:**
- Create: `Assets/Scripts/Studio/UI/SwatchPicker.cs`

**Step 1: Implement**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup of color squares, 8 per row; a click picks and closes.
    public sealed class SwatchPicker
    {
        public SwatchPicker(Transform canvasRoot, string title, IReadOnlyList<Color> colors, Action<Color> onPick)
        {
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(600, 460), out var close);
            HorizontalLayoutGroup row = null;
            for (int i = 0; i < colors.Count; i++)
            {
                if (i % 8 == 0) row = StudioUi.Row(list, "Swatches", 54f, 8f);
                var color = colors[i];
                StudioUi.Swatch(row.transform, color, 54f, () => { onPick(color); close(); });
            }
        }
    }
}
```

**Step 2: Compile** (none; probe the type).

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI/SwatchPicker.cs*
git commit -m "feat(studio): color swatch picker popup"
```

---

## Task 22: Bulk screen complete

**Files:**
- Modify (replace whole file): `Assets/Scripts/Studio/UI/BulkScreen.cs`

No unit tests (logic is in Tasks 15-18). Checked by the executor's Editor smoke run (Task 23) and at CHECKPOINT E.

**Step 1: Replace `BulkScreen.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// The grid of random characters (ported from the Godot randomizer).
    /// Top: Count, Species, Randomize All, Equip Random, Settings, then the
    /// search box and Forced items chips. Bottom: export. Each card: species,
    /// skin color, Select, Lock, Reroll.
    public sealed class BulkScreen
    {
        private const int MaxResults = 15;
        private const string NoSpecies = "No species is checked. Use the Species button to check at least one.";

        private sealed class Card
        {
            public GameObject Root;
            public CharacterDisplay Display;
            public RawImage Image;
            public TMP_Dropdown Species;
            public Button SkinButton;
            public Image Skin;
            public Toggle Lock;
            public bool Selected;
        }

        private readonly StudioApp app;
        private readonly Transform canvas;
        private readonly StudioRoller roller;
        private readonly ForcedItems forced;
        private readonly RandomSettings random = new RandomSettings();
        private readonly System.Random rng = new System.Random(Environment.TickCount);
        private readonly List<StudioCharacter> characters = new List<StudioCharacter>();
        private readonly List<Card> cards = new List<Card>();
        private readonly Dictionary<ForcedItem, GameObject> chips = new Dictionary<ForcedItem, GameObject>();
        private readonly RectTransform grid;
        private readonly Transform forceRow;
        private readonly TMP_Text noChips;
        private readonly TMP_InputField search;
        private readonly RectTransform results;
        private readonly TMP_Text status;
        private readonly Button warnings, speciesButton, exportButton;

        private StudioData Data => app.Data;
        private StudioSettings S => app.Settings;

        public BulkScreen(Transform parent, StudioApp app)
        {
            this.app = app;
            canvas = parent;
            roller = new StudioRoller(app.Data);
            forced = new ForcedItems(app.Data);

            var root = StudioUi.Panel(parent, "Bulk", StudioUi.Background);
            StudioUi.Stretch(root);

            var top = StudioUi.Column(root, "TopBars", 8f);
            Dock((RectTransform)top.transform, true, 104f);

            var make = StudioUi.Row(top.transform, "Make", 48f);
            StudioUi.Label(make.transform, "Count", 20f);
            StudioUi.Dropdown(make.transform, StudioSettings.Counts.Select(c => c.ToString()).ToList(),
                Array.IndexOf(StudioSettings.Counts, S.count),
                i => { S.count = StudioSettings.Counts[i]; app.SaveSettings(); SetCount(S.count); }, 90f);
            speciesButton = StudioUi.Button(make.transform, "Species", OpenSpecies, 190f);
            StudioUi.Button(make.transform, "Randomize All", RandomizeAll, 170f);
            StudioUi.Button(make.transform, "Equip Random", EquipRandom, 170f);
            StudioUi.Button(make.transform, "Settings", OpenSettings, 130f);
            StudioUi.Spacer(make.transform);
            warnings = StudioUi.Button(make.transform, "Warnings", () => new WarningsView(canvas, app), 170f);
            StudioUi.Button(make.transform, "Repo folder", app.ShowRepoPicker, 150f);

            forceRow = StudioUi.Row(top.transform, "Force", 48f).transform;
            search = StudioUi.Input(forceRow, "", _ => { }, 420f, "Search: hd01-0001, a file name, or a word like hat");
            search.onValueChanged.AddListener(ShowResults);
            results = CreateResultsPanel(search.transform);
            StudioUi.Label(forceRow, "Forced items:", 20f);
            noChips = StudioUi.Label(forceRow, "none", 18f);

            var bottom = StudioUi.Column(root, "BottomBar", 4f);
            Dock((RectTransform)bottom.transform, false, 84f);
            var export = StudioUi.Row(bottom.transform, "Export", 48f);
            StudioUi.Label(export.transform, "Scale", 20f);
            StudioUi.Dropdown(export.transform, StudioSettings.Scales.Select(x => x + "x").ToList(),
                Array.IndexOf(StudioSettings.Scales, StudioSettings.SnapScale(S.exportScale)),
                i => { S.exportScale = StudioSettings.Scales[i]; app.SaveSettings(); }, 80f);
            StudioUi.Toggle(export.transform, "Trim", S.trim, v => { S.trim = v; app.SaveSettings(); }, 90f);
            StudioUi.Label(export.transform, "Folder", 20f);
            StudioUi.Input(export.transform, S.exportFolder, SetExportFolder, 640f, "Default: " + StudioSettings.DefaultExportFolder);
            StudioUi.Button(export.transform, "Open folder", OpenExportFolder, 150f);
            exportButton = StudioUi.Button(export.transform, "Export", Export, 220f);
            status = StudioUi.Label(bottom.transform, "", 18f);

            grid = StudioUi.ScrollGrid(root, new Vector2(220, 340), 12f);
            StudioUi.Stretch((RectTransform)grid.parent.parent, 0, 120, 0, 96); // the ScrollView

            SetCount(S.count);
            UpdateSpeciesButton();
            UpdateWarningsButton();
            UpdateExportButton();
        }

        private static void Dock(RectTransform rt, bool top, float height)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.sizeDelta = new Vector2(-24, height);
            rt.anchoredPosition = new Vector2(0, top ? -8 : 6);
        }

        // ---- characters ----

        private List<string> ChosenSpecies() => Data.SpeciesNames.Where(n => !S.excludedSpecies.Contains(n)).ToList();

        private StudioCharacter NewCharacter(IReadOnlyList<string> species)
        {
            var c = roller.RollCharacter(species, random, rng);
            forced.Apply(c, roller, rng);
            return c;
        }

        private void SetCount(int count)
        {
            var species = ChosenSpecies();
            while (characters.Count < count)
            {
                if (species.Count == 0) { ShowStatus(NoSpecies, true); break; }
                characters.Add(NewCharacter(species));
                cards.Add(CreateCard(cards.Count));
                Redraw(cards.Count - 1);
            }
            while (characters.Count > count)
            {
                int last = characters.Count - 1;
                UnityEngine.Object.Destroy(cards[last].Root);
                UnityEngine.Object.Destroy(cards[last].Display.gameObject);
                cards.RemoveAt(last);
                characters.RemoveAt(last);
            }
            UpdateExportButton();
        }

        private Card CreateCard(int index)
        {
            var card = new Card();
            var panel = StudioUi.Panel(grid, $"Card{index + 1}", StudioUi.PanelColor);
            card.Root = panel.gameObject;
            var col = StudioUi.Column(panel, "Column", 4f);
            StudioUi.Stretch((RectTransform)col.transform, 6, 6, 6, 6);

            var checker = new GameObject("Checker", typeof(RectTransform), typeof(RawImage));
            checker.transform.SetParent(col.transform, false);
            var checkerImage = checker.GetComponent<RawImage>();
            checkerImage.texture = StudioUi.Checkerboard();
            checkerImage.uvRect = new Rect(0, 0, 13, 13);
            StudioUi.Size(checker, 208, 208);

            var doll = new GameObject("Character", typeof(RectTransform), typeof(RawImage));
            doll.transform.SetParent(checker.transform, false);
            StudioUi.Stretch((RectTransform)doll.transform);
            card.Image = doll.GetComponent<RawImage>();
            card.Display = app.Renderer.CreatePreview();
            card.Image.texture = card.Display.Texture;

            card.Species = StudioUi.Dropdown(col.transform, Data.SpeciesNames, 0, v => ChangeSpecies(index, v), -1f);
            StudioUi.Size(card.Species.gameObject, -1, 34f);

            var body = StudioUi.Row(col.transform, "Body", 34f, 6f);
            card.SkinButton = StudioUi.Button(body.transform, "Skin", () => PickSkin(index), 64f);
            card.Skin = StudioUi.Swatch(body.transform, Color.white, 28f, () => PickSkin(index));
            StudioUi.Toggle(body.transform, "Select", false, v => { card.Selected = v; UpdateExportButton(); }, 96f);

            var row = StudioUi.Row(col.transform, "Buttons", 34f, 6f);
            card.Lock = StudioUi.Toggle(row.transform, "Lock", false, v => characters[index].Locked = v, 90f);
            StudioUi.Button(row.transform, "Reroll", () => Reroll(index), 100f);
            return card;
        }

        private void Redraw(int i)
        {
            var c = characters[i];
            var card = cards[i];
            app.Renderer.Draw(card.Display, c);
            card.Species.SetValueWithoutNotify(Data.SpeciesNames.IndexOf(c.SpeciesName));
            card.Skin.color = c.Body.SkinColor;
            card.Lock.SetIsOnWithoutNotify(c.Locked);
            UpdateWarningsButton();
        }

        private void RandomizeAll()
        {
            var species = ChosenSpecies();
            if (species.Count == 0) { ShowStatus(NoSpecies, true); return; }
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                characters[i] = NewCharacter(species);
                Redraw(i);
            }
            ShowStatus("", false);
        }

        private void EquipRandom()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                roller.RollGear(characters[i], random, rng);
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus("", false);
        }

        private void Reroll(int i)
        {
            var species = ChosenSpecies();
            if (species.Count == 0) { ShowStatus(NoSpecies, true); return; }
            bool locked = characters[i].Locked;
            characters[i] = NewCharacter(species);
            characters[i].Locked = locked;
            Redraw(i);
        }

        private void ChangeSpecies(int i, int option)
        {
            var c = characters[i];
            roller.ChangeSpecies(c, Data.SpeciesNames[option], random, rng);
            if (!c.Locked) forced.Apply(c, roller, rng);
            Redraw(i);
        }

        private void PickSkin(int i)
        {
            var c = characters[i];
            var palette = Data.Registry.Colors.ResolveColorField(Data.Registry.Species[c.SpeciesName].SkinColor);
            new SwatchPicker(canvas, $"Skin color: {c.SpeciesName}", palette, color =>
            {
                c.Body.SkinColor = color;
                Redraw(i);
            });
        }

        // ---- species and settings windows ----

        private void OpenSpecies()
        {
            search.text = "";
            new SpeciesPickerView(canvas, Data.SpeciesNames, S.excludedSpecies, () =>
            {
                app.SaveSettings();
                UpdateSpeciesButton();
            });
        }

        private void OpenSettings()
        {
            search.text = "";
            new SettingsWindow(canvas, Data, random);
        }

        private void UpdateSpeciesButton()
        {
            int n = ChosenSpecies().Count;
            speciesButton.GetComponentInChildren<TMP_Text>().text = $"Species ({n}/{Data.SpeciesNames.Count})";
            if (n == 0) ShowStatus(NoSpecies, true);
            else if (status.text == NoSpecies) ShowStatus("", false);
        }

        // ---- search and forced items ----

        private static RectTransform CreateResultsPanel(Transform searchInput)
        {
            var go = new GameObject("SearchResults", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(searchInput, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -4);
            rt.sizeDelta = new Vector2(640, 0);
            go.GetComponent<Image>().color = StudioUi.PanelColor;
            var col = go.GetComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(6, 6, 6, 6);
            col.spacing = 2f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Draw above the card grid, which comes later in the hierarchy.
            var overlay = go.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 50;
            go.AddComponent<GraphicRaycaster>();
            go.SetActive(false);
            return rt;
        }

        private void ShowResults(string query)
        {
            for (int i = results.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(results.GetChild(i).gameObject);
            if (string.IsNullOrWhiteSpace(query))
            {
                results.gameObject.SetActive(false);
                return;
            }
            results.gameObject.SetActive(true);
            var overlay = results.GetComponent<Canvas>(); // nested canvases can drop these when first enabled
            overlay.overrideSorting = true;
            overlay.sortingOrder = 50;

            var found = ItemSearch.Find(Data, query, MaxResults);
            foreach (var item in found.Items)
            {
                var it = item;
                var button = StudioUi.Button(results, $"{it.Token}   {ItemSearch.Description(Data, it.ItemType)}", () => AddForced(it), -1f);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(10, 0, 0, 0);
                button.interactable = !forced.Items.Contains(it); // already a chip
            }
            if (found.Total == 0)
                StudioUi.Label(results, "No match. Try hd01-0001, a file name, or a word like hat.", 18f);
            else if (found.Total > found.Items.Count)
                StudioUi.Label(results, $"Showing {found.Items.Count} of {found.Total}. Type more to narrow it.", 18f);
        }

        private void AddForced(ForcedItem item)
        {
            if (!forced.TryAdd(item, out var message))
            {
                ShowStatus(message, true);
                return;
            }
            search.text = "";
            AddChip(item);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus($"Forced {item} on every unlocked character. It stays through Randomize All and Equip Random.", false);
        }

        private void AddChip(ForcedItem item)
        {
            string label = item.Token + "  x";
            var chip = StudioUi.Button(forceRow, label, () => RemoveForced(item), 9f * label.Length + 30f);
            chip.GetComponent<Image>().color = StudioUi.ChipColor;
            chip.GetComponentInChildren<TMP_Text>().fontSize = 17f;
            chips[item] = chip.gameObject;
            noChips.gameObject.SetActive(false);
        }

        private void RemoveForced(ForcedItem item)
        {
            var before = forced.Items.ToList();
            forced.Remove(item);
            if (chips.TryGetValue(item, out var chip))
            {
                UnityEngine.Object.Destroy(chip);
                chips.Remove(item);
            }
            noChips.gameObject.SetActive(chips.Count == 0);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                ForcedItems.Strip(characters[i], before);
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus($"Removed {item}. Its slot stays empty until the next Randomize All or Equip Random.", false);
        }

        // ---- export ----

        private void SetExportFolder(string value)
        {
            string v = (value ?? "").Trim().Trim('"');
            if (v.Length > 0 && !Path.IsPathRooted(v))
            {
                ShowStatus(@"Use a full folder path, for example C:\Users\you\Pictures\Starquill.", true);
                return;
            }
            S.exportFolder = v;
            app.SaveSettings();
            ShowStatus($"Exports go to {S.ResolvedExportFolder}", false);
        }

        private void Export()
        {
            string folder = S.ResolvedExportFolder;
            var targets = Enumerable.Range(0, characters.Count).Where(i => cards[i].Selected).ToList();
            if (targets.Count == 0) targets = Enumerable.Range(0, characters.Count).ToList();
            try
            {
                Directory.CreateDirectory(folder);
                var today = DateTime.Now;
                foreach (int i in targets)
                {
                    var png = app.Renderer.ExportPng(characters[i], StudioSettings.SnapScale(S.exportScale), S.trim, S.padding);
                    File.WriteAllBytes(ExportNamer.UniquePath(folder, ExportNamer.FileName(characters[i].SpeciesName, i + 1, today)), png);
                }
                ShowStatus($"Exported {targets.Count} PNGs to {folder}", false);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                ShowStatus($"Could not write to {folder}: {e.Message}. Nothing was lost; pick another folder.", true);
            }
            UpdateWarningsButton();
        }

        private void OpenExportFolder()
        {
            string folder = S.ResolvedExportFolder;
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is UriFormatException)
            {
                ShowStatus($"Could not open {folder}: {e.Message}", true);
            }
        }

        private void UpdateExportButton()
        {
            int n = cards.Count(c => c.Selected);
            exportButton.GetComponentInChildren<TMP_Text>().text = n > 0 ? $"Export selected ({n})" : $"Export all ({cards.Count})";
        }

        // ---- status ----

        private void ShowStatus(string message, bool error)
        {
            status.color = error ? StudioUi.ErrorColor : StudioUi.TextColor;
            status.text = message;
        }

        private void UpdateWarningsButton()
        {
            int count = Data.Issues.Count + app.Renderer.Resolver.MissingPaths.Count;
            warnings.GetComponentInChildren<TMP_Text>().text = $"Warnings ({count})";
        }
    }
}
```

**Step 2: Compile** (none). Check the Console for new warnings in `Assets/Scripts/Studio`. Probe `BulkScreen` constructor `(Transform, StudioApp)`.

**Step 3: Commit**

```bash
git add Assets/Scripts/Studio/UI/BulkScreen.cs
git commit -m "feat(studio): Bulk screen complete (species, search and forced items, card species and skin, selected export, export folder)"
```

---

## Task 23: Editor smoke run and Linux build

**Files:** none new (unless a fix is needed).

**Step 1: Smoke run in the Editor (executor)**

Open `Assets/Scenes/CharacterStudio.unity` (see Facts: dirty ExploreScene check), `play_game`, then drive it with a scratchpad script that finds objects by name and invokes them (B1's `Drive.cs` pattern):
- 8 cards; top bar shows `Species (27/27)`; the bottom shows `Export all (8)`.
- Type `hat` in the search field (`TMP_InputField.text = "hat"`): `SearchResults` is active with 15 buttons and a "Showing 15 of N" label. Invoke the first result: a chip named after the token appears in the `Force` row, `SearchResults` hides, and the status says "Forced ...". (Which slot the item lands in is covered by `ForcedItemsTests`; the capture below shows it on the cards.)
- Search `w04`, add it; then search `w08` and add it: refused with a "two-handed" status in red.
- Lock card 1, Randomize All: card 1's species unchanged; the forced hat is on every other card.
- Change card 2's species dropdown to `stone golem`: no hat on card 2 (restricted), body redrawn.
- Open Species, press None: the button shows `Species (0/27)` and Randomize All shows the red NoSpecies message; press All.
- Open Settings: the popup lists sliders; Reset to defaults reopens it; Close.
- Select cards 1 and 3: the button says `Export selected (2)`; Export writes 2 PNGs to `Library/CharacterStudio/exports`; delete them afterwards.
- `capture_ui_canvas` of `StudioCanvas`: labels on one line, dropdowns show a triangle arrow.
- `get_unity_logs` errors: none from `Starquill.Studio` (the Coplay capture's "Releasing render texture" error is the tool's own, B1).

Stop play. Fix any exception before the build; layout polish waits for Zach.

**Step 2: Linux build**

Run `CharacterStudioTools.BuildLinux()` through a scratchpad script (it times out in Coplay after 60 s and keeps running); wait for a new `Character Studio build` line in `~/.config/unity3d/Editor.log`. Record the size. Save `git diff ProjectSettings/` before the build and confirm it is unchanged after. (The Windows build waits for B6 or Zach's request.)

**Step 3: Update the progress doc and commit**

```bash
git add docs/plans/2026-11-08-character-studio-progress.md
git commit -m "docs: Character Studio B2 done, CHECKPOINT E pending"
```

**CHECKPOINT E (Zach):**
- Test Runner > EditMode > Run All: all green except the 2 known old failures. New B2 tests: `ItemSearchTests` (7 tests, 10 cases), `StudioRollerItemTests` (5), `ForcedItemsTests` (12 tests, 14 cases), `StudioSettingsTests` (+3).
- In the Editor or the Linux build: Species window (uncheck a few, Randomize All only rolls checked ones; restart remembers them), search by `hd01-0001`, by a pasted file name, by `hat`; add chips, remove one with its x; Equip Random keeps the forced items; a weapon conflict shows a red message; card species and skin; Settings sliders change the next roll (e.g. Head 0 gives no hats); Select a few and export; type an export folder.
- Then: detailed tasks for B3 (Single screen).

---

## Stage B3: Single screen (Tasks 24-31)

Written 2026-11-08 during the run-through Zach asked for ("continue all the way through the tasks until complete, then a linux build for full testing of all remaining checkpoints"). Checkpoints E-G are tested together on the final Linux build.

### Decisions made while planning B3 (2026-11-08)

| # | Decision | Why |
|---|---|---|
| P17 | From B3 on, the plan lists tasks, files, decisions, tests and checks; the code lives in the commits, not copied here. | Planner and executor are the same session in one run; a second copy of every file in this doc only drifts. |
| P18 | The Single screen opens over the Bulk screen (Bulk stays alive, hidden). A card's **Edit** opens it on that same character; **Back** redraws the card. | No second copy of the character to sync; Bulk keeps its state. |
| P19 | Variant thumbnails: the item or body part drawn alone through the game's `DisplayBuilder` at 200 px, cropped to the drawing. Items in their current colors, parts in the current skin/hair/eye color. | Shows what the artist will get, layers and tints included; one PNG per layer would not. |
| P20 | **None** is offered for hair, facial hair and facial detail; every other part always has a variant (the species draws it). | Matches the game data: only those can be absent. |
| P21 | A gear row has its own **Lock**: locked slots survive Reroll gear, Equip Random and the other rows' rerolls. The card Lock (whole character) is unchanged. | Design §2 Gear "row reroll, Lock". |
| P22 | Color picker: palette swatches (species palette for skin, hair, eyes; the main palette for gear) plus H/S/V sliders and a hex field, applied live. One undo step per opening of the picker. | Design §2 "game swatches + free picker"; one step keeps undo usable. |
| P23 | Undo/redo: a snapshot of the whole character before each change, up to 100, Ctrl+Z and Ctrl+Y (Ctrl+Shift+Z too), ignored while typing in a text field. History lasts for one visit to the Single screen. | Design §2 "Undo/redo across all changes including rerolls". |
| P24 | A row reroll picks any item type of that slot by the type weights (the slot chance is ignored: the artist asked for an item). Rerolling the off hand while the main hand is two-handed empties the main hand, and the reverse. | Same rule as P10: the explicit action wins. |

### Tasks

| Task | Files | Tests |
|---|---|---|
| 24 Character copy, slot locks, edit history | `StudioCharacter.cs` (`Clone`, `CopyFrom`, `LockedSlots`, `StudioItem.Clone`), new `EditHistory.cs` | `StudioCharacterCopyTests`, `EditHistoryTests` |
| 25 Row reroll, locked slots, hand rule | `StudioRoller.cs` (`RollSlot`, `RollGear` keeps locked slots, `ResolveHands`) | `StudioRollerSlotTests` |
| 26 Body parts model | new `BodyParts.cs` (editable part groups per species, hair groups, parts an item hides) | `BodyPartsTests` |
| 27 Thumbnails | new `ThumbnailRenderer.cs` | `ThumbnailRendererTests` |
| 28 Color and variant pickers | new `UI/ColorPickerView.cs`, `UI/VariantPickerView.cs` | none (UI) |
| 29 Single screen | new `UI/SingleScreen.cs`, `UI/KeyShortcuts.cs` | none (UI) |
| 30 Edit button on cards | `UI/BulkScreen.cs` | none (UI) |
| 31 Editor smoke run | none | executor play run: Edit opens Single, Body/Gear edits redraw, undo/redo, Back updates the card, export this |

Each task: write tests, compile-check (Refresh, `check_compile_errors`, reflection probe), implement, compile-check, commit. Tests run in Zach's Test Runner at the end.

## Stages B4-B6 (outline; detailed after the previous stage)

B2 (Tasks 14-23) and B3 (Tasks 24-31) are detailed above.


**B4 Presets** (JSON in `presets/` next to the exe; Editor: `Library/CharacterStudio/presets/`)
- Character: species, body choices (part numbers, hide flags, colors as hex), gear per slot (type, number, layer variants, hex colors).
- Cast: the Bulk grid incl. locks and forced items. Random settings: `RandomSettings`.
- Loading missing art: empty slot + a Warnings entry. EditMode round-trip tests, including a preset that names a deleted variant.

**B5 Library panel**
- New item type (slot → next free code suggestion, description, layer codes, hidden layers, second-color layers): appends a CSV row via `CsvTable` (one-line diff), creates `equipment/<type>/` or `weapons/<type>/`, **Open folder**. Same pattern for species (`species.csv`) and part groups (`species_parts.csv`).
- Edit any row in a form; Remove deletes the folder with `.meta` files and the row (git is the undo).
- Rescan: `StudioData.Reload()`, `ImageResolver.ClearCache()`, rebuild dropdowns; optional `ArtImageAudit.Run` (6 s) behind a **Check images** button.
- `.meta` writer for new PNGs: template with the game's sprite import settings and a new GUID (`Guid.NewGuid().ToString("N")`).
- File safety: CSV locked by Excel → "close equipment.csv in Excel", edit kept in memory; CSV changed on disk since the last Rescan → ask before overwriting.

**B6 Release**
- Zip the Windows build; GitHub Release (`gh release create studio-v0.1 ...`) after Zach approves; the `.exe` is never committed (D10).
- Artist README `docs/character-studio-readme.md` after testing: plain words, screenshots of both screens, setup, adding a set, warnings, commit/push to the `art` branch.
