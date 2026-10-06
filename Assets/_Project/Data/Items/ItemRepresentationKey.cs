using System;

namespace VrGame.Data.Items
{
    public readonly struct ItemRepresentationKey : IEquatable<ItemRepresentationKey>, IComparable<ItemRepresentationKey>
    {
        private const string Prefix = "representation.";
        private readonly string value;

        public ItemRepresentationKey(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidRepresentationKey,
                    $"Representation key должен быть lowercase ASCII namespaced-значением с префиксом '{Prefix}'.");

            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public int CompareTo(ItemRepresentationKey other) => StringComparer.Ordinal.Compare(Value, other.Value);

        public bool Equals(ItemRepresentationKey other) => StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is ItemRepresentationKey other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(ItemRepresentationKey left, ItemRepresentationKey right) => left.Equals(right);

        public static bool operator !=(ItemRepresentationKey left, ItemRepresentationKey right) => !left.Equals(right);
    }
}
