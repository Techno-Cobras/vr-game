using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Crafting;

namespace VrGame.DataAssets.Crafting
{
    [CreateAssetMenu(menuName = "VR Game/Crafting/Recipe Definition", fileName = "RecipeDefinition")]
    public sealed class RecipeDefinitionAsset : ScriptableObject
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private RecipeIngredientAsset[] ingredients = Array.Empty<RecipeIngredientAsset>();

        [SerializeField]
        private ItemDefinitionAsset outputItem;

        [SerializeField, Min(1)]
        private int outputQuantity = 1;

        public RecipeDefinition ToDefinition()
        {
            if (outputItem == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidOutputItem, $"У recipe asset '{name}' отсутствует output item asset.");
            if (ingredients == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingIngredients, $"У recipe asset '{name}' отсутствует коллекция ингредиентов.");

            var ingredientDefinitions = new List<RecipeIngredient>(ingredients.Length);
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || ingredient.Item == null)
                    throw new RecipeDataValidationException(RecipeDataValidationError.MissingIngredient, $"У recipe asset '{name}' отсутствует ingredient item asset.");
                ingredientDefinitions.Add(new RecipeIngredient(ingredient.Item.GetId(), ingredient.Quantity));
            }

            return new RecipeDefinition(new RecipeId(id), displayName, ingredientDefinitions, outputItem.GetId(), outputQuantity);
        }
    }
}
