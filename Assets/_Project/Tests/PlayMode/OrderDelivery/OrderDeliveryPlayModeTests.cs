using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;
using VrGame.Domain.Inventory;
using VrGame.Domain.Orders;
using VrGame.VRInteraction.Orders;

namespace VrGame.Tests.PlayMode.OrderDelivery
{
    public sealed class OrderDeliveryPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_DeliveryPointConsumesCompletesPaysAndHidesWaypointOnce()
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

            var available = composition.Orders.GetSnapshot().Orders.First(order => order.Quantity > 1);
            var wrongMedicine = composition.Orders.GetSnapshot().Orders
                .First(order => order.MedicineItemId != available.MedicineItemId)
                .MedicineItemId;
            var accepted = composition.Orders.Accept(
                available.OrderId,
                new OrderContext("playmode accept", Guid.NewGuid(), Guid.NewGuid()));
            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(composition.PlayerInventory.Add(
                wrongMedicine,
                available.Quantity,
                new InventoryChangeContext("playmode wrong medicine", Guid.NewGuid(), Guid.NewGuid())).Succeeded,
                Is.True);
            var balanceBefore = composition.Economy.Balance;
            var orderVersionBefore = composition.Orders.Version;
            var wrong = controllers[0].TryDeliver();

            Assert.That(wrong.Rejection, Is.EqualTo(OrderDeliveryRejection.InventoryRejected));
            Assert.That(composition.PlayerInventory.GetQuantity(wrongMedicine), Is.EqualTo(available.Quantity));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersionBefore));
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(available.OrderId));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.True);

            controllers[0].enabled = false;
            controllers[0].enabled = true;
            Assert.That(composition.PlayerInventory.Add(
                available.MedicineItemId,
                available.Quantity - 1,
                new InventoryChangeContext("playmode partial medicine", Guid.NewGuid(), Guid.NewGuid())).Succeeded,
                Is.True);
            var partial = controllers[0].TryDeliver();

            Assert.That(partial.Rejection, Is.EqualTo(OrderDeliveryRejection.InventoryRejected));
            Assert.That(composition.PlayerInventory.GetQuantity(available.MedicineItemId),
                Is.EqualTo(available.Quantity - 1));
            Assert.That(composition.Orders.Version, Is.EqualTo(orderVersionBefore));
            Assert.That(composition.Orders.ActiveOrderId, Is.EqualTo(available.OrderId));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.True);

            controllers[0].enabled = false;
            controllers[0].enabled = true;
            Assert.That(composition.PlayerInventory.Add(
                available.MedicineItemId,
                2,
                new InventoryChangeContext("playmode complete medicine", Guid.NewGuid(), Guid.NewGuid())).Succeeded,
                Is.True);

            var delivered = controllers[0].TryDeliver();
            var repeated = controllers[0].TryDeliver();

            Assert.That(delivered, Is.Not.Null);
            Assert.That(delivered.Succeeded, Is.True);
            Assert.That(repeated, Is.Not.Null);
            Assert.That(composition.PlayerInventory.GetQuantity(available.MedicineItemId), Is.EqualTo(1));
            Assert.That(composition.Orders.ActiveOrderId, Is.Null);
            Assert.That(composition.Orders.GetSnapshot().Orders.Single(order => order.OrderId == available.OrderId).State,
                Is.EqualTo(OrderState.Completed));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore + available.Reward));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));
            Assert.That(composition.DeliveryIndicatorSource.GetSnapshot().Single().Visible, Is.False);

            yield return null;
        }
    }
}
