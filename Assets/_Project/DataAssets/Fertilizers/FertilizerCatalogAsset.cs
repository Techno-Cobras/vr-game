using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Fertilizers;

namespace VrGame.DataAssets.Fertilizers
{
    [CreateAssetMenu(menuName = "VR Game/Fertilizers/Catalog", fileName = "FertilizerCatalog")]
    public sealed class FertilizerCatalogAsset : ScriptableObject
    {
        [SerializeField] private FertilizerDefinitionAsset[] definitions = System.Array.Empty<FertilizerDefinitionAsset>();

        public FertilizerCatalog Build(RuntimeItemCatalog items)
        {
            if (definitions == null)
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.MissingDefinition,
                    $"У fertilizer catalog asset '{name}' отсутствует коллекция definitions.");
            var runtime = new List<FertilizerDefinition>(definitions.Length);
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new FertilizerDataValidationException(
                        FertilizerDataValidationError.MissingDefinition,
                        $"Fertilizer catalog asset '{name}' содержит пустую ссылку.");
                runtime.Add(definition.ToDefinition(items));
            }
            return new FertilizerCatalog(runtime);
        }
    }
}
