using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    /// Converts the real CSVs and images in this repo. Fails on table errors;
    /// prints every warning so they can be read in the test output.
    public class RepoArtTablesTests
    {
        [Test, Category("RepoData")]
        public void RepoArtTables_HaveNoErrors()
        {
            string root = FindProjectRoot();
            if (root == null) Assert.Ignore("project root not found");

            var r = ArtTablesConverter.Convert(ArtRepo.ReadSources(root));

            foreach (var issue in r.Issues) TestContext.WriteLine(issue);
            Assert.IsFalse(r.HasErrors, string.Join("\n", r.Issues.Where(i => i.level == ArtIssueLevel.Error)));
            Assert.AreEqual(49, r.Data.equipment.Count);
            Assert.AreEqual(9, r.Data.weapons.Count);
            Assert.AreEqual(27, r.Data.species.Count);
            Assert.AreEqual(19, r.Data.partGroups.Count);
        }

        private static string FindProjectRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Environment.CurrentDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (Directory.Exists(Path.Combine(dir.FullName, ArtRepo.TablesDir))) return dir.FullName;
            }
            return null;
        }
    }
}
