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
