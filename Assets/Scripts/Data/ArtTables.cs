using Starquill.Data.Art;
using UnityEngine;

namespace Starquill.Data
{
    /// Baked from Assets/Data/ArtTables/*.csv + the image folders by
    /// ArtTablesBaker (Editor). Generated and gitignored: never edit it.
    public class ArtTables : ScriptableObject
    {
        public const string ResourcePath = "ArtTables";

        public ArtTableData data = new ArtTableData();

        public static ArtTableData LoadFromResources()
        {
            var asset = Resources.Load<ArtTables>(ResourcePath);
            if (asset != null) return asset.data;
            Debug.LogError("ArtTables asset missing. In the Editor run Tools > Art Tables > Bake Now.");
            return new ArtTableData();
        }
    }
}
