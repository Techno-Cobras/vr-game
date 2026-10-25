using System;
using VrGame.Data.Items;

namespace VrGame.Data.Fertilizers
{
    public sealed class FertilizerDefinition
    {
        public FertilizerDefinition(FertilizerGrade grade, ItemDefinition item, float growthMultiplier)
        {
            if (!Enum.IsDefined(typeof(FertilizerGrade), grade))
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.InvalidGrade,
                    "Указан неизвестный grade удобрения.");
            if (item == null)
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.MissingDefinition,
                    $"У fertilizer grade '{grade}' отсутствует item definition.");
            if (item.Category != ItemCategory.Fertilizer)
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.InvalidItemCategory,
                    $"Item '{item.Id}' должен иметь категорию Fertilizer.");
            if (float.IsNaN(growthMultiplier) || float.IsInfinity(growthMultiplier) || growthMultiplier <= 1f)
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.InvalidMultiplier,
                    $"Multiplier fertilizer grade '{grade}' должен быть конечным числом больше единицы.");

            Grade = grade;
            Item = item;
            GrowthMultiplier = growthMultiplier;
        }

        public FertilizerGrade Grade { get; }
        public ItemDefinition Item { get; }
        public ItemId ItemId => Item.Id;
        public float GrowthMultiplier { get; }
    }
}
