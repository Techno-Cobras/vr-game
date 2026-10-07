using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Presentation.Indicators;

namespace VrGame.Tests.EditMode.Indicators
{
    public sealed class WorldSpaceIndicatorPresenterTests
    {
        private const string CatalogPath = "Assets/_Project/Presentation/Indicators/Assets/WorldSpaceIndicatorCatalog.asset";
        private const string PresenterPath = "Assets/_Project/Presentation/Indicators/Assets/WorldSpaceIndicatorPresenter.prefab";

        [Test]
        public void Catalog_ConfiguresEveryRequiredVariant()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldSpaceIndicatorCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.ValidateOrThrow);
            foreach (IndicatorVariant variant in Enum.GetValues(typeof(IndicatorVariant)))
            {
                var definition = catalog.GetRequired(variant);
                Assert.That(definition.Prefab, Is.Not.Null);
                Assert.That(definition.LocalOffset.y, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Prefabs_AreReadableAndDoNotInterceptVrInput()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldSpaceIndicatorCatalog>(CatalogPath);
            var labels = new HashSet<string>();
            var colors = new HashSet<Color>();

            foreach (IndicatorVariant variant in Enum.GetValues(typeof(IndicatorVariant)))
            {
                var prefab = catalog.GetRequired(variant).Prefab;
                var canvas = prefab.GetComponent<Canvas>();
                var rect = prefab.GetComponent<RectTransform>();
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(rect.sizeDelta.x * rect.localScale.x, Is.InRange(0.2f, 0.3f));
                Assert.That(prefab.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                labels.Add(prefab.Label);
                colors.Add(prefab.BackgroundColor);
            }

            Assert.That(labels, Has.Count.EqualTo(3));
            Assert.That(colors, Has.Count.EqualTo(3));
        }

        [Test]
        public void ShowAndVariantChange_ReuseSingleProjection()
        {
            var presenter = CreatePresenter();
            var target = new GameObject("Target").AddComponent<IndicatorTarget>();
            var key = new IndicatorKey("plant.slot.1");

            try
            {
                foreach (IndicatorVariant variant in Enum.GetValues(typeof(IndicatorVariant)))
                {
                    Assert.That(presenter.Show(key, variant, target), Is.True);
                    Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                    Assert.That(presenter.TryGetActiveView(key, out var view), Is.True);
                    Assert.That(view.TargetAnchor, Is.EqualTo(target.Anchor));
                    Assert.That(view.transform.parent, Is.EqualTo(presenter.transform));
                    Assert.That(view.gameObject.activeSelf, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Observe_StateEventsControlVisibilityAndDisableStopsObservation()
        {
            var presenter = CreatePresenter();
            var target = new GameObject("Target").AddComponent<IndicatorTarget>();
            var key = new IndicatorKey("plant.slot.events");
            var source = new FakeProjectionSource(
                new IndicatorProjection(key, IndicatorVariant.Water, target, true));

            try
            {
                presenter.Observe(source);
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));

                source.Publish(new IndicatorProjection(key, IndicatorVariant.Water, target, false));
                Assert.That(presenter.ActiveCount, Is.Zero);

                source.Publish(new IndicatorProjection(key, IndicatorVariant.ReadyToHarvest, target, true));
                Assert.That(presenter.TryGetActiveView(key, out var view), Is.True);
                Assert.That(view.Label, Does.Contain("ГОТОВО"));

                InvokeLifecycle(presenter, "OnDisable");
                source.Publish(new IndicatorProjection(key, IndicatorVariant.Delivery, target, true));
                Assert.That(presenter.ActiveCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Rebind_UnsubscribesOldTargetAndCurrentDisableCleansUp()
        {
            var presenter = CreatePresenter();
            var first = new GameObject("First").AddComponent<IndicatorTarget>();
            var second = new GameObject("Second").AddComponent<IndicatorTarget>();
            var key = new IndicatorKey("delivery.active");

            try
            {
                presenter.Show(key, IndicatorVariant.Delivery, first);
                Assert.That(presenter.Rebind(key, second), Is.True);

                InvokeLifecycle(first, "OnDisable");
                first.gameObject.SetActive(false);
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));

                InvokeLifecycle(second, "OnDisable");
                second.gameObject.SetActive(false);
                Assert.That(presenter.ActiveCount, Is.Zero);
                Assert.That(presenter.TryGetActiveView(key, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void Reconcile_AppliesExactSetAndRejectsDuplicatesBeforeMutation()
        {
            var presenter = CreatePresenter();
            var first = new GameObject("First").AddComponent<IndicatorTarget>();
            var second = new GameObject("Second").AddComponent<IndicatorTarget>();
            var firstKey = new IndicatorKey("plant.slot.1");
            var secondKey = new IndicatorKey("delivery.entry.1");

            try
            {
                presenter.Reconcile(new[]
                {
                    new IndicatorProjection(firstKey, IndicatorVariant.Water, first, true),
                    new IndicatorProjection(secondKey, IndicatorVariant.Delivery, second, true)
                });
                Assert.That(presenter.ActiveCount, Is.EqualTo(2));

                Assert.Throws<ArgumentException>(() => presenter.Reconcile(new[]
                {
                    new IndicatorProjection(firstKey, IndicatorVariant.Water, first, true),
                    new IndicatorProjection(firstKey, IndicatorVariant.ReadyToHarvest, second, true)
                }));
                Assert.That(presenter.ActiveCount, Is.EqualTo(2));

                Assert.Throws<InvalidOperationException>(() => presenter.Reconcile(new[]
                {
                    new IndicatorProjection(firstKey, (IndicatorVariant)999, first, true)
                }));
                Assert.That(presenter.ActiveCount, Is.EqualTo(2));

                presenter.Reconcile(new[]
                {
                    new IndicatorProjection(firstKey, IndicatorVariant.ReadyToHarvest, first, true),
                    new IndicatorProjection(secondKey, IndicatorVariant.Delivery, second, false)
                });
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void DisabledTargetAndPresenter_DoNotLeaveOrphanViews()
        {
            var presenter = CreatePresenter();
            var target = new GameObject("Target").AddComponent<IndicatorTarget>();
            var key = new IndicatorKey("plant.slot.1");

            try
            {
                target.gameObject.SetActive(false);
                Assert.That(presenter.Show(key, IndicatorVariant.Water, target), Is.False);
                Assert.That(presenter.ActiveCount, Is.Zero);

                target.gameObject.SetActive(true);
                presenter.Show(key, IndicatorVariant.Water, target);
                InvokeLifecycle(presenter, "OnDisable");
                presenter.gameObject.SetActive(false);
                Assert.That(presenter.ActiveCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }

        private static WorldSpaceIndicatorPresenter CreatePresenter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresenterPath);
            Assert.That(prefab, Is.Not.Null);
            return UnityEngine.Object.Instantiate(prefab).GetComponent<WorldSpaceIndicatorPresenter>();
        }

        private static void InvokeLifecycle(MonoBehaviour behaviour, string methodName)
        {
            var method = behaviour.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(behaviour, null);
        }

        private sealed class FakeProjectionSource : IIndicatorProjectionSource
        {
            private readonly IReadOnlyList<IndicatorProjection> snapshot;

            public FakeProjectionSource(params IndicatorProjection[] snapshot)
            {
                this.snapshot = snapshot;
            }

            public event Action<IndicatorProjection> ProjectionChanged;

            public IReadOnlyList<IndicatorProjection> GetSnapshot() => snapshot;

            public void Publish(IndicatorProjection projection) => ProjectionChanged?.Invoke(projection);
        }
    }
}
