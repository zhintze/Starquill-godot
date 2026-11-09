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

        private HashSet<string> Restricted(string species) =>
            new HashSet<string>(data.Registry.Species[species].ItemRestrictions ?? Array.Empty<string>());

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

        /// Strips and rerolls gear only; slots in c.LockedSlots keep their item (P21).
        public void RollGear(StudioCharacter c, RandomSettings s, Random rng)
        {
            var kept = c.LockedSlots.Where(c.Gear.ContainsKey).ToDictionary(slot => slot, slot => c.Gear[slot]);
            c.Gear.Clear();
            var restricted = Restricted(c.SpeciesName);

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

            foreach (var kv in kept) c.Gear[kv.Key] = kv.Value;
            ResolveHands(c, kept.ContainsKey(EquipmentSlot.OffHand) ? EquipmentSlot.OffHand : EquipmentSlot.MainHand);
        }

        /// A new random item for one slot, any type of that slot by the type
        /// weights (the slot chance is ignored, P24). Empty when no type can be picked.
        public void RollSlot(StudioCharacter c, EquipmentSlot slot, RandomSettings s, Random rng)
        {
            var restricted = Restricted(c.SpeciesName);
            StudioItem item = null;
            if (slot == EquipmentSlot.MainHand || slot == EquipmentSlot.OffHand)
            {
                var w = PickWeighted(data.Catalog.WeaponEntries.Where(e => !restricted.Contains(e.ItemType)
                    && (slot == EquipmentSlot.MainHand ? !IsShield(e.ItemType) : e.HandType == "one_handed")).ToList(), s, rng);
                if (w != null) item = MakeWeapon(w, rng);
            }
            else
            {
                var entry = PickWeighted(data.Catalog.GetByPrefix(PrefixFor(slot)).Where(e => !restricted.Contains(e.ItemType)).ToList(), s, rng);
                if (entry != null) item = MakeArmor(entry, rng);
            }
            if (item == null) c.Gear.Remove(slot);
            else c.Gear[slot] = item;
            ResolveHands(c, slot);
        }

        /// A two-handed main hand and an off-hand item cannot both stay: the
        /// one in winner stays, the other goes.
        public void ResolveHands(StudioCharacter c, EquipmentSlot winner)
        {
            if (c.Gear.TryGetValue(EquipmentSlot.MainHand, out var main) && main != null
                && data.Catalog.GetWeapon(main.ItemType)?.HandType == "two_handed"
                && c.Gear.ContainsKey(EquipmentSlot.OffHand))
                c.Gear.Remove(winner == EquipmentSlot.OffHand ? EquipmentSlot.MainHand : EquipmentSlot.OffHand);
        }

        /// Item type prefix for an armor or misc slot; null for the hands.
        public static string PrefixFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "hd";
                case EquipmentSlot.Torso: return "tr";
                case EquipmentSlot.Arms: return "ar";
                case EquipmentSlot.Legs: return "lg";
                case EquipmentSlot.Feet: return "fe";
                case EquipmentSlot.MainHand:
                case EquipmentSlot.OffHand: return null;
                default: return "mc";
            }
        }

        /// New species and a new body for it; gear the new species can wear stays.
        public void ChangeSpecies(StudioCharacter c, string species, RandomSettings s, Random rng)
        {
            c.SpeciesName = species;
            RollBody(c, s, rng);
            var restricted = Restricted(species);
            foreach (var slot in c.Gear.Keys.ToList())
                if (restricted.Contains(c.Gear[slot].ItemType)) c.Gear.Remove(slot);
        }

        /// This exact variant with random colors; null for an unknown type.
        /// A modular weapon uses the number on every layer.
        public StudioItem MakeItem(string itemType, int itemNum, Random rng)
        {
            var weapon = data.Catalog.GetWeapon(itemType);
            var entry = weapon == null ? data.Catalog.GetEntry(itemType) : null;
            if (weapon == null && entry == null) return null;

            var item = new StudioItem
            {
                ItemType = itemType,
                ItemNum = itemNum,
                BaseColor = data.Registry.Colors.GetRandomColor("main", rng)
            };
            if (weapon != null && weapon.Modular)
                item.LayerVariants = weapon.LayerCodes.Select(_ => itemNum).ToArray();
            AddVariance(item, weapon != null ? weapon.LayerColorVariance : entry.LayerColorVariance, rng);
            return item;
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

        internal static bool IsShield(string itemType) => itemType == "w08" || itemType == "w09";

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
