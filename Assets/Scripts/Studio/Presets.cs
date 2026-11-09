using System;
using System.Collections.Generic;
using System.Linq;
using Starquill.Core;
using Starquill.Display;
using UnityEngine;

namespace Starquill.Studio
{
    // ---- preset files (JsonUtility, P25/P26): choices only, colors as #RRGGBB ----

    [Serializable]
    public class LayerColorPreset
    {
        public int layer;
        public string color;
    }

    [Serializable]
    public class PartColorPreset
    {
        public string group;
        public string color;
    }

    [Serializable]
    public class PartPreset
    {
        public string group;
        public string variant;
    }

    [Serializable]
    public class ItemPreset
    {
        public string slot;
        public string type;
        public int number;
        public int[] parts = Array.Empty<int>();   // modular weapons: number per layer
        public string color;
        public List<LayerColorPreset> secondColors = new List<LayerColorPreset>();
    }

    [Serializable]
    public class CharacterPreset
    {
        public int version = 1;
        public string species;
        public string skinColor, hairColor, eyeColor, facialDetailColor;
        public string hairGroup;
        public List<PartPreset> parts = new List<PartPreset>();
        /// species_extra_parts.csv color groups -> color.
        public List<PartColorPreset> partColors = new List<PartColorPreset>();
        /// Optional extras that are not drawn.
        public List<string> omittedExtras = new List<string>();
        public bool hideFacialHair, hideFacialDetail, locked;
        public List<string> lockedSlots = new List<string>();
        public List<ItemPreset> gear = new List<ItemPreset>();
    }

    [Serializable]
    public class CastPreset
    {
        public int version = 1;
        public List<CharacterPreset> characters = new List<CharacterPreset>();
        public List<string> forcedItems = new List<string>();
        public List<string> excludedSpecies = new List<string>();
    }

    [Serializable]
    public class TypeWeightPreset
    {
        public string type;
        public float weight;
    }

    [Serializable]
    public class RandomPreset
    {
        public int version = 1;
        public float head, torso, arms, legs, feet, mainHand, offHand, miscEach, facialHair, facialDetail;
        public List<TypeWeightPreset> weights = new List<TypeWeightPreset>();
    }

