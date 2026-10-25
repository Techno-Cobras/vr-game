using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Data.Fertilizers;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.DataAssets.Fertilizers;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Harvesting;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.Presentation.Indicators;
using VrGame.VRInteraction.Plants;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.Harvesting;
using VrGame.VRInteraction.SeedStorage;
using VrGame.VRInteraction.Watering;

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

        [Serializable]
        private struct InitialFertilizerStockEntry
        {
            [SerializeField] private FertilizerGrade grade;
            [SerializeField, Min(1)] private int quantity;
            public FertilizerGrade Grade => grade;
            public int Quantity => quantity;
        }

        [SerializeField]
        private ItemCatalogAsset itemCatalog;

        [SerializeField]
        private PlantCatalogAsset plantCatalog;

        [SerializeField]
        private FertilizerCatalogAsset fertilizerCatalog;

        [SerializeField]
        private InitialStockEntry[] initialStock = Array.Empty<InitialStockEntry>();

        [SerializeField]
        private InitialFertilizerStockEntry[] initialFertilizerStock = Array.Empty<InitialFertilizerStockEntry>();

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

        [SerializeField]
        private WateringReceiver wateringReceiver;

        [SerializeField]
        private WateringSource wateringSource;

        [SerializeField]
        private PlantingSlotIndicatorProjectionSource indicatorSource;

        [SerializeField]
        private WorldSpaceIndicatorPresenter indicatorPresenter;

        [SerializeField] private FertilizerItemMaterializer fertilizerMaterializer;
        [SerializeField] private FertilizerContainerController fertilizerController;
        [SerializeField] private MonoBehaviour fertilizerView;
        [SerializeField] private Transform issuedFertilizersRoot;
        [SerializeField] private FertilizerApplicationReceiver fertilizerReceiver;
        [SerializeField] private XRSimpleInteractable harvestInteractable;
        [SerializeField] private HarvestInteractionController harvestController;

        private PlantGrowthController growthController;
        private IFertilizerApplicationView fertilizerApplicationView;

        public SeedDispenseCoordinator SeedStorage { get; private set; }
        public PlantSeedCoordinator SeedPlanting { get; private set; }
        public PlantingSlot PlantingSlot { get; private set; }
        public Inventory WarehouseInventory { get; private set; }
        public Inventory PlayerInventory { get; private set; }
        public Inventory Inventory => WarehouseInventory;
        public SeedStorageStationController StationController => stationController;
        public SeedPlantingReceiver PlantingReceiver => plantingReceiver;
        public PlantGrowthController GrowthController => growthController;
        public WateringReceiver WateringReceiver => wateringReceiver;
        public WateringSource WateringSource => wateringSource;
        public PlantingSlotIndicatorProjectionSource IndicatorSource => indicatorSource;
        public WorldSpaceIndicatorPresenter IndicatorPresenter => indicatorPresenter;
        public FertilizerDispenseCoordinator FertilizerStorage { get; private set; }
        public FertilizerApplicationCoordinator FertilizerApplication { get; private set; }
        public FertilizerContainerController FertilizerController => fertilizerController;
        public FertilizerApplicationReceiver FertilizerReceiver => fertilizerReceiver;
        public HarvestCoordinator Harvesting { get; private set; }
        public HarvestInteractionController HarvestController => harvestController;

        private void Awake()
        {
            var view = stationView as ISeedStorageStationView;
            var fertilizerStationView = fertilizerView as IFertilizerContainerView;
            fertilizerApplicationView = fertilizerView as IFertilizerApplicationView;
            if (itemCatalog == null || plantCatalog == null || fertilizerCatalog == null || materializer == null ||
                stationController == null || view == null || issuedSeedsRoot == null ||
                plantingReceiver == null || plantingSocket == null || wateringReceiver == null ||
                wateringSource == null || indicatorSource == null || indicatorPresenter == null ||
                fertilizerMaterializer == null || fertilizerController == null || fertilizerStationView == null ||
                fertilizerApplicationView == null ||
                issuedFertilizersRoot == null || fertilizerReceiver == null ||
                harvestInteractable == null || harvestController == null)
                throw new InvalidOperationException("Композиция станции саженцев настроена не полностью.");

            var runtimeItems = itemCatalog.Build();
            var runtimePlants = plantCatalog.Build(runtimeItems);
            var runtimeFertilizers = fertilizerCatalog.Build(runtimeItems);
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
            for (var index = 0; index < initialFertilizerStock.Length; index++)
            {
                var entry = initialFertilizerStock[index];
                if (entry.Quantity <= 0 || !runtimeFertilizers.TryGet(entry.Grade, out var definition))
                    throw new InvalidOperationException($"Начальный остаток удобрения #{index + 1} настроен неверно.");
                var result = WarehouseInventory.Add(
                    definition.ItemId,
                    entry.Quantity,
                    new InventoryChangeContext("начальное наполнение ящика удобрений", Guid.NewGuid()));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Не удалось добавить начальный остаток fertilizer grade '{entry.Grade}'.");
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
                indicatorSource,
                new UnityPlantGrowthRandomSource());
            indicatorSource.Configure(PlantingSlot);
            SeedPlanting = new PlantSeedCoordinator(PlayerInventory, runtimePlants.Plants, PlantingSlot);
            plantingReceiver.Configure(plantingSocket, SeedPlanting);
            growthController = plantingReceiver.GetComponent<PlantGrowthController>();
            if (growthController == null)
                throw new InvalidOperationException("Контроллер роста отсутствует на planting slot.");
            growthController.Configure(PlantingSlot, runtimePlants);
            Harvesting = new HarvestCoordinator(PlayerInventory, runtimePlants.Plants, PlantingSlot);
            harvestController.Configure(harvestInteractable, Harvesting, growthController);
            wateringReceiver.Configure(
                wateringReceiver.GetComponent<Collider>(),
                wateringSource,
                PlantingSlot);
            fertilizerMaterializer.Configure(runtimeItems, issuedFertilizersRoot, CleanupIssuedFertilizer);
            FertilizerStorage = new FertilizerDispenseCoordinator(
                WarehouseInventory, PlayerInventory, runtimeFertilizers, fertilizerMaterializer);
            fertilizerController.Configure(FertilizerStorage, fertilizerStationView);
            FertilizerApplication = new FertilizerApplicationCoordinator(
                PlayerInventory, runtimeFertilizers, PlantingSlot);
            fertilizerReceiver.Configure(fertilizerReceiver.GetComponent<Collider>(), FertilizerApplication);
            fertilizerReceiver.ApplicationCompleted += fertilizerApplicationView.RenderApplication;
            indicatorPresenter.Observe(indicatorSource);
        }

        private void OnDestroy()
        {
            if (fertilizerReceiver != null && fertilizerApplicationView != null)
                fertilizerReceiver.ApplicationCompleted -= fertilizerApplicationView.RenderApplication;
        }

        private void CleanupIssuedFertilizer(VrGame.Data.Items.ItemId itemId)
        {
            if (PlayerInventory == null)
                return;

            var result = PlayerInventory.Remove(
                itemId,
                1,
                new InventoryChangeContext("cleanup физического удобрения", Guid.NewGuid()));
            if (!result.Succeeded)
                Debug.LogWarning($"Не удалось согласовать cleanup удобрения '{itemId}': {result.Rejection}.", this);
        }

        private sealed class InventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }

        private sealed class UnityPlantGrowthRandomSource : IPlantGrowthRandomSource
        {
            public float NextUnit() => UnityEngine.Random.value;
        }
    }
}
