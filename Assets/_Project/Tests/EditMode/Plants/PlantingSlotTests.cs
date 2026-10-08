using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Plants;

namespace VrGame.Tests.EditMode.Plants
{
    public sealed class PlantingSlotTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly PlantTypeId Mint = new PlantTypeId("plant.mint");

        [Test]
        public void InitialSnapshot_IsEmptyAndUsesBaselineModifier()
        {
            var slot = CreateSlot(out _);
            var snapshot = slot.GetSnapshot();

            Assert.That(snapshot.SlotId, Is.EqualTo(slot.Id));
            Assert.That(snapshot.Version, Is.Zero);
            Assert.That(snapshot.State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(snapshot.PlantTypeId, Is.Null);
            Assert.That(snapshot.NormalizedProgress, Is.Zero);
            Assert.That(snapshot.GrowthModifier, Is.EqualTo(PlantGrowthModifier.None));
            Assert.That(snapshot.GrowthModifier.IsApplied, Is.False);
            Assert.That(snapshot.GrowthModifier.EffectiveMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void FullLifecycle_PreservesInvariantsAndHarvestAtomicallyResetsSlot()
        {
            var slot = CreateSlot(out var sink);

            Assert.That(slot.Plant(Basil, Context("посадка")).Succeeded, Is.True);
            Assert.That(slot.AdvanceProgress(.25f, slot.Version, Context("рост")).Succeeded, Is.True);
            Assert.That(slot.RequestWater(Context("запрос воды")).Succeeded, Is.True);
            Assert.That(slot.ApplyFertilizer(new PlantGrowthModifier(1.5f), Context("удобрение")).Succeeded, Is.True);
            Assert.That(slot.Water(Context("полив")).Succeeded, Is.True);
            Assert.That(slot.AdvanceProgress(1f, slot.Version, Context("созревание")).Succeeded, Is.True);

            var ready = slot.GetSnapshot();
            Assert.That(ready.State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
            Assert.That(ready.PlantTypeId, Is.EqualTo(Basil));
            Assert.That(ready.NormalizedProgress, Is.EqualTo(1f));
            Assert.That(ready.GrowthModifier.EffectiveMultiplier, Is.EqualTo(1.5f));

            var harvest = slot.Harvest(Context("сбор урожая"));
            var empty = slot.GetSnapshot();

            Assert.That(harvest.Succeeded, Is.True);
            Assert.That(empty.Version, Is.EqualTo(7));
            Assert.That(empty.State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(empty.PlantTypeId, Is.Null);
            Assert.That(empty.NormalizedProgress, Is.Zero);
            Assert.That(empty.GrowthModifier, Is.EqualTo(PlantGrowthModifier.None));
            Assert.That(sink.Events, Has.Count.EqualTo(7));
            Assert.That(harvest.Change.Before.State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
            Assert.That(harvest.Change.After.State, Is.EqualTo(PlantingSlotState.Empty));
        }

        [Test]
        public void Plant_WhenOccupied_IsExactNoOp()
        {
            var slot = CreateSlot(out var sink);
            slot.Plant(Basil, Context("первая посадка"));
            var before = slot.GetSnapshot();

            var result = slot.Plant(Mint, Context("повторная посадка"));

            AssertNoOp(slot, before, result, PlantingSlotRejection.Occupied);
            Assert.That(sink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void InvalidTransitions_InEveryState_AreExactNoOps()
        {
            var empty = CreateSlot(out _);
            AssertRejectedNoOp(empty, () => empty.AdvanceProgress(.1f, empty.Version, Context("рост пустого")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(empty, () => empty.RequestWater(Context("вода пустому")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(empty, () => empty.Water(Context("полив пустого")), PlantingSlotRejection.WaterNotNeeded);
            AssertRejectedNoOp(empty, () => empty.ApplyFertilizer(new PlantGrowthModifier(2f), Context("удобрение пустого")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(empty, () => empty.Harvest(Context("сбор пустого")), PlantingSlotRejection.NotReady);

            var growing = CreateSlot(out _);
            growing.Plant(Basil, Context("посадка"));
            AssertRejectedNoOp(growing, () => growing.Water(Context("лишний полив")), PlantingSlotRejection.WaterNotNeeded);
            AssertRejectedNoOp(growing, () => growing.Harvest(Context("ранний сбор")), PlantingSlotRejection.NotReady);

            var needsWater = CreateSlot(out _);
            needsWater.Plant(Basil, Context("посадка"));
            needsWater.RequestWater(Context("запрос воды"));
            AssertRejectedNoOp(needsWater, () => needsWater.AdvanceProgress(.2f, needsWater.Version, Context("рост без воды")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(needsWater, () => needsWater.RequestWater(Context("повторный запрос")), PlantingSlotRejection.AlreadyNeedsWater);
            AssertRejectedNoOp(needsWater, () => needsWater.Harvest(Context("ранний сбор")), PlantingSlotRejection.NotReady);

            var ready = CreateReadySlot();
            AssertRejectedNoOp(ready, () => ready.AdvanceProgress(1f, ready.Version, Context("рост готового")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(ready, () => ready.RequestWater(Context("вода готовому")), PlantingSlotRejection.NotGrowing);
            AssertRejectedNoOp(ready, () => ready.Water(Context("полив готового")), PlantingSlotRejection.WaterNotNeeded);
            AssertRejectedNoOp(ready, () => ready.ApplyFertilizer(new PlantGrowthModifier(2f), Context("удобрение готового")), PlantingSlotRejection.NotGrowing);
        }

        [Test]
        public void Plant_RejectsUnknownTypeWithoutMutation()
        {
            var slot = CreateSlot(out _);
            AssertRejectedNoOp(
                slot,
                () => slot.Plant(new PlantTypeId("plant.unknown"), Context("неизвестное растение")),
                PlantingSlotRejection.UnknownPlant);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-.01f)]
        [TestCase(1.01f)]
        public void AdvanceProgress_RejectsNonFiniteAndOutOfRangeTargets(float target)
        {
            var slot = CreateSlot(out _);
            slot.Plant(Basil, Context("посадка"));

            AssertRejectedNoOp(
                slot,
                () => slot.AdvanceProgress(target, slot.Version, Context("невалидный прогресс")),
                PlantingSlotRejection.InvalidProgress);
        }

        [Test]
        public void AdvanceProgress_RejectsEqualAndRegressingTargets()
        {
            var slot = CreateSlot(out _);
            slot.Plant(Basil, Context("посадка"));
            slot.AdvanceProgress(.5f, slot.Version, Context("рост"));

            AssertRejectedNoOp(slot, () => slot.AdvanceProgress(.5f, slot.Version, Context("равный прогресс")), PlantingSlotRejection.ProgressRegressionOrNoProgress);
            AssertRejectedNoOp(slot, () => slot.AdvanceProgress(.4f, slot.Version, Context("регресс")), PlantingSlotRejection.ProgressRegressionOrNoProgress);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GrowthModifier_RejectsInvalidAppliedValue(float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlantGrowthModifier(multiplier));
        }

        [Test]
        public void Fertilizer_RejectsBaselineAndSecondApplication_ThenResetsAfterHarvest()
        {
            var slot = CreateSlot(out _);
            slot.Plant(Basil, Context("посадка"));
            AssertRejectedNoOp(slot, () => slot.ApplyFertilizer(default, Context("пустой modifier")), PlantingSlotRejection.InvalidFertilizer);
            Assert.That(slot.ApplyFertilizer(new PlantGrowthModifier(1.25f), Context("удобрение")).Succeeded, Is.True);
            AssertRejectedNoOp(
                slot,
                () => slot.ApplyFertilizer(new PlantGrowthModifier(2f), Context("повторное удобрение")),
                PlantingSlotRejection.FertilizerAlreadyApplied);
            slot.AdvanceProgress(1f, slot.Version, Context("созревание"));
            slot.Harvest(Context("сбор"));

            Assert.That(slot.GetSnapshot().GrowthModifier, Is.EqualTo(PlantGrowthModifier.None));
        }

        [Test]
        public void SuccessfulCommands_EmitExactlyOneImmutableVersionedEvent()
        {
            var slot = CreateSlot(out var sink);
            var commandId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var context = new PlantingSlotChangeContext("посадка игроком", commandId, correlationId);

            var result = slot.Plant(Basil, context);
            var change = sink.Events[0];

            Assert.That(result.EventPublished, Is.True);
            Assert.That(result.Change, Is.SameAs(change));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
            Assert.That(change.Kind, Is.EqualTo(PlantingSlotMutationKind.Plant));
            Assert.That(change.Reason, Is.EqualTo("посадка игроком"));
            Assert.That(change.CommandId, Is.EqualTo(commandId));
            Assert.That(change.CorrelationId, Is.EqualTo(correlationId));
            Assert.That(change.Before.Version, Is.Zero);
            Assert.That(change.Before.State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(change.After.Version, Is.EqualTo(1));
            Assert.That(change.After.State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(change.After.PlantTypeId, Is.EqualTo(Basil));
        }

        [Test]
        public void SinkException_DoesNotRollbackCommittedMutation()
        {
            var slot = new PlantingSlot(NewSlotId(), CreateCatalog(), new ThrowingSink());

            var result = slot.Plant(Basil, Context("посадка"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EventPublished, Is.False);
            Assert.That(result.Change, Is.Not.Null);
            Assert.That(slot.Version, Is.EqualTo(1));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
        }

        [Test]
        public void SinkReentry_IsRejectedAndOuterMutationRemainsCommitted()
        {
            var sink = new ReentrantSink();
            var slot = new PlantingSlot(NewSlotId(), CreateCatalog(), sink);
            sink.Slot = slot;

            var outer = slot.Plant(Basil, Context("посадка"));

            Assert.That(outer.Succeeded, Is.True);
            Assert.That(sink.ReentrantResult.Rejection, Is.EqualTo(PlantingSlotRejection.ReentrantMutation));
            Assert.That(slot.Version, Is.EqualTo(1));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
        }

        [Test]
        public void SuccessfulCommandReplay_ReturnsOriginalResultWithoutSecondMutationOrEvent()
        {
            var slot = CreateSlot(out var sink);
            var context = Context("посадка");
            var first = slot.Plant(Basil, context);

            var replay = slot.Plant(Basil, context);

            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.Change, Is.SameAs(first.Change));
            Assert.That(replay.EventPublished, Is.EqualTo(first.EventPublished));
            Assert.That(slot.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void RejectedCommandReplay_ReturnsOriginalRejectionWithoutReevaluation()
        {
            var slot = CreateSlot(out _);
            var context = Context("неизвестная посадка");
            var unknown = new PlantTypeId("plant.unknown");
            var first = slot.Plant(unknown, context);

            slot.Plant(Basil, Context("другая успешная посадка"));
            var replay = slot.Plant(unknown, context);

            Assert.That(first.Rejection, Is.EqualTo(PlantingSlotRejection.UnknownPlant));
            Assert.That(replay.Rejection, Is.EqualTo(PlantingSlotRejection.UnknownPlant));
            Assert.That(replay.IsReplay, Is.True);
        }

        [Test]
        public void SameCommandWithDifferentPayloadOrContext_IsConflict()
        {
            var slot = CreateSlot(out _);
            var commandId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var original = new PlantingSlotChangeContext("посадка", commandId, correlationId);
            slot.Plant(Basil, original);
            var before = slot.GetSnapshot();

            AssertNoOp(
                slot,
                before,
                slot.Plant(Mint, original),
                PlantingSlotRejection.CommandConflict);
            AssertNoOp(
                slot,
                before,
                slot.Plant(Basil, new PlantingSlotChangeContext("другая причина", commandId, correlationId)),
                PlantingSlotRejection.CommandConflict);
            AssertNoOp(
                slot,
                before,
                slot.Plant(Basil, new PlantingSlotChangeContext("посадка", commandId, Guid.NewGuid())),
                PlantingSlotRejection.CommandConflict);
        }

        [Test]
        public void ProgressUsesExpectedVersionWithoutReplayCache_AndModifierDetectsPayloadConflict()
        {
            var progressSlot = CreateSlot(out _);
            progressSlot.Plant(Basil, Context("посадка"));
            var progressContext = Context("рост");
            var expectedVersion = progressSlot.Version;
            progressSlot.AdvanceProgress(.2f, expectedVersion, progressContext);
            Assert.That(
                progressSlot.AdvanceProgress(.3f, expectedVersion, progressContext).Rejection,
                Is.EqualTo(PlantingSlotRejection.StaleVersion));
            var commandCache = (System.Collections.IDictionary)typeof(PlantingSlot)
                .GetField("completedCommands", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(progressSlot);
            Assert.That(commandCache.Count, Is.EqualTo(1), "В cache должна остаться только дискретная команда Plant.");

            var fertilizerSlot = CreateSlot(out _);
            fertilizerSlot.Plant(Basil, Context("посадка"));
            var fertilizerContext = Context("удобрение");
            fertilizerSlot.ApplyFertilizer(new PlantGrowthModifier(1.5f), fertilizerContext);
            Assert.That(
                fertilizerSlot.ApplyFertilizer(new PlantGrowthModifier(2f), fertilizerContext).Rejection,
                Is.EqualTo(PlantingSlotRejection.CommandConflict));
        }

        [Test]
        public void InvalidContextAndReentry_AreCheckedBeforeCommandCache()
        {
            var slot = CreateSlot(out _);
            var invalid = default(PlantingSlotChangeContext);

            Assert.That(slot.Plant(Basil, invalid).Rejection, Is.EqualTo(PlantingSlotRejection.InvalidContext));
            Assert.That(slot.Plant(Basil, invalid).IsReplay, Is.False);

            var sink = new ReplayDuringPublishSink();
            var reentrantSlot = new PlantingSlot(NewSlotId(), CreateCatalog(), sink);
            sink.Slot = reentrantSlot;
            sink.Context = Context("посадка");
            reentrantSlot.Plant(Basil, sink.Context);
            Assert.That(sink.Result.Rejection, Is.EqualTo(PlantingSlotRejection.ReentrantMutation));
            Assert.That(sink.Result.IsReplay, Is.False);
        }

        [Test]
        public void ContextAndSlotId_RejectMissingRequiredValues()
        {
            Assert.Throws<ArgumentException>(() => new PlantingSlotId(Guid.Empty));
            Assert.Throws<ArgumentException>(() => new PlantingSlotChangeContext(" ", Guid.NewGuid(), Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => new PlantingSlotChangeContext("причина", Guid.Empty, Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => new PlantingSlotChangeContext("причина", Guid.NewGuid(), Guid.Empty));
            Assert.Throws<ArgumentException>(() => new PlantingSlot(default, CreateCatalog(), new RecordingSink()));
            Assert.Throws<ArgumentNullException>(() => new PlantingSlot(NewSlotId(), null, new RecordingSink()));
            Assert.Throws<ArgumentNullException>(() => new PlantingSlot(NewSlotId(), CreateCatalog(), null));
        }

        [Test]
        public void VersionOverflow_IsCachedRejectionAndExactNoOp()
        {
            var slot = CreateSlot(out _);
            typeof(PlantingSlot)
                .GetProperty(nameof(PlantingSlot.Version), BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, long.MaxValue);
            var context = Context("посадка при переполнении");
            var before = slot.GetSnapshot();

            var result = slot.Plant(Basil, context);
            AssertNoOp(slot, before, result, PlantingSlotRejection.ArithmeticOverflow);
            Assert.That(slot.Plant(Basil, context).IsReplay, Is.True);
        }

        private static PlantingSlot CreateReadySlot()
        {
            var slot = CreateSlot(out _);
            slot.Plant(Basil, Context("посадка"));
            slot.AdvanceProgress(1f, slot.Version, Context("созревание"));
            return slot;
        }

        private static void AssertRejectedNoOp(
            PlantingSlot slot,
            Func<PlantingSlotMutationResult> command,
            PlantingSlotRejection expected)
        {
            var before = slot.GetSnapshot();
            AssertNoOp(slot, before, command(), expected);
        }

        private static void AssertNoOp(
            PlantingSlot slot,
            PlantingSlotSnapshot before,
            PlantingSlotMutationResult result,
            PlantingSlotRejection expected)
        {
            var after = slot.GetSnapshot();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(expected));
            Assert.That(result.Change, Is.Null);
            Assert.That(after.Version, Is.EqualTo(before.Version));
            Assert.That(after.State, Is.EqualTo(before.State));
            Assert.That(after.PlantTypeId, Is.EqualTo(before.PlantTypeId));
            Assert.That(after.NormalizedProgress, Is.EqualTo(before.NormalizedProgress));
            Assert.That(after.GrowthModifier, Is.EqualTo(before.GrowthModifier));
        }

        private static PlantingSlot CreateSlot(out RecordingSink sink)
        {
            sink = new RecordingSink();
            return new PlantingSlot(NewSlotId(), CreateCatalog(), sink);
        }

        private static PlantingSlotId NewSlotId() => new PlantingSlotId(Guid.NewGuid());

        private static PlantingSlotChangeContext Context(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private static PlantCatalog CreateCatalog()
        {
            var items = new ItemCatalog(new[]
            {
                Item("item.seed.basil", ItemCategory.Seed),
                Item("item.harvest.basil", ItemCategory.HarvestedPlant),
                Item("item.seed.mint", ItemCategory.Seed),
                Item("item.harvest.mint", ItemCategory.HarvestedPlant)
            });
            return new PlantCatalog(items, new[]
            {
                Plant(Basil, "item.seed.basil", "item.harvest.basil"),
                Plant(Mint, "item.seed.mint", "item.harvest.mint")
            });
        }

        private static PlantDefinition Plant(PlantTypeId id, string seed, string harvest) =>
            new PlantDefinition(
                id,
                new GrowthDuration(10f),
                new ItemId(seed),
                new ItemId(harvest),
                new[] { new PlantStageDefinition(0f, new PlantRepresentationKey("representation.plant.test")) });

        private static ItemDefinition Item(string id, ItemCategory category) =>
            new ItemDefinition(
                new ItemId(id),
                id,
                category,
                new ItemRepresentationKey("representation.test.item"),
                new ItemStackRules(10, false));

        private sealed class RecordingSink : IPlantingSlotEventSink
        {
            public List<PlantingSlotChanged> Events { get; } = new List<PlantingSlotChanged>();

            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                Events.Add(plantingSlotChanged);
                return true;
            }
        }

        private sealed class ThrowingSink : IPlantingSlotEventSink
        {
            public bool TryPublish(PlantingSlotChanged plantingSlotChanged) =>
                throw new InvalidOperationException("Проверочная ошибка sink.");
        }

        private sealed class ReentrantSink : IPlantingSlotEventSink
        {
            public PlantingSlot Slot { get; set; }
            public PlantingSlotMutationResult ReentrantResult { get; private set; }

            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                ReentrantResult = Slot.RequestWater(Context("reentrant запрос воды"));
                return true;
            }
        }

        private sealed class ReplayDuringPublishSink : IPlantingSlotEventSink
        {
            public PlantingSlot Slot { get; set; }
            public PlantingSlotChangeContext Context { get; set; }
            public PlantingSlotMutationResult Result { get; private set; }

            public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
            {
                Result = Slot.Plant(Basil, Context);
                return true;
            }
        }
    }
}
