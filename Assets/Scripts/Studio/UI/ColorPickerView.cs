using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup color picker (P22, palette only since 2026-11-08): the swatches
    /// of the palette the game itself rolls from (species skin palette, the
    /// species hair/eye/detail palette or main, main for gear). No free
    /// colors. A click applies at once (live preview); Done closes.
    public sealed class ColorPickerView
    {
        public ColorPickerView(Transform canvasRoot, string title, Color current, IReadOnlyList<Color> palette, Action<Color> onChanged,
            Action onClosed = null)
        {
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(700, 860), out var close, onClosed);

            var top = StudioUi.Row(list, "Current", 56f, 10f);
            var preview = StudioUi.Swatch(top.transform, current, 52f, () => { });
            var hex = StudioUi.Label(top.transform, "#" + ColorUtility.ToHtmlStringRGB(current), 20f, 140f);
            StudioUi.Label(top.transform, $"{palette?.Count ?? 0} colors", 18f, 140f);
            StudioUi.Button(top.transform, "Done", () => close(), 120f);

            if (palette == null || palette.Count == 0)
            {
                StudioUi.Label(list, "This palette is empty in palettes.csv.", 18f);
                return;
            }
            HorizontalLayoutGroup row = null;
            for (int i = 0; i < palette.Count; i++)
            {
                if (i % 12 == 0) row = StudioUi.Row(list, "Swatches", 44f, 6f);
                var c = palette[i];
                c.a = 1f;
                StudioUi.Swatch(row.transform, c, 44f, () =>
                {
                    preview.color = c;
                    hex.text = "#" + ColorUtility.ToHtmlStringRGB(c);
                    onChanged(c);
                });
            }
        }
    }
}
