using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.Domain.Orders;

namespace VrGame.Presentation.Orders
{
    public sealed class OrderBoardController : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour viewBehaviour;

        private OrderService service;
        private ItemCatalog items;
        private IOrderChangeSource changes;
        private IOrderBoardView view;
        private OrderBoardSnapshot snapshot;
        private OrderMutationResult lastResult;
        private OrderBoardFeedback feedback;
        private int selectedIndex;
        private bool subscribed;
        private bool isSubmitting;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private int lastIntentFrame = -1;
        private OrderId lastIntentOrderId;
        private OrderMutationResult lastIntentResult;

        public event Action<OrderBoardSnapshot> SnapshotChanged;
        public event Action<OrderMutationResult> AcceptanceCompleted;

        public bool CanInteract =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            service != null && items != null && view != null;
        public int SelectedIndex => selectedIndex;
        public OrderBoardSnapshot Snapshot => snapshot;

        public void Configure(
            OrderService orderService,
            ItemCatalog itemCatalog,
            IOrderChangeSource changeSource,
            IOrderBoardView boardView = null)
        {
            Unsubscribe();
            service = orderService ?? throw new ArgumentNullException(nameof(orderService));
            items = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
            changes = changeSource ?? throw new ArgumentNullException(nameof(changeSource));
            view = boardView ?? viewBehaviour as IOrderBoardView;
            if (view == null)
                throw new InvalidOperationException("Представление Order Board не настроено.");
            Subscribe();
            RefreshView();
        }

        public void SelectPreviousOrder()
        {
            if (!CanInteract || snapshot == null || snapshot.Orders.Count == 0)
                return;
            selectedIndex = (selectedIndex - 1 + snapshot.Orders.Count) % snapshot.Orders.Count;
            ResetIntent();
            feedback = OrderBoardFeedback.None;
            lastResult = null;
            RefreshView();
        }

        public void SelectNextOrder()
        {
            if (!CanInteract || snapshot == null || snapshot.Orders.Count == 0)
                return;
            selectedIndex = (selectedIndex + 1) % snapshot.Orders.Count;
            ResetIntent();
            feedback = OrderBoardFeedback.None;
            lastResult = null;
            RefreshView();
        }

        public OrderMutationResult AcceptSelected()
        {
            if (!CanInteract || isSubmitting || snapshot?.SelectedOrder == null)
                return null;

            var selected = snapshot.SelectedOrder;
            if (lastIntentFrame == Time.frameCount && lastIntentOrderId == selected.OrderId)
                return lastIntentResult;

            lastIntentFrame = Time.frameCount;
            lastIntentOrderId = selected.OrderId;
            lastIntentResult = null;

            if (snapshot.ActiveOrder != null)
            {
                feedback = OrderBoardFeedback.ActiveOrderExists;
                lastResult = null;
                RefreshView();
                return null;
            }
            if (!selected.IsAvailable)
            {
                feedback = OrderBoardFeedback.Unavailable;
                lastResult = null;
                RefreshView();
                return null;
            }

            isSubmitting = true;
            try
            {
                var context = new OrderContext(
                    "Принятие заказа на VR Order Board",
                    Guid.NewGuid(),
                    Guid.NewGuid());
                lastResult = service.Accept(selected.OrderId, context);
                lastIntentResult = lastResult;
                feedback = lastResult.Succeeded
                    ? lastResult.IsReplay ? OrderBoardFeedback.Replay : OrderBoardFeedback.Accepted
                    : lastResult.Rejection == OrderRejection.ActiveOrderExists
                        ? OrderBoardFeedback.ActiveOrderExists
                        : lastResult.Rejection == OrderRejection.InvalidTransition ||
                          lastResult.Rejection == OrderRejection.AlreadyCompleted
                            ? OrderBoardFeedback.Unavailable
                            : OrderBoardFeedback.Failure;
                RefreshView();
                InvokeIsolated(AcceptanceCompleted, lastResult);
                return lastResult;
            }
            finally
            {
                isSubmitting = false;
            }
        }

        public void RequestAcceptSelected() => AcceptSelected();

        public void RefreshView()
        {
            if (service == null || items == null || view == null)
                return;

            var serviceSnapshot = service.GetSnapshot();
            if (serviceSnapshot.Orders.Count == 0)
                selectedIndex = 0;
            else
                selectedIndex = Mathf.Clamp(selectedIndex, 0, serviceSnapshot.Orders.Count - 1);

            var models = new List<OrderBoardEntry>(serviceSnapshot.Orders.Count);
            foreach (var order in serviceSnapshot.Orders)
            {
                var medicine = items.GetRequired(order.MedicineItemId);
                models.Add(new OrderBoardEntry(
                    order.OrderId,
                    order.DisplayName,
                    order.MedicineItemId,
                    medicine.DisplayName,
                    order.Quantity,
                    order.Reward,
                    order.State,
                    serviceSnapshot.ActiveOrderId == order.OrderId));
            }

            snapshot = new OrderBoardSnapshot(models, selectedIndex, CanInteract, feedback, lastResult);
            view.Render(snapshot);
            InvokeIsolated(SnapshotChanged, snapshot);
        }

        public void SetApplicationPaused(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                ResetIntent();
            RefreshView();
        }

        public void SetApplicationFocused(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                ResetIntent();
            RefreshView();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshView();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetIntent();
            isSubmitting = false;
        }

        private void OnApplicationPause(bool paused) => SetApplicationPaused(paused);
        private void OnApplicationFocus(bool focused) => SetApplicationFocused(focused);

        private void HandleOrderChanged(OrderChanged _)
        {
            if (!isSubmitting)
            {
                feedback = OrderBoardFeedback.None;
                lastResult = null;
            }
            RefreshView();
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || changes == null)
                return;
            changes.Changed += HandleOrderChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (changes != null)
                changes.Changed -= HandleOrderChanged;
            subscribed = false;
        }

        private void ResetIntent()
        {
            lastIntentFrame = -1;
            lastIntentOrderId = default;
            lastIntentResult = null;
        }

        private void InvokeIsolated<T>(Action<T> callbacks, T value)
        {
            var handlers = callbacks?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<T> handler in handlers)
            {
                try { handler(value); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
