using System;

namespace VrGame.Domain.Inventory
{
    public readonly struct InventoryChangeContext
    {
        public InventoryChangeContext(string reason, Guid correlationId)
            : this(reason, Guid.Empty, correlationId)
        {
        }

        public InventoryChangeContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина изменения инвентаря обязательна.", nameof(reason));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));

            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }

        public Guid CommandId { get; }

        public Guid CorrelationId { get; }

        internal bool IsValid => !string.IsNullOrWhiteSpace(Reason) && CorrelationId != Guid.Empty;
    }
}
