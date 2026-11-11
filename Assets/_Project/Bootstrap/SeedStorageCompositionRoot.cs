using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Data.Crafting;
using VrGame.Data.Fertilizers;
using VrGame.Data.Orders;
using VrGame.Data.Plants;
using VrGame.Data.Shop;
using VrGame.DataAssets;
using VrGame.DataAssets.Crafting;
using VrGame.DataAssets.Fertilizers;
using VrGame.DataAssets.Orders;
using VrGame.DataAssets.Plants;
using VrGame.DataAssets.Shop;
using VrGame.Domain.Delivery;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Crafting;
using VrGame.Domain.Economy;
using VrGame.Domain.Harvesting;
using VrGame.Domain.Inventory;
using VrGame.Domain.Orders;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.Domain.Shop;
using VrGame.Presentation.Indicators;
using VrGame.Presentation.Crafting;
using VrGame.Presentation.Orders;
using VrGame.Presentation.Shop;
using VrGame.VRInteraction.Plants;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.Harvesting;
using VrGame.VRInteraction.Orders;
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
        [SerializeField] private RecipeCatalogAsset recipeCatalog;
        [SerializeField] private CraftingTableController craftingController;
        [SerializeField] private MonoBehaviour craftingView;
        [SerializeField] private OrderTemplateCatalogAsset orderTemplateCatalog;
        [SerializeField] private OrderBoardController orderBoardController;
        [SerializeField] private MonoBehaviour orderBoardView;
        [SerializeField] private OrderDeliveryIndicatorProjectionSource deliveryIndicatorSource;
        private OrderDeliveryInteractionController deliveryController;
        [SerializeField, Min(0)] private int startingBalance = 100;
        [SerializeField] private ShopCatalogAsset shopCatalog;
        [SerializeField] private Transform shopTabletAnchor;

        private PlantGrowthController growthController;
        private IFertilizerApplicationView fertilizerApplicationView;
        private InventoryEventRelay playerInventoryEvents;
        private CraftingEventRelay craftingEvents;
        private OrderEventRelay orderEvents;
        private EconomyEventRelay economyEvents;
        private CompositeIndicatorProjectionSource indicatorSources;

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
        public RecipeCatalog Recipes { get; private set; }
        public CraftingService Crafting { get; private set; }
        public CraftingTableController CraftingController => craftingController;
        public OrderTemplateCatalog OrderTemplates { get; private set; }
        public OrderService Orders { get; private set; }
        public OrderBoardController OrderBoardController => orderBoardController;
        public OrderDeliveryIndicatorProjectionSource DeliveryIndicatorSource => deliveryIndicatorSource;
        public OrderDeliveryInteractionController DeliveryController => deliveryController;
        public OrderDeliveryCoordinator OrderDelivery { get; private set; }
        public EconomyService Economy { get; private set; }
        public EconomyEventRelay EconomyEvents => economyEvents;
        public ShopCatalog ShopCatalog { get; private set; }
        public PendingDeliveryLedger Deliveries { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public ShopTabletController ShopTablet { get; private set; }

        private void Awake()
        {
            var view = stationView as ISeedStorageStationView;
            var fertilizerStationView = fertilizerView as IFertilizerContainerView;
            fertilizerApplicationView = fertilizerView as IFertilizerApplicationView;
            var craftingTableView = craftingView as ICraftingTableView;
            var orderView = orderBoardView as IOrderBoardView;
            if (deliveryIndicatorSource != null)
                deliveryController = deliveryIndicatorSource.GetComponentInChildren<OrderDeliveryInteractionController>(true);
            if (itemCatalog == null || plantCatalog == null || fertilizerCatalog == null || materializer == null ||
                stationController == null || view == null || issuedSeedsRoot == null ||
                plantingReceiver == null || plantingSocket == null || wateringReceiver == null ||
                wateringSource == null || indicatorSource == null || indicatorPresenter == null ||
                fertilizerMaterializer == null || fertilizerController == null || fertilizerStationView == null ||
                fertilizerApplicationView == null ||
                issuedFertilizersRoot == null || fertilizerReceiver == null ||
                harvestInteractable == null || harvestController == null ||
                recipeCatalog == null || craftingController == null || craftingTableView == null ||
                orderTemplateCatalog == null || orderBoardController == null || orderView == null ||
                deliveryIndicatorSource == null || deliveryController == null ||
                deliveryController.Interactable == null || shopCatalog == null || shopTabletAnchor == null ||
                startingBalance < 0)
                throw new InvalidOperationException("Композиция станции саженцев настроена не полностью.");

            var runtimeItems = itemCatalog.Build();
            ShopCatalog = shopCatalog.Build(runtimeItems);
            var runtimePlants = plantCatalog.Build(runtimeItems);
            var runtimeFertilizers = fertilizerCatalog.Build(runtimeItems);
            Recipes = recipeCatalog.Build(runtimeItems);
            OrderTemplates = orderTemplateCatalog.Build(runtimeItems);
            playerInventoryEvents = new InventoryEventRelay();
            craftingEvents = new CraftingEventRelay();
            orderEvents = new OrderEventRelay();
            economyEvents = new EconomyEventRelay();
            WarehouseInventory = new Inventory(InventoryId.New(), runtimeItems.Items, new InventorySink());
            PlayerInventory = new Inventory(InventoryId.New(), runtimeItems.Items, playerInventoryEvents);
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
            Crafting = new CraftingService(PlayerInventory, Recipes, craftingEvents);
            craftingController.Configure(
                Crafting,
                Recipes,
                runtimeItems.Items,
                PlayerInventory,
                playerInventoryEvents,
                craftingTableView);
            Orders = new OrderService(OrderTemplates, new SequentialOrderTemplateSelector(), orderEvents);
            Economy = new EconomyService(startingBalance, economyEvents);
            OrderDelivery = new OrderDeliveryCoordinator(PlayerInventory, Orders, Economy);
            Deliveries = new PendingDeliveryLedger();
            Purchases = new PurchaseService(ShopCatalog, Economy, Deliveries);
            ShopTablet = ShopTabletFactory.Create(
                shopTabletAnchor,
                ShopCatalog,
                Economy,
                Purchases,
                economyEvents);
            orderBoardController.Configure(Orders, runtimeItems.Items, orderEvents, orderView);
            deliveryIndicatorSource.Configure(Orders, orderEvents);
            deliveryController.Configure(deliveryController.Interactable, OrderDelivery, Orders);
            for (var index = 0; index < OrderTemplates.Definitions.Count; index++)
            {
                var generated = Orders.Generate(new OrderContext(
                    "Начальная генерация доступного заказа",
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                if (!generated.Succeeded)
                    throw new InvalidOperationException($"Не удалось создать начальный заказ: {generated.Rejection}.");
            }
            indicatorSources = new CompositeIndicatorProjectionSource(indicatorSource, deliveryIndicatorSource);
            indicatorPresenter.Observe(indicatorSources);
        }

        private void OnDestroy()
        {
            if (ShopTablet != null) Destroy(ShopTablet.gameObject);
            if (fertilizerReceiver != null && fertilizerApplicationView != null)
                fertilizerReceiver.ApplicationCompleted -= fertilizerApplicationView.RenderApplication;
            if (indicatorPresenter != null)
                indicatorPresenter.Observe(null);
            indicatorSources?.Dispose();
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

        private sealed class SequentialOrderTemplateSelector : IOrderTemplateSelector
        {
            private int nextIndex;

            public int SelectIndex(int exclusiveMax)
            {
                if (exclusiveMax <= 0)
                    return -1;
                var selected = nextIndex % exclusiveMax;
                nextIndex++;
                return selected;
            }
        }
    }
}
