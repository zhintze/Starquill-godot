using System.Collections.Generic;

namespace Starquill.Studio
{
    /// Undo/redo for the Single screen (P23): a deep snapshot of the character
    /// before each change. Undo and Redo change the character in place.
    public sealed class EditHistory
    {
        public const int Limit = 100;

        private readonly List<StudioCharacter> undo = new List<StudioCharacter>();
        private readonly List<StudioCharacter> redo = new List<StudioCharacter>();

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int UndoCount => undo.Count;

        /// Call before changing current.
        public void Record(StudioCharacter current)
        {
            undo.Add(current.Clone());
            if (undo.Count > Limit) undo.RemoveAt(0);
            redo.Clear();
        }

        public bool Undo(StudioCharacter current) => Step(undo, redo, current);

        public bool Redo(StudioCharacter current) => Step(redo, undo, current);

        private static bool Step(List<StudioCharacter> from, List<StudioCharacter> to, StudioCharacter current)
        {
            if (from.Count == 0) return false;
            to.Add(current.Clone());
            current.CopyFrom(from[from.Count - 1]);
            from.RemoveAt(from.Count - 1);
            return true;
        }
    }
}
