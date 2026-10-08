using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public sealed class PlantingSlotSnapshot
    {
        internal PlantingSlotSnapshot(
            PlantingSlotId slotId,
            long version,
            PlantingSlotState state,
            PlantTypeId? plantTypeId,
            float normalizedProgress,
            PlantGrowthModifier growthModifier)
        {
            SlotId = slotId;
            Version = version;
            State = state;
            PlantTypeId = plantTypeId;
            NormalizedProgress = normalizedProgress;
            GrowthModifier = growthModifier;
        }

        public PlantingSlotId SlotId { get; }
        public long Version { get; }
        public PlantingSlotState State { get; }
        public PlantTypeId? PlantTypeId { get; }
        public float NormalizedProgress { get; }
        public PlantGrowthModifier GrowthModifier { get; }
    }
}
