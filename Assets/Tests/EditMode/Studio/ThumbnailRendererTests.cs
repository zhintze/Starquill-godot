using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class ThumbnailRendererTests
    {
        private static int Opaque(Texture2D t)
        {
            int n = 0;
            foreach (var p in t.GetPixels32()) if (p.a > 0) n++;
            return n;
        }

        [Test]
        public void Item_And_Part_AreSquareCropsWithTheDrawing()
        {
            var d = StudioTestRepo.Data;
            using var source = new DiskSpriteSource(StudioTestRepo.Root);
            using var thumbs = new ThumbnailRenderer(d, new ImageResolver(source));
            var roller = new StudioRoller(d);
            var c = roller.RollCharacter(new[] { "human" }, new RandomSettings(), new System.Random(1));

            var item = thumbs.Item(roller.MakeItem("hd01", ItemSearch.NumbersFor(d, "hd01")[0], new System.Random(2)));
            string eyesVariant = d.Registry.ModularPartVariants["f01"][0].ToString("D4");
            var eyes = thumbs.Part(c.Body, "eyes", "f01", "f01", eyesVariant);

            Assert.AreEqual(item.width, item.height);
            Assert.Less(item.width, StudioRenderer.CanvasPixels + 1);
            Assert.Greater(Opaque(item), 50);
            Assert.AreEqual(eyes.width, eyes.height);
            Assert.Greater(Opaque(eyes), 10);

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(eyes);
        }
    }
}
