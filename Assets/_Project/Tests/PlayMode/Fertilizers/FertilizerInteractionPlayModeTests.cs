using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Bootstrap;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.VRInteraction.Fertilizers;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.PlayMode.Fertilizers
{
    public sealed class FertilizerInteractionPlayModeTests
    {
        private static readonly ItemId FertilizerItem = new ItemId("item.fertilizer.cheap");
        private static readonly PlantTypeId Plant = new PlantTypeId("plant.test");

        [UnityTest]
        public IEnumerator Startup_DispensesEveryGrade_AppliesHeldItem_AndReconcilesCleanup()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.FertilizerStorage, Is.Not.Null);
            Assert.That(composition.FertilizerController, Is.Not.Null);
            Assert.That(composition.FertilizerReceiver, Is.Not.Null);

            var issued = new FertilizerItemInstance[3];
            var expectedGrades = new[]
            {
                FertilizerGrade.Cheap,
                FertilizerGrade.Medium,
                FertilizerGrade.Expensive
            };
            for (var index = 0; index < expectedGrades.Length; index++)
            {
                var before = composition.FertilizerStorage.GetSnapshot().Entries[index];
                Assert.That(before.Grade, Is.EqualTo(expectedGrades[index]));
                Assert.That(before.Quantity, Is.EqualTo(5));

                var result = composition.FertilizerController.DispenseSelected();
                Assert.That(result.Succeeded, Is.True);
                issued[index] = FindIssued(expectedGrades[index]);
                Assert.That(issued[index], Is.Not.Null);
                Assert.That(issued[index].GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(issued[index].GetComponent<XRGrabInteractable>(), Is.Not.Null);
                Assert.That(issued[index].GetComponent<Collider>(), Is.Not.Null);
                Assert.That(composition.PlayerInventory.GetQuantity(result.ItemId), Is.EqualTo(1));
                Assert.That(composition.FertilizerStorage.GetSnapshot().Entries[index].Quantity, Is.EqualTo(4));
                composition.FertilizerController.SelectNextGrade();
            }

            composition.PlantingSlot.Plant(
                new PlantTypeId("plant.basil"),
                new PlantingSlotChangeContext("playmode fertilizer planting", Guid.NewGuid(), Guid.NewGuid()));

            var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            var interactorObject = new GameObject("Fertilizer test interactor");
            var interactor = interactorObject.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = manager;
            var cheapGrab = issued[0].GetComponent<XRGrabInteractable>();
            manager.SelectEnter((IXRSelectInteractor)interactor, cheapGrab);
            Assert.That(cheapGrab.isSelected, Is.True);

            FertilizerApplicationResult application = null;
            composition.FertilizerReceiver.ApplicationCompleted += result => application = result;
            var receiverCollider = composition.FertilizerReceiver.GetComponent<Collider>();
            var cheapCollider = issued[0].GetComponent<Collider>();
            issued[0].transform.position += receiverCollider.bounds.center - cheapCollider.bounds.center;
            Physics.SyncTransforms();
            composition.FertilizerReceiver.SendMessage("OnTriggerEnter", cheapCollider);

            Assert.That(application, Is.Not.Null);
            Assert.That(application.Succeeded, Is.True);
            Assert.That(issued[0].IsConsumed, Is.True);
            Assert.That(issued[0].gameObject.activeSelf, Is.False);
            Assert.That(composition.PlayerInventory.GetQuantity(new ItemId("item.fertilizer.cheap")), Is.Zero);
            Assert.That(composition.PlantingSlot.GetSnapshot().GrowthModifier.EffectiveMultiplier, Is.EqualTo(1.25f));

            var mediumItemId = new ItemId("item.fertilizer.medium");
            Assert.That(composition.PlayerInventory.GetQuantity(mediumItemId), Is.EqualTo(1));
            var secondInteractorObject = new GameObject("Rejected fertilizer test interactor");
            var secondInteractor = secondInteractorObject.AddComponent<XRDirectInteractor>();
            secondInteractor.interactionManager = manager;
            var mediumGrab = issued[1].GetComponent<XRGrabInteractable>();
            manager.SelectEnter((IXRSelectInteractor)secondInteractor, mediumGrab);
            application = null;
            var mediumCollider = issued[1].GetComponent<Collider>();
            issued[1].transform.position += receiverCollider.bounds.center - mediumCollider.bounds.center;
            Physics.SyncTransforms();
            composition.FertilizerReceiver.SendMessage("OnTriggerEnter", mediumCollider);
            Assert.That(application, Is.Not.Null);
            Assert.That(application.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(application.SlotRejection, Is.EqualTo(PlantingSlotRejection.FertilizerAlreadyApplied));
            Assert.That(issued[1].IsConsumed, Is.False);
            Assert.That(issued[1].gameObject.activeSelf, Is.True);
            Assert.That(composition.PlayerInventory.GetQuantity(mediumItemId), Is.EqualTo(1));

            UnityEngine.Object.Destroy(issued[1].gameObject);
            yield return null;
            Assert.That(composition.PlayerInventory.GetQuantity(mediumItemId), Is.Zero,
                "Unexpected physical cleanup must remove the matching player-inventory ownership.");

            UnityEngine.Object.Destroy(issued[2].gameObject);
            UnityEngine.Object.Destroy(interactorObject);
            UnityEngine.Object.Destroy(secondInteractorObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Adapter_SuccessConsumesPhysicalItemAndSecondItemIsPreserved()
        {
            var fixture = CreateFixture();
            fixture.Slot.Plant(Plant, SlotContext());
            fixture.Inventory.Add(FertilizerItem, 2, InventoryContext());
            var adapter = new FertilizerApplicationAdapter(fixture.Coordinator);
            var firstObject = CreatePhysicalItem();
            var first = firstObject.GetComponent<FertilizerItemInstance>();

            var success = adapter.Apply(first, CommandContext());

            Assert.That(success.Succeeded, Is.True);
            Assert.That(first.IsConsumed, Is.True);
            Assert.That(firstObject.activeSelf, Is.False);
            var secondObject = CreatePhysicalItem();
            var second = secondObject.GetComponent<FertilizerItemInstance>();
            var rejected = adapter.Apply(second, CommandContext());
            Assert.That(rejected.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(second.IsConsumed, Is.False);
            Assert.That(secondObject.activeSelf, Is.True);
            Assert.That(fixture.Inventory.GetQuantity(FertilizerItem), Is.EqualTo(1));
            UnityEngine.Object.Destroy(secondObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Adapter_EmptyTargetPreservesPhysicalItemAndInventory()
        {
            var fixture = CreateFixture();
            fixture.Inventory.Add(FertilizerItem, 1, InventoryContext());
            var gameObject = CreatePhysicalItem();
            var item = gameObject.GetComponent<FertilizerItemInstance>();

            var result = new FertilizerApplicationAdapter(fixture.Coordinator).Apply(item, CommandContext());

            Assert.That(result.Rejection, Is.EqualTo(FertilizerApplicationRejection.SlotRejected));
            Assert.That(item.IsConsumed, Is.False);
            Assert.That(gameObject.activeSelf, Is.True);
            Assert.That(fixture.Inventory.GetQuantity(FertilizerItem), Is.EqualTo(1));
            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        private static GameObject CreatePhysicalItem()
        {
            var gameObject = new GameObject("Test Fertilizer");
            var item = gameObject.AddComponent<FertilizerItemInstance>();
            item.Initialize(FertilizerGrade.Cheap, FertilizerItem);
            return gameObject;
        }

        private static FertilizerItemInstance FindIssued(FertilizerGrade grade)
        {
            foreach (var item in UnityEngine.Object.FindObjectsByType<FertilizerItemInstance>(FindObjectsSortMode.None))
            {
                if (item.IsInitialized && !item.IsConsumed && item.Grade == grade)
                    return item;
            }

            return null;
        }

        private static Fixture CreateFixture()
        {
            var fertilizer = Item(FertilizerItem, ItemCategory.Fertilizer);
            var seed = Item(new ItemId("item.seed.test"), ItemCategory.Seed);
            var harvest = Item(new ItemId("item.harvest.test"), ItemCategory.HarvestedPlant);
            var items = new ItemCatalog(new[] { fertilizer, seed, harvest });
            var plants = new PlantCatalog(items, new[]
            {
                new PlantDefinition(Plant, new GrowthDuration(60f), seed.Id, harvest.Id,
                    new[] { new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.test")) })
            });
            var catalog = new FertilizerCatalog(new[] { new FertilizerDefinition(FertilizerGrade.Cheap, fertilizer, 1.25f) });
            var inventory = new InventoryAggregate(InventoryId.New(), items, new InventorySink());
            var slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), plants, new SlotSink());
            return new Fixture(inventory, slot, new FertilizerApplicationCoordinator(inventory, catalog, slot));
        }

        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id, id.Value, category, new ItemRepresentationKey("representation." + id.Value.Substring(5)), new ItemStackRules(16, true));
        private static FertilizerCommandContext CommandContext() => new FertilizerCommandContext("playmode", Guid.NewGuid(), Guid.NewGuid());
        private static InventoryChangeContext InventoryContext() => new InventoryChangeContext("playmode", Guid.NewGuid());
        private static PlantingSlotChangeContext SlotContext() => new PlantingSlotChangeContext("playmode", Guid.NewGuid(), Guid.NewGuid());

        private sealed class Fixture
        {
            public Fixture(InventoryAggregate inventory, PlantingSlot slot, FertilizerApplicationCoordinator coordinator)
            { Inventory = inventory; Slot = slot; Coordinator = coordinator; }
            public InventoryAggregate Inventory { get; }
            public PlantingSlot Slot { get; }
            public FertilizerApplicationCoordinator Coordinator { get; }
        }
        private sealed class InventorySink : IInventoryEventSink { public bool TryPublish(InventoryChanged inventoryChanged) => true; }
        private sealed class SlotSink : IPlantingSlotEventSink { public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true; }
    }
}
