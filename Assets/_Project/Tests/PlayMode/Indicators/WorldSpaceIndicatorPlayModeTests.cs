using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VrGame.Presentation.Indicators;

namespace VrGame.Tests.PlayMode.Indicators
{
    public sealed class WorldSpaceIndicatorPlayModeTests
    {
        private const string PresenterPath = "Assets/_Project/Presentation/Indicators/Assets/WorldSpaceIndicatorPresenter.prefab";

        [UnityTest]
        public IEnumerator TargetDisable_RemovesProjectionAndBillboardUsesInjectedViewer()
        {
            var prefab = Resources.Load<GameObject>("__not_used__");
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PresenterPath);
#endif
            Assert.That(prefab, Is.Not.Null);
            var presenter = Object.Instantiate(prefab).GetComponent<WorldSpaceIndicatorPresenter>();
            var target = new GameObject("Target").AddComponent<IndicatorTarget>();
            var viewer = new GameObject("Viewer");
            viewer.transform.position = new Vector3(1f, 1f, -2f);
            var key = new IndicatorKey("plant.slot.playmode");

            presenter.SetViewer(viewer.transform);
            presenter.Show(key, IndicatorVariant.Water, target);
            yield return null;

            Assert.That(presenter.TryGetActiveView(key, out var view), Is.True);
            var expectedForward = (view.transform.position - viewer.transform.position).normalized;
            Assert.That(Vector3.Dot(view.transform.forward, expectedForward), Is.GreaterThan(0.999f));

            target.gameObject.SetActive(false);
            yield return null;
            Assert.That(presenter.ActiveCount, Is.Zero);

            Object.Destroy(presenter.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(viewer);
        }

        [UnityTest]
        public IEnumerator SeparateAnchorDisable_RemovesProjection()
        {
            var prefab = Resources.Load<GameObject>("__not_used__");
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PresenterPath);
#endif
            Assert.That(prefab, Is.Not.Null);
            var presenter = Object.Instantiate(prefab).GetComponent<WorldSpaceIndicatorPresenter>();
            var target = new GameObject("Target").AddComponent<IndicatorTarget>();
            var anchor = new GameObject("Anchor");
            anchor.transform.SetParent(target.transform, false);
            var anchorField = typeof(IndicatorTarget).GetField(
                "anchor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(anchorField, Is.Not.Null);
            anchorField.SetValue(target, anchor.transform);
            var key = new IndicatorKey("plant.slot.anchor");

            presenter.Show(key, IndicatorVariant.ReadyToHarvest, target);
            Assert.That(presenter.ActiveCount, Is.EqualTo(1));
            Assert.That(presenter.TryGetActiveView(key, out var view), Is.True);
            Assert.That(view.TargetAnchor, Is.EqualTo(anchor.transform));

            anchor.SetActive(false);
            view.SendMessage("LateUpdate");
            Assert.That(presenter.ActiveCount, Is.Zero);

            yield return null;

            Object.Destroy(presenter.gameObject);
            Object.Destroy(target.gameObject);
        }
    }
}
