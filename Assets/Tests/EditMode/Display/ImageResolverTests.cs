using NUnit.Framework;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Tests.Display
{
    public class ImageResolverTests
    {
        private sealed class CountingSource : ISpriteSource
        {
            public int Calls;
            public Sprite Result;
            public Sprite Load(string spritePath) { Calls++; return Result; }
        }

        [Test]
        public void Resolve_AsksTheSourceOnceThenCaches()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            var source = new CountingSource { Result = sprite };
            var resolver = new ImageResolver(source);

            Assert.AreSame(sprite, resolver.Resolve("Images/a"));
            Assert.AreSame(sprite, resolver.Resolve("Images/a"));
            Assert.AreEqual(1, source.Calls);

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Resolve_MissingSpriteIsRecorded()
        {
            var resolver = new ImageResolver(new CountingSource());

            Assert.IsNull(resolver.Resolve("Images/equipment/hd01/hd01-0999-130"));

            CollectionAssert.Contains(resolver.MissingPaths, "Images/equipment/hd01/hd01-0999-130");
        }

        [Test]
        public void ClearCache_ForgetsMissingPaths()
        {
            var resolver = new ImageResolver(new CountingSource());
            resolver.Resolve("Images/x");

            resolver.ClearCache();

            Assert.AreEqual(0, resolver.MissingPaths.Count);
        }
    }
}
