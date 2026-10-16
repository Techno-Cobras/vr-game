using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Crafting
{
    public sealed class RecipeDefinition
    {
        public RecipeDefinition(RecipeId id, string displayName, IEnumerable<RecipeIngredient> ingredients,
            ItemId outputItemId, int outputQuantity)
        {
            if (string.IsNullOrEmpty(id.Value))
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidRecipeId, "У рецепта отсутствует стабильный ID.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingDisplayName, $"У рецепта '{id}' отсутствует display name.");
            if (string.IsNullOrEmpty(outputItemId.Value))
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidOutputItem, $"У рецепта '{id}' отсутствует output item.");
            if (outputQuantity <= 0)
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidOutputQuantity, $"Количество результата рецепта '{id}' должно быть положительным.");
            if (ingredients == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingIngredients, $"У рецепта '{id}' отсутствуют ингредиенты.");

            var ingredientList = ingredients.ToList();
            if (ingredientList.Count == 0)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingIngredients, $"У рецепта '{id}' отсутствуют ингредиенты.");
            var itemIds = new HashSet<ItemId>();
            foreach (var ingredient in ingredientList)
            {
                if (ingredient == null)
                    throw new RecipeDataValidationException(RecipeDataValidationError.MissingIngredient, $"Рецепт '{id}' содержит пустой ингредиент.");
                if (!itemIds.Add(ingredient.ItemId))
                    throw new RecipeDataValidationException(RecipeDataValidationError.DuplicateIngredient, $"Ингредиент '{ingredient.ItemId}' повторяется в рецепте '{id}'.");
            }

            Id = id;
            DisplayName = displayName;
            Ingredients = new ReadOnlyCollection<RecipeIngredient>(ingredientList);
            OutputItemId = outputItemId;
            OutputQuantity = outputQuantity;
        }

        public RecipeId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<RecipeIngredient> Ingredients { get; }
        public ItemId OutputItemId { get; }
        public int OutputQuantity { get; }
    }
}
