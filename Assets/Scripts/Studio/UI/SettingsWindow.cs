using System;
using UnityEngine;

namespace Starquill.Studio.UI
{
    /// Popup with the RandomSettings sliders: chance per slot, facial
    /// chances, weight per item type grouped by slot. Edits the Bulk
    /// screen's settings object directly; the next roll uses them.
    public sealed class SettingsWindow
    {
        private static readonly (string prefix, string title)[] Groups =
        {
            ("hd", "Head"), ("tr", "Torso"), ("ar", "Arms"), ("lg", "Legs"), ("fe", "Feet"), ("mc", "Misc")
        };

        public SettingsWindow(Transform canvasRoot, StudioData data, RandomSettings s)
        {
            var list = StudioUi.Popup(canvasRoot, "Random settings", new Vector2(820, 920), out var close);

            var top = StudioUi.Row(list, "Top", 44f);
            StudioUi.Button(top.transform, "Reset to defaults", () =>
            {
                s.Reset();
                close();
                new SettingsWindow(canvasRoot, data, s);
            }, 230f);
            StudioUi.Label(top.transform, "Changes apply to the next Randomize All, Equip Random or Reroll.", 17f);

            Header(list, "Chance per slot (0 = never, 1 = always)");
            Chance(list, "Head", s.Head, v => s.Head = v);
            Chance(list, "Torso", s.Torso, v => s.Torso = v);
            Chance(list, "Arms", s.Arms, v => s.Arms = v);
            Chance(list, "Legs", s.Legs, v => s.Legs = v);
            Chance(list, "Feet", s.Feet, v => s.Feet = v);
            Chance(list, "Main hand", s.MainHand, v => s.MainHand = v);
            Chance(list, "Off hand", s.OffHand, v => s.OffHand = v);
            Note(list, "Off hand rolls only when the main hand is empty or one-handed.");
            Chance(list, "Misc, each of 1-4", s.MiscEach, v => s.MiscEach = v);

            Header(list, "Face");
            Chance(list, "Facial hair", s.FacialHair, v => s.FacialHair = v);
            Chance(list, "Facial detail", s.FacialDetail, v => s.FacialDetail = v);

            Header(list, "Weight per item type within its slot (0 = never, 1 = normal)");
            foreach (var (prefix, title) in Groups)
            {
                Note(list, title);
                foreach (var e in data.Catalog.GetByPrefix(prefix)) Weight(list, e.ItemType, e.Description, s);
            }
            Note(list, "Weapons");
            foreach (var w in data.Catalog.WeaponEntries) Weight(list, w.ItemType, w.Description, s);
        }

        private static void Header(Transform list, string text) => StudioUi.Label(list, text, 22f).color = StudioUi.Accent;

        private static void Note(Transform list, string text) => StudioUi.Label(list, text, 17f);

        private static void Chance(Transform list, string label, float value, Action<float> set) =>
            StudioUi.Slider(list, label, 0f, 1f, value, set, "0.00");

        private static void Weight(Transform list, string itemType, string description, RandomSettings s) =>
            StudioUi.Slider(list, $"{itemType}  {description}", 0f, 3f, s.WeightOf(itemType), v => s.TypeWeights[itemType] = v, "0.0");
    }
}
