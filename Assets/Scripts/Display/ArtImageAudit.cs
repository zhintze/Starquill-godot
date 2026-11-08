using System;
using System.Collections.Generic;
using System.IO;
using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Display
{
    /// Decodes every art PNG and reports empty images and pixel-identical
    /// variants inside one item type / part group. Slow (seconds): run on
    /// build, from the menu, and on the studio's Rescan, not on every import.
    public static class ArtImageAudit
    {
        public static List<ArtIssue> Run(string imagesRoot, IEnumerable<string> relativePaths)
        {
            var issues = new List<ArtIssue>();
            var seen = new Dictionary<(string folder, int layer, string hash), string>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (var rel in relativePaths)
                {
                    if (!ArtFileName.TryParse(rel, out var f, out var problem) || problem != null) continue;
                    if (!tex.LoadImage(File.ReadAllBytes(Path.Combine(imagesRoot, rel)))) continue;

                    var pixels = tex.GetPixels32();
                    bool empty = true;
                    foreach (var p in pixels) if (p.a != 0) { empty = false; break; }
                    if (empty)
                    {
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, rel, "image is empty (fully transparent)"));
                        continue;
                    }

                    string hash = Hash(pixels, tex.width, tex.height);
                    string folder = rel.Substring(0, rel.LastIndexOf('/'));
                    var key = (folder, f.Layer, hash);
                    if (seen.TryGetValue(key, out var first))
                        issues.Add(new ArtIssue(ArtIssueLevel.Warning, rel, $"same pixels as {first}"));
                    else
                        seen[key] = rel;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return issues;
        }

        private static string Hash(Color32[] pixels, int w, int h)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
            }
            using var sha = System.Security.Cryptography.SHA1.Create();
            return $"{w}x{h}:" + Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}
