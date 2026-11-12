using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Shop;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Delivery
{
    public enum DeliveryStatus { Pending, Available, Claimed }

    public sealed class PendingDeliveryEntry
    {
        internal PendingDeliveryEntry(long sequence, ShopOfferDefinition offer, Guid purchaseCommandId)
        {
            Id = Guid.NewGuid();
            Sequence = sequence;
            ProductId = offer.Id;
            ItemId = offer.ItemId;
            Quantity = offer.Quantity;
            Representation = offer.Representation;
            PurchaseCommandId = purchaseCommandId;
            Status = DeliveryStatus.Pending;
        }

        internal PendingDeliveryEntry(PendingDeliveryEntry source, DeliveryStatus status)
        {
            Id = source.Id;
            Sequence = source.Sequence;
            ProductId = source.ProductId;
            ItemId = source.ItemId;
            Quantity = source.Quantity;
            Representation = source.Representation;
            PurchaseCommandId = source.PurchaseCommandId;
            Status = status;
        }

        public DeliveryStatus Status { get; }
        public Guid Id { get; }
        public long Sequence { get; }
        public ProductId ProductId { get; }
        public ItemId ItemId { get; }
        public int Quantity { get; }
        public ItemRepresentationKey Representation { get; }
        public Guid PurchaseCommandId { get; }
    }

    // Authoritative pending purchases;
    // Physical representations belong to the delivery adapter.
    public sealed class PendingDeliveryLedger
    {
        private readonly List<PendingDeliveryEntry> entries = new List<PendingDeliveryEntry>();
        private bool locked;
        private readonly Dictionary<Guid, ClaimCommand> claims = new Dictionary<Guid, ClaimCommand>();
        public event Action<PendingDeliveryEntry> Added;
        public event Action<PendingDeliveryEntry> Changed;
        public IReadOnlyList<PendingDeliveryEntry> GetSnapshot() => new ReadOnlyCollection<PendingDeliveryEntry>(entries.ToList());
        internal bool TryAcquire()
        {
            if (locked)
                return false;
            locked = true;
            return true;
        }

        internal void Release() => locked = false;
        internal PendingDeliveryEntry Prepare(ShopOfferDefinition offer, Guid command)
        {
            if (entries.Count == int.MaxValue)
                throw new InvalidOperationException("Ёмкость очереди доставки исчерпана.");
            return new PendingDeliveryEntry(entries.Count + 1L, offer, command);
        }
        internal void Commit(PendingDeliveryEntry entry) => entries.Add(entry);
        internal bool Publish(PendingDeliveryEntry entry)
        {
            var success = true;
            var handlers = Added?.GetInvocationList();
            if (handlers != null)
                foreach (Action<PendingDeliveryEntry> handler in handlers)
                    try
                    {
                        handler(entry);
                    }
                    catch (Exception)
                    {
                        success = false;
                    }

            return PublishChanged(entry) && success;
        }
        public bool TryGetNextPending(out PendingDeliveryEntry entry)
        {
            entry = entries.FirstOrDefault(value => value.Status == DeliveryStatus.Pending);
            return entry != null;
        }

        public DeliveryMutationResult MarkAvailable(Guid entryId)
        {
            if (!TryAcquire()) return DeliveryMutationResult.Reject(DeliveryRejection.ReentrantMutation);
            try
            {
                var index = entries.FindIndex(value => value.Id == entryId);
                if (index < 0) return DeliveryMutationResult.Reject(DeliveryRejection.UnknownEntry);
                var entry = entries[index];
                if (entry.Status == DeliveryStatus.Available)
                    return DeliveryMutationResult.Success(entry, replay: true, published: false);
                if (entry.Status != DeliveryStatus.Pending) return DeliveryMutationResult.Reject(DeliveryRejection.InvalidState);
                entry = new PendingDeliveryEntry(entry, DeliveryStatus.Available);
                entries[index] = entry;
                return DeliveryMutationResult.Success(entry, published: PublishChanged(entry));
            }
            finally { Release(); }
        }

        public DeliveryMutationResult Claim(Guid entryId, Inventory.Inventory inventory, InventoryChangeContext context)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!TryAcquire()) return DeliveryMutationResult.Reject(DeliveryRejection.ReentrantMutation);
            try
            {
                if (!context.IsValid || context.CommandId == Guid.Empty)
                    return DeliveryMutationResult.Reject(DeliveryRejection.InvalidContext);
                if (claims.TryGetValue(context.CommandId, out var command))
                {
                    if (!command.Matches(entryId, inventory.Id, context))
                        return DeliveryMutationResult.Reject(DeliveryRejection.CommandConflict);
                    if (command.Result != null)
                        return command.Result.AsReplay();
                }
                else
                {
                    command = new ClaimCommand(entryId, inventory.Id, context);
                    claims.Add(context.CommandId, command);
                }
                var index = entries.FindIndex(value => value.Id == entryId);
                if (index < 0) return DeliveryMutationResult.Reject(DeliveryRejection.UnknownEntry);
                var entry = entries[index];
                if (entry.Status == DeliveryStatus.Claimed)
                    return DeliveryMutationResult.Reject(DeliveryRejection.AlreadyClaimed);
                if (entry.Status != DeliveryStatus.Available)
                    return DeliveryMutationResult.Reject(DeliveryRejection.InvalidState);
                if (!inventory.TryAcquireCoordinatedMutation())
                    return DeliveryMutationResult.Reject(DeliveryRejection.ReentrantMutation);
                try
                {
                    var rejection = inventory.TryPrepareCoordinatedAdd(entry.ItemId, entry.Quantity, context, out var prepared);
                    if (rejection != InventoryRejection.None)
                        return DeliveryMutationResult.Reject(DeliveryRejection.InventoryRejected, rejection);
                    var claimed = new PendingDeliveryEntry(entry, DeliveryStatus.Claimed);
                    var change = inventory.CommitPreparedAdd(prepared);
                    entries[index] = claimed;
                    var result = DeliveryMutationResult.Success(claimed);
                    command.Complete(result);
                    result.InventoryEventPublished = inventory.PublishPrepared(change);
                    result.QueueEventPublished = PublishChanged(claimed);
                    return result;
                }
                finally { inventory.ReleaseCoordinatedMutation(); }
            }
            finally { Release(); }
        }

        private bool PublishChanged(PendingDeliveryEntry entry)
        {
            var success = true;
            var handlers = Changed?.GetInvocationList();
            if (handlers != null)
                foreach (Action<PendingDeliveryEntry> handler in handlers)
                    try { handler(entry); }
                    catch (Exception) { success = false; }
            return success;
        }

        private sealed class ClaimCommand
        {
            private readonly Guid entryId;
            private readonly InventoryId inventoryId;
            private readonly InventoryChangeContext context;
            public ClaimCommand(Guid entryId, InventoryId inventoryId, InventoryChangeContext context)
            {
                this.entryId = entryId;
                this.inventoryId = inventoryId;
                this.context = context;
            }
            public DeliveryMutationResult Result { get; private set; }
            public void Complete(DeliveryMutationResult result) => Result = result;
            public bool Matches(Guid entry, InventoryId inventory, InventoryChangeContext candidate) =>
                entryId == entry && inventoryId.Equals(inventory) && context.Reason == candidate.Reason && context.CorrelationId == candidate.CorrelationId;
        }
    }
}
