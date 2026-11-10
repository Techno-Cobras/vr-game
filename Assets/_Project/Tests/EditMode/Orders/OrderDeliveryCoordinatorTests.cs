using System;
using System.Collections.Generic;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Orders;
using VrGame.Domain.Economy;
using VrGame.Domain.Inventory;
using VrGame.Domain.Orders;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Orders
{
    public sealed class OrderDeliveryCoordinatorTests
    {
        private static readonly ItemId RequiredMedicine = new ItemId("item.medicine.required");
        private static readonly ItemId WrongMedicine = new ItemId("item.medicine.wrong");

        [Test]
        public void WrongPartialExcessAndInsufficientSubmissions_DoNotMutateAnyAggregate()
        {
            var fixture = CreateFixture();
            fixture.Stock(RequiredMedicine, 1);
            fixture.Stock(WrongMedicine, 5);
            var inventoryVersion = fixture.Inventory.Version;
            var orderVersion = fixture.Orders.Version;

            var wrong = fixture.Coordinator.Deliver(WrongMedicine, 2, Context("wrong"));
            var partial = fixture.Coordinator.Deliver(RequiredMedicine, 1, Context("partial"));
            var excess = fixture.Coordinator.Deliver(RequiredMedicine, 3, Context("excess"));
            var insufficient = fixture.Coordinator.Deliver(RequiredMedicine, 2, Context("insufficient"));

            Assert.That(wrong.Rejection, Is.EqualTo(OrderDeliveryRejection.WrongMedicine));
            Assert.That(partial.Rejection, Is.EqualTo(OrderDeliveryRejection.WrongQuantity));
            Assert.That(excess.Rejection, Is.EqualTo(OrderDeliveryRejection.WrongQuantity));
            Assert.That(insufficient.Rejection, Is.EqualTo(OrderDeliveryRejection.InventoryRejected));
            Assert.That(insufficient.InventoryRejection, Is.EqualTo(InventoryRejection.InsufficientStock));
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.EqualTo(1));
            Assert.That(fixture.Inventory.GetQuantity(WrongMedicine), Is.EqualTo(5));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(fixture.Orders.Version, Is.EqualTo(orderVersion));
            Assert.That(fixture.Orders.ActiveOrderId, Is.EqualTo(fixture.OrderId));
            Assert.That(fixture.Economy.Balance, Is.Zero);
            Assert.That(fixture.Economy.Version, Is.Zero);
        }

        [Test]
        public void ValidDelivery_CommitsExactQuantityCompletionAndRewardOnce()
        {
            var fixture = CreateFixture();
            fixture.Stock(RequiredMedicine, 5);
            var eventsBefore = fixture.OrderSink.Events.Count;
            var context = Context("valid");

            var result = fixture.Coordinator.Deliver(RequiredMedicine, 2, context);
            var replay = fixture.Coordinator.Deliver(RequiredMedicine, 2, context);
            var repeated = fixture.Coordinator.Deliver(RequiredMedicine, 2, Context("new repeat"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.InventoryChange, Is.Not.Null);
            Assert.That(result.OrderChange.After.State, Is.EqualTo(OrderState.Completed));
            Assert.That(result.BalanceChange.Delta, Is.EqualTo(60));
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.EqualTo(3));
            Assert.That(fixture.Orders.ActiveOrderId, Is.Null);
            Assert.That(fixture.Economy.Balance, Is.EqualTo(60));
            Assert.That(fixture.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(repeated.Rejection, Is.EqualTo(OrderDeliveryRejection.NoActiveOrder));
            Assert.That(fixture.InventorySink.Events, Has.Count.EqualTo(2));
            Assert.That(fixture.OrderSink.Events, Has.Count.EqualTo(eventsBefore + 1));
            Assert.That(fixture.EconomySink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void SameCommandWithDifferentPayload_IsConflictWithoutMutation()
        {
            var fixture = CreateFixture();
            fixture.Stock(RequiredMedicine, 2);
            var context = Context("command");
            Assert.That(fixture.Coordinator.Deliver(WrongMedicine, 2, context).Rejection,
                Is.EqualTo(OrderDeliveryRejection.WrongMedicine));

            var conflict = fixture.Coordinator.Deliver(RequiredMedicine, 2, context);

            Assert.That(conflict.Rejection, Is.EqualTo(OrderDeliveryRejection.CommandConflict));
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.EqualTo(2));
            Assert.That(fixture.Orders.ActiveOrderId, Is.EqualTo(fixture.OrderId));
            Assert.That(fixture.Economy.Balance, Is.Zero);
        }

        [Test]
        public void EconomyOverflow_RejectsBeforeAnyCommit()
        {
            var fixture = CreateFixture(int.MaxValue);
            fixture.Stock(RequiredMedicine, 2);
            var inventoryVersion = fixture.Inventory.Version;
            var orderVersion = fixture.Orders.Version;

            var result = fixture.Coordinator.Deliver(RequiredMedicine, 2, Context("overflow"));

            Assert.That(result.Rejection, Is.EqualTo(OrderDeliveryRejection.EconomyRejected));
            Assert.That(result.EconomyRejection, Is.EqualTo(EconomyRejection.ArithmeticOverflow));
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.EqualTo(2));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(fixture.Orders.Version, Is.EqualTo(orderVersion));
            Assert.That(fixture.Orders.ActiveOrderId, Is.EqualTo(fixture.OrderId));
            Assert.That(fixture.Economy.Balance, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ReentrantDeliveryFromTerminalOrderEvent_IsRejectedAndCannotDuplicatePayout()
        {
            var orderSink = new ReentrantOrderSink();
            var fixture = CreateFixture(orderSink: orderSink);
            fixture.Stock(RequiredMedicine, 2);
            orderSink.Coordinator = fixture.Coordinator;
            orderSink.Medicine = RequiredMedicine;
            orderSink.Context = Context("nested");

            var result = fixture.Coordinator.Deliver(RequiredMedicine, 2, Context("outer"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(orderSink.NestedResult.Rejection, Is.EqualTo(OrderDeliveryRejection.ReentrantCommand));
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.Zero);
            Assert.That(fixture.Economy.Balance, Is.EqualTo(60));
            Assert.That(fixture.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));
        }

        [Test]
        public void ThrowingEventSinks_DoNotRollBackCommittedTransaction()
        {
            var fixture = CreateFixture(
                inventorySink: new ThrowingInventorySink(),
                orderSink: new ThrowingOrderSink(),
                economySink: new ThrowingEconomySink());
            fixture.Stock(RequiredMedicine, 2);

            var result = fixture.Coordinator.Deliver(RequiredMedicine, 2, Context("throwing sinks"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.InventoryEventPublished, Is.False);
            Assert.That(result.OrderEventPublished, Is.False);
            Assert.That(result.EconomyEventPublished, Is.False);
            Assert.That(fixture.Inventory.GetQuantity(RequiredMedicine), Is.Zero);
            Assert.That(fixture.Orders.ActiveOrderId, Is.Null);
            Assert.That(fixture.Economy.Balance, Is.EqualTo(60));
        }

        private static Fixture CreateFixture(
            int startingBalance = 0,
            IInventoryEventSink inventorySink = null,
            IOrderEventSink orderSink = null,
            IEconomyEventSink economySink = null)
        {
            var items = new ItemCatalog(new[]
            {
                Item(RequiredMedicine),
                Item(WrongMedicine)
            });
            var templates = new OrderTemplateCatalog(items, new[]
            {
                new OrderTemplateDefinition(
                    new OrderTemplateId("order-template.delivery"),
                    "Доставка",
                    RequiredMedicine,
                    2,
                    60)
            });
            var recordingInventory = inventorySink as RecordingInventorySink ?? new RecordingInventorySink();
            var recordingOrder = orderSink as RecordingOrderSink ?? new RecordingOrderSink();
            var recordingEconomy = economySink as RecordingEconomySink ?? new RecordingEconomySink();
            var inventory = new InventoryAggregate(
                InventoryId.New(),
                items,
                inventorySink ?? recordingInventory);
            var orders = new OrderService(
                templates,
                new FirstSelector(),
                orderSink ?? recordingOrder);
            var economy = new EconomyService(startingBalance, economySink ?? recordingEconomy);
            var generated = orders.Generate(OrderContext("generate"));
            var orderId = generated.Change.After.OrderId;
            Assert.That(orders.Accept(orderId, OrderContext("accept")).Succeeded, Is.True);
            return new Fixture(
                inventory,
                orders,
                economy,
                new OrderDeliveryCoordinator(inventory, orders, economy),
                orderId,
                recordingInventory,
                recordingOrder,
                recordingEconomy);
        }

        private static ItemDefinition Item(ItemId id) => new ItemDefinition(
            id,
            id.Value,
            ItemCategory.Medicine,
            new ItemRepresentationKey("representation." + id.Value),
            new ItemStackRules(16, false));

        private static OrderDeliveryContext Context(string reason) =>
            new OrderDeliveryContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static OrderContext OrderContext(string reason) =>
            new OrderContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class Fixture
        {
            public Fixture(
                InventoryAggregate inventory,
                OrderService orders,
                EconomyService economy,
                OrderDeliveryCoordinator coordinator,
                OrderId orderId,
                RecordingInventorySink inventorySink,
                RecordingOrderSink orderSink,
                RecordingEconomySink economySink)
            {
                Inventory = inventory;
                Orders = orders;
                Economy = economy;
                Coordinator = coordinator;
                OrderId = orderId;
                InventorySink = inventorySink;
                OrderSink = orderSink;
                EconomySink = economySink;
            }

            public InventoryAggregate Inventory { get; }
            public OrderService Orders { get; }
            public EconomyService Economy { get; }
            public OrderDeliveryCoordinator Coordinator { get; }
            public OrderId OrderId { get; }
            public RecordingInventorySink InventorySink { get; }
            public RecordingOrderSink OrderSink { get; }
            public RecordingEconomySink EconomySink { get; }

            public void Stock(ItemId itemId, int quantity)
            {
                var result = Inventory.Add(
                    itemId,
                    quantity,
                    new InventoryChangeContext("stock", Guid.NewGuid(), Guid.NewGuid()));
                Assert.That(result.Succeeded, Is.True);
            }
        }

        private sealed class FirstSelector : IOrderTemplateSelector
        {
            public int SelectIndex(int exclusiveMax) => 0;
        }

        private sealed class RecordingInventorySink : IInventoryEventSink
        {
            public List<InventoryChanged> Events { get; } = new List<InventoryChanged>();
            public bool TryPublish(InventoryChanged change) { Events.Add(change); return true; }
        }

        private class RecordingOrderSink : IOrderEventSink
        {
            public List<OrderChanged> Events { get; } = new List<OrderChanged>();
            public virtual bool TryPublish(OrderChanged change) { Events.Add(change); return true; }
        }

        private sealed class RecordingEconomySink : IEconomyEventSink
        {
            public List<BalanceChanged> Events { get; } = new List<BalanceChanged>();
            public bool TryPublish(BalanceChanged change) { Events.Add(change); return true; }
        }

        private sealed class ReentrantOrderSink : RecordingOrderSink
        {
            public OrderDeliveryCoordinator Coordinator { get; set; }
            public ItemId Medicine { get; set; }
            public OrderDeliveryContext Context { get; set; }
            public OrderDeliveryResult NestedResult { get; private set; }

            public override bool TryPublish(OrderChanged change)
            {
                base.TryPublish(change);
                if (change.Kind == OrderMutationKind.Complete)
                    NestedResult = Coordinator.Deliver(Medicine, 2, Context);
                return true;
            }
        }

        private sealed class ThrowingInventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged change) => throw new InvalidOperationException("inventory");
        }

        private sealed class ThrowingOrderSink : IOrderEventSink
        {
            public bool TryPublish(OrderChanged change) => throw new InvalidOperationException("orders");
        }

        private sealed class ThrowingEconomySink : IEconomyEventSink
        {
            public bool TryPublish(BalanceChanged change) => throw new InvalidOperationException("economy");
        }
    }
}
