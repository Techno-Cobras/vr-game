using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Bootstrap;
using VrGame.DataAssets;
using VrGame.DataAssets.Crafting;
using VrGame.Domain.Crafting;
using VrGame.Domain.Inventory;
using VrGame.Presentation.Crafting;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.Crafting
{
    public sealed class CraftingScenarioBTests
    {
        private const string Samples = "Assets/_Project/Crafting/Samples/";
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ScenarioB_ConfiguredRecipe_TableToMedicineConservesInventory(int recipeIndex)
        {
            var itemAsset = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(Samples + "ItemCatalog.asset");
            var recipeAsset = AssetDatabase.LoadAssetAtPath<RecipeCatalogAsset>(Samples + "RecipeCatalog.asset");
            Assert.That(itemAsset, Is.Not.Null);
            Assert.That(recipeAsset, Is.Not.Null);
            var items = itemAsset.Build();
            var recipes = recipeAsset.Build(items);
            var recipe = recipes.Definitions[recipeIndex];
            var inventoryEvents = new InventoryEventRelay();
            var inventory = new InventoryAggregate(InventoryId.New(), items.Items, inventoryEvents);
            var craftEvents = new CraftingEventRelay();
            var service = new CraftingService(inventory, recipes, craftEvents);
            var view = new RecordingView();
            host = new GameObject("Scenario B Crafting Table");
            var controller = host.AddComponent<CraftingTableController>();
            controller.Configure(service, recipes, items.Items, inventory, inventoryEvents, view);
            for (var index = 0; index < recipeIndex; index++)
                controller.SelectNextRecipe();
            Assert.That(view.Snapshot.SelectedRecipe.RecipeId, Is.EqualTo(recipe.Id));
            Assert.That(view.Snapshot.SelectedRecipe.CanCraft, Is.False);

            // Keep enough ingredients for two crafts: a duplicate cannot pass merely
            // because the first craft exhausted the inventory.
            foreach (var ingredient in recipe.Ingredients)
                Assert.That(inventory.Add(ingredient.ItemId, ingredient.Quantity * 2,
                    new InventoryChangeContext("Scenario B harvest setup", Guid.NewGuid(), Guid.NewGuid())).Succeeded, Is.True);
            Assert.That(view.Snapshot.SelectedRecipe.CanCraft, Is.True);
            var before = items.Items.Definitions.ToDictionary(item => item.Id, item => inventory.GetQuantity(item.Id));
            var version = inventory.Version;
            var inventoryNotifications = 0;
            var completionNotifications = 0;
            var uiCompletions = 0;
            CraftResult reentrantResult = null;
            inventoryEvents.Changed += _ => inventoryNotifications++;
            craftEvents.Completed += _ => completionNotifications++;
            controller.CraftCompleted += _ =>
            {
                uiCompletions++;
                reentrantResult = controller.CraftSelected();
            };

            var first = controller.CraftSelected();
            var duplicate = controller.CraftSelected();

            Assert.That(first, Is.Not.Null);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(reentrantResult, Is.Null);
            Assert.That(uiCompletions, Is.EqualTo(1));
            Assert.That(inventory.Version, Is.EqualTo(version + 1));
            Assert.That(inventoryNotifications, Is.EqualTo(1));
            Assert.That(completionNotifications, Is.EqualTo(1));
            foreach (var item in items.Items.Definitions)
            {
                var consumed = recipe.Ingredients.Where(value => value.ItemId == item.Id).Sum(value => value.Quantity);
                var produced = item.Id == recipe.OutputItemId ? recipe.OutputQuantity : 0;
                Assert.That(inventory.GetQuantity(item.Id), Is.EqualTo(before[item.Id] - consumed + produced), item.Id.Value);
            }
            Assert.That(view.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.Success));
            Assert.That(view.Snapshot.SelectedRecipe.OwnedOutputQuantity, Is.EqualTo(recipe.OutputQuantity));
            Assert.That(view.Snapshot.SelectedRecipe.CanCraft, Is.True);

            // Replay the original domain command after the UI guard has completed.
            var replay = service.Craft(recipe.Id, new CraftContext(first.Completed.Reason,
                first.Completed.CommandId, first.Completed.CorrelationId));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(inventory.Version, Is.EqualTo(version + 1));
            Assert.That(inventoryNotifications, Is.EqualTo(1));
            Assert.That(completionNotifications, Is.EqualTo(1));

            // Navigation starts another intentional craft, even in this EditMode frame.
            controller.SelectNextRecipe();
            controller.SelectPreviousRecipe();
            Assert.That(controller.CraftSelected().Succeeded, Is.True);
            Assert.That(inventory.Version, Is.EqualTo(version + 2));
            Assert.That(inventoryNotifications, Is.EqualTo(2));
            Assert.That(completionNotifications, Is.EqualTo(2));
            Assert.That(view.Snapshot.SelectedRecipe.CanCraft, Is.False);
            foreach (var ingredient in recipe.Ingredients)
                Assert.That(inventory.GetQuantity(ingredient.ItemId), Is.Zero);
            Assert.That(inventory.GetQuantity(recipe.OutputItemId), Is.EqualTo(recipe.OutputQuantity * 2));

            controller.SelectNextRecipe();
            controller.SelectPreviousRecipe();
            var exhausted = items.Items.Definitions.Select(item => inventory.GetQuantity(item.Id)).ToArray();
            Assert.That(controller.CraftSelected(), Is.Null);
            Assert.That(view.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.MissingIngredients));
            Assert.That(view.Snapshot.SelectedRecipe.Ingredients.All(value => value.MissingQuantity == value.RequiredQuantity), Is.True);
            Assert.That(inventory.Version, Is.EqualTo(version + 2));
            Assert.That(items.Items.Definitions.Select(item => inventory.GetQuantity(item.Id)), Is.EqualTo(exhausted));
            Assert.That(inventoryNotifications, Is.EqualTo(2));
            Assert.That(completionNotifications, Is.EqualTo(2));
        }

        [Test]
        public void ScenarioB_PartialShortage_PreservesAvailableIngredientsAndReportsExactMissingQuantity()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(Samples + "ItemCatalog.asset").Build();
            var recipes = AssetDatabase.LoadAssetAtPath<RecipeCatalogAsset>(Samples + "RecipeCatalog.asset").Build(items);
            var recipe = recipes.Definitions.First(value => value.Ingredients.Count > 1);
            var shortIngredient = recipe.Ingredients.Last();
            var inventoryEvents = new InventoryEventRelay();
            var inventory = new InventoryAggregate(InventoryId.New(), items.Items, inventoryEvents);
            foreach (var ingredient in recipe.Ingredients)
            {
                var quantity = ingredient.Quantity - (ingredient.ItemId == shortIngredient.ItemId ? 1 : 0);
                if (quantity > 0)
                    Assert.That(inventory.Add(ingredient.ItemId, quantity,
                        new InventoryChangeContext("Scenario B partial stock", Guid.NewGuid(), Guid.NewGuid())).Succeeded, Is.True);
            }
            var craftEvents = new CraftingEventRelay();
            var service = new CraftingService(inventory, recipes, craftEvents);
            var view = new RecordingView();
            host = new GameObject("Scenario B partial shortage");
            var controller = host.AddComponent<CraftingTableController>();
            controller.Configure(service, recipes, items.Items, inventory, inventoryEvents, view);
            for (var index = 0; index < recipes.Definitions.Count &&
                controller.Snapshot.SelectedRecipe.RecipeId != recipe.Id; index++)
                controller.SelectNextRecipe();
            Assert.That(view.Snapshot.SelectedRecipe.RecipeId, Is.EqualTo(recipe.Id));
            Assert.That(view.Snapshot.SelectedRecipe.Ingredients.Any(value => value.IsAvailable), Is.True);
            Assert.That(view.Snapshot.SelectedRecipe.CanCraft, Is.False);
            foreach (var ingredient in view.Snapshot.SelectedRecipe.Ingredients)
                Assert.That(ingredient.MissingQuantity,
                    Is.EqualTo(ingredient.ItemId == shortIngredient.ItemId ? 1 : 0));

            var quantities = items.Items.Definitions.Select(item => inventory.GetQuantity(item.Id)).ToArray();
            var version = inventory.Version;
            var inventoryNotifications = 0;
            var craftNotifications = 0;
            inventoryEvents.Changed += _ => inventoryNotifications++;
            craftEvents.Completed += _ => craftNotifications++;
            Assert.That(controller.CraftSelected(), Is.Null);
            Assert.That(view.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.MissingIngredients));

            // Exercise the transaction rejection too, independently of the UI precheck.
            var rejected = service.Craft(recipe.Id,
                new CraftContext("Scenario B partial shortage", Guid.NewGuid(), Guid.NewGuid()));
            Assert.That(rejected.Rejection, Is.EqualTo(CraftRejection.MissingIngredients));
            Assert.That(rejected.MissingIngredients, Has.Count.EqualTo(1));
            Assert.That(rejected.MissingIngredients[0].ItemId, Is.EqualTo(shortIngredient.ItemId));
            Assert.That(rejected.MissingIngredients[0].MissingQuantity, Is.EqualTo(1));
            Assert.That(inventory.Version, Is.EqualTo(version));
            Assert.That(items.Items.Definitions.Select(item => inventory.GetQuantity(item.Id)), Is.EqualTo(quantities));
            Assert.That(inventoryNotifications, Is.Zero);
            Assert.That(craftNotifications, Is.Zero);
        }

        private sealed class RecordingView : ICraftingTableView
        {
            public CraftingTableSnapshot Snapshot { get; private set; }
            public void Render(CraftingTableSnapshot snapshot) => Snapshot = snapshot;
        }
    }
}
