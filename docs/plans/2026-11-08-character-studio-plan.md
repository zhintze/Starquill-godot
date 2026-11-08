# Character Studio Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make CSV tables the only stored copy of the art rules (baked into a Unity asset at build time, variant numbers read from the files on disk), then build the Character Studio app on top of them.

**Architecture:** Pure C# (no `UnityEngine`) CSV reader, file-name parser and converter in `Assets/Scripts/Data/Art/`, unit-tested headless with `dotnet test` and also inside Unity's Test Runner. An Editor baker turns the CSVs plus a scan of `Assets/Resources/Images` into `Assets/Resources/ArtTables.asset` (generated, gitignored). The game's three loaders (`EquipmentCatalog`, `DisplayDataRegistry`, `ColorManager`) read that asset instead of JSON; random picks choose from the variant numbers that exist.

**Tech Stack:** Unity 6 (6000.3.8f1), C# 9, NUnit 3, .NET 10 SDK (`dotnet test` for the pure code), Python 3 + Pillow (one-time migration script).

**Design of record:** `docs/plans/2026-11-08-character-studio-design.md` (decisions D1-D12).

---

## Scope of this document

- **Phase A: Art Tables** (Tasks 1-17) is fully specified below. It changes the game and must land, pass the Test Runner, and be play-tested before Phase B starts.
- **Phase B: Character Studio app** is outlined at the end. Its detailed plan is written after Phase A merges, because it builds on Phase A's real APIs and needs the Windows build module installed.

## Ground rules for the executor

- Branch: `unity-idle-clicker`. Never commit to `main`. Do not push.
- Commit messages: no `Co-Authored-By` lines.
- **Unity tests are never run from a script.** Scripted Test Runner runs have crashed the Editor (see memory `current-status.md`). The pure Art tests run headless with `dotnet test`. Everything else waits for **Zach to run Window > General > Test Runner > EditMode > Run All** at the checkpoints marked **CHECKPOINT**.
- Compile checks: with the Editor open and Coplay connected, use `check_compile_errors`; otherwise ask Zach to open the Editor and read the Console.
- New `.cs` files get their `.meta` from Unity on the next import. Commit the `.meta` files Unity creates (`git status` after the Editor has refreshed).
- File moves under `Assets/` happen with the **Unity Editor closed**, and always move the `.meta` with its file.
- Code in `Assets/Scripts/Data/Art/` and `Assets/Tests/EditMode/Art/` must not reference `UnityEngine` (the headless test project compiles those folders).

## Facts the executor needs

- Image naming today (flat folders under `Assets/Resources/Images/`):
  - equipment `hd01-0001-130.png` = type-number-layer
  - weapons `w01-164-0001.png` = type-layer-number
  - species body parts `0001-016.png` = number-layer
  - species part groups `f01-0001-084.png` = group-number-layer
- 49 equipment types, 9 weapon types, 27 species, 19 part groups (`f01`-`f06`, `h01`-`h04`, `d02 d04 d05 d06`, `g02`, `s01`-`s04`).
- Every multi-layer item and part group has all layers for every variant today, so the "complete variants only" rule drops nothing.
- `0000` part files: identical to `0001` for `f04 h01 h02 h03 h04`; empty for `s04`; unique drawings for `f01 f02 f03 f05 f06 s01 s02 s03`.
- `Artboard 1hd10-0001-094.png` and `Artboard 1hd10-0004-094.png` both differ from their `hd10-*` namesakes. **Leave both untouched** (artist decides); after the move they surface as "misnamed file" warnings.
- `bridge troll` restricts item type `hd14`, which does not exist: expect one warning.
- `System.Random.Next(n)` and `Next(1, n + 1)` consume one sample each and map it the same way, so picking `variants[rng.Next(variants.Length)]` from `{1..n}` gives the same results as the old `rng.Next(1, n + 1)`. Seeded tests keep their outcomes.

---

## Task 1: Headless test project + CSV reader/writer

**Files:**
- Create: `tools/ArtTablesTests/ArtTablesTests.csproj`
- Create: `Assets/Scripts/Data/Art/CsvTable.cs`
- Create: `Assets/Tests/EditMode/Art/CsvTableTests.cs`
- Modify: `.gitignore`
- Create: `.gitattributes`

**Step 1: Create the test project**

`tools/ArtTablesTests/ArtTablesTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Headless tests for the pure-C# art table code. The same test files
       also run inside Unity's Test Runner (EditModeTests assembly). -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../Assets/Scripts/Data/Art/**/*.cs" />
    <Compile Include="../../Assets/Tests/EditMode/Art/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
```

Append to `.gitignore`:
```
# Headless art table tests (dotnet build output)
tools/ArtTablesTests/bin/
tools/ArtTablesTests/obj/

# Generated from Assets/Data/ArtTables/*.csv by the Editor baker
/Assets/Resources/ArtTables.asset
/Assets/Resources/ArtTables.asset.meta
```

Create `.gitattributes`:
```
# Excel on Windows saves CRLF; normalize so one edited row is one changed line.
*.csv text eol=lf
```

**Step 2: Write the failing tests**

`Assets/Tests/EditMode/Art/CsvTableTests.cs`:
```csharp
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class CsvTableTests
    {
        [Test]
        public void Parse_ReadsHeaderAndRows()
        {
            var t = CsvTable.Parse("a,b\n1,2\n3,4\n");
            CollectionAssert.AreEqual(new[] { "a", "b" }, t.Header);
            Assert.AreEqual(2, t.Rows.Count);
            Assert.AreEqual("4", t.Rows[1][1]);
        }

        [Test]
        public void Parse_HandlesCrlfBomAndBlankLines()
        {
            var t = CsvTable.Parse("﻿a,b\r\n1,2\r\n\r\n3,4");
            CollectionAssert.AreEqual(new[] { "a", "b" }, t.Header);
            Assert.AreEqual(2, t.Rows.Count);
            Assert.AreEqual("3", t.Rows[1][0]);
        }

        [Test]
        public void Parse_QuotedFieldsWithCommaQuoteAndNewline()
        {
            var t = CsvTable.Parse("a,b\n\"x, y\",\"say \"\"hi\"\"\"\n\"two\nlines\",z\n");
            Assert.AreEqual("x, y", t.Rows[0][0]);
            Assert.AreEqual("say \"hi\"", t.Rows[0][1]);
            Assert.AreEqual("two\nlines", t.Rows[1][0]);
        }

        [Test]
        public void Parse_PadsShortRowsAndRecordsLineNumbers()
        {
            var t = CsvTable.Parse("a,b,c\n1\n\n2,3,4\n");
            CollectionAssert.AreEqual(new[] { "1", "", "" }, t.Rows[0]);
            Assert.AreEqual(2, t.RowLines[0]);
            Assert.AreEqual(4, t.RowLines[1]);
        }

        [Test]
        public void Column_FindsHeaderIndexOrMinusOne()
        {
            var t = CsvTable.Parse("item_type,description\n");
            Assert.AreEqual(1, t.Column("description"));
            Assert.AreEqual(-1, t.Column("missing"));
        }

        [Test]
        public void ToCsv_RoundTripsAndQuotesOnlyWhenNeeded()
        {
            string text = "a,b\nplain,\"x, y\"\n";
            Assert.AreEqual(text, CsvTable.Parse(text).ToCsv());
        }

        [Test]
        public void ToCsv_EditOneCell_ChangesOneLine()
        {
            var t = CsvTable.Parse("a,b\n1,2\n3,4\n5,6\n");
            t.Rows[1][1] = "40";
            Assert.AreEqual("a,b\n1,2\n3,40\n5,6\n", t.ToCsv());
        }
    }
}
```

**Step 3: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests`
Expected: build error, `CsvTable` not found. (The first run downloads NUnit packages; it needs network.)

**Step 4: Implement**

`Assets/Scripts/Data/Art/CsvTable.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace Starquill.Data.Art
{
    /// Comma-separated table: fields holding a comma, quote or newline are
    /// double-quoted with quotes doubled. Reads CRLF or LF and a UTF-8 BOM
    /// (Excel on Windows writes both). Writes LF and quotes only when needed,
    /// so editing one row changes one line in git.
    /// No UnityEngine here: tools/ArtTablesTests compiles this file headless.
    public sealed class CsvTable
    {
        private static readonly char[] NeedsQuotes = { ',', '"', '\n', '\r' };

        public string[] Header { get; }
        public List<string[]> Rows { get; } = new List<string[]>();
        /// 1-based line where each row starts, for messages ("line 12").
        public List<int> RowLines { get; } = new List<int>();

        public CsvTable(string[] header) { Header = header; }

        public int Column(string name) => Array.IndexOf(Header, name);

        public static CsvTable Parse(string text)
        {
            var lines = new List<int>();
            var records = ParseRecords(text ?? "", lines);
            if (records.Count == 0) return new CsvTable(Array.Empty<string>());

            var header = records[0];
            for (int i = 0; i < header.Length; i++) header[i] = header[i].Trim();
            var table = new CsvTable(header);

            for (int r = 1; r < records.Count; r++)
            {
                var rec = records[r];
                if (rec.Length == 1 && rec[0].Trim().Length == 0) continue; // blank line
                var row = new string[header.Length];
                for (int c = 0; c < header.Length; c++) row[c] = c < rec.Length ? rec[c] : "";
                table.Rows.Add(row);
                table.RowLines.Add(lines[r]);
            }
            return table;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            AppendRecord(sb, Header);
            foreach (var row in Rows) AppendRecord(sb, row);
            return sb.ToString();
        }

        private static void AppendRecord(StringBuilder sb, string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string f = fields[i] ?? "";
                if (f.IndexOfAny(NeedsQuotes) >= 0)
                    sb.Append('"').Append(f.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(f);
            }
            sb.Append('\n');
        }

        private static List<string[]> ParseRecords(string text, List<int> recordLines)
        {
            var records = new List<string[]>();
            var fields = new List<string>();
            var sb = new StringBuilder();
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            int line = 1, recordLine = 1;
            bool inQuotes = false, any = false;

            while (i < text.Length)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        inQuotes = false; i++; continue;
                    }
                    if (c == '\n') line++;
                    if (c != '\r') sb.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; any = true; break;
                    case ',': fields.Add(sb.ToString()); sb.Clear(); any = true; break;
                    case '\r': break;
                    case '\n':
                        fields.Add(sb.ToString()); sb.Clear();
                        records.Add(fields.ToArray()); recordLines.Add(recordLine);
                        fields.Clear(); any = false;
                        line++; recordLine = line;
                        break;
                    default: sb.Append(c); any = true; break;
                }
                i++;
            }

            if (any || sb.Length > 0 || fields.Count > 0)
            {
                fields.Add(sb.ToString());
                records.Add(fields.ToArray());
                recordLines.Add(recordLine);
            }
            return records;
        }
    }
}
```

**Step 5: Run to verify it passes**

Run: `dotnet test tools/ArtTablesTests`
Expected: `Passed!  - Failed: 0, Passed: 7`

**Step 6: Commit**

```bash
git add tools/ArtTablesTests/ArtTablesTests.csproj Assets/Scripts/Data/Art/CsvTable.cs Assets/Tests/EditMode/Art/CsvTableTests.cs .gitignore .gitattributes
git commit -m "feat: CSV reader/writer for art tables with headless test project"
```
(Commit the `Art` folder and file `.meta` files once Unity has created them; they can join a later commit.)

---

## Task 2: Art file name parser

**Files:**
- Create: `Assets/Scripts/Data/Art/ArtFileName.cs`
- Create: `Assets/Tests/EditMode/Art/ArtFileNameTests.cs`

Paths are relative to `Assets/Resources/Images`, `/`-separated, in the **new** per-folder layout:
`equipment/hd01/hd01-0001-130.png`, `weapons/w01/w01-164-0001.png`, `species/body/0001-016.png`, `species/f01/f01-0001-084.png`.

**Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtFileNameTests
    {
        [Test]
        public void Equipment_ParsesTypeNumberLayer()
        {
            Assert.IsTrue(ArtFileName.TryParse("equipment/hd01/hd01-0012-130.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual(ArtFileKind.Equipment, f.Kind);
            Assert.AreEqual("hd01", f.Type);
            Assert.AreEqual(12, f.Number);
            Assert.AreEqual(130, f.Layer);
        }

        [Test]
        public void Weapon_ParsesTypeLayerNumber()
        {
            Assert.IsTrue(ArtFileName.TryParse("weapons/w01/w01-164-0007.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual(ArtFileKind.Weapon, f.Kind);
            Assert.AreEqual(164, f.Layer);
            Assert.AreEqual(7, f.Number);
        }

        [Test]
        public void SpeciesBodyAndPart_Parse()
        {
            Assert.IsTrue(ArtFileName.TryParse("species/body/0001-016.png", out var body, out var p1));
            Assert.IsNull(p1);
            Assert.AreEqual(ArtFileKind.SpeciesBody, body.Kind);
            Assert.AreEqual(1, body.Number);
            Assert.AreEqual(16, body.Layer);

            Assert.IsTrue(ArtFileName.TryParse("species/h04/h04-0020-154.png", out var part, out var p2));
            Assert.IsNull(p2);
            Assert.AreEqual(ArtFileKind.SpeciesPart, part.Kind);
            Assert.AreEqual("h04", part.Type);
        }

        [Test]
        public void BackslashesAreAccepted()
        {
            Assert.IsTrue(ArtFileName.TryParse(@"equipment\hd01\hd01-0001-130.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual("hd01", f.Type);
        }

        [Test]
        public void NonPng_IsNotArt()
        {
            Assert.IsFalse(ArtFileName.TryParse("equipment/hd01/Thumbs.db", out _, out _));
        }

        [TestCase("equipment/hd10/Artboard 1hd10-0004-094.png")]
        [TestCase("equipment/hd01/hd01-12-130.png")]
        [TestCase("equipment/hd01-0001-130.png")]
        [TestCase("equipment/hd02/hd01-0001-130.png")]
        [TestCase("weapons/w01/w01-0001-164.png")]
        [TestCase("species/f01/f02-0001-084.png")]
        [TestCase("props/x01/x01-0001-001.png")]
        public void Misnamed_ReportsProblem(string path)
        {
            Assert.IsTrue(ArtFileName.TryParse(path, out _, out var problem));
            Assert.IsNotNull(problem);
        }

        [Test]
        public void ExpectedPath_BuildsNamesForEachKind()
        {
            Assert.AreEqual("equipment/hd08/hd08-0012-162.png", ArtFileName.ExpectedPath(ArtFileKind.Equipment, "hd08", 12, 162));
            Assert.AreEqual("weapons/w01/w01-164-0003.png", ArtFileName.ExpectedPath(ArtFileKind.Weapon, "w01", 3, 164));
            Assert.AreEqual("species/h04/h04-0002-128.png", ArtFileName.ExpectedPath(ArtFileKind.SpeciesPart, "h04", 2, 128));
        }
    }
}
```

**Step 2: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests`
Expected: build error, `ArtFileName` not found.

**Step 3: Implement**

`Assets/Scripts/Data/Art/ArtFileName.cs`:
```csharp
using System;
using System.Text.RegularExpressions;

namespace Starquill.Data.Art
{
    public enum ArtFileKind { Equipment, Weapon, SpeciesBody, SpeciesPart }

    public readonly struct ArtFile
    {
        public readonly ArtFileKind Kind;
        public readonly string Type;   // item type or part group; "body" for body parts
        public readonly int Number;
        public readonly int Layer;
        public readonly string Path;

        public ArtFile(ArtFileKind kind, string type, int number, int layer, string path)
        {
            Kind = kind; Type = type; Number = number; Layer = layer; Path = path;
        }
    }

