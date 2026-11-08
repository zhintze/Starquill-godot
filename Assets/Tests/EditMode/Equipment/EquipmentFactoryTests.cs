using System.Collections.Generic;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Data;
using Starquill.Display;
using Starquill.Equipment;
using UnityEngine;

namespace Starquill.Tests.Equipment
{
    [TestFixture]
    public class EquipmentFactoryTests
    {
        private EquipmentFactory factory;
        private EquipmentCatalog catalog;
        private AbilityTable abilityTable;
        private ColorManager colors;

        [SetUp]
        public void SetUp()
        {
            catalog = new EquipmentCatalog();
            catalog.LoadFromResources();

            abilityTable = new AbilityTable();
            abilityTable.LoadFromResources();

            colors = new ColorManager();
            colors.LoadFromResources();

            factory = new EquipmentFactory(catalog, abilityTable, colors);
        }

        [Test]
        public void CreateRandom_ProducesValidInstance()
        {
            var rng = new System.Random(42);
            var instance = factory.CreateRandom("tr", Rarity.Common, rng);
            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.ItemType.StartsWith("tr"));
            Assert.AreEqual(Rarity.Common, instance.Rarity);
            Assert.Greater(instance.ItemNum, 0);
        }

        [Test]
        public void CreateRandom_HasStatPair()
        {
            var rng = new System.Random(42);
            var instance = factory.CreateRandom("tr", Rarity.Rare, rng);
            Assert.Greater(instance.PrimaryValue, 0);
            Assert.Greater(instance.SecondaryValue, 0);
            Assert.AreNotEqual(instance.PrimaryStat, instance.SecondaryStat);
        }

