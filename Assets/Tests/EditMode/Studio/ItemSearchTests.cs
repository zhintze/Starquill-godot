using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ItemSearchTests
    {
        private static StudioData D => StudioTestRepo.Data;

        [Test]
        public void Token_FindsThatItemFirst()
        {
            int n = ItemSearch.NumbersFor(D, "hd01")[0];
            string token = $"hd01-{n:D4}";

            var r = ItemSearch.Find(D, token);

            Assert.AreEqual(token, r.Items[0].Token);
        }

        [Test]
        public void EquipmentFileName_OrPastedPath_FindsExactlyThatItem()
        {
            int n = ItemSearch.NumbersFor(D, "hd01")[0];
            int layer = D.Catalog.GetEntry("hd01").LayerCodes[0];
            string file = Path.GetFileName(ImageToken.BuildEquipmentSpritePath("hd01", n, layer)) + ".png";

            foreach (var q in new[] { file, file.ToUpperInvariant(),
                         @"C:\Users\artist\Starquill\Assets\Resources\Images\equipment\hd01\" + file })
            {
                var r = ItemSearch.Find(D, q);
                Assert.AreEqual(1, r.Total, q);
                Assert.AreEqual(new ForcedItem("hd01", n), r.Items[0], q);
            }
        }

        [Test]
        public void WeaponFileName_FindsTheWeapon()
        {
            var w = D.Catalog.GetWeapon("w01");
            int n = ItemSearch.NumbersFor(D, "w01")[0];
            string file = Path.GetFileName(ImageToken.BuildWeaponSpritePath("w01", w.LayerCodes[0], n)) + ".png";

            var r = ItemSearch.Find(D, file);

            Assert.AreEqual(1, r.Total, file);
            Assert.AreEqual(new ForcedItem("w01", n), r.Items[0]);
        }

        [Test]
        public void DescriptionWord_FindsEveryVariantOfThatType()
        {
            var r = ItemSearch.Find(D, "gloves", 1000);

            Assert.AreEqual(ItemSearch.NumbersFor(D, "ar01").Length, r.Total);
            Assert.IsTrue(r.Items.All(i => i.ItemType == "ar01"));
        }

        [Test]
        public void ResultsStopAtMax_TotalCountsAll()
        {
            var r = ItemSearch.Find(D, "hd", 10);

            Assert.AreEqual(10, r.Items.Count);
            Assert.Greater(r.Total, 10);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("zzzz-nothing")]
        [TestCase("hd01-9999-130.png")]
        public void NoMatch_IsEmpty(string q)
        {
            var r = ItemSearch.Find(D, q);

            Assert.AreEqual(0, r.Total);
            Assert.AreEqual(0, r.Items.Count);
        }

        [Test]
        public void NumbersFor_ModularWeapon_EveryLayerHasTheImage()
        {
            var w = D.Catalog.WeaponEntries.First(e => e.Modular);
            var nums = ItemSearch.NumbersFor(D, w.ItemType);

            Assert.IsNotEmpty(nums);
            foreach (int n in nums)
                foreach (int layer in w.LayerCodes)
                    Assert.IsTrue(File.Exists(DiskSpriteSource.FilePathFor(StudioTestRepo.Root,
                        ImageToken.BuildWeaponSpritePath(w.ItemType, layer, n))), $"{w.ItemType} layer {layer} #{n}");
        }

        [Test]
        public void NumbersFor_UnknownType_IsEmpty()
        {
            Assert.IsEmpty(ItemSearch.NumbersFor(D, "zz99"));
        }
    }
}
