using System.Linq;
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
        /// Rolled color per SpeciesPartRule.ColorKey (rules with a palette).
        public Dictionary<string, Color> PartColors = new();
        /// Extras rolled absent (SpeciesPartRule.Chance below 1).
        public HashSet<string> OmittedExtras = new();
        public Dictionary<string, string> ModularImageNums = new();
        public float XScale = 1f, YScale = 1f;
        public string ChosenHairGroup;

        /// The color a part draws in: its rule's color, else fallback (plan E4).
        public Color ColorFor(SpeciesPartRule rule, Color fallback)
        {
            if (rule == null) return fallback;
            switch (rule.ColorSource)
            {
                case SpeciesPartRule.Skin: return SkinColor;
                case SpeciesPartRule.HairColor: return HairColor;
                case SpeciesPartRule.EyesColor: return EyesColor;
                case SpeciesPartRule.FacialDetailColor: return FacialDetailColor;
                default: return PartColors.TryGetValue(rule.ColorKey, out var c) ? c : fallback;
            }
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

            // One roll per palette color group, in file order (plan E3: same rolls as the old layer rules).
            foreach (var rule in species.PartRules ?? System.Array.Empty<SpeciesPartRule>())
            {
                if (rule.Palette == null || rule.Palette.Length == 0 || instance.PartColors.ContainsKey(rule.ColorKey)) continue;
                instance.PartColors[rule.ColorKey] = rule.Palette[rng?.Next(rule.Palette.Length) ?? Random.Range(0, rule.Palette.Length)];
            }

            PickModularNums(instance, species, registry, rng);

            // Optional extras last, so species without them roll exactly as before.
            foreach (var rule in species.PartRules ?? System.Array.Empty<SpeciesPartRule>())
            {
                if (!rule.IsExtra || rule.Chance >= 1f) continue;
                double roll = rng?.NextDouble() ?? Random.value;
                if (roll >= rule.Chance) instance.OmittedExtras.Add(rule.Part);
            }

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

            if (species.PartRules != null)
            {
                foreach (var token in species.PartRules.Where(r => r.IsExtra).Select(r => r.Part))
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
