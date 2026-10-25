using System;
using System.Collections.Generic;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;

namespace VrGame.Domain.Harvesting
{
    public sealed class HarvestCoordinator
    {
        private readonly Inventory.Inventory inventory;
        private readonly PlantCatalog catalog;
        private readonly PlantingSlot slot;
        private readonly Dictionary<Guid, CompletedCommand> completedCommands =
            new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public HarvestCoordinator(
            Inventory.Inventory inventory,
            PlantCatalog catalog,
            PlantingSlot slot)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.slot = slot ?? throw new ArgumentNullException(nameof(slot));
        }

        public HarvestResult Harvest(HarvestContext context)
        {
            if (isExecuting)
                return HarvestResult.Reject(HarvestRejection.ReentrantCommand);
            if (!context.IsValid)
                return HarvestResult.Reject(HarvestRejection.InvalidContext);

            var payload = new CommandPayload(context.Reason, context.CorrelationId);
            if (completedCommands.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(payload))
                    return HarvestResult.Reject(HarvestRejection.CommandConflict);
                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = Execute(context);
                completedCommands.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private HarvestResult Execute(HarvestContext context)
        {
            if (!inventory.TryAcquireCoordinatedMutation())
                return HarvestResult.Reject(HarvestRejection.ReentrantCommand);
            if (!slot.TryAcquireCoordinatedMutation())
            {
                inventory.ReleaseCoordinatedMutation();
                return HarvestResult.Reject(HarvestRejection.ReentrantCommand);
            }

            try
            {
                var slotContext = new PlantingSlotChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var slotRejection = slot.TryPrepareCoordinatedHarvest(slotContext, out var preparedSlot);
                if (slotRejection != PlantingSlotRejection.None)
                    return HarvestResult.Reject(HarvestRejection.SlotRejected, slotRejection);
                if (!catalog.TryGet(preparedSlot.PlantTypeId, out var plant))
                    return HarvestResult.Reject(HarvestRejection.UnknownPlant);

                var inventoryContext = new InventoryChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var inventoryRejection = inventory.TryPrepareCoordinatedAdd(
                    plant.HarvestItemId,
                    plant.HarvestQuantity,
                    inventoryContext,
                    out var preparedInventory);
                if (inventoryRejection != InventoryRejection.None)
                    return HarvestResult.Reject(
                        HarvestRejection.InventoryRejected,
                        inventoryRejection: inventoryRejection,
                        itemId: plant.HarvestItemId,
                        quantity: plant.HarvestQuantity);

                var inventoryChange = inventory.CommitPreparedAdd(preparedInventory);
                var slotChange = slot.CommitPreparedHarvest(preparedSlot);
                var inventoryPublished = inventory.PublishPrepared(inventoryChange);
                var slotPublished = slot.PublishPrepared(slotChange);
                return HarvestResult.Success(
                    plant.HarvestItemId,
                    plant.HarvestQuantity,
                    inventoryChange,
                    slotChange,
                    inventoryPublished,
                    slotPublished);
            }
            finally
            {
                slot.ReleaseCoordinatedMutation();
                inventory.ReleaseCoordinatedMutation();
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(string reason, Guid correlationId)
            {
                Reason = reason;
                CorrelationId = correlationId;
            }

            private string Reason { get; }
            private Guid CorrelationId { get; }
            public bool Equals(CommandPayload other) =>
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;
            public CompletedCommand(CommandPayload payload, HarvestResult result)
            {
                this.payload = payload;
                Result = result;
            }
            public HarvestResult Result { get; }
            public bool Matches(CommandPayload candidate) => payload.Equals(candidate);
        }
    }
}
