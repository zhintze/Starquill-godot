using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Starquill.Data.Art;

namespace Starquill.Studio
{
    public enum TableKind { ItemTypes, Weapons, Species, PartGroups, ExtraParts }

    /// What a Library save or remove did.
    public sealed class LibraryResult
    {
        public bool Ok;
        public string Message;
        /// The file changed on disk since it was read; nothing was written (P31).
        public bool ChangedOnDisk;
        /// Errors the change would add; nothing was written (P30).
        public List<ArtIssue> Errors = new List<ArtIssue>();
    }

    /// Adds, edits and removes rows of the art tables in a repo clone, with
    /// the Phase A converter as the judge (P29-P32). One row change is one
    /// changed line in the CSV.
    public sealed class LibraryEditor
    {
        private static readonly string[] ArmorPrefixes = { "hd", "tr", "ar", "lg", "fe", "mc" };

        public string RepoRoot { get; }
        public string ImagesRoot => Path.Combine(RepoRoot, ArtRepo.ImagesDir);
        private readonly Dictionary<TableKind, TableFile> files = new Dictionary<TableKind, TableFile>();

        public LibraryEditor(string repoRoot) { RepoRoot = repoRoot; }

        public static string FileName(TableKind kind)
        {
            switch (kind)
            {
                case TableKind.ItemTypes: return ArtTablesConverter.EquipmentFile;
                case TableKind.Weapons: return ArtTablesConverter.WeaponsFile;
                case TableKind.Species: return ArtTablesConverter.SpeciesFile;
                case TableKind.ExtraParts: return ArtTablesConverter.ExtraPartsFile;
                default: return ArtTablesConverter.SpeciesPartsFile;
            }
        }

        /// Image folder for a row, relative to Assets/Resources/Images; null for species
        /// (species draw from part groups and species/body).
        public static string FolderOf(TableKind kind, string key)
        {
            switch (kind)
            {
                case TableKind.ItemTypes: return "equipment/" + key;
                case TableKind.Weapons: return "weapons/" + key;
                case TableKind.PartGroups: return "species/" + key;
                default: return null;
            }
        }

        public string FolderPath(TableKind kind, string key)
        {
            string rel = FolderOf(kind, key);
            return rel == null ? null : Path.Combine(ImagesRoot, rel.Replace('/', Path.DirectorySeparatorChar));
        }

        public TableFile Open(TableKind kind)
        {
            if (!files.TryGetValue(kind, out var f))
                files[kind] = f = new TableFile(Path.Combine(RepoRoot, ArtRepo.TablesDir, FileName(kind)));
            return f;
        }

        public void ReloadAll()
        {
            foreach (var f in files.Values) f.Reload();
        }

        /// Columns that make a row's key: species + part for extra parts, else the code.
        public static int KeyColumns(TableKind kind) => kind == TableKind.ExtraParts ? 2 : 1;

        public static string KeyOf(TableKind kind, string[] row) =>
            kind == TableKind.ExtraParts ? $"{row[0]} / {(row.Length > 1 ? row[1] : "")}" : row[0];

        /// Next free code with this prefix: hd -> hd14 when hd01-hd13 exist.
        public string NextCode(TableKind kind, string prefix)
        {
            int max = 0;
            foreach (var row in Open(kind).Table.Rows)
            {
                string key = row[0];
                if (key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.Substring(prefix.Length), out int n))
                    max = Math.Max(max, n);
            }
            return prefix + (max + 1).ToString("D2");
        }

        /// Null when the key is fine for that table.
        public static string CheckKey(TableKind kind, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "The code (first field) is empty.";
            switch (kind)
            {
                case TableKind.ItemTypes:
                    if (!Regex.IsMatch(key, @"^[a-z]{2}\d{2}$") || !ArmorPrefixes.Contains(key.Substring(0, 2)))
                        return $"'{key}' is not an item code: two letters for the slot (hd tr ar lg fe mc) and two digits, like hd14.";
                    return null;
                case TableKind.Weapons:
                    return Regex.IsMatch(key, @"^w\d{2}$") ? null : $"'{key}' is not a weapon code: w and two digits, like w10.";
                case TableKind.PartGroups:
                    return Regex.IsMatch(key, @"^[a-z]\d{2}$") ? null : $"'{key}' is not a part group code: one letter and two digits, like f07.";
                case TableKind.ExtraParts:
                    return key.StartsWith(" /") || key.EndsWith("/ ") ? "Species and part must both be filled in." : null;
                default:
                    return Regex.IsMatch(key, @"^[a-z][a-z \-]*$") ? null : $"'{key}' is not a species name: lowercase letters, spaces and dashes.";
            }
        }

