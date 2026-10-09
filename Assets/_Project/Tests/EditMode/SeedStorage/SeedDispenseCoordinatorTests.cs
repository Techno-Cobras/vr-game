using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.SeedStorage;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.SeedStorage
{
    public sealed class SeedDispenseCoordinatorTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly PlantTypeId Mint = new PlantTypeId("plant.mint");
        private static readonly ItemId BasilSeed = new ItemId("item.seed.basil");
        private static readonly ItemId MintSeed = new ItemId("item.seed.mint");

        [Test]
        public void Snapshot_ContainsEveryPlantAndCurrentInventoryQuantity()
        {
            var fixture = CreateFixture(3, 1);

            var snapshot = fixture.Coordinator.GetSnapshot();

            Assert.That(snapshot.InventoryVersion, Is.EqualTo(2));
            Assert.That(snapshot.Entries, Has.Count.EqualTo(2));
            Assert.That(snapshot.Entries[0].PlantTypeId, Is.EqualTo(Basil));
            Assert.That(snapshot.Entries[0].SeedItemId, Is.EqualTo(BasilSeed));
            Assert.That(snapshot.Entries[0].Quantity, Is.EqualTo(3));
            Assert.That(snapshot.Entries[1].PlantTypeId, Is.EqualTo(Mint));
            Assert.That(snapshot.Entries[1].Quantity, Is.EqualTo(1));
        }

        [Test]
        public void Dispense_AvailableSeed_StagesBeforeSingleRemovalAndReleasesBatch()
        {
            var fixture = CreateFixture(3);
            fixture.Materializer.OnStage = () =>
                Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(3));

            var result = fixture.Coordinator.Dispense(Basil, 2, Context());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsReplay, Is.False);
            Assert.That(result.AvailableQuantity, Is.EqualTo(1));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Materializer.StageCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.ReleaseCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.CommitCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.Zero);
            Assert.That(result.TransferResult.SourceChange.Changes, Has.Count.EqualTo(1));
            Assert.That(result.TransferResult.SourceChange.Changes[0].Delta, Is.EqualTo(-2));
            Assert.That(result.TransferResult.DestinationChange.Changes[0].Delta, Is.EqualTo(2));
        }

        [Test]
        public void SameCommand_IsReplayWithoutSecondSpawnOrRemoval()
        {
            var fixture = CreateFixture(3);
            var context = Context();
            var first = fixture.Coordinator.Dispense(Basil, 1, context);

            var replay = fixture.Coordinator.Dispense(Basil, 1, context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.TransferResult, Is.SameAs(first.TransferResult));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.Materializer.StageCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.ReleaseCalls, Is.EqualTo(1));
        }

        [Test]
        public void SameCommandWithDifferentPayload_IsConflictAndExactNoOp()
        {
            var fixture = CreateFixture(3);
            var context = Context();
            fixture.Coordinator.Dispense(Basil, 1, context);

            var conflict = fixture.Coordinator.Dispense(Basil, 2, context);

            Assert.That(conflict.Rejection, Is.EqualTo(SeedDispenseRejection.CommandConflict));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Materializer.StageCalls, Is.EqualTo(1));
        }

        [Test]
        public void UnavailableQuantity_DoesNotMaterializeOrMutateInventory()
        {
            var fixture = CreateFixture(1);

            var result = fixture.Coordinator.Dispense(Basil, 2, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.InsufficientStock));
            Assert.That(result.AvailableQuantity, Is.EqualTo(1));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.Materializer.StageCalls, Is.Zero);
        }

        [Test]
        public void MaterializationFailure_DoesNotLoseStock()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.ShouldSucceed = false;

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseFailure_RollsBackAndLeavesBothInventoriesUnchanged()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.Batch.ReleaseSucceeds = false;

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseException_AndRollbackException_DoNotMutateInventories()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.Batch.ReleaseException = new InvalidOperationException("Ошибка release.");
            fixture.Materializer.Batch.RollbackException = new InvalidOperationException("Ошибка rollback.");

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void CommitException_StillCachesCommittedTransfer()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.Batch.CommitException = new InvalidOperationException("Ошибка commit handle.");
            var context = Context();

            var first = fixture.Coordinator.Dispense(Basil, 1, context);
            var replay = fixture.Coordinator.Dispense(Basil, 1, context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.EqualTo(1));
            Assert.That(fixture.Materializer.StageCalls, Is.EqualTo(1));
        }

        [Test]
        public void MaterializerReturningBatchWithFailure_RollsItBackWithoutLosingStock()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.ShouldSucceed = false;
            fixture.Materializer.ReturnBatchOnFailure = true;

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void MaterializerException_DoesNotLoseStock()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.Exception = new InvalidOperationException("Тестовая ошибка создания.");

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
        }

        [Test]
        public void MaterializerAssigningBatchThenThrowing_RollsBackWithoutLosingStock()
        {
            var fixture = CreateFixture(2);
            fixture.Materializer.AssignBatchBeforeException = true;
            fixture.Materializer.Exception = new InvalidOperationException("Ошибка после создания партии.");

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.MaterializationFailed));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void InventoryRejection_RollsBackStagedBatch()
        {
            var fixture = CreateFixture(2);
            typeof(InventoryAggregate).GetProperty(nameof(InventoryAggregate.Version))
                ?.SetValue(fixture.Inventory, long.MaxValue);

            var result = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.InventoryRejected));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(2));
            Assert.That(fixture.Materializer.Batch.ReleaseCalls, Is.EqualTo(1));
            Assert.That(fixture.Materializer.Batch.RollbackCalls, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCommand_IsRejectedWithoutASecondMaterialization()
        {
            var fixture = CreateFixture(2);
            SeedDispenseResult nested = null;
            fixture.Materializer.OnStage = () =>
                nested = fixture.Coordinator.Dispense(Basil, 1, Context());

            var outer = fixture.Coordinator.Dispense(Basil, 1, Context());

            Assert.That(outer.Succeeded, Is.True);
            Assert.That(nested.Rejection, Is.EqualTo(SeedDispenseRejection.ReentrantCommand));
            Assert.That(fixture.Materializer.StageCalls, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveQuantity_IsRejectedBeforeMaterialization(int quantity)
        {
            var fixture = CreateFixture(2);

            var result = fixture.Coordinator.Dispense(Basil, quantity, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.InvalidQuantity));
            Assert.That(fixture.Materializer.StageCalls, Is.Zero);
        }

        [Test]
        public void QuantityAboveConfiguredBatchLimit_IsRejectedBeforeMaterialization()
        {
            var fixture = CreateFixture(20);

            var result = fixture.Coordinator.Dispense(Basil, 9, Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.QuantityLimitExceeded));
            Assert.That(fixture.Inventory.GetQuantity(BasilSeed), Is.EqualTo(20));
            Assert.That(fixture.PlayerInventory.GetQuantity(BasilSeed), Is.Zero);
            Assert.That(fixture.Materializer.StageCalls, Is.Zero);
        }

        [Test]
        public void UnknownPlant_IsRejectedBeforeMaterialization()
        {
            var fixture = CreateFixture(2);

            var result = fixture.Coordinator.Dispense(
                new PlantTypeId("plant.unknown"),
                1,
                Context());

            Assert.That(result.Rejection, Is.EqualTo(SeedDispenseRejection.UnknownPlant));
            Assert.That(fixture.Materializer.StageCalls, Is.Zero);
        }

        private static Fixture CreateFixture(int basilQuantity, int mintQuantity = 0)
        {
            var itemCatalog = new ItemCatalog(new[]
            {
                Item(BasilSeed, ItemCategory.Seed),
                Item(MintSeed, ItemCategory.Seed),
                Item(new ItemId("item.harvest.basil"), ItemCategory.HarvestedPlant),
                Item(new ItemId("item.harvest.mint"), ItemCategory.HarvestedPlant)
            });
            var plantCatalog = new PlantCatalog(itemCatalog, new[]
            {
                Plant(Basil, BasilSeed, new ItemId("item.harvest.basil")),
                Plant(Mint, MintSeed, new ItemId("item.harvest.mint"))
            });
            var inventory = new InventoryAggregate(InventoryId.New(), itemCatalog, new AcceptingInventorySink());
            var playerInventory = new InventoryAggregate(InventoryId.New(), itemCatalog, new AcceptingInventorySink());
            if (basilQuantity > 0)
                inventory.Add(BasilSeed, basilQuantity, InventoryContext());
            if (mintQuantity > 0)
                inventory.Add(MintSeed, mintQuantity, InventoryContext());
            var materializer = new FakeMaterializer();
            return new Fixture(
                inventory,
                playerInventory,
                materializer,
                new SeedDispenseCoordinator(inventory, playerInventory, plantCatalog, materializer));
        }

        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id,
            id.Value,
            category,
            new ItemRepresentationKey("representation." + id.Value.Substring("item.".Length)),
            new ItemStackRules(16, true));

        private static PlantDefinition Plant(PlantTypeId id, ItemId seed, ItemId harvest) =>
            new PlantDefinition(
                id,
                new GrowthDuration(60f),
                seed,
                harvest,
                new[]
                {
                    new PlantStageDefinition(
                        0f,
                        new PlantRepresentationKey("representation." + id.Value))
                });

        private static SeedDispenseContext Context() =>
            new SeedDispenseContext("выдача саженцев", Guid.NewGuid(), Guid.NewGuid());

        private static InventoryChangeContext InventoryContext() =>
            new InventoryChangeContext("начальное наполнение", Guid.NewGuid());

        private sealed class Fixture
        {
            public Fixture(
                InventoryAggregate inventory,
                InventoryAggregate playerInventory,
                FakeMaterializer materializer,
                SeedDispenseCoordinator coordinator)
            {
                Inventory = inventory;
                PlayerInventory = playerInventory;
                Materializer = materializer;
                Coordinator = coordinator;
            }

            public InventoryAggregate Inventory { get; }
            public InventoryAggregate PlayerInventory { get; }
            public FakeMaterializer Materializer { get; }
            public SeedDispenseCoordinator Coordinator { get; }
        }

        private sealed class FakeMaterializer : ISeedDispenseMaterializer
        {
            public bool ShouldSucceed { get; set; } = true;
            public bool ReturnBatchOnFailure { get; set; }
            public bool AssignBatchBeforeException { get; set; }
            public Exception Exception { get; set; }
            public Action OnStage { get; set; }
            public int StageCalls { get; private set; }
            public FakeBatch Batch { get; } = new FakeBatch();

            public bool TryStage(SeedMaterializationRequest request, out IStagedSeedBatch batch)
            {
                StageCalls++;
                OnStage?.Invoke();
                batch = AssignBatchBeforeException ? Batch : null;
                if (Exception != null)
                    throw Exception;

                batch = ShouldSucceed || ReturnBatchOnFailure ? Batch : null;
                return ShouldSucceed;
            }
        }

        private sealed class FakeBatch : IStagedSeedBatch
        {
            public int ReleaseCalls { get; private set; }
            public int CommitCalls { get; private set; }
            public int RollbackCalls { get; private set; }
            public bool ReleaseSucceeds { get; set; } = true;
            public Exception ReleaseException { get; set; }
            public Exception CommitException { get; set; }
            public Exception RollbackException { get; set; }
            public bool TryRelease()
            {
                ReleaseCalls++;
                if (ReleaseException != null)
                    throw ReleaseException;
                return ReleaseSucceeds;
            }
            public void Commit()
            {
                CommitCalls++;
                if (CommitException != null)
                    throw CommitException;
            }
            public void Rollback()
            {
                RollbackCalls++;
                if (RollbackException != null)
                    throw RollbackException;
            }
        }

        private sealed class AcceptingInventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) => true;
        }
    }
}
