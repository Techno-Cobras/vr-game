using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Bootstrap;
using VrGame.Domain.Orders;
using VrGame.Presentation.Orders;

namespace VrGame.Tests.PlayMode.OrdersBoard
{
    public sealed class OrderBoardPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_WiresOrderBoardInitialOrdersAndDeliveryWaypoint()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var boards = UnityEngine.Object.FindObjectsByType<OrderBoardController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var deliverySources = UnityEngine.Object.FindObjectsByType<OrderDeliveryIndicatorProjectionSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.That(composition, Is.Not.Null);
            Assert.That(boards, Has.Length.EqualTo(1));
            Assert.That(deliverySources, Has.Length.EqualTo(1));
            Assert.That(composition.OrderBoardController, Is.SameAs(boards[0]));
            Assert.That(composition.DeliveryIndicatorSource, Is.SameAs(deliverySources[0]));
            Assert.That(composition.Orders, Is.Not.Null);
            Assert.That(composition.OrderTemplates.Definitions, Has.Count.EqualTo(2));
            Assert.That(composition.Orders.GetSnapshot().Orders, Has.Count.EqualTo(2));
            Assert.That(composition.Orders.GetSnapshot().Orders.All(value => value.State == OrderState.Available), Is.True);

            var canvas = boards[0].GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            var buttons = boards[0].GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(3));
            foreach (var button in buttons)
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));

            var version = composition.Orders.Version;
            var accepted = boards[0].AcceptSelected();
            var duplicate = boards[0].AcceptSelected();

            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(duplicate, Is.SameAs(accepted));
            Assert.That(composition.Orders.Version, Is.EqualTo(version + 1));
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(accepted.TargetOrderId));
            Assert.That(boards[0].Snapshot.ActiveOrder, Is.Not.Null);
            Assert.That(deliverySources[0].GetSnapshot().Single().Visible, Is.True);

            boards[0].SelectNextOrder();
            var blockedVersion = composition.Orders.Version;
            Assert.That(boards[0].AcceptSelected(), Is.Null);
            Assert.That(composition.Orders.Version, Is.EqualTo(blockedVersion));
            Assert.That(boards[0].Snapshot.Feedback, Is.EqualTo(OrderBoardFeedback.ActiveOrderExists));

            yield return null;
        }
    }
}