        /// Adds (originalKey null) or replaces a row. Nothing is written when
        /// the result is not Ok.
        public LibraryResult Save(TableKind kind, string originalKey, string[] row, bool overwriteChangedFile)
        {
            var file = Open(kind);
            var result = new LibraryResult();
            if (file.ChangedOnDisk && !overwriteChangedFile)
            {
                result.ChangedOnDisk = true;
                result.Message = $"{FileName(kind)} changed on disk since the studio read it.";
                return result;
            }

            var cells = Normalize(row, file.Table.Header.Length);
            string key = KeyOf(kind, cells);
            string keyProblem = CheckKey(kind, key);
            if (keyProblem != null) return Fail(result, keyProblem);

            var table = Copy(file.Table);
            int index = originalKey == null ? -1 : Find(kind, table, originalKey);
            if (originalKey != null && index < 0) return Fail(result, $"{originalKey} is no longer in {FileName(kind)}; press Rescan.");
            if (originalKey != null && key != originalKey) return Fail(result, "A code cannot be renamed; remove it and add a new one.");
            if (originalKey == null && Find(kind, table, key) >= 0) return Fail(result, $"{key} already exists in {FileName(kind)}.");

            if (index >= 0) table.Rows[index] = cells;
            else table.Rows.Add(cells);

            string text = table.ToCsv();
            if (!Check(kind, text, result)) return result;
            if (!Write(file, text, result)) return result;

            string folder = FolderPath(kind, key);
            if (folder != null && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                MetaWriter.WriteMissing(ImagesRoot);
            }
            result.Ok = true;
            result.Message = originalKey == null
                ? $"Added {key} to {FileName(kind)}." + (folder != null ? $" Put its PNGs in {folder}, then press Rescan." : "")
                : $"Saved {key} in {FileName(kind)}.";
            return result;
        }

        /// Removes the row and its image folder (P32).
        public LibraryResult Remove(TableKind kind, string key, bool overwriteChangedFile)
        {
            var file = Open(kind);
            var result = new LibraryResult();
            if (file.ChangedOnDisk && !overwriteChangedFile)
            {
                result.ChangedOnDisk = true;
                result.Message = $"{FileName(kind)} changed on disk since the studio read it.";
                return result;
            }
            var table = Copy(file.Table);
            int index = Find(kind, table, key);
            if (index < 0) return Fail(result, $"{key} is not in {FileName(kind)}.");
            table.Rows.RemoveAt(index);

            string text = table.ToCsv();
            if (!Check(kind, text, result)) return result;
            if (!Write(file, text, result)) return result;

            string folder = FolderPath(kind, key);
            int images = 0;
            if (folder != null && Directory.Exists(folder))
            {
                images = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).Length;
                Directory.Delete(folder, true);
            }
            if (folder != null && File.Exists(folder + ".meta")) File.Delete(folder + ".meta");
            result.Ok = true;
            result.Message = $"Removed {key} from {FileName(kind)}" + (folder != null ? $" and its folder ({images} {(images == 1 ? "image" : "images")})." : ".");
            return result;
        }

        /// Number of PNGs a remove would delete.
        public int ImageCount(TableKind kind, string key)
        {
            string folder = FolderPath(kind, key);
            return folder != null && Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).Length : 0;
        }

        // ---- helpers ----

        private static LibraryResult Fail(LibraryResult r, string message)
        {
            r.Message = message;
            return r;
        }

        private static string[] Normalize(string[] row, int width)
        {
            var cells = new string[width];
            for (int i = 0; i < width; i++) cells[i] = i < row.Length ? (row[i] ?? "").Trim() : "";
            return cells;
        }

        private static CsvTable Copy(CsvTable t)
        {
            var copy = new CsvTable(t.Header);
            foreach (var row in t.Rows) copy.Rows.Add((string[])row.Clone());
            return copy;
        }

        private static int Find(TableKind kind, CsvTable t, string key)
        {
            for (int i = 0; i < t.Rows.Count; i++) if (KeyOf(kind, t.Rows[i]) == key) return i;
            return -1;
        }

        /// P30: refuse a change that adds converter errors.
        private bool Check(TableKind kind, string newText, LibraryResult result)
        {
            var before = ArtTablesConverter.Convert(ArtRepo.ReadSources(RepoRoot)).Issues
                .Where(i => i.level == ArtIssueLevel.Error).ToList();
            var sources = ArtRepo.ReadSources(RepoRoot);
            switch (kind)
            {
                case TableKind.ItemTypes: sources.EquipmentCsv = newText; break;
                case TableKind.Weapons: sources.WeaponsCsv = newText; break;
                case TableKind.Species: sources.SpeciesCsv = newText; break;
                case TableKind.ExtraParts: sources.ExtraPartsCsv = newText; break;
                default: sources.SpeciesPartsCsv = newText; break;
            }
            var after = ArtTablesConverter.Convert(sources).Issues.Where(i => i.level == ArtIssueLevel.Error).ToList();
            if (after.Count <= before.Count) return true;
            var added = after.Where(a => !before.Any(b => b.message == a.message)).ToList();
            result.Errors.AddRange(added.Count > 0 ? added : after);
            result.Message = $"Not saved: the change adds {result.Errors.Count} error(s).";
            return false;
        }

        private bool Write(TableFile file, string text, LibraryResult result)
        {
            try
            {
                file.Write(text);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                result.Message = $"Could not write {System.IO.Path.GetFileName(file.Path)}: close it in Excel (or any program that has it open) and press Save again. Your edit is still in the form. ({e.Message})";
                return false;
            }
        }
    }
}
