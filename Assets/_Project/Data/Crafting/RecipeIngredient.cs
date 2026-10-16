using VrGame.Data.Items;

namespace VrGame.Data.Crafting
{
    public sealed class RecipeIngredient
    {
        public RecipeIngredient(ItemId itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId.Value))
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidIngredientItem, "У ингредиента отсутствует item ID.");
            if (quantity <= 0)
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidIngredientQuantity, "Количество ингредиента должно быть положительным.");
            ItemId = itemId;
            Quantity = quantity;
        }

        public ItemId ItemId { get; }
        public int Quantity { get; }
    }
}
