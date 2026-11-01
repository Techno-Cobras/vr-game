using System;

namespace VrGame.Domain.Orders
{
    public readonly struct OrderId : IEquatable<OrderId>, IComparable<OrderId>
    {
        public OrderId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Order ID не может быть пустым.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public int CompareTo(OrderId other) => Value.CompareTo(other.Value);
        public bool Equals(OrderId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is OrderId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("D");
        public static bool operator ==(OrderId left, OrderId right) => left.Equals(right);
        public static bool operator !=(OrderId left, OrderId right) => !left.Equals(right);
    }
}
