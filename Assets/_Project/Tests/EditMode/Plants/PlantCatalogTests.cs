using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantCatalogTests
    {
        [Test]
        public void Catalog_SupportsTwoPlantsAndDeterministicLookup()
        {
            var items = CreateItems();
            var mint = CreatePlant("plant.mint", 45f, "item.seed.mint", "item.harvest.mint");
            var basil = CreatePlant("plant.basil", 90f, "item.seed.basil", "item.harvest.basil");

            var catalog = new PlantCatalog(items, new[] { mint, basil });

            Assert.That(catalog.Definitions.Select(x => x.Id.Value), Is.EqualTo(new[] { "plant.basil", "plant.mint" }));
            Assert.That(catalog.GetRequired(mint.Id), Is.SameAs(mint));
            Assert.That(catalog.GetRequired(basil.Id).GrowthDuration.Seconds, Is.EqualTo(90f));
            Assert.That(catalog.TryGet(new PlantTypeId("plant.unknown"), out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(new PlantTypeId("plant.unknown")));
        }

        [TestCase("")]
        [TestCase("crop.basil")]
        [TestCase("plant.Basil")]
        [TestCase("plant.basil.")]
        public void PlantTypeId_RejectsInvalidValues(string value)
        {
            Assert.That(Assert.Throws<PlantDataValidationException>(() => new PlantTypeId(value)).Error,
                Is.EqualTo(PlantDataValidationError.InvalidPlantTypeId));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GrowthDuration_RejectsInvalidValues(float seconds)
        {
            Assert.That(Assert.Throws<PlantDataValidationException>(() => new GrowthDuration(seconds)).Error,
                Is.EqualTo(PlantDataValidationError.InvalidGrowthDuration));
        }

        [Test]
        public void Catalog_RejectsNullAndDuplicateDefinitions()
        {
            var items = CreateItems();
            var plant = CreatePlant("plant.basil", 10f, "item.seed.basil", "item.harvest.basil");

            Assert.That(Assert.Throws<PlantDataValidationException>(() => new PlantCatalog(items, null)).Error,
                Is.EqualTo(PlantDataValidationError.MissingDefinition));
            Assert.That(Assert.Throws<PlantDataValidationException>(() => new PlantCatalog(items, new PlantDefinition[] { null })).Error,
                Is.EqualTo(PlantDataValidationError.MissingDefinition));
            Assert.That(Assert.Throws<PlantDataValidationException>(() => new PlantCatalog(items, new[] { plant, plant })).Error,
                Is.EqualTo(PlantDataValidationError.DuplicatePlantTypeId));
        }

        [Test]
        public void Catalog_RejectsUnknownAndWrongCategoryItems()
        {
            var items = CreateItems();
            AssertError(PlantDataValidationError.UnknownSeedItem,
                CreatePlant("plant.test", 10f, "item.seed.unknown", "item.harvest.basil"), items);
            AssertError(PlantDataValidationError.UnknownHarvestItem,
                CreatePlant("plant.test", 10f, "item.seed.basil", "item.harvest.unknown"), items);
            AssertError(PlantDataValidationError.InvalidSeedItemCategory,
                CreatePlant("plant.test", 10f, "item.harvest.basil", "item.harvest.mint"), items);
            AssertError(PlantDataValidationError.InvalidHarvestItemCategory,
                CreatePlant("plant.test", 10f, "item.seed.basil", "item.seed.mint"), items);
        }

        [Test]
        public void Definition_RejectsMissingAndUnorderedStages()
        {
            AssertDefinitionError(PlantDataValidationError.MissingStages);
            AssertDefinitionError(PlantDataValidationError.InvalidStageThreshold, Stage(.1f));
            AssertDefinitionError(PlantDataValidationError.UnorderedStages, Stage(0f), Stage(.5f), Stage(.5f));
            AssertDefinitionError(PlantDataValidationError.UnorderedStages, Stage(0f), Stage(.8f), Stage(.4f));
            AssertDefinitionError(PlantDataValidationError.MissingRepresentation, Stage(0f), null);
            Assert.That(
                Assert.Throws<PlantDataValidationException>(() =>
                    new PlantDefinition(new PlantTypeId("plant.test"), new GrowthDuration(10f), default,
                        new ItemId("item.harvest.basil"), new[] { Stage(0f) })).Error,
                Is.EqualTo(PlantDataValidationError.MissingSeedItem));
        }

        [TestCase(-.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Stage_RejectsInvalidThreshold(float threshold)
        {
            Assert.That(
                Assert.Throws<PlantDataValidationException>(() =>
                    new PlantStageDefinition(threshold, new PlantRepresentationKey("representation.plant.test"))).Error,
                Is.EqualTo(PlantDataValidationError.InvalidStageThreshold));
        }

        [Test]
        public void Stage_RejectsMissingRepresentation()
        {
            Assert.That(
                Assert.Throws<PlantDataValidationException>(() => new PlantStageDefinition(0f, default)).Error,
                Is.EqualTo(PlantDataValidationError.MissingRepresentation));
        }

        [Test]
        public void Definition_DefensivelyCopiesStagesAndCatalogCopiesDefinitions()
        {
            var stages = new List<PlantStageDefinition> { Stage(0f), Stage(.5f) };
            var definition = CreatePlant("plant.basil", 10f, "item.seed.basil", "item.harvest.basil", stages);
            var definitions = new List<PlantDefinition> { definition };
            var catalog = new PlantCatalog(CreateItems(), definitions);

            stages.Clear();
            definitions.Clear();

            Assert.That(definition.Stages, Has.Count.EqualTo(2));
            Assert.That(catalog.Definitions, Has.Count.EqualTo(1));
        }

        private static void AssertError(PlantDataValidationError expected, PlantDefinition definition, ItemCatalog items) =>
            Assert.That(Assert.Throws<PlantDataValidationException>(() => new PlantCatalog(items, new[] { definition })).Error, Is.EqualTo(expected));

        private static void AssertDefinitionError(PlantDataValidationError expected, params PlantStageDefinition[] stages)
        {
            Assert.That(
                Assert.Throws<PlantDataValidationException>(() =>
                    CreatePlant("plant.test", 10f, "item.seed.basil", "item.harvest.basil", stages)).Error,
                Is.EqualTo(expected));
        }

        private static PlantDefinition CreatePlant(string id, float duration, string seed, string harvest, IEnumerable<PlantStageDefinition> stages = null) =>
            new PlantDefinition(new PlantTypeId(id), new GrowthDuration(duration), new ItemId(seed), new ItemId(harvest), stages ?? new[] { Stage(0f), Stage(.7f) });

        private static PlantStageDefinition Stage(float threshold) =>
            new PlantStageDefinition(threshold, new PlantRepresentationKey("representation.plant.test"));

        private static ItemCatalog CreateItems() => new ItemCatalog(new[]
        {
            Item("item.seed.basil", ItemCategory.Seed), Item("item.harvest.basil", ItemCategory.HarvestedPlant),
            Item("item.seed.mint", ItemCategory.Seed), Item("item.harvest.mint", ItemCategory.HarvestedPlant)
        });

        private static ItemDefinition Item(string id, ItemCategory category) => new ItemDefinition(
            new ItemId(id), id, category, new ItemRepresentationKey("representation.test.item"), new ItemStackRules(10, false));
    }
}
