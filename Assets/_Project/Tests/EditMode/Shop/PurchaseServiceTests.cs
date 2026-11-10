using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VrGame.Data.Shop;
using VrGame.DataAssets;
using VrGame.DataAssets.Shop;
using VrGame.Domain.Shop;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;

namespace VrGame.Tests.EditMode.Shop
{
    public sealed class PurchaseServiceTests
    {
        private static EconomyChangeContext Context() => new EconomyChangeContext("shop test", Guid.NewGuid(), Guid.NewGuid());
        private static ShopCatalog Catalog()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
            return AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>("Assets/_Project/Shop/Samples/ShopCatalog.asset").Build(items);
        }
        [Test]
        public void ConfiguredOffers_PurchaseExactValues_AndReplayOnce()
        {
            var catalog = Catalog();
            Assert.That(catalog.Definitions, Has.Count.EqualTo(5));
            Assert.That(catalog.Definitions.Select(value => value.DisplayName), Is.EquivalentTo(new[]
            {
                "Саженец базилика",
                "Саженец мяты",
                "Обычное удобрение",
                "Улучшенное удобрение",
                "Премиальное удобрение"
            }));
            foreach (var offer in catalog.Definitions)
            {
                var economy = new EconomyService(offer.Price, new Sink());
                var ledger = new PendingDeliveryLedger();
                var service = new PurchaseService(catalog, economy, ledger);
                var context = Context();
                var result = service.Purchase(offer.Id, context);
                var replay = service.Purchase(offer.Id, context);
                Assert.That(result.Succeeded, Is.True);
                Assert.That(replay.IsReplay, Is.True);
                Assert.That(replay.Entry, Is.SameAs(result.Entry));
                Assert.That(economy.Balance, Is.Zero);
                Assert.That(economy.Version, Is.EqualTo(1));
                Assert.That(ledger.GetSnapshot(), Has.Count.EqualTo(1));
                Assert.That(result.Entry.ProductId, Is.EqualTo(offer.Id));
                Assert.That(result.Entry.ItemId, Is.EqualTo(offer.ItemId));
                Assert.That(result.Entry.Quantity, Is.EqualTo(offer.Quantity));
                Assert.That(result.Entry.Representation, Is.EqualTo(offer.Representation));
                Assert.That(result.Entry.PurchaseCommandId, Is.EqualTo(context.CommandId));
                Assert.That(result.Entry.Sequence, Is.EqualTo(1));
            }
        }
        [Test]
        public void RejectedPurchases_LeaveMoneyAndDeliveriesUnchanged()
        {
            var catalog = Catalog();
            var economy = new EconomyService(0, new Sink());
            var ledger = new PendingDeliveryLedger();
            var service = new PurchaseService(catalog, economy, ledger);
            Assert.That(service.Purchase(catalog.Definitions[0].Id, Context()).EconomyRejection, Is.EqualTo(EconomyRejection.InsufficientFunds));
            Assert.That(service.Purchase(new ProductId("unknown"), Context()).Rejection, Is.EqualTo(PurchaseRejection.UnknownProduct));
            Assert.That(service.Purchase(catalog.Definitions[0].Id, default).Rejection, Is.EqualTo(PurchaseRejection.InvalidContext));
            Assert.That(economy.Version, Is.Zero);
            Assert.That(economy.GetLedgerSnapshot(), Is.Empty);
            Assert.That(ledger.GetSnapshot(), Is.Empty);
        }
        [Test]
        public void CallbacksSeeBothCommits_BlockReentry_AndIsolateFailures()
        {
            var catalog = Catalog();
            var offer = catalog.Definitions[0];
            var sink = new Sink();
            var economy = new EconomyService(100, sink);
            var ledger = new PendingDeliveryLedger();
            var service = new PurchaseService(catalog, economy, ledger);
            PurchaseResult nested = null;
            var observedCount = 0;
            var nestedEconomyRejection = EconomyRejection.None;
            sink.Callback = () =>
            {
                observedCount = ledger.GetSnapshot().Count;
                nested = service.Purchase(offer.Id, Context());
                nestedEconomyRejection = economy.Credit(1, Context()).Rejection;
                throw new InvalidOperationException("observer failed");
            };
            var deliveryNotifications = 0;
            ledger.Added += _ => throw new InvalidOperationException("delivery observer failed");
            ledger.Added += _ => deliveryNotifications++;
            var first = service.Purchase(offer.Id, Context());
            Assert.That(observedCount, Is.EqualTo(1));
            Assert.That(nestedEconomyRejection, Is.EqualTo(EconomyRejection.ReentrantMutation));
            Assert.That(nested.Rejection, Is.EqualTo(PurchaseRejection.ReentrantCommand));
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.BalanceEventPublished, Is.False);
            Assert.That(first.DeliveryEventPublished, Is.False);
            Assert.That(deliveryNotifications, Is.EqualTo(1));
            sink.Callback = null;
            var second = service.Purchase(offer.Id, Context());
            Assert.That(second.Entry.Id, Is.Not.EqualTo(first.Entry.Id));
            Assert.That(ledger.GetSnapshot().Select(x => x.Sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(economy.Balance, Is.EqualTo(100 - offer.Price * 2));
        }
        [Test]
        public void ExistingSpendOrAnotherServiceCannotMintUnpaidDelivery()
        {
            var catalog = Catalog();
            var offer = catalog.Definitions[0];
            var economy = new EconomyService(100, new Sink());
            var ledger = new PendingDeliveryLedger();
            var service = new PurchaseService(catalog, economy, ledger);
            var direct = Context();
            economy.TrySpend(offer.Price, direct);
            Assert.That(service.Purchase(offer.Id, direct).EconomyRejection, Is.EqualTo(EconomyRejection.CommandConflict));
            Assert.That(ledger.GetSnapshot(), Is.Empty);
            var context = Context();
            service.Purchase(offer.Id, context);
            var other = new PurchaseService(catalog, economy, ledger);
            Assert.That(other.Purchase(offer.Id, context).EconomyRejection, Is.EqualTo(EconomyRejection.CommandConflict));
            Assert.That(service.Purchase(catalog.Definitions[1].Id, context).Rejection, Is.EqualTo(PurchaseRejection.CommandConflict));
            Assert.That(ledger.GetSnapshot(), Has.Count.EqualTo(1));
            Assert.That(economy.Balance, Is.EqualTo(100 - offer.Price * 2));
        }
        [Test]
        public void Catalog_RejectsDuplicateMissingAndInvalidOffers()
        {
            var runtime = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
            var offer = Catalog().Definitions[0];
            Assert.Throws<ArgumentNullException>(() => new ShopCatalog(null, new[] { offer }));
            Assert.Throws<ArgumentNullException>(() => new ShopCatalog(runtime.Items, null));
            Assert.Throws<ArgumentException>(() => new ShopCatalog(runtime.Items, new[] { offer, offer }));
            Assert.Throws<ArgumentException>(() => new ShopCatalog(runtime.Items, new ShopOfferDefinition[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShopOfferDefinition(offer.Id, offer.DisplayName, 0, offer.ItemId, 1, offer.Representation));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShopOfferDefinition(offer.Id, offer.DisplayName, 1, offer.ItemId, 0, offer.Representation));
            var wrongRepresentation = new ShopOfferDefinition(offer.Id, offer.DisplayName, 1, offer.ItemId, 1,
                new VrGame.Data.Items.ItemRepresentationKey("representation.wrong"));
            Assert.Throws<ArgumentException>(() => new ShopCatalog(runtime.Items, new[] { wrongRepresentation }));
            var harvest = runtime.Items.Definitions.First(x => x.Category == VrGame.Data.Items.ItemCategory.HarvestedPlant);
            var invalidCategory = new ShopOfferDefinition(offer.Id, offer.DisplayName, 1, harvest.Id, 1, harvest.Representation);
            Assert.Throws<ArgumentException>(() => new ShopCatalog(runtime.Items, new[] { invalidCategory }));
        }
        private sealed class Sink : IEconomyEventSink
        {
            public Action Callback;
            public bool TryPublish(BalanceChanged change) { Callback?.Invoke(); return true; }
        }
    }
}
