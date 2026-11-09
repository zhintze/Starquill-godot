using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Starquill.Studio
{
    /// Preset files (P25): <root>/<kind>/<name>.json, pretty-printed JsonUtility.
    public sealed class PresetStore
    {
        public const string Characters = "characters";
        public const string Casts = "casts";
        public const string Random = "random";

        public string Root { get; }

        public PresetStore(string root) { Root = root; }

        /// presets/ next to the exe; Library/CharacterStudio/presets in the Editor.
        public static PresetStore Default => new PresetStore(Path.Combine(StudioSettings.AppFolder, "presets"));

        /// Letters, digits, spaces, - and _ stay; anything else becomes -.
        public static string CleanName(string name)
        {
            string n = Regex.Replace((name ?? "").Trim(), @"[^A-Za-z0-9 _-]", "-").Trim(' ', '-');
            return n.Length == 0 ? "preset" : n;
        }

        public string PathFor(string kind, string name) => Path.Combine(Root, kind, CleanName(name) + ".json");

        public List<string> List(string kind)
        {
            string dir = Path.Combine(Root, kind);
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// Replaces a preset of the same name (P28). Returns the file path.
        public string Save<T>(string kind, string name, T preset)
        {
            string path = PathFor(kind, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(preset, true));
            return path;
        }

        /// Null with a reason when the file is missing or not valid JSON.
        public T Load<T>(string kind, string name, out string error) where T : class
        {
            error = null;
            string path = PathFor(kind, name);
            if (!File.Exists(path))
            {
                error = $"No preset named {name}.";
                return null;
            }
            try
            {
                var result = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (result == null) error = $"{Path.GetFileName(path)} is empty.";
                return result;
            }
            catch (ArgumentException e)
            {
                error = $"{Path.GetFileName(path)} is not valid JSON: {e.Message}";
                return null;
            }
        }
    }
}
