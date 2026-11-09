using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Starquill.Display;

namespace Starquill.Tests.Display
{
    public class DisplayBuilderTests
    {
        private DisplayDataRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new DisplayDataRegistry();
            registry.Species["test_human"] = new SpeciesDisplayData
            {
                Name = "test_human",
                BackArm = "0001-016",
                Body = "0001-038",
                Head = "0001-082",
                Legs = "0001-037",
                FrontArm = "0001-102",
                Eyes = "f01",
                Nose = "f02",
                Mouth = "f03",
                Ears = "",
                Hair = new[] { "h01" },
                FacialHair = "",
                FacialDetail = "",
                ItemRestrictions = new string[0],
                SkinColor = new[] { "FFFFFF" },
                HairColor = new[] { "FF0000" },
                EyesColor = new[] { "0000FF" },
                FacialDetailColor = new[] { "FFFFFF" },
                PartRules = new SpeciesPartRule[0],
                XScale = 1f,
                YScale = 1f
            };

            var ten = System.Linq.Enumerable.Range(1, 10).ToArray();
            registry.ModularPartVariants["f01"] = ten;
            registry.ModularPartVariants["f02"] = ten;
            registry.ModularPartVariants["f03"] = ten;
            registry.ModularPartVariants["h01"] = ten;

            registry.SpeciesLayerMappings["f01"] = new[] { 84 };
            registry.SpeciesLayerMappings["f02"] = new[] { 86 };
            registry.SpeciesLayerMappings["f03"] = new[] { 85 };
            registry.SpeciesLayerMappings["h01"] = new[] { 92 };
        }

        [Test]
        public void BuildSpecies_StaticTokens_CreatePiecesWithCorrectLayers()
        {
            var instance = CreateTestInstance("test_human");
            var builder = new DisplayBuilder(registry);

            var pieces = builder.BuildSpeciesPieces(instance, registry.Species["test_human"]);

            Assert.Greater(pieces.Count, 0);
            for (int i = 1; i < pieces.Count; i++)
                Assert.LessOrEqual(pieces[i - 1].Layer, pieces[i].Layer);
        }

        [Test]
        public void BuildSpecies_ModularGroup_ExpandsToLayerMapping()
        {
            var instance = CreateTestInstance("test_human");
            var builder = new DisplayBuilder(registry);

            var pieces = builder.BuildSpeciesPieces(instance, registry.Species["test_human"]);

            Assert.IsTrue(pieces.Any(p => p.Layer == 84), "Should have eyes piece at layer 84");
            Assert.IsTrue(pieces.Any(p => p.Layer == 92), "Should have hair piece at layer 92");
        }

        [Test]
        public void BuildSpecies_HairColor_AppliedToHairLayers()
        {
            var instance = CreateTestInstance("test_human");
            instance.HairColor = Color.red;
            var builder = new DisplayBuilder(registry);

            var pieces = builder.BuildSpeciesPieces(instance, registry.Species["test_human"]);

            var hairPiece = pieces.FirstOrDefault(p => p.Layer == 92);
            Assert.IsNotNull(hairPiece);
            Assert.AreEqual(Color.red, hairPiece.TintColor);
        }

        [Test]
        public void BuildSpecies_EyesColor_AppliedToEyeLayers()
        {
            var instance = CreateTestInstance("test_human");
            instance.EyesColor = Color.blue;
            var builder = new DisplayBuilder(registry);

            var pieces = builder.BuildSpeciesPieces(instance, registry.Species["test_human"]);

            var eyesPiece = pieces.FirstOrDefault(p => p.Layer == 84);
            Assert.IsNotNull(eyesPiece);
            Assert.AreEqual(Color.blue, eyesPiece.TintColor);
        }

        [Test]
        public void HiddenLayers_RemovesSpeciesPieces()
        {
            var speciesPieces = new List<DisplayPiece>
            {
                new(16, "path/a"), new(82, "path/b"), new(92, "path/c"), new(128, "path/d")
            };
            var hiddenLayers = new HashSet<int> { 92, 128 };

            var filtered = DisplayBuilder.FilterHiddenLayers(speciesPieces, hiddenLayers);

            Assert.AreEqual(2, filtered.Count);
            Assert.IsFalse(filtered.Any(p => p.Layer == 92));
            Assert.IsFalse(filtered.Any(p => p.Layer == 128));
        }

        [Test]
        public void HatDeduplication_LatestWins()
        {
            var equipItems = new List<(string itemType, int itemNum)>
            {
                ("hd01", 5), ("hd03", 10), ("hd02", 7)
            };

            var deduped = DisplayBuilder.DeduplicateHats(equipItems);

            Assert.AreEqual(1, deduped.Count(e => e.itemType.StartsWith("hd") && int.Parse(e.itemType.Substring(2)) <= 8));
            Assert.AreEqual("hd02", deduped.Last(e => e.itemType.StartsWith("hd")).itemType);
        }

        [Test]
        public void MergeAndSort_CombinesBothSets_SortedByLayer()
        {
            var species = new List<DisplayPiece>
            {
                new(38, "body"), new(16, "arm"), new(82, "head")
            };
            var equipment = new List<DisplayPiece>
            {
                new(130, "hat"), new(24, "gloves")
            };

            var merged = DisplayBuilder.MergeAndSort(species, equipment);

            Assert.AreEqual(5, merged.Count);
            Assert.AreEqual(16, merged[0].Layer);
            Assert.AreEqual(24, merged[1].Layer);
            Assert.AreEqual(38, merged[2].Layer);
            Assert.AreEqual(82, merged[3].Layer);
            Assert.AreEqual(130, merged[4].Layer);
        }

        private SpeciesInstanceData CreateTestInstance(string speciesName)
        {
            return new SpeciesInstanceData
            {
                SpeciesName = speciesName,
                SkinColor = Color.white,
                HairColor = Color.red,
                EyesColor = Color.blue,
                FacialDetailColor = Color.white,
                ModularImageNums = new Dictionary<string, string>
                {
                    { "f01", "0001" }, { "f02", "0001" }, { "f03", "0001" }, { "h01", "0001" }
                },
                ChosenHairGroup = "h01",
                XScale = 1f,
                YScale = 1f
            };
        }
    }
}
