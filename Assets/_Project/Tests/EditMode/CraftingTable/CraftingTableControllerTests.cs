using System;
using NUnit.Framework;
using UnityEngine;
using VrGame.Bootstrap;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Crafting;
using VrGame.Domain.Inventory;
using VrGame.Presentation.Crafting;
using InventoryAggregate = VrGame.Domain.Inventory.Inventory;

namespace VrGame.Tests.EditMode.CraftingTable
{
    public sealed class CraftingTableControllerTests
    {
        private static readonly ItemId Basil = new ItemId("item.harvest.basil");
        private static readonly ItemId Mint = new ItemId("item.harvest.mint");
        private static readonly ItemId Tonic = new ItemId("item.medicine.tonic");
        private static readonly ItemId Blend = new ItemId("item.medicine.blend");
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void Configure_BuildsStableRecipeListAndExactMissingDetails()
        {
            var fixture = CreateFixture();

            var snapshot = fixture.Controller.Snapshot;

            Assert.That(snapshot.Recipes, Has.Count.EqualTo(2));
            Assert.That(snapshot.Recipes[0].RecipeId.Value, Is.EqualTo("recipe.a-tonic"));
            Assert.That(snapshot.Recipes[1].RecipeId.Value, Is.EqualTo("recipe.b-blend"));
            Assert.That(snapshot.SelectedRecipe.Ingredients[0].RequiredQuantity, Is.EqualTo(2));
            Assert.That(snapshot.SelectedRecipe.Ingredients[0].AvailableQuantity, Is.Zero);
            Assert.That(snapshot.SelectedRecipe.Ingredients[0].MissingQuantity, Is.EqualTo(2));
            Assert.That(snapshot.SelectedRecipe.CanCraft, Is.False);
            Assert.That(fixture.View.LastSnapshot, Is.SameAs(snapshot));
        }

        [Test]
        public void MissingIngredients_BlockServiceAndExposeFeedback()
        {
            var fixture = CreateFixture();
            var version = fixture.Inventory.Version;

            var result = fixture.Controller.CraftSelected();

            Assert.That(result, Is.Null);
            Assert.That(fixture.Inventory.Version, Is.EqualTo(version));
            Assert.That(fixture.CraftEvents, Is.Zero);
            Assert.That(fixture.Controller.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.MissingIngredients));
        }

