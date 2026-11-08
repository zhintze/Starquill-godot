using System;
using System.Collections.Generic;
using System.IO;
using Starquill.Display;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Starquill.Studio
{
    /// Loads art PNGs from a repo clone (<repo>/Assets/Resources/<spritePath>.png)
    /// with the game's sprite import settings: 100 PPU, center pivot, bilinear,
    /// clamp, full-rect mesh. Dispose frees every texture it created.
    public sealed class DiskSpriteSource : ISpriteSource, IDisposable
    {
        private readonly string repoRoot;
        private readonly List<Object> created = new();

        public DiskSpriteSource(string repoRoot) { this.repoRoot = repoRoot; }

        public static string FilePathFor(string repoRoot, string spritePath) =>
            Path.Combine(repoRoot, "Assets", "Resources", spritePath.Replace('/', Path.DirectorySeparatorChar) + ".png");

        public Sprite Load(string spritePath)
        {
            string file = FilePathFor(repoRoot, spritePath);
            if (!File.Exists(file)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(file)))
            {
                Kill(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = tex.GetPixels32();
            PixelOps.BleedTransparentEdges(px, tex.width, tex.height);
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            created.Add(tex);
            created.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var o in created) Kill(o);
            created.Clear();
        }

        internal static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
