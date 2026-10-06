using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Data.Items;

namespace VrGame.Tests.EditMode.Items
{
    public sealed class ItemCatalogTests
    {
        [TestCase(ItemCategory.Seed)]
        [TestCase(ItemCategory.HarvestedPlant)]
        [TestCase(ItemCategory.Medicine)]
        [TestCase(ItemCategory.Fertilizer)]
        public void Definition_SupportsEveryMvpCategory(ItemCategory category)
        {
            var definition = CreateDefinition("item.test.category", category);

            Assert.That(definition.Category, Is.EqualTo(category));
        }

        [Test]
        public void Catalog_UsesStableIdAndDeterministicOrder()
        {
            var later = CreateDefinition("item.seed.mint", ItemCategory.Seed, "Одинаковое имя");
            var earlier = CreateDefinition("item.fertilizer.basic", ItemCategory.Fertilizer, "Одинаковое имя");

            var catalog = new ItemCatalog(new[] { later, earlier });

            Assert.That(catalog.Definitions.Select(item => item.Id.Value), Is.EqualTo(new[]
            {
                "item.fertilizer.basic",
                "item.seed.mint"
            }));
            Assert.That(catalog.GetRequired(later.Id), Is.SameAs(later));
            Assert.That(catalog.GetRequired(earlier.Id), Is.SameAs(earlier));
            Assert.That(ItemId.TryParse("ITEM.SEED.MINT", out var wrongCaseId), Is.False);
            Assert.That(wrongCaseId, Is.EqualTo(default(ItemId)));
        }

        [Test]
        public void Catalog_ReportsUnknownIdWithoutFallbackToDisplayName()
        {
            var definition = CreateDefinition("item.seed.basil", ItemCategory.Seed, "item.seed.unknown");
            var catalog = new ItemCatalog(new[] { definition });
            var unknown = new ItemId("item.seed.unknown");

            Assert.That(catalog.TryGet(unknown, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(unknown));
        }

        [Test]
        public void Catalog_RejectsDuplicateIds()
        {
            var first = CreateDefinition("item.seed.basil", ItemCategory.Seed);
            var duplicate = CreateDefinition("item.seed.basil", ItemCategory.HarvestedPlant);

            var error = Assert.Throws<ItemDataValidationException>(() => new ItemCatalog(new[] { first, duplicate }));

            Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.DuplicateItemId));
        }

        [Test]
        public void Catalog_RejectsMissingDefinition()
        {
            var error = Assert.Throws<ItemDataValidationException>(() =>
                new ItemCatalog(new ItemDefinition[] { null }));

            Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.MissingDefinition));
        }

        [Test]
        public void Catalog_CopiesInputCollection()
        {
            var definitions = new List<ItemDefinition> { CreateDefinition("item.seed.basil", ItemCategory.Seed) };
            var catalog = new ItemCatalog(definitions);

            definitions.Clear();

            Assert.That(catalog.Definitions, Has.Count.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("seed.basil")]
        [TestCase("item.Seed.basil")]
        [TestCase("item.seed.")]
        public void ItemId_RejectsInvalidValues(string value)
        {
            var error = Assert.Throws<ItemDataValidationException>(() => new ItemId(value));

            Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.InvalidItemId));
        }

        [Test]
        public void Definition_RejectsInvalidFieldsAndStackRules()
        {
            var id = new ItemId("item.seed.basil");
            var representation = new ItemRepresentationKey("representation.seed.basil");

            Assert.That(
                Assert.Throws<ItemDataValidationException>(() =>
                    new ItemDefinition(id, " ", ItemCategory.Seed, representation, new ItemStackRules(1, false))).Error,
                Is.EqualTo(ItemDataValidationError.MissingDisplayName));
            Assert.That(
                Assert.Throws<ItemDataValidationException>(() => new ItemStackRules(0, false)).Error,
                Is.EqualTo(ItemDataValidationError.InvalidStackRules));
            Assert.That(
                Assert.Throws<ItemDataValidationException>(() => new ItemStackRules(1, true)).Error,
                Is.EqualTo(ItemDataValidationError.InvalidStackRules));
            Assert.That(
                Assert.Throws<ItemDataValidationException>(() =>
                    new ItemDefinition(default, "Базилик", ItemCategory.Seed, representation, new ItemStackRules(1, false))).Error,
                Is.EqualTo(ItemDataValidationError.InvalidItemId));
            Assert.That(
                Assert.Throws<ItemDataValidationException>(() =>
                    new ItemDefinition(id, "Базилик", default, representation, new ItemStackRules(1, false))).Error,
                Is.EqualTo(ItemDataValidationError.InvalidCategory));
            Assert.That(
                Assert.Throws<ItemDataValidationException>(() =>
                    new ItemDefinition(id, "Базилик", ItemCategory.Seed, default, new ItemStackRules(1, false))).Error,
                Is.EqualTo(ItemDataValidationError.MissingRepresentation));
        }

        private static ItemDefinition CreateDefinition(
            string id,
            ItemCategory category,
            string displayName = "Тестовый предмет") =>
            new ItemDefinition(
                new ItemId(id),
                displayName,
                category,
                new ItemRepresentationKey("representation.test.item"),
                new ItemStackRules(10, false));
    }
}
