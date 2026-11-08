using System;
using System.Collections.Generic;
using Starquill.Core;

namespace Starquill.Studio
{
    /// Chances and weights for random characters. Defaults copy the Godot
    /// randomizer (godot-archive/scripts/core/starquill_data.gd:30-46).
    public sealed class RandomSettings
    {
        public float Head = 0.9f, Torso = 1f, Arms = 0.8f, Legs = 1f, Feet = 0.7f;
        public float MainHand = 0.6f;
        /// Only when the main hand is empty or one-handed.
        public float OffHand = 0.3f;
        /// Each of Misc 1-4, filled from mc items.
        public float MiscEach = 0.3f;
        public float FacialHair = 0.7f, FacialDetail = 0.8f;
        /// Item type -> weight within its slot; missing means 1, 0 means never.
        public Dictionary<string, float> TypeWeights = new Dictionary<string, float>();

        public float WeightOf(string itemType) =>
            TypeWeights.TryGetValue(itemType, out var w) ? Math.Max(0f, w) : 1f;

        public float ChanceFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return Head;
                case EquipmentSlot.Torso: return Torso;
                case EquipmentSlot.Arms: return Arms;
                case EquipmentSlot.Legs: return Legs;
                case EquipmentSlot.Feet: return Feet;
                case EquipmentSlot.MainHand: return MainHand;
                case EquipmentSlot.OffHand: return OffHand;
                default: return MiscEach;
            }
        }
    }
}
