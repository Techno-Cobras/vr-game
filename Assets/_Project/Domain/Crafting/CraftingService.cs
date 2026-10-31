using System;
using System.Collections.Generic;
using System.Linq;
using VrGame.Data.Crafting;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Crafting
{
    public sealed class CraftingService
    {
        private readonly Inventory.Inventory inventory;
        private readonly RecipeCatalog recipes;
        private readonly ICraftingEventSink eventSink;
        private readonly Dictionary<Guid, CompletedCommand> completedCommands =
            new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public CraftingService(
            Inventory.Inventory inventory,
            RecipeCatalog recipes,
            ICraftingEventSink eventSink)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public CraftResult Craft(RecipeId recipeId, CraftContext context)
        {
            if (isExecuting)
                return CraftResult.Reject(CraftRejection.ReentrantCommand, recipeId);
            if (!context.IsValid)
                return CraftResult.Reject(CraftRejection.InvalidContext, recipeId);

            var payload = new CommandPayload(recipeId, context.Reason, context.CorrelationId);
            if (completedCommands.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(payload))
                    return CraftResult.Reject(CraftRejection.CommandConflict, recipeId);
                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = Execute(recipeId, context);
                completedCommands.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private CraftResult Execute(RecipeId recipeId, CraftContext context)
        {
            if (!recipes.TryGet(recipeId, out var recipe))
                return CraftResult.Reject(CraftRejection.UnknownRecipe, recipeId);
            if (!inventory.TryAcquireCoordinatedMutation())
                return CraftResult.Reject(CraftRejection.ReentrantCommand, recipeId);

            try
            {
                var inventoryContext = new InventoryChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var preparation = inventory.TryPrepareCoordinatedTransform(
                    recipe.Ingredients.Select(value =>
                        new InventoryItemQuantity(value.ItemId, value.Quantity)),
                    recipe.OutputItemId,
                    recipe.OutputQuantity,
                    inventoryContext);
                if (!preparation.Succeeded)
                    return MapFailure(recipe, preparation);

                var inventoryChange = inventory.CommitPreparedTransform(preparation.Prepared);
                var inventoryPublished = inventory.PublishPrepared(inventoryChange);
                var completed = new CraftCompleted(
                    recipe.Id,
                    recipe.OutputItemId,
                    recipe.OutputQuantity,
                    context,
                    inventoryChange);
                var craftPublished = false;
                try
                {
                    craftPublished = eventSink.TryPublish(completed);
                }
                catch (Exception)
                {
                    // Inventory transform уже зафиксирована; event остаётся доступен в result.
                }
                return CraftResult.Success(
                    recipe,
                    inventoryChange,
                    completed,
                    inventoryPublished,
                    craftPublished);
            }
            finally
            {
                inventory.ReleaseCoordinatedMutation();
            }
        }

        private static CraftResult MapFailure(
            RecipeDefinition recipe,
            InventoryTransformPreparationResult preparation)
        {
            if (preparation.Missing.Count > 0)
            {
                return CraftResult.Reject(
                    CraftRejection.MissingIngredients,
                    recipe.Id,
                    recipe.OutputItemId,
                    recipe.OutputQuantity,
                    preparation.Missing.Select(value => new CraftMissingIngredient(
                        value.ItemId,
                        value.Required,
                        value.Available)),
                    inventoryRejection: preparation.Rejection);
            }

            if (preparation.FailureTarget == InventoryTransformFailureTarget.Output)
            {
                return CraftResult.Reject(
                    CraftRejection.OutputRejected,
                    recipe.Id,
                    recipe.OutputItemId,
                    recipe.OutputQuantity,
                    outputFailure: MapOutputFailure(preparation.Rejection),
                    inventoryRejection: preparation.Rejection);
            }

            return CraftResult.Reject(
                CraftRejection.InventoryRejected,
                recipe.Id,
                recipe.OutputItemId,
                recipe.OutputQuantity,
                inventoryRejection: preparation.Rejection);
        }

        private static CraftOutputFailure MapOutputFailure(InventoryRejection rejection)
        {
            switch (rejection)
            {
                case InventoryRejection.UnknownItem:
                    return CraftOutputFailure.UnknownItem;
                case InventoryRejection.InvalidQuantity:
                    return CraftOutputFailure.InvalidQuantity;
                case InventoryRejection.ArithmeticOverflow:
                    return CraftOutputFailure.ArithmeticOverflow;
                default:
                    return CraftOutputFailure.None;
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(RecipeId recipeId, string reason, Guid correlationId)
            {
                RecipeId = recipeId;
                Reason = reason;
                CorrelationId = correlationId;
            }

            private RecipeId RecipeId { get; }
            private string Reason { get; }
            private Guid CorrelationId { get; }
            public bool Equals(CommandPayload other) =>
                RecipeId.Equals(other.RecipeId) &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;
            public CompletedCommand(CommandPayload payload, CraftResult result)
            {
                this.payload = payload;
                Result = result;
            }
            public CraftResult Result { get; }
            public bool Matches(CommandPayload candidate) => payload.Equals(candidate);
        }
    }
}
