using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Core;
using Starquill.Display;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Starquill.Studio.UI
{
    /// Edits one character (design §2 Single screen): a large preview with a
    /// background choice, Body and Gear tabs, undo/redo, export this one.
    /// Opens over the Bulk screen on the same StudioCharacter (P18).
    public sealed class SingleScreen
    {
        private static readonly (EquipmentSlot slot, string label)[] Slots =
        {
            (EquipmentSlot.Head, "Head"), (EquipmentSlot.Torso, "Torso"), (EquipmentSlot.Arms, "Arms"),
            (EquipmentSlot.Legs, "Legs"), (EquipmentSlot.Feet, "Feet"), (EquipmentSlot.MainHand, "Main hand"),
            (EquipmentSlot.OffHand, "Off hand"), (EquipmentSlot.Misc1, "Misc 1"), (EquipmentSlot.Misc2, "Misc 2"),
            (EquipmentSlot.Misc3, "Misc 3"), (EquipmentSlot.Misc4, "Misc 4")
        };

        private const float PreviewSize = 800f;

        private readonly StudioApp app;
        private readonly Transform canvas;
        private readonly StudioCharacter c;
        private readonly int number;
        private readonly RandomSettings random;
        private readonly StudioRoller roller;
        private readonly System.Random rng = new System.Random(Environment.TickCount);
        private readonly EditHistory history = new EditHistory();
        private readonly Action onClosed;
        private readonly GameObject root;
        private readonly CharacterDisplay preview;
        private readonly RawImage backdrop;
        private readonly ThumbnailRenderer thumbs;
        private readonly List<Texture2D> rowTextures = new List<Texture2D>();
        private readonly RectTransform list;
        private readonly ScrollRect scroll;
        private readonly TMP_Text status;
        private readonly Button undoButton, redoButton, bodyTab, gearTab;
        private bool showGear;

        private StudioData Data => app.Data;
        private SpeciesDisplayData Species => Data.Registry.Species[c.SpeciesName];

        public SingleScreen(Transform canvas, StudioApp app, StudioCharacter character, int number, RandomSettings random, Action onClosed)
        {
            this.app = app;
            this.canvas = canvas;
            c = character;
            this.number = number;
            this.random = random;
            this.onClosed = onClosed;
            roller = new StudioRoller(app.Data);

            var rootRt = StudioUi.Panel(canvas, "Single", StudioUi.Background);
            StudioUi.Stretch(rootRt);
            root = rootRt.gameObject;
            root.AddComponent<KeyShortcuts>().Init(Undo, Redo, PopupOpen);

            var top = StudioUi.Row(root.transform, "TopBar", 48f);
            Place((RectTransform)top.transform, 16f, -8f, -32f, 48f, stretch: true);
            StudioUi.Button(top.transform, "Back", Close, 110f);
            undoButton = StudioUi.Button(top.transform, "Undo", Undo, 100f);
            redoButton = StudioUi.Button(top.transform, "Redo", Redo, 100f);
            StudioUi.Button(top.transform, "Reroll body", () => Change(() => roller.RollBody(c, random, rng)), 150f);
            StudioUi.Button(top.transform, "Reroll gear", () => Change(() => roller.RollGear(c, random, rng)), 150f);
            StudioUi.Button(top.transform, "Export this", Export, 150f);
            status = StudioUi.Label(top.transform, "Ctrl+Z undo, Ctrl+Y redo.", 18f);

            var frame = new GameObject("PreviewFrame", typeof(RectTransform), typeof(RawImage));
            frame.transform.SetParent(root.transform, false);
            Place((RectTransform)frame.transform, 16f, -68f, PreviewSize, PreviewSize);
            backdrop = frame.GetComponent<RawImage>();
            var doll = new GameObject("Character", typeof(RectTransform), typeof(RawImage));
            doll.transform.SetParent(frame.transform, false);
            StudioUi.Stretch((RectTransform)doll.transform);
            preview = app.Renderer.CreatePreview((int)PreviewSize);
            var dollImage = doll.GetComponent<RawImage>();
            dollImage.texture = preview.Texture;
            dollImage.raycastTarget = false;

            var bg = StudioUi.Row(root.transform, "Backgrounds", 40f);
            Place((RectTransform)bg.transform, 16f, -876f, PreviewSize, 40f);
            StudioUi.Label(bg.transform, "Background", 20f);
            StudioUi.Button(bg.transform, "Checkerboard", () => SetBackground(0), 170f);
            StudioUi.Button(bg.transform, "White", () => SetBackground(1), 110f);
            StudioUi.Button(bg.transform, "Dark", () => SetBackground(2), 110f);
            StudioUi.Label(bg.transform, "Preview only: exports are transparent.", 16f);

            var tabs = StudioUi.Row(root.transform, "Tabs", 44f);
            Place((RectTransform)tabs.transform, 840f, -68f, -856f, 44f, stretch: true);
            bodyTab = StudioUi.Button(tabs.transform, "Body", () => ShowTab(false), 140f);
            gearTab = StudioUi.Button(tabs.transform, "Gear", () => ShowTab(true), 140f);

            list = StudioUi.ScrollList(root.transform);
            var view = (RectTransform)list.parent.parent;
            StudioUi.Stretch(view, 840f, 120f, 16f, 16f);
            scroll = view.GetComponent<ScrollRect>();

            thumbs = new ThumbnailRenderer(app.Data, app.Renderer.Resolver, app.transform);
            SetBackground(0);
            Refresh(true);
        }

        /// Top-left anchored rect; stretch: width is a sizeDelta against the full width.
        private static void Place(RectTransform rt, float x, float y, float width, float height, bool stretch = false)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(stretch ? 1 : 0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
        }

        // ---- screen ----

        private bool PopupOpen()
        {
            for (int i = 0; i < canvas.childCount; i++)
                if (canvas.GetChild(i).name.StartsWith("Popup: ", StringComparison.Ordinal)) return true;
            return false;
        }

        private void SetBackground(int which)
        {
            backdrop.texture = which == 0 ? StudioUi.Checkerboard() : null;
            backdrop.uvRect = new Rect(0, 0, 40, 40);
            backdrop.color = which == 2 ? new Color(0.08f, 0.08f, 0.10f) : Color.white;
        }

        private void ShowTab(bool gear)
        {
            showGear = gear;
            Refresh(true);
        }

        /// One undo step, then the change, then redraw.
        private void Change(Action change, string message = null)
        {
            history.Record(c);
            change();
            Refresh(false);
            if (message != null) status.text = message;
        }

        private void Undo()
        {
            status.text = history.Undo(c) ? "Undone." : "Nothing to undo.";
            Refresh(false);
        }

        private void Redo()
        {
            status.text = history.Redo(c) ? "Redone." : "Nothing to redo.";
            Refresh(false);
        }

        private void Refresh(bool resetScroll)
        {
            app.Renderer.Draw(preview, c);
            undoButton.interactable = history.CanUndo;
            redoButton.interactable = history.CanRedo;
            bodyTab.GetComponent<Image>().color = showGear ? StudioUi.ControlColor : StudioUi.ChipColor;
            gearTab.GetComponent<Image>().color = showGear ? StudioUi.ChipColor : StudioUi.ControlColor;

            float position = scroll.verticalNormalizedPosition;
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            foreach (var t in rowTextures) if (t != null) UnityEngine.Object.Destroy(t);
            rowTextures.Clear();

            if (showGear) BuildGear();
            else BuildBody();

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = resetScroll ? 1f : position;
        }

        private void Close()
        {
            foreach (var t in rowTextures) if (t != null) UnityEngine.Object.Destroy(t);
            thumbs.Dispose();
            app.Renderer.Release(preview);
            UnityEngine.Object.Destroy(root);
            onClosed?.Invoke();
        }

        private void Export()
        {
            var s = app.Settings;
            string folder = s.ResolvedExportFolder;
            try
            {
                Directory.CreateDirectory(folder);
                var png = app.Renderer.ExportPng(c, StudioSettings.SnapScale(s.exportScale), s.trim, s.padding);
                string path = ExportNamer.UniquePath(folder, ExportNamer.FileName(c.SpeciesName, number, DateTime.Now));
                File.WriteAllBytes(path, png);
                status.text = "Exported " + path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                status.text = $"Could not write to {folder}: {e.Message}";
            }
        }

        // ---- row helpers ----

        private Transform Row(string label, float height = 46f)
        {
            var row = StudioUi.Row(list, "Row " + label, height, 8f);
            StudioUi.Label(row.transform, label, 20f, 150f);
            return row.transform;
        }

        private void Note(string text) => StudioUi.Label(list, "      " + text, 16f).color = new Color(0.75f, 0.75f, 0.8f);

        private void Thumb(Transform parent, Texture2D tex)
        {
            var bgPanel = StudioUi.Panel(parent, "Thumb", StudioUi.ControlColor);
            StudioUi.Size(bgPanel.gameObject, 40f, 40f);
            var go = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(bgPanel, false);
            StudioUi.Stretch((RectTransform)go.transform, 2, 2, 2, 2);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = tex;
            raw.color = tex != null ? Color.white : Color.clear;
            if (tex != null) rowTextures.Add(tex);
        }

        private void ColorButton(Transform row, Color color, string title, IReadOnlyList<Color> palette, Action<Color> apply)
        {
            StudioUi.Swatch(row, color, 36f, () => PickColor(title, color, palette, apply));
        }

        /// One undo step for the whole picker visit (P22); live preview while
        /// it is open, rows rebuilt when it closes.
        private void PickColor(string title, Color current, IReadOnlyList<Color> palette, Action<Color> apply)
        {
            history.Record(c);
            new ColorPickerView(canvas, title, current, palette, col =>
            {
                apply(col);
                app.Renderer.Draw(preview, c);
            }, () => Refresh(false));
        }

        private Color[] Palette(string[] field, bool mainWhenEmpty)
        {
            if (mainWhenEmpty && (field == null || field.Length == 0)) return Data.Registry.Colors.GetPalette("main");
            return Data.Registry.Colors.ResolveColorField(field);
        }

        // ---- Body tab ----

        private void BuildBody()
        {
            var sp = Species;
            var row = Row("Species");
            StudioUi.Dropdown(row, Data.SpeciesNames, Data.SpeciesNames.IndexOf(c.SpeciesName),
                i => Change(() => roller.ChangeSpecies(c, Data.SpeciesNames[i], random, rng), "New body for " + Data.SpeciesNames[i] + "; gear it can wear stays."), 300f);
            StudioUi.Button(row, "Reroll body", () => Change(() => roller.RollBody(c, random, rng)), 150f);

            row = Row("Skin");
            ColorButton(row, c.Body.SkinColor, "Skin color", Palette(sp.SkinColor, false), col => c.Body.SkinColor = col);

            var groups = BodyParts.HairGroups(sp);
            if (groups.Count > 0)
            {
                row = Row("Hair");
                var options = new List<string> { "None" };
                options.AddRange(groups);
                string chosen = c.Body.ChosenHairGroup;
                int current = string.IsNullOrEmpty(chosen) ? 0 : groups.ToList().IndexOf(chosen) + 1;
                StudioUi.Dropdown(row, options, Math.Max(0, current), i => Change(() => SetHairGroup(i == 0 ? null : groups[i - 1])), 120f);
                if (!string.IsNullOrEmpty(chosen))
                {
                    string group = ImageToken.Parse(chosen).GroupType;
                    c.Body.ModularImageNums.TryGetValue(group, out var variant);
                    Thumb(row, thumbs.Part(c.Body, "hair", chosen, group, variant));
                    StudioUi.Button(row, variant ?? "pick", () => PickPartVariant("Hair", "hair", chosen, group, false), 110f);
                }
                ColorButton(row, c.Body.HairColor, "Hair color", Palette(sp.HairColor, true), col => c.Body.HairColor = col);
            }

            row = Row("Eye color");
            ColorButton(row, c.Body.EyesColor, "Eye color", Palette(sp.EyesColor, true), col => c.Body.EyesColor = col);
            if (!string.IsNullOrEmpty(sp.FacialDetail))
            {
                row = Row("Detail color");
                ColorButton(row, c.Body.FacialDetailColor, "Facial detail color", Palette(sp.FacialDetailColor, true), col => c.Body.FacialDetailColor = col);
            }

            foreach (var part in BodyParts.Editable(sp))
            {
                row = Row(part.Label);
                bool hidden = IsHidden(part.Field);
                if (part.Group != null)
                {
                    c.Body.ModularImageNums.TryGetValue(part.Group, out var variant);
                    Thumb(row, hidden ? null : thumbs.Part(c.Body, part.Field, part.Token, part.Group, variant));
                    var p = part;
                    StudioUi.Button(row, hidden ? "None" : variant ?? "pick", () => PickPartVariant(p.Label, p.Field, p.Token, p.Group, p.CanBeNone), 110f);
                }
                else
                {
                    var p = part;
                    StudioUi.Toggle(row, "Show", !hidden, on => Change(() => SetHidden(p.Field, !on)), 100f);
                }
            }
        }

        private bool IsHidden(string field) =>
            field == "facialHair" ? c.HideFacialHair : field == "facialDetail" && c.HideFacialDetail;

        private void SetHidden(string field, bool hidden)
        {
            if (field == "facialHair") c.HideFacialHair = hidden;
            else if (field == "facialDetail") c.HideFacialDetail = hidden;
        }

        private void SetHairGroup(string token)
        {
            c.Body.ChosenHairGroup = token;
            if (token == null) return;
            string group = ImageToken.Parse(token).GroupType;
            if (!c.Body.ModularImageNums.ContainsKey(group))
                c.Body.ModularImageNums[group] = Data.Registry.PickModularImageNum(group, rng);
        }

        private void PickPartVariant(string label, string field, string token, string group, bool allowNone)
        {
            if (!Data.Registry.ModularPartVariants.TryGetValue(group, out var variants) || variants.Length == 0)
            {
                status.text = $"{group} has no drawings.";
                return;
            }
            var names = variants.Select(n => n.ToString("D4")).ToList();
            c.Body.ModularImageNums.TryGetValue(group, out var current);
            int selected = IsHidden(field) ? -1 : names.IndexOf(current);
            var options = names.Select(n => new VariantOption
            {
                Label = n,
                Thumbnail = () => thumbs.Part(c.Body, field, token, group, n)
            }).ToList();
            new VariantPickerView(canvas, $"{label} ({group})", options, selected, allowNone, i => Change(() =>
            {
                SetHidden(field, i < 0);
                if (i >= 0) c.Body.ModularImageNums[group] = names[i];
            }));
        }

        // ---- Gear tab ----

        private IEnumerable<string> TypesFor(EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.MainHand)
                return Data.Catalog.WeaponEntries.Where(w => !StudioRoller.IsShield(w.ItemType)).Select(w => w.ItemType);
            if (slot == EquipmentSlot.OffHand)
                return Data.Catalog.WeaponEntries.Where(w => w.HandType == "one_handed").Select(w => w.ItemType);
            return Data.Catalog.GetByPrefix(StudioRoller.PrefixFor(slot)).Select(e => e.ItemType);
        }

        private int[] HiddenLayersOf(string itemType) =>
            Data.Catalog.GetEntry(itemType)?.HiddenLayers ?? Data.Catalog.GetWeapon(itemType)?.HiddenLayers;

        private void BuildGear()
        {
            var view = c.SpeciesView(Species);
            var restricted = new HashSet<string>(Species.ItemRestrictions ?? Array.Empty<string>());
            foreach (var (slot, label) in Slots)
            {
                c.Gear.TryGetValue(slot, out var item);
                var types = TypesFor(slot).Where(t => !restricted.Contains(t)).ToList();
                var row = Row(label);
                var options = new List<string> { "None" };
                options.AddRange(types.Select(t => $"{t}  {ItemSearch.Description(Data, t)}"));
                int current = item == null ? 0 : types.IndexOf(item.ItemType) + 1;
                var s = slot;
                StudioUi.Dropdown(row, options, Math.Max(0, current), i => Change(() => SetType(s, i == 0 ? null : types[i - 1])), 300f);

                if (item != null)
                {
                    Thumb(row, thumbs.Item(item));
                    StudioUi.Button(row, item.ItemNum.ToString("D4"), () => PickItemVariant(s), 90f);
                    ColorButton(row, item.BaseColor, $"{label} color", Palette(null, true), col => item.BaseColor = col);
                    foreach (int layer in item.VarianceColors.Keys.OrderBy(l => l).ToList())
                    {
                        int l = layer;
                        ColorButton(row, item.VarianceColors[l], $"{label} second color (layer {l})", Palette(null, true), col => item.VarianceColors[l] = col);
                    }
                }
                StudioUi.Button(row, "Reroll", () => Change(() => roller.RollSlot(c, s, random, rng)), 90f);
                StudioUi.Toggle(row, "Lock", c.LockedSlots.Contains(slot), on => Change(() =>
                {
                    if (on) c.LockedSlots.Add(s);
                    else c.LockedSlots.Remove(s);
                }), 80f);
                if (item != null) StudioUi.Button(row, "x", () => Change(() => c.Gear.Remove(s)), 44f);

                if (item == null) continue;
                var weapon = Data.Catalog.GetWeapon(item.ItemType);
                if (weapon != null && weapon.Modular && item.LayerVariants != null)
                {
                    var parts = Row("   parts", 40f);
                    for (int l = 0; l < weapon.LayerCodes.Length && l < item.LayerVariants.Length; l++)
                    {
                        int index = l;
                        StudioUi.Button(parts, $"{weapon.LayerCodes[l]}: {item.LayerVariants[l]:D4}", () => PickWeaponPart(s, index), 140f);
                    }
                }
                var hides = BodyParts.HiddenBy(view, Data.Registry, c.Body.ChosenHairGroup, HiddenLayersOf(item.ItemType));
                if (hides.Count > 0) Note("Hides: " + string.Join(", ", hides));
            }
        }

        private void SetType(EquipmentSlot slot, string itemType)
        {
            c.Gear.TryGetValue(slot, out var old);
            if (itemType == null)
            {
                c.Gear.Remove(slot);
                return;
            }
            var numbers = ItemSearch.NumbersFor(Data, itemType);
            if (numbers.Length == 0) return;
            var item = roller.MakeItem(itemType, numbers[0], rng);
            if (old != null) item.BaseColor = old.BaseColor;
            c.Gear[slot] = item;
            roller.ResolveHands(c, slot);
        }

        private void PickItemVariant(EquipmentSlot slot)
        {
            var item = c.Gear[slot];
            var numbers = ItemSearch.NumbersFor(Data, item.ItemType);
            var options = numbers.Select(n => new VariantOption
            {
                Label = n.ToString("D4"),
                Thumbnail = () => thumbs.Item(WithNumber(item, n))
            }).ToList();
            new VariantPickerView(canvas, $"{item.ItemType} {ItemSearch.Description(Data, item.ItemType)}", options,
                Array.IndexOf(numbers, item.ItemNum), false, i => Change(() =>
                {
                    var picked = WithNumber(item, numbers[i]);
                    item.ItemNum = picked.ItemNum;
                    item.LayerVariants = picked.LayerVariants;
                }));
        }

        private static StudioItem WithNumber(StudioItem item, int n)
        {
            var copy = item.Clone();
            copy.ItemNum = n;
            if (copy.LayerVariants != null) copy.LayerVariants = Enumerable.Repeat(n, copy.LayerVariants.Length).ToArray();
            return copy;
        }

        private void PickWeaponPart(EquipmentSlot slot, int layerIndex)
        {
            var item = c.Gear[slot];
            var weapon = Data.Catalog.GetWeapon(item.ItemType);
            var numbers = weapon.VariantsPerLayer[layerIndex];
            var options = numbers.Select(n => new VariantOption
            {
                Label = n.ToString("D4"),
                Thumbnail = () =>
                {
                    var copy = item.Clone();
                    copy.LayerVariants[layerIndex] = n;
                    return thumbs.Item(copy);
                }
            }).ToList();
            new VariantPickerView(canvas, $"{item.ItemType} part {weapon.LayerCodes[layerIndex]}", options,
                Array.IndexOf(numbers, item.LayerVariants[layerIndex]), false, i => Change(() =>
                {
                    item.LayerVariants[layerIndex] = numbers[i];
                    if (layerIndex == 0) item.ItemNum = numbers[i];
                }));
        }
    }
}
