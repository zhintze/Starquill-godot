using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ForcedItemsTests
    {
        private static StudioData D => StudioTestRepo.Data;
        private static StudioRoller Roller => new StudioRoller(D);

        private static ForcedItem First(string type) => new ForcedItem(type, ItemSearch.NumbersFor(D, type)[0]);

        private static ForcedItem[] Several(string prefix, int count) =>
            D.Catalog.GetByPrefix(prefix)
                .SelectMany(e => ItemSearch.NumbersFor(D, e.ItemType).Select(n => new ForcedItem(e.ItemType, n)))
                .Take(count).ToArray();

        private static StudioCharacter Bare(string species = "human")
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 0f;
            return Roller.RollCharacter(new[] { species }, s, new System.Random(1));
        }

        [Test]
        public void Duplicate_IsRefused()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("hd01"), out _));

            Assert.IsFalse(f.TryAdd(First("hd01"), out var msg));
            StringAssert.Contains("already", msg);
        }

        [Test]
        public void NumberWithoutArt_IsRefused()
        {
            Assert.IsFalse(new ForcedItems(D).TryAdd(new ForcedItem("hd01", 9999), out var msg));
            StringAssert.Contains("no art", msg);
        }

        [Test]
        public void FiveOfOneSlotType_Fit_TheSixthIsRefused()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 6);
            for (int i = 0; i < 5; i++) Assert.IsTrue(f.TryAdd(hats[i], out var m), m);

            Assert.IsFalse(f.TryAdd(hats[5], out var msg));
            StringAssert.Contains("Misc", msg);
        }

        [Test]
        public void MiscSlotsAreSharedByOverflowAndMiscItems()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            var misc = Several("mc", 4);
            Assert.IsTrue(f.TryAdd(hats[0], out _));
            Assert.IsTrue(f.TryAdd(hats[1], out _)); // takes Misc 1
            for (int i = 0; i < 3; i++) Assert.IsTrue(f.TryAdd(misc[i], out _));

            Assert.IsFalse(f.TryAdd(misc[3], out _));
        }

        [TestCase("w04", "w01")] // two-handed main, then a one-handed off hand
        [TestCase("w01", "w04")] // two-handed cannot go in the off hand
        [TestCase("w08", "w04")] // shield in the off hand, then two-handed
        public void HandConflicts_AreRefused(string first, string second)
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First(first), out _));

            Assert.IsFalse(f.TryAdd(First(second), out var msg));
            StringAssert.Contains("two-handed", msg);
        }

        [Test]
        public void OneHandedAndShield_Fit_AThirdWeaponIsRefused()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("w01"), out _));
            Assert.IsTrue(f.TryAdd(First("w08"), out _));

            Assert.IsFalse(f.TryAdd(First("w02"), out var msg));
            StringAssert.Contains("Both hands", msg);
        }

        [Test]
        public void Apply_FirstOfAPrefixTakesItsSlot_LaterOnesGoToMisc()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            f.TryAdd(hats[0], out _);
            f.TryAdd(hats[1], out _);
            var c = Bare();

            f.Apply(c, Roller, new System.Random(2));

            Assert.IsTrue(hats[0].Matches(c.Gear[EquipmentSlot.Head]));
            Assert.IsTrue(hats[1].Matches(c.Gear[EquipmentSlot.Misc1]));
            Assert.AreEqual(2, c.Gear.Count);
        }

        [Test]
        public void Apply_KeepsOtherGear_AndASecondApplyChangesNothing()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("hd01"), out _);
            var c = Bare();
            var boots = Roller.MakeItem("fe01", ItemSearch.NumbersFor(D, "fe01")[0], new System.Random(3));
            c.Gear[EquipmentSlot.Feet] = boots;

            f.Apply(c, Roller, new System.Random(4));
            var placed = c.Gear[EquipmentSlot.Head];
            f.Apply(c, Roller, new System.Random(5));

            Assert.AreSame(boots, c.Gear[EquipmentSlot.Feet]);
            Assert.AreSame(placed, c.Gear[EquipmentSlot.Head], "the item and its colors stay");
        }

        [Test]
        public void Apply_ForcedTwoHanded_EmptiesTheOffHand()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("w04"), out _);
            var c = Bare();
            c.Gear[EquipmentSlot.OffHand] = Roller.MakeItem("w08", ItemSearch.NumbersFor(D, "w08")[0], new System.Random(6));

            f.Apply(c, Roller, new System.Random(7));

            Assert.AreEqual("w04", c.Gear[EquipmentSlot.MainHand].ItemType);
            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.OffHand));
        }

        [Test]
        public void Apply_ForcedShield_EmptiesARandomTwoHandedMainHand()
        {
            var f = new ForcedItems(D);
            f.TryAdd(First("w08"), out _);
            var c = Bare();
            c.Gear[EquipmentSlot.MainHand] = Roller.MakeItem("w04", ItemSearch.NumbersFor(D, "w04")[0], new System.Random(8));

            f.Apply(c, Roller, new System.Random(9));

            Assert.AreEqual("w08", c.Gear[EquipmentSlot.OffHand].ItemType);
            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.MainHand));
        }

        [Test]
        public void Apply_SkipsItemsTheSpeciesCannotWear()
        {
            var f = new ForcedItems(D);
            Assert.IsTrue(f.TryAdd(First("hd01"), out _)); // stone golem cannot wear hd01
            var c = Bare("stone golem");

            f.Apply(c, Roller, new System.Random(10));

            Assert.IsFalse(c.Gear.Values.Any(i => i.ItemType == "hd01"));
        }

        [Test]
        public void RemoveThenStripAndApply_MovesTheNextItemIntoTheSlot()
        {
            var f = new ForcedItems(D);
            var hats = Several("hd", 2);
            f.TryAdd(hats[0], out _);
            f.TryAdd(hats[1], out _);
            var c = Bare();
            f.Apply(c, Roller, new System.Random(11));

            var before = f.Items.ToList();
            Assert.IsTrue(f.Remove(hats[0]));
            ForcedItems.Strip(c, before);
            f.Apply(c, Roller, new System.Random(12));

            Assert.IsTrue(hats[1].Matches(c.Gear[EquipmentSlot.Head]));
            Assert.AreEqual(1, c.Gear.Count);
        }
    }
}
