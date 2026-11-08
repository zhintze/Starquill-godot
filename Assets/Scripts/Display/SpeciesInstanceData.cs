using System.Collections.Generic;
using UnityEngine;

namespace Starquill.Display
{
    public class SpeciesInstanceData
    {
        public string SpeciesName;
        public Color SkinColor = Color.white;
        public Color HairColor = Color.white;
        public Color EyesColor = Color.white;
        public Color FacialDetailColor = Color.white;
        public Dictionary<int, Color> SkinVarianceColors = new();
        public Dictionary<string, string> ModularImageNums = new();
        public float XScale = 1f, YScale = 1f;
        public string ChosenHairGroup;

        public Color GetVarianceColor(int layer)
        {
            return SkinVarianceColors.TryGetValue(layer, out var color) ? color : Color.white;
        }

        public static SpeciesInstanceData CreateFrom(SpeciesDisplayData species, DisplayDataRegistry registry, System.Random rng = null)
        {
            var instance = new SpeciesInstanceData();
            instance.SpeciesName = species.Name;
            instance.XScale = species.XScale;
            instance.YScale = species.YScale;

            var colors = registry.Colors;
            var skinPalette = colors.ResolveColorField(species.SkinColor);
            instance.SkinColor = skinPalette.Length > 0
                ? skinPalette[rng?.Next(skinPalette.Length) ?? Random.Range(0, skinPalette.Length)]
                : Color.white;

            var hairPalette = species.HairColor != null && species.HairColor.Length > 0
                ? colors.ResolveColorField(species.HairColor)
                : colors.GetPalette("main");
            instance.HairColor = hairPalette[rng?.Next(hairPalette.Length) ?? Random.Range(0, hairPalette.Length)];

            var eyesPalette = species.EyesColor != null && species.EyesColor.Length > 0
                ? colors.ResolveColorField(species.EyesColor)
                : colors.GetPalette("main");
            instance.EyesColor = eyesPalette[rng?.Next(eyesPalette.Length) ?? Random.Range(0, eyesPalette.Length)];

            var fdPalette = species.FacialDetailColor != null && species.FacialDetailColor.Length > 0
                ? colors.ResolveColorField(species.FacialDetailColor)
                : colors.GetPalette("main");
            instance.FacialDetailColor = fdPalette[rng?.Next(fdPalette.Length) ?? Random.Range(0, fdPalette.Length)];

            if (species.SkinVarianceSets != null)
            {
                foreach (var set in species.SkinVarianceSets)
                {
                    if (set == null || set.HexColors == null || set.HexColors.Length == 0) continue;
                    var hex = set.HexColors[rng?.Next(set.HexColors.Length) ?? Random.Range(0, set.HexColors.Length)];
                    var color = ColorManager.ParseHex(hex);
                    if (set.Indices != null)
                    {
                        foreach (int layer in set.Indices)
                            instance.SkinVarianceColors[layer] = color;
                    }
                }
            }

            PickModularNums(instance, species, registry, rng);

            return instance;
        }

        private static void PickModularNums(SpeciesInstanceData instance, SpeciesDisplayData species, DisplayDataRegistry registry, System.Random rng)
        {
            // Every part field that may reference a modular group (e.g. the
            // skeleton's head "s01") must roll an image num here, or the part
            // is silently skipped at display time.
            var fields = new[] { species.Head, species.Body, species.Legs,
                                 species.BackArm, species.FrontArm,
                                 species.Eyes, species.Nose, species.Mouth, species.Ears,
                                 species.FacialHair, species.FacialDetail };
            foreach (var token in fields)
            {
                if (string.IsNullOrEmpty(token)) continue;
                var parsed = ImageToken.Parse(token);
                if (parsed.Kind == ImageTokenKind.ModularGroup && !instance.ModularImageNums.ContainsKey(parsed.GroupType))
                    instance.ModularImageNums[parsed.GroupType] = registry.PickModularImageNum(parsed.GroupType, rng);
            }

            if (species.OtherBodyParts != null)
            {
                foreach (var token in species.OtherBodyParts)
                {
                    if (string.IsNullOrEmpty(token)) continue;
                    var parsed = ImageToken.Parse(token);
                    if (parsed.Kind == ImageTokenKind.ModularGroup && !instance.ModularImageNums.ContainsKey(parsed.GroupType))
                        instance.ModularImageNums[parsed.GroupType] = registry.PickModularImageNum(parsed.GroupType, rng);
                }
            }

            if (species.Hair != null && species.Hair.Length > 0)
            {
                string hairChoice;
                if (species.Hair.Length == 1)
                {
                    hairChoice = species.Hair[0];
                }
                else
                {
                    int totalWeight = 0;
                    foreach (var h in species.Hair)
                    {
                        int amt = registry.ModularPartVariants.TryGetValue(h, out var hv) ? hv.Length : 0;
                        totalWeight += System.Math.Max(amt, 1);
                    }
                    int roll = rng?.Next(totalWeight) ?? Random.Range(0, totalWeight);
                    hairChoice = species.Hair[0];
                    int cumulative = 0;
                    foreach (var h in species.Hair)
                    {
                        int amt = registry.ModularPartVariants.TryGetValue(h, out var hv) ? hv.Length : 0;
                        cumulative += System.Math.Max(amt, 1);
                        if (roll < cumulative) { hairChoice = h; break; }
                    }
                }

                var parsed = ImageToken.Parse(hairChoice);
                if (parsed.Kind == ImageTokenKind.ModularGroup && !instance.ModularImageNums.ContainsKey(parsed.GroupType))
                    instance.ModularImageNums[parsed.GroupType] = registry.PickModularImageNum(parsed.GroupType, rng);

                instance.ChosenHairGroup = hairChoice;
            }
        }
    }
}
