using VrGame.Data.Items;

namespace VrGame.Domain.Inventory
{
    public enum InventoryMutationKind
    {
        Add = 1,
        Remove = 2,
        ConsumeBatch = 3,
        TransferOut = 4,
        TransferIn = 5
    }

    public enum InventoryRejection
    {
        None = 0,
        InvalidContext,
        InvalidQuantity,
        UnknownItem,
        InsufficientStock,
        ArithmeticOverflow,
        InvalidBatch,
        ReentrantMutation
    }

    public readonly struct InventoryItemQuantity
    {
        public InventoryItemQuantity(ItemId itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public ItemId ItemId { get; }

        public int Quantity { get; }
    }

    public readonly struct InventoryQuantityChange
    {
        public InventoryQuantityChange(ItemId itemId, int before, int after)
        {
            ItemId = itemId;
            Before = before;
            After = after;
        }

        public ItemId ItemId { get; }

        public int Before { get; }

        public int After { get; }

        public int Delta => After - Before;
    }
}
