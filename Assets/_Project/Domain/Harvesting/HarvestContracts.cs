using System;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;

namespace VrGame.Domain.Harvesting
{
    public enum HarvestRejection
    {
        None = 0,
        InvalidContext,
        SlotRejected,
        InventoryRejected,
        UnknownPlant,
        ReentrantCommand,
        CommandConflict
    }

    public readonly struct HarvestContext
    {
        public HarvestContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина сбора урожая обязательна.", nameof(reason));
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
            !string.IsNullOrWhiteSpace(Reason) && CommandId != Guid.Empty && CorrelationId != Guid.Empty;
    }

    public sealed class HarvestResult
    {
        private HarvestResult(
            HarvestRejection rejection,
            PlantingSlotRejection slotRejection,
            InventoryRejection inventoryRejection,
            ItemId harvestItemId,
            int quantity,
            InventoryChanged inventoryChange,
            PlantingSlotChanged slotChange,
            bool inventoryEventPublished,
            bool slotEventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            SlotRejection = slotRejection;
            InventoryRejection = inventoryRejection;
            HarvestItemId = harvestItemId;
            Quantity = quantity;
            InventoryChange = inventoryChange;
            SlotChange = slotChange;
            InventoryEventPublished = inventoryEventPublished;
            SlotEventPublished = slotEventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == HarvestRejection.None;
        public HarvestRejection Rejection { get; }
        public PlantingSlotRejection SlotRejection { get; }
        public InventoryRejection InventoryRejection { get; }
        public ItemId HarvestItemId { get; }
        public int Quantity { get; }
        public InventoryChanged InventoryChange { get; }
        public PlantingSlotChanged SlotChange { get; }
        public bool InventoryEventPublished { get; }
        public bool SlotEventPublished { get; }
        public bool IsReplay { get; }

        internal static HarvestResult Success(
            ItemId itemId,
            int quantity,
            InventoryChanged inventoryChange,
            PlantingSlotChanged slotChange,
            bool inventoryPublished,
            bool slotPublished) =>
            new HarvestResult(
                HarvestRejection.None,
                PlantingSlotRejection.None,
                InventoryRejection.None,
                itemId,
                quantity,
                inventoryChange,
                slotChange,
                inventoryPublished,
                slotPublished,
                false);

        internal static HarvestResult Reject(
            HarvestRejection rejection,
            PlantingSlotRejection slotRejection = PlantingSlotRejection.None,
            InventoryRejection inventoryRejection = InventoryRejection.None,
            ItemId itemId = default,
            int quantity = 0) =>
            new HarvestResult(
                rejection,
                slotRejection,
                inventoryRejection,
                itemId,
                quantity,
                null,
                null,
                false,
                false,
                false);

        internal HarvestResult AsReplay() =>
            new HarvestResult(
                Rejection,
                SlotRejection,
                InventoryRejection,
                HarvestItemId,
                Quantity,
                InventoryChange,
                SlotChange,
                InventoryEventPublished,
                SlotEventPublished,
                true);
    }
}
