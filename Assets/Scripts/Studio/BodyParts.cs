using System;
using System.Collections.Generic;
using Starquill.Display;

namespace Starquill.Studio
{
    /// One species part the Single screen lets the artist change.
    public sealed class BodyPart
    {
        public string Field;   // DisplayBuilder field name: eyes, facialHair, otherBodyParts, ...
        public string Label;   // plain words for the screen
        public string Token;   // the species.csv cell
        public string Group;   // part group with a choice of drawings; null for a fixed drawing
        public bool CanBeNone; // facial hair and facial detail (P20)
    }

    /// Which species parts have a choice of drawings, and which parts an
    /// item hides. Field order follows DisplayBuilder.BuildSpeciesPieces.
    public static class BodyParts
    {
        private static readonly (string field, string label, Func<SpeciesDisplayData, string> get)[] Fields =
        {
            ("backArm", "Back arm", s => s.BackArm), ("legs", "Legs", s => s.Legs), ("body", "Body", s => s.Body),
            ("head", "Head", s => s.Head), ("ears", "Ears", s => s.Ears), ("eyes", "Eyes", s => s.Eyes),
            ("nose", "Nose", s => s.Nose), ("mouth", "Mouth", s => s.Mouth),
            ("facialHair", "Facial hair", s => s.FacialHair), ("facialDetail", "Facial detail", s => s.FacialDetail),
            ("frontArm", "Front arm", s => s.FrontArm)
        };

        /// Parts with a choice of drawings (one entry per part group), plus
        /// facial hair / facial detail even when fixed (they can be None).
        /// Hair is separate: see HairGroups.
        public static List<BodyPart> Editable(SpeciesDisplayData s)
        {
            var list = new List<BodyPart>();
            var seen = new HashSet<string>();
            foreach (var (field, label, get) in Fields) Add(field, label, get(s));
            if (s.OtherBodyParts != null)
                foreach (var token in s.OtherBodyParts) Add("otherBodyParts", "Other part", token);
            return list;

            void Add(string field, string label, string token)
            {
                if (string.IsNullOrEmpty(token)) return;
                var parsed = ImageToken.Parse(token);
                bool group = parsed.Kind == ImageTokenKind.ModularGroup;
                bool canBeNone = field == "facialHair" || field == "facialDetail";
                if (group && !seen.Add(parsed.GroupType)) return;
                if (!group && !canBeNone) return;
                list.Add(new BodyPart
                {
                    Field = field, Label = label, Token = token,
                    Group = group ? parsed.GroupType : null, CanBeNone = canBeNone
                });
            }
        }

        /// Hair group tokens the species can use (species.csv hair_groups).
        public static IReadOnlyList<string> HairGroups(SpeciesDisplayData s) => s.Hair ?? Array.Empty<string>();

        /// Layers a species cell draws on.
        public static IEnumerable<int> LayersOf(string token, DisplayDataRegistry registry)
        {
            if (string.IsNullOrEmpty(token)) yield break;
            var parsed = ImageToken.Parse(token);
            if (parsed.Kind == ImageTokenKind.ModularGroup)
            {
                if (registry.SpeciesLayerMappings.TryGetValue(parsed.GroupType, out var layers))
                    foreach (int l in layers) yield return l;
            }
            else if (parsed.Layer >= 0)
            {
                yield return parsed.Layer;
            }
        }

        /// Labels of the parts drawn on these layers (a skin variance row's name).
        public static List<string> PartsOn(SpeciesDisplayData species, DisplayDataRegistry registry, string hairGroup, IEnumerable<int> layers) =>
            HiddenBy(species, registry, hairGroup, layers);

        /// Labels of the parts drawn on one of these layers: the gear row's
        /// "hides ..." note. species is what is drawn (StudioCharacter.SpeciesView).
        public static List<string> HiddenBy(SpeciesDisplayData species, DisplayDataRegistry registry, string hairGroup, IEnumerable<int> hiddenLayers)
        {
            var hidden = new HashSet<int>(hiddenLayers ?? Array.Empty<int>());
            var result = new List<string>();
            if (hidden.Count == 0) return result;
            foreach (var (_, label, get) in Fields) Check(label, get(species));
            if (species.OtherBodyParts != null)
                foreach (var token in species.OtherBodyParts) Check("Other part", token);
            Check("Hair", hairGroup);
            return result;

            void Check(string label, string token)
            {
                if (result.Contains(label)) return;
                foreach (int layer in LayersOf(token, registry))
                    if (hidden.Contains(layer)) { result.Add(label); return; }
            }
        }
    }
}
