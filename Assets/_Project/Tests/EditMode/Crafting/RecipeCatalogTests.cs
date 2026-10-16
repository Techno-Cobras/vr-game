using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Data.Crafting;
using VrGame.Data.Items;

namespace VrGame.Tests.EditMode.Crafting
{
    public sealed class RecipeCatalogTests
    {
        [Test]
        public void Catalog_ResolvesTwoRecipesInStableOrder()
        {
            var second = Recipe("recipe.second");
            var first = Recipe();
            var catalog = new RecipeCatalog(Items(), new[] { second, first });

            Assert.That(catalog.Definitions.Select(value => value.Id.Value), Is.EqualTo(new[] { "recipe.first", "recipe.second" }));
            Assert.That(catalog.GetRequired(new RecipeId("recipe.first")), Is.SameAs(first));
            Assert.That(catalog.TryGet(second.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(second));
            Assert.That(catalog.TryGet(default, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(new RecipeId("recipe.unknown")));
            Assert.That(first.Ingredients.Select(value => value.Quantity), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(first.OutputQuantity, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("item.recipe")]
        [TestCase("recipe.First")]
        [TestCase("recipe.трава")]
        [TestCase("recipe.")]
        [TestCase("recipe.a..b")]
        [TestCase("recipe.a-")]
        public void RecipeId_RejectsInvalidValues(string value)
        {
            Error(RecipeDataValidationError.InvalidRecipeId, () => new RecipeId(value));
            Assert.That(RecipeId.TryParse(value, out var id), Is.False);
            Assert.That(id, Is.EqualTo(default(RecipeId)));
        }

        [Test]
        public void RecipeId_HasOrdinalValueSemantics()
        {
            Assert.That(RecipeId.TryParse("recipe.herbal-blend_2", out var id), Is.True);
            var same = new RecipeId("recipe.herbal-blend_2");
            Assert.That(id == same, Is.True);
            Assert.That(id != same, Is.False);
            Assert.That(id.Equals((object)same), Is.True);
            Assert.That(id.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(id.ToString(), Is.EqualTo("recipe.herbal-blend_2"));
            Assert.That(new RecipeId("recipe.a").CompareTo(id), Is.LessThan(0));
            Assert.That(default(RecipeId).Value, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Quantities_MustBePositive(int quantity)
        {
            Error(RecipeDataValidationError.InvalidIngredientQuantity, () => new RecipeIngredient(new ItemId("item.basil"), quantity));
            Error(RecipeDataValidationError.InvalidOutputQuantity, () => Definition(Ingredients(), outputQuantity: quantity));
        }

        [Test]
        public void Definition_RejectsMissingDataAndDuplicateIngredients()
        {
            Error(RecipeDataValidationError.InvalidRecipeId, () => new RecipeDefinition(default, "Recipe", Ingredients(), new ItemId("item.medicine"), 1));
            Error(RecipeDataValidationError.MissingDisplayName, () => new RecipeDefinition(new RecipeId("recipe.test"), " ", Ingredients(), new ItemId("item.medicine"), 1));
            Error(RecipeDataValidationError.InvalidIngredientItem, () => new RecipeIngredient(default, 1));
            Error(RecipeDataValidationError.InvalidOutputItem, () => new RecipeDefinition(new RecipeId("recipe.test"), "Recipe", Ingredients(), default, 1));
            Error(RecipeDataValidationError.MissingIngredients, () => Definition(null));
            Error(RecipeDataValidationError.MissingIngredients, () => Definition(Array.Empty<RecipeIngredient>()));
            Error(RecipeDataValidationError.MissingIngredient, () => Definition(new RecipeIngredient[] { null }));
            Error(RecipeDataValidationError.DuplicateIngredient, () => Definition(new[] { Ingredients()[0], Ingredients()[0] }));
        }

        [Test]
        public void Catalog_RejectsNullDataDuplicatesAndUnknownItems()
        {
            Error(RecipeDataValidationError.MissingItemCatalog, () => new RecipeCatalog(null, new[] { Recipe() }));
            Error(RecipeDataValidationError.MissingDefinition, () => new RecipeCatalog(Items(), null));
            Error(RecipeDataValidationError.MissingDefinition, () => new RecipeCatalog(Items(), new RecipeDefinition[] { null }));
            Error(RecipeDataValidationError.DuplicateRecipeId, () => new RecipeCatalog(Items(), new[] { Recipe(), Recipe() }));
            Error(RecipeDataValidationError.UnknownIngredientItem, () => new RecipeCatalog(Items(), new[] { Definition(new[] { new RecipeIngredient(new ItemId("item.unknown"), 1) }) }));
            Error(RecipeDataValidationError.UnknownOutputItem, () => new RecipeCatalog(Items(), new[] { Definition(Ingredients(), "item.unknown") }));
            Error(RecipeDataValidationError.InvalidOutputItemCategory, () => new RecipeCatalog(Items(), new[] { Definition(Ingredients(), "item.basil") }));
            Assert.That(new RecipeCatalog(Items(), Array.Empty<RecipeDefinition>()).Definitions, Is.Empty);
        }

        [Test]
        public void DefinitionsAndCatalog_DefensivelyCopyAndExposeReadOnlyCollections()
        {
            var ingredients = Ingredients().ToList();
            var recipe = Definition(ingredients);
            var definitions = new List<RecipeDefinition> { recipe };
            var catalog = new RecipeCatalog(Items(), definitions);
            ingredients.Clear();
            definitions.Clear();

            Assert.That(recipe.Ingredients, Has.Count.EqualTo(2));
            Assert.That(catalog.Definitions, Has.Count.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<RecipeIngredient>)recipe.Ingredients).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<RecipeDefinition>)catalog.Definitions).Clear());
        }

        private static RecipeDefinition Recipe(string id = "recipe.first") =>
            new RecipeDefinition(new RecipeId(id), "Herbal medicine", Ingredients(), new ItemId("item.medicine"), 1);

        private static RecipeDefinition Definition(IEnumerable<RecipeIngredient> ingredients, string output = "item.medicine", int outputQuantity = 1) =>
            new RecipeDefinition(new RecipeId("recipe.test"), "Medicine", ingredients, new ItemId(output), outputQuantity);

        private static RecipeIngredient[] Ingredients() => new[]
        {
            new RecipeIngredient(new ItemId("item.basil"), 2), new RecipeIngredient(new ItemId("item.mint"), 3)
        };

        private static ItemCatalog Items() => new ItemCatalog(new[]
        {
            Item("item.basil", ItemCategory.HarvestedPlant), Item("item.mint", ItemCategory.HarvestedPlant), Item("item.medicine", ItemCategory.Medicine)
        });

        private static ItemDefinition Item(string id, ItemCategory category) =>
            new ItemDefinition(new ItemId(id), id, category, new ItemRepresentationKey("representation.test"), new ItemStackRules(10, false));

        private static void Error(RecipeDataValidationError expected, TestDelegate action) =>
            Assert.That(Assert.Throws<RecipeDataValidationException>(action).Error, Is.EqualTo(expected));
    }
}
