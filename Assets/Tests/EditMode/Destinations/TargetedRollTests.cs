using NUnit.Framework;
using Starquill.Core;
using Starquill.Data;
using Starquill.Display;
using Starquill.Tests.Fixtures;
using Starquill.Equipment;
using UnityEngine;

namespace Starquill.Tests.Destinations
{
    public class TargetedRollTests
    {
        private EconomyConfig config;

        [TearDown]
        public void TearDown()
        {
            if (config != null) Object.DestroyImmediate(config);
        }
        [Test]
        public void GenerateStatPair_ForcedPair_UsesBothStats()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 50; i++)
            {
                var (p, pv, s, sv) = EquipmentFactory.GenerateStatPair(
                    "tr", Rarity.Rare, rng, questLevel: 10,
                    forcedStatA: StatType.INT, forcedStatB: StatType.CHA);
                Assert.IsTrue((p == StatType.INT && s == StatType.CHA)
                           || (p == StatType.CHA && s == StatType.INT));
                Assert.Greater(pv, sv);
            }
        }

        [Test]
        public void GenerateStatPair_NoForcing_UnchangedBehavior()
        {
            var rng = new System.Random(3);
            var (p, pv, s, sv) = EquipmentFactory.GenerateStatPair("tr", Rarity.Common, rng);
            Assert.AreNotEqual(p, s);
            Assert.Greater(pv, sv);
        }

        [Test]
        public void RollRarity_LegendaryWeightMultiplier_RaisesLegendaryRate()
        {
            int baseline = CountLegendaries(mult: 1f), boosted = CountLegendaries(mult: 10f);
            Assert.Greater(boosted, baseline);
        }

        private static int CountLegendaries(float mult)
        {
            var rng = new System.Random(9);
            int count = 0;
            for (int i = 0; i < 20000; i++)
                if (EquipmentFactory.RollRarity(50, rng, mult) == Rarity.Legendary) count++;
            return count;
        }

        [Test]
        public void TryDrop_WithPreferredSlot_BiasesSlot()
        {
            var dropper = MakeDropper(dropRate: 1f);
            var mods = new DropModifiers { PreferredSlot = EquipmentSlot.Head, SlotWeight = 1f };
            var rng = new System.Random(5);
            for (int i = 0; i < 30; i++)
            {
                var item = dropper.TryDrop(10, rng, mods);
                Assert.IsNotNull(item);
                Assert.AreEqual(EquipmentSlot.Head, item.Slot);
            }
        }

        private LootDropper MakeDropper(float dropRate)
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

            var factory = new EquipmentFactory(catalog, abilityTable, colors);
            config = ScriptableObject.CreateInstance<EconomyConfig>();
            config.baseDropRate = dropRate;
            return new LootDropper(factory, config, new PityTracker());
        }
    }
}
