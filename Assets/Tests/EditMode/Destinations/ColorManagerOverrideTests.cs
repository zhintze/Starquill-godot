using NUnit.Framework;
using Starquill.Core;
using Starquill.Display;
using Starquill.Tests.Fixtures;

namespace Starquill.Tests.Destinations
{
    public class ColorManagerOverrideTests
    {
        private ColorManager NewManager()
        {
            var cm = new ColorManager();
            // two reds, one green
            cm.LoadPalettes(ArtFixtures.Palette("main", "FF0000", "AA1111", "22CC44"));
            return cm;
        }

        [Test]
        public void Override_MovesColorBetweenFamilies()
        {
            var cm = NewManager();
            cm.LoadOverridesFromJson("{\"FF0000\": \"Blue\"}");
            var rng = new System.Random(3);

            // Blue was empty; the override is now its only member.
            for (int i = 0; i < 20; i++)
            {
                var c = cm.GetRandomColor("main", rng, ColorFamily.Blue);
                Assert.AreEqual("FF0000", ColorManager.ColorToHex(c));
            }

            // Red no longer contains the moved color.
            for (int i = 0; i < 20; i++)
            {
                var c = cm.GetRandomColor("main", rng, ColorFamily.Red);
                Assert.AreEqual("AA1111", ColorManager.ColorToHex(c));
            }
        }

        [Test]
        public void Override_UnknownPool_ExcludesColorFromAllFamilies()
        {
            var cm = NewManager();
            cm.LoadOverridesFromJson("{\"FF0000\": \"Sage\"}");
            var rng = new System.Random(3);

            for (int i = 0; i < 20; i++)
            {
                var c = cm.GetRandomColor("main", rng, ColorFamily.Red);
                Assert.AreEqual("AA1111", ColorManager.ColorToHex(c));
            }
        }

        [Test]
        public void Override_IsCaseInsensitive_OnHexAndFamily()
        {
            var cm = NewManager();
            cm.LoadOverridesFromJson("{\"ff0000\": \"blue\"}");
            var rng = new System.Random(3);
            var c = cm.GetRandomColor("main", rng, ColorFamily.Blue);
            Assert.AreEqual("FF0000", ColorManager.ColorToHex(c));
        }

        [Test]
        public void ColorToHex_RoundTripsParseHex()
        {
            foreach (var hex in new[] { "FF0000", "8B4513", "D8C4B6", "010203" })
                Assert.AreEqual(hex, ColorManager.ColorToHex(ColorManager.ParseHex(hex)));
        }

        [Test]
        public void EmptyOrMissingOverrides_LeaveClassifierBehavior()
        {
            var cm = NewManager();
            cm.LoadOverridesFromJson("{}");
            var rng = new System.Random(3);
            var c = cm.GetRandomColor("main", rng, ColorFamily.Green);
            Assert.AreEqual("22CC44", ColorManager.ColorToHex(c));
        }
    }
}
