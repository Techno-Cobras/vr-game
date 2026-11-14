using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using VrGame.Domain.Orders;
using VrGame.Presentation.Indicators;
using VrGame.Presentation.Orders;
using VrGame.VRInteraction.Orders;

namespace VrGame.Tests.PlayMode.OrderDelivery
{
    public sealed class OrderDeliveryPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScenarioC_FromBoardAcceptanceToSinglePayoutAndReplay()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var controllers = UnityEngine.Object.FindObjectsByType<OrderDeliveryInteractionController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.That(composition, Is.Not.Null);
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(composition.DeliveryController, Is.SameAs(controllers[0]));
            Assert.That(controllers[0].Interactable, Is.Not.Null);
            Assert.That(controllers[0].GetComponent<XRSimpleInteractable>(), Is.SameAs(controllers[0].Interactable));
            Assert.That(composition.OrderDelivery, Is.Not.Null);
            Assert.That(composition.Economy, Is.Not.Null);
            Assert.That(composition.Economy.Balance, Is.EqualTo(100));

            var orderCount = composition.OrderBoardController.Snapshot.Orders.Count;
            for (var index = 0;
                 index < orderCount && composition.OrderBoardController.Snapshot.SelectedOrder.Quantity <= 1;
                 index++)
                composition.OrderBoardController.SelectNextOrder();
            var selected = composition.OrderBoardController.Snapshot.SelectedOrder;
            Assert.That(selected.Quantity, Is.EqualTo(2));
            Assert.That(selected.Reward, Is.EqualTo(60));

            var accepted = composition.OrderBoardController.AcceptSelected();
            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(selected.OrderId));
            Assert.That(composition.OrderBoardController.Snapshot.ActiveOrder.OrderId, Is.EqualTo(selected.OrderId));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.True);
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.EqualTo(1));
            Assert.That(composition.IndicatorPresenter.TryGetActiveView(
                new IndicatorKey("order.active.delivery"), out var waypoint), Is.True);
            Assert.That(waypoint, Is.Not.Null);

            composition.OrderBoardController.SelectNextOrder();
            var orderVersionBeforeBlockedAcceptance = composition.Orders.Version;
            Assert.That(composition.OrderBoardController.AcceptSelected(), Is.Null);
            Assert.That(composition.OrderBoardController.Snapshot.Feedback,
                Is.EqualTo(OrderBoardFeedback.ActiveOrderExists));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersionBeforeBlockedAcceptance));
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(selected.OrderId));

            var wrongMedicine = composition.Orders.GetSnapshot().Orders
                .First(order => order.MedicineItemId != selected.MedicineItemId)
                .MedicineItemId;
            Assert.That(composition.PlayerInventory.Add(
                wrongMedicine,
                selected.Quantity,
                new InventoryChangeContext("playmode wrong medicine", Guid.NewGuid(), Guid.NewGuid())).Succeeded,
                Is.True);
            Assert.That(composition.PlayerInventory.Add(
                selected.MedicineItemId,
                selected.Quantity,
                new InventoryChangeContext("playmode required medicine", Guid.NewGuid(), Guid.NewGuid())).Succeeded,
                Is.True);
            var inventoryVersionBeforeInvalid = composition.PlayerInventory.Version;
            var orderVersionBeforeInvalid = composition.Orders.Version;
            var economyVersionBeforeInvalid = composition.Economy.Version;
            var balanceBefore = composition.Economy.Balance;
            var ledgerCountBefore = composition.Economy.GetLedgerSnapshot().Count;

            var wrong = composition.OrderDelivery.Deliver(
                wrongMedicine,
                selected.Quantity,
                DeliveryContext("неверное лекарство Scenario C"));
            Assert.That(wrong.Rejection, Is.EqualTo(OrderDeliveryRejection.WrongMedicine));
            AssertInvalidSubmissionIsExactNoOp(
                composition,
                selected.MedicineItemId,
                wrongMedicine,
                selected.Quantity,
                inventoryVersionBeforeInvalid,
                orderVersionBeforeInvalid,
                economyVersionBeforeInvalid,
                balanceBefore,
                ledgerCountBefore,
                selected.OrderId);

            var partial = composition.OrderDelivery.Deliver(
                selected.MedicineItemId,
                selected.Quantity - 1,
                DeliveryContext("неполное количество Scenario C"));
            Assert.That(partial.Rejection, Is.EqualTo(OrderDeliveryRejection.WrongQuantity));
            AssertInvalidSubmissionIsExactNoOp(
                composition,
                selected.MedicineItemId,
                wrongMedicine,
                selected.Quantity,
                inventoryVersionBeforeInvalid,
                orderVersionBeforeInvalid,
                economyVersionBeforeInvalid,
                balanceBefore,
                ledgerCountBefore,
                selected.OrderId);

            var delivered = controllers[0].TryDeliver();

            Assert.That(delivered.Succeeded, Is.True);
            Assert.That(delivered.IsReplay, Is.False);
            Assert.That(composition.PlayerInventory.GetQuantity(selected.MedicineItemId), Is.Zero);
            Assert.That(composition.PlayerInventory.GetQuantity(wrongMedicine), Is.EqualTo(selected.Quantity));
            Assert.That(composition.Orders.ActiveOrderId, Is.Null);
            Assert.That(composition.Orders.GetSnapshot().Orders.Single(order => order.OrderId == selected.OrderId).State,
                Is.EqualTo(OrderState.Completed));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore + selected.Reward));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.False);
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.Zero);

            var inventoryVersionAfterDelivery = composition.PlayerInventory.Version;
            var orderVersionAfterDelivery = composition.Orders.Version;
            var economyVersionAfterDelivery = composition.Economy.Version;
            var repeated = controllers[0].TryDeliver();

            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.IsReplay, Is.True);
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(inventoryVersionAfterDelivery));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersionAfterDelivery));
            Assert.That(composition.Economy.Version, Is.EqualTo(economyVersionAfterDelivery));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore + selected.Reward));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));

            controllers[0].enabled = false;
            controllers[0].enabled = true;
            var freshIntent = controllers[0].TryDeliver();
            Assert.That(freshIntent.Rejection, Is.EqualTo(OrderDeliveryRejection.NoActiveOrder));
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(inventoryVersionAfterDelivery));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersionAfterDelivery));
            Assert.That(composition.Economy.Version, Is.EqualTo(economyVersionAfterDelivery));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));

            yield return null;
        }

        private static OrderDeliveryContext DeliveryContext(string reason) =>
            new OrderDeliveryContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static void AssertInvalidSubmissionIsExactNoOp(
            SeedStorageCompositionRoot composition,
            ItemId requiredMedicine,
            ItemId wrongMedicine,
            int quantity,
            long inventoryVersion,
            long orderVersion,
            long economyVersion,
            int balance,
            int ledgerCount,
            OrderId activeOrderId)
        {
            Assert.That(composition.PlayerInventory.GetQuantity(requiredMedicine), Is.EqualTo(quantity));
            Assert.That(composition.PlayerInventory.GetQuantity(wrongMedicine), Is.EqualTo(quantity));
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersion));
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(activeOrderId));
            Assert.That(composition.Economy.Version, Is.EqualTo(economyVersion));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balance));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(ledgerCount));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.True);
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.EqualTo(1));
        }
    }
}
