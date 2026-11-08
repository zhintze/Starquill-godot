using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Tests.Fixtures;

namespace Starquill.Tests.Destinations
{
    public class ColorManagerFamilyTests
    {
        private ColorManager NewManager()
        {
            var cm = new ColorManager();
            // red, dark red, green, gray, brown-ish
            cm.LoadPalettes(ArtFixtures.Palette("main", "FF0000", "AA1111", "22CC44", "888888", "8B4513"));
            return cm;
        }

        [Test]
        public void GetRandomColor_WithFamily_ReturnsOnlyFamilyMembers()
        {
            var cm = NewManager();
            var rng = new System.Random(7);
            for (int i = 0; i < 50; i++)
            {
                var c = cm.GetRandomColor("main", rng, ColorFamily.Red);
                Assert.AreEqual(ColorFamily.Red, ColorFamilyClassifier.Classify(c.r, c.g, c.b));
            }
        }

        [Test]
        public void GetRandomColor_EmptyFamily_FallsBackToWholePalette()
        {
            var cm = NewManager(); // palette has no Blue entries
            var rng = new System.Random(7);
            var c = cm.GetRandomColor("main", rng, ColorFamily.Blue);
            Assert.IsNotNull(c); // no throw, any palette color acceptable
        }
    }
}
