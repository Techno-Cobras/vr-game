using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrGame.Domain.Inventory
{
    public sealed class InventorySnapshot
    {
        internal InventorySnapshot(
            InventoryId inventoryId,
            long version,
            IEnumerable<InventoryItemQuantity> items)
        {
            InventoryId = inventoryId;
            Version = version;
            Items = new ReadOnlyCollection<InventoryItemQuantity>(items.ToList());
        }

        public InventoryId InventoryId { get; }

        public long Version { get; }

        public IReadOnlyList<InventoryItemQuantity> Items { get; }
    }
}
