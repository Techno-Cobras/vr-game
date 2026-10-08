using System;
using VrGame.Data.Items;

namespace VrGame.Data.Plants
{
    public readonly struct PlantRepresentationKey : IEquatable<PlantRepresentationKey>
    {
        private const string Prefix = "representation.plant.";
        private readonly string value;

        public PlantRepresentationKey(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new PlantDataValidationException(
                    PlantDataValidationError.InvalidRepresentationKey,
                    $"Plant representation key должен иметь префикс '{Prefix}'.");

            this.value = value;
        }

        public string Value => value ?? string.Empty;
        public bool Equals(PlantRepresentationKey other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is PlantRepresentationKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(PlantRepresentationKey left, PlantRepresentationKey right) => left.Equals(right);
        public static bool operator !=(PlantRepresentationKey left, PlantRepresentationKey right) => !left.Equals(right);
    }
}
