using System;

namespace VrGame.Data.Plants
{
    public readonly struct GrowthDuration : IEquatable<GrowthDuration>
    {
        public GrowthDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new PlantDataValidationException(
                    PlantDataValidationError.InvalidGrowthDuration,
                    "Длительность роста должна быть конечным положительным числом секунд.");

            Seconds = seconds;
        }

        public float Seconds { get; }
        public bool Equals(GrowthDuration other) => Seconds.Equals(other.Seconds);
        public override bool Equals(object obj) => obj is GrowthDuration other && Equals(other);
        public override int GetHashCode() => Seconds.GetHashCode();
        public override string ToString() => $"{Seconds} s";
    }
}
