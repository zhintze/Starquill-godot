using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Starquill.Studio
{
    /// The Bulk screen's search box: an item token (hd01-0001, partial too),
    /// a file name or pasted path, or a word from the item description.
    /// Only variants whose every layer image exists are listed.
    public static class ItemSearch
    {
        public sealed class Result
        {
            public readonly List<ForcedItem> Items = new List<ForcedItem>();
            /// Matches before the max cut.
            public int Total;
        }

        private static readonly Regex EquipmentFile = new Regex(@"^([a-z]{2}\d{2})-(\d{4})-\d{3}(\.png)?$");
        private static readonly Regex WeaponFile = new Regex(@"^(w\d{2})-\d{1,3}-(\d{4})(\.png)?$");

        /// Variant numbers that exist on every layer of the type, ascending;
        /// empty for an unknown type.
        public static int[] NumbersFor(StudioData d, string itemType)
        {
            var weapon = d.Catalog.GetWeapon(itemType);
            if (weapon != null)
            {
                IEnumerable<int> common = weapon.VariantsPerLayer[0];
                foreach (var layer in weapon.VariantsPerLayer.Skip(1)) common = common.Intersect(layer);
                return common.OrderBy(n => n).ToArray();
            }
            var entry = d.Catalog.GetEntry(itemType);
            return entry != null ? entry.Variants.OrderBy(n => n).ToArray() : Array.Empty<int>();
        }

        public static string Description(StudioData d, string itemType) =>
            d.Catalog.GetEntry(itemType)?.Description ?? d.Catalog.GetWeapon(itemType)?.Description ?? "";

        private static IEnumerable<string> AllTypes(StudioData d) =>
            d.Catalog.AllEntries.Select(e => e.ItemType)
                .Concat(d.Catalog.WeaponEntries.Select(w => w.ItemType))
                .OrderBy(t => t, StringComparer.Ordinal);

        public static Result Find(StudioData d, string query, int max = 15)
        {
            var r = new Result();
            string q = (query ?? "").Trim().Trim('"').ToLowerInvariant();
            int slash = Math.Max(q.LastIndexOf('/'), q.LastIndexOf('\\'));
            if (slash >= 0) q = q.Substring(slash + 1); // a pasted path: keep the file name
            if (q.Length == 0) return r;

            var file = EquipmentFile.Match(q);
            if (!file.Success) file = WeaponFile.Match(q);
            if (file.Success)
            {
                string type = file.Groups[1].Value;
                int num = int.Parse(file.Groups[2].Value);
                if (NumbersFor(d, type).Contains(num))
                {
                    r.Items.Add(new ForcedItem(type, num));
                    r.Total = 1;
                }
                return r;
            }

            foreach (var type in AllTypes(d))
            {
                bool descriptionHit = Description(d, type).ToLowerInvariant().Contains(q);
                foreach (int n in NumbersFor(d, type))
                {
                    var item = new ForcedItem(type, n);
                    if (!descriptionHit && !item.Token.Contains(q)) continue;
                    r.Total++;
                    if (r.Items.Count < max) r.Items.Add(item);
                }
            }
            return r;
        }
    }
}
