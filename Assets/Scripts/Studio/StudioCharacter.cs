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

        public List<EquipmentDisplayInfo> ToDisplayList()
        {
            var list = new List<EquipmentDisplayInfo>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!Gear.TryGetValue(slot, out var item) || item == null) continue;
                list.Add(new EquipmentDisplayInfo
                {
                    ItemType = item.ItemType,
                    ItemNum = item.ItemNum,
                    BaseColor = item.BaseColor,
                    VarianceColors = new Dictionary<int, Color>(item.VarianceColors),
                    LayerVariants = item.LayerVariants,
                    IsOffhand = slot == EquipmentSlot.OffHand
                });
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
                Hair = s.Hair, OtherBodyParts = s.OtherBodyParts, ItemRestrictions = s.ItemRestrictions,
                SkinColor = s.SkinColor, HairColor = s.HairColor, EyesColor = s.EyesColor,
                FacialDetailColor = s.FacialDetailColor, SkinVarianceSets = s.SkinVarianceSets,
                XScale = s.XScale, YScale = s.YScale, HeadYOffset = s.HeadYOffset, HeadZoom = s.HeadZoom
            };
        }
    }
}
