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
            float elapsedGrowthSeconds,
            float normalizedProgress,
            PlantGrowthModifier growthModifier,
            float? waterRequestThreshold,
            bool waterRequestOccurred,
            bool waterRequestCompleted)
        {
            SlotId = slotId;
            Version = version;
            State = state;
            PlantTypeId = plantTypeId;
            ElapsedGrowthSeconds = elapsedGrowthSeconds;
            NormalizedProgress = normalizedProgress;
            GrowthModifier = growthModifier;
            WaterRequestThreshold = waterRequestThreshold;
            WaterRequestOccurred = waterRequestOccurred;
            WaterRequestCompleted = waterRequestCompleted;
        }

        public PlantingSlotId SlotId { get; }
        public long Version { get; }
        public PlantingSlotState State { get; }
        public PlantTypeId? PlantTypeId { get; }
        public float ElapsedGrowthSeconds { get; }
        public float NormalizedProgress { get; }
        public PlantGrowthModifier GrowthModifier { get; }
        public float? WaterRequestThreshold { get; }
        public bool WaterRequestOccurred { get; }
        public bool WaterRequestCompleted { get; }
    }
}
