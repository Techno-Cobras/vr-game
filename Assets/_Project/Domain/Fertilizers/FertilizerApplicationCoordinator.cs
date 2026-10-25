using System;
using System.Collections.Generic;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;

namespace VrGame.Domain.Fertilizers
{
    public sealed class FertilizerApplicationCoordinator
    {
        private readonly Inventory.Inventory inventory;
        private readonly FertilizerCatalog catalog;
        private readonly PlantingSlot slot;
        private readonly Dictionary<Guid, CompletedCommand> completed = new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public FertilizerApplicationCoordinator(Inventory.Inventory inventory, FertilizerCatalog catalog, PlantingSlot slot)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.slot = slot ?? throw new ArgumentNullException(nameof(slot));
        }

        public FertilizerApplicationResult Apply(FertilizerGrade grade, ItemId itemId, FertilizerCommandContext context)
        {
            if (isExecuting) return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.ReentrantCommand);
            if (!context.IsValid) return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.InvalidContext);
            var payload = new CommandPayload(grade, itemId, context.Reason, context.CorrelationId);
            if (completed.TryGetValue(context.CommandId, out var previous))
                return previous.Payload.Equals(payload)
                    ? previous.Result.AsReplay()
                    : FertilizerApplicationResult.Reject(FertilizerApplicationRejection.CommandConflict);

            isExecuting = true;
            try
            {
                var result = Execute(grade, itemId, context);
                completed.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally { isExecuting = false; }
        }

        private FertilizerApplicationResult Execute(FertilizerGrade grade, ItemId itemId, FertilizerCommandContext context)
        {
            if (!catalog.TryGet(grade, out var definition))
                return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.UnknownGrade);
            if (definition.ItemId != itemId)
                return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.ItemIdentityMismatch);
            if (!inventory.TryAcquireCoordinatedMutation())
                return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.ReentrantCommand);
            if (!slot.TryAcquireCoordinatedMutation())
            {
                inventory.ReleaseCoordinatedMutation();
                return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.ReentrantCommand);
            }

            try
            {
                var inventoryContext = new InventoryChangeContext(context.Reason, context.CommandId, context.CorrelationId);
                var inventoryRejection = inventory.TryPrepareCoordinatedRemove(itemId, 1, inventoryContext, out var preparedInventory);
                if (inventoryRejection != InventoryRejection.None)
                    return FertilizerApplicationResult.Reject(FertilizerApplicationRejection.ItemUnavailable);
                var slotContext = new PlantingSlotChangeContext(context.Reason, context.CommandId, context.CorrelationId);
                var slotRejection = slot.TryPrepareCoordinatedFertilizer(
                    new PlantGrowthModifier(definition.GrowthMultiplier), slotContext, out var preparedSlot);
                if (slotRejection != PlantingSlotRejection.None)
                    return FertilizerApplicationResult.Reject(
                        FertilizerApplicationRejection.SlotRejected,
                        slotRejection);

                var inventoryChange = inventory.CommitPreparedRemove(preparedInventory);
                var slotChange = slot.CommitPreparedFertilizer(preparedSlot);
                return FertilizerApplicationResult.Success(
                    inventoryChange, slotChange, inventory.PublishPrepared(inventoryChange), slot.PublishPrepared(slotChange));
            }
            finally
            {
                slot.ReleaseCoordinatedMutation();
                inventory.ReleaseCoordinatedMutation();
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(FertilizerGrade grade, ItemId itemId, string reason, Guid correlation)
            { Grade = grade; ItemId = itemId; Reason = reason; Correlation = correlation; }
            private FertilizerGrade Grade { get; }
            private ItemId ItemId { get; }
            private string Reason { get; }
            private Guid Correlation { get; }
            public bool Equals(CommandPayload other) => Grade == other.Grade && ItemId == other.ItemId &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) && Correlation == other.Correlation;
        }

        private sealed class CompletedCommand
        {
            public CompletedCommand(CommandPayload payload, FertilizerApplicationResult result) { Payload = payload; Result = result; }
            public CommandPayload Payload { get; }
            public FertilizerApplicationResult Result { get; }
        }
    }
}
