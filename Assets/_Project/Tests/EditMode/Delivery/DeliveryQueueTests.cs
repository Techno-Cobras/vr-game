using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VrGame.DataAssets;
using VrGame.DataAssets.Shop;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.Domain.Inventory;
using VrGame.Domain.Shop;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Delivery
{
    public sealed class DeliveryQueueTests
    {
        private static InventoryChangeContext Context() => new InventoryChangeContext("delivery", Guid.NewGuid(), Guid.NewGuid());

        [Test]
        public void DuplicatePurchases_OrderedImmutableSnapshots_AndSingleClaim()
        {
            var fixture = new Fixture();
            var first = fixture.Buy();
            var second = fixture.Buy();
            var pending = fixture.Queue.GetSnapshot();
            Assert.That(pending.Select(x => x.Sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(fixture.Queue.TryGetNextPending(out var next), Is.True);
            Assert.That(next.Id, Is.EqualTo(first.Id));
            Assert.That(fixture.Queue.Claim(first.Id, fixture.Inventory, Context()).Rejection, Is.EqualTo(DeliveryRejection.InvalidState));
            fixture.Queue.MarkAvailable(first.Id);
            Assert.That(fixture.Queue.MarkAvailable(first.Id).IsReplay, Is.True);
            Assert.That(pending[0].Status, Is.EqualTo(DeliveryStatus.Pending));
            Assert.That(fixture.Queue.TryGetNextPending(out next), Is.True);
            Assert.That(next.Id, Is.EqualTo(second.Id));
            var context = Context();
            var result = fixture.Queue.Claim(first.Id, fixture.Inventory, context);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Queue.Claim(first.Id, fixture.Inventory, context).IsReplay, Is.True);
            Assert.That(fixture.Queue.Claim(second.Id, fixture.Inventory, context).Rejection, Is.EqualTo(DeliveryRejection.CommandConflict));
            Assert.That(fixture.Queue.Claim(first.Id, fixture.Inventory, Context()).Rejection, Is.EqualTo(DeliveryRejection.AlreadyClaimed));
            Assert.That(fixture.Queue.MarkAvailable(first.Id).Rejection, Is.EqualTo(DeliveryRejection.InvalidState));
            Assert.That(fixture.Inventory.GetQuantity(first.ItemId), Is.EqualTo(first.Quantity));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(1));
        }

        [Test]
        public void FailedInventoryAdd_RetainsAvailableEntry_AndSameCommandCanRetry()
        {
            var fixture = new Fixture();
            var entry = fixture.Buy();
            var second = fixture.Buy();
            fixture.Queue.MarkAvailable(entry.Id);
            fixture.Queue.MarkAvailable(second.Id);
            fixture.Inventory.Add(entry.ItemId, int.MaxValue, Context());
            var context = Context();
            var result = fixture.Queue.Claim(entry.Id, fixture.Inventory, context);
            Assert.That(result.InventoryRejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(fixture.Queue.GetSnapshot()[0].Status, Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Inventory.GetQuantity(entry.ItemId), Is.EqualTo(int.MaxValue));
            Assert.That(fixture.Queue.Claim(second.Id, fixture.Inventory, context).Rejection,
                Is.EqualTo(DeliveryRejection.CommandConflict));
            var otherInventory = new InventoryAggregate(InventoryId.New(), fixture.Items, new InventorySink());
            Assert.That(fixture.Queue.Claim(entry.Id, otherInventory, context).Rejection,
                Is.EqualTo(DeliveryRejection.CommandConflict));
            fixture.Inventory.Remove(entry.ItemId, int.MaxValue, Context());
            Assert.That(fixture.Queue.Claim(entry.Id, fixture.Inventory, context).Succeeded, Is.True);
        }

        [Test]
        public void Changes_PublishPendingAvailableClaimed_AndAvailableReplayDoesNotRepublish()
        {
            var fixture = new Fixture();
            var added = new List<DeliveryStatus>();
            var changed = new List<DeliveryStatus>();
            fixture.Queue.Added += entry => added.Add(entry.Status);
            fixture.Queue.Changed += entry => changed.Add(entry.Status);

            var entry = fixture.Buy();
            var available = fixture.Queue.MarkAvailable(entry.Id);
            var changedBeforeReplay = changed.Count;
            var replay = fixture.Queue.MarkAvailable(entry.Id);
            var claimed = fixture.Queue.Claim(entry.Id, fixture.Inventory, Context());

            Assert.That(available.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.QueueEventPublished, Is.False);
            Assert.That(changed, Has.Count.EqualTo(changedBeforeReplay + 1));
            Assert.That(claimed.Succeeded, Is.True);
            Assert.That(added, Is.EqualTo(new[] { DeliveryStatus.Pending }));
            Assert.That(changed, Is.EqualTo(new[]
            {
                DeliveryStatus.Pending,
                DeliveryStatus.Available,
                DeliveryStatus.Claimed
            }));
            Assert.That(fixture.Queue.MarkAvailable(Guid.NewGuid()).Rejection,
                Is.EqualTo(DeliveryRejection.UnknownEntry));
        }

        [Test]
        public void ObserversSeeBothCommits_ReentryBlocked_AndExceptionsIsolated()
        {
            var fixture = new Fixture();
            var entry = fixture.Buy();
            fixture.Queue.MarkAvailable(entry.Id);
            var observed = DeliveryStatus.Pending;
            DeliveryRejection nested = DeliveryRejection.None;
            fixture.Sink.Callback = () =>
            {
                observed = fixture.Queue.GetSnapshot()[0].Status;
                nested = fixture.Queue.MarkAvailable(entry.Id).Rejection;
                throw new InvalidOperationException("observer");
            };
            var notified = 0;
            fixture.Queue.Changed += _ => throw new InvalidOperationException("queue observer");
            fixture.Queue.Changed += _ => notified++;
            var result = fixture.Queue.Claim(entry.Id, fixture.Inventory, Context());
            Assert.That(result.Succeeded, Is.True);
            Assert.That(observed, Is.EqualTo(DeliveryStatus.Claimed));
            Assert.That(nested, Is.EqualTo(DeliveryRejection.ReentrantMutation));
            Assert.That(result.InventoryEventPublished, Is.False);
            Assert.That(result.QueueEventPublished, Is.False);
            Assert.That(notified, Is.EqualTo(1));
            Assert.That(fixture.Inventory.GetQuantity(entry.ItemId), Is.EqualTo(entry.Quantity));
        }

        [Test]
        public void UnknownItemAndEntry_RejectWithoutLoss_ThenNewCommandCanRetry()
        {
            var fixture = new Fixture();
            var entry = fixture.Buy();
            fixture.Queue.MarkAvailable(entry.Id);
            var emptyCatalog = new VrGame.Data.Items.ItemCatalog(Array.Empty<VrGame.Data.Items.ItemDefinition>());
            var wrongInventory = new InventoryAggregate(InventoryId.New(), emptyCatalog, new InventorySink());
            var context = Context();
            Assert.That(fixture.Queue.Claim(entry.Id, wrongInventory, context).InventoryRejection, Is.EqualTo(InventoryRejection.UnknownItem));
            Assert.That(wrongInventory.Version, Is.Zero);
            Assert.That(fixture.Queue.GetSnapshot()[0].Status, Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Queue.Claim(Guid.NewGuid(), fixture.Inventory, Context()).Rejection, Is.EqualTo(DeliveryRejection.UnknownEntry));
            Assert.That(fixture.Queue.Claim(entry.Id, fixture.Inventory, default).Rejection, Is.EqualTo(DeliveryRejection.InvalidContext));
            Assert.That(fixture.Queue.Claim(entry.Id, fixture.Inventory, context).Rejection, Is.EqualTo(DeliveryRejection.CommandConflict));
            Assert.That(fixture.Queue.Claim(entry.Id, wrongInventory, context).InventoryRejection, Is.EqualTo(InventoryRejection.UnknownItem));
            Assert.That(wrongInventory.Version, Is.Zero);
            Assert.That(fixture.Queue.Claim(entry.Id, fixture.Inventory, Context()).Succeeded, Is.True);
        }
        private sealed class Fixture
        {
            public readonly PendingDeliveryLedger Queue = new PendingDeliveryLedger();
            public readonly InventorySink Sink = new InventorySink();
            public readonly InventoryAggregate Inventory;
            public readonly VrGame.Data.Items.ItemCatalog Items;
            private readonly PurchaseService purchases;
            private readonly VrGame.Data.Shop.ProductId product;
            public Fixture()
            {
                var items = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
                var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>("Assets/_Project/Shop/Samples/ShopCatalog.asset").Build(items);
                Items = items.Items;
                product = catalog.Definitions[0].Id;
                Inventory = new InventoryAggregate(InventoryId.New(), Items, Sink);
                purchases = new PurchaseService(catalog, new EconomyService(1000, new EconomySink()), Queue);
            }
            public PendingDeliveryEntry Buy() => purchases.Purchase(product, new EconomyChangeContext("buy", Guid.NewGuid(), Guid.NewGuid())).Entry;
        }
        private sealed class InventorySink : IInventoryEventSink
        {
            public Action Callback;
            public bool TryPublish(InventoryChanged change) { Callback?.Invoke(); return true; }
        }
        private sealed class EconomySink : IEconomyEventSink
        {
            public bool TryPublish(BalanceChanged change) => true;
        }
    }
}
