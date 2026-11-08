using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioRendererTests
    {
        [Test]
        public void ExportPng_ScaleAndTrim()
        {
            var data = StudioTestRepo.Data;
            using var source = new DiskSpriteSource(StudioTestRepo.Root);
            using var renderer = new StudioRenderer(data, new ImageResolver(source));
            var c = new StudioRoller(data).RollCharacter(new[] { "human" }, new RandomSettings(), new System.Random(11));

            var full = new Texture2D(2, 2);
            full.LoadImage(renderer.ExportPng(c, 2, trim: false, padding: 0));
            Assert.AreEqual(400, full.width);
            Assert.AreEqual(400, full.height);
            Assert.AreEqual(0, full.GetPixel(0, 0).a, "background is transparent");

            var trimmed = new Texture2D(2, 2);
            trimmed.LoadImage(renderer.ExportPng(c, 2, trim: true, padding: 8));
            Assert.Less(trimmed.width, 400);
            Assert.AreEqual(0, trimmed.GetPixel(0, 0).a, "padding is transparent");
            Assert.Greater(trimmed.GetPixel(trimmed.width / 2, trimmed.height / 2).a, 0, "the character is in the middle");

            Object.DestroyImmediate(full);
            Object.DestroyImmediate(trimmed);
        }
    }
}
