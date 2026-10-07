using System;
using System.Collections.Generic;

namespace VrGame.Presentation.Indicators
{
    public interface IIndicatorProjectionSource
    {
        event Action<IndicatorProjection> ProjectionChanged;

        IReadOnlyList<IndicatorProjection> GetSnapshot();
    }
}
