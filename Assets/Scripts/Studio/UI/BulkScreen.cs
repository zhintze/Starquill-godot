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
    /// B1: Count, Randomize All, Equip Random, Lock, reroll, Export.
    public sealed class BulkScreen
    {
        private sealed class Card
        {
            public GameObject Root;
            public CharacterDisplay Display;
            public RawImage Image;
            public TMP_Text Species;
            public Toggle Lock;
        }

        private readonly StudioApp app;
        private readonly StudioRoller roller;
        private readonly RandomSettings random = new RandomSettings();
        private readonly System.Random rng = new System.Random(Environment.TickCount);
        private readonly List<StudioCharacter> characters = new List<StudioCharacter>();
        private readonly List<Card> cards = new List<Card>();
        private readonly RectTransform grid;
        private readonly TMP_Text status;
        private readonly Button warnings;

        public BulkScreen(Transform parent, StudioApp app)
        {
            this.app = app;
            roller = new StudioRoller(app.Data);
            var s = app.Settings;

            var root = StudioUi.Panel(parent, "Bulk", StudioUi.Background);
            StudioUi.Stretch(root);

            var top = StudioUi.Row(root, "TopBar", 56f);
            var topRt = (RectTransform)top.transform;
            topRt.anchorMin = new Vector2(0, 1);
            topRt.anchorMax = new Vector2(1, 1);
            topRt.pivot = new Vector2(0.5f, 1);
            topRt.sizeDelta = new Vector2(-24, 56);
            topRt.anchoredPosition = new Vector2(0, -8);

            StudioUi.Label(top.transform, "Count", 20f, 60f);
            StudioUi.Dropdown(top.transform, StudioSettings.Counts.Select(c => c.ToString()).ToList(),
                Array.IndexOf(StudioSettings.Counts, s.count), i => { s.count = StudioSettings.Counts[i]; app.SaveSettings(); SetCount(s.count); }, 90f);
            StudioUi.Button(top.transform, "Randomize All", RandomizeAll, 170f);
            StudioUi.Button(top.transform, "Equip Random", EquipRandom, 170f);
            StudioUi.Label(top.transform, "Scale", 20f, 56f);
            StudioUi.Dropdown(top.transform, StudioSettings.Scales.Select(x => x + "x").ToList(),
                Array.IndexOf(StudioSettings.Scales, StudioSettings.SnapScale(s.exportScale)),
                i => { s.exportScale = StudioSettings.Scales[i]; app.SaveSettings(); }, 80f);
            StudioUi.Toggle(top.transform, "Trim", s.trim, v => { s.trim = v; app.SaveSettings(); }, 90f);
            StudioUi.Button(top.transform, "Export", Export, 120f);
            StudioUi.Button(top.transform, "Open folder", OpenExportFolder, 150f);
            warnings = StudioUi.Button(top.transform, "Warnings", () => new WarningsView(parent, app), 170f);
            StudioUi.Button(top.transform, "Repo folder", app.ShowRepoPicker, 150f);

            status = StudioUi.Label(root, "", 18f);
            var statusRt = (RectTransform)status.transform;
            statusRt.anchorMin = new Vector2(0, 0);
            statusRt.anchorMax = new Vector2(1, 0);
            statusRt.pivot = new Vector2(0.5f, 0);
            statusRt.sizeDelta = new Vector2(-24, 30);
            statusRt.anchoredPosition = new Vector2(0, 6);

            grid = StudioUi.ScrollGrid(root, new Vector2(220, 300), 12f);
            StudioUi.Stretch((RectTransform)grid.parent.parent, 0, 72, 0, 40); // the ScrollView

            SetCount(s.count);
            UpdateWarningsButton();
        }

        private void SetCount(int count)
        {
            while (characters.Count < count)
            {
                characters.Add(roller.RollCharacter(app.Data.SpeciesNames, random, rng));
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

            card.Species = StudioUi.Label(col.transform, "", 18f);
            var row = StudioUi.Row(col.transform, "Buttons", 34f, 6f);
            card.Lock = StudioUi.Toggle(row.transform, "Lock", false, v => characters[index].Locked = v, 90f);
            StudioUi.Button(row.transform, "Reroll", () => Reroll(index), 100f);
            return card;
        }

        private void Redraw(int i)
        {
            app.Renderer.Draw(cards[i].Display, characters[i]);
            cards[i].Species.text = characters[i].SpeciesName;
            cards[i].Lock.SetIsOnWithoutNotify(characters[i].Locked);
            UpdateWarningsButton();
        }

        private void RandomizeAll()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                characters[i] = roller.RollCharacter(app.Data.SpeciesNames, random, rng);
                Redraw(i);
            }
        }

        private void EquipRandom()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].Locked) continue;
                roller.RollGear(characters[i], random, rng);
                Redraw(i);
            }
        }

        private void Reroll(int i)
        {
            bool locked = characters[i].Locked;
            characters[i] = roller.RollCharacter(app.Data.SpeciesNames, random, rng);
            characters[i].Locked = locked;
            Redraw(i);
        }

        private void Export()
        {
            var s = app.Settings;
            string folder = s.ResolvedExportFolder;
            try
            {
                Directory.CreateDirectory(folder);
                var today = DateTime.Now;
                for (int i = 0; i < characters.Count; i++)
                {
                    var png = app.Renderer.ExportPng(characters[i], StudioSettings.SnapScale(s.exportScale), s.trim, s.padding);
                    File.WriteAllBytes(ExportNamer.UniquePath(folder, ExportNamer.FileName(characters[i].SpeciesName, i + 1, today)), png);
                }
                status.color = StudioUi.TextColor;
                status.text = $"Exported {characters.Count} PNGs to {folder}";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                status.color = StudioUi.ErrorColor;
                status.text = $"Could not write to {folder}: {e.Message}. Nothing was lost; pick another folder.";
            }
            UpdateWarningsButton();
        }

        private void OpenExportFolder()
        {
            string folder = app.Settings.ResolvedExportFolder;
            Directory.CreateDirectory(folder);
            Application.OpenURL(new Uri(folder).AbsoluteUri);
        }

        private void UpdateWarningsButton()
        {
            int count = app.Data.Issues.Count + app.Renderer.Resolver.MissingPaths.Count;
            warnings.GetComponentInChildren<TMP_Text>().text = $"Warnings ({count})";
        }
    }
}
