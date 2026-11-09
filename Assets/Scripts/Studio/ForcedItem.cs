using System;

namespace Starquill.Studio
{
    /// One item variant picked in search, e.g. hd01-0001.
    public sealed class ForcedItem : IEquatable<ForcedItem>
    {
        public readonly string ItemType;
        public readonly int ItemNum;

        public ForcedItem(string itemType, int itemNum)
        {
            ItemType = itemType;
            ItemNum = itemNum;
        }

        public string Token => $"{ItemType}-{ItemNum:D4}";
        public bool IsWeapon => ItemType.StartsWith("w", StringComparison.Ordinal);
        /// Slot code: hd, tr, ar, lg, fe, mc (weapons: w0).
        public string Prefix => ItemType.Substring(0, 2);

        /// True when the slot holds this exact variant.
        public bool Matches(StudioItem item) => item != null && item.ItemType == ItemType && item.ItemNum == ItemNum;

        public bool Equals(ForcedItem other) => other != null && other.ItemType == ItemType && other.ItemNum == ItemNum;
        public override bool Equals(object obj) => Equals(obj as ForcedItem);
        public override int GetHashCode() => (ItemType, ItemNum).GetHashCode();
        public override string ToString() => Token;
    }
}
