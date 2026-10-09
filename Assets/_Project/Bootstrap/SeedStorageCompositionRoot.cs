using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.VRInteraction.SeedStorage;

namespace VrGame.Bootstrap
{
    public sealed class SeedStorageCompositionRoot : MonoBehaviour
    {
        [Serializable]
        private struct InitialStockEntry
        {
            [SerializeField]
            private string plantTypeId;

            [SerializeField, Min(1)]
            private int quantity;

            public string PlantTypeId => plantTypeId;
            public int Quantity => quantity;
        }

        [SerializeField]
        private ItemCatalogAsset itemCatalog;

        [SerializeField]
        private PlantCatalogAsset plantCatalog;

        [SerializeField]
        private InitialStockEntry[] initialStock = Array.Empty<InitialStockEntry>();

        [SerializeField, Min(1)]
        private int maxDispenseBatch = 5;

        [SerializeField]
        private SeedBatchMaterializer materializer;

        [SerializeField]
        private SeedStorageStationController stationController;

        [SerializeField]
        private MonoBehaviour stationView;

        [SerializeField]
        private Transform issuedSeedsRoot;

        [SerializeField]
        private SeedPlantingReceiver plantingReceiver;

        [SerializeField]
        private XRSocketInteractor plantingSocket;

        public SeedDispenseCoordinator SeedStorage { get; private set; }
        public PlantSeedCoordinator SeedPlanting { get; private set; }
        public PlantingSlot PlantingSlot { get; private set; }
        public Inventory WarehouseInventory { get; private set; }
        public Inventory PlayerInventory { get; private set; }
        public Inventory Inventory => WarehouseInventory;
        public SeedStorageStationController StationController => stationController;
        public SeedPlantingReceiver PlantingReceiver => plantingReceiver;

        private void Awake()
        {
            var view = stationView as ISeedStorageStationView;
            if (itemCatalog == null || plantCatalog == null || materializer == null ||
                stationController == null || view == null || issuedSeedsRoot == null ||
                plantingReceiver == null || plantingSocket == null)
                throw new InvalidOperationException("Композиция станции саженцев настроена не полностью.");

            var runtimeItems = itemCatalog.Build();
            var runtimePlants = plantCatalog.Build(runtimeItems);
            WarehouseInventory = new Inventory(InventoryId.New(), runtimeItems.Items, new InventorySink());
            PlayerInventory = new Inventory(InventoryId.New(), runtimeItems.Items, new InventorySink());
            for (var index = 0; index < initialStock.Length; index++)
            {
                var entry = initialStock[index];
                if (!PlantTypeId.TryParse(entry.PlantTypeId, out var plantId) || entry.Quantity <= 0)
                    throw new InvalidOperationException($"Начальный остаток #{index + 1} настроен неверно.");
                var definition = runtimePlants.Plants.GetRequired(plantId);
                var result = WarehouseInventory.Add(
                    definition.SeedItemId,
                    entry.Quantity,
                    new InventoryChangeContext("начальное наполнение станции саженцев", Guid.NewGuid()));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Не удалось добавить начальный остаток '{plantId}'.");
            }

            materializer.Configure(runtimeItems, issuedSeedsRoot);
            SeedStorage = new SeedDispenseCoordinator(
                WarehouseInventory,
                PlayerInventory,
                runtimePlants.Plants,
                materializer,
                maxDispenseBatch);
            stationController.Configure(SeedStorage, view);

            PlantingSlot = new PlantingSlot(
                new PlantingSlotId(Guid.NewGuid()),
                runtimePlants.Plants,
                new PlantingSink());
            SeedPlanting = new PlantSeedCoordinator(PlayerInventory, runtimePlants.Plants, PlantingSlot);
            plantingReceiver.Configure(plantingSocket, SeedPlanting);
        }

        private sealed class InventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }

        private sealed class PlantingSink : IPlantingSlotEventSink
        {
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true;
        }
    }
}
