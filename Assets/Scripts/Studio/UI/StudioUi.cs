using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Code-built uGUI for the studio: flat colors, desktop sizes
    /// (1920x1080 reference). Wraps DefaultControls / TMP_DefaultControls.
    public static class StudioUi
    {
        public static readonly Color Background = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color PanelColor = new Color(0.19f, 0.19f, 0.22f);
        public static readonly Color ControlColor = new Color(0.30f, 0.30f, 0.35f);
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.95f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.94f);
        public static readonly Color ErrorColor = new Color(0.95f, 0.40f, 0.40f);

        private static readonly TMP_DefaultControls.Resources TmpRes = new TMP_DefaultControls.Resources();
        private static readonly DefaultControls.Resources UiRes = new DefaultControls.Resources();

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

        public static LayoutElement Size(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            return le;
        }

        public static TMP_Text Label(Transform parent, string text, float fontSize = 20f, float width = -1f)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes);
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            Size(go, width, fontSize + 12f);
            return tmp;
        }

        public static Button Button(Transform parent, string label, Action onClick, float width = 150f)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ControlColor;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 20f;
            text.color = TextColor;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Size(go, width, 40f);
            return button;
        }

        public static TMP_Dropdown Dropdown(Transform parent, IList<string> options, int value, Action<int> onChanged, float width = 160f)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpRes);
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
            var go = DefaultControls.CreateToggle(UiRes);
            go.transform.SetParent(parent, false);
            var toggle = go.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(value);
            ((Image)toggle.targetGraphic).color = ControlColor;
            toggle.graphic.color = Accent;
            var text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = TextColor;
            text.fontSize = 18;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            Size(go, width, 30f);
            return toggle;
        }

        public static TMP_InputField Input(Transform parent, string value, Action<string> onEndEdit, float width = 700f)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpRes);
            go.transform.SetParent(parent, false);
            var input = go.GetComponent<TMP_InputField>();
            input.text = value ?? "";
            input.textComponent.fontSize = 20f;
            input.onEndEdit.AddListener(v => onEndEdit(v));
            Size(go, width, 40f);
            return input;
        }

        /// Vertical scroll area whose content is a grid of fixed-size cells.
        public static RectTransform ScrollGrid(Transform parent, Vector2 cell, float spacing)
        {
            var go = DefaultControls.CreateScrollView(UiRes);
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

        /// Vertical scroll list (Warnings).
        public static RectTransform ScrollList(Transform parent)
        {
            var go = DefaultControls.CreateScrollView(UiRes);
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

        private static Texture2D checker;

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
