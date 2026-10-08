using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Plants
{
    public sealed class PlantDefinition
    {
        public PlantDefinition(
            PlantTypeId id,
            GrowthDuration growthDuration,
            ItemId seedItemId,
            ItemId harvestItemId,
            IEnumerable<PlantStageDefinition> stages)
        {
            if (string.IsNullOrEmpty(id.Value))
                throw new PlantDataValidationException(PlantDataValidationError.InvalidPlantTypeId, "У растения отсутствует стабильный ID.");
            if (growthDuration.Seconds <= 0f)
                throw new PlantDataValidationException(PlantDataValidationError.InvalidGrowthDuration, $"У растения '{id}' отсутствует длительность роста.");
            if (string.IsNullOrEmpty(seedItemId.Value))
                throw new PlantDataValidationException(PlantDataValidationError.MissingSeedItem, $"У растения '{id}' отсутствует seed item.");
            if (string.IsNullOrEmpty(harvestItemId.Value))
                throw new PlantDataValidationException(PlantDataValidationError.MissingHarvestItem, $"У растения '{id}' отсутствует harvest item.");
            if (stages == null)
                throw new PlantDataValidationException(PlantDataValidationError.MissingStages, $"У растения '{id}' отсутствуют стадии роста.");

            var stageList = stages.ToList();
            if (stageList.Count == 0)
                throw new PlantDataValidationException(PlantDataValidationError.MissingStages, $"У растения '{id}' отсутствуют стадии роста.");

            var previousThreshold = -1f;
            for (var index = 0; index < stageList.Count; index++)
            {
                var stage = stageList[index];
                if (stage == null)
                    throw new PlantDataValidationException(PlantDataValidationError.MissingRepresentation, $"У стадии {index} растения '{id}' отсутствует definition.");
                if (index == 0 && stage.StartsAtNormalized != 0f)
                    throw new PlantDataValidationException(PlantDataValidationError.InvalidStageThreshold, $"Первая стадия растения '{id}' должна начинаться с 0.");
                if (index > 0 && stage.StartsAtNormalized <= previousThreshold)
                    throw new PlantDataValidationException(PlantDataValidationError.UnorderedStages, $"Пороги стадий растения '{id}' должны строго возрастать.");
                previousThreshold = stage.StartsAtNormalized;
            }

            Id = id;
            GrowthDuration = growthDuration;
            SeedItemId = seedItemId;
            HarvestItemId = harvestItemId;
            Stages = new ReadOnlyCollection<PlantStageDefinition>(stageList);
        }

        public PlantTypeId Id { get; }
        public GrowthDuration GrowthDuration { get; }
        public ItemId SeedItemId { get; }
        public ItemId HarvestItemId { get; }
        public IReadOnlyList<PlantStageDefinition> Stages { get; }
    }
}
