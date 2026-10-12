using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Plants;
using VrGame.Presentation.Indicators;
using VrGame.VRInteraction.Watering;

namespace VrGame.Tests.EditMode.Watering
{
    public sealed class WateringAdapterAndProjectionTests
    {
        private static readonly PlantTypeId PlantId = new PlantTypeId("plant.watering-adapter");
        private static readonly ItemId SeedId = new ItemId("item.seed.watering-adapter");
        private static readonly ItemId HarvestId = new ItemId("item.harvest.watering-adapter");

        [Test]
        public void Adapter_WatersExactlyOnceAndReplayHasNoSecondMutation()
        {
            var slot = CreateSlot(new AcceptingSink());
            MoveToNeedsWater(slot);
            var adapter = new WateringAdapter(slot);
            var activationId = Guid.NewGuid();
            var beforeVersion = slot.Version;

            var first = adapter.Water(activationId);
            var replay = adapter.Water(activationId);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.IsReplay, Is.False);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(slot.Version, Is.EqualTo(beforeVersion + 1));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.5f));
        }

        [Test]
        public void Projection_IsSnapshotDrivenAndReconcilesAcrossDisableEnable()
        {
            var root = new GameObject("projection-test");
            try
            {
                var targetObject = new GameObject("target");
                targetObject.transform.SetParent(root.transform, false);
                var target = targetObject.AddComponent<IndicatorTarget>();
                var source = root.AddComponent<PlantingSlotIndicatorProjectionSource>();
                var slot = CreateSlot(source);
                source.Configure(slot, target);
                var changes = new List<IndicatorProjection>();
                source.ProjectionChanged += changes.Add;

                slot.Plant(PlantId, Context("посадка"));
                Assert.That(source.GetSnapshot()[0].Visible, Is.False);

                slot.AdvanceProgress(.5f, slot.Version, Context("запрос воды"));
                Assert.That(changes[^1].Visible, Is.True);
                Assert.That(source.GetSnapshot()[0].Visible, Is.True);

                slot.Water(Context("полив"));
                Assert.That(changes[^1].Visible, Is.False);
                Assert.That(source.GetSnapshot()[0].Visible, Is.False);

                source.enabled = false;
                source.enabled = true;
                Assert.That(changes[^1].Visible, Is.False);
                Assert.That(source.GetSnapshot()[0].Visible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Projection_IsolatesSubscriberFailureAndStillNotifiesOthers()
        {
            var root = new GameObject("projection-subscriber-test");
            try
            {
                var target = new GameObject("target").AddComponent<IndicatorTarget>();
                target.transform.SetParent(root.transform, false);
                var source = root.AddComponent<PlantingSlotIndicatorProjectionSource>();
                var slot = CreateSlot(source);
                source.Configure(slot, target);
                var delivered = 0;
                source.ProjectionChanged += _ => throw new InvalidOperationException("тестовый observer");
                source.ProjectionChanged += _ => delivered++;
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: тестовый observer");

                slot.Plant(PlantId, Context("посадка"));

                Assert.That(delivered, Is.EqualTo(1));
                Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static PlantingSlot CreateSlot(IPlantingSlotEventSink sink)
        {
            var items = new ItemCatalog(new[]
            {
                Item(SeedId, ItemCategory.Seed),
                Item(HarvestId, ItemCategory.HarvestedPlant)
            });
            var plants = new PlantCatalog(items, new[]
            {
                new PlantDefinition(
                    PlantId,
                    new GrowthDuration(10f),
                    SeedId,
                    HarvestId,
                    new WaterRequestRange(.5f, .5f),
                    new[]
                    {
                        new PlantStageDefinition(
                            0f,
                            new PlantRepresentationKey("representation.plant.watering-adapter"))
                    })
            });
            return new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), plants, sink, new FixedRandom());
        }

        private static ItemDefinition Item(ItemId id, ItemCategory category) =>
            new ItemDefinition(
                id,
                id.Value,
                category,
                new ItemRepresentationKey("representation." + id.Value),
                new ItemStackRules(16, false));

        private static void MoveToNeedsWater(PlantingSlot slot)
        {
            slot.Plant(PlantId, Context("посадка"));
            slot.AdvanceProgress(.5f, slot.Version, Context("достижение порога"));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.NeedsWater));
        }

        private static PlantingSlotChangeContext Context(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class FixedRandom : IPlantGrowthRandomSource
        {
            public float NextUnit() => .5f;
        }

        private sealed class AcceptingSink : IPlantingSlotEventSink
        {
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged) => true;
        }
    }
}
