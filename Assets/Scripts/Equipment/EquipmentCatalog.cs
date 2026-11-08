using System.Collections.Generic;
using System.Linq;
using Starquill.Core;
using Starquill.Data;
using Starquill.Data.Art;

namespace Starquill.Equipment
{
    public class CatalogEntry
    {
        public string ItemType;
        public string Description;
        public int[] Variants;
        public int[] LayerCodes;
        public int[] HiddenLayers;
        public int[] LayerColorVariance;
        public bool Modular;
    }

    public class WeaponEntry
    {
        public string ItemType;
        public string Description;
        public int[][] VariantsPerLayer;
        public int[] LayerCodes;
        public int[] HiddenLayers;
        public int[] LayerColorVariance;
        public bool Modular;
        public string HandType;
    }

    public class EquipmentCatalog
    {
        public List<CatalogEntry> AllEntries { get; private set; } = new();
        public List<WeaponEntry> WeaponEntries { get; private set; } = new();

        private readonly Dictionary<string, CatalogEntry> byType = new();
        private readonly Dictionary<string, List<CatalogEntry>> byPrefix = new();
        private readonly Dictionary<string, WeaponEntry> weaponsByType = new();

        public void LoadFromResources() => Load(ArtTables.LoadFromResources());

        public void Load(ArtTableData data)
        {
            AllEntries.Clear(); byType.Clear(); byPrefix.Clear();
            WeaponEntries.Clear(); weaponsByType.Clear();

            foreach (var row in data.equipment)
            {
                if (string.IsNullOrEmpty(row.itemType) || row.variants == null || row.variants.Length == 0)
                    continue; // no art yet: never rolled (the bake already warned)
                var entry = new CatalogEntry
                {
                    ItemType = row.itemType, Description = row.description, Variants = row.variants,
                    LayerCodes = row.layerCodes, HiddenLayers = row.hiddenLayers,
                    LayerColorVariance = row.colorVarianceLayers, Modular = false
                };
                AllEntries.Add(entry);
                byType[entry.ItemType] = entry;
                string prefix = entry.ItemType.Substring(0, 2);
                if (!byPrefix.TryGetValue(prefix, out var list)) byPrefix[prefix] = list = new List<CatalogEntry>();
                list.Add(entry);
            }

            foreach (var row in data.weapons)
            {
                var perLayer = row.variantsPerLayer.Select(l => l.values ?? System.Array.Empty<int>()).ToArray();
                if (string.IsNullOrEmpty(row.itemType) || perLayer.Length == 0 || perLayer.Any(v => v.Length == 0))
                    continue;
                var entry = new WeaponEntry
                {
                    ItemType = row.itemType, Description = row.description, VariantsPerLayer = perLayer,
                    LayerCodes = row.layerCodes, HiddenLayers = row.hiddenLayers,
                    LayerColorVariance = row.colorVarianceLayers, Modular = row.modular,
                    HandType = string.IsNullOrEmpty(row.handType) ? "one_handed" : row.handType
                };
                WeaponEntries.Add(entry);
                weaponsByType[entry.ItemType] = entry;
            }
        }

        public List<CatalogEntry> GetByPrefix(string prefix)
        {
            prefix = prefix.ToLower();
            return byPrefix.TryGetValue(prefix, out var list) ? list : new List<CatalogEntry>();
        }

        public CatalogEntry GetEntry(string itemType)
        {
            return byType.TryGetValue(itemType, out var entry) ? entry : null;
        }

        public WeaponEntry GetWeapon(string itemType)
        {
            return weaponsByType.TryGetValue(itemType, out var entry) ? entry : null;
        }

        public static EquipmentSlot SlotForPrefix(string prefix)
        {
            return prefix.ToLower() switch
            {
                "hd" => EquipmentSlot.Head,
                "tr" => EquipmentSlot.Torso,
                "ar" => EquipmentSlot.Arms,
                "lg" => EquipmentSlot.Legs,
                "fe" => EquipmentSlot.Feet,
                "mc" => EquipmentSlot.Misc1,
                "w" => EquipmentSlot.MainHand,
                _ => EquipmentSlot.Misc1
            };
        }
    }
}
