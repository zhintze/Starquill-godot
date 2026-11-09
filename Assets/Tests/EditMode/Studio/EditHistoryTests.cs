using NUnit.Framework;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class EditHistoryTests
    {
        private static StudioCharacter NewCharacter() =>
            new StudioRoller(StudioTestRepo.Data).RollCharacter(new[] { "human" }, new RandomSettings(), new System.Random(1));

        [Test]
        public void UndoThenRedo_RestoresEachState()
        {
            var c = NewCharacter();
            var h = new EditHistory();
            var first = c.Body.SkinColor;

            h.Record(c); c.Body.SkinColor = Color.red;
            h.Record(c); c.Body.SkinColor = Color.blue;

            Assert.IsTrue(h.Undo(c)); Assert.AreEqual(Color.red, c.Body.SkinColor);
            Assert.IsTrue(h.Undo(c)); Assert.AreEqual(first, c.Body.SkinColor);
            Assert.IsFalse(h.Undo(c));
            Assert.IsTrue(h.Redo(c)); Assert.AreEqual(Color.red, c.Body.SkinColor);
            Assert.IsTrue(h.Redo(c)); Assert.AreEqual(Color.blue, c.Body.SkinColor);
            Assert.IsFalse(h.Redo(c));
        }

        [Test]
        public void NewChangeAfterUndo_ClearsRedo()
        {
            var c = NewCharacter();
            var h = new EditHistory();
            h.Record(c); c.Body.SkinColor = Color.red;
            h.Undo(c);

            h.Record(c); c.Body.SkinColor = Color.green;

            Assert.IsFalse(h.CanRedo);
        }

        [Test]
        public void KeepsAtMostTheLimit()
        {
            var c = NewCharacter();
            var h = new EditHistory();
            for (int i = 0; i < EditHistory.Limit + 20; i++) h.Record(c);
            Assert.AreEqual(EditHistory.Limit, h.UndoCount);
        }
    }
}
