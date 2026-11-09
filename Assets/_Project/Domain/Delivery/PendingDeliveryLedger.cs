using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Shop;
using VrGame.Data.Items;

namespace VrGame.Domain.Delivery
{
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
        }

        public Guid Id { get; }
        public long Sequence { get; }
        public ProductId ProductId { get; }
        public ItemId ItemId { get; }
        public int Quantity { get; }
        public ItemRepresentationKey Representation { get; }
        public Guid PurchaseCommandId { get; }
    }

    // Authoritative pending purchases;
    // Spawn/claim lifecycle belongs to the delivery subsystem.
    public sealed class PendingDeliveryLedger
    {
        private readonly List<PendingDeliveryEntry> entries = new List<PendingDeliveryEntry>();
        private bool locked;
        public event Action<PendingDeliveryEntry> Added;
        public IReadOnlyList<PendingDeliveryEntry> GetSnapshot() => new ReadOnlyCollection<PendingDeliveryEntry>(entries.ToList());
        internal bool TryAcquire()
        {
            if (locked || entries.Count == int.MaxValue)
                return false;
            locked = true;
            return true;
        }

        internal void Release() => locked = false;
        internal PendingDeliveryEntry Prepare(ShopOfferDefinition offer, Guid command) => new PendingDeliveryEntry(entries.Count + 1L, offer, command);
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

            return success;
        }
    }
}
