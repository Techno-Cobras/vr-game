using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;

namespace VrGame.Tests.EditMode.Inventory
{
    public sealed class InventoryTests
    {
        private static readonly ItemId Basil = new ItemId("item.seed.basil");
        private static readonly ItemId Mint = new ItemId("item.seed.mint");
        private static readonly ItemId Unknown = new ItemId("item.seed.unknown");

        [Test]
        public void AddAndRemove_UpdateQuantityVersionAndEvents()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);

            var added = inventory.Add(Basil, 5, Context("inventory.test.add", 1));
            var removed = inventory.Remove(Basil, 2, Context("inventory.test.remove", 2));
            var removedAll = inventory.Remove(Basil, 3, Context("inventory.test.remove", 3));

            Assert.That(added.Succeeded, Is.True);
            Assert.That(added.EventPublished, Is.True);
            Assert.That(removed.Succeeded, Is.True);
            Assert.That(removedAll.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity(Basil), Is.Zero);
            Assert.That(inventory.GetSnapshot().Items, Is.Empty);
            Assert.That(inventory.Version, Is.EqualTo(3));
            Assert.That(sink.Events, Has.Count.EqualTo(3));
            Assert.That(sink.Events[0].Kind, Is.EqualTo(InventoryMutationKind.Add));
            Assert.That(sink.Events[1].Kind, Is.EqualTo(InventoryMutationKind.Remove));
            Assert.That(sink.Events[1].Changes.Single().Before, Is.EqualTo(5));
            Assert.That(sink.Events[1].Changes.Single().After, Is.EqualTo(3));
            Assert.That(sink.Events[1].Changes.Single().Delta, Is.EqualTo(-2));
        }

        [Test]
        public void KnownAbsentItem_IsZero_AndUnknownItemIsRejected()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);

            Assert.That(inventory.GetQuantity(Basil), Is.Zero);
            Assert.That(inventory.TryGetQuantity(Unknown, out var quantity), Is.False);
            Assert.That(quantity, Is.Zero);
            Assert.Throws<KeyNotFoundException>(() => inventory.GetQuantity(Unknown));

            var add = inventory.Add(Unknown, 1, Context("inventory.test.unknown", 1));
            var remove = inventory.Remove(Unknown, 1, Context("inventory.test.unknown", 2));

            Assert.That(add.Rejection, Is.EqualTo(InventoryRejection.UnknownItem));
            Assert.That(remove.Rejection, Is.EqualTo(InventoryRejection.UnknownItem));
            AssertUnchanged(inventory, sink);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidQuantity_IsRejectedWithoutMutation(int quantity)
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);

            var add = inventory.Add(Basil, quantity, Context("inventory.test.invalid", 1));
            var remove = inventory.Remove(Basil, quantity, Context("inventory.test.invalid", 2));

            Assert.That(add.Rejection, Is.EqualTo(InventoryRejection.InvalidQuantity));
            Assert.That(remove.Rejection, Is.EqualTo(InventoryRejection.InvalidQuantity));
            AssertUnchanged(inventory, sink);
        }

        [Test]
        public void RemoveInsufficientStock_IsRejectedWithoutMutation()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);
            inventory.Add(Basil, 2, Context("inventory.test.setup", 1));
            sink.Events.Clear();

            var result = inventory.Remove(Basil, 3, Context("inventory.test.remove", 2));

            Assert.That(result.Rejection, Is.EqualTo(InventoryRejection.InsufficientStock));
            Assert.That(result.AvailableQuantity, Is.EqualTo(2));
            Assert.That(result.RequestedQuantity, Is.EqualTo(3));
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(2));
            Assert.That(inventory.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Is.Empty);
        }

        [Test]
        public void ConsumeBatch_NormalizesDuplicatesAndCommitsOnce()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);
            inventory.Add(Basil, 5, Context("inventory.test.setup", 1));
            inventory.Add(Mint, 4, Context("inventory.test.setup", 2));
            sink.Events.Clear();

            var result = inventory.ConsumeBatch(new[]
            {
                new InventoryItemQuantity(Mint, 1),
                new InventoryItemQuantity(Basil, 2),
                new InventoryItemQuantity(Basil, 1)
            }, Context("inventory.test.consume", 3));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(Mint), Is.EqualTo(3));
            Assert.That(inventory.Version, Is.EqualTo(3));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
            Assert.That(sink.Events[0].Changes.Select(change => change.ItemId), Is.EqualTo(new[] { Basil, Mint }));
            Assert.That(sink.Events[0].Changes.Select(change => change.Delta), Is.EqualTo(new[] { -3, -1 }));
        }

        [Test]
        public void ConsumeBatch_InsufficientStockIsAtomic()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);
            inventory.Add(Basil, 5, Context("inventory.test.setup", 1));
            inventory.Add(Mint, 1, Context("inventory.test.setup", 2));
            sink.Events.Clear();

            var result = inventory.ConsumeBatch(new[]
            {
                new InventoryItemQuantity(Basil, 2),
                new InventoryItemQuantity(Mint, 2)
            }, Context("inventory.test.consume", 3));

            Assert.That(result.Rejection, Is.EqualTo(InventoryRejection.InsufficientStock));
            Assert.That(result.FailedItem, Is.EqualTo(Mint));
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(5));
            Assert.That(inventory.GetQuantity(Mint), Is.EqualTo(1));
            Assert.That(inventory.Version, Is.EqualTo(2));
            Assert.That(sink.Events, Is.Empty);
        }

        [Test]
        public void ConsumeBatch_RejectsInvalidInputsWithoutMutation()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);

            Assert.That(
                inventory.ConsumeBatch(null, Context("inventory.test.consume", 1)).Rejection,
                Is.EqualTo(InventoryRejection.InvalidBatch));
            Assert.That(
                inventory.ConsumeBatch(Array.Empty<InventoryItemQuantity>(), Context("inventory.test.consume", 2)).Rejection,
                Is.EqualTo(InventoryRejection.InvalidBatch));
            Assert.That(
                inventory.ConsumeBatch(
                    new[] { new InventoryItemQuantity(Basil, -1) },
                    Context("inventory.test.consume", 3)).Rejection,
                Is.EqualTo(InventoryRejection.InvalidQuantity));
            Assert.That(
                inventory.ConsumeBatch(
                    new[] { new InventoryItemQuantity(Basil, 0) },
                    Context("inventory.test.consume", 31)).Rejection,
                Is.EqualTo(InventoryRejection.InvalidQuantity));
            Assert.That(
                inventory.ConsumeBatch(
                    new[] { new InventoryItemQuantity(Unknown, 1) },
                    Context("inventory.test.consume", 4)).Rejection,
                Is.EqualTo(InventoryRejection.UnknownItem));
            Assert.That(
                inventory.ConsumeBatch(new[]
                {
                    new InventoryItemQuantity(Basil, int.MaxValue),
                    new InventoryItemQuantity(Basil, 1)
                }, Context("inventory.test.consume", 5)).Rejection,
                Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            AssertUnchanged(inventory, sink);
        }

        [Test]
        public void InvalidContext_IsRejectedWithoutMutation()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);

            var result = inventory.Add(Basil, 1, default);

            Assert.That(result.Rejection, Is.EqualTo(InventoryRejection.InvalidContext));
            AssertUnchanged(inventory, sink);
        }

        [Test]
        public void Add_RejectsArithmeticOverflowWithoutMutation()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);
            inventory.Add(Basil, int.MaxValue, Context("inventory.test.setup", 1));
            sink.Events.Clear();

            var result = inventory.Add(Basil, 1, Context("inventory.test.add", 2));

            Assert.That(result.Rejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(int.MaxValue));
            Assert.That(inventory.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Is.Empty);
        }

        [Test]
        public void Event_ContainsCommittedUiDataAndContext()
        {
            var sink = new RecordingSink();
            var inventory = CreateInventory(sink);
            var context = Context("inventory.harvest", 42);
            var observedQuantity = -1;
            sink.OnPublish = changed => observedQuantity = inventory.GetQuantity(Basil);

            var result = inventory.Add(Basil, 7, context);
            var changed = result.Change;

            Assert.That(changed.InventoryId, Is.EqualTo(inventory.Id));
            Assert.That(changed.Version, Is.EqualTo(1));
            Assert.That(changed.Reason, Is.EqualTo("inventory.harvest"));
            Assert.That(changed.CorrelationId, Is.EqualTo(context.CorrelationId));
            Assert.That(changed.Changes.Single().Before, Is.Zero);
            Assert.That(changed.Changes.Single().After, Is.EqualTo(7));
            Assert.That(changed.Changes.Single().Delta, Is.EqualTo(7));
            Assert.That(observedQuantity, Is.EqualTo(changed.Changes.Single().After));
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.IList)changed.Changes).Add(
                    new InventoryQuantityChange(Basil, 7, 8)));
        }

        [Test]
        public void EventSinkFailure_DoesNotHideCommittedMutation()
        {
            var inventory = new VrGame.Domain.Inventory.Inventory(
                new InventoryId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
                CreateCatalog(),
                new ThrowingSink());

            var result = inventory.Add(Basil, 2, Context("inventory.test.add", 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EventPublished, Is.False);
            Assert.That(result.Change, Is.Not.Null);
            Assert.That(result.Change.Changes.Single().After, Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(2));
            Assert.That(inventory.Version, Is.EqualTo(1));
        }

        [Test]
        public void EventSink_CannotMutateInventoryReentrantly()
        {
            var sink = new ReentrantSink();
            var inventory = new VrGame.Domain.Inventory.Inventory(
                new InventoryId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
                CreateCatalog(),
                sink);
            sink.Inventory = inventory;

            var result = inventory.Add(Basil, 2, Context("inventory.test.add", 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.ReentrantResult.Rejection, Is.EqualTo(InventoryRejection.ReentrantMutation));
            Assert.That(inventory.GetQuantity(Basil), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(Mint), Is.Zero);
            Assert.That(inventory.Version, Is.EqualTo(1));
            Assert.That(sink.PublishedVersions, Is.EqualTo(new[] { 1L }));
        }

        [Test]
        public void Snapshot_IsSortedReadOnlyAndIndependentBetweenInventories()
        {
            var catalog = CreateCatalog();
            var firstSink = new RecordingSink();
            var secondSink = new RecordingSink();
            var first = new VrGame.Domain.Inventory.Inventory(
                new InventoryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), catalog, firstSink);
            var second = new VrGame.Domain.Inventory.Inventory(
                new InventoryId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), catalog, secondSink);
            first.Add(Mint, 2, Context("inventory.test.add", 1));
            first.Add(Basil, 1, Context("inventory.test.add", 2));

            var snapshot = first.GetSnapshot();
            first.Add(Basil, 2, Context("inventory.test.add", 3));

            Assert.That(snapshot.Items.Select(item => item.ItemId), Is.EqualTo(new[] { Basil, Mint }));
            Assert.That(snapshot.Items.Single(item => item.ItemId == Basil).Quantity, Is.EqualTo(1));
            Assert.That(first.GetQuantity(Basil), Is.EqualTo(3));
            Assert.That(snapshot.Version, Is.EqualTo(2));
            Assert.That(second.GetQuantity(Basil), Is.Zero);
            Assert.That(snapshot.Items, Is.InstanceOf<System.Collections.IList>());
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.IList)snapshot.Items).Add(new InventoryItemQuantity(Basil, 99)));
        }

        private static VrGame.Domain.Inventory.Inventory CreateInventory(RecordingSink sink) =>
            new VrGame.Domain.Inventory.Inventory(
                new InventoryId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                CreateCatalog(),
                sink);

        private static ItemCatalog CreateCatalog() => new ItemCatalog(new[]
        {
            CreateDefinition(Basil),
            CreateDefinition(Mint)
        });

        private static ItemDefinition CreateDefinition(ItemId id) => new ItemDefinition(
            id,
            id.Value,
            ItemCategory.Seed,
            new ItemRepresentationKey("representation.test.seed"),
            new ItemStackRules(20, false));

        private static InventoryChangeContext Context(string reason, int seed) =>
            new InventoryChangeContext(reason, new Guid(seed, 0, 0, new byte[8]));

        private static void AssertUnchanged(VrGame.Domain.Inventory.Inventory inventory, RecordingSink sink)
        {
            Assert.That(inventory.Version, Is.Zero);
            Assert.That(inventory.GetSnapshot().Items, Is.Empty);
            Assert.That(sink.Events, Is.Empty);
        }

        private sealed class RecordingSink : IInventoryEventSink
        {
            public List<InventoryChanged> Events { get; } = new List<InventoryChanged>();

            public Action<InventoryChanged> OnPublish { get; set; }

            public bool TryPublish(InventoryChanged inventoryChanged)
            {
                Events.Add(inventoryChanged);
                OnPublish?.Invoke(inventoryChanged);
                return true;
            }
        }

        private sealed class ThrowingSink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) =>
                throw new InvalidOperationException("Test publication failure.");
        }

        private sealed class ReentrantSink : IInventoryEventSink
        {
            public VrGame.Domain.Inventory.Inventory Inventory { get; set; }

            public InventoryMutationResult ReentrantResult { get; private set; }

            public List<long> PublishedVersions { get; } = new List<long>();

            public bool TryPublish(InventoryChanged inventoryChanged)
            {
                PublishedVersions.Add(inventoryChanged.Version);
                ReentrantResult = Inventory.Add(Mint, 1, Context("inventory.test.reentrant", 99));
                return true;
            }
        }
    }
}
