using System;
using System.Collections.Generic;
using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public sealed partial class PlantingSlot
    {
        private readonly PlantCatalog catalog;
        private readonly IPlantingSlotEventSink eventSink;
        private readonly IPlantGrowthRandomSource randomSource;
        private readonly Dictionary<CommandKey, CompletedCommand> completedCommands =
            new Dictionary<CommandKey, CompletedCommand>();
        private PlantingSlotState state;
        private PlantTypeId? plantTypeId;
        private float elapsedGrowthSeconds;
        private float normalizedProgress;
        private PlantGrowthModifier growthModifier;
        private float? waterRequestThreshold;
        private bool waterRequestOccurred;
        private bool waterRequestCompleted;
        private bool isPublishingEvent;

        public PlantingSlot(PlantingSlotId id, PlantCatalog catalog, IPlantingSlotEventSink eventSink)
            : this(id, catalog, eventSink, MidpointRandomSource.Instance)
        {
        }

        public PlantingSlot(
            PlantingSlotId id,
            PlantCatalog catalog,
            IPlantingSlotEventSink eventSink,
            IPlantGrowthRandomSource randomSource)
        {
            if (id.Value == Guid.Empty)
                throw new ArgumentException("Planting slot ID не может быть пустым.", nameof(id));

            Id = id;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
            state = PlantingSlotState.Empty;
            growthModifier = PlantGrowthModifier.None;
        }

        public PlantingSlotId Id { get; }
        public long Version { get; private set; }

        public PlantingSlotSnapshot GetSnapshot() =>
            new PlantingSlotSnapshot(
                Id,
                Version,
                state,
                plantTypeId,
                elapsedGrowthSeconds,
                normalizedProgress,
                growthModifier,
                waterRequestThreshold,
                waterRequestOccurred,
                waterRequestCompleted);

        public PlantingSlotMutationResult Plant(PlantTypeId typeId, PlantingSlotChangeContext context) =>
            Execute(PlantingSlotMutationKind.Plant, CommandPayload.ForPlant(typeId), context);

        public PlantingSlotMutationResult AdvanceProgress(
            float absoluteTarget,
            long expectedVersion,
            PlantingSlotChangeContext context)
        {
            var elapsedTarget = 0f;
            if (plantTypeId.HasValue && catalog.TryGet(plantTypeId.Value, out var definition))
                elapsedTarget = absoluteTarget * definition.GrowthDuration.Seconds;

            return AdvanceGrowth(elapsedTarget, expectedVersion, context);
        }

        public PlantingSlotMutationResult AdvanceGrowth(
            float absoluteElapsedSeconds,
            long expectedVersion,
            PlantingSlotChangeContext context)
        {
            var normalizedTarget = 0f;
            if (plantTypeId.HasValue && catalog.TryGet(plantTypeId.Value, out var definition))
                normalizedTarget = absoluteElapsedSeconds / definition.GrowthDuration.Seconds;

            return Execute(
                PlantingSlotMutationKind.AdvanceProgress,
                CommandPayload.ForGrowth(absoluteElapsedSeconds, normalizedTarget),
                context,
                cacheResult: false,
                expectedVersion: expectedVersion);
        }

        [Obsolete("Запрос воды создаётся автоматически при достижении порога роста.")]
        public PlantingSlotMutationResult RequestWater(PlantingSlotChangeContext context)
        {
            if (isPublishingEvent)
                return PlantingSlotMutationResult.Reject(PlantingSlotRejection.ReentrantMutation);
            if (!context.IsValid)
                return PlantingSlotMutationResult.Reject(PlantingSlotRejection.InvalidContext);

            return PlantingSlotMutationResult.Reject(
                state == PlantingSlotState.NeedsWater
                    ? PlantingSlotRejection.AlreadyNeedsWater
                    : PlantingSlotRejection.AutomaticWaterRequestOnly);
        }

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

            float? selectedWaterThreshold = null;
            if (kind == PlantingSlotMutationKind.Plant)
            {
                var definition = catalog.GetRequired(payload.PlantTypeId);
                selectedWaterThreshold = definition.WaterRequestRange.Select(randomSource.NextUnit());
            }

            var before = GetSnapshot();
            Apply(kind, payload, selectedWaterThreshold);
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
                        payload.Progress < 0f || payload.Progress > 1f ||
                        float.IsNaN(payload.ElapsedSeconds) || float.IsInfinity(payload.ElapsedSeconds) ||
                        payload.ElapsedSeconds < 0f)
                        return PlantingSlotRejection.InvalidProgress;
                    if (state != PlantingSlotState.Growing)
                        return PlantingSlotRejection.NotGrowing;
                    return payload.Progress <= normalizedProgress || payload.ElapsedSeconds <= elapsedGrowthSeconds
                        ? PlantingSlotRejection.ProgressRegressionOrNoProgress
                        : PlantingSlotRejection.None;

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

        private void Apply(
            PlantingSlotMutationKind kind,
            CommandPayload payload,
            float? selectedWaterThreshold = null)
        {
            switch (kind)
            {
                case PlantingSlotMutationKind.Plant:
                    state = PlantingSlotState.Growing;
                    plantTypeId = payload.PlantTypeId;
                    elapsedGrowthSeconds = 0f;
                    normalizedProgress = 0f;
                    growthModifier = PlantGrowthModifier.None;
                    waterRequestThreshold = selectedWaterThreshold ??
                        throw new InvalidOperationException("Для нового цикла растения не выбран порог воды.");
                    waterRequestOccurred = false;
                    waterRequestCompleted = false;
                    break;
                case PlantingSlotMutationKind.AdvanceProgress:
                    if (!waterRequestOccurred &&
                        waterRequestThreshold.HasValue &&
                        payload.Progress >= waterRequestThreshold.Value)
                    {
                        var definition = catalog.GetRequired(plantTypeId.Value);
                        normalizedProgress = waterRequestThreshold.Value;
                        elapsedGrowthSeconds = definition.GrowthDuration.Seconds * normalizedProgress;
                        waterRequestOccurred = true;
                        state = PlantingSlotState.NeedsWater;
                    }
                    else
                    {
                        elapsedGrowthSeconds = payload.ElapsedSeconds;
                        normalizedProgress = payload.Progress;
                    }
                    if (normalizedProgress == 1f)
                        state = PlantingSlotState.ReadyToHarvest;
                    break;
                case PlantingSlotMutationKind.Water:
                    state = PlantingSlotState.Growing;
                    waterRequestCompleted = true;
                    break;
                case PlantingSlotMutationKind.ApplyFertilizer:
                    growthModifier = payload.Modifier;
                    break;
                case PlantingSlotMutationKind.Harvest:
                    state = PlantingSlotState.Empty;
                    plantTypeId = null;
                    elapsedGrowthSeconds = 0f;
                    normalizedProgress = 0f;
                    growthModifier = PlantGrowthModifier.None;
                    waterRequestThreshold = null;
                    waterRequestOccurred = false;
                    waterRequestCompleted = false;
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
            private CommandPayload(
                PlantTypeId plantTypeId,
                float elapsedSeconds,
                float progress,
                PlantGrowthModifier modifier)
            {
                PlantTypeId = plantTypeId;
                ElapsedSeconds = elapsedSeconds;
                Progress = progress;
                Modifier = modifier;
            }

            public static CommandPayload None => default;
            public PlantTypeId PlantTypeId { get; }
            public float ElapsedSeconds { get; }
            public float Progress { get; }
            public PlantGrowthModifier Modifier { get; }

            public static CommandPayload ForPlant(PlantTypeId id) => new CommandPayload(id, 0f, 0f, default);
            public static CommandPayload ForGrowth(float elapsedSeconds, float progress) =>
                new CommandPayload(default, elapsedSeconds, progress, default);
            public static CommandPayload ForModifier(PlantGrowthModifier modifier) =>
                new CommandPayload(default, 0f, 0f, modifier);

            public bool Equals(CommandPayload other) =>
                PlantTypeId.Equals(other.PlantTypeId) &&
                ElapsedSeconds.Equals(other.ElapsedSeconds) &&
                Progress.Equals(other.Progress) &&
                Modifier.Equals(other.Modifier);
            public override bool Equals(object obj) => obj is CommandPayload other && Equals(other);
            public override int GetHashCode() =>
                (((PlantTypeId.GetHashCode() * 397) ^ ElapsedSeconds.GetHashCode()) * 397 ^
                 Progress.GetHashCode()) * 397 ^ Modifier.GetHashCode();
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

        private sealed class MidpointRandomSource : IPlantGrowthRandomSource
        {
            public static readonly MidpointRandomSource Instance = new MidpointRandomSource();
            public float NextUnit() => .5f;
        }
    }
}
