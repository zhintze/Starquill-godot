using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Starquill.Studio.UI
{
    /// Ctrl+Z undo, Ctrl+Y / Ctrl+Shift+Z redo (Cmd on a Mac keyboard).
    /// Ignored while typing in a text field or while blocked() is true.
    public sealed class KeyShortcuts : MonoBehaviour
    {
        private Action undo, redo;
        private Func<bool> blocked;

        public void Init(Action undo, Action redo, Func<bool> blocked)
        {
            this.undo = undo;
            this.redo = redo;
            this.blocked = blocked;
        }

        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                        || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (!ctrl) return;
            bool z = Input.GetKeyDown(KeyCode.Z), y = Input.GetKeyDown(KeyCode.Y);
            if (!z && !y) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            if (field != null && field.isFocused) return;
            if (blocked != null && blocked()) return;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (y || shift) redo?.Invoke();
            else undo?.Invoke();
        }
    }
}
