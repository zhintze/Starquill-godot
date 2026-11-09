using System.Linq;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class BodyPartsTests
    {
        private static StudioData D => StudioTestRepo.Data;

        [Test]
        public void Human_FacePartsAreEditable_OnlyFacialHairAndDetailCanBeNone()
        {
            var parts = BodyParts.Editable(D.Registry.Species["human"]);
            var labels = parts.Select(p => p.Label).ToList();

            CollectionAssert.IsSubsetOf(new[] { "Eyes", "Nose", "Mouth", "Ears", "Facial hair", "Facial detail" }, labels);
            Assert.IsTrue(parts.All(p => p.CanBeNone == (p.Field == "facialHair" || p.Field == "facialDetail")));
            Assert.AreEqual(parts.Count, parts.Where(p => p.Group != null).Select(p => p.Group).Distinct().Count()
                + parts.Count(p => p.Group == null), "one entry per group");
        }

        [Test]
        public void FixedFacialDetail_IsListedWithoutGroup_SoItCanBeNone()
        {
            var golem = D.Registry.Species["stone golem"]; // facial_detail 0017-087 (fixed drawing)
            var detail = BodyParts.Editable(golem).Single(p => p.Field == "facialDetail");

            Assert.IsNull(detail.Group);
            Assert.IsTrue(detail.CanBeNone);
        }

        [Test]
        public void HairGroups_FromSpeciesCsv()
        {
            CollectionAssert.AreEqual(new[] { "h01", "h02", "h03", "h04" }, BodyParts.HairGroups(D.Registry.Species["human"]));
        }

        [Test]
        public void LayersOf_StaticAndGroup()
        {
            CollectionAssert.AreEqual(new[] { 16 }, BodyParts.LayersOf("0001-016", D.Registry).ToArray());
            CollectionAssert.AreEqual(D.Registry.SpeciesLayerMappings["f01"], BodyParts.LayersOf("f01", D.Registry).ToArray());
            Assert.IsEmpty(BodyParts.LayersOf("", D.Registry));
        }

        [Test]
        public void PartsOn_CentaurVarianceLayer_IsTheLegs()
        {
            var centaur = D.Registry.Species["centaur"];
            var set = centaur.SkinVarianceSets.Single(); // species_skin_variance.csv: centaur,37

            CollectionAssert.Contains(BodyParts.PartsOn(centaur, D.Registry, null, set.Indices), "Legs");
        }

        [Test]
        public void HiddenBy_NamesThePartsAnItemCovers()
        {
            var human = D.Registry.Species["human"];
            var helmet = D.Catalog.GetEntry("hd07"); // hides 10 134 130 132 156 158 128 154 96 122 123 152

            var hidden = BodyParts.HiddenBy(human, D.Registry, "h01", helmet.HiddenLayers);

            Assert.IsNotEmpty(hidden);
            Assert.IsEmpty(BodyParts.HiddenBy(human, D.Registry, "h01", new int[0]));
            Assert.AreEqual(hidden.Count, hidden.Distinct().Count());
        }
    }
}