    /// Names and folders of art PNGs, relative to Assets/Resources/Images:
    ///   equipment/hd01/hd01-0001-130.png   (type-number-layer)
    ///   weapons/w01/w01-164-0001.png       (type-layer-number)
    ///   species/body/0001-016.png          (number-layer)
    ///   species/f01/f01-0001-084.png       (group-number-layer)
    public static class ArtFileName
    {
        private static readonly Regex Equip = new Regex(@"^([a-z]{2}\d{2})-(\d{4})-(\d{3})\.png$");
        private static readonly Regex Weapon = new Regex(@"^(w\d{2})-(\d{1,3})-(\d{4})\.png$");
        private static readonly Regex Body = new Regex(@"^(\d{4})-(\d{3})\.png$");
        private static readonly Regex Part = new Regex(@"^([a-z]\d{2})-(\d{4})-(\d{3})\.png$");

        /// False: not an art file (not a PNG), ignore it.
        /// True with problem == null: a valid art file.
        /// True with problem != null: a PNG that breaks the naming rules.
        public static bool TryParse(string relativePath, out ArtFile file, out string problem)
        {
            file = default;
            problem = null;
            string p = relativePath.Replace('\\', '/');
            if (!p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;

            var parts = p.Split('/');
            if (parts.Length != 3)
            {
                problem = "must sit in one folder per type, e.g. equipment/hd01/hd01-0001-130.png";
                return true;
            }
            string root = parts[0], folder = parts[1], name = parts[2];
            Match m;

            switch (root)
            {
                case "equipment":
                    m = Equip.Match(name);
                    if (!m.Success) { problem = "name must look like hd01-0001-130.png (type-number-layer)"; return true; }
                    if (m.Groups[1].Value != folder) { problem = $"belongs in folder equipment/{m.Groups[1].Value}"; return true; }
                    file = new ArtFile(ArtFileKind.Equipment, folder, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), p);
                    return true;

                case "weapons":
                    m = Weapon.Match(name);
                    if (!m.Success) { problem = "name must look like w01-164-0001.png (type-layer-number)"; return true; }
                    if (m.Groups[1].Value != folder) { problem = $"belongs in folder weapons/{m.Groups[1].Value}"; return true; }
                    file = new ArtFile(ArtFileKind.Weapon, folder, int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), p);
                    return true;

                case "species" when folder == "body":
                    m = Body.Match(name);
                    if (!m.Success) { problem = "name must look like 0001-016.png (number-layer)"; return true; }
                    file = new ArtFile(ArtFileKind.SpeciesBody, "body", int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), p);
                    return true;

                case "species":
                    m = Part.Match(name);
                    if (!m.Success) { problem = "name must look like f01-0001-084.png (group-number-layer)"; return true; }
                    if (m.Groups[1].Value != folder) { problem = $"belongs in folder species/{m.Groups[1].Value}"; return true; }
                    file = new ArtFile(ArtFileKind.SpeciesPart, folder, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), p);
                    return true;

                default:
                    problem = "top folder must be equipment, weapons or species";
                    return true;
            }
        }

        public static string ExpectedPath(ArtFileKind kind, string type, int number, int layer)
        {
            switch (kind)
            {
                case ArtFileKind.Equipment: return $"equipment/{type}/{type}-{number:D4}-{layer:D3}.png";
                case ArtFileKind.Weapon: return $"weapons/{type}/{type}-{layer}-{number:D4}.png";
                case ArtFileKind.SpeciesBody: return $"species/body/{number:D4}-{layer:D3}.png";
                default: return $"species/{type}/{type}-{number:D4}-{layer:D3}.png";
            }
        }
    }
}
```

**Step 4: Run to verify it passes**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass (7 + 13).

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtFileName.cs Assets/Tests/EditMode/Art/ArtFileNameTests.cs
git commit -m "feat: art file name parser for the per-type folder layout"
```

---

## Task 3: Art table data model, issues, cell parsing

**Files:**
- Create: `Assets/Scripts/Data/Art/ArtTableData.cs`
- Create: `Assets/Scripts/Data/Art/ArtCells.cs`
- Create: `Assets/Tests/EditMode/Art/ArtCellsTests.cs`

**Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtCellsTests
    {
        [TestCase("", new int[0])]
        [TestCase("130", new[] { 130 })]
        [TestCase("10 134  130", new[] { 10, 134, 130 })]
        [TestCase("10, 134;130", new[] { 10, 134, 130 })]
        public void TryInts_AcceptsSpaceCommaSemicolon(string cell, int[] expected)
        {
            Assert.IsTrue(ArtCells.TryInts(cell, out var values));
            CollectionAssert.AreEqual(expected, values);
        }

        [Test]
        public void TryInts_RejectsWords()
        {
            Assert.IsFalse(ArtCells.TryInts("10 abc", out _));
        }

        [Test]
        public void Strings_SplitsAndDropsEmpties()
        {
            CollectionAssert.AreEqual(new[] { "h01", "h02" }, ArtCells.Strings(" h01  h02 "));
            Assert.AreEqual(0, ArtCells.Strings("").Length);
        }

        [TestCase("y", true)] [TestCase("Yes", true)] [TestCase("TRUE", true)] [TestCase("1", true)]
        [TestCase("n", false)] [TestCase("no", false)] [TestCase("", false)] [TestCase("0", false)]
        public void TryBool_ReadsCommonSpellings(string cell, bool expected)
        {
            Assert.IsTrue(ArtCells.TryBool(cell, out var value));
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void TryBool_RejectsOther()
        {
            Assert.IsFalse(ArtCells.TryBool("maybe", out _));
        }

        [TestCase("#a1b2c3", "A1B2C3")]
        [TestCase("040400", "040400")]
        public void TryHex_NormalizesToUpperWithoutHash(string cell, string expected)
        {
            Assert.IsTrue(ArtCells.TryHex(cell, out var hex));
            Assert.AreEqual(expected, hex);
        }

        [TestCase("40400")] [TestCase("#GGGGGG")] [TestCase("")]
        public void TryHex_RejectsBadValues(string cell)
        {
            Assert.IsFalse(ArtCells.TryHex(cell, out _));
        }

        [Test]
        public void TryFloat_BlankUsesDefault_InvariantCulture()
        {
            Assert.IsTrue(ArtCells.TryFloat("", 0.35f, out var a));
            Assert.AreEqual(0.35f, a, 1e-6f);
            Assert.IsTrue(ArtCells.TryFloat("1.1", 1f, out var b));
            Assert.AreEqual(1.1f, b, 1e-6f);
            Assert.IsFalse(ArtCells.TryFloat("1,1", 1f, out _));
        }
    }
}
```

**Step 2: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests`
Expected: build error, `ArtCells` not found.

**Step 3: Implement**

`Assets/Scripts/Data/Art/ArtTableData.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Starquill.Data.Art
{
    /// Everything the game needs from the art tables, after the bake.
    /// Variant numbers are the ones that exist on disk (gaps allowed);
    /// nothing here is a hand-typed count. Unity serializes these into
    /// Assets/Resources/ArtTables.asset.
    [Serializable]
    public class ArtTableData
    {
        public List<EquipmentRow> equipment = new List<EquipmentRow>();
        public List<WeaponRow> weapons = new List<WeaponRow>();
        public List<SpeciesRow> species = new List<SpeciesRow>();
        public List<PartGroupRow> partGroups = new List<PartGroupRow>();
        public List<SkinVarianceRow> skinVariance = new List<SkinVarianceRow>();
        public List<PaletteRow> palettes = new List<PaletteRow>();
    }

    [Serializable]
    public class EquipmentRow
    {
        public string itemType;
        public string description;
        public int[] layerCodes = Array.Empty<int>();
        public int[] hiddenLayers = Array.Empty<int>();
        public int[] colorVarianceLayers = Array.Empty<int>();
        public int[] variants = Array.Empty<int>();
    }

    /// Unity cannot serialize int[][]; one of these per weapon layer.
    [Serializable]
    public class IntList
    {
        public int[] values = Array.Empty<int>();
        public IntList() { }
        public IntList(int[] values) { this.values = values; }
    }

    [Serializable]
    public class WeaponRow
    {
        public string itemType;
        public string description;
        public string handType;
        public bool modular;
        public int[] layerCodes = Array.Empty<int>();
        public int[] hiddenLayers = Array.Empty<int>();
        public int[] colorVarianceLayers = Array.Empty<int>();
        /// Parallel to layerCodes. Modular weapons pick each layer on its own;
        /// non-modular ones share one number, so every entry is the same list.
        public IntList[] variantsPerLayer = Array.Empty<IntList>();
    }

    [Serializable]
    public class SpeciesRow
    {
        public string name;
        public float xScale = 1f, yScale = 1f, headYOffset = 0.35f, headZoom = 0.5f;
        public string backArm, legs, body, head, frontArm;
        public string eyes, nose, mouth, ears, facialHair, facialDetail;
        public string[] hairGroups = Array.Empty<string>();
        public string[] otherBodyParts = Array.Empty<string>();
        public string[] itemRestrictions = Array.Empty<string>();
        public string skinPalette, hairPalette, eyePalette, facialDetailPalette; // blank = main
    }

    [Serializable]
    public class PartGroupRow
    {
        public string group;
        public string description;
        public int[] layers = Array.Empty<int>();
        public int[] variants = Array.Empty<int>();
    }

    [Serializable]
    public class SkinVarianceRow
    {
        public string species;
        public int[] layers = Array.Empty<int>();
        public string palette;
    }

    [Serializable]
    public class PaletteRow
    {
        public string name;
        public string[] hex = Array.Empty<string>(); // "RRGGBB", upper case, no '#'
    }

    public enum ArtIssueLevel { Warning, Error }

    [Serializable]
    public class ArtIssue
    {
        public ArtIssueLevel level;
        public string where;   // "equipment.csv line 12" or an image path
        public string message;

        public ArtIssue(ArtIssueLevel level, string where, string message)
        {
            this.level = level; this.where = where; this.message = message;
        }

        public override string ToString() => $"{level}: {where}: {message}";
    }

    /// Raw inputs: the six CSV texts (null = file not found) and every
    /// file path under Assets/Resources/Images, relative and '/'-separated.
    public sealed class ArtSources
    {
        public string EquipmentCsv, WeaponsCsv, SpeciesCsv, SpeciesPartsCsv, SkinVarianceCsv, PalettesCsv;
        public List<string> ImageFiles = new List<string>();
    }

    public sealed class ArtBakeResult
    {
        public ArtTableData Data = new ArtTableData();
        public List<ArtIssue> Issues = new List<ArtIssue>();
        public bool HasErrors => Issues.Exists(i => i.level == ArtIssueLevel.Error);
    }
}
```

`Assets/Scripts/Data/Art/ArtCells.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starquill.Data.Art
{
    /// Cell formats used by the art tables. Lists are space separated;
    /// commas and semicolons are also accepted because spreadsheets tempt them.
    public static class ArtCells
    {
        private static readonly char[] ListSeparators = { ' ', ',', ';', '\t' };

        public static string[] Strings(string cell) =>
            (cell ?? "").Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);

        public static bool TryInts(string cell, out int[] values)
        {
            var parts = Strings(cell);
            var list = new List<int>(parts.Length);
            foreach (var p in parts)
            {
                if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
                {
                    values = Array.Empty<int>();
                    return false;
                }
                list.Add(v);
            }
            values = list.ToArray();
            return true;
        }

        public static bool TryBool(string cell, out bool value)
        {
            switch ((cell ?? "").Trim().ToLowerInvariant())
            {
                case "y": case "yes": case "true": case "1": value = true; return true;
                case "": case "n": case "no": case "false": case "0": value = false; return true;
                default: value = false; return false;
            }
        }

        public static bool TryHex(string cell, out string hex)
        {
            hex = (cell ?? "").Trim().TrimStart('#').ToUpperInvariant();
            if (hex.Length != 6) return false;
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        public static bool TryFloat(string cell, float fallback, out float value)
        {
            string s = (cell ?? "").Trim();
            if (s.Length == 0) { value = fallback; return true; }
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && s.IndexOf(',') < 0;
        }
    }
}
```

