using System.Collections.Generic;
using System.Linq;
using Starquill.Data;
using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Display
{
    public class SpeciesDisplayData
    {
        public string Name;
        public string BackArm, Body, Ears, Eyes, FacialDetail, FacialHair;
        public string FrontArm, Head, Legs, Mouth, Nose;
        public string[] Hair;
        public string[] ItemRestrictions;
        public string[] SkinColor;
        public string[] HairColor;
        public string[] EyesColor;
        public string[] FacialDetailColor;
        /// species_extra_parts.csv rows of this species, in file order:
        /// extra drawings and species parts with their own color.
        public SpeciesPartRule[] PartRules = System.Array.Empty<SpeciesPartRule>();
        public float XScale = 1f, YScale = 1f;
        public float HeadYOffset;
        public float HeadZoom = 0.5f;
    }

    /// One species_extra_parts.csv row (docs/plans/2026-11-08-species-extra-parts-plan.md).
    public class SpeciesPartRule
    {
        public const string Skin = "skin", HairColor = "hair", EyesColor = "eyes", FacialDetailColor = "facial_detail";

        /// A species.csv part column (legs, mouth, facial_detail, ...) or, for
        /// an extra, its drawing: body image, part group or full part token.
        public string Part;
        public bool IsExtra;
        /// skin, hair, eyes, facial_detail, or a palette name.
        public string ColorSource;
        /// The palette's colors when ColorSource is a palette; null otherwise.
        public Color[] Palette;
        /// Rules of one species with the same key share one rolled color.
        public string ColorKey;
        /// Extras only: how often the part is drawn (1 = always).
        public float Chance = 1f;
    }

    public class EquipmentDisplayData
    {
        public string ItemType;
        public string Description;
        public int[] Variants = System.Array.Empty<int>();
        public int[] LayerCodes;
        public int[] LayerColorVariance;
        public int[] HiddenLayers;
        public bool Modular;
    }

    public class DisplayDataRegistry
    {
        public Dictionary<string, SpeciesDisplayData> Species { get; private set; } = new();
        public Dictionary<string, EquipmentDisplayData> Equipment { get; private set; } = new();
        /// Part group -> variant numbers that exist on disk (gaps allowed).
        public Dictionary<string, int[]> ModularPartVariants { get; private set; } = new();
        public Dictionary<string, int[]> SpeciesLayerMappings { get; private set; } = new();
        public ColorManager Colors { get; private set; } = new();

        private static DisplayDataRegistry _instance;
        public static DisplayDataRegistry Instance => _instance ??= new DisplayDataRegistry();

        public void LoadAll()
        {
            Load(ArtTables.LoadFromResources());
            Colors.LoadOverridesFromResources();
        }

        public void Load(ArtTableData data)
        {
            Species.Clear();
            Equipment.Clear();
            ModularPartVariants.Clear();
            SpeciesLayerMappings = new Dictionary<string, int[]>();
            Colors.LoadPalettes(data.palettes);

            var paletteColors = data.palettes.ToDictionary(p => p.name, p => p.hex.Select(ColorManager.ParseHex).ToArray());
            var partColumns = new HashSet<string>(ArtTablesConverter.PartColumns);

            foreach (var g in data.partGroups)
            {
                SpeciesLayerMappings[g.group] = g.layers;
                ModularPartVariants[g.group] = g.variants;
            }

            foreach (var e in data.equipment)
            {
                Equipment[e.itemType] = new EquipmentDisplayData
                {
                    ItemType = e.itemType, Description = e.description, Variants = e.variants,
                    LayerCodes = e.layerCodes, LayerColorVariance = e.colorVarianceLayers,
                    HiddenLayers = e.hiddenLayers, Modular = false
                };
            }

            foreach (var w in data.weapons)
            {
                Equipment[w.itemType] = new EquipmentDisplayData
                {
                    ItemType = w.itemType, Description = w.description,
                    LayerCodes = w.layerCodes, LayerColorVariance = w.colorVarianceLayers,
                    HiddenLayers = w.hiddenLayers, Modular = w.modular
                };
            }

            foreach (var s in data.species)
            {
                Species[s.name] = new SpeciesDisplayData
                {
                    Name = s.name,
                    BackArm = s.backArm, Body = s.body, Ears = s.ears, Eyes = s.eyes,
                    FacialDetail = s.facialDetail, FacialHair = s.facialHair, FrontArm = s.frontArm,
                    Head = s.head, Legs = s.legs, Mouth = s.mouth, Nose = s.nose,
                    Hair = s.hairGroups, ItemRestrictions = s.itemRestrictions,
                    SkinColor = PaletteField(s.skinPalette),
                    HairColor = PaletteField(s.hairPalette),
                    EyesColor = PaletteField(s.eyePalette),
                    FacialDetailColor = PaletteField(s.facialDetailPalette),
                    PartRules = data.speciesExtraParts
                        .Where(x => x.species == s.name)
                        .Select(x => new SpeciesPartRule
                        {
                            Part = x.part,
                            IsExtra = !partColumns.Contains(x.part),
                            ColorSource = x.color,
                            Palette = paletteColors.TryGetValue(x.color ?? "", out var pc) ? pc : null,
                            ColorKey = string.IsNullOrEmpty(x.colorGroup) ? x.part : x.colorGroup,
                            Chance = x.chance
                        })
                        .ToArray(),
                    XScale = s.xScale, YScale = s.yScale, HeadYOffset = s.headYOffset, HeadZoom = s.headZoom
                };
            }
        }

        /// Blank = null, which SpeciesInstanceData reads as "use main".
        private static string[] PaletteField(string palette) =>
            string.IsNullOrEmpty(palette) ? null : new[] { palette };

        public string PickModularImageNum(string groupType, System.Random rng = null)
        {
            if (!ModularPartVariants.TryGetValue(groupType, out var variants) || variants.Length == 0)
                return "0001";
            int i = rng != null ? rng.Next(variants.Length) : UnityEngine.Random.Range(0, variants.Length);
            return variants[i].ToString("D4");
        }
    }
}
