using System;
using VrGame.Data.Items;
using VrGame.Data.Plants;

namespace VrGame.Domain.SeedStorage
{
    public enum SeedDispenseRejection
    {
        None = 0,
        InvalidContext,
        InvalidQuantity,
        QuantityLimitExceeded,
        UnknownPlant,
        InsufficientStock,
        MaterializationFailed,
        InventoryRejected,
        CommandConflict,
        ReentrantCommand
    }

    public readonly struct SeedDispenseContext
    {
        public SeedDispenseContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина выдачи саженцев обязательна.", nameof(reason));
            if (commandId == Guid.Empty)
                throw new ArgumentException("Command ID не может быть пустым.", nameof(commandId));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));

            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }

        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(Reason) &&
            CommandId != Guid.Empty &&
            CorrelationId != Guid.Empty;
    }

    public readonly struct SeedMaterializationRequest
    {
        public SeedMaterializationRequest(PlantTypeId plantTypeId, ItemId seedItemId, int quantity)
        {
            PlantTypeId = plantTypeId;
            SeedItemId = seedItemId;
            Quantity = quantity;
        }

        public PlantTypeId PlantTypeId { get; }
        public ItemId SeedItemId { get; }
        public int Quantity { get; }
    }

    public interface ISeedDispenseMaterializer
    {
        bool TryStage(SeedMaterializationRequest request, out IStagedSeedBatch batch);
    }

    public interface IStagedSeedBatch
    {
        // Методы идемпотентны. До Commit партия остаётся обратимой даже после release.
        bool TryRelease();
        void Commit();
        void Rollback();
    }

    public interface ISeedStorageStationView
    {
        void Render(
            SeedStorageSnapshot snapshot,
            int selectedIndex,
            int requestedQuantity,
            SeedDispenseResult lastResult);
    }
}
