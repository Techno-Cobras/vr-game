using System;
using NUnit.Framework;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Fertilizers
{
    public sealed class FertilizerSystemTests
    {
        private static readonly ItemId CheapItem = new ItemId("item.fertilizer.cheap");
        private static readonly ItemId MediumItem = new ItemId("item.fertilizer.medium");
        private static readonly ItemId ExpensiveItem = new ItemId("item.fertilizer.expensive");
        private static readonly PlantTypeId PlantId = new PlantTypeId("plant.test");

        [TestCase(FertilizerGrade.Cheap, 1.25f)]
        [TestCase(FertilizerGrade.Medium, 1.5f)]
        [TestCase(FertilizerGrade.Expensive, 2f)]
        public void Catalog_ExposesConfiguredGradeMultipliers(FertilizerGrade grade, float expected)
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Fertilizers.GetRequired(grade).GrowthMultiplier, Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_RejectsDuplicateGradesItemsAndInvalidMultiplier()
        {
            var item = FertilizerItem(CheapItem);
            Assert.That(() => new FertilizerDefinition(FertilizerGrade.Cheap, item, 1f),
                Throws.TypeOf<FertilizerDataValidationException>().With.Property("Error").EqualTo(FertilizerDataValidationError.InvalidMultiplier));
            Assert.That(() => new FertilizerCatalog(new[]
            {
                new FertilizerDefinition(FertilizerGrade.Cheap, item, 1.25f),
                new FertilizerDefinition(FertilizerGrade.Cheap, FertilizerItem(MediumItem), 1.5f)
            }), Throws.TypeOf<FertilizerDataValidationException>().With.Property("Error").EqualTo(FertilizerDataValidationError.DuplicateGrade));
        }

        [TestCase(FertilizerGrade.Cheap, 1.25f)]
        [TestCase(FertilizerGrade.Medium, 1.5f)]
        [TestCase(FertilizerGrade.Expensive, 2f)]
        public void Apply_ConsumesExactlyOneAndSetsPermanentModifier(FertilizerGrade grade, float multiplier)
        {
            var fixture = CreateFixture();
            fixture.Slot.Plant(PlantId, SlotContext());
            var itemId = fixture.Fertilizers.GetRequired(grade).ItemId;
            fixture.Player.Add(itemId, 2, InventoryContext());

            var result = fixture.Application.Apply(grade, itemId, CommandContext());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Player.GetQuantity(itemId), Is.EqualTo(1));
            Assert.That(fixture.Slot.GetSnapshot().GrowthModifier.EffectiveMultiplier, Is.EqualTo(multiplier));
            Assert.That(result.InventoryChange.Changes[0].Delta, Is.EqualTo(-1));
        }

        [Test]
        public void Apply_ToEmptyReadyOrAlreadyFertilizedSlot_DoesNotConsume()
        {
            var empty = CreateFixture();
            empty.Player.Add(CheapItem, 1, InventoryContext());
            var emptyResult = empty.Application.Apply(FertilizerGrade.Cheap, CheapItem, CommandContext());
            Assert.That(emptyResult.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(emptyResult.SlotRejection, Is.EqualTo(PlantingSlotRejection.NotGrowing));
            Assert.That(empty.Player.GetQuantity(CheapItem), Is.EqualTo(1));

            var repeated = CreateFixture();
            repeated.Slot.Plant(PlantId, SlotContext());
            repeated.Player.Add(CheapItem, 2, InventoryContext());
            Assert.That(repeated.Application.Apply(FertilizerGrade.Cheap, CheapItem, CommandContext()).Succeeded, Is.True);
            var repeatedResult = repeated.Application.Apply(FertilizerGrade.Cheap, CheapItem, CommandContext());
            Assert.That(repeatedResult.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(repeatedResult.SlotRejection, Is.EqualTo(PlantingSlotRejection.FertilizerAlreadyApplied));
            Assert.That(repeated.Player.GetQuantity(CheapItem), Is.EqualTo(1));

            var ready = CreateFixture();
            ready.Slot.Plant(PlantId, SlotContext());
            ready.Slot.AdvanceProgress(1f, ready.Slot.Version, SlotContext());
            ready.Slot.Water(SlotContext());
            ready.Slot.AdvanceProgress(1f, ready.Slot.Version, SlotContext());
            ready.Player.Add(CheapItem, 1, InventoryContext());
            Assert.That(ready.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
            var readyResult = ready.Application.Apply(FertilizerGrade.Cheap, CheapItem, CommandContext());
            Assert.That(readyResult.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(readyResult.SlotRejection, Is.EqualTo(PlantingSlotRejection.NotGrowing));
            Assert.That(ready.Player.GetQuantity(CheapItem), Is.EqualTo(1));
        }

        [Test]
        public void Apply_RejectsIdentityMismatchAndMissingInventoryWithoutSlotMutation()
        {
            var fixture = CreateFixture();
            fixture.Slot.Plant(PlantId, SlotContext());
            var version = fixture.Slot.Version;
            Assert.That(fixture.Application.Apply(FertilizerGrade.Cheap, MediumItem, CommandContext()).Rejection, Is.EqualTo(FertilizerApplicationRejection.ItemIdentityMismatch));
            Assert.That(fixture.Application.Apply(FertilizerGrade.Cheap, CheapItem, CommandContext()).Rejection, Is.EqualTo(FertilizerApplicationRejection.ItemUnavailable));
            Assert.That(fixture.Slot.Version, Is.EqualTo(version));
        }

        [TestCase(FertilizerGrade.Cheap)]
        [TestCase(FertilizerGrade.Medium)]
        [TestCase(FertilizerGrade.Expensive)]
        public void Dispense_EveryGradeStagesAndTransfersExactlyOne(FertilizerGrade grade)
        {
            var fixture = CreateFixture();
            var item = fixture.Fertilizers.GetRequired(grade).ItemId;
            fixture.Warehouse.Add(item, 2, InventoryContext());
            var success = fixture.Dispense.Dispense(grade, CommandContext());
            Assert.That(success.Succeeded, Is.True);
            Assert.That(fixture.Warehouse.GetQuantity(item), Is.EqualTo(1));
            Assert.That(fixture.Player.GetQuantity(item), Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.CommitCalls, Is.EqualTo(1));
        }

        [Test]
        public void Dispense_ReleaseFailureRollsBackWithoutChangingStock()
        {
            var fixture = CreateFixture();
            fixture.Warehouse.Add(CheapItem, 1, InventoryContext());
            fixture.Materializer.Batch.ReleaseSucceeds = false;
            var rejected = fixture.Dispense.Dispense(FertilizerGrade.Cheap, CommandContext());
            Assert.That(rejected.Rejection, Is.EqualTo(FertilizerDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Warehouse.GetQuantity(CheapItem), Is.EqualTo(1));
            Assert.That(fixture.Player.GetQuantity(CheapItem), Is.Zero);
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void Dispense_OwnershipCommitFailureCompensatesTransferAndDestroysStagedItem()
        {
            var fixture = CreateFixture();
            fixture.Warehouse.Add(CheapItem, 1, InventoryContext());
            fixture.Materializer.Batch.CommitSucceeds = false;

            var rejected = fixture.Dispense.Dispense(FertilizerGrade.Cheap, CommandContext());

            Assert.That(rejected.Rejection, Is.EqualTo(FertilizerDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Warehouse.GetQuantity(CheapItem), Is.EqualTo(1));
            Assert.That(fixture.Player.GetQuantity(CheapItem), Is.Zero);
            Assert.That(fixture.Materializer.Batch.CommitCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void GrowthTimer_UsesEveryGradeMultiplier()
        {
            foreach (var definition in CreateFixture().Fertilizers.Definitions)
            {
                var fixture = CreateFixture();
                fixture.Slot.Plant(PlantId, SlotContext());
                fixture.Player.Add(definition.ItemId, 1, InventoryContext());
                fixture.Application.Apply(definition.Grade, definition.ItemId, CommandContext());
                var time = new FakeTime();
                var timer = new PlantGrowthTimer(fixture.Slot, fixture.Plants, time);
                timer.Resume();
                time.Value = 10;
                timer.Tick();
                Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(10f * definition.GrowthMultiplier).Within(.001f));
            }
        }

        private static Fixture CreateFixture()
        {
            var seed = Item(new ItemId("item.seed.test"), ItemCategory.Seed);
            var harvest = Item(new ItemId("item.harvest.test"), ItemCategory.HarvestedPlant);
            var cheap = FertilizerItem(CheapItem); var medium = FertilizerItem(MediumItem); var expensive = FertilizerItem(ExpensiveItem);
            var items = new ItemCatalog(new[] { seed, harvest, cheap, medium, expensive });
            var plants = new PlantCatalog(items, new[] { new PlantDefinition(PlantId, new GrowthDuration(100f), seed.Id, harvest.Id,
                new[] { new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.test")) }) });
            var fertilizers = new FertilizerCatalog(new[]
            {
                new FertilizerDefinition(FertilizerGrade.Cheap, cheap, 1.25f),
                new FertilizerDefinition(FertilizerGrade.Medium, medium, 1.5f),
                new FertilizerDefinition(FertilizerGrade.Expensive, expensive, 2f)
            });
            var warehouse = new InventoryAggregate(InventoryId.New(), items, new Sink());
            var player = new InventoryAggregate(InventoryId.New(), items, new Sink());
            var slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), plants, new SlotSink());
            var materializer = new Materializer();
            return new Fixture(warehouse, player, plants, fertilizers, slot, materializer,
                new FertilizerDispenseCoordinator(warehouse, player, fertilizers, materializer),
                new FertilizerApplicationCoordinator(player, fertilizers, slot));
        }

        private static ItemDefinition FertilizerItem(ItemId id) => Item(id, ItemCategory.Fertilizer);
        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(id, id.Value, category,
            new ItemRepresentationKey("representation." + id.Value.Substring(5)), new ItemStackRules(16, true));
        private static FertilizerCommandContext CommandContext() => new FertilizerCommandContext("тест", Guid.NewGuid(), Guid.NewGuid());
        private static InventoryChangeContext InventoryContext() => new InventoryChangeContext("тест", Guid.NewGuid());
        private static PlantingSlotChangeContext SlotContext() => new PlantingSlotChangeContext("тест", Guid.NewGuid(), Guid.NewGuid());

        private sealed class Fixture
        {
            public Fixture(InventoryAggregate warehouse, InventoryAggregate player, PlantCatalog plants, FertilizerCatalog fertilizers, PlantingSlot slot, Materializer materializer, FertilizerDispenseCoordinator dispense, FertilizerApplicationCoordinator application)
            { Warehouse = warehouse; Player = player; Plants = plants; Fertilizers = fertilizers; Slot = slot; Materializer = materializer; Dispense = dispense; Application = application; }
            public InventoryAggregate Warehouse { get; } public InventoryAggregate Player { get; } public PlantCatalog Plants { get; }
            public FertilizerCatalog Fertilizers { get; } public PlantingSlot Slot { get; } public Materializer Materializer { get; }
            public FertilizerDispenseCoordinator Dispense { get; } public FertilizerApplicationCoordinator Application { get; }
        }
        private sealed class Materializer : IFertilizerMaterializer
        {
            public Batch Batch { get; } = new Batch();
            public bool TryStage(FertilizerMaterializationRequest request, out IStagedFertilizerItem item) { item = Batch; return true; }
        }
        private sealed class Batch : IStagedFertilizerItem
        {
            public bool ReleaseSucceeds { get; set; } = true;
            public bool CommitSucceeds { get; set; } = true;
            public int CommitCalls { get; private set; }
            public int RollbackCalls { get; private set; }
            public bool TryRelease() => ReleaseSucceeds;
            public bool TryCommit() { CommitCalls++; return CommitSucceeds; }
            public void Rollback() => RollbackCalls++;
        }
        private sealed class Sink : IInventoryEventSink { public bool TryPublish(InventoryChanged inventoryChanged) => true; }
        private sealed class SlotSink : IPlantingSlotEventSink { public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true; }
        private sealed class FakeTime : IPlantGrowthTimeSource { public double Value { get; set; } public double TimeSeconds => Value; }
    }
}
