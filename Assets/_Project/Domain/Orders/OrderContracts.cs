using System;

namespace VrGame.Domain.Orders
{
    public enum OrderState
    {
        Available = 0,
        InProgress = 1,
        Completed = 2
    }

    public enum OrderMutationKind
    {
        Generate = 1,
        Accept = 2,
        Complete = 3
    }

    public enum OrderRejection
    {
        None = 0,
        InvalidContext = 1,
        UnknownOrder = 2,
        ActiveOrderExists = 3,
        InvalidTransition = 4,
        AlreadyCompleted = 5,
        InvalidTemplateSelection = 6,
        InvalidGeneratedOrderId = 7,
        ArithmeticOverflow = 8,
        ReentrantMutation = 9,
        CommandConflict = 10
    }

    public readonly struct OrderContext
    {
        public OrderContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина изменения заказа обязательна.", nameof(reason));
            if (commandId == Guid.Empty)
                throw new ArgumentException("Command ID не может быть пустым.", nameof(commandId));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));
            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(Reason) &&
            CommandId != Guid.Empty &&
            CorrelationId != Guid.Empty;
    }

    public interface IOrderTemplateSelector
    {
        int SelectIndex(int exclusiveMax);
    }

    public interface IOrderIdSource
    {
        OrderId NextId();
    }

    public interface IOrderEventSink
    {
        bool TryPublish(OrderChanged orderChanged);
    }
}
