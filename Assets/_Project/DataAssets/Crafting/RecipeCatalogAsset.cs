using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Crafting;

namespace VrGame.DataAssets.Crafting
{
    [CreateAssetMenu(menuName = "VR Game/Crafting/Recipe Catalog", fileName = "RecipeCatalog")]
    public sealed class RecipeCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private RecipeDefinitionAsset[] definitions = Array.Empty<RecipeDefinitionAsset>();

        public RecipeCatalog Build(RuntimeItemCatalog itemCatalog)
        {
            if (itemCatalog == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingItemCatalog, "Runtime item catalog для рецептов отсутствует.");
            if (definitions == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingDefinition, $"У recipe catalog asset '{name}' отсутствует коллекция definitions.");

            var recipeDefinitions = new List<RecipeDefinition>(definitions.Length);
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new RecipeDataValidationException(RecipeDataValidationError.MissingDefinition, $"Recipe catalog asset '{name}' содержит пустую definition.");
                recipeDefinitions.Add(definition.ToDefinition());
            }
            return new RecipeCatalog(itemCatalog.Items, recipeDefinitions);
        }
    }
}
