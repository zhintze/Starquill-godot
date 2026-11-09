using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class MetaWriterTests
    {
        private string images;

        [SetUp]
        public void SetUp() => images = Path.Combine(Path.GetTempPath(), "metas_" + Path.GetRandomFileName());

        [TearDown]
        public void TearDown() => Directory.Delete(images, true);

        [Test]
        public void WritesMissingMetas_CopyingTheArtSettings_WithNewGuids()
        {
            string old = Path.Combine(images, "equipment", "hd01");
            string fresh = Path.Combine(images, "equipment", "hd14");
            Directory.CreateDirectory(old);
            Directory.CreateDirectory(fresh);
            File.WriteAllText(Path.Combine(images, "equipment") + ".meta", "x");
            File.WriteAllText(old + ".meta", "x");
            File.WriteAllBytes(Path.Combine(old, "hd01-0001-130.png"), new byte[] { 1 });
            const string template = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nTextureImporter:\n  spritePixelsToUnits: 100\n";
            File.WriteAllText(Path.Combine(old, "hd01-0001-130.png.meta"), template);
            string png = Path.Combine(fresh, "hd14-0001-130.png");
            File.WriteAllBytes(png, new byte[] { 1 });

            int written = MetaWriter.WriteMissing(images);

            Assert.AreEqual(2, written, "hd14 folder + its PNG");
            string meta = File.ReadAllText(png + ".meta");
            StringAssert.Contains("spritePixelsToUnits: 100", meta);
            StringAssert.DoesNotContain("0123456789abcdef0123456789abcdef", meta);
            StringAssert.IsMatch("guid: [0-9a-f]{32}\n", meta);
            StringAssert.Contains("folderAsset: yes", File.ReadAllText(fresh + ".meta"));
            Assert.AreEqual(0, MetaWriter.WriteMissing(images), "second run writes nothing");
        }
    }
}
