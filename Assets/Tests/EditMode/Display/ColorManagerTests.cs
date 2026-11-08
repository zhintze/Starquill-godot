using NUnit.Framework;
using UnityEngine;
using Starquill.Display;
using Starquill.Tests.Fixtures;

namespace Starquill.Tests.Display
{
    public class ColorManagerTests
    {
        [Test]
        public void ParseHex_SixCharNoHash_ReturnsCorrectColor()
        {
            Color c = ColorManager.ParseHex("FF0000");
            Assert.AreEqual(1f, c.r, 0.01f);
            Assert.AreEqual(0f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
        }

        [Test]
        public void ParseHex_WithHash_ReturnsCorrectColor()
        {
            Color c = ColorManager.ParseHex("#00FF00");
            Assert.AreEqual(0f, c.r, 0.01f);
            Assert.AreEqual(1f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
        }

        [Test]
        public void ParseHex_White_ReturnsWhite()
        {
            Color c = ColorManager.ParseHex("FFFFFF");
            Assert.AreEqual(Color.white, c);
        }

        [Test]
        public void ParseHex_Empty_ReturnsWhite()
        {
            Color c = ColorManager.ParseHex("");
            Assert.AreEqual(Color.white, c);
        }

        [Test]
        public void LoadPalettes_ParsesHex()
        {
            var manager = new ColorManager();
            manager.LoadPalettes(ArtFixtures.Palette("test_palette", "FF0000", "00FF00", "0000FF"));

            Color[] palette = manager.GetPalette("test_palette");
            Assert.AreEqual(3, palette.Length);
            Assert.AreEqual(1f, palette[0].r, 0.01f);
            Assert.AreEqual(1f, palette[1].g, 0.01f);
            Assert.AreEqual(1f, palette[2].b, 0.01f);
        }

        [Test]
        public void GetPalette_Missing_ReturnsFallback()
        {
            var manager = new ColorManager();
            manager.LoadPalettes(ArtFixtures.Palette("main", "FFFFFF"));

            Color[] palette = manager.GetPalette("nonexistent");
            Assert.IsNotNull(palette);
            Assert.Greater(palette.Length, 0);
        }

        [Test]
        public void GetRandomColor_ReturnsColorFromPalette()
        {
            var manager = new ColorManager();
            manager.LoadPalettes(ArtFixtures.Palette("single", "FF0000"));

            Color c = manager.GetRandomColor("single");
            Assert.AreEqual(1f, c.r, 0.01f);
        }

        [Test]
        public void ResolveSpeciesColorField_HexArray_ReturnsParsedColors()
        {
            var manager = new ColorManager();
            manager.LoadPalettes(ArtFixtures.Palette("main", "FFFFFF"));

            string[] hexArray = { "FF0000", "00FF00" };
            Color[] colors = manager.ResolveColorField(hexArray);
            Assert.AreEqual(2, colors.Length);
            Assert.AreEqual(1f, colors[0].r, 0.01f);
        }

        [Test]
        public void ResolveSpeciesColorField_PaletteKeyword_ReturnsFromPalette()
        {
            var manager = new ColorManager();
            manager.LoadPalettes(ArtFixtures.Palette("human", "3A1914", "45443C"));

            string[] keyword = { "human" };
            Color[] colors = manager.ResolveColorField(keyword);
            Assert.AreEqual(2, colors.Length);
        }
    }
}
