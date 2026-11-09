using System.Linq;
using Starquill.Data.Art;
using TMPro;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Overlay listing art table problems and images that failed to load.
    public sealed class WarningsView
    {
        public WarningsView(Transform parent, StudioApp app)
        {
            var panel = StudioUi.Panel(parent, "Warnings", StudioUi.PanelColor);
            StudioUi.Stretch(panel, 200, 100, 200, 100);
            var col = StudioUi.Column(panel, "Column", 10f);
            StudioUi.Stretch((RectTransform)col.transform, 20, 20, 20, 20);

            var header = StudioUi.Row(col.transform, "Header", 44f);
            var issues = app.Data.Issues;
            int errors = issues.Count(i => i.level == ArtIssueLevel.Error);
            StudioUi.Label(header.transform, $"Warnings: {app.WarningCount - errors}   Errors: {errors}", 24f, 600f);
            StudioUi.Button(header.transform, "Close", () => Object.Destroy(panel.gameObject), 120f);

            var list = StudioUi.ScrollList(col.transform);
            StudioUi.Size(list.parent.parent.gameObject, -1, 780f).flexibleHeight = 1;
            foreach (var issue in issues)
            {
                var label = StudioUi.Label(list, issue.ToString(), 17f);
                if (issue.level == ArtIssueLevel.Error) label.color = StudioUi.ErrorColor;
                label.textWrappingMode = TextWrappingModes.Normal;
            }
            foreach (var path in app.Renderer.Resolver.MissingPaths.OrderBy(p => p))
                StudioUi.Label(list, $"Warning: {path}.png: image not found while drawing", 17f).textWrappingMode = TextWrappingModes.Normal;
            foreach (var issue in app.ImageWarnings)
                StudioUi.Label(list, issue.ToString(), 17f).textWrappingMode = TextWrappingModes.Normal;
            foreach (var line in app.LoadWarnings)
                StudioUi.Label(list, "Warning: " + line, 17f).textWrappingMode = TextWrappingModes.Normal;
            if (app.WarningCount == 0)
                StudioUi.Label(list, "No problems found.", 18f);
        }
    }
}
