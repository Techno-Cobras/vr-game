using System;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.SeedStorage
{
    public sealed class PlantSeedCoordinatorTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly ItemId BasilSeed = new ItemId("item.seed.basil");
        private static readonly ItemId BasilHarvest = new ItemId("item.harvest.basil");

        [Test]
        public void Plant_CommitsInventoryAndSlotBeforePublishingEitherEvent()
        {
            var fixture = CreateFixture(1);
            fixture.InventorySink.OnPublish = () =>
            {
                fixture.InventoryQuantitySeenBySink = fixture.Inventory.GetQuantity(BasilSeed);
                fixture.SlotStateSeenBySink = fixture.Slot.GetSnapshot().State;
                fixture.InventoryReentry = fixture.Inventory.Add(BasilSeed, 1, InventoryContext());
                fixture.SlotReentry = fixture.Slot.RequestWater(SlotContext());
            };

            var result = fixture.Coordinator.Plant(Basil, BasilSeed, Context());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(fixture.InventoryQuantitySeenBySink, Is.Zero);
            Assert.That(fixture.SlotStateSeenBySink, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(fixture.InventoryReentry.Rejection, Is.EqualTo(InventoryRejection.ReentrantMutation));
            Assert.That(fixture.SlotReentry.Rejection, Is.EqualTo(PlantingSlotRejection.ReentrantMutation));
            Assert.That(result.InventoryChange.Changes[0].Delta, Is.EqualTo(-1));
            Assert.That(result.SlotChange.After.State, Is.EqualTo(PlantingSlotState.Growing));
        }

        [Test]
        public void Plant_RejectionsLeaveBothOwnersUnchanged()
        {
            var unavailable = CreateFixture(0);
            var unavailableResult = unavailable.Coordinator.Plant(Basil, BasilSeed, Context());
            Assert.That(unavailableResult.Rejection, Is.EqualTo(PlantSeedRejection.SeedUnavailable));
            Assert.That(unavailable.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));

            var occupied = CreateFixture(2);
            occupied.Slot.Plant(Basil, SlotContext());
            var inventoryVersion = occupied.Inventory.Version;
            var quantity = occupied.Inventory.GetQuantity(BasilSeed);
            var occupiedResult = occupied.Coordinator.Plant(Basil, BasilSeed, Context());
            Assert.That(occupiedResult.Rejection, Is.EqualTo(PlantSeedRejection.SlotRejected));
            Assert.That(occupied.Inventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(occupied.Inventory.GetQuantity(BasilSeed), Is.EqualTo(quantity));

            var mismatch = CreateFixture(1);
            var mismatchResult = mismatch.Coordinator.Plant(Basil, BasilHarvest, Context());
            Assert.That(mismatchResult.Rejection, Is.EqualTo(PlantSeedRejection.SeedIdentityMismatch));
            Assert.That(mismatch.Inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(mismatch.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
        }

        [Test]
        public void Plant_ReplayAndConflictDoNotConsumeAgain()
        {
            var fixture = CreateFixture(2);
            var context = Context();

            var first = fixture.Coordinator.Plant(Basil, BasilSeed, context);
            var replay = fixture.Coordinator.Plant(Basil, BasilSeed, context);
            var conflict = fixture.Coordinator.Plant(Basil, BasilHarvest, context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(conflict.Rejection, Is.EqualTo(PlantSeedRejection.CommandConflict));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.InventorySink.PublishCount, Is.EqualTo(2)); // initial Add + plant
            Assert.That(fixture.SlotSink.PublishCount, Is.EqualTo(1));
        }

        [Test]
        public void Plant_SinkExceptionsKeepBothCommittedAndAttemptBothPublications()
        {
            var fixture = CreateFixture(1);
            fixture.InventorySink.Throw = true;
            fixture.SlotSink.Throw = true;

            var result = fixture.Coordinator.Plant(Basil, BasilSeed, Context());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.InventoryEventPublished, Is.False);
            Assert.That(result.SlotEventPublished, Is.False);
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(fixture.SlotSink.PublishCount, Is.EqualTo(1));
        }

        private static Fixture CreateFixture(int quantity)
        {
            var itemCatalog = new ItemCatalog(new[]
            {
                Item(BasilSeed, ItemCategory.Seed),
                Item(BasilHarvest, ItemCategory.HarvestedPlant)
            });
            var plantCatalog = new PlantCatalog(itemCatalog, new[]
            {
                new PlantDefinition(
                    Basil,
                    new GrowthDuration(60f),
                    BasilSeed,
                    BasilHarvest,
                    new[]
                    {
                        new PlantStageDefinition(
                            0f,
                            new PlantRepresentationKey("representation.plant.basil"))
                    })
            });
            var inventorySink = new RecordingInventorySink();
            var slotSink = new RecordingSlotSink();
            var inventory = new InventoryAggregate(InventoryId.New(), itemCatalog, inventorySink);
            if (quantity > 0)
                inventory.Add(BasilSeed, quantity, InventoryContext());
            var slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), plantCatalog, slotSink);
            var fixture = new Fixture(inventory, slot, inventorySink, slotSink);
            fixture.Coordinator = new PlantSeedCoordinator(inventory, plantCatalog, slot);
            return fixture;
        }

        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id,
            id.Value,
            category,
            new ItemRepresentationKey("representation." + id.Value.Substring("item.".Length)),
            new ItemStackRules(16, true));

        private static PlantSeedContext Context() =>
            new PlantSeedContext("тестовая посадка", Guid.NewGuid(), Guid.NewGuid());
        private static InventoryChangeContext InventoryContext() =>
            new InventoryChangeContext("начальное наполнение", Guid.NewGuid());
        private static PlantingSlotChangeContext SlotContext() =>
            new PlantingSlotChangeContext("тест слота", Guid.NewGuid(), Guid.NewGuid());

        private sealed class Fixture
        {
            public Fixture(
                InventoryAggregate inventory,
                PlantingSlot slot,
                RecordingInventorySink inventorySink,
                RecordingSlotSink slotSink)
            {
                Inventory = inventory;
                Slot = slot;
                InventorySink = inventorySink;
                SlotSink = slotSink;
            }

            public InventoryAggregate Inventory { get; }
            public PlantingSlot Slot { get; }
            public RecordingInventorySink InventorySink { get; }
            public RecordingSlotSink SlotSink { get; }
            public PlantSeedCoordinator Coordinator { get; set; }
            public int InventoryQuantitySeenBySink { get; set; } = -1;
            public PlantingSlotState SlotStateSeenBySink { get; set; }
            public InventoryMutationResult InventoryReentry { get; set; }
            public PlantingSlotMutationResult SlotReentry { get; set; }
        }

        private sealed class RecordingInventorySink : IInventoryEventSink
        {
            public Action OnPublish { get; set; }
            public bool Throw { get; set; }
            public int PublishCount { get; private set; }

            public bool TryPublish(InventoryChanged inventoryChanged)
            {
                PublishCount++;
                OnPublish?.Invoke();
                if (Throw)
                    throw new InvalidOperationException("Тестовая ошибка inventory sink.");
                return true;
            }
        }

        private sealed class RecordingSlotSink : IPlantingSlotEventSink
        {
            public bool Throw { get; set; }
            public int PublishCount { get; private set; }

            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                PublishCount++;
                if (Throw)
                    throw new InvalidOperationException("Тестовая ошибка slot sink.");
                return true;
            }
        }
    }
}
