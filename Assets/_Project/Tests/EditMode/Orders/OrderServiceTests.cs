using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Orders;
using VrGame.Domain.Inventory;
using VrGame.Domain.Orders;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Orders
{
    public sealed class OrderServiceTests
    {
        private static readonly ItemId MedicineA = new ItemId("item.medicine.a");
        private static readonly ItemId MedicineB = new ItemId("item.medicine.b");

        [Test]
        public void Generate_UsesDeterministicSelectorAndAllowsMultipleAvailableOrders()
        {
            var selector = new SequenceSelector(0, 1);
            var sink = new RecordingSink();
            var service = CreateService(selector, sink, out _);

            var first = service.Generate(Context("первый"));
            var second = service.Generate(Context("второй"));
            var snapshot = service.GetSnapshot();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(snapshot.Orders, Has.Count.EqualTo(2));
            Assert.That(snapshot.Orders[0].TemplateId.Value, Is.EqualTo("order-template.a"));
            Assert.That(snapshot.Orders[1].TemplateId.Value, Is.EqualTo("order-template.b"));
            Assert.That(snapshot.Orders[0].State, Is.EqualTo(OrderState.Available));
            Assert.That(snapshot.ActiveOrderId, Is.Null);
            Assert.That(selector.DrawCount, Is.EqualTo(2));
            Assert.That(sink.Events, Has.Count.EqualTo(2));
        }

        [Test]
        public void Generate_ReplayAndRejectedInputsDoNotCreateAdditionalOrders()
        {
            var selector = new SequenceSelector(0, 1);
            var sink = new RecordingSink();
            var service = CreateService(selector, sink, out _);
            var context = Context("generation");

            var first = service.Generate(context);
            var replay = service.Generate(context);
            var invalidContext = service.Generate(default);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(invalidContext.Rejection, Is.EqualTo(OrderRejection.InvalidContext));
            Assert.That(selector.DrawCount, Is.EqualTo(1));
            Assert.That(service.GetSnapshot().Orders, Has.Count.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void InvalidSelector_IsTypedCachedFailure()
        {
            var selector = new SequenceSelector(2);
            var service = CreateService(selector, new RecordingSink(), out _);
            var context = Context("invalid selector");

            var first = service.Generate(context);
            var replay = service.Generate(context);

            Assert.That(first.Rejection, Is.EqualTo(OrderRejection.InvalidTemplateSelection));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(selector.DrawCount, Is.EqualTo(1));
            Assert.That(service.Version, Is.Zero);
        }

        [Test]
        public void InvalidOrDuplicateGeneratedId_DoesNotDrawSelectorOrMutate()
        {
            var duplicate = new OrderId(Guid.NewGuid());
            var selector = new SequenceSelector(0, 1, 0);
            var ids = new SequenceIdSource(duplicate, duplicate, default);
            var service = CreateService(selector, new RecordingSink(), out _, ids);

            Assert.That(service.Generate(Context("first")).Succeeded, Is.True);
            Assert.That(service.Generate(Context("duplicate")).Rejection,
                Is.EqualTo(OrderRejection.InvalidGeneratedOrderId));
            Assert.That(service.Generate(Context("empty")).Rejection,
                Is.EqualTo(OrderRejection.InvalidGeneratedOrderId));
            Assert.That(selector.DrawCount, Is.EqualTo(1));
            Assert.That(service.GetSnapshot().Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public void Accept_EnforcesOneActiveAndIllegalTransitionsAreNoOps()
        {
            var sink = new RecordingSink();
            var service = CreateService(new SequenceSelector(0, 1), sink, out _);
            var first = Generate(service, "first");
            var second = Generate(service, "second");

            Assert.That(service.Accept(first, Context("accept first")).Succeeded, Is.True);
            var version = service.Version;
            var blocked = service.Accept(second, Context("accept second"));
            var duplicate = service.Accept(first, Context("accept active again"));
            var completeAvailable = service.Complete(second, Context("complete available"));

            Assert.That(blocked.Rejection, Is.EqualTo(OrderRejection.ActiveOrderExists));
            Assert.That(duplicate.Rejection, Is.EqualTo(OrderRejection.InvalidTransition));
            Assert.That(completeAvailable.Rejection, Is.EqualTo(OrderRejection.InvalidTransition));
            Assert.That(service.Version, Is.EqualTo(version));
            Assert.That(service.ActiveOrderId, Is.EqualTo(first));
            Assert.That(sink.Events, Has.Count.EqualTo(3));
        }

        [Test]
        public void Completion_IsExactOnceReplayAndAllowsNextAvailableAcceptance()
        {
            var sink = new RecordingSink();
            var service = CreateService(new SequenceSelector(0, 1), sink, out _);
            var first = Generate(service, "first");
            var second = Generate(service, "second");
            service.Accept(first, Context("accept"));
            var completionContext = Context("complete");

            var completed = service.Complete(first, completionContext);
            var replay = service.Complete(first, completionContext);
            var duplicate = service.Complete(first, Context("complete twice"));
            var acceptedSecond = service.Accept(second, Context("accept second"));

            Assert.That(completed.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(duplicate.Rejection, Is.EqualTo(OrderRejection.AlreadyCompleted));
            Assert.That(acceptedSecond.Succeeded, Is.True);
            Assert.That(service.ActiveOrderId, Is.EqualTo(second));
            Assert.That(sink.Events, Has.Count.EqualTo(5));
        }

        [Test]
        public void Readiness_IsDerivedFromActiveOrderAndInventorySnapshot()
        {
            var service = CreateService(new SequenceSelector(1), new RecordingSink(), out var items);
            var inventory = new InventoryAggregate(InventoryId.New(), items, new InventorySink());
            Assert.That(service.GetReadiness(inventory.GetSnapshot()).HasActiveOrder, Is.False);
            var order = Generate(service, "order");
            service.Accept(order, Context("accept"));

            var empty = service.GetReadiness(inventory.GetSnapshot());
            inventory.Add(MedicineA, 99, InventoryContext("unrelated"));
            var unrelated = service.GetReadiness(inventory.GetSnapshot());
            inventory.Add(MedicineB, 1, InventoryContext("partial"));
            var partial = service.GetReadiness(inventory.GetSnapshot());
            inventory.Add(MedicineB, 1, InventoryContext("exact"));
            var exact = service.GetReadiness(inventory.GetSnapshot());
            inventory.Add(MedicineB, 3, InventoryContext("surplus"));
            var surplus = service.GetReadiness(inventory.GetSnapshot());

            Assert.That(empty.AvailableQuantity, Is.Zero);
            Assert.That(unrelated.AvailableQuantity, Is.Zero);
            Assert.That(partial.MissingQuantity, Is.EqualTo(1));
            Assert.That(partial.IsReady, Is.False);
            Assert.That(exact.MissingQuantity, Is.Zero);
            Assert.That(exact.IsReady, Is.True);
            Assert.That(surplus.AvailableQuantity, Is.EqualTo(5));
            Assert.That(surplus.IsReady, Is.True);
        }

        [Test]
        public void UnknownAndCommandConflict_DoNotMutateOrPublish()
        {
            var sink = new RecordingSink();
            var service = CreateService(new SequenceSelector(0, 1), sink, out _);
            var first = Generate(service, "first");
            var second = Generate(service, "second");
            var context = Context("accept command");
            Assert.That(service.Accept(first, context).Succeeded, Is.True);
            var version = service.Version;

            Assert.That(service.Accept(second, context).Rejection, Is.EqualTo(OrderRejection.CommandConflict));
            Assert.That(service.Accept(new OrderId(Guid.NewGuid()), Context("unknown")).Rejection,
                Is.EqualTo(OrderRejection.UnknownOrder));
            Assert.That(service.Version, Is.EqualTo(version));
            Assert.That(sink.Events, Has.Count.EqualTo(3));
        }

        [Test]
        public void VersionOverflow_IsCachedNoOpBeforeSelectorDraw()
        {
            var selector = new SequenceSelector(0);
            var sink = new RecordingSink();
            var service = CreateService(selector, sink, out _);
            typeof(OrderService).GetProperty(nameof(OrderService.Version),
                    BindingFlags.Instance | BindingFlags.Public)
                .SetValue(service, long.MaxValue);
            var context = Context("overflow");

            var result = service.Generate(context);
            var replay = service.Generate(context);

            Assert.That(result.Rejection, Is.EqualTo(OrderRejection.ArithmeticOverflow));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(selector.DrawCount, Is.Zero);
            Assert.That(service.Version, Is.EqualTo(long.MaxValue));
            Assert.That(sink.Events, Is.Empty);
        }

        [Test]
        public void EventFailure_DoesNotRollBackAndEventRemainsInResult()
        {
            var service = CreateService(new SequenceSelector(0), new ThrowingSink(), out _);

            var result = service.Generate(Context("event failure"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EventPublished, Is.False);
            Assert.That(result.Change, Is.Not.Null);
            Assert.That(service.Version, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantMutation_IsRejectedWithoutSecondEvent()
        {
            var sink = new ReentrantSink();
            var service = CreateService(new SequenceSelector(0, 1), sink, out _);
            sink.Service = service;

            var result = service.Generate(Context("outer"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.ReentrantResult.Rejection, Is.EqualTo(OrderRejection.ReentrantMutation));
            Assert.That(service.GetSnapshot().Orders, Has.Count.EqualTo(1));
            Assert.That(sink.EventCount, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotsAndEventsAreImmutableCopiesAndEnumValuesStayStable()
        {
            var service = CreateService(new SequenceSelector(0), new RecordingSink(), out _);
            var generated = service.Generate(Context("generate"));
            var snapshot = service.GetSnapshot();
            var collection = snapshot.Orders as ICollection<OrderSnapshot>;

            Assert.That(collection, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => collection.Add(generated.Change.After));
            Assert.That(snapshot.Orders, Has.Count.EqualTo(1));
            Assert.That(generated.Change.After.State, Is.EqualTo(OrderState.Available));
            Assert.That((int)OrderState.Available, Is.EqualTo(0));
            Assert.That((int)OrderState.InProgress, Is.EqualTo(1));
            Assert.That((int)OrderState.Completed, Is.EqualTo(2));
            Assert.That((int)OrderMutationKind.Generate, Is.EqualTo(1));
            Assert.That((int)OrderMutationKind.Accept, Is.EqualTo(2));
            Assert.That((int)OrderMutationKind.Complete, Is.EqualTo(3));
        }

        private static OrderService CreateService(
            IOrderTemplateSelector selector,
            IOrderEventSink sink,
            out ItemCatalog items,
            IOrderIdSource ids = null)
        {
            items = Items();
            var templates = new OrderTemplateCatalog(items, new[]
            {
                Template("order-template.a", MedicineA, 1, 25),
                Template("order-template.b", MedicineB, 2, 60)
            });
            return new OrderService(templates, selector, ids ?? new SequenceIdSource(), sink);
        }

        private static OrderId Generate(OrderService service, string reason) =>
            service.Generate(Context(reason)).Change.After.OrderId;

        private static ItemCatalog Items() => new ItemCatalog(new[]
        {
            Item(MedicineA),
            Item(MedicineB)
        });

        private static ItemDefinition Item(ItemId id) => new ItemDefinition(
            id,
            id.Value,
            ItemCategory.Medicine,
            new ItemRepresentationKey("representation." + id.Value),
            new ItemStackRules(16, false));

        private static OrderTemplateDefinition Template(
            string id,
            ItemId medicine,
            int quantity,
            int reward) => new OrderTemplateDefinition(
            new OrderTemplateId(id), id, medicine, quantity, reward);

        private static OrderContext Context(string reason) =>
            new OrderContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static InventoryChangeContext InventoryContext(string reason) =>
            new InventoryChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class SequenceSelector : IOrderTemplateSelector
        {
            private readonly Queue<int> values;
            public SequenceSelector(params int[] values) { this.values = new Queue<int>(values); }
            public int DrawCount { get; private set; }
            public int SelectIndex(int exclusiveMax)
            {
                DrawCount++;
                return values.Count > 0 ? values.Dequeue() : 0;
            }
        }

        private sealed class SequenceIdSource : IOrderIdSource
        {
            private readonly Queue<OrderId> ids;
            public SequenceIdSource(params OrderId[] ids) { this.ids = new Queue<OrderId>(ids); }
            public OrderId NextId() => ids.Count > 0 ? ids.Dequeue() : new OrderId(Guid.NewGuid());
        }

        private sealed class RecordingSink : IOrderEventSink
        {
            public List<OrderChanged> Events { get; } = new List<OrderChanged>();
            public bool TryPublish(OrderChanged orderChanged)
            {
                Events.Add(orderChanged);
                return true;
            }
        }

        private sealed class ThrowingSink : IOrderEventSink
        {
            public bool TryPublish(OrderChanged orderChanged) => throw new InvalidOperationException("sink");
        }

        private sealed class ReentrantSink : IOrderEventSink
        {
            public OrderService Service { get; set; }
            public OrderMutationResult ReentrantResult { get; private set; }
            public int EventCount { get; private set; }
            public bool TryPublish(OrderChanged orderChanged)
            {
                EventCount++;
                ReentrantResult = Service.Generate(Context("reentrant"));
                return true;
            }
        }

        private sealed class InventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }
    }
}
