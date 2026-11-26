using System;
using System.Collections;
using System.Linq;
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
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.Domain.Fertilizers;
using VrGame.Domain.Plants;
using VrGame.Domain.SeedStorage;
using VrGame.VRInteraction.Delivery;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.SeedStorage;
using VrGame.VRInteraction.Watering;

namespace VrGame.Tests.PlayMode.Integration
{
    public sealed class GameplayLoopScenarioEPlayModeTests
    {
        private const string BasilSeedProductId = "product.basilseed";
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly ItemId BasilSeed = new ItemId("item.seed.basil");
        private static readonly ItemId BasilHarvest = new ItemId("item.harvest.basil");
        private static readonly ItemId BasilTonic = new ItemId("item.medicine.basil-tonic");

        [UnityTest]
        public IEnumerator ScenarioE_CompletesFullLoopAndStartsSecondCycleWithPurchasedSeed()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(manager, Is.Not.Null);

            AssertBoundary(composition, 5, 0, 5, 0, 0, 0, 100);
            Assert.That(composition.PlantingSlot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Is.Empty);

            composition.StationController.SelectPlant(Basil.Value);
            var dispenseSeed = composition.StationController.DispenseSelected();
            Assert.That(dispenseSeed.Succeeded, Is.True);
            AssertBoundary(composition, 4, 1, 5, 0, 0, 0, 100);
            var firstSeed = FindSeed(BasilSeed);

            var plant = new SeedPlantingAdapter(composition.SeedPlanting).Plant(
                firstSeed,
                PlantingContext("Scenario E: первая посадка базилика"));
            Assert.That(plant.Succeeded, Is.True);
            Assert.That(firstSeed.IsConsumed, Is.True);
            AssertBoundary(composition, 4, 0, 5, 0, 0, 0, 100);
            Assert.That(composition.PlantingSlot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));

            SelectFertilizer(composition, FertilizerGrade.Cheap);
            var dispenseFertilizer = composition.FertilizerController.DispenseSelected();
            Assert.That(dispenseFertilizer.Succeeded, Is.True);
            AssertBoundary(composition, 4, 0, 4, 1, 0, 0, 100);
            var fertilizer = FindFertilizer(FertilizerGrade.Cheap);

            var apply = new FertilizerApplicationAdapter(composition.FertilizerApplication).Apply(
                fertilizer,
                FertilizerContext("Scenario E: применение дешёвого удобрения"));
            Assert.That(apply.Succeeded, Is.True);
            Assert.That(fertilizer.IsConsumed, Is.True);
            AssertBoundary(composition, 4, 0, 4, 0, 0, 0, 100);
            Assert.That(composition.PlantingSlot.GetSnapshot().GrowthModifier.IsApplied, Is.True);

