using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starquill.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// The grid of random characters (ported from the Godot randomizer).
    /// Top: Count, Species, Randomize All, Equip Random, Settings, then the
    /// search box and Forced items chips. Bottom: export. Each card: species,
    /// skin color, Select, Lock, Reroll.
    public sealed class BulkScreen
    {
        private const int MaxResults = 15;
        private const string NoSpecies = "No species is checked. Use the Species button to check at least one.";

        private sealed class Card
        {
            public GameObject Root;
            public CharacterDisplay Display;
            public RawImage Image;
            public TMP_Dropdown Species;
            public Button SkinButton;
            public Image Skin;
            public Toggle Lock;
            public bool Selected;
        }

        private readonly StudioApp app;
        private readonly Transform canvas;
        private readonly StudioRoller roller;
        private readonly ForcedItems forced;
        private readonly RandomSettings random = new RandomSettings();
        private readonly System.Random rng = new System.Random(Environment.TickCount);
        private readonly List<StudioCharacter> characters = new List<StudioCharacter>();
        private readonly List<Card> cards = new List<Card>();
        private readonly Dictionary<ForcedItem, GameObject> chips = new Dictionary<ForcedItem, GameObject>();
        private readonly RectTransform grid;
        private readonly Transform forceRow;
        private readonly TMP_Text noChips;
        private readonly TMP_InputField search;
        private readonly RectTransform results;
        private readonly TMP_Text status;
        private readonly Button warnings, speciesButton, exportButton;

        private StudioData Data => app.Data;
        private StudioSettings S => app.Settings;

        public BulkScreen(Transform parent, StudioApp app)
        {
            this.app = app;
            canvas = parent;
            roller = new StudioRoller(app.Data);
            forced = new ForcedItems(app.Data);

            var root = StudioUi.Panel(parent, "Bulk", StudioUi.Background);
            StudioUi.Stretch(root);

            var top = StudioUi.Column(root, "TopBars", 8f);
            Dock((RectTransform)top.transform, true, 104f);

            var make = StudioUi.Row(top.transform, "Make", 48f);
            StudioUi.Label(make.transform, "Count", 20f);
            StudioUi.Dropdown(make.transform, StudioSettings.Counts.Select(c => c.ToString()).ToList(),
                Array.IndexOf(StudioSettings.Counts, S.count),
                i => { S.count = StudioSettings.Counts[i]; app.SaveSettings(); SetCount(S.count); }, 90f);
            speciesButton = StudioUi.Button(make.transform, "Species", OpenSpecies, 190f);
            StudioUi.Button(make.transform, "Randomize All", RandomizeAll, 170f);
            StudioUi.Button(make.transform, "Equip Random", EquipRandom, 170f);
            StudioUi.Button(make.transform, "Settings", OpenSettings, 130f);
            StudioUi.Spacer(make.transform);
            warnings = StudioUi.Button(make.transform, "Warnings", () => new WarningsView(canvas, app), 170f);
            StudioUi.Button(make.transform, "Repo folder", app.ShowRepoPicker, 150f);

            forceRow = StudioUi.Row(top.transform, "Force", 48f).transform;
            search = StudioUi.Input(forceRow, "", _ => { }, 420f, "Search: hd01-0001, a file name, or a word like hat");
            search.onValueChanged.AddListener(ShowResults);
            results = CreateResultsPanel(search.transform);
            StudioUi.Label(forceRow, "Forced items:", 20f);
            noChips = StudioUi.Label(forceRow, "none", 18f);

            var bottom = StudioUi.Column(root, "BottomBar", 4f);
            Dock((RectTransform)bottom.transform, false, 84f);
            var export = StudioUi.Row(bottom.transform, "Export", 48f);
            StudioUi.Label(export.transform, "Scale", 20f);
            StudioUi.Dropdown(export.transform, StudioSettings.Scales.Select(x => x + "x").ToList(),
                Array.IndexOf(StudioSettings.Scales, StudioSettings.SnapScale(S.exportScale)),
                i => { S.exportScale = StudioSettings.Scales[i]; app.SaveSettings(); }, 80f);
            StudioUi.Toggle(export.transform, "Trim", S.trim, v => { S.trim = v; app.SaveSettings(); }, 90f);
            StudioUi.Label(export.transform, "Folder", 20f);
            StudioUi.Input(export.transform, S.exportFolder, SetExportFolder, 640f, "Default: " + StudioSettings.DefaultExportFolder);
            StudioUi.Button(export.transform, "Open folder", OpenExportFolder, 150f);
            exportButton = StudioUi.Button(export.transform, "Export", Export, 220f);
            status = StudioUi.Label(bottom.transform, "", 18f);

            grid = StudioUi.ScrollGrid(root, new Vector2(220, 340), 12f);
            StudioUi.Stretch((RectTransform)grid.parent.parent, 0, 120, 0, 96); // the ScrollView

            SetCount(S.count);
            UpdateSpeciesButton();
            UpdateWarningsButton();
            UpdateExportButton();
        }

        private static void Dock(RectTransform rt, bool top, float height)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.sizeDelta = new Vector2(-24, height);
            rt.anchoredPosition = new Vector2(0, top ? -8 : 6);
        }

        // ---- characters ----

        private List<string> ChosenSpecies() => Data.SpeciesNames.Where(n => !S.excludedSpecies.Contains(n)).ToList();

        private StudioCharacter NewCharacter(IReadOnlyList<string> species)
        {
            var c = roller.RollCharacter(species, random, rng);
            forced.Apply(c, roller, rng);
            return c;
        }

        private void SetCount(int count)
        {
            var species = ChosenSpecies();
            while (characters.Count < count)
            {
                if (species.Count == 0) { ShowStatus(NoSpecies, true); break; }
                characters.Add(NewCharacter(species));
                cards.Add(CreateCard(cards.Count));
                Redraw(cards.Count - 1);
            }
            while (characters.Count > count)
            {
                int last = characters.Count - 1;
                UnityEngine.Object.Destroy(cards[last].Root);
                UnityEngine.Object.Destroy(cards[last].Display.gameObject);
                cards.RemoveAt(last);
                characters.RemoveAt(last);
            }
            UpdateExportButton();
        }

        private Card CreateCard(int index)
        {
            var card = new Card();
            var panel = StudioUi.Panel(grid, $"Card{index + 1}", StudioUi.PanelColor);
            card.Root = panel.gameObject;
            var col = StudioUi.Column(panel, "Column", 4f);
            StudioUi.Stretch((RectTransform)col.transform, 6, 6, 6, 6);

            var checker = new GameObject("Checker", typeof(RectTransform), typeof(RawImage));
            checker.transform.SetParent(col.transform, false);
            var checkerImage = checker.GetComponent<RawImage>();
            checkerImage.texture = StudioUi.Checkerboard();
            checkerImage.uvRect = new Rect(0, 0, 13, 13);
            StudioUi.Size(checker, 208, 208);

            var doll = new GameObject("Character", typeof(RectTransform), typeof(RawImage));
            doll.transform.SetParent(checker.transform, false);
            StudioUi.Stretch((RectTransform)doll.transform);
            card.Image = doll.GetComponent<RawImage>();
            card.Display = app.Renderer.CreatePreview();
            card.Image.texture = card.Display.Texture;

            card.Species = StudioUi.Dropdown(col.transform, Data.SpeciesNames, 0, v => ChangeSpecies(index, v), -1f);
            StudioUi.Size(card.Species.gameObject, -1, 34f);

            var body = StudioUi.Row(col.transform, "Body", 34f, 6f);
            card.SkinButton = StudioUi.Button(body.transform, "Skin", () => PickSkin(index), 64f);
            card.Skin = StudioUi.Swatch(body.transform, Color.white, 28f, () => PickSkin(index));
            StudioUi.Toggle(body.transform, "Select", false, v => { card.Selected = v; UpdateExportButton(); }, 96f);

            var row = StudioUi.Row(col.transform, "Buttons", 34f, 6f);
            card.Lock = StudioUi.Toggle(row.transform, "Lock", false, v => characters[index].Locked = v, 90f);
            StudioUi.Button(row.transform, "Reroll", () => Reroll(index), 100f);
            return card;
        }

        private void Redraw(int i)
        {
            var c = characters[i];
            var card = cards[i];
            app.Renderer.Draw(card.Display, c);
            card.Species.SetValueWithoutNotify(Data.SpeciesNames.IndexOf(c.SpeciesName));
            card.Skin.color = c.Body.SkinColor;
            card.Lock.SetIsOnWithoutNotify(c.Locked);
            UpdateWarningsButton();
        }

        private void RandomizeAll()
        {
            var species = ChosenSpecies();
            if (species.Count == 0) { ShowStatus(NoSpecies, true); return; }
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                characters[i] = NewCharacter(species);
                Redraw(i);
            }
            ShowStatus("", false);
        }

        private void EquipRandom()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                roller.RollGear(characters[i], random, rng);
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus("", false);
        }

        private void Reroll(int i)
        {
            var species = ChosenSpecies();
            if (species.Count == 0) { ShowStatus(NoSpecies, true); return; }
            bool locked = characters[i].Locked;
            characters[i] = NewCharacter(species);
            characters[i].Locked = locked;
            Redraw(i);
        }

        private void ChangeSpecies(int i, int option)
        {
            var c = characters[i];
            roller.ChangeSpecies(c, Data.SpeciesNames[option], random, rng);
            if (!c.Locked) forced.Apply(c, roller, rng);
            Redraw(i);
        }

        private void PickSkin(int i)
        {
            var c = characters[i];
            var palette = Data.Registry.Colors.ResolveColorField(Data.Registry.Species[c.SpeciesName].SkinColor);
            new SwatchPicker(canvas, $"Skin color: {c.SpeciesName}", palette, color =>
            {
                c.Body.SkinColor = color;
                Redraw(i);
            });
        }

        // ---- species and settings windows ----

        private void OpenSpecies()
        {
            search.text = "";
            new SpeciesPickerView(canvas, Data.SpeciesNames, S.excludedSpecies, () =>
            {
                app.SaveSettings();
                UpdateSpeciesButton();
            });
        }

        private void OpenSettings()
        {
            search.text = "";
            new SettingsWindow(canvas, Data, random);
        }

        private void UpdateSpeciesButton()
        {
            int n = ChosenSpecies().Count;
            speciesButton.GetComponentInChildren<TMP_Text>().text = $"Species ({n}/{Data.SpeciesNames.Count})";
            if (n == 0) ShowStatus(NoSpecies, true);
            else if (status.text == NoSpecies) ShowStatus("", false);
        }

        // ---- search and forced items ----

        private static RectTransform CreateResultsPanel(Transform searchInput)
        {
            var go = new GameObject("SearchResults", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(searchInput, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -4);
            rt.sizeDelta = new Vector2(640, 0);
            go.GetComponent<Image>().color = StudioUi.PanelColor;
            var col = go.GetComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(6, 6, 6, 6);
            col.spacing = 2f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Draw above the card grid, which comes later in the hierarchy.
            var overlay = go.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 50;
            go.AddComponent<GraphicRaycaster>();
            go.SetActive(false);
            return rt;
        }

        private void ShowResults(string query)
        {
            for (int i = results.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(results.GetChild(i).gameObject);
            if (string.IsNullOrWhiteSpace(query))
            {
                results.gameObject.SetActive(false);
                return;
            }
            results.gameObject.SetActive(true);
            var overlay = results.GetComponent<Canvas>(); // nested canvases can drop these when first enabled
            overlay.overrideSorting = true;
            overlay.sortingOrder = 50;

            var found = ItemSearch.Find(Data, query, MaxResults);
            foreach (var item in found.Items)
            {
                var it = item;
                var button = StudioUi.Button(results, $"{it.Token}   {ItemSearch.Description(Data, it.ItemType)}", () => AddForced(it), -1f);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(10, 0, 0, 0);
                button.interactable = !forced.Items.Contains(it); // already a chip
            }
            if (found.Total == 0)
                StudioUi.Label(results, "No match. Try hd01-0001, a file name, or a word like hat.", 18f);
            else if (found.Total > found.Items.Count)
                StudioUi.Label(results, $"Showing {found.Items.Count} of {found.Total}. Type more to narrow it.", 18f);
        }

        private void AddForced(ForcedItem item)
        {
            if (!forced.TryAdd(item, out var message))
            {
                ShowStatus(message, true);
                return;
            }
            search.text = "";
            AddChip(item);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus($"Forced {item} on every unlocked character. It stays through Randomize All and Equip Random.", false);
        }

        private void AddChip(ForcedItem item)
        {
            string label = item.Token + "  x";
            var chip = StudioUi.Button(forceRow, label, () => RemoveForced(item), 9f * label.Length + 30f);
            chip.GetComponent<Image>().color = StudioUi.ChipColor;
            chip.GetComponentInChildren<TMP_Text>().fontSize = 17f;
            chips[item] = chip.gameObject;
            noChips.gameObject.SetActive(false);
        }

        private void RemoveForced(ForcedItem item)
        {
            var before = forced.Items.ToList();
            forced.Remove(item);
            if (chips.TryGetValue(item, out var chip))
            {
                UnityEngine.Object.Destroy(chip);
                chips.Remove(item);
            }
            noChips.gameObject.SetActive(chips.Count == 0);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                ForcedItems.Strip(characters[i], before);
                forced.Apply(characters[i], roller, rng);
                Redraw(i);
            }
            ShowStatus($"Removed {item}. Its slot stays empty until the next Randomize All or Equip Random.", false);
        }

        // ---- export ----

        private void SetExportFolder(string value)
        {
            string v = (value ?? "").Trim().Trim('"');
            if (v.Length > 0 && !Path.IsPathRooted(v))
            {
                ShowStatus(@"Use a full folder path, for example C:\Users\you\Pictures\Starquill.", true);
                return;
            }
            S.exportFolder = v;
            app.SaveSettings();
            ShowStatus($"Exports go to {S.ResolvedExportFolder}", false);
        }

        private void Export()
        {
            string folder = S.ResolvedExportFolder;
            var targets = Enumerable.Range(0, characters.Count).Where(i => cards[i].Selected).ToList();
            if (targets.Count == 0) targets = Enumerable.Range(0, characters.Count).ToList();
            try
            {
                Directory.CreateDirectory(folder);
                var today = DateTime.Now;
                foreach (int i in targets)
                {
                    var png = app.Renderer.ExportPng(characters[i], StudioSettings.SnapScale(S.exportScale), S.trim, S.padding);
                    File.WriteAllBytes(ExportNamer.UniquePath(folder, ExportNamer.FileName(characters[i].SpeciesName, i + 1, today)), png);
                }
                ShowStatus($"Exported {targets.Count} PNGs to {folder}", false);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                ShowStatus($"Could not write to {folder}: {e.Message}. Nothing was lost; pick another folder.", true);
            }
            UpdateWarningsButton();
        }

        private void OpenExportFolder()
        {
            string folder = S.ResolvedExportFolder;
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is UriFormatException)
            {
                ShowStatus($"Could not open {folder}: {e.Message}", true);
            }
        }

        private void UpdateExportButton()
        {
            int n = cards.Count(c => c.Selected);
            exportButton.GetComponentInChildren<TMP_Text>().text = n > 0 ? $"Export selected ({n})" : $"Export all ({cards.Count})";
        }

        // ---- status ----

        private void ShowStatus(string message, bool error)
        {
            status.color = error ? StudioUi.ErrorColor : StudioUi.TextColor;
            status.text = message;
        }

        private void UpdateWarningsButton()
        {
            int count = Data.Issues.Count + app.Renderer.Resolver.MissingPaths.Count;
            warnings.GetComponentInChildren<TMP_Text>().text = $"Warnings ({count})";
        }
    }
}
