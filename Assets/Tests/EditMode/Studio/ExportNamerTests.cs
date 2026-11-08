using System;
using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class ExportNamerTests
    {
        [Test]
        public void FileName_SlugIndexDate()
        {
            Assert.AreEqual("stone-golem_03_2026-11-08.png", ExportNamer.FileName("Stone Golem", 3, new DateTime(2026, 11, 8)));
        }

        [Test]
        public void UniquePath_NeverOverwrites()
        {
            string dir = Path.Combine(Path.GetTempPath(), "names_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                Assert.AreEqual(Path.Combine(dir, "a.png"), ExportNamer.UniquePath(dir, "a.png"));
                File.WriteAllText(Path.Combine(dir, "a.png"), "");
                File.WriteAllText(Path.Combine(dir, "a-2.png"), "");
                Assert.AreEqual(Path.Combine(dir, "a-3.png"), ExportNamer.UniquePath(dir, "a.png"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
