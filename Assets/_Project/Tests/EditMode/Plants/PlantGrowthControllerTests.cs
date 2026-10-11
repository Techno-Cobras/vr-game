using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Plants;
using VrGame.VRInteraction.Plants;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantGrowthControllerTests
    {
        [Test]
        public void Controller_ThrottlesCommitsSwitchesPrecreatedStagesAndDropsPausedTime()
        {
            var fixture = new Fixture();
            try
            {
                fixture.Slot.Plant(fixture.PlantId, fixture.Context("посадка"));
                fixture.Controller.Configure(fixture.Slot, fixture.Catalog, fixture.Time);
                var childCount = fixture.VisualRoot.childCount;

                fixture.Time.Advance(.5d);
                fixture.Controller.ProcessScheduledUpdate();
                var firstVersion = fixture.Slot.Version;

                Assert.That(fixture.Controller.ActiveRepresentation.Value.Value,
                    Is.EqualTo("representation.plant.controller.young"));
                Assert.That(fixture.Controller.PrecreatedStageCount, Is.EqualTo(3));
                Assert.That(fixture.VisualRoot.childCount, Is.EqualTo(childCount));
                AssertStageAboveSurface(fixture.Controller);

                fixture.Time.Advance(.05d);
                fixture.Controller.ProcessScheduledUpdate();
                Assert.That(fixture.Slot.Version, Is.EqualTo(firstVersion));

                fixture.Controller.SetApplicationPaused(true);
                Assert.That(fixture.Slot.Version, Is.EqualTo(firstVersion + 1));
                Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(.55f).Within(.0001f));
                fixture.Time.Advance(100d);
                fixture.Controller.SetApplicationPaused(false);
                fixture.Time.Advance(.5d);
                fixture.Controller.ProcessScheduledUpdate();

                Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
                Assert.That(fixture.Controller.ActiveRepresentation.Value.Value,
                    Is.EqualTo("representation.plant.controller.mature"));
                Assert.That(fixture.VisualRoot.childCount, Is.EqualTo(childCount));
                AssertStageAboveSurface(fixture.Controller);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FocusLossAndRestore_PreserveActiveDeltaAndDropUnfocusedTime()
        {
            var fixture = new Fixture();
            try
            {
                fixture.Slot.Plant(fixture.PlantId, fixture.Context("посадка"));
                fixture.Controller.Configure(fixture.Slot, fixture.Catalog, fixture.Time);

                fixture.Time.Advance(.05d);
                fixture.Controller.SetApplicationFocused(false);
                fixture.Time.Advance(100d);
                fixture.Controller.SetApplicationFocused(true);
                fixture.Time.Advance(.1d);
                fixture.Controller.ProcessScheduledUpdate();

                Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(.15f).Within(.0001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void DisableAndEnable_PreserveActiveDeltaAndDropDisabledTime()
        {
            var fixture = new Fixture();
            try
            {
                fixture.Slot.Plant(fixture.PlantId, fixture.Context("посадка"));
                fixture.Controller.Configure(fixture.Slot, fixture.Catalog, fixture.Time);

                fixture.Time.Advance(.05d);
                InvokeLifecycle(fixture.Controller, "OnDisable");
                fixture.Time.Advance(100d);
                InvokeLifecycle(fixture.Controller, "OnEnable");
                fixture.Time.Advance(.1d);
                fixture.Controller.ProcessScheduledUpdate();

                Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(.15f).Within(.0001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertStageAboveSurface(PlantGrowthController controller)
        {
            var renderer = controller.ActiveStageObject.GetComponentInChildren<Renderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.bounds.min.y, Is.EqualTo(controller.StageSurfaceWorldY).Within(.0001f));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly UnityEngine.Object[] assets;
            private readonly GameObject[] prefabs;
            private readonly GameObject root;

            public Fixture()
            {
                PlantId = new PlantTypeId("plant.controller");
                root = new GameObject("Тест контроллера роста");
                VisualRoot = new GameObject("Стадии").transform;
                VisualRoot.SetParent(root.transform, false);
                Controller = root.AddComponent<PlantGrowthController>();
                SetField(Controller, "visualRoot", VisualRoot);

                prefabs = new[]
                {
                    GameObject.CreatePrimitive(PrimitiveType.Capsule),
                    GameObject.CreatePrimitive(PrimitiveType.Capsule),
                    GameObject.CreatePrimitive(PrimitiveType.Capsule)
                };
                for (var index = 0; index < prefabs.Length; index++)
                {
                    prefabs[index].name = index == 0 ? "Росток" : index == 1 ? "Молодое растение" : "Зрелое растение";
                    prefabs[index].transform.localScale = new Vector3(.2f, .2f + index * .2f, .2f);
                    UnityEngine.Object.DestroyImmediate(prefabs[index].GetComponent<Collider>());
                    prefabs[index].SetActive(false);
                }

                var itemRuntime = BuildItems(out var itemAssets);
                var representations = new PlantRepresentationAsset[3];
                var stages = new PlantStageAsset[3];
                for (var index = 0; index < 3; index++)
                {
                    representations[index] = ScriptableObject.CreateInstance<PlantRepresentationAsset>();
                    var suffix = index == 0 ? "sprout" : index == 1 ? "young" : "mature";
                    SetField(representations[index], "key", "representation.plant.controller." + suffix);
                    SetField(representations[index], "prefab", prefabs[index]);
                    stages[index] = new PlantStageAsset();
                    SetField(stages[index], "startsAtNormalized", index * .5f);
                    SetField(stages[index], "representation", representations[index]);
                }

                var definitionAsset = ScriptableObject.CreateInstance<PlantDefinitionAsset>();
                SetField(definitionAsset, "id", PlantId.Value);
                SetField(definitionAsset, "growthDurationSeconds", 1f);
                SetField(definitionAsset, "seedItem", itemAssets[0]);
                SetField(definitionAsset, "harvestItem", itemAssets[1]);
                SetField(definitionAsset, "stages", stages);
                var catalogAsset = ScriptableObject.CreateInstance<PlantCatalogAsset>();
                SetField(catalogAsset, "definitions", new[] { definitionAsset });
                Catalog = catalogAsset.Build(itemRuntime);
                Slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), Catalog.Plants, new Sink());
                Time = new FakeTimeSource();

                assets = new UnityEngine.Object[]
                {
                    itemAssets[0], itemAssets[1], itemAssets[2], itemAssets[3],
                    representations[0], representations[1], representations[2], definitionAsset, catalogAsset
                };
            }

            public PlantTypeId PlantId { get; }
            public Transform VisualRoot { get; }
            public PlantGrowthController Controller { get; }
            public RuntimePlantCatalog Catalog { get; }
            public PlantingSlot Slot { get; }
            public FakeTimeSource Time { get; }

            public PlantingSlotChangeContext Context(string reason) =>
                new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var prefab in prefabs)
                    UnityEngine.Object.DestroyImmediate(prefab);
                foreach (var asset in assets)
                    UnityEngine.Object.DestroyImmediate(asset);
            }

            private static RuntimeItemCatalog BuildItems(out UnityEngine.Object[] assets)
            {
                var prefab = new GameObject("Представление предмета");
                var seedRepresentation = ScriptableObject.CreateInstance<ItemRepresentationAsset>();
                SetField(seedRepresentation, "key", "representation.seed.controller");
                SetField(seedRepresentation, "prefab", prefab);
                var harvestRepresentation = ScriptableObject.CreateInstance<ItemRepresentationAsset>();
                SetField(harvestRepresentation, "key", "representation.harvest.controller");
                SetField(harvestRepresentation, "prefab", prefab);
                var seed = Item("item.seed.controller", ItemCategory.Seed, seedRepresentation);
                var harvest = Item("item.harvest.controller", ItemCategory.HarvestedPlant, harvestRepresentation);
                var catalog = ScriptableObject.CreateInstance<ItemCatalogAsset>();
                SetField(catalog, "definitions", new[] { seed, harvest });
                assets = new UnityEngine.Object[] { seed, harvest, seedRepresentation, harvestRepresentation };
                var runtime = catalog.Build();
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(prefab);
                return runtime;
            }

            private static ItemDefinitionAsset Item(
                string id,
                ItemCategory category,
                ItemRepresentationAsset representation)
            {
                var asset = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
                SetField(asset, "id", id);
                SetField(asset, "displayName", id);
                SetField(asset, "category", category);
                SetField(asset, "representation", representation);
                SetField(asset, "maxStackSize", 16);
                SetField(asset, "allowsStackRepresentation", true);
                return asset;
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Поле '{name}' не найдено.");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Lifecycle method '{name}' не найден.");
            method.Invoke(target, null);
        }

        private sealed class FakeTimeSource : IPlantGrowthTimeSource
        {
            public double TimeSeconds { get; private set; }
            public void Advance(double seconds) => TimeSeconds += seconds;
        }

        private sealed class Sink : IPlantingSlotEventSink
        {
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true;
        }
    }
}
