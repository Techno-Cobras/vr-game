using System;

namespace VrGame.Presentation.Indicators
{
    public readonly struct IndicatorKey : IEquatable<IndicatorKey>
    {
        private readonly string value;

        public IndicatorKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Indicator key не может быть пустым.", nameof(value));

            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public bool Equals(IndicatorKey other) => StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is IndicatorKey other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(IndicatorKey left, IndicatorKey right) => left.Equals(right);

        public static bool operator !=(IndicatorKey left, IndicatorKey right) => !left.Equals(right);
    }
}
