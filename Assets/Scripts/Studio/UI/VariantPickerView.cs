using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// One choice in a VariantPickerView: a label and a function that draws
    /// its thumbnail (called once; the picker destroys the texture on close).
    public sealed class VariantOption
    {
        public string Label;
        public Func<Texture2D> Thumbnail;
    }

    /// Popup grid of thumbnails, 6 per row. onPick gets the option index,
    /// or -1 for None when allowNone.
    public sealed class VariantPickerView
    {
        private const float Cell = 112f;

        public VariantPickerView(Transform canvasRoot, string title, IReadOnlyList<VariantOption> options, int selected,
            bool allowNone, Action<int> onPick)
        {
            var textures = new List<Texture2D>();
            var list = StudioUi.Popup(canvasRoot, title, new Vector2(820, 860), out var close, () =>
            {
                foreach (var t in textures) if (t != null) UnityEngine.Object.Destroy(t);
            });

            if (allowNone)
                StudioUi.Button(StudioUi.Row(list, "NoneRow", 44f).transform, "None", () => { close(); onPick(-1); }, 160f)
                    .GetComponent<Image>().color = selected < 0 ? StudioUi.ChipColor : StudioUi.ControlColor;

            HorizontalLayoutGroup row = null;
            for (int i = 0; i < options.Count; i++)
            {
                if (i % 6 == 0) row = StudioUi.Row(list, "Thumbs", Cell + 28f, 8f);
                int index = i;
                var tex = options[i].Thumbnail?.Invoke();
                if (tex != null) textures.Add(tex);
                Cell_(row.transform, tex, options[i].Label, i == selected, () => { close(); onPick(index); });
            }
        }

        private static void Cell_(Transform parent, Texture2D tex, string label, bool selected, Action onClick)
        {
            var cell = StudioUi.Panel(parent, "Variant " + label, selected ? StudioUi.ChipColor : StudioUi.ControlColor);
            cell.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            StudioUi.Size(cell.gameObject, Cell, Cell + 28f);

            var picture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(cell, false);
            var rt = (RectTransform)picture.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(Cell - 12f, Cell - 12f);
            rt.anchoredPosition = new Vector2(0, -6);
            var raw = picture.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = tex != null ? Color.white : Color.clear;
            raw.raycastTarget = false;

            var text = StudioUi.Label(cell, label, 17f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var trt = (RectTransform)text.transform;
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(0, 26);
            trt.anchoredPosition = new Vector2(0, 2);
        }
    }
}
