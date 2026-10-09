using System;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.SeedStorage
{
    public sealed class InventoryTransferTests
    {
        private static readonly ItemId Seed = new ItemId("item.seed.basil");

        [Test]
        public void Transfer_CommitsBothInventoriesBeforePublishingEvents()
        {
            var catalog = Catalog();
            var sourceSink = new ObservingSink();
            var destinationSink = new ObservingSink();
            var source = new InventoryAggregate(InventoryId.New(), catalog, sourceSink);
            var destination = new InventoryAggregate(InventoryId.New(), catalog, destinationSink);
            sourceSink.Source = source;
            sourceSink.Destination = destination;
            destinationSink.Source = source;
            destinationSink.Destination = destination;
            source.Add(Seed, 3, ChangeContext());
            sourceSink.Reset();

            var context = TransferContext();
            var result = source.TransferTo(destination, Seed, 2, context);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(source.GetQuantity(Seed), Is.EqualTo(1));
            Assert.That(destination.GetQuantity(Seed), Is.EqualTo(2));
            Assert.That(result.SourceChange.Kind, Is.EqualTo(InventoryMutationKind.TransferOut));
            Assert.That(result.DestinationChange.Kind, Is.EqualTo(InventoryMutationKind.TransferIn));
            Assert.That(result.SourceChange.CommandId, Is.EqualTo(context.CommandId));
            Assert.That(result.DestinationChange.CommandId, Is.EqualTo(context.CommandId));
            Assert.That(sourceSink.ObservedSourceQuantity, Is.EqualTo(1));
            Assert.That(sourceSink.ObservedDestinationQuantity, Is.EqualTo(2));
            Assert.That(destinationSink.ObservedSourceQuantity, Is.EqualTo(1));
            Assert.That(destinationSink.ObservedDestinationQuantity, Is.EqualTo(2));
        }

        [Test]
        public void Transfer_ReplayAndConflictNeverMutateTwice()
        {
            var catalog = Catalog();
            var source = new InventoryAggregate(InventoryId.New(), catalog, new ObservingSink());
            var destination = new InventoryAggregate(InventoryId.New(), catalog, new ObservingSink());
            source.Add(Seed, 3, ChangeContext());
            var context = TransferContext();

            var first = source.TransferTo(destination, Seed, 1, context);
            var replay = source.TransferTo(destination, Seed, 1, context);
            var conflict = source.TransferTo(destination, Seed, 2, context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(conflict.Rejection, Is.EqualTo(InventoryTransferRejection.CommandConflict));
            Assert.That(source.GetQuantity(Seed), Is.EqualTo(2));
            Assert.That(destination.GetQuantity(Seed), Is.EqualTo(1));
        }

        [Test]
        public void Transfer_RejectionsAreExactNoOp()
        {
            var catalog = Catalog();
            var source = new InventoryAggregate(InventoryId.New(), catalog, new ObservingSink());
            var destination = new InventoryAggregate(InventoryId.New(), catalog, new ObservingSink());
            source.Add(Seed, 1, ChangeContext());
            var sourceVersion = source.Version;
            var destinationVersion = destination.Version;

            var insufficient = source.TransferTo(destination, Seed, 2, TransferContext());
            var same = source.TransferTo(source, Seed, 1, TransferContext());

            Assert.That(insufficient.Rejection, Is.EqualTo(InventoryTransferRejection.InsufficientStock));
            Assert.That(same.Rejection, Is.EqualTo(InventoryTransferRejection.SameInventory));
            Assert.That(source.GetQuantity(Seed), Is.EqualTo(1));
            Assert.That(destination.GetQuantity(Seed), Is.Zero);
            Assert.That(source.Version, Is.EqualTo(sourceVersion));
            Assert.That(destination.Version, Is.EqualTo(destinationVersion));
        }

        [Test]
        public void Transfer_BlocksReentryAndStillPublishesSecondEventAfterSinkException()
        {
            var catalog = Catalog();
            var sourceSink = new ObservingSink { Throw = true };
            var destinationSink = new ObservingSink();
            var source = new InventoryAggregate(InventoryId.New(), catalog, sourceSink);
            var destination = new InventoryAggregate(InventoryId.New(), catalog, destinationSink);
            sourceSink.Source = source;
            sourceSink.Destination = destination;
            destinationSink.Source = source;
            destinationSink.Destination = destination;
            source.Add(Seed, 2, ChangeContext());
            sourceSink.Reset();
            sourceSink.OnPublish = () => sourceSink.ReentrantResult =
                destination.Add(Seed, 1, ChangeContext());

            var result = source.TransferTo(destination, Seed, 1, TransferContext());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SourceEventPublished, Is.False);
            Assert.That(result.DestinationEventPublished, Is.True);
            Assert.That(sourceSink.ReentrantResult.Rejection, Is.EqualTo(InventoryRejection.ReentrantMutation));
            Assert.That(destinationSink.PublishCount, Is.EqualTo(1));
            Assert.That(source.GetQuantity(Seed), Is.EqualTo(1));
            Assert.That(destination.GetQuantity(Seed), Is.EqualTo(1));
        }

        private static ItemCatalog Catalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(
                Seed,
                "Саженец базилика",
                ItemCategory.Seed,
                new ItemRepresentationKey("representation.seed.basil"),
                new ItemStackRules(16, true))
        });

        private static InventoryChangeContext ChangeContext() =>
            new InventoryChangeContext("тестовое изменение", Guid.NewGuid());

        private static InventoryTransferContext TransferContext() =>
            new InventoryTransferContext("тестовая передача", Guid.NewGuid(), Guid.NewGuid());

        private sealed class ObservingSink : IInventoryEventSink
        {
            public InventoryAggregate Source { get; set; }
            public InventoryAggregate Destination { get; set; }
            public bool Throw { get; set; }
            public Action OnPublish { get; set; }
            public int PublishCount { get; private set; }
            public int ObservedSourceQuantity { get; private set; } = -1;
            public int ObservedDestinationQuantity { get; private set; } = -1;
            public InventoryMutationResult ReentrantResult { get; set; }

            public bool TryPublish(InventoryChanged inventoryChanged)
            {
                PublishCount++;
                if (Source != null)
                    ObservedSourceQuantity = Source.GetQuantity(Seed);
                if (Destination != null)
                    ObservedDestinationQuantity = Destination.GetQuantity(Seed);
                OnPublish?.Invoke();
                if (Throw)
                    throw new InvalidOperationException("Тестовая ошибка sink.");
                return true;
            }

            public void Reset()
            {
                PublishCount = 0;
                ObservedSourceQuantity = -1;
                ObservedDestinationQuantity = -1;
                OnPublish = null;
                ReentrantResult = null;
            }
        }
    }
}
