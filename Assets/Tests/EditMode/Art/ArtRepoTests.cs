using System.IO;
using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class ArtRepoTests
    {
        [Test]
        public void ReadSources_ReadsCsvsAndListsPngsRelativeWithSlashes()
        {
            string root = Path.Combine(Path.GetTempPath(), "artrepo_" + Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(Path.Combine(root, ArtRepo.TablesDir));
                File.WriteAllText(Path.Combine(root, ArtRepo.TablesDir, "equipment.csv"), "item_type\n");
                string hd = Path.Combine(root, ArtRepo.ImagesDir, "equipment", "hd01");
                Directory.CreateDirectory(hd);
                File.WriteAllBytes(Path.Combine(hd, "hd01-0001-130.png"), new byte[0]);
                File.WriteAllBytes(Path.Combine(hd, "hd01-0001-130.png.meta"), new byte[0]);

                var src = ArtRepo.ReadSources(root);

                Assert.AreEqual("item_type\n", src.EquipmentCsv);
                Assert.IsNull(src.WeaponsCsv);
                CollectionAssert.AreEqual(new[] { "equipment/hd01/hd01-0001-130.png" }, src.ImageFiles);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
