using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Studio.UI
{
    /// Popup with a checkbox per species. Unchecked names go into
    /// excluded (StudioSettings.excludedSpecies); onChanged saves.
    public sealed class SpeciesPickerView
    {
        public SpeciesPickerView(Transform canvasRoot, IReadOnlyList<string> names, List<string> excluded, Action onChanged)
        {
            var list = StudioUi.Popup(canvasRoot, "Species", new Vector2(560, 860), out _);
            var toggles = new List<Toggle>();

            var buttons = StudioUi.Row(list, "AllNone", 44f);
            StudioUi.Button(buttons.transform, "All", () => SetAll(true), 120f);
            StudioUi.Button(buttons.transform, "None", () => SetAll(false), 120f);
            StudioUi.Label(list, "New and rerolled characters pick from the checked species.", 17f);

            foreach (var name in names)
            {
                string n = name;
                toggles.Add(StudioUi.Toggle(list, n, !excluded.Contains(n), on =>
                {
                    if (on) excluded.Remove(n);
                    else if (!excluded.Contains(n)) excluded.Add(n);
                    onChanged();
                }, 400f));
            }

            void SetAll(bool on)
            {
                excluded.Clear();
                if (!on) excluded.AddRange(names);
                foreach (var t in toggles) t.SetIsOnWithoutNotify(on);
                onChanged();
            }
        }
    }
}
