using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starquill.Data.Art
{
    /// Cell formats used by the art tables. Lists are space separated;
    /// commas and semicolons are also accepted because spreadsheets tempt them.
    public static class ArtCells
    {
        private static readonly char[] ListSeparators = { ' ', ',', ';', '\t' };

        public static string[] Strings(string cell) =>
            (cell ?? "").Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);

        public static bool TryInts(string cell, out int[] values)
        {
            var parts = Strings(cell);
            var list = new List<int>(parts.Length);
            foreach (var p in parts)
            {
                if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
                {
                    values = Array.Empty<int>();
                    return false;
                }
                list.Add(v);
            }
            values = list.ToArray();
            return true;
        }

        public static bool TryBool(string cell, out bool value)
        {
            switch ((cell ?? "").Trim().ToLowerInvariant())
            {
                case "y": case "yes": case "true": case "1": value = true; return true;
                case "": case "n": case "no": case "false": case "0": value = false; return true;
                default: value = false; return false;
            }
        }

        public static bool TryHex(string cell, out string hex)
        {
            hex = (cell ?? "").Trim().TrimStart('#').ToUpperInvariant();
            if (hex.Length != 6) return false;
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        public static bool TryFloat(string cell, float fallback, out float value)
        {
            string s = (cell ?? "").Trim();
            if (s.Length == 0) { value = fallback; return true; }
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && s.IndexOf(',') < 0;
        }
    }
}
