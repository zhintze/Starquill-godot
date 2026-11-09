using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioRollerSlotTests
    {
        private static StudioData D => StudioTestRepo.Data;
        private static StudioRoller Roller => new StudioRoller(D);

        private static RandomSettings All(float chance)
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = chance;
            return s;
        }

        private static bool TwoHandedWithOffHand(StudioCharacter c) =>
            c.Gear.TryGetValue(EquipmentSlot.MainHand, out var m) && D.Catalog.GetWeapon(m.ItemType).HandType == "two_handed"
            && c.Gear.ContainsKey(EquipmentSlot.OffHand);

        [Test]
        public void RollSlot_FillsTheSlotEvenAtChanceZero()
        {
            var c = Roller.RollCharacter(new[] { "human" }, All(0f), new System.Random(1));
            Roller.RollSlot(c, EquipmentSlot.Head, All(0f), new System.Random(2));
            Roller.RollSlot(c, EquipmentSlot.Misc3, All(0f), new System.Random(3));

            StringAssert.StartsWith("hd", c.Gear[EquipmentSlot.Head].ItemType);
            StringAssert.StartsWith("mc", c.Gear[EquipmentSlot.Misc3].ItemType);
            Assert.AreEqual(2, c.Gear.Count);
        }

        [Test]
        public void RollSlot_OffHandBesideTwoHanded_EmptiesTheMainHand()
        {
            var c = Roller.RollCharacter(new[] { "human" }, All(0f), new System.Random(4));
            c.Gear[EquipmentSlot.MainHand] = Roller.MakeItem("w04", ItemSearch.NumbersFor(D, "w04")[0], new System.Random(5));

            Roller.RollSlot(c, EquipmentSlot.OffHand, All(0f), new System.Random(6));

            Assert.IsTrue(c.Gear.ContainsKey(EquipmentSlot.OffHand));
            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.MainHand));
        }

        [Test]
        public void RollSlot_MainHand_NeverLeavesTwoHandedWithOffHand()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                var c = Roller.RollCharacter(new[] { "human" }, All(0f), rng);
                c.Gear[EquipmentSlot.OffHand] = Roller.MakeItem("w08", ItemSearch.NumbersFor(D, "w08")[0], rng);
                Roller.RollSlot(c, EquipmentSlot.MainHand, All(0f), rng);
                Assert.IsTrue(c.Gear.ContainsKey(EquipmentSlot.MainHand));
                Assert.IsFalse(TwoHandedWithOffHand(c));
            }
        }

        [Test]
        public void RollGear_KeepsLockedSlots()
        {
            var c = Roller.RollCharacter(new[] { "human" }, All(1f), new System.Random(8));
            var head = c.Gear[EquipmentSlot.Head];
            c.LockedSlots.Add(EquipmentSlot.Head);

            Roller.RollGear(c, All(0f), new System.Random(9));

            Assert.AreSame(head, c.Gear[EquipmentSlot.Head]);
            Assert.AreEqual(1, c.Gear.Count);
        }

        [Test]
        public void RollGear_LockedTwoHanded_NeverGetsAnOffHand()
        {
            var rng = new System.Random(10);
            var c = Roller.RollCharacter(new[] { "human" }, All(0f), rng);
            c.Gear[EquipmentSlot.MainHand] = Roller.MakeItem("w04", ItemSearch.NumbersFor(D, "w04")[0], rng);
            c.LockedSlots.Add(EquipmentSlot.MainHand);
            for (int i = 0; i < 100; i++)
            {
                Roller.RollGear(c, All(1f), rng);
                Assert.AreEqual("w04", c.Gear[EquipmentSlot.MainHand].ItemType);
                Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.OffHand));
            }
        }

        [Test]
        public void PrefixFor_Slots()
        {
            Assert.AreEqual("hd", StudioRoller.PrefixFor(EquipmentSlot.Head));
            Assert.AreEqual("mc", StudioRoller.PrefixFor(EquipmentSlot.Misc4));
            Assert.IsNull(StudioRoller.PrefixFor(EquipmentSlot.OffHand));
        }
    }
}
