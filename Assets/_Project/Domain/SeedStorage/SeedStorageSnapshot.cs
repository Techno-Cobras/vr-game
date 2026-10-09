using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;
using VrGame.Data.Plants;

namespace VrGame.Domain.SeedStorage
{
    public readonly struct SeedStockEntry
    {
        public SeedStockEntry(PlantTypeId plantTypeId, ItemId seedItemId, int quantity)
        {
            PlantTypeId = plantTypeId;
            SeedItemId = seedItemId;
            Quantity = quantity;
        }

        public PlantTypeId PlantTypeId { get; }
        public ItemId SeedItemId { get; }
        public int Quantity { get; }
    }

    public sealed class SeedStorageSnapshot
    {
        public SeedStorageSnapshot(long inventoryVersion, IEnumerable<SeedStockEntry> entries)
        {
            InventoryVersion = inventoryVersion;
            Entries = new ReadOnlyCollection<SeedStockEntry>(entries.ToList());
        }

        public long InventoryVersion { get; }
        public IReadOnlyList<SeedStockEntry> Entries { get; }
    }
}
