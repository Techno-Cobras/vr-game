using System;
using System.Collections.Generic;
using VrGame.Presentation.Indicators;

namespace VrGame.Bootstrap
{
    public sealed class CompositeIndicatorProjectionSource : IIndicatorProjectionSource, IDisposable
    {
        private readonly IIndicatorProjectionSource[] sources;
        private bool disposed;

        public CompositeIndicatorProjectionSource(params IIndicatorProjectionSource[] sources)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index] == null)
                    throw new ArgumentException("Composite indicator source содержит пустой source.", nameof(sources));
                sources[index].ProjectionChanged += Forward;
            }
        }

        public event Action<IndicatorProjection> ProjectionChanged;

        public IReadOnlyList<IndicatorProjection> GetSnapshot()
        {
            var merged = new Dictionary<IndicatorKey, IndicatorProjection>();
            for (var index = 0; index < sources.Length; index++)
            {
                var snapshot = sources[index].GetSnapshot();
                for (var projectionIndex = 0; projectionIndex < snapshot.Count; projectionIndex++)
                    merged[snapshot[projectionIndex].Key] = snapshot[projectionIndex];
            }
            return new List<IndicatorProjection>(merged.Values);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            for (var index = 0; index < sources.Length; index++)
                sources[index].ProjectionChanged -= Forward;
            ProjectionChanged = null;
        }

        private void Forward(IndicatorProjection projection)
        {
            if (disposed)
                return;
            var handlers = ProjectionChanged?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<IndicatorProjection> handler in handlers)
            {
                try { handler(projection); }
                catch (Exception) { }
            }
        }
    }
}
