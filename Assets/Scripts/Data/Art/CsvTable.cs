using System;
using System.Collections.Generic;
using System.Text;

namespace Starquill.Data.Art
{
    /// Comma-separated table: fields holding a comma, quote or newline are
    /// double-quoted with quotes doubled. Reads CRLF or LF and a UTF-8 BOM
    /// (Excel on Windows writes both). Writes LF and quotes only when needed,
    /// so editing one row changes one line in git.
    /// No UnityEngine here: tools/ArtTablesTests compiles this file headless.
    public sealed class CsvTable
    {
        private static readonly char[] NeedsQuotes = { ',', '"', '\n', '\r' };

        public string[] Header { get; }
        public List<string[]> Rows { get; } = new List<string[]>();
        /// 1-based line where each row starts, for messages ("line 12").
        public List<int> RowLines { get; } = new List<int>();

        public CsvTable(string[] header) { Header = header; }

        public int Column(string name) => Array.IndexOf(Header, name);

        public static CsvTable Parse(string text)
        {
            var lines = new List<int>();
            var records = ParseRecords(text ?? "", lines);
            if (records.Count == 0) return new CsvTable(Array.Empty<string>());

            var header = records[0];
            for (int i = 0; i < header.Length; i++) header[i] = header[i].Trim();
            var table = new CsvTable(header);

            for (int r = 1; r < records.Count; r++)
            {
                var rec = records[r];
                if (rec.Length == 1 && rec[0].Trim().Length == 0) continue; // blank line
                var row = new string[header.Length];
                for (int c = 0; c < header.Length; c++) row[c] = c < rec.Length ? rec[c] : "";
                table.Rows.Add(row);
                table.RowLines.Add(lines[r]);
            }
            return table;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            AppendRecord(sb, Header);
            foreach (var row in Rows) AppendRecord(sb, row);
            return sb.ToString();
        }

        private static void AppendRecord(StringBuilder sb, string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string f = fields[i] ?? "";
                if (f.IndexOfAny(NeedsQuotes) >= 0)
                    sb.Append('"').Append(f.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(f);
            }
            sb.Append('\n');
        }

        private static List<string[]> ParseRecords(string text, List<int> recordLines)
        {
            var records = new List<string[]>();
            var fields = new List<string>();
            var sb = new StringBuilder();
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            int line = 1, recordLine = 1;
            bool inQuotes = false, any = false;

            while (i < text.Length)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        inQuotes = false; i++; continue;
                    }
                    if (c == '\n') line++;
                    if (c != '\r') sb.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; any = true; break;
                    case ',': fields.Add(sb.ToString()); sb.Clear(); any = true; break;
                    case '\r': break;
                    case '\n':
                        fields.Add(sb.ToString()); sb.Clear();
                        records.Add(fields.ToArray()); recordLines.Add(recordLine);
                        fields.Clear(); any = false;
                        line++; recordLine = line;
                        break;
                    default: sb.Append(c); any = true; break;
                }
                i++;
            }

            if (any || sb.Length > 0 || fields.Count > 0)
            {
                fields.Add(sb.ToString());
                records.Add(fields.ToArray());
                recordLines.Add(recordLine);
            }
            return records;
        }
    }
}
