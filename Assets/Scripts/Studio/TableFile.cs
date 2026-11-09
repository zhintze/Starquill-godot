using System.IO;
using Starquill.Data.Art;

namespace Starquill.Studio
{
    /// One art table opened for editing. Remembers the text it read so a
    /// change on disk since then is noticed before overwriting it (P31).
    public sealed class TableFile
    {
        public string Path { get; }
        public CsvTable Table { get; private set; }
        public string LoadedText { get; private set; }

        public TableFile(string path)
        {
            Path = path;
            Reload();
        }

        public void Reload()
        {
            LoadedText = File.Exists(Path) ? File.ReadAllText(Path) : "";
            Table = CsvTable.Parse(LoadedText);
        }

        public bool ChangedOnDisk => (File.Exists(Path) ? File.ReadAllText(Path) : "") != LoadedText;

        /// Row index whose first column is key, or -1.
        public int Find(string key)
        {
            for (int i = 0; i < Table.Rows.Count; i++)
                if (Table.Rows[i][0] == key) return i;
            return -1;
        }

        /// Writes text (IOException when another program holds the file).
        public void Write(string text)
        {
            File.WriteAllText(Path, text);
            LoadedText = text;
            Table = CsvTable.Parse(text);
        }
    }
}
