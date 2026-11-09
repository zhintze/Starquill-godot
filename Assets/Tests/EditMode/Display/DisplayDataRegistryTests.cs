using System;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Display;

namespace Starquill.Tests.Display
{
    public class DisplayDataRegistryTests
    {
        private static ArtTableData Data()
        {
            var d = new ArtTableData();
            d.palettes.Add(new PaletteRow { name = "main", hex = new[] { "FFFFFF" } });
            d.palettes.Add(new PaletteRow { name = "skin_naga", hex = new[] { "F3D9C2", "746C5B" } });
            d.partGroups.Add(new PartGroupRow { group = "f01", layers = new[] { 84 }, variants = new[] { 2, 7 } });
            d.species.Add(new SpeciesRow { name = "naga", eyes = "f01", skinPalette = "skin_naga", hairPalette = "" });
            d.speciesExtraParts.Add(new SpeciesExtraPartRow { species = "naga", part = "legs", color = "skin_naga", colorGroup = "" });
            d.speciesExtraParts.Add(new SpeciesExtraPartRow { species = "naga", part = "0003-002", color = "hair", colorGroup = "", chance = 0.5f });
            d.equipment.Add(new EquipmentRow { itemType = "hd01", description = "hat", layerCodes = new[] { 130 }, variants = new[] { 1, 3 } });
            d.weapons.Add(new WeaponRow { itemType = "w01", description = "sword", layerCodes = new[] { 164 },
                variantsPerLayer = new[] { new IntList(new[] { 1 }) } });
            return d;
        }

        [Test]
        public void Load_MapsSpeciesPalettesAndPartRules()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());

            var s = reg.Species["naga"];
            CollectionAssert.AreEqual(new[] { "skin_naga" }, s.SkinColor);
            Assert.IsNull(s.HairColor);
            Assert.AreEqual(2, s.PartRules.Length);
            var legs = s.PartRules[0];
            Assert.AreEqual("legs", legs.Part);
            Assert.IsFalse(legs.IsExtra);
            Assert.AreEqual("legs", legs.ColorKey);
            CollectionAssert.AreEqual(new[] { ColorManager.ParseHex("F3D9C2"), ColorManager.ParseHex("746C5B") }, legs.Palette);
            var extra = s.PartRules[1];
            Assert.IsTrue(extra.IsExtra);
            Assert.AreEqual("hair", extra.ColorSource);
            Assert.IsNull(extra.Palette);
            Assert.AreEqual(0.5f, extra.Chance);
        }

        [Test]
        public void Load_MapsPartGroupsAndEquipment()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());

            CollectionAssert.AreEqual(new[] { 84 }, reg.SpeciesLayerMappings["f01"]);
            CollectionAssert.AreEqual(new[] { 2, 7 }, reg.ModularPartVariants["f01"]);
            CollectionAssert.AreEqual(new[] { 1, 3 }, reg.Equipment["hd01"].Variants);
            Assert.IsTrue(reg.Equipment.ContainsKey("w01"));
        }

        [Test]
        public void PickModularImageNum_OnlyReturnsNumbersThatExist()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());
            var rng = new Random(1);

            var picks = Enumerable.Range(0, 500).Select(_ => reg.PickModularImageNum("f01", rng)).Distinct().OrderBy(x => x);

            CollectionAssert.AreEqual(new[] { "0002", "0007" }, picks);
        }
    }
}
