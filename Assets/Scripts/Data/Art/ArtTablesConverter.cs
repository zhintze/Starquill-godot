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