        [Test]
        public void SuccessAndSameFrameDuplicate_CreateOneCraftAndRefreshInventory()
        {
            var fixture = CreateFixture(basil: 2);
            var version = fixture.Inventory.Version;

            var first = fixture.Controller.CraftSelected();
            var duplicate = fixture.Controller.CraftSelected();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(fixture.Inventory.Version, Is.EqualTo(version + 1));
            Assert.That(fixture.Inventory.GetQuantity(Basil), Is.Zero);
            Assert.That(fixture.Inventory.GetQuantity(Tonic), Is.EqualTo(1));
            Assert.That(fixture.CraftEvents, Is.EqualTo(1));
            Assert.That(fixture.Controller.Snapshot.SelectedRecipe.OwnedOutputQuantity, Is.EqualTo(1));
            Assert.That(fixture.Controller.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.Success));
        }

        [Test]
        public void ExternalInventoryEvent_RecomputesAvailabilityFromAuthoritativeInventory()
        {
            var fixture = CreateFixture();

            fixture.Inventory.Add(Basil, 2, InventoryContext("внешний урожай"));

            Assert.That(fixture.Controller.Snapshot.SelectedRecipe.CanCraft, Is.True);
            Assert.That(fixture.Controller.Snapshot.SelectedRecipe.Ingredients[0].AvailableQuantity, Is.EqualTo(2));
            Assert.That(fixture.View.RenderCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void NavigationAndLifecycle_GateCommandsAndResetSelectionFeedback()
        {
            var fixture = CreateFixture(basil: 2, mint: 2);
            fixture.Controller.SelectNextRecipe();
            Assert.That(fixture.Controller.Snapshot.SelectedRecipe.RecipeId.Value, Is.EqualTo("recipe.b-blend"));

            fixture.Controller.SetApplicationPaused(true);
            var pausedVersion = fixture.Inventory.Version;
            Assert.That(fixture.Controller.CraftSelected(), Is.Null);
            Assert.That(fixture.Inventory.Version, Is.EqualTo(pausedVersion));

            fixture.Controller.SetApplicationPaused(false);
            fixture.Controller.SetApplicationFocused(false);
            Assert.That(fixture.Controller.CraftSelected(), Is.Null);
            fixture.Controller.SetApplicationFocused(true);
            fixture.Controller.SelectPreviousRecipe();
            Assert.That(fixture.Controller.Snapshot.SelectedRecipe.RecipeId.Value, Is.EqualTo("recipe.a-tonic"));
            Assert.That(fixture.Controller.Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.None));
        }

        private Fixture CreateFixture(int basil = 0, int mint = 0)
        {
            var items = CreateItemCatalog();
            var recipes = new RecipeCatalog(items, new[]
            {
                new RecipeDefinition(
                    new RecipeId("recipe.b-blend"),
                    "Травяная смесь",
                    new[] { new RecipeIngredient(Basil, 1), new RecipeIngredient(Mint, 2) },
                    Blend,
                    2),
                new RecipeDefinition(
                    new RecipeId("recipe.a-tonic"),
                    "Настой базилика",
                    new[] { new RecipeIngredient(Basil, 2) },
                    Tonic,
                    1)
            });
            var inventoryEvents = new InventoryEventRelay();
            var inventory = new InventoryAggregate(InventoryId.New(), items, inventoryEvents);
            if (basil > 0)
                inventory.Add(Basil, basil, InventoryContext("базилик"));
            if (mint > 0)
                inventory.Add(Mint, mint, InventoryContext("мята"));
            var craftingEvents = new CraftingEventRelay();
            var eventCount = 0;
            craftingEvents.Completed += _ => eventCount++;
            var service = new CraftingService(inventory, recipes, craftingEvents);
            var view = new RecordingView();
            host = new GameObject("Crafting Table test");
            var controller = host.AddComponent<CraftingTableController>();
            controller.Configure(service, recipes, items, inventory, inventoryEvents, view);
            return new Fixture(controller, inventory, view, () => eventCount);
        }

        private static ItemCatalog CreateItemCatalog() => new ItemCatalog(new[]
        {
            Item(Basil, "Урожай базилика", ItemCategory.HarvestedPlant),
            Item(Mint, "Урожай мяты", ItemCategory.HarvestedPlant),
            Item(Tonic, "Настой базилика", ItemCategory.Medicine),
            Item(Blend, "Травяная смесь", ItemCategory.Medicine)
        });

        private static ItemDefinition Item(ItemId id, string name, ItemCategory category) =>
            new ItemDefinition(
                id,
                name,
                category,
                new ItemRepresentationKey("representation." + id.Value),
                new ItemStackRules(16, true));

        private static InventoryChangeContext InventoryContext(string reason) =>
            new InventoryChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class RecordingView : ICraftingTableView
        {
            public int RenderCount { get; private set; }
            public CraftingTableSnapshot LastSnapshot { get; private set; }
            public void Render(CraftingTableSnapshot snapshot)
            {
                RenderCount++;
                LastSnapshot = snapshot;
            }
        }

        private sealed class Fixture
        {
            private readonly Func<int> eventCount;
            public Fixture(
                CraftingTableController controller,
                InventoryAggregate inventory,
                RecordingView view,
                Func<int> eventCount)
            {
                Controller = controller;
                Inventory = inventory;
                View = view;
                this.eventCount = eventCount;
            }
            public CraftingTableController Controller { get; }
            public InventoryAggregate Inventory { get; }
            public RecordingView View { get; }
            public int CraftEvents => eventCount();
        }
    }
}
