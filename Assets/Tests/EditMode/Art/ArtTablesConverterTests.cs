using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtTablesConverterTests
    {
        private const string EquipHeader = "item_type,description,layer_codes,hidden_layers,color_variance_layers\n";

        private static ArtSources Sources(params string[] files)
        {
            return new ArtSources
            {
                EquipmentCsv = EquipHeader,
                WeaponsCsv = "item_type,description,hand_type,modular,layer_codes,hidden_layers,color_variance_layers\n",
                SpeciesCsv = "name,x_scale,y_scale,head_y_offset,head_zoom,back_arm,legs,body,head,front_arm,eyes,nose,mouth,ears,facial_hair,facial_detail,hair_groups,other_body_parts,skin_palette,hair_palette,eye_palette,facial_detail_palette,item_restrictions\n",
                SpeciesPartsCsv = "group,description,layers\n",
                SkinVarianceCsv = "species,layers,palette\n",
                PalettesCsv = "palette,hex\nmain,#FFFFFF\n",
                ImageFiles = files.ToList()
            };
        }

        private static string[] Messages(ArtBakeResult r, ArtIssueLevel level) =>
            r.Issues.Where(i => i.level == level).Select(i => i.where + ": " + i.message).ToArray();

        [Test]
        public void Equipment_VariantsComeFromFiles_GapsAllowed()
        {
            var src = Sources(
                "equipment/tr08/tr08-0102-014.png", "equipment/tr08/tr08-0102-150.png",
                "equipment/tr08/tr08-0104-014.png", "equipment/tr08/tr08-0104-150.png");
            src.EquipmentCsv += "tr08,coat,14 150,,\n";

            var r = ArtTablesConverter.Convert(src);

            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
            CollectionAssert.AreEqual(new[] { 102, 104 }, r.Data.equipment.Single().variants);
        }

        [Test]
        public void Equipment_IncompleteVariant_IsSkippedWithWarningNamingTheMissingFile()
        {
            var src = Sources(
                "equipment/hd08/hd08-0001-160.png", "equipment/hd08/hd08-0001-162.png",
                "equipment/hd08/hd08-0002-160.png");
            src.EquipmentCsv += "hd08,helmet with detail,160 162,,160\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("equipment/hd08/hd08-0002-162.png")));
        }

        [Test]
        public void Equipment_ParsesListsAndDescription()
        {
            var src = Sources("equipment/hd08/hd08-0001-160.png");
            src.EquipmentCsv += "hd08,\"helmet, detailed\",160,10 134,160\n";

            var row = ArtTablesConverter.Convert(src).Data.equipment.Single();

            Assert.AreEqual("helmet, detailed", row.description);
            CollectionAssert.AreEqual(new[] { 160 }, row.layerCodes);
            CollectionAssert.AreEqual(new[] { 10, 134 }, row.hiddenLayers);
            CollectionAssert.AreEqual(new[] { 160 }, row.colorVarianceLayers);
        }

        [TestCase("HD01,hat,130,,", "item_type")]
        [TestCase("hd01,hat,,,", "layer_codes")]
        [TestCase("hd01,hat,130 abc,,", "layer_codes")]
        [TestCase("hd01,hat,130,,131", "color_variance_layers")]
        public void Equipment_BadCells_AreErrorsWithLineNumber(string row, string column)
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png");
            src.EquipmentCsv += row + "\n";

            var r = ArtTablesConverter.Convert(src);

            var errors = Messages(r, ArtIssueLevel.Error);
            Assert.AreEqual(1, errors.Length);
            StringAssert.StartsWith("equipment.csv line 2", errors[0]);
            StringAssert.Contains(column, errors[0]);
            Assert.AreEqual(0, r.Data.equipment.Count);
        }

        [Test]
        public void Equipment_DuplicateType_IsError()
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png");
            src.EquipmentCsv += "hd01,hat,130,,\nhd01,hat again,130,,\n";

            var r = ArtTablesConverter.Convert(src);

            Assert.AreEqual(1, r.Data.equipment.Count);
            StringAssert.Contains("twice", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MissingColumn_IsOneErrorForTheFile()
        {
            var src = Sources();
            src.EquipmentCsv = "item_type,description\nhd01,hat\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.Contains("layer_codes", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MissingFile_IsError()
        {
            var src = Sources();
            src.EquipmentCsv = null;

            var r = ArtTablesConverter.Convert(src);

            StringAssert.StartsWith("equipment.csv", Messages(r, ArtIssueLevel.Error).Single());
        }

        [Test]
        public void MisnamedFile_IsWarningAndNeverUsed()
        {
            var src = Sources("equipment/hd10/hd10-0001-094.png", "equipment/hd10/Artboard 1hd10-0004-094.png");
            src.EquipmentCsv += "hd10,headband,94,,\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("equipment/hd10/Artboard 1hd10-0004-094.png")));
        }

        [Test]
        public void FolderWithoutRow_AndRowWithoutImages_AreWarnings()
        {
            var src = Sources("equipment/hd15/hd15-0001-130.png");
            src.EquipmentCsv += "hd14,crown,130,,\n";

            var r = ArtTablesConverter.Convert(src);
            var warnings = Messages(r, ArtIssueLevel.Warning);

            Assert.IsTrue(warnings.Any(m => m.StartsWith("equipment/hd15") && m.Contains("no row")));
            Assert.IsTrue(warnings.Any(m => m.StartsWith("equipment/hd14") && m.Contains("no images")));
            Assert.IsFalse(r.HasErrors);
        }

        [Test]
        public void ImageLayerNotInLayerCodes_IsWarning()
        {
            var src = Sources("equipment/hd01/hd01-0001-130.png", "equipment/hd01/hd01-0001-131.png");
            src.EquipmentCsv += "hd01,hat,130,,\n";

            var r = ArtTablesConverter.Convert(src);

            CollectionAssert.AreEqual(new[] { 1 }, r.Data.equipment.Single().variants);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.Contains("131")));
        }

        [Test]
        public void Palettes_GroupRowsInOrderAndNormalizeHex()
        {
            var src = Sources();
            src.PalettesCsv = "palette,hex\nmain,#ff0000\nhuman,3A1914\nmain,00ff00\n";

            var p = ArtTablesConverter.Convert(src).Data.palettes;

            Assert.AreEqual(2, p.Count);
            Assert.AreEqual("main", p[0].name);
            CollectionAssert.AreEqual(new[] { "FF0000", "00FF00" }, p[0].hex);
            CollectionAssert.AreEqual(new[] { "3A1914" }, p[1].hex);
        }

        [Test]
        public void Palettes_BadHex_IsError()
        {
            var src = Sources();
            src.PalettesCsv = "palette,hex\nmain,40400\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.StartsWith("palettes.csv line 2", Messages(r, ArtIssueLevel.Error).Single());
        }
    }
}
