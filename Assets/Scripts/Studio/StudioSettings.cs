using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Starquill.Studio
{
    /// Per-install studio choices. In the Editor: Library/CharacterStudio/;
    /// in a build: next to the exe (where presets/ also goes).
    [Serializable]
    public class StudioSettings
    {
        public static readonly int[] Scales = { 1, 2, 4, 8 };
        public static readonly int[] Counts = { 8, 16, 32, 64 };

        public string repoRoot = "";
        public string exportFolder = "";
        public int exportScale = 2;
        public bool trim = true;
        public int padding = 8;
        public int count = 8;
        /// Species left out of random rolls (unchecked in the Species window).
        /// Stored as exclusions so species added later start checked (P13).
        public List<string> excludedSpecies = new List<string>();

        public static string AppFolder => Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "CharacterStudio")
            : Directory.GetParent(Application.dataPath).FullName;

        public static string DefaultPath => Path.Combine(AppFolder, "studio-settings.json");

        public static string DefaultExportFolder => Path.Combine(AppFolder, "exports");

        public string ResolvedExportFolder =>
            string.IsNullOrWhiteSpace(exportFolder) ? DefaultExportFolder : exportFolder;

        public static int SnapScale(int scale)
        {
            int result = 1;
            foreach (int s in Scales) if (scale >= s) result = s;
            return result;
        }

        public static StudioSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var s = JsonUtility.FromJson<StudioSettings>(File.ReadAllText(path)) ?? new StudioSettings();
                    s.excludedSpecies ??= new List<string>();
                    return s;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Studio settings unreadable, using defaults: {e.Message}");
            }
            return new StudioSettings();
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}
