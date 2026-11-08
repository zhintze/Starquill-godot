using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Data.Art;
using Starquill.Display;
using Starquill.Equipment;

namespace Starquill.Studio
{
    /// The art tables of one repo clone, converted in memory (no baked asset),
    /// loaded into the game's own registry and catalog.
    public sealed class StudioData
    {
        public string RepoRoot { get; }
        public DisplayDataRegistry Registry { get; } = new DisplayDataRegistry();
        public EquipmentCatalog Catalog { get; } = new EquipmentCatalog();
        public List<ArtIssue> Issues { get; } = new List<ArtIssue>();
        public List<string> SpeciesNames { get; } = new List<string>();

        private StudioData(string repoRoot) { RepoRoot = repoRoot; }

        public static bool LooksLikeRepo(string root) =>
            !string.IsNullOrWhiteSpace(root)
            && Directory.Exists(Path.Combine(root, ArtRepo.TablesDir))
            && Directory.Exists(Path.Combine(root, ArtRepo.ImagesDir));

        public static StudioData Load(string repoRoot)
        {
            var d = new StudioData(repoRoot);
            d.Reload();
            return d;
        }

        /// Rereads CSVs and image folders (the Library panel's Rescan, B5).
        public void Reload()
        {
            var result = ArtTablesConverter.Convert(ArtRepo.ReadSources(RepoRoot));
            Issues.Clear();
            Issues.AddRange(result.Issues);
            Registry.Load(result.Data);
            Catalog.Load(result.Data);
            SpeciesNames.Clear();
            SpeciesNames.AddRange(Registry.Species.Keys.OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