**Step 4: Run to verify it passes**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtTableData.cs Assets/Scripts/Data/Art/ArtCells.cs Assets/Tests/EditMode/Art/ArtCellsTests.cs
git commit -m "feat: art table data model and cell parsing"
```

---

## Task 4: Converter, part 1: file index, palettes, equipment

**Files:**
- Create: `Assets/Scripts/Data/Art/ArtTablesConverter.cs`
- Create: `Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtTablesConverterTests
    {
        private const string EquipHeader = "item_type,description,layer_codes,hidden_layers,color_variance_layers\n";

        private static ArtSources Sources(params string[] files)
        {
            return new ArtSources
            {
                EquipmentCsv = EquipHeader,
                WeaponsCsv = "item_type,description,hand_type,modular,layer_codes,hidden_layers,color_variance_layers\n",
                SpeciesCsv = "name,x_scale,y_scale,head_y_offset,head_zoom,back_arm,legs,body,head,front_arm,eyes,nose,mouth,ears,facial_hair,facial_detail,hair_groups,other_body_parts,skin_palette,hair_palette,eye_palette,facial_detail_palette,item_restrictions\n",
                SpeciesPartsCsv = "group,description,layers\n",
                SkinVarianceCsv = "species,layers,palette\n",
                PalettesCsv = "palette,hex\nmain,#FFFFFF\n",
                ImageFiles = files.ToList()
            };
        }

        private static string[] Messages(ArtBakeResult r, ArtIssueLevel level) =>
            r.Issues.Where(i => i.level == level).Select(i => i.where + ": " + i.message).ToArray();

        [Test]
        public void Equipment_VariantsComeFromFiles_GapsAllowed()
        {
            var src = Sources(
                "equipment/tr08/tr08-0102-014.png", "equipment/tr08/tr08-0102-150.png",
                "equipment/tr08/tr08-0104-014.png", "equipment/tr08/tr08-0104-150.png");
            src.EquipmentCsv += "tr08,coat,14 150,,\n";

            var r = ArtTablesConverter.Convert(src);

            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
            CollectionAssert.AreEqual(new[] { 102, 104 }, r.Data.equipment.Single().variants);
        }

        [Test]
        public void Equipment_IncompleteVariant_IsSkippedWithWarningNamingTheMissingFile()
        {
            var src = Sources(
                "equipment/hd08/hd08-0001-160.png", "equipment/hd08/hd08-0001-162.png",
                "equipment/hd08/hd08-0002-160.png");
            src.EquipmentCsv += "hd08,helmet with detail,160 162,,160\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("equipment/hd08/hd08-0002-162.png")));
        }

        [Test]
        public void Equipment_ParsesListsAndDescription()
        {
            var src = Sources("equipment/hd08/hd08-0001-160.png");
            src.EquipmentCsv += "hd08,\"helmet, detailed\",160,10 134,160\n";

            var row = ArtTablesConverter.Convert(src).Data.equipment.Single();

            Assert.AreEqual("helmet, detailed", row.description);
            CollectionAssert.AreEqual(new[] { 160 }, row.layerCodes);
            CollectionAssert.AreEqual(new[] { 10, 134 }, row.hiddenLayers);
            CollectionAssert.AreEqual(new[] { 160 }, row.colorVarianceLayers);
        }

        [TestCase("HD01,hat,130,,", "item_type")]
        [TestCase("hd01,hat,,,", "layer_codes")]
        [TestCase("hd01,hat,130 abc,,", "layer_codes")]
        [TestCase("hd01,hat,130,,131", "color_variance_layers")]
        public void Equipment_BadCells_AreErrorsWithLineNumber(string row, string column)
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png");
            src.EquipmentCsv += row + "\n";

            var r = ArtTablesConverter.Convert(src);

            var errors = Messages(r, ArtIssueLevel.Error);
            Assert.AreEqual(1, errors.Length);
            StringAssert.StartsWith("equipment.csv line 2", errors[0]);
            StringAssert.Contains(column, errors[0]);
            Assert.AreEqual(0, r.Data.equipment.Count);
        }

        [Test]
        public void Equipment_DuplicateType_IsError()
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png");
            src.EquipmentCsv += "hd01,hat,130,,\nhd01,hat again,130,,\n";

            var r = ArtTablesConverter.Convert(src);

            Assert.AreEqual(1, r.Data.equipment.Count);
            StringAssert.Contains("twice", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MissingColumn_IsOneErrorForTheFile()
        {
            var src = Sources();
            src.EquipmentCsv = "item_type,description\nhd01,hat\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.Contains("layer_codes", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MissingFile_IsError()
        {
            var src = Sources();
            src.EquipmentCsv = null;

            var r = ArtTablesConverter.Convert(src);

            StringAssert.StartsWith("equipment.csv", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MisnamedFile_IsWarningAndNeverUsed()
        {
            var src = Sources("equipment/hd10/hd10-0001-094.png", "equipment/hd10/Artboard 1hd10-0004-094.png");
            src.EquipmentCsv += "hd10,headband,94,,\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("equipment/hd10/Artboard 1hd10-0004-094.png")));
        }

        [Test]
        public void FolderWithoutRow_AndRowWithoutImages_AreWarnings()
        {
            var src = Sources("equipment/hd15/hd15-0001-130.png");
            src.EquipmentCsv += "hd14,crown,130,,\n";

            var r = ArtTablesConverter.Convert(src);
            var warnings = Messages(r, ArtIssueLevel.Warning);

            Assert.IsTrue(warnings.Any(m => m.StartsWith("equipment/hd15") && m.Contains("no row")));
            Assert.IsTrue(warnings.Any(m => m.StartsWith("equipment/hd14") && m.Contains("no images")));
            Assert.IsFalse(r.HasErrors);
        }

        [Test]
        public void ImageLayerNotInLayerCodes_IsWarning()
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png", "equipment/hd01/hd01-0001-131.png");
            src.EquipmentCsv += "hd01,hat,130,,\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.Contains("131")));
        }

        [Test]
        public void Palettes_GroupRowsInOrderAndNormalizeHex()
        {
            var src = Sources();
            src.PalettesCsv = "palette,hex\nmain,#ff0000\nhuman,3A1914\nmain,00ff00\n";

            var p = ArtTablesConverter.Convert(src).Data.palettes;

            Assert.AreEqual(2, p.Count);
            Assert.AreEqual("main", p[0].name);
            CollectionAssert.AreEqual(new[] { "FF0000", "00FF00" }, p[0].hex);
            CollectionAssert.AreEqual(new[] { "3A1914" }, p[1].hex);
        }

        [Test]
        public void Palettes_BadHex_IsError()
        {
            var src = Sources();
            src.PalettesCsv = "palette,hex\nmain,40400\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.StartsWith("palettes.csv line 2", Messages(r, ArtIssueLevel.Error).Single());
        }
    }
}
```

**Step 2: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests`
Expected: build error, `ArtTablesConverter` not found.

**Step 3: Implement**

`Assets/Scripts/Data/Art/ArtTablesConverter.cs` (weapons, parts, species and skin variance are added in Tasks 5-6; leave their methods out for now):
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Starquill.Data.Art
{
    /// CSV tables + a file listing -> ArtTableData + issues.
    /// Errors are bad table cells (the game build fails on them; the row is
    /// skipped). Warnings are art-file problems (the variant or file is
    /// skipped, everything else keeps working).
    public static class ArtTablesConverter
    {
        public const string EquipmentFile = "equipment.csv";
        public const string WeaponsFile = "weapons.csv";
        public const string SpeciesFile = "species.csv";
        public const string SpeciesPartsFile = "species_parts.csv";
        public const string SkinVarianceFile = "species_skin_variance.csv";
        public const string PalettesFile = "palettes.csv";

        private static readonly Regex ItemTypeRx = new Regex(@"^[a-z]{2}\d{2}$");

        public static ArtBakeResult Convert(ArtSources src)
        {
            var r = new ArtBakeResult();
            var files = FileIndex.Build(src.ImageFiles, r.Issues);

            ConvertPalettes(Open(src.PalettesCsv, PalettesFile, r), r);
            ConvertEquipment(Open(src.EquipmentCsv, EquipmentFile, r), files, r);

            files.ReportUnusedFolders(r.Issues);
            return r;
        }

        // ---------- tables ----------

        private static void ConvertPalettes(Table t, ArtBakeResult r)
        {
            if (t == null || !t.Require("palette", "hex")) return;
            var order = new List<string>();
            var byName = new Dictionary<string, List<string>>();
            for (int i = 0; i < t.Count; i++)
            {
                string name = t.Text(i, "palette");
                if (name.Length == 0) { t.Error(i, "palette name is blank"); continue; }
                if (!ArtCells.TryHex(t.Text(i, "hex"), out string hex)) { t.Error(i, $"hex '{t.Text(i, "hex")}' must look like #A1B2C3"); continue; }
                if (!byName.TryGetValue(name, out var list)) { list = new List<string>(); byName[name] = list; order.Add(name); }
                list.Add(hex);
            }
            foreach (var name in order)
                r.Data.palettes.Add(new PaletteRow { name = name, hex = byName[name].ToArray() });
        }

        private static void ConvertEquipment(Table t, FileIndex files, ArtBakeResult r)
        {
            if (t == null || !t.Require("item_type", "description", "layer_codes", "hidden_layers", "color_variance_layers")) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < t.Count; i++)
            {
                string type = t.Text(i, "item_type");
                if (!ItemTypeRx.IsMatch(type)) { t.Error(i, $"item_type '{type}' must look like hd01"); continue; }
                if (!seen.Add(type)) { t.Error(i, $"item_type {type} appears twice"); continue; }
                if (!t.Ints(i, "layer_codes", out var layers, required: true)) continue;
                if (!t.Ints(i, "hidden_layers", out var hidden)) continue;
                if (!t.Ints(i, "color_variance_layers", out var variance)) continue;
                if (!variance.All(layers.Contains)) { t.Error(i, "color_variance_layers must also be listed in layer_codes"); continue; }

                r.Data.equipment.Add(new EquipmentRow
                {
                    itemType = type,
                    description = t.Text(i, "description"),
                    layerCodes = layers,
                    hiddenLayers = hidden,
                    colorVarianceLayers = variance,
                    variants = files.CompleteVariants(ArtFileKind.Equipment, type, layers, r.Issues)
                });
            }
        }

        // ---------- helpers ----------

        private static Table Open(string csv, string fileName, ArtBakeResult r)
        {
            if (csv == null)
            {
                r.Issues.Add(new ArtIssue(ArtIssueLevel.Error, fileName, "file not found"));
                return null;
            }
            return new Table(CsvTable.Parse(csv), fileName, r.Issues);
        }

        /// Row access with error reporting by file and line.
        private sealed class Table
        {
            private readonly CsvTable csv;
            private readonly string file;
            private readonly List<ArtIssue> issues;

            public Table(CsvTable csv, string file, List<ArtIssue> issues)
            {
                this.csv = csv; this.file = file; this.issues = issues;
            }

            public int Count => csv.Rows.Count;

            public bool Require(params string[] columns)
            {
                var missing = columns.Where(c => csv.Column(c) < 0).ToArray();
                if (missing.Length == 0) return true;
                issues.Add(new ArtIssue(ArtIssueLevel.Error, file, "missing column(s): " + string.Join(", ", missing)));
                return false;
            }

            public string Text(int row, string column)
            {
                int c = csv.Column(column);
                return c < 0 ? "" : (csv.Rows[row][c] ?? "").Trim();
            }

            public bool Ints(int row, string column, out int[] values, bool required = false)
            {
                if (!ArtCells.TryInts(Text(row, column), out values))
                {
                    Error(row, $"{column} '{Text(row, column)}' must be numbers separated by spaces");
                    return false;
                }
                if (required && values.Length == 0)
                {
                    Error(row, $"{column} is blank");
                    return false;
                }
                return true;
            }

            public void Error(int row, string message) =>
                issues.Add(new ArtIssue(ArtIssueLevel.Error, $"{file} line {csv.RowLines[row]}", message));

            public void Warning(int row, string message) =>
                issues.Add(new ArtIssue(ArtIssueLevel.Warning, $"{file} line {csv.RowLines[row]}", message));
        }

        /// Which variant numbers exist on disk, per kind/type/layer.
        private sealed class FileIndex
        {
            private readonly Dictionary<(ArtFileKind, string), Dictionary<int, SortedSet<int>>> byType =
                new Dictionary<(ArtFileKind, string), Dictionary<int, SortedSet<int>>>();
            private readonly HashSet<(ArtFileKind, string)> used = new HashSet<(ArtFileKind, string)>();
            public readonly HashSet<string> BodyTokens = new HashSet<string>(); // "0001-016"

            public static FileIndex Build(IEnumerable<string> paths, List<ArtIssue> issues)
            {
                var index = new FileIndex();
                foreach (var path in paths ?? Enumerable.Empty<string>())
                {
                    if (!ArtFileName.TryParse(path, out var f, out var problem)) continue;
                    if (problem != null)
                    {
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, path.Replace('\\', '/'), "misnamed file, never used: " + problem));
                        continue;
                    }
                    if (f.Kind == ArtFileKind.SpeciesBody)
                    {
                        index.BodyTokens.Add($"{f.Number:D4}-{f.Layer:D3}");
                        continue;
                    }
                    var key = (f.Kind, f.Type);
                    if (!index.byType.TryGetValue(key, out var layers)) index.byType[key] = layers = new Dictionary<int, SortedSet<int>>();
                    if (!layers.TryGetValue(f.Layer, out var nums)) layers[f.Layer] = nums = new SortedSet<int>();
                    nums.Add(f.Number);
                }
                return index;
            }

            public bool HasPart(string group, int number, int layer) =>
                byType.TryGetValue((ArtFileKind.SpeciesPart, group), out var layers)
                && layers.TryGetValue(layer, out var nums) && nums.Contains(number);

            /// Numbers that have a file for every layer. Incomplete numbers and
            /// stray layers are reported, not used.
            public int[] CompleteVariants(ArtFileKind kind, string type, int[] layerCodes, List<ArtIssue> issues)
            {
                var layers = Layers(kind, type, layerCodes, issues);
                if (layers == null) return Array.Empty<int>();

                var all = new SortedSet<int>();
                foreach (int code in layerCodes)
                    if (layers.TryGetValue(code, out var nums)) all.UnionWith(nums);

                var complete = new List<int>();
                foreach (int n in all)
                {
                    bool ok = true;
                    foreach (int code in layerCodes)
                    {
                        if (layers.TryGetValue(code, out var nums) && nums.Contains(n)) continue;
                        ok = false;
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, ArtFileName.ExpectedPath(kind, type, n, code),
                            $"missing: variant {n:D4} has other layers but not this one, so it is never used"));
                    }
                    if (ok) complete.Add(n);
                }
                return complete.ToArray();
            }

            /// Numbers per layer, each layer on its own (modular weapons).
            public int[][] VariantsPerLayer(ArtFileKind kind, string type, int[] layerCodes, List<ArtIssue> issues)
            {
                var layers = Layers(kind, type, layerCodes, issues);
                var result = new int[layerCodes.Length][];
                for (int i = 0; i < layerCodes.Length; i++)
                {
                    if (layers != null && layers.TryGetValue(layerCodes[i], out var nums))
                        result[i] = nums.ToArray();
                    else
                    {
                        result[i] = Array.Empty<int>();
                        if (layers != null)
                            issues.Add(new ArtIssue(ArtIssueLevel.Warning, Folder(kind, type), $"no images for layer {layerCodes[i]}"));
                    }
                }
                return result;
            }

            private Dictionary<int, SortedSet<int>> Layers(ArtFileKind kind, string type, int[] layerCodes, List<ArtIssue> issues)
            {
                used.Add((kind, type));
                if (!byType.TryGetValue((kind, type), out var layers))
                {
                    issues.Add(new ArtIssue(ArtIssueLevel.Warning, Folder(kind, type), "no images found for this row"));
                    return null;
                }
                foreach (int layer in layers.Keys)
                    if (Array.IndexOf(layerCodes, layer) < 0)
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, Folder(kind, type),
                            $"images for layer {layer} exist but the layer is not listed in the table, so they are never used"));
                return layers;
            }

            public void ReportUnusedFolders(List<ArtIssue> issues)
            {
                foreach (var key in byType.Keys)
                    if (!used.Contains(key))
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, Folder(key.Item1, key.Item2),
                            "folder has images but no row in its table, so they are never used"));
            }

            private static string Folder(ArtFileKind kind, string type)
            {
                switch (kind)
                {
                    case ArtFileKind.Equipment: return "equipment/" + type;
                    case ArtFileKind.Weapon: return "weapons/" + type;
                    default: return "species/" + type;
                }
            }
        }
    }
}
```

**Step 4: Run to verify it passes**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass. If the `FolderWithoutRow` test fails because the empty species/weapons tables report nothing, that is correct; investigate only failures in the listed assertions.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtTablesConverter.cs Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs
git commit -m "feat: art tables converter for palettes and equipment, variants from files"
```

---

## Task 5: Converter, part 2: weapons and part groups

**Files:**
- Modify: `Assets/Scripts/Data/Art/ArtTablesConverter.cs`
- Modify: `Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs`

**Step 1: Add failing tests** (inside `ArtTablesConverterTests`)

```csharp
        private const string WeaponHeader = "item_type,description,hand_type,modular,layer_codes,hidden_layers,color_variance_layers\n";

        [Test]
        public void ModularWeapon_EachLayerKeepsItsOwnNumbers()
        {
            var src = Sources(
                "weapons/w01/w01-164-0001.png", "weapons/w01/w01-164-0002.png",
                "weapons/w01/w01-166-0001.png",
                "weapons/w01/w01-166-0005.png");
            src.WeaponsCsv = WeaponHeader + "w01,sword 1 hand,one_handed,y,164 166,,166\n";

            var w = ArtTablesConverter.Convert(src).Data.weapons.Single();

            Assert.IsTrue(w.modular);
            CollectionAssert.AreEqual(new[] { 1, 2 }, w.variantsPerLayer[0].values);
            CollectionAssert.AreEqual(new[] { 1, 5 }, w.variantsPerLayer[1].values);
        }

        [Test]
        public void NonModularWeapon_SharesCompleteNumbersAcrossLayers()
        {
            var src = Sources(
                "weapons/w09/w09-170-0001.png", "weapons/w09/w09-172-0001.png",
                "weapons/w09/w09-170-0002.png");
            src.WeaponsCsv = WeaponHeader + "w09,shield with detail,one_handed,n,170 172,,\n";

            var r = ArtTablesConverter.Convert(src);
            var w = r.Data.weapons.Single();

            CollectionAssert.AreEqual(new[] { 1 }, w.variantsPerLayer[0].values);
            CollectionAssert.AreEqual(new[] { 1 }, w.variantsPerLayer[1].values);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("weapons/w09/w09-172-0002.png")));
        }

        [TestCase("w01,sword,three_handed,y,164,,", "hand_type")]
        [TestCase("w01,sword,one_handed,maybe,164,,", "modular")]
        [TestCase("x01,sword,one_handed,y,164,,", "item_type")]
        public void Weapon_BadCells_AreErrors(string row, string column)
        {
            var src = Sources("weapons/w01/w01-164-0001.png");
            src.WeaponsCsv = WeaponHeader + row + "\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.Contains(column, Messages(r, ArtIssueLevel.Error).Single());
            Assert.AreEqual(0, r.Data.weapons.Count);
        }

        [Test]
        public void PartGroups_LayersAndCompleteVariants()
        {
            var src = Sources(
                "species/h02/h02-0001-092.png", "species/h02/h02-0001-128.png",
                "species/h02/h02-0003-092.png", "species/h02/h02-0003-128.png");
            src.SpeciesPartsCsv = "group,description,layers\nh02,hair,92 128\n";

            var g = ArtTablesConverter.Convert(src).Data.partGroups.Single();

            Assert.AreEqual("h02", g.group);
            CollectionAssert.AreEqual(new[] { 92, 128 }, g.layers);
            CollectionAssert.AreEqual(new[] { 1, 3 }, g.variants);
        }

        [Test]
        public void ZeroNumber_IsAnOrdinaryVariant()
        {
            var src = Sources("species/f01/f01-0000-084.png", "species/f01/f01-0001-084.png");
            src.SpeciesPartsCsv = "group,description,layers\nf01,eyes,84\n";

            var g = ArtTablesConverter.Convert(src).Data.partGroups.Single();

            CollectionAssert.AreEqual(new[] { 0, 1 }, g.variants);
        }
```

