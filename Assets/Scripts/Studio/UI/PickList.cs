using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// The studio's dropdown (Zach, 2026-11-08: uGUI's list showed ~5 rows and
    /// barely scrolled). A button with the current choice and an arrow; a
    /// click opens a tall list under it (as much as fits on screen) that
    /// scrolls fast, with a search field when there are more than 8 choices.
    /// Typing filters, Enter picks the first match, Escape or a click outside closes.
    public sealed class PickList : MonoBehaviour
    {
        private const float RowHeight = 36f;
        private const float SearchHeight = 44f;
        private const int SearchFrom = 9;

        private readonly List<string> options = new List<string>();
        private TMP_Text caption;
        private Action<int> onChanged;

        public int Value { get; private set; }
        public IReadOnlyList<string> Options => options;
        public TMP_Text Caption => caption;

        public void Init(TMP_Text captionText, IList<string> choices, int value, Action<int> changed)
        {
            caption = captionText;
            options.Clear();
            options.AddRange(choices);
            onChanged = changed;
            SetValueWithoutNotify(value);
        }

        public void SetValueWithoutNotify(int value)
        {
            Value = options.Count == 0 ? 0 : Mathf.Clamp(value, 0, options.Count - 1);
            caption.text = options.Count == 0 ? "" : options[Value];
        }

        /// Picks like a click on a row (used by the list and by scripts).
        public void Pick(int value)
        {
            SetValueWithoutNotify(value);
            onChanged?.Invoke(Value);
        }

        public void Open()
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var canvasRect = (RectTransform)canvas.transform;

            var root = new GameObject("Popup: list", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            StudioUi.Stretch((RectTransform)root.transform);
            var overlay = root.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 200; // above popups (100) and the search results (50)
            root.AddComponent<GraphicRaycaster>();
            Action close = () => { if (root != null) Destroy(root); };
            root.AddComponent<CloseOnEscape>().OnEscape = close;

            var backdrop = StudioUi.Panel(root.transform, "Backdrop", new Color(0, 0, 0, 0.01f));
            StudioUi.Stretch(backdrop);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => close());

            // Where the button is, in canvas units (world corners: never anchoredPosition across hierarchies).
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            Vector2 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 topLeft = canvasRect.InverseTransformPoint(corners[1]);
            Vector2 bottomRight = canvasRect.InverseTransformPoint(corners[3]);
            float half = canvasRect.rect.height / 2f;
            float below = bottomLeft.y + half - 8f;
            float above = half - topLeft.y - 8f;
            bool search = options.Count >= SearchFrom;
            float wanted = options.Count * (RowHeight + 2f) + 12f + (search ? SearchHeight + 6f : 0f);
            bool down = below >= Mathf.Min(wanted, 320f) || below >= above;
            float height = Mathf.Min(wanted, down ? below : above, 820f);
            float width = Mathf.Max(bottomRight.x - bottomLeft.x, 320f);
            float x = Mathf.Clamp(bottomLeft.x, -canvasRect.rect.width / 2f + 8f, canvasRect.rect.width / 2f - width - 8f);

            var panel = StudioUi.Panel(root.transform, "List", StudioUi.PanelColor);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, down ? 1f : 0f);
            panel.sizeDelta = new Vector2(width, height);
            panel.anchoredPosition = new Vector2(x, down ? bottomLeft.y - 2f : topLeft.y + 2f);
            var col = StudioUi.Column(panel, "Column", 6f);
            StudioUi.Stretch((RectTransform)col.transform, 6, 6, 6, 6);

            TMP_InputField input = null;
            if (search) input = StudioUi.Input(col.transform, "", _ => { }, -1f, "Type to filter");

            var scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources());
            scrollGo.transform.SetParent(col.transform, false);
            scrollGo.GetComponent<Image>().color = StudioUi.Background;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 60f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            StudioUi.StyleScrollbars(scroll);
            StudioUi.Size(scrollGo, -1, 10f).flexibleHeight = 1;
            var content = scroll.content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rows = new List<GameObject>();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var b = StudioUi.Button(content, options[i], () => { close(); Pick(index); }, -1f);
                StudioUi.Size(b.gameObject, -1, RowHeight);
                b.GetComponent<Image>().color = i == Value ? StudioUi.ChipColor : StudioUi.ControlColor;
                var t = b.GetComponentInChildren<TMP_Text>();
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.margin = new Vector4(10, 0, 0, 0);
                t.fontSize = 19f;
                t.overflowMode = TextOverflowModes.Ellipsis;
                rows.Add(b.gameObject);
            }

            if (input != null)
            {
                input.onValueChanged.AddListener(q =>
                {
                    string needle = (q ?? "").Trim().ToLowerInvariant();
                    for (int i = 0; i < rows.Count; i++)
                        rows[i].SetActive(needle.Length == 0 || options[i].ToLowerInvariant().Contains(needle));
                    scroll.verticalNormalizedPosition = 1f;
                });
                input.onSubmit.AddListener(_ =>
                {
                    int first = rows.FindIndex(r => r.activeSelf);
                    if (first < 0) return;
                    close();
                    Pick(first);
                });
                input.ActivateInputField();
            }

            // Show the current choice.
            Canvas.ForceUpdateCanvases();
            if (options.Count > 1)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - (float)Value / (options.Count - 1));
        }

        /// Closes the open list on Escape.
        private sealed class CloseOnEscape : MonoBehaviour
        {
            public Action OnEscape;
            private void Update()
            {
                if (Input.GetKeyDown(KeyCode.Escape)) OnEscape?.Invoke();
            }
        }
    }
}
