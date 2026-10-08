using System;

namespace VrGame.Domain.Plants
{
    public readonly struct PlantingSlotId : IEquatable<PlantingSlotId>
    {
        public PlantingSlotId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Planting slot ID не может быть пустым.", nameof(value));

            Value = value;
        }

        public Guid Value { get; }

        public bool Equals(PlantingSlotId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is PlantingSlotId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(PlantingSlotId left, PlantingSlotId right) => left.Equals(right);
        public static bool operator !=(PlantingSlotId left, PlantingSlotId right) => !left.Equals(right);
    }
}