**Step 2: Run to verify they fail**

Run: `dotnet test tools/ArtTablesTests`
Expected: the new tests fail (`weapons`/`partGroups` empty).

**Step 3: Implement**

In `Convert`, after `ConvertEquipment(...)`:
```csharp
            ConvertWeapons(Open(src.WeaponsCsv, WeaponsFile, r), files, r);
            ConvertPartGroups(Open(src.SpeciesPartsCsv, SpeciesPartsFile, r), files, r);
```

Add to the class:
```csharp
        private static readonly Regex WeaponTypeRx = new Regex(@"^w\d{2}$");
        private static readonly Regex GroupRx = new Regex(@"^[a-z]\d{2}$");

        private static void ConvertWeapons(Table t, FileIndex files, ArtBakeResult r)
        {
            if (t == null || !t.Require("item_type", "description", "hand_type", "modular", "layer_codes", "hidden_layers", "color_variance_layers")) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < t.Count; i++)
            {
                string type = t.Text(i, "item_type");
                if (!WeaponTypeRx.IsMatch(type)) { t.Error(i, $"item_type '{type}' must look like w01"); continue; }
                if (!seen.Add(type)) { t.Error(i, $"item_type {type} appears twice"); continue; }
                string hand = t.Text(i, "hand_type");
                if (hand != "one_handed" && hand != "two_handed") { t.Error(i, $"hand_type '{hand}' must be one_handed or two_handed"); continue; }
                if (!ArtCells.TryBool(t.Text(i, "modular"), out bool modular)) { t.Error(i, $"modular '{t.Text(i, "modular")}' must be y or n"); continue; }
                if (!t.Ints(i, "layer_codes", out var layers, required: true)) continue;
                if (!t.Ints(i, "hidden_layers", out var hidden)) continue;
                if (!t.Ints(i, "color_variance_layers", out var variance)) continue;
                if (!variance.All(layers.Contains)) { t.Error(i, "color_variance_layers must also be listed in layer_codes"); continue; }

                IntList[] perLayer;
                if (modular)
                {
                    perLayer = files.VariantsPerLayer(ArtFileKind.Weapon, type, layers, r.Issues)
                        .Select(v => new IntList(v)).ToArray();
                }
                else
                {
                    var shared = files.CompleteVariants(ArtFileKind.Weapon, type, layers, r.Issues);
                    perLayer = layers.Select(_ => new IntList(shared)).ToArray();
                }

                r.Data.weapons.Add(new WeaponRow
                {
                    itemType = type,
                    description = t.Text(i, "description"),
                    handType = hand,
                    modular = modular,
                    layerCodes = layers,
                    hiddenLayers = hidden,
                    colorVarianceLayers = variance,
                    variantsPerLayer = perLayer
                });
            }
        }

        private static void ConvertPartGroups(Table t, FileIndex files, ArtBakeResult r)
        {
            if (t == null || !t.Require("group", "description", "layers")) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < t.Count; i++)
            {
                string group = t.Text(i, "group");
                if (!GroupRx.IsMatch(group)) { t.Error(i, $"group '{group}' must look like f01"); continue; }
                if (!seen.Add(group)) { t.Error(i, $"group {group} appears twice"); continue; }
                if (!t.Ints(i, "layers", out var layers, required: true)) continue;

                r.Data.partGroups.Add(new PartGroupRow
                {
                    group = group,
                    description = t.Text(i, "description"),
                    layers = layers,
                    variants = files.CompleteVariants(ArtFileKind.SpeciesPart, group, layers, r.Issues)
                });
            }
        }
```

**Step 4: Run to verify they pass**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtTablesConverter.cs Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs
git commit -m "feat: art tables converter for weapons and species part groups"
```

---

## Task 6: Converter, part 3: species, skin variance, cross-checks

**Files:**
- Modify: `Assets/Scripts/Data/Art/ArtTablesConverter.cs`
- Modify: `Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs`

**Step 1: Add failing tests**

```csharp
        private static ArtSources SpeciesSources(string speciesRow, string skinVarianceRows = "")
        {
            var src = Sources(
                "species/body/0001-016.png", "species/body/0001-037.png",
                "species/f01/f01-0001-084.png", "species/h01/h01-0001-092.png",
                "equipment/hd12/hd12-0001-130.png");
            src.EquipmentCsv += "hd12,tight facemask,130,,\n";
            src.SpeciesPartsCsv = "group,description,layers\nf01,eyes,84\nh01,hair,92\n";
            src.PalettesCsv = "palette,hex\nmain,#FFFFFF\nhuman,#3A1914\nskin_naga,#F3D9C2\n";
            src.SpeciesCsv += speciesRow + "\n";
            src.SkinVarianceCsv += skinVarianceRows;
            return src;
        }

        // name,x_scale,y_scale,head_y_offset,head_zoom,back_arm,legs,body,head,front_arm,eyes,nose,mouth,ears,facial_hair,facial_detail,hair_groups,other_body_parts,skin_palette,hair_palette,eye_palette,facial_detail_palette,item_restrictions
        private const string Human = "human,1,1,0.35,0.5,0001-016,0001-037,,,,f01,,,,,,h01,,human,,,,hd12";

        [Test]
        public void Species_ParsesRow()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human));

            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
            var s = r.Data.species.Single();
            Assert.AreEqual("human", s.name);
            Assert.AreEqual("0001-016", s.backArm);
            Assert.AreEqual("f01", s.eyes);
            CollectionAssert.AreEqual(new[] { "h01" }, s.hairGroups);
            Assert.AreEqual("human", s.skinPalette);
            Assert.AreEqual("", s.hairPalette);
            CollectionAssert.AreEqual(new[] { "hd12" }, s.itemRestrictions);
            Assert.AreEqual(0.35f, s.headYOffset, 1e-6f);
        }

        [Test]
        public void Species_FullPartToken_IsAccepted()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",f01,", ",f01-0001-084,")));
            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
        }

        [TestCase("0009-016", ArtIssueLevel.Warning)]   // body image missing
        [TestCase("f09", ArtIssueLevel.Error)]          // unknown part group
        [TestCase("eyes!", ArtIssueLevel.Error)]        // not a token
        public void Species_BadPartToken(string token, ArtIssueLevel level)
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",0001-016,", "," + token + ",")));
            Assert.IsTrue(Messages(r, level).Any(m => m.StartsWith("species.csv line 2") && m.Contains("back_arm")));
        }

        [Test]
        public void Species_UnknownPalette_IsError_UnknownRestriction_IsWarning()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",human,,,,hd12", ",skin_troll,,,,hd14")));

            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.Contains("skin_troll")));
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.Contains("hd14")));
        }

        [Test]
        public void Species_BlankSkinPalette_IsError()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",human,,,,hd12", ",,,,,hd12")));
            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.Contains("skin_palette")));
        }

        [Test]
        public void SkinVariance_RowsReferenceSpeciesAndPalette()
        {
            var src = SpeciesSources(Human, "human,4 37,skin_naga\nelf,37,skin_naga\nhuman,37,nope\n");

            var r = ArtTablesConverter.Convert(src);

            var v = r.Data.skinVariance.Single();
            Assert.AreEqual("human", v.species);
            CollectionAssert.AreEqual(new[] { 4, 37 }, v.layers);
            Assert.AreEqual(2, Messages(r, ArtIssueLevel.Error).Count(m => m.StartsWith("species_skin_variance.csv")));
        }
```

**Step 2: Run to verify they fail**

Run: `dotnet test tools/ArtTablesTests`
Expected: the new tests fail.

**Step 3: Implement**

In `Convert`, after `ConvertPartGroups(...)` (the order matters: species checks part groups, item types and palettes):
```csharp
            ConvertSpecies(Open(src.SpeciesCsv, SpeciesFile, r), files, r);
            ConvertSkinVariance(Open(src.SkinVarianceCsv, SkinVarianceFile, r), r);
```

Add to the class:
```csharp
        private static readonly Regex BodyTokenRx = new Regex(@"^\d{4}-\d{3}$");
        private static readonly Regex FullPartTokenRx = new Regex(@"^([a-z]\d{2})-(\d{4})-(\d{3})$");

        private static readonly string[] PartColumns =
            { "back_arm", "legs", "body", "head", "front_arm", "eyes", "nose", "mouth", "ears", "facial_hair", "facial_detail" };

        private static void ConvertSpecies(Table t, FileIndex files, ArtBakeResult r)
        {
            var required = new List<string> { "name", "x_scale", "y_scale", "head_y_offset", "head_zoom" };
            required.AddRange(PartColumns);
            required.AddRange(new[] { "hair_groups", "other_body_parts", "skin_palette", "hair_palette", "eye_palette", "facial_detail_palette", "item_restrictions" });
            if (t == null || !t.Require(required.ToArray())) return;

            var groups = new HashSet<string>(r.Data.partGroups.Select(g => g.group));
            var palettes = new HashSet<string>(r.Data.palettes.Select(p => p.name));
            var itemTypes = new HashSet<string>(r.Data.equipment.Select(e => e.itemType).Concat(r.Data.weapons.Select(w => w.itemType)));
            var seen = new HashSet<string>();

            for (int i = 0; i < t.Count; i++)
            {
                string name = t.Text(i, "name");
                if (name.Length == 0) { t.Error(i, "name is blank"); continue; }
                if (!seen.Add(name)) { t.Error(i, $"species {name} appears twice"); continue; }

                bool ok = true;
                float x = 1, y = 1, headY = 0.35f, zoom = 0.5f;
                ok &= Float(t, i, "x_scale", 1f, ref x);
                ok &= Float(t, i, "y_scale", 1f, ref y);
                ok &= Float(t, i, "head_y_offset", 0.35f, ref headY);
                ok &= Float(t, i, "head_zoom", 0.5f, ref zoom);

                foreach (var column in PartColumns)
                    ok &= CheckPartToken(t, i, column, t.Text(i, column), files, groups);
                var hair = ArtCells.Strings(t.Text(i, "hair_groups"));
                foreach (var g in hair) ok &= CheckPartToken(t, i, "hair_groups", g, files, groups);
                var other = ArtCells.Strings(t.Text(i, "other_body_parts"));
                foreach (var g in other) ok &= CheckPartToken(t, i, "other_body_parts", g, files, groups);

                string skin = t.Text(i, "skin_palette");
                if (skin.Length == 0) { t.Error(i, "skin_palette is blank"); ok = false; }
                foreach (var column in new[] { "skin_palette", "hair_palette", "eye_palette", "facial_detail_palette" })
                {
                    string p = t.Text(i, column);
                    if (p.Length > 0 && !palettes.Contains(p)) { t.Error(i, $"{column} '{p}' is not in palettes.csv"); ok = false; }
                }

                var restrictions = ArtCells.Strings(t.Text(i, "item_restrictions"));
                foreach (var type in restrictions)
                    if (!itemTypes.Contains(type)) t.Warning(i, $"item_restrictions lists {type}, which is not an item type");

                if (!ok) continue;
                r.Data.species.Add(new SpeciesRow
                {
                    name = name, xScale = x, yScale = y, headYOffset = headY, headZoom = zoom,
                    backArm = t.Text(i, "back_arm"), legs = t.Text(i, "legs"), body = t.Text(i, "body"),
                    head = t.Text(i, "head"), frontArm = t.Text(i, "front_arm"),
                    eyes = t.Text(i, "eyes"), nose = t.Text(i, "nose"), mouth = t.Text(i, "mouth"),
                    ears = t.Text(i, "ears"), facialHair = t.Text(i, "facial_hair"), facialDetail = t.Text(i, "facial_detail"),
                    hairGroups = hair, otherBodyParts = other, itemRestrictions = restrictions,
                    skinPalette = skin, hairPalette = t.Text(i, "hair_palette"),
                    eyePalette = t.Text(i, "eye_palette"), facialDetailPalette = t.Text(i, "facial_detail_palette")
                });
            }
        }

        private static bool Float(Table t, int i, string column, float fallback, ref float value)
        {
            if (ArtCells.TryFloat(t.Text(i, column), fallback, out value)) return true;
            t.Error(i, $"{column} '{t.Text(i, column)}' must be a number like 1.1");
            return false;
        }

        /// Blank, a body token (0001-016), a part group (f01) or a full part
        /// token (f01-0001-084). Unknown shapes and groups are errors; a
        /// missing body or part image is a warning (the part draws nothing).
        private static bool CheckPartToken(Table t, int i, string column, string token, FileIndex files, HashSet<string> groups)
        {
            if (token.Length == 0) return true;
            if (BodyTokenRx.IsMatch(token))
            {
                if (!files.BodyTokens.Contains(token)) t.Warning(i, $"{column} {token}: image species/body/{token}.png not found");
                return true;
            }
            if (GroupRx.IsMatch(token))
            {
                if (groups.Contains(token)) return true;
                t.Error(i, $"{column} {token} is not a group in species_parts.csv");
                return false;
            }
            var m = FullPartTokenRx.Match(token);
            if (m.Success)
            {
                if (!files.HasPart(m.Groups[1].Value, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)))
                    t.Warning(i, $"{column} {token}: image species/{m.Groups[1].Value}/{token}.png not found");
                return true;
            }
            t.Error(i, $"{column} '{token}' must look like 0001-016, f01 or f01-0001-084");
            return false;
        }

        private static void ConvertSkinVariance(Table t, ArtBakeResult r)
        {
            if (t == null || !t.Require("species", "layers", "palette")) return;
            var species = new HashSet<string>(r.Data.species.Select(s => s.name));
            var palettes = new HashSet<string>(r.Data.palettes.Select(p => p.name));
            for (int i = 0; i < t.Count; i++)
            {
                string name = t.Text(i, "species"), palette = t.Text(i, "palette");
                if (!species.Contains(name)) { t.Error(i, $"species '{name}' is not in species.csv"); continue; }
                if (!palettes.Contains(palette)) { t.Error(i, $"palette '{palette}' is not in palettes.csv"); continue; }
                if (!t.Ints(i, "layers", out var layers, required: true)) continue;
                r.Data.skinVariance.Add(new SkinVarianceRow { species = name, layers = layers, palette = palette });
            }
        }
```

**Step 4: Run to verify they pass**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtTablesConverter.cs Assets/Tests/EditMode/Art/ArtTablesConverterTests.cs
git commit -m "feat: art tables converter for species and skin variance with cross-checks"
```

---

## Task 7: Repo reader

**Files:**
- Create: `Assets/Scripts/Data/Art/ArtRepo.cs`
- Create: `Assets/Tests/EditMode/Art/ArtRepoTests.cs`

