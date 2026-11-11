using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Bootstrap;
using VrGame.Domain.Economy;
using VrGame.Presentation.Shop;

namespace VrGame.Tests.PlayMode.Shop
{
    public sealed class ShopTabletPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_ComposesSharedTabletAndRunsPurchaseFeedbackFlow()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var tablets = UnityEngine.Object.FindObjectsByType<ShopTabletController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.That(composition, Is.Not.Null);
            Assert.That(tablets, Has.Length.EqualTo(1));
            Assert.That(composition.ShopTablet, Is.SameAs(tablets[0]));
            Assert.That(composition.Purchases, Is.Not.Null);
            Assert.That(composition.Deliveries, Is.Not.Null);
            Assert.That(composition.Economy, Is.Not.Null);
            Assert.That(tablets[0].IsOpen, Is.False);
            Assert.That(tablets[0].GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(tablets[0].GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            Assert.That(tablets[0].transform.parent.name, Is.EqualTo("Left Controller"));

            var buttons = tablets[0].GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(5));
            var open = buttons.Single(button => button.name == "Магазин");
            var next = buttons.Single(button => button.name == "Далее");
            var confirm = buttons.Single(button => button.name == "Купить");
            var content = tablets[0].GetComponentsInChildren<Text>(true)
                .Single(text => text.transform.parent.name == "Каталог");

            ActivateHierarchy(tablets[0].transform);
            tablets[0].SetApplicationFocused(true);
            tablets[0].SetApplicationPaused(false);
            tablets[0].SetTrackingAvailable(true);
            Assert.That(tablets[0].CanInteract, Is.True,
                $"enabled={tablets[0].enabled}, active={tablets[0].gameObject.activeInHierarchy}");
            open.onClick.Invoke();

            Assert.That(tablets[0].IsOpen, Is.True);
            Assert.That(content.text, Does.Contain("Баланс: 100"));
            foreach (var offer in composition.ShopCatalog.Definitions)
                Assert.That(content.text, Does.Contain(
                    $"{offer.DisplayName} ×{offer.Quantity} — {offer.Price}"));

            var selected = composition.ShopCatalog.Definitions[0];
            var balanceBefore = composition.Economy.Balance;
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();

            Assert.That(composition.Deliveries.GetSnapshot(), Has.Count.EqualTo(1));
            Assert.That(composition.Economy.Balance, Is.EqualTo(balanceBefore - selected.Price));
            Assert.That(content.text, Does.Contain("Покупка оформлена"));

            var spend = composition.Economy.TrySpend(
                composition.Economy.Balance,
                new EconomyChangeContext("Подготовка проверки нехватки средств", Guid.NewGuid(), Guid.NewGuid()));
            Assert.That(spend.Succeeded, Is.True);
            Assert.That(content.text, Does.Contain("Баланс: 0"));

            yield return null;

            ActivateHierarchy(tablets[0].transform);
            tablets[0].SetApplicationFocused(true);
            tablets[0].SetApplicationPaused(false);
            tablets[0].SetTrackingAvailable(true);
            if (!tablets[0].IsOpen)
                open.onClick.Invoke();
            Assert.That(tablets[0].IsOpen, Is.True);
            next.onClick.Invoke();
            var deliveriesBefore = composition.Deliveries.GetSnapshot().Count;
            confirm.onClick.Invoke();

            Assert.That(composition.Economy.Balance, Is.Zero);
            Assert.That(composition.Deliveries.GetSnapshot(), Has.Count.EqualTo(deliveriesBefore));
            Assert.That(content.text, Does.Contain("Недостаточно средств"));

            tablets[0].SetTrackingAvailable(false);
            open.onClick.Invoke();
            Assert.That(tablets[0].IsOpen, Is.False);
        }

        private static void ActivateHierarchy(Transform leaf)
        {
            for (var current = leaf; current != null; current = current.parent)
                current.gameObject.SetActive(true);
        }
    }
}
