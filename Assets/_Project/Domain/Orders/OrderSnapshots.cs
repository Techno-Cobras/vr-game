using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;
using VrGame.Data.Orders;

namespace VrGame.Domain.Orders
{
    public sealed class OrderSnapshot
    {
        internal OrderSnapshot(
            OrderId orderId,
            OrderTemplateId templateId,
            string displayName,
            ItemId medicineItemId,
            int quantity,
            int reward,
            OrderState state,
            long version)
        {
            OrderId = orderId;
            TemplateId = templateId;
            DisplayName = displayName;
            MedicineItemId = medicineItemId;
            Quantity = quantity;
            Reward = reward;
            State = state;
            Version = version;
        }

        public OrderId OrderId { get; }
        public OrderTemplateId TemplateId { get; }
        public string DisplayName { get; }
        public ItemId MedicineItemId { get; }
        public int Quantity { get; }
        public int Reward { get; }
        public OrderState State { get; }
        public long Version { get; }
    }

    public sealed class OrderServiceSnapshot
    {
        internal OrderServiceSnapshot(long version, IEnumerable<OrderSnapshot> orders, OrderId? activeOrderId)
        {
            Version = version;
            Orders = new ReadOnlyCollection<OrderSnapshot>(orders.ToList());
            ActiveOrderId = activeOrderId;
        }

        public long Version { get; }
        public IReadOnlyList<OrderSnapshot> Orders { get; }
        public OrderId? ActiveOrderId { get; }
    }

    public readonly struct OrderReadiness
    {
        internal OrderReadiness(
            bool hasActiveOrder,
            OrderId? orderId,
            ItemId medicineItemId,
            int requiredQuantity,
            int availableQuantity)
        {
            HasActiveOrder = hasActiveOrder;
            OrderId = orderId;
            MedicineItemId = medicineItemId;
            RequiredQuantity = requiredQuantity;
            AvailableQuantity = availableQuantity;
        }

        public bool HasActiveOrder { get; }
        public OrderId? OrderId { get; }
        public ItemId MedicineItemId { get; }
        public int RequiredQuantity { get; }
        public int AvailableQuantity { get; }
        public int MissingQuantity => Math.Max(0, RequiredQuantity - AvailableQuantity);
        public bool IsReady => HasActiveOrder && AvailableQuantity >= RequiredQuantity;
    }
}
