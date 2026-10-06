using VrGame.Data.Items;

namespace VrGame.Domain.Inventory
{
    public sealed class InventoryMutationResult
    {
        private InventoryMutationResult(
            InventoryRejection rejection,
            ItemId? failedItem,
            int requestedQuantity,
            int availableQuantity,
            InventoryChanged change,
            bool eventPublished)
        {
            Rejection = rejection;
            FailedItem = failedItem;
            RequestedQuantity = requestedQuantity;
            AvailableQuantity = availableQuantity;
            Change = change;
            EventPublished = eventPublished;
        }

        public bool Succeeded => Rejection == InventoryRejection.None;

        public InventoryRejection Rejection { get; }

        public ItemId? FailedItem { get; }

        public int RequestedQuantity { get; }

        public int AvailableQuantity { get; }

        public InventoryChanged Change { get; }

        public bool EventPublished { get; }

        internal static InventoryMutationResult Success(InventoryChanged change, bool eventPublished) =>
            new InventoryMutationResult(InventoryRejection.None, null, 0, 0, change, eventPublished);

        internal static InventoryMutationResult Reject(
            InventoryRejection rejection,
            ItemId? failedItem = null,
            int requestedQuantity = 0,
            int availableQuantity = 0) =>
            new InventoryMutationResult(rejection, failedItem, requestedQuantity, availableQuantity, null, false);
    }
}
