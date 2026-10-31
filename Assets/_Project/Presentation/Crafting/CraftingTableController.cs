using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Crafting;
using VrGame.Domain.Inventory;

namespace VrGame.Presentation.Crafting
{
    public sealed class CraftingTableController : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour viewBehaviour;

        private CraftingService service;
        private RecipeCatalog recipes;
        private ItemCatalog items;
        private Inventory inventory;
        private IInventoryChangeSource inventoryChanges;
        private ICraftingTableView view;
        private CraftingTableSnapshot snapshot;
        private CraftResult lastResult;
        private CraftingTableFeedback feedback;
        private int selectedIndex;
        private bool subscribed;
        private bool isSubmitting;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private int lastIntentFrame = -1;
        private RecipeId lastIntentRecipe;
        private CraftResult lastIntentResult;

        public event Action<CraftingTableSnapshot> SnapshotChanged;
        public event Action<CraftResult> CraftCompleted;

        public bool CanInteract =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            service != null && recipes != null && items != null && inventory != null && view != null;
        public int SelectedIndex => selectedIndex;
        public CraftingTableSnapshot Snapshot => snapshot;

        public void Configure(
            CraftingService craftingService,
            RecipeCatalog recipeCatalog,
            ItemCatalog itemCatalog,
            Inventory playerInventory,
            IInventoryChangeSource changeSource,
            ICraftingTableView tableView = null)
        {
            Unsubscribe();
            service = craftingService ?? throw new ArgumentNullException(nameof(craftingService));
            recipes = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
            items = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            inventoryChanges = changeSource ?? throw new ArgumentNullException(nameof(changeSource));
            view = tableView ?? viewBehaviour as ICraftingTableView;
            if (view == null)
                throw new InvalidOperationException("Представление Crafting Table не настроено.");
            Subscribe();
            RefreshView();
        }

        public void SelectPreviousRecipe()
        {
            if (!CanInteract || recipes.Definitions.Count == 0)
                return;
            selectedIndex = (selectedIndex - 1 + recipes.Definitions.Count) % recipes.Definitions.Count;
            ResetIntent();
            feedback = CraftingTableFeedback.None;
            lastResult = null;
            RefreshView();
        }

        public void SelectNextRecipe()
        {
            if (!CanInteract || recipes.Definitions.Count == 0)
                return;
            selectedIndex = (selectedIndex + 1) % recipes.Definitions.Count;
            ResetIntent();
            feedback = CraftingTableFeedback.None;
            lastResult = null;
            RefreshView();
        }

        public CraftResult CraftSelected()
        {
            if (!CanInteract || isSubmitting || snapshot?.SelectedRecipe == null)
                return null;

            var selected = snapshot.SelectedRecipe;
            if (lastIntentFrame == Time.frameCount && lastIntentRecipe == selected.RecipeId)
                return lastIntentResult;

            lastIntentFrame = Time.frameCount;
            lastIntentRecipe = selected.RecipeId;
            lastIntentResult = null;

            if (!selected.CanCraft)
            {
                feedback = CraftingTableFeedback.MissingIngredients;
                lastResult = null;
                RefreshView();
                return null;
            }

            isSubmitting = true;
            try
            {
                var context = new CraftContext(
                    "VR-крафт на Crafting Table",
                    Guid.NewGuid(),
                    Guid.NewGuid());
                lastResult = service.Craft(selected.RecipeId, context);
                lastIntentResult = lastResult;
                feedback = lastResult.Succeeded
                    ? lastResult.IsReplay ? CraftingTableFeedback.Replay : CraftingTableFeedback.Success
                    : lastResult.Rejection == CraftRejection.MissingIngredients
                        ? CraftingTableFeedback.MissingIngredients
                        : CraftingTableFeedback.Failure;
                RefreshView();
                InvokeIsolated(CraftCompleted, lastResult);
                return lastResult;
            }
            finally
            {
                isSubmitting = false;
            }
        }

        public void RequestCraftSelected() => CraftSelected();

        public void RefreshView()
        {
            if (recipes == null || items == null || inventory == null || view == null)
                return;

            if (recipes.Definitions.Count == 0)
                selectedIndex = 0;
            else
                selectedIndex = Mathf.Clamp(selectedIndex, 0, recipes.Definitions.Count - 1);

            var readModels = new List<CraftingRecipeAvailability>(recipes.Definitions.Count);
            foreach (var recipe in recipes.Definitions)
            {
                var ingredients = new List<CraftingIngredientAvailability>(recipe.Ingredients.Count);
                foreach (var ingredient in recipe.Ingredients)
                {
                    var definition = items.GetRequired(ingredient.ItemId);
                    ingredients.Add(new CraftingIngredientAvailability(
                        ingredient.ItemId,
                        definition.DisplayName,
                        ingredient.Quantity,
                        inventory.GetQuantity(ingredient.ItemId)));
                }

                var output = items.GetRequired(recipe.OutputItemId);
                readModels.Add(new CraftingRecipeAvailability(
                    recipe.Id,
                    recipe.DisplayName,
                    recipe.OutputItemId,
                    output.DisplayName,
                    recipe.OutputQuantity,
                    inventory.GetQuantity(recipe.OutputItemId),
                    ingredients));
            }

            snapshot = new CraftingTableSnapshot(readModels, selectedIndex, feedback, lastResult);
            view.Render(snapshot);
            InvokeIsolated(SnapshotChanged, snapshot);
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshView();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetIntent();
            isSubmitting = false;
        }

        private void OnApplicationPause(bool paused) => SetApplicationPaused(paused);

        private void OnApplicationFocus(bool focused) => SetApplicationFocused(focused);

        public void SetApplicationPaused(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                ResetIntent();
            else
                RefreshView();
        }

        public void SetApplicationFocused(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                ResetIntent();
            else
                RefreshView();
        }

        private void HandleInventoryChanged(InventoryChanged _) => RefreshView();

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || inventoryChanges == null)
                return;
            inventoryChanges.Changed += HandleInventoryChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (inventoryChanges != null)
                inventoryChanges.Changed -= HandleInventoryChanged;
            subscribed = false;
        }

        private void ResetIntent()
        {
            lastIntentFrame = -1;
            lastIntentRecipe = default;
            lastIntentResult = null;
        }

        private void InvokeIsolated<T>(Action<T> callbacks, T value)
        {
            var handlers = callbacks?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<T> handler in handlers)
            {
                try { handler(value); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
