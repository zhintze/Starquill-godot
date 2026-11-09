using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Form for one art table row (P29): one field per column with plain
    /// words. New rows get a suggested code and can start from a copy.
    /// Stays open with the edit when saving fails (P30, P31).
    public sealed class RowFormView
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "item_type", "Code" }, { "group", "Code" }, { "name", "Name" }, { "description", "Description" },
            { "layer_codes", "Layer codes (numbers, spaces between)" }, { "layers", "Layers (numbers, spaces between)" },
            { "hidden_layers", "Hides body layers (numbers)" }, { "color_variance_layers", "Second-color layers (numbers)" },
            { "hand_type", "Hands: one_handed or two_handed" }, { "modular", "Separate parts per layer: y or n" },
            { "x_scale", "Width scale (not drawn yet)" }, { "y_scale", "Height scale (not drawn yet)" },
            { "head_y_offset", "Head y offset (portraits)" }, { "head_zoom", "Head zoom (portraits)" },
            { "hair_groups", "Hair groups (codes, spaces between)" }, { "other_body_parts", "Other parts (codes)" },
            { "item_restrictions", "Cannot wear (item codes)" }
        };

        private static readonly (string label, string prefix)[] Slots =
            { ("Head", "hd"), ("Torso", "tr"), ("Arms", "ar"), ("Legs", "lg"), ("Feet", "fe"), ("Misc", "mc") };

        private readonly Transform canvas;
        private readonly LibraryEditor editor;
        private readonly TableKind kind;
        private readonly string originalKey;
        private readonly Action<LibraryResult> onDone;
        private readonly List<TMP_InputField> inputs = new List<TMP_InputField>();
        private readonly TMP_Text message;
        private Action close;

        public static string KindName(TableKind kind) =>
            kind == TableKind.ItemTypes ? "item type" : kind == TableKind.Weapons ? "weapon type"
            : kind == TableKind.Species ? "species" : "part group";

        /// row null: a new row. onDone gets every successful save or remove.
        public RowFormView(Transform canvas, LibraryEditor editor, TableKind kind, string[] row, Action<LibraryResult> onDone)
        {
            this.canvas = canvas;
            this.editor = editor;
            this.kind = kind;
            this.onDone = onDone;
            var table = editor.Open(kind).Table;
            originalKey = row?[0];
            string title = originalKey == null ? $"New {KindName(kind)}" : $"Edit {originalKey} ({LibraryEditor.FileName(kind)})";
            var list = StudioUi.Popup(canvas, title, new Vector2(1000, 900), out close);

            if (originalKey == null) BuildNewControls(list, table);

            for (int c = 0; c < table.Header.Length; c++)
            {
                string column = table.Header[c];
                var line = StudioUi.Row(list, "Field " + column, 42f);
                StudioUi.Label(line.transform, Labels.TryGetValue(column, out var l) ? l : column.Replace('_', ' '), 18f, 380f);
                var input = StudioUi.Input(line.transform, row != null && c < row.Length ? row[c] : "", _ => { }, 520f);
                if (c == 0 && originalKey != null) input.interactable = false; // codes are not renamed (P29)
                inputs.Add(input);
            }
            if (originalKey == null && kind != TableKind.Species) inputs[0].text = editor.NextCode(kind, DefaultPrefix(table));

            message = StudioUi.Label(list, "", 18f);
            message.color = StudioUi.ErrorColor;
            message.textWrappingMode = TextWrappingModes.Normal;

            var buttons = StudioUi.Row(list, "Buttons", 46f);
            StudioUi.Button(buttons.transform, "Save", () => Save(false), 140f);
            if (originalKey != null)
            {
                StudioUi.Button(buttons.transform, "Remove", AskRemove, 140f);
                string folder = editor.FolderPath(kind, originalKey);
                if (folder != null) StudioUi.Button(buttons.transform, "Open folder", () => OpenFolder(folder), 160f);
            }
        }

        private string DefaultPrefix(Starquill.Data.Art.CsvTable table)
        {
            if (kind == TableKind.ItemTypes) return "hd";
            if (kind == TableKind.Weapons) return "w";
            return table.Rows.Select(r => r[0]).FirstOrDefault(k => k.Length > 0)?.Substring(0, 1) ?? "f";
        }

        private void BuildNewControls(Transform list, Starquill.Data.Art.CsvTable table)
        {
            if (kind == TableKind.ItemTypes)
            {
                var slotRow = StudioUi.Row(list, "Slot", 42f);
                StudioUi.Label(slotRow.transform, "Slot", 18f, 380f);
                StudioUi.Dropdown(slotRow.transform, Slots.Select(s => $"{s.label} ({s.prefix})").ToList(), 0,
                    i => inputs[0].text = editor.NextCode(kind, Slots[i].prefix), 240f);
            }
            else if (kind == TableKind.PartGroups)
            {
                var letters = table.Rows.Select(r => r[0]).Where(k => k.Length > 0).Select(k => k.Substring(0, 1)).Distinct().OrderBy(x => x).ToList();
                if (letters.Count > 0)
                {
                    var letterRow = StudioUi.Row(list, "Letter", 42f);
                    StudioUi.Label(letterRow.transform, "Code letter", 18f, 380f);
                    StudioUi.Dropdown(letterRow.transform, letters, 0, i => inputs[0].text = editor.NextCode(kind, letters[i]), 120f);
                }
            }

            var keys = table.Rows.Select(r => r[0]).ToList();
            var startRow = StudioUi.Row(list, "StartFrom", 42f);
            StudioUi.Label(startRow.transform, "Start from a copy of", 18f, 380f);
            var options = new List<string> { "(empty)" };
            options.AddRange(keys);
            StudioUi.Dropdown(startRow.transform, options, 0, i =>
            {
                var source = i == 0 ? null : table.Rows[i - 1];
                for (int c = 1; c < inputs.Count; c++) inputs[c].text = source != null && c < source.Length ? source[c] : "";
            }, 300f);
        }

        private string[] Values() => inputs.Select(i => i.text).ToArray();

        private void Save(bool overwrite)
        {
            var result = editor.Save(kind, originalKey, Values(), overwrite);
            if (result.ChangedOnDisk)
            {
                AskChangedOnDisk(() => Save(true));
                return;
            }
            Finish(result);
        }

        private void AskRemove()
        {
            string file = LibraryEditor.FileName(kind);
            string folder = LibraryEditor.FolderOf(kind, originalKey);
            int images = editor.ImageCount(kind, originalKey);
            string what = folder != null ? $" and the folder {folder} with its {images} {(images == 1 ? "image" : "images")}" : "";
            StudioUi.Confirm(canvas, $"Remove {originalKey}?",
                $"Deletes the {originalKey} row from {file}{what}. Git can bring them back (Discard changes in GitHub Desktop).",
                "Remove", () => Remove(false));
        }

        private void Remove(bool overwrite)
        {
            var result = editor.Remove(kind, originalKey, overwrite);
            if (result.ChangedOnDisk)
            {
                AskChangedOnDisk(() => Remove(true));
                return;
            }
            Finish(result);
        }

        private void AskChangedOnDisk(Action overwrite)
        {
            string file = LibraryEditor.FileName(kind);
            StudioUi.Confirm(canvas, $"{file} changed",
                $"{file} changed on disk since the studio read it (another program, or a git pull). Overwrite it with your edit, or reload it and drop your edit?",
                "Overwrite with my edit", overwrite, "Reload (drop my edit)", () =>
                {
                    editor.Open(kind).Reload();
                    close();
                    onDone(new LibraryResult { Ok = true, Message = $"Reloaded {file}; your edit was dropped." });
                });
        }

        private void Finish(LibraryResult result)
        {
            if (!result.Ok)
            {
                message.text = result.Message + (result.Errors.Count > 0 ? "\n" + string.Join("\n", result.Errors.Take(6)) : "");
                return;
            }
            close();
            onDone(result);
        }

        private static void OpenFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is UriFormatException) { }
        }
    }
}
