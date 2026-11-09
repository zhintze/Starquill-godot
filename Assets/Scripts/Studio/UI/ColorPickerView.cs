using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup color picker (P22): palette swatches, H/S/V sliders and a hex
    /// field. Every change calls onChanged at once (live preview).
    public sealed class ColorPickerView
    {
        private readonly Action<Color> onChanged;
        private readonly Image preview;
        private readonly TMP_InputField hex;
        private readonly Slider h, s, v;
        private bool syncing;

        public ColorPickerView(Transform canvasRoot, string title, Color current, IReadOnlyList<Color> palette, Action<Color> onChanged,
            Action onClosed = null)
        {
            this.onChanged = onChanged;
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(660, 820), out var close, onClosed);

            var top = StudioUi.Row(list, "Current", 56f, 10f);
            preview = StudioUi.Swatch(top.transform, current, 52f, () => { });
            StudioUi.Label(top.transform, "Hex", 20f);
            hex = StudioUi.Input(top.transform, ColorUtility.ToHtmlStringRGB(current), SetHex, 160f);
            StudioUi.Button(top.transform, "Done", () => close(), 120f);

            Color.RGBToHSV(current, out float hh, out float ss, out float vv);
            h = StudioUi.Slider(list, "Hue", 0f, 1f, hh, _ => FromSliders(), "0.00");
            s = StudioUi.Slider(list, "Saturation", 0f, 1f, ss, _ => FromSliders(), "0.00");
            v = StudioUi.Slider(list, "Brightness", 0f, 1f, vv, _ => FromSliders(), "0.00");

            if (palette == null || palette.Count == 0) return;
            StudioUi.Label(list, "Game colors", 20f).color = StudioUi.Accent;
            HorizontalLayoutGroup row = null;
            for (int i = 0; i < palette.Count; i++)
            {
                if (i % 12 == 0) row = StudioUi.Row(list, "Swatches", 40f, 6f);
                var c = palette[i];
                StudioUi.Swatch(row.transform, c, 40f, () => Apply(c, true, true));
            }
        }

        private void FromSliders()
        {
            if (syncing) return;
            Apply(Color.HSVToRGB(h.value, s.value, v.value), false, true);
        }

        private void SetHex(string text)
        {
            if (syncing) return;
            string t = (text ?? "").Trim().TrimStart('#');
            if (ColorUtility.TryParseHtmlString("#" + t, out var c)) Apply(c, true, false);
            else hex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(preview.color));
        }

        private void Apply(Color c, bool updateSliders, bool updateHex)
        {
            c.a = 1f;
            syncing = true;
            preview.color = c;
            if (updateHex) hex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(c));
            if (updateSliders)
            {
                Color.RGBToHSV(c, out float hh, out float ss, out float vv);
                h.value = hh;
                s.value = ss;
                v.value = vv;
            }
            syncing = false;
            onChanged(c);
        }
    }
}
