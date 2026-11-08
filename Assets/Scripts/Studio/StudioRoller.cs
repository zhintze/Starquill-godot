using System;
using System.Collections.Generic;
using System.Linq;
using Starquill.Core;
using Starquill.Display;
using Starquill.Equipment;

namespace Starquill.Studio
{
    /// Random bodies and gear for studio characters, from the game's registry,
    /// catalog and palettes. Variant numbers come only from files on disk.
    public sealed class StudioRoller
    {
        private static readonly (EquipmentSlot slot, string prefix)[] ArmorSlots =
        {
            (EquipmentSlot.Head, "hd"), (EquipmentSlot.Torso, "tr"), (EquipmentSlot.Arms, "ar"),
            (EquipmentSlot.Legs, "lg"), (EquipmentSlot.Feet, "fe")
        };

        private static readonly EquipmentSlot[] MiscSlots =
            { EquipmentSlot.Misc1, EquipmentSlot.Misc2, EquipmentSlot.Misc3, EquipmentSlot.Misc4 };

        private readonly StudioData data;

        public StudioRoller(StudioData data) { this.data = data; }

        public StudioCharacter RollCharacter(IReadOnlyList<string> species, RandomSettings s, Random rng)
        {
            var c = new StudioCharacter { SpeciesName = species[rng.Next(species.Count)] };
            RollBody(c, s, rng);
            RollGear(c, s, rng);
            return c;
        }

        /// New appearance for the character's current species.
        public void RollBody(StudioCharacter c, RandomSettings s, Random rng)
        {
            var species = data.Registry.Species[c.SpeciesName];
            c.Body = SpeciesInstanceData.CreateFrom(species, data.Registry, rng);
            c.HideFacialHair = !string.IsNullOrEmpty(species.FacialHair) && rng.NextDouble() >= s.FacialHair;
            c.HideFacialDetail = !string.IsNullOrEmpty(species.FacialDetail) && rng.NextDouble() >= s.FacialDetail;
        }

        /// Strips and rerolls gear only.
        public void RollGear(StudioCharacter c, RandomSettings s, Random rng)
        {
            c.Gear.Clear();
            var restricted = new HashSet<string>(data.Registry.Species[c.SpeciesName].ItemRestrictions ?? Array.Empty<string>());

            foreach (var (slot, prefix) in ArmorSlots)
                if (rng.NextDouble() < s.ChanceFor(slot))
                    PutArmor(c, slot, prefix, restricted, s, rng);

            WeaponEntry main = null;
            if (rng.NextDouble() < s.MainHand)
            {
                main = PickWeighted(data.Catalog.WeaponEntries
                    .Where(w => !IsShield(w.ItemType) && !restricted.Contains(w.ItemType)).ToList(), s, rng);
                if (main != null) c.Gear[EquipmentSlot.MainHand] = MakeWeapon(main, rng);
            }

            bool twoHanded = main != null && main.HandType == "two_handed";
            if (!twoHanded && rng.NextDouble() < s.OffHand)
            {
                var off = PickWeighted(data.Catalog.WeaponEntries
                    .Where(w => w.HandType == "one_handed" && !restricted.Contains(w.ItemType)).ToList(), s, rng);
                if (off != null) c.Gear[EquipmentSlot.OffHand] = MakeWeapon(off, rng);
            }

            foreach (var slot in MiscSlots)
                if (rng.NextDouble() < s.MiscEach)
                    PutArmor(c, slot, "mc", restricted, s, rng);
        }

        private void PutArmor(StudioCharacter c, EquipmentSlot slot, string prefix, HashSet<string> restricted, RandomSettings s, Random rng)
        {
            var entry = PickWeighted(data.Catalog.GetByPrefix(prefix).Where(e => !restricted.Contains(e.ItemType)).ToList(), s, rng);
            if (entry != null) c.Gear[slot] = MakeArmor(entry, rng);
        }

        public StudioItem MakeArmor(CatalogEntry entry, Random rng)
        {
            var item = new StudioItem
            {
                ItemType = entry.ItemType,
                ItemNum = entry.Variants[rng.Next(entry.Variants.Length)],
                BaseColor = data.Registry.Colors.GetRandomColor("main", rng)
            };
            AddVariance(item, entry.LayerColorVariance, rng);
            return item;
        }

        public StudioItem MakeWeapon(WeaponEntry entry, Random rng)
        {
            var item = new StudioItem { ItemType = entry.ItemType, BaseColor = data.Registry.Colors.GetRandomColor("main", rng) };
            if (entry.Modular)
            {
                item.LayerVariants = entry.VariantsPerLayer.Select(v => v[rng.Next(v.Length)]).ToArray();
                item.ItemNum = item.LayerVariants[0];
            }
            else
            {
                var shared = entry.VariantsPerLayer[0];
                item.ItemNum = shared[rng.Next(shared.Length)];
            }
            AddVariance(item, entry.LayerColorVariance, rng);
            return item;
        }

        private void AddVariance(StudioItem item, int[] layers, Random rng)
        {
            if (layers == null) return;
            foreach (int layer in layers)
                item.VarianceColors[layer] = data.Registry.Colors.GetRandomColor("main", rng);
        }

        private static bool IsShield(string itemType) => itemType == "w08" || itemType == "w09";

        private static T PickWeighted<T>(List<T> items, RandomSettings s, Random rng) where T : class
        {
            float total = 0f;
            foreach (var it in items) total += s.WeightOf(TypeOf(it));
            if (total <= 0f) return null;
            double roll = rng.NextDouble() * total;
            foreach (var it in items)
            {
                roll -= s.WeightOf(TypeOf(it));
                if (roll < 0) return it;
            }
            return items.LastOrDefault(it => s.WeightOf(TypeOf(it)) > 0f);
        }

        private static string TypeOf(object it) =>
            it is CatalogEntry e ? e.ItemType : ((WeaponEntry)it).ItemType;
    }
}
