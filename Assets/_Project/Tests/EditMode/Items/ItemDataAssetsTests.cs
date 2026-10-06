using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.DataAssets;

namespace VrGame.Tests.EditMode.Items
{
    public sealed class ItemDataAssetsTests
    {
        [Test]
        public void CatalogAsset_BuildsDefinitionsAndRepresentationResolver()
        {
            var prefab = new GameObject("Basil Representation");
            var representation = CreateRepresentation("representation.seed.basil", prefab);
            var seed = CreateDefinition("item.seed.basil", "Базилик", ItemCategory.Seed, representation);
            var catalogAsset = CreateCatalog(seed);

            try
            {
                var runtimeCatalog = catalogAsset.Build();
                var definition = runtimeCatalog.Items.GetRequired(new ItemId("item.seed.basil"));

                Assert.That(definition.DisplayName, Is.EqualTo("Базилик"));
                Assert.That(runtimeCatalog.GetRequiredRepresentation(definition.Representation), Is.SameAs(prefab));
            }
            finally
            {
                Object.DestroyImmediate(catalogAsset);
                Object.DestroyImmediate(seed);
                Object.DestroyImmediate(representation);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CatalogAsset_RejectsMissingDefinitionReference()
        {
            var catalogAsset = CreateCatalog((ItemDefinitionAsset)null);

            try
            {
                var error = Assert.Throws<ItemDataValidationException>(() => catalogAsset.Build());
                Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.MissingDefinition));
            }
            finally
            {
                Object.DestroyImmediate(catalogAsset);
            }
        }

        [Test]
        public void DefinitionAsset_RejectsMissingRepresentationReference()
        {
            var definition = CreateDefinition("item.seed.basil", "Базилик", ItemCategory.Seed, null);

            try
            {
                var error = Assert.Throws<ItemDataValidationException>(() => definition.ToDefinition());
                Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.MissingRepresentation));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RepresentationAsset_RejectsMissingPrefab()
        {
            var representation = CreateRepresentation("representation.seed.basil", null);

            try
            {
                var error = Assert.Throws<ItemDataValidationException>(() => representation.GetKey());
                Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.MissingRepresentation));
            }
            finally
            {
                Object.DestroyImmediate(representation);
            }
        }

        [Test]
        public void RepresentationAsset_RejectsInvalidKey()
        {
            var prefab = new GameObject("Invalid Representation");
            var representation = CreateRepresentation("Basil Prefab", prefab);

            try
            {
                var error = Assert.Throws<ItemDataValidationException>(() => representation.GetKey());
                Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.InvalidRepresentationKey));
            }
            finally
            {
                Object.DestroyImmediate(representation);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CatalogAsset_RejectsSameRepresentationKeyWithDifferentPrefabs()
        {
            var firstPrefab = new GameObject("First");
            var secondPrefab = new GameObject("Second");
            var firstRepresentation = CreateRepresentation("representation.shared.item", firstPrefab);
            var secondRepresentation = CreateRepresentation("representation.shared.item", secondPrefab);
            var first = CreateDefinition("item.seed.basil", "Базилик", ItemCategory.Seed, firstRepresentation);
            var second = CreateDefinition("item.seed.mint", "Мята", ItemCategory.Seed, secondRepresentation);
            var catalogAsset = CreateCatalog(first, second);

            try
            {
                var error = Assert.Throws<ItemDataValidationException>(() => catalogAsset.Build());
                Assert.That(error.Error, Is.EqualTo(ItemDataValidationError.ConflictingRepresentation));
            }
            finally
            {
                Object.DestroyImmediate(catalogAsset);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(firstRepresentation);
                Object.DestroyImmediate(secondRepresentation);
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        private static ItemRepresentationAsset CreateRepresentation(string key, GameObject prefab)
        {
            var asset = ScriptableObject.CreateInstance<ItemRepresentationAsset>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("key").stringValue = key;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static ItemDefinitionAsset CreateDefinition(
            string id,
            string displayName,
            ItemCategory category,
            ItemRepresentationAsset representation)
        {
            var asset = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category - 1;
            serialized.FindProperty("representation").objectReferenceValue = representation;
            serialized.FindProperty("maxStackSize").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static ItemCatalogAsset CreateCatalog(params ItemDefinitionAsset[] definitions)
        {
            var asset = ScriptableObject.CreateInstance<ItemCatalogAsset>();
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty("definitions");
            property.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }
    }
}
