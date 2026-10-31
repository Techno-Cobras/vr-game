using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Crafting;
using VrGame.Domain.Inventory;

namespace VrGame.Presentation.Crafting
{
    public enum CraftingTableFeedback
    {
        None = 0,
        MissingIngredients,
        Success,
        Failure,
        Replay
    }

    public interface IInventoryChangeSource
    {
        event Action<InventoryChanged> Changed;
    }

    public interface ICraftingTableView
    {
        void Render(CraftingTableSnapshot snapshot);
    }

    public sealed class CraftingIngredientAvailability
    {
        public CraftingIngredientAvailability(
            ItemId itemId,
            string displayName,
            int requiredQuantity,
            int availableQuantity)
        {
            ItemId = itemId;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            RequiredQuantity = requiredQuantity;
            AvailableQuantity = availableQuantity;
        }

        public ItemId ItemId { get; }
        public string DisplayName { get; }
        public int RequiredQuantity { get; }
        public int AvailableQuantity { get; }
        public int MissingQuantity => Math.Max(0, RequiredQuantity - AvailableQuantity);
        public bool IsAvailable => MissingQuantity == 0;
    }

    public sealed class CraftingRecipeAvailability
    {
        public CraftingRecipeAvailability(
            RecipeId recipeId,
            string displayName,
            ItemId outputItemId,
            string outputDisplayName,
            int outputQuantity,
            int ownedOutputQuantity,
            IEnumerable<CraftingIngredientAvailability> ingredients)
        {
            RecipeId = recipeId;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            OutputItemId = outputItemId;
            OutputDisplayName = outputDisplayName ?? throw new ArgumentNullException(nameof(outputDisplayName));
            OutputQuantity = outputQuantity;
            OwnedOutputQuantity = ownedOutputQuantity;
            Ingredients = new ReadOnlyCollection<CraftingIngredientAvailability>(
                (ingredients ?? throw new ArgumentNullException(nameof(ingredients))).ToList());
        }

        public RecipeId RecipeId { get; }
        public string DisplayName { get; }
        public ItemId OutputItemId { get; }
        public string OutputDisplayName { get; }
        public int OutputQuantity { get; }
        public int OwnedOutputQuantity { get; }
        public IReadOnlyList<CraftingIngredientAvailability> Ingredients { get; }
        public bool CanCraft => Ingredients.All(value => value.IsAvailable);
    }

    public sealed class CraftingTableSnapshot
    {
        public CraftingTableSnapshot(
            IEnumerable<CraftingRecipeAvailability> recipes,
            int selectedIndex,
            CraftingTableFeedback feedback,
            CraftResult lastResult)
        {
            Recipes = new ReadOnlyCollection<CraftingRecipeAvailability>(
                (recipes ?? throw new ArgumentNullException(nameof(recipes))).ToList());
            SelectedIndex = Recipes.Count == 0 ? 0 : Math.Max(0, Math.Min(selectedIndex, Recipes.Count - 1));
            Feedback = feedback;
            LastResult = lastResult;
        }

        public IReadOnlyList<CraftingRecipeAvailability> Recipes { get; }
        public int SelectedIndex { get; }
        public CraftingTableFeedback Feedback { get; }
        public CraftResult LastResult { get; }
        public CraftingRecipeAvailability SelectedRecipe =>
            Recipes.Count == 0 ? null : Recipes[SelectedIndex];
    }
}
