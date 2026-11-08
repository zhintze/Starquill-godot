using System.Collections.Generic;
using Starquill.Core;
using Starquill.Data;
using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Display
{
    public class ColorManager
    {
        private Dictionary<string, Color[]> palettes = new();
        private readonly Dictionary<string, Dictionary<ColorFamily, Color[]>> familyIndex = new();
        private readonly Dictionary<string, string> familyOverrides = new();
        private static readonly Color[] FallbackPalette = { Color.white };

        public void LoadPalettes(IEnumerable<PaletteRow> rows)
        {
            palettes.Clear();
            familyIndex.Clear();
            foreach (var row in rows)
            {
                var colors = new Color[row.hex.Length];
                for (int i = 0; i < row.hex.Length; i++)
                    colors[i] = ParseHex(row.hex[i]);
                palettes[row.name] = colors;
            }
        }

        public void LoadFromResources()
        {
            LoadPalettes(ArtTables.LoadFromResources().palettes);
            LoadOverridesFromResources();
        }

        /// Manual family overrides authored via tools/color_pools.py:
        /// flat JSON { "HEX": "FamilyName", ... }. A name that is not a
        /// ColorFamily member (a workshop pool awaiting promotion) excludes
        /// the color from every key's drop pool; the color stays in the
        /// palette for unfiltered rolls.
        public void LoadOverridesFromJson(string json)
        {
            familyOverrides.Clear();
            familyIndex.Clear();
            foreach (var kvp in ParseStringMapJson(json))
                familyOverrides[kvp.Key.ToUpperInvariant()] = kvp.Value;
        }

        public void LoadOverridesFromResources(string resourcePath = "Data/color_family_overrides")
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset != null)
                LoadOverridesFromJson(textAsset.text);
        }

        public Color[] GetPalette(string name)
        {
            if (palettes.TryGetValue(name, out var palette))
                return palette;
            if (palettes.TryGetValue("main", out var main))
                return main;
            return FallbackPalette;
        }

        public Color GetRandomColor(string paletteName)
        {
            var palette = GetPalette(paletteName);
            return palette[Random.Range(0, palette.Length)];
        }

        public Color GetRandomColor(string paletteName, System.Random rng)
        {
            var palette = GetPalette(paletteName);
            return palette[rng.Next(palette.Length)];
        }

        public Color GetRandomColor(string paletteName, System.Random rng, ColorFamily? family)
        {
            if (!family.HasValue) return GetRandomColor(paletteName, rng);

            if (!familyIndex.TryGetValue(paletteName, out var byFamily))
            {
                byFamily = BuildFamilyIndex(GetPalette(paletteName));
                familyIndex[paletteName] = byFamily;
            }

            if (byFamily.TryGetValue(family.Value, out var members) && members.Length > 0)
                return members[rng.Next(members.Length)];
            return GetRandomColor(paletteName, rng); // family empty in this palette
        }

        private Dictionary<ColorFamily, Color[]> BuildFamilyIndex(Color[] palette)
        {
            var lists = new Dictionary<ColorFamily, List<Color>>();
            foreach (var c in palette)
            {
                ColorFamily f;
                if (familyOverrides.TryGetValue(ColorToHex(c), out var name))
                {
                    // Workshop pools (names outside the enum) drop the color
                    // from every family until the pool is promoted in code.
                    if (!System.Enum.TryParse(name, true, out f)) continue;
                }
                else
                {
                    f = ColorFamilyClassifier.Classify(c.r, c.g, c.b);
                }
                if (!lists.TryGetValue(f, out var list)) { list = new List<Color>(); lists[f] = list; }
                list.Add(c);
            }
            var result = new Dictionary<ColorFamily, Color[]>();
            foreach (var kvp in lists) result[kvp.Key] = kvp.Value.ToArray();
            return result;
        }

        public static string ColorToHex(Color c)
        {
            return $"{Mathf.RoundToInt(c.r * 255f):X2}" +
                   $"{Mathf.RoundToInt(c.g * 255f):X2}" +
                   $"{Mathf.RoundToInt(c.b * 255f):X2}";
        }

        public Color[] ResolveColorField(string[] field)
        {
            if (field == null || field.Length == 0)
                return FallbackPalette;

            if (field[0].Length >= 6 && IsHexString(field[0]))
            {
                var colors = new Color[field.Length];
                for (int i = 0; i < field.Length; i++)
                    colors[i] = ParseHex(field[i]);
                return colors;
            }

            return GetPalette(field[0]);
        }

        public static Color ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Color.white;

            hex = hex.TrimStart('#');
            if (hex.Length < 6) return Color.white;

            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static bool IsHexString(string s)
        {
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }

        /// Flat { "key": "value", ... } parser, hand-rolled
        /// (no nesting, no escapes: hex keys + pool names).
        private static Dictionary<string, string> ParseStringMapJson(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return result;
            json = json.Trim();
            if (!json.StartsWith("{") || json.Length < 2) return result;

            json = json.Substring(1, json.Length - 2);
            int i = 0;
            while (i < json.Length)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                int colon = json.IndexOf(':', keyEnd);
                if (colon < 0) break;
                int valStart = json.IndexOf('"', colon);
                if (valStart < 0) break;
                int valEnd = json.IndexOf('"', valStart + 1);
                if (valEnd < 0) break;
                result[key] = json.Substring(valStart + 1, valEnd - valStart - 1);
                i = valEnd + 1;
            }
            return result;
        }
    }
}
