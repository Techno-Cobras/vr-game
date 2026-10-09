using System;
using System.Collections.Generic;
using System.Linq;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.SeedStorage
{
    public sealed class SeedDispenseCoordinator
    {
        private readonly Inventory.Inventory warehouseInventory;
        private readonly Inventory.Inventory playerInventory;
        private readonly PlantCatalog plantCatalog;
        private readonly ISeedDispenseMaterializer materializer;
        private readonly Dictionary<Guid, CompletedCommand> completedCommands =
            new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public SeedDispenseCoordinator(
            Inventory.Inventory warehouseInventory,
            Inventory.Inventory playerInventory,
            PlantCatalog plantCatalog,
            ISeedDispenseMaterializer materializer,
            int maxBatchQuantity = 8)
        {
            this.warehouseInventory = warehouseInventory ?? throw new ArgumentNullException(nameof(warehouseInventory));
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            if (ReferenceEquals(warehouseInventory, playerInventory))
                throw new ArgumentException("Inventory склада и игрока должны быть разными.", nameof(playerInventory));
            this.plantCatalog = plantCatalog ?? throw new ArgumentNullException(nameof(plantCatalog));
            this.materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
            if (maxBatchQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchQuantity));
            MaxBatchQuantity = maxBatchQuantity;
        }

        public int MaxBatchQuantity { get; }

        public SeedStorageSnapshot GetSnapshot() => new SeedStorageSnapshot(
            warehouseInventory.Version,
            plantCatalog.Definitions
                .OrderBy(definition => definition.Id)
                .Select(definition => new SeedStockEntry(
                    definition.Id,
                    definition.SeedItemId,
                    warehouseInventory.GetQuantity(definition.SeedItemId))));

        public SeedDispenseResult Dispense(
            PlantTypeId plantTypeId,
            int quantity,
            SeedDispenseContext context)
        {
            if (isExecuting)
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.ReentrantCommand,
                    plantTypeId,
                    quantity: quantity);
            if (!context.IsValid)
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.InvalidContext,
                    plantTypeId,
                    quantity: quantity);

            var command = new CommandPayload(plantTypeId, quantity, context);
            if (completedCommands.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(command))
                    return SeedDispenseResult.Reject(
                        SeedDispenseRejection.CommandConflict,
                        plantTypeId,
                        quantity: quantity);

                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = Execute(command);
                completedCommands.Add(context.CommandId, new CompletedCommand(command, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private SeedDispenseResult Execute(CommandPayload command)
        {
            if (command.Quantity <= 0)
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.InvalidQuantity,
                    command.PlantTypeId,
                    quantity: command.Quantity);
            if (command.Quantity > MaxBatchQuantity)
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.QuantityLimitExceeded,
                    command.PlantTypeId,
                    quantity: command.Quantity);
            if (!plantCatalog.TryGet(command.PlantTypeId, out var definition))
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.UnknownPlant,
                    command.PlantTypeId,
                    quantity: command.Quantity);

            var available = warehouseInventory.GetQuantity(definition.SeedItemId);
            if (available < command.Quantity)
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.InsufficientStock,
                    command.PlantTypeId,
                    definition.SeedItemId,
                    command.Quantity,
                    available);

            IStagedSeedBatch batch = null;
            try
            {
                var staged = materializer.TryStage(
                        new SeedMaterializationRequest(
                            command.PlantTypeId,
                            definition.SeedItemId,
                            command.Quantity),
                        out batch);
                if (!staged || batch == null)
                {
                    if (batch != null)
                        TryRollback(batch);
                    return SeedDispenseResult.Reject(
                        SeedDispenseRejection.MaterializationFailed,
                        command.PlantTypeId,
                        definition.SeedItemId,
                        command.Quantity,
                        available);
                }
            }
            catch (Exception)
            {
                if (batch != null)
                    TryRollback(batch);
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.MaterializationFailed,
                    command.PlantTypeId,
                    definition.SeedItemId,
                    command.Quantity,
                    available);
            }

            var released = false;
            try
            {
                released = batch.TryRelease();
            }
            catch (Exception)
            {
                // Нарушивший контракт adapter не должен привести к потере inventory.
            }
            if (!released)
            {
                TryRollback(batch);
                return SeedDispenseResult.Reject(
                    SeedDispenseRejection.MaterializationFailed,
                    command.PlantTypeId,
                    definition.SeedItemId,
                    command.Quantity,
                    available);
            }

            var transferResult = warehouseInventory.TransferTo(
                playerInventory,
                definition.SeedItemId,
                command.Quantity,
                new InventoryTransferContext(
                    command.Reason,
                    command.CommandId,
                    command.CorrelationId));
            if (!transferResult.Succeeded)
            {
                TryRollback(batch);
                var rejection = transferResult.Rejection == InventoryTransferRejection.InsufficientStock
                    ? SeedDispenseRejection.InsufficientStock
                    : SeedDispenseRejection.InventoryRejected;
                return SeedDispenseResult.Reject(
                    rejection,
                    command.PlantTypeId,
                    definition.SeedItemId,
                    command.Quantity,
                    warehouseInventory.GetQuantity(definition.SeedItemId),
                    transferResult);
            }

            try
            {
                batch.Commit();
            }
            catch (Exception)
            {
                // Партия уже released, transfer зафиксирован. Commit только закрывает rollback handle.
            }
            return SeedDispenseResult.Success(
                command.PlantTypeId,
                definition.SeedItemId,
                command.Quantity,
                transferResult);
        }

        private static void TryRollback(IStagedSeedBatch batch)
        {
            try
            {
                batch.Rollback();
            }
            catch (Exception)
            {
                // Контракт запрещает исключения; inventory при этом не изменён.
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(PlantTypeId plantTypeId, int quantity, SeedDispenseContext context)
            {
                PlantTypeId = plantTypeId;
                Quantity = quantity;
                CommandId = context.CommandId;
                Reason = context.Reason;
                CorrelationId = context.CorrelationId;
            }

            public PlantTypeId PlantTypeId { get; }
            public int Quantity { get; }
            public Guid CommandId { get; }
            public string Reason { get; }
            public Guid CorrelationId { get; }

            public bool Equals(CommandPayload other) =>
                PlantTypeId.Equals(other.PlantTypeId) &&
                Quantity == other.Quantity &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;

            public override bool Equals(object obj) => obj is CommandPayload other && Equals(other);
            public override int GetHashCode() =>
                (((PlantTypeId.GetHashCode() * 397) ^ Quantity) * 397 ^
                 StringComparer.Ordinal.GetHashCode(Reason ?? string.Empty)) * 397 ^
                CorrelationId.GetHashCode();
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload command;

            public CompletedCommand(CommandPayload command, SeedDispenseResult result)
            {
                this.command = command;
                Result = result;
            }

            public SeedDispenseResult Result { get; }
            public bool Matches(CommandPayload candidate) => command.Equals(candidate);
        }
    }
}
