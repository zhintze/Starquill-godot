using NUnit.Framework;
using Starquill.Core;
using Starquill.Data.Art;
using Starquill.Equipment;
using Starquill.Tests.Fixtures;

namespace Starquill.Tests.Equipment
{
    public class EquipmentCatalogTests
    {
        [Test]
        public void LoadFromResources_HasRealItemsAndWeapons()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            Assert.AreEqual(49, catalog.AllEntries.Count);
            Assert.AreEqual(9, catalog.WeaponEntries.Count);
        }

        [Test]
        public void GetByPrefix_ReturnsOnlyThatPrefix()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            var torso = catalog.GetByPrefix("tr");
            Assert.Greater(torso.Count, 10);
            foreach (var item in torso) StringAssert.StartsWith("tr", item.ItemType);
        }

        [Test]
        public void SlotForPrefix_MapsCorrectly()
        {
            Assert.AreEqual(EquipmentSlot.Head, EquipmentCatalog.SlotForPrefix("hd"));
            Assert.AreEqual(EquipmentSlot.Torso, EquipmentCatalog.SlotForPrefix("tr"));
            Assert.AreEqual(EquipmentSlot.Arms, EquipmentCatalog.SlotForPrefix("ar"));
            Assert.AreEqual(EquipmentSlot.Legs, EquipmentCatalog.SlotForPrefix("lg"));
            Assert.AreEqual(EquipmentSlot.Feet, EquipmentCatalog.SlotForPrefix("fe"));
            Assert.AreEqual(EquipmentSlot.Misc1, EquipmentCatalog.SlotForPrefix("mc"));
            Assert.AreEqual(EquipmentSlot.MainHand, EquipmentCatalog.SlotForPrefix("w"));
        }

        [Test]
        public void WeaponHandTypeAndPerLayerVariants()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            var sword = catalog.GetWeapon("w01");
            Assert.AreEqual("one_handed", sword.HandType);
            Assert.IsTrue(sword.Modular);
            Assert.AreEqual(3, sword.VariantsPerLayer.Length);
            Assert.AreEqual("two_handed", catalog.GetWeapon("w04").HandType);
        }

        [Test]
        public void StaleCountsAreGone_FilesDecide()
        {
            var catalog = new EquipmentCatalog();
            catalog.LoadFromResources();
            Assert.AreEqual(71, catalog.GetEntry("fe02").Variants.Length);
            Assert.AreEqual(35, catalog.GetEntry("hd08").Variants.Length);
            Assert.AreEqual(14, catalog.GetEntry("hd10").Variants.Length);
            Assert.AreEqual(17, catalog.GetEntry("tr14").Variants.Length);
        }

        [Test]
        public void ItemWithNoVariants_IsLeftOut()
        {
            var data = new ArtTableData();
            data.equipment.Add(ArtFixtures.Armor("hd01", "hat", 130, 2));
            data.equipment.Add(new EquipmentRow { itemType = "hd02", description = "no art yet", layerCodes = new[] { 130 } });
            var catalog = new EquipmentCatalog();
            catalog.Load(data);
            Assert.AreEqual(1, catalog.AllEntries.Count);
            Assert.IsNull(catalog.GetEntry("hd02"));
        }
    }
}
