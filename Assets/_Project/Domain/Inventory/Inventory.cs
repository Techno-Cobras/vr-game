using System;
using System.Collections.Generic;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Domain.Inventory
{
    public sealed class Inventory
    {
        private readonly ItemCatalog catalog;
        private readonly IInventoryEventSink eventSink;
        private readonly Dictionary<ItemId, int> quantities = new Dictionary<ItemId, int>();
        private bool isPublishingEvent;

        public Inventory(InventoryId id, ItemCatalog catalog, IInventoryEventSink eventSink)
        {
            if (id.Value == Guid.Empty)
                throw new ArgumentException("Inventory ID не может быть пустым.", nameof(id));

            Id = id;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public InventoryId Id { get; }

        public long Version { get; private set; }

        public bool TryGetQuantity(ItemId itemId, out int quantity)
        {
            if (!catalog.TryGet(itemId, out _))
            {
                quantity = 0;
                return false;
            }

            quantity = GetStoredQuantity(itemId);
            return true;
        }

        public int GetQuantity(ItemId itemId)
        {
            if (!TryGetQuantity(itemId, out var quantity))
                throw new KeyNotFoundException($"Item ID '{itemId}' отсутствует в каталоге.");

            return quantity;
        }

        public InventorySnapshot GetSnapshot() => new InventorySnapshot(
            Id,
            Version,
            quantities
                .OrderBy(pair => pair.Key)
                .Select(pair => new InventoryItemQuantity(pair.Key, pair.Value)));

        public InventoryMutationResult Add(ItemId itemId, int quantity, InventoryChangeContext context)
        {
            var validation = ValidateSingle(itemId, quantity, context);
            if (validation != null)
                return validation;

            var before = GetStoredQuantity(itemId);
            if (quantity > int.MaxValue - before || Version == long.MaxValue)
                return InventoryMutationResult.Reject(
                    InventoryRejection.ArithmeticOverflow,
                    itemId,
                    quantity,
                    before);

            var after = before + quantity;
            quantities[itemId] = after;
            return Commit(
                InventoryMutationKind.Add,
                context,
                new[] { new InventoryQuantityChange(itemId, before, after) });
        }

        public InventoryMutationResult Remove(ItemId itemId, int quantity, InventoryChangeContext context)
        {
            var validation = ValidateSingle(itemId, quantity, context);
            if (validation != null)
                return validation;

            var before = GetStoredQuantity(itemId);
            if (before < quantity)
                return InventoryMutationResult.Reject(
                    InventoryRejection.InsufficientStock,
                    itemId,
                    quantity,
                    before);
            if (Version == long.MaxValue)
                return InventoryMutationResult.Reject(
                    InventoryRejection.ArithmeticOverflow,
                    itemId,
                    quantity,
                    before);

            var after = before - quantity;
            SetQuantity(itemId, after);
            return Commit(
                InventoryMutationKind.Remove,
                context,
                new[] { new InventoryQuantityChange(itemId, before, after) });
        }

        public InventoryMutationResult ConsumeBatch(
            IEnumerable<InventoryItemQuantity> requirements,
            InventoryChangeContext context)
        {
            if (isPublishingEvent)
                return InventoryMutationResult.Reject(InventoryRejection.ReentrantMutation);
            if (!context.IsValid)
                return InventoryMutationResult.Reject(InventoryRejection.InvalidContext);
            if (requirements == null)
                return InventoryMutationResult.Reject(InventoryRejection.InvalidBatch);

            var normalized = new Dictionary<ItemId, int>();
            var hasRequirements = false;
            foreach (var requirement in requirements)
            {
                hasRequirements = true;
                if (requirement.Quantity <= 0)
                    return InventoryMutationResult.Reject(
                        InventoryRejection.InvalidQuantity,
                        requirement.ItemId,
                        requirement.Quantity);
                if (!catalog.TryGet(requirement.ItemId, out _))
                    return InventoryMutationResult.Reject(
                        InventoryRejection.UnknownItem,
                        requirement.ItemId,
                        requirement.Quantity);

                var accumulated = normalized.TryGetValue(requirement.ItemId, out var existing) ? existing : 0;
                if (requirement.Quantity > int.MaxValue - accumulated)
                    return InventoryMutationResult.Reject(
                        InventoryRejection.ArithmeticOverflow,
                        requirement.ItemId,
                        requirement.Quantity,
                        accumulated);

                normalized[requirement.ItemId] = accumulated + requirement.Quantity;
            }

            if (!hasRequirements)
                return InventoryMutationResult.Reject(InventoryRejection.InvalidBatch);

            var ordered = normalized.OrderBy(pair => pair.Key).ToArray();
            foreach (var requirement in ordered)
            {
                var available = GetStoredQuantity(requirement.Key);
                if (available < requirement.Value)
                    return InventoryMutationResult.Reject(
                        InventoryRejection.InsufficientStock,
                        requirement.Key,
                        requirement.Value,
                        available);
            }

            if (Version == long.MaxValue)
                return InventoryMutationResult.Reject(InventoryRejection.ArithmeticOverflow);

            var changes = new InventoryQuantityChange[ordered.Length];
            for (var index = 0; index < ordered.Length; index++)
            {
                var requirement = ordered[index];
                var before = GetStoredQuantity(requirement.Key);
                var after = before - requirement.Value;
                changes[index] = new InventoryQuantityChange(requirement.Key, before, after);
            }

            foreach (var change in changes)
                SetQuantity(change.ItemId, change.After);

            return Commit(InventoryMutationKind.ConsumeBatch, context, changes);
        }

        private InventoryMutationResult ValidateSingle(
            ItemId itemId,
            int quantity,
            InventoryChangeContext context)
        {
            if (isPublishingEvent)
                return InventoryMutationResult.Reject(
                    InventoryRejection.ReentrantMutation,
                    itemId,
                    quantity);
            if (!context.IsValid)
                return InventoryMutationResult.Reject(InventoryRejection.InvalidContext, itemId, quantity);
            if (quantity <= 0)
                return InventoryMutationResult.Reject(InventoryRejection.InvalidQuantity, itemId, quantity);
            if (!catalog.TryGet(itemId, out _))
                return InventoryMutationResult.Reject(InventoryRejection.UnknownItem, itemId, quantity);

            return null;
        }

        private int GetStoredQuantity(ItemId itemId) => quantities.TryGetValue(itemId, out var quantity) ? quantity : 0;

        private void SetQuantity(ItemId itemId, int quantity)
        {
            if (quantity == 0)
                quantities.Remove(itemId);
            else
                quantities[itemId] = quantity;
        }

        private InventoryMutationResult Commit(
            InventoryMutationKind kind,
            InventoryChangeContext context,
            IReadOnlyList<InventoryQuantityChange> changes)
        {
            Version++;
            var inventoryChanged = new InventoryChanged(Id, Version, kind, context, changes);
            var eventPublished = false;
            isPublishingEvent = true;
            try
            {
                eventPublished = eventSink.TryPublish(inventoryChanged);
            }
            catch (Exception)
            {
                // Mutation уже зафиксирована. Возвращаем событие, чтобы вызывающий код мог
                // повторить публикацию без повторного выполнения inventory command.
            }
            finally
            {
                isPublishingEvent = false;
            }

            return InventoryMutationResult.Success(inventoryChanged, eventPublished);
        }
    }
}
