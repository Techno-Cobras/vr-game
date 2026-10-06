using System;

namespace VrGame.Domain.Inventory
{
    public readonly struct InventoryId : IEquatable<InventoryId>
    {
        public InventoryId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Inventory ID не может быть пустым.", nameof(value));

            Value = value;
        }

        public Guid Value { get; }

        public static InventoryId New() => new InventoryId(Guid.NewGuid());

        public bool Equals(InventoryId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is InventoryId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString("D");

        public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);

        public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);
    }
}
