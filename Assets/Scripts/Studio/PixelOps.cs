using System;
using UnityEngine;

namespace Starquill.Studio
{
    /// Pixel helpers for PNG export. Arrays are Unity order: row 0 is the
    /// bottom row, index = y * width + x.
    public static class PixelOps
    {
        /// Unity's sprite shader writes premultiplied color; PNG wants straight alpha.
        public static void Unpremultiply(Color32[] px)
        {
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 255) continue;
                if (c.a == 0) { px[i] = new Color32(0, 0, 0, 0); continue; }
                px[i] = new Color32(Div(c.r, c.a), Div(c.g, c.a), Div(c.b, c.a), c.a);
            }
        }

        private static byte Div(byte v, byte a) => (byte)Math.Min(255, (v * 255 + a / 2) / a);

        /// Smallest rect holding every pixel with alpha > 0; null if none.
        public static RectInt? AlphaBounds(Color32[] px, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX < 0) return null;
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public static Color32[] Crop(Color32[] px, int w, RectInt r)
        {
            var result = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                Array.Copy(px, (r.y + y) * w + r.x, result, y * r.width, r.width);
            return result;
        }

        public static Color32[] Pad(Color32[] px, int w, int h, int pad, out int newW, out int newH)
        {
            newW = w + pad * 2;
            newH = h + pad * 2;
            var result = new Color32[newW * newH];
            for (int y = 0; y < h; y++)
                Array.Copy(px, y * w, result, (y + pad) * newW + pad, w);
            return result;
        }

        /// One pass: each fully transparent pixel takes the average color of its
        /// opaque 4-neighbours (alpha stays 0). Approximates the importer's
        /// "alpha is transparency", so bilinear edges do not pick up black.
        public static void BleedTransparentEdges(Color32[] px, int w, int h)
        {
            var src = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (src[i].a != 0) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    void Add(int xx, int yy)
                    {
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) return;
                        var c = src[yy * w + xx];
                        if (c.a == 0) return;
                        r += c.r; g += c.g; b += c.b; n++;
                    }
                    Add(x - 1, y); Add(x + 1, y); Add(x, y - 1); Add(x, y + 1);
                    if (n > 0) px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                }
        }
    }
}
