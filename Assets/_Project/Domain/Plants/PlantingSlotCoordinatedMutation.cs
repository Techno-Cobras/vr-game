using System;
using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public sealed partial class PlantingSlot
    {
        internal bool TryAcquireCoordinatedMutation()
        {
            if (isPublishingEvent)
                return false;
            isPublishingEvent = true;
            return true;
        }

        internal void ReleaseCoordinatedMutation() => isPublishingEvent = false;

        internal PlantingSlotRejection TryPrepareCoordinatedPlant(
            PlantTypeId plantType,
            PlantingSlotChangeContext context,
            out PreparedPlanting prepared)
        {
            prepared = default;
            if (!context.IsValid)
                return PlantingSlotRejection.InvalidContext;
            var payload = CommandPayload.ForPlant(plantType);
            var rejection = Validate(PlantingSlotMutationKind.Plant, payload);
            if (rejection != PlantingSlotRejection.None)
                return rejection;
            if (Version == long.MaxValue)
                return PlantingSlotRejection.ArithmeticOverflow;

            prepared = new PreparedPlanting(plantType, Version, context);
            return PlantingSlotRejection.None;
        }

        internal PlantingSlotChanged CommitPreparedPlant(PreparedPlanting prepared)
        {
            if (Version != prepared.ExpectedVersion || state != PlantingSlotState.Empty)
                throw new InvalidOperationException("Подготовленная посадка устарела.");

            var before = GetSnapshot();
            Apply(PlantingSlotMutationKind.Plant, CommandPayload.ForPlant(prepared.PlantTypeId));
            Version++;
            return new PlantingSlotChanged(
                PlantingSlotMutationKind.Plant,
                prepared.Context,
                before,
                GetSnapshot());
        }

        internal bool PublishPrepared(PlantingSlotChanged change)
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

        internal readonly struct PreparedPlanting
        {
            public PreparedPlanting(
                PlantTypeId plantTypeId,
                long expectedVersion,
                PlantingSlotChangeContext context)
            {
                PlantTypeId = plantTypeId;
                ExpectedVersion = expectedVersion;
                Context = context;
            }

            public PlantTypeId PlantTypeId { get; }
            public long ExpectedVersion { get; }
            public PlantingSlotChangeContext Context { get; }
        }
    }
}
