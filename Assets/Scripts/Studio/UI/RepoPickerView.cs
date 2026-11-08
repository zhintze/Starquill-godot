using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// First launch: where is the Starquill clone? (P2: typed or pasted path.)
    public sealed class RepoPickerView
    {
        public RepoPickerView(Transform parent, string current, Action<string> onChosen)
        {
            var panel = StudioUi.Panel(parent, "RepoPicker", StudioUi.PanelColor);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900, 300);
            var col = StudioUi.Column(panel, "Column", 14f);
            StudioUi.Stretch((RectTransform)col.transform, 30, 30, 30, 30);

            StudioUi.Label(col.transform, "Where is your Starquill folder?", 28f);
            StudioUi.Label(col.transform, "Paste the folder GitHub Desktop cloned (it holds Assets/Data/ArtTables).", 18f);
            TMP_Text error = null;
            var input = StudioUi.Input(col.transform, current, _ => { });
            var row = StudioUi.Row(col.transform, "Buttons", 44f);
            StudioUi.Button(row.transform, "Use this folder", () =>
            {
                string path = input.text.Trim().Trim('"');
                if (StudioData.LooksLikeRepo(path)) onChosen(Path.GetFullPath(path));
                else error.text = "That folder has no Assets/Data/ArtTables and Assets/Resources/Images. Pick the top Starquill folder.";
            }, 220f);
            error = StudioUi.Label(col.transform, "", 18f);
            error.color = StudioUi.ErrorColor;
        }
    }
}
