using System;
using System.Collections.Generic;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantWaterRequestTests
    {
        private static readonly PlantTypeId PlantId = new PlantTypeId("plant.water-test");
        private static readonly ItemId SeedId = new ItemId("item.seed.water-test");
        private static readonly ItemId HarvestId = new ItemId("item.harvest.water-test");

        [TestCase(float.NaN, .7f)]
        [TestCase(float.PositiveInfinity, .7f)]
        [TestCase(.3f, float.NegativeInfinity)]
        [TestCase(0f, .7f)]
        [TestCase(-.1f, .7f)]
        [TestCase(.7f, .3f)]
        [TestCase(.3f, 1f)]
        public void Range_RejectsNonFiniteAndOutOfBoundsValues(float min, float max)
        {
            var error = Assert.Throws<PlantDataValidationException>(() => new WaterRequestRange(min, max));
            Assert.That(error.Error, Is.EqualTo(PlantDataValidationError.InvalidWaterRequestRange));
        }

        [TestCase(0f, .3f)]
        [TestCase(.5f, .5f)]
        [TestCase(1f, .7f)]
        public void Range_MapsInclusiveUnitSamples(float sample, float expected)
        {
            Assert.That(new WaterRequestRange(.3f, .7f).Select(sample), Is.EqualTo(expected).Within(.00001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-.01f)]
        [TestCase(1.01f)]
        public void Range_RejectsInvalidRandomSamples(float sample)
        {
            Assert.Throws<InvalidOperationException>(() => WaterRequestRange.Default.Select(sample));
        }

        [Test]
        public void PlantDefinition_RejectsDefaultRange()
        {
            var error = Assert.Throws<PlantDataValidationException>(() => new PlantDefinition(
                PlantId,
                new GrowthDuration(10f),
                SeedId,
                HarvestId,
                default,
                new[] { new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.water-test")) }));
            Assert.That(error.Error, Is.EqualTo(PlantDataValidationError.InvalidWaterRequestRange));
        }

        [Test]
        public void AddedEnumValues_PreserveExistingNumericContracts()
        {
            Assert.That((int)PlantDataValidationError.MissingDefinition, Is.EqualTo(3));
            Assert.That((int)PlantDataValidationError.ConflictingRepresentation, Is.EqualTo(16));
            Assert.That((int)PlantDataValidationError.InvalidWaterRequestRange, Is.EqualTo(17));
            Assert.That((int)PlantingSlotMutationKind.RequestWater, Is.EqualTo(3));
            Assert.That((int)PlantingSlotMutationKind.Water, Is.EqualTo(4));
            Assert.That((int)PlantingSlotRejection.AlreadyNeedsWater, Is.EqualTo(6));
            Assert.That((int)PlantingSlotRejection.NotReady, Is.EqualTo(7));
            Assert.That((int)PlantingSlotRejection.CommandConflict, Is.EqualTo(15));
            Assert.That((int)PlantingSlotRejection.AutomaticWaterRequestOnly, Is.EqualTo(16));
        }

        [Test]
        public void LegacyRequestWater_IsAlwaysRejectingExactNoOp()
        {
            var slot = CreateSlot(new SequenceRandomSource(.5f), out var sink);

#pragma warning disable CS0618
            var empty = slot.GetSnapshot();
            var emptyResult = slot.RequestWater(Context("устаревший ручной запрос"));
            AssertExactNoOp(slot, empty, emptyResult, PlantingSlotRejection.AutomaticWaterRequestOnly);

            slot.Plant(PlantId, Context("посадка"));
            var growing = slot.GetSnapshot();
            var growingResult = slot.RequestWater(Context("устаревший ручной запрос"));
            AssertExactNoOp(slot, growing, growingResult, PlantingSlotRejection.AutomaticWaterRequestOnly);

            slot.AdvanceProgress(.5f, slot.Version, Context("автоматический порог"));
            var needsWater = slot.GetSnapshot();
            var repeatedResult = slot.RequestWater(Context("повторный ручной запрос"));
#pragma warning restore CS0618
            AssertExactNoOp(slot, needsWater, repeatedResult, PlantingSlotRejection.AlreadyNeedsWater);
            Assert.That(sink.Events, Has.Count.EqualTo(2));
        }

        [Test]
        public void Plant_SelectsThresholdOnce_AndReplayOrRejectionDoNotDrawAgain()
        {
            var random = new SequenceRandomSource(.25f, .75f);
            var slot = CreateSlot(random, out _);
            var context = Context("посадка");

            var first = slot.Plant(PlantId, context);
            var replay = slot.Plant(PlantId, context);
            var occupied = slot.Plant(PlantId, Context("повторная посадка"));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(occupied.Rejection, Is.EqualTo(PlantingSlotRejection.Occupied));
            Assert.That(random.DrawCount, Is.EqualTo(1));
            Assert.That(slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.4f).Within(.00001f));
        }

        [Test]
        public void Plant_InvalidRandomSampleLeavesSlotUntouchedAndRetryCanSucceed()
        {
            var random = new SequenceRandomSource(float.NaN, .5f);
            var slot = CreateSlot(random, out var sink);
            var context = Context("посадка");

            Assert.Throws<InvalidOperationException>(() => slot.Plant(PlantId, context));
            Assert.That(slot.Version, Is.Zero);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(sink.Events, Is.Empty);

            Assert.That(slot.Plant(PlantId, context).Succeeded, Is.True);
            Assert.That(random.DrawCount, Is.EqualTo(2));
            Assert.That(slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.5f));
        }

        [Test]
        public void AdvanceGrowth_CrossingThreshold_IsOneAtomicExactMutation()
        {
            var slot = CreateSlot(new SequenceRandomSource(.25f), out var sink);
            slot.Plant(PlantId, Context("посадка"));
            var before = slot.GetSnapshot();

            var crossing = slot.AdvanceGrowth(9f, before.Version, Context("крупный шаг роста"));
            var after = slot.GetSnapshot();

            Assert.That(crossing.Succeeded, Is.True);
            Assert.That(after.Version, Is.EqualTo(before.Version + 1));
            Assert.That(after.State, Is.EqualTo(PlantingSlotState.NeedsWater));
            Assert.That(after.NormalizedProgress, Is.EqualTo(.4f).Within(.00001f));
            Assert.That(after.ElapsedGrowthSeconds, Is.EqualTo(4f).Within(.00001f));
            Assert.That(after.WaterRequestOccurred, Is.True);
            Assert.That(after.WaterRequestCompleted, Is.False);
            Assert.That(sink.Events, Has.Count.EqualTo(2));
            Assert.That(crossing.Change.After.State, Is.EqualTo(PlantingSlotState.NeedsWater));
        }

        [Test]
        public void AdvanceGrowth_AtExactThreshold_RequestsWaterAndFreezesUntilValidWater()
        {
            var slot = CreateSlot(new SequenceRandomSource(.5f), out var sink);
            slot.Plant(PlantId, Context("посадка"));
            var crossing = slot.AdvanceProgress(.5f, slot.Version, Context("точный порог"));
            var frozen = slot.GetSnapshot();

            Assert.That(crossing.Change.After.State, Is.EqualTo(PlantingSlotState.NeedsWater));
            var blocked = slot.AdvanceProgress(.9f, slot.Version, Context("рост без воды"));
            Assert.That(blocked.Rejection, Is.EqualTo(PlantingSlotRejection.NotGrowing));
            Assert.That(slot.GetSnapshot().Version, Is.EqualTo(frozen.Version));
            Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(frozen.ElapsedGrowthSeconds));

            var waterContext = Context("полив");
            var watered = slot.Water(waterContext);
            var replay = slot.Water(waterContext);
            var repeated = slot.Water(Context("лишний полив"));
            Assert.That(watered.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(repeated.Rejection, Is.EqualTo(PlantingSlotRejection.WaterNotNeeded));
            Assert.That(slot.GetSnapshot().WaterRequestCompleted, Is.True);

            Assert.That(slot.AdvanceProgress(.9f, slot.Version, Context("рост после полива")).Succeeded, Is.True);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.9f));
            Assert.That(sink.Events, Has.Count.EqualTo(4));
        }

        [Test]
        public void HarvestAndReplant_SelectsNextThresholdForNewCycle()
        {
            var random = new SequenceRandomSource(0f, 1f);
            var slot = CreateSlot(random, out _);
            slot.Plant(PlantId, Context("первая посадка"));
            Assert.That(slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.3f));
            slot.AdvanceProgress(1f, slot.Version, Context("первый рост"));
            slot.Water(Context("первый полив"));
            slot.AdvanceProgress(1f, slot.Version, Context("первое созревание"));
            slot.Harvest(Context("сбор"));

            var empty = slot.GetSnapshot();
            Assert.That(empty.WaterRequestThreshold, Is.Null);
            Assert.That(empty.WaterRequestOccurred, Is.False);
            Assert.That(empty.WaterRequestCompleted, Is.False);

            slot.Plant(PlantId, Context("вторая посадка"));
            Assert.That(slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.7f));
            Assert.That(random.DrawCount, Is.EqualTo(2));
        }

        [Test]
        public void StaleVersionAndDirectOvershoot_CannotBypassWaterBoundary()
        {
            var slot = CreateSlot(new SequenceRandomSource(.5f), out _);
            slot.Plant(PlantId, Context("посадка"));
            var staleVersion = slot.Version;
            slot.ApplyFertilizer(new PlantGrowthModifier(2f), Context("удобрение"));

            var stale = slot.AdvanceGrowth(9f, staleVersion, Context("устаревший рост"));
            Assert.That(stale.Rejection, Is.EqualTo(PlantingSlotRejection.StaleVersion));
            Assert.That(slot.GetSnapshot().NormalizedProgress, Is.Zero);

            slot.AdvanceGrowth(9f, slot.Version, Context("прямой большой шаг"));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.NeedsWater));
            Assert.That(slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.5f));
        }

        [Test]
        public void CoordinatedPlant_DrawsOnceOnSuccessAndNeverOnReplayOrUnavailableSeed()
        {
            var random = new SequenceRandomSource(.5f, .75f);
            var fixture = CreateCoordinator(random, 1);
            var context = new PlantSeedContext("посадка", Guid.NewGuid(), Guid.NewGuid());

            var result = fixture.Coordinator.Plant(PlantId, SeedId, context);
            var replay = fixture.Coordinator.Plant(PlantId, SeedId, context);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(random.DrawCount, Is.EqualTo(1));
            Assert.That(fixture.Slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.5f));

            var unavailableRandom = new SequenceRandomSource(.5f);
            var unavailable = CreateCoordinator(unavailableRandom, 0);
            Assert.That(
                unavailable.Coordinator.Plant(
                    PlantId,
                    SeedId,
                    new PlantSeedContext("нет семян", Guid.NewGuid(), Guid.NewGuid())).Rejection,
                Is.EqualTo(PlantSeedRejection.SeedUnavailable));
            Assert.That(unavailableRandom.DrawCount, Is.Zero);
        }

        [Test]
        public void CoordinatedPlant_OccupiedSlotRejectsBeforeRandomDrawOrInventoryMutation()
        {
            var random = new SequenceRandomSource(.25f, .75f);
            var fixture = CreateCoordinator(random, 2);
            fixture.Slot.Plant(PlantId, Context("первая посадка"));
            var inventoryVersion = fixture.Inventory.Version;

            var result = fixture.Coordinator.Plant(
                PlantId,
                SeedId,
                new PlantSeedContext("посадка в занятую грядку", Guid.NewGuid(), Guid.NewGuid()));

            Assert.That(result.Rejection, Is.EqualTo(PlantSeedRejection.SlotRejected));
            Assert.That(random.DrawCount, Is.EqualTo(1));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(fixture.Inventory.GetQuantity(SeedId), Is.EqualTo(2));
            Assert.That(fixture.Slot.GetSnapshot().WaterRequestThreshold, Is.EqualTo(.4f).Within(.00001f));
        }

        [Test]
        public void CoordinatedPlant_InvalidRandomSampleLeavesInventoryAndSlotUntouched()
        {
            var fixture = CreateCoordinator(new SequenceRandomSource(float.NaN), 1);

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.Plant(
                PlantId,
                SeedId,
                new PlantSeedContext("посадка", Guid.NewGuid(), Guid.NewGuid())));
            Assert.That(fixture.Inventory.GetQuantity(SeedId), Is.EqualTo(1));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(1));
            Assert.That(fixture.Slot.Version, Is.Zero);
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
        }

        private static PlantingSlot CreateSlot(IPlantGrowthRandomSource random, out RecordingSink sink)
        {
            var catalog = CreateCatalog(CreateItemCatalog());
            sink = new RecordingSink();
            return new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), catalog, sink, random);
        }

        private static CoordinatorFixture CreateCoordinator(IPlantGrowthRandomSource random, int seedQuantity)
        {
            var itemCatalog = CreateItemCatalog();
            var catalog = CreateCatalog(itemCatalog);
            var inventory = new InventoryAggregate(InventoryId.New(), itemCatalog, new InventorySink());
            if (seedQuantity > 0)
                inventory.Add(SeedId, seedQuantity, new InventoryChangeContext("наполнение", Guid.NewGuid()));
            var slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), catalog, new RecordingSink(), random);
            return new CoordinatorFixture(inventory, slot, new PlantSeedCoordinator(inventory, catalog, slot));
        }

        private static ItemCatalog CreateItemCatalog() =>
            new ItemCatalog(new[]
            {
                Item(SeedId, ItemCategory.Seed),
                Item(HarvestId, ItemCategory.HarvestedPlant)
            });

        private static PlantCatalog CreateCatalog(ItemCatalog items) =>
            new PlantCatalog(items, new[]
            {
                new PlantDefinition(
                    PlantId,
                    new GrowthDuration(10f),
                    SeedId,
                    HarvestId,
                    new WaterRequestRange(.3f, .7f),
                    new[] { new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.water-test")) })
            });

        private static ItemDefinition Item(ItemId id, ItemCategory category) =>
            new ItemDefinition(
                id,
                id.Value,
                category,
                new ItemRepresentationKey("representation." + id.Value),
                new ItemStackRules(16, false));

        private static PlantingSlotChangeContext Context(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static void AssertExactNoOp(
            PlantingSlot slot,
            PlantingSlotSnapshot before,
            PlantingSlotMutationResult result,
            PlantingSlotRejection expectedRejection)
        {
            var after = slot.GetSnapshot();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(expectedRejection));
            Assert.That(result.Change, Is.Null);
            Assert.That(after.Version, Is.EqualTo(before.Version));
            Assert.That(after.State, Is.EqualTo(before.State));
            Assert.That(after.PlantTypeId, Is.EqualTo(before.PlantTypeId));
            Assert.That(after.ElapsedGrowthSeconds, Is.EqualTo(before.ElapsedGrowthSeconds));
            Assert.That(after.NormalizedProgress, Is.EqualTo(before.NormalizedProgress));
            Assert.That(after.GrowthModifier, Is.EqualTo(before.GrowthModifier));
            Assert.That(after.WaterRequestThreshold, Is.EqualTo(before.WaterRequestThreshold));
            Assert.That(after.WaterRequestOccurred, Is.EqualTo(before.WaterRequestOccurred));
            Assert.That(after.WaterRequestCompleted, Is.EqualTo(before.WaterRequestCompleted));
        }

        private sealed class SequenceRandomSource : IPlantGrowthRandomSource
        {
            private readonly Queue<float> samples;

            public SequenceRandomSource(params float[] samples)
            {
                this.samples = new Queue<float>(samples);
            }

            public int DrawCount { get; private set; }

            public float NextUnit()
            {
                DrawCount++;
                if (samples.Count == 0)
                    throw new InvalidOperationException("Тестовый источник случайности исчерпан.");
                return samples.Dequeue();
            }
        }

        private sealed class RecordingSink : IPlantingSlotEventSink
        {
            public List<PlantingSlotChanged> Events { get; } = new List<PlantingSlotChanged>();
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                Events.Add(plantingSlotChanged);
                return true;
            }
        }

        private sealed class InventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }

        private sealed class CoordinatorFixture
        {
            public CoordinatorFixture(
                InventoryAggregate inventory,
                PlantingSlot slot,
                PlantSeedCoordinator coordinator)
            {
                Inventory = inventory;
                Slot = slot;
                Coordinator = coordinator;
            }

            public InventoryAggregate Inventory { get; }
            public PlantingSlot Slot { get; }
            public PlantSeedCoordinator Coordinator { get; }
        }
    }
}
