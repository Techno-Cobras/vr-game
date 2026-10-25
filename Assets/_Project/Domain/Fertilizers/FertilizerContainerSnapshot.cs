using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;

namespace VrGame.Domain.Fertilizers
{
    public readonly struct FertilizerStockEntry
    {
        public FertilizerStockEntry(FertilizerGrade grade, ItemId itemId, float multiplier, int quantity)
        { Grade = grade; ItemId = itemId; Multiplier = multiplier; Quantity = quantity; }
        public FertilizerGrade Grade { get; }
        public ItemId ItemId { get; }
        public float Multiplier { get; }
        public int Quantity { get; }
    }

    public sealed class FertilizerContainerSnapshot
    {
        public FertilizerContainerSnapshot(long inventoryVersion, IEnumerable<FertilizerStockEntry> entries)
        {
            InventoryVersion = inventoryVersion;
            Entries = new ReadOnlyCollection<FertilizerStockEntry>(entries.ToList());
        }
        public long InventoryVersion { get; }
        public IReadOnlyList<FertilizerStockEntry> Entries { get; }
    }
}
