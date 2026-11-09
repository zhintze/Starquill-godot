using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup of color squares, 8 per row; a click picks and closes.
    public sealed class SwatchPicker
    {
        public SwatchPicker(Transform canvasRoot, string title, IReadOnlyList<Color> colors, Action<Color> onPick)
        {
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(600, 460), out var close);
            HorizontalLayoutGroup row = null;
            for (int i = 0; i < colors.Count; i++)
            {
                if (i % 8 == 0) row = StudioUi.Row(list, "Swatches", 54f, 8f);
                var color = colors[i];
                StudioUi.Swatch(row.transform, color, 54f, () => { onPick(color); close(); });
            }
        }
    }
}
