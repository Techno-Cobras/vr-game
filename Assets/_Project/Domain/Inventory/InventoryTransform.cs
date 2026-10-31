using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Domain.Inventory
{
    internal enum InventoryTransformFailureTarget
    {
        None = 0,
        Input,
        Output
    }

    internal readonly struct InventoryTransformMissing
    {
        public InventoryTransformMissing(ItemId itemId, int required, int available)
        {
            ItemId = itemId;
            Required = required;
            Available = available;
        }

        public ItemId ItemId { get; }
        public int Required { get; }
        public int Available { get; }
    }

    internal sealed class InventoryTransformPreparationResult
    {
        private InventoryTransformPreparationResult(
            InventoryRejection rejection,
            InventoryTransformFailureTarget failureTarget,
            IReadOnlyList<InventoryTransformMissing> missing,
            PreparedInventoryTransform prepared)
        {
            Rejection = rejection;
            FailureTarget = failureTarget;
            Missing = missing;
            Prepared = prepared;
        }

        public bool Succeeded => Rejection == InventoryRejection.None;
        public InventoryRejection Rejection { get; }
        public InventoryTransformFailureTarget FailureTarget { get; }
        public IReadOnlyList<InventoryTransformMissing> Missing { get; }
        public PreparedInventoryTransform Prepared { get; }

        public static InventoryTransformPreparationResult Success(PreparedInventoryTransform prepared) =>
            new InventoryTransformPreparationResult(
                InventoryRejection.None,
                InventoryTransformFailureTarget.None,
                Array.Empty<InventoryTransformMissing>(),
                prepared);

        public static InventoryTransformPreparationResult Reject(
            InventoryRejection rejection,
            InventoryTransformFailureTarget failureTarget = InventoryTransformFailureTarget.None,
            IEnumerable<InventoryTransformMissing> missing = null) =>
            new InventoryTransformPreparationResult(
                rejection,
                failureTarget,
                new ReadOnlyCollection<InventoryTransformMissing>((missing ?? Array.Empty<InventoryTransformMissing>()).ToList()),
                default);
    }

    internal readonly struct PreparedInventoryTransform
    {
        public PreparedInventoryTransform(
            long expectedVersion,
            InventoryChangeContext context,
            IReadOnlyList<InventoryQuantityChange> changes)
        {
            ExpectedVersion = expectedVersion;
            Context = context;
            Changes = changes;
        }

        public long ExpectedVersion { get; }
        public InventoryChangeContext Context { get; }
        public IReadOnlyList<InventoryQuantityChange> Changes { get; }
    }

    public sealed partial class Inventory
    {
        internal InventoryTransformPreparationResult TryPrepareCoordinatedTransform(
            IEnumerable<InventoryItemQuantity> inputs,
            ItemId outputItemId,
            int outputQuantity,
            InventoryChangeContext context)
        {
            if (!context.IsValid)
                return InventoryTransformPreparationResult.Reject(InventoryRejection.InvalidContext);
            if (inputs == null)
                return InventoryTransformPreparationResult.Reject(
                    InventoryRejection.InvalidBatch,
                    InventoryTransformFailureTarget.Input);
            if (outputQuantity <= 0)
                return InventoryTransformPreparationResult.Reject(
                    InventoryRejection.InvalidQuantity,
                    InventoryTransformFailureTarget.Output);

            var requirements = new Dictionary<ItemId, int>();
            var hasInputs = false;
            foreach (var input in inputs)
            {
                hasInputs = true;
                if (input.Quantity <= 0)
                    return InventoryTransformPreparationResult.Reject(
                        InventoryRejection.InvalidQuantity,
                        InventoryTransformFailureTarget.Input);
                if (!catalog.TryGet(input.ItemId, out _))
                    return InventoryTransformPreparationResult.Reject(
                        InventoryRejection.UnknownItem,
                        InventoryTransformFailureTarget.Input);
                var accumulated = requirements.TryGetValue(input.ItemId, out var existing) ? existing : 0;
                if (input.Quantity > int.MaxValue - accumulated)
                    return InventoryTransformPreparationResult.Reject(
                        InventoryRejection.ArithmeticOverflow,
                        InventoryTransformFailureTarget.Input);
                requirements[input.ItemId] = accumulated + input.Quantity;
            }

            if (!hasInputs)
                return InventoryTransformPreparationResult.Reject(
                    InventoryRejection.InvalidBatch,
                    InventoryTransformFailureTarget.Input);
            if (!catalog.TryGet(outputItemId, out _))
                return InventoryTransformPreparationResult.Reject(
                    InventoryRejection.UnknownItem,
                    InventoryTransformFailureTarget.Output);

            var missing = requirements
                .OrderBy(pair => pair.Key)
                .Where(pair => GetStoredQuantity(pair.Key) < pair.Value)
                .Select(pair => new InventoryTransformMissing(
                    pair.Key,
                    pair.Value,
                    GetStoredQuantity(pair.Key)))
                .ToArray();
            if (missing.Length > 0)
                return InventoryTransformPreparationResult.Reject(
                    InventoryRejection.InsufficientStock,
                    InventoryTransformFailureTarget.Input,
                    missing);
            if (Version == long.MaxValue)
                return InventoryTransformPreparationResult.Reject(InventoryRejection.ArithmeticOverflow);

            var affected = new SortedSet<ItemId>(requirements.Keys) { outputItemId };
            var changes = new List<InventoryQuantityChange>(affected.Count);
            foreach (var itemId in affected)
            {
                var before = GetStoredQuantity(itemId);
                var consumed = requirements.TryGetValue(itemId, out var required) ? required : 0;
                var afterConsumption = before - consumed;
                var after = afterConsumption;
                if (itemId == outputItemId)
                {
                    if (outputQuantity > int.MaxValue - afterConsumption)
                        return InventoryTransformPreparationResult.Reject(
                            InventoryRejection.ArithmeticOverflow,
                            InventoryTransformFailureTarget.Output);
                    after += outputQuantity;
                }
                changes.Add(new InventoryQuantityChange(itemId, before, after));
            }

            return InventoryTransformPreparationResult.Success(
                new PreparedInventoryTransform(
                    Version,
                    context,
                    new ReadOnlyCollection<InventoryQuantityChange>(changes)));
        }

        internal InventoryChanged CommitPreparedTransform(PreparedInventoryTransform prepared)
        {
            if (Version != prepared.ExpectedVersion || prepared.Changes == null)
                throw new InvalidOperationException("Подготовленная Inventory transform устарела.");
            foreach (var change in prepared.Changes)
                if (GetStoredQuantity(change.ItemId) != change.Before)
                    throw new InvalidOperationException("Подготовленная Inventory transform устарела.");

            foreach (var change in prepared.Changes)
                SetQuantity(change.ItemId, change.After);
            Version++;
            return new InventoryChanged(
                Id,
                Version,
                InventoryMutationKind.Transform,
                prepared.Context,
                prepared.Changes);
        }
    }
}
