using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Starquill.Studio
{
    /// Writes Unity .meta files for new art (P33), so a PNG the artist adds
    /// gets the game's sprite import settings, not Unity's defaults.
    public static class MetaWriter
    {
        public const string FolderMeta =
            "fileFormatVersion: 2\nguid: {0}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private static readonly Regex GuidLine = new Regex(@"^guid: [0-9a-f]+", RegexOptions.Multiline);

        public static string NewGuid() => Guid.NewGuid().ToString("N");

        /// An existing art PNG .meta to copy, or null.
        public static string FindPngTemplate(string imagesRoot) =>
            Directory.Exists(imagesRoot)
                ? Directory.EnumerateFiles(imagesRoot, "*.png.meta", SearchOption.AllDirectories).FirstOrDefault()
                : null;

        /// Writes the missing .meta for every folder and PNG below imagesRoot.
        /// Returns how many it wrote. PNGs are skipped when no template exists.
        public static int WriteMissing(string imagesRoot)
        {
            if (!Directory.Exists(imagesRoot)) return 0;
            int written = 0;
            foreach (var dir in Directory.EnumerateDirectories(imagesRoot, "*", SearchOption.AllDirectories))
            {
                if (File.Exists(dir + ".meta")) continue;
                File.WriteAllText(dir + ".meta", string.Format(FolderMeta, NewGuid()));
                written++;
            }
            string templatePath = FindPngTemplate(imagesRoot);
            if (templatePath == null) return written;
            string template = File.ReadAllText(templatePath);
            foreach (var png in Directory.EnumerateFiles(imagesRoot, "*.png", SearchOption.AllDirectories))
            {
                if (File.Exists(png + ".meta")) continue;
                File.WriteAllText(png + ".meta", GuidLine.Replace(template, "guid: " + NewGuid(), 1));
                written++;
            }
            return written;
        }
    }
}