**Step 1: Write the failing test**

```csharp
using System.IO;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtRepoTests
    {
        [Test]
        public void ReadSources_ReadsCsvsAndListsPngsRelativeWithSlashes()
        {
            string root = Path.Combine(Path.GetTempPath(), "artrepo_" + Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(Path.Combine(root, ArtRepo.TablesDir));
                File.WriteAllText(Path.Combine(root, ArtRepo.TablesDir, "equipment.csv"), "item_type\n");
                string hd = Path.Combine(root, ArtRepo.ImagesDir, "equipment", "hd01");
                Directory.CreateDirectory(hd);
                File.WriteAllBytes(Path.Combine(hd, "hd01-0001-130.png"), new byte[0]);
                File.WriteAllBytes(Path.Combine(hd, "hd01-0001-130.png.meta"), new byte[0]);

                var src = ArtRepo.ReadSources(root);

                Assert.AreEqual("item_type\n", src.EquipmentCsv);
                Assert.IsNull(src.WeaponsCsv);
                CollectionAssert.AreEqual(new[] { "equipment/hd01/hd01-0001-130.png" }, src.ImageFiles);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
```

**Step 2: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests`
Expected: build error, `ArtRepo` not found.

**Step 3: Implement**

`Assets/Scripts/Data/Art/ArtRepo.cs`:
```csharp
using System;
using System.IO;

namespace Starquill.Data.Art
{
    /// Reads the art tables and image listing from a project folder (the
    /// Unity project in the Editor; the artist's repo clone in the studio).
    public static class ArtRepo
    {
        public const string TablesDir = "Assets/Data/ArtTables";
        public const string ImagesDir = "Assets/Resources/Images";

        public static ArtSources ReadSources(string projectRoot)
        {
            string tables = Path.Combine(projectRoot, TablesDir);
            string images = Path.Combine(projectRoot, ImagesDir);
            var src = new ArtSources
            {
                EquipmentCsv = ReadOrNull(tables, ArtTablesConverter.EquipmentFile),
                WeaponsCsv = ReadOrNull(tables, ArtTablesConverter.WeaponsFile),
                SpeciesCsv = ReadOrNull(tables, ArtTablesConverter.SpeciesFile),
                SpeciesPartsCsv = ReadOrNull(tables, ArtTablesConverter.SpeciesPartsFile),
                SkinVarianceCsv = ReadOrNull(tables, ArtTablesConverter.SkinVarianceFile),
                PalettesCsv = ReadOrNull(tables, ArtTablesConverter.PalettesFile),
            };
            if (Directory.Exists(images))
            {
                foreach (var f in Directory.EnumerateFiles(images, "*.png", SearchOption.AllDirectories))
                    src.ImageFiles.Add(Path.GetRelativePath(images, f).Replace('\\', '/'));
                src.ImageFiles.Sort(StringComparer.Ordinal);
            }
            return src;
        }

