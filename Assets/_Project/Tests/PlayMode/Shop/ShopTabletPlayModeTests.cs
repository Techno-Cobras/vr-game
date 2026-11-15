using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Bootstrap;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.Domain.Plants;
using VrGame.Presentation.Shop;
using VrGame.VRInteraction.Delivery;
using VrGame.VRInteraction.SeedStorage;

namespace VrGame.Tests.PlayMode.Shop
{
    public sealed class ShopTabletPlayModeTests
    {
        private const string BasilSeedProductId = "product.basilseed";

        [UnityTest]
        public IEnumerator ScenarioD_FromTabletPurchaseToPlantingPhysicalSeed()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            var tablets = UnityEngine.Object.FindObjectsByType<ShopTabletController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.That(composition, Is.Not.Null);
            Assert.That(manager, Is.Not.Null);
            Assert.That(tablets, Has.Length.EqualTo(1));
            var tablet = tablets[0];
            Assert.That(composition.ShopTablet, Is.SameAs(tablet));
            Assert.That(tablet.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(tablet.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            Assert.That(tablet.transform.parent.name, Is.EqualTo("Left Controller"));

            var buttons = tablet.GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(5));
            var open = buttons.Single(button => button.name == "Магазин");
            var next = buttons.Single(button => button.name == "Далее");
            var confirm = buttons.Single(button => button.name == "Купить");
            var content = tablet.GetComponentsInChildren<Text>(true)
                .Single(text => text.transform.parent.name == "Каталог");

            ActivateTablet(tablet, open);
            Assert.That(content.text, Does.Contain("Баланс: 100"));
            foreach (var offer in composition.ShopCatalog.Definitions)
                Assert.That(content.text, Does.Contain(
                    $"{offer.DisplayName} ×{offer.Quantity} — {offer.Price}"));
            SelectProduct(tablet, composition, next, BasilSeedProductId);
            var seedOffer = composition.ShopCatalog.Definitions[tablet.SelectedIndex];
            var balanceBefore = composition.Economy.Balance;
            var economyVersionBefore = composition.Economy.Version;

            confirm.onClick.Invoke();
            confirm.onClick.Invoke();

            var purchaseEntries = composition.Deliveries.GetSnapshot();
            Assert.That(purchaseEntries, Has.Count.EqualTo(1));
            var purchasedEntry = purchaseEntries.Single();
            Assert.That(purchasedEntry.ProductId, Is.EqualTo(seedOffer.Id));
            Assert.That(purchasedEntry.ItemId, Is.EqualTo(seedOffer.ItemId));
            Assert.That(purchasedEntry.Status, Is.EqualTo(DeliveryStatus.Pending));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore - seedOffer.Price));
            Assert.That(composition.Economy.Version, Is.EqualTo(economyVersionBefore + 1));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(1));
            Assert.That(content.text, Does.Contain("Покупка оформлена"));

            var drain = composition.Economy.TrySpend(
                composition.Economy.Balance,
                new EconomyChangeContext(
                    "Подготовка Scenario D к проверке нехватки средств",
                    Guid.NewGuid(),
                    Guid.NewGuid()));
            Assert.That(drain.Succeeded, Is.True);
            Assert.That(composition.Economy.Balance, Is.Zero);
            SelectDifferentProduct(tablet, composition, next, seedOffer.Id.Value);

            var economyVersionBeforeFailure = composition.Economy.Version;
            var economyLedgerBeforeFailure = composition.Economy.GetLedgerSnapshot().Count;
            var queueBeforeFailure = composition.Deliveries.GetSnapshot();
            var inventoryVersionBeforeFailure = composition.PlayerInventory.Version;
            var inventoryQuantityBeforeFailure = composition.PlayerInventory.GetQuantity(seedOffer.ItemId);
            var projectionsBeforeFailure = composition.DeliveryBox.ActiveProjectionCount;

            confirm.onClick.Invoke();

            Assert.That(composition.Economy.Balance, Is.Zero);
            Assert.That(composition.Economy.Version, Is.EqualTo(economyVersionBeforeFailure));
            Assert.That(composition.Economy.GetLedgerSnapshot(), Has.Count.EqualTo(economyLedgerBeforeFailure));
            Assert.That(composition.Deliveries.GetSnapshot(), Has.Count.EqualTo(queueBeforeFailure.Count));
            Assert.That(composition.Deliveries.GetSnapshot().Single(), Is.SameAs(purchasedEntry));
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(inventoryVersionBeforeFailure));
            Assert.That(composition.PlayerInventory.GetQuantity(seedOffer.ItemId),
                Is.EqualTo(inventoryQuantityBeforeFailure));
            Assert.That(composition.DeliveryBox.ActiveProjectionCount, Is.EqualTo(projectionsBeforeFailure));
            Assert.That(content.text, Does.Contain("Недостаточно средств"));

            composition.DeliveryBox.ReconcileNow();
            Assert.That(composition.Deliveries.GetSnapshot().Single().Status,
                Is.EqualTo(DeliveryStatus.Available));
            Assert.That(composition.DeliveryBox.ActiveProjectionCount, Is.EqualTo(1));
            var binding = UnityEngine.Object.FindObjectsByType<DeliveryItemBinding>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single(value => value.EntryId == purchasedEntry.Id);
            var seed = binding.GetComponent<SeedItemInstance>();
            var grab = binding.GetComponent<XRGrabInteractable>();
            Assert.That(seed, Is.Not.Null);
            Assert.That(seed.SeedItemId, Is.EqualTo(seedOffer.ItemId));
            Assert.That(grab, Is.Not.Null);

            var handObject = new GameObject("Scenario D — тестовая рука");
            var hand = handObject.AddComponent<XRDirectInteractor>();
            hand.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);

            Assert.That(composition.Deliveries.GetSnapshot().Single().Status,
                Is.EqualTo(DeliveryStatus.Claimed));
            Assert.That(composition.PlayerInventory.GetQuantity(seedOffer.ItemId), Is.EqualTo(1));
            Assert.That(composition.DeliveryBox.ActiveProjectionCount, Is.Zero);
            var inventoryVersionAfterClaim = composition.PlayerInventory.Version;
            Assert.That(binding.TryClaim(), Is.Null,
                "Повторный callback уже завершённого handoff не должен создавать второй claim.");
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(inventoryVersionAfterClaim));
            Assert.That(composition.PlayerInventory.GetQuantity(seedOffer.ItemId), Is.EqualTo(1));

            manager.SelectExit((IXRSelectInteractor)hand, grab);
            Assert.That(grab.isSelected, Is.False);

            var socket = composition.PlantingReceiver.GetComponent<XRSocketInteractor>();
            Assert.That(socket, Is.Not.Null);
            Assert.That(composition.PlantingReceiver.Process(socket, grab), Is.True,
                "Полученный из Delivery Box physical seed должен приниматься production planting receiver.");
            var plantingCompletions = 0;
            composition.PlantingReceiver.PlantingCompleted += _ => plantingCompletions++;
            var plantingArgs = new SelectEnterEventArgs
            {
                interactorObject = socket,
                interactableObject = grab
            };

            socket.selectEntered.Invoke(plantingArgs);
            socket.selectEntered.Invoke(plantingArgs);

            var planted = composition.PlantingSlot.GetSnapshot();
            Assert.That(planted.State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(planted.PlantTypeId, Is.EqualTo(seed.PlantTypeId));
            Assert.That(composition.PlayerInventory.GetQuantity(seedOffer.ItemId), Is.Zero);
            Assert.That(seed.IsConsumed, Is.True);
            Assert.That(plantingCompletions, Is.EqualTo(1));
            Assert.That(composition.Deliveries.GetSnapshot().Single().Status,
                Is.EqualTo(DeliveryStatus.Claimed));

            tablet.SetTrackingAvailable(false);
            open.onClick.Invoke();
            Assert.That(tablet.IsOpen, Is.False,
                "Потеря tracking должна закрыть планшет и блокировать повторное открытие.");

            UnityEngine.Object.Destroy(handObject);
            yield return null;
            Assert.That(seed == null, Is.True);
        }

        private static void ActivateTablet(ShopTabletController tablet, Button open)
        {
            for (var current = tablet.transform; current != null; current = current.parent)
                current.gameObject.SetActive(true);
            tablet.SetApplicationFocused(true);
            tablet.SetApplicationPaused(false);
            tablet.SetTrackingAvailable(true);
            Assert.That(tablet.CanInteract, Is.True,
                $"enabled={tablet.enabled}, active={tablet.gameObject.activeInHierarchy}");
            if (!tablet.IsOpen)
                open.onClick.Invoke();
            Assert.That(tablet.IsOpen, Is.True);
        }

        private static void SelectProduct(
            ShopTabletController tablet,
            SeedStorageCompositionRoot composition,
            Button next,
            string productId)
        {
            var offerCount = composition.ShopCatalog.Definitions.Count;
            for (var index = 0;
                 index < offerCount &&
                 composition.ShopCatalog.Definitions[tablet.SelectedIndex].Id.Value != productId;
                 index++)
                next.onClick.Invoke();

            Assert.That(composition.ShopCatalog.Definitions[tablet.SelectedIndex].Id.Value,
                Is.EqualTo(productId));
        }

        private static void SelectDifferentProduct(
            ShopTabletController tablet,
            SeedStorageCompositionRoot composition,
            Button next,
            string excludedProductId)
        {
            var offerCount = composition.ShopCatalog.Definitions.Count;
            for (var index = 0;
                 index < offerCount &&
                 composition.ShopCatalog.Definitions[tablet.SelectedIndex].Id.Value == excludedProductId;
                 index++)
                next.onClick.Invoke();

            Assert.That(composition.ShopCatalog.Definitions[tablet.SelectedIndex].Id.Value,
                Is.Not.EqualTo(excludedProductId));
        }
    }
}
