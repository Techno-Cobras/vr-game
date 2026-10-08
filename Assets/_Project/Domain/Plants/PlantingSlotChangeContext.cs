using System;

namespace VrGame.Domain.Plants
{
    public readonly struct PlantingSlotChangeContext
    {
        public PlantingSlotChangeContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина изменения planting slot обязательна.", nameof(reason));
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
}
