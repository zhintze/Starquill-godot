using System;
using System.Collections.Generic;
using System.Linq;
using Starquill.Core;

namespace Starquill.Studio
{
    /// The Bulk screen's Forced items: variants put on every unlocked
    /// character after each roll (P9-P11). The first item of a slot type
    /// takes its slot, later ones go to Misc 1-4; weapons: one main hand,
    /// one off hand, shields only in the off hand, two-handed leaves the
    /// off hand empty.
    public sealed class ForcedItems
    {
        private static readonly EquipmentSlot[] MiscSlots =
            { EquipmentSlot.Misc1, EquipmentSlot.Misc2, EquipmentSlot.Misc3, EquipmentSlot.Misc4 };

        private readonly StudioData data;
        private readonly List<ForcedItem> items = new List<ForcedItem>();

        public ForcedItems(StudioData data) { this.data = data; }

        public IReadOnlyList<ForcedItem> Items => items;

        /// False, with the reason, when the item cannot be added.
        public bool TryAdd(ForcedItem item, out string message)
        {
            message = null;
            if (items.Contains(item))
            {
                message = $"{item} is already forced.";
                return false;
            }
            if (!ItemSearch.NumbersFor(data, item.ItemType).Contains(item.ItemNum))
            {
                message = $"{item} has no art (an image for one of its layers is missing).";
                return false;
            }

            var next = new List<ForcedItem>(items) { item };
            if (item.IsWeapon)
            {
                if (!PlanHands(next.Where(i => i.IsWeapon), out _, out _, out message)) return false;
            }
            else if (MiscNeeded(next) > MiscSlots.Length)
            {
                message = $"Misc 1-4 are full, so {item} has no slot. Remove a forced item first.";
                return false;
            }
            items.Add(item);
            return true;
        }

        public bool Remove(ForcedItem item) => items.Remove(item);

        /// Puts every forced item the species can wear on c, replacing what
        /// is in those slots. An item already in its slot stays as it is.
        public void Apply(StudioCharacter c, StudioRoller roller, Random rng)
        {
            if (items.Count == 0) return;
            var restricted = new HashSet<string>(data.Registry.Species[c.SpeciesName].ItemRestrictions ?? Array.Empty<string>());
            var usable = items.Where(i => !restricted.Contains(i.ItemType)).ToList();
            var taken = new HashSet<EquipmentSlot>();

            PlanHands(usable.Where(i => i.IsWeapon), out var main, out var off, out _);
            if (main != null) Put(c, EquipmentSlot.MainHand, main, taken, roller, rng);
            if (off != null) Put(c, EquipmentSlot.OffHand, off, taken, roller, rng);

            var prefixes = new HashSet<string>();
            foreach (var item in usable.Where(i => !i.IsWeapon))
            {
                EquipmentSlot slot;
                if (item.Prefix != "mc" && prefixes.Add(item.Prefix))
                {
                    slot = NaturalSlot(item.Prefix);
                }
                else
                {
                    int free = Array.FindIndex(MiscSlots, s => !taken.Contains(s));
                    if (free < 0) continue;
                    slot = MiscSlots[free];
                }
                Put(c, slot, item, taken, roller, rng);
            }

            if (c.Gear.TryGetValue(EquipmentSlot.MainHand, out var held) && IsTwoHanded(held.ItemType)
                && c.Gear.ContainsKey(EquipmentSlot.OffHand))
                c.Gear.Remove(taken.Contains(EquipmentSlot.OffHand) ? EquipmentSlot.MainHand : EquipmentSlot.OffHand);
        }

        /// Empties every slot of c that holds one of these items.
        public static void Strip(StudioCharacter c, IEnumerable<ForcedItem> list)
        {
            var set = list.ToList();
            foreach (var slot in c.Gear.Keys.ToList())
                if (set.Any(i => i.Matches(c.Gear[slot]))) c.Gear.Remove(slot);
        }

        private static void Put(StudioCharacter c, EquipmentSlot slot, ForcedItem item, HashSet<EquipmentSlot> taken,
            StudioRoller roller, Random rng)
        {
            taken.Add(slot);
            if (c.Gear.TryGetValue(slot, out var current) && item.Matches(current)) return;
            c.Gear[slot] = roller.MakeItem(item.ItemType, item.ItemNum, rng);
        }

        private bool PlanHands(IEnumerable<ForcedItem> weapons, out ForcedItem main, out ForcedItem off, out string problem)
        {
            main = off = null;
            problem = null;
            foreach (var w in weapons)
            {
                if (StudioRoller.IsShield(w.ItemType))
                {
                    if (off != null) { problem = $"The off hand already holds {off}."; return false; }
                    off = w;
                }
                else if (main == null)
                {
                    main = w;
                }
                else if (off != null)
                {
                    problem = "Both hands are already forced. Remove a weapon first.";
                    return false;
                }
                else if (IsTwoHanded(w.ItemType))
                {
                    problem = $"{w} is two-handed and can only go in the main hand, which holds {main}.";
                    return false;
                }
                else
                {
                    off = w;
                }
            }
            if (main != null && off != null && IsTwoHanded(main.ItemType))
            {
                problem = $"{main} is two-handed, so the off hand cannot hold {off}.";
                return false;
            }
            return true;
        }

        private bool IsTwoHanded(string itemType) => data.Catalog.GetWeapon(itemType)?.HandType == "two_handed";

        /// Misc slots the armor list needs: every item after the first of its
        /// slot type, plus every mc item.
        private static int MiscNeeded(IEnumerable<ForcedItem> list) =>
            list.Where(i => !i.IsWeapon).GroupBy(i => i.Prefix).Sum(g => g.Key == "mc" ? g.Count() : g.Count() - 1);

        private static EquipmentSlot NaturalSlot(string prefix)
        {
            switch (prefix)
            {
                case "hd": return EquipmentSlot.Head;
                case "tr": return EquipmentSlot.Torso;
                case "ar": return EquipmentSlot.Arms;
                case "lg": return EquipmentSlot.Legs;
                case "fe": return EquipmentSlot.Feet;
                default: return EquipmentSlot.Misc1;
            }
        }
    }
}
