using System.Collections.Generic;
using UnityEngine;

namespace Starquill.Display
{
    public class ImageResolver
    {
        private readonly Dictionary<string, Sprite> cache = new();
        private readonly HashSet<string> missing = new();
        private readonly ISpriteSource source;
        private int missCount;

        public ImageResolver() : this(new ResourcesSpriteSource()) { }

        public ImageResolver(ISpriteSource source)
        {
            this.source = source ?? new ResourcesSpriteSource();
        }

        /// Sprite paths that were asked for and not found (studio Warnings list).
        public IReadOnlyCollection<string> MissingPaths => missing;

        public Sprite Resolve(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
                return null;

            if (cache.TryGetValue(spritePath, out var cached))
                return cached;

            var sprite = source.Load(spritePath);
            if (sprite == null)
            {
                missing.Add(spritePath);
                missCount++;
                if (missCount <= 10)
                    Debug.LogWarning($"ImageResolver: Sprite not found at '{spritePath}'");
                else if (missCount == 11)
                    Debug.LogWarning("ImageResolver: Suppressing further missing sprite warnings");
            }

            cache[spritePath] = sprite;
            return sprite;
        }

        public void ClearCache()
        {
            cache.Clear();
            missing.Clear();
            missCount = 0;
        }

        public int CachedCount => cache.Count;
    }
}
