using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Tests.Display
{
    /// species_extra_parts.csv rules at draw time (docs/plans/2026-11-08-species-extra-parts-plan.md E3, E4).
    public class SpeciesPartRuleTests
    {
        private static DisplayDataRegistry Registry(params SpeciesExtraPartRow[] rules)
        {
            var d = new ArtTableData();
            d.palettes.Add(new PaletteRow { name = "main", hex = new[] { "FFFFFF" } });
            d.palettes.Add(new PaletteRow { name = "human", hex = new[] { "C08060" } });
            d.palettes.Add(new PaletteRow { name = "red", hex = new[] { "FF0000" } });
            d.palettes.Add(new PaletteRow { name = "twotone", hex = new[] { "00FF00", "0000FF" } });
            d.species.Add(new SpeciesRow
            {
                name = "test", legs = "0001-037", body = "0001-038", facialDetail = "0011-087",
                skinPalette = "human", hairPalette = "", eyePalette = "", facialDetailPalette = ""
            });
            foreach (var r in rules) { r.species = "test"; d.speciesExtraParts.Add(r); }
            var reg = new DisplayDataRegistry();
            reg.Load(d);
            return reg;
        }

        private static SpeciesExtraPartRow Row(string part, string color, string group = "", float chance = 1f) =>
            new SpeciesExtraPartRow { part = part, color = color, colorGroup = group, chance = chance };

        private static Color TintOn(System.Collections.Generic.List<DisplayPiece> pieces, int layer) =>
            pieces.Single(p => p.Layer == layer).TintColor;

        private static SpeciesInstanceData Body(DisplayDataRegistry reg, int seed = 1) =>
            SpeciesInstanceData.CreateFrom(reg.Species["test"], reg, new System.Random(seed));

        [Test]
        public void SpeciesPartRule_GivesThatPartItsPaletteColor_OtherPartsKeepSkin()
        {
            var reg = Registry(Row("legs", "red"));
            var body = Body(reg);

            var pieces = new DisplayBuilder(reg).BuildSpeciesPieces(body, reg.Species["test"]);

            Assert.AreEqual(Color.red, TintOn(pieces, 37));
            Assert.AreEqual(body.SkinColor, TintOn(pieces, 38));
        }

        [Test]
        public void FacialDetailRule_OverridesTheFacialDetailColor()
        {
            var reg = Registry(Row("facial_detail", "red"));
            var body = Body(reg);
            body.FacialDetailColor = Color.blue;

            var pieces = new DisplayBuilder(reg).BuildSpeciesPieces(body, reg.Species["test"]);

            Assert.AreEqual(Color.red, TintOn(pieces, 87));
        }

        [Test]
        public void Extras_AreDrawn_SkinByDefault_OrTheNamedBodyColor()
        {
            var reg = Registry(Row("0014-002", "skin"), Row("0014-134", "hair"));
            var body = Body(reg);
            body.HairColor = Color.magenta;

            var pieces = new DisplayBuilder(reg).BuildSpeciesPieces(body, reg.Species["test"]);

            Assert.AreEqual(body.SkinColor, TintOn(pieces, 2));
            Assert.AreEqual(Color.magenta, TintOn(pieces, 134));
            StringAssert.EndsWith("0014-134", pieces.Single(p => p.Layer == 134).SpritePath);
        }

        [Test]
        public void ColorGroup_SharesOneRoll()
        {
            var reg = Registry(Row("legs", "twotone", "fur"), Row("0005-004", "twotone", "fur"));
            for (int seed = 0; seed < 20; seed++)
            {
                var body = Body(reg, seed);
                Assert.AreEqual(1, body.PartColors.Count);
                var pieces = new DisplayBuilder(reg).BuildSpeciesPieces(body, reg.Species["test"]);
                Assert.AreEqual(TintOn(pieces, 37), TintOn(pieces, 4));
            }
        }

        [Test]
        public void ChanceZero_NeverDrawn_ChanceOne_AlwaysDrawn()
        {
            var reg = Registry(Row("0014-002", "skin", chance: 0f), Row("0014-134", "skin"));
            for (int seed = 0; seed < 20; seed++)
            {
                var body = Body(reg, seed);
                var pieces = new DisplayBuilder(reg).BuildSpeciesPieces(body, reg.Species["test"]);
                Assert.IsFalse(pieces.Any(p => p.Layer == 2));
                Assert.IsTrue(pieces.Any(p => p.Layer == 134));
            }
        }

        [Test]
        public void NoOptionalExtras_NoExtraRandomRolls()
        {
            // Saved characters rebuild their look from the id: a species without optional
            // extras must consume the same random numbers as before (plan E3).
            var plain = Registry(Row("0014-002", "skin"));
            var withOptional = Registry(Row("0014-002", "skin"), Row("0014-134", "skin", chance: 0.5f));
            var a = Body(plain, 5);
            var b = Body(withOptional, 5);

            Assert.AreEqual(a.SkinColor, b.SkinColor);
            Assert.AreEqual(a.HairColor, b.HairColor);
        }
    }
}
