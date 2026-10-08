using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Plants;

namespace VrGame.DataAssets.Plants
{
    [CreateAssetMenu(menuName = "VR Game/Plants/Definition", fileName = "PlantDefinition")]
    public sealed class PlantDefinitionAsset : ScriptableObject
    {
        [SerializeField]
        private string id;

        [SerializeField, Min(0.01f)]
        private float growthDurationSeconds = 1f;

        [SerializeField]
        private ItemDefinitionAsset seedItem;

        [SerializeField]
        private ItemDefinitionAsset harvestItem;

        [SerializeField]
        private PlantStageAsset[] stages = Array.Empty<PlantStageAsset>();

        public IReadOnlyList<PlantStageAsset> Stages => stages;

        public PlantDefinition ToDefinition()
        {
            if (seedItem == null || harvestItem == null)
                throw new PlantDataValidationException(
                    PlantDataValidationError.MissingDefinition,
                    $"У plant asset '{name}' отсутствует ссылка на seed или harvest item asset.");
            if (stages == null)
                throw new PlantDataValidationException(
                    PlantDataValidationError.MissingStages,
                    $"У plant asset '{name}' отсутствует коллекция стадий.");

            var stageDefinitions = new List<PlantStageDefinition>(stages.Length);
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                if (stage == null || stage.Representation == null)
                    throw new PlantDataValidationException(
                        PlantDataValidationError.MissingRepresentation,
                        $"У стадии {index} plant asset '{name}' отсутствует representation asset.");
                stageDefinitions.Add(new PlantStageDefinition(stage.StartsAtNormalized, stage.Representation.GetKey()));
            }

            return new PlantDefinition(
                new PlantTypeId(id),
                new GrowthDuration(growthDurationSeconds),
                seedItem.GetId(),
                harvestItem.GetId(),
                stageDefinitions);
        }
    }
}
