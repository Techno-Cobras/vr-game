using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using VrGame.Domain.Plants;

namespace VrGame.Domain.Fertilizers
{
    public sealed class FertilizerDispenseResult
    {
        private FertilizerDispenseResult(FertilizerDispenseRejection rejection, FertilizerGrade grade, ItemId itemId, int available, InventoryTransferResult transfer, bool replay)
        { Rejection = rejection; Grade = grade; ItemId = itemId; AvailableQuantity = available; TransferResult = transfer; IsReplay = replay; }
        public bool Succeeded => Rejection == FertilizerDispenseRejection.None;
        public FertilizerDispenseRejection Rejection { get; }
        public FertilizerGrade Grade { get; }
        public ItemId ItemId { get; }
        public int AvailableQuantity { get; }
        public InventoryTransferResult TransferResult { get; }
        public bool IsReplay { get; }
        internal static FertilizerDispenseResult Success(FertilizerGrade grade, ItemId itemId, InventoryTransferResult transfer) =>
            new FertilizerDispenseResult(FertilizerDispenseRejection.None, grade, itemId, transfer.SourceChange.Changes[0].After, transfer, false);
        internal static FertilizerDispenseResult Reject(FertilizerDispenseRejection rejection, FertilizerGrade grade = default, ItemId itemId = default, int available = 0, InventoryTransferResult transfer = null) =>
            new FertilizerDispenseResult(rejection, grade, itemId, available, transfer, false);
        internal FertilizerDispenseResult AsReplay() => new FertilizerDispenseResult(Rejection, Grade, ItemId, AvailableQuantity, TransferResult, true);
    }

    public sealed class FertilizerApplicationResult
    {
        private FertilizerApplicationResult(
            FertilizerApplicationRejection rejection,
            PlantingSlotRejection slotRejection,
            InventoryChanged inventoryChange,
            PlantingSlotChanged slotChange,
            bool inventoryPublished,
            bool slotPublished,
            bool replay)
        {
            Rejection = rejection;
            SlotRejection = slotRejection;
            InventoryChange = inventoryChange;
            SlotChange = slotChange;
            InventoryEventPublished = inventoryPublished;
            SlotEventPublished = slotPublished;
            IsReplay = replay;
        }
        public bool Succeeded => Rejection == FertilizerApplicationRejection.None;
        public FertilizerApplicationRejection Rejection { get; }
        public PlantingSlotRejection SlotRejection { get; }
        public InventoryChanged InventoryChange { get; }
        public PlantingSlotChanged SlotChange { get; }
        public bool InventoryEventPublished { get; }
        public bool SlotEventPublished { get; }
        public bool IsReplay { get; }
        internal static FertilizerApplicationResult Success(InventoryChanged inventory, PlantingSlotChanged slot, bool inventoryPublished, bool slotPublished) =>
            new FertilizerApplicationResult(
                FertilizerApplicationRejection.None,
                PlantingSlotRejection.None,
                inventory,
                slot,
                inventoryPublished,
                slotPublished,
                false);
        internal static FertilizerApplicationResult Reject(
            FertilizerApplicationRejection rejection,
            PlantingSlotRejection slotRejection = PlantingSlotRejection.None) =>
            new FertilizerApplicationResult(rejection, slotRejection, null, null, false, false, false);
        public static FertilizerApplicationResult InvalidItem() => Reject(FertilizerApplicationRejection.ItemIdentityMismatch);
        internal FertilizerApplicationResult AsReplay() => new FertilizerApplicationResult(
            Rejection,
            SlotRejection,
            InventoryChange,
            SlotChange,
            InventoryEventPublished,
            SlotEventPublished,
            true);
    }
}
