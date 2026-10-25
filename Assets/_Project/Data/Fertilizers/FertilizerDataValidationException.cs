using System;

namespace VrGame.Data.Fertilizers
{
    public enum FertilizerDataValidationError
    {
        MissingDefinition,
        DuplicateGrade,
        DuplicateItem,
        InvalidGrade,
        InvalidMultiplier,
        InvalidItemCategory
    }

    public sealed class FertilizerDataValidationException : ArgumentException
    {
        public FertilizerDataValidationException(FertilizerDataValidationError error, string message)
            : base(message) => Error = error;

        public FertilizerDataValidationError Error { get; }
    }
}
