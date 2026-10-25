using System;
using System.Collections.Generic;
using System.Linq;
using VrGame.Data.Fertilizers;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Fertilizers
{
    public sealed class FertilizerDispenseCoordinator
    {
        private readonly Inventory.Inventory warehouse;
        private readonly Inventory.Inventory player;
        private readonly FertilizerCatalog catalog;
        private readonly IFertilizerMaterializer materializer;
        private readonly Dictionary<Guid, CompletedCommand> completed = new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public FertilizerDispenseCoordinator(Inventory.Inventory warehouse, Inventory.Inventory player, FertilizerCatalog catalog, IFertilizerMaterializer materializer)
        {
            this.warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (ReferenceEquals(warehouse, player)) throw new ArgumentException("Inventories должны быть разными.", nameof(player));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
        }

        public FertilizerContainerSnapshot GetSnapshot() => new FertilizerContainerSnapshot(
            warehouse.Version,
            catalog.Definitions.OrderBy(value => value.Grade).Select(value =>
                new FertilizerStockEntry(value.Grade, value.ItemId, value.GrowthMultiplier, warehouse.GetQuantity(value.ItemId))));

        public FertilizerDispenseResult Dispense(FertilizerGrade grade, FertilizerCommandContext context)
        {
            if (isExecuting) return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.ReentrantCommand, grade);
            if (!context.IsValid) return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.InvalidContext, grade);
            var payload = new CommandPayload(grade, context.Reason, context.CorrelationId);
            if (completed.TryGetValue(context.CommandId, out var previous))
                return previous.Payload.Equals(payload) ? previous.Result.AsReplay() : FertilizerDispenseResult.Reject(FertilizerDispenseRejection.CommandConflict, grade);
            isExecuting = true;
            try
            {
                var result = Execute(grade, context);
                completed.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally { isExecuting = false; }
        }

        private FertilizerDispenseResult Execute(FertilizerGrade grade, FertilizerCommandContext context)
        {
            if (!catalog.TryGet(grade, out var definition))
                return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.UnknownGrade, grade);
            var available = warehouse.GetQuantity(definition.ItemId);
            if (available < 1)
                return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.InsufficientStock, grade, definition.ItemId, available);
            IStagedFertilizerItem staged = null;
            try
            {
                if (!materializer.TryStage(new FertilizerMaterializationRequest(grade, definition.ItemId), out staged) || staged == null)
                {
                    TryRollback(staged);
                    return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.MaterializationFailed, grade, definition.ItemId, available);
                }
            }
            catch (Exception)
            {
                TryRollback(staged);
                return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.MaterializationFailed, grade, definition.ItemId, available);
            }
            bool released;
            try { released = staged.TryRelease(); }
            catch (Exception) { released = false; }
            if (!released)
            {
                TryRollback(staged);
                return FertilizerDispenseResult.Reject(FertilizerDispenseRejection.MaterializationFailed, grade, definition.ItemId, available);
            }
            var transfer = warehouse.TransferTo(player, definition.ItemId, 1,
                new InventoryTransferContext(context.Reason, context.CommandId, context.CorrelationId));
            if (!transfer.Succeeded)
            {
                TryRollback(staged);
                return FertilizerDispenseResult.Reject(
                    transfer.Rejection == InventoryTransferRejection.InsufficientStock ? FertilizerDispenseRejection.InsufficientStock : FertilizerDispenseRejection.InventoryRejected,
                    grade, definition.ItemId, warehouse.GetQuantity(definition.ItemId), transfer);
            }
            var committed = false;
            try
            {
                committed = staged.TryCommit();
            }
            catch (Exception)
            {
                // Нарушивший контракт adapter не должен оставить выданный предмет без ownership.
            }
            if (!committed)
            {
                var compensation = player.TransferTo(
                    warehouse,
                    definition.ItemId,
                    1,
                    new InventoryTransferContext(
                        "откат выдачи удобрения после ошибки ownership",
                        Guid.NewGuid(),
                        context.CorrelationId));
                TryRollback(staged);
                return FertilizerDispenseResult.Reject(
                    compensation.Succeeded
                        ? FertilizerDispenseRejection.MaterializationFailed
                        : FertilizerDispenseRejection.InventoryRejected,
                    grade,
                    definition.ItemId,
                    warehouse.GetQuantity(definition.ItemId),
                    compensation);
            }
            return FertilizerDispenseResult.Success(grade, definition.ItemId, transfer);
        }

        private static void TryRollback(IStagedFertilizerItem value) { if (value == null) return; try { value.Rollback(); } catch (Exception) { } }
        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(FertilizerGrade grade, string reason, Guid correlation) { Grade = grade; Reason = reason; Correlation = correlation; }
            private FertilizerGrade Grade { get; }
            private string Reason { get; }
            private Guid Correlation { get; }
            public bool Equals(CommandPayload other) => Grade == other.Grade && string.Equals(Reason, other.Reason, StringComparison.Ordinal) && Correlation == other.Correlation;
        }
        private sealed class CompletedCommand
        {
            public CompletedCommand(CommandPayload payload, FertilizerDispenseResult result) { Payload = payload; Result = result; }
            public CommandPayload Payload { get; }
            public FertilizerDispenseResult Result { get; }
        }
    }
}
