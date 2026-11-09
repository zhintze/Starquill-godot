using System.Linq;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioRollerItemTests
    {
        private static StudioData D => StudioTestRepo.Data;

        [Test]
        public void MakeItem_Armor_UsesTheNumberAndRollsSecondColors()
        {
            var entry = D.Catalog.GetEntry("hd08"); // second-color layer 160
            int n = ItemSearch.NumbersFor(D, "hd08").Last();

            var item = new StudioRoller(D).MakeItem("hd08", n, new System.Random(1));

            Assert.AreEqual("hd08", item.ItemType);
            Assert.AreEqual(n, item.ItemNum);
            Assert.IsNull(item.LayerVariants);
            CollectionAssert.AreEquivalent(entry.LayerColorVariance, item.VarianceColors.Keys);
        }

        [Test]
        public void MakeItem_ModularWeapon_EveryLayerUsesTheNumber()
        {
            var w = D.Catalog.WeaponEntries.First(e => e.Modular);
            int n = ItemSearch.NumbersFor(D, w.ItemType)[0];

            var item = new StudioRoller(D).MakeItem(w.ItemType, n, new System.Random(2));

            Assert.AreEqual(n, item.ItemNum);
            Assert.AreEqual(w.LayerCodes.Length, item.LayerVariants.Length);
            Assert.IsTrue(item.LayerVariants.All(v => v == n));
        }

        [Test]
        public void MakeItem_UnknownType_IsNull()
        {
            Assert.IsNull(new StudioRoller(D).MakeItem("zz99", 1, new System.Random(3)));
        }

        [Test]
        public void ChangeSpecies_NewBody_KeepsWearableGear_DropsRestricted()
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            var roller = new StudioRoller(D);
            var restricted = D.Registry.Species["stone golem"].ItemRestrictions ?? new string[0];
            var rng = new System.Random(4);

            for (int i = 0; i < 50; i++)
            {
                var c = roller.RollCharacter(new[] { "human" }, s, rng);
                var keep = c.Gear.Where(kv => !restricted.Contains(kv.Value.ItemType)).ToDictionary(kv => kv.Key, kv => kv.Value);

                roller.ChangeSpecies(c, "stone golem", s, rng);

                Assert.AreEqual("stone golem", c.SpeciesName);
                Assert.AreEqual("stone golem", c.Body.SpeciesName);
                CollectionAssert.AreEquivalent(keep, c.Gear);
            }
        }

        [Test]
        public void RandomSettings_Reset_RestoresDefaults()
        {
            var s = new RandomSettings { Head = 0f, MiscEach = 1f, FacialHair = 0f };
            s.TypeWeights["hd01"] = 0f;

            s.Reset();

            var d = new RandomSettings();
            Assert.AreEqual(d.Head, s.Head);
            Assert.AreEqual(d.MiscEach, s.MiscEach);
            Assert.AreEqual(d.FacialHair, s.FacialHair);
            Assert.AreEqual(0, s.TypeWeights.Count);
        }
    }
}