        [Test]
        public void CreateRandom_PrimaryAlwaysGreaterThanSecondary()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new System.Random(seed);
                var instance = factory.CreateRandom("tr", Rarity.Rare, rng);
                Assert.Greater(instance.PrimaryValue, instance.SecondaryValue,
                    $"Seed {seed}: primary {instance.PrimaryValue} <= secondary {instance.SecondaryValue}");
            }
        }

        [Test]
        public void CreateRandom_HasAbility()
        {
            var rng = new System.Random(42);
            var instance = factory.CreateRandom("tr", Rarity.Rare, rng);
            Assert.IsNotNull(instance.Ability);
            Assert.AreEqual(1, instance.Ability.Level);
            Assert.AreEqual("ironhide", instance.Ability.AbilityId);
        }

        [Test]
        public void CreateRandom_SetsSlotFromPrefix()
        {
            var rng = new System.Random(42);
            Assert.AreEqual(EquipmentSlot.Torso, factory.CreateRandom("tr", Rarity.Common, rng).Slot);
            Assert.AreEqual(EquipmentSlot.Head, factory.CreateRandom("hd", Rarity.Common, rng).Slot);
            Assert.AreEqual(EquipmentSlot.Arms, factory.CreateRandom("ar", Rarity.Common, rng).Slot);
        }

        [Test]
        public void CreateRandomWeapon_SetsHandType()
        {
            var rng = new System.Random(42);
            var weapon = factory.CreateRandomWeapon(Rarity.Common, rng);
            Assert.IsNotNull(weapon);
            Assert.IsTrue(weapon.ItemType.StartsWith("w"));
            Assert.IsTrue(weapon.HandType == "one_handed" || weapon.HandType == "two_handed");
        }

        [Test]
        public void CreateRandomWeapon_HasAbility()
        {
            var rng = new System.Random(42);
            var weapon = factory.CreateRandomWeapon(Rarity.Rare, rng);
            Assert.IsNotNull(weapon.Ability);
        }

        [Test]
        public void GenerateStatPair_BudgetWithinRarityRange()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var (_, pv, _, sv) = EquipmentFactory.GenerateStatPair("tr", Rarity.Rare, new System.Random(seed));
                int total = pv + sv;
                Assert.GreaterOrEqual(total, 11, $"Seed {seed}: budget {total} < 11");
                Assert.LessOrEqual(total, 14, $"Seed {seed}: budget {total} > 14");
            }
        }

        [Test]
        public void GenerateStatPair_BudgetScalesWithQuestLevel()
        {
            // Rare base budget 11-14; at questLevel 50 with 1.5%/level: x1.75
            for (int seed = 0; seed < 30; seed++)
            {
                var (_, pv, _, sv) = EquipmentFactory.GenerateStatPair("tr", Rarity.Rare,
                    new System.Random(seed), questLevel: 50);
                int total = pv + sv;
                Assert.GreaterOrEqual(total, 19, $"Seed {seed}: {total}");
                Assert.LessOrEqual(total, 25, $"Seed {seed}: {total}");
            }
        }

        [Test]
        public void GenerateStatPair_StatsMustDiffer()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var (ps, _, ss, _) = EquipmentFactory.GenerateStatPair("tr", Rarity.Common, new System.Random(seed));
                Assert.AreNotEqual(ps, ss, $"Seed {seed}: primary == secondary ({ps})");
            }
        }

        [Test]
        public void CreateRandomLoadout_FillsPrioritySlots()
        {
            var rng = new System.Random(42);
            var loadout = factory.CreateRandomLoadout(1, rng);
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Torso], "Torso should always be filled");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Legs], "Legs should always be filled");
        }

        [Test]
        public void CreateStarterLoadout_FillsAllCoreSlots()
        {
            var loadout = factory.CreateStarterLoadout(new System.Random(42));
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Head], "Head");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Torso], "Torso");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Arms], "Arms");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Legs], "Legs");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Feet], "Feet");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.MainHand], "MainHand");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Misc1], "Misc1");
            Assert.IsNotNull(loadout[(int)EquipmentSlot.Misc2], "Misc2");
        }

        [Test]
        public void CreateStarterLoadout_AllCommonRarity()
        {
            var loadout = factory.CreateStarterLoadout(new System.Random(42));
            for (int i = 0; i < loadout.Length; i++)
                if (loadout[i] != null)
                    Assert.AreEqual(Rarity.Common, loadout[i].Rarity, $"Slot {i}");
        }

        [Test]
        public void RollRarity_AtLevel1_MostlyCommon()
        {
            var rng = new System.Random(42);
            int commonCount = 0;
            for (int i = 0; i < 100; i++)
                if (EquipmentFactory.RollRarity(1, rng) == Rarity.Common) commonCount++;
            Assert.Greater(commonCount, 60, "At level 1, >60% should be Common");
        }

        [Test]
        public void GenerateDisplayName_IncludesRarity()
        {
            var name = EquipmentFactory.GenerateDisplayName(Rarity.Rare, "shirt");
            Assert.IsTrue(name.Contains("Rare"));
        }

        [Test]
        public void RollRarityWithFloor_NullFloor_MatchesPlainRoll()
        {
            var a = EquipmentFactory.RollRarityWithFloor(5, null, new System.Random(7));
            var b = EquipmentFactory.RollRarity(5, new System.Random(7));
            Assert.AreEqual(b, a);
        }

        [Test]
        public void RollRarityWithFloor_NeverBelowFloor()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var r = EquipmentFactory.RollRarityWithFloor(1, Rarity.Rare, new System.Random(seed));
                Assert.GreaterOrEqual((int)r, (int)Rarity.Rare, $"seed {seed}");
            }
        }

        [Test]
        public void RollRarityWithFloor_LegendaryFloor_AlwaysLegendary()
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.AreEqual(Rarity.Legendary,
                    EquipmentFactory.RollRarityWithFloor(1, Rarity.Legendary, new System.Random(seed)));
        }

        [Test]
        public void CreateRandom_NeverRollsAMissingVariant()
        {
            var data = new Starquill.Data.Art.ArtTableData();
            data.equipment.Add(new Starquill.Data.Art.EquipmentRow
            {
                itemType = "tr08", description = "coat", layerCodes = new[] { 14 }, variants = new[] { 102, 104 }
            });
            var catalog = new EquipmentCatalog();
            catalog.Load(data);
            var f = new EquipmentFactory(catalog, new AbilityTable(), Starquill.Tests.Fixtures.ArtFixtures.RgbColors());
            var rng = new System.Random(7);

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 5000; i++) seen.Add(f.CreateRandom("tr", Rarity.Common, rng).ItemNum);

            CollectionAssert.AreEquivalent(new[] { 102, 104 }, seen);
        }

        [Test]
        public void Reconstruct_WeaponVariantThatNoLongerExists_FallsBackToAnExistingOne()
        {
            // Shields (w08/w09) are not modular and keep no per-layer numbers.
            var rng = new System.Random(3);
            EquipmentInstance saved;
            do saved = factory.CreateRandomWeapon(Rarity.Common, rng);
            while (!catalog.GetWeapon(saved.ItemType).Modular);
            var data = SerializedEquipment.FromInstance(saved);
            data.layerVariants = new[] { 999, 999, 999 };

            var rebuilt = factory.Reconstruct(data);

            var entry = catalog.GetWeapon(rebuilt.ItemType);
            for (int i = 0; i < rebuilt.LayerVariants.Length; i++)
                CollectionAssert.Contains(entry.VariantsPerLayer[i], rebuilt.LayerVariants[i]);
        }
    }
}
