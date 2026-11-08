using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioRollerTests
    {
        private static StudioData D => StudioTestRepo.Data;
        private static StudioRoller Roller => new StudioRoller(D);

        private static RandomSettings Never()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 0f;
            return s;
        }

        private static RandomSettings Always()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            s.FacialHair = s.FacialDetail = 1f;
            return s;
        }

        [Test]
        public void ChancesZero_NoGear()
        {
            var c = Roller.RollCharacter(new[] { "human" }, Never(), new System.Random(1));
            Assert.AreEqual(0, c.Gear.Count);
            Assert.AreEqual("human", c.SpeciesName);
            Assert.IsNotNull(c.Body);
        }

        [Test]
        public void ChancesOne_FillsArmorMainHandAndMisc()
        {
            var c = Roller.RollCharacter(new[] { "human" }, Always(), new System.Random(2));
            foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Torso, EquipmentSlot.Arms, EquipmentSlot.Legs,
                         EquipmentSlot.Feet, EquipmentSlot.MainHand, EquipmentSlot.Misc1, EquipmentSlot.Misc2,
                         EquipmentSlot.Misc3, EquipmentSlot.Misc4 })
                Assert.IsTrue(c.Gear.ContainsKey(slot), slot.ToString());
            StringAssert.StartsWith("mc", c.Gear[EquipmentSlot.Misc1].ItemType);
        }

        [Test]
        public void TwoHandedMainHand_NeverHasOffHand_ShieldsNeverInMainHand()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 500; i++)
            {
                var c = Roller.RollCharacter(new[] { "human" }, Always(), rng);
                var main = D.Catalog.GetWeapon(c.Gear[EquipmentSlot.MainHand].ItemType);
                Assert.IsFalse(main.ItemType == "w08" || main.ItemType == "w09");
                if (main.HandType == "two_handed") Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.OffHand));
                else Assert.IsTrue(c.Gear.ContainsKey(EquipmentSlot.OffHand));
            }
        }

        [Test]
        public void EveryRolledLayer_HasItsImageOnDisk()
        {
            var rng = new System.Random(4);
            for (int i = 0; i < 300; i++)
            {
                var c = Roller.RollCharacter(D.SpeciesNames, Always(), rng);
                foreach (var kv in c.Gear)
                {
                    var item = kv.Value;
                    var weapon = D.Catalog.GetWeapon(item.ItemType);
                    var codes = weapon != null ? weapon.LayerCodes : D.Catalog.GetEntry(item.ItemType).LayerCodes;
                    for (int l = 0; l < codes.Length; l++)
                    {
                        string path = weapon != null
                            ? ImageToken.BuildWeaponSpritePath(item.ItemType, codes[l], item.LayerVariants != null ? item.LayerVariants[l] : item.ItemNum)
                            : ImageToken.BuildEquipmentSpritePath(item.ItemType, item.ItemNum, codes[l]);
                        Assert.IsTrue(File.Exists(DiskSpriteSource.FilePathFor(StudioTestRepo.Root, path)), path);
                    }
                }
            }
        }

        [Test]
        public void ItemRestrictions_AreNeverRolled()
        {
            var restricted = D.Registry.Species["stone golem"].ItemRestrictions;
            var rng = new System.Random(5);
            for (int i = 0; i < 300; i++)
            {
                var c = Roller.RollCharacter(new[] { "stone golem" }, Always(), rng);
                foreach (var item in c.Gear.Values) CollectionAssert.DoesNotContain(restricted, item.ItemType);
            }
        }

        [Test]
        public void TypeWeightZero_IsNeverPicked()
        {
            var s = Always();
            foreach (var e in D.Catalog.GetByPrefix("hd").Where(e => e.ItemType != "hd03")) s.TypeWeights[e.ItemType] = 0f;
            var rng = new System.Random(6);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual("hd03", Roller.RollCharacter(new[] { "human" }, s, rng).Gear[EquipmentSlot.Head].ItemType);
        }

        [Test]
        public void FacialChances_HideOrShowTheParts()
        {
            var s = Always();
            s.FacialHair = 0f;
            var c = Roller.RollCharacter(new[] { "human" }, s, new System.Random(7));
            Assert.IsTrue(c.HideFacialHair);
            Assert.IsFalse(c.HideFacialDetail);
        }

        [Test]
        public void Gear_UsesOnlyTheChosenSpecies()
        {
            var rng = new System.Random(8);
            var picks = Enumerable.Range(0, 200).Select(_ => Roller.RollCharacter(new[] { "dwarf", "naga" }, Never(), rng).SpeciesName).Distinct().OrderBy(x => x);
            CollectionAssert.AreEqual(new[] { "dwarf", "naga" }, picks);
        }
    }
}
