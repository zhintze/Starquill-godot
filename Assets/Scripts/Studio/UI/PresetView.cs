using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Popup to save the current thing under a name or load a saved one (P28).
    public sealed class PresetView
    {
        public PresetView(Transform canvasRoot, string title, string what, string folder, IReadOnlyList<string> names,
            string suggestedName, Action<string> onSave, Action<string> onLoad)
        {
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(760, 780), out var close);

            var save = StudioUi.Row(list, "Save", 44f);
            StudioUi.Label(save.transform, "Save as", 20f);
            TMP_InputField input = StudioUi.Input(save.transform, suggestedName, _ => { }, 380f);
            StudioUi.Button(save.transform, "Save", () =>
            {
                close();
                onSave(input.text);
            }, 120f);
            StudioUi.Label(list, "Saving under an existing name replaces it.", 16f);

            var folderRow = StudioUi.Row(list, "Folder", 40f);
            StudioUi.Label(folderRow.transform, folder, 15f);
            StudioUi.Button(folderRow.transform, "Open folder", () =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    Application.OpenURL(new Uri(folder).AbsoluteUri);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is UriFormatException) { }
            }, 150f);

            StudioUi.Label(list, names.Count == 0 ? $"No saved {what} yet." : $"Saved {what} (click to load):", 20f).color = StudioUi.Accent;
            foreach (var name in names)
            {
                string n = name;
                var button = StudioUi.Button(list, n, () =>
                {
                    close();
                    onLoad(n);
                }, -1f);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(10, 0, 0, 0);
            }
        }
    }
}
