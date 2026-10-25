using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Harvesting;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Harvesting
{
    public sealed class HarvestCoordinatorTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly ItemId Seed = new ItemId("item.seed.basil");
        private static readonly ItemId Harvest = new ItemId("item.harvest.basil");

        [Test]
        public void LegacyConstructors_DefaultHarvestQuantityToOne()
        {
            var stages = Stages();
            var defaultWater = new PlantDefinition(Basil, new GrowthDuration(10f), Seed, Harvest, stages);
            var explicitWater = new PlantDefinition(
                Basil, new GrowthDuration(10f), Seed, Harvest, new WaterRequestRange(.4f, .6f), stages);

            Assert.That(defaultWater.HarvestQuantity, Is.EqualTo(1));
            Assert.That(explicitWater.HarvestQuantity, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Definition_RejectsNonPositiveHarvestQuantity(int quantity)
        {
            var exception = Assert.Throws<PlantDataValidationException>(() => new PlantDefinition(
                Basil, new GrowthDuration(10f), Seed, Harvest, Stages(), quantity));
            Assert.That(exception.Error, Is.EqualTo(PlantDataValidationError.InvalidHarvestQuantity));
        }

        [Test]
        public void PlantDefinitionAsset_MapsConfiguredHarvestQuantity()
        {
            var seed = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            var harvest = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            var representation = ScriptableObject.CreateInstance<PlantRepresentationAsset>();
            var plant = ScriptableObject.CreateInstance<PlantDefinitionAsset>();
            var prefab = new GameObject("harvest-data-test-prefab");
            try
            {
                SetString(seed, "id", Seed.Value);
                SetString(harvest, "id", Harvest.Value);
                var representationSerialized = new SerializedObject(representation);
                representationSerialized.FindProperty("key").stringValue = "representation.plant.basil";
                representationSerialized.FindProperty("prefab").objectReferenceValue = prefab;
                representationSerialized.ApplyModifiedPropertiesWithoutUndo();

                var serialized = new SerializedObject(plant);
                serialized.FindProperty("id").stringValue = Basil.Value;
                serialized.FindProperty("growthDurationSeconds").floatValue = 10f;
                serialized.FindProperty("waterRequestMinNormalized").floatValue = .4f;
                serialized.FindProperty("waterRequestMaxNormalized").floatValue = .6f;
                serialized.FindProperty("seedItem").objectReferenceValue = seed;
                serialized.FindProperty("harvestItem").objectReferenceValue = harvest;
                serialized.FindProperty("harvestQuantity").intValue = 7;
                var stages = serialized.FindProperty("stages");
                stages.arraySize = 1;
                stages.GetArrayElementAtIndex(0).FindPropertyRelative("startsAtNormalized").floatValue = 0f;
                stages.GetArrayElementAtIndex(0).FindPropertyRelative("representation").objectReferenceValue = representation;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(plant.ToDefinition().HarvestQuantity, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(plant);
                UnityEngine.Object.DestroyImmediate(representation);
                UnityEngine.Object.DestroyImmediate(harvest);
                UnityEngine.Object.DestroyImmediate(seed);
            }
        }

        [Test]
        public void Harvest_ReadyPlantAddsExactConfiguredItemAndQuantityThenResetsSlot()
        {
            var fixture = CreateFixture(3);
            MoveToReady(fixture.Slot);

            var result = fixture.Coordinator.Harvest(Context());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.HarvestItemId, Is.EqualTo(Harvest));
            Assert.That(result.Quantity, Is.EqualTo(3));
            Assert.That(fixture.Inventory.GetQuantity(Harvest), Is.EqualTo(3));
            Assert.That(result.InventoryChange.Changes[0].ItemId, Is.EqualTo(Harvest));
            Assert.That(result.InventoryChange.Changes[0].Delta, Is.EqualTo(3));
            Assert.That(result.SlotChange.Kind, Is.EqualTo(PlantingSlotMutationKind.Harvest));
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(fixture.Slot.GetSnapshot().PlantTypeId, Is.Null);
        }

        [TestCase(PlantingSlotState.Empty)]
        [TestCase(PlantingSlotState.Growing)]
        [TestCase(PlantingSlotState.NeedsWater)]
        public void Harvest_NotReadyPreservesInventoryAndExactSlotRejection(PlantingSlotState state)
        {
            var fixture = CreateFixture(2);
            if (state != PlantingSlotState.Empty)
                fixture.Slot.Plant(Basil, SlotContext());
            if (state == PlantingSlotState.NeedsWater)
                fixture.Slot.AdvanceProgress(.5f, fixture.Slot.Version, SlotContext());
            var version = fixture.Slot.Version;

            var result = fixture.Coordinator.Harvest(Context());

            Assert.That(result.Rejection, Is.EqualTo(HarvestRejection.SlotRejected));
            Assert.That(result.SlotRejection, Is.EqualTo(PlantingSlotRejection.NotReady));
            Assert.That(fixture.Inventory.GetQuantity(Harvest), Is.Zero);
            Assert.That(fixture.Slot.Version, Is.EqualTo(version));
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(state));
        }

        [Test]
        public void FailedInventoryAddPreservesReadyPlantAndReportsExactRejection()
        {
            var fixture = CreateFixture(2);
            fixture.Inventory.Add(Harvest, int.MaxValue, InventoryContext());
            MoveToReady(fixture.Slot);
            var slotVersion = fixture.Slot.Version;

            var result = fixture.Coordinator.Harvest(Context());

            Assert.That(result.Rejection, Is.EqualTo(HarvestRejection.InventoryRejected));
            Assert.That(result.InventoryRejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(result.HarvestItemId, Is.EqualTo(Harvest));
            Assert.That(result.Quantity, Is.EqualTo(2));
            Assert.That(fixture.Inventory.GetQuantity(Harvest), Is.EqualTo(int.MaxValue));
            Assert.That(fixture.Slot.Version, Is.EqualTo(slotVersion));
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
        }

        [Test]
        public void ReplayReturnsOriginalResultWithoutSecondAddOrReset()
        {
            var fixture = CreateFixture(4);
            MoveToReady(fixture.Slot);
            var context = Context();
            var first = fixture.Coordinator.Harvest(context);

            var replay = fixture.Coordinator.Harvest(context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.InventoryChange, Is.SameAs(first.InventoryChange));
            Assert.That(fixture.Inventory.GetQuantity(Harvest), Is.EqualTo(4));
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
        }

        [Test]
        public void ReusedCommandWithDifferentContextIsConflictAndExactNoOp()
        {
            var fixture = CreateFixture(1);
            MoveToReady(fixture.Slot);
            var commandId = Guid.NewGuid();
            var original = new HarvestContext("сбор", commandId, Guid.NewGuid());
            fixture.Coordinator.Harvest(original);
            var inventoryVersion = fixture.Inventory.Version;
            var slotVersion = fixture.Slot.Version;

            var conflict = fixture.Coordinator.Harvest(
                new HarvestContext("другой сбор", commandId, Guid.NewGuid()));

            Assert.That(conflict.Rejection, Is.EqualTo(HarvestRejection.CommandConflict));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(inventoryVersion));
            Assert.That(fixture.Slot.Version, Is.EqualTo(slotVersion));
        }

        [Test]
        public void SuccessfulHarvestAllowsImmediateReplant()
        {
            var fixture = CreateFixture(1);
            MoveToReady(fixture.Slot);
            fixture.Coordinator.Harvest(Context());

            var replant = fixture.Slot.Plant(Basil, SlotContext());

            Assert.That(replant.Succeeded, Is.True);
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
        }

        private static Fixture CreateFixture(int harvestQuantity)
        {
            var items = new ItemCatalog(new[]
            {
                Item(Seed, ItemCategory.Seed),
                Item(Harvest, ItemCategory.HarvestedPlant)
            });
            var plants = new PlantCatalog(items, new[]
            {
                new PlantDefinition(
                    Basil,
                    new GrowthDuration(10f),
                    Seed,
                    Harvest,
                    new WaterRequestRange(.5f, .5f),
                    Stages(),
                    harvestQuantity)
            });
            var inventory = new InventoryAggregate(InventoryId.New(), items, new InventorySink());
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plants,
                new SlotSink(),
                new FixedRandom());
            return new Fixture(inventory, slot, new HarvestCoordinator(inventory, plants, slot));
        }

        private static void MoveToReady(PlantingSlot slot)
        {
            slot.Plant(Basil, SlotContext());
            slot.AdvanceProgress(1f, slot.Version, SlotContext());
            slot.Water(SlotContext());
            slot.AdvanceProgress(1f, slot.Version, SlotContext());
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
        }

        private static PlantStageDefinition[] Stages() => new[]
        {
            new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.basil"))
        };
        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id, id.Value, category, new ItemRepresentationKey("representation." + id.Value), new ItemStackRules(16, true));
        private static HarvestContext Context() => new HarvestContext("сбор урожая", Guid.NewGuid(), Guid.NewGuid());
        private static InventoryChangeContext InventoryContext() => new InventoryChangeContext("наполнение", Guid.NewGuid());
        private static PlantingSlotChangeContext SlotContext() => new PlantingSlotChangeContext("рост", Guid.NewGuid(), Guid.NewGuid());
        private static void SetString(UnityEngine.Object target, string property, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Fixture
        {
            public Fixture(InventoryAggregate inventory, PlantingSlot slot, HarvestCoordinator coordinator)
            { Inventory = inventory; Slot = slot; Coordinator = coordinator; }
            public InventoryAggregate Inventory { get; }
            public PlantingSlot Slot { get; }
            public HarvestCoordinator Coordinator { get; }
        }
        private sealed class InventorySink : IInventoryEventSink { public bool TryPublish(InventoryChanged inventoryChanged) => true; }
        private sealed class SlotSink : IPlantingSlotEventSink { public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true; }
        private sealed class FixedRandom : IPlantGrowthRandomSource { public float NextUnit() => .5f; }
    }
}
