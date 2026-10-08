using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using VrGame.Data.Plants;

namespace VrGame.DataAssets.Plants
{
    [CreateAssetMenu(menuName = "VR Game/Plants/Catalog", fileName = "PlantCatalog")]
    public sealed class PlantCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private PlantDefinitionAsset[] definitions = Array.Empty<PlantDefinitionAsset>();

        public RuntimePlantCatalog Build(RuntimeItemCatalog itemCatalog)
        {
            if (itemCatalog == null)
                throw new PlantDataValidationException(PlantDataValidationError.MissingItemCatalog, "Runtime item catalog для растений отсутствует.");
            if (definitions == null)
                throw new PlantDataValidationException(PlantDataValidationError.MissingDefinition, $"У plant catalog asset '{name}' отсутствует коллекция definitions.");

            var plantDefinitions = new List<PlantDefinition>(definitions.Length);
            var representations = new Dictionary<PlantRepresentationKey, GameObject>();
            foreach (var definitionAsset in definitions)
            {
                if (definitionAsset == null)
                    throw new PlantDataValidationException(PlantDataValidationError.MissingDefinition, $"Plant catalog asset '{name}' содержит пустую definition.");

                var definition = definitionAsset.ToDefinition();
                plantDefinitions.Add(definition);
                foreach (var stage in definitionAsset.Stages)
                {
                    var representationAsset = stage.Representation;
                    var key = representationAsset.GetKey();
                    var prefab = representationAsset.Prefab;
                    if (representations.TryGetValue(key, out var existing) && existing != prefab)
                        throw new PlantDataValidationException(
                            PlantDataValidationError.ConflictingRepresentation,
                            $"Plant representation key '{key}' ссылается на разные prefabs.");
                    representations[key] = prefab;
                }
            }

            return new RuntimePlantCatalog(
                new PlantCatalog(itemCatalog.Items, plantDefinitions),
                new ReadOnlyDictionary<PlantRepresentationKey, GameObject>(representations));
        }
    }
}
