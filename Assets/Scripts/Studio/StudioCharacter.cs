using System;
using System.Collections.Generic;
using Starquill.Core;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Studio
{
    /// One gear choice: what the game's EquipmentDisplayInfo needs, editable.
    public sealed class StudioItem
    {
        public string ItemType;
        public int ItemNum;
        public Color BaseColor = Color.white;
        public Dictionary<int, Color> VarianceColors = new Dictionary<int, Color>();
        public int[] LayerVariants; // modular weapons only, parallel to layer codes

        /// What the game's DisplayBuilder draws for this item.
        public EquipmentDisplayInfo ToDisplayInfo(bool offhand) => new EquipmentDisplayInfo
        {
            ItemType = ItemType,
            ItemNum = ItemNum,
            BaseColor = BaseColor,
            VarianceColors = new Dictionary<int, Color>(VarianceColors),
            LayerVariants = LayerVariants,
            IsOffhand = offhand
        };

        public StudioItem Clone() => new StudioItem
        {
            ItemType = ItemType,
            ItemNum = ItemNum,
            BaseColor = BaseColor,
            VarianceColors = new Dictionary<int, Color>(VarianceColors),
            LayerVariants = LayerVariants == null ? null : (int[])LayerVariants.Clone()
        };
    }

    /// A character on the studio's grid: species appearance (the game's
    /// SpeciesInstanceData), gear per slot, and studio-only flags.
    public sealed class StudioCharacter
    {
        public string SpeciesName;
        public SpeciesInstanceData Body;
        public readonly Dictionary<EquipmentSlot, StudioItem> Gear = new Dictionary<EquipmentSlot, StudioItem>();
        public bool HideFacialHair, HideFacialDetail;
        public bool Locked;
        /// Gear slots kept by Reroll gear / Equip Random (Single screen row Lock).
        public readonly HashSet<EquipmentSlot> LockedSlots = new HashSet<EquipmentSlot>();

        /// Deep copy (undo snapshots, presets).
        public StudioCharacter Clone()
        {
            var c = new StudioCharacter();
            c.CopyFrom(this);
            return c;
        }

        /// Makes this character an exact deep copy of other, keeping this object
        /// (the Bulk grid and the Single screen hold the same reference).
        public void CopyFrom(StudioCharacter other)
        {
            SpeciesName = other.SpeciesName;
            Body = CloneBody(other.Body);
            Gear.Clear();
            foreach (var kv in other.Gear) Gear[kv.Key] = kv.Value?.Clone();
            HideFacialHair = other.HideFacialHair;
            HideFacialDetail = other.HideFacialDetail;
            Locked = other.Locked;
            LockedSlots.Clear();
            LockedSlots.UnionWith(other.LockedSlots);
        }

        /// Field list mirrors SpeciesInstanceData.cs:8-16; a new field there must be copied here.
        public static SpeciesInstanceData CloneBody(SpeciesInstanceData b)
        {
            if (b == null) return null;
            return new SpeciesInstanceData
            {
                SpeciesName = b.SpeciesName,
                SkinColor = b.SkinColor,
                HairColor = b.HairColor,
                EyesColor = b.EyesColor,
                FacialDetailColor = b.FacialDetailColor,
                PartColors = new Dictionary<string, Color>(b.PartColors),
                OmittedExtras = new HashSet<string>(b.OmittedExtras),
                ModularImageNums = new Dictionary<string, string>(b.ModularImageNums),
                XScale = b.XScale,
                YScale = b.YScale,
                ChosenHairGroup = b.ChosenHairGroup
            };
        }

        public List<EquipmentDisplayInfo> ToDisplayList()
        {
            var list = new List<EquipmentDisplayInfo>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!Gear.TryGetValue(slot, out var item) || item == null) continue;
                list.Add(item.ToDisplayInfo(slot == EquipmentSlot.OffHand));
            }
            return list;
        }

        /// The species data to draw with: a copy with hidden facial parts blanked.
        public SpeciesDisplayData SpeciesView(SpeciesDisplayData s)
        {
            if (!HideFacialHair && !HideFacialDetail) return s;
            return new SpeciesDisplayData
            {
                Name = s.Name,
                BackArm = s.BackArm, Body = s.Body, Ears = s.Ears, Eyes = s.Eyes,
                FacialDetail = HideFacialDetail ? "" : s.FacialDetail,
                FacialHair = HideFacialHair ? "" : s.FacialHair,
                FrontArm = s.FrontArm, Head = s.Head, Legs = s.Legs, Mouth = s.Mouth, Nose = s.Nose,
                Hair = s.Hair, PartRules = s.PartRules, ItemRestrictions = s.ItemRestrictions,
                SkinColor = s.SkinColor, HairColor = s.HairColor, EyesColor = s.EyesColor,
                FacialDetailColor = s.FacialDetailColor,
                XScale = s.XScale, YScale = s.YScale, HeadYOffset = s.HeadYOffset, HeadZoom = s.HeadZoom
            };
        }
    }
}
