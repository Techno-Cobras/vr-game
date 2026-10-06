using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Items;

namespace VrGame.DataAssets
{
    [CreateAssetMenu(menuName = "VR Game/Items/Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private ItemDefinitionAsset[] definitions = System.Array.Empty<ItemDefinitionAsset>();

        public RuntimeItemCatalog Build()
        {
            if (definitions == null)
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingDefinition,
                    $"У catalog asset '{name}' отсутствует коллекция item definitions.");

            var itemDefinitions = new List<ItemDefinition>(definitions.Length);
            var representations = new Dictionary<ItemRepresentationKey, GameObject>();

            foreach (var definitionAsset in definitions)
            {
                if (definitionAsset == null)
                    throw new ItemDataValidationException(
                        ItemDataValidationError.MissingDefinition,
                        $"Catalog asset '{name}' содержит пустую ссылку на item definition.");

                var definition = definitionAsset.ToDefinition();
                itemDefinitions.Add(definition);

                var representationAsset = definitionAsset.Representation;
                var key = definition.Representation;
                var prefab = representationAsset.Prefab;
                if (representations.TryGetValue(key, out var existingPrefab) && existingPrefab != prefab)
                    throw new ItemDataValidationException(
                        ItemDataValidationError.ConflictingRepresentation,
                        $"Representation key '{key}' ссылается на разные prefabs.");

                representations[key] = prefab;
            }

            return new RuntimeItemCatalog(
                new ItemCatalog(itemDefinitions),
                new System.Collections.ObjectModel.ReadOnlyDictionary<ItemRepresentationKey, GameObject>(representations));
        }
    }
}
