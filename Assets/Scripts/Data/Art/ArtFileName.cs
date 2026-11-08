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
