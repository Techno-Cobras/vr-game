using System;

namespace VrGame.Data.Plants
{
    public enum PlantDataValidationError
    {
        InvalidPlantTypeId,
        InvalidRepresentationKey,
        InvalidGrowthDuration,
        MissingDefinition,
        DuplicatePlantTypeId,
        MissingItemCatalog,
        MissingSeedItem,
        MissingHarvestItem,
        UnknownSeedItem,
        InvalidSeedItemCategory,
        UnknownHarvestItem,
        InvalidHarvestItemCategory,
        MissingStages,
        InvalidStageThreshold,
        UnorderedStages,
        MissingRepresentation,
        ConflictingRepresentation
    }

    public sealed class PlantDataValidationException : ArgumentException
    {
        public PlantDataValidationException(PlantDataValidationError error, string message)
            : base(message)
        {
            Error = error;
        }

        public PlantDataValidationError Error { get; }
    }
}
