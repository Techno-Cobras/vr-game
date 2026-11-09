using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Bootstrap;
using VrGame.Data.Shop;
using VrGame.DataAssets;
using VrGame.DataAssets.Shop;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.Domain.Shop;
using VrGame.Presentation.Shop;

namespace VrGame.Tests.EditMode.Shop
{
    public sealed class ShopTabletControllerTests
    {
        private GameObject host;
        [TearDown]
        public void Cleanup()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void Confirmation_UsesSharedMoneyAndGuardsDuplicateWithFundsRemaining()
        {
            var fixture = Create(1000);
            fixture.Controller.Toggle();
            var offer = fixture.Catalog.Definitions[0];
            var first = fixture.Controller.Confirm();
            Assert.That(first.Succeeded, Is.True);
            Assert.That(fixture.Controller.Confirm(), Is.SameAs(first));
            Assert.That(fixture.Queue.GetSnapshot(), Has.Count.EqualTo(1));
            Assert.That(fixture.Economy.Balance, Is.EqualTo(1000 - offer.Price));
            Assert.That(fixture.View.Balance, Is.EqualTo(fixture.Economy.Balance));
            Assert.That(fixture.View.Feedback, Is.EqualTo("Покупка оформлена"));
        }

        [Test]
        public void FailureAndLifecycle_KeepStateAndReenableEventSubscription()
        {
            var fixture = Create(0);
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.Confirm(), Is.Null);
            fixture.Controller.Toggle();
            Assert.That(fixture.Controller.Confirm().EconomyRejection, Is.EqualTo(EconomyRejection.InsufficientFunds));
            Assert.That(fixture.View.Feedback, Is.EqualTo("Недостаточно средств"));
            Assert.That(fixture.Queue.GetSnapshot(), Is.Empty);
            fixture.Controller.SetApplicationPaused(true);
            Assert.That(fixture.Controller.Confirm(), Is.Null);
            Assert.That(fixture.Controller.IsOpen, Is.False);
            fixture.Controller.SetApplicationPaused(false);
            fixture.Controller.SetApplicationFocused(false);
            fixture.Controller.Toggle();
            Assert.That(fixture.Controller.IsOpen, Is.False);
            fixture.Controller.SetApplicationFocused(true);
            fixture.Controller.enabled = false;
            var renders = fixture.View.Renders;
            fixture.Economy.Credit(10, new EconomyChangeContext("credit", Guid.NewGuid(), Guid.NewGuid()));
            Assert.That(fixture.View.Renders, Is.EqualTo(renders));
            fixture.Controller.enabled = true;
            Assert.That(fixture.View.Balance, Is.EqualTo(10));
            fixture.Controller.SetTrackingAvailable(false);
            fixture.Controller.Toggle();
            Assert.That(fixture.Controller.IsOpen, Is.False);
        }

        private Fixture Create(int balance)
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>("Assets/_Project/Shop/Samples/ShopCatalog.asset").Build(items);
            var relay = new EconomyEventRelay();
            var economy = new EconomyService(balance, relay);
            var queue = new PendingDeliveryLedger();
            host = new GameObject("Tablet Test");
            var controller = host.AddComponent<ShopTabletController>();
            var view = new View();
            controller.Configure(catalog, economy, new PurchaseService(catalog, economy, queue), relay, view);
            return new Fixture
            {
                Controller = controller,
                Catalog = catalog,
                Economy = economy,
                Queue = queue,
                View = view
            };
        }

        private sealed class Fixture
        {
            public ShopTabletController Controller;
            public ShopCatalog Catalog;
            public EconomyService Economy;
            public PendingDeliveryLedger Queue;
            public View View;
        }

        private sealed class View : IShopTabletView
        {
            public int Balance, Renders;
            public string Feedback;
            public void Render(ShopCatalog catalog, int selectedIndex, int balance, bool open, string feedback, bool canInteract)
            {
                Balance = balance;
                Feedback = feedback;
                Renders++;
            }
        }
    }
}
