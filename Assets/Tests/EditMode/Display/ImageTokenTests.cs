using NUnit.Framework;
using Starquill.Display;

namespace Starquill.Tests.Display
{
    public class ImageTokenTests
    {
        [Test]
        public void Parse_StaticToken_ExtractsComponents()
        {
            var token = ImageToken.Parse("0001-082");
            Assert.AreEqual(ImageTokenKind.Static, token.Kind);
            Assert.AreEqual("0001", token.ImageNum);
            Assert.AreEqual(82, token.Layer);
        }

        [Test]
        public void Parse_ModularFullToken_ExtractsComponents()
        {
            var token = ImageToken.Parse("f01-0043-084");
            Assert.AreEqual(ImageTokenKind.ModularFull, token.Kind);
            Assert.AreEqual("f01", token.GroupType);
            Assert.AreEqual("0043", token.ImageNum);
            Assert.AreEqual(84, token.Layer);
        }

        [Test]
        public void Parse_ModularGroupToken_ExtractsGroupType()
        {
            var token = ImageToken.Parse("h02");
            Assert.AreEqual(ImageTokenKind.ModularGroup, token.Kind);
            Assert.AreEqual("h02", token.GroupType);
            Assert.AreEqual(-1, token.Layer);
        }

        [Test]
        public void Parse_EmptyString_ReturnsEmpty()
        {
            var token = ImageToken.Parse("");
            Assert.AreEqual(ImageTokenKind.Empty, token.Kind);
        }

        [Test]
        public void Parse_Null_ReturnsEmpty()
        {
            var token = ImageToken.Parse(null);
            Assert.AreEqual(ImageTokenKind.Empty, token.Kind);
        }

        [Test]
        public void ToSpritePath_Static_ConstructsCorrectPath()
        {
            var token = ImageToken.Parse("0001-016");
            Assert.AreEqual("Images/species/body/0001-016", token.ToSpeciesSpritePath());
        }

        [Test]
        public void ToSpritePath_ModularFull_ConstructsCorrectPath()
        {
            var token = ImageToken.Parse("f01-0043-084");
            Assert.AreEqual("Images/species/f01/f01-0043-084", token.ToSpeciesSpritePath());
        }

        [Test]
        public void ToEquipmentSpritePath_ConstructsCorrectPath()
        {
            string path = ImageToken.BuildEquipmentSpritePath("hd01", 32, 130);
            Assert.AreEqual("Images/equipment/hd01/hd01-0032-130", path);
        }

        [Test]
        public void ToWeaponSpritePath_ConstructsCorrectPath()
        {
            string path = ImageToken.BuildWeaponSpritePath("w01", 164, 1);
            Assert.AreEqual("Images/weapons/w01/w01-164-0001", path);
        }

        [Test]
        public void BuildModularSpritePath_ConstructsCorrectPath()
        {
            string path = ImageToken.BuildModularSpeciesPath("h02", "0043", 92);
            Assert.AreEqual("Images/species/h02/h02-0043-092", path);
        }
    }
}
