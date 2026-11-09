using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Code-built uGUI for the studio: flat colors, desktop sizes
    /// (1920x1080 reference). Wraps DefaultControls / TMP_DefaultControls;
    /// the dropdown arrow and check mark are drawn in code (no sprite assets).
    public static class StudioUi
    {
        public static readonly Color Background = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color PanelColor = new Color(0.19f, 0.19f, 0.22f);
        public static readonly Color ControlColor = new Color(0.30f, 0.30f, 0.35f);
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.95f);
        public static readonly Color ChipColor = new Color(0.25f, 0.43f, 0.66f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.94f);
        public static readonly Color ErrorColor = new Color(0.95f, 0.40f, 0.40f);

        private static Sprite arrow, dot;
        private static Texture2D checker;

        private static TMP_DefaultControls.Resources TmpRes() =>
            new TMP_DefaultControls.Resources { dropdown = ArrowSprite(), checkmark = DotSprite() };

        private static DefaultControls.Resources UiRes() =>
            new DefaultControls.Resources { dropdown = ArrowSprite(), checkmark = DotSprite() };

        /// Down-pointing triangle for dropdowns.
        public static Sprite ArrowSprite()
        {
            if (arrow != null) return arrow;
            return arrow = MakeSprite(32, (x, y) =>
            {
                if (y < 9 || y > 22) return false;
                float halfWidth = (y - 9) * 12f / 13f;
                return Mathf.Abs(x + 0.5f - 16f) <= halfWidth;
            });
        }

        /// Filled circle for check marks.
        public static Sprite DotSprite()
        {
            if (dot != null) return dot;
            return dot = MakeSprite(32, (x, y) => (new Vector2(x + 0.5f, y + 0.5f) - new Vector2(16f, 16f)).sqrMagnitude <= 100f);
        }

        private static Sprite MakeSprite(int size, Func<int, int, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(inside(x, y) ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float height, float spacing = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var row = go.GetComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            Size(go, -1, height);
            return row;
        }

        public static VerticalLayoutGroup Column(Transform parent, string name, float spacing = 6f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            return col;
        }

        /// Takes the free width in a Row, pushing later items to the right.
        public static void Spacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        public static LayoutElement Size(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            return le;
        }

        /// One line, never wrapped. width -1: as wide as the text.
        public static TMP_Text Label(Transform parent, string text, float fontSize = 20f, float width = -1f)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes());
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            Size(go, width, fontSize + 12f);
            return tmp;
        }

        public static Button Button(Transform parent, string label, Action onClick, float width = 150f)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes());
            go.name = label;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 20f;
            text.color = TextColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Size(go, width, 40f);
            return button;
        }

        public static TMP_Dropdown Dropdown(Transform parent, IList<string> options, int value, Action<int> onChanged, float width = 160f)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var dd = go.GetComponent<TMP_Dropdown>();
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Count - 1));
            dd.captionText.color = TextColor;
            dd.captionText.fontSize = 20f;
            dd.itemText.fontSize = 20f;
            dd.onValueChanged.AddListener(i => onChanged(i));
            Size(go, width, 40f);
            return dd;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged, float width = 120f)
        {
            var go = DefaultControls.CreateToggle(UiRes());
            go.transform.SetParent(parent, false);
            var toggle = go.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(value);
            ((Image)toggle.targetGraphic).color = ControlColor;
            toggle.graphic.color = Accent;
            var text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = TextColor;
            text.fontSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            Size(go, width, 30f);
            return toggle;
        }

        public static TMP_InputField Input(Transform parent, string value, Action<string> onEndEdit, float width = 700f, string placeholder = null)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpRes());
            go.transform.SetParent(parent, false);
            var input = go.GetComponent<TMP_InputField>();
            input.text = value ?? "";
            input.textComponent.fontSize = 20f;
            if (placeholder != null && input.placeholder is TMP_Text hint)
            {
                hint.text = placeholder;
                hint.fontSize = 18f;
                hint.textWrappingMode = TextWrappingModes.NoWrap;
            }
            input.onEndEdit.AddListener(v => onEndEdit(v));
            Size(go, width, 40f);
            return input;
        }

        /// Row: name, slider, value. The value text follows the slider.
        public static Slider Slider(Transform parent, string label, float min, float max, float value, Action<float> onChanged, string format = "0.00")
        {
            var row = Row(parent, "Slider " + label, 30f, 10f);
            Label(row.transform, label, 18f, 330f);
            var go = DefaultControls.CreateSlider(UiRes());
            go.transform.SetParent(row.transform, false);
            go.transform.Find("Background").GetComponent<Image>().color = ControlColor;
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = TextColor;
            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            Size(go, 300f, 20f);
            var valueText = Label(row.transform, value.ToString(format), 18f, 60f);
            slider.onValueChanged.AddListener(v => { valueText.text = v.ToString(format); onChanged(v); });
            return slider;
        }

        /// Clickable color square.
        public static Image Swatch(Transform parent, Color color, float size, Action onClick)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            Size(go, size, size);
            return image;
        }

        /// Modal window over the whole canvas: a dim backdrop (a click on it
        /// closes), a centered panel with a title and Close, and a scrolling
        /// list. Returns the list; close() removes the window.
        public static RectTransform Popup(Transform canvasRoot, string title, Vector2 size, out Action close)
        {
            var root = new GameObject("Popup: " + title, typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)root.transform);
            // Above everything else, including the search results overlay (sorting order 50).
            var overlay = root.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 100;
            root.AddComponent<GraphicRaycaster>();
            Action doClose = () => { if (root != null) UnityEngine.Object.Destroy(root); };
            close = doClose;

            // The backdrop and the window are siblings, so clicks inside the
            // window never reach the backdrop's Button.
            var backdrop = Panel(root.transform, "Backdrop", new Color(0f, 0f, 0f, 0.55f));
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => doClose());

            var window = Panel(root.transform, "Window", PanelColor);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = size;
            var col = Column(window, "Column", 10f);
            Stretch((RectTransform)col.transform, 20, 20, 20, 20);

            var header = Row(col.transform, "Header", 44f);
            Label(header.transform, title, 26f);
            Spacer(header.transform);
            Button(header.transform, "Close", () => doClose(), 120f);

            var list = ScrollList(col.transform);
            Size(list.parent.parent.gameObject, -1, size.y - 104f).flexibleHeight = 1;
            return list;
        }

        /// Vertical scroll area whose content is a grid of fixed-size cells.
        public static RectTransform ScrollGrid(Transform parent, Vector2 cell, float spacing)
        {
            var go = DefaultControls.CreateScrollView(UiRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(12, 12, 12, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// Vertical scroll list (Warnings, popups).
        public static RectTransform ScrollList(Transform parent)
        {
            var go = DefaultControls.CreateScrollView(UiRes());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Background;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = scroll.content;
            var col = content.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(10, 10, 10, 10);
            col.spacing = 4f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// 2x2 light/dark gray tile; use with RawImage.uvRect to repeat it.
        public static Texture2D Checkerboard()
        {
            if (checker != null) return checker;
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var a = new Color32(200, 200, 200, 255);
            var b = new Color32(150, 150, 150, 255);
            checker.SetPixels32(new[] { a, b, b, a });
            checker.Apply();
            return checker;
        }
    }
}
