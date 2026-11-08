using UnityEngine;

namespace Starquill.Display
{
    /// Where ImageResolver gets sprites. The game loads from Resources;
    /// the Character Studio loads PNGs from the artist's repo clone.
    public interface ISpriteSource
    {
        /// Null when the sprite does not exist.
        Sprite Load(string spritePath);
    }

    public sealed class ResourcesSpriteSource : ISpriteSource
    {
        public Sprite Load(string spritePath) => Resources.Load<Sprite>(spritePath);
    }
}
