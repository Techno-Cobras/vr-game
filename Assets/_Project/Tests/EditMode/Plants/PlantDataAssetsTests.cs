using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.DataAssets.Plants;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantDataAssetsTests
    {
        [Test]
        public void CatalogAsset_BuildsTwoDefinitionsAndResolvesPrefabs()
        {
            var fixture = new Fixture();
            try
            {
                var runtimeItems = fixture.CreateRuntimeItems();
                var firstPrefab = fixture.CreateObject("Basil");
                var secondPrefab = fixture.CreateObject("Mint");
                var firstRepresentation = fixture.Representation("representation.plant.basil", firstPrefab);
                var secondRepresentation = fixture.Representation("representation.plant.mint", secondPrefab);
                var basil = fixture.Plant("plant.basil", 90f, fixture.BasilSeed, fixture.BasilHarvest, Stage(0f, firstRepresentation));
                var mint = fixture.Plant("plant.mint", 45f, fixture.MintSeed, fixture.MintHarvest, Stage(0f, secondRepresentation));
                var catalogAsset = fixture.Catalog(mint, basil);

                var runtime = catalogAsset.Build(runtimeItems);

                Assert.That(runtime.Plants.Definitions[0].Id.Value, Is.EqualTo("plant.basil"));
                Assert.That(runtime.Plants.GetRequired(new PlantTypeId("plant.mint")).GrowthDuration.Seconds, Is.EqualTo(45f));
                Assert.That(runtime.GetRequiredRepresentation(new PlantRepresentationKey("representation.plant.basil")), Is.SameAs(firstPrefab));
                Assert.That(runtime.TryGetRepresentation(new PlantRepresentationKey("representation.plant.unknown"), out _), Is.False);
            }
            finally { fixture.Dispose(); }
        }

        [Test]
        public void CatalogAsset_RejectsNullsMissingPrefabAndUnknownMembership()
        {
            var fixture = new Fixture();
            try
            {
                var runtimeItems = fixture.CreateRuntimeItems();
                AssertError(PlantDataValidationError.MissingItemCatalog, () => fixture.Catalog().Build(null));
                AssertError(PlantDataValidationError.MissingDefinition, () => fixture.Catalog((PlantDefinitionAsset)null).Build(runtimeItems));

                var noPrefab = fixture.Representation("representation.plant.none", null);
                var definition = fixture.Plant("plant.none", 10f, fixture.BasilSeed, fixture.BasilHarvest, Stage(0f, noPrefab));
                AssertError(PlantDataValidationError.MissingRepresentation, () => fixture.Catalog(definition).Build(runtimeItems));

                var externalSeed = fixture.Item("item.seed.external", ItemCategory.Seed);
                var prefab = fixture.CreateObject("External");
                definition = fixture.Plant("plant.external", 10f, externalSeed, fixture.BasilHarvest,
                    Stage(0f, fixture.Representation("representation.plant.external", prefab)));
                AssertError(PlantDataValidationError.UnknownSeedItem, () => fixture.Catalog(definition).Build(runtimeItems));
            }
            finally { fixture.Dispose(); }
        }

        [Test]
        public void CatalogAsset_RejectsRepresentationKeyConflict()
        {
            var fixture = new Fixture();
            try
            {
                var runtimeItems = fixture.CreateRuntimeItems();
                var first = fixture.Representation("representation.plant.shared", fixture.CreateObject("First"));
                var second = fixture.Representation("representation.plant.shared", fixture.CreateObject("Second"));
                var basil = fixture.Plant("plant.basil", 10f, fixture.BasilSeed, fixture.BasilHarvest, Stage(0f, first));
                var mint = fixture.Plant("plant.mint", 10f, fixture.MintSeed, fixture.MintHarvest, Stage(0f, second));

                AssertError(PlantDataValidationError.ConflictingRepresentation, () => fixture.Catalog(basil, mint).Build(runtimeItems));
            }
            finally { fixture.Dispose(); }
        }

        private static void AssertError(PlantDataValidationError expected, TestDelegate action) =>
            Assert.That(Assert.Throws<PlantDataValidationException>(action).Error, Is.EqualTo(expected));

        private static StageData Stage(float threshold, PlantRepresentationAsset representation) => new StageData(threshold, representation);

        private readonly struct StageData
        {
            public StageData(float threshold, PlantRepresentationAsset representation) { Threshold = threshold; Representation = representation; }
            public float Threshold { get; }
            public PlantRepresentationAsset Representation { get; }
        }

        private sealed class Fixture
        {
            private readonly System.Collections.Generic.List<Object> objects = new System.Collections.Generic.List<Object>();
            public ItemDefinitionAsset BasilSeed { get; private set; }
            public ItemDefinitionAsset BasilHarvest { get; private set; }
            public ItemDefinitionAsset MintSeed { get; private set; }
            public ItemDefinitionAsset MintHarvest { get; private set; }

            public RuntimeItemCatalog CreateRuntimeItems()
            {
                BasilSeed = Item("item.seed.basil", ItemCategory.Seed);
                BasilHarvest = Item("item.harvest.basil", ItemCategory.HarvestedPlant);
                MintSeed = Item("item.seed.mint", ItemCategory.Seed);
                MintHarvest = Item("item.harvest.mint", ItemCategory.HarvestedPlant);
                var catalog = Track(ScriptableObject.CreateInstance<ItemCatalogAsset>());
                SetArray(catalog, "definitions", BasilSeed, BasilHarvest, MintSeed, MintHarvest);
                return catalog.Build();
            }

            public ItemDefinitionAsset Item(string id, ItemCategory category)
            {
                var prefab = CreateObject(id);
                var representation = Track(ScriptableObject.CreateInstance<ItemRepresentationAsset>());
                Set(representation, "key", "representation." + id);
                Set(representation, "prefab", prefab);
                var asset = Track(ScriptableObject.CreateInstance<ItemDefinitionAsset>());
                Set(asset, "id", id); Set(asset, "displayName", id); Set(asset, "category", category);
                Set(asset, "representation", representation); Set(asset, "maxStackSize", 10);
                return asset;
            }

            public PlantRepresentationAsset Representation(string key, GameObject prefab)
            {
                var asset = Track(ScriptableObject.CreateInstance<PlantRepresentationAsset>());
                Set(asset, "key", key); Set(asset, "prefab", prefab); return asset;
            }

            public PlantDefinitionAsset Plant(string id, float duration, ItemDefinitionAsset seed, ItemDefinitionAsset harvest, params StageData[] stages)
            {
                var asset = Track(ScriptableObject.CreateInstance<PlantDefinitionAsset>());
                Set(asset, "id", id); Set(asset, "growthDurationSeconds", duration); Set(asset, "seedItem", seed); Set(asset, "harvestItem", harvest);
                var serialized = new SerializedObject(asset); var property = serialized.FindProperty("stages"); property.arraySize = stages.Length;
                for (var i = 0; i < stages.Length; i++) { var element = property.GetArrayElementAtIndex(i); element.FindPropertyRelative("startsAtNormalized").floatValue = stages[i].Threshold; element.FindPropertyRelative("representation").objectReferenceValue = stages[i].Representation; }
                serialized.ApplyModifiedPropertiesWithoutUndo(); return asset;
            }

            public PlantCatalogAsset Catalog(params PlantDefinitionAsset[] definitions)
            {
                var asset = Track(ScriptableObject.CreateInstance<PlantCatalogAsset>()); SetArray(asset, "definitions", definitions); return asset;
            }

            public GameObject CreateObject(string name) => Track(new GameObject(name));
            public void Dispose() { for (var i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); }
            private T Track<T>(T value) where T : Object { objects.Add(value); return value; }
            private static void Set(Object target, string name, object value) { var serialized = new SerializedObject(target); var property = serialized.FindProperty(name); if (value is string text) property.stringValue = text; else if (value is int number) property.intValue = number; else if (value is float real) property.floatValue = real; else if (value is System.Enum enumeration) property.enumValueIndex = System.Convert.ToInt32(enumeration) - 1; else property.objectReferenceValue = (Object)value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            private static void SetArray(Object target, string name, params Object[] values) { var serialized = new SerializedObject(target); var property = serialized.FindProperty(name); property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
