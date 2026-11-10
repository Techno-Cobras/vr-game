using System;

namespace VrGame.Domain.Orders
{
    public sealed partial class OrderService
    {
        internal bool TryAcquireCoordinatedMutation()
        {
            if (isExecuting)
                return false;
            isExecuting = true;
            return true;
        }

        internal void ReleaseCoordinatedMutation() => isExecuting = false;

        internal OrderRejection TryPrepareCoordinatedCompletion(
            OrderId orderId,
            OrderContext context,
            out PreparedOrderCompletion prepared)
        {
            prepared = default;
            if (!context.IsValid)
                return OrderRejection.InvalidContext;
            if (!ordersById.TryGetValue(orderId, out var target))
                return OrderRejection.UnknownOrder;

            var rejection = ValidateTransition(OrderMutationKind.Complete, target);
            if (rejection != OrderRejection.None)
                return rejection;
            if (Version == long.MaxValue || target.Version == long.MaxValue)
                return OrderRejection.ArithmeticOverflow;

            var key = new CommandKey(OrderMutationKind.Complete, context.CommandId);
            if (completedCommands.ContainsKey(key))
                return OrderRejection.CommandConflict;

            prepared = new PreparedOrderCompletion(
                orderId,
                Version,
                target.Version,
                target.GetSnapshot(),
                activeOrderId,
                context);
            return OrderRejection.None;
        }

        internal OrderChanged CommitPreparedCompletion(PreparedOrderCompletion prepared)
        {
            if (!ordersById.TryGetValue(prepared.OrderId, out var target) ||
                Version != prepared.ExpectedServiceVersion ||
                target.Version != prepared.ExpectedOrderVersion ||
                target.State != OrderState.InProgress ||
                activeOrderId != prepared.OrderId)
                throw new InvalidOperationException("Подготовленное завершение заказа устарело.");

            target.State = OrderState.Completed;
            activeOrderId = null;
            target.Version++;
            Version++;
            return new OrderChanged(
                Version,
                OrderMutationKind.Complete,
                prepared.Context,
                prepared.Before,
                target.GetSnapshot(),
                prepared.ActiveOrderIdBefore,
                activeOrderId);
        }

        internal bool PublishPrepared(OrderChanged change)
        {
            try
            {
                return eventSink.TryPublish(change);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal OrderMutationResult CompletePreparedCommand(
            PreparedOrderCompletion prepared,
            OrderChanged change,
            bool eventPublished)
        {
            var result = OrderMutationResult.Success(change, eventPublished);
            var key = new CommandKey(OrderMutationKind.Complete, prepared.Context.CommandId);
            var payload = new CommandPayload(
                prepared.OrderId,
                prepared.Context.Reason,
                prepared.Context.CorrelationId);
            completedCommands.Add(key, new CompletedCommand(payload, result));
            return result;
        }

        internal readonly struct PreparedOrderCompletion
        {
            public PreparedOrderCompletion(
                OrderId orderId,
                long expectedServiceVersion,
                long expectedOrderVersion,
                OrderSnapshot before,
                OrderId? activeOrderIdBefore,
                OrderContext context)
            {
                OrderId = orderId;
                ExpectedServiceVersion = expectedServiceVersion;
                ExpectedOrderVersion = expectedOrderVersion;
                Before = before;
                ActiveOrderIdBefore = activeOrderIdBefore;
                Context = context;
            }

            public OrderId OrderId { get; }
            public long ExpectedServiceVersion { get; }
            public long ExpectedOrderVersion { get; }
            public OrderSnapshot Before { get; }
            public OrderId? ActiveOrderIdBefore { get; }
            public OrderContext Context { get; }
        }
    }
}