        private static string ReadOrNull(string dir, string file)
        {
            string path = Path.Combine(dir, file);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
```

**Step 4: Run to verify it passes**

Run: `dotnet test tools/ArtTablesTests`
Expected: all pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/Art/ArtRepo.cs Assets/Tests/EditMode/Art/ArtRepoTests.cs
git commit -m "feat: read art tables and image listing from a project folder"
```

---

## Task 8: Migration script, part 1: `0000` cleanup

**Files:**
- Create: `tools/migrate_art_tables.py`

**Step 1: Write the script** (all three subcommands; only `cleanup` is run in this task)

`tools/migrate_art_tables.py`:
```python
#!/usr/bin/env python3
"""One-time art migration for the Character Studio design (2026-11-08).

  python3 tools/migrate_art_tables.py cleanup [--apply]  # 0000 species parts
  python3 tools/migrate_art_tables.py move    [--apply]  # one folder per type
  python3 tools/migrate_art_tables.py csv     [--apply]  # JSON -> Assets/Data/ArtTables/*.csv

Without --apply it only prints what it would do. Close the Unity Editor
first: files move with their .meta so Unity keeps every asset ID.
Run the steps in order: cleanup, move, csv.
"""
import argparse
import csv
import io
import json
import os
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
IMG = ROOT / "Assets/Resources/Images"
DATA = ROOT / "Assets/Resources/Data"
TABLES = ROOT / "Assets/Data/ArtTables"

PART_RX = re.compile(r"^([a-z]\d{2})-(\d{4})-(\d{3})\.png$")


def move_with_meta(src: Path, dst: Path, apply: bool):
    print(f"  move {src.relative_to(IMG)} -> {dst.relative_to(IMG)}")
    if apply:
        dst.parent.mkdir(parents=True, exist_ok=True)
        src.rename(dst)
        meta = src.with_name(src.name + ".meta")
        if meta.exists():
            meta.rename(dst.with_name(dst.name + ".meta"))


def delete_with_meta(p: Path, apply: bool):
    print(f"  delete {p.relative_to(IMG)}")
    if apply:
        p.unlink()
        meta = p.with_name(p.name + ".meta")
        if meta.exists():
            meta.unlink()


def same_pixels(a: Path, b: Path) -> bool:
    A = Image.open(a).convert("RGBA")
    B = Image.open(b).convert("RGBA")
    return A.size == B.size and A.tobytes() == B.tobytes()


def is_empty(p: Path) -> bool:
    return Image.open(p).convert("RGBA").getchannel("A").getbbox() is None


def cmd_cleanup(apply: bool):
    """0000 is not special after this: copies of 0001 and empty images are
    deleted; unique drawings are renumbered to the next free number."""
    folder = IMG / "species"
    groups = {}  # group -> {number: {layer: path}}
    for f in sorted(folder.iterdir()):
        m = PART_RX.match(f.name)
        if m:
            groups.setdefault(m[1], {}).setdefault(int(m[2]), {})[int(m[3])] = f

    for group, numbers in sorted(groups.items()):
        zero = numbers.get(0)
        if not zero:
            continue
        one = numbers.get(1, {})
        if one and set(zero) == set(one) and all(same_pixels(zero[L], one[L]) for L in zero):
            print(f"{group}: 0000 is a copy of 0001")
            for p in zero.values():
                delete_with_meta(p, apply)
        elif all(is_empty(p) for p in zero.values()):
            print(f"{group}: 0000 is empty")
            for p in zero.values():
                delete_with_meta(p, apply)
        else:
            new = max(numbers) + 1
            print(f"{group}: 0000 is a unique drawing, renumbered to {new:04d}")
            for layer, p in zero.items():
                move_with_meta(p, p.with_name(f"{group}-{new:04d}-{layer:03d}.png"), apply)


FLAT_RULES = [
    ("equipment", re.compile(r"^([a-z]{2}\d{2})-\d{4}-\d{3}\.png$"), lambda m: m[1]),
    ("weapons", re.compile(r"^(w\d{2})-\d{1,3}-\d{4}\.png$"), lambda m: m[1]),
    ("species", re.compile(r"^\d{4}-\d{3}\.png$"), lambda m: "body"),
    ("species", PART_RX, lambda m: m[1]),
]


def cmd_move(apply: bool):
    """Flat folders -> one folder per item type / part group (+ species/body)."""
    left = []
    for root in ("equipment", "weapons", "species"):
        for f in sorted((IMG / root).iterdir()):
            if not f.is_file() or f.suffix != ".png":
                continue
            for rule_root, rx, folder in FLAT_RULES:
                m = rx.match(f.name) if rule_root == root else None
                if m:
                    move_with_meta(f, IMG / root / folder(m) / f.name, apply)
                    break
            else:
                left.append(f.relative_to(IMG))
    for p in left:
        print(f"  left in place (misnamed, artist decides): {p}")


def ints(xs):
    return " ".join(str(x) for x in xs)


def num(v):
    return f"{v:g}"


def slug(name):
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


# Copied from DisplayDataRegistry.LoadLayerMappings (moves into species_parts.csv).
LAYER_MAPPINGS = {
    "f01": [84], "f02": [86], "f03": [85], "f04": [100], "f05": [122], "f06": [87],
    "h01": [92], "h02": [92, 128], "h03": [92, 154], "h04": [92, 128, 154],
    "d02": [86], "d04": [100], "d05": [122], "d06": [87], "g02": [86],
    "s01": [82], "s02": [84], "s03": [86], "s04": [87],
}

PART_FIELDS = [("backArm", "back arm"), ("legs", "legs"), ("body", "body"), ("head", "head"),
               ("frontArm", "front arm"), ("eyes", "eyes"), ("nose", "nose"), ("mouth", "mouth"),
               ("ears", "ears"), ("facialHair", "facial hair"), ("facialDetail", "facial detail")]


def write_csv(path: Path, header, rows, apply: bool):
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    w.writerows(rows)
    print(f"  {path.relative_to(ROOT)}: {len(rows)} rows")
    if apply:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(buf.getvalue(), encoding="utf-8", newline="")


def cmd_csv(apply: bool):
    eq = json.loads((DATA / "equipment.json").read_text())
    wp = json.loads((DATA / "weapons.json").read_text())
    sp = json.loads((DATA / "species.json").read_text())
    palettes = {k: list(v) for k, v in json.loads((DATA / "color_palettes.json").read_text()).items()}

    write_csv(TABLES / "equipment.csv",
              ["item_type", "description", "layer_codes", "hidden_layers", "color_variance_layers"],
              [[e["item_type"], e["description"], ints(e["layer_codes"]), ints(e["hidden_layers"]),
                ints(e["layer_color_variance"])] for e in eq], apply)

    write_csv(TABLES / "weapons.csv",
              ["item_type", "description", "hand_type", "modular", "layer_codes", "hidden_layers", "color_variance_layers"],
              [[w["item_type"], w["description"], w.get("hand_type", "one_handed"), "y" if w["modular"] else "n",
                ints(w["layer_codes"]), ints(w["hidden_layers"]), ints(w["layer_color_variance"])] for w in wp], apply)

    def palette_for(values, prefix, species):
        if not values:
            return ""
        if len(values) == 1 and values[0] in palettes:
            return values[0]
        name = f"{prefix}_{slug(species)}"
        palettes[name] = values
        return name

    species_rows, variance_rows, used_by = [], [], {}
    for s in sp:
        n = s["name"]
        for field, label in PART_FIELDS:
            tok = s.get(field, "")
            if tok and re.match(r"^[a-z]\d{2}", tok):
                used_by.setdefault(tok[:3], set()).add((label, n))
        for h in s.get("hair", []):
            used_by.setdefault(h[:3], set()).add(("hair", n))
        species_rows.append([
            n, num(s.get("x_scale", 1)), num(s.get("y_scale", 1)),
            num(s.get("head_y_offset", 0.35)), num(s.get("head_zoom", 0.5)),
            s.get("backArm", ""), s.get("legs", ""), s.get("body", ""), s.get("head", ""), s.get("frontArm", ""),
            s.get("eyes", ""), s.get("nose", ""), s.get("mouth", ""), s.get("ears", ""),
            s.get("facialHair", ""), s.get("facialDetail", ""),
            " ".join(s.get("hair", [])), " ".join(s.get("otherBodyParts", [])),
            palette_for(s.get("skin_color", []), "skin", n),
            palette_for(s.get("hair_color", []), "hair", n),
            palette_for(s.get("eyes_color", []), "eyes", n),
            palette_for(s.get("facialDetail_color", []), "facial_detail", n),
            " ".join(s.get("itemRestrictions", [])),
        ])
        for idx, vs in enumerate(s.get("skinVariance_sets", []), 1):
            pname = f"skinvar_{slug(n)}_{idx}"
            palettes[pname] = vs["hex_colors"]
            variance_rows.append([n, ints(vs["indices"]), pname])

    write_csv(TABLES / "species.csv",
              ["name", "x_scale", "y_scale", "head_y_offset", "head_zoom", "back_arm", "legs", "body", "head",
               "front_arm", "eyes", "nose", "mouth", "ears", "facial_hair", "facial_detail", "hair_groups",
               "other_body_parts", "skin_palette", "hair_palette", "eye_palette", "facial_detail_palette",
               "item_restrictions"], species_rows, apply)

    def describe(group):
        uses = used_by.get(group, set())
        labels = sorted({label for label, _ in uses})
        names = sorted({n for _, n in uses})
        who = ", ".join(names[:3]) + (f" +{len(names) - 3} more" if len(names) > 3 else "")
        return f"{' / '.join(labels) or 'unused'} ({who})" if names else "unused"

    write_csv(TABLES / "species_parts.csv", ["group", "description", "layers"],
              [[g, describe(g), ints(layers)] for g, layers in LAYER_MAPPINGS.items()], apply)

    write_csv(TABLES / "species_skin_variance.csv", ["species", "layers", "palette"], variance_rows, apply)

    write_csv(TABLES / "palettes.csv", ["palette", "hex"],
              [[name, "#" + h.lstrip("#").upper()] for name, colors in palettes.items() for h in colors], apply)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("step", choices=["cleanup", "move", "csv"])
    ap.add_argument("--apply", action="store_true", help="make the changes (default: print only)")
    a = ap.parse_args()
    {"cleanup": cmd_cleanup, "move": cmd_move, "csv": cmd_csv}[a.step](a.apply)
    if not a.apply:
        print("\n(dry run; add --apply to make these changes)")


if __name__ == "__main__":
    sys.exit(main())
```

**Step 2: Dry run**

Make sure the Unity Editor is closed. Run: `python3 tools/migrate_art_tables.py cleanup`
Expected:
- `copy of 0001` for `f04 h01 h02 h03 h04`
- `empty` for `s04`
- `unique drawing, renumbered` for `f01 f02 f03 f05 f06 s01 s02 s03` (f01 → 0247, f02 → 0221, f03 → 0210, f05 → 0067, f06 → 0054, s01-s03 → 0004)

Anything else: stop and report.

**Step 3: Apply and check**

Run: `python3 tools/migrate_art_tables.py cleanup --apply`
Run: `ls Assets/Resources/Images/species | grep -c -- '-0000-'`
Expected: `0`
Run: `git status --short | head`. Expected: deleted and renamed `species/*` PNG + `.meta` pairs only.

**Step 4: Commit**

```bash
git add tools/migrate_art_tables.py Assets/Resources/Images/species
git commit -m "chore: clean up 0000 species parts: drop copies and blanks, renumber unique drawings"
```

The game is unaffected: it still picks `0001..count` from JSON, and the renumbered drawings sit above the count until Task 14.

---

## Task 9: Migration part 2: one folder per type + new image paths

The game must keep working, so the file move and the path change land in one commit.

**Files:**
- Modify (move): everything under `Assets/Resources/Images/`
- Modify: `Assets/Scripts/Display/ImageToken.cs:70-91`
- Modify: `Assets/Tests/EditMode/Display/ImageTokenTests.cs:54-82`
- Modify: `tools/analyze_item_framing.py:72-73`

**Step 1: Update the path tests first**

In `ImageTokenTests.cs` change the expected strings:
```csharp
            Assert.AreEqual("Images/species/body/0001-016", token.ToSpeciesSpritePath());        // was Images/species/0001-016
            Assert.AreEqual("Images/species/f01/f01-0043-084", token.ToSpeciesSpritePath());     // was Images/species/f01-0043-084
            Assert.AreEqual("Images/equipment/hd01/hd01-0032-130", path);                        // was Images/equipment/hd01-0032-130
            Assert.AreEqual("Images/weapons/w01/w01-164-0001", path);                            // was Images/weapons/w01-164-0001
            Assert.AreEqual("Images/species/h02/h02-0043-092", path);                            // was Images/species/h02-0043-092
```

**Step 2: Change the path builders**

In `ImageToken.cs` replace `ToSpeciesSpritePath` and the three builders:
```csharp
        public string ToSpeciesSpritePath()
        {
            return Kind switch
            {
                ImageTokenKind.Static => $"Images/species/body/{ImageNum}-{Layer:D3}",
                ImageTokenKind.ModularFull => $"Images/species/{GroupType}/{GroupType}-{ImageNum}-{Layer:D3}",
                _ => null
            };
        }

        public static string BuildModularSpeciesPath(string groupType, string imageNum, int layer)
        {
            return $"Images/species/{groupType}/{groupType}-{imageNum}-{layer:D3}";
        }

        public static string BuildEquipmentSpritePath(string itemType, int itemNum, int layer)
        {
            return $"Images/equipment/{itemType}/{itemType}-{itemNum:D4}-{layer:D3}";
        }

        public static string BuildWeaponSpritePath(string itemType, int layer, int variant)
        {
            return $"Images/weapons/{itemType}/{itemType}-{layer}-{variant:D4}";
        }
```

**Step 3: Move the files (Editor closed)**

Run: `python3 tools/migrate_art_tables.py move` (dry run; scan the output)
Expected last lines: `left in place (misnamed, artist decides): equipment/Artboard 1hd10-0001-094.png` and `...0004...`.
Run: `python3 tools/migrate_art_tables.py move --apply`
Run: `find Assets/Resources/Images -maxdepth 2 -name '*.png' | wc -l`
Expected: `2` (the two Artboard files).
Run: `find Assets/Resources/Images -name '*.png' | wc -l` before and after must match (record the before number in Step 3's dry run with the same command).

**Step 4: Update the framing tool**

In `tools/analyze_item_framing.py` replace lines 72-73:
```python
    files = glob.glob(os.path.join(EQUIP_DIR, '*', '*.png')) + \
        glob.glob(os.path.join(WEAPON_DIR, '*', '*.png'))
```

**Step 5: Compile and verify in Unity**

Open the Editor (it reimports the moved files and creates folder `.meta` files). Check the Console has no compile errors (Coplay `check_compile_errors`).

**CHECKPOINT A (Zach):**
- Test Runner > EditMode > Run All: all green (ImageTokenTests with new paths).
- Press Play in `ExploreScene`: party dolls and enemies render with hats, armor and weapons; open Party and Loot screens; no `ImageResolver: Sprite not found` warnings in the Console.

**Step 6: Commit**

```bash
git add -A Assets/Resources/Images Assets/Scripts/Display/ImageToken.cs Assets/Tests/EditMode/Display/ImageTokenTests.cs tools/analyze_item_framing.py
git commit -m "refactor: one art folder per item type and part group, image paths include the folder"
```
Check `git show --stat HEAD | tail -3` reports renames, not delete+add pairs (similarity detection).

---

## Task 10: Migration part 3: generate the CSVs + repo smoke test

**Files:**
- Create: `Assets/Data/ArtTables/{equipment,weapons,species,species_parts,species_skin_variance,palettes}.csv`
- Create: `Assets/Tests/EditMode/Art/RepoArtTablesTests.cs`

**Step 1: Write the smoke test**

```csharp
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    /// Converts the real CSVs and images in this repo. Fails on table errors;
    /// prints every warning so they can be read in the test output.
    public class RepoArtTablesTests
    {
        [Test, Category("RepoData")]
        public void RepoArtTables_HaveNoErrors()
        {
            string root = FindProjectRoot();
            if (root == null) Assert.Ignore("project root not found");

            var r = ArtTablesConverter.Convert(ArtRepo.ReadSources(root));

            foreach (var issue in r.Issues) TestContext.WriteLine(issue);
            Assert.IsFalse(r.HasErrors, string.Join("\n", r.Issues.Where(i => i.level == ArtIssueLevel.Error)));
            Assert.AreEqual(49, r.Data.equipment.Count);
            Assert.AreEqual(9, r.Data.weapons.Count);
            Assert.AreEqual(27, r.Data.species.Count);
            Assert.AreEqual(19, r.Data.partGroups.Count);
        }

        private static string FindProjectRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Environment.CurrentDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (Directory.Exists(Path.Combine(dir.FullName, ArtRepo.TablesDir))) return dir.FullName;
            }
            return null;
        }
    }
}
```

**Step 2: Run to verify it fails**

Run: `dotnet test tools/ArtTablesTests --filter Category=RepoData`
Expected: Ignored ("project root not found"), since the tables folder does not exist yet. Treat Ignored as the failing state.

**Step 3: Generate the CSVs**

Run: `python3 tools/migrate_art_tables.py csv` (dry run)
Expected row counts: equipment 49, weapons 9, species 27, species_parts 19, species_skin_variance ≥ 3, palettes ≥ 1745.
Run: `python3 tools/migrate_art_tables.py csv --apply`

**Step 4: Run the smoke test**

Run: `dotnet test tools/ArtTablesTests --filter Category=RepoData --logger "console;verbosity=detailed"`
Expected: PASS. In the output, the warnings are exactly:
- 2 misnamed `equipment/Artboard 1hd10-000{1,4}-094.png` (file not in a type folder)
- 1 `species.csv` line for bridge troll `item_restrictions lists hd14`

Any other warning or error: stop and investigate. Do not edit the CSVs by hand to silence it without telling Zach.

**Step 5: Spot-check the variant counts that were stale**

```bash
for t in fe02 hd08 hd10 tr14; do ls Assets/Resources/Images/equipment/$t | grep -v meta | sed -E 's/.*-([0-9]{4})-.*/\1/' | sort -u | wc -l; done
```
Expected: `71 35 14 17` (Task 14's `StaleCountsAreGone_FilesDecide` test pins these in Unity).

**Step 6: Commit**

```bash
git add Assets/Data/ArtTables Assets/Tests/EditMode/Art/RepoArtTablesTests.cs
git commit -m "feat: art tables as CSV (generated once from the current JSON)"
```

The game still reads JSON at this point; the CSVs are unused until Task 12.

---

## Task 11: ArtTables asset + Editor baker + import settings + image audit

**Files:**
- Create: `Assets/Scripts/Data/ArtTables.cs`
- Create: `Assets/Scripts/Display/ArtImageAudit.cs`
- Create: `Assets/Editor/ArtTablesBaker.cs`
- Delete: `Assets/Editor/SpriteImportFixer.cs` (+ `.meta`)

No unit tests here: this is Editor glue around the tested converter. Verification is manual (Step 5).

**Step 1: The asset class**

`Assets/Scripts/Data/ArtTables.cs`:
```csharp
using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Data
{
    /// Baked from Assets/Data/ArtTables/*.csv + the image folders by
    /// ArtTablesBaker (Editor). Generated and gitignored: never edit it.
    public class ArtTables : ScriptableObject
    {
        public const string ResourcePath = "ArtTables";

        public ArtTableData data = new ArtTableData();

        public static ArtTableData LoadFromResources()
        {
            var asset = Resources.Load<ArtTables>(ResourcePath);
            if (asset != null) return asset.data;
            Debug.LogError("ArtTables asset missing. In the Editor run Tools > Art Tables > Bake Now.");
            return new ArtTableData();
        }
    }
}
```

**Step 2: Image audit (duplicates and empty images)**

`Assets/Scripts/Display/ArtImageAudit.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Display
{
    /// Decodes every art PNG and reports empty images and pixel-identical
    /// variants inside one item type / part group. Slow (seconds): run on
    /// build, from the menu, and on the studio's Rescan, not on every import.
    public static class ArtImageAudit
    {
        public static List<ArtIssue> Run(string imagesRoot, IEnumerable<string> relativePaths)
        {
            var issues = new List<ArtIssue>();
            var seen = new Dictionary<(string folder, int layer, string hash), string>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (var rel in relativePaths)
                {
                    if (!ArtFileName.TryParse(rel, out var f, out var problem) || problem != null) continue;
                    if (!tex.LoadImage(File.ReadAllBytes(Path.Combine(imagesRoot, rel)))) continue;

                    var pixels = tex.GetPixels32();
                    bool empty = true;
                    foreach (var p in pixels) if (p.a != 0) { empty = false; break; }
                    if (empty)
                    {
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, rel, "image is empty (fully transparent)"));
                        continue;
                    }

                    string hash = Hash(pixels, tex.width, tex.height);
                    string folder = rel.Substring(0, rel.LastIndexOf('/'));
                    var key = (folder, f.Layer, hash);
                    if (seen.TryGetValue(key, out var first))
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, rel, $"same pixels as {first}"));
                    else
                        seen[key] = rel;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return issues;
        }

        private static string Hash(Color32[] pixels, int w, int h)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
            }
            using var sha = System.Security.Cryptography.SHA1.Create();
            return $"{w}x{h}:" + Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}
```

**Step 3: The baker**

`Assets/Editor/ArtTablesBaker.cs`:
```csharp
using System.IO;
using System.Linq;
using Starquill.Data;
using Starquill.Data.Art;
using Starquill.Display;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Turns Assets/Data/ArtTables/*.csv + Assets/Resources/Images into
/// Assets/Resources/ArtTables.asset (generated, gitignored).
/// Runs: on Editor load if the asset is missing, whenever a CSV or art PNG
/// changes, before every build (fails the build on table errors), and from
/// Tools > Art Tables.
public static class ArtTablesBaker
{
    public const string AssetPath = "Assets/Resources/ArtTables.asset";

    [MenuItem("Tools/Art Tables/Bake Now")]
    public static void BakeFromMenu() => Bake();

    [MenuItem("Tools/Art Tables/Check Images (duplicates, empty)")]
    public static void AuditFromMenu()
    {
        var issues = Audit();
        foreach (var i in issues) Debug.LogWarning("Art images: " + i);
        Debug.Log($"Art images checked: {issues.Count} warning(s).");
    }

    public static ArtBakeResult Bake()
    {
        var result = ArtTablesConverter.Convert(ArtRepo.ReadSources(ProjectRoot));

        var asset = AssetDatabase.LoadAssetAtPath<ArtTables>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ArtTables>();
            asset.data = result.Data;
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        else
        {
            asset.data = result.Data;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();

        foreach (var issue in result.Issues)
        {
            if (issue.level == ArtIssueLevel.Error) Debug.LogError("Art tables: " + issue);
            else Debug.LogWarning("Art tables: " + issue);
        }
        int errors = result.Issues.Count(i => i.level == ArtIssueLevel.Error);
        Debug.Log($"Art tables baked: {result.Data.equipment.Count} equipment, {result.Data.weapons.Count} weapons, " +
                  $"{result.Data.species.Count} species, {result.Data.partGroups.Count} part groups; " +
                  $"{errors} error(s), {result.Issues.Count - errors} warning(s).");
        return result;
    }

    public static System.Collections.Generic.List<ArtIssue> Audit()
    {
        var src = ArtRepo.ReadSources(ProjectRoot);
        return ArtImageAudit.Run(Path.Combine(ProjectRoot, ArtRepo.ImagesDir), src.ImageFiles);
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    internal static bool IsArtPath(string p) =>
        p.StartsWith(ArtRepo.TablesDir + "/") || p.StartsWith(ArtRepo.ImagesDir + "/");
}

[InitializeOnLoad]
internal static class ArtTablesBootstrap
{
    static ArtTablesBootstrap()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ArtTablesBaker.AssetPath)) ArtTablesBaker.Bake();
        };
    }
}

internal class ArtTablesPostprocessor : AssetPostprocessor
{
    private static bool pending;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (pending) return;
        if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(ArtTablesBaker.IsArtPath)) return;
        pending = true;
        EditorApplication.delayCall += () =>
        {
            pending = false;
            ArtTablesBaker.Bake();
        };
    }

    /// New art PNGs get the paper-doll sprite settings automatically
    /// (replaces the old manual Tools > Fix Sprite Import Settings).
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRepo.ImagesDir + "/") || !assetImporter.importSettingsMissing) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 256;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }
}

internal class ArtTablesBuildStep : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (var issue in ArtTablesBaker.Audit()) Debug.LogWarning("Art images: " + issue);
        var result = ArtTablesBaker.Bake();
        if (result.HasErrors)
            throw new BuildFailedException("Art tables have errors (see Console lines starting 'Art tables: Error'). Fix those CSV rows and build again.");
    }
}
```

`Assets/Editor` has no asmdef, so it compiles into `Assembly-CSharp-Editor`, which sees every runtime assembly.

**Step 4: Delete the old manual fixer**

```bash
git rm Assets/Editor/SpriteImportFixer.cs Assets/Editor/SpriteImportFixer.cs.meta
```

**Step 5: Verify in the Editor**

1. Open the Editor; Console: no compile errors; a line `Art tables baked: 49 equipment, 9 weapons, 27 species, 19 part groups; 0 error(s), 3 warning(s).`
2. `ls Assets/Resources/ArtTables.asset` exists; `git status --short Assets/Resources/ArtTables.asset` prints nothing (ignored).
3. Tools > Art Tables > Check Images: expect warnings only for genuinely duplicated or empty art. Copy the list into the commit message body or a note for Zach; do not "fix" art.
4. Edit any `equipment.csv` description in a text editor, return to Unity: the bake line reappears. Revert the edit.
5. Copy one PNG into `Assets/Resources/Images/equipment/hd01/` as `hd01-0999-130.png`; after import, its Inspector shows Sprite, 100 PPU, Bilinear, max size 256, no compression; bake logs no new warnings (gaps are fine). Delete the copy (and its `.meta`).

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/ArtTables.cs Assets/Scripts/Display/ArtImageAudit.cs Assets/Editor/ArtTablesBaker.cs
git add Assets/Scripts/Data/Art Assets/Tests/EditMode/Art   # pick up .meta files Unity created
git commit -m "feat: bake art tables into a Unity asset on import and before builds; auto sprite import settings"
```

---

## Task 12: Palettes come from ArtTables

**Files:**
- Modify: `Assets/Scripts/Display/ColorManager.cs`
- Create: `Assets/Tests/EditMode/Fixtures/ArtFixtures.cs`
- Modify: `Assets/Tests/EditMode/Display/ColorManagerTests.cs`
- Modify: `Assets/Tests/EditMode/Destinations/ColorManagerFamilyTests.cs:13`
- Modify: `Assets/Tests/EditMode/Destinations/ColorManagerOverrideTests.cs:13`
- Modify: `Assets/Tests/EditMode/Equipment/LootDropperTests.cs:44`
- Modify: `Assets/Tests/EditMode/Destinations/TargetedRollTests.cs:98`
- Modify: `Assets/Tests/EditMode/Destinations/DungeonRewardMathTests.cs:80`

**Step 1: Shared test fixtures**

`Assets/Tests/EditMode/Fixtures/ArtFixtures.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Starquill.Data.Art;
using Starquill.Display;

namespace Starquill.Tests.Fixtures
{
    public static class ArtFixtures
    {
        public static List<PaletteRow> Palette(string name, params string[] hex) =>
            new List<PaletteRow> { new PaletteRow { name = name, hex = hex } };

        public static ColorManager Colors(string name, params string[] hex)
        {
            var c = new ColorManager();
            c.LoadPalettes(Palette(name, hex));
            return c;
        }

        /// Red, green, blue "main" palette shared by the loot/dungeon tests.
        public static ColorManager RgbColors() => Colors("main", "FF0000", "00FF00", "0000FF");

        public static EquipmentRow Armor(string type, string description, int layer, int count) => new EquipmentRow
        {
            itemType = type,
            description = description,
            layerCodes = new[] { layer },
            variants = Enumerable.Range(1, count).ToArray()
        };
    }
}
```

**Step 2: Replace the JSON palette loading in tests**

- `ColorManagerTests.cs`: each `manager.LoadFromJson(...)` becomes `manager.LoadPalettes(ArtFixtures.Palette(...))` with the same names and hex values, e.g.
  ```csharp
  manager.LoadPalettes(ArtFixtures.Palette("test_palette", "FF0000", "00FF00", "0000FF"));
  manager.LoadPalettes(ArtFixtures.Palette("main", "FFFFFF"));
  manager.LoadPalettes(ArtFixtures.Palette("single", "FF0000"));
  manager.LoadPalettes(ArtFixtures.Palette("human", "3A1914", "45443C"));
  ```
  Rename test `LoadPalettes_FromJson_ParsesCorrectly` to `LoadPalettes_ParsesHex`. Add `using Starquill.Tests.Fixtures;`.
- `ColorManagerFamilyTests.cs:13`: `cm.LoadPalettes(ArtFixtures.Palette("main", "FF0000", "AA1111", "22CC44", "888888", "8B4513"));`
- `ColorManagerOverrideTests.cs:13`: `cm.LoadPalettes(ArtFixtures.Palette("main", "FF0000", "AA1111", "22CC44"));`
- `LootDropperTests.cs:43-44`, `TargetedRollTests.cs:97-98`, `DungeonRewardMathTests.cs:79-80`: replace the `new ColorManager()` + `LoadFromJson` pair with `var colors = ArtFixtures.RgbColors();`

**Step 3: Change ColorManager**

In `ColorManager.cs`:
- Add `using Starquill.Data;` and `using Starquill.Data.Art;`.
- Replace `LoadFromJson` and `LoadFromResources` with:
```csharp
        public void LoadPalettes(IEnumerable<PaletteRow> rows)
        {
            palettes.Clear();
            familyIndex.Clear();
            foreach (var row in rows)
            {
                var colors = new Color[row.hex.Length];
                for (int i = 0; i < row.hex.Length; i++)
                    colors[i] = ParseHex(row.hex[i]);
                palettes[row.name] = colors;
            }
        }

        public void LoadFromResources()
        {
            LoadPalettes(ArtTables.LoadFromResources().palettes);
            LoadOverridesFromResources();
        }
```
- Delete `ParsePaletteJson`.

**Step 4: Compile**

Coplay `check_compile_errors`: expect none. If `LoadFromJson` is still referenced somewhere, `grep -rn 'LoadFromJson' Assets/Scripts Assets/Tests` and convert it (AbilityTable/QuestZoneTable/NameGenerator have their own `LoadFromJson`; leave those).

**Step 5: Commit**

```bash
git add Assets/Scripts/Display/ColorManager.cs Assets/Tests/EditMode/Fixtures Assets/Tests/EditMode/Display/ColorManagerTests.cs Assets/Tests/EditMode/Destinations/ColorManagerFamilyTests.cs Assets/Tests/EditMode/Destinations/ColorManagerOverrideTests.cs Assets/Tests/EditMode/Equipment/LootDropperTests.cs Assets/Tests/EditMode/Destinations/TargetedRollTests.cs Assets/Tests/EditMode/Destinations/DungeonRewardMathTests.cs
git commit -m "refactor: color palettes load from baked art tables"
```

---

## Task 13: Display registry reads ArtTables; part picks use real variant numbers

**Files:**
- Modify: `Assets/Scripts/Display/DisplayDataRegistry.cs`
- Modify: `Assets/Scripts/Display/SpeciesInstanceData.cs:99-121`
- Modify: `Assets/Tests/EditMode/Display/DisplayBuilderTests.cs:43-46`
- Create: `Assets/Tests/EditMode/Display/DisplayDataRegistryTests.cs`

**Step 1: Write the failing tests**

`Assets/Tests/EditMode/Display/DisplayDataRegistryTests.cs`:
```csharp
using System;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Display;

namespace Starquill.Tests.Display
{
    public class DisplayDataRegistryTests
    {
        private static ArtTableData Data()
        {
            var d = new ArtTableData();
            d.palettes.Add(new PaletteRow { name = "main", hex = new[] { "FFFFFF" } });
            d.palettes.Add(new PaletteRow { name = "skin_naga", hex = new[] { "F3D9C2", "746C5B" } });
            d.partGroups.Add(new PartGroupRow { group = "f01", layers = new[] { 84 }, variants = new[] { 2, 7 } });
            d.species.Add(new SpeciesRow { name = "naga", eyes = "f01", skinPalette = "skin_naga", hairPalette = "" });
            d.skinVariance.Add(new SkinVarianceRow { species = "naga", layers = new[] { 37 }, palette = "skin_naga" });
            d.equipment.Add(new EquipmentRow { itemType = "hd01", description = "hat", layerCodes = new[] { 130 }, variants = new[] { 1, 3 } });
            d.weapons.Add(new WeaponRow { itemType = "w01", description = "sword", layerCodes = new[] { 164 },
                variantsPerLayer = new[] { new IntList(new[] { 1 }) } });
            return d;
        }

        [Test]
        public void Load_MapsSpeciesPalettesAndSkinVariance()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());

            var s = reg.Species["naga"];
            CollectionAssert.AreEqual(new[] { "skin_naga" }, s.SkinColor);
            Assert.IsNull(s.HairColor);
            Assert.AreEqual(1, s.SkinVarianceSets.Length);
            CollectionAssert.AreEqual(new[] { "F3D9C2", "746C5B" }, s.SkinVarianceSets[0].HexColors);
            CollectionAssert.AreEqual(new[] { 37 }, s.SkinVarianceSets[0].Indices);
        }

        [Test]
        public void Load_MapsPartGroupsAndEquipment()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());

            CollectionAssert.AreEqual(new[] { 84 }, reg.SpeciesLayerMappings["f01"]);
            CollectionAssert.AreEqual(new[] { 2, 7 }, reg.ModularPartVariants["f01"]);
            CollectionAssert.AreEqual(new[] { 1, 3 }, reg.Equipment["hd01"].Variants);
            Assert.IsTrue(reg.Equipment.ContainsKey("w01"));
        }

        [Test]
        public void PickModularImageNum_OnlyReturnsNumbersThatExist()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());
            var rng = new Random(1);

