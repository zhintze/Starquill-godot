using System;
using System.Collections;
using System.IO;
using System.Linq;
using Starquill.Data.Art;
using Starquill.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Library panel (design §2): the four art tables as lists with New,
    /// Edit (and Remove inside the form), Rescan and Check images. Opens over
    /// the Bulk screen; onClosed(true) when anything changed (P34).
    public sealed class LibraryScreen
    {
        private static readonly (TableKind kind, string label)[] Tabs =
        {
            (TableKind.ItemTypes, "Item types"), (TableKind.Weapons, "Weapons"),
            (TableKind.Species, "Species"), (TableKind.PartGroups, "Part groups"), (TableKind.ExtraParts, "Extra parts")
        };

        private readonly StudioApp app;
        private readonly Transform canvas;
        private readonly LibraryEditor editor;
        private readonly Action<bool> onClosed;
        private readonly GameObject root;
        private readonly RectTransform list;
        private readonly TMP_Text status;
        private readonly Button[] tabButtons = new Button[Tabs.Length];
        private TableKind current = TableKind.ItemTypes;
        private bool changed;

        public LibraryScreen(Transform canvas, StudioApp app, Action<bool> onClosed)
        {
            this.app = app;
            this.canvas = canvas;
            this.onClosed = onClosed;
            editor = new LibraryEditor(app.Data.RepoRoot);

            var rootRt = StudioUi.Panel(canvas, "Library", StudioUi.Background);
            StudioUi.Stretch(rootRt);
            root = rootRt.gameObject;

            var top = StudioUi.Row(root.transform, "TopBar", 48f);
            Dock((RectTransform)top.transform, -8f, 48f);
            StudioUi.Button(top.transform, "Back", Close, 110f);
            StudioUi.Button(top.transform, "Rescan", Rescan, 130f);
            StudioUi.Button(top.transform, "Check images", () => app.StartCoroutine(CheckImages()), 180f);
            status = StudioUi.Label(top.transform, "Tables: " + Path.Combine(app.Data.RepoRoot, ArtRepo.TablesDir), 17f);

            var tabs = StudioUi.Row(root.transform, "Tabs", 44f);
            Dock((RectTransform)tabs.transform, -64f, 44f);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var kind = Tabs[i].kind;
                tabButtons[i] = StudioUi.Button(tabs.transform, Tabs[i].label, () => Show(kind), 180f);
            }
            StudioUi.Spacer(tabs.transform);
            StudioUi.Button(tabs.transform, "New", () => new RowFormView(canvas, editor, current, null, Saved), 120f);

            list = StudioUi.ScrollList(root.transform);
            StudioUi.Stretch((RectTransform)list.parent.parent, 12f, 120f, 12f, 12f);
            Show(TableKind.ItemTypes);
        }

        private static void Dock(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(-24, height);
            rt.anchoredPosition = new Vector2(0, y);
        }

        private void Show(TableKind kind)
        {
            current = kind;
            for (int i = 0; i < Tabs.Length; i++)
                tabButtons[i].GetComponent<Image>().color = Tabs[i].kind == kind ? StudioUi.ChipColor : StudioUi.ControlColor;

            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            var table = editor.Open(kind).Table;
            StudioUi.Label(list, $"{LibraryEditor.FileName(kind)}: {table.Rows.Count} rows. Edit opens a form; Remove is inside it.", 17f);
            foreach (var row in table.Rows)
            {
                var r = row;
                var line = StudioUi.Row(list, "Row " + r[0], 40f);
                StudioUi.Label(line.transform, r[0], 19f, 160f);
                string detail = kind == TableKind.ExtraParts && r.Length > 4
                    ? $"{r[1]}    color {r[2]}" + (r[3].Length > 0 ? $"    shares with {r[3]}" : "") + (r[4].Length > 0 ? $"    chance {r[4]}" : "")
                    : r.Length > 1 ? r[1] : "";
                StudioUi.Label(line.transform, detail, 17f, 760f);
                StudioUi.Button(line.transform, "Edit", () => new RowFormView(canvas, editor, current, (string[])r.Clone(), Saved), 90f);
                string folder = editor.FolderPath(kind, r[0]);
                if (folder != null)
                    StudioUi.Button(line.transform, $"Open folder ({editor.ImageCount(kind, r[0])})", () => OpenFolder(folder), 220f);
            }
        }

        private void Saved(LibraryResult result)
        {
            changed = true;
            status.color = StudioUi.TextColor;
            status.text = result.Message;
            Show(current);
        }

        /// P33: .meta for new art, then reread every table.
        private void Rescan()
        {
            int metas = MetaWriter.WriteMissing(editor.ImagesRoot);
            editor.ReloadAll();
            app.Data.Reload();
            app.Renderer.Resolver.ClearCache();
            changed = true;
            int errors = app.Data.Issues.Count(i => i.level == ArtIssueLevel.Error);
            status.color = errors > 0 ? StudioUi.ErrorColor : StudioUi.TextColor;
            status.text = $"Rescanned: wrote {metas} .meta files; {errors} errors, {app.Data.Issues.Count - errors} warnings (Warnings on the Bulk screen).";
            Show(current);
        }

        private IEnumerator CheckImages()
        {
            status.color = StudioUi.TextColor;
            status.text = "Checking every image for empty and duplicate drawings (a few seconds)...";
            yield return null;
            yield return null;
            var files = ArtRepo.ReadSources(app.Data.RepoRoot).ImageFiles;
            app.ImageWarnings.Clear();
            app.ImageWarnings.AddRange(ArtImageAudit.Run(editor.ImagesRoot, files));
            status.text = $"Checked {files.Count} images: {app.ImageWarnings.Count} problems (Warnings on the Bulk screen).";
        }

        private void Close()
        {
            UnityEngine.Object.Destroy(root);
            onClosed(changed);
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
