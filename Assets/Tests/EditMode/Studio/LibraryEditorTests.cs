using System.IO;
using System.Linq;
using NUnit.Framework;
using Starquill.Data.Art;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    /// Works on a temporary copy of the six CSVs (no images), never the real repo.
    public class LibraryEditorTests
    {
        private string root;
        private LibraryEditor editor;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "library_" + Path.GetRandomFileName());
            string tables = Path.Combine(root, ArtRepo.TablesDir);
            Directory.CreateDirectory(tables);
            Directory.CreateDirectory(Path.Combine(root, ArtRepo.ImagesDir));
            foreach (var f in Directory.GetFiles(Path.Combine(StudioTestRepo.Root, ArtRepo.TablesDir), "*.csv"))
                File.Copy(f, Path.Combine(tables, Path.GetFileName(f)));
            editor = new LibraryEditor(root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        private string CsvPath(TableKind k) => Path.Combine(root, ArtRepo.TablesDir, LibraryEditor.FileName(k));
        private string Csv(TableKind k) => File.ReadAllText(CsvPath(k));

        private static int ChangedLines(string a, string b)
        {
            var x = a.Split('\n');
            var y = b.Split('\n');
            int n = 0;
            for (int i = 0; i < System.Math.Max(x.Length, y.Length); i++)
                if (i >= x.Length || i >= y.Length || x[i] != y[i]) n++;
            return n;
        }

        [Test]
        public void NextCode_FollowsTheHighest()
        {
            Assert.AreEqual("hd14", editor.NextCode(TableKind.ItemTypes, "hd"));
            Assert.AreEqual("w10", editor.NextCode(TableKind.Weapons, "w"));
        }

        [Test]
        public void AddItemType_AppendsOneLine_AndMakesItsFolderWithMeta()
        {
            string before = Csv(TableKind.ItemTypes);

            var r = editor.Save(TableKind.ItemTypes, null, new[] { "hd14", "test hat", "130", "", "" }, false);

            Assert.IsTrue(r.Ok, r.Message);
            Assert.AreEqual(before + "hd14,test hat,130,,\n", Csv(TableKind.ItemTypes));
            string folder = editor.FolderPath(TableKind.ItemTypes, "hd14");
            Assert.IsTrue(Directory.Exists(folder));
            StringAssert.Contains("folderAsset: yes", File.ReadAllText(folder + ".meta"));
        }

        [Test]
        public void EditRow_ChangesOneLine()
        {
            string before = Csv(TableKind.ItemTypes);
            var row = (string[])editor.Open(TableKind.ItemTypes).Table.Rows.First(x => x[0] == "hd01").Clone();
            row[1] = "hat, edited";

            var r = editor.Save(TableKind.ItemTypes, "hd01", row, false);

            Assert.IsTrue(r.Ok, r.Message);
            string after = Csv(TableKind.ItemTypes);
            Assert.AreEqual(1, ChangedLines(before, after));
            StringAssert.Contains("hd01,\"hat, edited\",", after);
        }

        [TestCase("hd01", "already exists")]
        [TestCase("hd1x", "not an item code")]
        [TestCase("zz01", "not an item code")]
        [TestCase("", "empty")]
        public void BadOrDuplicateCode_IsRefused(string code, string reason)
        {
            string before = Csv(TableKind.ItemTypes);

            var r = editor.Save(TableKind.ItemTypes, null, new[] { code, "x", "130", "", "" }, false);

            Assert.IsFalse(r.Ok);
            StringAssert.Contains(reason, r.Message);
            Assert.AreEqual(before, Csv(TableKind.ItemTypes));
        }

        [Test]
        public void ChangeThatAddsAnError_IsRefused()
        {
            string before = Csv(TableKind.ItemTypes);

            var r = editor.Save(TableKind.ItemTypes, null, new[] { "hd14", "bad", "abc", "", "" }, false);

            Assert.IsFalse(r.Ok);
            Assert.IsNotEmpty(r.Errors);
            Assert.AreEqual(before, Csv(TableKind.ItemTypes));
        }

        [Test]
        public void FileChangedOnDisk_AsksBeforeOverwriting()
        {
            editor.Open(TableKind.ItemTypes);
            File.AppendAllText(CsvPath(TableKind.ItemTypes), "\n");
            string changed = Csv(TableKind.ItemTypes);

            var r = editor.Save(TableKind.ItemTypes, null, new[] { "hd14", "x", "130", "", "" }, false);
            Assert.IsTrue(r.ChangedOnDisk);
            Assert.AreEqual(changed, Csv(TableKind.ItemTypes));

            Assert.IsTrue(editor.Save(TableKind.ItemTypes, null, new[] { "hd14", "x", "130", "", "" }, true).Ok);
        }

        [Test]
        public void Remove_DeletesRowFolderAndMeta()
        {
            Assert.IsTrue(editor.Save(TableKind.ItemTypes, null, new[] { "hd14", "x", "130", "", "" }, false).Ok);
            string folder = editor.FolderPath(TableKind.ItemTypes, "hd14");
            File.WriteAllBytes(Path.Combine(folder, "hd14-0001-130.png"), new byte[] { 1 });
            Assert.AreEqual(1, editor.ImageCount(TableKind.ItemTypes, "hd14"));

            var r = editor.Remove(TableKind.ItemTypes, "hd14", false);

            Assert.IsTrue(r.Ok, r.Message);
            Assert.AreEqual(-1, editor.Open(TableKind.ItemTypes).Find("hd14"));
            Assert.IsFalse(Directory.Exists(folder));
            Assert.IsFalse(File.Exists(folder + ".meta"));
        }

        [TestCase(TableKind.Weapons, "w10", true)]
        [TestCase(TableKind.Weapons, "x10", false)]
        [TestCase(TableKind.PartGroups, "f07", true)]
        [TestCase(TableKind.PartGroups, "ff7", false)]
        [TestCase(TableKind.Species, "sea elf", true)]
        [TestCase(TableKind.Species, "Sea Elf", false)]
        public void CheckKey(TableKind kind, string key, bool ok)
        {
            Assert.AreEqual(ok, LibraryEditor.CheckKey(kind, key) == null);
        }
    }
}
