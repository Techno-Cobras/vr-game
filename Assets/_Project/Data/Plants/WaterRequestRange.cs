using System;

namespace VrGame.Data.Plants
{
    public readonly struct WaterRequestRange : IEquatable<WaterRequestRange>
    {
        public WaterRequestRange(float minNormalized, float maxNormalized)
        {
            if (!IsValidValues(minNormalized, maxNormalized))
                throw new PlantDataValidationException(
                    PlantDataValidationError.InvalidWaterRequestRange,
                    "Диапазон запроса воды должен быть конечным и удовлетворять 0 < min <= max < 1.");

            MinNormalized = minNormalized;
            MaxNormalized = maxNormalized;
        }

        public static WaterRequestRange Default => new WaterRequestRange(.3f, .7f);
        public float MinNormalized { get; }
        public float MaxNormalized { get; }
        public bool IsValid => IsValidValues(MinNormalized, MaxNormalized);

        public float Select(float unitSample)
        {
            if (float.IsNaN(unitSample) || float.IsInfinity(unitSample) || unitSample < 0f || unitSample > 1f)
                throw new InvalidOperationException("Источник случайности роста должен вернуть конечное число в диапазоне [0, 1].");

            return MinNormalized + ((MaxNormalized - MinNormalized) * unitSample);
        }

        public bool Equals(WaterRequestRange other) =>
            MinNormalized.Equals(other.MinNormalized) && MaxNormalized.Equals(other.MaxNormalized);
        public override bool Equals(object obj) => obj is WaterRequestRange other && Equals(other);
        public override int GetHashCode() => (MinNormalized.GetHashCode() * 397) ^ MaxNormalized.GetHashCode();

        private static bool IsValidValues(float minNormalized, float maxNormalized) =>
            !float.IsNaN(minNormalized) && !float.IsInfinity(minNormalized) &&
            !float.IsNaN(maxNormalized) && !float.IsInfinity(maxNormalized) &&
            minNormalized > 0f && minNormalized <= maxNormalized && maxNormalized < 1f;
    }
}
