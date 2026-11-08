using NUnit.Framework;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class PixelOpsTests
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [Test]
        public void Unpremultiply_RestoresStraightColor()
        {
            var px = new[] { new Color32(64, 32, 0, 128), new Color32(10, 20, 30, 255), new Color32(9, 9, 9, 0) };

            PixelOps.Unpremultiply(px);

            Assert.AreEqual(new Color32(128, 64, 0, 128), px[0]);
            Assert.AreEqual(new Color32(10, 20, 30, 255), px[1]);
            Assert.AreEqual(Clear, px[2]);
        }

        [Test]
        public void AlphaBounds_FindsTheOpaqueBox_OrNullWhenEmpty()
        {
            var px = new Color32[4 * 4];
            Assert.IsNull(PixelOps.AlphaBounds(px, 4, 4));

            px[2 * 4 + 1] = new Color32(255, 0, 0, 255); // x=1, y=2
            px[3 * 4 + 2] = new Color32(255, 0, 0, 10);  // x=2, y=3

            Assert.AreEqual(new RectInt(1, 2, 2, 2), PixelOps.AlphaBounds(px, 4, 4));
        }

        [Test]
        public void Crop_KeepsOnlyTheRect()
        {
            var px = new Color32[3 * 2];
            px[1 * 3 + 2] = new Color32(1, 2, 3, 4); // x=2, y=1

            var cropped = PixelOps.Crop(px, 3, new RectInt(2, 1, 1, 1));

            Assert.AreEqual(1, cropped.Length);
            Assert.AreEqual(new Color32(1, 2, 3, 4), cropped[0]);
        }

        [Test]
        public void Pad_AddsClearBorder()
        {
            var px = new[] { new Color32(5, 5, 5, 255) };

            var padded = PixelOps.Pad(px, 1, 1, 2, out int w, out int h);

            Assert.AreEqual(5, w);
            Assert.AreEqual(5, h);
            Assert.AreEqual(new Color32(5, 5, 5, 255), padded[2 * 5 + 2]);
            Assert.AreEqual(Clear, padded[0]);
        }

        [Test]
        public void BleedTransparentEdges_GivesClearNeighboursTheOpaqueColor_OnePass()
        {
            var px = new[] { new Color32(200, 0, 0, 255), Clear, Clear };

            PixelOps.BleedTransparentEdges(px, 3, 1);

            Assert.AreEqual(new Color32(200, 0, 0, 0), px[1]);
            Assert.AreEqual(Clear, px[2]);
            Assert.AreEqual(new Color32(200, 0, 0, 255), px[0]);
        }
    }
}
