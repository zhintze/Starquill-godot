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
                ExtraPartsCsv = ReadOrNull(tables, ArtTablesConverter.ExtraPartsFile),
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
