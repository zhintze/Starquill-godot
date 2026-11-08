using System;
using System.Collections.Generic;
using Starquill.Display;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Starquill.Studio
{
    /// Draws studio characters with the game's CharacterDisplay and
    /// DisplayBuilder. Previews get their own display; exports share one
    /// display per size.
    public sealed class StudioRenderer : IDisposable
    {
        public const int CanvasPixels = 200; // the art canvas at 1x

        private readonly StudioData data;
        private readonly ImageResolver resolver;
        private readonly DisplayBuilder builder;
        private readonly Transform parent;
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly Dictionary<int, CharacterDisplay> exportDisplays = new Dictionary<int, CharacterDisplay>();

        public StudioRenderer(StudioData data, ImageResolver resolver, Transform parent = null)
        {
            this.data = data;
            this.resolver = resolver;
            this.parent = parent;
            builder = new DisplayBuilder(data.Registry);
        }

        public ImageResolver Resolver => resolver;

        public CharacterDisplay CreatePreview(int size = 400)
        {
            var go = new GameObject("StudioPreview");
            if (parent != null) go.transform.SetParent(parent, false);
            owned.Add(go);
            var display = go.AddComponent<CharacterDisplay>();
            display.Initialize(resolver, size);
            return display;
        }

        public void Draw(CharacterDisplay display, StudioCharacter c)
        {
            var species = data.Registry.Species[c.SpeciesName];
            display.RebuildFromData(c.Body, c.SpeciesView(species), c.ToDisplayList(), builder);
        }

        /// Premultiplied render, as the game draws it.
        public RenderTexture Render(StudioCharacter c, int size)
        {
            if (!exportDisplays.TryGetValue(size, out var display))
                exportDisplays[size] = display = CreatePreview(size);
            Draw(display, c);
            return display.Texture;
        }

        public static Color32[] ReadPixels(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            var px = tex.GetPixels32();
            DiskSpriteSource.Kill(tex);
            return px;
        }

        /// Transparent PNG at scale x the 200 px canvas; trim cuts to the
        /// character and adds padding (in 1x pixels, multiplied by scale).
        public byte[] ExportPng(StudioCharacter c, int scale, bool trim, int padding)
        {
            int size = CanvasPixels * scale;
            var px = ReadPixels(Render(c, size));
            PixelOps.Unpremultiply(px);
            int w = size, h = size;
            if (trim)
            {
                var bounds = PixelOps.AlphaBounds(px, w, h);
                if (bounds.HasValue)
                {
                    px = PixelOps.Crop(px, w, bounds.Value);
                    w = bounds.Value.width;
                    h = bounds.Value.height;
                    px = PixelOps.Pad(px, w, h, padding * scale, out w, out h);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            var png = tex.EncodeToPNG();
            DiskSpriteSource.Kill(tex);
            return png;
        }

        public void Dispose()
        {
            foreach (var go in owned) DiskSpriteSource.Kill(go);
            owned.Clear();
            exportDisplays.Clear();
        }
    }
}
