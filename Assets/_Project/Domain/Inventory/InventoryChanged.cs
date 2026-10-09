using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrGame.Domain.Inventory
{
    public sealed class InventoryChanged
    {
        internal InventoryChanged(
            InventoryId inventoryId,
            long version,
            InventoryMutationKind kind,
            InventoryChangeContext context,
            IEnumerable<InventoryQuantityChange> changes)
        {
            InventoryId = inventoryId;
            Version = version;
            Kind = kind;
            Reason = context.Reason;
            CommandId = context.CommandId;
            CorrelationId = context.CorrelationId;
            Changes = new ReadOnlyCollection<InventoryQuantityChange>(changes.ToList());
        }

        public InventoryId InventoryId { get; }

        public long Version { get; }

        public InventoryMutationKind Kind { get; }

        public string Reason { get; }

        public Guid CommandId { get; }

        public Guid CorrelationId { get; }

        public IReadOnlyList<InventoryQuantityChange> Changes { get; }
    }
}
