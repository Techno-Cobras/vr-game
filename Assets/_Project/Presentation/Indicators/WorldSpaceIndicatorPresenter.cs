using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VrGame.Presentation.Indicators
{
    public sealed class WorldSpaceIndicatorPresenter : MonoBehaviour
    {
        [SerializeField]
        private WorldSpaceIndicatorCatalog catalog;

        [SerializeField]
        private Transform viewer;

        [SerializeField, Min(0)]
        private int prewarmPerVariant = 1;

        private readonly Dictionary<IndicatorKey, Binding> bindings = new Dictionary<IndicatorKey, Binding>();
        private readonly Dictionary<IndicatorVariant, Stack<WorldSpaceIndicatorView>> pools =
            new Dictionary<IndicatorVariant, Stack<WorldSpaceIndicatorView>>();
        private IIndicatorProjectionSource projectionSource;
        private bool sourceSubscribed;
        private bool initialized;

        public int ActiveCount => bindings.Count;

        public void Observe(IIndicatorProjectionSource source)
        {
            if (ReferenceEquals(projectionSource, source))
            {
                if (!sourceSubscribed && isActiveAndEnabled)
                    SubscribeSource();
                return;
            }

            UnsubscribeSource();
            HideAll();
            projectionSource = source;
            if (isActiveAndEnabled)
                SubscribeSource();
        }

        public bool Show(IndicatorKey key, IndicatorVariant variant, IndicatorTarget target)
        {
            EnsureInitialized();
            ValidateKey(key);
            if (target == null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy)
            {
                Hide(key);
                return false;
            }

            var definition = catalog.GetRequired(variant);

            if (bindings.TryGetValue(key, out var existing))
            {
                if (existing.Variant != variant)
                {
                    RemoveBinding(existing);
                }
                else
                {
                    BindTarget(existing, target);
                    existing.View.Bind(target, existing.Definition.LocalOffset, viewer);
                    return true;
                }
            }

            var binding = new Binding(key, variant, definition, Acquire(definition));
            binding.TargetUnavailable = _ => Hide(key);
            binding.ViewUnavailable = _ => Hide(key);
            binding.View.TargetUnavailable += binding.ViewUnavailable;
            bindings.Add(key, binding);
            BindTarget(binding, target);
            binding.View.Bind(target, definition.LocalOffset, viewer);
            return true;
        }

        public bool Rebind(IndicatorKey key, IndicatorTarget target)
        {
            if (!bindings.TryGetValue(key, out var binding))
                return false;

            return Show(key, binding.Variant, target);
        }

        public void Reconcile(IEnumerable<IndicatorProjection> projections)
        {
            EnsureInitialized();
            if (projections == null)
                throw new ArgumentNullException(nameof(projections));

            var desired = new Dictionary<IndicatorKey, IndicatorProjection>();
            foreach (var projection in projections)
            {
                ValidateKey(projection.Key);
                if (!desired.TryAdd(projection.Key, projection))
                    throw new ArgumentException($"Indicator key '{projection.Key}' встречается больше одного раза.", nameof(projections));
            }

            var visible = desired
                .Where(pair => pair.Value.Visible &&
                               pair.Value.Target != null &&
                               pair.Value.Target.isActiveAndEnabled &&
                               pair.Value.Target.gameObject.activeInHierarchy)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            foreach (var projection in visible.Values)
                catalog.GetRequired(projection.Variant);

            foreach (var key in bindings.Keys.Where(key => !visible.ContainsKey(key)).ToArray())
                Hide(key);

            foreach (var projection in visible.Values)
                Show(projection.Key, projection.Variant, projection.Target);
        }

        public void Hide(IndicatorKey key)
        {
            if (bindings.TryGetValue(key, out var binding))
                RemoveBinding(binding);
        }

        public void HideAll()
        {
            foreach (var binding in bindings.Values.ToArray())
                RemoveBinding(binding);
        }

        public void SetViewer(Transform viewerTransform)
        {
            viewer = viewerTransform;
            foreach (var binding in bindings.Values)
                binding.View.SetViewer(viewer);
        }

        public bool TryGetActiveView(IndicatorKey key, out WorldSpaceIndicatorView view)
        {
            if (bindings.TryGetValue(key, out var binding))
            {
                view = binding.View;
                return true;
            }

            view = null;
            return false;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            SubscribeSource();
        }

        private void OnDisable()
        {
            UnsubscribeSource();
            HideAll();
        }

        private void SubscribeSource()
        {
            if (sourceSubscribed || projectionSource == null)
                return;

            projectionSource.ProjectionChanged += HandleProjectionChanged;
            sourceSubscribed = true;
            try
            {
                Reconcile(projectionSource.GetSnapshot());
            }
            catch
            {
                UnsubscribeSource();
                throw;
            }
        }

        private void UnsubscribeSource()
        {
            if (!sourceSubscribed)
                return;

            projectionSource.ProjectionChanged -= HandleProjectionChanged;
            sourceSubscribed = false;
        }

        private void HandleProjectionChanged(IndicatorProjection projection)
        {
            if (projection.Visible)
                Show(projection.Key, projection.Variant, projection.Target);
            else
                Hide(projection.Key);
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;
            if (catalog == null)
                throw new InvalidOperationException("World-space indicator catalog не назначен.");

            catalog.ValidateOrThrow();
            foreach (IndicatorVariant variant in Enum.GetValues(typeof(IndicatorVariant)))
            {
                var definition = catalog.GetRequired(variant);
                var pool = new Stack<WorldSpaceIndicatorView>();
                pools.Add(variant, pool);
                for (var index = 0; index < prewarmPerVariant; index++)
                {
                    var view = Instantiate(definition.Prefab, transform);
                    view.Release(transform);
                    pool.Push(view);
                }
            }

            initialized = true;
        }

        private WorldSpaceIndicatorView Acquire(IndicatorVariantDefinition definition)
        {
            var pool = pools[definition.Variant];
            return pool.Count > 0 ? pool.Pop() : Instantiate(definition.Prefab, transform);
        }

        private void BindTarget(Binding binding, IndicatorTarget target)
        {
            if (binding.Target == target)
                return;
            if (binding.Target != null)
                binding.Target.Unavailable -= binding.TargetUnavailable;

            binding.Target = target;
            binding.Target.Unavailable += binding.TargetUnavailable;
        }

        private void RemoveBinding(Binding binding)
        {
            if (binding.Target != null)
                binding.Target.Unavailable -= binding.TargetUnavailable;
            binding.View.TargetUnavailable -= binding.ViewUnavailable;

            bindings.Remove(binding.Key);
            binding.Target = null;
            binding.View.Release(transform);
            pools[binding.Variant].Push(binding.View);
        }

        private static void ValidateKey(IndicatorKey key)
        {
            if (string.IsNullOrEmpty(key.Value))
                throw new ArgumentException("Indicator key не может быть пустым.", nameof(key));
        }

        private sealed class Binding
        {
            public Binding(
                IndicatorKey key,
                IndicatorVariant variant,
                IndicatorVariantDefinition definition,
                WorldSpaceIndicatorView view)
            {
                Key = key;
                Variant = variant;
                Definition = definition;
                View = view;
            }

            public IndicatorKey Key { get; }

            public IndicatorVariant Variant { get; }

            public IndicatorVariantDefinition Definition { get; }

            public WorldSpaceIndicatorView View { get; }

            public IndicatorTarget Target { get; set; }

            public Action<IndicatorTarget> TargetUnavailable { get; set; }

            public Action<WorldSpaceIndicatorView> ViewUnavailable { get; set; }
        }
    }
}