    /// Converts studio objects to preset files and back. Loading checks every
    /// choice against the current art (P27) and adds a line to warnings for
    /// each one it had to change.
    public static class Presets
    {
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        private static Color ParseColor(string hex, string where, List<string> warnings)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c))
            {
                c.a = 1f;
                return c;
            }
            warnings.Add($"{where}: color '{hex}' is not #RRGGBB; using white.");
            return Color.white;
        }

        // ---- character ----

        public static CharacterPreset FromCharacter(StudioCharacter c)
        {
            var p = new CharacterPreset
            {
                species = c.SpeciesName,
                skinColor = Hex(c.Body.SkinColor),
                hairColor = Hex(c.Body.HairColor),
                eyeColor = Hex(c.Body.EyesColor),
                facialDetailColor = Hex(c.Body.FacialDetailColor),
                hairGroup = c.Body.ChosenHairGroup ?? "",
                hideFacialHair = c.HideFacialHair,
                hideFacialDetail = c.HideFacialDetail,
                locked = c.Locked
            };
            foreach (var kv in c.Body.ModularImageNums.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                p.parts.Add(new PartPreset { group = kv.Key, variant = kv.Value });
            foreach (var kv in c.Body.PartColors.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                p.partColors.Add(new PartColorPreset { group = kv.Key, color = Hex(kv.Value) });
            p.omittedExtras.AddRange(c.Body.OmittedExtras.OrderBy(x => x, StringComparer.Ordinal));
            foreach (var slot in c.LockedSlots.OrderBy(s => s)) p.lockedSlots.Add(slot.ToString());
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!c.Gear.TryGetValue(slot, out var item) || item == null) continue;
                var ip = new ItemPreset
                {
                    slot = slot.ToString(),
                    type = item.ItemType,
                    number = item.ItemNum,
                    parts = item.LayerVariants != null ? (int[])item.LayerVariants.Clone() : Array.Empty<int>(),
                    color = Hex(item.BaseColor)
                };
                foreach (var kv in item.VarianceColors.OrderBy(kv => kv.Key))
                    ip.secondColors.Add(new LayerColorPreset { layer = kv.Key, color = Hex(kv.Value) });
                p.gear.Add(ip);
            }
            return p;
        }

        /// Null (plus a warning) when the species no longer exists.
        public static StudioCharacter ToCharacter(CharacterPreset p, StudioData d, string name, List<string> warnings)
        {
            string where = $"Preset {name}";
            if (p == null || string.IsNullOrEmpty(p.species) || !d.Registry.Species.TryGetValue(p.species, out var species))
            {
                warnings.Add($"{where}: species '{p?.species}' does not exist; character skipped.");
                return null;
            }

            var body = new SpeciesInstanceData
            {
                SpeciesName = species.Name,
                SkinColor = ParseColor(p.skinColor, where + " skin", warnings),
                HairColor = ParseColor(p.hairColor, where + " hair", warnings),
                EyesColor = ParseColor(p.eyeColor, where + " eyes", warnings),
                FacialDetailColor = ParseColor(p.facialDetailColor, where + " facial detail", warnings),
                XScale = species.XScale,
                YScale = species.YScale
            };
            var rules = species.PartRules ?? Array.Empty<SpeciesPartRule>();
            foreach (var pc in p.partColors ?? new List<PartColorPreset>())
            {
                if (!rules.Any(r => r.ColorKey == pc.group && r.Palette != null))
                {
                    warnings.Add($"{where}: {species.Name} has no color group '{pc.group}' any more; left out.");
                    continue;
                }
                body.PartColors[pc.group] = ParseColor(pc.color, $"{where} {pc.group}", warnings);
            }
            foreach (var rule in rules) // groups added since the preset was saved
                if (rule.Palette != null && rule.Palette.Length > 0 && !body.PartColors.ContainsKey(rule.ColorKey))
                    body.PartColors[rule.ColorKey] = rule.Palette[0];
            foreach (var part in p.omittedExtras ?? new List<string>())
            {
                if (rules.Any(r => r.IsExtra && r.Part == part && r.Chance < 1f)) body.OmittedExtras.Add(part);
                else warnings.Add($"{where}: {part} is not an optional extra of {species.Name} any more; it is drawn.");
            }

            foreach (var part in p.parts ?? new List<PartPreset>())
            {
                if (part == null || string.IsNullOrEmpty(part.group)) continue;
                if (!d.Registry.ModularPartVariants.TryGetValue(part.group, out var variants) || variants.Length == 0)
                {
                    warnings.Add($"{where}: part group {part.group} has no drawings; left out.");
                    continue;
                }
                bool exists = int.TryParse(part.variant, out int n) && variants.Contains(n);
                if (!exists)
                    warnings.Add($"{where}: {part.group}-{part.variant} has no drawing; using {part.group}-{variants[0]:D4}.");
                body.ModularImageNums[part.group] = (exists ? n : variants[0]).ToString("D4");
            }
            string hair = p.hairGroup ?? "";
            if (hair.Length > 0 && !(species.Hair ?? Array.Empty<string>()).Contains(hair))
            {
                warnings.Add($"{where}: {species.Name} has no hair group {hair}; no hair.");
                hair = "";
            }
            body.ChosenHairGroup = hair.Length > 0 ? hair : null;

            // Groups the species draws that the preset did not name (e.g. a group added later).
            var needed = BodyParts.Editable(species).Where(bp => bp.Group != null).Select(bp => bp.Group).ToList();
            if (body.ChosenHairGroup != null) needed.Add(ImageToken.Parse(body.ChosenHairGroup).GroupType);
            foreach (var group in needed)
                if (!body.ModularImageNums.ContainsKey(group))
                    body.ModularImageNums[group] = d.Registry.PickModularImageNum(group, new System.Random(0));

            var c = new StudioCharacter
            {
                SpeciesName = species.Name,
                Body = body,
                HideFacialHair = p.hideFacialHair,
                HideFacialDetail = p.hideFacialDetail,
                Locked = p.locked
            };
            foreach (var s in p.lockedSlots ?? new List<string>())
                if (Enum.TryParse(s, out EquipmentSlot slot)) c.LockedSlots.Add(slot);

            foreach (var ip in p.gear ?? new List<ItemPreset>())
            {
                if (ip == null) continue;
                if (!Enum.TryParse(ip.slot, out EquipmentSlot slot))
                {
                    warnings.Add($"{where}: unknown slot '{ip.slot}'; item {ip.type} left out.");
                    continue;
                }
                var item = ToItem(ip, d, $"{where} {ip.slot}", warnings);
                if (item != null) c.Gear[slot] = item;
            }
            return c;
        }

        private static StudioItem ToItem(ItemPreset ip, StudioData d, string where, List<string> warnings)
        {
            string token = $"{ip.type}-{ip.number:D4}";
            var weapon = d.Catalog.GetWeapon(ip.type ?? "");
            var entry = weapon == null ? d.Catalog.GetEntry(ip.type ?? "") : null;
            if (weapon == null && entry == null)
            {
                warnings.Add($"{where}: item type {ip.type} does not exist; slot left empty.");
                return null;
            }

            var item = new StudioItem { ItemType = ip.type, ItemNum = ip.number, BaseColor = ParseColor(ip.color, where, warnings) };
            if (weapon != null && weapon.Modular)
            {
                var parts = ip.parts != null && ip.parts.Length == weapon.LayerCodes.Length
                    ? ip.parts
                    : Enumerable.Repeat(ip.number, weapon.LayerCodes.Length).ToArray();
                for (int l = 0; l < parts.Length; l++)
                {
                    if (weapon.VariantsPerLayer[l].Contains(parts[l])) continue;
                    warnings.Add($"{where}: {ip.type} part {weapon.LayerCodes[l]} #{parts[l]:D4} has no image; slot left empty.");
                    return null;
                }
                item.LayerVariants = (int[])parts.Clone();
                item.ItemNum = parts[0];
            }
            else if (!ItemSearch.NumbersFor(d, ip.type).Contains(ip.number))
            {
                warnings.Add($"{where}: {token} has no art; slot left empty.");
                return null;
            }

            var allowed = weapon != null ? weapon.LayerColorVariance : entry.LayerColorVariance;
            foreach (var lc in ip.secondColors ?? new List<LayerColorPreset>())
                if (allowed != null && allowed.Contains(lc.layer))
                    item.VarianceColors[lc.layer] = ParseColor(lc.color, $"{where} layer {lc.layer}", warnings);
            return item;
        }

        // ---- cast ----

        public static CastPreset FromCast(IEnumerable<StudioCharacter> characters, IEnumerable<ForcedItem> forced, IEnumerable<string> excludedSpecies)
        {
            var p = new CastPreset();
            p.characters.AddRange(characters.Select(FromCharacter));
            p.forcedItems.AddRange(forced.Select(f => f.Token));
            p.excludedSpecies.AddRange(excludedSpecies);
            return p;
        }

        /// "hd01-0001" -> ForcedItem; null if the text is not a token.
        public static ForcedItem ParseToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            int dash = token.LastIndexOf('-');
            if (dash <= 0 || !int.TryParse(token.Substring(dash + 1), out int n)) return null;
            return new ForcedItem(token.Substring(0, dash), n);
        }

        // ---- random settings ----

        public static RandomPreset FromRandom(RandomSettings s)
        {
            var p = new RandomPreset
            {
                head = s.Head, torso = s.Torso, arms = s.Arms, legs = s.Legs, feet = s.Feet,
                mainHand = s.MainHand, offHand = s.OffHand, miscEach = s.MiscEach,
                facialHair = s.FacialHair, facialDetail = s.FacialDetail
            };
            foreach (var kv in s.TypeWeights.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                p.weights.Add(new TypeWeightPreset { type = kv.Key, weight = kv.Value });
            return p;
        }

        /// Writes the preset into s (the Bulk screen's settings object).
        public static void ToRandom(RandomPreset p, RandomSettings s)
        {
            s.Reset();
            s.Head = Clamp01(p.head); s.Torso = Clamp01(p.torso); s.Arms = Clamp01(p.arms);
            s.Legs = Clamp01(p.legs); s.Feet = Clamp01(p.feet); s.MainHand = Clamp01(p.mainHand);
            s.OffHand = Clamp01(p.offHand); s.MiscEach = Clamp01(p.miscEach);
            s.FacialHair = Clamp01(p.facialHair); s.FacialDetail = Clamp01(p.facialDetail);
            foreach (var w in p.weights ?? new List<TypeWeightPreset>())
                if (!string.IsNullOrEmpty(w.type)) s.TypeWeights[w.type] = Math.Max(0f, w.weight);
        }

        private static float Clamp01(float v) => Mathf.Clamp01(v);
    }
}
