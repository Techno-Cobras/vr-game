using System;
using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public static class PlantGrowthStageResolver
    {
        public static PlantStageDefinition Resolve(PlantDefinition definition, float normalizedProgress)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (float.IsNaN(normalizedProgress) || float.IsInfinity(normalizedProgress) ||
                normalizedProgress < 0f || normalizedProgress > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));

            var stages = definition.Stages;
            var selected = stages[0];
            for (var index = 1; index < stages.Count; index++)
            {
                if (normalizedProgress < stages[index].StartsAtNormalized)
                    break;
                selected = stages[index];
            }

            return selected;
        }
    }
}
