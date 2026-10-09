using System;
using System.Collections.Generic;
using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public sealed partial class PlantingSlot
    {
        private readonly PlantCatalog catalog;
        private readonly IPlantingSlotEventSink eventSink;
        private readonly Dictionary<CommandKey, CompletedCommand> completedCommands =
            new Dictionary<CommandKey, CompletedCommand>();
        private PlantingSlotState state;
        private PlantTypeId? plantTypeId;
        private float normalizedProgress;
        private PlantGrowthModifier growthModifier;
        private bool isPublishingEvent;

        public PlantingSlot(PlantingSlotId id, PlantCatalog catalog, IPlantingSlotEventSink eventSink)
        {
            if (id.Value == Guid.Empty)
                throw new ArgumentException("Planting slot ID не может быть пустым.", nameof(id));

            Id = id;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            state = PlantingSlotState.Empty;
            growthModifier = PlantGrowthModifier.None;
        }

        public PlantingSlotId Id { get; }
        public long Version { get; private set; }

        public PlantingSlotSnapshot GetSnapshot() =>
            new PlantingSlotSnapshot(Id, Version, state, plantTypeId, normalizedProgress, growthModifier);

        public PlantingSlotMutationResult Plant(PlantTypeId typeId, PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.Plant, CommandPayload.ForPlant(typeId), context);

        public PlantingSlotMutationResult AdvanceProgress(
            float absoluteTarget,
            long expectedVersion,
            PlantingSlotChangeContext context) =>
            Execute(
                PlantingSlotMutationKind.AdvanceProgress,
                CommandPayload.ForProgress(absoluteTarget),
                context,
                cacheResult: false,
                expectedVersion: expectedVersion);

        public PlantingSlotMutationResult RequestWater(PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.RequestWater, CommandPayload.None, context);

        public PlantingSlotMutationResult Water(PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.Water, CommandPayload.None, context);

        public PlantingSlotMutationResult ApplyFertilizer(
            PlantGrowthModifier modifier,
            PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.ApplyFertilizer, CommandPayload.ForModifier(modifier), context);

        public PlantingSlotMutationResult Harvest(PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.Harvest, CommandPayload.None, context);

        private PlantingSlotMutationResult Execute(
            PlantingSlotMutationKind kind,
            CommandPayload payload,
            PlantingSlotChangeContext context,
            bool cacheResult = true,
            long? expectedVersion = null)
        {
            if (isPublishingEvent)
                return PlantingSlotMutationResult.Reject(PlantingSlotRejection.ReentrantMutation);
            if (!context.IsValid)
                return PlantingSlotMutationResult.Reject(PlantingSlotRejection.InvalidContext);
            if (expectedVersion.HasValue && expectedVersion.Value != Version)
                return PlantingSlotMutationResult.Reject(PlantingSlotRejection.StaleVersion);

            var key = new CommandKey(kind, context.CommandId);
            if (cacheResult && completedCommands.TryGetValue(key, out var completed))
            {
                if (!completed.Matches(payload, context))
                    return PlantingSlotMutationResult.Reject(PlantingSlotRejection.CommandConflict);

                return completed.Result.AsReplay();
            }

            var rejection = Validate(kind, payload);
            if (rejection != PlantingSlotRejection.None)
                return cacheResult
                    ? CompleteRejection(key, payload, context, rejection)
                    : PlantingSlotMutationResult.Reject(rejection);
            if (Version == long.MaxValue)
                return cacheResult
                    ? CompleteRejection(key, payload, context, PlantingSlotRejection.ArithmeticOverflow)
                    : PlantingSlotMutationResult.Reject(PlantingSlotRejection.ArithmeticOverflow);

            var before = GetSnapshot();
            Apply(kind, payload);
            Version++;
            var after = GetSnapshot();
            var change = new PlantingSlotChanged(kind, context, before, after);

            var eventPublished = false;
            isPublishingEvent = true;
            try
            {
                eventPublished = eventSink.TryPublish(change);
            }
            catch (Exception)
            {
                // Состояние уже зафиксировано; событие доступно в результате команды,
                // поэтому adapter может повторить доставку без повторной mutation.
            }
            finally
            {
                isPublishingEvent = false;
            }

            var result = PlantingSlotMutationResult.Success(change, eventPublished);
            if (cacheResult)
                completedCommands.Add(key, new CompletedCommand(payload, context, result));
            return result;
        }

        private PlantingSlotRejection Validate(PlantingSlotMutationKind kind, CommandPayload payload)
        {
            switch (kind)
            {
                case PlantingSlotMutationKind.Plant:
                    if (!catalog.TryGet(payload.PlantTypeId, out _))
                        return PlantingSlotRejection.UnknownPlant;
                    return state == PlantingSlotState.Empty
                        ? PlantingSlotRejection.None
                        : PlantingSlotRejection.Occupied;

                case PlantingSlotMutationKind.AdvanceProgress:
                    if (float.IsNaN(payload.Progress) || float.IsInfinity(payload.Progress) ||
                        payload.Progress < 0f || payload.Progress > 1f)
                        return PlantingSlotRejection.InvalidProgress;
                    if (state != PlantingSlotState.Growing)
                        return PlantingSlotRejection.NotGrowing;
                    return payload.Progress <= normalizedProgress
                        ? PlantingSlotRejection.ProgressRegressionOrNoProgress
                        : PlantingSlotRejection.None;

                case PlantingSlotMutationKind.RequestWater:
                    if (state == PlantingSlotState.NeedsWater)
                        return PlantingSlotRejection.AlreadyNeedsWater;
                    return state == PlantingSlotState.Growing
                        ? PlantingSlotRejection.None
                        : PlantingSlotRejection.NotGrowing;

                case PlantingSlotMutationKind.Water:
                    return state == PlantingSlotState.NeedsWater
                        ? PlantingSlotRejection.None
                        : PlantingSlotRejection.WaterNotNeeded;

                case PlantingSlotMutationKind.ApplyFertilizer:
                    if (!payload.Modifier.IsValid)
                        return PlantingSlotRejection.InvalidFertilizer;
                    if (state != PlantingSlotState.Growing && state != PlantingSlotState.NeedsWater)
                        return PlantingSlotRejection.NotGrowing;
                    return growthModifier.IsApplied
                        ? PlantingSlotRejection.FertilizerAlreadyApplied
                        : PlantingSlotRejection.None;

                case PlantingSlotMutationKind.Harvest:
                    return state == PlantingSlotState.ReadyToHarvest
                        ? PlantingSlotRejection.None
                        : PlantingSlotRejection.NotReady;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private void Apply(PlantingSlotMutationKind kind, CommandPayload payload)
        {
            switch (kind)
            {
                case PlantingSlotMutationKind.Plant:
                    state = PlantingSlotState.Growing;
                    plantTypeId = payload.PlantTypeId;
                    normalizedProgress = 0f;
                    growthModifier = PlantGrowthModifier.None;
                    break;
                case PlantingSlotMutationKind.AdvanceProgress:
                    normalizedProgress = payload.Progress;
                    if (normalizedProgress == 1f)
                        state = PlantingSlotState.ReadyToHarvest;
                    break;
                case PlantingSlotMutationKind.RequestWater:
                    state = PlantingSlotState.NeedsWater;
                    break;
                case PlantingSlotMutationKind.Water:
                    state = PlantingSlotState.Growing;
                    break;
                case PlantingSlotMutationKind.ApplyFertilizer:
                    growthModifier = payload.Modifier;
                    break;
                case PlantingSlotMutationKind.Harvest:
                    state = PlantingSlotState.Empty;
                    plantTypeId = null;
                    normalizedProgress = 0f;
                    growthModifier = PlantGrowthModifier.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private PlantingSlotMutationResult CompleteRejection(
            CommandKey key,
            CommandPayload payload,
            PlantingSlotChangeContext context,
            PlantingSlotRejection rejection)
        {
            var result = PlantingSlotMutationResult.Reject(rejection);
            completedCommands.Add(key, new CompletedCommand(payload, context, result));
            return result;
        }

        private readonly struct CommandKey : IEquatable<CommandKey>
        {
            public CommandKey(PlantingSlotMutationKind kind, Guid commandId)
            {
                Kind = kind;
                CommandId = commandId;
            }

            private PlantingSlotMutationKind Kind { get; }
            private Guid CommandId { get; }

            public bool Equals(CommandKey other) => Kind == other.Kind && CommandId == other.CommandId;
            public override bool Equals(object obj) => obj is CommandKey other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ CommandId.GetHashCode();
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            private CommandPayload(PlantTypeId plantTypeId, float progress, PlantGrowthModifier modifier)
            {
                PlantTypeId = plantTypeId;
                Progress = progress;
                Modifier = modifier;
            }

            public static CommandPayload None => default;
            public PlantTypeId PlantTypeId { get; }
            public float Progress { get; }
            public PlantGrowthModifier Modifier { get; }

            public static CommandPayload ForPlant(PlantTypeId id) => new CommandPayload(id, 0f, default);
            public static CommandPayload ForProgress(float progress) => new CommandPayload(default, progress, default);
            public static CommandPayload ForModifier(PlantGrowthModifier modifier) => new CommandPayload(default, 0f, modifier);

            public bool Equals(CommandPayload other) =>
                PlantTypeId.Equals(other.PlantTypeId) &&
                Progress.Equals(other.Progress) &&
                Modifier.Equals(other.Modifier);
            public override bool Equals(object obj) => obj is CommandPayload other && Equals(other);
            public override int GetHashCode() =>
                ((PlantTypeId.GetHashCode() * 397) ^ Progress.GetHashCode()) * 397 ^ Modifier.GetHashCode();
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;
            private readonly string reason;
            private readonly Guid correlationId;

            public CompletedCommand(
                CommandPayload payload,
                PlantingSlotChangeContext context,
                PlantingSlotMutationResult result)
            {
                this.payload = payload;
                reason = context.Reason;
                correlationId = context.CorrelationId;
                Result = result;
            }

            public PlantingSlotMutationResult Result { get; }

            public bool Matches(CommandPayload candidatePayload, PlantingSlotChangeContext context) =>
                payload.Equals(candidatePayload) &&
                string.Equals(reason, context.Reason, StringComparison.Ordinal) &&
                correlationId == context.CorrelationId;
        }
    }
}