            var picks = Enumerable.Range(0, 500).Select(_ => reg.PickModularImageNum("f01", rng)).Distinct().OrderBy(x => x);

            CollectionAssert.AreEqual(new[] { "0002", "0007" }, picks);
        }
    }
}
```

**Step 2: Compile-check to see it fail**

Coplay `check_compile_errors`: errors for `Load`, `ModularPartVariants`, `Variants`. That is the failing state.

**Step 3: Implement**

In `DisplayDataRegistry.cs`:
- Add `using System.Linq; using Starquill.Data; using Starquill.Data.Art;`.
- `EquipmentDisplayData`: replace `public int Amount;` with `public int[] Variants = System.Array.Empty<int>();`.
- Replace `ModularPartCounts` with:
```csharp
        /// Part group -> variant numbers that exist on disk (gaps allowed).
        public Dictionary<string, int[]> ModularPartVariants { get; private set; } = new();
```
- Replace `LoadAll` through `ParseModularPartsJson` (all the `Load*`/`Parse*Json` methods and the hardcoded `LoadLayerMappings`) with:
```csharp
        public void LoadAll()
        {
            Load(ArtTables.LoadFromResources());
            Colors.LoadOverridesFromResources();
        }

        public void Load(ArtTableData data)
        {
            Species.Clear();
            Equipment.Clear();
            ModularPartVariants.Clear();
            SpeciesLayerMappings = new Dictionary<string, int[]>();
            Colors.LoadPalettes(data.palettes);

            var hexByPalette = data.palettes.ToDictionary(p => p.name, p => p.hex);

            foreach (var g in data.partGroups)
            {
                SpeciesLayerMappings[g.group] = g.layers;
                ModularPartVariants[g.group] = g.variants;
            }

            foreach (var e in data.equipment)
            {
                Equipment[e.itemType] = new EquipmentDisplayData
                {
                    ItemType = e.itemType, Description = e.description, Variants = e.variants,
                    LayerCodes = e.layerCodes, LayerColorVariance = e.colorVarianceLayers,
                    HiddenLayers = e.hiddenLayers, Modular = false
                };
            }

            foreach (var w in data.weapons)
            {
                Equipment[w.itemType] = new EquipmentDisplayData
                {
                    ItemType = w.itemType, Description = w.description,
                    LayerCodes = w.layerCodes, LayerColorVariance = w.colorVarianceLayers,
                    HiddenLayers = w.hiddenLayers, Modular = w.modular
                };
            }

            foreach (var s in data.species)
            {
                Species[s.name] = new SpeciesDisplayData
                {
                    Name = s.name,
                    BackArm = s.backArm, Body = s.body, Ears = s.ears, Eyes = s.eyes,
                    FacialDetail = s.facialDetail, FacialHair = s.facialHair, FrontArm = s.frontArm,
                    Head = s.head, Legs = s.legs, Mouth = s.mouth, Nose = s.nose,
                    Hair = s.hairGroups, OtherBodyParts = s.otherBodyParts, ItemRestrictions = s.itemRestrictions,
                    SkinColor = PaletteField(s.skinPalette),
                    HairColor = PaletteField(s.hairPalette),
                    EyesColor = PaletteField(s.eyePalette),
                    FacialDetailColor = PaletteField(s.facialDetailPalette),
                    SkinVarianceSets = data.skinVariance
                        .Where(v => v.species == s.name && hexByPalette.ContainsKey(v.palette))
                        .Select(v => new SkinVarianceSet { HexColors = hexByPalette[v.palette], Indices = v.layers })
                        .ToArray(),
                    XScale = s.xScale, YScale = s.yScale, HeadYOffset = s.headYOffset, HeadZoom = s.headZoom
                };
            }
        }

        /// Blank = null, which SpeciesInstanceData reads as "use main".
        private static string[] PaletteField(string palette) =>
            string.IsNullOrEmpty(palette) ? null : new[] { palette };
```
  Remove the now-unused `using Starquill.Core;` only if nothing else in the file needs it (SimpleJson was its only use).
- Replace `PickModularImageNum`:
```csharp
        public string PickModularImageNum(string groupType, System.Random rng = null)
        {
            if (!ModularPartVariants.TryGetValue(groupType, out var variants) || variants.Length == 0)
                return "0001";
            int i = rng != null ? rng.Next(variants.Length) : Random.Range(0, variants.Length);
            return variants[i].ToString("D4");
        }
```

In `SpeciesInstanceData.cs`, the hair weighting (two places) changes from `ModularPartCounts.TryGetValue(h, out int amt)` to:
```csharp
                        int amt = registry.ModularPartVariants.TryGetValue(h, out var hv) ? hv.Length : 0;
```

In `DisplayBuilderTests.cs:43-46` replace the four `ModularPartCounts[...] = 10;` lines with:
```csharp
            var ten = System.Linq.Enumerable.Range(1, 10).ToArray();
            registry.ModularPartVariants["f01"] = ten;
            registry.ModularPartVariants["f02"] = ten;
            registry.ModularPartVariants["f03"] = ten;
            registry.ModularPartVariants["h01"] = ten;
```

**Step 4: Compile**

Coplay `check_compile_errors`: none. `grep -rn 'ModularPartCounts\|ParseSpeciesJson\|ParseEquipmentJson' Assets` returns nothing.

**Step 5: Commit**

```bash
git add Assets/Scripts/Display/DisplayDataRegistry.cs Assets/Scripts/Display/SpeciesInstanceData.cs Assets/Tests/EditMode/Display/DisplayBuilderTests.cs Assets/Tests/EditMode/Display/DisplayDataRegistryTests.cs
git commit -m "refactor: display registry loads baked art tables; part picks use real variant numbers"
```

---

## Task 14: Equipment catalog + factory use variant sets

**Files:**
- Modify: `Assets/Scripts/Equipment/EquipmentCatalog.cs`
- Modify: `Assets/Scripts/Equipment/EquipmentFactory.cs:54,104-119,431-450`
- Modify: `Assets/Tests/EditMode/Fixtures/ArtFixtures.cs`
- Modify: `Assets/Tests/EditMode/Equipment/LootDropperTests.cs:21-41`
- Modify: `Assets/Tests/EditMode/Destinations/TargetedRollTests.cs:75-95`
- Modify: `Assets/Tests/EditMode/Destinations/DungeonRewardMathTests.cs:57-77`
- Modify: `Assets/Tests/EditMode/Equipment/EquipmentCatalogTests.cs` (rewrite)
- Modify: `Assets/Tests/EditMode/Equipment/EquipmentFactoryTests.cs` (add tests)

**Step 1: Fixture for the shared six-slot catalog**

Add to `ArtFixtures`:
```csharp
        /// The six-slot + one-sword catalog the loot and dungeon tests share
        /// (was three identical JSON copies). Variant numbers 1..n keep the
        /// old rng sequences, so seeded expectations are unchanged.
        public static Starquill.Equipment.EquipmentCatalog SixSlotCatalog()
        {
            var data = new ArtTableData();
            data.equipment.Add(Armor("hd01", "helmet", 10, 3));
            data.equipment.Add(Armor("tr01", "tunic", 20, 2));
            data.equipment.Add(Armor("ar01", "bracers", 30, 2));
            data.equipment.Add(Armor("lg01", "pants", 40, 2));
            data.equipment.Add(Armor("fe01", "boots", 45, 2));
            data.equipment.Add(Armor("mc01", "ring", 48, 2));
            data.weapons.Add(new WeaponRow
            {
                itemType = "w01", description = "sword", handType = "one_handed", modular = false,
                layerCodes = new[] { 50 }, variantsPerLayer = new[] { new IntList(new[] { 1, 2 }) }
            });
            var catalog = new Starquill.Equipment.EquipmentCatalog();
            catalog.Load(data);
            return catalog;
        }
```
In the three loot/dungeon test files, replace the `new EquipmentCatalog()` + `LoadEquipmentJson(...)` + `LoadWeaponsJson(...)` block with `var catalog = ArtFixtures.SixSlotCatalog();`.

**Step 2: Write the failing tests**

Rewrite `EquipmentCatalogTests.cs`:
```csharp
using NUnit.Framework;
using Starquill.Core;
using Starquill.Data.Art;
using Starquill.Equipment;
using Starquill.Tests.Fixtures;

namespace Starquill.Tests.Equipment
{
    public class EquipmentCatalogTests
    {
        [Test]
        public void LoadFromResources_HasRealItemsAndWeapons()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            Assert.AreEqual(49, catalog.AllEntries.Count);
            Assert.AreEqual(9, catalog.WeaponEntries.Count);
        }

