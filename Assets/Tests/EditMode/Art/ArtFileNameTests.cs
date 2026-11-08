using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtFileNameTests
    {
        [Test]
        public void Equipment_ParsesTypeNumberLayer()
        {
            Assert.IsTrue(ArtFileName.TryParse("equipment/hd01/hd01-0012-130.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual(ArtFileKind.Equipment, f.Kind);
            Assert.AreEqual("hd01", f.Type);
            Assert.AreEqual(12, f.Number);
            Assert.AreEqual(130, f.Layer);
        }

        [Test]
        public void Weapon_ParsesTypeLayerNumber()
        {
            Assert.IsTrue(ArtFileName.TryParse("weapons/w01/w01-164-0007.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual(ArtFileKind.Weapon, f.Kind);
            Assert.AreEqual(164, f.Layer);
            Assert.AreEqual(7, f.Number);
        }

        [Test]
        public void SpeciesBodyAndPart_Parse()
        {
            Assert.IsTrue(ArtFileName.TryParse("species/body/0001-016.png", out var body, out var p1));
            Assert.IsNull(p1);
            Assert.AreEqual(ArtFileKind.SpeciesBody, body.Kind);
            Assert.AreEqual(1, body.Number);
            Assert.AreEqual(16, body.Layer);

            Assert.IsTrue(ArtFileName.TryParse("species/h04/h04-0020-154.png", out var part, out var p2));
            Assert.IsNull(p2);
            Assert.AreEqual(ArtFileKind.SpeciesPart, part.Kind);
            Assert.AreEqual("h04", part.Type);
        }

        [Test]
        public void BackslashesAreAccepted()
        {
            Assert.IsTrue(ArtFileName.TryParse(@"equipment\hd01\hd01-0001-130.png", out var f, out var problem));
            Assert.IsNull(problem);
            Assert.AreEqual("hd01", f.Type);
        }

        [Test]
        public void NonPng_IsNotArt()
        {
            Assert.IsFalse(ArtFileName.TryParse("equipment/hd01/Thumbs.db", out _, out _));
        }

        [TestCase("equipment/hd10/Artboard 1hd10-0004-094.png")]
        [TestCase("equipment/hd01/hd01-12-130.png")]
        [TestCase("equipment/hd01-0001-130.png")]
        [TestCase("equipment/hd02/hd01-0001-130.png")]
        [TestCase("weapons/w01/w01-0001-164.png")]
        [TestCase("species/f01/f02-0001-084.png")]
        [TestCase("props/x01/x01-0001-001.png")]
        public void Misnamed_ReportsProblem(string path)
        {
            Assert.IsTrue(ArtFileName.TryParse(path, out _, out var problem));
            Assert.IsNotNull(problem);
        }

        [Test]
        public void ExpectedPath_BuildsNamesForEachKind()
        {
            Assert.AreEqual("equipment/hd08/hd08-0012-162.png", ArtFileName.ExpectedPath(ArtFileKind.Equipment, "hd08", 12, 162));
            Assert.AreEqual("weapons/w01/w01-164-0003.png", ArtFileName.ExpectedPath(ArtFileKind.Weapon, "w01", 3, 164));
            Assert.AreEqual("species/h04/h04-0002-128.png", ArtFileName.ExpectedPath(ArtFileKind.SpeciesPart, "h04", 2, 128));
        }
    }
}
