using System;
using System.Collections.Generic;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Plants;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantGrowthTimerTests
    {
        private static readonly PlantTypeId PlantId = new PlantTypeId("plant.timer");

        [Test]
        public void Tick_UsesConfiguredDurationAndStoresElapsedTime()
        {
            var fixture = new Fixture(20f);
            fixture.Plant();
            fixture.Timer.Resume();

            fixture.Time.Advance(5d);
            var result = fixture.Timer.Tick();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(5f).Within(.0001f));
            Assert.That(fixture.Slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.25f).Within(.0001f));
        }

        [Test]
        public void Tick_AppliesGrowthModifierToElapsedTime()
        {
            var fixture = new Fixture(20f);
            fixture.Plant();
            fixture.Slot.ApplyFertilizer(
                new PlantGrowthModifier(1.5f),
                fixture.Context("ускорение"));
            fixture.Timer.Resume();

            fixture.Time.Advance(4d);
            fixture.Timer.Tick();

            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(6f).Within(.0001f));
            Assert.That(fixture.Slot.GetSnapshot().NormalizedProgress, Is.EqualTo(.3f).Within(.0001f));
        }

        [Test]
        public void PauseAndResume_DiscardPausedInterval()
        {
            var fixture = new Fixture(10f);
            fixture.Plant();
            fixture.Timer.Resume();
            fixture.Time.Advance(2d);
            fixture.Timer.Tick();

            fixture.Timer.Pause();
            fixture.Time.Advance(100d);
            fixture.Timer.Resume();
            fixture.Time.Advance(1d);
            fixture.Timer.Tick();

            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(3f).Within(.0001f));
        }

        [Test]
        public void Pause_PreservesActiveIntervalSincePreviousTick()
        {
            var fixture = new Fixture(10f);
            fixture.Plant();
            fixture.Timer.Resume();

            fixture.Time.Advance(.05d);
            fixture.Timer.Pause();
            fixture.Time.Advance(100d);
            fixture.Timer.Resume();
            fixture.Time.Advance(.05d);
            fixture.Timer.Tick();

            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(.1f).Within(.0001f));
        }

        [Test]
        public void Tick_RefreshesBaselineWhileWaterIsNeeded()
        {
            var fixture = new Fixture(10f);
            fixture.Plant();
            fixture.Timer.Resume();
            fixture.Time.Advance(2d);
            fixture.Timer.Tick();
            fixture.Slot.RequestWater(fixture.Context("потребность в воде"));

            fixture.Time.Advance(50d);
            Assert.That(fixture.Timer.Tick(), Is.Null);
            fixture.Slot.Water(fixture.Context("полив"));
            fixture.Time.Advance(1d);
            fixture.Timer.Tick();

            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(3f).Within(.0001f));
        }

        [Test]
        public void Tick_RefreshesBaselineWhileSlotIsEmpty()
        {
            var fixture = new Fixture(10f);
            fixture.Timer.Resume();
            fixture.Time.Advance(50d);
            Assert.That(fixture.Timer.Tick(), Is.Null);
            fixture.Plant();

            fixture.Time.Advance(1d);
            fixture.Timer.Tick();

            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(1f).Within(.0001f));
        }

        [Test]
        public void Tick_CompletesExactlyOnce()
        {
            var fixture = new Fixture(3f);
            fixture.Plant();
            fixture.Timer.Resume();

            fixture.Time.Advance(10d);
            var completion = fixture.Timer.Tick();
            fixture.Time.Advance(10d);
            var repeated = fixture.Timer.Tick();

            Assert.That(completion.Succeeded, Is.True);
            Assert.That(repeated, Is.Null);
            Assert.That(fixture.Slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
            Assert.That(fixture.Slot.GetSnapshot().ElapsedGrowthSeconds, Is.EqualTo(3f));
            Assert.That(fixture.Sink.GrowthChanges, Is.EqualTo(1));
        }

        [TestCase(0f, "representation.plant.timer.seed")]
        [TestCase(.4999f, "representation.plant.timer.seed")]
        [TestCase(.5f, "representation.plant.timer.young")]
        [TestCase(.9999f, "representation.plant.timer.young")]
        [TestCase(1f, "representation.plant.timer.mature")]
        public void StageResolver_UsesInclusiveLowerBoundary(float progress, string expectedKey)
        {
            var fixture = new Fixture(10f);

            var stage = PlantGrowthStageResolver.Resolve(fixture.Definition, progress);

            Assert.That(stage.Representation.Value, Is.EqualTo(expectedKey));
        }

        private sealed class Fixture
        {
            public Fixture(float duration)
            {
                var seed = new ItemId("item.seed.timer");
                var harvest = new ItemId("item.harvest.timer");
                var items = new ItemCatalog(new[]
                {
                    new ItemDefinition(seed, "Семя", ItemCategory.Seed,
                        new ItemRepresentationKey("representation.seed.timer"), new ItemStackRules(16, true)),
                    new ItemDefinition(harvest, "Урожай", ItemCategory.HarvestedPlant,
                        new ItemRepresentationKey("representation.harvest.timer"), new ItemStackRules(16, true))
                });
                Definition = new PlantDefinition(
                    PlantId,
                    new GrowthDuration(duration),
                    seed,
                    harvest,
                    new[]
                    {
                        new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.timer.seed")),
                        new PlantStageDefinition(.5f, new PlantRepresentationKey("representation.plant.timer.young")),
                        new PlantStageDefinition(1f, new PlantRepresentationKey("representation.plant.timer.mature"))
                    });
                var catalog = new PlantCatalog(items, new[] { Definition });
                Sink = new RecordingSink();
                Slot = new PlantingSlot(new PlantingSlotId(Guid.NewGuid()), catalog, Sink);
                Time = new FakeTimeSource();
                Timer = new PlantGrowthTimer(Slot, catalog, Time);
            }

            public PlantDefinition Definition { get; }
            public PlantingSlot Slot { get; }
            public FakeTimeSource Time { get; }
            public PlantGrowthTimer Timer { get; }
            public RecordingSink Sink { get; }

            public void Plant() => Slot.Plant(PlantId, Context("посадка"));

            public PlantingSlotChangeContext Context(string reason) =>
                new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());
        }

        private sealed class FakeTimeSource : IPlantGrowthTimeSource
        {
            public double TimeSeconds { get; private set; }
            public void Advance(double seconds) => TimeSeconds += seconds;
        }

        private sealed class RecordingSink : IPlantingSlotEventSink
        {
            public int GrowthChanges { get; private set; }

            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                if (plantingSlotChanged.Kind == PlantingSlotMutationKind.AdvanceProgress)
                    GrowthChanges++;
                return true;
            }
        }
    }
}
