using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Crafting
{
    public sealed class CraftCompleted
    {
        internal CraftCompleted(
            RecipeId recipeId,
            ItemId outputItemId,
            int outputQuantity,
            CraftContext context,
            InventoryChanged inventoryChange)
        {
            RecipeId = recipeId;
            OutputItemId = outputItemId;
            OutputQuantity = outputQuantity;
            Reason = context.Reason;
            CommandId = context.CommandId;
            CorrelationId = context.CorrelationId;
            InventoryId = inventoryChange.InventoryId;
            InventoryVersion = inventoryChange.Version;
            Changes = new ReadOnlyCollection<InventoryQuantityChange>(inventoryChange.Changes.ToList());
        }

        public RecipeId RecipeId { get; }
        public ItemId OutputItemId { get; }
        public int OutputQuantity { get; }
        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public InventoryId InventoryId { get; }
        public long InventoryVersion { get; }
        public IReadOnlyList<InventoryQuantityChange> Changes { get; }
    }
}
