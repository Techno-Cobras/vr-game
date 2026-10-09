using System;
using System.Collections.Generic;
using VrGame.Data.Items;

namespace VrGame.Domain.Inventory
{
    public enum InventoryTransferRejection
    {
        None = 0,
        InvalidContext,
        InvalidQuantity,
        SameInventory,
        UnknownItem,
        InsufficientStock,
        ArithmeticOverflow,
        ReentrantMutation,
        CommandConflict
    }

    public readonly struct InventoryTransferContext
    {
        public InventoryTransferContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина перемещения обязательна.", nameof(reason));
            if (commandId == Guid.Empty)
                throw new ArgumentException("Command ID не может быть пустым.", nameof(commandId));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));

            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(Reason) &&
            CommandId != Guid.Empty &&
            CorrelationId != Guid.Empty;
    }

    public sealed class InventoryTransferResult
    {
        private InventoryTransferResult(
            InventoryTransferRejection rejection,
            InventoryChanged sourceChange,
            InventoryChanged destinationChange,
            bool sourceEventPublished,
            bool destinationEventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            SourceChange = sourceChange;
            DestinationChange = destinationChange;
            SourceEventPublished = sourceEventPublished;
            DestinationEventPublished = destinationEventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == InventoryTransferRejection.None;
        public InventoryTransferRejection Rejection { get; }
        public InventoryChanged SourceChange { get; }
        public InventoryChanged DestinationChange { get; }
        public bool SourceEventPublished { get; }
        public bool DestinationEventPublished { get; }
        public bool IsReplay { get; }

        internal static InventoryTransferResult Success(
            InventoryChanged sourceChange,
            InventoryChanged destinationChange,
            bool sourceEventPublished,
            bool destinationEventPublished) =>
            new InventoryTransferResult(
                InventoryTransferRejection.None,
                sourceChange,
                destinationChange,
                sourceEventPublished,
                destinationEventPublished,
                false);

        internal static InventoryTransferResult Reject(InventoryTransferRejection rejection) =>
            new InventoryTransferResult(rejection, null, null, false, false, false);

        internal InventoryTransferResult AsReplay() =>
            new InventoryTransferResult(
                Rejection,
                SourceChange,
                DestinationChange,
                SourceEventPublished,
                DestinationEventPublished,
                true);
    }

    public sealed partial class Inventory
    {
        private readonly Dictionary<Guid, CompletedTransfer> completedTransfers =
            new Dictionary<Guid, CompletedTransfer>();

        public InventoryTransferResult TransferTo(
            Inventory destination,
            ItemId itemId,
            int quantity,
            InventoryTransferContext context)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (isPublishingEvent || destination.isPublishingEvent)
                return InventoryTransferResult.Reject(InventoryTransferRejection.ReentrantMutation);
            if (!context.IsValid)
                return InventoryTransferResult.Reject(InventoryTransferRejection.InvalidContext);

            var payload = new TransferPayload(destination.Id, itemId, quantity, context);
            if (completedTransfers.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(payload))
                    return InventoryTransferResult.Reject(InventoryTransferRejection.CommandConflict);
                return completed.Result.AsReplay();
            }

            var result = ExecuteTransfer(destination, itemId, quantity, context);
            completedTransfers.Add(context.CommandId, new CompletedTransfer(payload, result));
            return result;
        }

        private InventoryTransferResult ExecuteTransfer(
            Inventory destination,
            ItemId itemId,
            int quantity,
            InventoryTransferContext context)
        {
            if (ReferenceEquals(this, destination))
                return InventoryTransferResult.Reject(InventoryTransferRejection.SameInventory);
            if (quantity <= 0)
                return InventoryTransferResult.Reject(InventoryTransferRejection.InvalidQuantity);
            if (!catalog.TryGet(itemId, out _) || !destination.catalog.TryGet(itemId, out _))
                return InventoryTransferResult.Reject(InventoryTransferRejection.UnknownItem);

            var sourceBefore = GetStoredQuantity(itemId);
            if (sourceBefore < quantity)
                return InventoryTransferResult.Reject(InventoryTransferRejection.InsufficientStock);
            var destinationBefore = destination.GetStoredQuantity(itemId);
            if (quantity > int.MaxValue - destinationBefore ||
                Version == long.MaxValue || destination.Version == long.MaxValue)
                return InventoryTransferResult.Reject(InventoryTransferRejection.ArithmeticOverflow);

            var sourceAfter = sourceBefore - quantity;
            var destinationAfter = destinationBefore + quantity;
            SetQuantity(itemId, sourceAfter);
            destination.SetQuantity(itemId, destinationAfter);
            Version++;
            destination.Version++;

            var changeContext = new InventoryChangeContext(
                context.Reason,
                context.CommandId,
                context.CorrelationId);
            var sourceChange = new InventoryChanged(
                Id,
                Version,
                InventoryMutationKind.TransferOut,
                changeContext,
                new[] { new InventoryQuantityChange(itemId, sourceBefore, sourceAfter) });
            var destinationChange = new InventoryChanged(
                destination.Id,
                destination.Version,
                InventoryMutationKind.TransferIn,
                changeContext,
                new[] { new InventoryQuantityChange(itemId, destinationBefore, destinationAfter) });

            isPublishingEvent = true;
            destination.isPublishingEvent = true;
            var sourcePublished = false;
            var destinationPublished = false;
            try
            {
                try
                {
                    sourcePublished = eventSink.TryPublish(sourceChange);
                }
                catch (Exception)
                {
                    // Оба inventory уже зафиксированы; событие остаётся в result для повторной доставки.
                }

                try
                {
                    destinationPublished = destination.eventSink.TryPublish(destinationChange);
                }
                catch (Exception)
                {
                    // Оба inventory уже зафиксированы; событие остаётся в result для повторной доставки.
                }
            }
            finally
            {
                destination.isPublishingEvent = false;
                isPublishingEvent = false;
            }

            return InventoryTransferResult.Success(
                sourceChange,
                destinationChange,
                sourcePublished,
                destinationPublished);
        }

        internal bool TryAcquireCoordinatedMutation()
        {
            if (isPublishingEvent)
                return false;
            isPublishingEvent = true;
            return true;
        }

        internal void ReleaseCoordinatedMutation() => isPublishingEvent = false;

        internal InventoryRejection TryPrepareCoordinatedRemove(
            ItemId itemId,
            int quantity,
            InventoryChangeContext context,
            out PreparedInventoryRemoval prepared)
        {
            prepared = default;
            if (!context.IsValid)
                return InventoryRejection.InvalidContext;
            if (quantity <= 0)
                return InventoryRejection.InvalidQuantity;
            if (!catalog.TryGet(itemId, out _))
                return InventoryRejection.UnknownItem;
            var before = GetStoredQuantity(itemId);
            if (before < quantity)
                return InventoryRejection.InsufficientStock;
            if (Version == long.MaxValue)
                return InventoryRejection.ArithmeticOverflow;

            prepared = new PreparedInventoryRemoval(itemId, before, before - quantity, Version, context);
            return InventoryRejection.None;
        }

        internal InventoryChanged CommitPreparedRemove(PreparedInventoryRemoval prepared)
        {
            if (Version != prepared.ExpectedVersion || GetStoredQuantity(prepared.ItemId) != prepared.Before)
                throw new InvalidOperationException("Подготовленное списание Inventory устарело.");

            SetQuantity(prepared.ItemId, prepared.After);
            Version++;
            return new InventoryChanged(
                Id,
                Version,
                InventoryMutationKind.Remove,
                prepared.Context,
                new[] { new InventoryQuantityChange(prepared.ItemId, prepared.Before, prepared.After) });
        }

        internal bool PublishPrepared(InventoryChanged change)
        {
            try
            {
                return eventSink.TryPublish(change);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal readonly struct PreparedInventoryRemoval
        {
            public PreparedInventoryRemoval(
                ItemId itemId,
                int before,
                int after,
                long expectedVersion,
                InventoryChangeContext context)
            {
                ItemId = itemId;
                Before = before;
                After = after;
                ExpectedVersion = expectedVersion;
                Context = context;
            }

            public ItemId ItemId { get; }
            public int Before { get; }
            public int After { get; }
            public long ExpectedVersion { get; }
            public InventoryChangeContext Context { get; }
        }

        private readonly struct TransferPayload : IEquatable<TransferPayload>
        {
            public TransferPayload(
                InventoryId destinationId,
                ItemId itemId,
                int quantity,
                InventoryTransferContext context)
            {
                DestinationId = destinationId;
                ItemId = itemId;
                Quantity = quantity;
                Reason = context.Reason;
                CorrelationId = context.CorrelationId;
            }

            private InventoryId DestinationId { get; }
            private ItemId ItemId { get; }
            private int Quantity { get; }
            private string Reason { get; }
            private Guid CorrelationId { get; }

            public bool Equals(TransferPayload other) =>
                DestinationId.Equals(other.DestinationId) &&
                ItemId.Equals(other.ItemId) &&
                Quantity == other.Quantity &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
            public override bool Equals(object obj) => obj is TransferPayload other && Equals(other);
            public override int GetHashCode() =>
                (((DestinationId.GetHashCode() * 397) ^ ItemId.GetHashCode()) * 397 ^ Quantity) * 397 ^
                CorrelationId.GetHashCode();
        }

        private sealed class CompletedTransfer
        {
            private readonly TransferPayload payload;

            public CompletedTransfer(TransferPayload payload, InventoryTransferResult result)
            {
                this.payload = payload;
                Result = result;
            }

            public InventoryTransferResult Result { get; }
            public bool Matches(TransferPayload candidate) => payload.Equals(candidate);
        }
    }
}
