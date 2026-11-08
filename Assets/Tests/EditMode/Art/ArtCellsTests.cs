using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtCellsTests
    {
        [TestCase("", new int[0])]
        [TestCase("130", new[] { 130 })]
        [TestCase("10 134  130", new[] { 10, 134, 130 })]
        [TestCase("10, 134;130", new[] { 10, 134, 130 })]
        public void TryInts_AcceptsSpaceCommaSemicolon(string cell, int[] expected)
        {
            Assert.IsTrue(ArtCells.TryInts(cell, out var values));
            CollectionAssert.AreEqual(expected, values);
        }

        [Test]
        public void TryInts_RejectsWords()
        {
            Assert.IsFalse(ArtCells.TryInts("10 abc", out _));
        }

        [Test]
        public void Strings_SplitsAndDropsEmpties()
        {
            CollectionAssert.AreEqual(new[] { "h01", "h02" }, ArtCells.Strings(" h01  h02 "));
            Assert.AreEqual(0, ArtCells.Strings("").Length);
        }

        [TestCase("y", true)] [TestCase("Yes", true)] [TestCase("TRUE", true)] [TestCase("1", true)]
        [TestCase("n", false)] [TestCase("no", false)] [TestCase("", false)] [TestCase("0", false)]
        public void TryBool_ReadsCommonSpellings(string cell, bool expected)
        {
            Assert.IsTrue(ArtCells.TryBool(cell, out var value));
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void TryBool_RejectsOther()
        {
            Assert.IsFalse(ArtCells.TryBool("maybe", out _));
        }

        [TestCase("#a1b2c3", "A1B2C3")]
        [TestCase("040400", "040400")]
        public void TryHex_NormalizesToUpperWithoutHash(string cell, string expected)
        {
            Assert.IsTrue(ArtCells.TryHex(cell, out var hex));
            Assert.AreEqual(expected, hex);
        }

        [TestCase("40400")] [TestCase("#GGGGGG")] [TestCase("")]
        public void TryHex_RejectsBadValues(string cell)
        {
            Assert.IsFalse(ArtCells.TryHex(cell, out _));
        }

        [Test]
        public void TryFloat_BlankUsesDefault_InvariantCulture()
        {
            Assert.IsTrue(ArtCells.TryFloat("", 0.35f, out var a));
            Assert.AreEqual(0.35f, a, 1e-6f);
            Assert.IsTrue(ArtCells.TryFloat("1.1", 1f, out var b));
            Assert.AreEqual(1.1f, b, 1e-6f);
            Assert.IsFalse(ArtCells.TryFloat("1,1", 1f, out _));
        }
    }
}
