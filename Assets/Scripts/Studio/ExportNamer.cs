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
