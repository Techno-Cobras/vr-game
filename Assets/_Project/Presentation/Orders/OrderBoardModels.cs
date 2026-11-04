using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;
using VrGame.Domain.Orders;

namespace VrGame.Presentation.Orders
{
    public enum OrderBoardFeedback
    {
        None = 0,
        Accepted,
        ActiveOrderExists,
        Unavailable,
        Failure,
        Replay
    }

    public interface IOrderChangeSource
    {
        event Action<OrderChanged> Changed;
    }

    public interface IOrderBoardView
    {
        void Render(OrderBoardSnapshot snapshot);
    }

    public sealed class OrderBoardEntry
    {
        public OrderBoardEntry(
            OrderId orderId,
            string displayName,
            ItemId medicineItemId,
            string medicineDisplayName,
            int quantity,
            int reward,
            OrderState state,
            bool isActive)
        {
            OrderId = orderId;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            MedicineItemId = medicineItemId;
            MedicineDisplayName = medicineDisplayName ?? throw new ArgumentNullException(nameof(medicineDisplayName));
            Quantity = quantity;
            Reward = reward;
            State = state;
            IsActive = isActive;
        }

        public OrderId OrderId { get; }
        public string DisplayName { get; }
        public ItemId MedicineItemId { get; }
        public string MedicineDisplayName { get; }
        public int Quantity { get; }
        public int Reward { get; }
        public OrderState State { get; }
        public bool IsActive { get; }
        public bool IsAvailable => State == OrderState.Available;
    }

    public sealed class OrderBoardSnapshot
    {
        public OrderBoardSnapshot(
            IEnumerable<OrderBoardEntry> orders,
            int selectedIndex,
            bool canInteract,
            OrderBoardFeedback feedback,
            OrderMutationResult lastResult)
        {
            Orders = new ReadOnlyCollection<OrderBoardEntry>(
                (orders ?? throw new ArgumentNullException(nameof(orders))).ToList());
            SelectedIndex = Orders.Count == 0
                ? 0
                : Math.Max(0, Math.Min(selectedIndex, Orders.Count - 1));
            CanInteract = canInteract;
            Feedback = feedback;
            LastResult = lastResult;
        }

        public IReadOnlyList<OrderBoardEntry> Orders { get; }
        public int SelectedIndex { get; }
        public bool CanInteract { get; }
        public OrderBoardFeedback Feedback { get; }
        public OrderMutationResult LastResult { get; }
        public OrderBoardEntry SelectedOrder => Orders.Count == 0 ? null : Orders[SelectedIndex];
        public OrderBoardEntry ActiveOrder => Orders.FirstOrDefault(value => value.IsActive);
        public bool CanAcceptSelected =>
            CanInteract && ActiveOrder == null && SelectedOrder != null && SelectedOrder.IsAvailable;
    }
}
