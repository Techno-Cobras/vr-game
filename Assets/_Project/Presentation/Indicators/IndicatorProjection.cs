namespace VrGame.Presentation.Indicators
{
    public readonly struct IndicatorProjection
    {
        public IndicatorProjection(
            IndicatorKey key,
            IndicatorVariant variant,
            IndicatorTarget target,
            bool visible)
        {
            Key = key;
            Variant = variant;
            Target = target;
            Visible = visible;
        }

        public IndicatorKey Key { get; }

        public IndicatorVariant Variant { get; }

        public IndicatorTarget Target { get; }

        public bool Visible { get; }
    }
}
