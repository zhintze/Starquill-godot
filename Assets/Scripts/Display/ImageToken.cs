using System.Text.RegularExpressions;

namespace Starquill.Display
{
    public enum ImageTokenKind
    {
        Empty,
        Static,
        ModularFull,
        ModularGroup
    }

    public class ImageToken
    {
        private static readonly Regex StaticRegex = new(@"^\d{4}-\d{3}$");
        private static readonly Regex ModularFullRegex = new(@"^[A-Za-z]\d{2}-\d{4}-\d{3}$");
        private static readonly Regex ModularGroupRegex = new(@"^[A-Za-z]\d{2}$");

        public ImageTokenKind Kind { get; private set; }
        public string GroupType { get; private set; }
        public string ImageNum { get; private set; }
        public int Layer { get; private set; } = -1;
        public string RawToken { get; private set; }

        private ImageToken() { }

        public static ImageToken Parse(string token)
        {
            var result = new ImageToken();

            if (string.IsNullOrEmpty(token))
            {
                result.Kind = ImageTokenKind.Empty;
                result.RawToken = token ?? "";
                return result;
            }

            result.RawToken = token;

            if (StaticRegex.IsMatch(token))
            {
                result.Kind = ImageTokenKind.Static;
                var parts = token.Split('-');
                result.ImageNum = parts[0];
                result.Layer = int.Parse(parts[1]);
            }
            else if (ModularFullRegex.IsMatch(token))
            {
                result.Kind = ImageTokenKind.ModularFull;
                var parts = token.Split('-');
                result.GroupType = parts[0];
                result.ImageNum = parts[1];
                result.Layer = int.Parse(parts[2]);
            }
            else if (ModularGroupRegex.IsMatch(token))
            {
                result.Kind = ImageTokenKind.ModularGroup;
                result.GroupType = token;
            }
            else
            {
                result.Kind = ImageTokenKind.Empty;
            }

            return result;
        }

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
    }
}
