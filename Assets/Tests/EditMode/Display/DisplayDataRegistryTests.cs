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
            d.skinVariance.Add(new SkinVarianceRow { species = "naga", layers = new[] { 37 }, palette = "skin_naga" });
            d.equipment.Add(new EquipmentRow { itemType = "hd01", description = "hat", layerCodes = new[] { 130 }, variants = new[] { 1, 3 } });
            d.weapons.Add(new WeaponRow { itemType = "w01", description = "sword", layerCodes = new[] { 164 },
                variantsPerLayer = new[] { new IntList(new[] { 1 }) } });
            return d;
        }

        [Test]
        public void Load_MapsSpeciesPalettesAndSkinVariance()
        {
            var reg = new DisplayDataRegistry();
            reg.Load(Data());

            var s = reg.Species["naga"];
            CollectionAssert.AreEqual(new[] { "skin_naga" }, s.SkinColor);
            Assert.IsNull(s.HairColor);
            Assert.AreEqual(1, s.SkinVarianceSets.Length);
            CollectionAssert.AreEqual(new[] { "F3D9C2", "746C5B" }, s.SkinVarianceSets[0].HexColors);
            CollectionAssert.AreEqual(new[] { 37 }, s.SkinVarianceSets[0].Indices);
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
