using System;
using VrGame.Data.Items;

namespace VrGame.Data.Orders
{
    public readonly struct OrderTemplateId : IEquatable<OrderTemplateId>, IComparable<OrderTemplateId>
    {
        private const string Prefix = "order-template.";
        private readonly string value;

        public OrderTemplateId(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidTemplateId,
                    $"Order template ID должен быть lowercase ASCII namespaced-значением с префиксом '{Prefix}'.");
            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public static bool TryParse(string value, out OrderTemplateId id)
        {
            if (!StableId.IsValid(value, Prefix))
            {
                id = default;
                return false;
            }
            id = new OrderTemplateId(value);
            return true;
        }

        public int CompareTo(OrderTemplateId other) => StringComparer.Ordinal.Compare(Value, other.Value);
        public bool Equals(OrderTemplateId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is OrderTemplateId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(OrderTemplateId left, OrderTemplateId right) => left.Equals(right);
        public static bool operator !=(OrderTemplateId left, OrderTemplateId right) => !left.Equals(right);
    }
}
