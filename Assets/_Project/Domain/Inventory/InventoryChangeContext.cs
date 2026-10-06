using System;

namespace VrGame.Domain.Inventory
{
    public readonly struct InventoryChangeContext
    {
        public InventoryChangeContext(string reason, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина изменения инвентаря обязательна.", nameof(reason));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));

            Reason = reason;
            CorrelationId = correlationId;
        }

        public string Reason { get; }

        public Guid CorrelationId { get; }

        internal bool IsValid => !string.IsNullOrWhiteSpace(Reason) && CorrelationId != Guid.Empty;
    }
}
