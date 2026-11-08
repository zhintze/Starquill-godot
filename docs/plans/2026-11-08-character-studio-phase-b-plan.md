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
| B2 Bulk screen complete | species multi-select, search + Forced items chips, card species/skin dropdowns, Settings window, checked-only export | outline |
| B3 Single screen | Body and Gear tabs, variant thumbnails, color pickers, undo/redo | outline |
| B4 Presets | Character, Cast, Random settings JSON | outline |
| B5 Library panel | new item type / species / part group, edit row, remove, Rescan, `.meta` writer, Excel lock | outline |
| B6 Release | GitHub Release packaging, artist README | outline |

B2-B6 get their detailed tasks after CHECKPOINT D, written against B1's real code.

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

## Stages B2-B6 (outline; detailed after CHECKPOINT D)

**B2 Bulk screen complete** (`docs/plans/2026-11-08-character-studio-design.md` §2 Bulk)
- Species multi-select (toggle list popup); each Randomize rolls from the checked species (`StudioRoller.RollCharacter` already takes the list).
- Search box: matches `hd01-0001`, a full file name, or a description word (catalog descriptions); results list; a pick becomes a **Forced items** chip applied to every character, replacing that slot, kept by Equip Random, ✕ removes it. Godot's rules: one natural-slot item per prefix, later ones of the same prefix go to Misc 1-4, max 5 per slot type (4 for misc); weapons to main hand / off hand; a two-handed forced weapon clears the off hand, with a warning. `StudioRoller` gains `MakeItem(itemType, itemNum, rng)`.
- Card species dropdown (rerolls body for the new species, keeps unrestricted gear) and skin color dropdown (species skin palette swatches), **Edit** (opens Single, B3).
- Settings window: sliders per slot chance (incl. main/off hand), facial hair/detail chance, weight per item type grouped by slot.
- Checked-cards export (a checkbox per card; Export does checked, or all when none checked); export folder text field.

**B3 Single screen**
- Large preview (~800 px), background toggle checkerboard / white / dark; Body and Gear tabs.
- Body: species, skin color (palette + free picker), hair group/variant/color, eye color, each face part variant picker with None where allowed (uses `ModularImageNums` and the B1 hide flags).
- Gear: per slot (Head, Torso, Arms, Legs, Feet, Main Hand, Off Hand, Misc 1-4): item type dropdown filtered by slot prefix, variant thumbnail grid from `CatalogEntry.Variants` / `WeaponEntry.VariantsPerLayer` (thumbnails rendered through a small `CharacterDisplay` or the item PNG itself), base and second-color pickers (main palette swatches + HSV picker), modular weapon part pickers, row reroll, Lock, ✕; note when an item's `HiddenLayers` hides a body part.
- Undo/redo (Ctrl+Z / Ctrl+Y): snapshot stack of `StudioCharacter` (deep copy), every change and reroll pushes one.

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
