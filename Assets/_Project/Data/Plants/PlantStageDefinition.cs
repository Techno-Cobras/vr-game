namespace VrGame.Data.Plants
{
    public sealed class PlantStageDefinition
    {
        public PlantStageDefinition(float startsAtNormalized, PlantRepresentationKey representation)
        {
            if (float.IsNaN(startsAtNormalized) || float.IsInfinity(startsAtNormalized) ||
                startsAtNormalized < 0f || startsAtNormalized > 1f)
                throw new PlantDataValidationException(
                    PlantDataValidationError.InvalidStageThreshold,
                    "Порог стадии должен быть конечным числом от 0 до 1.");
            if (string.IsNullOrEmpty(representation.Value))
                throw new PlantDataValidationException(
                    PlantDataValidationError.MissingRepresentation,
                    "У стадии отсутствует representation.");

            StartsAtNormalized = startsAtNormalized;
            Representation = representation;
        }

        public float StartsAtNormalized { get; }
        public PlantRepresentationKey Representation { get; }
    }
}
