using System;

namespace VrGame.Data.Crafting
{
    public enum RecipeDataValidationError
    {
        InvalidRecipeId,
        MissingDisplayName,
        MissingIngredients,
        MissingIngredient,
        InvalidIngredientItem,
        InvalidIngredientQuantity,
        DuplicateIngredient,
        InvalidOutputItem,
        InvalidOutputQuantity,
        MissingDefinition,
        DuplicateRecipeId,
        MissingItemCatalog,
        UnknownIngredientItem,
        UnknownOutputItem,
        InvalidOutputItemCategory
    }

    public sealed class RecipeDataValidationException : ArgumentException
    {
        public RecipeDataValidationException(RecipeDataValidationError error, string message) : base(message)
        {
            Error = error;
        }

        public RecipeDataValidationError Error { get; }
    }
}
