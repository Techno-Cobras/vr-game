using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Crafting
{
    public sealed class CraftResult
    {
        private CraftResult(
            CraftRejection rejection,
            RecipeId recipeId,
            ItemId outputItemId,
            int outputQuantity,
            IEnumerable<CraftMissingIngredient> missingIngredients,
            CraftOutputFailure outputFailure,
            InventoryRejection inventoryRejection,
            InventoryChanged inventoryChange,
            CraftCompleted completed,
            bool inventoryEventPublished,
            bool craftEventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            RecipeId = recipeId;
            OutputItemId = outputItemId;
            OutputQuantity = outputQuantity;
            MissingIngredients = new ReadOnlyCollection<CraftMissingIngredient>(
                (missingIngredients ?? Array.Empty<CraftMissingIngredient>()).ToList());
            OutputFailure = outputFailure;
            InventoryRejection = inventoryRejection;
            InventoryChange = inventoryChange;
            Completed = completed;
            InventoryEventPublished = inventoryEventPublished;
            CraftEventPublished = craftEventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == CraftRejection.None;
        public CraftRejection Rejection { get; }
        public RecipeId RecipeId { get; }
        public ItemId OutputItemId { get; }
        public int OutputQuantity { get; }
        public IReadOnlyList<CraftMissingIngredient> MissingIngredients { get; }
        public CraftOutputFailure OutputFailure { get; }
        public InventoryRejection InventoryRejection { get; }
        public InventoryChanged InventoryChange { get; }
        public CraftCompleted Completed { get; }
        public bool InventoryEventPublished { get; }
        public bool CraftEventPublished { get; }
        public bool IsReplay { get; }

        internal static CraftResult Success(
            RecipeDefinition recipe,
            InventoryChanged inventoryChange,
            CraftCompleted completed,
            bool inventoryEventPublished,
            bool craftEventPublished) =>
            new CraftResult(
                CraftRejection.None,
                recipe.Id,
                recipe.OutputItemId,
                recipe.OutputQuantity,
                null,
                CraftOutputFailure.None,
                InventoryRejection.None,
                inventoryChange,
                completed,
                inventoryEventPublished,
                craftEventPublished,
                false);

        internal static CraftResult Reject(
            CraftRejection rejection,
            RecipeId recipeId = default,
            ItemId outputItemId = default,
            int outputQuantity = 0,
            IEnumerable<CraftMissingIngredient> missingIngredients = null,
            CraftOutputFailure outputFailure = CraftOutputFailure.None,
            InventoryRejection inventoryRejection = InventoryRejection.None) =>
            new CraftResult(
                rejection,
                recipeId,
                outputItemId,
                outputQuantity,
                missingIngredients,
                outputFailure,
                inventoryRejection,
                null,
                null,
                false,
                false,
                false);

        internal CraftResult AsReplay() =>
            new CraftResult(
                Rejection,
                RecipeId,
                OutputItemId,
                OutputQuantity,
                MissingIngredients,
                OutputFailure,
                InventoryRejection,
                InventoryChange,
                Completed,
                InventoryEventPublished,
                CraftEventPublished,
                true);
    }
}
