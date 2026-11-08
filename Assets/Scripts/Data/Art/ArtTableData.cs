using System;
using System.Collections.Generic;

namespace Starquill.Data.Art
{
    /// Everything the game needs from the art tables, after the bake.
    /// Variant numbers are the ones that exist on disk (gaps allowed);
    /// nothing here is a hand-typed count. Unity serializes these into
    /// Assets/Resources/ArtTables.asset.
    [Serializable]
    public class ArtTableData
    {
        public List<EquipmentRow> equipment = new List<EquipmentRow>();
        public List<WeaponRow> weapons = new List<WeaponRow>();
        public List<SpeciesRow> species = new List<SpeciesRow>();
        public List<PartGroupRow> partGroups = new List<PartGroupRow>();
        public List<SkinVarianceRow> skinVariance = new List<SkinVarianceRow>();
        public List<PaletteRow> palettes = new List<PaletteRow>();
    }

    [Serializable]
    public class EquipmentRow
    {
        public string itemType;
        public string description;
        public int[] layerCodes = Array.Empty<int>();
        public int[] hiddenLayers = Array.Empty<int>();
        public int[] colorVarianceLayers = Array.Empty<int>();
        public int[] variants = Array.Empty<int>();
    }

    /// Unity cannot serialize int[][]; one of these per weapon layer.
    [Serializable]
    public class IntList
    {
        public int[] values = Array.Empty<int>();
        public IntList() { }
        public IntList(int[] values) { this.values = values; }
    }

    [Serializable]
    public class WeaponRow
    {
        public string itemType;
        public string description;
        public string handType;
        public bool modular;
        public int[] layerCodes = Array.Empty<int>();
        public int[] hiddenLayers = Array.Empty<int>();
        public int[] colorVarianceLayers = Array.Empty<int>();
        /// Parallel to layerCodes. Modular weapons pick each layer on its own;
        /// non-modular ones share one number, so every entry is the same list.
        public IntList[] variantsPerLayer = Array.Empty<IntList>();
    }

    [Serializable]
    public class SpeciesRow
    {
        public string name;
        public float xScale = 1f, yScale = 1f, headYOffset = 0.35f, headZoom = 0.5f;
        public string backArm, legs, body, head, frontArm;
        public string eyes, nose, mouth, ears, facialHair, facialDetail;
        public string[] hairGroups = Array.Empty<string>();
        public string[] otherBodyParts = Array.Empty<string>();
        public string[] itemRestrictions = Array.Empty<string>();
        public string skinPalette, hairPalette, eyePalette, facialDetailPalette; // blank = main
    }

    [Serializable]
    public class PartGroupRow
    {
        public string group;
        public string description;
        public int[] layers = Array.Empty<int>();
        public int[] variants = Array.Empty<int>();
    }

    [Serializable]
    public class SkinVarianceRow
    {
        public string species;
        public int[] layers = Array.Empty<int>();
        public string palette;
    }

    [Serializable]
    public class PaletteRow
    {
        public string name;
        public string[] hex = Array.Empty<string>(); // "RRGGBB", upper case, no '#'
    }

    public enum ArtIssueLevel { Warning, Error }

    [Serializable]
    public class ArtIssue
    {
        public ArtIssueLevel level;
        public string where;   // "equipment.csv line 12" or an image path
        public string message;

        public ArtIssue(ArtIssueLevel level, string where, string message)
        {
            this.level = level; this.where = where; this.message = message;
        }

        public override string ToString() => $"{level}: {where}: {message}";
    }

    /// Raw inputs: the six CSV texts (null = file not found) and every
    /// file path under Assets/Resources/Images, relative and '/'-separated.
    public sealed class ArtSources
    {
        public string EquipmentCsv, WeaponsCsv, SpeciesCsv, SpeciesPartsCsv, SkinVarianceCsv, PalettesCsv;
        public List<string> ImageFiles = new List<string>();
    }

    public sealed class ArtBakeResult
    {
        public ArtTableData Data = new ArtTableData();
        public List<ArtIssue> Issues = new List<ArtIssue>();
        public bool HasErrors => Issues.Exists(i => i.level == ArtIssueLevel.Error);
    }
}
