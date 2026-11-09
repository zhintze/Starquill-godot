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
        public const string ExtraPartsFile = "species_extra_parts.csv";
        public const string PalettesFile = "palettes.csv";

        private static readonly Regex ItemTypeRx = new Regex(@"^[a-z]{2}\d{2}$");

        public static ArtBakeResult Convert(ArtSources src)
        {
            var r = new ArtBakeResult();
            var files = FileIndex.Build(src.ImageFiles, r.Issues);

            ConvertPalettes(Open(src.PalettesCsv, PalettesFile, r), r);
            ConvertEquipment(Open(src.EquipmentCsv, EquipmentFile, r), files, r);
            ConvertWeapons(Open(src.WeaponsCsv, WeaponsFile, r), files, r);
            ConvertPartGroups(Open(src.SpeciesPartsCsv, SpeciesPartsFile, r), files, r);
            ConvertSpecies(Open(src.SpeciesCsv, SpeciesFile, r), files, r);
            ConvertExtraParts(Open(src.ExtraPartsCsv, ExtraPartsFile, r), files, r);

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

        private static readonly Regex BodyTokenRx = new Regex(@"^\d{4}-\d{3}$");
        private static readonly Regex FullPartTokenRx = new Regex(@"^([a-z]\d{2})-(\d{4})-(\d{3})$");

        public static readonly string[] PartColumns =
            { "back_arm", "legs", "body", "head", "front_arm", "eyes", "nose", "mouth", "ears", "facial_hair", "facial_detail" };

        private static void ConvertSpecies(Table t, FileIndex files, ArtBakeResult r)
        {
            var required = new List<string> { "name", "x_scale", "y_scale", "head_y_offset", "head_zoom" };
            required.AddRange(PartColumns);
            required.AddRange(new[] { "hair_groups", "skin_palette", "hair_palette", "eye_palette", "facial_detail_palette", "item_restrictions" });
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
                if (t.Text(i, "other_body_parts").Length > 0)
                {
                    t.Error(i, $"other_body_parts moved to {ExtraPartsFile} (one row per part); delete this column");
                    ok = false;
                }

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
                    hairGroups = hair, itemRestrictions = restrictions,
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

        public static readonly string[] ColorSources = { "skin", "hair", "eyes", "facial_detail" };

        /// species_extra_parts.csv (plan E1-E3): extras and own-colored species parts.
        private static void ConvertExtraParts(Table t, FileIndex files, ArtBakeResult r)
        {
            if (t == null || !t.Require("species", "part", "color", "color_group", "chance")) return;
            var species = new HashSet<string>(r.Data.species.Select(s => s.name));
            var groups = new HashSet<string>(r.Data.partGroups.Select(g => g.group));
            var palettes = new HashSet<string>(r.Data.palettes.Select(p => p.name));
            var seen = new HashSet<(string, string)>();
            var groupColors = new Dictionary<(string, string), string>();

            for (int i = 0; i < t.Count; i++)
            {
                string name = t.Text(i, "species"), part = t.Text(i, "part"), color = t.Text(i, "color"), group = t.Text(i, "color_group");
                if (!species.Contains(name)) { t.Error(i, $"species '{name}' is not in species.csv"); continue; }
                if (part.Length == 0) { t.Error(i, "part is blank"); continue; }
                if (!seen.Add((name, part))) { t.Error(i, $"{name} lists part {part} twice"); continue; }

                bool speciesPart = PartColumns.Contains(part);
                if (!speciesPart)
                {
                    if (!BodyTokenRx.IsMatch(part) && !GroupRx.IsMatch(part) && !FullPartTokenRx.IsMatch(part))
                    {
                        t.Error(i, $"part '{part}' must be a species.csv part column (legs, mouth, ...) or look like 0001-016, f01 or f01-0001-084");
                        continue;
                    }
                    if (!CheckPartToken(t, i, "part", part, files, groups)) continue;
                }

                if (!ColorSources.Contains(color) && !palettes.Contains(color))
                {
                    t.Error(i, $"color '{color}' must be skin, hair, eyes, facial_detail or a palette in palettes.csv");
                    continue;
                }
                if (group.Length > 0)
                {
                    if (groupColors.TryGetValue((name, group), out var other) && other != color)
                    {
                        t.Error(i, $"color group {group} of {name} names two colors ({other} and {color})");
                        continue;
                    }
                    groupColors[(name, group)] = color;
                }

                string chanceText = t.Text(i, "chance");
                if (!ArtCells.TryFloat(chanceText, 1f, out float chance) || chance < 0f || chance > 1f)
                {
                    t.Error(i, $"chance '{chanceText}' must be blank (always) or a number from 0 to 1");
                    continue;
                }
                if (speciesPart && chanceText.Length > 0)
                {
                    t.Error(i, $"chance is only for extra parts; {part} is part of the species");
                    continue;
                }

                r.Data.speciesExtraParts.Add(new SpeciesExtraPartRow
                {
                    species = name, part = part, color = color, colorGroup = group, chance = chance
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
