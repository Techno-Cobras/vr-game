using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.Data.Orders;
using VrGame.DataAssets;
using VrGame.DataAssets.Orders;

namespace VrGame.Tests.EditMode.Orders
{
    public sealed class OrderDataTests
    {
        private static readonly ItemId MedicineA = new ItemId("item.medicine.a");
        private static readonly ItemId MedicineB = new ItemId("item.medicine.b");
        private static readonly ItemId Plant = new ItemId("item.plant.a");

        [TestCase("order-template.valid", true)]
        [TestCase("order-template.a-b", true)]
        [TestCase("order.valid", false)]
        [TestCase("order-template.Invalid", false)]
        [TestCase("", false)]
        public void TemplateId_ValidatesStablePrefix(string value, bool valid)
        {
            Assert.That(OrderTemplateId.TryParse(value, out _), Is.EqualTo(valid));
        }

        [TestCase(0, 10, OrderDataValidationError.InvalidQuantity)]
        [TestCase(-1, 10, OrderDataValidationError.InvalidQuantity)]
        [TestCase(1, 0, OrderDataValidationError.InvalidReward)]
        [TestCase(1, -1, OrderDataValidationError.InvalidReward)]
        public void Definition_RejectsInvalidQuantityAndReward(
            int quantity,
            int reward,
            OrderDataValidationError expected)
        {
            Error(expected, () => new OrderTemplateDefinition(
                new OrderTemplateId("order-template.invalid-values"),
                "Invalid",
                MedicineA,
                quantity,
                reward));
        }

        [Test]
        public void Catalog_SortsAndValidatesMembershipCategoryAndDuplicates()
        {
            var items = Items();
            var second = Template("order-template.z", MedicineB, 2, 60);
            var first = Template("order-template.a", MedicineA, 1, 25);
            var catalog = new OrderTemplateCatalog(items, new[] { second, first });

            Assert.That(catalog.Definitions[0].Id, Is.EqualTo(first.Id));
            Assert.That(catalog.Definitions[1].Id, Is.EqualTo(second.Id));
            Error(OrderDataValidationError.DuplicateTemplateId,
                () => new OrderTemplateCatalog(items, new[] { first, first }));
            Error(OrderDataValidationError.UnknownMedicineItem,
                () => new OrderTemplateCatalog(items, new[]
                {
                    Template("order-template.unknown", new ItemId("item.medicine.unknown"), 1, 1)
                }));
            Error(OrderDataValidationError.InvalidMedicineCategory,
                () => new OrderTemplateCatalog(items, new[]
                {
                    Template("order-template.plant", Plant, 1, 1)
                }));
            Error(OrderDataValidationError.MissingDefinition,
                () => new OrderTemplateCatalog(items, new OrderTemplateDefinition[0]));
        }

        [Test]
        public void ProductionAssets_BuildTwoExplicitMedicineTemplates()
        {
            var itemAsset = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(
                "Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset");
            var catalogAsset = AssetDatabase.LoadAssetAtPath<OrderTemplateCatalogAsset>(
                "Assets/_Project/Orders/Data/OrderTemplateCatalog.asset");
            Assert.That(itemAsset, Is.Not.Null);
            Assert.That(catalogAsset, Is.Not.Null);

            var catalog = catalogAsset.Build(itemAsset.Build());
            Assert.That(catalog.Definitions, Has.Count.EqualTo(2));
            Assert.That(catalog.Definitions[0].Id.Value, Is.EqualTo("order-template.basil-tonic"));
            Assert.That(catalog.Definitions[0].Quantity, Is.EqualTo(1));
            Assert.That(catalog.Definitions[0].Reward, Is.EqualTo(25));
            Assert.That(catalog.Definitions[1].Id.Value, Is.EqualTo("order-template.herbal-blend"));
            Assert.That(catalog.Definitions[1].Quantity, Is.EqualTo(2));
            Assert.That(catalog.Definitions[1].Reward, Is.EqualTo(60));
            foreach (var definition in catalog.Definitions)
                Assert.That(itemAsset.Build().Items.GetRequired(definition.MedicineItemId).Category,
                    Is.EqualTo(ItemCategory.Medicine));
        }

        [Test]
        public void DataAssets_FailFastForMissingReferencesAndInvalidSerializedValues()
        {
            var itemAsset = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(
                "Assets/_Project/SeedStorage/Data/SeedStorageItemCatalog.asset");
            var source = AssetDatabase.LoadAssetAtPath<OrderTemplateDefinitionAsset>(
                "Assets/_Project/Orders/Data/BasilTonicOrder.asset");
            var definition = Object.Instantiate(source);
            var catalog = ScriptableObject.CreateInstance<OrderTemplateCatalogAsset>();
            try
            {
                var catalogSerialized = new SerializedObject(catalog);
                var definitions = catalogSerialized.FindProperty("definitions");
                definitions.arraySize = 1;
                definitions.GetArrayElementAtIndex(0).objectReferenceValue = definition;
                catalogSerialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(catalog.Build(itemAsset.Build()).Definitions, Has.Count.EqualTo(1));
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("medicineItem").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Error(OrderDataValidationError.InvalidMedicineItem, () => catalog.Build(itemAsset.Build()));

                Object.DestroyImmediate(definition);
                definition = Object.Instantiate(source);
                definitions.GetArrayElementAtIndex(0).objectReferenceValue = definition;
                catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
                serialized = new SerializedObject(definition);
                serialized.FindProperty("reward").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Error(OrderDataValidationError.InvalidReward, () => catalog.Build(itemAsset.Build()));

                definitions.GetArrayElementAtIndex(0).objectReferenceValue = null;
                catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
                Error(OrderDataValidationError.MissingDefinition, () => catalog.Build(itemAsset.Build()));
                Error(OrderDataValidationError.MissingItemCatalog, () => catalog.Build(null));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
            }
        }

        private static ItemCatalog Items() => new ItemCatalog(new[]
        {
            Item(MedicineA, ItemCategory.Medicine),
            Item(MedicineB, ItemCategory.Medicine),
            Item(Plant, ItemCategory.HarvestedPlant)
        });

        private static ItemDefinition Item(ItemId id, ItemCategory category) => new ItemDefinition(
            id,
            id.Value,
            category,
            new ItemRepresentationKey("representation." + id.Value),
            new ItemStackRules(16, false));

        private static OrderTemplateDefinition Template(
            string id,
            ItemId medicine,
            int quantity,
            int reward) => new OrderTemplateDefinition(
            new OrderTemplateId(id), id, medicine, quantity, reward);

        private static void Error(OrderDataValidationError expected, TestDelegate action) =>
            Assert.That(Assert.Throws<OrderDataValidationException>(action).Error, Is.EqualTo(expected));
    }
}
