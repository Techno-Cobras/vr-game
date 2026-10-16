using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.DataAssets;
using VrGame.DataAssets.Crafting;

namespace VrGame.Tests.EditMode.Crafting
{
    public sealed class RecipeDataAssetsTests
    {
        private const string Samples = "Assets/_Project/Crafting/Samples/";
        private readonly List<Object> objects = new List<Object>();
        private RuntimeItemCatalog items;
        private RecipeDefinitionAsset recipe;
        private RecipeCatalogAsset catalog;

        [SetUp]
        public void SetUp()
        {
            items = Load<ItemCatalogAsset>("ItemCatalog.asset").Build();
            recipe = Track(Object.Instantiate(Load<RecipeDefinitionAsset>("HerbalBlend.asset")));
            catalog = Track(ScriptableObject.CreateInstance<RecipeCatalogAsset>());
            SetDefinitions(recipe);
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = objects.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(objects[index]);
            objects.Clear();
        }

        [Test]
        public void SampleAssets_BuildTwoRecipesWithCommonMedicineItems()
        {
            var result = Load<RecipeCatalogAsset>("RecipeCatalog.asset").Build(items);

            Assert.That(result.Definitions, Has.Count.EqualTo(2));
            foreach (var definition in result.Definitions)
            {
                Assert.That(items.Items.GetRequired(definition.OutputItemId).Category, Is.EqualTo(ItemCategory.Medicine));
                foreach (var ingredient in definition.Ingredients)
                    Assert.That(items.Items.TryGet(ingredient.ItemId, out _), Is.True);
            }
            Assert.That(result.Definitions[0].Id.CompareTo(result.Definitions[1].Id), Is.LessThan(0));
        }

        [Test]
        public void SerializedEdits_ChangeQuantitiesWithoutChangingBuiltSnapshot()
        {
            var original = catalog.Build(items).Definitions[0];
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("ingredients").GetArrayElementAtIndex(0).FindPropertyRelative("quantity").intValue = 7;
            serialized.FindProperty("outputQuantity").intValue = 4;
            serialized.FindProperty("displayName").stringValue = "Edited medicine";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var edited = catalog.Build(items).GetRequired(original.Id);

            Assert.That(edited.Ingredients[0].Quantity, Is.EqualTo(7));
            Assert.That(edited.OutputQuantity, Is.EqualTo(4));
            Assert.That(edited.DisplayName, Is.EqualTo("Edited medicine"));
            Assert.That(original.Ingredients[0].Quantity, Is.Not.EqualTo(7));
            Assert.That(original.OutputQuantity, Is.Not.EqualTo(4));
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void SerializedQuantities_AreValidatedAtBuild(int quantity)
        {
            SetInteger(recipe, "outputQuantity", quantity);
            Error(RecipeDataValidationError.InvalidOutputQuantity, () => catalog.Build(items));
            SetInteger(recipe, "outputQuantity", 1);
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("ingredients").GetArrayElementAtIndex(0).FindPropertyRelative("quantity").intValue = quantity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.InvalidIngredientQuantity, () => catalog.Build(items));
        }

        [Test]
        public void Catalog_ValidatesNullDataAndDuplicateRecipes()
        {
            Error(RecipeDataValidationError.MissingItemCatalog, () => catalog.Build(null));
            SetDefinitions((RecipeDefinitionAsset)null);
            Error(RecipeDataValidationError.MissingDefinition, () => catalog.Build(items));
            SetDefinitions(recipe, recipe);
            Error(RecipeDataValidationError.DuplicateRecipeId, () => catalog.Build(items));
            typeof(RecipeCatalogAsset).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, null);
            Error(RecipeDataValidationError.MissingDefinition, () => catalog.Build(items));
        }

        [Test]
        public void Recipe_ValidatesMissingReferencesAndIngredients()
        {
            var serialized = new SerializedObject(recipe);
            var output = serialized.FindProperty("outputItem").objectReferenceValue;
            serialized.FindProperty("outputItem").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.InvalidOutputItem, () => catalog.Build(items));
            serialized.FindProperty("outputItem").objectReferenceValue = output;
            serialized.FindProperty("ingredients").GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.MissingIngredient, () => catalog.Build(items));
            serialized.FindProperty("ingredients").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.MissingIngredients, () => catalog.Build(items));
            typeof(RecipeDefinitionAsset).GetField("ingredients", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recipe, null);
            Error(RecipeDataValidationError.MissingIngredients, () => catalog.Build(items));
        }

        [Test]
        public void Catalog_UsesAuthoritativeItemMembershipAndCategory()
        {
            var external = Track(ScriptableObject.CreateInstance<ItemDefinitionAsset>());
            var serializedItem = new SerializedObject(external);
            serializedItem.FindProperty("id").stringValue = "item.external";
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            var serialized = new SerializedObject(recipe);
            var ingredient = serialized.FindProperty("ingredients").GetArrayElementAtIndex(0).FindPropertyRelative("item");
            var knownIngredient = ingredient.objectReferenceValue;
            ingredient.objectReferenceValue = external;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.UnknownIngredientItem, () => catalog.Build(items));
            ingredient.objectReferenceValue = knownIngredient;
            serialized.FindProperty("outputItem").objectReferenceValue = external;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.UnknownOutputItem, () => catalog.Build(items));

            // A different asset claiming a known ingredient ID cannot override the catalog category.
            serializedItem.FindProperty("id").stringValue = ((ItemDefinitionAsset)knownIngredient).GetId().Value;
            serializedItem.FindProperty("category").intValue = (int)ItemCategory.Medicine;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            Error(RecipeDataValidationError.InvalidOutputItemCategory, () => catalog.Build(items));

            serializedItem.FindProperty("id").stringValue = Load<ItemDefinitionAsset>("HerbalMedicine.asset").GetId().Value;
            serializedItem.FindProperty("category").intValue = (int)ItemCategory.HarvestedPlant;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            var result = catalog.Build(items).Definitions[0];
            Assert.That(items.Items.GetRequired(result.OutputItemId).Category, Is.EqualTo(ItemCategory.Medicine));
        }

        private void SetDefinitions(params RecipeDefinitionAsset[] definitions)
        {
            var serialized = new SerializedObject(catalog);
            var property = serialized.FindProperty("definitions");
            property.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        private static T Load<T>(string fileName) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Samples + fileName);
            Assert.That(asset, Is.Not.Null, $"Sample asset '{fileName}' отсутствует.");
            return asset;
        }

        private static void Error(RecipeDataValidationError expected, TestDelegate action) =>
            Assert.That(Assert.Throws<RecipeDataValidationException>(action).Error, Is.EqualTo(expected));
    }
}