        [Test]
        public void GetByPrefix_ReturnsOnlyThatPrefix()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            var torso = catalog.GetByPrefix("tr");
            Assert.Greater(torso.Count, 10);
            foreach (var item in torso) StringAssert.StartsWith("tr", item.ItemType);
        }

        [Test]
        public void SlotForPrefix_MapsCorrectly()
        {
            Assert.AreEqual(EquipmentSlot.Head, EquipmentCatalog.SlotForPrefix("hd"));
            Assert.AreEqual(EquipmentSlot.Torso, EquipmentCatalog.SlotForPrefix("tr"));
            Assert.AreEqual(EquipmentSlot.Arms, EquipmentCatalog.SlotForPrefix("ar"));
            Assert.AreEqual(EquipmentSlot.Legs, EquipmentCatalog.SlotForPrefix("lg"));
            Assert.AreEqual(EquipmentSlot.Feet, EquipmentCatalog.SlotForPrefix("fe"));
            Assert.AreEqual(EquipmentSlot.Misc1, EquipmentCatalog.SlotForPrefix("mc"));
            Assert.AreEqual(EquipmentSlot.MainHand, EquipmentCatalog.SlotForPrefix("w"));
        }

        [Test]
        public void WeaponHandTypeAndPerLayerVariants()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            var sword = catalog.GetWeapon("w01");
            Assert.AreEqual("one_handed", sword.HandType);
            Assert.IsTrue(sword.Modular);
            Assert.AreEqual(3, sword.VariantsPerLayer.Length);
            Assert.AreEqual("two_handed", catalog.GetWeapon("w04").HandType);
        }

        [Test]
        public void StaleCountsAreGone_FilesDecide()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            Assert.AreEqual(71, catalog.GetEntry("fe02").Variants.Length);
            Assert.AreEqual(35, catalog.GetEntry("hd08").Variants.Length);
            Assert.AreEqual(14, catalog.GetEntry("hd10").Variants.Length);
            Assert.AreEqual(17, catalog.GetEntry("tr14").Variants.Length);
        }

        [Test]
        public void ItemWithNoVariants_IsLeftOut()
        {
            var data = new ArtTableData();
            data.equipment.Add(ArtFixtures.Armor("hd01", "hat", 130, 2));
            data.equipment.Add(new EquipmentRow { itemType = "hd02", description = "no art yet", layerCodes = new[] { 130 } });
            var catalog = new EquipmentCatalog();
            catalog.Load(data);
            Assert.AreEqual(1, catalog.AllEntries.Count);
            Assert.IsNull(catalog.GetEntry("hd02"));
        }
    }
}
```

Add to `EquipmentFactoryTests.cs` (keep its existing `SetUp`, which uses `LoadFromResources`):
```csharp
        [Test]
        public void CreateRandom_NeverRollsAMissingVariant()
        {
            var data = new Starquill.Data.Art.ArtTableData();
            data.equipment.Add(new Starquill.Data.Art.EquipmentRow
            {
                itemType = "tr08", description = "coat", layerCodes = new[] { 14 }, variants = new[] { 102, 104 }
            });
            var catalog = new EquipmentCatalog();
            catalog.Load(data);
            var f = new EquipmentFactory(catalog, new AbilityTable(), Starquill.Tests.Fixtures.ArtFixtures.RgbColors());
            var rng = new System.Random(7);

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 5000; i++) seen.Add(f.CreateRandom("tr", Rarity.Common, rng).ItemNum);

            CollectionAssert.AreEquivalent(new[] { 102, 104 }, seen);
        }

        [Test]
        public void Reconstruct_WeaponVariantThatNoLongerExists_FallsBackToAnExistingOne()
        {
            // Shields (w08/w09) are not modular and keep no per-layer numbers.
            var rng = new System.Random(3);
            EquipmentInstance saved;
            do saved = factory.CreateRandomWeapon(Rarity.Common, rng);
            while (!catalog.GetWeapon(saved.ItemType).Modular);
            var data = SerializedEquipment.FromInstance(saved);
            data.layerVariants = new[] { 999, 999, 999 };

            var rebuilt = factory.Reconstruct(data);

            var entry = catalog.GetWeapon(rebuilt.ItemType);
            for (int i = 0; i < rebuilt.LayerVariants.Length; i++)
                CollectionAssert.Contains(entry.VariantsPerLayer[i], rebuilt.LayerVariants[i]);
        }
```
(Names verified 2026-11-08: `EquipmentFactoryTests` has fields `factory` and `catalog`; `EquipmentInstance.ItemNum`, `.ItemType`, `.LayerVariants`; `SerializedEquipment.FromInstance`, field `layerVariants`.)

**Step 3: Compile-check to see it fail**

Coplay `check_compile_errors`: errors for `Load`, `Variants`, `VariantsPerLayer`.

**Step 4: Implement**

`EquipmentCatalog.cs`:
- `CatalogEntry`: replace `public int Amount;` with `public int[] Variants;`.
- `WeaponEntry`: replace `public int[] AmountPerLayer;` with `public int[][] VariantsPerLayer;`.
- Add `using System.Linq; using Starquill.Data; using Starquill.Data.Art;`; remove `using Starquill.Core;` if unused (keep it: `EquipmentSlot` lives in Core).
- Replace `LoadFromResources`, `LoadEquipmentJson`, `LoadWeaponsJson` with:
```csharp
        public void LoadFromResources() => Load(ArtTables.LoadFromResources());

        public void Load(ArtTableData data)
        {
            AllEntries.Clear(); byType.Clear(); byPrefix.Clear();
            WeaponEntries.Clear(); weaponsByType.Clear();

            foreach (var row in data.equipment)
            {
                if (string.IsNullOrEmpty(row.itemType) || row.variants == null || row.variants.Length == 0)
                    continue; // no art yet: never rolled (the bake already warned)
                var entry = new CatalogEntry
                {
                    ItemType = row.itemType, Description = row.description, Variants = row.variants,
                    LayerCodes = row.layerCodes, HiddenLayers = row.hiddenLayers,
                    LayerColorVariance = row.colorVarianceLayers, Modular = false
                };
                AllEntries.Add(entry);
                byType[entry.ItemType] = entry;
                string prefix = entry.ItemType.Substring(0, 2);
                if (!byPrefix.TryGetValue(prefix, out var list)) byPrefix[prefix] = list = new List<CatalogEntry>();
                list.Add(entry);
            }

            foreach (var row in data.weapons)
            {
                var perLayer = row.variantsPerLayer.Select(l => l.values ?? System.Array.Empty<int>()).ToArray();
                if (string.IsNullOrEmpty(row.itemType) || perLayer.Length == 0 || perLayer.Any(v => v.Length == 0))
                    continue;
                var entry = new WeaponEntry
                {
                    ItemType = row.itemType, Description = row.description, VariantsPerLayer = perLayer,
                    LayerCodes = row.layerCodes, HiddenLayers = row.hiddenLayers,
                    LayerColorVariance = row.colorVarianceLayers, Modular = row.modular,
                    HandType = string.IsNullOrEmpty(row.handType) ? "one_handed" : row.handType
                };
                WeaponEntries.Add(entry);
                weaponsByType[entry.ItemType] = entry;
            }
        }
```

`EquipmentFactory.cs`:
- Line 54: `int itemNum = entry.Variants[rng.Next(entry.Variants.Length)];`
- Lines 104-119 become:
```csharp
            int itemNum;
            int[] layerVariants = null;
            if (entry.Modular && entry.VariantsPerLayer.Length > 0)
            {
                layerVariants = new int[entry.VariantsPerLayer.Length];
                for (int i = 0; i < layerVariants.Length; i++)
                {
                    var options = entry.VariantsPerLayer[i];
                    layerVariants[i] = options[rng.Next(options.Length)];
                }
                itemNum = layerVariants[0];
            }
            else
            {
                var options = entry.VariantsPerLayer[0];
                itemNum = options[rng.Next(options.Length)];
            }
```
- Lines 431-450 (Reconstruct) become:
```csharp
            var lv = data.layerVariants;
            if (weaponEntry != null && modular && weaponEntry.VariantsPerLayer.Length > 0)
            {
                // Keep saved numbers that still exist; anything missing (art
                // renumbered or deleted, or saves that predate per-layer data)
                // falls back to the first existing number, so a saved weapon
                // never asks for a sprite that is not there.
                var fixedLv = new int[weaponEntry.VariantsPerLayer.Length];
                for (int i = 0; i < fixedLv.Length; i++)
                {
                    var options = weaponEntry.VariantsPerLayer[i];
                    int v = lv != null && i < lv.Length ? lv[i] : -1;
                    fixedLv[i] = System.Array.IndexOf(options, v) >= 0 ? v : options[0];
                }
                lv = fixedLv;
            }
```

**Step 5: Compile + headless tests**

Coplay `check_compile_errors`: none. `grep -rn 'AmountPerLayer\|\.Amount\b\|LoadEquipmentJson\|LoadWeaponsJson' Assets` returns nothing. `dotnet test tools/ArtTablesTests`: all pass.

**CHECKPOINT B (Zach):**
- Test Runner > EditMode > Run All: all green. Note the new count.
- Play `ExploreScene` for a few waves: drops render; equip a few; Party screen dolls look right; Tavern recruits render (Shop > Tavern); no `Sprite not found` warnings.
- Load an existing save (don't clear it): previously owned weapons still render.

**Step 6: Commit**

```bash
git add Assets/Scripts/Equipment/EquipmentCatalog.cs Assets/Scripts/Equipment/EquipmentFactory.cs Assets/Tests/EditMode
git commit -m "refactor: equipment catalog loads baked art tables; rolls pick only variant numbers that exist"
```

---

## Task 15: Delete the art JSON

**Files:**
- Delete: `Assets/Resources/Data/{equipment,weapons,species,speciesModularParts,color_palettes}.json` (+ `.meta`)

**Step 1: Confirm nothing reads them**

Run: `grep -rnE 'Data/(equipment|weapons|species|speciesModularParts|color_palettes)\b' Assets/Scripts Assets/Editor Assets/Tests tools`
Expected: no matches (the migration script reads `DATA / "equipment.json"`; that is fine, it is a one-time tool, see Step 3).

**Step 2: Delete**

```bash
git rm Assets/Resources/Data/equipment.json Assets/Resources/Data/equipment.json.meta \
       Assets/Resources/Data/weapons.json Assets/Resources/Data/weapons.json.meta \
       Assets/Resources/Data/species.json Assets/Resources/Data/species.json.meta \
       Assets/Resources/Data/speciesModularParts.json Assets/Resources/Data/speciesModularParts.json.meta \
       Assets/Resources/Data/color_palettes.json Assets/Resources/Data/color_palettes.json.meta
```

**Step 3: Retire the migration script's `csv` step**

At the top of `cmd_csv` in `tools/migrate_art_tables.py` add:
```python
    if not (DATA / "equipment.json").exists():
        sys.exit("The art JSON was deleted after migration; the CSVs in Assets/Data/ArtTables are the source now.")
```

**Step 4: Verify a build runs the bake**

Ask Zach (or via the Editor if driving it): File > Build Profiles > Android (or Linux) > Build to a scratch folder. Expected in the Console before the build starts: `Art tables baked: ...; 0 error(s)`. Then temporarily break one cell (`hd01` `layer_codes` → `abc`), build again: the build fails with `Art tables have errors`. Revert the cell.

**Step 5: Commit**

```bash
git add tools/migrate_art_tables.py
git commit -m "chore: remove art JSON; CSV tables are the only source"
```

---

## Task 16: Python tools read the CSVs

**Files:**
- Modify: `tools/color_pools.py:7,28,68`
- Modify: `tools/README.md` (paths)

**Step 1: Change the palette source**

In `tools/color_pools.py`:
- Line 28: `PALETTES = ROOT / "Assets/Data/ArtTables/palettes.csv"`
- Line 68: replace `palette = json.loads(PALETTES.read_text())["main"]` with:
```python
    import csv
    with PALETTES.open(newline="", encoding="utf-8-sig") as f:
        palette = [row["hex"].lstrip("#").upper() for row in csv.DictReader(f) if row["palette"] == "main"]
```
- Update the module docstring's mention of `color_palettes.json` (line ~7 area) to `Assets/Data/ArtTables/palettes.csv`.

**Step 2: Verify**

Run: `python3 tools/color_pools.py --help 2>/dev/null || timeout 3 python3 tools/color_pools.py; echo exit $?`
Then start it normally (`python3 tools/color_pools.py`), open `http://localhost:8787`, confirm the pools show ~1027 swatches as before, stop it. Do not save overrides.

**Step 3: README**

In `tools/README.md` replace any `color_palettes.json` path with `Assets/Data/ArtTables/palettes.csv`; add one paragraph for `migrate_art_tables.py` ("one-time, already run 2026-11; kept for history") and for `tools/ArtTablesTests` ("`dotnet test tools/ArtTablesTests` runs the art table tests headless; `--filter Category=RepoData` checks the real CSVs and images").

**Step 4: Commit**

```bash
git add tools/color_pools.py tools/README.md
git commit -m "chore: color pool tool reads palettes.csv; document art table tools"
```

---

## Task 17: Docs

**Files:**
- Modify: `docs/implemented-systems.md` §1 (Assemblies), §6 (Display), §10 (Editor & tooling)
- Modify: `CLAUDE.md` (Data Loading, Editor Tools, Known Gotchas)
- Modify: `docs/plans/2026-11-08-character-studio-design.md` (status line: Phase A implemented)

**Step 1: Write the updates**

- `implemented-systems.md` §6: art rules live in `Assets/Data/ArtTables/*.csv` (six tables), baked by `ArtTablesBaker` into `Assets/Resources/ArtTables.asset` (generated, gitignored) on import and before builds; variant numbers come from files on disk, gaps allowed; one image folder per type; `ArtImageAudit` for duplicates/empty images; Tools > Art Tables menu.
- §10: replace `SpriteImportFixer` with the automatic import settings; add `tools/ArtTablesTests`, `tools/migrate_art_tables.py`.
- `CLAUDE.md` Data Loading: "Art data (equipment, weapons, species, part groups, palettes) is CSV in `Assets/Data/ArtTables/`, baked into `Resources/ArtTables.asset`; never add art counts by hand. Game data (abilities, names, quest zones) stays JSON in `Assets/Resources/Data/`." Known Gotchas: "`ArtTables.asset` is generated; if it is missing or stale, Tools > Art Tables > Bake Now"; "Art PNGs: one folder per type; move files with their `.meta`".

**Step 2: Commit**

```bash
git add docs/implemented-systems.md CLAUDE.md docs/plans/2026-11-08-character-studio-design.md
git commit -m "docs: art tables pipeline"
```

**CHECKPOINT C (Zach): Phase A done.** Test Runner all green; one more play session; then decide on starting Phase B.

---

## Phase B outline: Character Studio app (detailed plan after Phase A merges)

Prerequisite: Unity Hub > Installs > 6000.3.8f1 > Add modules > **Windows Build Support (Mono)**.

1. `Starquill.Studio` assembly (`Assets/Scripts/Studio/`, refs Core, Data, Display, Equipment, Characters) + `Assets/Scenes/CharacterStudio.unity`, never in the game's scene list. Two build profiles (Windows Mono, Linux) with `CharacterStudio` as the only scene.
2. `ImageResolver` gains a sprite source interface; a disk source loads PNGs from `<repo>/Assets/Resources/Images/...` (`Texture2D.LoadImage` + `Sprite.Create`, 100 PPU, center pivot, bilinear) with a cache cleared on Rescan.
3. Studio data load: first-launch repo folder picker (remembered); `ArtRepo.ReadSources` + `ArtTablesConverter.Convert` + `ArtImageAudit.Run` → `DisplayDataRegistry.Load`, `EquipmentCatalog.Load`; Warnings list.
4. Character model for the studio: `SpeciesInstanceData` + a list of `EquipmentInstance` built through `EquipmentFactory` (random) or directly (manual), with locks.
5. Rendering + export: `CharacterDisplay` at 200×scale texture size; `ReadPixels` → `EncodeToPNG`; trim + padding; pixel-match test against the game's 400px render.
6. Bulk screen (Godot randomizer port): count, species multi-select, Randomize All, Equip Random, search + Forced items chips, cards with Lock/reroll/species/skin/Edit, Settings window (slot chances, type weights, facial hair/detail chances).
7. Single screen: Body tab, Gear tab (type dropdown, thumbnail variant grid from existing numbers, color pickers, weapon part pickers, row reroll/Lock/✕, hidden-part notes), undo/redo, background toggle.
8. Presets (JSON next to the exe): Character, Cast, Random settings; missing art → empty slot + warning.
9. Library panel: new item type / species / part group, edit row (CsvTable write, one-line diff), remove (folder + `.meta` + row), Rescan, `.meta` template writer for new PNGs, Excel-lock and changed-on-disk handling.
10. GitHub Release packaging for the `.exe`; artist README with screenshots after testing.
