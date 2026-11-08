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
            var catalog = new EquipmentCatalog();
            catalog.LoadEquipmentJson(@"[
                {""item_type"":""hd01"",""description"":""helmet"",""amount"":3,
                 ""layer_codes"":[10],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false},
                {""item_type"":""tr01"",""description"":""tunic"",""amount"":2,
                 ""layer_codes"":[20],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false},
                {""item_type"":""ar01"",""description"":""bracers"",""amount"":2,
                 ""layer_codes"":[30],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false},
                {""item_type"":""lg01"",""description"":""pants"",""amount"":2,
                 ""layer_codes"":[40],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false},
                {""item_type"":""fe01"",""description"":""boots"",""amount"":2,
                 ""layer_codes"":[45],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false},
                {""item_type"":""mc01"",""description"":""ring"",""amount"":2,
                 ""layer_codes"":[48],""hidden_layers"":[],""layer_color_variance"":[],""modular"":false}
            ]");
            catalog.LoadWeaponsJson(@"[
                {""item_type"":""w01"",""description"":""sword"",""amount"":2,
                 ""layer_codes"":[50],""hidden_layers"":[],""layer_color_variance"":[],
                 ""modular"":false,""hand_type"":""one_handed""}
            ]");

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
