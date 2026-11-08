using System.Collections.Generic;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Data;
using Starquill.Destinations;
using Starquill.Display;
using Starquill.Tests.Fixtures;
using Starquill.Equipment;
using Starquill.Managers;
using UnityEngine;

namespace Starquill.Tests.Destinations
{
    public class DungeonRewardMathTests
    {
        private EconomyConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<EconomyConfig>();
            config.dungeonBonusWavesPerRoll = 3;
            config.dungeonBonusRollCap = 3;
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null) Object.DestroyImmediate(config);
        }

        private static DungeonSpec MakeSpec(KeyInstance key = null, int baseRolls = 3,
            int parWaves = 10, Rarity floor = Rarity.Epic)
        {
            return new DungeonSpec
            {
                Key = key ?? new KeyInstance(),
                Seed = 1,
                QuestLevel = 10,
                DurationSeconds = 150f,
                EnemyHpMultiplier = 1f,
                RarityFloor = floor,
                LegendaryWeightMult = 1f,
                BaseRolls = baseRolls,
                ParWaves = parWaves
            };
        }

        private static DungeonRun MakeRun(DungeonSpec spec, int wavesCleared)
        {
            var run = new DungeonRun(spec);
            for (int i = 0; i < wavesCleared; i++) run.WaveCleared();
            return run;
        }

        private static EquipmentFactory MakeFactory()
        {
            var catalog = ArtFixtures.SixSlotCatalog();

            var abilityTable = new AbilityTable();
            var colors = ArtFixtures.RgbColors();
            return new EquipmentFactory(catalog, abilityTable, colors);
        }

        // --- Roll-count math ---

        [Test]
        public void Roll_AtOrBelowPar_UsesBaseRollsOnly()
        {
            var run = MakeRun(MakeSpec(baseRolls: 3, parWaves: 10), wavesCleared: 10);
            var items = DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(7), out _);
            Assert.AreEqual(3, items.Count);
        }

        [Test]
        public void Roll_OverPar_AddsOneBonusRollPerThreeWaves()
        {
            // 7 waves over par / 3 per roll = 2 bonus rolls
            var run = MakeRun(MakeSpec(baseRolls: 3, parWaves: 10), wavesCleared: 17);
            var items = DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(7), out _);
            Assert.AreEqual(5, items.Count);
        }

        [Test]
        public void Roll_BonusRolls_AreCapped()
        {
            // 30 waves over par would be 10 bonus rolls; cap is 3
            var run = MakeRun(MakeSpec(baseRolls: 3, parWaves: 10), wavesCleared: 40);
            var items = DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(7), out _);
            Assert.AreEqual(6, items.Count);
        }

        // --- Rarity floor ---

        [Test]
        public void Roll_AllItemsRespectRarityFloor()
        {
            var run = MakeRun(MakeSpec(floor: Rarity.Epic), wavesCleared: 20);
            var items = DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(11), out _);
            Assert.IsNotEmpty(items);
            foreach (var item in items)
                Assert.GreaterOrEqual((int)item.Rarity, (int)Rarity.Epic);
        }

        // --- ModifiersFor mapping ---

        [Test]
        public void ModifiersFor_WeaponKey_PrefersWeaponNotMainHandSlot()
        {
            var mods = DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Weapon });
            Assert.IsTrue(mods.PreferWeapon);
            Assert.IsNull(mods.PreferredSlot);
        }

        [Test]
        public void ModifiersFor_ArmorKeys_MapToEquipmentSlots()
        {
            Assert.AreEqual(EquipmentSlot.Head,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Head }).PreferredSlot);
            Assert.AreEqual(EquipmentSlot.Torso,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Torso }).PreferredSlot);
            Assert.AreEqual(EquipmentSlot.Arms,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Arms }).PreferredSlot);
            Assert.AreEqual(EquipmentSlot.Legs,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Legs }).PreferredSlot);
            Assert.AreEqual(EquipmentSlot.Feet,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Feet }).PreferredSlot);
            Assert.AreEqual(EquipmentSlot.Misc1,
                DungeonRewardRoller.ModifiersFor(new KeyInstance { Slot = KeySlot.Misc }).PreferredSlot);
        }

        [Test]
        public void ModifiersFor_ClassKey_ForcesArchetypeStatPair()
        {
            // Archetype 12 = Sage (INT/WIS)
            var mods = DungeonRewardRoller.ModifiersFor(new KeyInstance { ArchetypeId = 12 });
            Assert.AreEqual(StatType.INT, mods.ForcedStatA);
            Assert.AreEqual(StatType.WIS, mods.ForcedStatB);
        }

        [Test]
        public void ModifiersFor_ColorKey_PassesFamilyThrough()
        {
            var mods = DungeonRewardRoller.ModifiersFor(
                new KeyInstance { ColorFamily = ColorFamily.Blue });
            Assert.AreEqual(ColorFamily.Blue, mods.ColorFamily);
            Assert.IsNull(mods.PreferredSlot);
            Assert.IsFalse(mods.PreferWeapon);
        }

        [Test]
        public void ModifiersFor_PlainKey_HasNoTargeting()
        {
            var mods = DungeonRewardRoller.ModifiersFor(new KeyInstance());
            Assert.IsNull(mods.ColorFamily);
            Assert.IsNull(mods.PreferredSlot);
            Assert.IsFalse(mods.PreferWeapon);
            Assert.IsNull(mods.ForcedStatA);
            Assert.IsNull(mods.ForcedStatB);
        }

        // --- Slot bias flows into created items ---

        [Test]
        public void Roll_HeadKey_BiasesHeadSlot()
        {
            var key = new KeyInstance { Slot = KeySlot.Head };
            var run = MakeRun(MakeSpec(key, baseRolls: 6, parWaves: 100), wavesCleared: 0);
            var items = DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(3), out _);
            int headCount = 0;
            foreach (var item in items)
                if (item.Slot == EquipmentSlot.Head) headCount++;
            // SlotWeight 0.8 over 6 rolls: expect a clear majority
            Assert.GreaterOrEqual(headCount, 3);
        }

        // --- Gold formula ---

        [Test]
        public void Roll_GoldBonus_ScalesWithWavesCleared()
        {
            int questLevel = 10;
            var run = MakeRun(MakeSpec(), wavesCleared: 12);
            DungeonRewardRoller.Roll(run, MakeFactory(), config, questLevel,
                new System.Random(5), out double goldBonus);
            double expected = config.GoldPerKill(questLevel, 0f, config.prestigeMultiplierBase)
                * 12 * 3;
            Assert.AreEqual(expected, goldBonus, 0.0001);
        }

        [Test]
        public void Roll_GoldBonus_ZeroWhenNoWavesCleared()
        {
            var run = MakeRun(MakeSpec(), wavesCleared: 0);
            DungeonRewardRoller.Roll(run, MakeFactory(), config, 10,
                new System.Random(5), out double goldBonus);
            Assert.AreEqual(0, goldBonus, 0.0001);
        }
    }
}
