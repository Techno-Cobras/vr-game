using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.Bootstrap;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.Presentation.SeedStorage;
using VrGame.VRInteraction.SeedStorage;
using Object = UnityEngine.Object;

namespace VrGame.Tests.PlayMode.SeedStorage
{
    public sealed class SeedStoragePlayModeTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly ItemId BasilSeed = new ItemId("item.seed.basil");
        private static readonly ItemId BasilHarvest = new ItemId("item.harvest.basil");

        [UnityTest]
        public IEnumerator SuccessfulDispense_CreatesActiveGrabbableSeedInstances()
        {
            var existingManagers = CaptureInteractionManagers();
            var prefab = CreateSeedPrefab(withGrabInteractable: true);
            var runtimeCatalog = BuildRuntimeCatalog(prefab);
            var plantCatalog = BuildPlantCatalog(runtimeCatalog.Items);
            var inventory = CreateInventory(runtimeCatalog.Items, 2);
            var playerInventory = CreateInventory(runtimeCatalog.Items, 0);
            var host = new GameObject("Тестовый склад");
            var materializer = host.AddComponent<SeedBatchMaterializer>();
            materializer.Configure(runtimeCatalog, host.transform);
            var coordinator = new SeedDispenseCoordinator(inventory, playerInventory, plantCatalog, materializer);

            var result = coordinator.Dispense(Basil, 2, DispenseContext());
            yield return null;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            var seeds = Object.FindObjectsByType<SeedItemInstance>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(seeds, Has.Length.EqualTo(2));
            Assert.That(seeds[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(seeds[0].transform.parent.parent, Is.Null);
            Assert.That(seeds[0].PlantTypeId, Is.EqualTo(Basil));
            Assert.That(seeds[0].SeedItemId, Is.EqualTo(BasilSeed));
            Assert.That(seeds[0].GetComponent<XRGrabInteractable>(), Is.Not.Null);

            host.SetActive(false);
            Assert.That(seeds[0].gameObject.activeInHierarchy, Is.True);

            Object.Destroy(seeds[0].transform.parent.gameObject);
            Object.Destroy(host);
            Object.Destroy(prefab);
            yield return null;
            yield return DestroyNewInteractionManagers(existingManagers);
        }

        [UnityTest]
        public IEnumerator InvalidPhysicalPrefab_DoesNotDecreaseStockOrLeaveInstances()
        {
            var prefab = CreateSeedPrefab(withGrabInteractable: false);
            var runtimeCatalog = BuildRuntimeCatalog(prefab);
            var plantCatalog = BuildPlantCatalog(runtimeCatalog.Items);
            var inventory = CreateInventory(runtimeCatalog.Items, 1);
            var playerInventory = CreateInventory(runtimeCatalog.Items, 0);
            var host = new GameObject("Тестовый склад");
            var materializer = host.AddComponent<SeedBatchMaterializer>();
            materializer.Configure(runtimeCatalog, host.transform);
            var coordinator = new SeedDispenseCoordinator(inventory, playerInventory, plantCatalog, materializer);

            var result = coordinator.Dispense(Basil, 1, DispenseContext());
            yield return null;

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(host.GetComponentsInChildren<SeedItemInstance>(true), Is.Empty);

            Object.Destroy(host);
            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlantingAdapter_ConsumesSeedOnlyAfterSuccessfulPlanting()
        {
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var plantCatalog = BuildPlantCatalog(itemCatalog);
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plantCatalog,
                new AcceptingSlotSink());
            var playerInventory = CreateInventory(itemCatalog, 2);
            var adapter = new SeedPlantingAdapter(
                new PlantSeedCoordinator(playerInventory, plantCatalog, slot));
            var firstObject = new GameObject("Первый саженец");
            var first = firstObject.AddComponent<SeedItemInstance>();
            first.Initialize(Basil, BasilSeed);

            var planted = adapter.Plant(first, PlantingContext("первая посадка"));

            Assert.That(planted.Succeeded, Is.True);
            Assert.That(first.IsConsumed, Is.True);
            Assert.That(slot.GetSnapshot().PlantTypeId, Is.EqualTo(Basil));
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            yield return null;
            Assert.That(first == null, Is.True);

            var rejectedObject = new GameObject("Второй саженец");
            var rejectedSeed = rejectedObject.AddComponent<SeedItemInstance>();
            rejectedSeed.Initialize(Basil, BasilSeed);
            var rejected = adapter.Plant(rejectedSeed, PlantingContext("повторная посадка"));

            Assert.That(rejected.Rejection, Is.EqualTo(PlantSeedRejection.SlotRejected));
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(rejectedSeed.IsConsumed, Is.False);
            Assert.That(rejectedSeed.gameObject, Is.Not.Null);

            Object.Destroy(rejectedObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlantingCommandReplay_DoesNotConsumeAnotherPhysicalSeed()
        {
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var plantCatalog = BuildPlantCatalog(itemCatalog);
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plantCatalog,
                new AcceptingSlotSink());
            var playerInventory = CreateInventory(itemCatalog, 2);
            var adapter = new SeedPlantingAdapter(
                new PlantSeedCoordinator(playerInventory, plantCatalog, slot));
            var context = PlantingContext("идемпотентная посадка");
            var firstObject = new GameObject("Первый саженец");
            var first = firstObject.AddComponent<SeedItemInstance>();
            first.Initialize(Basil, BasilSeed);
            Assert.That(adapter.Plant(first, context).Succeeded, Is.True);
            yield return null;

            var secondObject = new GameObject("Второй саженец");
            var second = secondObject.AddComponent<SeedItemInstance>();
            second.Initialize(Basil, BasilSeed);
            var replay = adapter.Plant(second, context);

            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(second.IsConsumed, Is.False);
            Object.Destroy(secondObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlantingAdapter_RejectsMismatchedSeedIdentityWithoutConsumingIt()
        {
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var plantCatalog = BuildPlantCatalog(itemCatalog);
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plantCatalog,
                new AcceptingSlotSink());
            var playerInventory = CreateInventory(itemCatalog, 1);
            var seedObject = new GameObject("Саженец с неверной идентичностью");
            var seed = seedObject.AddComponent<SeedItemInstance>();
            seed.Initialize(Basil, BasilHarvest);

            var result = new SeedPlantingAdapter(
                new PlantSeedCoordinator(playerInventory, plantCatalog, slot)).Plant(
                seed,
                PlantingContext("проверка идентичности"));

            Assert.That(result.Rejection, Is.EqualTo(PlantSeedRejection.SeedIdentityMismatch));
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(seed.IsConsumed, Is.False);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            Object.Destroy(seedObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StationController_RendersSelectionAndDispensesOneIntentOnlyOnce()
        {
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var inventory = CreateInventory(itemCatalog, 3);
            var playerInventory = CreateInventory(itemCatalog, 0);
            var materializer = new CountingMaterializer();
            var coordinator = new SeedDispenseCoordinator(
                inventory,
                playerInventory,
                BuildPlantCatalog(itemCatalog),
                materializer);
            var host = new GameObject("Контроллер станции");
            var controller = host.AddComponent<SeedStorageStationController>();
            var view = new RecordingStationView();
            controller.Configure(coordinator, view);

            Assert.That(view.LastSnapshot.Entries[0].Quantity, Is.EqualTo(3));
            controller.SetQuantity(2f);
            var first = controller.DispenseSelected();
            var duplicateCallback = controller.DispenseSelected();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(duplicateCallback.IsReplay, Is.True);
            Assert.That(materializer.StageCalls, Is.EqualTo(1));
            Assert.That(inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(view.LastSnapshot.Entries[0].Quantity, Is.EqualTo(1));
            Assert.That(view.LastQuantity, Is.EqualTo(2));

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StationController_DisablePauseAndFocusLossBlockCommands()
        {
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var inventory = CreateInventory(itemCatalog, 3);
            var playerInventory = CreateInventory(itemCatalog, 0);
            var coordinator = new SeedDispenseCoordinator(
                inventory,
                playerInventory,
                BuildPlantCatalog(itemCatalog),
                new CountingMaterializer());
            var host = new GameObject("Контроллер станции");
            var controller = host.AddComponent<SeedStorageStationController>();
            controller.Configure(coordinator, new RecordingStationView());

            controller.SendMessage("OnApplicationPause", true);
            Assert.That(controller.DispenseSelected(), Is.Null);
            controller.SendMessage("OnApplicationPause", false);
            controller.SendMessage("OnApplicationFocus", false);
            Assert.That(controller.DispenseSelected(), Is.Null);
            controller.SendMessage("OnApplicationFocus", true);
            controller.enabled = false;
            Assert.That(controller.DispenseSelected(), Is.Null);
            Assert.That(inventory.GetQuantity(BasilSeed), Is.EqualTo(3));

            controller.enabled = true;
            Assert.That(controller.DispenseSelected().Succeeded, Is.True);
            Assert.That(inventory.GetQuantity(BasilSeed), Is.EqualTo(2));

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TextView_DisplaysPlantQuantitiesSelectionAndDispenseStatus()
        {
            var stockObject = new GameObject("Остатки");
            var selectionObject = new GameObject("Выбор");
            var statusObject = new GameObject("Статус");
            var viewObject = new GameObject("Представление станции");
            var stock = stockObject.AddComponent<Text>();
            var selection = selectionObject.AddComponent<Text>();
            var status = statusObject.AddComponent<Text>();
            var view = viewObject.AddComponent<SeedStorageStationTextView>();
            view.Configure(stock, selection, status);
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var inventory = CreateInventory(itemCatalog, 2);
            var playerInventory = CreateInventory(itemCatalog, 0);
            var controller = viewObject.AddComponent<SeedStorageStationController>();
            controller.Configure(
                new SeedDispenseCoordinator(
                    inventory,
                    playerInventory,
                    BuildPlantCatalog(itemCatalog),
                    new CountingMaterializer()),
                view);

            controller.SetQuantity(3f);
            var rejected = controller.DispenseSelected();

            Assert.That(rejected.Rejection, Is.EqualTo(SeedDispenseRejection.InsufficientStock));
            Assert.That(stock.text, Does.Contain("plant.basil: 2"));
            Assert.That(selection.text, Does.Contain("Количество: 3"));
            Assert.That(status.text, Does.Contain("Недостаточно саженцев"));

            Object.Destroy(stockObject);
            Object.Destroy(selectionObject);
            Object.Destroy(statusObject);
            Object.Destroy(viewObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlantingReceiver_SubscribesByLifecycleAndConsumesOnceOnSocketSelection()
        {
            var existingManagers = CaptureInteractionManagers();
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var plantCatalog = BuildPlantCatalog(itemCatalog);
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plantCatalog,
                new AcceptingSlotSink());
            var playerInventory = CreateInventory(itemCatalog, 1);
            var socketObject = new GameObject("Гнездо посадки");
            socketObject.SetActive(false);
            var socket = socketObject.AddComponent<XRSocketInteractor>();
            var receiver = socketObject.AddComponent<SeedPlantingReceiver>();
            receiver.Configure(
                socket,
                new PlantSeedCoordinator(playerInventory, plantCatalog, slot));
            socketObject.SetActive(true);
            var seedObject = CreateSeedPrefab(withGrabInteractable: true);
            var seed = seedObject.AddComponent<SeedItemInstance>();
            seed.Initialize(Basil, BasilSeed);
            seedObject.SetActive(true);
            var grab = seedObject.GetComponent<XRGrabInteractable>();
            var foreignObject = CreateSeedPrefab(withGrabInteractable: true);
            foreignObject.SetActive(true);
            var foreignGrab = foreignObject.GetComponent<XRGrabInteractable>();
            Assert.That(receiver.Process(socket, foreignGrab), Is.False);
            Assert.That(receiver.Process(socket, grab), Is.True);
            var args = new SelectEnterEventArgs
            {
                interactorObject = socket,
                interactableObject = grab
            };
            var completionCount = 0;
            receiver.PlantingCompleted += _ => completionCount++;

            socket.selectEntered.Invoke(args);
            socket.selectEntered.Invoke(args);

            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(playerInventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(seed.IsConsumed, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
            yield return null;

            Object.Destroy(socketObject);
            Object.Destroy(foreignObject);
            yield return null;
            yield return DestroyNewInteractionManagers(existingManagers);
        }

        [UnityTest]
        public IEnumerator PlantingReceiver_DisableAndPauseRemoveOrGateSocketHandling()
        {
            var existingManagers = CaptureInteractionManagers();
            var itemCatalog = BuildItemCatalogWithoutAssets();
            var plantCatalog = BuildPlantCatalog(itemCatalog);
            var slot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                plantCatalog,
                new AcceptingSlotSink());
            var playerInventory = CreateInventory(itemCatalog, 1);
            var socketObject = new GameObject("Гнездо посадки");
            socketObject.SetActive(false);
            var socket = socketObject.AddComponent<XRSocketInteractor>();
            var receiver = socketObject.AddComponent<SeedPlantingReceiver>();
            receiver.Configure(
                socket,
                new PlantSeedCoordinator(playerInventory, plantCatalog, slot));
            socketObject.SetActive(true);
            var seedObject = CreateSeedPrefab(withGrabInteractable: true);
            var seed = seedObject.AddComponent<SeedItemInstance>();
            seed.Initialize(Basil, BasilSeed);
            seedObject.SetActive(true);
            var args = new SelectEnterEventArgs
            {
                interactorObject = socket,
                interactableObject = seedObject.GetComponent<XRGrabInteractable>()
            };

            receiver.enabled = false;
            socket.selectEntered.Invoke(args);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            receiver.enabled = true;
            receiver.SendMessage("OnApplicationPause", true);
            socket.selectEntered.Invoke(args);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            receiver.SendMessage("OnApplicationPause", false);
            socket.selectEntered.Invoke(args);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            yield return null;

            Object.Destroy(socketObject);
            yield return null;
            yield return DestroyNewInteractionManagers(existingManagers);
        }

        [UnityTest]
        public IEnumerator StartupScene_HasSingleConfiguredSeedStorageComposition()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Startup");
            yield return null;
            var compositions = Object.FindObjectsByType<SeedStorageCompositionRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.That(compositions, Has.Length.EqualTo(1));
            var composition = compositions[0];
            Assert.That(composition.SeedStorage, Is.Not.Null);
            Assert.That(composition.SeedPlanting, Is.Not.Null);
            Assert.That(composition.PlantingSlot, Is.Not.Null);
            Assert.That(composition.WarehouseInventory, Is.Not.Null);
            Assert.That(composition.PlayerInventory, Is.Not.Null);
            Assert.That(composition.StationController, Is.Not.Null);
            Assert.That(composition.PlantingReceiver, Is.Not.Null);
            Assert.That(composition.StationController.Snapshot.Entries, Has.Count.EqualTo(2));
            Assert.That(composition.StationController.Snapshot.Entries[0].Quantity, Is.EqualTo(5));
            Assert.That(composition.StationController.Snapshot.Entries[1].Quantity, Is.EqualTo(5));

            var dispense = composition.StationController.DispenseSelected();
            Assert.That(dispense.Succeeded, Is.True);
            Assert.That(composition.WarehouseInventory.GetQuantity(BasilSeed), Is.EqualTo(4));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            var issuedSeed = Object.FindFirstObjectByType<SeedItemInstance>(FindObjectsInactive.Exclude);
            Assert.That(issuedSeed, Is.Not.Null);

            var planted = new SeedPlantingAdapter(composition.SeedPlanting).Plant(
                issuedSeed,
                PlantingContext("сквозной тест Startup"));
            Assert.That(planted.Succeeded, Is.True);
            Assert.That(composition.PlayerInventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(composition.PlantingSlot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            yield return null;
        }

        private static GameObject CreateSeedPrefab(bool withGrabInteractable)
        {
            var prefab = new GameObject("Саженец-prefab");
            prefab.SetActive(false);
            prefab.AddComponent<Rigidbody>();
            prefab.AddComponent<BoxCollider>();
            if (withGrabInteractable)
                prefab.AddComponent<XRGrabInteractable>();
            return prefab;
        }

        private static HashSet<int> CaptureInteractionManagers()
        {
            var ids = new HashSet<int>();
            foreach (var manager in Object.FindObjectsByType<XRInteractionManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                ids.Add(manager.GetInstanceID());
            return ids;
        }

        private static IEnumerator DestroyNewInteractionManagers(ISet<int> existingIds)
        {
            foreach (var manager in Object.FindObjectsByType<XRInteractionManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!existingIds.Contains(manager.GetInstanceID()))
                    Object.Destroy(manager.gameObject);
            }

            yield return null;
        }

        private static RuntimeItemCatalog BuildRuntimeCatalog(GameObject prefab)
        {
            var representation = ScriptableObject.CreateInstance<ItemRepresentationAsset>();
            SetField(representation, "key", "representation.seed.basil");
            SetField(representation, "prefab", prefab);

            var seed = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            SetField(seed, "id", BasilSeed.Value);
            SetField(seed, "displayName", "Базилик");
            SetField(seed, "category", ItemCategory.Seed);
            SetField(seed, "representation", representation);
            SetField(seed, "maxStackSize", 16);
            SetField(seed, "allowsStackRepresentation", true);

            var harvestRepresentation = ScriptableObject.CreateInstance<ItemRepresentationAsset>();
            SetField(harvestRepresentation, "key", "representation.harvest.basil");
            SetField(harvestRepresentation, "prefab", prefab);
            var harvest = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            SetField(harvest, "id", BasilHarvest.Value);
            SetField(harvest, "displayName", "Урожай базилика");
            SetField(harvest, "category", ItemCategory.HarvestedPlant);
            SetField(harvest, "representation", harvestRepresentation);
            SetField(harvest, "maxStackSize", 16);
            SetField(harvest, "allowsStackRepresentation", true);

            var catalog = ScriptableObject.CreateInstance<ItemCatalogAsset>();
            SetField(catalog, "definitions", new[] { seed, harvest });
            return catalog.Build();
        }

        private static ItemCatalog BuildItemCatalogWithoutAssets() => new ItemCatalog(new[]
        {
            new ItemDefinition(
                BasilSeed,
                "Базилик",
                ItemCategory.Seed,
                new ItemRepresentationKey("representation.seed.basil"),
                new ItemStackRules(16, true)),
            new ItemDefinition(
                BasilHarvest,
                "Урожай базилика",
                ItemCategory.HarvestedPlant,
                new ItemRepresentationKey("representation.harvest.basil"),
                new ItemStackRules(16, true))
        });

        private static PlantCatalog BuildPlantCatalog(ItemCatalog itemCatalog) => new PlantCatalog(
            itemCatalog,
            new[]
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

        private static Inventory CreateInventory(ItemCatalog itemCatalog, int quantity)
        {
            var inventory = new Inventory(InventoryId.New(), itemCatalog, new AcceptingInventorySink());
            if (quantity > 0)
                inventory.Add(
                    BasilSeed,
                    quantity,
                    new InventoryChangeContext("начальное наполнение", Guid.NewGuid()));
            return inventory;
        }

        private static SeedDispenseContext DispenseContext() =>
            new SeedDispenseContext("VR-выдача", Guid.NewGuid(), Guid.NewGuid());

        private static PlantSeedContext PlantingContext(string reason) =>
            new PlantSeedContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Поле '{name}' не найдено.");
            field.SetValue(target, value);
        }

        private sealed class AcceptingInventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }

        private sealed class AcceptingSlotSink : IPlantingSlotEventSink
        {
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true;
        }

        private sealed class CountingMaterializer : ISeedDispenseMaterializer
        {
            public int StageCalls { get; private set; }

            public bool TryStage(SeedMaterializationRequest request, out IStagedSeedBatch batch)
            {
                StageCalls++;
                batch = new NoOpBatch();
                return true;
            }
        }

        private sealed class NoOpBatch : IStagedSeedBatch
        {
            public bool TryRelease()
            {
                return true;
            }

            public void Commit()
            {
            }

            public void Rollback()
            {
            }
        }

        private sealed class RecordingStationView : ISeedStorageStationView
        {
            public SeedStorageSnapshot LastSnapshot { get; private set; }
            public int LastQuantity { get; private set; }

            public void Render(
                SeedStorageSnapshot snapshot,
                int selectedIndex,
                int requestedQuantity,
                SeedDispenseResult lastResult)
            {
                LastSnapshot = snapshot;
                LastQuantity = requestedQuantity;
            }
        }
    }
}
