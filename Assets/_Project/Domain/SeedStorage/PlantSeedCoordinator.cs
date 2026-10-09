using System;
using System.Collections.Generic;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;

namespace VrGame.Domain.SeedStorage
{
    public enum PlantSeedRejection
    {
        None = 0,
        InvalidSeed,
        InvalidContext,
        UnknownPlant,
        SeedIdentityMismatch,
        SeedUnavailable,
        SlotRejected,
        ReentrantCommand,
        CommandConflict
    }

    public readonly struct PlantSeedContext
    {
        public PlantSeedContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина посадки обязательна.", nameof(reason));
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

    public sealed class PlantSeedResult
    {
        private PlantSeedResult(
            PlantSeedRejection rejection,
            InventoryChanged inventoryChange,
            PlantingSlotChanged slotChange,
            bool inventoryEventPublished,
            bool slotEventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            InventoryChange = inventoryChange;
            SlotChange = slotChange;
            InventoryEventPublished = inventoryEventPublished;
            SlotEventPublished = slotEventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == PlantSeedRejection.None;
        public PlantSeedRejection Rejection { get; }
        public InventoryChanged InventoryChange { get; }
        public PlantingSlotChanged SlotChange { get; }
        public bool InventoryEventPublished { get; }
        public bool SlotEventPublished { get; }
        public bool IsReplay { get; }

        internal static PlantSeedResult Success(
            InventoryChanged inventoryChange,
            PlantingSlotChanged slotChange,
            bool inventoryEventPublished,
            bool slotEventPublished) =>
            new PlantSeedResult(
                PlantSeedRejection.None,
                inventoryChange,
                slotChange,
                inventoryEventPublished,
                slotEventPublished,
                false);

        internal static PlantSeedResult Reject(PlantSeedRejection rejection) =>
            new PlantSeedResult(rejection, null, null, false, false, false);

        public static PlantSeedResult InvalidSeed() => Reject(PlantSeedRejection.InvalidSeed);

        internal PlantSeedResult AsReplay() =>
            new PlantSeedResult(
                Rejection,
                InventoryChange,
                SlotChange,
                InventoryEventPublished,
                SlotEventPublished,
                true);
    }

    public sealed class PlantSeedCoordinator
    {
        private readonly Inventory.Inventory playerInventory;
        private readonly PlantCatalog plantCatalog;
        private readonly PlantingSlot plantingSlot;
        private readonly Dictionary<Guid, CompletedCommand> completedCommands =
            new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public PlantSeedCoordinator(
            Inventory.Inventory playerInventory,
            PlantCatalog plantCatalog,
            PlantingSlot plantingSlot)
        {
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            this.plantCatalog = plantCatalog ?? throw new ArgumentNullException(nameof(plantCatalog));
            this.plantingSlot = plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot));
        }

        public PlantSeedResult Plant(
            PlantTypeId plantTypeId,
            ItemId seedItemId,
            PlantSeedContext context)
        {
            if (isExecuting)
                return PlantSeedResult.Reject(PlantSeedRejection.ReentrantCommand);
            if (!context.IsValid)
                return PlantSeedResult.Reject(PlantSeedRejection.InvalidContext);

            var payload = new CommandPayload(plantTypeId, seedItemId, context);
            if (completedCommands.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(payload))
                    return PlantSeedResult.Reject(PlantSeedRejection.CommandConflict);
                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = Execute(plantTypeId, seedItemId, context);
                completedCommands.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private PlantSeedResult Execute(
            PlantTypeId plantTypeId,
            ItemId seedItemId,
            PlantSeedContext context)
        {
            if (!plantCatalog.TryGet(plantTypeId, out var definition))
                return PlantSeedResult.Reject(PlantSeedRejection.UnknownPlant);
            if (definition.SeedItemId != seedItemId)
                return PlantSeedResult.Reject(PlantSeedRejection.SeedIdentityMismatch);

            if (!playerInventory.TryAcquireCoordinatedMutation())
                return PlantSeedResult.Reject(PlantSeedRejection.ReentrantCommand);
            if (!plantingSlot.TryAcquireCoordinatedMutation())
            {
                playerInventory.ReleaseCoordinatedMutation();
                return PlantSeedResult.Reject(PlantSeedRejection.ReentrantCommand);
            }

            try
            {
                var inventoryContext = new InventoryChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var inventoryRejection = playerInventory.TryPrepareCoordinatedRemove(
                    seedItemId,
                    1,
                    inventoryContext,
                    out var preparedInventory);
                if (inventoryRejection != InventoryRejection.None)
                    return PlantSeedResult.Reject(PlantSeedRejection.SeedUnavailable);

                var slotContext = new PlantingSlotChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var slotRejection = plantingSlot.TryPrepareCoordinatedPlant(
                    plantTypeId,
                    slotContext,
                    out var preparedSlot);
                if (slotRejection != PlantingSlotRejection.None)
                    return PlantSeedResult.Reject(PlantSeedRejection.SlotRejected);

                var inventoryChange = playerInventory.CommitPreparedRemove(preparedInventory);
                var slotChange = plantingSlot.CommitPreparedPlant(preparedSlot);
                var inventoryPublished = playerInventory.PublishPrepared(inventoryChange);
                var slotPublished = plantingSlot.PublishPrepared(slotChange);
                return PlantSeedResult.Success(
                    inventoryChange,
                    slotChange,
                    inventoryPublished,
                    slotPublished);
            }
            finally
            {
                plantingSlot.ReleaseCoordinatedMutation();
                playerInventory.ReleaseCoordinatedMutation();
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(
                PlantTypeId plantTypeId,
                ItemId seedItemId,
                PlantSeedContext context)
            {
                PlantTypeId = plantTypeId;
                SeedItemId = seedItemId;
                Reason = context.Reason;
                CorrelationId = context.CorrelationId;
            }

            private PlantTypeId PlantTypeId { get; }
            private ItemId SeedItemId { get; }
            private string Reason { get; }
            private Guid CorrelationId { get; }

            public bool Equals(CommandPayload other) =>
                PlantTypeId.Equals(other.PlantTypeId) &&
                SeedItemId.Equals(other.SeedItemId) &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
            public override bool Equals(object obj) => obj is CommandPayload other && Equals(other);
            public override int GetHashCode() =>
                ((PlantTypeId.GetHashCode() * 397) ^ SeedItemId.GetHashCode()) * 397 ^
                CorrelationId.GetHashCode();
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;

            public CompletedCommand(CommandPayload payload, PlantSeedResult result)
            {
                this.payload = payload;
                Result = result;
            }

            public PlantSeedResult Result { get; }
            public bool Matches(CommandPayload candidate) => payload.Equals(candidate);
        }
    }
}
