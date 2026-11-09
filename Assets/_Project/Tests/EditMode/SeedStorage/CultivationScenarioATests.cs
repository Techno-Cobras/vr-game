using System;
using NUnit.Framework;
using UnityEngine;
using VrGame.Bootstrap;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Harvesting;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.Presentation.Indicators;
using VrGame.VRInteraction.Watering;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.SeedStorage
{
    public sealed class CultivationScenarioATests
    {
        private static readonly PlantTypeId Plant = new PlantTypeId("plant.scenario-a");
        private static readonly ItemId Seed = new ItemId("item.seed.scenario-a");
        private static readonly ItemId Harvest = new ItemId("item.harvest.scenario-a");
        private static readonly ItemId Fertilizer = new ItemId("item.fertilizer.scenario-a");

        [Test]
        public void ScenarioA_StorageToHarvest_PreservesOwnershipAndResumesGrowth()
        {
            var root = new GameObject("scenario-a");
            try
            {
                var fertilizerItem = Item(Fertilizer, ItemCategory.Fertilizer);
                var items = new ItemCatalog(new[]
                {
                    Item(Seed, ItemCategory.Seed), Item(Harvest, ItemCategory.HarvestedPlant), fertilizerItem
                });
                var plants = new PlantCatalog(items, new[]
                {
                    new PlantDefinition(Plant, new GrowthDuration(20f), Seed, Harvest,
                        new WaterRequestRange(.5f, .5f), new[]
                        {
                            new PlantStageDefinition(0f, new PlantRepresentationKey("representation.scenario-a"))
                        }, 3)
                });
                var fertilizers = new FertilizerCatalog(new[]
                {
                    new FertilizerDefinition(FertilizerGrade.Expensive, fertilizerItem, 2f)
                });
                var warehouse = new InventoryAggregate(InventoryId.New(), items, new InventorySink());
                var player = new InventoryAggregate(InventoryId.New(), items, new InventorySink());
                Assert.That(warehouse.Add(Seed, 2, InventoryContext()).Succeeded, Is.True);
                Assert.That(warehouse.Add(Fertilizer, 2, InventoryContext()).Succeeded, Is.True);
                var materializer = new Materializer();
                var dispense = new SeedDispenseCoordinator(warehouse, player, plants, materializer);
                var projection = root.AddComponent<PlantingSlotIndicatorProjectionSource>();
                var target = root.AddComponent<IndicatorTarget>();
                var slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), plants, projection, new FixedRandom());
                projection.Configure(slot, target);
                var planting = new PlantSeedCoordinator(player, plants, slot);
                var harvest = new HarvestCoordinator(player, plants, slot);
                var application = new FertilizerApplicationCoordinator(player, fertilizers, slot);
                var watering = new WateringAdapter(slot);
                var time = new Clock();
                var timer = new PlantGrowthTimer(slot, plants, time);

                Assert.That(harvest.Harvest(HarvestContext()).Succeeded, Is.False);
                Assert.That(watering.Water(Guid.NewGuid()).Succeeded, Is.False);
                var seedCommand = new SeedDispenseContext("scenario A", Guid.NewGuid(), Guid.NewGuid());
                Assert.That(dispense.Dispense(Plant, 2, seedCommand).Succeeded, Is.True);
                Assert.That(dispense.Dispense(Plant, 2, seedCommand).IsReplay, Is.True);
                Assert.That(dispense.Dispense(Plant, 1,
                    new SeedDispenseContext("empty stock", Guid.NewGuid(), Guid.NewGuid())).Rejection,
                    Is.EqualTo(SeedDispenseRejection.InsufficientStock));
                Assert.That(materializer.SeedStages, Is.EqualTo(1));
                Assert.That(warehouse.GetQuantity(Seed), Is.Zero);
                Assert.That(player.GetQuantity(Seed), Is.EqualTo(2));

                var plantCommand = new PlantSeedContext("scenario A", Guid.NewGuid(), Guid.NewGuid());
                Assert.That(planting.Plant(Plant, Seed, plantCommand).Succeeded, Is.True);
                Assert.That(planting.Plant(Plant, Seed, plantCommand).IsReplay, Is.True);
                var occupiedVersion = slot.Version;
                var occupied = planting.Plant(Plant, Seed,
                    new PlantSeedContext("occupied", Guid.NewGuid(), Guid.NewGuid()));
                Assert.That(occupied.Rejection, Is.EqualTo(PlantSeedRejection.SlotRejected));
                Assert.That(player.GetQuantity(Seed), Is.EqualTo(1));
                Assert.That(slot.Version, Is.EqualTo(occupiedVersion));
                Assert.That(harvest.Harvest(HarvestContext()).Succeeded, Is.False);
                Assert.That(player.GetQuantity(Harvest), Is.Zero);

                timer.Resume();
                time.TimeSeconds = 2;
                timer.Tick();
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(2f));
                var fertilizerDispense = new FertilizerDispenseCoordinator(warehouse, player, fertilizers, materializer);
                Assert.That(fertilizerDispense.Dispense(FertilizerGrade.Expensive, FertilizerContext()).Succeeded, Is.True);
                Assert.That(fertilizerDispense.Dispense(FertilizerGrade.Expensive, FertilizerContext()).Succeeded, Is.True);
                var fertilizerCommand = FertilizerContext();
                Assert.That(application.Apply(FertilizerGrade.Expensive, Fertilizer, fertilizerCommand).Succeeded, Is.True);
                Assert.That(application.Apply(FertilizerGrade.Expensive, Fertilizer, fertilizerCommand).IsReplay, Is.True);
                Assert.That(application.Apply(FertilizerGrade.Expensive, Fertilizer, FertilizerContext()).SlotRejection,
                    Is.EqualTo(PlantingSlotRejection.FertilizerAlreadyApplied));
                Assert.That(player.GetQuantity(Fertilizer), Is.EqualTo(1));
                time.TimeSeconds = 3;
                timer.Tick(); // delta > 0 observes modifier/version; no elapsed credit
                time.TimeSeconds = 4;
                timer.Tick();
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(4f)); // 1 second -> 2 growth seconds
                time.TimeSeconds = 7;
                timer.Tick();
                Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.NeedsWater));
                Assert.That(slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.5f));
                Assert.That(projection.GetSnapshot()[0].Visible, Is.True);
                var pausedVersion = slot.Version;
                time.TimeSeconds = 107;
                timer.Tick();
                Assert.That(slot.Version, Is.EqualTo(pausedVersion));
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(10f));
                Assert.That(harvest.Harvest(HarvestContext()).Succeeded, Is.False);
                var waterCommand = Guid.NewGuid();
                Assert.That(watering.Water(waterCommand).Succeeded, Is.True);
                Assert.That(watering.Water(waterCommand).IsReplay, Is.True);
                Assert.That(watering.Water(Guid.NewGuid()).Succeeded, Is.False);
                Assert.That(projection.GetSnapshot()[0].Visible, Is.False);
                time.TimeSeconds = 207;
                timer.Tick(); // resume baseline discards all time spent waiting for water
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(10f));
                time.TimeSeconds = 208;
                timer.Tick();
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(12f));
                timer.Pause();
                time.TimeSeconds = 308;
                Assert.That(timer.Tick(), Is.Null);
                timer.Resume();
                Assert.That(slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(12f));
                time.TimeSeconds = 312;
                timer.Tick();
                Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
                Assert.That(projection.GetSnapshot()[1].Visible, Is.True);

                var harvestCommand = HarvestContext();
                Assert.That(harvest.Harvest(harvestCommand).Succeeded, Is.True);
                Assert.That(harvest.Harvest(harvestCommand).IsReplay, Is.True);
                Assert.That(harvest.Harvest(HarvestContext()).Succeeded, Is.False);
                Assert.That(player.GetQuantity(Harvest), Is.EqualTo(3));
                Assert.That(player.GetQuantity(Seed), Is.EqualTo(1));
                Assert.That(player.GetQuantity(Fertilizer), Is.EqualTo(1));
                Assert.That(warehouse.GetQuantity(Fertilizer), Is.Zero);
                var reset = slot.GetSnapshot();
                Assert.That(reset.State, Is.EqualTo(PlantingSlotState.Empty));
                Assert.That(reset.PlantTypeId, Is.Null);
                Assert.That(reset.ElapsedGrowthSeconds, Is.Zero);
                Assert.That(reset.NormalizedProgress, Is.Zero);
                Assert.That(reset.GrowthModifier.EffectiveMultiplier, Is.EqualTo(1f));
                Assert.That(reset.WaterRequestThreshold, Is.Null);
                Assert.That(reset.WaterRequestOccurred, Is.False);
                Assert.That(reset.WaterRequestCompleted, Is.False);
                Assert.That(projection.GetSnapshot()[0].Visible, Is.False);
                Assert.That(projection.GetSnapshot()[1].Visible, Is.False);
                Assert.That(planting.Plant(Plant, Seed,
                    new PlantSeedContext("next cycle", Guid.NewGuid(), Guid.NewGuid())).Succeeded, Is.True);
                Assert.That(player.GetQuantity(Seed), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id, id.Value, category, new ItemRepresentationKey("representation." + id.Value), new ItemStackRules(16, true));
        private static InventoryChangeContext InventoryContext() => new InventoryChangeContext("scenario A", Guid.NewGuid());
        private static HarvestContext HarvestContext() => new HarvestContext("scenario A", Guid.NewGuid(), Guid.NewGuid());
        private static FertilizerCommandContext FertilizerContext() => new FertilizerCommandContext("scenario A", Guid.NewGuid(), Guid.NewGuid());
        private sealed class Clock : IPlantGrowthTimeSource { public double TimeSeconds { get; set; } }
        private sealed class FixedRandom : IPlantGrowthRandomSource { public float NextUnit() => .5f; }
        private sealed class InventorySink : IInventoryEventSink { public bool TryPublish(InventoryChanged change) => true; }

        // Physical spawning is covered by PlayMode tests and the manual Quest scenario.
        private sealed class Materializer : ISeedDispenseMaterializer, IFertilizerMaterializer
        {
            public int SeedStages { get; private set; }
            public bool TryStage(SeedMaterializationRequest request, out IStagedSeedBatch batch)
            { SeedStages++; batch = new SeedBatch(); return true; }
            public bool TryStage(FertilizerMaterializationRequest request, out IStagedFertilizerItem item)
            { item = new FertilizerItem(); return true; }
        }
        private sealed class SeedBatch : IStagedSeedBatch
        { public bool TryRelease() => true; public void Commit() { } public void Rollback() { } }
        private sealed class FertilizerItem : IStagedFertilizerItem
        { public bool TryRelease() => true; public bool TryCommit() => true; public void Rollback() { } }
    }
}
