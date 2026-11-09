using System;
using System.Collections.Generic;
using Starquill.Display;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Starquill.Studio
{
    /// Small pictures for the variant pickers (P19): one item or one body
    /// part drawn alone through the game's DisplayBuilder, cropped to the
    /// drawing. The caller owns the returned textures (Destroy them).
    public sealed class ThumbnailRenderer : IDisposable
    {
        private const int CanvasSize = StudioRenderer.CanvasPixels;
        private const int Padding = 6;

        private readonly DisplayBuilder builder;
        private readonly GameObject root;
        private readonly CharacterDisplay display;

        public ThumbnailRenderer(StudioData data, ImageResolver resolver, Transform parent = null)
        {
            builder = new DisplayBuilder(data.Registry);
            root = new GameObject("StudioThumbnails");
            if (parent != null) root.transform.SetParent(parent, false);
            display = root.AddComponent<CharacterDisplay>();
            display.Initialize(resolver, CanvasSize);
        }

        private static SpeciesDisplayData NoSpecies() =>
            new SpeciesDisplayData { Name = "", ItemRestrictions = Array.Empty<string>() };

        public Texture2D Item(StudioItem item, bool offhand = false)
        {
            var list = new List<EquipmentDisplayInfo> { item.ToDisplayInfo(offhand) };
            display.RebuildFromData(new SpeciesInstanceData(), NoSpecies(), list, builder);
            return Capture();
        }

        /// One body part with variant drawn in body's colors. field is a
        /// BodyPart.Field or "hair" (token = the hair group).
        public Texture2D Part(SpeciesInstanceData body, string field, string token, string group, string variant)
        {
            var b = StudioCharacter.CloneBody(body);
            if (group != null && variant != null) b.ModularImageNums[group] = variant;
            b.ChosenHairGroup = field == "hair" ? token : null;
            var s = NoSpecies();
            switch (field)
            {
                case "backArm": s.BackArm = token; break;
                case "legs": s.Legs = token; break;
                case "body": s.Body = token; break;
                case "head": s.Head = token; break;
                case "ears": s.Ears = token; break;
                case "eyes": s.Eyes = token; break;
                case "nose": s.Nose = token; break;
                case "mouth": s.Mouth = token; break;
                case "facialHair": s.FacialHair = token; break;
                case "facialDetail": s.FacialDetail = token; break;
                case "frontArm": s.FrontArm = token; break;
                case "otherBodyParts": s.OtherBodyParts = new[] { token }; break;
            }
            display.RebuildFromData(b, s, null, builder);
            return Capture();
        }

        /// The render, straight alpha, cropped to a square around the drawing.
        private Texture2D Capture()
        {
            var px = StudioRenderer.ReadPixels(display.Texture);
            PixelOps.Unpremultiply(px);
            var bounds = PixelOps.AlphaBounds(px, CanvasSize, CanvasSize);
            var rect = bounds.HasValue ? Square(bounds.Value) : new RectInt(0, 0, CanvasSize, CanvasSize);
            var crop = PixelOps.Crop(px, CanvasSize, rect);
            var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(crop);
            tex.Apply();
            return tex;
        }

        private static RectInt Square(RectInt b)
        {
            int side = Mathf.Min(CanvasSize, Mathf.Max(b.width, b.height) + Padding * 2);
            int x = Mathf.Clamp(b.x + b.width / 2 - side / 2, 0, CanvasSize - side);
            int y = Mathf.Clamp(b.y + b.height / 2 - side / 2, 0, CanvasSize - side);
            return new RectInt(x, y, side, side);
        }

        public void Dispose() => DiskSpriteSource.Kill(root);
    }
}
