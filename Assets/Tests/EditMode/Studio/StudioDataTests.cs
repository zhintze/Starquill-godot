using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioDataTests
    {
        [Test]
        public void Load_RealRepo_FillsRegistryAndCatalog()
        {
            var d = StudioTestRepo.Data;

            Assert.AreEqual(27, d.SpeciesNames.Count);
            Assert.AreEqual(49, d.Catalog.AllEntries.Count);
            Assert.AreEqual(9, d.Catalog.WeaponEntries.Count);
            Assert.IsTrue(d.Registry.Species.ContainsKey("human"));
            Assert.IsFalse(d.Issues.Any(i => i.level == ArtIssueLevel.Error), string.Join("\n", d.Issues));
        }

        [Test]
        public void SpeciesNames_AreSorted()
        {
            var names = StudioTestRepo.Data.SpeciesNames;
            CollectionAssert.AreEqual(names.OrderBy(n => n, System.StringComparer.Ordinal).ToList(), names);
        }

        [Test]
        public void LooksLikeRepo_NeedsTablesAndImages()
        {
            Assert.IsTrue(StudioData.LooksLikeRepo(StudioTestRepo.Root));
            Assert.IsFalse(StudioData.LooksLikeRepo(Path.GetTempPath()));
            Assert.IsFalse(StudioData.LooksLikeRepo(""));
        }
    }
}
