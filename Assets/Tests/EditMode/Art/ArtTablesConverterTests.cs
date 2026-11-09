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
                SpeciesCsv = "name,x_scale,y_scale,head_y_offset,head_zoom,back_arm,legs,body,head,front_arm,eyes,nose,mouth,ears,facial_hair,facial_detail,hair_groups,skin_palette,hair_palette,eye_palette,facial_detail_palette,item_restrictions\n",
                SpeciesPartsCsv = "group,description,layers\n",
                ExtraPartsCsv = "species,part,color,color_group,chance\n",
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

        private const string WeaponHeader = "item_type,description,hand_type,modular,layer_codes,hidden_layers,color_variance_layers\n";

        [Test]
        public void ModularWeapon_EachLayerKeepsItsOwnNumbers()
        {
            var src = Sources(
                "weapons/w01/w01-164-0001.png", "weapons/w01/w01-164-0002.png",
                "weapons/w01/w01-166-0001.png",
                "weapons/w01/w01-166-0005.png");
            src.WeaponsCsv = WeaponHeader + "w01,sword 1 hand,one_handed,y,164 166,,166\n";

            var w = ArtTablesConverter.Convert(src).Data.weapons.Single();

            Assert.IsTrue(w.modular);
            CollectionAssert.AreEqual(new[] { 1, 2 }, w.variantsPerLayer[0].values);
            CollectionAssert.AreEqual(new[] { 1, 5 }, w.variantsPerLayer[1].values);
        }

        [Test]
        public void NonModularWeapon_SharesCompleteNumbersAcrossLayers()
        {
            var src = Sources(
                "weapons/w09/w09-170-0001.png", "weapons/w09/w09-172-0001.png",
                "weapons/w09/w09-170-0002.png");
            src.WeaponsCsv = WeaponHeader + "w09,shield with detail,one_handed,n,170 172,,\n";

            var r = ArtTablesConverter.Convert(src);
            var w = r.Data.weapons.Single();

            CollectionAssert.AreEqual(new[] { 1 }, w.variantsPerLayer[0].values);
            CollectionAssert.AreEqual(new[] { 1 }, w.variantsPerLayer[1].values);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.StartsWith("weapons/w09/w09-172-0002.png")));
        }

        [TestCase("w01,sword,three_handed,y,164,,", "hand_type")]
        [TestCase("w01,sword,one_handed,maybe,164,,", "modular")]
        [TestCase("x01,sword,one_handed,y,164,,", "item_type")]
        public void Weapon_BadCells_AreErrors(string row, string column)
        {
            var src = Sources("weapons/w01/w01-164-0001.png");
            src.WeaponsCsv = WeaponHeader + row + "\n";

            var r = ArtTablesConverter.Convert(src);

            StringAssert.Contains(column, Messages(r, ArtIssueLevel.Error).Single());
            Assert.AreEqual(0, r.Data.weapons.Count);
        }

        [Test]
        public void PartGroups_LayersAndCompleteVariants()
        {
            var src = Sources(
                "species/h02/h02-0001-092.png", "species/h02/h02-0001-128.png",
                "species/h02/h02-0003-092.png", "species/h02/h02-0003-128.png");
            src.SpeciesPartsCsv = "group,description,layers\nh02,hair,92 128\n";

            var g = ArtTablesConverter.Convert(src).Data.partGroups.Single();

            Assert.AreEqual("h02", g.group);
            CollectionAssert.AreEqual(new[] { 92, 128 }, g.layers);
            CollectionAssert.AreEqual(new[] { 1, 3 }, g.variants);
        }

        [Test]
        public void ZeroNumber_IsAnOrdinaryVariant()
        {
            var src = Sources("species/f01/f01-0000-084.png", "species/f01/f01-0001-084.png");
            src.SpeciesPartsCsv = "group,description,layers\nf01,eyes,84\n";

            var g = ArtTablesConverter.Convert(src).Data.partGroups.Single();

            CollectionAssert.AreEqual(new[] { 0, 1 }, g.variants);
        }

        private static ArtSources SpeciesSources(string speciesRow, string extraPartRows = "")
        {
            var src = Sources(
                "species/body/0001-016.png", "species/body/0001-037.png",
                "species/f01/f01-0001-084.png", "species/h01/h01-0001-092.png", "species/body/0014-002.png",
                "equipment/hd12/hd12-0001-130.png");
            src.EquipmentCsv += "hd12,tight facemask,130,,\n";
            src.SpeciesPartsCsv = "group,description,layers\nf01,eyes,84\nh01,hair,92\n";
            src.PalettesCsv = "palette,hex\nmain,#FFFFFF\nhuman,#3A1914\nskin_naga,#F3D9C2\n";
            src.SpeciesCsv += speciesRow + "\n";
            src.ExtraPartsCsv += extraPartRows;
            return src;
        }

        // name,x_scale,y_scale,head_y_offset,head_zoom,back_arm,legs,body,head,front_arm,eyes,nose,mouth,ears,facial_hair,facial_detail,hair_groups,skin_palette,hair_palette,eye_palette,facial_detail_palette,item_restrictions
        private const string Human = "human,1,1,0.35,0.5,0001-016,0001-037,,,,f01,,,,,,h01,human,,,,hd12";

        [Test]
        public void Species_ParsesRow()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human));

            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
            var s = r.Data.species.Single();
            Assert.AreEqual("human", s.name);
            Assert.AreEqual("0001-016", s.backArm);
            Assert.AreEqual("f01", s.eyes);
            CollectionAssert.AreEqual(new[] { "h01" }, s.hairGroups);
            Assert.AreEqual("human", s.skinPalette);
            Assert.AreEqual("", s.hairPalette);
            CollectionAssert.AreEqual(new[] { "hd12" }, s.itemRestrictions);
            Assert.AreEqual(0.35f, s.headYOffset, 1e-6f);
        }

        [Test]
        public void Species_FullPartToken_IsAccepted()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",f01,", ",f01-0001-084,")));
            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
        }

        [TestCase("0009-016", ArtIssueLevel.Warning)]   // body image missing
        [TestCase("f09", ArtIssueLevel.Error)]          // unknown part group
        [TestCase("eyes!", ArtIssueLevel.Error)]        // not a token
        public void Species_BadPartToken(string token, ArtIssueLevel level)
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",0001-016,", "," + token + ",")));
            Assert.IsTrue(Messages(r, level).Any(m => m.StartsWith("species.csv line 2") && m.Contains("back_arm")));
        }

        [Test]
        public void Species_UnknownPalette_IsError_UnknownRestriction_IsWarning()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",human,,,,hd12", ",skin_troll,,,,hd14")));

            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.Contains("skin_troll")));
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.Contains("hd14")));
        }

        [Test]
        public void Species_BlankSkinPalette_IsError()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human.Replace(",human,,,,hd12", ",,,,,hd12")));
            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.Contains("skin_palette")));
        }

        [Test]
        public void ExtraParts_ParsesRowsInFileOrder()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human,
                "human,0014-002,skin_naga,wings,\nhuman,legs,skin_naga,wings,\nhuman,f01,hair,,0.25\n"));

            Assert.IsFalse(r.HasErrors, string.Join("\n", Messages(r, ArtIssueLevel.Error)));
            var rows = r.Data.speciesExtraParts;
            CollectionAssert.AreEqual(new[] { "0014-002", "legs", "f01" }, rows.Select(x => x.part));
            Assert.AreEqual("skin_naga", rows[0].color);
            Assert.AreEqual("wings", rows[1].colorGroup);
            Assert.AreEqual(1f, rows[0].chance);
            Assert.AreEqual(0.25f, rows[2].chance, 1e-6f);
        }

        [TestCase("elf,0014-002,skin,,", "species 'elf'")]                  // unknown species
        [TestCase("human,0014-002,nope,,", "color 'nope'")]                 // unknown color
        [TestCase("human,x09,skin,,", "x09")]                               // unknown part group
        [TestCase("human,wing!,skin,,", "part 'wing!'")]                    // not a part
        [TestCase("human,0014-002,skin,,1.5", "chance")]                    // chance out of range
        [TestCase("human,legs,skin_naga,,0.5", "chance")]                   // chance on a species part
        [TestCase("human,0014-002,skin,,\nhuman,0014-002,hair,,", "twice")] // duplicate part
        [TestCase("human,0014-002,skin_naga,g,\nhuman,legs,hair,g,", "color group g")] // group with two colors
        public void ExtraParts_BadRows_AreErrors(string rows, string fragment)
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human, rows + "\n"));

            var errors = Messages(r, ArtIssueLevel.Error).Where(m => m.StartsWith("species_extra_parts.csv")).ToList();
            Assert.IsTrue(errors.Any(m => m.Contains(fragment)), string.Join("\n", Messages(r, ArtIssueLevel.Error)));
        }

        [Test]
        public void ExtraParts_MissingBodyImage_IsWarning()
        {
            var r = ArtTablesConverter.Convert(SpeciesSources(Human, "human,0099-134,skin,,\n"));

            Assert.IsFalse(r.HasErrors);
            Assert.IsTrue(Messages(r, ArtIssueLevel.Warning).Any(m => m.Contains("0099-134")));
        }

        [Test]
        public void Species_OldOtherBodyPartsColumn_WithData_PointsToTheNewFile()
        {
            var src = SpeciesSources(Human);
            src.SpeciesCsv = src.SpeciesCsv.Replace("hair_groups,skin_palette", "hair_groups,other_body_parts,skin_palette")
                .Replace(",h01,human,", ",h01,0014-002,human,");

            var r = ArtTablesConverter.Convert(src);

            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.Contains("species_extra_parts.csv")));
        }

        [Test]
        public void ExtraPartsFileMissing_IsError()
        {
            var src = SpeciesSources(Human);
            src.ExtraPartsCsv = null;

            var r = ArtTablesConverter.Convert(src);

            Assert.IsTrue(Messages(r, ArtIssueLevel.Error).Any(m => m.StartsWith("species_extra_parts.csv")));
        }
    }
}