            var slot = composition.PlantingSlot;
            var waterThreshold = slot.GetSnapshot().WaterRequestThreshold;
            Assert.That(waterThreshold, Is.Not.Null);
            var waterTarget = waterThreshold.Value + 0.001f;
            var reachWater = slot.AdvanceProgress(
                waterTarget,
                slot.Version,
                SlotContext("Scenario E: достижение порога полива"));
            Assert.That(reachWater.Succeeded, Is.True);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.NeedsWater));

            var water = new WateringAdapter(slot).Water(Guid.NewGuid());
            Assert.That(water.Succeeded, Is.True);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            var ripen = slot.AdvanceProgress(1f, slot.Version, SlotContext("Scenario E: созревание базилика"));
            Assert.That(ripen.Succeeded, Is.True);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
            composition.GrowthController.RefreshVisual();

            var handObject = new GameObject("Scenario E — тестовая рука");
            var hand = handObject.AddComponent<XRDirectInteractor>();
            hand.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)hand, composition.HarvestController.Interactable);
            manager.SelectExit((IXRSelectInteractor)hand, composition.HarvestController.Interactable);

            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            AssertBoundary(composition, 4, 0, 4, 0, 3, 0, 100);

            SelectRecipe(composition, BasilTonic);
            var craft = composition.CraftingController.CraftSelected();
            Assert.That(craft.Succeeded, Is.True);
            AssertBoundary(composition, 4, 0, 4, 0, 1, 1, 100);

            SelectOrder(composition, BasilTonic, 1);
            var selectedOrder = composition.OrderBoardController.Snapshot.SelectedOrder;
            Assert.That(selectedOrder.Reward, Is.EqualTo(25));
            var accept = composition.OrderBoardController.AcceptSelected();
            Assert.That(accept.Succeeded, Is.True);
            var delivery = composition.DeliveryController.TryDeliver();
            Assert.That(delivery.Succeeded, Is.True);
            AssertBoundary(composition, 4, 0, 4, 0, 1, 0, 125);
            AssertLedger(composition, 25);

            ActivateShop(composition);
            SelectProduct(composition, BasilSeedProductId);
            var seedOffer = composition.ShopCatalog.Definitions[composition.ShopTablet.SelectedIndex];
            Assert.That(seedOffer.ItemId, Is.EqualTo(BasilSeed));
            Assert.That(seedOffer.Quantity, Is.EqualTo(1));
            Assert.That(seedOffer.Price, Is.EqualTo(5));
            var purchase = composition.ShopTablet.Confirm();
            Assert.That(purchase.Succeeded, Is.True);
            AssertBoundary(composition, 4, 0, 4, 0, 1, 0, 120);
            AssertLedger(composition, 25, -5);

            var entry = composition.Deliveries.GetSnapshot().Single();
            Assert.That(entry.Status, Is.EqualTo(DeliveryStatus.Pending));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilSeed), Is.Zero);
            composition.DeliveryBox.ReconcileNow();
            Assert.That(GetDelivery(composition, entry.Id).Status, Is.EqualTo(DeliveryStatus.Available));

            var binding = UnityEngine.Object.FindObjectsByType<DeliveryItemBinding>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single(value => value.EntryId == entry.Id);
            var purchasedSeed = binding.GetComponent<SeedItemInstance>();
            var grab = binding.GetComponent<XRGrabInteractable>();
            Assert.That(purchasedSeed.SeedItemId, Is.EqualTo(BasilSeed));
            Assert.That(grab, Is.Not.Null);

            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.That(GetDelivery(composition, entry.Id).Status, Is.EqualTo(DeliveryStatus.Claimed));
            AssertBoundary(composition, 4, 1, 4, 0, 1, 0, 120);
            Assert.That(composition.DeliveryBox.ActiveProjectionCount, Is.Zero);
            manager.SelectExit((IXRSelectInteractor)hand, grab);

            var secondPlant = new SeedPlantingAdapter(composition.SeedPlanting).Plant(
                purchasedSeed,
                PlantingContext("Scenario E: начало второго цикла купленным саженцем"));
            Assert.That(secondPlant.Succeeded, Is.True);
            Assert.That(purchasedSeed.IsConsumed, Is.True);
            AssertBoundary(composition, 4, 0, 4, 0, 1, 0, 120);
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(slot.GetSnapshot().PlantTypeId, Is.EqualTo(Basil));
            Assert.That(GetDelivery(composition, entry.Id).Status, Is.EqualTo(DeliveryStatus.Claimed));
            AssertLedger(composition, 25, -5);

            UnityEngine.Object.Destroy(handObject);
            yield return null;
        }

        private static void AssertBoundary(
            SeedStorageCompositionRoot composition,
            int warehouseSeeds,
            int playerSeeds,
            int warehouseFertilizer,
            int playerFertilizer,
            int harvest,
            int tonic,
            int balance)
        {
            var cheap = composition.FertilizerController.Snapshot.Entries
                .Single(entry => entry.Grade == FertilizerGrade.Cheap);
            Assert.That(composition.WarehouseInventory.GetQuantity(BasilSeed), Is.EqualTo(warehouseSeeds));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilSeed), Is.EqualTo(playerSeeds));
            Assert.That(composition.WarehouseInventory.GetQuantity(cheap.ItemId), Is.EqualTo(warehouseFertilizer));
            Assert.That(composition.PlayerInventory.GetQuantity(cheap.ItemId), Is.EqualTo(playerFertilizer));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilHarvest), Is.EqualTo(harvest));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilTonic), Is.EqualTo(tonic));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balance));
        }

        private static SeedItemInstance FindSeed(ItemId itemId) =>
            UnityEngine.Object.FindObjectsByType<SeedItemInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(seed => !seed.IsConsumed && seed.SeedItemId == itemId);

        private static FertilizerItemInstance FindFertilizer(FertilizerGrade grade) =>
            UnityEngine.Object.FindObjectsByType<FertilizerItemInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(item => !item.IsConsumed && item.Grade == grade);

        private static void SelectFertilizer(SeedStorageCompositionRoot composition, FertilizerGrade grade)
        {
            var count = composition.FertilizerController.Snapshot.Entries.Count;
            for (var index = 0;
                 index < count && composition.FertilizerController.Snapshot.Entries[composition.FertilizerController.SelectedIndex].Grade != grade;
                 index++)
                composition.FertilizerController.SelectNextGrade();
            Assert.That(composition.FertilizerController.Snapshot.Entries[composition.FertilizerController.SelectedIndex].Grade,
                Is.EqualTo(grade));
        }

        private static void SelectRecipe(SeedStorageCompositionRoot composition, ItemId output)
        {
            var count = composition.CraftingController.Snapshot.Recipes.Count;
            for (var index = 0;
                 index < count && composition.CraftingController.Snapshot.SelectedRecipe.OutputItemId != output;
                 index++)
                composition.CraftingController.SelectNextRecipe();
            Assert.That(composition.CraftingController.Snapshot.SelectedRecipe.OutputItemId, Is.EqualTo(output));
        }

        private static void SelectOrder(SeedStorageCompositionRoot composition, ItemId medicine, int quantity)
        {
            var count = composition.OrderBoardController.Snapshot.Orders.Count;
            for (var index = 0;
                 index < count &&
                 (composition.OrderBoardController.Snapshot.SelectedOrder.MedicineItemId != medicine ||
                  composition.OrderBoardController.Snapshot.SelectedOrder.Quantity != quantity);
                 index++)
                composition.OrderBoardController.SelectNextOrder();
            Assert.That(composition.OrderBoardController.Snapshot.SelectedOrder.MedicineItemId, Is.EqualTo(medicine));
            Assert.That(composition.OrderBoardController.Snapshot.SelectedOrder.Quantity, Is.EqualTo(quantity));
        }

        private static void ActivateShop(SeedStorageCompositionRoot composition)
        {
            for (var current = composition.ShopTablet.transform; current != null; current = current.parent)
                current.gameObject.SetActive(true);
            composition.ShopTablet.SetApplicationFocused(true);
            composition.ShopTablet.SetApplicationPaused(false);
            composition.ShopTablet.SetTrackingAvailable(true);
            if (!composition.ShopTablet.IsOpen)
                composition.ShopTablet.Toggle();
            Assert.That(composition.ShopTablet.IsOpen, Is.True);
        }

        private static void SelectProduct(SeedStorageCompositionRoot composition, string productId)
        {
            var count = composition.ShopCatalog.Definitions.Count;
            for (var index = 0;
                 index < count && composition.ShopCatalog.Definitions[composition.ShopTablet.SelectedIndex].Id.Value != productId;
                 index++)
                composition.ShopTablet.Next();
            Assert.That(composition.ShopCatalog.Definitions[composition.ShopTablet.SelectedIndex].Id.Value,
                Is.EqualTo(productId));
        }

        private static void AssertLedger(SeedStorageCompositionRoot composition, params int[] deltas)
        {
            var ledger = composition.Economy.GetLedgerSnapshot();
            Assert.That(ledger.Select(entry => entry.Delta), Is.EqualTo(deltas));
            Assert.That(ledger.Select(entry => entry.Kind), Is.EqualTo(
                deltas.Select(delta => delta > 0 ? EconomyMutationKind.Credit : EconomyMutationKind.Spend)));
        }

        private static PendingDeliveryEntry GetDelivery(SeedStorageCompositionRoot composition, Guid entryId) =>
            composition.Deliveries.GetSnapshot().Single(entry => entry.Id == entryId);

        private static PlantSeedContext PlantingContext(string reason) =>
            new PlantSeedContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static FertilizerCommandContext FertilizerContext(string reason) =>
            new FertilizerCommandContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static PlantingSlotChangeContext SlotContext(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());
    }
}
