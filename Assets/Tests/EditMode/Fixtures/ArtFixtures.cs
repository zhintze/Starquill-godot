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
    }
}
