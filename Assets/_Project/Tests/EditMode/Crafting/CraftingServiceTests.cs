using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Crafting;
using VrGame.Domain.Inventory;

namespace VrGame.Tests.EditMode.Crafting
{
    public sealed class CraftingServiceTests
    {
        private static readonly ItemId HerbA = new ItemId("item.herb.alpha");
        private static readonly ItemId HerbB = new ItemId("item.herb.beta");
        private static readonly ItemId Medicine = new ItemId("item.medicine.tonic");
        private static readonly ItemId OtherMedicine = new ItemId("item.medicine.other");
        private static readonly RecipeId TonicRecipe = new RecipeId("recipe.tonic");
        private static readonly RecipeId OtherRecipe = new RecipeId("recipe.other");

        [Test]
        public void Craft_ConsumesEveryIngredientAddsExactOutputAndCommitsOnce()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 5, 1);
            Stock(inventory, HerbB, 4, 2);
            inventorySink.Events.Clear();
            var service = CreateService(inventory, catalog, craftSink);
            var versionBefore = inventory.Version;
            var context = Context(10);

            var result = service.Craft(TonicRecipe, context);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsReplay, Is.False);
            Assert.That(inventory.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(3));
            Assert.That(inventory.GetQuantity(HerbB), Is.EqualTo(1));
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(2));
            Assert.That(result.OutputItemId, Is.EqualTo(Medicine));
            Assert.That(result.OutputQuantity, Is.EqualTo(2));
            Assert.That(result.InventoryChange.Kind, Is.EqualTo(InventoryMutationKind.Transform));
            Assert.That(result.InventoryChange.Changes.Select(change => change.ItemId),
                Is.EqualTo(new[] { HerbA, HerbB, Medicine }));
            Assert.That(result.InventoryChange.Changes.Select(change => change.Delta),
                Is.EqualTo(new[] { -2, -3, 2 }));
            Assert.That(inventorySink.Events, Has.Count.EqualTo(1));
            Assert.That(craftSink.Events, Has.Count.EqualTo(1));
            Assert.That(craftSink.Events.Single(), Is.SameAs(result.Completed));
            Assert.That(result.Completed.CommandId, Is.EqualTo(context.CommandId));
            Assert.That(result.Completed.CorrelationId, Is.EqualTo(context.CorrelationId));
            Assert.That(result.Completed.InventoryVersion, Is.EqualTo(inventory.Version));
            Assert.That(result.InventoryEventPublished, Is.True);
            Assert.That(result.CraftEventPublished, Is.True);
        }

        [Test]
        public void Craft_WithSeveralMissingIngredientsReturnsCompleteStableListWithoutMutation()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 1, 1);
            inventorySink.Events.Clear();
            var service = CreateService(inventory, catalog, craftSink);
            var versionBefore = inventory.Version;

            var result = service.Craft(TonicRecipe, Context(11));

            Assert.That(result.Rejection, Is.EqualTo(CraftRejection.MissingIngredients));
            Assert.That(result.InventoryRejection, Is.EqualTo(InventoryRejection.InsufficientStock));
            Assert.That(result.MissingIngredients.Select(missing => missing.ItemId),
                Is.EqualTo(new[] { HerbA, HerbB }));
            Assert.That(result.MissingIngredients.Select(missing => missing.RequiredQuantity),
                Is.EqualTo(new[] { 2, 3 }));
            Assert.That(result.MissingIngredients.Select(missing => missing.AvailableQuantity),
                Is.EqualTo(new[] { 1, 0 }));
            Assert.That(result.MissingIngredients.Select(missing => missing.MissingQuantity),
                Is.EqualTo(new[] { 1, 3 }));
            Assert.That(inventory.Version, Is.EqualTo(versionBefore));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(1));
            Assert.That(inventory.GetQuantity(HerbB), Is.Zero);
            Assert.That(inventory.GetQuantity(Medicine), Is.Zero);
            Assert.That(inventorySink.Events, Is.Empty);
            Assert.That(craftSink.Events, Is.Empty);
        }

        [Test]
        public void Craft_WhenOutputWouldOverflowReturnsTypedFailureWithoutConsumingInputs()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 2, 1);
            Stock(inventory, HerbB, 3, 2);
            Stock(inventory, Medicine, int.MaxValue, 3);
            inventorySink.Events.Clear();
            var service = CreateService(inventory, catalog, craftSink);
            var versionBefore = inventory.Version;

            var result = service.Craft(TonicRecipe, Context(12));

            Assert.That(result.Rejection, Is.EqualTo(CraftRejection.OutputRejected));
            Assert.That(result.OutputFailure, Is.EqualTo(CraftOutputFailure.ArithmeticOverflow));
            Assert.That(result.InventoryRejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(inventory.Version, Is.EqualTo(versionBefore));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(HerbB), Is.EqualTo(3));
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(int.MaxValue));
            Assert.That(inventorySink.Events, Is.Empty);
            Assert.That(craftSink.Events, Is.Empty);
        }

        [Test]
        public void Craft_WhenInventoryVersionWouldOverflowReturnsTypedFailureWithoutMutation()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 2, 1);
            Stock(inventory, HerbB, 3, 2);
            inventorySink.Events.Clear();
            var versionProperty = typeof(VrGame.Domain.Inventory.Inventory)
                .GetProperty(nameof(VrGame.Domain.Inventory.Inventory.Version));
            Assert.That(versionProperty, Is.Not.Null);
            versionProperty.SetValue(inventory, long.MaxValue);
            var service = CreateService(inventory, catalog, craftSink);

            var result = service.Craft(TonicRecipe, Context(26));

            Assert.That(result.Rejection, Is.EqualTo(CraftRejection.InventoryRejected));
            Assert.That(result.InventoryRejection, Is.EqualTo(InventoryRejection.ArithmeticOverflow));
            Assert.That(inventory.Version, Is.EqualTo(long.MaxValue));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(HerbB), Is.EqualTo(3));
            Assert.That(inventory.GetQuantity(Medicine), Is.Zero);
            Assert.That(inventorySink.Events, Is.Empty);
            Assert.That(craftSink.Events, Is.Empty);
        }

        [Test]
        public void Craft_SuccessReplayIsIdempotentAndConflictingPayloadIsRejected()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 4, 1);
            Stock(inventory, HerbB, 6, 2);
            inventorySink.Events.Clear();
            var service = CreateService(inventory, catalog, craftSink);
            var context = Context(13);

            var first = service.Craft(TonicRecipe, context);
            var replay = service.Craft(TonicRecipe, context);
            var conflict = service.Craft(OtherRecipe,
                new CraftContext(context.Reason, context.CommandId, context.CorrelationId));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.InventoryChange, Is.SameAs(first.InventoryChange));
            Assert.That(replay.Completed, Is.SameAs(first.Completed));
            Assert.That(conflict.Rejection, Is.EqualTo(CraftRejection.CommandConflict));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(HerbB), Is.EqualTo(3));
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(2));
            Assert.That(inventorySink.Events, Has.Count.EqualTo(1));
            Assert.That(craftSink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void Craft_FailureReplayRemainsStableAfterStockChangesAndConflictsOnContextChange()
        {
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, inventorySink);
            var service = CreateService(inventory, catalog, craftSink);
            var context = Context(14);

            var first = service.Craft(TonicRecipe, context);
            Stock(inventory, HerbA, 2, 1);
            Stock(inventory, HerbB, 3, 2);
            inventorySink.Events.Clear();
            var replay = service.Craft(TonicRecipe, context);
            var conflict = service.Craft(TonicRecipe,
                new CraftContext("craft.changed", context.CommandId, context.CorrelationId));

            Assert.That(first.Rejection, Is.EqualTo(CraftRejection.MissingIngredients));
            Assert.That(replay.Rejection, Is.EqualTo(CraftRejection.MissingIngredients));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.MissingIngredients.Select(value => value.MissingQuantity),
                Is.EqualTo(first.MissingIngredients.Select(value => value.MissingQuantity)));
            Assert.That(conflict.Rejection, Is.EqualTo(CraftRejection.CommandConflict));
            Assert.That(inventory.GetQuantity(Medicine), Is.Zero);
            Assert.That(inventorySink.Events, Is.Empty);
            Assert.That(craftSink.Events, Is.Empty);
        }

        [Test]
        public void Craft_RejectsUnknownRecipeInvalidContextAndCatalogMismatchWithoutMutation()
        {
            var fullCatalog = CreateItemCatalog();
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var inventory = CreateInventory(fullCatalog, inventorySink);
            var service = CreateService(inventory, fullCatalog, craftSink);

            var invalid = service.Craft(TonicRecipe, default);
            var unknown = service.Craft(new RecipeId("recipe.unknown"), Context(15));

            Assert.That(invalid.Rejection, Is.EqualTo(CraftRejection.InvalidContext));
            Assert.That(unknown.Rejection, Is.EqualTo(CraftRejection.UnknownRecipe));
            Assert.That(inventory.Version, Is.Zero);

            var inputMismatchCatalog = new ItemCatalog(new[] { Definition(Medicine, ItemCategory.Medicine) });
            var inputMismatchInventory = CreateInventory(inputMismatchCatalog, new RecordingInventorySink());
            var inputMismatch = CreateService(inputMismatchInventory, fullCatalog, new RecordingCraftingSink())
                .Craft(TonicRecipe, Context(16));
            Assert.That(inputMismatch.Rejection, Is.EqualTo(CraftRejection.InventoryRejected));
            Assert.That(inputMismatch.InventoryRejection, Is.EqualTo(InventoryRejection.UnknownItem));

            var outputMismatchCatalog = new ItemCatalog(new[]
            {
                Definition(HerbA, ItemCategory.HarvestedPlant),
                Definition(HerbB, ItemCategory.HarvestedPlant)
            });
            var outputMismatchInventory = CreateInventory(outputMismatchCatalog, new RecordingInventorySink());
            Stock(outputMismatchInventory, HerbA, 2, 3);
            Stock(outputMismatchInventory, HerbB, 3, 4);
            var mismatchVersion = outputMismatchInventory.Version;
            var outputMismatch = CreateService(outputMismatchInventory, fullCatalog, new RecordingCraftingSink())
                .Craft(TonicRecipe, Context(17));
            Assert.That(outputMismatch.Rejection, Is.EqualTo(CraftRejection.OutputRejected));
            Assert.That(outputMismatch.OutputFailure, Is.EqualTo(CraftOutputFailure.UnknownItem));
            Assert.That(outputMismatch.InventoryRejection, Is.EqualTo(InventoryRejection.UnknownItem));
            Assert.That(outputMismatchInventory.Version, Is.EqualTo(mismatchVersion));
            Assert.That(outputMismatchInventory.GetQuantity(HerbA), Is.EqualTo(2));
            Assert.That(outputMismatchInventory.GetQuantity(HerbB), Is.EqualTo(3));
        }

        [Test]
        public void Craft_WhenOutputIsAlsoInputUsesConsumeThenAddAndOneConsolidatedChange()
        {
            var catalog = new ItemCatalog(new[] { Definition(Medicine, ItemCategory.Medicine) });
            var recipe = new RecipeDefinition(
                TonicRecipe,
                "Recycle tonic",
                new[] { new RecipeIngredient(Medicine, 3) },
                Medicine,
                2);
            var recipes = new RecipeCatalog(catalog, new[] { recipe });
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, Medicine, 5, 1);
            inventorySink.Events.Clear();
            var versionBefore = inventory.Version;

            var result = new CraftingService(inventory, recipes, craftSink).Craft(TonicRecipe, Context(18));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(4));
            Assert.That(inventory.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(result.InventoryChange.Changes, Has.Count.EqualTo(1));
            Assert.That(result.InventoryChange.Changes.Single().Before, Is.EqualTo(5));
            Assert.That(result.InventoryChange.Changes.Single().After, Is.EqualTo(4));
            Assert.That(result.InventoryChange.Changes.Single().Delta, Is.EqualTo(-1));
            Assert.That(inventorySink.Events, Has.Count.EqualTo(1));
            Assert.That(craftSink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void Craft_WhenOutputIsInputChecksOverflowAfterConsumption()
        {
            var catalog = new ItemCatalog(new[] { Definition(Medicine, ItemCategory.Medicine) });
            var recipe = new RecipeDefinition(
                TonicRecipe,
                "Expand tonic",
                new[] { new RecipeIngredient(Medicine, 2) },
                Medicine,
                3);
            var recipes = new RecipeCatalog(catalog, new[] { recipe });
            var inventory = CreateInventory(catalog, new RecordingInventorySink());
            Stock(inventory, Medicine, int.MaxValue, 1);
            var versionBefore = inventory.Version;

            var result = new CraftingService(inventory, recipes, new RecordingCraftingSink())
                .Craft(TonicRecipe, Context(19));

            Assert.That(result.Rejection, Is.EqualTo(CraftRejection.OutputRejected));
            Assert.That(result.OutputFailure, Is.EqualTo(CraftOutputFailure.ArithmeticOverflow));
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(int.MaxValue));
            Assert.That(inventory.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void Craft_EventFailuresAreSafeAndReplayDoesNotRepublish()
        {
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, new ThrowingInventorySink());
            Stock(inventory, HerbA, 2, 1);
            Stock(inventory, HerbB, 3, 2);
            var craftSink = new ThrowingCraftingSink();
            var service = CreateService(inventory, catalog, craftSink);
            var context = Context(20);

            var result = service.Craft(TonicRecipe, context);
            var replay = service.Craft(TonicRecipe, context);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.InventoryEventPublished, Is.False);
            Assert.That(result.CraftEventPublished, Is.False);
            Assert.That(result.Completed, Is.Not.Null);
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(2));
            Assert.That(craftSink.Attempts, Is.EqualTo(1));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(craftSink.Attempts, Is.EqualTo(1));
        }

        [Test]
        public void Craft_ReentrantCommandFromEventSinkIsRejectedWithoutSecondMutation()
        {
            var catalog = CreateItemCatalog();
            var inventorySink = new RecordingInventorySink();
            var craftSink = new RecordingCraftingSink();
            var inventory = CreateInventory(catalog, inventorySink);
            Stock(inventory, HerbA, 4, 1);
            Stock(inventory, HerbB, 6, 2);
            inventorySink.Events.Clear();
            var service = CreateService(inventory, catalog, craftSink);
            InventoryMutationResult reentrantInventoryResult = null;
            inventorySink.OnPublish = _ => reentrantInventoryResult = inventory.Add(
                HerbA,
                1,
                new InventoryChangeContext("craft.test.reentrant.inventory", GuidFor(25)));
            craftSink.OnPublish = _ => craftSink.ReentrantResult = service.Craft(TonicRecipe, Context(22));

            var result = service.Craft(TonicRecipe, Context(21));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(reentrantInventoryResult.Rejection, Is.EqualTo(InventoryRejection.ReentrantMutation));
            Assert.That(craftSink.ReentrantResult.Rejection, Is.EqualTo(CraftRejection.ReentrantCommand));
            Assert.That(inventory.GetQuantity(Medicine), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(HerbA), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity(HerbB), Is.EqualTo(3));
            Assert.That(inventorySink.Events, Has.Count.EqualTo(1));
            Assert.That(craftSink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void Craft_ResultAndEventsExposeIndependentReadOnlyCollections()
        {
            var catalog = CreateItemCatalog();
            var inventory = CreateInventory(catalog, new RecordingInventorySink());
            var service = CreateService(inventory, catalog, new RecordingCraftingSink());
            var missing = service.Craft(TonicRecipe, Context(23));

            Assert.That(missing.MissingIngredients, Is.InstanceOf<IList>());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)missing.MissingIngredients).Add(new CraftMissingIngredient(HerbA, 1, 0)));

            Stock(inventory, HerbA, 2, 1);
            Stock(inventory, HerbB, 3, 2);
            var success = service.Craft(TonicRecipe, Context(24));
            var capturedChanges = success.Completed.Changes.ToArray();
            Stock(inventory, Medicine, 1, 3);

            Assert.That(success.Completed.Changes, Is.InstanceOf<IList>());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)success.Completed.Changes).Add(capturedChanges[0]));
            Assert.That(success.Completed.Changes.Select(change => change.Delta),
                Is.EqualTo(new[] { -2, -3, 2 }));
        }

        private static CraftingService CreateService(
            VrGame.Domain.Inventory.Inventory inventory,
            ItemCatalog catalog,
            ICraftingEventSink sink) =>
            new CraftingService(inventory, CreateRecipeCatalog(catalog), sink);

        private static RecipeCatalog CreateRecipeCatalog(ItemCatalog catalog) => new RecipeCatalog(
            catalog,
            new[]
            {
                new RecipeDefinition(
                    TonicRecipe,
                    "Tonic",
                    new[]
                    {
                        new RecipeIngredient(HerbB, 3),
                        new RecipeIngredient(HerbA, 2)
                    },
                    Medicine,
                    2),
                new RecipeDefinition(
                    OtherRecipe,
                    "Other medicine",
                    new[] { new RecipeIngredient(HerbA, 1) },
                    OtherMedicine,
                    1)
            });

        private static ItemCatalog CreateItemCatalog() => new ItemCatalog(new[]
        {
            Definition(HerbA, ItemCategory.HarvestedPlant),
            Definition(HerbB, ItemCategory.HarvestedPlant),
            Definition(Medicine, ItemCategory.Medicine),
            Definition(OtherMedicine, ItemCategory.Medicine)
        });

        private static ItemDefinition Definition(ItemId id, ItemCategory category) => new ItemDefinition(
            id,
            id.Value,
            category,
            new ItemRepresentationKey("representation.test.item"),
            new ItemStackRules(20, false));

        private static VrGame.Domain.Inventory.Inventory CreateInventory(
            ItemCatalog catalog,
            IInventoryEventSink sink) => new VrGame.Domain.Inventory.Inventory(
            new InventoryId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            catalog,
            sink);

        private static void Stock(
            VrGame.Domain.Inventory.Inventory inventory,
            ItemId itemId,
            int quantity,
            int seed)
        {
            var result = inventory.Add(
                itemId,
                quantity,
                new InventoryChangeContext("craft.test.setup", GuidFor(seed)));
            Assert.That(result.Succeeded, Is.True);
        }

        private static CraftContext Context(int seed) =>
            new CraftContext("craft.test", GuidFor(seed), GuidFor(seed + 100));

        private static Guid GuidFor(int seed) => new Guid(seed, 0, 0, new byte[8]);

        private sealed class RecordingInventorySink : IInventoryEventSink
        {
            public List<InventoryChanged> Events { get; } = new List<InventoryChanged>();
            public Action<InventoryChanged> OnPublish { get; set; }

            public bool TryPublish(InventoryChanged inventoryChanged)
            {
                Events.Add(inventoryChanged);
                OnPublish?.Invoke(inventoryChanged);
                return true;
            }
        }

        private sealed class RecordingCraftingSink : ICraftingEventSink
        {
            public List<CraftCompleted> Events { get; } = new List<CraftCompleted>();
            public Action<CraftCompleted> OnPublish { get; set; }
            public CraftResult ReentrantResult { get; set; }

            public bool TryPublish(CraftCompleted craftCompleted)
            {
                Events.Add(craftCompleted);
                OnPublish?.Invoke(craftCompleted);
                return true;
            }
        }

        private sealed class ThrowingInventorySink : IInventoryEventSink
        {
            public bool TryPublish(InventoryChanged inventoryChanged) =>
                throw new InvalidOperationException("Test inventory event failure.");
        }

        private sealed class ThrowingCraftingSink : ICraftingEventSink
        {
            public int Attempts { get; private set; }

            public bool TryPublish(CraftCompleted craftCompleted)
            {
                Attempts++;
                throw new InvalidOperationException("Test crafting event failure.");
            }
        }
    }
}
