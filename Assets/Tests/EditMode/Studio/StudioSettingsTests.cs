using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class StudioSettingsTests
    {
        [Test]
        public void SaveLoad_RoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), "studio_" + Path.GetRandomFileName(), "studio-settings.json");
            try
            {
                new StudioSettings { repoRoot = "/repo", exportScale = 4, trim = false, count = 32 }.Save(path);
                var s = StudioSettings.Load(path);
                Assert.AreEqual("/repo", s.repoRoot);
                Assert.AreEqual(4, s.exportScale);
                Assert.IsFalse(s.trim);
                Assert.AreEqual(32, s.count);
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void Load_MissingOrBrokenFile_GivesDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "broken_" + Path.GetRandomFileName() + ".json");
            Assert.AreEqual(2, StudioSettings.Load(path).exportScale);
            File.WriteAllText(path, "{ not json");
            try { Assert.AreEqual(8, StudioSettings.Load(path).count); }
            finally { File.Delete(path); }
        }

        [TestCase(0, 1)] [TestCase(3, 2)] [TestCase(5, 4)] [TestCase(8, 8)] [TestCase(99, 8)]
        public void SnapScale_ToAllowedValues(int input, int expected)
        {
            Assert.AreEqual(expected, StudioSettings.SnapScale(input));
        }
    }
}
