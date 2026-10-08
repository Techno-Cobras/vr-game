using System;

namespace VrGame.Domain.Plants
{
    public readonly struct PlantGrowthModifier : IEquatable<PlantGrowthModifier>
    {
        private readonly float multiplier;

        public PlantGrowthModifier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    "Множитель роста должен быть конечным положительным числом.");

            this.multiplier = multiplier;
            IsApplied = true;
        }

        public static PlantGrowthModifier None => default;

        public bool IsApplied { get; }

        public float EffectiveMultiplier => IsApplied ? multiplier : 1f;

        internal bool IsValid =>
            IsApplied &&
            !float.IsNaN(multiplier) &&
            !float.IsInfinity(multiplier) &&
            multiplier > 0f;

        public bool Equals(PlantGrowthModifier other) =>
            IsApplied == other.IsApplied && multiplier.Equals(other.multiplier);

        public override bool Equals(object obj) => obj is PlantGrowthModifier other && Equals(other);
        public override int GetHashCode() => (multiplier.GetHashCode() * 397) ^ IsApplied.GetHashCode();
        public override string ToString() => IsApplied ? $"x{multiplier}" : "None";
        public static bool operator ==(PlantGrowthModifier left, PlantGrowthModifier right) => left.Equals(right);
        public static bool operator !=(PlantGrowthModifier left, PlantGrowthModifier right) => !left.Equals(right);
    }
}
