namespace VrGame.Domain.Orders
{
    public sealed class OrderMutationResult
    {
        private OrderMutationResult(
            OrderRejection rejection,
            OrderId? targetOrderId,
            OrderChanged change,
            bool eventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            TargetOrderId = targetOrderId;
            Change = change;
            EventPublished = eventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == OrderRejection.None;
        public OrderRejection Rejection { get; }
        public OrderId? TargetOrderId { get; }
        public OrderChanged Change { get; }
        public bool EventPublished { get; }
        public bool IsReplay { get; }

        internal static OrderMutationResult Success(OrderChanged change, bool eventPublished) =>
            new OrderMutationResult(
                OrderRejection.None,
                change.After.OrderId,
                change,
                eventPublished,
                false);

        internal static OrderMutationResult Reject(OrderRejection rejection, OrderId? targetOrderId = null) =>
            new OrderMutationResult(rejection, targetOrderId, null, false, false);

        internal OrderMutationResult AsReplay() =>
            new OrderMutationResult(Rejection, TargetOrderId, Change, EventPublished, true);
    }
}
