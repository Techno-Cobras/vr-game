using VrGame.Domain.Inventory;

namespace VrGame.Domain.Delivery
{
    public enum DeliveryRejection
    {
        None, UnknownEntry, InvalidState, AlreadyClaimed, InvalidContext,
        CommandConflict, ReentrantMutation, InventoryRejected
    }

    public sealed class DeliveryMutationResult
    {
        private DeliveryMutationResult(DeliveryRejection rejection, PendingDeliveryEntry entry,
            InventoryRejection inventoryRejection, bool replay, bool published)
        {
            Rejection = rejection;
            Entry = entry;
            InventoryRejection = inventoryRejection;
            IsReplay = replay;
            QueueEventPublished = published;
        }
        public bool Succeeded => Rejection == DeliveryRejection.None;
        public DeliveryRejection Rejection { get; }
        public InventoryRejection InventoryRejection { get; }
        public PendingDeliveryEntry Entry { get; }
        public bool IsReplay { get; }
        public bool QueueEventPublished { get; internal set; }
        public bool InventoryEventPublished { get; internal set; }
        internal static DeliveryMutationResult Reject(DeliveryRejection rejection, InventoryRejection inventory = InventoryRejection.None) =>
            new DeliveryMutationResult(rejection, null, inventory, false, false);
        internal static DeliveryMutationResult Success(PendingDeliveryEntry entry, bool replay = false, bool published = true) =>
            new DeliveryMutationResult(DeliveryRejection.None, entry, InventoryRejection.None, replay, published);
        internal DeliveryMutationResult AsReplay() => new DeliveryMutationResult(Rejection, Entry, InventoryRejection, true, QueueEventPublished)
        { InventoryEventPublished = InventoryEventPublished };
    }
}
