using System;
using VrGame.Data.Items;

namespace VrGame.Data.Plants
{
    public readonly struct PlantTypeId : IEquatable<PlantTypeId>, IComparable<PlantTypeId>
    {
        private const string Prefix = "plant.";
        private readonly string value;

        public PlantTypeId(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new PlantDataValidationException(
                    PlantDataValidationError.InvalidPlantTypeId,
                    $"Plant type ID должен быть lowercase ASCII namespaced-значением с префиксом '{Prefix}'.");

            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public static bool TryParse(string value, out PlantTypeId id)
        {
            if (!StableId.IsValid(value, Prefix))
            {
                id = default;
                return false;
            }

            id = new PlantTypeId(value);
            return true;
        }

        public int CompareTo(PlantTypeId other) => StringComparer.Ordinal.Compare(Value, other.Value);
        public bool Equals(PlantTypeId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is PlantTypeId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(PlantTypeId left, PlantTypeId right) => left.Equals(right);
        public static bool operator !=(PlantTypeId left, PlantTypeId right) => !left.Equals(right);
    }
}
