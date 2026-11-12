using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Shop;
using VrGame.DataAssets;
using VrGame.DataAssets.Shop;
using VrGame.DataAssets.Fertilizers;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.Domain.Inventory;
using VrGame.Domain.Shop;
using VrGame.VRInteraction.Delivery;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.SeedStorage;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.DeliveryBox
{
    public sealed class DeliveryBoxControllerTests
    {
        private Fixture fixture;

        [SetUp]
        public void SetUp() => fixture = new Fixture(2);

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void QueueEvent_DoesNotMutateSynchronously_ThenFillsSlotsInFifoOrder()
        {
            var first = fixture.Buy(0);
            var duplicate = fixture.Buy(0);
            var third = fixture.Buy(1);

            Assert.That(fixture.Controller.ActiveProjectionCount, Is.Zero,
                "Callback очереди должен только запланировать reconciliation.");

            fixture.Controller.ReconcileNow();

            Assert.That(fixture.Materializer.MaterializedIds,
                Is.EqualTo(new[] { first.Id, duplicate.Id }));
            Assert.That(fixture.Status(first.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Status(duplicate.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Status(third.Id), Is.EqualTo(DeliveryStatus.Pending));
        }

        [Test]
        public void SuccessfulRetrieval_ClaimsOnce_AndReleasesCapacityForNextEntry()
        {
            var first = fixture.Buy(0);
            fixture.Buy(1);
            var third = fixture.Buy(2);
            fixture.Controller.ReconcileNow();

            var projection = fixture.Materializer.Get(first.Id);
            var result = projection.Claim();
            fixture.Controller.ReconcileNow();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Status(first.Id), Is.EqualTo(DeliveryStatus.Claimed));
            Assert.That(fixture.Inventory.GetQuantity(first.ItemId), Is.EqualTo(1));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(1));
            Assert.That(fixture.Controller.IsProjected(third.Id), Is.True);
            Assert.That(fixture.Status(third.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(projection.Claim().IsReplay, Is.True);
            Assert.That(fixture.Inventory.Version, Is.EqualTo(1));
        }

        [Test]
        public void FailedMaterialization_LeavesPending_AndRetrySucceeds()
        {
            fixture.Materializer.FailuresRemaining = 1;
            var entry = fixture.Buy(0);

            fixture.Controller.ReconcileNow();
            Assert.That(fixture.Status(entry.Id), Is.EqualTo(DeliveryStatus.Pending));
            Assert.That(fixture.Controller.ActiveProjectionCount, Is.Zero);

            fixture.Controller.ReconcileNow();
            Assert.That(fixture.Status(entry.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Controller.IsProjected(entry.Id), Is.True);
        }

        [Test]
        public void FailedFirstPending_DoesNotAllowLaterEntryToOvertakeFifo()
        {
            fixture.Materializer.FailuresRemaining = 1;
            var first = fixture.Buy(0);
            var second = fixture.Buy(1);

            fixture.Controller.ReconcileNow();

            Assert.That(fixture.Materializer.MaterializedIds, Is.Empty);
            Assert.That(fixture.Status(first.Id), Is.EqualTo(DeliveryStatus.Pending));
            Assert.That(fixture.Status(second.Id), Is.EqualTo(DeliveryStatus.Pending));

            fixture.Controller.ReconcileNow();

            Assert.That(fixture.Materializer.MaterializedIds,
                Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(fixture.Status(first.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Status(second.Id), Is.EqualTo(DeliveryStatus.Available));
        }

        [Test]
        public void FailedClaim_KeepsAvailable_AndExactRetryAddsInventoryOnce()
        {
            var entry = fixture.Buy(0);
            fixture.Controller.ReconcileNow();
            var projection = fixture.Materializer.Get(entry.Id);
            fixture.Inventory.Add(entry.ItemId, int.MaxValue, Context("переполнение"));

            var failed = projection.Claim();
            Assert.That(failed.InventoryRejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(fixture.Status(entry.Id), Is.EqualTo(DeliveryStatus.Available));

            fixture.Inventory.Remove(entry.ItemId, int.MaxValue, Context("освобождение"));
            var retry = projection.Claim();
            Assert.That(retry.Succeeded, Is.True);
            Assert.That(fixture.Inventory.GetQuantity(entry.ItemId), Is.EqualTo(1));
            Assert.That(fixture.Status(entry.Id), Is.EqualTo(DeliveryStatus.Claimed));
        }

        [Test]
        public void LostAvailableProjection_IsRecreatedFromQueueSourceOfTruth()
        {
            var entry = fixture.Buy(0);
            fixture.Controller.ReconcileNow();
            var firstProjection = fixture.Materializer.Get(entry.Id);

            firstProjection.Lose();
            fixture.Controller.ReconcileNow();

            Assert.That(fixture.Status(entry.Id), Is.EqualTo(DeliveryStatus.Available));
            Assert.That(fixture.Materializer.MaterializedIds.Count(id => id == entry.Id), Is.EqualTo(2));
            Assert.That(fixture.Controller.IsProjected(entry.Id), Is.True);
        }

        [TestCase(0, typeof(SeedItemInstance))]
        [TestCase(2, typeof(FertilizerItemInstance))]
        public void ProductionMaterializer_CreatesDownstreamIdentity_AndClaimTransfersOnce(
            int offerIndex,
            Type expectedIdentity)
        {
            var runtimeItems = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(
                "Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
            var plants = AssetDatabase.LoadAssetAtPath<PlantCatalogAsset>(
                "Assets/_Project/SeedStorage/Data/SeedStoragePlantCatalog.asset").Build(runtimeItems).Plants;
            var fertilizers = AssetDatabase.LoadAssetAtPath<FertilizerCatalogAsset>(
                "Assets/_Project/SeedStorage/Data/FertilizerCatalog.asset").Build(runtimeItems);
            var shop = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(
                "Assets/_Project/Shop/Samples/ShopCatalog.asset").Build(runtimeItems);
            var queue = new PendingDeliveryLedger();
            var inventory = new InventoryAggregate(InventoryId.New(), runtimeItems.Items, new InventorySink());
            var purchase = new PurchaseService(shop, new EconomyService(1000, new EconomySink()), queue)
                .Purchase(shop.Definitions[offerIndex].Id,
                    new EconomyChangeContext("покупка representation", Guid.NewGuid(), Guid.NewGuid()));
            var host = new GameObject("Production materializer test");
            try
            {
                var materializer = host.AddComponent<DeliveryItemMaterializer>();
                materializer.Configure(runtimeItems, plants, fertilizers, _ => { });
                IDeliveryProjection releasedProjection = null;
                Assert.That(materializer.TryMaterialize(
                    purchase.Entry,
                    host.transform,
                    queue,
                    inventory,
                    value => releasedProjection = value,
                    out var projection), Is.True);
                var binding = (DeliveryItemBinding)projection;
                Assert.That(binding.GetComponent(expectedIdentity), Is.Not.Null);
                Assert.That(queue.MarkAvailable(purchase.Entry.Id).Succeeded, Is.True);

                var claim = binding.TryClaim();

                Assert.That(claim.Succeeded, Is.True);
                Assert.That(releasedProjection, Is.SameAs(projection));
                Assert.That(inventory.GetQuantity(purchase.Entry.ItemId), Is.EqualTo(1));
                Assert.That(queue.GetSnapshot().Single().Status, Is.EqualTo(DeliveryStatus.Claimed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static InventoryChangeContext Context(string reason) =>
            new InventoryChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly PurchaseService purchases;
            private readonly ShopCatalog shop;
            public readonly PendingDeliveryLedger Queue = new PendingDeliveryLedger();
            public readonly InventoryAggregate Inventory;
            public readonly FakeMaterializer Materializer = new FakeMaterializer();
            public readonly DeliveryBoxController Controller;

            public Fixture(int capacity)
            {
                var runtimeItems = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(
                    "Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset").Build();
                shop = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(
                    "Assets/_Project/Shop/Samples/ShopCatalog.asset").Build(runtimeItems);
                Inventory = new InventoryAggregate(InventoryId.New(), runtimeItems.Items, new InventorySink());
                purchases = new PurchaseService(shop, new EconomyService(10000, new EconomySink()), Queue);
                root = new GameObject("Delivery Box test");
                var slots = new Transform[capacity];
                for (var index = 0; index < capacity; index++)
                {
                    var slot = new GameObject("Slot " + index);
                    slot.transform.SetParent(root.transform, false);
                    slots[index] = slot.transform;
                }
                Controller = root.AddComponent<DeliveryBoxController>();
                Controller.Configure(Queue, Inventory, Materializer, slots);
            }

            public PendingDeliveryEntry Buy(int offerIndex) => purchases.Purchase(
                shop.Definitions[offerIndex].Id,
                new EconomyChangeContext("покупка для теста Delivery Box", Guid.NewGuid(), Guid.NewGuid())).Entry;

            public DeliveryStatus Status(Guid entryId) =>
                Queue.GetSnapshot().Single(value => value.Id == entryId).Status;

            public void Dispose()
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeMaterializer : IDeliveryItemMaterializer
        {
            private readonly List<FakeProjection> projections = new List<FakeProjection>();
            public int FailuresRemaining { get; set; }
            public List<Guid> MaterializedIds { get; } = new List<Guid>();

            public bool TryMaterialize(
                PendingDeliveryEntry entry,
                Transform slot,
                PendingDeliveryLedger ledger,
                InventoryAggregate inventory,
                Action<IDeliveryProjection> projectionReleased,
                out IDeliveryProjection projection)
            {
                if (FailuresRemaining > 0)
                {
                    FailuresRemaining--;
                    projection = null;
                    return false;
                }
                var value = new FakeProjection(entry.Id, ledger, inventory, projectionReleased);
                projections.Add(value);
                MaterializedIds.Add(entry.Id);
                projection = value;
                return true;
            }

            public FakeProjection Get(Guid entryId) =>
                projections.Last(value => value.EntryId == entryId && value.IsAlive);
        }

        private sealed class FakeProjection : IDeliveryProjection
        {
            private readonly PendingDeliveryLedger ledger;
            private readonly InventoryAggregate inventory;
            private readonly Action<IDeliveryProjection> released;
            private readonly InventoryChangeContext context;
            private DeliveryMutationResult successfulResult;

            public FakeProjection(
                Guid entryId,
                PendingDeliveryLedger ledger,
                InventoryAggregate inventory,
                Action<IDeliveryProjection> released)
            {
                EntryId = entryId;
                this.ledger = ledger;
                this.inventory = inventory;
                this.released = released;
                context = new InventoryChangeContext("VR-получение тестовой покупки", Guid.NewGuid(), entryId);
                IsAlive = true;
            }

            public Guid EntryId { get; }
            public bool IsAlive { get; private set; }

            public DeliveryMutationResult Claim()
            {
                var result = ledger.Claim(EntryId, inventory, context);
                if (result.Succeeded && successfulResult == null)
                {
                    successfulResult = result;
                    IsAlive = false;
                    released(this);
                }
                return result;
            }

            public void Lose()
            {
                IsAlive = false;
                released(this);
            }

            public void Rollback() => IsAlive = false;
        }

        private sealed class InventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }

        private sealed class EconomySink : IEconomyEventSink
        {
            public bool TryPublish(BalanceChanged balanceChanged) => true;
        }
    }
}
