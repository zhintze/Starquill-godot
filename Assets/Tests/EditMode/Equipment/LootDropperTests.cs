using System.Collections.Generic;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Data;
using Starquill.Display;
using Starquill.Tests.Fixtures;
using Starquill.Equipment;
using UnityEngine;

namespace Starquill.Tests.Equipment
{
    [TestFixture]
    public class LootDropperTests
    {
        private EquipmentFactory factory;
        private EconomyConfig config;
        private PityTracker pity;

        [SetUp]
        public void SetUp()
        {
            var catalog = ArtFixtures.SixSlotCatalog();

            var abilityTable = new AbilityTable();
            var colors = ArtFixtures.RgbColors();

            factory = new EquipmentFactory(catalog, abilityTable, colors);
            config = ScriptableObject.CreateInstance<EconomyConfig>();
            config.baseDropRate = 0.15f;
            pity = new PityTracker();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TryDrop_HighDropRate_AlwaysDrops()
        {
            config.baseDropRate = 1.0f;
            var dropper = new LootDropper(factory, config, pity);
            int drops = 0;
            for (int i = 0; i < 20; i++)
            {
                if (dropper.TryDrop(1, new System.Random(i * 100)) != null)
                    drops++;
            }
            Assert.AreEqual(20, drops);
        }

        [Test]
        public void TryDrop_ZeroDropRate_NoPityDrop_ReturnsNull()
        {
            config.baseDropRate = 0f;
            // Fresh pity tracker, no kills registered yet
            var freshPity = new PityTracker();
            var dropper = new LootDropper(factory, config, freshPity);
            var item = dropper.TryDrop(1, new System.Random(42));
            Assert.IsNull(item);
        }

        [Test]
        public void TryDrop_PityOverride_ForcesDropAtThreshold()
        {
            config.baseDropRate = 0f;
            var dropper = new LootDropper(factory, config, pity);
            var rng = new System.Random(0);

            EquipmentInstance drop = null;
            for (int i = 0; i < config.pityUncommon + 1; i++)
            {
                drop = dropper.TryDrop(1, rng);
                if (drop != null) break;
            }
            Assert.IsNotNull(drop);
            Assert.GreaterOrEqual((int)drop.Rarity, (int)Rarity.Uncommon);
        }

        [Test]
        public void TryDrop_ReturnsItemWithValidSlot()
        {
            config.baseDropRate = 1.0f;
            var dropper = new LootDropper(factory, config, pity);
            var item = dropper.TryDrop(5, new System.Random(42));
            Assert.IsNotNull(item);
            Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), item.Slot));
        }

        [Test]
        public void TryDrop_ProducesVarietyOfSlots()
        {
            config.baseDropRate = 1.0f;
            var dropper = new LootDropper(factory, config, pity);
            var slots = new HashSet<EquipmentSlot>();
            for (int i = 0; i < 100; i++)
            {
                var item = dropper.TryDrop(1, new System.Random(i * 7));
                if (item != null) slots.Add(item.Slot);
            }
            Assert.Greater(slots.Count, 1, "Should produce items for multiple slots");
        }

        [Test]
        public void TryDrop_RegistersDrop_ResetsPity()
        {
            config.baseDropRate = 1.0f;
            var dropper = new LootDropper(factory, config, pity);

            // Set pity just below threshold so TryDrop's internal RegisterKill triggers it
            // This guarantees the drop is Uncommon+ (pity-forced), so RegisterDrop resets the counter
            pity.killsSinceUncommon = config.pityUncommon - 1;

            var drop = dropper.TryDrop(1, new System.Random(42));
            Assert.IsNotNull(drop);
            Assert.GreaterOrEqual((int)drop.Rarity, (int)Rarity.Uncommon);
            Assert.AreEqual(0, pity.killsSinceUncommon);
        }
    }
}
