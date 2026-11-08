using System.Collections.Generic;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioCharacterTests
    {
        [Test]
        public void ToDisplayList_SlotOrderAndOffhandFlag()
        {
            var c = new StudioCharacter { SpeciesName = "human" };
            c.Gear[EquipmentSlot.OffHand] = new StudioItem { ItemType = "w08", ItemNum = 1 };
            c.Gear[EquipmentSlot.Head] = new StudioItem { ItemType = "hd01", ItemNum = 3, BaseColor = Color.red };

            var list = c.ToDisplayList();

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("hd01", list[0].ItemType);
            Assert.AreEqual(3, list[0].ItemNum);
            Assert.AreEqual(Color.red, list[0].BaseColor);
            Assert.IsFalse(list[0].IsOffhand);
            Assert.AreEqual("w08", list[1].ItemType);
            Assert.IsTrue(list[1].IsOffhand);
        }

        [Test]
        public void ToDisplayList_CopiesVarianceColors()
        {
            var c = new StudioCharacter { SpeciesName = "human" };
            var item = new StudioItem { ItemType = "hd08", ItemNum = 1 };
            item.VarianceColors[160] = Color.blue;
            c.Gear[EquipmentSlot.Head] = item;

            var info = c.ToDisplayList()[0];
            item.VarianceColors[160] = Color.green;

            Assert.AreEqual(Color.blue, info.VarianceColors[160]);
        }

        [Test]
        public void SpeciesView_BlanksHiddenFacialParts()
        {
            var species = new SpeciesDisplayData { Name = "human", FacialHair = "f05", FacialDetail = "0006-087", Eyes = "f01" };
            var c = new StudioCharacter { SpeciesName = "human", HideFacialHair = true };

            var view = c.SpeciesView(species);

            Assert.AreEqual("", view.FacialHair);
            Assert.AreEqual("0006-087", view.FacialDetail);
            Assert.AreEqual("f01", view.Eyes);
            Assert.AreEqual("f05", species.FacialHair, "the registry's data is never changed");
        }
    }
}
