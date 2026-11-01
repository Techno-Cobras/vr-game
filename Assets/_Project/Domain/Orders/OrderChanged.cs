using System;

namespace VrGame.Domain.Orders
{
    public sealed class OrderChanged
    {
        internal OrderChanged(
            long serviceVersion,
            OrderMutationKind kind,
            OrderContext context,
            OrderSnapshot before,
            OrderSnapshot after,
            OrderId? activeOrderIdBefore,
            OrderId? activeOrderIdAfter)
        {
            ServiceVersion = serviceVersion;
            Kind = kind;
            Reason = context.Reason;
            CommandId = context.CommandId;
            CorrelationId = context.CorrelationId;
            Before = before;
            After = after;
            ActiveOrderIdBefore = activeOrderIdBefore;
            ActiveOrderIdAfter = activeOrderIdAfter;
        }

        public long ServiceVersion { get; }
        public OrderMutationKind Kind { get; }
        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public OrderSnapshot Before { get; }
        public OrderSnapshot After { get; }
        public OrderId? ActiveOrderIdBefore { get; }
        public OrderId? ActiveOrderIdAfter { get; }
    }
}
