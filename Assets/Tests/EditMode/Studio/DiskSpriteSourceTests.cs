using System.IO;
using NUnit.Framework;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class DiskSpriteSourceTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "studio_" + Path.GetRandomFileName());
            string dir = Path.Combine(root, "Assets", "Resources", "Images", "equipment", "hd01");
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[16]);
            tex.SetPixel(1, 1, Color.red);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "hd01-0001-130.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void FilePathFor_MapsSpritePathIntoTheRepo()
        {
            string expected = Path.Combine(root, "Assets", "Resources", "Images", "equipment", "hd01", "hd01-0001-130.png");
            Assert.AreEqual(expected, DiskSpriteSource.FilePathFor(root, "Images/equipment/hd01/hd01-0001-130"));
        }

        [Test]
        public void Load_ReadsPngAsSpriteAt100PixelsPerUnit()
        {
            using var source = new DiskSpriteSource(root);

            var sprite = source.Load("Images/equipment/hd01/hd01-0001-130");

            Assert.IsNotNull(sprite);
            Assert.AreEqual(4, (int)sprite.rect.width);
            Assert.AreEqual(100f, sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Bilinear, sprite.texture.filterMode);
        }

        [Test]
        public void Load_MissingFileIsNull()
        {
            using var source = new DiskSpriteSource(root);
            Assert.IsNull(source.Load("Images/equipment/hd01/hd01-0002-130"));
        }
    }
}
