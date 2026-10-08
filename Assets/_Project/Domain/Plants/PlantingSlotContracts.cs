namespace VrGame.Domain.Plants
{
    public enum PlantingSlotState
    {
        Empty = 0,
        Growing,
        NeedsWater,
        ReadyToHarvest
    }

    public enum PlantingSlotMutationKind
    {
        Plant = 1,
        AdvanceProgress,
        RequestWater,
        Water,
        ApplyFertilizer,
        Harvest
    }

    public enum PlantingSlotRejection
    {
        None = 0,
        InvalidContext,
        UnknownPlant,
        Occupied,
        NotGrowing,
        WaterNotNeeded,
        AlreadyNeedsWater,
        NotReady,
        InvalidProgress,
        ProgressRegressionOrNoProgress,
        StaleVersion,
        InvalidFertilizer,
        FertilizerAlreadyApplied,
        ArithmeticOverflow,
        ReentrantMutation,
        CommandConflict
    }
}
