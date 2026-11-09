using System.IO;
using NUnit.Framework;
using Starquill.Studio;

namespace Starquill.Tests.Studio
{
    public class PresetStoreTests
    {
        private string root;

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "presets_" + Path.GetRandomFileName());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void SaveListLoad()
        {
            var store = new PresetStore(root);
            Assert.IsEmpty(store.List(PresetStore.Random));

            store.Save(PresetStore.Random, "No hats", new RandomPreset { head = 0f, torso = 1f });
            store.Save(PresetStore.Random, "all gear", new RandomPreset { head = 1f });

            CollectionAssert.AreEqual(new[] { "all gear", "No hats" }, store.List(PresetStore.Random));
            var p = store.Load<RandomPreset>(PresetStore.Random, "No hats", out var error);
            Assert.IsNull(error);
            Assert.AreEqual(0f, p.head);
            Assert.AreEqual(1f, p.torso);
        }

        [Test]
        public void SaveSameName_Replaces()
        {
            var store = new PresetStore(root);
            store.Save(PresetStore.Random, "a", new RandomPreset { head = 0.1f });
            store.Save(PresetStore.Random, "a", new RandomPreset { head = 0.2f });

            Assert.AreEqual(1, store.List(PresetStore.Random).Count);
            Assert.AreEqual(0.2f, store.Load<RandomPreset>(PresetStore.Random, "a", out _).head);
        }

        [Test]
        public void MissingOrBroken_GivesAReason()
        {
            var store = new PresetStore(root);
            Assert.IsNull(store.Load<RandomPreset>(PresetStore.Random, "nope", out var missing));
            StringAssert.Contains("nope", missing);

            Directory.CreateDirectory(Path.Combine(root, PresetStore.Random));
            File.WriteAllText(store.PathFor(PresetStore.Random, "bad"), "{ not json");
            Assert.IsNull(store.Load<RandomPreset>(PresetStore.Random, "bad", out var broken));
            StringAssert.Contains("bad.json", broken);
        }

        [TestCase("Hero cast", "Hero cast")]
        [TestCase("  a/b\\c:d  ", "a-b-c-d")]
        [TestCase("???", "preset")]
        [TestCase("", "preset")]
        public void CleanName(string input, string expected)
        {
            Assert.AreEqual(expected, PresetStore.CleanName(input));
        }
    }
}
