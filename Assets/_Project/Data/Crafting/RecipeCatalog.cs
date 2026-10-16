using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Crafting
{
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<RecipeId, RecipeDefinition> definitionsById;

        public RecipeCatalog(ItemCatalog itemCatalog, IEnumerable<RecipeDefinition> definitions)
        {
            if (itemCatalog == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingItemCatalog, "Item catalog для рецептов отсутствует.");
            if (definitions == null)
                throw new RecipeDataValidationException(RecipeDataValidationError.MissingDefinition, "Коллекция recipe definitions отсутствует.");

            definitionsById = new Dictionary<RecipeId, RecipeDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new RecipeDataValidationException(RecipeDataValidationError.MissingDefinition, "Каталог содержит пустую recipe definition.");
                if (!definitionsById.TryAdd(definition.Id, definition))
                    throw new RecipeDataValidationException(RecipeDataValidationError.DuplicateRecipeId, $"Recipe ID '{definition.Id}' встречается больше одного раза.");
                foreach (var ingredient in definition.Ingredients)
                    if (!itemCatalog.TryGet(ingredient.ItemId, out _))
                        throw new RecipeDataValidationException(RecipeDataValidationError.UnknownIngredientItem, $"Рецепт '{definition.Id}' содержит неизвестный ингредиент '{ingredient.ItemId}'.");
                if (!itemCatalog.TryGet(definition.OutputItemId, out var output))
                    throw new RecipeDataValidationException(RecipeDataValidationError.UnknownOutputItem, $"У рецепта '{definition.Id}' указан неизвестный output item '{definition.OutputItemId}'.");
                if (output.Category != ItemCategory.Medicine)
                    throw new RecipeDataValidationException(RecipeDataValidationError.InvalidOutputItemCategory, $"Output item '{output.Id}' должен иметь категорию Medicine.");
            }

            Definitions = new ReadOnlyCollection<RecipeDefinition>(definitionsById.Values.OrderBy(value => value.Id).ToList());
        }

        public IReadOnlyList<RecipeDefinition> Definitions { get; }
        public bool TryGet(RecipeId id, out RecipeDefinition definition) => definitionsById.TryGetValue(id, out definition);

        public RecipeDefinition GetRequired(RecipeId id)
        {
            if (TryGet(id, out var definition))
                return definition;
            throw new KeyNotFoundException($"Recipe ID '{id}' отсутствует в каталоге.");
        }
    }
}
