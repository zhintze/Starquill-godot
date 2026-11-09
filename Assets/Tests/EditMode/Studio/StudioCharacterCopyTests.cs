using NUnit.Framework;
using Starquill.Core;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioCharacterCopyTests
    {
        private static StudioCharacter Rolled(int seed)
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            return new StudioRoller(StudioTestRepo.Data).RollCharacter(new[] { "human" }, s, new System.Random(seed));
        }

        [Test]
        public void Clone_IsDeep()
        {
            var a = Rolled(1);
            a.LockedSlots.Add(EquipmentSlot.Head);
            var head = a.Gear[EquipmentSlot.Head];
            head.VarianceColors[999] = Color.red;
            var b = a.Clone();

            b.Body.SkinColor = Color.green;
            b.Body.ModularImageNums["zz"] = "0001";
            b.Gear[EquipmentSlot.Head].BaseColor = Color.blue;
            b.Gear[EquipmentSlot.Head].VarianceColors[999] = Color.green;
            b.Gear.Remove(EquipmentSlot.Torso);
            b.LockedSlots.Clear();

            Assert.AreNotEqual(Color.green, a.Body.SkinColor);
            Assert.IsFalse(a.Body.ModularImageNums.ContainsKey("zz"));
            Assert.AreNotEqual(Color.blue, head.BaseColor);
            Assert.AreEqual(Color.red, head.VarianceColors[999]);
            Assert.IsTrue(a.Gear.ContainsKey(EquipmentSlot.Torso));
            Assert.IsTrue(a.LockedSlots.Contains(EquipmentSlot.Head));
        }

        [Test]
        public void Clone_CopiesModularWeaponParts()
        {
            var a = Rolled(2);
            var main = a.Gear[EquipmentSlot.MainHand];
            if (main.LayerVariants == null) Assert.Ignore("seed rolled a non-modular weapon");
            var b = a.Clone();
            b.Gear[EquipmentSlot.MainHand].LayerVariants[0] = -1;
            Assert.AreNotEqual(-1, main.LayerVariants[0]);
        }

        [Test]
        public void CopyFrom_KeepsTheObject_TakesEveryField()
        {
            var a = Rolled(3);
            var b = Rolled(4);
            b.Locked = true;
            b.HideFacialHair = true;
            b.LockedSlots.Add(EquipmentSlot.Feet);

            a.CopyFrom(b);

            Assert.AreEqual(b.SpeciesName, a.SpeciesName);
            Assert.AreEqual(b.Body.SkinColor, a.Body.SkinColor);
            CollectionAssert.AreEquivalent(b.Body.ModularImageNums, a.Body.ModularImageNums);
            CollectionAssert.AreEquivalent(b.Gear.Keys, a.Gear.Keys);
            Assert.AreNotSame(b.Gear[EquipmentSlot.Head], a.Gear[EquipmentSlot.Head]);
            Assert.IsTrue(a.Locked);
            Assert.IsTrue(a.HideFacialHair);
            Assert.IsTrue(a.LockedSlots.Contains(EquipmentSlot.Feet));
        }
    }
}
