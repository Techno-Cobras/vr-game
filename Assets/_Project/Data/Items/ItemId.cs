using System;

namespace VrGame.Data.Items
{
    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        private const string Prefix = "item.";
        private readonly string value;

        public ItemId(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidItemId,
                    $"Item ID должен быть lowercase ASCII namespaced-значением с префиксом '{Prefix}'.");

            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public static bool TryParse(string value, out ItemId itemId)
        {
            if (!StableId.IsValid(value, Prefix))
            {
                itemId = default;
                return false;
            }

            itemId = new ItemId(value);
            return true;
        }

        public int CompareTo(ItemId other) => StringComparer.Ordinal.Compare(Value, other.Value);

        public bool Equals(ItemId other) => StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is ItemId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);

        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
    }
}
