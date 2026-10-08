using System;

namespace VrGame.Domain.Plants
{
    public sealed class PlantingSlotChanged
    {
        internal PlantingSlotChanged(
            PlantingSlotMutationKind kind,
            PlantingSlotChangeContext context,
            PlantingSlotSnapshot before,
            PlantingSlotSnapshot after)
        {
            Kind = kind;
            Reason = context.Reason;
            CommandId = context.CommandId;
            CorrelationId = context.CorrelationId;
            Before = before;
            After = after;
        }

        public PlantingSlotMutationKind Kind { get; }
        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public PlantingSlotSnapshot Before { get; }
        public PlantingSlotSnapshot After { get; }
    }
}
